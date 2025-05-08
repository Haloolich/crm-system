using MySqlConnector; // Припускаємо, що використовується
using Newtonsoft.Json; // Припускаємо, що використовується
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading; // Додано для CancellationTokenSource
using System.Threading.Tasks;

namespace ConsoleBookingApp
{

    class Program
    {
        private static TcpListener _server = null;
        private static AppSettings _settings = new AppSettings(); // Завантажуємо налаштування один раз
        private static IPAddress _currentIpAddress;
        private static CancellationTokenSource _cts = new CancellationTokenSource();
        private static bool _restartRequested = false;
        private static IPAddress _newIpAddress = null;
        private static bool _shutdownRequested = false; // Додано для чистого виходу

        static async Task Main(string[] args)
        {
            // Встановлення початкової IP-адреси
            if (!IPAddress.TryParse(_settings.DefaultIpAddress, out _currentIpAddress))
            {
                Console.WriteLine($"[Помилка] Неправильна IP-адреса за замовчуванням у налаштуваннях: {_settings.DefaultIpAddress}. Використовується Loopback (127.0.0.1).");
                _currentIpAddress = IPAddress.Loopback; // Запасний варіант
            }

            Console.OutputEncoding = System.Text.Encoding.UTF8; // Для коректного відображення українських літер в консолі
            Console.InputEncoding = System.Text.Encoding.UTF8;

            Console.WriteLine("Запуск сервера...");
            Console.WriteLine("Введіть 'ipchange' щоб змінити IP адресу прослуховування.");
            Console.WriteLine("Введіть 'status' щоб переглянути поточний стан слухача.");
            Console.WriteLine("Введіть 'quit' або 'exit' щоб зупинити сервер.");

            // Запускаємо обробник консольних команд в окремому завданні
            _ = Task.Run(HandleConsoleCommands);

            // Основний цикл сервера (дозволяє перезапуск)
            while (!_shutdownRequested)
            {
                _restartRequested = false;
                _newIpAddress = null;
                // Створюємо новий CTS для кожного запуску (якщо попередній був скасований)
                // Якщо попередній cts не був скасований, його можна перевикористати, але безпечніше створити новий
                if (_cts.IsCancellationRequested)
                {
                    _cts.Dispose(); // Звільняємо старий
                    _cts = new CancellationTokenSource(); // Створюємо новий
                }

                // Спроба запустити сервер
                if (!await StartServerAsync(_currentIpAddress, _settings.Port))
                {
                    // *** ЗМІНЕНО: Немає автоматичного повтору кожні 10 сек ***
                    Console.WriteLine($"[Помилка] Не вдалося запустити сервер на {_currentIpAddress}:{_settings.Port}.");
                    Console.WriteLine("Перевірте IP/Порт та дозволи системи.");
                    Console.WriteLine("Сервер НЕ запущено. Очікування команд ('ipchange', 'quit')...");

                    // Чекаємо, доки користувач не введе 'quit' або успішний 'ipchange'
                    while (!_shutdownRequested && !_restartRequested)
                    {
                        // Невелике очікування, щоб не навантажувати CPU,
                        // поки HandleConsoleCommands працює паралельно і встановлює прапорці.
                        await Task.Delay(200); // Перевіряємо прапорці кожні 200 мс
                    }

                    // Якщо було запитано вихід, зовнішній цикл завершиться
                    if (_shutdownRequested) continue; // Перехід до умови `while (!_shutdownRequested)`

                    // Якщо було запитано перезапуск (тобто 'ipchange' був успішним)
                    if (_restartRequested && _newIpAddress != null)
                    {
                        Console.WriteLine($"[Інфо] Отримано запит на перезапуск з новим IP: {_newIpAddress}. Спроба...");
                        _currentIpAddress = _newIpAddress; // Оновлюємо IP для наступної спроби
                        // Цикл продовжиться і спробує запустити StartServerAsync з новим IP
                    }
                    else
                    {
                        // Ця ситуація не мала б виникнути, якщо логіка 'ipchange' коректна
                        Console.WriteLine("[Помилка] Невизначений стан після помилки запуску. Спробуйте команду знову.");
                        await Task.Delay(1000); // Невелика пауза перед наступною ітерацією
                    }
                    continue; // Переходимо до наступної ітерації основного циклу
                }

                // Якщо сервер успішно запущено:
                // Запускаємо цикл прийому клієнтів
                await RunAcceptLoopAsync(_cts.Token);

                // Зупиняємо сервер (відбувається, коли цикл прийому скасовано)
                StopServer();

                // Якщо було запитано перезапуск, оновлюємо IP для наступної ітерації циклу
                if (_restartRequested && _newIpAddress != null)
                {
                    Console.WriteLine($"[Інфо] Перезапуск сервера з новим IP: {_newIpAddress}");
                    _currentIpAddress = _newIpAddress;
                    // Цикл продовжиться і викличе StartServerAsync з новим IP
                }
                // Якщо було запитано вихід, умова циклу `while (!_shutdownRequested)` стане хибною
            }

            Console.WriteLine("[Інфо] Роботу сервера завершено.");
            Console.WriteLine("Натисніть Enter для виходу...");
            Console.ReadLine(); // Тримаємо консоль відкритою
        }

        static async Task<bool> StartServerAsync(IPAddress ipAddress, int port)
        {
            try
            {
                _server = new TcpListener(ipAddress, port);
                _server.Start();
                _currentIpAddress = ipAddress; // Оновлюємо поточний стан IP
                Console.WriteLine($"[Інфо] Сервер запущено. Прослуховування на {ipAddress}:{port}");
                Console.WriteLine("Очікування підключень...");
                return true;
            }
            catch (SocketException ex)
            {
                Console.WriteLine($"[Помилка] Не вдалося запустити слухач на {ipAddress}:{port}. SocketException: {ex.Message} (Код: {ex.SocketErrorCode})");
                // Поширені помилки: AddressAlreadyInUse, AddressNotAvailable
                _server = null;
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Помилка] Неочікувана помилка під час запуску сервера: {ex.Message}");
                _server = null;
                return false;
            }
        }

        static async Task RunAcceptLoopAsync(CancellationToken token)
        {
            Console.WriteLine("[Інфо] Прийом клієнтських підключень...");
            try
            {
                while (!token.IsCancellationRequested)
                {
                    // Асинхронно чекаємо клієнта або скасування
                    var acceptTask = _server.AcceptTcpClientAsync();

                    // Створюємо завдання, яке завершиться при запиті скасування
                    var cancelTask = Task.Delay(Timeout.Infinite, token);

                    // Чекаємо на завершення будь-якого з двох завдань
                    Task completedTask = await Task.WhenAny(acceptTask, cancelTask);

                    // Якщо завершилося завдання скасування, або було запитано скасування
                    // після WhenAny, але до перевірки, виходимо з циклу.
                    if (completedTask == cancelTask || token.IsCancellationRequested)
                    {
                        Console.WriteLine("[Інфо] Отримано запит на скасування циклу прийому.");
                        break;
                    }

                    // Якщо ми тут, acceptTask успішно завершився.
                    // Отримуємо результат (або можливий виняток).
                    TcpClient client = await acceptTask;

                    IPEndPoint clientEndPoint = (IPEndPoint)client.Client.RemoteEndPoint;
                    Console.WriteLine($"[Інфо] Прийнято з'єднання від {clientEndPoint.Address}:{clientEndPoint.Port}");

                    // Обробляємо клієнта в окремому завданні (запустили і забули)
                    ClientHandler clientHandler = new ClientHandler(_settings);
                    _ = clientHandler.HandleClient(client); // Не потрібно await тут
                }
            }
            catch (ObjectDisposedException)
            {
                // Очікувано, якщо слухач зупинено під час очікування AcceptTcpClientAsync
                Console.WriteLine("[Інфо] Слухач було зупинено (ObjectDisposedException).");
            }
            catch (InvalidOperationException ex)
            {
                // Може статися, якщо Start() не було викликано або слухач зупинено.
                Console.WriteLine($"[Попередження] Неприпустима операція під час циклу прийому: {ex.Message}");
            }
            catch (SocketException ex)
            {
                Console.WriteLine($"[Помилка] SocketException в циклі прийому: {ex.Message}");
                // Розгляньте, чи потрібно зупиняти сервер через певні помилки сокета
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Помилка] Неочікувана помилка в циклі прийому: {ex.Message}");
                // Вирішіть, чи є це фатальною помилкою, яка має спричинити зупинку
                // Наразі ми вийдемо з циклу, що призведе до StopServer()
            }
            finally
            {
                Console.WriteLine("[Інфо] Вихід з циклу прийому підключень.");
            }
        }

        static void StopServer()
        {
            try
            {
                if (_server != null)
                {
                    _server.Stop();
                    Console.WriteLine("[Інфо] Слухач сервера зупинено.");
                    _server = null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Помилка] Виняток під час зупинки сервера: {ex.Message}");
            }
        }

        static void HandleConsoleCommands()
        {
            Console.WriteLine("[Консоль] Обробник команд запущено. Введіть 'quit' або 'exit' для зупинки.");
            while (true) // Цей цикл працює до введення 'quit'/'exit'
            {
                // Читаємо команду синхронно. Це блокує цей потік, але не основний потік сервера.
                string command = Console.ReadLine()?.Trim().ToLower();

                if (string.IsNullOrEmpty(command)) continue;

                if (_shutdownRequested) // Якщо вже йде зупинка, ігноруємо нові команди
                {
                    Console.WriteLine("[Консоль] Процес зупинки вже розпочато.");
                    break; // Виходимо з циклу обробника команд
                }

                if (command == "ipchange")
                {
                    Console.Write("Введіть нову IP адресу: ");
                    string newIpStr = Console.ReadLine()?.Trim();
                    if (IPAddress.TryParse(newIpStr, out IPAddress parsedIp))
                    {
                        if (!parsedIp.Equals(_currentIpAddress))
                        {
                            // --- Блок підтвердження ---
                            Console.Write($"Ви впевнені, що хочете змінити IP на {parsedIp}? (так/ні): ");
                            string confirmation = Console.ReadLine()?.Trim().ToLower();

                            if (confirmation == "так" || confirmation == "yes" || confirmation == "т") // Перевіряємо підтвердження
                            {
                                Console.WriteLine($"[Консоль] Підтверджено. Запит на перезапуск сервера з IP: {parsedIp}");
                                _newIpAddress = parsedIp;
                                _restartRequested = true;
                                try
                                {
                                    _cts?.Cancel(); // Сигналізуємо циклу прийому зупинитися
                                }
                                catch (ObjectDisposedException)
                                {
                                    Console.WriteLine("[Консоль] CancellationTokenSource вже було звільнено.");
                                    // Це може статися, якщо скасування вже відбулося або cts не було створено
                                    // В цьому випадку прапорці _restartRequested/_newIpAddress все одно спрацюють
                                }
                            }
                            else
                            {
                                Console.WriteLine("[Консоль] Зміну IP адреси скасовано.");
                            }
                            // --- Кінець блоку підтвердження ---
                        }
                        else
                        {
                            Console.WriteLine($"[Консоль] Нова IP адреса ({parsedIp}) така ж, як і поточна. Змін не потрібно.");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"[Помилка] Неправильний формат IP адреси: '{newIpStr}'");
                    }
                }
                else if (command == "status")
                {
                    if (_server != null && _server.Server != null && _server.Server.IsBound)
                    {
                        Console.WriteLine($"[Статус] Сервер запущено. Прослуховування на: {_currentIpAddress}:{_settings.Port}");
                    }
                    else if (_restartRequested)
                    {
                        Console.WriteLine($"[Статус] Сервер у процесі перезапуску (або очікує на перезапуск) з IP: {_newIpAddress ?? _currentIpAddress}.");
                    }
                    else if (_shutdownRequested)
                    {
                        Console.WriteLine("[Статус] Сервер зупиняється.");
                    }
                    else // Якщо _server == null і немає запиту на перезапуск/вихід
                    {
                        Console.WriteLine($"[Статус] Сервер зупинено (або не вдалося запустити). Поточна спроба для IP: {_currentIpAddress}.");
                    }
                }
                else if (command == "quit" || command == "exit")
                {
                    Console.WriteLine("[Консоль] Запит на зупинку сервера...");
                    _shutdownRequested = true;
                    _restartRequested = false; // Переконуємось, що не буде перезапуску під час зупинки
                    try
                    {
                        _cts?.Cancel(); // Сигналізуємо циклу прийому зупинитися
                    }
                    catch (ObjectDisposedException)
                    {
                        Console.WriteLine("[Консоль] CancellationTokenSource вже було звільнено при запиті на вихід.");
                    }
                    break; // Вихід з циклу обробника команд
                }
                else
                {
                    Console.WriteLine($"[Консоль] Невідома команда: '{command}'. Доступні команди: ipchange, status, quit, exit");
                }
            }
            Console.WriteLine("[Консоль] Обробник команд зупинено.");
        }
    }
}