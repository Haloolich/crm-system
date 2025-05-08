// ClubRepository.cs
using MySqlConnector;
using System;
using System.Threading.Tasks;
using ConsoleBookingApp.Models; // Використовуємо модель Club
using System.Collections.Generic; // Для Dictionary
using System.Data.Common;

namespace ConsoleBookingApp.Repositories
{
    public class ClubRepository
    {
        private readonly string _connectionString;

        public ClubRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        /// <summary>
        /// Додає новий клуб до бази даних.
        /// </summary>
        /// <param name="clubData">Словник з даними клубу.</param>
        /// <param name="errorMessage">Вихідний параметр для повідомлення про помилку.</param>
        /// <returns>true, якщо клуб успішно додано; false, якщо сталася помилка.</returns>
        public static async Task<List<Club>> GetAllClubsAsyncEdit(string connectionString)
        {
            var clubs = new List<Club>();
            using (var connection = new MySqlConnection(connectionString))
            {
                try
                {
                    await connection.OpenAsync();
                    string query = "SELECT club_id, name FROM clubs ORDER BY name";

                    using (var cmd = new MySqlCommand(query, connection))
                    {
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                clubs.Add(new Club
                                {
                                    ClubId = reader.GetInt32("club_id"),
                                    Name = reader.GetString("name")
                                });
                            }
                        }
                    }
                    Console.WriteLine($"[ClubRepository] Loaded {clubs.Count} clubs.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ПОМИЛКА_SQL] GetAllClubsAsync: {ex.Message}");
                    return new List<Club>();
                }
            }
            return clubs;
        }
public async Task<(bool Success, string ErrorMessage)> AddClubAsync(Dictionary<string, object> clubData)
        {
            // Визначення SQL-запиту для вставки
            // Використовуємо параметри для безпеки
            string sql = @"
                INSERT INTO clubs (name, address, phone_number, email, max_ps_zones, max_vr_quest_zones, created_at)
                VALUES (@name, @address, @phone_number, @email, @max_ps_zones, @max_vr_quest_zones, NOW());
            ";

            using (var connection = new MySqlConnection(_connectionString))
            {
                try
                {
                    await connection.OpenAsync();
                    using (var command = new MySqlCommand(sql, connection))
                    {
                        // Додаємо параметри.
                        // Використовуємо Value.ToString() для рядкових полів,
                        // явне приведення або Convert для числових, обробляємо null
                        command.Parameters.AddWithValue("@name", GetValueOrDefault<string>(clubData, "name", string.Empty));
                        command.Parameters.AddWithValue("@address", GetValueOrDefault<string>(clubData, "address", string.Empty));
                        command.Parameters.AddWithValue("@phone_number", GetValueOrDefault<string>(clubData, "phone_number", string.Empty));
                        command.Parameters.AddWithValue("@email", GetValueOrDefault<string>(clubData, "email", string.Empty));

                        // Для числових полів потрібна обробка null та перетворення типу
                        command.Parameters.AddWithValue("@max_ps_zones", GetValueOrDefault<int>(clubData, "max_ps_zones", 0));
                        command.Parameters.AddWithValue("@max_vr_quest_zones", GetValueOrDefault<int>(clubData, "max_vr_quest_zones", 0));

                        Console.WriteLine("[ClubRepository] Executing INSERT command...");
                        int rowsAffected = await command.ExecuteNonQueryAsync();
                        Console.WriteLine($"[ClubRepository] INSERT command executed. Rows affected: {rowsAffected}");

                        if (rowsAffected > 0)
                        {
                            return (true, null); // Успішно додано
                        }
                        else
                        {
                            // Теоретично не повинно статися при INSERT одного рядка, але перевірка не зашкодить
                            return (false, "Не вдалося додати клуб до бази даних.");
                        }
                    }
                }
                catch (MySqlException mysqlEx)
                {
                    Console.WriteLine($"[ClubRepository] MySQL Error: {mysqlEx.Message}\n{mysqlEx.StackTrace}");
                    // Обробка специфічних помилок MySQL (наприклад, дублікат ключа)
                    if (mysqlEx.Number == 1062) // Код помилки для Duplicate entry
                    {
                        return (false, "Клуб з такою назвою або іншими унікальними даними вже існує.");
                    }
                    return (false, $"Помилка бази даних: {mysqlEx.Message}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ClubRepository] General Error: {ex.Message}\n{ex.StackTrace}");
                    return (false, $"Внутрішня помилка сервера при збереженні клубу: {ex.Message}");
                }
            }
        }
        // <--- ДОДАНО МЕТОД: Отримати всі клуби ---
        /// <summary>
        /// Отримує список усіх клубів з бази даних.
        /// </summary>
        /// <returns>Список об'єктів Club або порожній список у разі помилки/відсутності даних.</returns>
        public async Task<List<Club>> GetAllClubsAsync()
        {
            var clubs = new List<Club>();
            string sql = @"
                SELECT club_id, name, address, phone_number, email, max_ps_zones, max_vr_quest_zones, status
                FROM clubs;
            ";

            using (var connection = new MySqlConnection(_connectionString))
            {
                try
                {
                    await connection.OpenAsync();
                    using (var command = new MySqlCommand(sql, connection))
                    {
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                clubs.Add(MapClubFromReader(reader));
                            }
                        }
                    }
                    Console.WriteLine($"[ClubRepository] Found {clubs.Count} clubs.");
                    return clubs;
                }
                catch (MySqlException mysqlEx)
                {
                    Console.WriteLine($"[ClubRepository] MySQL Error getting all clubs: {mysqlEx.Message}\n{mysqlEx.StackTrace}");
                    // В реальному додатку, можливо, варто логувати помилку краще або повертати статус помилки
                    return new List<Club>(); // Повертаємо порожній список при помилці
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ClubRepository] General Error getting all clubs: {ex.Message}\n{ex.StackTrace}");
                    return new List<Club>();
                }
            }
        }
        // ---------------------------------------------

        // <--- ДОДАНО МЕТОД: Отримати один клуб за ID ---
        /// <summary>
        /// Отримує один клуб за його ID.
        /// </summary>
        /// <param name="clubId">ID клубу.</param>
        /// <returns>Об'єкт Club або null, якщо клуб не знайдено.</returns>
        public async Task<Club> GetClubByIdAsync(int clubId)
        {
            string sql = @"
                SELECT club_id, name, address, phone_number, email, max_ps_zones, max_vr_quest_zones, status
                FROM clubs
                WHERE club_id = @clubId;
            ";

            using (var connection = new MySqlConnection(_connectionString))
            {
                try
                {
                    await connection.OpenAsync();
                    using (var command = new MySqlCommand(sql, connection))
                    {
                        command.Parameters.AddWithValue("@clubId", clubId);

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                var club = MapClubFromReader(reader);
                                Console.WriteLine($"[ClubRepository] Found club with ID {clubId}.");
                                return club;
                            }
                            else
                            {
                                Console.WriteLine($"[ClubRepository] Club with ID {clubId} not found.");
                                return null; // Клуб не знайдено
                            }
                        }
                    }
                }
                catch (MySqlException mysqlEx)
                {
                    Console.WriteLine($"[ClubRepository] MySQL Error getting club by ID {clubId}: {mysqlEx.Message}\n{mysqlEx.StackTrace}");
                    return null;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ClubRepository] General Error getting club by ID {clubId}: {ex.Message}\n{ex.StackTrace}");
                    return null;
                }
            }
        }
        // ---------------------------------------------

        // <--- ДОДАНО МЕТОД: Видалити клуб за ID ---
        /// <summary>
        /// Видаляє клуб за його ID.
        /// </summary>
        /// <param name="clubId">ID клубу.</param>
        /// <returns>true, якщо клуб успішно видалено; false, якщо клуб не знайдено або сталася помилка.</returns>
        public async Task<(bool Success, string ErrorMessage)> DeleteClubAsync(int clubId)
        {
            string sql = @"
                DELETE FROM clubs
                WHERE club_id = @clubId;
            ";

            using (var connection = new MySqlConnection(_connectionString))
            {
                try
                {
                    await connection.OpenAsync();
                    using (var command = new MySqlCommand(sql, connection))
                    {
                        command.Parameters.AddWithValue("@clubId", clubId);

                        Console.WriteLine($"[ClubRepository] Executing DELETE command for club ID {clubId}...");
                        int rowsAffected = await command.ExecuteNonQueryAsync();
                        Console.WriteLine($"[ClubRepository] DELETE command executed for club ID {clubId}. Rows affected: {rowsAffected}");

                        if (rowsAffected > 0)
                        {
                            return (true, null); // Успішно видалено
                        }
                        else
                        {
                            return (false, $"Клуб з ID {clubId} не знайдено для видалення.");
                        }
                    }
                }
                catch (MySqlException mysqlEx)
                {
                    Console.WriteLine($"[ClubRepository] MySQL Error deleting club ID {clubId}: {mysqlEx.Message}\n{mysqlEx.StackTrace}");
                    // TODO: Обробка помилок зовнішнього ключа, якщо є залежні записи (напр., бронювання)
                    // if (mysqlEx.Number == 1451) // Код помилки для Foreign key constraint fails
                    // {
                    //     return (false, "Неможливо видалити клуб, оскільки існують пов'язані записи (наприклад, бронювання).");
                    // }
                    return (false, $"Помилка бази даних при видаленні клубу: {mysqlEx.Message}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ClubRepository] General Error deleting club ID {clubId}: {ex.Message}\n{ex.StackTrace}");
                    return (false, $"Внутрішня помилка сервера при видаленні клубу: {ex.Message}");
                }
            }
        }
        // ---------------------------------------------

        // <--- ДОДАНО МЕТОД: Оновити дані клубу ---
        /// <summary>
        /// Оновлює дані існуючого клубу.
        /// </summary>
        /// <param name="clubId">ID клубу для оновлення.</param>
        /// <param name="clubData">Словник з даними для оновлення (ключ -> нове значення).</param>
        /// <returns>true, якщо клуб успішно оновлено; false, якщо клуб не знайдено або сталася помилка.</returns>
        public async Task<(bool Success, string ErrorMessage)> UpdateClubAsync(int clubId, Dictionary<string, object> clubData)
        {
            string sql = @"
                UPDATE clubs
                SET
                    name = @name,
                    address = @address,
                    phone_number = @phone_number,
                    email = @email,
                    max_ps_zones = @max_ps_zones,
                    max_vr_quest_zones = @max_vr_quest_zones,
                    status = @status
                WHERE club_id = @clubId;
            ";

            using (var connection = new MySqlConnection(_connectionString))
            {
                try
                {
                    await connection.OpenAsync();
                    using (var command = new MySqlCommand(sql, connection))
                    {
                        // Використовуємо GetValueOrDefault для безпечного отримання даних зі словника
                        command.Parameters.AddWithValue("@clubId", clubId);
                        command.Parameters.AddWithValue("@name", GetValueOrDefault<string>(clubData, "name", string.Empty));
                        command.Parameters.AddWithValue("@address", GetValueOrDefault<string>(clubData, "address", string.Empty));
                        command.Parameters.AddWithValue("@phone_number", GetValueOrDefault<string>(clubData, "phone_number", string.Empty));
                        command.Parameters.AddWithValue("@email", GetValueOrDefault<string>(clubData, "email", string.Empty));
                        command.Parameters.AddWithValue("@max_ps_zones", GetValueOrDefault<int>(clubData, "max_ps_zones", 0));
                        command.Parameters.AddWithValue("@max_vr_quest_zones", GetValueOrDefault<int>(clubData, "max_vr_quest_zones", 0));
                        command.Parameters.AddWithValue("@status", GetValueOrDefault<string>(clubData, "status", "Відкритий")); // Додано статус

                        Console.WriteLine($"[ClubRepository] Executing UPDATE command for club ID {clubId}...");
                        int rowsAffected = await command.ExecuteNonQueryAsync();
                        Console.WriteLine($"[ClubRepository] UPDATE command executed for club ID {clubId}. Rows affected: {rowsAffected}");

                        if (rowsAffected > 0)
                        {
                            return (true, null); // Успішно оновлено
                        }
                        else
                        {
                            // Це може означати, що клуб не знайдено за ID
                            return (false, $"Клуб з ID {clubId} не знайдено для оновлення, або дані не змінилися.");
                        }
                    }
                }
                catch (MySqlException mysqlEx)
                {
                    Console.WriteLine($"[ClubRepository] MySQL Error updating club ID {clubId}: {mysqlEx.Message}\n{mysqlEx.StackTrace}");
                    // TODO: Обробка специфічних помилок MySQL (наприклад, дублікат ключа)
                    if (mysqlEx.Number == 1062) // Код помилки для Duplicate entry
                    {
                        return (false, "Не вдалося оновити клуб: така назва або інші унікальні дані вже існують.");
                    }
                    return (false, $"Помилка бази даних при оновленні клубу: {mysqlEx.Message}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ClubRepository] General Error updating club ID {clubId}: {ex.Message}\n{ex.StackTrace}");
                    return (false, $"Внутрішня помилка сервера при оновленні клубу: {ex.Message}");
                }
            }
        }
        // ---------------------------------------------

        // <--- ДОДАНО ДОПОМІЖНИЙ МЕТОД: Мапування даних з читача БД в об'єкт Club ---
        // ClubRepository.cs
        private Club MapClubFromReader(DbDataReader reader)
        {
            // Отримайте порядкові номери стовпців один раз
            int clubIdOrdinal = reader.GetOrdinal("club_id");
            int nameOrdinal = reader.GetOrdinal("name");
            int addressOrdinal = reader.GetOrdinal("address");
            int phoneOrdinal = reader.GetOrdinal("phone_number");
            int emailOrdinal = reader.GetOrdinal("email");
            int maxPsOrdinal = reader.GetOrdinal("max_ps_zones");
            int maxVrOrdinal = reader.GetOrdinal("max_vr_quest_zones");
            int statusOrdinal = reader.GetOrdinal("status");


            return new Club
            {
                // --- ЗАМІНІТЬ ВСЕ МАПУВАННЯ НА ЦЕ:
                ClubId = Convert.ToInt32(reader.GetValue(clubIdOrdinal)), // <-- ЗМІНА ТУТ, використовуємо GetValue
                Name = reader.GetString(nameOrdinal),
                Address = reader.GetString(addressOrdinal),
                PhoneNumber = reader.GetString(phoneOrdinal),
                Email = reader.GetString(emailOrdinal),
                MaxPsZones = reader.GetInt32(maxPsOrdinal),
                MaxVrQuestZones = reader.GetInt32(maxVrOrdinal),
                Status = reader.GetString(statusOrdinal)
            };
        }
        // Допоміжний метод для безпечного отримання значення з Dictionary<string, object>
        // з перетворенням типу та значенням за замовчуванням
        private T GetValueOrDefault<T>(Dictionary<string, object> data, string key, T defaultValue)
        {
            if (data.TryGetValue(key, out object valueObj) && valueObj != null)
            {
                try
                {
                    // Використовуємо Convert.ChangeType для спроби перетворення
                    return (T)Convert.ChangeType(valueObj, typeof(T), System.Globalization.CultureInfo.InvariantCulture);
                }
                catch (InvalidCastException)
                {
                    Console.WriteLine($"[ClubRepository] Failed to cast value for key '{key}' (type {valueObj.GetType().Name}) to {typeof(T).Name}. Returning default.");
                    return defaultValue; // Не вдалося перетворити, повертаємо значення за замовчуванням
                }
                catch (FormatException)
                {
                    Console.WriteLine($"[ClubRepository] Failed to format value for key '{key}' ('{valueObj}') to {typeof(T).Name}. Returning default.");
                    return defaultValue; // Невірний формат
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ClubRepository] Unexpected error getting value for key '{key}': {ex.Message}");
                    return defaultValue; // Інша помилка, повертаємо значення за замовчуванням
                }
            }
            Console.WriteLine($"[ClubRepository] Key '{key}' not found or value is null. Returning default.");
            return defaultValue; // Ключ відсутній або значення null
        }
    }
}