using ConsoleBookingApp.Data;
using ConsoleBookingApp.Services;
using MySqlConnector;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleBookingApp
{
    public static class BookingService
    {
        public static async Task HandleGetDailySummaryAsync(NetworkStream stream, string connectionString, Dictionary<string, string> data)
        {
            string logPrefix = "[HandleGetDailySummaryAsync]"; // Для логування
            Console.WriteLine($"{logPrefix} Запит отримано.");

            // 1. Валідація вхідних даних (дата та club_id)
            // Використовуємо існуючі методи валідації, якщо вони є
            if (!BookingValidationService.TryParseSessionDate(data.GetValueOrDefault("date"), out DateTime checkDate) ||
                !BookingValidationService.IsValidId(data.GetValueOrDefault("club_id"), out int clubId))
            {
                Console.WriteLine($"{logPrefix} ПОМИЛКА ВАЛІДАЦІЇ: Невірні або відсутні дані (date: {data.GetValueOrDefault("date")}, club_id: {data.GetValueOrDefault("club_id")}).");
                await ResponseHelper.SendErrorResponse(stream, "Невірні або відсутні дані (дата або ID клубу).");
                return;
            }

            Console.WriteLine($"{logPrefix} Запит даних для Підсумку дня: Дата {checkDate.ToShortDateString()}, Клуб: {clubId}.");

            try
            {
                // 2. Викликаємо НОВИЙ метод репозиторію
                var sessionsDetails = await BookingRepository.GetDailySummarySessionsAsync( // ВИКЛИК НОВОГО МЕТОДУ
                    connectionString,
                    checkDate,
                    clubId
                );

                // 3. Формуємо відповідь для клієнта (список словників)
                // Перетворюємо List<SessionDetails> на List<Dictionary<string, object>>
                var sessionsResponseList = new List<Dictionary<string, object>>();

                foreach (var session in sessionsDetails)
                {
                    var sessionData = new Dictionary<string, object>();
                    // Мапінг полів SessionDetails до Dictionary<string, object>
                    // Назви ключів мають відповідати тому, що очікує клієнт у DailySummarySession моделі
                    sessionData["SessionId"] = session.SessionId; // Можливо, не потрібен клієнту для підсумку, але корисно
                    sessionData["Time"] = session.StartTime.ToString(@"hh\:mm"); // Час у форматі "ГГ:ХХ"
                    sessionData["ClientName"] = session.ClientName;
                    sessionData["PaymentStatus"] = session.PaymentStatus;
                    sessionData["Amount"] = session.FinalPrice; // Фінальна сума
                    sessionData["PaymentMethod"] = session.PaymentMethod ?? "Не вказано"; // Метод оплати, "Не вказано", якщо null
                    sessionData["SessionType"] = session.SessionType; // Можливо, потрібно клієнту
                    sessionData["NumPeople"] = session.NumPeople; // Можливо, потрібно клієнту
                    // clientData["NeedsAttention"] буде розраховуватись на клієнті
                    // Можна додати інші поля з SessionDetails, якщо вони потрібні на клієнті
                    // sessionData["Notes"] = session.Notes;
                    // sessionData["CalculatePrice"] = session.CalculatePrice;
                    // sessionData["SessionDate"] = session.SessionDate.ToString("yyyy-MM-dd");
                    // sessionData["EndTime"] = session.EndTime.ToString(@"hh\:mm");

                    sessionsResponseList.Add(sessionData);
                }

                // Обертаємо список сесій у словник для стандартизованої відповіді
                var responseData = new Dictionary<string, object>
                {
                    { "success", true },
                    { "sessions", sessionsResponseList } // Список словників під ключем "sessions"
                };

                await ResponseHelper.SendJsonResponse(stream, responseData);
                Console.WriteLine($"{logPrefix} Запит даних для Підсумку дня успішний: Повернуто {sessionsResponseList.Count} сесій для дати {checkDate.ToShortDateString()} в клубі {clubId}.");

            }
            catch (Exception ex)
            {
                // Ловимо будь-які інші винятки з репозиторію або при обробці
                Console.WriteLine($"{logPrefix} ПОМИЛКА при отриманні даних для Підсумку дня: {ex.GetType().Name} - {ex.Message}\n{ex.StackTrace}");
                await ResponseHelper.SendErrorResponse(stream, $"Сталася помилка при отриманні даних для підсумку дня: {ex.Message}");
            }
        }
        public static async Task HandleCheckAvailabilityAsync(NetworkStream stream, string connectionString, Dictionary<string, string> data)
        {
            // 1. Валідація вхідних даних
            if (!BookingValidationService.IsValidAvailabilityCheckData(
                    data,
                    out DateTime checkDate,
                    out TimeSpan checkStartTime,
                    out TimeSpan checkEndTime,
                    out int clubId))
            {
                Console.WriteLine("[ПОМИЛКА ВАЛІДАЦІЇ] Невірні або відсутні дані для перевірки доступності.");
                await ResponseHelper.SendErrorResponse(stream, "Невірні або відсутні дані для перевірки доступності.");
                return;
            }

            // Перевірка логіки проміжку
            if (checkStartTime >= checkEndTime)
            {
                Console.WriteLine($"[ПОМИЛКА ВАЛІДАЦІЇ] Час початку {checkStartTime} має бути раніше часу кінця {checkEndTime}.");
                await ResponseHelper.SendErrorResponse(stream, "Час початку має бути раніше часу кінця.");
                return;
            }


            try
            {
                Console.WriteLine($"[ЛОГ] Перевірка доступності: Дата {checkDate.ToShortDateString()}, Час {checkStartTime}-{checkEndTime}, Клуб: {clubId}.");

                // 2. Викликаємо метод репозиторію для отримання зайнятих зон
                var occupiedZones = await BookingRepository.GetOccupiedZonesAsync(
                    connectionString,
                    checkDate,
                    checkStartTime,
                    checkEndTime,
                    clubId
                );

                // 3. Розраховуємо вільні зони, виходячи з лімітів
                const int maxVrQuestZones = 3;
                const int maxPsZones = 1;

                int availableVrQuestZones = maxVrQuestZones - occupiedZones.OccupiedVrQuest;
                int availablePsZones = maxPsZones - occupiedZones.OccupiedPs;

                // Гарантуємо, що кількість вільних зон не від'ємна (хоча запит і ліміти мають це запобігти, але на всякий випадок)
                availableVrQuestZones = Math.Max(0, availableVrQuestZones);
                availablePsZones = Math.Max(0, availablePsZones);

                // 4. Формуємо відповідь для клієнта
                var responseData = new Dictionary<string, object>
            {
                { "success", true },
                { "date", checkDate.ToString("yyyy-MM-dd") },
                { "start_time", checkStartTime.ToString(@"hh\:mm\:ss") }, // Форматуємо TimeSpan як HH:mm:ss
                { "end_time", checkEndTime.ToString(@"hh\:mm\:ss") },     // Форматуємо TimeSpan як HH:mm:ss
                { "club_id", clubId },
                { "occupied_vr_quest", occupiedZones.OccupiedVrQuest },
                { "available_vr_quest", availableVrQuestZones },
                { "occupied_ps", occupiedZones.OccupiedPs },
                { "available_ps", availablePsZones }
                // Можливо, ви захочете додати більше деталей про зайняті сесії,
                // але для візуалізації значків достатньо кількості.
            };

                await ResponseHelper.SendJsonResponse(stream, responseData);
                Console.WriteLine($"[ЛОГ] Перевірка доступності успішна: VR/Quest зайнято {occupiedZones.OccupiedVrQuest}, вільно {availableVrQuestZones}; PS зайнято {occupiedZones.OccupiedPs}, вільно {availablePsZones}.");

            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ПОМИЛКА] HandleCheckAvailabilityAsync: {ex.Message}\n{ex.StackTrace}");
                await ResponseHelper.SendErrorResponse(stream, "Сталася помилка при перевірці доступності.");
            }
        }
        public static async Task HandleGetSessionsForDateAsync(NetworkStream stream, string connectionString, Dictionary<string, string> data)
        {
            // 1. Валідація вхідних даних
            if (!BookingValidationService.IsValidDateAndClubData( // Потрібен новий метод валідації
                    data,
                    out DateTime checkDate,
                    out int clubId))
            {
                Console.WriteLine("[ПОМИЛКА ВАЛІДАЦІЇ] Невірні або відсутні дані для запиту сесій по даті.");
                await ResponseHelper.SendErrorResponse(stream, "Невірні або відсутні дані для запиту сесій по даті.");
                return;
            }

            try
            {
                Console.WriteLine($"[ЛОГ] Запит сесій: Дата {checkDate.ToShortDateString()}, Клуб: {clubId}.");

                // 2. Викликаємо метод репозиторію
                var sessions = await BookingRepository.GetSessionsForDateAsync(
                    connectionString,
                    checkDate,
                    clubId
                );

                // 3. Формуємо відповідь для клієнта (список сесій)
                // Обертаємо список сесій у словник для стандартизованої відповіді
                var responseData = new Dictionary<string, object>
            {
                { "success", true },
                { "date", checkDate.ToString("yyyy-MM-dd") },
                { "club_id", clubId },
                { "sessions", sessions } // Ключ "sessions" містить список словників, кожен - дані однієї сесії
            };

                await ResponseHelper.SendJsonResponse(stream, responseData);
                Console.WriteLine($"[ЛОГ] Запит сесій успішний: Повернуто {sessions.Count} сесій для дати {checkDate.ToShortDateString()} в клубі {clubId}.");

            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ПОМИЛКА] HandleGetSessionsForDateAsync: {ex.Message}\n{ex.StackTrace}");
                await ResponseHelper.SendErrorResponse(stream, "Сталася помилка при отриманні списку сесій.");
            }
        }
        // Метод AddBooking (без змін)
        public static async Task HandleAddBooking(NetworkStream stream, string connectionString, Dictionary<string, string> data)
        {
            // 1. Парсинг та валідація вхідних даних
            // Додаємо перевірку ключа 'phone_number'
            if (!BookingValidationService.TryParseSessionDate(data.GetValueOrDefault("session_date"), out DateTime sessionDate) ||
               !BookingValidationService.TryParseBookingTime(data.GetValueOrDefault("start_time"), data.GetValueOrDefault("end_time"), out TimeSpan startTime, out TimeSpan endTime) ||
               !BookingValidationService.IsValidName(data.GetValueOrDefault("client_name")) ||
               !data.ContainsKey("phone_number") || // Перевірка наявності ключа
               !BookingValidationService.IsValidPhoneNumber(data["phone_number"]) || // Перевірка валідності
               !BookingValidationService.IsValidZoneCount(data.GetValueOrDefault("zone_count"), out int zoneCount) ||
               !BookingValidationService.IsValidSessionType(data.GetValueOrDefault("session_type")) ||
               !BookingValidationService.IsValidId(data.GetValueOrDefault("manager_id"), out int managerId))
            {
                await ResponseHelper.SendErrorResponse(stream, "Невірний формат або відсутні обов'язкові дані для бронювання (включно з phone_number).");
                return;
            }
            string clientName = data["client_name"];
            string phone = data["phone_number"]; // Тепер ми знаємо, що ключ існує
            string sessionType = data["session_type"];
            string notes = data.GetValueOrDefault("notes"); // Може бути null
            int? discountId = null;
            if (BookingValidationService.IsValidId(data.GetValueOrDefault("discount_id"), out int parsedDiscountId))
            {
                discountId = parsedDiscountId;
            }


            // 2. Логіка додавання бронювання
            MySqlConnection connection = null;
            try
            {
                connection = new MySqlConnection(connectionString);
                await connection.OpenAsync();
                Console.WriteLine("[ЛОГ] З'єднання з БД відкрито для додавання бронювання.");

                // 2.1 Отримати club_id менеджера
                int? clubId = await BookingRepository.GetClubIdForManagerAsync(connectionString, managerId, connection);
                if (!clubId.HasValue)
                {
                    await ResponseHelper.SendErrorResponse(stream, "Помилка: Менеджера не знайдено або йому не призначено клуб.");
                    Console.WriteLine($"[ПОМИЛКА] Не вдалося знайти club_id для manager_id: {managerId}.");
                    return; // Вихід, якщо клуб не знайдено
                }

                // 2.2 Знайти або створити клієнта
                // Припускаємо, що ClientService існує і має такий метод
                // var clientResult = await ClientService.FindOrCreateClientAsync(connectionString, clientName, phone, connection);
                // Альтернатива: Використання методів репозиторію напряму
                int clientId = await BookingRepository.FindClientByPhoneAsync(connectionString, phone, connection);
                if (clientId == -1)
                {
                    clientId = await BookingRepository.CreateClientAsync(connectionString, clientName, phone, connection);
                    if (clientId <= 0)
                    {
                        await ResponseHelper.SendErrorResponse(stream, "Помилка при створенні клієнта.");
                        return;
                    }
                }
                else
                {
                    // Опціонально: оновити ім'я існуючого клієнта, якщо потрібно
                    // await BookingRepository.UpdateClientAsync(connectionString, clientId, clientName, phone, connection, null); // null бо немає транзакції тут
                }


                // 2.3 Перевірити доступність (передаємо clientId і clubId.Value)
                // Припускаємо, що AvailabilityService існує
                bool isAvailable = await AvailabilityService.CheckAvailabilityAsync(connectionString, sessionDate, startTime, endTime, sessionType, zoneCount, clubId.Value, null, connection);
                // Логіка перевірки доступності перенесена в метод AddBooking, якщо немає окремого AvailabilityService
                if (!isAvailable)
                {
                    await ResponseHelper.SendErrorResponse(stream, "Вибраний час або кількість місць недоступні для цього типу сеансу в даному клубі.");
                    // Лог вже є всередині AvailabilityService
                    return; // Вихід, якщо недоступно
                }

                // 2.4 Розрахувати ціну (передаємо clubId.Value)
                // Припускаємо, що PricingService існує
                var pricingResult = await PricingService.CalculateBookingPriceAsync(connectionString, sessionType, clubId.Value, startTime, endTime, discountId, connection);
                // Логи розрахунку вже є всередині PricingService

                // 2.5 Додати бронювання в БД (передаємо clubId.Value і ціни)
                bool bookingAdded = await BookingRepository.InsertBookingAsync(
                                            connectionString, clientId, sessionDate, startTime, endTime,
                                            zoneCount, notes, sessionType, managerId, clubId.Value,
                                            pricingResult.AppliedDiscountId, // З pricingResult
                                            pricingResult.CalculatedPrice, // З pricingResult
                                            pricingResult.FinalPrice,      // З pricingResult
                                            connection);

                if (bookingAdded)
                {
                    Console.WriteLine($"[УСПІХ] Бронювання успішно додано до бази даних! (Клуб: {clubId})");
                    await ResponseHelper.SendSuccessResponse(stream, "Бронювання успішно створено.");
                }
                else
                {
                    Console.WriteLine("[ПОМИЛКА] Помилка при додаванні бронювання до бази даних (InsertBookingAsync повернув false).");
                    await ResponseHelper.SendErrorResponse(stream, "Не вдалося додати бронювання до бази даних.");
                }
            }
            catch (MySqlException ex)
            {
                Console.WriteLine($"[ПОМИЛКА_SQL] HandleAddBooking: {ex.Message}");
                await ResponseHelper.SendErrorResponse(stream, $"Помилка бази даних при додаванні: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ПОМИЛКА_ЗАГАЛЬНА] HandleAddBooking: {ex.Message}");
                await ResponseHelper.SendErrorResponse(stream, $"Загальна помилка сервера: {ex.Message}");
            }
            finally
            {
                if (connection?.State == ConnectionState.Open)
                {
                    await connection.CloseAsync();
                    Console.WriteLine("[ЛОГ] З'єднання з БД закрито після додавання бронювання.");
                }
                if (connection != null) connection.Dispose();
            }
        }
        public static async Task HandleUpdateOrderAsync(NetworkStream stream, string connectionString, Dictionary<string, string> data)
        {
            // 1. Валідація основних ідентифікаторів
            if (!data.TryGetValue("session_id", out string sessionIdStr) ||
                !data.TryGetValue("manager_id", out string managerIdStr) ||
                !data.TryGetValue("club_id", out string clubIdStr) ||
                !BookingValidationService.IsValidOrder(sessionIdStr, managerIdStr, clubIdStr, out int sessionId, out int managerId, out int clubId))
            {
                await ResponseHelper.SendErrorResponse(stream, "Невірні або відсутні дані для ідентифікації замовлення (session_id, manager_id, club_id).");
                return;
            }

            MySqlConnection connection = null;

            try
            {
                connection = new MySqlConnection(connectionString);
                await connection.OpenAsync();
                Console.WriteLine($"[ЛОГ] З'єднання з БД відкрито для оновлення замовлення {sessionId}.");

                // 2. Збір даних для оновлення з запиту
                // Ми приймаємо, що клієнт може надіслати будь-яку комбінацію цих полів.
                // Репозиторій має вміти обробляти, які саме поля оновлювати.
                // Валідація типів (наприклад, num_people - int, final_price - decimal) відбувається тут або в репозиторії.
                // Для простоти, передаємо словник і робимо валідацію та динамічне оновлення в репозиторії.
                // Або, більш надійно, парсимо тут і передаємо перевірені значення або null.
                // Давайте зробимо парсинг тут і передамо структуровані дані.

                int? numPeople = null;
                if (data.TryGetValue("num_people", out string numPeopleStr) && int.TryParse(numPeopleStr, out int parsedNumPeople) && parsedNumPeople > 0)
                {
                    numPeople = parsedNumPeople;
                }
                // !!! Можна додати валідацію, якщо numPeopleStr присутній, але парсинг невдалий.
                // else if (data.ContainsKey("num_people")) { await ResponseHelper.SendErrorResponse(stream, "Невірний формат num_people."); return; }


                string notes = data.ContainsKey("notes") ? data["notes"] : null; // Notes може бути порожнім або відсутнім
                string sessionType = data.ContainsKey("session_type") ? data["session_type"] : null;
                string paymentStatus = data.ContainsKey("payment_status") ? data["payment_status"] : null; // Валідація статусу може бути потрібна
                decimal? finalPrice = null;
                if (data.TryGetValue("final_price", out string finalPriceStr) && decimal.TryParse(finalPriceStr.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal parsedFinalPrice) && parsedFinalPrice >= 0)
                {
                    finalPrice = parsedFinalPrice;
                }
                // !!! Можна додати валідацію для final_price

                // Перевіряємо, чи хоч одне поле для оновлення присутнє в запиті
                if (numPeople == null && notes == null && sessionType == null && paymentStatus == null && finalPrice == null &&
                    !data.ContainsKey("num_people") && !data.ContainsKey("notes") && !data.ContainsKey("session_type") && !data.ContainsKey("payment_status") && !data.ContainsKey("final_price"))
                {
                    await ResponseHelper.SendErrorResponse(stream, "Відсутні дані для оновлення замовлення.");
                    return;
                }


                // 3. Виклик методу репозиторію для оновлення в БД
                bool updated = await BookingRepository.UpdateOrderAsync(
                    connection,
                    sessionId,
                    managerId,
                    clubId,
                    numPeople, // Передаємо null, якщо не оновлюємо
                    notes,
                    sessionType,
                    paymentStatus,
                    finalPrice
                );

                if (updated)
                {
                    await ResponseHelper.SendJsonResponse(stream, new Dictionary<string, object>
                {
                    { "success", "true" },
                    { "message", $"Замовлення {sessionId} успішно оновлено." }
                });
                    Console.WriteLine($"[ЛОГ] Замовлення {sessionId} успішно оновлено.");
                }
                else
                {
                    // Може означати, що замовлення з такими ID не знайдено
                    await ResponseHelper.SendErrorResponse(stream, $"Замовлення з ID {sessionId} для менеджера {managerId} в клубі {clubId} не знайдено або не належить їм.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ПОМИЛКА] HandleUpdateOrderAsync для замовлення {sessionIdStr}: {ex.Message}");
                await ResponseHelper.SendErrorResponse(stream, "Сталася помилка при оновленні замовлення.");
            }
            finally
            {
                if (connection?.State == ConnectionState.Open)
                {
                    await connection.CloseAsync();
                    Console.WriteLine("[ЛОГ] З'єднання з БД закрито після оновлення замовлення.");
                }
                if (connection != null) connection.Dispose();
            }
        }

        public static async Task HandleRecordPaymentAsync(NetworkStream stream, string connectionString, Dictionary<string, string> data)
        {
            // 1. Валідація вхідних даних (працює з Dictionary<string, string>)
            if (!BookingValidationService.IsValidPaymentData(
                    data,
                    out int sessionId,
                    out int managerId,
                    out int clubId,
                    out decimal amount,
                    out string paymentMethod,
                    out DateTime paymentTime))
            {
                Console.WriteLine("[ПОМИЛКА ВАЛІДАЦІЇ] Невірні або відсутні дані для запису оплати."); // Логуємо валідаційну помилку
                await ResponseHelper.SendErrorResponse(stream, "Невірні або відсутні дані для запису оплати.");
                return;
            }

            // MySqlConnection connection = null; // З'єднання тепер управляється в репозиторії для транзакції

            try
            {
                Console.WriteLine($"[ЛОГ] Запис оплати: Спроба записати {amount:F2} ({paymentMethod}) для замовлення {sessionId} (Клуб: {clubId}, Менеджер: {managerId}).");

                // 2. Викликаємо НОВИЙ метод репозиторію для виконання транзакції
                bool paymentRecorded = await BookingRepository.RecordPaymentTransactionAsync(
                    connectionString, // Передаємо рядок підключення
                    sessionId,
                    managerId,
                    clubId,
                    amount,
                    paymentMethod,
                    paymentTime
                );

                if (paymentRecorded)
                {
                    await ResponseHelper.SendJsonResponse(stream, new Dictionary<string, object>
                {
                    { "success", true },
                    { "message", $"Оплата {amount:F2} ({paymentMethod}) для замовлення {sessionId} успішно записана." }
                });
                    Console.WriteLine($"[ЛОГ] Оплата для замовлення {sessionId} успішно записана.");
                }
                else
                {
                    // Репозиторій повернув false, ймовірно, через те, що замовлення не знайдено
                    // з потрібним статусом або не належить менеджеру/клубу.
                    await ResponseHelper.SendErrorResponse(stream, $"Не вдалося записати оплату для замовлення {sessionId}. Перевірте статус замовлення та належність менеджеру/клубу.");
                    Console.WriteLine($"[ЛОГ] Не вдалося записати оплату для замовлення {sessionId}. Репозиторій повернув false.");
                }
            }
            catch (Exception ex)
            {
                // Ловимо будь-які інші винятки (наприклад, проблеми з підключенням до БД, помилки SQL, які не були перехоплені в репозиторії тощо)
                Console.WriteLine($"[ПОМИЛКА] HandleRecordPaymentAsync для замовлення (ID: {sessionId}): {ex.Message}\n{ex.StackTrace}");
                await ResponseHelper.SendErrorResponse(stream, "Сталася помилка при записі оплати.");
            }
            // Блок finally для з'єднання прибрано, бо з'єднання управляється в репозиторії методом-транзакцією
        }
        /// <summary>
        /// Обробляє запит на скасування замовлення.
        /// </summary>
        public static async Task HandleCancelOrderAsync(NetworkStream stream, string connectionString, Dictionary<string, string> data)
        {
            // 1. Валідація основних ідентифікаторів
            if (!data.TryGetValue("session_id", out string sessionIdStr) ||
                !data.TryGetValue("manager_id", out string managerIdStr) ||
                !data.TryGetValue("club_id", out string clubIdStr) ||
                !BookingValidationService.IsValidOrder(sessionIdStr, managerIdStr, clubIdStr, out int sessionId, out int managerId, out int clubId))
            {
                await ResponseHelper.SendErrorResponse(stream, "Невірні або відсутні дані для ідентифікації замовлення (session_id, manager_id, club_id).");
                return;
            }

            MySqlConnection connection = null;

            try
            {
                connection = new MySqlConnection(connectionString);
                await connection.OpenAsync();
                Console.WriteLine($"[ЛОГ] З'єднання з БД відкрито для скасування замовлення {sessionId}.");

                // 2. Виклик методу репозиторію для скасування в БД
                bool cancelled = await BookingRepository.CancelOrderAsync(connection, sessionId, managerId, clubId);

                if (cancelled)
                {
                    await ResponseHelper.SendJsonResponse(stream, new Dictionary<string, object>
                {
                    { "success", "true" },
                    { "message", $"Замовлення {sessionId} успішно скасовано." }
                });
                    Console.WriteLine($"[ЛОГ] Замовлення {sessionId} успішно скасовано.");
                }
                else
                {
                    // Може означати, що замовлення з такими ID не знайдено або воно вже скасоване/завершене
                    await ResponseHelper.SendErrorResponse(stream, $"Замовлення з ID {sessionId} для менеджера {managerId} в клубі {clubId} не знайдено, не належить їм, або вже має статус, що не дозволяє скасування.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ПОМИЛКА] HandleCancelOrderAsync для замовлення {sessionIdStr}: {ex.Message}");
                await ResponseHelper.SendErrorResponse(stream, "Сталася помилка при скасуванні замовлення.");
            }
            finally
            {
                if (connection?.State == ConnectionState.Open)
                {
                    await connection.CloseAsync();
                    Console.WriteLine("[ЛОГ] З'єднання з БД закрито після скасування замовлення.");
                }
                if (connection != null) connection.Dispose();
            }
        }

        /// <summary>
        /// Обробляє запит на розрахунок вартості замовлення та оновлення поля calculate_price.
        /// </summary>
        public static async Task HandleCalculateOrderPriceAsync(NetworkStream stream, string connectionString, Dictionary<string, string> data)
        {
            // 1. Валідація основних ідентифікаторів
            if (!data.TryGetValue("session_id", out string sessionIdStr) ||
                !data.TryGetValue("manager_id", out string managerIdStr) ||
                !data.TryGetValue("club_id", out string clubIdStr) ||
                !BookingValidationService.IsValidOrder(sessionIdStr, managerIdStr, clubIdStr, out int sessionId, out int managerId, out int clubId))
            {
                await ResponseHelper.SendErrorResponse(stream, "Невірні або відсутні дані для ідентифікації замовлення (session_id, manager_id, club_id).");
                return;
            }

            MySqlConnection connection = null;

            try
            {
                connection = new MySqlConnection(connectionString);
                await connection.OpenAsync();
                Console.WriteLine($"[ЛОГ] З'єднання з БД відкрито для розрахунку ціни замовлення {sessionId}.");

                // 2. Отримання актуальних даних замовлення для розрахунку
                // Використовуємо вже існуючий метод GetSessionDetailsAsync, якщо він містить потрібні поля
                var sessionDetails = await BookingRepository.GetSessionDetailsAsync(connection, sessionId, managerId, clubId);

                if (sessionDetails == null)
                {
                    await ResponseHelper.SendErrorResponse(stream, $"Замовлення з ID {sessionId} для менеджера {managerId} в клубі {clubId} не знайдено.");
                    return;
                }

                // 3. Логіка розрахунку ціни
                // ЦЕ ПРИКЛАДОВА ЛОГІКА! Вам потрібно адаптувати її до ваших реальних правил.
                // Приклад: ціна = кількість людей * тривалість в годинах * базова ставка * коефіцієнт типу сесії
                decimal baseRatePerHourPerPerson = 100.0m; // Припустимо, 100 грн за годину за людину
                decimal sessionTypeMultiplier = 1.0m;

                // Проста логіка коефіцієнта залежно від типу сесії
                if (sessionDetails.SessionType?.ToLower() == "premium")
                {
                    sessionTypeMultiplier = 1.5m;
                }
                else if (sessionDetails.SessionType?.ToLower() == "vip")
                {
                    sessionTypeMultiplier = 2.0m;
                }
                // Додайте інші типи за необхідності

                // Розрахунок тривалості в годинах
                TimeSpan duration = sessionDetails.EndTime - sessionDetails.StartTime;
                double durationInHours = duration.TotalHours;

                // Перевірка на некоректну тривалість або кількість людей
                if (durationInHours <= 0 || sessionDetails.NumPeople <= 0)
                {
                    await ResponseHelper.SendErrorResponse(stream, $"Неможливо розрахувати ціну: некоректна тривалість ({durationInHours} год) або кількість людей ({sessionDetails.NumPeople}).");
                    return;
                }

                decimal durationDecimal = (decimal)durationInHours;
                decimal calculatedPrice = durationDecimal * sessionDetails.NumPeople * baseRatePerHourPerPerson * sessionTypeMultiplier;

                // Округлення до 2 знаків після коми
                calculatedPrice = Math.Round(calculatedPrice, 2);

                Console.WriteLine($"[ЛОГ] Розрахована ціна для замовлення {sessionId}: {calculatedPrice}");

                // 4. Оновлення поля calculate_price в БД
                bool updatedPrice = await BookingRepository.UpdateCalculatedPriceAsync(connection, sessionId, managerId, clubId, calculatedPrice);

                if (updatedPrice)
                {
                    await ResponseHelper.SendJsonResponse(stream, new Dictionary<string, object>
                {
                    { "success", "true" },
                    { "message", $"Ціна замовлення {sessionId} успішно розрахована та оновлена." },
                    { "calculated_price", calculatedPrice } // Повертаємо розраховану ціну клієнту
                });
                    Console.WriteLine($"[ЛОГ] Поле calculate_price для замовлення {sessionId} оновлено на {calculatedPrice}.");
                }
                else
                {
                    // Це може статись, якщо замовлення не знайдено між select та update (малоймовірно)
                    await ResponseHelper.SendErrorResponse(stream, $"Помилка при оновленні розрахованої ціни для замовлення {sessionId}. Можливо, замовлення було видалено.");
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ПОМИЛКА] HandleCalculateOrderPriceAsync для замовлення {sessionIdStr}: {ex.Message}");
                await ResponseHelper.SendErrorResponse(stream, "Сталася помилка при розрахунку та оновленні ціни замовлення.");
            }
            finally
            {
                if (connection?.State == ConnectionState.Open)
                {
                    await connection.CloseAsync();
                    Console.WriteLine("[ЛОГ] З'єднання з БД закрито після розрахунку ціни.");
                }
                if (connection != null) connection.Dispose();
            }
        }

        public static async Task HandleGetOrderDetailsAsync(NetworkStream stream, string connectionString, Dictionary<string, string> data)
        {
            // Отримуємо всі необхідні дані як рядки
            if (!data.TryGetValue("session_id", out string sessionIdStr) ||
                !data.TryGetValue("manager_id", out string managerIdStr) ||
                !data.TryGetValue("club_id", out string clubIdStr) ||
                !BookingValidationService.IsValidOrder(sessionIdStr, managerIdStr, clubIdStr, out int sessionId, out int managerId, out int clubId)) // !!! Змінено виклик IsValidOrder
            {
                // Повідомлення про помилку тепер більш загальне, оскільки будь-яке з трьох полів може бути невірним або відсутнім
                await ResponseHelper.SendErrorResponse(stream, "Невірні або відсутні дані для бронювання (session_id, manager_id, club_id).");
                return;
            }

            MySqlConnection connection = null;

            try
            {
                connection = new MySqlConnection(connectionString);
                await connection.OpenAsync();
                Console.WriteLine("[ЛОГ] З'єднання з БД відкрито для отримання деталей бронювання.");

                // Використовуємо валідовані int значення
                var sessionDetails = await BookingRepository.GetSessionDetailsAsync(connection, sessionId, managerId, clubId);

                if (sessionDetails == null)
                {
                    await ResponseHelper.SendErrorResponse(stream, "Сесію за вказаними даними не знайдено."); // Покращене повідомлення
                    return;
                }

                var response = new Dictionary<string, object>
        {
            { "success", "true" },
            { "session", sessionDetails }
        };

                await ResponseHelper.SendJsonResponse(stream, response);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ПОМИЛКА] HandleGetOrderDetailsAsync: {ex.Message}");
                await ResponseHelper.SendErrorResponse(stream, "Сталася помилка при отриманні деталей бронювання.");
            }
            finally
            {
                if (connection?.State == ConnectionState.Open)
                {
                    await connection.CloseAsync();
                    Console.WriteLine("[ЛОГ] З'єднання з БД закрито після отримання деталей.");
                }
                if (connection != null) connection.Dispose();
            }
        }


        // --- МЕТОД ОТРИМАННЯ БРОНЮВАНЬ ЗА ЧАСОВИМ СЛОТОМ (БЕЗ ЗМІН У ЛОГІЦІ, АЛЕ ВИКОРИСТОВУЄ ЗОВНІШНІЙ ResponseHelper) ---
        public static async Task HandleGetBookingsByTimeSlot(NetworkStream stream, string connectionString, Dictionary<string, string> data)
        {
            string logPrefix = "[HandleGetBookingsByTimeSlot]";
            Console.WriteLine($"{logPrefix} Запит отримано.");

            if (!BookingValidationService.TryParseSessionDate(data.GetValueOrDefault("session_date"), out DateTime sessionDate) ||
                !BookingValidationService.TryParseBookingTime(data.GetValueOrDefault("start_time"), data.GetValueOrDefault("end_time"), out TimeSpan startTime, out TimeSpan endTime))
            {
                Console.WriteLine($"{logPrefix} ПОМИЛКА: Невірний формат дати ({data.GetValueOrDefault("session_date")}) або часу ({data.GetValueOrDefault("start_time")}-{data.GetValueOrDefault("end_time")}).");
                await ResponseHelper.SendErrorResponse(stream, "Невірний формат дати сеансу (yyyy-MM-dd) або часу (ГГ:ХХ).");
                return;
            }
            Console.WriteLine($"{logPrefix} Дата: {sessionDate:yyyy-MM-dd}, Час: {startTime:hh\\:mm}-{endTime:hh\\:mm}");

            if (!BookingValidationService.IsValidId(data.GetValueOrDefault("manager_id"), out int managerId))
            {
                Console.WriteLine($"{logPrefix} ПОМИЛКА: Відсутній або некоректний manager_id у запиті.");
                await ResponseHelper.SendErrorResponse(stream, "Необхідно передати дійсний ID менеджера (manager_id).");
                return;
            }
            Console.WriteLine($"{logPrefix} Отримано manager_id: {managerId}");

            int? clubId = null;
            try
            {
                clubId = await BookingRepository.GetClubIdForManagerAsync(connectionString, managerId);
                if (!clubId.HasValue)
                {
                    Console.WriteLine($"{logPrefix} ПОМИЛКА: Не знайдено клуб для менеджера ID={managerId}.");
                    await ResponseHelper.SendErrorResponse(stream, $"Не вдалося знайти клуб, пов'язаний з менеджером ID {managerId}.");
                    return;
                }
                Console.WriteLine($"{logPrefix} Знайдено club_id: {clubId.Value} для менеджера ID={managerId}.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{logPrefix} ПОМИЛКА при отриманні club_id для менеджера {managerId}: {ex.Message}");
                await ResponseHelper.SendErrorResponse(stream, $"Помилка сервера при визначенні клубу менеджера: {ex.Message}");
                return;
            }

            List<Dictionary<string, object>> bookings = null;
            try
            {
                Console.WriteLine($"{logPrefix} Запит до BookingRepository.GetBookingsForTimeRangeAsync (Клуб: {clubId.Value})...");
                bookings = await BookingRepository.GetBookingsForTimeRangeAsync(connectionString, sessionDate, startTime, endTime, clubId.Value);
                Console.WriteLine($"{logPrefix} Отримано {bookings?.Count ?? 0} бронювань з репозиторію.");

                if (bookings == null)
                {
                    Console.WriteLine($"{logPrefix} ПОМИЛКА: BookingRepository повернув null замість списку.");
                    await ResponseHelper.SendErrorResponse(stream, "Помилка сервера: не вдалося отримати список бронювань.");
                    return;
                }

                string jsonResponse = "[]";
                try
                {
                    jsonResponse = JsonConvert.SerializeObject(bookings);
                    Console.WriteLine($"{logPrefix} Серіалізовано відповідь JSON (довжина: {jsonResponse.Length}).");
                }
                catch (JsonException jsonEx)
                {
                    Console.WriteLine($"{logPrefix} ПОМИЛКА серіалізації JSON: {jsonEx.Message}");
                    await ResponseHelper.SendErrorResponse(stream, "Помилка сервера при формуванні відповіді.");
                    return;
                }

                Console.WriteLine($"{logPrefix} Надсилання відповіді клієнту...");
                // Цей виклик тепер буде використовувати ЗОВНІШНІЙ ResponseHelper, який надсилає довжину
                await ResponseHelper.SendRawJsonResponse(stream, jsonResponse);
                Console.WriteLine($"{logPrefix} Відповідь надіслано успішно.");
            }
            catch (MySqlException dbEx)
            {
                Console.WriteLine($"{logPrefix} ПОМИЛКА БАЗИ ДАНИХ при отриманні бронювань: {dbEx.Message}\n{dbEx.StackTrace}");
                await ResponseHelper.SendErrorResponse(stream, $"Помилка бази даних при отриманні бронювань: {dbEx.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{logPrefix} ЗАГАЛЬНА ПОМИЛКА: {ex.GetType().Name} - {ex.Message}\n{ex.StackTrace}");
                await ResponseHelper.SendErrorResponse(stream, $"Внутрішня помилка сервера при отриманні бронювань: {ex.Message}");
            }
        }

        // Методи HandleCheckAvailability, HandleGetPrices, HandleDeleteBooking, HandleUpdateBooking, HandleGetCurrentBookings
        // Залишаються без змін, але тепер вони також будуть використовувати ЗОВНІШНІЙ ResponseHelper
        // (Якщо вони викликають SendJsonResponse або SendErrorResponse/SendSuccessResponse, які в свою чергу викликають SendJsonResponse)
        public static async Task HandleCheckAvailability(NetworkStream stream, string connectionString, Dictionary<string, string> data)
        {
            Console.WriteLine("[ЗАПИТ] Отримано запит на перевірку доступності.");
            // 1. Валідація вхідних даних
            if (!BookingValidationService.TryParseSessionDate(data.GetValueOrDefault("session_date"), out DateTime sessionDate) ||
               !BookingValidationService.TryParseBookingTime(data.GetValueOrDefault("start_time"), data.GetValueOrDefault("end_time"), out TimeSpan startTime, out TimeSpan endTime) ||
               !BookingValidationService.IsValidZoneCount(data.GetValueOrDefault("zone_count"), out int zoneCount) ||
               !BookingValidationService.IsValidSessionType(data.GetValueOrDefault("session_type")) ||
               !BookingValidationService.IsValidId(data.GetValueOrDefault("club_id"), out int clubId))
            {
                Console.WriteLine($"[ПОМИЛКА_ЗАПИТУ] Невірний формат даних для перевірки доступності.");
                await ResponseHelper.SendErrorResponse(stream, "Невірний формат дати, часу, кількості місць, типу сеансу або ID клубу.");
                return;
            }
            string sessionType = data["session_type"];
            int? editingSessionId = null;
            if (BookingValidationService.IsValidId(data.GetValueOrDefault("editing_session_id"), out int parsedEditingId))
            {
                editingSessionId = parsedEditingId;
            }

            // 2. Перевірка доступності
            try
            {
                // Припускаємо, що AvailabilityService існує
                bool isAvailable = await AvailabilityService.CheckAvailabilityAsync(connectionString, sessionDate, startTime, endTime, sessionType, zoneCount, clubId, editingSessionId);
                // Лог доступності вже є всередині AvailabilityService

                Dictionary<string, object> responseData = new Dictionary<string, object> { { "success", true }, { "available", isAvailable } };
                Console.WriteLine($"[ВІДПОВІДЬ] Доступність в клубі {clubId}: {isAvailable}. Надсилаємо клієнту.");
                await ResponseHelper.SendJsonResponse(stream, responseData);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ПОМИЛКА_ЗАГАЛЬНА] HandleCheckAvailability: {ex.Message}");
                await ResponseHelper.SendErrorResponse(stream, $"Помилка сервера при перевірці доступності: {ex.Message}");
            }
        }
        public static async Task HandleGetPrices(NetworkStream stream, string connectionString, Dictionary<string, string> data)
        {
            Console.WriteLine("[ЗАПИТ] Отримано запит на отримання цін.");
            int? filterClubId = null;
            if (BookingValidationService.IsValidId(data.GetValueOrDefault("club_id"), out int parsedClubId))
            {
                filterClubId = parsedClubId;
            }

            try
            {
                Dictionary<string, decimal> prices = await BookingRepository.GetPricesAsync(connectionString, filterClubId);

                var responseData = prices?.ToDictionary(kvp => kvp.Key, kvp => (object)kvp.Value) ?? new Dictionary<string, object>();
                responseData["success"] = true; // Завжди успіх, навіть якщо ціни не знайдено

                if (prices == null || prices.Count == 0)
                {
                    Console.WriteLine($"[ПОПЕРЕДЖЕННЯ] Ціни не знайдено (Клуб: {filterClubId?.ToString() ?? "Будь-який"}).");
                    // Додаємо нульові ціни для стандартних типів, якщо їх немає
                    var defaultTypes = new[] { "VR", "PS", "Quest" };
                    foreach (var type in defaultTypes)
                    {
                        if (!responseData.ContainsKey(type)) responseData[type] = 0m;
                    }
                }

                Console.WriteLine($"[ВІДПОВІДЬ] Знайдено {prices?.Count ?? 0} цін. Надсилаємо клієнту.");
                await ResponseHelper.SendJsonResponse(stream, responseData);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ПОМИЛКА_ЗАГАЛЬНА] HandleGetPrices: {ex.Message}");
                await ResponseHelper.SendErrorResponse(stream, $"Помилка сервера при отриманні цін: {ex.Message}");
            }
        }
        public static async Task HandleDeleteBooking(NetworkStream stream, string connectionString, Dictionary<string, string> data)
        {
            Console.WriteLine("[ЗАПИТ] Отримано запит на видалення бронювання.");
            // 1. Валідація
            if (!BookingValidationService.IsValidId(data.GetValueOrDefault("session_id"), out int sessionId))
            {
                Console.WriteLine("[ПОМИЛКА_ЗАПИТУ] Не вказано дійсний session_id для видалення.");
                await ResponseHelper.SendErrorResponse(stream, "Необхідно вказати дійсний ID бронювання (session_id) для видалення.");
                return;
            }

            // 2. Видалення
            try
            {
                bool success = await BookingRepository.DeleteBookingAsync(connectionString, sessionId);
                if (success)
                {
                    await ResponseHelper.SendSuccessResponse(stream, $"Бронювання ID {sessionId} успішно видалено.");
                    Console.WriteLine($"[УСПІХ] Бронювання ID {sessionId} успішно видалено з БД.");
                }
                else
                {
                    // Можливо, бронювання вже було видалено або не існувало
                    await ResponseHelper.SendErrorResponse(stream, $"Не вдалося видалити бронювання ID {sessionId}. Можливо, його не існує.");
                    Console.WriteLine($"[ПОПЕРЕДЖЕННЯ] Не вдалося видалити бронювання ID {sessionId} (DeleteBookingAsync повернув false).");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ПОМИЛКА_ЗАГАЛЬНА] HandleDeleteBooking: {ex.Message}");
                await ResponseHelper.SendErrorResponse(stream, $"Внутрішня помилка сервера при видаленні: {ex.Message}");
            }
        }
        public static async Task HandleUpdateBooking(NetworkStream stream, string connectionString, Dictionary<string, string> data)
        {
            Console.WriteLine("[ЗАПИТ] Отримано запит на оновлення бронювання.");
            Console.WriteLine("--- Step 1: Початок HandleUpdateBooking ---");

            // 1. Парсинг та валідація вхідних даних
            string sessionIdStr = data.GetValueOrDefault("session_id");
            string clientIdStr = data.GetValueOrDefault("client_id");
            string clientName = data.GetValueOrDefault("client_name");
            string phoneNumber = data.GetValueOrDefault("phone_number");
            string sessionDateStr = data.GetValueOrDefault("session_date");
            string startTimeStr = data.GetValueOrDefault("start_time");
            string endTimeStr = data.GetValueOrDefault("end_time");
            string numPeopleStr = data.GetValueOrDefault("num_people");
            string sessionType = data.GetValueOrDefault("session_type");
            string discountIdStr = data.GetValueOrDefault("discount_id");
            string notes = data.GetValueOrDefault("notes");
            string finalPriceStr = data.GetValueOrDefault("final_price");

            Console.WriteLine("--- Step 2: Дані спарсені, початок валідації ---");
            Console.WriteLine($"[DEBUG] Parsed Data: SessionId={sessionIdStr}, ClientId={clientIdStr}, Name={clientName}, Phone={phoneNumber}, Date={sessionDateStr}, Start={startTimeStr}, End={endTimeStr}, People={numPeopleStr}, Type={sessionType}, Discount={discountIdStr}, Notes='{notes}', FinalPrice={finalPriceStr}");


            if (!BookingValidationService.IsValidId(sessionIdStr, out int sessionId) ||
                !BookingValidationService.IsValidId(clientIdStr, out int clientId) ||
                string.IsNullOrWhiteSpace(clientName) || !BookingValidationService.IsValidName(clientName) ||
                string.IsNullOrWhiteSpace(phoneNumber) || !BookingValidationService.IsValidPhoneNumber(phoneNumber) ||
                !BookingValidationService.TryParseSessionDate(sessionDateStr, out DateTime sessionDate) ||
                !BookingValidationService.TryParseBookingTime(startTimeStr, endTimeStr, out TimeSpan startTime, out TimeSpan endTime) ||
                !BookingValidationService.IsValidZoneCount(numPeopleStr, out int numPeople) ||
                string.IsNullOrWhiteSpace(sessionType) || !BookingValidationService.IsValidSessionType(sessionType) ||
                !BookingValidationService.TryParseDecimal(finalPriceStr, out decimal finalPrice))
            {
                Console.WriteLine($"--- Step 3: Помилка валідації ---");
                Console.WriteLine($"[ПОМИЛКА ВАЛІДАЦІЇ] Невірний формат або відсутні обов'язкові дані для оновлення бронювання. session_id={sessionIdStr}, client_id={clientIdStr}, client_name='{clientName}', phone_number='{phoneNumber}', session_date='{sessionDateStr}', start_time='{startTimeStr}', end_time='{endTimeStr}', num_people='{numPeopleStr}', session_type='{sessionType}', final_price='{finalPriceStr}'");
                await ResponseHelper.SendErrorResponse(stream, "Невірний формат або відсутні обов'язкові дані для оновлення бронювання (перевірте усі поля).");
                return; // ВИХІД ПІСЛЯ ПОМИЛКИ ВАЛІДАЦІЇ
            }

            Console.WriteLine("--- Step 3: Валідація успішна ---");

            // Парсинг необов'язкового discount_id
            int? discountId = null;
            if (!string.IsNullOrEmpty(discountIdStr))
            {
                if (BookingValidationService.IsValidId(discountIdStr, out int parsedDiscountId))
                {
                    discountId = parsedDiscountId;
                }
                else
                {
                    Console.WriteLine($"--- Step 3a: Помилка валідації discount_id ---");
                    Console.WriteLine($"[ПОМИЛКА ВАЛІДАЦІЇ] Невірний формат discount_id: '{discountIdStr}'.");
                    await ResponseHelper.SendErrorResponse(stream, "Невірний формат ID знижки.");
                    return; // ВИХІД ПІСЛЯ ПОМИЛКИ ВАЛІДАЦІЇ DISCOUNT_ID
                }
            }
            Console.WriteLine($"[DEBUG] Parsed and Validated: SessionId={sessionId}, ClientId={clientId}, NumPeople={numPeople}, SessionType={sessionType}, DiscountId={discountId}, FinalPrice={finalPrice}");


            // 2. Логіка оновлення
            MySqlConnection connection = null;
            MySqlTransaction transaction = null;
            try
            {
                Console.WriteLine("--- Step 4: Початок блоку try ---");

                // 2.1 Отримати club_id сесії
                Console.WriteLine("--- Step 4a: Отримання club_id ---");
                int? currentClubId = await BookingRepository.GetSessionClubIdAsync(connectionString, sessionId);
                if (!currentClubId.HasValue)
                {
                    Console.WriteLine($"--- Step 4a: Помилка отримання club_id ---");
                    Console.WriteLine($"[ПОМИЛКА] Не вдалося знайти club_id для сесії ID={sessionId}, що оновлюється.");
                    await ResponseHelper.SendErrorResponse(stream, $"Помилка: не вдалося знайти клуб для сесії ID={sessionId}, що оновлюється.");
                    return; // ВИХІД ПІСЛЯ ПОМИЛКИ CLUB_ID
                }
                Console.WriteLine($"[ЛОГ] Знайдено club_id={currentClubId.Value} для сесії ID={sessionId}.");
                Console.WriteLine("--- Step 4a: club_id отримано ---");


                // 2.2 Перевірити доступність для нового часу/кількості місць/типу
                Console.WriteLine("--- Step 5: Відкриття з'єднання для перевірки доступності та транзакції ---");
                connection = new MySqlConnection(connectionString);
                await connection.OpenAsync();
                Console.WriteLine("[ЛОГ] З'єднання з БД відкрито для оновлення замовлення."); // Цей лог переміщено сюди

                Console.WriteLine("--- Step 6: Виклик CheckAvailabilityAsync ---");
                bool isAvailable = await AvailabilityService.CheckAvailabilityAsync(
                    connectionString, // Або connection, залежно від сигнатури вашого методу CheckAvailabilityAsync
                    sessionDate,
                    startTime,
                    endTime,
                    sessionType,
                    numPeople,
                    currentClubId.Value,
                    sessionId,
                    connection
                );
                Console.WriteLine($"--- Step 6a: CheckAvailabilityAsync повернув: {isAvailable} ---");


                if (!isAvailable)
                {
                    Console.WriteLine($"--- Step 7: Перевірка доступності не пройдена ---");
                    Console.WriteLine($"[ПОМИЛКА] Перевірка доступності не пройдена. Скасування оновлення.");
                    // Закриття з'єднання відбувається у блоці finally
                    // if (connection?.State == ConnectionState.Open) await connection.CloseAsync(); // Закриваємо у finally
                    // if (connection != null) connection.Dispose(); // Звільняємо у finally

                    // Надіслати помилкову відповідь і вийти
                    await ResponseHelper.SendErrorResponse(stream, $"Вибрана кількість людей або тип сесії недоступні на цей час.");
                    return; // ВИХІД ПІСЛЯ ПОМИЛКИ ДОСТУПНОСТІ
                }

                Console.WriteLine("--- Step 7: Перевірка доступності пройдена. Можна зберігати ---");


                // 2.3 Почати транзакцію
                Console.WriteLine("--- Step 8: Початок транзакції ---");
                transaction = await connection.BeginTransactionAsync();
                Console.WriteLine($"[ТРАНЗАКЦІЯ] Розпочато для оновлення бронювання ID: {sessionId}");
                Console.WriteLine("--- Step 8: Транзакція розпочата ---");


                // 2.4 Оновити клієнта (в рамках транзакції)
                Console.WriteLine("--- Step 9: Оновлення клієнта ---");
                bool clientUpdated = await BookingRepository.UpdateClientAsync(connectionString, clientId, clientName, phoneNumber, connection, transaction); // Переконайтесь, що метод UpdateClientAsync логує rowsAffected
                if (!clientUpdated)
                {
                    Console.WriteLine($"[ПОПЕРЕДЖЕННЯ] UpdateClientAsync для ID {clientId} не змінив жодного рядка. (rowsAffected=0)"); // Оновлено лог
                }
                else
                {
                    Console.WriteLine($"[ЛОГ] Дані клієнта ID {clientId} оновлено в рамках транзакції. (rowsAffected > 0)"); // Оновлено лог
                }
                Console.WriteLine("--- Step 9: Оновлення клієнта завершено ---");


                // 2.5 Оновити сесію (в рамках транзакції)
                Console.WriteLine("--- Step 10: Оновлення сесії ---");
                // Викликаємо метод, який оновлює всі поля, крім calculate_price (як ми обговорювали раніше)
                // Переконайтесь, що цей метод оновлює ВСІ поля, які мають оновлюватись (ім'я, телефон, дата, час, кількість людей, тип, нотатки, discount_id, final_price)
                // Переконайтесь, що метод BookingRepository.UpdateSessionDetailsAndPriceAsync логує rowsAffected
                bool sessionUpdated = await BookingRepository.UpdateSessionDetailsAndPriceAsync(
                    connectionString, // Або приберіть, якщо метод приймає лише connection
                    sessionId,
                    clientId, // Якщо clientId може змінюватись у замовленні
                    sessionDate,
                    startTime,
                    endTime,
                    numPeople, // ОНОВЛЮЄМО КІЛЬКІСТЬ ЛЮДЕЙ!
                    sessionType, // ОНОВЛЮЄМО ТИП!
                    notes,       // ОНОВЛЮЄМО НОТАТКИ!
                    discountId,  // ОНОВЛЮЄМО DISCOUNT_ID!
                    finalPrice,  // ОНОВЛЮЄМО FINAL_PRICE!
                    connection,
                    transaction);


                if (!sessionUpdated)
                {
                    Console.WriteLine($"--- Step 10a: Помилка оновлення сесії (rowsAffected=0) ---");
                    Console.WriteLine($"[ПОМИЛКА] UpdateSessionDetailsAndPriceAsync для ID {sessionId} не знайшов або не оновив рядок.");
                    // Транзакція буде відкочена в catch блоці
                    throw new Exception($"Не вдалося знайти або оновити сесію з ID {sessionId}. Можливо, її видалили.");
                }
                Console.WriteLine("--- Step 10: Оновлення сесії завершено (rowsAffected > 0) ---");


                // 2.6 Завершити транзакцію
                Console.WriteLine("--- Step 11: Спроба COMMIT ---");
                Console.WriteLine($"[ТРАНЗАКЦІЯ] Спроба COMMIT для бронювання ID: {sessionId}"); // Лог перед комітом
                await transaction.CommitAsync();
                Console.WriteLine($"[ТРАНЗАКЦІЯ] Успішно завершено (Commit) для оновлення бронювання ID {sessionId}."); // Лог після коміту
                Console.WriteLine("--- Step 11: COMMIT успішно ---");


                // 2.7 Надіслати успішну відповідь
                Console.WriteLine("--- Step 12: Надсилання успішної відповіді ---");
                // [ЛОГ] Замовлення {sessionId} успішно оновлено. // Цей лог переміщено сюди
                Console.WriteLine($"[ЛОГ] Замовлення {sessionId} успішно оновлено.");
                await ResponseHelper.SendSuccessResponse(stream, $"Замовлення {sessionId} успішно оновлено."); // Використовуємо стандартну успішну відповідь
                Console.WriteLine("--- Step 12: Успішна відповідь надіслана ---");


            } // КІНЕЦЬ БЛОКУ try
            catch (Exception ex)
            {
                Console.WriteLine($"--- Step 99: Початок блоку catch ---");
                Console.WriteLine($"[ПОМИЛКА] HandleUpdateBooking (Session ID: {sessionId}): {ex.Message}\n{ex.StackTrace}");
                // Відкат транзакції, якщо вона існує і активна
                if (transaction != null)
                {
                    try
                    {
                        Console.WriteLine($"--- Step 99a: Спроба відкоту транзакції ---");
                        await transaction.RollbackAsync();
                        Console.WriteLine($"[ТРАНЗАКЦІЯ] Відкочено для оновлення бронювання ID {sessionId} через помилку.");
                        Console.WriteLine($"--- Step 99a: Відкат успішно ---");
                    }
                    catch (Exception rollbackEx)
                    {
                        Console.WriteLine($"--- Step 99a: Помилка під час відкоту транзакції ---");
                        Console.WriteLine($"[ПОМИЛКА] Помилка під час відкочування транзакції для бронювання ID {sessionId}: {rollbackEx.Message}");
                    }
                }
                Console.WriteLine($"--- Step 99b: Надсилання помилкової відповіді ---");
                // Відправляємо повідомлення про помилку клієнту
                await ResponseHelper.SendErrorResponse(stream, $"Помилка сервера при оновленні бронювання: {ex.Message}");
                Console.WriteLine($"--- Step 99: Блок catch завершено ---");
            } // КІНЕЦЬ БЛОКУ catch
            finally
            {
                Console.WriteLine($"--- Step 100: Початок блоку finally ---");
                // Закриття з'єднання
                if (connection?.State == ConnectionState.Open)
                {
                    Console.WriteLine($"--- Step 100a: Закриття з'єднання ---");
                    await connection.CloseAsync();
                }
                if (connection != null) connection.Dispose();
                if (transaction != null) transaction.Dispose(); // Транзакція також потребує Dispose
                Console.WriteLine($"[ЛОГ] Ресурси звільнено після спроби оновлення бронювання ID: {sessionId}");
                Console.WriteLine($"--- Step 100: Блок finally завершено ---");
            } // КІНЕЦЬ БЛОКУ finally

            Console.WriteLine("--- Step 101: Кінець HandleUpdateBooking ---");
        }
        public static async Task HandleGetCurrentBookings(NetworkStream stream, string connectionString, Dictionary<string, string> data)
        {
            Console.WriteLine("[ЗАПИТ] Отримано запит на поточні бронювання.");
            // 1. Валідація вхідних даних
            if (!BookingValidationService.TryParseSessionDate(data.GetValueOrDefault("current_date"), out DateTime currentDate) ||
               !TimeSpan.TryParseExact(data.GetValueOrDefault("current_time"), @"hh\:mm", CultureInfo.InvariantCulture, TimeSpanStyles.None, out TimeSpan currentTime)) // Проста перевірка часу
            {
                Console.WriteLine($"[ПОМИЛКА_ЗАПИТУ] Невірний формат дати/часу для GetCurrentBookings.");
                await ResponseHelper.SendErrorResponse(stream, "Невірний формат поточної дати (yyyy-MM-dd) або часу (ГГ:ХХ).");
                return;
            }

            // 2. Отримання даних
            try
            {
                List<Dictionary<string, object>> currentBookings = await BookingRepository.GetCurrentBookingsAsync(connectionString, currentDate, currentTime);
                string jsonResponse = JsonConvert.SerializeObject(currentBookings);
                Console.WriteLine($"[ВІДПОВІДЬ] Знайдено {currentBookings.Count} поточних бронювань. Надсилаємо клієнту.");
                await ResponseHelper.SendRawJsonResponse(stream, jsonResponse);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ПОМИЛКА_ЗАГАЛЬНА] HandleGetCurrentBookings: {ex.Message}");
                await ResponseHelper.SendErrorResponse(stream, $"Помилка сервера при отриманні поточних бронювань: {ex.Message}");
            }
        }
        // Метод IsValidSessionType (без змін)
        public static bool IsValidSessionType(string sessionType)
        {
            var validTypes = new HashSet<string> { "PS", "VR", "Quest", /* інші типи... */ };
            return !string.IsNullOrWhiteSpace(sessionType) && validTypes.Contains(sessionType);
        }

        // Метод AddBooking (без змін)
        public static async Task AddBooking(
            NetworkStream stream,
            string connectionString,
            DateTime sessionDate,
            string startTimeStr,
            string endTimeStr,
            string clientName,
            string phone,
            int numPeople,
            string sessionType,
            string notes,
            int managerId)
        {
            // --- 1. Валідація та Парсинг ---
            TimeSpan startTime, endTime;
            if (!TimeSpan.TryParse(startTimeStr, out startTime) || !TimeSpan.TryParse(endTimeStr, out endTime))
            {
                Console.WriteLine($"[SERVICE_ERROR] AddBooking: Invalid time format. Start: '{startTimeStr}', End: '{endTimeStr}'");
                await ResponseHelper.SendErrorResponse(stream, "Невірний формат часу.");
                return;
            }

            if (endTime <= startTime)
            {
                Console.WriteLine($"[SERVICE_ERROR] AddBooking: End time must be after start time.");
                await ResponseHelper.SendErrorResponse(stream, "Час закінчення має бути пізнішим за час початку.");
                return;
            }

            // Додаткова валідація типу сесії (якщо потрібно)
            if (!IsValidSessionType(sessionType)) // Перевіряємо тут
            {
                Console.WriteLine($"[SERVICE_ERROR] AddBooking: Invalid session type: {sessionType}");
                await ResponseHelper.SendErrorResponse(stream, $"Неприпустимий тип сесії: {sessionType}");
                return;
            }

            // --- 2. Логіка Бронювання (з транзакцією) ---
            MySqlConnection connection = null;
            MySqlTransaction transaction = null;
            try
            {
                connection = new MySqlConnection(connectionString);
                await connection.OpenAsync();
                transaction = await connection.BeginTransactionAsync();
                Console.WriteLine($"[SERVICE_INFO] AddBooking: Transaction started.");

                // a) Отримати club_id менеджера
                int? clubId = await BookingRepository.GetClubIdForManagerAsync(connectionString, managerId, connection, transaction);
                if (!clubId.HasValue)
                {
                    Console.WriteLine($"[SERVICE_ERROR] AddBooking: Could not find club for manager ID: {managerId}");
                    await ResponseHelper.SendErrorResponse(stream, "Не вдалося визначити клуб для менеджера.");
                    await transaction.RollbackAsync(); // Відкат транзакції
                    return;
                }
                Console.WriteLine($"[SERVICE_INFO] AddBooking: Manager {managerId} belongs to Club {clubId.Value}.");

                // b) Знайти або створити клієнта
                int clientId = await BookingRepository.FindClientByPhoneAsync(connectionString, phone, connection, transaction);
                if (clientId == -1) // Клієнта не знайдено, створюємо
                {
                    Console.WriteLine($"[SERVICE_INFO] AddBooking: Client with phone {phone} not found. Creating...");
                    clientId = await BookingRepository.CreateClientAsync(connectionString, clientName, phone, connection, transaction);
                    if (clientId <= 0)
                    {
                        Console.WriteLine($"[SERVICE_ERROR] AddBooking: Failed to create client.");
                        await ResponseHelper.SendErrorResponse(stream, "Не вдалося створити нового клієнта.");
                        await transaction.RollbackAsync();
                        return;
                    }
                    Console.WriteLine($"[SERVICE_INFO] AddBooking: Client created with ID: {clientId}");
                }
                else
                {
                    Console.WriteLine($"[SERVICE_INFO] AddBooking: Found client with ID: {clientId}");
                    // Потенційно тут можна додати логіку оновлення імені клієнта, якщо воно змінилося:
                    // await BookingRepository.UpdateClientAsync(connectionString, clientId, clientName, phone, connection, transaction);
                }

                // c) Перевірити доступність зон/місць
                //    Визначаємо групу типів сесій для перевірки конфліктів
                string sessionTypeGroup = GetSessionTypeGroup(sessionType);
                int bookedZones = await BookingRepository.GetBookedZonesAsync(connectionString, sessionDate, startTime, endTime, clubId.Value, sessionTypeGroup, null, connection, transaction);

                // Потрібно знати максимальну кількість зон для цього типу сесії/групи в цьому клубі
                int maxZones = await GetMaxZonesForClubAsync(connectionString, clubId.Value, sessionTypeGroup, connection, transaction); // **ПОТРІБНО РЕАЛІЗУВАТИ GetMaxZonesForClubAsync**

                if (maxZones == -1)
                { // Помилка отримання ліміту
                    await ResponseHelper.SendErrorResponse(stream, "Не вдалося перевірити ліміт зон для клубу.");
                    await transaction.RollbackAsync();
                    return;
                }

                if (bookedZones + numPeople > maxZones)
                {
                    Console.WriteLine($"[SERVICE_WARN] AddBooking: Not enough zones available. Booked: {bookedZones}, Requested: {numPeople}, Max: {maxZones}");
                    await ResponseHelper.SendErrorResponse(stream, $"Недостатньо вільних місць/зон ({maxZones - bookedZones} доступно) на вибраний час.");
                    await transaction.RollbackAsync();
                    return;
                }
                Console.WriteLine($"[SERVICE_INFO] AddBooking: Availability check passed. Booked: {bookedZones}, Requested: {numPeople}, Max: {maxZones}");


                // d) Розрахувати ціну (базову)
                decimal pricePerHour = await BookingRepository.GetPricePerHourAsync(connectionString, sessionType, clubId, connection, transaction);
                if (pricePerHour < 0)
                {
                    Console.WriteLine($"[SERVICE_ERROR] AddBooking: Could not get price for session type: {sessionType}");
                    await ResponseHelper.SendErrorResponse(stream, "Не вдалося отримати ціну для типу сесії.");
                    await transaction.RollbackAsync();
                    return;
                }
                TimeSpan duration = endTime - startTime;
                decimal calculatedPrice = pricePerHour * (decimal)duration.TotalHours * numPeople; // Ціна залежить від кількості людей/зон? Чи фіксована за годину? Адаптуйте!

                // e) Розрахувати фінальну ціну (поки без знижки, бо її ID не передається)
                //    Якщо планується передача discount_id, додати логіку отримання знижки тут
                decimal finalPrice = calculatedPrice; // Поки що фінальна ціна = розрахована
                int? discountId = null; // Поки немає знижки


                // f) Вставити бронювання
                bool success = await BookingRepository.InsertBookingAsync(
                    connectionString, clientId, sessionDate, startTime, endTime,
                    numPeople, notes, sessionType, managerId, clubId.Value,
                    discountId, calculatedPrice, finalPrice, // Передаємо ціни
                    connection, transaction);

                if (success)
                {
                    await transaction.CommitAsync();
                    Console.WriteLine($"[SERVICE_SUCCESS] AddBooking: Booking successful for client {clientId} at club {clubId}.");
                    // Надіслати успішну відповідь
                    var response = new Dictionary<string, object> { { "status", "success" }, { "message", "Бронювання успішно створено." } /* Можна додати ID сесії */ };
                    await ResponseHelper.SendJsonResponse(stream, response);
                }
                else
                {
                    Console.WriteLine($"[SERVICE_ERROR] AddBooking: Failed to insert booking into database.");
                    await transaction.RollbackAsync();
                    await ResponseHelper.SendErrorResponse(stream, "Не вдалося зберегти бронювання в базі даних.");
                }
            }
            catch (MySqlException myEx)
            {
                Console.WriteLine($"[SERVICE_ERROR] AddBooking (MySQL): {myEx.Message}\n{myEx.StackTrace}");
                if (transaction != null) await TryRollbackTransaction(transaction);
                await ResponseHelper.SendErrorResponse(stream, $"Помилка бази даних при створенні бронювання: {myEx.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SERVICE_ERROR] AddBooking (General): {ex.GetType().Name} - {ex.Message}\n{ex.StackTrace}");
                if (transaction != null) await TryRollbackTransaction(transaction);
                await ResponseHelper.SendErrorResponse(stream, $"Внутрішня помилка сервера при створенні бронювання: {ex.Message}");
            }
            finally
            {
                if (connection?.State == System.Data.ConnectionState.Open)
                {
                    await connection.CloseAsync();
                    Console.WriteLine($"[SERVICE_INFO] AddBooking: Connection closed.");
                }
            }
        }
        public static async Task HandleGetNewBookingsAsync(NetworkStream stream, string connectionString, Dictionary<string, string> data)
        {
            string logPrefix = "[HandleGetNewBookingsAsync]";
            Console.WriteLine($"{logPrefix} Запит отримано.");

            // 1. Валідація вхідних даних
            if (!BookingValidationService.IsValidId(data.GetValueOrDefault("club_id"), out int clubId))
            {
                Console.WriteLine($"{logPrefix} ПОМИЛКА: Відсутній або некоректний club_id у запиті.");
                await ResponseHelper.SendErrorResponse(stream, "Необхідно передати дійсний ID клубу (club_id).");
                return;
            }
            Console.WriteLine($"{logPrefix} Отримано club_id: {clubId}");

            try
            {
                Console.WriteLine($"{logPrefix} Запит до BookingRepository.GetNewBookingsAsync (Клуб: {clubId})...");
                // 2. Викликаємо метод репозиторію для отримання нових бронювань
                var newBookings = await BookingRepository.GetNewBookingsAsync(connectionString, clubId);
                Console.WriteLine($"{logPrefix} Отримано {newBookings?.Count ?? 0} нових бронювань з репозиторію.");

                if (newBookings == null)
                {
                    Console.WriteLine($"{logPrefix} ПОМИЛКА: BookingRepository повернув null замість списку.");
                    // Можна вважати це внутрішньою помилкою сервера
                    await ResponseHelper.SendErrorResponse(stream, "Помилка сервера: не вдалося отримати список нових бронювань.");
                    return;
                }

                // 3. Формуємо відповідь для клієнта
                // Відповідь має бути у форматі {"success": true, "bookings": [...]}
                var responseData = new Dictionary<string, object>
                {
                    { "success", true },
                    { "bookings", newBookings } // Список словників під ключем "bookings"
                };

                await ResponseHelper.SendJsonResponse(stream, responseData);
                Console.WriteLine($"{logPrefix} Відповідь надіслано успішно. Повернуто {newBookings.Count} бронювань.");
            }
            catch (Exception ex)
            {
                // Ловимо винятки з репозиторію
                Console.WriteLine($"{logPrefix} ЗАГАЛЬНА ПОМИЛКА при отриманні нових бронювань: {ex.GetType().Name} - {ex.Message}\n{ex.StackTrace}");
                await ResponseHelper.SendErrorResponse(stream, $"Внутрішня помилка сервера при отриманні нових бронювань: {ex.Message}");
            }
        }

        /// <summary>
        /// Обробляє запит на видалення бронювання.
        /// </summary>
        /// <param name="stream">Мережевий потік для відповіді.</param>
        /// <param name="connectionString">Рядок підключення до БД.</param>
        /// <param name="data">Словник вхідних даних (очікується "booking_id").</param>
        public static async Task HandleDeleteBookingAsync(NetworkStream stream, string connectionString, Dictionary<string, string> data)
        {
            string logPrefix = "[HandleDeleteBookingAsync]";
            Console.WriteLine($"{logPrefix} Запит отримано.");

            // 1. Валідація вхідних даних
            if (!BookingValidationService.IsValidId(data.GetValueOrDefault("booking_id"), out int bookingId)) // Клієнт шле "booking_id", мапимо на session_id
            {
                Console.WriteLine($"{logPrefix} ПОМИЛКА: Відсутній або некоректний booking_id у запиті.");
                await ResponseHelper.SendErrorResponse(stream, "Необхідно передати дійсний ID бронювання (booking_id).");
                return;
            }
            Console.WriteLine($"{logPrefix} Отримано booking_id: {bookingId}");

            try
            {
                Console.WriteLine($"{logPrefix} Запит до BookingRepository.DeleteBookingAsync (ID: {bookingId})...");
                // 2. Викликаємо метод репозиторію для видалення (існуючий метод DeleteBookingAsync підходить)
                bool success = await BookingRepository.DeleteBookingAsync(connectionString, bookingId); // Використовуємо існуючий метод DeleteBookingAsync
                Console.WriteLine($"{logPrefix} BookingRepository.DeleteBookingAsync повернув: {success}.");

                // 3. Формуємо відповідь
                if (success)
                {
                    await ResponseHelper.SendSuccessResponse(stream, $"Бронювання ID {bookingId} успішно видалено.");
                    Console.WriteLine($"{logPrefix} Бронювання ID {bookingId} успішно видалено.");
                }
                else
                {
                    // Це може означати, що бронювання не знайдено (було вже видалено або ID невірний)
                    await ResponseHelper.SendErrorResponse(stream, $"Не вдалося видалити бронювання ID {bookingId}. Можливо, воно не існує.");
                    Console.WriteLine($"{logPrefix} Не вдалося видалити бронювання ID {bookingId} (Repo повернув false).");
                }
            }
            catch (Exception ex)
            {
                // Ловимо винятки з репозиторію (наприклад, проблеми з БД)
                Console.WriteLine($"{logPrefix} ЗАГАЛЬНА ПОМИЛКА при видаленні бронювання ID {bookingId}: {ex.GetType().Name} - {ex.Message}\n{ex.StackTrace}");
                await ResponseHelper.SendErrorResponse(stream, $"Внутрішня помилка сервера при видаленні бронювання: {ex.Message}");
            }
        }


        /// <summary>
        /// Обробляє запит на зміну статусу бронювання.
        /// </summary>
        /// <param name="stream">Мережевий потік для відповіді.</param>
        /// <param name="connectionString">Рядок підключення до БД.</param>
        /// <param name="data">Словник вхідних даних (очікується "booking_id" та "status").</param>
        public static async Task HandleUpdateBookingStatusAsync(NetworkStream stream, string connectionString, Dictionary<string, string> data)
        {
            string logPrefix = "[HandleUpdateBookingStatusAsync]";
            Console.WriteLine($"{logPrefix} Запит отримано.");

            // 1. Валідація вхідних даних
            if (!BookingValidationService.IsValidId(data.GetValueOrDefault("booking_id"), out int bookingId)) // Клієнт шле "booking_id"
            {
                Console.WriteLine($"{logPrefix} ПОМИЛКА: Відсутній або некоректний booking_id у запиті.");
                await ResponseHelper.SendErrorResponse(stream, "Необхідно передати дійсний ID бронювання (booking_id).");
                return;
            }
            // Отримуємо статус (очікуємо "Pending")
            if (!data.TryGetValue("status", out string newStatus) || string.IsNullOrWhiteSpace(newStatus))
            {
                Console.WriteLine($"{logPrefix} ПОМИЛКА: Відсутній або порожній статус у запиті для booking_id {bookingId}.");
                await ResponseHelper.SendErrorResponse(stream, "Необхідно передати статус для оновлення.");
                return;
            }

            // Додаткова валідація статусу (наприклад, чи "Pending" - дозволений статус для клієнта)
            // Припускаємо, що клієнт шле тільки "Pending" для нових бронювань
            // Якщо є інші випадки, додати перевірки тут.
            if (newStatus != "Pending") // Наприклад, дозволяємо тільки "Pending" з цієї дії
            {
                Console.WriteLine($"{logPrefix} ПОМИЛКА: Неприпустимий статус '{newStatus}' у запиті для booking_id {bookingId}. Дозволено тільки 'Pending'.");
                await ResponseHelper.SendErrorResponse(stream, $"Неприпустимий статус '{newStatus}'. Дозволено тільки 'Pending' для цієї операції.");
                return;
            }

            Console.WriteLine($"{logPrefix} Отримано booking_id: {bookingId}, Новий статус: {newStatus}");

            try
            {
                Console.WriteLine($"{logPrefix} Запит до BookingRepository.UpdateBookingStatusAsync (ID: {bookingId}, Status: {newStatus})...");
                // 2. Викликаємо новий метод репозиторію для оновлення статусу
                bool success = await BookingRepository.UpdateBookingStatusAsync(connectionString, bookingId, newStatus); // Використовуємо новий метод UpdateBookingStatusAsync
                Console.WriteLine($"{logPrefix} BookingRepository.UpdateBookingStatusAsync повернув: {success}.");


                // 3. Формуємо відповідь
                if (success)
                {
                    await ResponseHelper.SendSuccessResponse(stream, $"Статус бронювання ID {bookingId} успішно змінено на '{newStatus}'.");
                    Console.WriteLine($"{logPrefix} Статус бронювання ID {bookingId} успішно змінено на '{newStatus}'.");
                }
                else
                {
                    // Може означати, що бронювання не знайдено
                    await ResponseHelper.SendErrorResponse(stream, $"Не вдалося змінити статус бронювання ID {bookingId} на '{newStatus}'. Можливо, бронювання не існує.");
                    Console.WriteLine($"{logPrefix} Не вдалося змінити статус бронювання ID {bookingId} (Repo повернув false).");
                }
            }
            catch (Exception ex)
            {
                // Ловимо винятки з репозиторію (наприклад, проблеми з БД)
                Console.WriteLine($"{logPrefix} ЗАГАЛЬНА ПОМИЛКА при оновленні статусу бронювання ID {bookingId}: {ex.GetType().Name} - {ex.Message}\n{ex.StackTrace}");
                await ResponseHelper.SendErrorResponse(stream, $"Внутрішня помилка сервера при оновленні статусу бронювання: {ex.Message}");
            }
        }
        // Допоміжний метод для визначення групи типів сесій (PS або VR/Quest)
        private static string GetSessionTypeGroup(string sessionType)
        {
            if (sessionType.Equals("PS", StringComparison.OrdinalIgnoreCase))
            {
                return "PS";
            }
            if (sessionType.Equals("VR", StringComparison.OrdinalIgnoreCase) || sessionType.Equals("Quest", StringComparison.OrdinalIgnoreCase))
            {
                return "VR_QUEST";
            }
            return "UNKNOWN";
        }

        // ДОПОМІЖНИЙ МЕТОД ДЛЯ ОТРИМАННЯ ЛІМІТУ ЗОН (ПОТРІБНО РЕАЛІЗУВАТИ)
        private static async Task<int> GetMaxZonesForClubAsync(string connectionString, int clubId, string sessionTypeGroup, MySqlConnection connection, MySqlTransaction transaction)
        {
            string query = "";
            if (sessionTypeGroup == "PS")
            {
                query = "SELECT max_ps_zones FROM clubs WHERE club_id = @clubId";
            }
            else if (sessionTypeGroup == "VR_QUEST")
            {
                query = "SELECT max_vr_quest_zones FROM clubs WHERE club_id = @clubId";
            }
            else
            {
                Console.WriteLine($"[SERVICE_ERROR] GetMaxZonesForClubAsync: Unknown session type group: {sessionTypeGroup}");
                return -1;
            }

            try
            {
                using (var cmd = new MySqlCommand(query, connection, transaction))
                {
                    cmd.Parameters.AddWithValue("@clubId", clubId);
                    object result = await cmd.ExecuteScalarAsync();
                    if (result != null && result != DBNull.Value && int.TryParse(result.ToString(), out int maxZones))
                    {
                        return maxZones;
                    }
                    else
                    {
                        Console.WriteLine($"[SERVICE_ERROR] GetMaxZonesForClubAsync: Could not retrieve or parse max zones for club {clubId}, group {sessionTypeGroup}. Result: {result}");
                        return -1;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SERVICE_ERROR] GetMaxZonesForClubAsync: Exception while querying max zones for club {clubId}, group {sessionTypeGroup}. Error: {ex.Message}");
                return -1;
            }
        }

        // Допоміжний метод для безпечного відкату транзакції (без змін)
        private static async Task TryRollbackTransaction(MySqlTransaction transaction)
        {
            try
            {
                if (transaction != null && transaction.Connection != null)
                {
                    Console.WriteLine($"[SERVICE_INFO] Attempting to rollback transaction...");
                    await transaction.RollbackAsync();
                    Console.WriteLine($"[SERVICE_INFO] Transaction rollback successful.");
                }
            }
            catch (Exception rollbackEx)
            {
                Console.WriteLine($"[SERVICE_ERROR] Exception during transaction rollback: {rollbackEx.Message}");
            }
        }

        // --- ВИДАЛЕНО ВНУТРІШНІЙ КЛАС ResponseHelper ---
        // --- ВИДАЛЕНО ВНУТРІШНІЙ КЛАС DictionaryExtensions ---
        // Припускається, що ResponseHelper і DictionaryExtensions оголошені поза цим класом,
        // в тому ж просторі імен ConsoleBookingApp, як ви надали їх раніше.
    }

    // Допоміжний клас для отримання значення зі словника ПОВИНЕН БУТИ ТУТ або в окремому файлі
    public static class DictionaryExtensions
    {
        public static TValue GetValueOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, TValue defaultValue = default)
        {
            if (dictionary == null) return defaultValue;
            return dictionary.TryGetValue(key, out TValue value) ? value : defaultValue;
        }
    }
}