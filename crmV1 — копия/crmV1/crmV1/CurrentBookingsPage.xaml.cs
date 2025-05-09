using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading; // Додано для CancellationTokenSource
using System.Threading.Tasks;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;
using System.Net.Sockets;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;
using System.IO;
using System.Globalization; // Для ParseExact

namespace crmV1
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class CurrentBookingsPage : ContentPage
    {
        private string ServerIP = AppConfig.ServerIP;
        private const int ServerPort = 8888;

        private DateTime _selectedDate;
        private string _selectedStartTime;
        private TcpClient _client;
        private NetworkStream _stream;
        private bool _isConnected = false;
        private readonly SemaphoreSlim _asyncLock = new SemaphoreSlim(1, 1); // Для запобігання одночасного доступу до мережі
        private bool _isUpdating = false; // Прапор для запобігання реентрантності таймера
        private Timer _updateTimer; // Використаємо System.Threading.Timer

        private int _managerId;
        private int _clubId;

        // Зробимо ObservableCollection readonly, щоб уникнути заміни екземпляра
        public ReadOnlyObservableCollection<BookingItem> BookingItems { get; }
        private readonly ObservableCollection<BookingItem> _bookingItemsInternal = new ObservableCollection<BookingItem>();

        // Змінимо властивість для Binding, щоб вона використовувала _selectedStartTime
        public string BookingTimeLabel => $"Бронювання на ({_selectedStartTime})";

        public CurrentBookingsPage(int managerId, int clubId, DateTime selectedDate, string selectedStartTime)
        {
            InitializeComponent();
            NavigationPage.SetHasNavigationBar(this, false);
            _managerId = managerId;
            _clubId = clubId;
            _selectedDate = selectedDate;
            _selectedStartTime = selectedStartTime;

            // Ініціалізуємо публічну колекцію
            BookingItems = new ReadOnlyObservableCollection<BookingItem>(_bookingItemsInternal);

            BindingContext = this; // Встановлюємо BindingContext після ініціалізації властивостей

            // Запускаємо початкове оновлення
            // Не блокуємо конструктор, запускаємо у фоні
            Task.Run(async () => await UpdateBookingsAsync());

            // Налаштовуємо таймер System.Threading.Timer для фонового виконання
            _updateTimer = new Timer(TimerCallback, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
        }

        // Метод зворотного виклику для таймера
        private async void TimerCallback(object state)
        {
            await UpdateBookingsAsync();
        }

        private async Task ConnectToServerAsync()
        {
            // Використовуємо SemaphoreSlim для потокобезпечного доступу
            await _asyncLock.WaitAsync();
            try
            {
                // Перевіряємо з'єднання ще раз всередині блокування
                if (_client == null || !_client.Connected || _stream == null)
                {
                    // Закриваємо попередні ресурси, якщо вони існують і не закриті
                    Disconnect();

                    Console.WriteLine("[ConnectToServerAsync] Спроба підключення...");
                    _client = new TcpClient
                    {
                        ReceiveTimeout = 10000, // Збільшено таймаут
                        SendTimeout = 10000
                    };
                    // Використовуємо CancellationToken для таймаута підключення
                    using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                    {
                        try
                        {
                            await _client.ConnectAsync(ServerIP, ServerPort).ContinueWith(task =>
                            {
                                if (task.IsFaulted && cts.Token.IsCancellationRequested)
                                {
                                    throw new SocketException(10060); // Код помилки таймаута
                                }
                                task.GetAwaiter().GetResult(); // Перевірка інших помилок підключення
                            }, cts.Token);
                        }
                        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.TimedOut || cts.IsCancellationRequested)
                        {
                            Console.WriteLine($"[ConnectToServerAsync] Таймаут підключення до {ServerIP}:{ServerPort}.");
                            throw new Exception("Не вдалося підключитися до сервера: Таймаут.", ex);
                        }
                        catch (Exception ex) // Інші помилки підключення
                        {
                            Console.WriteLine($"[ConnectToServerAsync] Помилка підключення: {ex.Message}");
                            throw; // Перекидаємо виняток
                        }
                    }


                    _stream = _client.GetStream();
                    _isConnected = true;
                    Console.WriteLine("[ConnectToServerAsync] З'єднання з сервером встановлено.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ConnectToServerAsync] Помилка: {ex.Message}");
                Device.BeginInvokeOnMainThread(async () =>
                {
                    await DisplayAlert("Помилка підключення", $"Не вдалося підключитися до сервера: {ex.Message}", "OK");
                });
                Disconnect(); // Переконуємось, що все закрито
                _isConnected = false;
                // Не перекидаємо виняток далі, щоб UpdateBookings міг спробувати ще раз пізніше
            }
            finally
            {
                _asyncLock.Release();
            }
        }

        private async Task<int> ReadWithTimeoutAsync(NetworkStream stream, byte[] buffer, int offset, int count, TimeSpan timeout)
        {
            if (stream == null || !stream.CanRead)
            {
                Console.WriteLine($"[ReadWithTimeoutAsync] Помилка: Потік null або не для читання.");
                throw new IOException("Спроба читання з недоступного потоку.");
            }

            using (var cts = new CancellationTokenSource(timeout))
            {
                try
                {
                    return await stream.ReadAsync(buffer, offset, count, cts.Token);
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine($"[ReadWithTimeoutAsync] Таймаут ({timeout.TotalSeconds} сек) читання даних.");
                    throw new IOException($"Таймаут ({timeout.TotalSeconds} сек) читання даних з сервера.");
                }
                catch (ObjectDisposedException ode)
                {
                    Console.WriteLine($"[ReadWithTimeoutAsync] Помилка: Спроба читання з закритого потоку. {ode.Message}");
                    throw new IOException("Спроба читання з закритого потоку.", ode);
                }
                catch (IOException ioEx) // Часто виникає при розриві з'єднання
                {
                    Console.WriteLine($"[ReadWithTimeoutAsync] Помилка IO: {ioEx.Message}");
                    // Вважаємо це розривом з'єднання
                    _isConnected = false;
                    throw;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ReadWithTimeoutAsync] Неочікувана помилка: {ex.GetType().Name} - {ex.Message}");
                    _isConnected = false; // Також вважаємо розривом
                    throw new IOException($"Неочікувана помилка під час читання: {ex.Message}", ex);
                }
            }
        }

        private void AddFreeZoneItems(List<BookingItem> currentBookings, string slotStartTime, string slotEndTime)
        {
            // Припустимо, що є 3 зони VR/Quest і 1 зона PS (всього 4)
            const int totalVRQuestZones = 3;
            const int totalPSZones = 1;
            const int totalZones = totalVRQuestZones + totalPSZones;

            // Рахуємо зайняті зони (NumPeople може представляти кількість зон для одного бронювання?)
            // Припустимо, що NumPeople = 1 означає 1 зайняту зону відповідного типу
            int bookedVRQuestZones = currentBookings.Where(b => !b.IsFreeZone && (b.SessionType == "VR" || b.SessionType == "Quest")).Sum(b => b.NumPeople);
            int bookedPSZones = currentBookings.Where(b => !b.IsFreeZone && b.SessionType == "PS").Sum(b => b.NumPeople);

            int freeVRQuestZones = Math.Max(0, totalVRQuestZones - bookedVRQuestZones);
            int freePSZones = Math.Max(0, totalPSZones - bookedPSZones);

            // Додаємо вільні зони VR/Quest
            for (int i = 0; i < freeVRQuestZones; i++)
            {
                currentBookings.Add(new BookingItem
                {
                    IsFreeZone = true,
                    SessionType = "VR", // Або "Quest", залежно від логіки
                    StartTime = slotStartTime,
                    EndTime = slotEndTime,
                    NumPeople = 0,
                    ClientName = "Вільна зона" // Текст для вільної зони
                });
            }

            // Додаємо вільні зони PS
            for (int i = 0; i < freePSZones; i++)
            {
                currentBookings.Add(new BookingItem
                {
                    IsFreeZone = true,
                    SessionType = "PS",
                    StartTime = slotStartTime,
                    EndTime = slotEndTime,
                    NumPeople = 0,
                    ClientName = "Вільна зона" // Текст для вільної зони
                });
            }
        }


        private async void OnBookingItemTapped(object sender, ItemTappedEventArgs e)
        {
            if (e.Item is BookingItem selectedItem)
            {
                // Запобігаємо подвійному кліку або навігації під час оновлення
                if (_isUpdating) return;

                // Перевіряємо, чи це вільна зона
                if (selectedItem.IsFreeZone)
                {
                    Console.WriteLine($"Натиснуто на вільну зону: {selectedItem.SessionType} ({selectedItem.StartTime}-{selectedItem.EndTime})");
                    string timeSlot = $"{selectedItem.StartTime}-{selectedItem.EndTime}"; // Використовуємо вже форматований час

                    try
                    {
                        // Переходимо на сторінку створення бронювання
                        // Для нового бронювання використовуємо _selectedDate (дату, обрану на попередній сторінці)
                        await Navigation.PushAsync(new BookingPage(timeSlot, _selectedDate, _managerId, _clubId));
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Помилка навігації на BookingPage: {ex.Message}");
                        await DisplayAlert("Помилка", "Не вдалося відкрити сторінку створення бронювання.", "OK");
                    }
                }
                else // Це вже існуюче бронювання
                {
                    Console.WriteLine($"Натиснуто на існуюче бронювання: ID={selectedItem.SessionId}, Клієнт={selectedItem.ClientName}");
                    try
                    {
                        // Переходимо на сторінку деталей бронювання
                        // Передаємо ID сесії, можливо managerId та clubId, якщо вони потрібні на сторінці деталей
                        await Navigation.PushAsync(new OrderDetailsPage(selectedItem.SessionId, _managerId, _clubId));
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Помилка навігації на BookingDetailsPage: {ex.Message}");
                        await DisplayAlert("Помилка", "Не вдалося відкрити сторінку деталей бронювання.", "OK");
                    }
                }
            }

            // Скидання виділення
            if (sender is ListView listView)
            {
                listView.SelectedItem = null;
            }
        }
        // Метод для безпечного закриття з'єднання
        private void Disconnect()
        {
            Console.WriteLine("[Disconnect] Закриття з'єднання...");
            _isConnected = false; // Встановлюємо прапор перед закриттям
            try
            {
                _stream?.Close(); // Закриває і потік, і клієнт
                _stream?.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Disconnect] Помилка при закритті потоку: {ex.Message}");
            }
            finally { _stream = null; } // Обнуляємо посилання

            try
            {
                _client?.Close();
                _client?.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Disconnect] Помилка при закритті клієнта: {ex.Message}");
            }
            finally { _client = null; } // Обнуляємо посилання

            Console.WriteLine("[Disconnect] З'єднання закрито.");

        }
        // Перейменовано на UpdateBookingsAsync для ясності
        public async Task UpdateBookingsAsync()
        {
            // Запобігання реентрантності
            if (_isUpdating)
            {
                Console.WriteLine("[UpdateBookingsAsync] Оновлення вже виконується.");
                return;
            }
            _isUpdating = true;

            try
            {
                await ConnectToServerAsync(); // Спробувати підключитися або перевірити з'єднання

                if (!_isConnected || _stream == null)
                {
                    Console.WriteLine("[UpdateBookingsAsync] Немає з'єднання з сервером. Оновлення скасовано.");
                    // Можливо, варто очистити список або показати повідомлення користувачу
                    Device.BeginInvokeOnMainThread(() => _bookingItemsInternal.Clear());
                    return;
                }

                string startTime = null;
                string endTime = null;

                // Використовуємо ParseExact для надійності
                string timeFormat = @"hh\:mm";
                if (TimeSpan.TryParseExact(_selectedStartTime, timeFormat, CultureInfo.InvariantCulture, out TimeSpan startTimeSpan))
                {
                    TimeSpan endTimeSpan = startTimeSpan.Add(TimeSpan.FromHours(1));
                    startTime = startTimeSpan.ToString(timeFormat);
                    // Перевірка переходу через північ (малоймовірно для бронювань, але можливо)
                    endTime = endTimeSpan.Days > 0 ? "23:59" : endTimeSpan.ToString(timeFormat);
                }
                else
                {
                    Console.WriteLine("Невірний формат _selectedStartTime: " + _selectedStartTime);
                    Device.BeginInvokeOnMainThread(async () =>
                    {
                        await DisplayAlert("Помилка формату", "Невірний формат часу початку.", "OK");
                    });
                    return; // Вихід, якщо формат часу невірний
                }

                var requestData = new Dictionary<string, string>
                {
                    { "action", "get_bookings_by_time_slot" },
                    // --- ДОДАНО ЦЕЙ РЯДОК ---
                    { "manager_id", _managerId.ToString() },
                    // -----------------------
                    { "session_date", _selectedDate.ToString("yyyy-MM-dd") },
                    { "start_time", startTime },
                    { "end_time", endTime },
                    // Згідно логів, club_id вже був у запиті, тому залишаємо його
                    // {"club_id", "1"} // Якщо club_id динамічний, потрібно отримати його після логіну
                    // Припускаємо, що club_id також повинен передаватися, якщо він не фіксований.
                    // Якщо club_id фіксований "1", можна залишити як є або отримати його звідкись (наприклад, після логіну).
                    // Виходячи з логу, клуб ID 1 вже був надісланий клієнтом.
                    // Якщо manager_id асоційований з club_id, сервер може сам його знайти по manager_id.
                    // Але для точності, якщо клієнт знає club_id, краще його теж надсилати.
                    // Оскільки в логах клієнт вже надсилав "club_id":"1", я залишаю його присутність
                    // у випадку, якщо він потрібен серверу разом з manager_id.
                    // Якщо club_id приходить після логіну, його потрібно зберегти так само як manager_id
                    // і використовувати тут.
                    // Якщо club_id статичний "1", можна просто додати його сюди:
                    // { "club_id", "1" } // Якщо club_id статичний
                    // Або використовувати збережене значення, як для manager_id, якщо воно динамічне.
                    // Наразі, базуючись лише на помилці про manager_id, додаємо лише manager_id.
                };

                string jsonRequest = JsonConvert.SerializeObject(requestData);
                byte[] requestBytes = Encoding.UTF8.GetBytes(jsonRequest);
                byte[] lengthBytes = BitConverter.GetBytes(requestBytes.Length);

                string jsonResponse = null;

                // Блокування для надсилання/отримання
                await _asyncLock.WaitAsync();
                try
                {
                    if (!_isConnected || _stream == null || !_stream.CanWrite) // Перевірка перед записом
                    {
                        throw new IOException("З'єднання втрачено перед надсиланням запиту.");
                    }

                    Console.WriteLine($"[UpdateBookingsAsync] Надсилаємо запит: {jsonRequest}");
                    // 1. Надсилаємо довжину
                    await _stream.WriteAsync(lengthBytes, 0, lengthBytes.Length);
                    // 2. Надсилаємо дані
                    await _stream.WriteAsync(requestBytes, 0, requestBytes.Length);
                    await _stream.FlushAsync(); // Переконуємося, що дані надіслано

                    // 3. Читаємо довжину відповіді
                    byte[] lengthBuffer = new byte[4];
                    int bytesReadLength = await ReadWithTimeoutAsync(_stream, lengthBuffer, 0, 4, TimeSpan.FromSeconds(10));

                    if (bytesReadLength < 4)
                    {
                        throw new IOException($"Не вдалося прочитати довжину відповіді (прочитано {bytesReadLength} байт).");
                    }

                    int responseLength = BitConverter.ToInt32(lengthBuffer, 0);
                    Console.WriteLine($"[UpdateBookingsAsync] Очікувана довжина відповіді: {responseLength}");

                    if (responseLength <= 0 || responseLength > 10 * 1024 * 1024) // Ліміт 10 MB
                    {
                        throw new IOException($"Отримана некоректна довжина відповіді: {responseLength}.");
                    }

                    // 4. Читаємо тіло відповіді
                    byte[] responseBuffer = new byte[responseLength];
                    int totalBytesRead = 0;
                    TimeSpan readTimeout = TimeSpan.FromSeconds(15); // Загальний таймаут на читання тіла
                    using (var readCts = new CancellationTokenSource(readTimeout))
                    {
                        while (totalBytesRead < responseLength)
                        {
                            if (!_isConnected || _stream == null || !_stream.CanRead) // Перевірка перед читанням
                            {
                                throw new IOException("З'єднання втрачено під час читання відповіді.");
                            }
                            int bytesRead = await _stream.ReadAsync(responseBuffer, totalBytesRead, responseLength - totalBytesRead, readCts.Token);
                            if (bytesRead == 0)
                            {
                                // Сервер закрив з'єднання
                                throw new IOException($"З'єднання закрито сервером під час читання тіла відповіді (прочитано {totalBytesRead}/{responseLength}).");
                            }
                            totalBytesRead += bytesRead;
                        }
                    }

                    jsonResponse = Encoding.UTF8.GetString(responseBuffer, 0, totalBytesRead);
                    Console.WriteLine($"[UpdateBookingsAsync] Отримана відповідь: {jsonResponse}");
                }
                catch (IOException ex) // Обробка помилок IO (включаючи таймаути з ReadWithTimeoutAsync)
                {
                    Console.WriteLine($"[UpdateBookingsAsync] Помилка IO під час комунікації: {ex.Message}");
                    Device.BeginInvokeOnMainThread(async () =>
                    {
                        await DisplayAlert("Помилка зв'язку", $"Помилка мережі: {ex.Message}", "OK");
                    });
                    Disconnect(); // Вважаємо з'єднання втраченим
                    _isConnected = false;
                    return; // Вихід з оновлення
                }
                catch (Exception ex) // Інші неочікувані помилки під час комунікації
                {
                    Console.WriteLine($"[UpdateBookingsAsync] Неочікувана помилка під час комунікації: {ex.Message}\n{ex.StackTrace}");
                    Device.BeginInvokeOnMainThread(async () =>
                    {
                        await DisplayAlert("Помилка", $"Загальна помилка: {ex.Message}", "OK");
                    });
                    Disconnect(); // Вважаємо з'єднання втраченим
                    _isConnected = false;
                    return; // Вихід з оновлення
                }
                finally
                {
                    _asyncLock.Release();
                }


                // Обробка отриманої відповіді (поза блоком _asyncLock)
                List<BookingItem> fetchedItems = new List<BookingItem>();
                try
                {
                    JToken responseToken = JToken.Parse(jsonResponse);

                    if (responseToken.Type == JTokenType.Array) // Очікуваний список бронювань
                    {
                        JArray results = (JArray)responseToken;

                        foreach (JObject result in results.OfType<JObject>())
                        {
                            // Додамо перевірки на наявність ключів
                            int sessionId = result.Value<int?>("session_id") ?? 0; // <--- Отримуємо session_id
                            string clientName = result.Value<string>("client_name") ?? "N/A";
                            string startTimeStr = result.Value<string>("start_time");
                            string endTimeStr = result.Value<string>("end_time");
                            string sessionType = result.Value<string>("session_type") ?? "N/A";
                            int numPeople = result.Value<int?>("num_people") ?? 0;
                            string sessionDateStr = result.Value<string>("session_date");
                            string paymentStatus = result.Value<string>("payment_status") ?? "Статус невідомий";
                            // Додайте інші поля, якщо вони потрібні для детальної сторінки (наприклад, phone_number, notes)
                            string phoneNumber = result.Value<string>("phone_number") ?? "";
                            string notes = result.Value<string>("notes") ?? "";


                            // Більш надійне парсингування дати та часу
                            DateTime sessionDate = DateTime.TryParse(sessionDateStr, out var date) ? date : DateTime.MinValue;
                            // Парсинг часу з припущенням, що сервер повертає повну дату-час або лише час HH:mm:ss
                            // Спробуємо ParseExact для HH:mm:ss, інакше звичайний TryParse
                            string parsedStartTime = TimeSpan.TryParse(startTimeStr, out var stTimeSpan) ? stTimeSpan.ToString(@"hh\:mm")
                                                       : DateTime.TryParse(startTimeStr, out var stDateTime) ? stDateTime.ToString(@"HH:mm") : "??:??";
                            string parsedEndTime = TimeSpan.TryParse(endTimeStr, out var etTimeSpan) ? etTimeSpan.ToString(@"hh\:mm")
                                                     : DateTime.TryParse(endTimeStr, out var etDateTime) ? etDateTime.ToString(@"HH:mm") : "??:??";


                            fetchedItems.Add(new BookingItem
                            {
                                SessionId = sessionId, // <--- Присвоюємо session_id
                                ClientName = clientName,
                                StartTime = parsedStartTime, // Використовуємо форматований час
                                EndTime = parsedEndTime,   // Використовуємо форматований час
                                SessionType = sessionType,
                                NumPeople = numPeople,
                                SessionDate = sessionDate,
                                IsFreeZone = false, // Це реальне бронювання
                                PaymentStatus = paymentStatus
                                // Можливо, варто додати phone_number та notes до BookingItem,
                                // якщо вони потрібні для відображення на сторінці деталей без додаткового запиту
                                // PhoneNumber = phoneNumber,
                                // Notes = notes
                            });
                        }
                    }
                    else
                    {
                        // Неочікуваний тип токена
                        Console.WriteLine($"[UpdateBookingsAsync] Неочікуваний тип відповіді від сервера: {responseToken.Type}");
                        Device.BeginInvokeOnMainThread(async () =>
                        {
                            await DisplayAlert("Помилка даних", $"Неочікуваний формат даних від сервера. Отримано {responseToken.Type}.", "OK");
                        });
                        // Можливо, тут варто очистити список
                        Device.BeginInvokeOnMainThread(() => _bookingItemsInternal.Clear());
                        return; // Вихід
                    }

                }
                catch (JsonException ex)
                {
                    Console.WriteLine($"[UpdateBookingsAsync] Помилка парсингу JSON: {ex.Message}");
                    Device.BeginInvokeOnMainThread(async () =>
                    {
                        await DisplayAlert("Помилка даних", $"Некоректна відповідь від сервера: {ex.Message}", "OK");
                    });
                    // Можливо, тут варто очистити список
                    Device.BeginInvokeOnMainThread(() => _bookingItemsInternal.Clear());
                    return; // Не оновлюємо список, якщо дані невірні
                }
                catch (Exception ex) // Загальна помилка при обробці відповіді
                {
                    Console.WriteLine($"[UpdateBookingsAsync] Загальна помилка при обробці відповіді: {ex.Message}\n{ex.StackTrace}");
                    Device.BeginInvokeOnMainThread(async () =>
                    {
                        await DisplayAlert("Помилка обробки даних", $"Не вдалося обробити відповідь сервера: {ex.Message}", "OK");
                    });
                    // Можливо, тут варто очистити список
                    Device.BeginInvokeOnMainThread(() => _bookingItemsInternal.Clear());
                    return; // Вихід
                }


                // Логіка додавання вільних зон (винесена для читабельності)
                AddFreeZoneItems(fetchedItems, startTime, endTime);

                // Сортування перед оновленням ObservableCollection
                var sortedItems = fetchedItems.OrderBy(item => item.IsFreeZone)
                                              .ThenBy(item => item.SessionType) // Додаткове сортування для стабільності
                                              .ThenBy(item => item.StartTime) // Додано сортування за часом
                                              .ToList();

                // Оновлення ObservableCollection в головному потоці
                Device.BeginInvokeOnMainThread(() =>
                {
                    _bookingItemsInternal.Clear();
                    foreach (var item in sortedItems)
                    {
                        _bookingItemsInternal.Add(item);
                    }
                    // Оновлюємо мітку часу, якщо потрібно (хоча вона статична в цьому випадку)
                    OnPropertyChanged(nameof(BookingTimeLabel));
                });

            }
            catch (Exception ex) // Загальний обробник помилок на рівні UpdateBookingsAsync
            {
                Console.WriteLine($"[UpdateBookingsAsync] Загальна помилка: {ex.Message}\n{ex.StackTrace}");
                Device.BeginInvokeOnMainThread(async () =>
                {
                    await DisplayAlert("Помилка", $"Не вдалося оновити бронювання: {ex.Message}", "OK");
                });
                // Тут не робимо Disconnect, бо помилка могла бути не пов'язана з мережею
            }
            finally
            {
                _isUpdating = false; // Дозволяємо наступне оновлення
            }
        }
        // Звільнення ресурсів при закритті сторінки
        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            Console.WriteLine("[OnDisappearing] Сторінка закривається, зупиняємо таймер та з'єднання.");
            // Зупиняємо таймер
            _updateTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            _updateTimer?.Dispose();
            _updateTimer = null;

            // Закриваємо з'єднання у фоновому потоці, щоб не блокувати UI
            Task.Run(() => Disconnect());

            // Звільняємо семафор
            _asyncLock?.Dispose();
        }

        // Видалено непотрібні серверні методи SendJsonResponse, SendErrorResponse, SendSuccessResponse

        // Клас моделі даних (без змін)
        public class BookingItem
        {
            public int SessionId { get; set; }
            public string ClientName { get; set; }
            public string StartTime { get; set; } // Зберігаємо як рядок HH:mm
            public string EndTime { get; set; }   // Зберігаємо як рядок HH:mm
            public string SessionType { get; set; }
            public int NumPeople { get; set; }
            public DateTime SessionDate { get; set; } // Зберігаємо повну дату
            public bool IsFreeZone { get; set; }
            public string PaymentStatus { get; set; }

            // Додаткова властивість для відображення в UI (приклад)
            public string DisplayText => IsFreeZone ? $"{SessionType} - Вільна зона" : $"{ClientName} ({SessionType} x{NumPeople})";
            public Color BackgroundColor => IsFreeZone ? Color.LightGreen : Color.White; // Приклад для візуалізації
            public Color PaymentStatusColor
            {
                get
                {
                    // Приклад логіки кольору за статусом
                    if (IsFreeZone) return Color.Transparent; // Для вільних зон не показуємо статус
                    if (string.IsNullOrEmpty(PaymentStatus) || PaymentStatus.Equals("Статус невідомий", StringComparison.OrdinalIgnoreCase)) return Color.Gray;
                    if (PaymentStatus.Equals("Paid", StringComparison.OrdinalIgnoreCase)) return Color.LightGreen;
                    if (PaymentStatus.Equals("Unpaid", StringComparison.OrdinalIgnoreCase) || PaymentStatus.Equals("Pending", StringComparison.OrdinalIgnoreCase)) return Color.OrangeRed;
                    // Додайте інші статуси за потреби
                    return Color.LightGray; // Колір за замовчуванням для інших статусів
                }
            }
            public string LocalizedPaymentStatus
            {
                get
                {
                    // Якщо це вільна зона, статус оплати неактуальний
                    if (IsFreeZone) return "";

                    // Перевіряємо сирий статус PaymentStatus (без урахування регістру)
                    if (string.IsNullOrEmpty(PaymentStatus))
                    {
                        return "Статус невідомий";
                    }

                    switch (PaymentStatus.Trim().ToLowerInvariant()) // Використовуємо Trim та ToLowerInvariant для стійкості
                    {
                        case "paid":
                            return "оплачено";
                        case "pending":
                            return "очікує оплати";
                        case "unpaid":
                            return "не оплачено"; // Можливо, є й такий статус
                                                  // Додайте інші статуси, які може повертати ваш сервер
                                                  // case "cancelled":
                                                  //     return "скасовано";
                        default:
                            return "Статус невідомий"; // Для будь-яких інших невідомих статусів
                    }
                }
            }
        }
    }
}