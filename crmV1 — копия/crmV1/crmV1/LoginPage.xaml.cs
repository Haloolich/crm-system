using System;
using Xamarin.Forms;
using System.Net.Sockets;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xamarin.Essentials;
using Newtonsoft.Json.Linq; // Додано для JObject

namespace crmV1
{
    public partial class LoginPage : ContentPage
    {
        private TcpClient _client;
        private NetworkStream _stream;
        private bool _isConnected = false;
        public string _serverIp { get; private set; } // Значення за замовчуванням

        private const string ServerIpPreferenceKey = "ServerIP";


        public LoginPage()
        {
            InitializeComponent();
            // Встановлюємо значення _serverIp з Preferences або використовуємо значення за замовчуванням з AppConfig
            _serverIp = Preferences.Get(ServerIpPreferenceKey, AppConfig.ServerIP); // Використовуємо AppConfig.ServerIP як значення за замовчуванням, якщо нічого немає в Preferences
            if (string.IsNullOrEmpty(_serverIp))
            {
                // Якщо навіть у AppConfig нічого немає (наприклад, перший запуск), встановіть IP за замовчуванням
                _serverIp = "192.168.0.101"; // Або інший IP за замовчуванням
                Preferences.Set(ServerIpPreferenceKey, _serverIp); // Зберегти в Preferences
            }
            // Оновлюємо AppConfig.ServerIP на випадок, якщо він був прочитаний з Preferences
            AppConfig.ServerIP = _serverIp;

            ConnectToServerAsync();
            NavigationPage.SetHasNavigationBar(this, false);
        }

        private async Task ConnectToServerAsync()
        {
            errorMessageLabel.Text = $"Спроба підключення до {_serverIp}...";
            errorMessageLabel.IsVisible = true;
            errorMessageLabel.TextColor = Color.Orange;

            try
            {
                // Якщо клієнт існує і підключений, не намагаємось підключитися знову
                if (_client != null && _client.Connected && _stream != null)
                {
                    errorMessageLabel.Text = "З'єднано з сервером.";
                    errorMessageLabel.TextColor = Color.Green;
                    await Task.Delay(2000);
                    errorMessageLabel.IsVisible = false;
                    _isConnected = true; // Переконаємось, що прапорець встановлено
                    return; // Вже підключено, виходимо
                }

                // Закриваємо попередні ресурси, якщо вони є і не закриті коректно
                DisconnectFromServer();

                _client = new TcpClient();
                // Встановлюємо таймаут підключення
                var connectTask = _client.ConnectAsync(_serverIp, 8888);
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(10)); // Таймаут 10 секунд

                var completedTask = await Task.WhenAny(connectTask, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    // Таймаут підключення
                    Console.WriteLine($"[LoginPage] Таймаут підключення до {_serverIp}:8888.");
                    throw new TimeoutException($"Не вдалося підключитися до сервера за 10 секунд.");
                }

                // Перевіряємо, чи була помилка під час підключення
                if (connectTask.IsFaulted)
                {
                    throw connectTask.Exception.InnerException ?? connectTask.Exception; // Перекидаємо реальний виняток
                }

                // Підключення успішне
                _stream = _client.GetStream();
                _isConnected = true;

                Console.WriteLine("З'єднання з сервером встановлено.");
                errorMessageLabel.Text = "З'єднано з сервером.";
                errorMessageLabel.TextColor = Color.Green;
                await Task.Delay(2000);
                if (_isConnected) // Перевіряємо ще раз, бо статус міг змінитися
                    errorMessageLabel.IsVisible = false;
            }
            catch (TimeoutException tex)
            {
                Console.WriteLine($"[LoginPage] Помилка підключення (Таймаут): {tex.Message}");
                errorMessageLabel.Text = $"Не вдалося підключитися до сервера ({_serverIp}): Таймаут.";
                errorMessageLabel.TextColor = Color.Red;
                errorMessageLabel.IsVisible = true;
                _isConnected = false;
                DisconnectFromServer(); // Переконуємось, що ресурси звільнені
            }
            catch (SocketException sex)
            {
                Console.WriteLine($"[LoginPage] Помилка підключення (SocketException): {sex.Message}");
                errorMessageLabel.Text = $"Не вдалося підключитися до сервера ({_serverIp}): {sex.Message}";
                errorMessageLabel.TextColor = Color.Red;
                errorMessageLabel.IsVisible = true;
                _isConnected = false;
                DisconnectFromServer();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LoginPage] Загальна помилка під час підключення: {ex.Message}");
                errorMessageLabel.Text = $"Не вдалося підключитися до сервера ({_serverIp}). Перевірте IP адресу (Help).";
                errorMessageLabel.TextColor = Color.Red;
                errorMessageLabel.IsVisible = true;
                _isConnected = false;
                DisconnectFromServer();
            }
        }

        async void OnHelpClicked(object sender, EventArgs e)
        {
            string newIpAddress = await DisplayPromptAsync(
                "Змінити IP адресу", "Введіть нову IP адресу сервера:", "OK", "Скасувати",
                placeholder: _serverIp, initialValue: _serverIp, keyboard: Keyboard.Url
            );

            if (!string.IsNullOrWhiteSpace(newIpAddress) && newIpAddress != _serverIp)
            {
                // Проста валідація IP (можна покращити)
                if (!System.Net.IPAddress.TryParse(newIpAddress, out _))
                {
                    await DisplayAlert("Помилка", "Невірний формат IP адреси", "OK");
                    return;
                }

                _serverIp = newIpAddress;
                Preferences.Set(ServerIpPreferenceKey, _serverIp);
                AppConfig.ServerIP = _serverIp; // Оновлюємо AppConfig
                await DisplayAlert("Інфо", $"IP адресу оновлено на: {_serverIp}. Спроба перепідключення...", "OK");
               DisconnectFromServer(); // Закриваємо старе з'єднання перед новим
                await ConnectToServerAsync(); // Спробувати підключитися до нового IP
            }
        }

        async void OnLoginClicked(object sender, EventArgs e)
        {
            // AppConfig.ServerIP вже встановлено в конструкторі та OnHelpClicked

            if (!_isConnected || _stream == null || !_stream.CanWrite) // Додано перевірку CanWrite
            {
                errorMessageLabel.Text = $"Немає активного з'єднання з сервером ({_serverIp}).";
                errorMessageLabel.IsVisible = true;
                errorMessageLabel.TextColor = Color.Red;
                // Можливо, спробувати перепідключитися тут
                await ConnectToServerAsync();
                if (!_isConnected) return; // Якщо перепідключення не вдалося
            }

            string login = loginEntry.Text;
            string password = passwordEntry.Text;

            if (string.IsNullOrEmpty(login) || string.IsNullOrEmpty(password))
            {
                errorMessageLabel.Text = "Будь ласка, введіть логін і пароль.";
                errorMessageLabel.TextColor = Color.Red;
                errorMessageLabel.IsVisible = true;
                return;
            }

            errorMessageLabel.Text = "Авторизація...";
            errorMessageLabel.TextColor = Color.Orange;
            errorMessageLabel.IsVisible = true;


            // Дані для відправки залишаються <string, string>
            var dataToSend = new Dictionary<string, string>
            {
                { "action", "login" }, // Додаємо action сюди
                { "login", login },
                { "password", password }
            };

            // SendDataToServerAsync повертає Dictionary<string, object>
            Dictionary<string, object> response = await SendDataToServerAsync(dataToSend);

            if (response == null)
            {
                // Помилка вже була показана в SendDataToServerAsync
                // errorMessageLabel.Text вже встановлено
                // errorMessageLabel.IsVisible = true;
                // errorMessageLabel.TextColor = Color.Red;
                return; // Виходимо, якщо відповідь null (означає помилку зв'язку або парсингу)
            }

            // --- Обробка Dictionary<string, object> ---
            bool isSuccess = false;
            string message = "Невідома помилка авторизації."; // Значення за замовчуванням для повідомлення про помилку

            // Безпечно отримуємо значення "success"
            if (response.TryGetValue("success", out object successValue) && successValue != null)
            {
                // Сервер надсилає "true"/"false" як рядки, тому порівнюємо з рядком
                isSuccess = successValue.ToString().ToLower() == "true";
            }

            // Безпечно отримуємо значення "message"
            if (response.TryGetValue("message", out object messageValue) && messageValue != null)
            {
                message = messageValue.ToString(); // Використовуємо повідомлення від сервера, якщо є
            }


            if (isSuccess)
            {
                // Авторизація успішна! Тепер безпечно отримуємо manager_id та club_id
                int managerId = -1; // Значення за замовчуванням, якщо ID не знайдено
                int clubId = -1;    // Значення за замовчуванням
                string userRole = "manager";

                bool gotManagerId = false;
                bool gotClubId = false;

                // Спроба отримати та розпарсити manager_id
                if (response.TryGetValue("manager_id", out object managerIdValue) && managerIdValue != null)
                {
                    if (int.TryParse(managerIdValue.ToString(), out managerId))
                    {
                        gotManagerId = true;
                        Console.WriteLine($"Отримано manager_id: {managerId}");
                    }
                    else
                    {
                        Console.WriteLine($"Помилка парсингу manager_id: '{managerIdValue}'");
                    }
                }
                else
                {
                    Console.WriteLine("Ключ 'manager_id' відсутній у відповіді.");
                }


                // --- ДОДАНО: Спроба отримати та розпарсити club_id ---
                if (response.TryGetValue("club_id", out object clubIdValue) && clubIdValue != null)
                {
                    if (int.TryParse(clubIdValue.ToString(), out clubId))
                    {
                        gotClubId = true;
                        Console.WriteLine($"Отримано club_id: {clubId}");
                    }
                    else
                    {
                        Console.WriteLine($"Помилка парсингу club_id: '{clubIdValue}'");
                    }
                }
                else
                {
                    Console.WriteLine("Ключ 'club_id' відсутній у відповіді.");
                }
                // ----------------------------------------------------

                if (response.TryGetValue("role", out object roleValue) && roleValue != null)
                {
                    userRole = roleValue.ToString();
                    Console.WriteLine($"Отримано роль: {userRole}");
                }
                else
                {
                    Console.WriteLine("Ключ 'role' відсутній у відповіді або значення null. Використовуємо роль за замовчуванням: manager.");
                }

                // Перевіряємо, чи обидва ID отримані успішно
                if (gotManagerId && gotClubId)
                {
                   // await DisplayAlert("Успіх", message, "OK");

                    loginEntry.Text = string.Empty;
                    passwordEntry.Text = string.Empty;
                    errorMessageLabel.IsVisible = false; // Приховуємо повідомлення про помилку

                    // --- ЗМІНЕНО ЦЕЙ РЯДОК (рядок 145 або близько того): Переходимо на MainPage і передаємо обидва ID ---
                    // Замінюємо кореневу сторінку на NavigationPage з MainPage, щоб скинути стек навігації
                    Application.Current.MainPage = new NavigationPage(new MainPage(managerId, clubId, userRole));
                    // -------------------------------------------------------------

                    // Після успішного логіну і переходу на іншу сторінку, це з'єднання можна розірвати
                    // якщо наступні сторінки встановлюють свої з'єднання або якщо це з'єднання більше не потрібне.
                    // Якщо ж з'єднання _client і _stream потрібне для RegisterPage або інших сторінок
                    // без повторного підключення, то disconnect не викликаємо.
                    // Судячи з RegisterPage(_client, _stream), ви передаєте це з'єднання, тому НЕ розриваємо його тут.
                    // DisconnectFromServer(); // <- Якщо з'єднання потрібне, цей рядок закоментований або видалений.

                }
                else
                {
                    // Успіх=true, але ID відсутні або некоректні у відповіді сервера
                    Console.WriteLine("Відповідь сервера успішна, але ID менеджера або клубу відсутні/некоректні.");
                    errorMessageLabel.Text = message + "\nВідсутні ID менеджера або клубу у відповіді сервера."; // Доповнюємо повідомлення сервера
                    errorMessageLabel.TextColor = Color.Red;
                    errorMessageLabel.IsVisible = true;
                    // Не розриваємо з'єднання, бо воно, можливо, потрібне для RegisterPage
                }
            }
            else // Якщо isSuccess == false (логін невдалий)
            {
                // Повідомлення про помилку вже присвоєно змінній `message`
                errorMessageLabel.Text = message;
                errorMessageLabel.TextColor = Color.Red;
                errorMessageLabel.IsVisible = true;
                Console.WriteLine($"Помилка авторизації: {message}");
                // Не розриваємо з'єднання при помилці логіну, щоб користувач міг спробувати ще раз
                // або перейти на сторінку реєстрації з існуючим з'єднанням.
            }
        }

        // --- SendDataToServerAsync тепер коректно десеріалізує в Dictionary<string, object> ---
        private async Task<Dictionary<string, object>> SendDataToServerAsync(Dictionary<string, string> data)
        {
            // Перевірки на null та CanWrite вже є на початку OnLoginClicked
            // Але додамо їх і тут для безпеки, якщо метод викликається з інших місць
            if (!_isConnected || _stream == null || !_stream.CanWrite)
            {
                Console.WriteLine("[SendDataToServerAsync] Немає активного з'єднання.");
                // errorMessageLabel оновлюється в OnLoginClicked або ConnectToServerAsync
                return null;
            }

            try
            {
                // Дані для відправки - це Dictionary<string, string>
                string jsonData = JsonConvert.SerializeObject(data);
                byte[] buffer = Encoding.UTF8.GetBytes(jsonData);
                byte[] lengthBytes = BitConverter.GetBytes(buffer.Length);

                // Console.WriteLine($"[Client] Sending Length: {buffer.Length}"); // Забагато логів
                Console.WriteLine($"[Client] Sending JSON: {jsonData}"); // Важливо для відладки

                await _stream.WriteAsync(lengthBytes, 0, lengthBytes.Length);
                await _stream.WriteAsync(buffer, 0, buffer.Length);
                await _stream.FlushAsync(); // Використовуємо асинхронний варіант

                // Отримання відповіді
                byte[] lengthBuffer = new byte[4];
                // Додамо таймаут для читання довжини
                int bytesRead = await ReadWithTimeoutAsync(_stream, lengthBuffer, 0, 4, TimeSpan.FromSeconds(10)); // Таймаут 10 сек на довжину

                if (bytesRead < 4) // Перевіряємо < 4, бо може бути прочитано 0 байт
                {
                    if (bytesRead == 0)
                    {
                        Console.WriteLine("[Client] Сервер закрив з'єднання під час очікування довжини відповіді.");
                        errorMessageLabel.Text = "З'єднання з сервером втрачено під час очікування відповіді.";
                        errorMessageLabel.TextColor = Color.Red;
                        errorMessageLabel.IsVisible = true;
                        DisconnectFromServer(); // Розрив з'єднання
                        return null;
                    }
                    Console.WriteLine($"[Client] Не вдалося прочитати повну довжину відповіді (прочитано {bytesRead}/4).");
                    errorMessageLabel.Text = "Неповні дані довжини відповіді від сервера.";
                    errorMessageLabel.TextColor = Color.Red;
                    errorMessageLabel.IsVisible = true;
                    DisconnectFromServer(); // Некоректні дані -> розрив з'єднання
                    return null;
                }

                int responseLength = BitConverter.ToInt32(lengthBuffer, 0);
                Console.WriteLine($"[Client] Received Response Length: {responseLength}");

                if (responseLength <= 0 || responseLength > 10 * 1024 * 1024) // Додано ліміт розміру
                {
                    Console.WriteLine($"[Client] Сервер надіслав некоректну довжину відповіді: {responseLength}");
                    errorMessageLabel.Text = $"Сервер надіслав некоректну відповідь (довжина: {responseLength}).";
                    errorMessageLabel.TextColor = Color.Red;
                    errorMessageLabel.IsVisible = true;
                    DisconnectFromServer(); // Некоректні дані -> розрив з'єднання
                    return null;
                }

                byte[] responseBuffer = new byte[responseLength];
                int totalBytesRead = 0;
                // Додамо таймаут для читання тіла відповіді
                TimeSpan bodyReadTimeout = TimeSpan.FromSeconds(20); // Таймаут 20 секунд на тіло відповіді
                using (var readCts = new System.Threading.CancellationTokenSource(bodyReadTimeout))
                {
                    while (totalBytesRead < responseLength)
                    {
                        if (!_isConnected || _stream == null || !_stream.CanRead)
                        {
                            Console.WriteLine("[Client] З'єднання втрачено під час читання тіла відповіді.");
                            errorMessageLabel.Text = "З'єднання втрачено під час отримання відповіді.";
                            errorMessageLabel.TextColor = Color.Red;
                            errorMessageLabel.IsVisible = true;
                            DisconnectFromServer();
                            return null;
                        }
                        bytesRead = await _stream.ReadAsync(responseBuffer, totalBytesRead, responseLength - totalBytesRead, readCts.Token);
                        if (bytesRead == 0)
                        {
                            Console.WriteLine($"[Client] Сервер закрив з'єднання під час читання тіла відповіді (прочитано {totalBytesRead}/{responseLength}).");
                            errorMessageLabel.Text = "З'єднання з сервером закрито під час отримання відповіді.";
                            errorMessageLabel.TextColor = Color.Red;
                            errorMessageLabel.IsVisible = true;
                            DisconnectFromServer();
                            return null;
                        }
                        totalBytesRead += bytesRead;
                    }
                }


                string responseJson = Encoding.UTF8.GetString(responseBuffer, 0, totalBytesRead);
                Console.WriteLine($"[Client] Received JSON: {responseJson}");

                // --- Десеріалізуємо в Dictionary<string, object> ---
                try
                {
                    var responseObject = JsonConvert.DeserializeObject<Dictionary<string, object>>(responseJson);
                    return responseObject;
                }
                catch (JsonException jsonEx)
                {
                    Console.WriteLine($"[Client] Помилка десеріалізації JSON: {jsonEx.Message}. JSON: {responseJson}");
                    errorMessageLabel.Text = "Помилка обробки відповіді від сервера. Некоректний JSON формат.";
                    errorMessageLabel.TextColor = Color.Red;
                    errorMessageLabel.IsVisible = true;
                    // При помилці парсингу JSON не обов'язково розривати з'єднання
                    return null;
                }

            }
            catch (TimeoutException tex) // Обробка таймаутів з ReadWithTimeoutAsync
            {
                Console.WriteLine($"[Client] Таймаут читання відповіді: {tex.Message}");
                errorMessageLabel.Text = $"Таймаут отримання відповіді від сервера: {tex.Message}";
                errorMessageLabel.TextColor = Color.Red;
                errorMessageLabel.IsVisible = true;
                DisconnectFromServer(); // При таймауті з'єднання, ймовірно, не в порядку
                return null;
            }
            catch (IOException ioEx)
            {
                Console.WriteLine($"[Client] Помилка IO при обміні даними: {ioEx.Message}");
                errorMessageLabel.Text = $"Помилка мережі: {ioEx.Message}";
                errorMessageLabel.TextColor = Color.Red;
                errorMessageLabel.IsVisible = true;
                DisconnectFromServer(); // При помилці IO з'єднання втрачене
                return null;
            }
            catch (ObjectDisposedException odEx)
            {
                Console.WriteLine($"[Client] Помилка: Спроба використати закритий потік/клієнт. {odEx.Message}");
                errorMessageLabel.Text = "З'єднання було закрито.";
                errorMessageLabel.TextColor = Color.Red;
                errorMessageLabel.IsVisible = true;
                _isConnected = false; // Оновити статус
                _stream = null;
                _client = null;
                // Не викликаємо DisconnectFromServer(), бо ресурси вже disposed
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Client] Загальна помилка при обміні даними: {ex.GetType().Name} - {ex.Message}");
                errorMessageLabel.Text = $"Помилка: {ex.Message}";
                errorMessageLabel.TextColor = Color.Red;
                errorMessageLabel.IsVisible = true;
                DisconnectFromServer(); // При неочікуваній помилці також краще розірвати з'єднання
                return null;
            }
        }

        // Допоміжний метод для читання з таймаутом (скопійовано з CurrentBookingsPage)
        private async Task<int> ReadWithTimeoutAsync(NetworkStream stream, byte[] buffer, int offset, int count, TimeSpan timeout)
        {
            if (stream == null || !stream.CanRead)
            {
                Console.WriteLine($"[LoginPage][ReadWithTimeoutAsync] Помилка: Потік null або не для читання.");
                // Не кидаємо виняток тут, повертаємо 0, щоб викликаючий метод обробив це як помилку читання
                return 0; // Сигналізуємо про проблему читання
            }

            using (var cts = new System.Threading.CancellationTokenSource(timeout))
            {
                try
                {
                    return await stream.ReadAsync(buffer, offset, count, cts.Token);
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine($"[LoginPage][ReadWithTimeoutAsync] Таймаут ({timeout.TotalSeconds} сек) читання даних.");
                    throw new TimeoutException($"Таймаут ({timeout.TotalSeconds} сек) читання даних з сервера."); // Кидаємо TimeoutException
                }
                catch (ObjectDisposedException ode)
                {
                    Console.WriteLine($"[LoginPage][ReadWithTimeoutAsync] Помилка: Спроба читання з закритого потоку. {ode.Message}");
                    // Вважаємо це розривом з'єднання
                    _isConnected = false; // Оновлюємо статус
                    throw; // Перекидаємо ObjectDisposedException
                }
                catch (IOException ioEx) // Часто виникає при розриві з'єднання
                {
                    Console.WriteLine($"[LoginPage][ReadWithTimeoutAsync] Помилка IO: {ioEx.Message}");
                    // Вважаємо це розривом з'єднання
                    _isConnected = false; // Оновлюємо статус
                    throw; // Перекидаємо IOException
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[LoginPage][ReadWithTimeoutAsync] Неочікувана помилка: {ex.GetType().Name} - {ex.Message}");
                    _isConnected = false; // Також вважаємо розривом
                    throw new Exception($"Неочікувана помилка під час читання: {ex.Message}", ex); // Перекидаємо загальний виняток
                }
            }
        }


        private void DisconnectFromServer()
        {
            // Перевіряємо статус _isConnected, а не _client != null && _client.Connected,
            // оскільки статус може бути оновлений раніше при помилці
            if (!_isConnected && (_client == null || !_client.Connected))
            {
                Console.WriteLine("[Client] З'єднання вже розірвано або не встановлено.");
                return; // З'єднання вже не активне
            }

            _isConnected = false; // Встановлюємо статус перед закриттям
            Console.WriteLine("[Client] Роз'єднання з сервером...");
            try
            {
                // Закриваємо спочатку потік, потім клієнт
                _stream?.Close();
                _stream?.Dispose(); // Переконуємось, що ресурси звільнені
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Client] Помилка при закритті потоку: {ex.Message}");
            }
            finally
            {
                _stream = null; // Обнуляємо посилання
            }

            try
            {
                _client?.Close();
                _client?.Dispose(); // Переконуємось, що ресурси звільнені
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Client] Помилка при закритті клієнта: {ex.Message}");
            }
            finally
            {
                _client = null; // Обнуляємо посилання
            }

            Console.WriteLine("[Client] З'єднання закрито.");
        }
        async void OnRegisterClicked(object sender, EventArgs e)
        {
            if (!_isConnected)
            {
                errorMessageLabel.Text = $"Немає з'єднання з сервером ({_serverIp}) для реєстрації. Перевірте IP адресу (Help).";
                errorMessageLabel.IsVisible = true;
                errorMessageLabel.TextColor = Color.Red;
                return;
            }
            // Передаємо активне з'єднання на сторінку реєстрації
            await Navigation.PushAsync(new RegistrationPage(_client, _stream));
        }
        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            Console.WriteLine("[LoginPage] Сторінка закривається. Розриваємо з'єднання.");
            // Розриваємо з'єднання при переході з LoginPage
            //DisconnectFromServer();
        }
    }
    public static class AppConfig
    {
        // Ініціалізуємо значення за замовчуванням, яке може бути змінене
        public static string ServerIP { get; set; } = "192.168.0.101"; // IP за замовчуванням
    }
}