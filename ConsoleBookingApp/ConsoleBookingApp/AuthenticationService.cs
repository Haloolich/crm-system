using MySqlConnector;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json; // Додано для серіалізації списку клубів

namespace ConsoleBookingApp
{
    public static class AuthenticationService // Зробив клас статичним
    {
        // ... Ваш HandleLogin метод залишається без змін або з незначними виправленнями, які ми обговорювали раніше (NULL club_id, status) ...
        // Наприклад, перевірка статусу і обробка club_id = NULL при логіні вже додана в попередній відповіді.

        // НОВИЙ метод для отримання списку клубів
        public static async Task HandleLogin(NetworkStream stream, string connectionString, Dictionary<string, object> data) // Або Dictionary<string, string> якщо саме так приходить від клієнта
        {
            if (!data.ContainsKey("login") || !data.ContainsKey("password"))
            {
                await ResponseHelper.SendErrorResponse(stream, "Необхідно вказати логін і пароль.");
                return;
            }

            // Безпечний доступ до даних
            if (!(data["login"] is string login) || !(data["password"] is string password))
            {
                await ResponseHelper.SendErrorResponse(stream, "Некоректний формат даних логіну або пароля.");
                return;
            }

            // ... ваш код логіну з використанням login та password ...

            // Приклад використання login та password
            Console.WriteLine($"[AuthService] Handling login for user: {login}");

            try
            {
                using (MySqlConnection connection = new MySqlConnection(connectionString))
                {
                    await connection.OpenAsync();

                    // УВАГА: Тут має бути перевірка хешу пароля!
                    string query = "SELECT manager_id, club_id, role, status FROM managers WHERE login = @login AND password_hash = @passwordHash"; // ВИКОРИСТОВУЙТЕ password_hash

                    using (MySqlCommand command = new MySqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@login", login);
                        // command.Parameters.AddWithValue("@passwordHash", HashPassword(password)); // Хешуйте вхідний пароль!
                        command.Parameters.AddWithValue("@passwordHash", password); // Тимчасово, для роботи з поточним небезпечним кодом

                        using (MySqlDataReader reader = await command.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                int managerId = reader.GetInt32("manager_id");
                                int clubIdIndex = reader.GetOrdinal("club_id");
                                int? clubId = reader.IsDBNull(clubIdIndex) ? (int?)null : reader.GetInt32(clubIdIndex);
                                string userRole = reader.GetString("role");
                                string status = reader.GetString("status");

                                if (status == "inactive")
                                {
                                    Console.WriteLine($"[AuthService] Login failed for user: {login} - Account inactive.");
                                    await ResponseHelper.SendErrorResponse(stream, "Ваш акаунт неактивний.");
                                    return;
                                }

                                Console.WriteLine($"[AuthService] Login successful for user: {login}, Manager ID: {managerId}, Club ID: {clubId}, Role: {userRole}");

                                Dictionary<string, object> responseData = new Dictionary<string, object>
                                 {
                                     { "success", "true" },
                                     { "message", "Авторизація успішна!" },
                                     { "manager_id", managerId },
                                     { "club_id", clubId },
                                      { "role", userRole },
                                      { "status", status }
                                 };

                                await ResponseHelper.SendJsonResponse(stream, responseData);
                            }
                            else
                            {
                                Console.WriteLine($"[AuthService] Login failed for user: {login} - Invalid credentials.");
                                await ResponseHelper.SendErrorResponse(stream, "Невірний логін або пароль.");
                            }
                        }
                    }
                }
            }
            catch (MySqlException ex)
            {
                Console.WriteLine($"[AuthService] SQL Error during login for {login}: {ex.Message}");
                await ResponseHelper.SendErrorResponse(stream, $"Помилка бази даних при авторизації.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AuthService] General Error during login for {login}: {ex.Message}");
                await ResponseHelper.SendErrorResponse(stream, $"Внутрішня помилка сервера при авторизації.");
            }

        }
        public static async Task HandleGetClubs(NetworkStream stream, string connectionString)
        {
            Console.WriteLine("[AuthService] Handling get_clubs request.");
            try
            {
                using (MySqlConnection connection = new MySqlConnection(connectionString))
                {
                    await connection.OpenAsync();

                    // Вибираємо тільки club_id та name для активних клубів
                    string query = "SELECT club_id, name FROM clubs WHERE status = 'Open'"; // Припускаємо, що є поле status в таблиці clubs

                    using (MySqlCommand command = new MySqlCommand(query, connection))
                    {
                        using (MySqlDataReader reader = await command.ExecuteReaderAsync())
                        {
                            List<Dictionary<string, object>> clubsList = new List<Dictionary<string, object>>();
                            while (await reader.ReadAsync())
                            {
                                clubsList.Add(new Dictionary<string, object>
                                {
                                    { "club_id", reader.GetInt32("club_id") },
                                    { "name", reader.GetString("name") }
                                });
                            }

                            Console.WriteLine($"[AuthService] Found {clubsList.Count} active clubs.");

                            // --- ВИПРАВЛЕННЯ ТУТ ---
                            // Обгортаємо список клубів в об'єкт успішної відповіді
                            var successResponse = new Dictionary<string, object>
                            {
                                { "success", "true" }, // Статус успіху
                                { "clubs", clubsList } // Список клубів під ключем "clubs"
                                // Можна додати { "message", "Список клубів отримано" } за бажанням
                            };

                            await ResponseHelper.SendJsonResponse(stream, successResponse); // Тепер передаємо Dictionary<string, object>

                        }
                    }
                }
            }
            catch (MySqlException ex)
            {
                Console.WriteLine($"[AuthService] SQL Error getting clubs: {ex.Message}");
                // Відправляємо помилку клієнту у стандартному форматі
                await ResponseHelper.SendErrorResponse(stream, "Помилка бази даних при отриманні списку клубів.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AuthService] General Error getting clubs: {ex.Message}");
                // Відправляємо помилку клієнту у стандартному форматі
                await ResponseHelper.SendErrorResponse(stream, "Внутрішня помилка сервера при отриманні списку клубів.");
            }
        }

        // Модифікований HandleRegistration метод
        public static async Task HandleRegistration(NetworkStream stream, string connectionString, Dictionary<string, object> data)
        {
            Console.WriteLine($"[AuthService] Handling registration.");

            // --- Безпечне отримання даних з Dictionary<string, object> ---
            // Перевіряємо наявність ключів та їх тип
            if (!data.TryGetValue("name", out object nameObj) || !(nameObj is string name) || string.IsNullOrWhiteSpace(name) ||
                !data.TryGetValue("phone", out object phoneObj) || !(phoneObj is string phone) || string.IsNullOrWhiteSpace(phone) ||
                !data.TryGetValue("login", out object loginObj) || !(loginObj is string login) || string.IsNullOrWhiteSpace(login) ||
                !data.TryGetValue("password", out object passwordObj) || !(passwordObj is string password) || string.IsNullOrWhiteSpace(password) ||
                !data.TryGetValue("club_id", out object clubIdObj)) // club_id може прийти як число (long/int) або рядок, або бути відсутнім
            {
                Console.WriteLine($"[AuthService] Registration failed: Missing or invalid required fields.");
                await ResponseHelper.SendErrorResponse(stream, "Необхідно вказати ім'я, телефон, логін, пароль і клуб. Перевірте формат даних.");
                return;
            }

            // Безпечне перетворення club_id на int
            int clubId;
            if (clubIdObj is long clubIdLong) // JSON парсить числа як long за замовчуванням
            {
                clubId = (int)clubIdLong;
            }
            else if (clubIdObj is int clubIdInt) // Може бути вже int
            {
                clubId = clubIdInt;
            }
            else // Спроба парсингу з рядка або інший тип
            {
                // Можна спробувати розпарсити як рядок, якщо це очікувано
                if (clubIdObj is string clubIdString && int.TryParse(clubIdString, out int parsedClubId))
                {
                    clubId = parsedClubId;
                }
                else
                {
                    Console.WriteLine($"[AuthService] Registration failed: Invalid type for club_id: {clubIdObj?.GetType().Name ?? "NULL"}");
                    await ResponseHelper.SendErrorResponse(stream, "Некоректний формат ID клубу.");
                    return;
                }
            }

            // Додаткова валідація для clubId
            if (clubId <= 0)
            {
                Console.WriteLine($"[AuthService] Registration failed: Invalid club_id value: {clubId}.");
                await ResponseHelper.SendErrorResponse(stream, "Некоректне значення ID клубу.");
                return;
            }

            Console.WriteLine($"[AuthService] Handling registration for login: {login}, phone: {phone}, club_id: {clubId}");

            // --- ДОДАТИ ХЕШУВАННЯ ПАРОЛЮ ТУТ ---
            // string hashedPassword = BCrypt.Net.BCrypt.HashPassword(password);
            string passwordToStore = password; // <= !!! ЦЕ НЕБЕЗПЕЧНО! Використовуйте hashedPassword !!!

            try
            {
                using (MySqlConnection connection = new MySqlConnection(connectionString))
                {
                    await connection.OpenAsync();

                    // --- ДОДАТИ ВАЛІДАЦІЮ CLUB_ID ТУТ ---
                    // Перевірити, чи існує клуб з таким clubId і чи він активний
                    string checkClubQuery = "SELECT COUNT(*) FROM clubs WHERE club_id = @clubId AND status = 'Open'"; // Припускаємо, що є поле status в таблиці clubs
                    using (MySqlCommand checkClubCommand = new MySqlCommand(checkClubQuery, connection))
                    {
                        checkClubCommand.Parameters.AddWithValue("@clubId", clubId);
                        long clubCount = (long)await checkClubCommand.ExecuteScalarAsync();
                        if (clubCount == 0)
                        {
                            Console.WriteLine($"[AuthService] Registration failed for {login}: Invalid or inactive club_id {clubId}.");
                            await ResponseHelper.SendErrorResponse(stream, "Обраний клуб не існує або неактивний.");
                            return;
                        }
                    }


                    // Перевірка, чи існує вже такий логін або телефон
                    string checkUserQuery = "SELECT COUNT(*) FROM managers WHERE login = @login OR phone_number = @phone";
                    using (MySqlCommand checkUserCommand = new MySqlCommand(checkUserQuery, connection))
                    {
                        checkUserCommand.Parameters.AddWithValue("@login", login);
                        checkUserCommand.Parameters.AddWithValue("@phone", phone);
                        long existingCount = (long)await checkUserCommand.ExecuteScalarAsync();
                        if (existingCount > 0)
                        {
                            Console.WriteLine($"[AuthService] Registration failed for {login}: Login or phone already exists.");
                            await ResponseHelper.SendErrorResponse(stream, "Логін або номер телефону вже зареєстровано.");
                            return;
                        }
                    }

                    // Додавання нового менеджера
                    string query = @"INSERT INTO managers
                               (name, phone_number, login, password_hash, club_id, role, status)
                               VALUES (@name, @phone, @login, @password, @clubId, @role, @status)";

                    using (MySqlCommand command = new MySqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@name", name);
                        command.Parameters.AddWithValue("@phone", phone);
                        command.Parameters.AddWithValue("@login", login);
                        command.Parameters.AddWithValue("@password", passwordToStore); // <= !!! ВИКОРИСТОВУЙТЕ ТУТ ХЕШ !!!
                        command.Parameters.AddWithValue("@clubId", clubId);
                        command.Parameters.AddWithValue("@role", "менеджер");      // role за замовчуванням 'менеджер'
                        command.Parameters.AddWithValue("@status", "active");     // status за замовчуванням 'active'

                        int rowsAffected = await command.ExecuteNonQueryAsync();
                        if (rowsAffected > 0)
                        {
                            Console.WriteLine($"[AuthService] Registration successful for login: {login}");
                            await ResponseHelper.SendSuccessResponse(stream, "Реєстрація успішна! Ваш акаунт очікує активації адміністратором.");
                        }
                        else
                        {
                            Console.WriteLine($"[AuthService] Registration failed for {login}: Insert query affected 0 rows.");
                            await ResponseHelper.SendErrorResponse(stream, "Помилка при реєстрації.");
                        }
                    }
                }
            }
            catch (MySqlException ex)
            {
                Console.WriteLine($"[AuthService] SQL Error during registration for {login}: {ex.Message}");
                if (ex.Number == 1062) // Duplicate entry
                {
                    await ResponseHelper.SendErrorResponse(stream, "Логін або номер телефону вже зареєстровано.");
                }
                else
                {
                    await ResponseHelper.SendErrorResponse(stream, "Помилка бази даних при реєстрації.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AuthService] General Error during registration for {login}: {ex.Message}");
                await ResponseHelper.SendErrorResponse(stream, "Внутрішня помилка сервера при реєстрації.");
            }
        }
        // ... Припускається існування класу ResponseHelper ...
        /*
         public static class ResponseHelper
         {
             public static async Task SendJsonResponse(NetworkStream stream, object data) // Змінено на object для гнучкості
             {
                 try
                 {
                     string json = JsonConvert.SerializeObject(data);
                     Console.WriteLine($"Sending JSON response: {json}"); // Для налагодження
                     byte[] buffer = Encoding.UTF8.GetBytes(json);
                     byte[] lengthBytes = BitConverter.GetBytes(buffer.Length);

                     await stream.WriteAsync(lengthBytes, 0, lengthBytes.Length);
                     await stream.WriteAsync(buffer, 0, buffer.Length);
                     await stream.FlushAsync();
                 }
                 catch (Exception ex)
                 {
                     Console.WriteLine($"Error sending JSON response: {ex.Message}");
                     // Обробка помилки відправки
                     // Тут може знадобитися закрити з'єднання, якщо сталася помилка запису
                     try { stream?.Close(); } catch { }
                 }
             }

             public static async Task SendSuccessResponse(NetworkStream stream, string message)
             {
                 var response = new Dictionary<string, object> // Використовуємо object для сумісності
                 {
                     { "success", "true" },
                     { "message", message }
                 };
                 await SendJsonResponse(stream, response);
             }

             public static async Task SendErrorResponse(NetworkStream stream, string message)
             {
                 var response = new Dictionary<string, object> // Використовуємо object для сумісності
                 {
                     { "success", "false" },
                     { "message", message }
                 };
                 await SendJsonResponse(stream, response);
             }
         }
        */
    }
}