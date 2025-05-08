// Services/ReportingService.cs
using MySqlConnector;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization; // Для парсингу дат
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json; // Для формування складної відповіді JSON
using Newtonsoft.Json.Linq; // Для роботи з JArray, JObject

namespace ConsoleBookingApp.Services
{
    public static class ReportingService
    {
        // <--- ЗМІНЕНО СИГНАТУРУ: Приймає Dictionary<string, object> ---
        // Головний обробник для запиту генерації звітів
        public static async Task HandleGenerateReportsAsync(NetworkStream stream, string connectionString, Dictionary<string, object> requestData)
        {
            Console.WriteLine("[ReportingService] Handling generate_reports request.");

            // 1. Парсимо вхідні дані з клієнта з Dictionary<string, object>
            List<string> reportIds = new List<string>();
            // Отримуємо report_ids як object, потім перевіряємо, чи це JArray
            if (requestData.TryGetValue("report_ids", out object reportIdsObject) && reportIdsObject is JArray reportIdsJArray)
            {
                try
                {
                    // Перетворюємо JArray на List<string>
                    reportIds = reportIdsJArray.ToObject<List<string>>();
                    if (reportIds == null) reportIds = new List<string>(); // Забезпечуємо, що це не null
                }
                catch (JsonException jsonEx)
                {
                    Console.WriteLine($"[ReportingService] JSON error converting 'report_ids' JArray to List<string>: {jsonEx.Message}");
                    await ResponseHelper.SendErrorResponse(stream, "Помилка обробки списку звітів.");
                    return;
                }
            }
            else
            {
                Console.WriteLine("[ReportingService] 'report_ids' missing or not a JSON array.");
                await ResponseHelper.SendErrorResponse(stream, "Не вказано список звітів для формування або невірний формат.");
                return;
            }

            // <--- ВИПРАВЛЕНО CS0165: Оголошуємо та ініціалізуємо дати тут ---
            // Парсимо дати з object
            DateTime startDate = default; // Ініціалізуємо початковим значенням (DateTime.MinValue)
            DateTime endDate = default;   // Ініціалізуємо початковим значенням

            if (requestData.TryGetValue("start_date", out object startDateObj) && startDateObj is string startDateStr)
            {
                if (!DateTime.TryParseExact(startDateStr, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out startDate))
                {
                    Console.WriteLine($"[ReportingService] Warning: Invalid 'start_date' format: {startDateStr}. Using default value.");
                    // startDate залишається default(DateTime)
                }
            }
            else
            {
                Console.WriteLine("[ReportingService] Warning: 'start_date' key missing or not string. Using default value.");
                // startDate залишається default(DateTime)
            }

            if (requestData.TryGetValue("end_date", out object endDateObj) && endDateObj is string endDateStr)
            {
                if (!DateTime.TryParseExact(endDateStr, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out endDate))
                {
                    Console.WriteLine($"[ReportingService] Warning: Invalid 'end_date' format: {endDateStr}. Using default value.");
                    // endDate залишається default(DateTime)
                }
            }
            else
            {
                Console.WriteLine("[ReportingService] Warning: 'end_date' key missing or not string. Using default value.");
                // endDate залишається default(DateTime)
            }

            // Якщо start_date валідна (не default), але end_date ні, встановлюємо end=start.
            if (startDate != default && endDate == default)
            {
                endDate = startDate;
            }
            // --------------------------------------------------------------------


            // 3. Генерація обраних звітів
            var reportsResults = new Dictionary<string, object>(); // Словник для зберігання результатів

            using (var connection = new MySqlConnection(connectionString))
            {
                try
                {
                    await connection.OpenAsync();

                    foreach (var reportId in reportIds)
                    {
                        Console.WriteLine($"[ReportingService] Generating report: {reportId}...");
                        try
                        {
                            object result = null;
                            switch (reportId)
                            {
                                case "revenue":
                                    // <--- АДАПТОВАНО ПІД СХЕМУ БД: revenue з payments JOIN sessions ---
                                    result = await GenerateRevenueReportAsync(connection, startDate, endDate);
                                    // -------------------------------------------------------------------
                                    break;
                                case "active_clients_period":
                                case "active_clients_all_time":
                                    // <--- АДАПТОВАНО ПІД СХЕМУ БД: active_clients з sessions JOIN clients ---
                                    result = await GetActiveClientsReportAsync(connection, startDate, endDate, reportId); // Передаємо reportId
                                    // -----------------------------------------------------------------------
                                    break;
                                case "manager_performance":
                                    // Можливо, передати ClubId, ManagerId з requestData, якщо потрібно фільтрувати
                                    // <--- АДАПТОВАНО ПІД СХЕМУ БД: manager_performance з sessions JOIN managers ---
                                    result = await GetManagerPerformanceReportAsync(connection, startDate, endDate);
                                    // -------------------------------------------------------------------------
                                    break;
                                case "popular_sessions":
                                    // <--- АДАПТОВАНО ПІД СХЕМУ БД: popular_sessions з sessions ---
                                    result = await GetPopularSessionsReportAsync(connection, startDate, endDate);
                                    // -------------------------------------------------------------
                                    break;
                                case "average_people":
                                    // <--- АДАПТОВАНО ПІД СХЕМУ БД: average_people з sessions ---
                                    result = await GetAveragePeopleReportAsync(connection, startDate, endDate);
                                    // ----------------------------------------------------------
                                    break;
                                case "cancelled_sessions":
                                    // <--- АДАПТОВАНО ПІД СХЕМУ БД: cancelled_sessions з sessions ---
                                    result = await GetCancelledSessionsReportAsync(connection, startDate, endDate);
                                    // --------------------------------------------------------------
                                    break;
                                // TODO: Додайте інші кейси для інших звітів тут (напр., "discounts")
                                default:
                                    Console.WriteLine($"[ReportingService] Warning: Unknown report ID requested: {reportId}");
                                    reportsResults[reportId] = "Невідомий звіт."; // Додаємо повідомлення про помилку для цього звіту
                                    continue; // Пропускаємо невідомий звіт
                            }

                            // Додаємо результат (якщо не null) до словника результатів
                            // Якщо результат є повідомленням про помилку (напр. рядок), додаємо його
                            if (result != null)
                            {
                                reportsResults[reportId] = result;
                                Console.WriteLine($"[ReportingService] Report '{reportId}' generated successfully.");
                            }
                            else
                            {
                                reportsResults[reportId] = "Не вдалося отримати дані для звіту.";
                                Console.WriteLine($"[ReportingService] Report '{reportId}' returned null result.");
                            }
                        }
                        catch (Exception reportEx)
                        {
                            Console.WriteLine($"[ReportingService] Error generating report '{reportId}': {reportEx.Message}\n{reportEx.StackTrace}");
                            reportsResults[reportId] = $"Помилка при генерації: {reportEx.Message}"; // Повідомлення про помилку для цього конкретного звіту
                        }
                    }
                }
                catch (MySqlException sqlConnectEx)
                {
                    Console.WriteLine($"[ReportingService] Database connection error: {sqlConnectEx.Message}");
                    await ResponseHelper.SendErrorResponse(stream, "Помилка підключення до бази даних.");
                    return; // Критична помилка, зупиняємо обробку
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ReportingService] General error during reports generation: {ex.Message}\n{ex.StackTrace}");
                    await ResponseHelper.SendErrorResponse(stream, "Внутрішня помилка сервера під час генерації звітів.");
                    return; // Критична помилка, зупиняємо обробку
                }
            } // using connection

            // 4. Формуємо фінальну відповідь
            var finalResponseData = new Dictionary<string, object>
            {
                { "success", "true" },
                { "message", "Звіти сформовано." },
                { "reports", reportsResults } // Додаємо словник з результатами всіх звітів
            };

            // Відправляємо відповідь клієнту
            await ResponseHelper.SendJsonResponse(stream, finalResponseData);

            Console.WriteLine("[ReportingService] Finished handling generate_reports request.");
        }

        // --- Методи для генерації конкретних звітів (адаптовані під схему БД) ---

        private static async Task<object> GenerateRevenueReportAsync(MySqlConnection connection, DateTime startDate, DateTime endDate)
        {
            // Валідація періоду для звіту, що його вимагає
            if (startDate == default || endDate == default || startDate > endDate)
            {
                Console.WriteLine($"[ReportingService] Error: Revenue report requires a valid period, but dates ({startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}) are invalid.");
                return "Помилка: Вкажіть коректний період для звіту про дохід."; // Повертаємо повідомлення про помилку
            }

            Console.WriteLine($"[ReportingService] Generating Revenue Report for {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}");

            // <--- АДАПТОВАНО ПІД СХЕМУ БД ---
            string query = @"
                SELECT
                    SUM(p.amount) AS total_revenue,
                    SUM(CASE WHEN p.payment_method = 'Cash' THEN p.amount ELSE 0 END) AS cash_revenue,
                    SUM(CASE WHEN p.payment_method = 'Card' THEN p.amount ELSE 0 END) AS card_revenue
                    -- Додайте інші способи оплати тут (Online)
                    , SUM(CASE WHEN p.payment_method = 'Online' THEN p.amount ELSE 0 END) AS online_revenue
                FROM payments p
                JOIN sessions s ON p.session_id = s.session_id -- Приєднуємо sessions, щоб фільтрувати за датою сесії
                WHERE s.session_date BETWEEN @startDate AND @endDate
                -- Можливо, додати фільтр за статусом сесії, якщо потрібно враховувати тільки оплачені сесії:
                -- AND s.payment_status = 'Paid'
                ";
            // -----------------------------

            using (var cmd = new MySqlCommand(query, connection))
            {
                cmd.Parameters.AddWithValue("@startDate", startDate.ToString("yyyy-MM-dd"));
                cmd.Parameters.AddWithValue("@endDate", endDate.ToString("yyyy-MM-dd"));

                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        // <--- ЗМІНЕНО НАЗВИ СТОВПЦІВ ПІД ЗАПИТ ---
                        object totalValue = reader["total_revenue"];
                        decimal total = (totalValue == null || totalValue == DBNull.Value) ? 0m : Convert.ToDecimal(totalValue);

                        object cashValue = reader["cash_revenue"];
                        decimal cash = (cashValue == null || cashValue == DBNull.Value) ? 0m : Convert.ToDecimal(cashValue);

                        object cardValue = reader["card_revenue"];
                        decimal card = (cardValue == null || cardValue == DBNull.Value) ? 0m : Convert.ToDecimal(cardValue);

                        object onlineValue = reader["online_revenue"]; // Додано для Online
                        decimal online = (onlineValue == null || onlineValue == DBNull.Value) ? 0m : Convert.ToDecimal(onlineValue);

                        // -----------------------------------------

                        return new Dictionary<string, object>
                        {
                            { "total", total },
                            { "cash", cash },
                            { "card", card },
                            { "online", online } // Додано для Online
                            // Додайте інші способи оплати тут
                        };
                    }
                }
            }
            // Повертаємо нулі, якщо немає даних або сталася помилка (або якщо результат запиту порожній)
            return new Dictionary<string, object> { { "total", 0m }, { "cash", 0m }, { "card", 0m }, { "online", 0m } };
        }

        // <--- ЗМІНЕНО МЕТОД: Додано параметр reportId ---
        private static async Task<object> GetActiveClientsReportAsync(MySqlConnection connection, DateTime startDate, DateTime endDate, string reportId)
        {
            // Визначаємо, чи потрібен фільтр за датою
            bool useDateFilter = reportId != "active_clients_all_time";

            Console.WriteLine($"[ReportingService] Generating Active Clients Report ('{reportId}') {(useDateFilter ? $"for {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}" : "(All Time)")}");

            string query;
            // <--- АДАПТОВАНО ПІД СХЕМУ БД ---
            if (useDateFilter)
            {
                // Валідація періоду для звіту, що його вимагає
                if (startDate == default || endDate == default || startDate > endDate)
                {
                    Console.WriteLine($"[ReportingService] Error: Active clients period report requires a valid period, but dates ({startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}) are invalid.");
                    return "Помилка: Вкажіть коректний період для звіту про активних клієнтів (період)."; // Повертаємо повідомлення про помилку
                }

                query = @"
                    SELECT c.name AS client_name, COUNT(s.session_id) AS booking_count
                    FROM sessions s -- Змінено з bookings на sessions
                    JOIN clients c ON s.client_id = c.client_id -- Додано JOIN до таблиці clients
                    WHERE s.session_date BETWEEN @startDate AND @endDate -- Фільтр за періодом
                    GROUP BY c.name
                    ORDER BY booking_count DESC
                    LIMIT 10"; // Можна зробити ліміт гнучким
            }
            else // reportId == "active_clients_all_time"
            {
                query = @"
                    SELECT c.name AS client_name, COUNT(s.session_id) AS booking_count
                    FROM sessions s -- Змінено з bookings на sessions
                    JOIN clients c ON s.client_id = c.client_id -- Додано JOIN до таблиці clients
                    GROUP BY c.name
                    ORDER BY booking_count DESC
                    LIMIT 10"; // Без фільтра за датою
            }
            // -----------------------------

            var clientsList = new List<object>();

            using (var cmd = new MySqlCommand(query, connection))
            {
                if (useDateFilter) // <--- ДОДАЄМО ПАРАМЕТРИ ТІЛЬКИ ЯКЩО Є WHERE CLAUSE ---
                {
                    cmd.Parameters.AddWithValue("@startDate", startDate.ToString("yyyy-MM-dd"));
                    cmd.Parameters.AddWithValue("@endDate", endDate.ToString("yyyy-MM-dd"));
                }

                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        clientsList.Add(new
                        {
                            // <--- ЗМІНЕНО НАЗВИ СТОВПЦІВ ПІД ЗАПИТ ---
                            name = reader.GetString("client_name"),
                            bookings = reader.GetInt64("booking_count")
                            // -----------------------------------------
                        });
                    }
                }
            }

            return clientsList; // Повертаємо список анонімних об'єктів, JsonConvert їх коректно серіалізує
        }
        // --------------------------------------------------------------------------


        private static async Task<object> GetManagerPerformanceReportAsync(MySqlConnection connection, DateTime startDate, DateTime endDate)
        {
            // Валідація періоду для звіту, що його вимагає
            if (startDate == default || endDate == default || startDate > endDate)
            {
                Console.WriteLine($"[ReportingService] Error: Manager performance report requires a valid period, but dates ({startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}) are invalid.");
                return "Помилка: Вкажіть коректний період для звіту про продуктивність менеджерів."; // Повертаємо повідомлення про помилку
            }

            Console.WriteLine($"[ReportingService] Generating Manager Performance Report for {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}");

            // <--- АДАПТОВАНО ПІД СХЕМУ БД ---
            string query = @"
                SELECT m.name AS manager_name, COUNT(s.session_id) AS booking_count
                FROM managers m
                JOIN sessions s ON m.manager_id = s.manager_id -- Змінено з bookings на sessions
                WHERE s.session_date BETWEEN @startDate AND @endDate -- Фільтр за періодом
                GROUP BY m.name
                ORDER BY booking_count DESC";
            // -----------------------------

            var performanceList = new List<object>();

            using (var cmd = new MySqlCommand(query, connection))
            {
                cmd.Parameters.AddWithValue("@startDate", startDate.ToString("yyyy-MM-dd"));
                cmd.Parameters.AddWithValue("@endDate", endDate.ToString("yyyy-MM-dd"));

                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        performanceList.Add(new
                        {
                            // <--- ЗМІНЕНО НАЗВИ СТОВПЦІВ ПІД ЗАПИТ ---
                            manager_name = reader.GetString("manager_name"),
                            bookings = reader.GetInt64("booking_count")
                            // -----------------------------------------
                        });
                    }
                }
            }

            return performanceList;
        }

        private static async Task<object> GetPopularSessionsReportAsync(MySqlConnection connection, DateTime startDate, DateTime endDate)
        {
            // Валідація періоду для звіту, що його вимагає
            if (startDate == default || endDate == default || startDate > endDate)
            {
                Console.WriteLine($"[ReportingService] Error: Popular sessions report requires a valid period, but dates ({startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}) are invalid.");
                return "Помилка: Вкажіть коректний період для звіту про популярні сеанси."; // Повертаємо повідомлення про помилку
            }

            Console.WriteLine($"[ReportingService] Generating Popular Sessions Report for {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}");

            var results = new Dictionary<string, object>();

            // Звіт по типах сесій
            // <--- АДАПТОВАНО ПІД СХЕМУ БД ---
            string typeQuery = @"
                SELECT session_type, COUNT(*) AS count
                FROM sessions -- Змінено з bookings на sessions
                WHERE session_date BETWEEN @startDate AND @endDate
                GROUP BY session_type";
            // -----------------------------

            using (var cmd = new MySqlCommand(typeQuery, connection))
            {
                cmd.Parameters.AddWithValue("@startDate", startDate.ToString("yyyy-MM-dd"));
                cmd.Parameters.AddWithValue("@endDate", endDate.ToString("yyyy-MM-dd"));
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        // <--- НАЗВИ СТОВПЦІВ ПІД ЗАПИТ ---
                        results[reader.GetString("session_type")] = reader.GetInt64("count");
                        // -----------------------------
                    }
                }
            }

            // Звіт по часових слотах (приклад - групуємо по годині початку)
            // <--- АДАПТОВАНО ПІД СХЕМУ БД ---
            string slotQuery = @"
                SELECT TIME_FORMAT(start_time, '%H:00') as start_hour, COUNT(*) as count
                FROM sessions -- Змінено з bookings на sessions
                WHERE session_date BETWEEN @startDate AND @endDate
                GROUP BY start_hour
                ORDER BY start_hour"; // Сортуємо за часом
                                      // -----------------------------

            using (var cmd = new MySqlCommand(slotQuery, connection))
            {
                cmd.Parameters.AddWithValue("@startDate", startDate.ToString("yyyy-MM-dd"));
                cmd.Parameters.AddWithValue("@endDate", endDate.ToString("yyyy-MM-dd"));
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        // <--- НАЗВИ СТОВПЦІВ ПІД ЗАПИТ ---
                        results[reader.GetString("start_hour")] = reader.GetInt64("count");
                        // -----------------------------
                    }
                }
            }

            return results;
        }

        private static async Task<object> GetAveragePeopleReportAsync(MySqlConnection connection, DateTime startDate, DateTime endDate)
        {
            // Валідація періоду для звіту, що його вимагає
            if (startDate == default || endDate == default || startDate > endDate)
            {
                Console.WriteLine($"[ReportingService] Error: Average people report requires a valid period, but dates ({startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}) are invalid.");
                return "Помилка: Вкажіть коректний період для звіту про середню кількість людей."; // Повертаємо повідомлення про помилку
            }

            Console.WriteLine($"[ReportingService] Generating Average People Report for {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}");

            // <--- АДАПТОВАНО ПІД СХЕМУ БД ---
            string query = @"
                SELECT AVG(num_people) AS avg_people
                FROM sessions -- Змінено з bookings на sessions
                WHERE session_date BETWEEN @startDate AND @endDate";
            // -----------------------------

            using (var cmd = new MySqlCommand(query, connection))
            {
                cmd.Parameters.AddWithValue("@startDate", startDate.ToString("yyyy-MM-dd"));
                cmd.Parameters.AddWithValue("@endDate", endDate.ToString("yyyy-MM-dd"));

                object result = await cmd.ExecuteScalarAsync();

                // AVG повертає decimal або null, якщо немає рядків
                if (result != null && result != DBNull.Value)
                {
                    // <--- НАЗВА СТОВПЦЯ ПІД ЗАПИТ ---
                    // Оскільки це ExecuteScalarAsync, ми просто перетворюємо результат
                    return Convert.ToDecimal(result); // Повертаємо як decimal
                                                      // -----------------------------
                }
            }
            return 0m; // Повертаємо 0, якщо немає даних або сталася помилка
        }

        private static async Task<object> GetCancelledSessionsReportAsync(MySqlConnection connection, DateTime startDate, DateTime endDate)
        {
            // Валідація періоду для звіту, що його вимагає
            if (startDate == default || endDate == default || startDate > endDate)
            {
                Console.WriteLine($"[ReportingService] Error: Cancelled sessions report requires a valid period, but dates ({startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}) are invalid.");
                return "Помилка: Вкажіть коректний період для звіту про скасовані сеанси."; // Повертаємо повідомлення про помилку
            }

            Console.WriteLine($"[ReportingService] Generating Cancelled Sessions Report for {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}");

            // <--- АДАПТОВАНО ПІД СХЕМУ БД ---
            string query = @"
                SELECT COUNT(*)
                FROM sessions -- Змінено з bookings на sessions
                WHERE session_date BETWEEN @startDate AND @endDate AND payment_status = 'Cancelled'"; // Припускаємо статус 'Cancelled' в таблиці sessions
                                                                                                      // -----------------------------

            using (var cmd = new MySqlCommand(query, connection))
            {
                cmd.Parameters.AddWithValue("@startDate", startDate.ToString("yyyy-MM-dd"));
                cmd.Parameters.AddWithValue("@endDate", endDate.ToString("yyyy-MM-dd"));

                object result = await cmd.ExecuteScalarAsync();

                // COUNT(*) повертає long або null
                if (result != null && result != DBNull.Value)
                {
                    // <--- НАЗВА СТОВПЦЯ ПІД ЗАПИТ ---
                    // Оскільки це ExecuteScalarAsync, ми просто перетворюємо результат
                    return Convert.ToInt64(result); // Повертаємо як long
                                                    // -----------------------------
                }
            }
            return 0L; // Повертаємо 0, якщо немає даних
        }

        // TODO: Додайте інші методи для інших звітів тут
        // Приклад методу для звіту "Знижки за період":
        /*
         private static async Task<object> GetDiscountsReportAsync(MySqlConnection connection, DateTime startDate, DateTime endDate)
         {
             if (startDate == default || endDate == default || startDate > endDate)
             {
                  return "Помилка: Вкажіть коректний період для звіту про знижки.";
             }
             Console.WriteLine($"[ReportingService] Generating Discounts Report for {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}");

             // Приклад запиту: сума знижок застосованих до оплачених сесій за період
             string query = @"
                SELECT SUM(s.calculate_price - s.final_price) AS total_discount_amount -- Різниця між розрахованою і фінальною ціною
                FROM sessions s
                WHERE s.session_date BETWEEN @startDate AND @endDate AND s.payment_status = 'Paid' AND s.discount_id IS NOT NULL; -- Тільки оплачені сесії зі знижкою

             // Альтернатива: кількість сесій зі знижкою
             // SELECT COUNT(s.session_id) AS sessions_with_discount
             // FROM sessions s
             // WHERE s.session_date BETWEEN @startDate AND @endDate AND s.discount_id IS NOT NULL;

             // Альтернатива: які знижки використовувалися найчастіше
             // SELECT d.name AS discount_name, COUNT(s.session_id) AS usage_count
             // FROM sessions s
             // JOIN discounts d ON s.discount_id = d.discount_id
             // WHERE s.session_date BETWEEN @startDate AND @endDate AND s.discount_id IS NOT NULL
             // GROUP BY d.name ORDER BY usage_count DESC;

             using (var cmd = new MySqlCommand(query, connection))
             {
                 cmd.Parameters.AddWithValue("@startDate", startDate.ToString("yyyy-MM-dd"));
                 cmd.Parameters.AddWithValue("@endDate", endDate.ToString("yyyy-MM-dd"));

                 object result = await cmd.ExecuteScalarAsync(); // Або ExecuteReaderAsync залежно від запиту

                 if (result != null && result != DBNull.Value)
                 {
                     // Якщо запит повертає суму знижки
                     return Convert.ToDecimal(result); // Повертаємо як decimal
                     // Якщо запит повертає кількість сесій
                     // return Convert.ToInt64(result); // Повертаємо як long
                     // Якщо запит повертає список (як для активних клієнтів)
                     // return discountUsageList; // List<object>
                 }
             }
             return 0m; // або empty list, залежно від типу звіту
         }
        */
    }
}