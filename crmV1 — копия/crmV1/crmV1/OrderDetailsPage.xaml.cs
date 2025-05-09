using System;
using System.Collections.Generic;
using System.Collections.ObjectModel; // Для ObservableCollection
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Globalization; // Для CultureInfo та NumberStyles
using System.Net.NetworkInformation;
using System.Linq; // Для LINQ запитів
using System.ComponentModel; // Для INotifyPropertyChanged
using System.Diagnostics; // Для Stopwatch або інших інструментів відладки

namespace crmV1
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class OrderDetailsPage : ContentPage, INotifyPropertyChanged // Додано INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged; // Реалізація INotifyPropertyChanged

        private bool IsNetworkAvailable => NetworkInterface.GetIsNetworkAvailable();

        private string ServerIP = AppConfig.ServerIP; // Припускається, що AppConfig - це клас з IP сервісом
        private const int ServerPort = 8888;

        private TcpClient _client;
        private NetworkStream _stream;
        private bool _isConnected = false;
        private readonly SemaphoreSlim _networkSemaphore = new SemaphoreSlim(1, 1); // Семафор для контролю доступу до мережі

        private int _sessionId;
        private int _managerId; // Потрібен для надсилання на сервер при діях
        private int _clubId;    // Потрібен для надсилання на сервер при діях
        private int _clientId; // Зберігаємо ClientId, якщо сервер його повертає

        // Модель даних для деталей замовлення
        public class OrderDetailsData
        {
            public int SessionId { get; set; }
            public int ClientId { get; set; }
            public string ClientName { get; set; }
            public string PhoneNumber { get; set; }
            public DateTime SessionDate { get; set; }
            public TimeSpan StartTime { get; set; }
            public TimeSpan EndTime { get; set; }
            public int NumPeople { get; set; }
            public string SessionType { get; set; } // Зберігаємо сирий тип
            public string Notes { get; set; }
            public string PaymentStatus { get; set; }
            public int? DiscountId { get; set; } // Знижка може бути відсутня
            public decimal CalculatePrice { get; set; } // Розрахована ціна (з сервера)
            public decimal FinalPrice { get; set; }   // Фінальна ціна (може бути від сервера або введена вручну)

            // Додаткові поля, які можуть бути потрібні для UI/логіки
            public string PaymentMethod { get; set; } // Спосіб оплати для запису
        }

        // Моделі для Pickers
        // SessionTypeOption вже не потрібен для RadioButton, але може бути потрібен, якщо сервер повертає ціни разом з типами
        public class SessionTypeOption // Залишаємо на випадок, якщо потрібні ціни за годину
        {
            public string Type { get; set; }
            public decimal PricePerHour { get; set; }
        }

        public class DiscountOption
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public decimal Percentage { get; set; }

            public override string ToString() => Name;
        }

        public class PaymentMethodOption
        {
            public string Method { get; set; }
            public override string ToString() => Method;
        }


        // Публічні властивості для зберігання даних та списків для UI
        public OrderDetailsData OrderDetails { get; private set; }

        // Колекції для Picker'ів. AvailableSessionTypes тепер використовується тільки для отримання даних, якщо це потрібно.
        // ObservableCollection для AvailableSessionTypes все ще потрібна, якщо її заповнює LoadOrderDetailsAsync,
        // але вона не прив'язана до UI через ItemsSource для RadioButtons.
        public ObservableCollection<SessionTypeOption> AvailableSessionTypes { get; set; } // Залишаємо, якщо дані про типи все одно приходять з сервера
        public ObservableCollection<DiscountOption> AvailableDiscounts { get; set; }
        public ObservableCollection<PaymentMethodOption> AvailablePaymentMethods { get; set; }

        // Властивості для SelectedItem у Pickers.
        // Для RadioButton ми будемо відстежувати обраний тип через SelectedSessionType (як string).
        private string _selectedSessionType; // Змінено на string
        public string SelectedSessionType // Змінено на string
        {
            get => _selectedSessionType;
            set
            {
                if (_selectedSessionType != value)
                {
                    _selectedSessionType = value;
                    OnPropertyChanged(); // Повідомляємо про зміну SelectedSessionType
                    Console.WriteLine($"[OrderDetailsPage] Selected Session Type set to: {value}");
                    // Можна оновити UI стан тут, якщо потрібно
                    // Device.BeginInvokeOnMainThread(() => SetUIState(PageState.Loaded)); // За потреби
                }
            }
        }

        private DiscountOption _selectedDiscount;
        public DiscountOption SelectedDiscount
        {
            get => _selectedDiscount;
            set
            {
                if (_selectedDiscount != value)
                {
                    _selectedDiscount = value;
                    OnPropertyChanged(); // Повідомляємо про зміну
                }
            }
        }

        private PaymentMethodOption _selectedPaymentMethod;
        public PaymentMethodOption SelectedPaymentMethod
        {
            get => _selectedPaymentMethod;
            set
            {
                if (_selectedPaymentMethod != value)
                {
                    _selectedPaymentMethod = value;
                    OnPropertyChanged(); // Повідомляємо про зміну
                }
            }
        }


        // Конструктор
        public OrderDetailsPage(int sessionId, int managerId, int clubId)
        {
            InitializeComponent(); // Це викликає завантаження XAML та створення елементів UI
            NavigationPage.SetHasNavigationBar(this, false);
            _sessionId = sessionId;
            _managerId = managerId;
            _clubId = clubId;

            Title = $"Замовлення #{_sessionId}"; // Встановлюємо заголовок сторінки

            // Ініціалізація колекцій для UI елементів.
            // AvailableSessionTypes все ще ініціалізується, якщо дані про типи приходять з сервера,
            // але не для прив'язки до RadioButton.
            AvailableSessionTypes = new ObservableCollection<SessionTypeOption>();
            AvailableDiscounts = new ObservableCollection<DiscountOption>();
            AvailablePaymentMethods = new ObservableCollection<PaymentMethodOption>
            {
                new PaymentMethodOption { Method = "Cash" },
                new PaymentMethodOption { Method = "Card" },
                new PaymentMethodOption { Method = "Online" }
            };

            // Встановлюємо BindingContext, щоб працювали прив'язки в XAML (Picker SelectedItem)
            BindingContext = this;

            // Встановлюємо початковий стан UI
            SetUIState(PageState.Loading);
        }

        // Викликається, коли сторінка з'являється на екрані
        protected override void OnAppearing()
        {
            base.OnAppearing();
            Console.WriteLine("[OrderDetailsPage.OnAppappearing] Сторінка з'явилася.");

            // Запускаємо завантаження даних, якщо ще не завантажено або якщо потрібно оновити
            if (OrderDetails == null || OrderDetails.SessionId == 0)
            {
                Task.Run(async () => await LoadOrderDetailsAsync());
            }
        }

        // Enum для станів UI
        private enum PageState { Loading, Loaded, Error, Saving, Deleting, Paying, Calculating }

        // Метод для управління станом UI
        // Всі зміни UI елементів мають відбуватися в головному потоці
        private void SetUIState(PageState state, string message = null)
        {
            Device.BeginInvokeOnMainThread(() =>
            {
                bool isBusy = state == PageState.Loading || state == PageState.Saving || state == PageState.Deleting || state == PageState.Paying || state == PageState.Calculating;
                loadingIndicator.IsRunning = isBusy;
                loadingIndicator.IsVisible = isBusy;

                errorMessageLabel.IsVisible = state == PageState.Error;
                errorMessageLabel.Text = message;

                // Визначаємо, чи поля мають бути редаговані
                bool isEditable = state == PageState.Loaded && (OrderDetails?.PaymentStatus != "Paid" && OrderDetails?.PaymentStatus != "Canceled");

                // Вмикаємо/вимикаємо поля введення та Pickers
                // sessionTypeRadioButtonsContainer.IsEnabled = isEditable; // Керуємо контейнером радіо кнопок
                // Або перебираємо окремі кнопки:
                foreach (var child in sessionTypeRadioButtonsContainer.Children)
                {
                    if (child is RadioButton rb)
                    {
                        rb.IsEnabled = isEditable;
                    }
                }


                clientNameEntry.IsEnabled = isEditable;
                phoneNumberEntry.IsEnabled = isEditable;
                sessionDatePicker.IsEnabled = isEditable;
                startTimePicker.IsEnabled = isEditable;
                endTimePicker.IsEnabled = isEditable;
                numPeopleEntry.IsEnabled = isEditable;
                //discountPicker.IsEnabled = isEditable && AvailableDiscounts.Any(); // Picker знижки
                notesEditor.IsEnabled = isEditable;
                finalPriceEntry.IsEnabled = isEditable; // Фінальна ціна редагується, якщо замовлення редаговане

                // Доступність кнопки "Записати оплату" та Picker способу оплати залежить від статусу та фінальної ціни
                // Перевіряємо, чи фінальна ціна в Entry є коректним числом > 0
                bool hasValidFinalPriceForPayment = decimal.TryParse(finalPriceEntry.Text, NumberStyles.Any, CultureInfo.CurrentCulture, out decimal finalPriceForPayment) && finalPriceForPayment > 0;

                bool canRecordPayment = state == PageState.Loaded && OrderDetails?.PaymentStatus == "Pending" && hasValidFinalPriceForPayment && SelectedPaymentMethod != null;

                paymentMethodPicker.IsEnabled = state == PageState.Loaded && OrderDetails?.PaymentStatus == "Pending" && AvailablePaymentMethods.Any() && hasValidFinalPriceForPayment; // Можна обрати спосіб, якщо "Pending", є опції та є фінальна ціна > 0


                // Вмикаємо/вимикаємо кнопки дій
                saveButton.IsEnabled = isEditable; // Збереження доступне, якщо поля редаговані
                deleteButton.IsEnabled = state == PageState.Loaded && OrderDetails?.PaymentStatus != "Paid" && OrderDetails?.PaymentStatus != "Canceled"; // Видалити можна, якщо не оплачено і не відмінено
                recordPaymentButton.IsEnabled = isEditable; // Оплатити можна, якщо відповідає умовам canRecordPayment


                // Керування видимістю кнопок та полів залежно від статусу замовлення
                if (state == PageState.Loaded && (OrderDetails?.PaymentStatus == "Paid" || OrderDetails?.PaymentStatus == "Canceled"))
                {
                    recordPaymentButton.IsVisible = false;
                    paymentMethodPicker.IsVisible = false; // Приховуємо вибір способу оплати, якщо не можна оплатити
                                                           // calculatePriceButton.IsVisible = false; // Можливо, приховати розрахунок після оплати/відміни?
                                                           // saveButton.IsVisible = false; // Можливо, приховати збереження?
                                                           // deleteButton залишається видимою, але неактивною, якщо статус "Canceled"
                }
                else
                {
                    recordPaymentButton.IsVisible = true;
                    paymentMethodPicker.IsVisible = true; // Показуємо вибір способу оплати
                                                          // calculatePriceButton.IsVisible = true;
                                                          // saveButton.IsVisible = true;
                                                          // deleteButton залишається видимою
                }

                // Якщо статус "Pending", але фінальна ціна 0 або спосіб оплати не обрано, кнопка оплати неактивна, але видима.
                // Якщо статус інший, кнопка оплати прихована.

                // Щоб доступність кнопки оплати оновлювалася при зміні finalPriceEntry,
                // можна підписатися на події TextChanged finalPriceEntry і SelectedItemChanged paymentMethodPicker
                // та викликати SetUIState(PageState.Loaded) або інший метод оновлення стану.
                // Або використовувати ICommand з CanExecute в MVVM.
            });
        }

        // --- Мережеві методи ---
        // Ці методи залишаються практично без змін
        private async Task ConnectToServerAsync()
        {
            if (!IsNetworkAvailable)
            {
                throw new Exception("Мережа недоступна. Перевірте підключення.");
            }

            await _networkSemaphore.WaitAsync();
            try
            {
                if (_client != null && _client.Connected && _stream != null && _stream.CanRead && _stream.CanWrite)
                {
                    _isConnected = true;
                    return;
                }

                Disconnect();

                Console.WriteLine($"[OrderDetailsPage.ConnectToServerAsync] Спроба підключення до {ServerIP}:{ServerPort}...");
                _client = new TcpClient
                {
                    ReceiveTimeout = 15000,
                    SendTimeout = 15000
                };

                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                {
                    var connectTask = _client.ConnectAsync(ServerIP, ServerPort);
                    await Task.WhenAny(connectTask, Task.Delay(Timeout.Infinite, cts.Token));

                    if (cts.IsCancellationRequested && !connectTask.IsCompleted)
                    {
                        throw new TimeoutException($"Таймаут ({cts.Token.WaitHandle.GetHashCode()} сек) підключення до сервера {ServerIP}:{ServerPort}.");
                    }
                    if (connectTask.IsFaulted)
                    {
                        throw connectTask.Exception.InnerException ?? connectTask.Exception;
                    }
                }

                _stream = _client.GetStream();
                _isConnected = true;
                Console.WriteLine("[OrderDetailsPage.ConnectToServerAsync] З'єднання з сервером встановлено.");
            }
            catch (TimeoutException tex)
            {
                Console.WriteLine($"[OrderDetailsPage.ConnectToServerAsync] Помилка таймауту підключення: {tex.Message}");
                Disconnect();
                _isConnected = false;
                throw new Exception($"Не вдалося підключитися до сервера: Таймаут підключення.", tex);
            }
            catch (SocketException sex)
            {
                Console.WriteLine($"[OrderDetailsPage.ConnectToServerAsync] Помилка сокета ({sex.SocketErrorCode}): {sex.Message}");
                Disconnect();
                _isConnected = false;
                throw new Exception($"Помилка підключення до сервера ({sex.SocketErrorCode}): {sex.Message}", sex);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[OrderDetailsPage.ConnectToServerAsync] Загальна помилка підключення: {ex.Message}");
                Disconnect();
                _isConnected = false;
                throw new Exception($"Невідома помилка при підключенні до сервера: {ex.Message}", ex);
            }
            finally
            {
                _networkSemaphore.Release();
            }
        }

        // Допоміжний метод для безпечного читання з потоку з таймаутом
        private async Task<int> ReadWithTimeoutAsync(NetworkStream stream, byte[] buffer, int offset, int count, TimeSpan timeout)
        {
            if (stream == null || !stream.CanRead)
            {
                _isConnected = false;
                throw new IOException("Спроба читання з недоступного потоку.");
            }

            using (var cts = new CancellationTokenSource(timeout))
            {
                try
                {
                    if (_client == null || !_client.Connected || _stream == null || !_stream.CanRead)
                    {
                        _isConnected = false;
                        throw new IOException("З'єднання втрачено перед читанням.");
                    }

                    var bytesRead = await stream.ReadAsync(buffer, offset, count, cts.Token);
                    if (bytesRead == 0 && count > 0)
                    {
                        _isConnected = false;
                        throw new IOException("З'єднання з сервером було розірвано (отримано 0 байт).");
                    }
                    return bytesRead;
                }
                catch (OperationCanceledException)
                {
                    _isConnected = false;
                    throw new IOException($"Таймаут ({timeout.TotalSeconds} сек) читання даних з сервера.");
                }
                catch (ObjectDisposedException ode)
                {
                    _isConnected = false;
                    throw new IOException("Спроба читання з закритого потоку.", ode);
                }
                catch (IOException ioEx)
                {
                    _isConnected = false;
                    throw;
                }
                catch (SocketException sex)
                {
                    _isConnected = false;
                    throw new IOException($"Помилка сокета під час читання ({sex.SocketErrorCode}): {sex.Message}", sex);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[OrderDetailsPage.ReadWithTimeoutAsync] Неочікувана помилка: {ex.GetType().Name} - {ex.Message}");
                    _isConnected = false;
                    throw new IOException($"Неочікувана помилка під час читання: {ex.Message}", ex);
                }
            }
        }


        private void Disconnect()
        {
            bool entered = _networkSemaphore.Wait(0);
            try
            {
                if (!_isConnected && (_client == null || !_client.Connected))
                {
                    return;
                }

                Console.WriteLine("[OrderDetailsPage.Disconnect] Закриття з'єднання...");
                _isConnected = false;

                _stream?.Close();
                _stream?.Dispose();
                _stream = null;

                _client?.Close();
                _client?.Dispose();
                _client = null;

                Console.WriteLine("[OrderDetailsPage.Disconnect] З'єднання закрито.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[OrderDetailsPage.Disconnect] Помилка при закритті з'єднання: {ex.Message}");
            }
            finally
            {
                if (entered)
                {
                    _networkSemaphore.Release();
                }
            }
        }

        // Універсальний метод для надсилання запиту та отримання відповіді
        private async Task<JToken> SendRequestAsync(Dictionary<string, object> requestData)
        {
            await ConnectToServerAsync();

            if (!_isConnected || _stream == null)
            {
                throw new IOException("Не вдалося встановити або відновити з'єднання з сервером перед надсиланням даних.");
            }

            string jsonRequest = JsonConvert.SerializeObject(requestData);
            byte[] requestBytes = Encoding.UTF8.GetBytes(jsonRequest);
            byte[] lengthBytes = BitConverter.GetBytes(requestBytes.Length);

            JToken jsonResponseToken = null;

            await _networkSemaphore.WaitAsync();
            try
            {
                if (!_isConnected || _stream == null || !_stream.CanWrite)
                {
                    throw new IOException("З'єднання втрачено перед надсиланням запиту (після отримання семафора).");
                }

                string actionName = "unknown";
                if (requestData.TryGetValue("action", out object actionValue))
                {
                    actionName = actionValue?.ToString() ?? "unknown";
                }
                Console.WriteLine($"[SendRequestAsync] Надсилаємо запит ({actionName}): {jsonRequest}");


                await _stream.WriteAsync(lengthBytes, 0, lengthBytes.Length);
                await _stream.WriteAsync(requestBytes, 0, requestBytes.Length);
                await _stream.FlushAsync();

                byte[] lengthBuffer = new byte[4];
                int bytesReadLength = await ReadWithTimeoutAsync(_stream, lengthBuffer, 0, 4, TimeSpan.FromSeconds(10));

                if (bytesReadLength < 4)
                {
                    throw new IOException($"Несподіваний кінець потоку під час читання довжини відповіді (прочитано {bytesReadLength} байт).");
                }

                int responseLength = BitConverter.ToInt32(lengthBuffer, 0);
                Console.WriteLine($"[SendRequestAsync] Очікувана довжина відповіді: {responseLength}");

                if (responseLength <= 0 || responseLength > 10 * 1024 * 1024)
                {
                    throw new IOException($"Отримана некоректна довжина відповіді: {responseLength}.");
                }

                byte[] responseBuffer = new byte[responseLength];
                int totalBytesRead = 0;
                TimeSpan readTimeout = TimeSpan.FromSeconds(30);

                while (totalBytesRead < responseLength)
                {
                    int bytesRead = await ReadWithTimeoutAsync(_stream, responseBuffer, totalBytesRead, responseLength - totalBytesRead, readTimeout);
                    if (bytesRead == 0)
                    {
                        throw new IOException("З'єднання з сервером було розірвано під час читання тіла відповіді.");
                    }
                    totalBytesRead += bytesRead;
                }

                string jsonResponse = Encoding.UTF8.GetString(responseBuffer, 0, totalBytesRead);
                Console.WriteLine($"[SendRequestAsync] Отримана відповідь: {jsonResponse}");

                jsonResponseToken = JToken.Parse(jsonResponse);

                if (jsonResponseToken.Type == JTokenType.Object && jsonResponseToken.SelectToken("success")?.Value<bool>() == false)
                {
                    string serverErrorMessage = jsonResponseToken.SelectToken("message")?.Value<string>() ?? "Невідома помилка сервера.";
                    Console.WriteLine($"[SendRequestAsync] Сервер повідомив про помилку: {serverErrorMessage}");
                    throw new Exception($"Помилка сервера: {serverErrorMessage}");
                }

                return jsonResponseToken;

            }
            catch (IOException ioEx)
            {
                Console.WriteLine($"[SendRequestAsync] Помилка IO під час обміну даними: {ioEx.Message}");
                Disconnect();
                throw new Exception($"Помилка зв'язку з сервером: {ioEx.Message}", ioEx);
            }
            catch (JsonException jsonEx)
            {
                Console.WriteLine($"[SendRequestAsync] Помилка парсингу JSON: {jsonEx.Message}");
                throw new Exception($"Помилка обробки відповіді сервера (JSON): {jsonEx.Message}", jsonEx);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SendRequestAsync] Загальна помилка під час обміну даними: {ex.Message}");
                if (_isConnected) Disconnect();
                throw;
            }
            finally
            {
                _networkSemaphore.Release();
            }
        }


        // Метод для завантаження деталей замовлення та списків для UI елементів
        private async Task LoadOrderDetailsAsync()
        {
            SetUIState(PageState.Loading, "Завантаження деталей...");

            try
            {
                var requestData = new Dictionary<string, object>
                {
                    { "action", "get_order_details" }, // Припускається, що ця дія повертає деталі, типи сесій та знижки
                    { "session_id", _sessionId },
                    { "manager_id", _managerId },
                    { "club_id", _clubId }
                };

                JToken responseToken = await SendRequestAsync(requestData); // Виконується у фоновому потоці

                if (responseToken?.Type != JTokenType.Object)
                {
                    throw new Exception($"Неочікуваний формат відповіді від сервера: {responseToken?.Type}");
                }

                JObject responseObject = (JObject)responseToken;

                // 1. Парсинг деталей замовлення (все ще у фоновому потоці)
                JToken sessionDetailsToken = responseObject["session"] ?? responseObject["details"]; // Спробувати різні ключі
                if (sessionDetailsToken?.Type != JTokenType.Object)
                {
                    throw new Exception("Відповідь сервера не містить об'єкт деталей замовлення ('session' або 'details').");
                }
                OrderDetails = ParseOrderDetails((JObject)sessionDetailsToken);
                if (OrderDetails == null)
                {
                    throw new Exception("Не вдалося розпарсити деталі замовлення.");
                }
                _clientId = OrderDetails.ClientId; // Зберігаємо ClientId

                // 2. Парсинг доступних типів сесій (на головному потоці).
                // Колекція AvailableSessionTypes все ще може заповнюватися, якщо це потрібно для інших цілей,
                // але вона більше не прив'язана до RadioButton.
                Device.BeginInvokeOnMainThread(() =>
                {
                    JToken sessionTypesToken = responseObject["sessionTypes"] ?? responseObject["session_types"];
                    AvailableSessionTypes.Clear(); // Очищаємо колекцію (не обов'язково, якщо вона не використовується UI)
                    if (sessionTypesToken?.Type == JTokenType.Array)
                    {
                        foreach (var item in (JArray)sessionTypesToken)
                        {
                            if (item.Type == JTokenType.Object)
                            {
                                // Заповнюємо колекцію, хоча вона не використовується для RadioButton UI
                                AvailableSessionTypes.Add(new SessionTypeOption
                                {
                                    Type = item.Value<string>("type") ?? item.Value<string>("Type") ?? "Невідомий",
                                    PricePerHour = item.Value<decimal?>("price_per_hour") ?? item.Value<decimal?>("PricePerHour") ?? 0m
                                });
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine("[LoadOrderDetailsAsync] Сервер не повернув список типів сесій або формат некоректний.");
                        // Можливо, додати стандартні типи, якщо сервер не надав список
                        if (!AvailableSessionTypes.Any())
                        {
                            AvailableSessionTypes.Add(new SessionTypeOption { Type = "VR", PricePerHour = 0 });
                            AvailableSessionTypes.Add(new SessionTypeOption { Type = "PS", PricePerHour = 0 });
                            AvailableSessionTypes.Add(new SessionTypeOption { Type = "Quest", PricePerHour = 0 });
                        }
                    }

                    // 3. Парсинг доступних знижок та оновлення колекції (на головному потоці)
                    JToken discountsToken = responseObject["discounts"];
                    AvailableDiscounts.Clear();
                    AvailableDiscounts.Add(new DiscountOption { Id = 0, Name = "Без знижки", Percentage = 0m });
                    if (discountsToken?.Type == JTokenType.Array)
                    {
                        foreach (var item in (JArray)discountsToken)
                        {
                            if (item.Type == JTokenType.Object)
                            {
                                AvailableDiscounts.Add(new DiscountOption
                                {
                                    Id = item.Value<int?>("id") ?? item.Value<int?>("Id") ?? 0,
                                    Name = item.Value<string>("name") ?? item.Value<string>("Name") ?? "Невідома знижка",
                                    Percentage = item.Value<decimal?>("percentage") ?? item.Value<decimal?>("Percentage") ?? 0m
                                });
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine("[LoadOrderDetailsAsync] Сервер не повернув список знижок або формат некоректний.");
                    }

                    // Способи оплати вже статично заповнені в конструкторі AvailablePaymentMethods

                    // Оновлюємо UI з завантаженими даними.
                    UpdateUIWithDetails(); // Цей метод повинен бути викликаний на головному потоці

                    // SetUIState(PageState.Loaded) викликається всередині UpdateUIWithDetails()
                });
                // --- Кінець блоку на головному потоці ---


            }
            catch (Exception ex)
            {
                Console.WriteLine($"[OrderDetailsPage.LoadOrderDetailsAsync] Помилка завантаження: {ex.Message}\n{ex.StackTrace}");
                SetUIState(PageState.Error, $"Не вдалося завантажити деталі: {ex.Message}");
                OrderDetails = null;
            }
        }


        // Допоміжний метод для парсингу JObject в OrderDetailsData
        private OrderDetailsData ParseOrderDetails(JObject obj)
        {
            if (obj == null) return null;

            try
            {
                var details = new OrderDetailsData
                {
                    SessionId = obj.Value<int?>("SessionId") ?? 0,
                    ClientId = obj.Value<int?>("ClientId") ?? 0,
                    ClientName = obj.Value<string>("ClientName") ?? "N/A",
                    PhoneNumber = obj.Value<string>("ClientPhone") ?? "",
                    NumPeople = obj.Value<int?>("NumPeople") ?? 0,
                    SessionType = obj.Value<string>("SessionType") ?? "N/A", // Парсимо сирий тип з сервера
                    Notes = obj.Value<string>("Notes") ?? "",
                    PaymentStatus = obj.Value<string>("PaymentStatus") ?? "Unknown",
                    DiscountId = obj.Value<int?>("DiscountId"),
                    CalculatePrice = obj.Value<decimal?>("CalculatePrice") ?? 0m,
                    FinalPrice = obj.Value<decimal?>("FinalPrice") ?? 0m, // Парсимо фінальну ціну
                };
                Console.WriteLine($"[ParseOrderDetails] Parsed ClientId: {details.ClientId}"); // ДОДАЙТЕ ЦЕЙ ЛОГ
                // Парсинг дати (спроба кількох форматів)
                DateTime sessionDate;
                if (DateTime.TryParseExact(obj.Value<string>("SessionDate"), "yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out sessionDate))
                {
                    details.SessionDate = sessionDate;
                }
                else if (DateTime.TryParseExact(obj.Value<string>("SessionDate"), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out sessionDate))
                {
                    details.SessionDate = sessionDate;
                }
                else if (DateTime.TryParse(obj.Value<string>("SessionDate"), CultureInfo.InvariantCulture, DateTimeStyles.None, out sessionDate))
                {
                    details.SessionDate = sessionDate;
                }
                else if (DateTime.TryParse(obj.Value<string>("SessionDate"), out sessionDate))
                {
                    details.SessionDate = sessionDate;
                }
                else
                {
                    Console.WriteLine($"[ParseOrderDetails] Не вдалося розпарсити дату SessionDate: {obj.Value<string>("SessionDate")}");
                    details.SessionDate = DateTime.Today;
                }


                // Парсинг часу початку та закінчення (спроба кількох форматів)
                TimeSpan startTimeSpan;
                if (TimeSpan.TryParse(obj.Value<string>("StartTime"), CultureInfo.InvariantCulture, out startTimeSpan))
                {
                    details.StartTime = startTimeSpan;
                }
                else if (TimeSpan.TryParse(obj.Value<string>("StartTime"), out startTimeSpan))
                {
                    details.StartTime = startTimeSpan;
                }
                else
                {
                    Console.WriteLine($"[ParseOrderDetails] Не вдалося розпарсити час початку StartTime: {obj.Value<string>("StartTime")}");
                    details.StartTime = TimeSpan.Zero;
                }

                TimeSpan endTimeSpan;
                if (TimeSpan.TryParse(obj.Value<string>("EndTime"), CultureInfo.InvariantCulture, out endTimeSpan))
                {
                    details.EndTime = endTimeSpan;
                }
                else if (TimeSpan.TryParse(obj.Value<string>("EndTime"), out endTimeSpan))
                {
                    details.EndTime = endTimeSpan;
                }
                else
                {
                    Console.WriteLine($"[ParseOrderDetails] Не вдалося розпарсити час закінчення EndTime: {obj.Value<string>("EndTime")}");
                    details.EndTime = TimeSpan.Zero;
                }


                return details;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ParseOrderDetails] Помилка при створенні OrderDetailsData з JObject: {ex.Message}");
                return null;
            }
        }

        // Метод для оновлення UI елементів на основі OrderDetails (після завантаження або збереження)
        // Викликається вже на головному потоці (всередині Device.BeginInvokeOnMainThread)
        private void UpdateUIWithDetails()
        {
            // Перевірка на головний потік під час відладки
            //Debug.Assert(Device.IsMainThread, "UpdateUIWithDetails викликано не на головному потоці!");

            if (OrderDetails == null)
            {
                return;
            }

            // Заповнюємо Label та Entry/Picker
            sessionIdLabel.Text = OrderDetails.SessionId.ToString();
            clientNameEntry.Text = OrderDetails.ClientName;
            phoneNumberEntry.Text = OrderDetails.PhoneNumber;

            sessionDatePicker.Date = OrderDetails.SessionDate;
            startTimePicker.Time = OrderDetails.StartTime;
            endTimePicker.Time = OrderDetails.EndTime;

            numPeopleEntry.Text = OrderDetails.NumPeople.ToString();

            notesEditor.Text = OrderDetails.Notes;

            paymentStatusLabel.Text = OrderDetails.PaymentStatus;
            calculatedPriceLabel.Text = OrderDetails.CalculatePrice.ToString("C2", CultureInfo.CurrentCulture);

            finalPriceEntry.Text = OrderDetails.FinalPrice.ToString("N2", CultureInfo.CurrentCulture);


            // --- Встановлюємо обраний тип сесії для RadioButtons ---
            // Спочатку зберігаємо обраний тип у властивості SelectedSessionType (string)
            SelectedSessionType = OrderDetails.SessionType; // Тепер це просто присвоєння рядка

            // Тепер перебираємо статичні RadioButton та встановлюємо IsChecked для відповідної
            if (sessionTypeRadioButtonsContainer.Children != null)
            {
                foreach (var child in sessionTypeRadioButtonsContainer.Children)
                {
                    if (child is RadioButton rb)
                    {
                        // Порівнюємо Value RadioButton (який є string) з OrderDetails.SessionType
                        if (rb.Value?.ToString() == OrderDetails.SessionType)
                        {
                            rb.IsChecked = true;
                            Console.WriteLine($"[UpdateUIWithDetails] Checked RadioButton for type: {OrderDetails.SessionType}");
                            // SelectedSessionType вже встановлено вище
                        }
                        else
                        {
                            rb.IsChecked = false; // Явно знімаємо відмітку з інших
                        }
                    }
                }
            }
            else
            {
                Console.WriteLine("[UpdateUIWithDetails] sessionTypeRadioButtonsContainer.Children is null, cannot set IsChecked.");
            }
            // --- Кінець встановлення RadioButton ---


            // Встановлюємо обрані значення в інших Pickers через прив'язані властивості
            SelectedDiscount = AvailableDiscounts.FirstOrDefault(d => d.Id == OrderDetails.DiscountId) ?? AvailableDiscounts.FirstOrDefault(d => d.Id == 0);

            SelectedPaymentMethod = null;


            // Оновлюємо стан UI (доступність кнопок/полів) після заповнення полів
            SetUIState(PageState.Loaded);

        }

        // Обробник події CheckedChanged для RadioButtons типу сесії
        private void SessionType_CheckedChanged(object sender, CheckedChangedEventArgs e)
        {
            // Цей обробник викликається для КОЖНОЇ RadioButton при зміні її стану.
            // Нас цікавить тільки та, що стала IsChecked = true.
            if (e.Value) // Якщо ця RadioButton стала відміченою
            {
                if (sender is RadioButton checkedRadioButton)
                {
                    // Оновлюємо властивість SelectedSessionType (string) значенням Value RadioButton
                    SelectedSessionType = checkedRadioButton.Value?.ToString();
                    Console.WriteLine($"[SessionType_CheckedChanged] User selected: {SelectedSessionType}");

                    // Можна оновити UI стан тут, якщо потрібно
                    // Device.BeginInvokeOnMainThread(() => SetUIState(PageState.Loaded)); // За потреби
                }
            }
            // Якщо e.Value == false, це означає, що RadioButton була "розмічена",
            // нас це не цікавить напряму, бо CheckedChanged для НОВОЇ обраної RadioButton
            // вже встановить SelectedSessionType.
        }

        private async void SaveButton_Clicked(object sender, EventArgs e)
        {
            // 1. Валідація всіх даних з UI полів
            if (!ValidateInput(out string validationError))
            {
                Device.BeginInvokeOnMainThread(async () =>
                {
                    await DisplayAlert("Помилка валідації", validationError, "OK");
                });
                return;
            }

            // 2. Збираємо всі дані з UI полів
            var updatedDetails = CollectDataFromUI();

            SetUIState(PageState.Saving, "Збереження змін...");

            try
            {
                var requestData = new Dictionary<string, object>
                 {
                     { "action", "update_order" },
                     { "session_id", OrderDetails?.SessionId ?? _sessionId },
                     { "club_id", _clubId },
                     { "manager_id", _managerId },

                     { "client_id", _clientId },
                     { "client_name", updatedDetails.ClientName },
                     { "phone_number", updatedDetails.PhoneNumber },
                     { "session_date", updatedDetails.SessionDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) },
                     { "start_time", updatedDetails.StartTime.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture) },
                     { "end_time", updatedDetails.EndTime.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture) },
                     { "num_people", updatedDetails.NumPeople },
                     { "session_type", updatedDetails.SessionType }, // Беремо тип з SelectedSessionType (string)
                     //{ "discount_id", updatedDetails.DiscountId },
                     { "notes", updatedDetails.Notes },
                     { "final_price", updatedDetails.FinalPrice }, // Передаємо фінальну ціну з Entry
                 };

                JToken responseToken = await SendRequestAsync(requestData);

                if (responseToken?.Type == JTokenType.Object && responseToken.SelectToken("success")?.Value<bool>() == true)
                {
                    JObject successResponseObject = (JObject)responseToken;

                    Console.WriteLine($"[SaveButton_Clicked] Замовлення #{OrderDetails.SessionId} успішно збережено.");

                    // Оновлюємо локальний об'єкт OrderDetails з даних, які щойно зберегли (з UI)
                    OrderDetails.ClientId = updatedDetails.ClientId;
                    OrderDetails.ClientName = updatedDetails.ClientName;
                    OrderDetails.PhoneNumber = updatedDetails.PhoneNumber;
                    OrderDetails.SessionDate = updatedDetails.SessionDate;
                    OrderDetails.StartTime = updatedDetails.StartTime;
                    OrderDetails.EndTime = updatedDetails.EndTime;
                    OrderDetails.NumPeople = updatedDetails.NumPeople;
                    OrderDetails.SessionType = updatedDetails.SessionType; // Синхронізуємо з обраним типом (string)
                    OrderDetails.DiscountId = updatedDetails.DiscountId;
                    OrderDetails.Notes = updatedDetails.Notes;
                    OrderDetails.FinalPrice = updatedDetails.FinalPrice; // Синхронізуємо з Entry

                    // Оновлюємо calculate_price та final_price, якщо сервер повернув їх
                    OrderDetails.CalculatePrice = successResponseObject.Value<decimal?>("calculate_price") ?? successResponseObject.Value<decimal?>("CalculatePrice") ?? OrderDetails.CalculatePrice;
                    // OrderDetails.FinalPrice = successResponseObject.Value<decimal?>("final_price") ?? successResponseObject.Value<decimal?>("FinalPrice") ?? OrderDetails.FinalPrice; // Модель FinalPrice теж оновлюємо, якщо сервер повертає


                    Device.BeginInvokeOnMainThread(() =>
                    {
                        UpdateUIWithDetails();
                    });


                    Device.BeginInvokeOnMainThread(async () =>
                    {
                        await DisplayAlert("Успіх", "Замовлення успішно збережено.", "OK");
                    });
                }
                else
                {
                    throw new Exception("Сервер повернув неочікувану відповідь при збереженні.");
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SaveButton_Clicked] Помилка збереження: {ex.Message}\n{ex.StackTrace}");
                SetUIState(PageState.Loaded);
                Device.BeginInvokeOnMainThread(async () =>
                {
                    await DisplayAlert("Помилка збереження", $"Не вдалося зберегти замовлення: {ex.Message}", "OK");
                });
            }
        }

        private async void RecordPaymentButton_Clicked(object sender, EventArgs e)
        {
            decimal finalPriceToPay;
            if (OrderDetails == null || OrderDetails.SessionId == 0 || OrderDetails.PaymentStatus != "Pending")
            {
                await DisplayAlert("Помилка", "Це замовлення не може бути оплачене.", "OK");
                SetUIState(PageState.Loaded);
                return;
            }
            if (!decimal.TryParse(finalPriceEntry.Text?.Trim(), NumberStyles.Any, CultureInfo.CurrentCulture, out finalPriceToPay) || finalPriceToPay <= 0)
            {
                await DisplayAlert("Помилка", "Будь ласка, введіть коректну фінальну ціну більшу за нуль для оплати.", "OK");
                SetUIState(PageState.Loaded);
                return;
            }
            if (SelectedPaymentMethod == null)
            {
                await DisplayAlert("Помилка", "Будь ласка, оберіть спосіб оплати.", "OK");
                SetUIState(PageState.Loaded);
                return;
            }


            bool confirmed = await DisplayAlert("Підтвердження оплати", $"Записати оплату {finalPriceToPay:C2} ({SelectedPaymentMethod.Method}) для замовлення #{OrderDetails.SessionId}?", "Так", "Ні");
            if (!confirmed)
            {
                SetUIState(PageState.Loaded);
                return;
            }

            SetUIState(PageState.Paying, "Запис оплати...");

            try
            {
                var requestData = new Dictionary<string, object>
                 {
                     { "action", "record_payment" },
                     { "session_id", OrderDetails.SessionId },
                     { "club_id", _clubId },
                     { "manager_id", _managerId },
                     { "amount", finalPriceToPay },
                     { "payment_method", SelectedPaymentMethod.Method },
                     { "payment_time", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) }
                 };

                JToken responseToken = await SendRequestAsync(requestData);

                if (responseToken?.Type == JTokenType.Object && responseToken.SelectToken("success")?.Value<bool>() == true)
                {
                    Console.WriteLine($"[RecordPaymentButton_Clicked] Оплата для замовлення #{OrderDetails.SessionId} успішно записана.");

                    OrderDetails.PaymentStatus = "Paid";
                    // OrderDetails.FinalPrice = finalPriceToPay; // Синхронізуємо модель з оплаченою сумою (не обов'язково)

                    Device.BeginInvokeOnMainThread(() =>
                    {
                        UpdateUIWithDetails();
                    });

                    Device.BeginInvokeOnMainThread(async () =>
                    {
                        await DisplayAlert("Успіх", "Оплата успішно записана.", "OK");
                    });
                }
                else
                {
                    throw new Exception("Сервер повернув неочікувану відповідь при записі оплати.");
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RecordPaymentButton_Clicked] Помилка запису оплати: {ex.Message}\n{ex.StackTrace}");
                SetUIState(PageState.Loaded);
                Device.BeginInvokeOnMainThread(async () =>
                {
                    await DisplayAlert("Помилка оплати", $"Не вдалося записати оплату: {ex.Message}", "OK");
                });
            }
        }

        private async void DeleteButton_Clicked(object sender, EventArgs e)
        {
            if (OrderDetails == null || OrderDetails.SessionId == 0 || OrderDetails.PaymentStatus == "Paid" || OrderDetails.PaymentStatus == "Canceled")
            {
                await DisplayAlert("Помилка", "Це замовлення не може бути видалене (відмінене).", "OK");
                SetUIState(PageState.Loaded);
                return;
            }


            bool confirmed = await DisplayAlert("Підтвердження відміни", $"Ви впевнені, що хочете відмінити замовлення #{OrderDetails.SessionId}?", "Так", "Ні");
            if (!confirmed)
            {
                SetUIState(PageState.Loaded);
                return;
            }

            SetUIState(PageState.Deleting, "Відміна замовлення...");

            try
            {
                var requestData = new Dictionary<string, object>
                 {
                     { "action", "cancel_order" },
                     { "session_id", OrderDetails.SessionId },
                     { "club_id", _clubId },
                     { "manager_id", _managerId },
                 };

                JToken responseToken = await SendRequestAsync(requestData);

                if (responseToken?.Type == JTokenType.Object && responseToken.SelectToken("success")?.Value<bool>() == true)
                {
                    Console.WriteLine($"[DeleteButton_Clicked] Замовлення #{OrderDetails.SessionId} успішно відмінено.");

                    OrderDetails.PaymentStatus = "Canceled";

                    Device.BeginInvokeOnMainThread(async () =>
                    {
                        await DisplayAlert("Успіх", "Замовлення успішно відмінено.", "OK");
                        await Navigation.PopAsync();
                    });
                }
                else
                {
                    throw new Exception("Сервер повернув неочікувану відповідь при відміні.");
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DeleteButton_Clicked] Помилка відміни: {ex.Message}\n{ex.StackTrace}");
                SetUIState(PageState.Loaded);
                Device.BeginInvokeOnMainThread(async () =>
                {
                    await DisplayAlert("Помилка відміни", $"Не вдалося відмінити замовлення: {ex.Message}", "OK");
                });
            }
        }


        // Допоміжний метод для збору ВСІХ даних з UI полів
        // Цей метод має викликатись тільки на головному потоці!
        private OrderDetailsData CollectDataFromUI()
        {
            // Debug.Assert(Device.IsMainThread, "CollectDataFromUI викликано не на головному потоці!");

            var details = new OrderDetailsData
            {
                SessionId = OrderDetails?.SessionId ?? _sessionId,
                ClientId = _clientId,
                ClientName = clientNameEntry.Text?.Trim() ?? "",
                PhoneNumber = phoneNumberEntry.Text?.Trim() ?? "",
                SessionDate = sessionDatePicker.Date,
                StartTime = startTimePicker.Time,
                EndTime = endTimePicker.Time,
                NumPeople = int.TryParse(numPeopleEntry.Text?.Trim(), out int peopleCount) ? peopleCount : 0,
                // Беремо тип сесії з SelectedSessionType (string)
                SessionType = SelectedSessionType ?? "N/A",

                Notes = notesEditor.Text?.Trim() ?? "",

                //DiscountId = (discountPicker.SelectedItem as DiscountOption)?.Id,

                PaymentStatus = OrderDetails?.PaymentStatus ?? "Unknown",

                CalculatePrice = OrderDetails?.CalculatePrice ?? 0m,

                // !!! Фінальна ціна береться безпосередньо з Entry !!!
                FinalPrice = decimal.TryParse(finalPriceEntry.Text?.Trim(), NumberStyles.Any, CultureInfo.CurrentCulture, out decimal finalPrice) ? finalPrice : 0m,

                PaymentMethod = (paymentMethodPicker.SelectedItem as PaymentMethodOption)?.Method
            };
            return details;
        }

        // Допоміжний метод для валідації введених даних (для збереження)
        // Цей метод має викликатись тільки на головному потоці!
        private bool ValidateInput(out string errorMessage)
        {
            // Debug.Assert(Device.IsMainThread, "ValidateInput викликано не на головному потоці!");

            errorMessage = null;

            if (string.IsNullOrWhiteSpace(clientNameEntry.Text?.Trim()))
            {
                errorMessage = "Введіть ім'я клієнта.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(phoneNumberEntry.Text?.Trim()))
            {
                errorMessage = "Введіть телефон клієнта.";
                return false;
            }
            // Перевірка, чи обрано тип сесії (перевіряємо SelectedSessionType string)
            if (string.IsNullOrWhiteSpace(SelectedSessionType) || SelectedSessionType == "N/A") // Перевіряємо на null, порожній рядок або "N/A"
            {
                errorMessage = "Оберіть тип сесії.";
                return false;
            }
            if (!int.TryParse(numPeopleEntry.Text?.Trim(), out int numPeople) || numPeople <= 0)
            {
                errorMessage = "Кількість людей має бути додатнім числом.";
                return false;
            }
            if (sessionDatePicker.Date == null || sessionDatePicker.Date == DateTime.MinValue)
            {
                errorMessage = "Оберіть коректну дату.";
                return false;
            }
            if (startTimePicker.Time >= endTimePicker.Time)
            {
                errorMessage = "Час початку має бути раніше часу закінчення.";
                return false;
            }
            if (!decimal.TryParse(finalPriceEntry.Text?.Trim(), NumberStyles.Any, CultureInfo.CurrentCulture, out decimal finalPrice) || finalPrice < 0)
            {
                errorMessage = "Введіть коректну фінальну ціну (не від'ємну).";
                return false;
            }

            return true;
        }

        // Допоміжний метод для валідації даних, що потрібні лише для розрахунку ціни
        // Цей метод має викликатись тільки на головному потоці!
        private string ValidateInputForCalculation()
        {
            // Debug.Assert(Device.IsMainThread, "ValidateInputForCalculation викликано не на головному потоці!");

            // Перевірка, чи обрано тип сесії (перевіряємо SelectedSessionType string)
            if (string.IsNullOrWhiteSpace(SelectedSessionType) || SelectedSessionType == "N/A")
            {
                return "Оберіть тип сесії для розрахунку.";
            }
            if (!int.TryParse(numPeopleEntry.Text?.Trim(), out int numPeople) || numPeople <= 0)
            {
                return "Кількість людей має бути додатнім числом для розрахунку.";
            }
            if (sessionDatePicker.Date == null || sessionDatePicker.Date == DateTime.MinValue)
            {
                return "Оберіть коректну дату для розрахунку.";
            }
            if (startTimePicker.Time >= endTimePicker.Time)
            {
                return "Час початку має бути раніше часу закінчення для розрахунку.";
            }

            return null;
        }


        // Звільнення ресурсів при закритті сторінки
        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            Console.WriteLine("[OrderDetailsPage.OnDisappearing] Сторінка закривається, закриваємо з'єднання.");
            Task.Run(() => Disconnect());
            /*
            try
            {
                _networkSemaphore?.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[OrderDetailsPage.OnDisappearing] Помилка при Dispose семафора: {ex.Message}");
            }
            */
        }

        // Реалізація OnPropertyChanged для INotifyPropertyChanged
        protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}