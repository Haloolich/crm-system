// Data/ManagerRepository.cs
using MySqlConnector;
using System;
using System.Data;
using System.Threading.Tasks;
using System.Collections.Generic; // Додано для List
using ConsoleBookingApp.Models; // Використовуємо створені моделі

namespace ConsoleBookingApp.Data
{
    public static class ManagerRepository // Зробимо його static, як і BookingRepository
    {
        // Метод для отримання повної інформації про менеджера разом з назвою клубу
        public static async Task<bool> CheckForPendingBookingsBeforeShiftCloseAsync(string connectionString, int managerId, int shiftId)
        {
            Console.WriteLine($"[ManagerRepository] Checking for unprocessed bookings before closing shift ID {shiftId} for manager ID {managerId}.");

            using (var connection = new MySqlConnection(connectionString))
            {
                try
                {
                    await connection.OpenAsync();

                    // 1. Отримуємо дату зміни та ID клубу для даної зміни та менеджера
                    // Цей запит залишається без змін
                    string getShiftInfoQuery = @"
                    SELECT ms.shift_date, m.club_id
                    FROM manager_shifts ms
                    JOIN managers m ON ms.manager_id = m.manager_id
                    WHERE ms.shift_id = @shiftId AND ms.manager_id = @managerId";

                    DateTime? shiftDate = null;
                    int? clubId = null;

                    using (var cmdShiftInfo = new MySqlCommand(getShiftInfoQuery, connection))
                    {
                        cmdShiftInfo.Parameters.AddWithValue("@shiftId", shiftId);
                        cmdShiftInfo.Parameters.AddWithValue("@managerId", managerId);

                        using (var reader = await cmdShiftInfo.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                shiftDate = reader.GetDateTime("shift_date");
                                clubId = reader.GetInt32("club_id");
                            }
                        }
                    }

                    // Якщо зміна або менеджер не знайдені (це аномалія в нормальному флоу)
                    if (!shiftDate.HasValue || !clubId.HasValue)
                    {
                        Console.WriteLine($"[ManagerRepository] Could not find shift_date or club_id for shift ID {shiftId}, manager ID {managerId}. This indicates a data inconsistency.");
                        // В цьому випадку, не можемо виконати перевірку коректно. Краще заблокувати.
                        return true; // Блокуємо закриття, бо не можемо перевірити коректно
                    }

                    // 2. Перевіряємо наявність бронювань згідно з уточненими правилами:
                    //    - status = 'panding' І booking_date = shiftDate
                    //    АБО
                    //    - status = 'new' (без умови на дату)
                    string checkBookingsQuery = @"
                    SELECT COUNT(*)
                    FROM sessions
                    WHERE club_id = @clubId
                      AND (
                           (payment_status = 'panding' AND DATE(session_date) = @shiftDate)
                        OR (payment_status = 'new') -- Нові бронювання на будь-яку дату для цього клубу
                      )";

                    using (var cmdCheckBookings = new MySqlCommand(checkBookingsQuery, connection))
                    {
                        cmdCheckBookings.Parameters.AddWithValue("@clubId", clubId.Value);
                        cmdCheckBookings.Parameters.AddWithValue("@shiftDate", shiftDate.Value.Date); // Передаємо тільки дату зміни для умови 'panding'

                        object result = await cmdCheckBookings.ExecuteScalarAsync();

                        if (result != null && result != DBNull.Value)
                        {
                            long count = Convert.ToInt64(result); // COUNT(*) повертає long
                            Console.WriteLine($"[ManagerRepository] Found {count} blocking bookings for club ID {clubId.Value}.");
                            return count > 0; // Повертаємо true, якщо знайшли хоча б одне бронювання, що відповідає умовам
                        }
                        else
                        {
                            // Якщо COUNT(*) повернув щось неочікуване
                            Console.WriteLine("[ManagerRepository] Unexpected result from COUNT(*) query for blocking bookings.");
                            return true; // Блокуємо закриття у випадку аномалії запиту
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ПОМИЛКА_SQL] CheckForPendingBookingsBeforeShiftCloseAsync (ID: {managerId}, ShiftID: {shiftId}): {ex.Message}");
                    // При будь-якій помилці бази даних під час перевірки, краще заблокувати закриття зміни.
                    return true; // Блокуємо закриття у випадку помилки
                }
            }
        }
        public static async Task<Manager> GetManagerByIdAsync(string connectionString, int managerId)
        {
            Manager manager = null;
            using (var connection = new MySqlConnection(connectionString))
            {
                try
                {
                    await connection.OpenAsync();
                    string query = @"
                        SELECT m.manager_id, m.name, m.phone_number, m.login, m.role, m.club_id, c.name AS club_name
                        FROM managers m
                        INNER JOIN clubs c ON m.club_id = c.club_id
                        WHERE m.manager_id = @managerId";

                    using (var cmd = new MySqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@managerId", managerId);

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                // Читаємо дані з Reader
                                manager = new Manager
                                {
                                    ManagerId = reader.GetInt32("manager_id"),
                                    Name = reader.GetString("name"),
                                    PhoneNumber = reader.GetString("phone_number"),
                                    Login = reader.GetString("login"),
                                    Role = reader.GetString("role"),
                                    ClubId = reader.GetInt32("club_id"),
                                    ClubName = reader.GetString("club_name") // Отримуємо назву клубу
                                };
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ПОМИЛКА_SQL] GetManagerByIdAsync (ID: {managerId}): {ex.Message}");
                    // Логуємо помилку, але не кидаємо виняток вище, повертаємо null
                    return null;
                }
            }
            return manager; // Повертаємо об'єкт менеджера або null, якщо не знайдено/помилка
        }

        // Метод для оновлення імені та номеру телефону менеджера
        public static async Task<bool> UpdateManagerDetailsAsync(string connectionString, int managerId, string newName, string newPhone)
        {
            using (var connection = new MySqlConnection(connectionString))
            {
                try
                {
                    await connection.OpenAsync();
                    string query = @"
                        UPDATE managers
                        SET name = @newName, phone_number = @newPhone
                        WHERE manager_id = @managerId";

                    using (var cmd = new MySqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@newName", newName);
                        cmd.Parameters.AddWithValue("@newPhone", newPhone);
                        cmd.Parameters.AddWithValue("@managerId", managerId);

                        int rowsAffected = await cmd.ExecuteNonQueryAsync();
                        return rowsAffected > 0; // Повертаємо true, якщо хоча б один рядок був оновлений
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ПОМИЛКА_SQL] UpdateManagerDetailsAsync (ID: {managerId}): {ex.Message}");
                    // Логуємо помилку
                    return false; // Повертаємо false у випадку помилки
                }
            }
        }

        // --- НОВІ МЕТОДИ ДЛЯ УПРАВЛІННЯ ЗМІНАМИ ---

        // Метод для отримання статусу поточної відкритої зміни та останніх N змін
        // Повертає Tuple: int? (ID відкритої зміни або null) та List<ShiftHistoryItem> (остання історія)

        // Метод для отримання статусу поточної відкритої зміни та останніх N змін
        // Повертає Tuple: int? (ID відкритої зміни або null) та List<ShiftHistoryItem> (остання історія)
        public static async Task<(int? OpenShiftId, List<ShiftHistoryItem> LastShifts)> GetShiftStatusAndHistoryAsync(string connectionString, int managerId, int limit = 3)
        {
            int? openShiftId = null;
            var history = new List<ShiftHistoryItem>();

            using (var connection = new MySqlConnection(connectionString))
            {
                try
                {
                    await connection.OpenAsync();

                    // 1. Шукаємо відкриту зміну
                    string openShiftQuery = @"
                        SELECT shift_id
                        FROM manager_shifts
                        WHERE manager_id = @managerId AND end_time IS NULL
                        LIMIT 1"; // Обмежуємося одним результатом

                    using (var cmdOpen = new MySqlCommand(openShiftQuery, connection))
                    {
                        cmdOpen.Parameters.AddWithValue("@managerId", managerId);
                        object result = await cmdOpen.ExecuteScalarAsync(); // Отримуємо тільки одне значення (shift_id)

                        if (result != null && result != DBNull.Value)
                        {
                            openShiftId = Convert.ToInt32(result); // Перетворюємо результат на int?
                        }
                    }

                    // 2. Отримуємо останню історію змін
                    string historyQuery = $@"
                        SELECT shift_id, shift_date, start_time, end_time, worked_hours
                        FROM manager_shifts
                        WHERE manager_id = @managerId
                        ORDER BY shift_date DESC, start_time DESC
                        LIMIT {limit}"; // Використовуємо ліміт

                    using (var cmdHistory = new MySqlCommand(historyQuery, connection))
                    {
                        cmdHistory.Parameters.AddWithValue("@managerId", managerId);

                        using (var reader = await cmdHistory.ExecuteReaderAsync())
                        {
                            // Отримуємо індекси стовпців один раз до циклу
                            int endTimeOrdinal = reader.GetOrdinal("end_time");
                            int workedHoursOrdinal = reader.GetOrdinal("worked_hours");
                            int shiftIdOrdinal = reader.GetOrdinal("shift_id"); // Отримуємо всі необхідні індекси
                            int shiftDateOrdinal = reader.GetOrdinal("shift_date");
                            int startTimeOrdinal = reader.GetOrdinal("start_time");


                            while (await reader.ReadAsync())
                            {
                                history.Add(new ShiftHistoryItem
                                {
                                    // Використовуємо індекси для читання даних
                                    ShiftId = reader.GetInt32(shiftIdOrdinal),
                                    ShiftDate = reader.GetDateTime(shiftDateOrdinal).ToString("yyyy-MM-dd"),
                                    StartTime = reader.GetTimeSpan(startTimeOrdinal).ToString(@"hh\:mm\:ss"), // Кома тут потрібна

                                    // ВИПРАВЛЕНО: Використовуємо reader.IsDBNull з індексом стовпця
                                    EndTime = reader.IsDBNull(endTimeOrdinal) ? null : reader.GetTimeSpan(endTimeOrdinal).ToString(@"hh\:mm\:ss"), // Кома тут потрібна

                                    // ВИПРАВЛЕНО: Використовуємо reader.IsDBNull з індексом стовпця
                                    WorkedHours = reader.IsDBNull(workedHoursOrdinal) ? null : (decimal?)reader.GetDecimal(workedHoursOrdinal)
                                });
                            }
                        }
                    }

                    Console.WriteLine($"[ManagerRepository] Loaded shift data for manager ID {managerId}. Open Shift ID: {openShiftId}, History Items: {history?.Count ?? 0}"); // Перевірка на null додана для безпеки

                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ПОМИЛКА_SQL] GetShiftStatusAndHistoryAsync (ID: {managerId}): {ex.Message}");
                    // Логуємо помилку, повертаємо порожній результат з null OpenShiftId
                    return (null, new List<ShiftHistoryItem>());
                }
            }
            return (openShiftId, history); // Повертаємо отримані дані
        }

        // Метод для відкриття нової зміни
        // Повертає ID нової зміни або null, якщо відкриття не вдалося (наприклад, вже є відкрита зміна)
        public static async Task<int?> OpenShiftAsync(string connectionString, int managerId)
        {
            using (var connection = new MySqlConnection(connectionString))
            {
                try
                {
                    await connection.OpenAsync();

                    // Спочатку перевіряємо, чи немає вже відкритої зміни
                    string checkOpenShiftQuery = @"
                        SELECT 1
                        FROM manager_shifts
                        WHERE manager_id = @managerId AND end_time IS NULL
                        LIMIT 1";
                    using (var cmdCheck = new MySqlCommand(checkOpenShiftQuery, connection))
                    {
                        cmdCheck.Parameters.AddWithValue("@managerId", managerId);
                        object existingShift = await cmdCheck.ExecuteScalarAsync();
                        if (existingShift != null)
                        {
                            Console.WriteLine($"[ManagerRepository] Manager ID {managerId} already has an open shift. Cannot open new one.");
                            return null; // Вже є відкрита зміна
                        }
                    }

                    // Вставляємо новий запис зміни
                    string insertQuery = @"
    INSERT INTO manager_shifts (manager_id, shift_date, start_time)
    VALUES (@managerId, CURDATE(), CURTIME());
    SELECT LAST_INSERT_ID();"; // Отримуємо ID щойно вставленого запису

                    using (var cmdInsert = new MySqlCommand(insertQuery, connection))
                    {
                        cmdInsert.Parameters.AddWithValue("@managerId", managerId);

                        object result = await cmdInsert.ExecuteScalarAsync(); // Виконуємо запит і отримуємо LAST_INSERT_ID

                        if (result != null && result != DBNull.Value)
                        {
                            int newShiftId = Convert.ToInt32(result);
                            Console.WriteLine($"[ManagerRepository] Successfully opened shift with ID {newShiftId} for manager ID {managerId}.");
                            return newShiftId; // Повертаємо ID нової зміни
                        }
                        else
                        {
                            Console.WriteLine($"[ПОМИЛКА_SQL] OpenShiftAsync (ID: {managerId}): Failed to retrieve LAST_INSERT_ID after insertion.");
                            return null; // Вставка, можливо, відбулась, але ID не отримано
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ПОМИЛКА_SQL] OpenShiftAsync (ID: {managerId}): {ex.Message}");
                    // Логуємо помилку
                    return null; // Повертаємо null у випадку помилки
                }
            }
        }

        // Метод для закриття відкритої зміни
        // Повертає true, якщо зміна успішно закрита, false - якщо зміна не знайдена, вже закрита або помилка
        public static async Task<bool> CloseShiftAsync(string connectionString, int managerId, int shiftId)
        {
            using (var connection = new MySqlConnection(connectionString))
            {
                try
                {
                    await connection.OpenAsync();

                    // Оновлюємо запис зміни, встановлюючи end_time та розраховуючи worked_hours
                    // Важливо: перевіряємо, що shift_id належить manager_id і що end_time ще NULL
                    string updateQuery = @"
    UPDATE manager_shifts
    SET end_time = CURTIME()
    WHERE shift_id = @shiftId
      AND manager_id = @managerId
      AND end_time IS NULL";

                    using (var cmd = new MySqlCommand(updateQuery, connection))
                    {
                        cmd.Parameters.AddWithValue("@shiftId", shiftId);
                        cmd.Parameters.AddWithValue("@managerId", managerId);

                        int rowsAffected = await cmd.ExecuteNonQueryAsync();

                        if (rowsAffected > 0)
                        {
                            Console.WriteLine($"[ManagerRepository] Successfully closed shift ID {shiftId} for manager ID {managerId}.");
                            return true; // Зміна успішно оновлена
                        }
                        else
                        {
                            Console.WriteLine($"[ManagerRepository] CloseShiftAsync failed. Shift ID {shiftId} not found for manager ID {managerId}, or was already closed.");
                            return false; // Зміна не знайдена, не належить менеджеру, або вже закрита
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ПОМИЛКА_SQL] CloseShiftAsync (ID: {managerId}, ShiftID: {shiftId}): {ex.Message}");
                    // Логуємо помилку
                    return false; // Повертаємо false у випадку помилки
                }
            }
        }
        public static async Task<List<Manager>> GetAllManagersAsync(string connectionString)
        {
            var managers = new List<Manager>();
            using (var connection = new MySqlConnection(connectionString))
            {
                try
                {
                    await connection.OpenAsync();
                    // Включаємо status у запит
                    string query = @"
                        SELECT m.manager_id, m.name, m.phone_number, m.login, m.role, m.club_id, m.status, c.name AS club_name
                        FROM managers m
                        INNER JOIN clubs c ON m.club_id = c.club_id";

                    using (var cmd = new MySqlCommand(query, connection))
                    {
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            // Отримуємо індекси стовпців один раз до циклу
                            int managerIdOrdinal = reader.GetOrdinal("manager_id");
                            int nameOrdinal = reader.GetOrdinal("name");
                            int phoneOrdinal = reader.GetOrdinal("phone_number");
                            int loginOrdinal = reader.GetOrdinal("login"); // Серверна модель може містити логін
                            int roleOrdinal = reader.GetOrdinal("role");
                            int clubIdOrdinal = reader.GetOrdinal("club_id");
                            int statusOrdinal = reader.GetOrdinal("status"); // Читаємо status
                            int clubNameOrdinal = reader.GetOrdinal("club_name"); // Читаємо назву клубу

                            while (await reader.ReadAsync())
                            {
                                managers.Add(new Manager // Використовуємо вашу модель Manager з Models/Manager.cs
                                {
                                    ManagerId = reader.GetInt32(managerIdOrdinal),
                                    Name = reader.GetString(nameOrdinal),
                                    PhoneNumber = reader.GetString(phoneOrdinal),
                                    Login = reader.GetString(loginOrdinal), // Читаємо логін на сервері (не відправляємо клієнту)
                                    Role = reader.GetString(roleOrdinal),
                                    ClubId = reader.GetInt32(clubIdOrdinal),
                                    Status = reader.GetString(statusOrdinal), // Читаємо статус
                                    ClubName = reader.GetString(clubNameOrdinal) // Читаємо назву клубу
                                });
                            }
                        }
                    }
                    Console.WriteLine($"[ManagerRepository] Loaded {managers.Count} managers.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ПОМИЛКА_SQL] GetAllManagersAsync: {ex.Message}");
                    // Логуємо помилку, повертаємо порожній список або кидаємо виняток залежно від логіки обробки помилок
                    return new List<Manager>(); // Повертаємо порожній список при помилці БД
                }
            }
            return managers; // Повертаємо список менеджерів
        }

        // НОВИЙ МЕТОД: Оновити роль менеджера
        public static async Task<bool> UpdateManagerRoleAsync(string connectionString, int managerId, string newRole)
        {
            using (var connection = new MySqlConnection(connectionString))
            {
                try
                {
                    await connection.OpenAsync();
                    string query = @"
                        UPDATE managers
                        SET role = @newRole
                        WHERE manager_id = @managerId";

                    using (var cmd = new MySqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@newRole", newRole);
                        cmd.Parameters.AddWithValue("@managerId", managerId);

                        int rowsAffected = await cmd.ExecuteNonQueryAsync();
                        Console.WriteLine($"[ManagerRepository] UpdateManagerRoleAsync (ID: {managerId}, Role: {newRole}). Rows affected: {rowsAffected}");
                        return rowsAffected > 0; // Повертаємо true, якщо хоча б один рядок був оновлений
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ПОМИЛКА_SQL] UpdateManagerRoleAsync (ID: {managerId}, Role: {newRole}): {ex.Message}");
                    // Логуємо помилку
                    return false; // Повертаємо false у випадку помилки
                }
            }
        }

        // НОВИЙ МЕТОД: Оновити статус менеджера (Hired/Dismissed)
        public static async Task<bool> UpdateManagerStatusAsync(string connectionString, int managerId, string newStatus)
        {
            using (var connection = new MySqlConnection(connectionString))
            {
                try
                {
                    await connection.OpenAsync();
                    string query = @"
                        UPDATE managers
                        SET status = @newStatus
                        WHERE manager_id = @managerId";

                    using (var cmd = new MySqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@newStatus", newStatus);
                        cmd.Parameters.AddWithValue("@managerId", managerId);

                        int rowsAffected = await cmd.ExecuteNonQueryAsync();
                        Console.WriteLine($"[ManagerRepository] UpdateManagerStatusAsync (ID: {managerId}, Status: {newStatus}). Rows affected: {rowsAffected}");
                        return rowsAffected > 0; // Повертаємо true, якщо хоча б один рядок був оновлений
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ПОМИЛКА_SQL] UpdateManagerStatusAsync (ID: {managerId}, Status: {newStatus}): {ex.Message}");
                    // Логуємо помилку
                    return false; // Повертаємо false у випадку помилки
                }
            }
        }
        public static async Task<ManagerDetailsForEdit?> GetManagerDetailsForEditAsync(string connectionString, int managerId)
        {
            Console.WriteLine($"[ManagerRepository] GetManagerDetailsForEditAsync called with ID: {managerId}"); // Лог початку методу
            ManagerDetailsForEdit? manager = null; // Використовуємо Nullable struct або клас

            using (var connection = new MySqlConnection(connectionString))
            {
                try
                {
                    await connection.OpenAsync();
                    // >>> ВИПРАВЛЕНО: Змінено role_id на role у SELECT <<<
                    string query = @"
                        SELECT manager_id, name, phone_number, role, club_id
                        FROM managers
                        WHERE manager_id = @managerId";

                    using (var cmd = new MySqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@managerId", managerId);
                        Console.WriteLine($"[ManagerRepository] Executing query for ID: {managerId}"); // Лог перед виконанням запиту

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                Console.WriteLine($"[ManagerRepository] Manager with ID {managerId} FOUND in DB."); // Лог, якщо знайдено
                                manager = new ManagerDetailsForEdit
                                {
                                    ManagerId = reader.GetInt32("manager_id"),
                                    Name = reader.GetString("name"),
                                    PhoneNumber = reader.GetString("phone_number"),
                                    Role = reader.GetString("role"), // <<< ВИПРАВЛЕНО: Читаємо рядок
                                    ClubId = reader.GetInt32("club_id")
                                };
                            }
                            else
                            {
                                Console.WriteLine($"[ManagerRepository] Manager with ID {managerId} NOT FOUND in DB."); // Лог, якщо не знайдено
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ПОМИЛКА_SQL] GetManagerDetailsForEditAsync (ID: {managerId}): {ex.Message}");
                    // Логуємо помилку
                    return null; // Повертаємо null у випадку помилки або якщо не знайдено
                }
            }
            return manager;
        }
        // НОВИЙ МЕТОД: Отримання всіх доступних ролей
        // Припускаємо, що існує таблиця 'roles'
        public static async Task<List<string>> GetAllUniqueRoleNamesAsync(string connectionString)
        {
            var roles = new List<string>();
            using (var connection = new MySqlConnection(connectionString))
            {
                try
                {
                    await connection.OpenAsync();
                    // Вибираємо всі унікальні значення з колонки 'role', ігноруємо null та порожні рядки, сортуємо
                    string query = "SELECT DISTINCT role FROM managers WHERE role IS NOT NULL AND role != '' ORDER BY role";

                    using (var cmd = new MySqlCommand(query, connection))
                    {
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                roles.Add(reader.GetString("role")); // Читаємо рядок ролі
                            }
                        }
                    }
                    Console.WriteLine($"[ManagerRepository] Loaded {roles.Count} unique roles from managers table.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ПОМИЛКА_SQL] GetAllUniqueRoleNamesAsync: {ex.Message}");
                    return new List<string>();
                }
            }
            return roles;
        }
        // НОВИЙ МЕТОД: Оновлення всіх редагованих деталей менеджера
        // Припускаємо, що в таблиці 'managers' є колонки name, phone_number, role_id, club_id
        public static async Task<bool> UpdateManagerFullDetailsAsync(string connectionString, int managerId, string newName, string newPhone, string newRole, int newClubId)
        {
            using (var connection = new MySqlConnection(connectionString))
            {
                try
                {
                    await connection.OpenAsync();
                    // >>> ВИПРАВЛЕНО: Оновлюємо колонку 'role', не 'role_id' <<<
                    string query = @"
                        UPDATE managers
                        SET name = @newName, phone_number = @newPhone, role = @newRole, club_id = @newClubId
                        WHERE manager_id = @managerId";

                    using (var cmd = new MySqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@newName", newName);
                        cmd.Parameters.AddWithValue("@newPhone", newPhone);
                        cmd.Parameters.AddWithValue("@newRole", newRole); // Прив'язуємо рядок
                        cmd.Parameters.AddWithValue("@newClubId", newClubId);
                        cmd.Parameters.AddWithValue("@managerId", managerId);

                        int rowsAffected = await cmd.ExecuteNonQueryAsync();
                        Console.WriteLine($"[ManagerRepository] UpdateManagerFullDetailsAsync (ID: {managerId}). Rows affected: {rowsAffected}");
                        return rowsAffected > 0; // Повертаємо true, якщо хоча б один рядок був оновлений
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ПОМИЛКА_SQL] UpdateManagerFullDetailsAsync (ID: {managerId}): {ex.Message}");
                    // Логуємо помилку
                    return false; // Повертаємо false у випадку помилки
                }
            }
        }
        // ... (існуючі методи, включаючи GetAllManagersAsync, UpdateManagerRoleAsync, UpdateManagerStatusAsync) ...

        // Додаємо внутрішню структуру для повернення деталей менеджера для редагування
        // Це краще, ніж повертати анонімний тип або змінювати основну модель Manager
        public struct ManagerDetailsForEdit
        {
            public int ManagerId { get; set; }
            public string Name { get; set; }
            public string PhoneNumber { get; set; }
            public string Role { get; set; } // <<< Змінено на string
            public int ClubId { get; set; }
        }
        public static async Task<int> GetNewSessionsCountAsync(string connectionString, int clubId)
        {
            Console.WriteLine($"[ManagerRepository] Getting new sessions count for club ID {clubId}.");

            using (var connection = new MySqlConnection(connectionString))
            {
                try
                {
                    await connection.OpenAsync();

                    // Припускаємо, що сеанси (бронювання) зберігаються в таблиці bookings
                    // і мають стовпці club_id та status.
                    string countQuery = @"
                    SELECT COUNT(*)
                    FROM sessions
                    WHERE club_id = @clubId AND payment_status = 'new'";

                    using (var cmd = new MySqlCommand(countQuery, connection))
                    {
                        cmd.Parameters.AddWithValue("@clubId", clubId);

                        object result = await cmd.ExecuteScalarAsync();

                        if (result != null && result != DBNull.Value)
                        {
                            // COUNT(*) повертає long в MySQL connector, потрібно конвертувати
                            int count = Convert.ToInt32(Convert.ToInt64(result));
                            Console.WriteLine($"[ManagerRepository] Found {count} new sessions for club ID {clubId}.");
                            return count;
                        }
                        else
                        {
                            Console.WriteLine("[ManagerRepository] Unexpected result from COUNT(*) query for new sessions.");
                            return 0; // Якщо результат DBNull або null, припускаємо 0
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ПОМИЛКА_SQL] GetNewSessionsCountAsync (Club ID: {clubId}): {ex.Message}");
                    // Логуємо помилку
                    return -1; // Повертаємо -1 для індикації помилки на рівні репозиторію
                }
            }
        }
            // Додаємо модель ShiftHistoryItem сюди, або у файл Models/ShiftHistoryItem.cs
            // Я додам сюди для зручності, якщо у вас немає окремої папки Models.
            // Якщо є папка Models, краще створити там файл ShiftHistoryItem.cs і використовувати using.
            // Враховуючи, що у вас вже є Models/Manager.cs, створимо Models/ShiftHistoryItem.cs
        }
    // TODO: Можливо, знадобиться метод для оновлення статусу "Вийти на роботу" в таблиці manager_shifts згодом.
    // Наприклад: AddOrUpdateManagerShiftAsync(int managerId, DateTime shiftDate, TimeSpan startTime)
}