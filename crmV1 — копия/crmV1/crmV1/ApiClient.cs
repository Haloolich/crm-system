// crmV1/Services/ApiClient.cs
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics; // Для Debug.WriteLine
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq; // Додаємо для безпечної обробки відповідей
using System.Threading; // Для CancellationTokenSource (потрібен для таймауту читання/запису)
using System.Net;
using System.ComponentModel; // Для IPAddress (якщо потрібно парсити IP)

// Припустимо, що AppConfig існує і містить ServerIP.
// using crmV1; // Якщо AppConfig знаходиться в цьому просторі імен

namespace crmV1.Services
{
    public static class ApiClient // Зробимо статичним для простоти використання
    {
        // TODO: Вам потрібно буде отримати реальні IP та Port сервера!
        // Припустимо, що AppConfig.ServerIP існує і містить IP (string).
        // Якщо порт фіксований, можна залишити константою.
        private static string ServerIp => AppConfig.ServerIP; // Отримуємо з AppConfig
        //private const string ServerIp = "192.168.0.102"; // Тимчасово хардкодимо для прикладу. Використовуйте AppConfig!
        private const int ServerPort = 8888; // Фіксований порт (прикладовий)

        private const int ConnectTimeoutMilliseconds = 15000; // Таймаут підключення (15 секунд)
        private const int ReadWriteTimeoutMilliseconds = 30000; // Таймаут читання/запису (30 секунд)


        // --- МОДИФІКОВАНО: Використовує Task.WhenAny для таймауту підключення ---
        // --- ЗБЕРЕЖЕНО: Обробляє як root Object, так і root Array відповіді ---
        public static async Task<Dictionary<string, object>> SendRequestAsync(Dictionary<string, object> requestData)
        {
            TcpClient client = null;
            NetworkStream stream = null;

            try
            {
                Debug.WriteLine($"[ApiClient] Connecting to {ServerIp}:{ServerPort}...");
                client = new TcpClient();
                // Можна встановити таймаути на рівні сокета, хоча ReadAsync/WriteAsync
                // з CancellationToken більш надійні для таймаутів операцій.
                client.ReceiveTimeout = ReadWriteTimeoutMilliseconds;
                client.SendTimeout = ReadWriteTimeoutMilliseconds;

                // --- Реалізація таймауту підключення за допомогою Task.WhenAny ---

                // Примітка: Є перевантаження ConnectAsync, яке приймає IPAddress.
                // Якщо ServerIp завжди IP-адреса, можна спробувати:
                // if (IPAddress.TryParse(ServerIp, out IPAddress ipAddress))
                // {
                //     var connectTask = client.ConnectAsync(ipAddress, ServerPort);
                //     // ... далі логіка Task.WhenAny з connectTask
                // } else { /* Обробка невірної IP */ }
                // Але перевантаження з string hostname зазвичай працює і для IP.
                var connectTask = client.ConnectAsync(ServerIp, ServerPort); // Використовуємо 2-аргументне перевантаження

                var timeoutTask = Task.Delay(TimeSpan.FromMilliseconds(ConnectTimeoutMilliseconds));

                var completedTask = await Task.WhenAny(connectTask, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    // Таймаут підключення
                    Debug.WriteLine($"[ApiClient] Connection Timeout after {ConnectTimeoutMilliseconds} ms.");
                    // Важливо закрити клієнт, якщо підключення не вдалося або таймаут
                    client?.Dispose();
                    throw new TimeoutException($"Таймаут з'єднання з сервером ({ConnectTimeoutMilliseconds / 1000} сек).");
                }

                // Якщо завершився connectTask, перевіряємо, чи була помилка підключення
                if (connectTask.IsFaulted)
                {
                    Debug.WriteLine($"[ApiClient] ConnectAsync task faulted: {connectTask.Exception}");
                    client?.Dispose(); // Важливо закрити клієнт при помилці
                    // Перекидаємо оригінальний виняток підключення
                    throw connectTask.Exception.InnerException ?? connectTask.Exception ?? new Exception("Невідома помилка підключення.");
                }

                // Якщо ми дійшли сюди, connectTask завершився першим і без помилок.
                // await connectTask; // Можна додати await тут, хоча IsFaulted вже перевірено. Це завершить таск.

                Debug.WriteLine("[ApiClient] Connected. Getting stream...");
                stream = client.GetStream();

                // Таймаути для читання/запису на рівні потоку
                stream.ReadTimeout = ReadWriteTimeoutMilliseconds;
                stream.WriteTimeout = ReadWriteTimeoutMilliseconds;


                // 1. Серіалізуємо дані запиту (Dictionary<string, object>) в JSON
                string jsonRequest = JsonConvert.SerializeObject(requestData);
                byte[] requestBytes = Encoding.UTF8.GetBytes(jsonRequest);
                int requestLength = requestBytes.Length;

                Debug.WriteLine($"[ApiClient] Request JSON ({requestLength} bytes): {jsonRequest}");

                // 2. Відправляємо довжину даних (4 байти)
                byte[] requestLengthBytes = BitConverter.GetBytes(requestLength);
                Debug.WriteLine($"[ApiClient] Sending length: {requestLength}");
                // await stream.WriteAsync поважає WriteTimeout, якщо встановлено
                await stream.WriteAsync(requestLengthBytes, 0, 4);

                // 3. Відправляємо JSON дані
                if (requestLength > 0)
                {
                    Debug.WriteLine("[ApiClient] Sending JSON data...");
                    // await stream.WriteAsync поважає WriteTimeout
                    await stream.WriteAsync(requestBytes, 0, requestLength);
                }
                await stream.FlushAsync(); // Гарантуємо відправку даних
                Debug.WriteLine("[ApiClient] Request sent. Waiting for response length...");


                // 4. Читаємо довжину відповіді (4 байти) з таймаутом
                byte[] responseLengthBytes = new byte[4];
                // Використовуємо ReadAsync з CancellationToken для контролю таймауту читання довжини
                // АБО покладаємося на stream.ReadTimeout, тоді ReadAsync без токена.
                // Використання CancellationTokenSource для ReadAsync є більш явним і рекомендованим в .NET (хоча може не працювати на старіших платформах для ConnectAsync)
                // Якщо ReadAsync з токеном теж не працює, потрібно використовувати складнішу ручну реалізацію читання з таймаутом.
                // Спробуємо з токеном, якщо компілятор дозволить для ReadAsync:
                int bytesReadLength;
                using (var readLengthCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(ReadWriteTimeoutMilliseconds)))
                {
                    try
                    {
                        bytesReadLength = await stream.ReadAsync(responseLengthBytes, 0, 4, readLengthCts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        Debug.WriteLine($"[ApiClient] Read Length Timeout after {ReadWriteTimeoutMilliseconds} ms.");
                        throw new TimeoutException($"Таймаут ({ReadWriteTimeoutMilliseconds / 1000} сек) читання довжини відповіді від сервера.");
                    }
                }


                if (bytesReadLength == 0)
                {
                    Debug.WriteLine("[ApiClient] Server disconnected while waiting for response length (read 0 bytes).");
                    throw new IOException("Сервер відключився під час очікування довжини відповіді.");
                }
                if (bytesReadLength < 4)
                {
                    Debug.WriteLine($"[ApiClient] Received unexpected amount of data for length: {bytesReadLength} bytes (expected 4).");
                    throw new IOException($"Сервер повернув некоректний формат довжини відповіді: отримано {bytesReadLength} з 4 байт.");
                }

                int responseLength = BitConverter.ToInt32(responseLengthBytes, 0);
                Debug.WriteLine($"[ApiClient] Received response length: {responseLength}");

                // Ліміт відповіді 10 MB.
                const int MaxResponseLength = 10 * 1024 * 1024; // 10 MB
                if (responseLength <= 0 || responseLength > MaxResponseLength)
                {
                    Debug.WriteLine($"[ApiClient] Received invalid response length: {responseLength}.");
                    throw new InvalidDataException($"Сервер повернув некоректну довжину відповіді: {responseLength}. Довжина має бути > 0 та <= {MaxResponseLength / 1024 / 1024} MB.");
                }

                // 5. Читаємо дані відповіді JSON з таймаутом
                byte[] responseBuffer = new byte[responseLength];
                int totalBytesRead = 0;
                Debug.WriteLine($"[ApiClient] Reading {responseLength} bytes of response data...");

                using (var readDataCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(ReadWriteTimeoutMilliseconds)))
                {
                    while (totalBytesRead < responseLength)
                    {
                        try
                        {
                            // Використовуємо ReadAsync з CancellationToken
                            int bytesRead = await stream.ReadAsync(responseBuffer, totalBytesRead, responseLength - totalBytesRead, readDataCts.Token);
                            if (bytesRead == 0)
                            {
                                Debug.WriteLine($"[ApiClient] Server disconnected while reading response data. Read {totalBytesRead}/{responseLength} bytes so far.");
                                throw new IOException($"Сервер відключився під час читання даних відповіді (прочитано {totalBytesRead} з {responseLength} байт).");
                            }
                            totalBytesRead += bytesRead;
                        }
                        catch (OperationCanceledException)
                        {
                            Debug.WriteLine($"[ApiClient] Read Data Timeout after {ReadWriteTimeoutMilliseconds} ms. Read {totalBytesRead}/{responseLength} bytes.");
                            throw new TimeoutException($"Таймаут ({ReadWriteTimeoutMilliseconds / 1000} сек) читання даних відповіді від сервера.");
                        }
                    }
                }
                Debug.WriteLine($"[ApiClient] Finished reading {totalBytesRead}/{responseLength} bytes.");


                // 6. Десеріалізуємо дані відповіді JSON в JToken для гнучкої обробки
                string jsonResponse = Encoding.UTF8.GetString(responseBuffer, 0, totalBytesRead);
                Debug.WriteLine($"[ApiClient] Response JSON: {jsonResponse}");

                JToken responseToken = null; // Використовуємо JToken
                try
                {
                    responseToken = JToken.Parse(jsonResponse); // Парсимо як JToken
                }
                catch (JsonReaderException jre) // Ловимо специфічні помилки парсингу JSON
                {
                    Debug.WriteLine($"[ApiClient] JSON Parse Error: {jre.Message}. Path: {jre.Path}, Line: {jre.LineNumber}, Position: {jre.LinePosition}. JSON: {jsonResponse}");
                    // Повертаємо стандартизований словник з помилкою парсингу
                    return new Dictionary<string, object> { { "success", false }, { "message", $"Помилка парсингу JSON відповіді від сервера: {jre.Message}" } };
                }
                catch (Exception jsonParseEx) // Ловимо інші можливі винятки при парсингу
                {
                    Debug.WriteLine($"[ApiClient] Unexpected JSON Parse Error: {jsonParseEx.GetType().Name} - {jsonParseEx.Message}. JSON: {jsonResponse}");
                    return new Dictionary<string, object> { { "success", false }, { "message", $"Неочікувана помилка при парсингу відповіді сервера: {jsonParseEx.Message}" } };
                }


                // --- Обробляємо responseToken залежно від його типу ---
                if (responseToken.Type == JTokenType.Object)
                {
                    Debug.WriteLine("[ApiClient] Received root JObject.");
                    // Сервер повернув кореневий об'єкт. Припускаємо стандартний формат {"success": ..., "message": ..., "data": ...}
                    var responseData = responseToken.ToObject<Dictionary<string, object>>();
                    if (responseData == null)
                    {
                        Debug.WriteLine("[ApiClient] Failed to convert root JObject to Dictionary<string, object>.");
                        return new Dictionary<string, object> { { "success", false }, { "message", "Не вдалося перетворити відповідь сервера у словник даних." } };
                    }
                    // Повертаємо отриманий словник (він вже має містити success/message/data)
                    return responseData;
                }
                else if (responseToken.Type == JTokenType.Array)
                {
                    Debug.WriteLine("[ApiClient] Received root JArray. Wrapping in success response.");
                    // Сервер повернув кореневий масив (як для списку бронювань).
                    // Обертаємо масив у словник стандартизованого формату {"success": true, "data": ваш_масив}
                    return new Dictionary<string, object>
                    {
                        { "success", true },
                        { "data", responseToken } // Зберігаємо сам JArray під ключем "data"
                    };
                }
                else
                {
                    // Обробляємо інші неочікувані типи кореневих елементів (примітив, null тощо)
                    Debug.WriteLine($"[ApiClient] Received unexpected root JSON type: {responseToken.Type}. JSON: {jsonResponse}");
                    return new Dictionary<string, object> { { "success", false }, { "message", $"Сервер повернув неочікуваний формат відповіді: {responseToken.Type}" } };
                }

            }
            // --- Catch блоки для обробки різних типів помилок ---
            catch (TimeoutException tex) // Ловимо наші власні TimeoutException (для підключення або читання)
            {
                Debug.WriteLine($"[ApiClient] Caught TimeoutException: {tex.Message}");
                return new Dictionary<string, object> { { "success", false }, { "message", tex.Message } };
            }
            catch (OperationCanceledException oce) // Може спіймати таймаути, якщо ReadAsync/WriteAsync кидає цей виняток при скасуванні/таймауті
            {
                Debug.WriteLine($"[ApiClient] Caught OperationCanceledException: {oce.Message}");
                // Можливо, варто перевірити oce.CancellationToken.IsCancellationRequested, щоб відрізнити таймаут від іншого скасування
                return new Dictionary<string, object> { { "success", false }, { "message", $"Операція скасована (можливо, таймаут): {oce.Message}" } };
            }
            catch (SocketException sex) // Ловимо помилки сокетів (ConnectionRefused, HostNotFound, NetworkUnreachable тощо)
            {
                Debug.WriteLine($"[ApiClient] Caught SocketException: {sex.SocketErrorCode} - {sex.Message}");
                string userMessage = $"Помилка мережі: {sex.Message}";
                if (sex.SocketErrorCode == SocketError.ConnectionRefused)
                {
                    userMessage = $"Сервер відмовив у з'єднанні. Перевірте, чи сервер запущено та доступний.";
                }
                else if (sex.SocketErrorCode == SocketError.TimedOut)
                {
                    // Хоча ми обробляємо таймаут підключення через Task.WhenAny,
                    // деякі OS/платформи можуть кидати SocketException.TimedOut
                    // Також таймаут може бути на рівні сокета під час читання/запису.
                    userMessage = $"Таймаут мережевої операції: {sex.Message}";
                }
                return new Dictionary<string, object> { { "success", false }, { "message", userMessage } };
            }
            catch (IOException ioEx) // Ловимо помилки IO (розрив з'єднання, помилки потоку)
            {
                Debug.WriteLine($"[ApiClient] Caught IOException: {ioEx.Message}");
                return new Dictionary<string, object> { { "success", false }, { "message", $"Помилка мережі: {ioEx.Message}" } };
            }
            catch (Exception ex) // Ловимо будь-які інші неочікувані винятки
            {
                Debug.WriteLine($"[ApiClient] Caught General Exception in SendRequestAsync: {ex.GetType().Name} - {ex.Message}\n{ex.StackTrace}");
                // Повертаємо стандартизований Dictionary з помилкою
                return new Dictionary<string, object> { { "success", false }, { "message", $"Неочікувана помилка зв'язку: {ex.Message}" } };
            }
            finally
            {
                // Закриваємо потік та клієнта незалежно від результату
                stream?.Dispose();
                client?.Dispose();
                Debug.WriteLine("[ApiClient] Connection closed.");
            }
        }
    }

    public class Booking : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        [JsonProperty("booking_id")]
        public int Id { get; set; }

        [JsonProperty("client_name")]
        public string ClientName { get; set; }

        [JsonProperty("phone")]
        public string Phone { get; set; }

        [JsonProperty("date")]
        public string DateString { get; set; }

        [JsonProperty("start_time")]
        public string StartTimeString { get; set; }

        [JsonProperty("end_time")]
        public string EndTimeString { get; set; }

        [JsonProperty("session_type")]
        public string SessionType { get; set; }

        [JsonProperty("num_people")]
        public int NumPeople { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; } // Очікуємо тут "New" для цієї сторінки

        [JsonIgnore]
        public DateTime? BookingDateTime
        {
            get
            {
                if (DateTime.TryParse(DateString, out DateTime date) &&
                    TimeSpan.TryParse(StartTimeString, out TimeSpan time))
                {
                    return date.Date + time;
                }
                return null;
            }
        }

        [JsonIgnore]
        public string DisplayTime
        {
            get
            {
                if (TimeSpan.TryParse(StartTimeString, out TimeSpan startTime) &&
                    TimeSpan.TryParse(EndTimeString, out TimeSpan endTime))
                {
                    return $"{startTime:hh\\:mm} - {endTime:hh\\:mm}";
                }
                return StartTimeString;
            }
        }

        [JsonIgnore]
        public string DisplayDetails
        {
            get
            {
                return $"{SessionType} на {NumPeople} осіб";
            }
        }

        [JsonIgnore]
        public string DisplayStatus
        {
            get
            {
                return Status;
            }
        }
    }
}