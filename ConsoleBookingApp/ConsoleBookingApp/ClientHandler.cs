// ClientHandler.cs
using MySqlConnector; // Переконайтесь, що using є, якщо AppSettings його потребує
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization; // Для парсингу дат
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq; // Додано для роботи з JArray, JObject
using ConsoleBookingApp.Services; // Додано using для ReportingService та інших сервісів
using ConsoleBookingApp.Repositories;

namespace ConsoleBookingApp
{
    public class ClientHandler
    {
        private readonly AppSettings _settings;
        private const int MaxRequestSize = 1024 * 1024; // Ліміт розміру запиту (1MB), щоб уникнути DoS
        private const int ReadTimeoutMilliseconds = 300000; // Таймаут читання (5 хвилин), щоб звільнити неактивні з'єднання

        public ClientHandler(AppSettings settings)
        {
            _settings = settings;
        }

        public async Task HandleClient(TcpClient client)
        {
            NetworkStream stream = null;
            string clientId = "UnknownClient"; // Для логування

            try
            {
                clientId = client?.Client?.RemoteEndPoint?.ToString() ?? "InitializingClient";
                Console.WriteLine($"[{clientId}] New client connected.");

                stream = client.GetStream();
                // Встановлюємо таймаути (необов'язково, але корисно)
                stream.ReadTimeout = ReadTimeoutMilliseconds;
                stream.WriteTimeout = ReadTimeoutMilliseconds / 2; // Таймаут запису зазвичай менший

                // --- ОСНОВНИЙ ЦИКЛ ОБРОБКИ ЗАПИТІВ ---
                while (client.Connected && stream != null && stream.CanRead) // Поки клієнт підключений і потік читається
                {
                    Console.WriteLine($"[{clientId}] Waiting for data...");

                    // 1. Читання довжини даних (з таймаутом)
                    byte[] lengthBuffer = new byte[4];
                    int bytesReadLength = await ReadWithTimeoutAsync(stream, lengthBuffer, 0, 4, clientId);

                    if (bytesReadLength == 0)
                    {
                        Console.WriteLine($"[{clientId}] Client disconnected gracefully (read 0 bytes for length).");
                        break; // Клієнт закрив з'єднання
                    }
                    if (bytesReadLength < 0 || bytesReadLength != 4) // Помилка читання або таймаут (-1 означає таймаут)
                    {
                        // Якщо ReadWithTimeoutAsync повернув код помилки
                        string errorMessage = bytesReadLength == -1 ? "Read timeout" : $"Incomplete length data (read {bytesReadLength}/4 bytes)";
                        Console.WriteLine($"[{clientId}] Failed to read length: {errorMessage}. Closing connection.");
                        // Можна спробувати надіслати помилку, але клієнт може її не очікувати
                        // await ResponseHelper.SendErrorResponse(stream, "Invalid request length.");
                        break; // Помилка або таймаут, закриваємо
                    }

                    int dataLength = BitConverter.ToInt32(lengthBuffer, 0);
                    Console.WriteLine($"[{clientId}] Received data length: {dataLength}");

                    // 2. Перевірка довжини
                    if (dataLength <= 0 || dataLength > MaxRequestSize)
                    {
                        Console.WriteLine($"[{clientId}] Invalid data length received: {dataLength}. Closing connection.");
                        // Можна спробувати надіслати помилку, але клієнт може її не очікувати
                        // await ResponseHelper.SendErrorResponse(stream, "Invalid request length.");
                        break; // Некоректна довжина, розриваємо
                    }

                    // 3. Читання даних JSON
                    byte[] buffer = new byte[dataLength];
                    int totalBytesRead = await ReadWithTimeoutAsync(stream, buffer, 0, dataLength, clientId);

                    if (totalBytesRead == 0)
                    {
                        Console.WriteLine($"[{clientId}] Client disconnected gracefully (read 0 bytes for data).");
                        break; // Клієнт закрив з'єднання під час читання даних
                    }
                    if (totalBytesRead < 0 || totalBytesRead != dataLength) // Помилка читання або таймаут
                    {
                        // Якщо ReadWithTimeoutAsync повернув код помилки
                        string errorMessage = totalBytesRead == -1 ? "Read timeout" : $"Incomplete data (read {totalBytesRead}/{dataLength} bytes)";
                        Console.WriteLine($"[{clientId}] Failed to read full message: {errorMessage}. Closing connection.");
                        break; // Помилка або таймаут читання даних
                    }

                    string jsonString = Encoding.UTF8.GetString(buffer, 0, totalBytesRead);
                    Console.WriteLine($"[{clientId}] Received JSON: {jsonString}");

                    // 4. Обробка запиту (внутрішній try-catch для помилок конкретного запиту)
                    try
                    {
                        // <--- ЗМІНЕНО: Десеріалізуємо в Dictionary<string, object> ---
                        // Це дозволить обробляти різні типи даних у JSON (рядки, числа, масиви тощо)
                        var requestData = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonString);
                        // -------------------------------------------------------------


                        if (requestData != null && requestData.TryGetValue("action", out object actionObj) && actionObj is string action)
                        {
                            Console.WriteLine($"[{clientId}] Processing action: {action}");
                            // <--- Передаємо Dictionary<string, object> в ProcessAction ---
                            bool keepConnection = await ProcessAction(stream, action, requestData, clientId);
                            // -------------------------------------------------------------
                            Console.WriteLine($"[{clientId}] Action '{action}' processed. Keep connection: {keepConnection}");

                            if (!keepConnection) // Якщо дія вимагає розриву (наприклад, "disconnect")
                            {
                                break; // Виходимо з циклу while
                            }
                            // Якщо keepConnection = true, цикл продовжується і чекає на наступний запит
                        }
                        else
                        {
                            Console.WriteLine($"[{clientId}] Invalid request format: Missing or invalid 'action'.");
                            await ResponseHelper.SendErrorResponse(stream, "Невірний формат запиту: відсутня або некоректна 'action'.");
                            // Не розриваємо з'єднання через одну помилку формату, даємо клієнту шанс
                        }
                    }
                    catch (JsonReaderException jsonEx) // Ловимо специфічну помилку парсингу JSON
                    {
                        Console.WriteLine($"[{clientId}] JSON Deserialization Error: {jsonEx.Message}. Path: {jsonEx.Path}. Line: {jsonEx.LineNumber}. Position: {jsonEx.LinePosition}. JSON: {jsonString}");
                        await ResponseHelper.SendErrorResponse(stream, $"Помилка обробки запиту (невірний JSON формат). Деталі: {jsonEx.Message}"); // Можна включити деталі для відладки
                        // Не розриваємо з'єднання при помилці парсингу одного запиту
                    }
                    catch (Exception ex) // Обробка інших помилок під час обробки запиту
                    {
                        Console.WriteLine($"[{clientId}] Error processing request: {ex.GetType().Name} - {ex.Message}");
                        Console.WriteLine(ex.StackTrace);
                        // Намагаємося надіслати помилку клієнту, якщо потік ще доступний
                        if (stream != null && stream.CanWrite)
                        {
                            await ResponseHelper.SendErrorResponse(stream, "Внутрішня помилка сервера при обробці запиту.");
                        }
                        // Не розриваємо з'єднання через помилку одного запиту
                    }
                    // --- Кінець обробки одного запиту, цикл while продовжується ---
                }
            }
            catch (IOException ioEx) // Помилка вводу/виводу, зазвичай означає розрив з'єднання
            {
                // Часто виникає при ReadAsync/WriteAsync, якщо клієнт раптово відключився
                Console.WriteLine($"[{clientId}] IO Error in client loop (connection likely closed by client or network issue): {ioEx.Message}");
            }
            catch (ObjectDisposedException odEx) // Спроба використати закритий потік/клієнт
            {
                Console.WriteLine($"[{clientId}] Object Disposed Error in client loop: {odEx.Message}");
            }
            catch (Exception ex) // Інші непередбачені помилки на рівні циклу клієнта
            {
                Console.WriteLine($"[{clientId}] Unhandled error in client loop for {clientId}: {ex.GetType().Name} - {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                // Спробувати надіслати помилку, якщо можливо
                if (stream != null && stream.CanWrite && client.Connected)
                {
                    try
                    {
                        await ResponseHelper.SendErrorResponse(stream, "Критична помишка сервера.");
                    }
                    catch { } // Ігноруємо помилку надсилання помилки
                }
            }
            finally
            {
                Console.WriteLine($"[{clientId}] Closing connection and cleaning up resources...");
                // Закриваємо потік та клієнта незалежно від причини виходу з циклу
                stream?.Close(); // Dispose stream
                client?.Close(); // Dispose client
                Console.WriteLine($"[{clientId}] Connection closed.");
            }
        }

        // <--- ЗМІНЕНО СИГНАТУРУ МЕТОДУ: Приймає Dictionary<string, object> ---
        private async Task<bool> ProcessAction(NetworkStream stream, string action, Dictionary<string, object> data, string clientId)
        {
            // Повертає true, якщо з'єднання треба тримати, false - якщо розірвати
            try // Додамо try-catch навколо всієї обробки дії
            {
                // --- СПЕЦІАЛІЗОВАНА ОБРОБКА ДІЙ, ЯКІ НЕ ОЧІКУЮТЬ Dictionary<string, string> БЕЗПОСЕРЕДНЬО ---
                if (action == "add_club")
                {
                    Console.WriteLine($"[{clientId}] Processing add_club...");

                    // Перевірка наявності всіх необхідних полів
                    if (!data.ContainsKey("name") || !data.ContainsKey("address") ||
                        !data.ContainsKey("phone_number") || !data.ContainsKey("email") ||
                        !data.ContainsKey("max_ps_zones") || !data.ContainsKey("max_vr_quest_zones"))
                    {
                        Console.WriteLine($"[{clientId}] Missing required fields for add_club.");
                        await ResponseHelper.SendErrorResponse(stream, "Некоректні або відсутні дані для додавання клубу.");
                        return true; // Не розриваємо з'єднання
                    }

                    // Можна додати додаткову валідацію даних тут, якщо потрібно
                    // Наприклад, перевірка, що max_ps_zones та max_vr_quest_zones є числами >= 0
                    // та є цілими
                    if (!((data.TryGetValue("max_ps_zones", out object maxPsObj) && (maxPsObj is long || maxPsObj is int)) && Convert.ToInt32(maxPsObj) >= 0) ||
                         !((data.TryGetValue("max_vr_quest_zones", out object maxVrObj) && (maxVrObj is long || maxVrObj is int)) && Convert.ToInt32(maxVrObj) >= 0))
                    {
                        Console.WriteLine($"[{clientId}] Invalid data types or values for zone counts in add_club.");
                        await ResponseHelper.SendErrorResponse(stream, "Некоректний формат або значення для кількості зон.");
                        return true; // Не розриваємо з'єднання
                    }


                    // Виклик репозиторію для збереження в БД
                    var clubRepository = new ClubRepository(_settings.ConnectionString);
                    var (success, error) = await clubRepository.AddClubAsync(data); // Передаємо весь словник data

                    if (success)
                    {
                        Console.WriteLine($"[{clientId}] Club added successfully.");
                        // !!! ВИПРАВЛЕНО: Передаємо просто рядок з повідомленням !!!
                        await ResponseHelper.SendSuccessResponse(stream, "Клуб успішно додано.");
                    }
                    else
                    {
                        Console.WriteLine($"[{clientId}] Failed to add club: {error}");
                        await ResponseHelper.SendErrorResponse(stream, error);
                    }

                    return true; // Продовжуємо тримати з'єднання після додавання
                }
                // <--- ДОДАНО case для нових дій з клубами ---
                // Ці дії також будуть оброблятися до блоку stringOnlyData
                // та прийматимуть Dictionary<string, object> безпосередньо
                if (action == "get_all_clubs")
                {
                    Console.WriteLine($"[{clientId}] Processing get_all_clubs...");
                    await ClubService.HandleGetAllClubsAsync(stream, _settings.ConnectionString);
                    return true; // Тримаємо з'єднання
                }

                if (action == "get_club_details")
                {
                    Console.WriteLine($"[{clientId}] Processing get_club_details...");
                    await ClubService.HandleGetClubDetailsAsync(stream, _settings.ConnectionString, data);
                    return true; // Тримаємо з'єднання
                }

                if (action == "delete_club")
                {
                    Console.WriteLine($"[{clientId}] Processing delete_club...");
                    await ClubService.HandleDeleteClubAsync(stream, _settings.ConnectionString, data);
                    return true; // Тримаємо з'єднання
                }
                if (action == "get_manager_details")
                {
                    Console.WriteLine($"[{clientId}] Processing get_manager_details...");
                    // Викликаємо новий метод у ManagerService, передаємо raw 'data' dictionary
                    await ManagerService.HandleGetManagerDetailsAsync(stream, _settings.ConnectionString, data);
                    return true; // Тримаємо з'єднання
                }
                if (action == "search_client_by_phone")
                {
                    Console.WriteLine($"[{clientId}] Processing search_client_by_phone...");
                    // Викликаємо новий метод у ClientService, передаємо 'data' Dictionary
                    await ClientService.HandleSearchClientByPhoneAsync(stream, _settings.ConnectionString, data);
                    return true; // Зазвичай тримаємо з'єднання після пошуку
                }

                // НОВИЙ БЛОК: Обробка запиту "update_manager_details"
                if (action == "update_manager_details")
                {
                    Console.WriteLine($"[{clientId}] Processing update_manager_details...");
                    // Викликаємо новий метод у ManagerService, передаємо raw 'data' dictionary
                    await ManagerService.HandleUpdateManagerDetailsAsync(stream, _settings.ConnectionString, data);
                    return true; // Тримаємо з'єднання
                }
                if (action == "update_club")
                {
                    Console.WriteLine($"[{clientId}] Processing update_club...");
                    await ClubService.HandleUpdateClubAsync(stream, _settings.ConnectionString, data);
                    return true; // Тримаємо з'єднання
                }
                // <--- ДОДАНО: ОБРОБКА generate_reports (працює з Dictionary<string, object>) ---
                if (action == "generate_reports")
                {
                    Console.WriteLine($"[{clientId}] Processing generate_reports...");
                    await ReportingService.HandleGenerateReportsAsync(stream, _settings.ConnectionString, data); // Передаємо Dictionary<string, object>
                    return true; // Продовжуємо тримати з'єднання
                }
                // --------------------------------------------------------------------------

                // <--- ВИПРАВЛЕНО БЛОК add_booking: Адаптований парсинг з Dictionary<string, object> ---
                if (action == "add_booking")
                {
                    Console.WriteLine($"[{clientId}] Processing add_booking...");

                    // Оголошуємо змінні та ініціалізуємо їх значеннями за замовчуванням
                    DateTime sessionDate = default;
                    string startTimeStr = null;
                    string endTimeStr = null;
                    string clientName = null;
                    string phone = null;
                    int zoneCount = 0;
                    string sessionType = null;
                    string notes = null;
                    int managerId = 0;

                    bool dataValid = true; // Припускаємо валідність на початку

                    // --- Парсинг даних з Dictionary<string, object> ---

                    // 1. session_date (DateTime)
                    if (data.TryGetValue("session_date", out object dateObj) && dateObj is string dateStr_temp)
                    {
                        if (!DateTime.TryParseExact(dateStr_temp, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out sessionDate))
                        {
                            dataValid = false;
                            Console.WriteLine($"[{clientId}] Invalid 'session_date' format: {dateStr_temp}. Required: yyyy-MM-dd");
                        }
                    }
                    else
                    {
                        dataValid = false;
                        Console.WriteLine($"[{clientId}] Missing or non-string 'session_date'.");
                    }

                    // 2. start_time (string)
                    if (data.TryGetValue("start_time", out object startTimeObj) && startTimeObj is string startTimeStr_temp)
                    {
                        startTimeStr = startTimeStr_temp; // Присвоюємо значення оголошеній змінній
                        if (string.IsNullOrWhiteSpace(startTimeStr))
                        {
                            dataValid = false;
                            Console.WriteLine($"[{clientId}] 'start_time' is empty or whitespace.");
                        }
                    }
                    else
                    {
                        dataValid = false;
                        Console.WriteLine($"[{clientId}] Missing or non-string 'start_time'.");
                    }

                    // 3. end_time (string)
                    if (data.TryGetValue("end_time", out object endTimeObj) && endTimeObj is string endTimeStr_temp)
                    {
                        endTimeStr = endTimeStr_temp; // Присвоюємо значення оголошеній змінній
                        if (string.IsNullOrWhiteSpace(endTimeStr))
                        {
                            dataValid = false;
                            Console.WriteLine($"[{clientId}] 'end_time' is empty or whitespace.");
                        }
                    }
                    else
                    {
                        dataValid = false;
                        Console.WriteLine($"[{clientId}] Missing or non-string 'end_time'.");
                    }

                    // 4. client_name (string)
                    if (data.TryGetValue("client_name", out object clientNameObj) && clientNameObj is string clientName_temp)
                    {
                        clientName = clientName_temp; // Присвоюємо значення оголошеній змінній
                        if (string.IsNullOrWhiteSpace(clientName))
                        {
                            dataValid = false;
                            Console.WriteLine($"[{clientId}] 'client_name' is empty or whitespace.");
                        }
                    }
                    else
                    {
                        dataValid = false;
                        Console.WriteLine($"[{clientId}] Missing or non-string 'client_name'.");
                    }

                    // 5. phone (string)
                    if (data.TryGetValue("phone", out object phoneObj) && phoneObj is string phone_temp)
                    {
                        phone = phone_temp; // Присвоюємо значення оголошеній змінній
                        if (string.IsNullOrWhiteSpace(phone))
                        {
                            dataValid = false;
                            Console.WriteLine($"[{clientId}] 'phone' is empty or whitespace.");
                        }
                    }
                    else
                    {
                        dataValid = false;
                        Console.WriteLine($"[{clientId}] Missing or non-string 'phone'.");
                    }


                    // 6. zone_count (int - може прийти як long з JSON)
                    if (data.TryGetValue("zone_count", out object zoneCountObj))
                    {
                        if (zoneCountObj is long zoneCountLong)
                        {
                            zoneCount = (int)zoneCountLong;
                            if (zoneCount <= 0)
                            {
                                dataValid = false;
                                Console.WriteLine($"[{clientId}] Invalid 'zone_count' value: {zoneCountLong}. Must be a positive integer.");
                            }
                        }
                        else if (zoneCountObj is int zoneCountInt) // Обробляємо, якщо це вже int
                        {
                            zoneCount = zoneCountInt;
                            if (zoneCount <= 0)
                            {
                                dataValid = false;
                                Console.WriteLine($"[{clientId}] Invalid 'zone_count' value: {zoneCountInt}. Must be a positive integer.");
                            }
                        }
                        else
                        {
                            dataValid = false;
                            Console.WriteLine($"[{clientId}] 'zone_count' has unexpected type: {zoneCountObj.GetType().Name}. Expecting number.");
                        }
                    }
                    else
                    {
                        dataValid = false;
                        Console.WriteLine($"[{clientId}] Missing 'zone_count'.");
                    }

                    // 7. session_type (string)
                    if (data.TryGetValue("session_type", out object sessionTypeObj) && sessionTypeObj is string sessionType_temp)
                    {
                        sessionType = sessionType_temp; // Присвоюємо значення оголошеній змінній
                        if (string.IsNullOrWhiteSpace(sessionType))
                        {
                            dataValid = false;
                            Console.WriteLine($"[{clientId}] 'session_type' is empty or whitespace.");
                        }
                        // TODO: Додати валідацію самого значення session_type (напр., "VR", "PS")
                    }
                    else
                    {
                        dataValid = false;
                        Console.WriteLine($"[{clientId}] Missing or non-string 'session_type'.");
                    }

                    // 8. notes (string - необов'язково)
                    if (data.TryGetValue("notes", out object notesObj))
                    {
                        notes = notesObj?.ToString(); // Безпечно перетворюємо на рядок, обробляє null
                    }
                    // Якщо ключ відсутній, notes залишається null, що нормально, оскільки це необов'язкове поле.

                    // 9. manager_id (int - може прийти як long з JSON)
                    if (data.TryGetValue("manager_id", out object managerIdObj))
                    {
                        if (managerIdObj is long managerIdLong)
                        {
                            managerId = (int)managerIdLong;
                            if (managerId <= 0) // Базова валідація для ID
                            {
                                dataValid = false;
                                Console.WriteLine($"[{clientId}] Invalid 'manager_id' value: {managerIdLong}. Must be positive.");
                            }
                        }
                        else if (managerIdObj is int managerIdInt) // Обробляємо, якщо це вже int
                        {
                            managerId = managerIdInt;
                            if (managerId <= 0) // Базова валідація для ID
                            {
                                dataValid = false;
                                Console.WriteLine($"[{clientId}] Invalid 'manager_id' value: {managerIdInt}. Must be positive.");
                            }
                        }
                        else
                        {
                            dataValid = false;
                            Console.WriteLine($"[{clientId}] 'manager_id' has unexpected type: {managerIdObj.GetType().Name}. Expecting number.");
                        }
                    }
                    else
                    {
                        dataValid = false;
                        Console.WriteLine($"[{clientId}] Missing 'manager_id'.");
                    }

                    // --- Кінець парсингу ---

                    if (dataValid)
                    {
                        // Викликаємо BookingService.AddBooking з призначеними змінними
                        // Використовуємо змінні: sessionDate, startTimeStr, endTimeStr, clientName, phone, zoneCount, sessionType, notes, managerId
                        // Примітка: notes вже оброблено на null вище
                        await BookingService.AddBooking(stream, _settings.ConnectionString, sessionDate, startTimeStr, endTimeStr, clientName, phone, zoneCount, sessionType, notes ?? string.Empty, managerId);
                    }
                    else
                    {
                        Console.WriteLine($"[{clientId}] Invalid or missing data for add_booking action.");
                        await ResponseHelper.SendErrorResponse(stream, "Некоректні або відсутні дані для створення бронювання. Перевірте формат даних.");
                    }
                    return true; // Продовжуємо тримати з'єднання
                } // Кінець case "add_booking"
                // --------------------------------------------------------------------------

                // --- ДІЇ, ЯКІ ОЧІКУЮТЬ Dictionary<string, string> (Після перетворення з object) ---
                // Створюємо локальний словник для строкових даних, намагаючись перетворити значення з object
                var stringOnlyData = new Dictionary<string, string>();

                foreach (var pair in data)
                {
                    if (pair.Value == null)
                    {
                        stringOnlyData[pair.Key] = null;
                    }
                    else if (pair.Value is string stringValue)
                    {
                        stringOnlyData[pair.Key] = stringValue;
                    }
                    else if (pair.Value is IConvertible convertibleValue)
                    {
                        stringOnlyData[pair.Key] = Convert.ToString(convertibleValue, CultureInfo.InvariantCulture);
                    }
                    else if (pair.Value is bool boolValue)
                    {
                        stringOnlyData[pair.Key] = boolValue.ToString();
                    }
                    else if (pair.Value is JToken jtokenValue)
                    {
                        Console.WriteLine($"[{clientId}] Warning: Converting JToken (JArray/JObject?) to string for key '{pair.Key}' in action '{action}'. Expected simple value.");
                        stringOnlyData[pair.Key] = jtokenValue.ToString(Formatting.None);
                    }
                    else
                    {
                        Console.WriteLine($"[{clientId}] Warning: Attempting to convert unexpected type '{pair.Value.GetType().Name}' to string for key '{pair.Key}' in action '{action}'.");
                        stringOnlyData[pair.Key] = pair.Value.ToString();
                    }
                }


                // Викликаємо існуючі сервісні методи, передаючи stringOnlyData
                switch (action)
                {
                    case "login":
                        // РЯДОК 543: ТЕПЕР ПЕРЕДАЄМО 'data' (Dictionary<string, object>)
                        await AuthenticationService.HandleLogin(stream, _settings.ConnectionString, data);
                        break;

                    case "register":
                        // Ця дія вже оброблялася з 'data' раніше. Тепер просто додаємо її в switch.
                        Console.WriteLine($"[{clientId}] Processing register...");
                        await AuthenticationService.HandleRegistration(stream, _settings.ConnectionString, data);
                        break;
                    case "check_new_sessions_count": // <--- НОВИЙ CASE
                        Console.WriteLine($"[{clientId}] Processing check_new_sessions_count...");
                        await ManagerService.HandleCheckNewSessionsCountAsync(stream, _settings.ConnectionString, data);
                        break;
                    case "get_clubs":
                        Console.WriteLine($"[{clientId}] Processing get_clubs...");
                        // AuthenticationService.HandleGetClubs не очікує Dictionary<string,object> (лише stream та connectionString)
                        // Якщо метод AuthenticationService.HandleGetClubs приймає Dictionary<string, object> як параметр,
                        // передайте його сюди: await AuthenticationService.HandleGetClubs(stream, _settings.ConnectionString, data);
                        // Якщо ні, як у попередньому прикладі, передайте лише потрібні параметри:
                        await AuthenticationService.HandleGetClubs(stream, _settings.ConnectionString);
                        break;
                    case "get_daily_summary":
                        Console.WriteLine($"[{clientId}] Processing get_daily_summary...");
                        // Викликаємо НОВИЙ метод у BookingService, передаємо stringOnlyData
                        await BookingService.HandleGetDailySummaryAsync(stream, _settings.ConnectionString, stringOnlyData);
                        return true;
                    case "check_availability":
                        await BookingService.HandleCheckAvailability(stream, _settings.ConnectionString, stringOnlyData);
                        break;
                    case "get_prices":
                        await BookingService.HandleGetPrices(stream, _settings.ConnectionString, stringOnlyData);
                        break;
                    case "delete_booking":
                        await BookingService.HandleDeleteBooking(stream, _settings.ConnectionString, stringOnlyData);
                        break;
                    // case "update_order": // duplicated with update_booking
                    //    await BookingService.HandleUpdateBooking(stream, _settings.ConnectionString, stringOnlyData);
                    //    break;
                    case "get_current_bookings":
                        await BookingService.HandleGetCurrentBookings(stream, _settings.ConnectionString, stringOnlyData);
                        break;
                    case "get_bookings_by_time_slot":
                        await BookingService.HandleGetBookingsByTimeSlot(stream, _settings.ConnectionString, stringOnlyData);
                        break;
                    case "get_order_details":
                        await BookingService.HandleGetOrderDetailsAsync(stream, _settings.ConnectionString, stringOnlyData);
                        break;
                    case "update_booking": // assuming this is the correct one
                        await BookingService.HandleUpdateOrderAsync(stream, _settings.ConnectionString, stringOnlyData);
                        break;
                    case "check_availability_zone":
                        await BookingService.HandleCheckAvailabilityAsync(stream, _settings.ConnectionString, stringOnlyData);
                        break;
                    case "cancel_order":
                        await BookingService.HandleCancelOrderAsync(stream, _settings.ConnectionString, stringOnlyData);
                        break;
                    case "record_payment":
                        await BookingService.HandleRecordPaymentAsync(stream, _settings.ConnectionString, stringOnlyData);
                        break;
                    case "calculate_order_price":
                        await BookingService.HandleCalculateOrderPriceAsync(stream, _settings.ConnectionString, stringOnlyData);
                        break;
                    case "get_managers":
                        Console.WriteLine($"[{clientId}] Processing get_managers...");
                        await ManagerService.HandleGetManagersAsync(stream, _settings.ConnectionString, data); // Викликаємо новий метод
                        return true; // Тримаємо з'єднання

                    case "update_manager_role":
                        Console.WriteLine($"[{clientId}] Processing update_manager_role...");
                        await ManagerService.HandleUpdateManagerRoleAsync(stream, _settings.ConnectionString, data); // Викликаємо новий метод
                        return true; // Тримаємо з'єднання

                    case "update_manager_status":
                        Console.WriteLine($"[{clientId}] Processing update_manager_status...");
                        await ManagerService.HandleUpdateManagerStatusAsync(stream, _settings.ConnectionString, data); // Викликаємо новий метод
                        return true; // Тримаємо з'єднання
                    case "get_account_data":
                        await ManagerService.HandleGetAccountData(stream, _settings.ConnectionString, stringOnlyData);
                        break;
                    case "update_account_data":
                        await ManagerService.HandleUpdateAccountData(stream, _settings.ConnectionString, stringOnlyData);
                        break;
                    case "get_shift_status_and_history":
                        Console.WriteLine($"[{clientId}] Processing get_shift_status_and_history...");
                        await ManagerService.HandleGetShiftStatusAndHistoryAsync(stream, _settings.ConnectionString, data); // Передаємо Dictionary<string, object>
                        return true; // Тримаємо з'єднання
                    case "get_new_bookings":
                        Console.WriteLine($"[{clientId}] Processing get_new_bookings...");
                        // HandleGetNewBookingsAsync очікує Dictionary<string, string>
                        await BookingService.HandleGetNewBookingsAsync(stream, _settings.ConnectionString, stringOnlyData);
                        return true; // Залишаємо з'єднання відкритим

                    case "delete_new_booking":
                        Console.WriteLine($"[{clientId}] Processing delete_booking...");
                        // HandleDeleteBookingAsync очікує Dictionary<string, string>
                        await BookingService.HandleDeleteBookingAsync(stream, _settings.ConnectionString, stringOnlyData);
                        return true; // Залишаємо з'єднання відкритим

                    case "update_booking_status":
                        Console.WriteLine($"[{clientId}] Processing update_booking_status...");
                        // HandleUpdateBookingStatusAsync очікує Dictionary<string, string>
                        await BookingService.HandleUpdateBookingStatusAsync(stream, _settings.ConnectionString, stringOnlyData);
                        return true;
                    case "open_shift":
                        Console.WriteLine($"[{clientId}] Processing open_shift...");
                        await ManagerService.HandleOpenShiftAsync(stream, _settings.ConnectionString, data); // Передаємо Dictionary<string, object>
                        return true; // Тримаємо з'єднання

                    case "close_shift":
                        Console.WriteLine($"[{clientId}] Processing close_shift...");
                        await ManagerService.HandleCloseShiftAsync(stream, _settings.ConnectionString, data); // Передаємо Dictionary<string, object>
                        return true; // Тримаємо з'єднання
                    // <--- ПОВЕРНУТО: ОБРОБКА get_sessions_by_date ---
                    case "get_sessions_by_date":
                        Console.WriteLine($"[{clientId}] Processing get_sessions_by_date...");
                        // Передаємо stringOnlyData, оскільки BookingService.HandleGetSessionsForDateAsync очікує Dictionary<string, string>
                        await BookingService.HandleGetSessionsForDateAsync(stream, _settings.ConnectionString, stringOnlyData);
                        // Для цього типу запиту, можливо, краще розривати з'єднання після відповіді (KeepConnection = false)
                        // return false; // Якщо вирішите розривати з'єднання
                        return true; // Залишаємо тримати з'єднання за замовчуванням
                    // ---------------------------------------------


                    case "disconnect":
                        Console.WriteLine($"[{clientId}] Received disconnect request.");
                        return false; // Сигнал розірвати з'єднання

                    default:
                        Console.WriteLine($"[{clientId}] Unknown action received: {action}");
                        await ResponseHelper.SendErrorResponse(stream, $"Невідома дія: {action}");
                        break;
                }
                // --- Кінець логіки для інших дій ---
            }
            catch (Exception ex) // Ловимо помилки, що могли виникнути при обробці ДІЇ
            {
                Console.WriteLine($"[{clientId}] Error processing action '{action}': {ex.GetType().Name} - {ex.Message}");
                Console.WriteLine(ex.StackTrace); // Логуємо стек
                                                  // Намагаємося надіслати загальну помилку, якщо відповідь ще не була надіслана
                if (stream != null && stream.CanWrite)
                {
                    try
                    {
                        await ResponseHelper.SendErrorResponse(stream, $"Помилка сервера при обробці дії '{action}'.");
                    }
                    catch { } // Ігноруємо, якщо відправка помилки теж невдала
                }
            }

            return true; // За замовчуванням тримаємо з'єднання після обробки дії
        }

        // Допоміжний метод для читання з таймаутом (з кодами помилок)
        private async Task<int> ReadWithTimeoutAsync(NetworkStream stream, byte[] buffer, int offset, int count, string clientId, int timeoutMilliseconds = ReadTimeoutMilliseconds)
        {
            // Перевірка перед читанням
            if (stream == null || !stream.CanRead)
            {
                Console.WriteLine($"[{clientId}] Stream is not readable before ReadWithTimeoutAsync.");
                return -2; // Спеціальний код помилки для нечитабельного потоку
            }

            CancellationTokenSource cts = null;
            try
            {
                cts = new CancellationTokenSource(timeoutMilliseconds);
                // ВИПРАВЛЕНО: Використовуємо ReadAsync з CancellationToken
                int bytesRead = await stream.ReadAsync(buffer, offset, count, cts.Token);
                cts.Dispose(); // Звільняємо ресурси CancellationTokenSource
                return bytesRead; // Може бути 0, якщо клієнт закрив з'єднання
            }
            catch (OperationCanceledException) // Спрацював таймаут
            {
                Console.WriteLine($"[{clientId}] Read operation timed out after {timeoutMilliseconds} ms.");
                cts?.Dispose();
                return -1; // Ознака таймауту
            }
            catch (IOException ioEx) // Помилка мережі/потоку
            {
                Console.WriteLine($"[{clientId}] IOException during read: {ioEx.Message}");
                cts?.Dispose();
                return -3; // Ознака IOException
            }
            catch (ObjectDisposedException) // Потік вже закрито
            {
                Console.WriteLine($"[{clientId}] Stream was disposed during read.");
                cts?.Dispose();
                return -4; // Ознака ObjectDisposedException
            }
            catch (Exception ex) // Інші помилки
            {
                Console.WriteLine($"[{clientId}] Unexpected error during read: {ex.GetType().Name} - {ex.Message}");
                cts?.Dispose();
                return -5; // Ознака іншої помилки
            }
        }
    }
}