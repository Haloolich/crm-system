// ManagerService.cs
using ConsoleBookingApp.Data; // Використовуємо ManagerRepository
using ConsoleBookingApp.Models; // Використовуємо моделі Manager та Club
using ConsoleBookingApp.Repositories;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleBookingApp
{
    public static class ManagerService // Зробимо його static
    {
        // Обробка запиту на отримання даних менеджера
        public static async Task HandleGetAccountData(NetworkStream stream, string connectionString, Dictionary<string, string> requestData)
        {
            Console.WriteLine("[ManagerService] Handling GetAccountData request.");

            // 1. Валідація вхідних даних
            if (!requestData.TryGetValue("manager_id", out string managerIdStr) || !int.TryParse(managerIdStr, out int managerId))
            {
                Console.WriteLine("[ManagerService] Missing or invalid manager_id in GetAccountData request.");
                await ResponseHelper.SendErrorResponse(stream, "Невірний формат запиту: відсутній або некоректний manager_id.");
                return;
            }

            // 2. Отримання даних з репозиторію
            Manager manager = await ManagerRepository.GetManagerByIdAsync(connectionString, managerId);

            // 3. Формування відповіді
            if (manager != null)
            {
                // Формуємо Dictionary для відправки на клієнт
                var managerData = new Dictionary<string, object>
                {
                    { "manager_id", manager.ManagerId },
                    { "name", manager.Name },
                    { "phone_number", manager.PhoneNumber },
                    { "role", manager.Role },
                    { "club_name", manager.ClubName } // Відправляємо назву клубу
                    // Login та PasswordHash не відправляємо з міркувань безпеки
                };

                Console.WriteLine($"[ManagerService] Successfully retrieved data for manager ID: {managerId}");
                // Відправляємо успішну відповідь з даними
                await ResponseHelper.SendJsonResponse(stream, new Dictionary<string, object>
                {
                    { "success", "true" },
                    { "message", "Дані менеджера завантажено." },
                    { "data", managerData } // Вкладаємо дані менеджера у поле "data"
                });
            }
            else
            {
                Console.WriteLine($"[ManagerService] Manager with ID {managerId} not found.");
                // Відправляємо помилку, якщо менеджера не знайдено або сталася помилка в репозиторії
                await ResponseHelper.SendErrorResponse(stream, $"Менеджера з ID {managerId} не знайдено або виникла помилка при завантаженні.");
            }
        }


        // Обробка запиту на оновлення даних менеджера
        public static async Task HandleUpdateAccountData(NetworkStream stream, string connectionString, Dictionary<string, string> requestData)
        {
            Console.WriteLine("[ManagerService] Handling UpdateAccountData request.");

            // 1. Валідація вхідних даних
            if (!requestData.TryGetValue("manager_id", out string managerIdStr) || !int.TryParse(managerIdStr, out int managerId))
            {
                Console.WriteLine("[ManagerService] Missing or invalid manager_id in UpdateAccountData request.");
                await ResponseHelper.SendErrorResponse(stream, "Невірний формат запиту: відсутній або некоректний manager_id.");
                return;
            }

            if (!requestData.TryGetValue("name", out string newName) || string.IsNullOrWhiteSpace(newName))
            {
                Console.WriteLine("[ManagerService] Missing or empty name in UpdateAccountData request.");
                await ResponseHelper.SendErrorResponse(stream, "Ім'я не може бути порожнім.");
                return;
            }

            if (!requestData.TryGetValue("phone_number", out string newPhone) || string.IsNullOrWhiteSpace(newPhone))
            {
                Console.WriteLine("[ManagerService] Missing or empty phone_number in UpdateAccountData request.");
                await ResponseHelper.SendErrorResponse(stream, "Номер телефону не може бути порожнім.");
                return;
            }

            // TODO: Додати більш детальну валідацію формату номеру телефону, якщо потрібно.

            // 2. Оновлення даних через репозиторій
            bool success = await ManagerRepository.UpdateManagerDetailsAsync(connectionString, managerId, newName, newPhone);

            // 3. Формування відповіді
            if (success)
            {
                Console.WriteLine($"[ManagerService] Successfully updated data for manager ID: {managerId}");
                await ResponseHelper.SendSuccessResponse(stream, "Дані менеджера успішно оновлено.");
            }
            else
            {
                Console.WriteLine($"[ManagerService] Failed to update data for manager ID: {managerId}. Manager not found or DB error.");
                 await ResponseHelper.SendErrorResponse(stream, "Не вдалося оновити дані менеджера. Можливо, менеджер не знайдений або виникла помилка на сервері.");
            }
        }

        // --- НОВІ МЕТОДИ ДЛЯ УПРАВЛІННЯ ЗМІНАМИ ---

        // Обробка запиту "get_shift_status_and_history"
        // ПРИМІТКА: Приймає Dictionary<string, object>, оскільки ClientHandler передає його
        public static async Task HandleGetShiftStatusAndHistoryAsync(NetworkStream stream, string connectionString, Dictionary<string, object> requestData)
        {
            Console.WriteLine("[ManagerService] Handling GetShiftStatusAndHistory request.");

            // 1. Валідація manager_id (як int)
            if (!requestData.TryGetValue("manager_id", out object managerIdObj) || !(managerIdObj is long || managerIdObj is int) || Convert.ToInt32(managerIdObj) <= 0)
            {
                Console.WriteLine("[ManagerService] Missing or invalid manager_id in GetShiftStatusAndHistory request.");
                await ResponseHelper.SendErrorResponse(stream, "Невірний формат запиту: відсутній або некоректний manager_id.");
                return;
            }
            int managerId = Convert.ToInt32(managerIdObj);


            // 2. Отримання даних з репозиторію
            var (openShiftId, history) = await ManagerRepository.GetShiftStatusAndHistoryAsync(connectionString, managerId);

            // 3. Формування відповіді
            // Завжди повертаємо success:true, навіть якщо немає змін або відкритої зміни.
            // Статус та історія передаються в полі 'data'.
            var responseData = new Dictionary<string, object>
            {
                { "is_shift_open", openShiftId.HasValue }, // Булеве значення
                { "open_shift_id", openShiftId.HasValue ? (object)openShiftId.Value : null }, // int або null
                { "last_shifts", history } // List<ShiftHistoryItem>
            };

            Console.WriteLine($"[ManagerService] Sending shift data for manager ID {managerId}. IsOpen: {openShiftId.HasValue}, History Count: {history?.Count ?? 0}");

            await ResponseHelper.SendJsonResponse(stream, new Dictionary<string, object>
            {
                { "success", "true" }, // Успіх, якщо репозиторій не кинув виняток
                { "message", "Дані про зміни завантажено." },
                { "data", responseData }
            });
            // Примітка: Якщо репозиторій кинув виняток, він буде спійманий у ClientHandler,
            // і клієнту буде надіслано загальну помилку сервера.

        }

        // Обробка запиту "open_shift"
        // ПРИМІТКА: Приймає Dictionary<string, object>, оскільки ClientHandler передає його
        public static async Task HandleOpenShiftAsync(NetworkStream stream, string connectionString, Dictionary<string, object> requestData)
        {
            Console.WriteLine("[ManagerService] Handling OpenShift request.");

            // 1. Валідація manager_id (як int)
            if (!requestData.TryGetValue("manager_id", out object managerIdObj) || !(managerIdObj is long || managerIdObj is int) || Convert.ToInt32(managerIdObj) <= 0)
            {
                Console.WriteLine("[ManagerService] Missing or invalid manager_id in OpenShift request.");
                await ResponseHelper.SendErrorResponse(stream, "Невірний формат запиту: відсутній або некоректний manager_id.");
                return;
            }
            int managerId = Convert.ToInt32(managerIdObj);

            // 2. Виклик репозиторію для відкриття зміни
            int? newShiftId = await ManagerRepository.OpenShiftAsync(connectionString, managerId);

            // 3. Формування відповіді
            if (newShiftId.HasValue)
            {
                Console.WriteLine($"[ManagerService] Successfully opened shift ID {newShiftId.Value} for manager ID {managerId}.");
                await ResponseHelper.SendJsonResponse(stream, new Dictionary<string, object>
                 {
                     { "success", "true" },
                     { "message", "Зміну успішно відкрито." },
                     { "data", new Dictionary<string, object> { { "shift_id", newShiftId.Value } } } // Повертаємо новий ID
                 });
            }
            else
            {
                Console.WriteLine($"[ManagerService] Failed to open shift for manager ID {managerId}. Repository returned null (possibly already open or DB error).");
                // Репозиторій повертає null, якщо зміна вже відкрита або сталася помилка.
                // Тут можна спробувати запитати статус ще раз, щоб дати точніше повідомлення,
                // але для простоти припустимо, що null означає "не вдалося відкрити".
                // Якщо null прийшов через те, що вже відкрито, репозиторій вже залогіював це.
                await ResponseHelper.SendErrorResponse(stream, "Не вдалося відкрити зміну. Можливо, у вас вже є відкрита зміна або виникла помилка на сервері.");
            }
        }

        // Обробка запиту "close_shift"
        // ПРИМІТКА: Приймає Dictionary<string, object>, оскільки ClientHandler передає його
        public static async Task HandleCloseShiftAsync(NetworkStream stream, string connectionString, Dictionary<string, object> requestData)
        {
            Console.WriteLine("[ManagerService] Handling CloseShift request.");

            // 1. Валідація manager_id та shift_id (як int)
            if (!requestData.TryGetValue("manager_id", out object managerIdObj) || !(managerIdObj is long || managerIdObj is int) || Convert.ToInt32(managerIdObj) <= 0)
            {
                Console.WriteLine("[ManagerService] Missing or invalid manager_id in CloseShift request.");
                await ResponseHelper.SendErrorResponse(stream, "Невірний формат запиту: відсутній або некоректний manager_id.");
                return;
            }
            int managerId = Convert.ToInt32(managerIdObj);

            if (!requestData.TryGetValue("shift_id", out object shiftIdObj) || !(shiftIdObj is long || shiftIdObj is int) || Convert.ToInt32(shiftIdObj) <= 0)
            {
                Console.WriteLine("[ManagerService] Missing or invalid shift_id in CloseShift request.");
                await ResponseHelper.SendErrorResponse(stream, "Невірний формат запиту: відсутній або некоректний shift_id.");
                return;
            }
            int shiftId = Convert.ToInt32(shiftIdObj);

            // === ПОЧАТОК ДОДАНОЇ ЛОГІКИ ПЕРЕВІРКИ ===
            try
            {
                bool hasPendingBookings = await ManagerRepository.CheckForPendingBookingsBeforeShiftCloseAsync(connectionString, managerId, shiftId);

                if (hasPendingBookings)
                {
                    Console.WriteLine($"[ManagerService] Blocking shift close for manager {managerId}, shift {shiftId} due to pending bookings.");
                    // Надсилаємо помилку клієнту
                    await ResponseHelper.SendErrorResponse(stream, "Будь ласка, опрацюйте всі нові замовлення або розрахуйте сеанси зі статусом 'panding' перед закриттям зміни.");
                    return; // Зупиняємо виконання, не закриваємо зміну
                }
            }
            catch (Exception checkEx)
            {
                Console.WriteLine($"[ПОМИЛКА] Exception during booking check for close_shift (ID: {managerId}, ShiftID: {shiftId}): {checkEx.Message}");
                // Якщо виникла помилка при самій перевірці, краще заборонити закриття.
                await ResponseHelper.SendErrorResponse(stream, $"Виникла помилка при перевірці бронювань: {checkEx.Message}. Спробуйте пізніше або зверніться до адміністратора.");
                return; // Зупиняємо виконання
            }
            // === КІНЕЦЬ ДОДАНОЇ ЛОГІКИ ПЕРЕВІРКИ ===


            // 2. Виклик репозиторію для закриття зміни (цей код залишається без змін)
            bool success = await ManagerRepository.CloseShiftAsync(connectionString, managerId, shiftId);

            // 3. Формування відповіді
            if (success)
            {
                Console.WriteLine($"[ManagerService] Successfully closed shift ID {shiftId} for manager ID {managerId}.");
                await ResponseHelper.SendSuccessResponse(stream, "Зміну успішно закрито.");
            }
            else
            {
                Console.WriteLine($"[ManagerService] Failed to close shift ID {shiftId} for manager ID {managerId}. Repository returned false.");
                // Це повідомлення буде відправлено, якщо CloseShiftAsync повернув false (зміна не знайдена, вже закрита тощо)
                await ResponseHelper.SendErrorResponse(stream, "Не вдалося закрити зміну. Можливо, зміна не знайдена, вже закрита або виникла помилка на сервері.");
            }
        }
        public static async Task HandleCheckNewSessionsCountAsync(NetworkStream stream, string connectionString, Dictionary<string, object> requestData)
        {
            Console.WriteLine("[ManagerService] Handling CheckNewSessionsCount request.");

            // 1. Валідація club_id
            if (!requestData.TryGetValue("club_id", out object clubIdObj) || !(clubIdObj is long || clubIdObj is int) || Convert.ToInt32(clubIdObj) <= 0)
            {
                Console.WriteLine("[ManagerService] Missing or invalid club_id in CheckNewSessionsCount request.");
                await ResponseHelper.SendErrorResponse(stream, "Невірний формат запиту: відсутній або некоректний club_id.");
                return;
            }
            int clubId = Convert.ToInt32(clubIdObj);

            // 2. Отримуємо кількість нових сеансів з репозиторію
            int newSessionsCount = await ManagerRepository.GetNewSessionsCountAsync(connectionString, clubId);

            // 3. Формуємо відповідь
            if (newSessionsCount >= 0) // Перевіряємо, чи репозиторій не повернув помилку (-1)
            {
                Console.WriteLine($"[ManagerService] Sending new sessions count ({newSessionsCount}) for club ID {clubId}.");
                await ResponseHelper.SendJsonResponse(stream, new Dictionary<string, object>
            {
                { "success", true }, // success має бути булевим значенням
                { "message", "Кількість нових сеансів отримано." },
                { "data", new Dictionary<string, object> { { "new_sessions_count", newSessionsCount } } } // Повертаємо кількість у полі data
            });
            }
            else
            {
                // Якщо репозиторій повернув помилку (-1)
                Console.WriteLine($"[ManagerService] Error getting new sessions count for club ID {clubId}. Repository returned -1.");
                await ResponseHelper.SendErrorResponse(stream, "Не вдалося отримати кількість нових сеансів.");
            }
        }
        public static async Task HandleGetManagersAsync(NetworkStream stream, string connectionString, Dictionary<string, object> requestData) // Приймає Dictionary<string, object>
        {
            Console.WriteLine("[ManagerService] Handling GetManagers request.");

            // Валідація вхідних даних для get_managers (зазвичай не потрібна, якщо запит без параметрів)
            // Якщо плануєте фільтрувати за клубом, додайте валідацію club_id тут.

            try
            {
                // 1. Отримуємо список менеджерів з репозиторію
                var managers = await ManagerRepository.GetAllManagersAsync(connectionString);

                // 2. Формуємо список даних для відправки клієнту (БЕЗ Login, PasswordHash)
                var clientManagersData = managers.Select(m => new // Використовуємо анонімний тип для проектування
                {
                    manager_id = m.ManagerId,
                    name = m.Name,
                    phone_number = m.PhoneNumber,
                    club_id = m.ClubId, // Клієнту може знадобитися ID клубу
                    club_name = m.ClubName, // Клієнту потрібна назва клубу
                    role = m.Role,
                    status = m.Status // Відправляємо статус
                    // Поля Login та PasswordHash тут не включаємо з міркувань безпеки
                }).ToList(); // Перетворюємо результат на список

                // 3. Формуємо відповідь
                await ResponseHelper.SendJsonResponse(stream, new Dictionary<string, object>
                {
                    { "success", "true" },
                    { "message", "Список менеджерів завантажено." },
                    { "data", clientManagersData } // Відправляємо список даних менеджерів
                });

                Console.WriteLine($"[ManagerService] Successfully sent {clientManagersData.Count} managers.");

            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ПОМИЛКА_SERVER] ManagerService.HandleGetManagersAsync: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                // Надсилаємо загальну помилку клієнту
                await ResponseHelper.SendErrorResponse(stream, "Помилка сервера при завантаженні списку менеджерів.");
            }
        }

        // НОВИЙ МЕТОД: Обробка запиту "update_manager_role"
        public static async Task HandleUpdateManagerRoleAsync(NetworkStream stream, string connectionString, Dictionary<string, object> requestData) // Приймає Dictionary<string, object>
        {
            Console.WriteLine("[ManagerService] Handling UpdateManagerRole request.");

            // 1. Валідація вхідних даних
            if (!requestData.TryGetValue("manager_id", out object managerIdObj) || !(managerIdObj is long || managerIdObj is int) || Convert.ToInt32(managerIdObj) <= 0)
            {
                Console.WriteLine("[ManagerService] Missing or invalid manager_id in UpdateManagerRole request.");
                await ResponseHelper.SendErrorResponse(stream, "Невірний формат запиту: відсутній або некоректний manager_id.");
                return;
            }
            int managerId = Convert.ToInt32(managerIdObj);

            if (!requestData.TryGetValue("role", out object roleObj) || !(roleObj is string newRole) || string.IsNullOrWhiteSpace(newRole))
            {
                Console.WriteLine("[ManagerService] Missing or empty role in UpdateManagerRole request.");
                await ResponseHelper.SendErrorResponse(stream, "Невірний формат запиту: відсутня або некоректна роль.");
                return;
            }
            // TODO: Додати валідацію допустимих значень ролі ("admin", "manager", etc.), якщо потрібно

            try
            {
                // 2. Виклик репозиторію для оновлення ролі
                bool success = await ManagerRepository.UpdateManagerRoleAsync(connectionString, managerId, newRole);

                // 3. Формування відповіді
                if (success)
                {
                    Console.WriteLine($"[ManagerService] Successfully updated role for manager ID: {managerId} to '{newRole}'.");
                    await ResponseHelper.SendSuccessResponse(stream, "Роль менеджера успішно оновлено.");
                }
                else
                {
                    Console.WriteLine($"[ManagerService] Failed to update role for manager ID: {managerId}. Manager not found or DB error.");
                    await ResponseHelper.SendErrorResponse(stream, "Не вдалося оновити роль менеджера. Можливо, менеджер не знайдений або виникла помилка на сервері.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ПОМИЛКА_SERVER] ManagerService.HandleUpdateManagerRoleAsync (ID: {managerId}, Role: {newRole}): {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                await ResponseHelper.SendErrorResponse(stream, "Помилка сервера при оновленні ролі менеджера.");
            }
        }

        // НОВИЙ МЕТОД: Обробка запиту "update_manager_status"
        public static async Task HandleUpdateManagerStatusAsync(NetworkStream stream, string connectionString, Dictionary<string, object> requestData) // Приймає Dictionary<string, object>
        {
            Console.WriteLine("[ManagerService] Handling UpdateManagerStatus request.");

            // 1. Валідація вхідних даних
            if (!requestData.TryGetValue("manager_id", out object managerIdObj) || !(managerIdObj is long || managerIdObj is int) || Convert.ToInt32(managerIdObj) <= 0)
            {
                Console.WriteLine("[ManagerService] Missing or invalid manager_id in UpdateManagerStatus request.");
                await ResponseHelper.SendErrorResponse(stream, "Невірний формат запиту: відсутній або некоректний manager_id.");
                return;
            }
            int managerId = Convert.ToInt32(managerIdObj);

            if (!requestData.TryGetValue("status", out object statusObj) || !(statusObj is string newStatus) || string.IsNullOrWhiteSpace(newStatus))
            {
                Console.WriteLine("[ManagerService] Missing or empty status in UpdateManagerStatus request.");
                await ResponseHelper.SendErrorResponse(stream, "Невірний формат запиту: відсутній або некоректний статус.");
                return;
            }
            // TODO: Додати валідацію допустимих значень статусу ("Hired", "Dismissed"), якщо потрібно

            try
            {
                // 2. Виклик репозиторію для оновлення статусу
                bool success = await ManagerRepository.UpdateManagerStatusAsync(connectionString, managerId, newStatus);

                // 3. Формування відповіді
                if (success)
                {
                    Console.WriteLine($"[ManagerService] Successfully updated status for manager ID: {managerId} to '{newStatus}'.");
                    await ResponseHelper.SendSuccessResponse(stream, "Статус менеджера успішно оновлено.");
                }
                else
                {
                    Console.WriteLine($"[ManagerService] Failed to update status for manager ID: {managerId}. Manager not found or DB error.");
                    await ResponseHelper.SendErrorResponse(stream, "Не вдалося оновити статус менеджера. Можливо, менеджер не знайдений або виникла помилка на сервері.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ПОМИЛКА_SERVER] ManagerService.HandleUpdateManagerStatusAsync (ID: {managerId}, Status: {newStatus}): {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                await ResponseHelper.SendErrorResponse(stream, "Помилка сервера при оновленні статусу менеджера.");
            }
        }
        public static async Task HandleGetManagerDetailsAsync(NetworkStream stream, string connectionString, Dictionary<string, object> requestData)
        {
            Console.WriteLine("[ManagerService] Handling GetManagerDetails request.");

            if (!requestData.TryGetValue("manager_id", out object managerIdObj) || !(managerIdObj is long || managerIdObj is int) || Convert.ToInt32(managerIdObj) <= 0)
            {
                Console.WriteLine("[ManagerService] Missing or invalid manager_id in GetManagerDetails request.");
                await ResponseHelper.SendErrorResponse(stream, "Невірний формат запиту: відсутній або некоректний manager_id.");
                return;
            }
            int managerId = Convert.ToInt32(managerIdObj);

            try
            {
                // 1. Отримуємо деталі конкретного менеджера (тепер з Role string)
                var managerDetails = await ManagerRepository.GetManagerDetailsForEditAsync(connectionString, managerId);

                // 2. Отримуємо УСІ унікальні імена ролей з таблиці managers
                var availableRoleNames = await ManagerRepository.GetAllUniqueRoleNamesAsync(connectionString); // Це List<string>

                // 3. Отримуємо всі клуби
                // TODO: Переконайтесь, що ClubRepository.GetAllClubsAsync існує і приймає connectionString
                var availableClubs = await ClubRepository.GetAllClubsAsyncEdit(connectionString);


                // 4. Формування відповіді
                if (managerDetails.HasValue) // Перевіряємо, чи менеджера знайдено
                {
                    // --- КОД ДЛЯ РОЗРАХУНКУ availableRolesWithSyntheticIds та currentRoleId ---
                    // >>> ЦЕЙ КОД ПОВИНЕН БУТИ ТУТ, ДО ОГОЛОШЕННЯ СЛОВНИКА responseData <<<

                    // Створюємо тимчасові {id, name} об'єкти для доступних ролей
                    var availableRolesWithSyntheticIds = availableRoleNames
                        .Select((name, index) => new { id = index + 1, name = name }) // Використовуємо індекс+1 як фіктивний ID
                        .ToList(); // !!! Рядок, де, ймовірно, була помилка CS0428, якщо .Count() був замість .Count !!!
                                   // (Але зараз це просто .ToList(), тут не має бути Count)

                    // Знаходимо фіктивний ID для поточної ролі менеджера
                    // Порівнюємо ім'я ролі менеджера (managerDetails.Value.Role) з іменами в списку унікальних ролей
                    int currentRoleId = availableRolesWithSyntheticIds
                        .FirstOrDefault(r => r.name.Equals(managerDetails.Value.Role, StringComparison.OrdinalIgnoreCase))?.id ?? 0; // Знаходимо ID за іменем ролі

                    // --- КІНЕЦЬ КОДУ ДЛЯ РОЗРАХУНКУ ---


                    // >>> ОГОЛОШЕННЯ СЛОВНИКА responseData <<<
                    var responseData = new Dictionary<string, object>
                    {
                        // Тепер тут тільки пари ключ-значення, використовуючи розраховані вище змінні
                        { "manager_id", managerDetails.Value.ManagerId },
                        { "name", managerDetails.Value.Name },
                        { "phone_number", managerDetails.Value.PhoneNumber },

                        // Надсилаємо фіктивний ID поточної ролі (як очікує клієнтська модель)
                        { "current_role_id", currentRoleId },

                        // Надсилаємо ID поточного клубу
                        { "current_club_id", managerDetails.Value.ClubId },

                        // Надсилаємо список ролей, використовуючи тимчасові об'єкти з фіктивними ID
                        // Клієнтська модель ManagerDetailsResponseData.AvailableRoles (List<Role>)
                        // ОЧІКУЄ СПИСОК ОБ'ЄКТІВ З ПОЛЯМИ "id" та "name".
                        { "available_roles", availableRolesWithSyntheticIds },


                        // TODO: Переконайтесь, що ClubRepository.GetAllClubsAsync повертає Club з ClubId/Name
                        { "available_clubs", availableClubs.Select(c => new { id = c.ClubId, name = c.Name }).ToList() } // Відправляємо список клубів у потрібному клієнту форматі Club->{id, name}
                    };
                    // >>> КІНЕЦЬ ОГОЛОШЕННЯ СЛОВНИКА <<<


                    Console.WriteLine($"[ManagerService] Sending details for manager ID {managerId}. Role: {managerDetails.Value.Role}, Club ID: {managerDetails.Value.ClubId}. Available Roles Count: {availableRoleNames.Count}, Clubs: {availableClubs.Count}");
                    await ResponseHelper.SendJsonResponse(stream, new Dictionary<string, object>
                    {
                        { "success", "true" },
                        { "message", "Деталі менеджера завантажено." },
                        { "data", responseData }
                    });
                }
                else
                {
                    Console.WriteLine($"[ManagerService] Manager with ID {managerId} not found for details edit (Repository returned null).");
                    await ResponseHelper.SendErrorResponse(stream, $"Менеджера з ID {managerId} не знайдено.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ПОМИЛКА_SERVER] ManagerService.HandleGetManagerDetailsAsync (ID: {managerId}): {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                await ResponseHelper.SendErrorResponse(stream, "Помилка сервера при завантаженні деталей менеджера.");
            }
        }
        // НОВИЙ МЕТОД: Обробка запиту "update_manager_details"
        // Примітка: Приймає Dictionary<string, object>, оскільки ClientHandler передає його
        public static async Task HandleUpdateManagerDetailsAsync(NetworkStream stream, string connectionString, Dictionary<string, object> requestData)
        {
            Console.WriteLine("[ManagerService] Handling UpdateManagerDetails request.");

            int managerId, newClubId;
            string newName, newPhone;
            string newRoleName = null; // Змінна для зберігання реального імені ролі

            // ... (валідація manager_id, name, phone_number, club_id - код без змін) ...
            if (!requestData.TryGetValue("manager_id", out object managerIdObj) || !(managerIdObj is long || managerIdObj is int) || (managerId = Convert.ToInt32(managerIdObj)) <= 0)
            {
                Console.WriteLine("[ManagerService] Missing or invalid manager_id in UpdateManagerDetails request.");
                await ResponseHelper.SendErrorResponse(stream, "Невірний формат запиту: відсутній або некоректний manager_id.");
                return;
            }
            if (!requestData.TryGetValue("name", out object nameObj) || !(nameObj is string nameStr) || string.IsNullOrWhiteSpace(nameStr))
            {
                Console.WriteLine("[ManagerService] Missing, empty, or non-string name in UpdateManagerDetails request.");
                await ResponseHelper.SendErrorResponse(stream, "Ім'я не може бути порожнім або має невірний формат.");
                return;
            }
            newName = nameStr.Trim();
            if (!requestData.TryGetValue("phone_number", out object phoneObj) || !(phoneObj is string phoneStr) || string.IsNullOrWhiteSpace(phoneStr))
            {
                Console.WriteLine("[ManagerService] Missing, empty, or non-string phone_number in UpdateManagerDetails request.");
                await ResponseHelper.SendErrorResponse(stream, "Номер телефону не може бути порожнім або має невірний формат.");
                return;
            }
            newPhone = phoneStr.Trim();
            if (!requestData.TryGetValue("club_id", out object clubIdObj) || !(clubIdObj is long || clubIdObj is int) || (newClubId = Convert.ToInt32(clubIdObj)) <= 0)
            {
                Console.WriteLine("[ManagerService] Missing or invalid club_id in UpdateManagerDetails request.");
                await ResponseHelper.SendErrorResponse(stream, "Невірний формат запиту: відсутній або некоректний club_id.");
                return;
            }


            // >>> АДАПТАЦІЯ: Отримуємо ФІКТИВНИЙ role_id від клієнта та знаходимо реальне ім'я ролі <<<
            if (!requestData.TryGetValue("role_id", out object roleIdObj) || !(roleIdObj is long || roleIdObj is int) || Convert.ToInt32(roleIdObj) <= 0)
            {
                Console.WriteLine("[ManagerService] Missing or invalid role_id in UpdateManagerDetails request.");
                await ResponseHelper.SendErrorResponse(stream, "Невірний формат запиту: відсутній або некоректний role_id.");
                return;
            }
            int receivedRoleId = Convert.ToInt32(roleIdObj); // Це фіктивний ID з клієнта

            // Отримуємо список унікальних імен ролей з БД, щоб знайти відповідне ім'я за фіктивним ID
            var availableRoleNames = await ManagerRepository.GetAllUniqueRoleNamesAsync(connectionString);

            // Знаходимо ім'я ролі за отриманим фіктивним ID (припускаємо, що ID = індекс у списку + 1)
            if (receivedRoleId > 0 && receivedRoleId <= availableRoleNames.Count)
            {
                newRoleName = availableRoleNames[receivedRoleId - 1]; // Отримуємо ім'я ролі за індексом
                Console.WriteLine($"[ManagerService] Mapped received role_id {receivedRoleId} to role name '{newRoleName}'.");
            }
            else
            {
                Console.WriteLine($"[ManagerService] Received invalid role_id {receivedRoleId} for UpdateManagerDetails.");
                await ResponseHelper.SendErrorResponse(stream, $"Невідома роль з ID {receivedRoleId}.");
                return; // Не можемо продовжити, якщо роль недійсна
            }
            // --------------------------------------------------------------------------


            try
            {
                // 2. Виклик репозиторію для оновлення (передаємо ЗНАЙДЕНЕ STRING ім'я ролі)
                bool success = await ManagerRepository.UpdateManagerFullDetailsAsync(connectionString, managerId, newName, newPhone, newRoleName, newClubId);

                // 3. Формування відповіді
                if (success)
                {
                    Console.WriteLine($"[ManagerService] Successfully updated full details for manager ID: {managerId}. New Role: {newRoleName}");
                    await ResponseHelper.SendSuccessResponse(stream, "Дані менеджера успішно оновлено.");
                }
                else
                {
                    Console.WriteLine($"[ManagerService] Failed to update full details for manager ID: {managerId}. Repository returned false (Manager not found or DB error).");
                    await ResponseHelper.SendErrorResponse(stream, "Не вдалося оновити дані менеджера. Можливо, менеджер не знайдений або виникла помилка на сервері.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ПОМИЛКА_SERVER] ManagerService.HandleUpdateManagerDetailsAsync (ID: {managerId}): {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                await ResponseHelper.SendErrorResponse(stream, "Помилка сервера при оновленні даних менеджера.");
            }
        }
    }
    // TODO: Додати методи для обробки інших дій акаунта, наприклад:
    // HandleStartWork(NetworkStream stream, string connectionString, Dictionary<string, string> requestData)
    // HandleEndWork(NetworkStream stream, string connectionString, Dictionary<string, string> requestData)
}