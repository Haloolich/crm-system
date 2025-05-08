using ConsoleBookingApp.Models;
using MySqlConnector;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Threading.Tasks;

namespace ConsoleBookingApp.Data
{
    public class BookingRepository
    {
            // ВИПРАВЛЕННЯ: Поле для рядка підключення (не статичне)
            private readonly string _connectionString;

            // ВИПРАВЛЕННЯ: Додаємо конструктор для ініціалізації _connectionString
            public BookingRepository(string connectionString)
            {
                _connectionString = connectionString;
            }
            // --- КЛІЄНТИ ---
            public static async Task<List<SessionDetails>> GetDailySummarySessionsAsync(
        string connectionString,
        DateTime checkDate,
        int clubId)
        {
            var sessionsList = new List<SessionDetails>();

            // SQL запит:
            // - Вибираємо з sessions (s)
            // - JOIN clients (c) для імені клієнта
            // - LEFT JOIN payments (p) для методу оплати та підтвердженої суми. Використовуємо LEFT JOIN, бо не всі сесії можуть мати запис в payments
            // - Фільтруємо за датою та клубом
            // - Можливо, варто ORDER BY start_time
            var query = @"
            SELECT
                s.session_id,
                s.client_id,
                s.session_date,
                s.start_time,
                s.end_time,
                s.num_people,
                s.notes,
                s.session_type,
                s.payment_status,
                s.manager_id,
                s.club_id,
                s.discount_id,
                s.calculate_price,
                s.final_price,      -- Беремо final_price з sessions (якщо схема точна)
                c.name AS client_name, -- З таблиці clients
                p.payment_method -- <<< ДОДАНО: Беремо метод оплати з таблиці payments
                -- Якщо payment_method немає в payments, приберіть цей рядок SELECT
                -- Якщо payment_method є в sessions, але ви забули його включити в схему,
                -- додайте s.payment_method в SELECT і не потрібен JOIN payments.
            FROM
                sessions s
            JOIN clients c ON s.client_id = c.client_id      -- Приєднуємо клієнтів
            LEFT JOIN payments p ON s.session_id = p.session_id -- Приєднуємо оплати (LEFT JOIN!)
            -- Якщо method зберігається ТІЛЬКИ в sessions, приберіть LEFT JOIN payments
            -- Якщо method зберігається і в sessions, і в payments, вирішіть, звідки брати (можливо, payments більш актуальний для Paid)
            WHERE
                s.session_date = @check_date
                AND s.club_id = @club_id
            ORDER BY s.start_time ASC; -- Сортуємо за часом початку
        ";

            using (var connection = new MySqlConnection(connectionString))
            {
                await connection.OpenAsync();
                Console.WriteLine($"[ЛОГ Репозиторій] GetDailySummarySessionsAsync: З'єднання з БД відкрито.");

                using (var cmd = new MySqlCommand(query, connection))
                {
                    cmd.Parameters.AddWithValue("@check_date", checkDate.Date); // Передаємо тільки дату
                    cmd.Parameters.AddWithValue("@club_id", clubId);

                    try
                    {
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            Console.WriteLine($"[ЛОГ Репозиторій] GetDailySummarySessionsAsync: Запит виконано, читаємо дані...");
                            while (await reader.ReadAsync())
                            {
                                try
                                {
                                    // Читаємо final_price.
                                    // Якщо session.final_price не оновлюється при оплаті, а фінальна сума тільки в payments,
                                    // тоді треба брати суму з payments.amount (але там може бути кілька оплат, що ускладнить запит).
                                    // Наразі використовуємо sessions.final_price як фінальну суму.
                                    decimal finalPrice = reader.GetDecimal("final_price");

                                    // Читаємо payment_method. Потрібна перевірка на DBNull, бо використовуємо LEFT JOIN payments.
                                    string paymentMethod = reader["payment_method"] == DBNull.Value ? null : reader.GetString("payment_method");

                                    // Заповнюємо об'єкт SessionDetails даними
                                    var sessionDetails = new SessionDetails
                                    {
                                        SessionId = reader.GetInt32("session_id"),
                                        ClientId = reader.GetInt32("client_id"), // Залишаємо, якщо потрібен для налагодження
                                        SessionDate = reader.GetDateTime("session_date"), // Залишаємо
                                        StartTime = reader.GetTimeSpan("start_time"), // TimeSpan
                                        EndTime = reader.GetTimeSpan("end_time"),     // TimeSpan
                                        NumPeople = reader.GetInt32("num_people"), // Залишаємо
                                        Notes = reader["notes"] == DBNull.Value ? null : reader.GetString("notes"), // Залишаємо
                                        SessionType = reader.GetString("session_type"), // Залишаємо
                                        PaymentStatus = reader.GetString("payment_status"),
                                        // ManagerId = reader.IsDBNull("manager_id") ? (int?)null : reader.GetInt32("manager_id"), // Якщо потрібно ID
                                        // ManagerName = reader["manager_name"] == DBNull.Value ? null : reader.GetString("manager_name"), // Якщо ManagerName потрібен, додайте JOIN managers
                                        ClubId = reader.GetInt32("club_id"), // Залишаємо
                                        CalculatePrice = reader.GetDecimal("calculate_price"), // Залишаємо
                                        FinalPrice = finalPrice, // Використовуємо прочитане значення
                                        ClientName = reader.GetString("client_name"), // Читаємо ім'я клієнта
                                        PaymentMethod = paymentMethod // Використовуємо прочитане значення
                                                                      // Додаткові поля з SessionDetails можна додати, якщо потрібні
                                    };
                                    sessionsList.Add(sessionDetails);
                                }
                                catch (Exception readEx)
                                {
                                    Console.WriteLine($"[ПОМИЛКА Репозиторій] GetDailySummarySessionsAsync: Помилка читання рядка даних: {readEx.Message}");
                                    // Щоб не впасти повністю, можна пропустити цей рядок і продовжити
                                    // Log the error and continue loop
                                }
                            }
                            Console.WriteLine($"[ЛОГ Репозиторій] GetDailySummarySessionsAsync: Прочитано {sessionsList.Count} сесій.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ПОМИЛКА Репозиторій] GetDailySummarySessionsAsync: Помилка виконання запиту: {ex.Message}\n{ex.StackTrace}");
                        throw; // Прокидаємо виняток, щоб сервіс його обробив
                    }
                }
                Console.WriteLine($"[ЛОГ Репозиторій] GetDailySummarySessionsAsync: З'єднання з БД закрито.");

                return sessionsList;
            }
        }
        public async Task<Client> GetClientByPhoneAsync(string phoneNumber)
    {
        Client client = null;
        // ПЕРЕВІРТЕ: Чи вибирається email, birthday та created_at? Так, вибирається тут.
        string sql = "SELECT client_id, name, phone_number, email, birthday, created_at FROM clients WHERE phone_number = @phoneNumber LIMIT 1;";

        try
        {
            using (var connection = new MySqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@phoneNumber", phoneNumber);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                                client = new Client
                                {
                                    ClientId = reader.GetInt32(reader.GetOrdinal("client_id")),
                                    Name = reader.GetString(reader.GetOrdinal("name")),
                                    PhoneNumber = reader.GetString(reader.GetOrdinal("phone_number")),
                                    Email = reader.IsDBNull(reader.GetOrdinal("email"))
            ? null
            : reader.GetString(reader.GetOrdinal("email")),
                                    DateOfBirth = reader.IsDBNull(reader.GetOrdinal("birthday"))
            ? default(DateTime)
            : reader.GetDateTime(reader.GetOrdinal("birthday")),
                                    RegistrationDate = reader.IsDBNull(reader.GetOrdinal("created_at"))
            ? default(DateTime)
            : reader.GetDateTime(reader.GetOrdinal("created_at"))
                                };

                            }
                        }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting client by phone '{phoneNumber}': {ex.Message}");
            throw;
        }
        return client;
    }
        /// <summary>
        /// Отримує всі сесії для вказаного клієнта.
        /// </summary>
        /// <param name="clientId">ID клієнта.</param>
        /// <returns>Список об'єктів ClientSession.</returns>
        // ВИПРАВЛЕННЯ: Прибираємо 'static' з оголошення методу
        public async Task<List<ClientSession>> GetSessionsByClientIdAsync(int clientId)
        {
            var sessions = new List<ClientSession>();
            // SQL запит виглядає правильно для ваших колонок
            string sql = "SELECT session_date, session_type FROM sessions WHERE client_id = @clientId ORDER BY session_date DESC;";

            try
            {
                // ВИПРАВЛЕННЯ: Використовуємо поле _connectionString з екземпляра класу
                using (var connection = new MySqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    using (var command = new MySqlCommand(sql, connection))
                    {
                        // ВИПРАВЛЕННЯ: Параметр @clientId має тип int, передаємо int
                        command.Parameters.AddWithValue("@clientId", clientId);

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                sessions.Add(new ClientSession
                                {
                                    SessionDate = reader.GetDateTime("session_date"),
                                    SessionType = reader.GetString("session_type")
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting sessions for client ID {clientId}: {ex.Message}");
                throw; // Перекидаємо виняток
            }

            return sessions;
        }
        public static async Task<int> FindClientByPhoneAsync(string connectionString, string phone, MySqlConnection connection = null, MySqlTransaction transaction = null)
        {
            bool ownConnection = connection == null;
            MySqlConnection conn = connection ?? new MySqlConnection(connectionString);
            try
            {
                if (ownConnection) await conn.OpenAsync();
                string query = "SELECT client_id FROM clients WHERE phone_number = @phone";
                using (var cmd = new MySqlCommand(query, conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@phone", phone);
                    object result = await cmd.ExecuteScalarAsync();
                    return (result != null && result != DBNull.Value && int.TryParse(result.ToString(), out int id)) ? id : -1;
                }
            }
            finally
            {
                if (ownConnection && conn?.State == ConnectionState.Open) await conn.CloseAsync();
            }
        }

        public struct AvailabilitySummaryResult
        {
            public int PaidVrQuest { get; set; }
            public int PendingVrQuest { get; set; }
            public int PaidPs { get; set; }
            public int PendingPs { get; set; }
        }
        public static async Task<AvailabilitySummaryResult> GetAvailabilitySummaryAsync(
    string connectionString,
    DateTime checkDate,
    TimeSpan checkStartTime, // Використовуємо TimeSpan
    TimeSpan checkEndTime,   // Використовуємо TimeSpan
    int clubId)
        {
            var result = new AvailabilitySummaryResult { PaidVrQuest = 0, PendingVrQuest = 0, PaidPs = 0, PendingPs = 0 };

            // Модифікований SQL запит з окремим підрахунком для Paid та Pending
            var query = @"
        SELECT
            COALESCE(SUM(CASE WHEN session_type IN ('VR', 'Quest') AND payment_status = 'Paid' THEN num_people ELSE 0 END), 0) AS paid_vr_quest_zones,
            COALESCE(SUM(CASE WHEN session_type IN ('VR', 'Quest') AND payment_status = 'Pending' THEN num_people ELSE 0 END), 0) AS pending_vr_quest_zones,
            COALESCE(SUM(CASE WHEN session_type = 'PS' AND payment_status = 'Paid' THEN num_people ELSE 0 END), 0) AS paid_ps_zones,
            COALESCE(SUM(CASE WHEN session_type = 'PS' AND payment_status = 'Pending' THEN num_people ELSE 0 END), 0) AS pending_ps_zones
        FROM
            sessions
        WHERE
            session_date = @check_date
            AND club_id = @club_id
            -- Перевірка перетину часових проміжків
            AND start_time < @check_end_time_ts
            AND end_time > @check_start_time_ts
            -- Додаємо фільтр за статусом, щоб запит був ефективнішим, хоча CASE теж фільтрує
            AND payment_status IN ('Paid', 'Pending');
    ";

            using (var connection = new MySqlConnection(connectionString))
            {
                await connection.OpenAsync();

                using (var cmd = new MySqlCommand(query, connection))
                {
                    cmd.Parameters.AddWithValue("@check_date", checkDate.Date);
                    cmd.Parameters.AddWithValue("@club_id", clubId);
                    cmd.Parameters.AddWithValue("@check_start_time_ts", checkStartTime);
                    cmd.Parameters.AddWithValue("@check_end_time_ts", checkEndTime);

                    try
                    {
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                result.PaidVrQuest = reader.GetInt32("paid_vr_quest_zones");
                                result.PendingVrQuest = reader.GetInt32("pending_vr_quest_zones");
                                result.PaidPs = reader.GetInt32("paid_ps_zones");
                                result.PendingPs = reader.GetInt32("pending_ps_zones");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ПОМИЛКА Репозиторій] GetAvailabilitySummaryAsync: Помилка виконання запиту: {ex.Message}\n{ex.StackTrace}");
                        throw;
                    }
                }
            }

            return result;
        }
        public static async Task<List<Dictionary<string, object>>> GetSessionsForDateAsync(
    string connectionString,
    DateTime checkDate,
    int clubId)
        {
            var sessionsList = new List<Dictionary<string, object>>();

            var query = @"
        SELECT
            session_id,
            client_id,
            session_date, -- Хоча дату знаємо, повертаємо для повноти
            start_time,
            end_time,
            num_people,
            notes, -- Можливо, не потрібні для цього запиту, можна прибрати
            session_type,
            payment_status,
            manager_id, -- Можливо, не потрібні
            discount_id, -- Можливо, не потрібні
            calculate_price, -- Можливо, не потрібні
            final_price -- Можливо, не потрібні
            -- club_id -- Хоча клуб знаємо, повертаємо для повноти
        FROM
            sessions
        WHERE
            session_date = @check_date
            AND club_id = @club_id
            AND payment_status IN ('Paid', 'Pending'); -- Тільки оплачені або в очікуванні
    ";

            using (var connection = new MySqlConnection(connectionString))
            {
                await connection.OpenAsync();

                using (var cmd = new MySqlCommand(query, connection))
                {
                    cmd.Parameters.AddWithValue("@check_date", checkDate.Date);
                    cmd.Parameters.AddWithValue("@club_id", clubId);

                    try
                    {
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var sessionData = new Dictionary<string, object>();
                                // Заповнюємо словник даними сесії
                                // Важливо: читаємо дані відповідно до типів у БД
                                sessionData["session_id"] = reader.GetInt32("session_id");
                                // sessionData["client_id"] = reader.GetInt32("client_id");
                                // sessionData["session_date"] = reader.GetDateTime("session_date").ToString("yyyy-MM-dd"); // Можна форматувати дату як рядок
                                sessionData["start_time"] = reader.GetTimeSpan("start_time").ToString(@"hh\:mm\:ss"); // Форматуємо час як рядок
                                sessionData["end_time"] = reader.GetTimeSpan("end_time").ToString(@"hh\:mm\:ss");     // Форматуємо час як рядок
                                sessionData["num_people"] = reader.GetInt32("num_people");
                                // sessionData["notes"] = reader.GetString("notes"); // Читаємо, якщо потрібно
                                sessionData["session_type"] = reader.GetString("session_type");
                                sessionData["payment_status"] = reader.GetString("payment_status");
                                // sessionData["manager_id"] = reader.GetInt32("manager_id");
                                // sessionData["discount_id"] = reader.IsDBNull("discount_id") ? (int?)null : reader.GetInt32("discount_id"); // Обробка nullable
                                // sessionData["calculate_price"] = reader.GetDecimal("calculate_price");
                                // sessionData["final_price"] = reader.GetDecimal("final_price");
                                // sessionData["club_id"] = reader.GetInt32("club_id");

                                sessionsList.Add(sessionData);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ПОМИЛКА Репозиторій] GetSessionsForDateAsync: Помилка виконання запиту: {ex.Message}\n{ex.StackTrace}");
                        throw;
                    }
                }
            }

            return sessionsList;
        }
        public static async Task<bool> UpdateOrderAsync(
        MySqlConnection connection,
        int sessionId,
        int managerId,
        int clubId,
        int? numPeople = null, // Nullable int
        string notes = null,
        string sessionType = null,
        string paymentStatus = null,
        decimal? finalPrice = null // Nullable decimal
    )
        {
            // Будуємо SQL запит динамічно, додаючи SET-параметри лише для полів, які не null
            var setClauses = new List<string>();
            var parameters = new List<MySqlParameter>();

            if (numPeople.HasValue)
            {
                setClauses.Add("num_people = @num_people");
                parameters.Add(new MySqlParameter("@num_people", numPeople.Value));
            }

            // Для рядкових полів перевіряємо, чи вони передані (не null), а не чи вони порожні
            // Якщо передано "", це може бути явне бажання очистити поле.
            // Рішення: Перевіряємо `notes != null`, `sessionType != null`, `paymentStatus != null`
            if (notes != null)
            {
                setClauses.Add("notes = @notes");
                parameters.Add(new MySqlParameter("@notes", notes));
            }
            if (sessionType != null)
            {
                setClauses.Add("session_type = @session_type");
                parameters.Add(new MySqlParameter("@session_type", sessionType));
            }
            if (paymentStatus != null)
            {
                setClauses.Add("payment_status = @payment_status");
                parameters.Add(new MySqlParameter("@payment_status", paymentStatus));
            }


            if (finalPrice.HasValue)
            {
                setClauses.Add("final_price = @final_price");
                parameters.Add(new MySqlParameter("@final_price", finalPrice.Value));
            }

            // Якщо немає жодного поля для оновлення, повертаємо false або true (залежить від бізнес-логіки)
            // Тут вирішили повертати true, оскільки запит успішно оброблено, просто нічого не змінилось
            if (setClauses.Count == 0)
            {
                Console.WriteLine($"[ЛОГ] UpdateOrderAsync для замовлення {sessionId}: Немає полів для оновлення.");
                return true; // Або false, якщо ви хочете явно вказати, що дія не відбулася
            }

            var query = $"UPDATE sessions SET {string.Join(", ", setClauses)} WHERE session_id = @session_id AND manager_id = @manager_id AND club_id = @club_id";

            var cmd = new MySqlCommand(query, connection);
            cmd.Parameters.AddWithValue("@session_id", sessionId);
            cmd.Parameters.AddWithValue("@manager_id", managerId);
            cmd.Parameters.AddWithValue("@club_id", clubId);
            parameters.ForEach(p => cmd.Parameters.Add(p)); // Додаємо динамічні параметри

            int rowsAffected = await cmd.ExecuteNonQueryAsync();

            // Якщо rowsAffected > 0, значить, замовлення було знайдено та оновлено
            return rowsAffected > 0;
        }

        public static async Task<bool> RecordPaymentAsync(
        MySqlConnection connection,
        int sessionId,
        int managerId,
        int clubId,
        decimal amount,
        string paymentMethod,
        DateTime paymentTime // Отримуємо вже розпарсену DateTime
    )
        {
            // Оновлюємо payment_status, final_price, payment_method, payment_time
            // Перевіряємо по session_id, manager_id, club_id І статусу 'Pending'
            var query = @"
        UPDATE sessions
        SET
            payment_status = 'Paid',
            final_price = @amount,
            payment_method = @payment_method,
            payment_time = @payment_time
        WHERE session_id = @session_id
          AND manager_id = @manager_id
          AND club_id = @club_id
          AND payment_status = 'Pending'; -- Важливо: оновлюємо тільки 'Pending' замовлення
    ";

            var cmd = new MySqlCommand(query, connection);
            cmd.Parameters.AddWithValue("@session_id", sessionId);
            cmd.Parameters.AddWithValue("@manager_id", managerId);
            cmd.Parameters.AddWithValue("@club_id", clubId);
            cmd.Parameters.AddWithValue("@amount", amount);
            cmd.Parameters.AddWithValue("@payment_method", paymentMethod);
            cmd.Parameters.AddWithValue("@payment_time", paymentTime); // Передаємо DateTime об'єкт

            int rowsAffected = await cmd.ExecuteNonQueryAsync();

            // Якщо rowsAffected > 0, значить, замовлення було знайдено зі статусом 'Pending' та оновлено
            return rowsAffected > 0;
        }

        public static async Task<bool> RecordPaymentTransactionAsync(
        string connectionString, // Приймаємо рядок підключення
        int sessionId,
        int managerId,
        int clubId,
        decimal amount,
        string paymentMethod,
        DateTime paymentTime // Отримуємо вже розпарсену DateTime
    )
        {
            using (var connection = new MySqlConnection(connectionString))
            {
                await connection.OpenAsync();

                // Починаємо транзакцію
                using (var transaction = await connection.BeginTransactionAsync()) // Або connection.BeginTransaction()
                {
                    try
                    {
                        // Крок 1: Перевірка та оновлення статусу в таблиці sessions
                        // Ми перевіряємо статус 'Pending' ТА ОДРАЗУ його оновлюємо.
                        // Це робиться першим, щоб уникнути подвійних оплат одного Pending замовлення
                        // з різних паралельних запитів. `WHERE payment_status = 'Pending'` є КРИТИЧНИМ.
                        var updateSessionQuery = @"
                        UPDATE sessions
                        SET
                            payment_status = 'Paid',
                            final_price = @amount -- Синхронізуємо final_price з фактичною оплаченою сумою
                        WHERE session_id = @session_id
                          AND manager_id = @manager_id
                          AND club_id = @club_id
                          AND payment_status = 'Pending'; -- Оновлюємо ТІЛЬКИ ті, що очікують
                    ";

                        using (var updateCmd = new MySqlCommand(updateSessionQuery, connection, transaction))
                        {
                            updateCmd.Parameters.AddWithValue("@session_id", sessionId);
                            updateCmd.Parameters.AddWithValue("@manager_id", managerId);
                            updateCmd.Parameters.AddWithValue("@club_id", clubId);
                            updateCmd.Parameters.AddWithValue("@amount", amount); // Використовуємо amount для final_price

                            int sessionsAffected = await updateCmd.ExecuteNonQueryAsync();

                            // Якщо sessionsAffected == 0, це означає, що замовлення не знайдено
                            // з потрібними ID та статусом 'Pending'. Транзакцію потрібно відкатити.
                            if (sessionsAffected == 0)
                            {
                                Console.WriteLine($"[ЛОГ Репозиторій] RecordPaymentTransactionAsync: Не знайдено замовлення {sessionId} (менеджер {managerId}, клуб {clubId}) зі статусом 'Pending' для оновлення.");
                                await transaction.RollbackAsync(); // Відкатуємо транзакцію
                                return false; // Повертаємо false, бо операція не виконана
                            }

                            // Крок 2: Вставка запису про оплату в таблицю payments
                            var insertPaymentQuery = @"
                            INSERT INTO payments (session_id, amount, payment_time, payment_method, manager_id, club_id)
                            VALUES (@sessionId, @amount, @paymentTime, @paymentMethod, @managerId, @clubId);
                        ";

                            using (var insertCmd = new MySqlCommand(insertPaymentQuery, connection, transaction))
                            {
                                insertCmd.Parameters.AddWithValue("@sessionId", sessionId);
                                insertCmd.Parameters.AddWithValue("@amount", amount);
                                insertCmd.Parameters.AddWithValue("@paymentTime", paymentTime);
                                insertCmd.Parameters.AddWithValue("@paymentMethod", paymentMethod);
                                insertCmd.Parameters.AddWithValue("@managerId", managerId); // Дублюємо менеджер/клуб в оплаті для зручності запитів, якщо потрібно
                                insertCmd.Parameters.AddWithValue("@clubId", clubId);     // або перевірки належності оплати

                                int paymentsAffected = await insertCmd.ExecuteNonQueryAsync();

                                // Перевіряємо, чи вставився хоча б один рядок
                                if (paymentsAffected == 0)
                                {
                                    // Це малоймовірно, якщо update sessions пройшов,
                                    // але є гарною перевіркою на всяк випадок.
                                    Console.WriteLine($"[ПОМИЛКА Репозиторій] RecordPaymentTransactionAsync: Не вдалося вставити запис в payments для замовлення {sessionId}.");
                                    await transaction.RollbackAsync(); // Відкатуємо транзакцію
                                    return false;
                                }
                            }
                        } // end using updateCmd, using insertCmd

                        // Якщо обидва кроки успішні, комітимо транзакцію
                        await transaction.CommitAsync();
                        Console.WriteLine($"[ЛОГ Репозиторій] RecordPaymentTransactionAsync: Транзакція для замовлення {sessionId} успішно завершена.");
                        return true; // Повертаємо true, бо операція успішна

                    }
                    catch (Exception ex)
                    {
                        // Якщо сталася будь-яка помилка всередині try, відкатуємо транзакцію
                        Console.WriteLine($"[ПОМИЛКА Репозиторій] RecordPaymentTransactionAsync: Помилка під час транзакції для замовлення {sessionId}: {ex.Message}\n{ex.StackTrace}");
                        try
                        {
                            await transaction.RollbackAsync();
                            Console.WriteLine($"[ЛОГ Репозиторій] RecordPaymentTransactionAsync: Транзакція для замовлення {sessionId} відкатана.");
                        }
                        catch (Exception rollbackEx)
                        {
                            // Обробка помилок відкату
                            Console.WriteLine($"[КРИТИЧНА ПОМИЛКА Репозиторій] RecordPaymentTransactionAsync: Помилка при відкаті транзакції для замовлення {sessionId}: {rollbackEx.Message}\n{rollbackEx.StackTrace}");
                        }
                        // Викидаємо виняток далі, щоб він був спійманий у BookingService
                        throw;
                    }
                } // end using transaction
            } // end using connection
        }

        /// <summary>
        /// Скасовує замовлення, оновлюючи його статус.
        /// </summary>
        /// <returns>True, якщо замовлення знайдено та скасовано; False, якщо замовлення не знайдено.</returns>
        public static async Task<bool> CancelOrderAsync(
            MySqlConnection connection,
            int sessionId,
            int managerId,
            int clubId
        )
        {
            // Оновлюємо payment_status на 'Cancelled' і, можливо, скидаємо final_price
            var query = @"
            UPDATE sessions
            SET
                payment_status = 'Cancelled',
                final_price = 0.00 -- Або NULL, залежно від вашої схеми та логіки
            WHERE session_id = @session_id
              AND manager_id = @manager_id
              AND club_id = @club_id
              AND payment_status != 'Cancelled' -- Опціонально: не скасовувати, якщо вже скасовано
        ";

            var cmd = new MySqlCommand(query, connection);
            cmd.Parameters.AddWithValue("@session_id", sessionId);
            cmd.Parameters.AddWithValue("@manager_id", managerId);
            cmd.Parameters.AddWithValue("@club_id", clubId);

            int rowsAffected = await cmd.ExecuteNonQueryAsync();

            // Якщо rowsAffected > 0, значить, замовлення було знайдено та оновлено (скасовано)
            return rowsAffected > 0;
        }

        /// <summary>
        /// Оновлює поле calculate_price для замовлення.
        /// </summary>
        /// <returns>True, якщо замовлення знайдено та оновлено; False, якщо замовлення не знайдено.</returns>
        public static async Task<bool> UpdateCalculatedPriceAsync(
            MySqlConnection connection,
            int sessionId,
            int managerId,
            int clubId,
            decimal calculatedPrice
        )
        {
            var query = @"
            UPDATE sessions
            SET
                calculate_price = @calculated_price
            WHERE session_id = @session_id
              AND manager_id = @manager_id
              AND club_id = @club_id
        ";

            var cmd = new MySqlCommand(query, connection);
            cmd.Parameters.AddWithValue("@session_id", sessionId);
            cmd.Parameters.AddWithValue("@manager_id", managerId);
            cmd.Parameters.AddWithValue("@club_id", clubId);
            cmd.Parameters.AddWithValue("@calculated_price", calculatedPrice); // Використовуємо тип decimal для MySqlParameter

            int rowsAffected = await cmd.ExecuteNonQueryAsync();

            // Якщо rowsAffected > 0, значить, замовлення було знайдено та оновлено
            return rowsAffected > 0;
        }
        public struct OccupiedZonesResult
        {
            public int OccupiedVrQuest { get; set; }
            public int OccupiedPs { get; set; }
        }

        /// <summary>
        /// Виконує запит до БД для підрахунку зайнятих зон VR/Quest та PS
        /// для заданого часового проміжку та клубу.
        /// </summary>
        public static async Task<OccupiedZonesResult> GetOccupiedZonesAsync(
            string connectionString,
            DateTime checkDate,
            TimeSpan checkStartTime, // Використовуємо TimeSpan
            TimeSpan checkEndTime,   // Використовуємо TimeSpan
            int clubId)
        {
            var result = new OccupiedZonesResult { OccupiedVrQuest = 0, OccupiedPs = 0 };

            // Ваш SQL запит з плейсхолдерами
            var query = @"
            SELECT
                COALESCE(SUM(CASE WHEN session_type IN ('VR', 'Quest') AND payment_status IN ('Paid', 'Pending') THEN num_people ELSE 0 END), 0) AS occupied_vr_quest_zones,
                COALESCE(SUM(CASE WHEN session_type = 'PS' AND payment_status IN ('Paid', 'Pending') THEN num_people ELSE 0 END), 0) AS occupied_ps_zones
            FROM
                sessions
            WHERE
                session_date = @check_date
                AND club_id = @club_id
                -- Перевірка перетину часових проміжків: [start_time, end_time] ПЕРЕТИНАЄТЬСЯ з [check_start_time, check_end_time]
                -- якщо (start_time < check_end_time) AND (end_time > check_start_time)
                AND start_time < @check_end_time_ts -- end_time перевіряється з початком сесії
                AND end_time > @check_start_time_ts; -- start_time перевіряється з кінцем сесії
        ";

            using (var connection = new MySqlConnection(connectionString))
            {
                await connection.OpenAsync();

                using (var cmd = new MySqlCommand(query, connection))
                {
                    // Додаємо параметри. Для TimeSpan MySqlConnector (або Connector/NET) зазвичай добре працює.
                    cmd.Parameters.AddWithValue("@check_date", checkDate.Date); // Важливо: беремо тільки дату
                    cmd.Parameters.AddWithValue("@club_id", clubId);
                    cmd.Parameters.AddWithValue("@check_start_time_ts", checkStartTime); // Передаємо TimeSpan
                    cmd.Parameters.AddWithValue("@check_end_time_ts", checkEndTime);   // Передаємо TimeSpan

                    try
                    {
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            // Очікуємо отримати рівно один рядок з результатами
                            if (await reader.ReadAsync())
                            {
                                // Читаємо результати за назвами стовпців
                                result.OccupiedVrQuest = reader.GetInt32("occupied_vr_quest_zones");
                                result.OccupiedPs = reader.GetInt32("occupied_ps_zones");
                            }
                            // Якщо reader.ReadAsync() повернув false, це означає, що SUM повернув NULL (немає відповідних сесій),
                            // але ми додали COALESCE в запит, тому отримаємо 0, і структура result вже ініціалізована нулями.
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ПОМИЛКА Репозиторій] GetOccupiedZonesAsync: Помилка виконання запиту: {ex.Message}\n{ex.StackTrace}");
                        throw; // Прокидаємо виняток далі, щоб обробник міг його спіймати і повернути помилку клієнту
                    }
                }
            }

            return result;
        }
        public static async Task<SessionDetails> GetSessionDetailsAsync(
    MySqlConnection connection,
    int sessionId,
    int managerId,
    int clubId)
        {
            var query = @"
        SELECT 
            s.session_id,
            s.client_id AS ClientId,
            s.session_date,
            s.start_time,
            s.end_time,
            s.num_people,
            s.notes,
            s.session_type,
            s.payment_status,
            s.calculate_price,
            s.final_price,
            c.name AS client_name,
            c.phone_number AS client_phone,
            m.name AS manager_name
        FROM sessions s
        JOIN clients c ON s.client_id = c.client_id
        JOIN managers m ON s.manager_id = m.manager_id
        WHERE s.session_id = @session_id 
          AND s.manager_id = @manager_id 
          AND s.club_id = @club_id";

            var cmd = new MySqlCommand(query, connection);
            cmd.Parameters.AddWithValue("@session_id", sessionId);
            cmd.Parameters.AddWithValue("@manager_id", managerId);
            cmd.Parameters.AddWithValue("@club_id", clubId);

            using (var reader = await cmd.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync())
                {
                    return new SessionDetails
                    {
                        SessionId = reader.GetInt32("session_id"),
                        ClientId = reader.GetInt32("ClientId"),
                        SessionDate = reader.GetDateTime("session_date"),
                        StartTime = reader.GetTimeSpan("start_time"),
                        EndTime = reader.GetTimeSpan("end_time"),
                        NumPeople = reader.GetInt32("num_people"),
                        Notes = reader["notes"]?.ToString(),
                        SessionType = reader["session_type"]?.ToString(),
                        PaymentStatus = reader["payment_status"]?.ToString(),
                        CalculatePrice = reader.GetDecimal("calculate_price"),
                        FinalPrice = reader.GetDecimal("final_price"),
                        ClientName = reader["client_name"]?.ToString(),
                        ClientPhone = reader["client_phone"]?.ToString(),
                        ManagerName = reader["manager_name"]?.ToString()
                    };
                }
            }


            return null; // якщо нічого не знайдено
        }

        public static async Task<int> CreateClientAsync(string connectionString, string clientName, string phone, MySqlConnection connection, MySqlTransaction transaction = null) // Потрібне відкрите з'єднання
        {
            if (connection == null || connection.State != ConnectionState.Open)
                throw new ArgumentException("Відкрите з'єднання з БД є обов'язковим для створення клієнта.");

            string query = "INSERT INTO clients (name, phone_number) VALUES (@client_name, @phone); SELECT LAST_INSERT_ID();";
            using (var cmd = new MySqlCommand(query, connection, transaction))
            {
                cmd.Parameters.AddWithValue("@client_name", clientName);
                cmd.Parameters.AddWithValue("@phone", phone);
                object result = await cmd.ExecuteScalarAsync();
                if (result != null && ulong.TryParse(result.ToString(), out ulong newIdUlong) && newIdUlong > 0)
                {
                    return (int)newIdUlong;
                }
                return -1; // Помилка
            }
        }

        public static async Task<bool> UpdateClientAsync(string connectionString, int clientId, string clientName, string phone, MySqlConnection connection, MySqlTransaction transaction)
        {
            Console.WriteLine($"[REPO LOG] UpdateClientAsync (ClientId={clientId}): Початок оновлення клієнта."); // Додано лог
            if (connection == null || connection.State != ConnectionState.Open || transaction == null)
                throw new ArgumentException("Відкрите з'єднання та активна транзакція є обов'язковими для оновлення клієнта.");

            string query = "UPDATE clients SET name = @client_name, phone_number = @phone_number WHERE client_id = @client_id";
            using (var cmd = new MySqlCommand(query, connection, transaction))
            {
                cmd.Parameters.AddWithValue("@client_name", clientName);
                cmd.Parameters.AddWithValue("@phone_number", phone);
                cmd.Parameters.AddWithValue("@client_id", clientId);
                int rowsAffected = await cmd.ExecuteNonQueryAsync();
                Console.WriteLine($"[REPO LOG] UpdateClientAsync (ClientId={clientId}): Rows Affected = {rowsAffected}"); // ДОДАНО ЛОГ ROWS AFFECTED
                return rowsAffected > 0;
            }
        }

        // --- СЕСІЇ (БРОНЮВАННЯ) ---
        public static async Task<int?> GetSessionClubIdAsync(string connectionString, int sessionId)
        {
            using (var connection = new MySqlConnection(connectionString))
            {
                try
                {
                    await connection.OpenAsync();
                    string query = "SELECT club_id FROM sessions WHERE session_id = @session_id";
                    using (var cmd = new MySqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@session_id", sessionId);
                        object result = await cmd.ExecuteScalarAsync();
                        return (result != null && result != DBNull.Value && int.TryParse(result.ToString(), out int id)) ? id : (int?)null;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ПОМИЛКА_SQL] GetSessionClubIdAsync: {ex.Message}");
                    return null;
                }
            }
        }

        public static async Task<int> GetBookedZonesAsync(string connectionString, DateTime sessionDate, TimeSpan startTime, TimeSpan endTime, int clubId, string sessionTypeGroup, int? editingSessionId, MySqlConnection connection, MySqlTransaction transaction = null)
        {
            if (connection == null || connection.State != ConnectionState.Open)
                throw new ArgumentException("Відкрите з'єднання з БД є обов'язковим для GetBookedZonesAsync.");

            string sessionTypeCondition;
            if (sessionTypeGroup == "PS") sessionTypeCondition = "AND session_type = 'PS'";
            else if (sessionTypeGroup == "VR_QUEST") sessionTypeCondition = "AND session_type IN ('VR', 'Quest')";
            else sessionTypeCondition = "AND 1=0"; // Невідома група

            string query = $@"
    SELECT SUM(num_people) FROM sessions s -- <<< ДОДАНО ПСЕВДОНІМ 's'
    WHERE session_date = @session_date
      AND club_id = @club_id
      AND (start_time < @end_time AND end_time > @start_time)
      AND s.payment_status <> 'Cancelled' -- <<< ТАКОЖ ВИПРАВЛЕНО ОДНУ 'L' НА ДВІ ДЛЯ КОНСИСТЕНТНОСТІ З CANCEL_ORDER
      {sessionTypeCondition}";

            if (editingSessionId.HasValue)
            {
                query += " AND session_id <> @editing_session_id";
            }

            using (var cmd = new MySqlCommand(query, connection, transaction))
            {
                cmd.Parameters.AddWithValue("@session_date", sessionDate.Date);
                cmd.Parameters.AddWithValue("@club_id", clubId);
                cmd.Parameters.AddWithValue("@start_time", startTime);
                cmd.Parameters.AddWithValue("@end_time", endTime);
                if (editingSessionId.HasValue) cmd.Parameters.AddWithValue("@editing_session_id", editingSessionId.Value);

                object result = await cmd.ExecuteScalarAsync();
                return (result == DBNull.Value || result == null) ? 0 : Convert.ToInt32(result);
            }
        }

        public static async Task<bool> InsertBookingAsync(string connectionString, int clientId, DateTime sessionDate, TimeSpan startTime, TimeSpan endTime, int numPeople, string notes, string sessionType, int managerId, int clubId, int? discountId, decimal calculatedPrice, decimal finalPrice, MySqlConnection connection, MySqlTransaction transaction = null)
        {
            if (connection == null || connection.State != ConnectionState.Open)
                throw new ArgumentException("Відкрите з'єднання з БД є обов'язковим для InsertBookingAsync.");

            string query = @"
                INSERT INTO sessions (client_id, session_date, start_time, end_time, num_people, notes, session_type, payment_status, manager_id, club_id, discount_id, calculate_price, final_price)
                VALUES (@client_id, @session_date, @start_time, @end_time, @num_people, @notes, @session_type, @payment_status, @manager_id, @club_id, @discount_id, @calculate_price, @final_price)";

            using (var cmd = new MySqlCommand(query, connection, transaction))
            {
                cmd.Parameters.AddWithValue("@client_id", clientId);
                cmd.Parameters.AddWithValue("@session_date", sessionDate.Date);
                cmd.Parameters.AddWithValue("@start_time", startTime);
                cmd.Parameters.AddWithValue("@end_time", endTime);
                cmd.Parameters.AddWithValue("@num_people", numPeople);
                cmd.Parameters.AddWithValue("@notes", (object)notes ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@session_type", sessionType);
                cmd.Parameters.AddWithValue("@payment_status", "Pending"); // Статус за замовчуванням
                cmd.Parameters.AddWithValue("@manager_id", managerId);
                cmd.Parameters.AddWithValue("@club_id", clubId);
                cmd.Parameters.AddWithValue("@discount_id", (object)discountId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@calculate_price", calculatedPrice);
                cmd.Parameters.AddWithValue("@final_price", finalPrice);

                int rowsAffected = await cmd.ExecuteNonQueryAsync();
                return rowsAffected > 0;
            }
        }

        public static async Task<bool> UpdateSessionAsync(string connectionString, int sessionId, int clientId, DateTime sessionDate, TimeSpan startTime, TimeSpan endTime, int numPeople, string sessionType, string notes, int? discountId, decimal calculatedPrice, decimal finalPrice, MySqlConnection connection, MySqlTransaction transaction)
        {
            if (connection == null || connection.State != ConnectionState.Open || transaction == null)
                throw new ArgumentException("Відкрите з'єднання та активна транзакція є обов'язковими для UpdateSessionAsync.");

            string query = @"
                UPDATE sessions
                SET client_id = @client_id,
                    session_date = @session_date,
                    start_time = @start_time,
                    end_time = @end_time,
                    num_people = @num_people,
                    session_type = @session_type,
                    notes = @notes,
                    discount_id = @discount_id,
                    calculate_price = @calculate_price,
                    final_price = @final_price
                WHERE session_id = @session_id";
            using (var cmd = new MySqlCommand(query, connection, transaction))
            {
                cmd.Parameters.AddWithValue("@client_id", clientId);
                cmd.Parameters.AddWithValue("@session_date", sessionDate.Date);
                cmd.Parameters.AddWithValue("@start_time", startTime);
                cmd.Parameters.AddWithValue("@end_time", endTime);
                cmd.Parameters.AddWithValue("@num_people", numPeople);
                cmd.Parameters.AddWithValue("@session_type", sessionType);
                cmd.Parameters.AddWithValue("@notes", (object)notes ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@discount_id", (object)discountId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@calculate_price", calculatedPrice);
                cmd.Parameters.AddWithValue("@final_price", finalPrice);
                cmd.Parameters.AddWithValue("@session_id", sessionId);

                int rowsAffected = await cmd.ExecuteNonQueryAsync();
                Console.WriteLine($"[REPO LOG] UpdateSessionAsync (SessionId={sessionId}): Rows Affected = {rowsAffected}"); // Додайте це логування
                return rowsAffected > 0;
            }
        }

        public static async Task<List<Dictionary<string, object>>> GetCurrentBookingsAsync(string connectionString, DateTime currentDate, TimeSpan currentTime)
        {
            var bookings = new List<Dictionary<string, object>>();
            using (var connection = new MySqlConnection(connectionString))
            {
                try
                {
                    await connection.OpenAsync();
                    string query = @"
                        SELECT c.name AS client_name, c.phone_number, s.session_id, s.start_time, s.end_time,
                               s.num_people, s.session_type, s.session_date, s.payment_status, s.club_id
                        FROM sessions s
                        INNER JOIN clients c ON s.client_id = c.client_id
                        WHERE s.session_date = @currentDate
                          AND TIME(s.start_time) <= @currentTime
                          AND TIME(s.end_time) > @currentTime
                          AND s.payment_status <> 'Canceled'";
                    using (var cmd = new MySqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@currentDate", currentDate.Date);
                        cmd.Parameters.AddWithValue("@currentTime", currentTime);
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                bookings.Add(ReadBookingSummary(reader));
                            }
                        }
                    }
                }
                catch (Exception ex) { Console.WriteLine($"[ПОМИЛКА_SQL] GetCurrentBookingsAsync: {ex.Message}"); }
            }
            return bookings;
        }
        public static async Task<bool> UpdateSessionDetailsAndPriceAsync(string connectionString, int sessionId, int clientId, DateTime sessionDate, TimeSpan startTime, TimeSpan endTime, int numPeople, string sessionType, string notes, int? discountId, decimal finalPrice, MySqlConnection connection, MySqlTransaction transaction)
        {
            Console.WriteLine($"[REPO LOG] UpdateSessionDetailsAndPriceAsync (SessionId={sessionId}): Початок оновлення сесії."); // Додано лог
            if (connection == null || connection.State != ConnectionState.Open || transaction == null)
                throw new ArgumentException("Відкрите з'єднання та активна транзакція є обов'язковими для UpdateSessionDetailsAndPriceAsync.");

            string query = @"
         UPDATE sessions
         SET client_id = @client_id,
             session_date = @session_date,
             start_time = @start_time,
             end_time = @end_time,
             num_people = @num_people,
             session_type = @session_type,
             notes = @notes,
             discount_id = @discount_id,
             final_price = @final_price
         WHERE session_id = @session_id";

            using (var cmd = new MySqlCommand(query, connection, transaction))
            {
                cmd.Parameters.AddWithValue("@client_id", clientId);
                cmd.Parameters.AddWithValue("@session_date", sessionDate.Date);
                cmd.Parameters.AddWithValue("@start_time", startTime);
                cmd.Parameters.AddWithValue("@end_time", endTime);
                cmd.Parameters.AddWithValue("@num_people", numPeople);
                cmd.Parameters.AddWithValue("@session_type", sessionType);
                cmd.Parameters.AddWithValue("@notes", (object)notes ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@discount_id", (object)discountId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@final_price", finalPrice);
                cmd.Parameters.AddWithValue("@session_id", sessionId);

                int rowsAffected = await cmd.ExecuteNonQueryAsync();
                Console.WriteLine($"[REPO LOG] UpdateSessionDetailsAndPriceAsync (SessionId={sessionId}): Rows Affected = {rowsAffected}"); // ДОДАНО ЛОГ ROWS AFFECTED
                return rowsAffected > 0;
            }
        }
        public static async Task<List<Dictionary<string, object>>> GetBookingsForTimeRangeAsync(string connectionString, DateTime sessionDate, TimeSpan startTime, TimeSpan endTime, int? clubId = null)
        {
            var bookings = new List<Dictionary<string, object>>();
            using (var connection = new MySqlConnection(connectionString))
            {
                try
                {
                    await connection.OpenAsync();
                    string query = @"
                        SELECT s.session_id, s.client_id, c.name AS client_name, c.phone_number,
                               s.start_time, s.end_time, s.num_people, s.session_type, s.session_date,
                               s.notes, s.payment_status, s.discount_id, d.name as discount_name,
                               d.percent as discount_percent, s.calculate_price, s.final_price,
                               s.manager_id, m.name as manager_name, s.club_id, cl.name as club_name
                        FROM sessions s
                        INNER JOIN clients c ON s.client_id = c.client_id
                        LEFT JOIN managers m ON s.manager_id = m.manager_id
                        LEFT JOIN discounts d ON s.discount_id = d.discount_id AND s.club_id = d.club_id
                        LEFT JOIN clubs cl ON s.club_id = cl.club_id
                        WHERE s.session_date = @sessionDate
                          AND (s.start_time < @endTime AND s.end_time > @startTime)
                          AND s.payment_status != 'Cancelled'";
                    if (clubId.HasValue) query += " AND s.club_id = @clubId";

                    using (var cmd = new MySqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@sessionDate", sessionDate.Date);
                        cmd.Parameters.AddWithValue("@startTime", startTime);
                        cmd.Parameters.AddWithValue("@endTime", endTime);
                        if (clubId.HasValue) cmd.Parameters.AddWithValue("@clubId", clubId.Value);

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                bookings.Add(ReadFullBookingDetails(reader));
                            }
                        }
                    }
                }
                catch (Exception ex) { Console.WriteLine($"[ПОМИЛКА_SQL] GetBookingsForTimeRangeAsync: {ex.Message}"); }
            }
            return bookings;
        }

        public static async Task<bool> DeleteBookingAsync(string connectionString, int sessionId)
        {
            using (var connection = new MySqlConnection(connectionString))
            {
                try
                {
                    await connection.OpenAsync();
                    string query = "DELETE FROM sessions WHERE session_id = @session_id";
                    using (var cmd = new MySqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@session_id", sessionId);
                        int rowsAffected = await cmd.ExecuteNonQueryAsync();
                        return rowsAffected > 0;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ПОМИЛКА_SQL] DeleteBookingAsync: {ex.Message}");
                    return false;
                }
            }
        }

        // --- ЦІНИ ТА ЗНИЖКИ ---
        public static async Task<decimal> GetPricePerHourAsync(string connectionString, string sessionType, int? clubId, MySqlConnection connection = null, MySqlTransaction transaction = null)
        {
            bool ownConnection = connection == null;
            MySqlConnection conn = connection ?? new MySqlConnection(connectionString);
            try
            {
                if (ownConnection) await conn.OpenAsync();

                // Припускаємо, що ціни НЕ залежать від клубу (якщо залежать - додати фільтр)
                string query = "SELECT price_per_hour FROM session_prices WHERE session_type = @session_type";
                // if (clubId.HasValue) query += " AND club_id = @clubId"; // Якщо ціни залежать від клубу

                using (var cmd = new MySqlCommand(query, conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@session_type", sessionType);
                    // if (clubId.HasValue) cmd.Parameters.AddWithValue("@clubId", clubId.Value);

                    object result = await cmd.ExecuteScalarAsync();
                    return (result != null && result != DBNull.Value && decimal.TryParse(result.ToString(), out decimal price)) ? price : -1m;
                }
            }
            catch (MySqlException myEx) when (myEx.Number == 1054 && clubId.HasValue) // Unknown column
            {
                Console.WriteLine($"[ПОМИЛКА_SQL] GetPricePerHourAsync: Колонка 'club_id' відсутня в 'session_prices'? {myEx.Message}");
                return -1m;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ПОМИЛКА_SQL] GetPricePerHourAsync: {ex.Message}");
                return -1m;
            }
            finally
            {
                if (ownConnection && conn?.State == ConnectionState.Open) await conn.CloseAsync();
            }
        }

        public static async Task<int> GetDiscountPercentAsync(string connectionString, int discountId, int clubId, MySqlConnection connection = null, MySqlTransaction transaction = null)
        {
            bool ownConnection = connection == null;
            MySqlConnection conn = connection ?? new MySqlConnection(connectionString);
            try
            {
                if (ownConnection) await conn.OpenAsync();
                string query = "SELECT percent FROM discounts WHERE discount_id = @discount_id AND club_id = @club_id AND is_active = TRUE";
                using (var cmd = new MySqlCommand(query, conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@discount_id", discountId);
                    cmd.Parameters.AddWithValue("@club_id", clubId);
                    object result = await cmd.ExecuteScalarAsync();
                    return (result != null && result != DBNull.Value && int.TryParse(result.ToString(), out int percent)) ? percent : 0;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ПОМИЛКА_SQL] GetDiscountPercentAsync: {ex.Message}");
                return 0;
            }
            finally
            {
                if (ownConnection && conn?.State == ConnectionState.Open) await conn.CloseAsync();
            }
        }

        public static async Task<Dictionary<string, decimal>> GetPricesAsync(string connectionString, int? clubId = null)
        {
            var prices = new Dictionary<string, decimal>();
            using (var connection = new MySqlConnection(connectionString))
            {
                try
                {
                    await connection.OpenAsync();
                    string query = "SELECT session_type, price_per_hour FROM session_prices";
                    // if (clubId.HasValue) query += " WHERE club_id = @clubId"; // Якщо ціни залежать від клубу

                    using (var cmd = new MySqlCommand(query, connection))
                    {
                        // if (clubId.HasValue) cmd.Parameters.AddWithValue("@clubId", clubId.Value);
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                prices[reader.GetString("session_type")] = reader.GetDecimal("price_per_hour");
                            }
                        }
                    }
                }
                catch (MySqlException myEx) when (myEx.Number == 1054 && clubId.HasValue)
                {
                    Console.WriteLine($"[ПОМИЛКА_SQL] GetPricesAsync: Колонка 'club_id' відсутня в 'session_prices'? {myEx.Message}");
                    // Повертаємо пустий словник, бо не змогли відфільтрувати
                    return prices;
                }
                catch (Exception ex) { Console.WriteLine($"[ПОМИЛКА_SQL] GetPricesAsync: {ex.Message}"); }
            }
            return prices;
        }

        public static async Task<List<Dictionary<string, object>>> GetNewBookingsAsync(
            string connectionString,
            int clubId)
        {
            var newBookingsList = new List<Dictionary<string, object>>();

            // Запит для отримання бронювань зі статусом 'New' та приєднання даних клієнта
            var query = @"
                SELECT
                    s.session_id,
                    c.name AS client_name,
                    c.phone_number,
                    s.session_date,
                    s.start_time,
                    s.end_time,
                    s.num_people,
                    s.session_type,
                    s.payment_status -- Очікуємо 'New'
                    -- s.club_id -- Можна не повертати, якщо фільтруємо по ньому
                FROM
                    sessions s
                JOIN
                    clients c ON s.client_id = c.client_id
                WHERE
                    s.club_id = @club_id
                    AND s.payment_status = 'New';
            ";

            using (var connection = new MySqlConnection(connectionString))
            {
                try
                {
                    await connection.OpenAsync();

                    using (var cmd = new MySqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@club_id", clubId);

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                // Створюємо словник для одного бронювання, відповідаючи структурі мобільної моделі Booking
                                var bookingData = new Dictionary<string, object>();
                                bookingData["booking_id"] = reader.GetInt32("session_id"); // Мапінг session_id на booking_id для клієнта
                                bookingData["client_name"] = reader.GetString("client_name");
                                // Перевірка на DBNull для телефону, якщо він може бути порожнім в БД
                                bookingData["phone"] = reader.IsDBNull(reader.GetOrdinal("phone_number")) ? null : reader.GetString("phone_number");
                                bookingData["date"] = reader.GetDateTime("session_date").ToString("yyyy-MM-dd");
                                bookingData["start_time"] = reader.GetTimeSpan("start_time").ToString(@"hh\:mm\:ss"); // Форматуємо TimeSpan
                                bookingData["end_time"] = reader.GetTimeSpan("end_time").ToString(@"hh\:mm\:ss");     // Форматуємо TimeSpan
                                bookingData["num_people"] = reader.GetInt32("num_people");
                                bookingData["session_type"] = reader.GetString("session_type");
                                bookingData["status"] = reader.GetString("payment_status"); // Має бути 'New'

                                newBookingsList.Add(bookingData);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ПОМИЛКА Репозиторій] GetNewBookingsAsync: Помилка виконання запиту: {ex.Message}\n{ex.StackTrace}");
                    // В разі помилки можна повернути пустий список або прокинути виняток
                    throw; // Прокидаємо виняток, щоб сервіс його обробив
                }
            }

            return newBookingsList;
        }

        /// <summary>
        /// Оновлює статус бронювання за його ID.
        /// </summary>
        /// <param name="connectionString">Рядок підключення до бази даних.</param>
        /// <param name="sessionId">ID бронювання (сесії).</param>
        /// <param name="newStatus">Новий статус ('Pending', 'Paid', 'Cancelled' тощо).</param>
        /// <returns>True, якщо бронювання знайдено та оновлено; False, якщо не знайдено.</returns>
        public static async Task<bool> UpdateBookingStatusAsync(
            string connectionString,
            int sessionId,
            string newStatus)
        {
            // Перевірка на допустимі статуси може бути тут або в сервісі
            // if (!new List<string> {"New", "Pending", "Paid", "Cancelled", "Completed"}.Contains(newStatus))
            // {
            //     Console.WriteLine($"[ПОПЕРЕДЖЕННЯ Репозиторій] UpdateBookingStatusAsync: Неприпустимий статус '{newStatus}'.");
            //     return false; // Або кинути ArgumentException
            // }

            var query = @"
                UPDATE sessions
                SET payment_status = @new_status
                WHERE session_id = @session_id;
            ";

            using (var connection = new MySqlConnection(connectionString))
            {
                try
                {
                    await connection.OpenAsync();

                    using (var cmd = new MySqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@new_status", newStatus);
                        cmd.Parameters.AddWithValue("@session_id", sessionId);

                        int rowsAffected = await cmd.ExecuteNonQueryAsync();

                        // Якщо rowsAffected > 0, значить, бронювання з таким ID існувало та статус був оновлений
                        return rowsAffected > 0;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ПОМИЛКА Репозиторій] UpdateBookingStatusAsync (ID: {sessionId}, Status: {newStatus}): Помилка виконання запиту: {ex.Message}\n{ex.StackTrace}");
                    throw; // Прокидаємо виняток
                }
            }
        }

        // --- МЕНЕДЖЕРИ ---
        public static async Task<int?> GetClubIdForManagerAsync(string connectionString, int managerId, MySqlConnection connection = null, MySqlTransaction transaction = null)
        {
            bool ownConnection = connection == null;
            MySqlConnection conn = connection ?? new MySqlConnection(connectionString);
            try
            {
                if (ownConnection) await conn.OpenAsync();
                string query = "SELECT club_id FROM managers WHERE manager_id = @managerId";
                using (var cmd = new MySqlCommand(query, conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@managerId", managerId);
                    object result = await cmd.ExecuteScalarAsync();
                    return (result != null && result != DBNull.Value && int.TryParse(result.ToString(), out int id)) ? id : (int?)null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ПОМИЛКА_SQL] GetClubIdForManagerAsync: {ex.Message}");
                return null;
            }
            finally
            {
                if (ownConnection && conn?.State == ConnectionState.Open) await conn.CloseAsync();
            }
        }

        // --- Допоміжні методи читання даних ---
        private static Dictionary<string, object> ReadBookingSummary(MySqlDataReader reader)
        {
            // Отримуємо індекси стовпців один раз для ефективності
            int sessionIdIdx = reader.GetOrdinal("session_id");
            int clientNameIdx = reader.GetOrdinal("client_name");
            int phoneNumberIdx = reader.GetOrdinal("phone_number");
            int startTimeIdx = reader.GetOrdinal("start_time");
            int endTimeIdx = reader.GetOrdinal("end_time");
            int numPeopleIdx = reader.GetOrdinal("num_people");
            int sessionTypeIdx = reader.GetOrdinal("session_type");
            int sessionDateIdx = reader.GetOrdinal("session_date");
            int paymentStatusIdx = reader.GetOrdinal("payment_status");
            int clubIdIdx = reader.GetOrdinal("club_id");

            return new Dictionary<string, object> {
                { "session_id", reader.GetInt32(sessionIdIdx) }, // Використовуємо індекс
                { "client_name", reader.GetString(clientNameIdx) }, // Використовуємо індекс
                { "phone_number", reader.IsDBNull(phoneNumberIdx) ? null : reader.GetString(phoneNumberIdx) }, // Використовуємо індекс
                { "start_time", reader.GetTimeSpan(startTimeIdx).ToString(@"hh\:mm") }, // Використовуємо індекс
                { "end_time", reader.GetTimeSpan(endTimeIdx).ToString(@"hh\:mm") }, // Використовуємо індекс
                { "num_people", reader.GetInt32(numPeopleIdx) }, // Використовуємо індекс
                { "session_type", reader.GetString(sessionTypeIdx) }, // Використовуємо індекс
                { "session_date", reader.GetDateTime(sessionDateIdx).ToString("yyyy-MM-dd") }, // Використовуємо індекс
                { "payment_status", reader.GetString(paymentStatusIdx) }, // Використовуємо індекс
                { "club_id", reader.IsDBNull(clubIdIdx) ? (int?)null : reader.GetInt32(clubIdIdx) } // Використовуємо індекс
            };
        }

        private static Dictionary<string, object> ReadFullBookingDetails(MySqlDataReader reader)
        {
            // Отримуємо індекси стовпців один раз
            int sessionIdIdx = reader.GetOrdinal("session_id");
            int clientIdIdx = reader.GetOrdinal("client_id");
            int clientNameIdx = reader.GetOrdinal("client_name");
            int phoneNumberIdx = reader.GetOrdinal("phone_number");
            int startTimeIdx = reader.GetOrdinal("start_time");
            int endTimeIdx = reader.GetOrdinal("end_time");
            int numPeopleIdx = reader.GetOrdinal("num_people");
            int sessionTypeIdx = reader.GetOrdinal("session_type");
            int sessionDateIdx = reader.GetOrdinal("session_date");
            int notesIdx = reader.GetOrdinal("notes");
            int paymentStatusIdx = reader.GetOrdinal("payment_status");
            int discountIdIdx = reader.GetOrdinal("discount_id");
            int discountNameIdx = reader.GetOrdinal("discount_name");
            int discountPercentIdx = reader.GetOrdinal("discount_percent");
            int calculatePriceIdx = reader.GetOrdinal("calculate_price");
            int finalPriceIdx = reader.GetOrdinal("final_price");
            int managerIdIdx = reader.GetOrdinal("manager_id");
            int managerNameIdx = reader.GetOrdinal("manager_name");
            int clubIdIdx = reader.GetOrdinal("club_id");
            int clubNameIdx = reader.GetOrdinal("club_name");

            return new Dictionary<string, object> {
                { "session_id", reader.GetInt32(sessionIdIdx) }, // Використовуємо індекс
                { "client_id", reader.GetInt32(clientIdIdx) }, // Використовуємо індекс
                { "client_name", reader.GetString(clientNameIdx) }, // Використовуємо індекс
                { "phone_number", reader.IsDBNull(phoneNumberIdx) ? null : reader.GetString(phoneNumberIdx) }, // Використовуємо індекс
                { "start_time", reader.GetTimeSpan(startTimeIdx).ToString(@"hh\:mm") }, // Використовуємо індекс
                { "end_time", reader.GetTimeSpan(endTimeIdx).ToString(@"hh\:mm") }, // Використовуємо індекс
                { "num_people", reader.GetInt32(numPeopleIdx) }, // Використовуємо індекс
                { "session_type", reader.GetString(sessionTypeIdx) }, // Використовуємо індекс
                { "session_date", reader.GetDateTime(sessionDateIdx).ToString("yyyy-MM-dd") }, // Використовуємо індекс
                { "notes", reader.IsDBNull(notesIdx) ? null : reader.GetString(notesIdx) }, // Використовуємо індекс
                { "payment_status", reader.GetString(paymentStatusIdx) }, // Використовуємо індекс
                { "discount_id", reader.IsDBNull(discountIdIdx) ? (int?)null : reader.GetInt32(discountIdIdx) }, // Використовуємо індекс
                { "discount_name", reader.IsDBNull(discountNameIdx) ? null : reader.GetString(discountNameIdx) }, // Використовуємо індекс
                { "discount_percent", reader.IsDBNull(discountPercentIdx) ? (int?)null : reader.GetInt32(discountPercentIdx) }, // Використовуємо індекс
                { "calculate_price", reader.IsDBNull(calculatePriceIdx) ? (decimal?)null : reader.GetDecimal(calculatePriceIdx) }, // Використовуємо індекс
                { "final_price", reader.IsDBNull(finalPriceIdx) ? (decimal?)null : reader.GetDecimal(finalPriceIdx) }, // Використовуємо індекс
                { "manager_id", reader.IsDBNull(managerIdIdx) ? (int?)null : reader.GetInt32(managerIdIdx) }, // Використовуємо індекс
                { "manager_name", reader.IsDBNull(managerNameIdx) ? null : reader.GetString(managerNameIdx) }, // Використовуємо індекс
                { "club_id", reader.IsDBNull(clubIdIdx) ? (int?)null : reader.GetInt32(clubIdIdx) }, // Використовуємо індекс
                { "club_name", reader.IsDBNull(clubNameIdx) ? null : reader.GetString(clubNameIdx) } // Використовуємо індекс
            };
        }
    }
}