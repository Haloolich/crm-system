// Services/ClubService.cs
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading.Tasks;
using ConsoleBookingApp.Repositories; // Потрібно для ClubRepository
using ConsoleBookingApp.Models; // Потрібно для Club
using Newtonsoft.Json;
using System.Text; // Для серіалізації списку клубів

namespace ConsoleBookingApp.Services
{
    public static class ClubService
    {
        // <--- ДОДАНО МЕТОД: Обробка запиту на отримання всіх клубів ---
        public static async Task HandleGetAllClubsAsync(NetworkStream stream, string connectionString)
        {
            Console.WriteLine("[ClubService] Handling get_all_clubs...");
            var clubRepository = new ClubRepository(connectionString);

            try
            {
                var clubs = await clubRepository.GetAllClubsAsync();

                // --- ПОЧАТОК: ФОРМУВАННЯ ВІДПОВІДІ З КЛЮЧЕМ "clubs" НА ВЕРХНЬОМУ РІВНІ ---
                // Створюємо словник, що представляє ПОВНУ JSON-відповідь
                var responseData = new Dictionary<string, object>
                {
                    { "success", true },
                    { "message", "Список клубів успішно отримано." },
                    { "clubs", clubs } // Додаємо список клубів ПРЯМО ТУТ під ключем "clubs"
                };

                // Серіалізуємо ВЕСЬ словник у JSON рядок
                string finalJsonResponse = JsonConvert.SerializeObject(responseData, Formatting.None); // Formatting.None для компактності

                // Перетворюємо JSON рядок у байти
                byte[] jsonBytes = Encoding.UTF8.GetBytes(finalJsonResponse);

                // Отримуємо довжину байтів та перетворюємо її у 4 байти (довжина префікса)
                byte[] lengthPrefix = BitConverter.GetBytes(jsonBytes.Length);

                // Відправляємо спочатку довжину, потім дані
                await stream.WriteAsync(lengthPrefix, 0, lengthPrefix.Length);
                await stream.WriteAsync(jsonBytes, 0, jsonBytes.Length);
                await stream.FlushAsync(); // Переконуємось, що дані відправлені

                Console.WriteLine($"[ClubService] Sent custom get_all_clubs response ({jsonBytes.Length} bytes).");
                // --- КІНЕЦЬ: ФОРМУВАННЯ ВІДПОВІДІ ВРУЧНУ ---
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ClubService] Error getting all clubs: {ex.Message}\n{ex.StackTrace}");
                // Якщо сталася помилка *до* успішного формування відповіді,
                // надсилаємо стандартну помилку через ResponseHelper
                await ResponseHelper.SendErrorResponse(stream, "Помилка сервера при отриманні списку клубів.");
            }
        }
        // ---------------------------------------------

        // <--- ДОДАНО МЕТОД: Обробка запиту на отримання деталей клубу ---
        public static async Task HandleGetClubDetailsAsync(NetworkStream stream, string connectionString, Dictionary<string, object> data)
        {
            Console.WriteLine("[ClubService] Handling get_club_details...");

            // Парсинг club_id - залишаємо без змін
            if (!data.TryGetValue("club_id", out object clubIdObj) || !(clubIdObj is long || clubIdObj is int))
            {
                Console.WriteLine("[ClubService] Missing or invalid 'club_id' for get_club_details.");
                await ResponseHelper.SendErrorResponse(stream, "Некоректний або відсутній ID клубу.");
                return;
            }

            int clubId = Convert.ToInt32(clubIdObj);

            var clubRepository = new ClubRepository(connectionString);

            try
            {
                var club = await clubRepository.GetClubByIdAsync(clubId);

                if (club != null)
                {
                    // --- ПОЧАТОК: ФОРМУВАННЯ ВІДПОВІДІ З ОБ'ЄКТОМ "club" НА ВЕРХНЬОМУ РІВНІ ---
                    // Створюємо словник, що представляє ПОВНУ JSON-відповідь
                    var responseData = new Dictionary<string, object>
                    {
                        { "success", true },
                        { "message", $"Деталі клубу {clubId} успішно отримано." },
                        { "club", club } // Додаємо об'єкт клубу ПРЯМО ТУТ під ключем "club"
                    };

                    // Серіалізуємо ВЕСЬ словник у JSON рядок
                    string finalJsonResponse = JsonConvert.SerializeObject(responseData, Formatting.None); // Formatting.None для компактності

                    // Перетворюємо JSON рядок у байти
                    byte[] jsonBytes = Encoding.UTF8.GetBytes(finalJsonResponse);

                    // Отримуємо довжину байтів та перетворюємо її у 4 байти (довжина префікса)
                    byte[] lengthPrefix = BitConverter.GetBytes(jsonBytes.Length);

                    // Відправляємо спочатку довжину, потім дані
                    await stream.WriteAsync(lengthPrefix, 0, lengthPrefix.Length);
                    await stream.WriteAsync(jsonBytes, 0, jsonBytes.Length);
                    await stream.FlushAsync(); // Переконуємось, що дані відправлені

                    Console.WriteLine($"[ClubService] Sent custom get_club_details response ({jsonBytes.Length} bytes) for ID {clubId}.");
                    // --- КІНЕЦЬ: ФОРМУВАННЯ ВІДПОВІДІ ВРУЧНУ ---
                }
                else
                {
                    // Клуб не знайдено - надсилаємо помилку через ResponseHelper
                    await ResponseHelper.SendErrorResponse(stream, $"Клуб з ID {clubId} не знайдено.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ClubService] Error getting club details for ID {clubId}: {ex.Message}\n{ex.StackTrace}");
                // Якщо сталася помилка при отриманні з БД тощо - надсилаємо помилку через ResponseHelper
                await ResponseHelper.SendErrorResponse(stream, "Помилка сервера при отриманні деталей клубу.");
            }
        }
        // ---------------------------------------------

        // <--- ДОДАНО МЕТОД: Обробка запиту на видалення клубу ---
        public static async Task HandleDeleteClubAsync(NetworkStream stream, string connectionString, Dictionary<string, object> data)
        {
            Console.WriteLine("[ClubService] Handling delete_club...");
            if (!data.TryGetValue("club_id", out object clubIdObj) || !(clubIdObj is long || clubIdObj is int))
            {
                Console.WriteLine("[ClubService] Missing or invalid 'club_id' for delete_club.");
                await ResponseHelper.SendErrorResponse(stream, "Некоректний або відсутній ID клубу для видалення.");
                return;
            }

            int clubId = Convert.ToInt32(clubIdObj); // Безпечно перетворюємо

            var clubRepository = new ClubRepository(connectionString);

            try
            {
                var (success, error) = await clubRepository.DeleteClubAsync(clubId);

                if (success)
                {
                    await ResponseHelper.SendSuccessResponse(stream, $"Клуб з ID {clubId} успішно видалено.");
                }
                else
                {
                    await ResponseHelper.SendErrorResponse(stream, error);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ClubService] Error deleting club ID {clubId}: {ex.Message}\n{ex.StackTrace}");
                await ResponseHelper.SendErrorResponse(stream, "Помилка сервера при видаленні клубу.");
            }
        }
        // ---------------------------------------------

        // <--- ДОДАНО МЕТОД: Обробка запиту на оновлення клубу ---
        public static async Task HandleUpdateClubAsync(NetworkStream stream, string connectionString, Dictionary<string, object> data)
        {
            Console.WriteLine("[ClubService] Handling update_club...");

            // Перевірка та парсинг ID клубу
            if (!data.TryGetValue("club_id", out object clubIdObj) || !(clubIdObj is long || clubIdObj is int))
            {
                Console.WriteLine("[ClubService] Missing or invalid 'club_id' for update_club.");
                await ResponseHelper.SendErrorResponse(stream, "Некоректний або відсутній ID клубу для оновлення.");
                return;
            }

            int clubId = Convert.ToInt32(clubIdObj); // Безпечно перетворюємо

            // Тут можна додати перевірки на наявність інших обов'язкових полів для оновлення,
            // якщо вони потрібні. Наприклад, якщо оновлення вимагає обов'язкової назви:
            /*
            if (!data.ContainsKey("name") || !(data["name"] is string))
            {
                 await ResponseHelper.SendErrorResponse(stream, "Відсутня або некоректна назва клубу для оновлення.");
                 return;
            }
            */
            // Однак, оскільки репозиторій використовує GetValueOrDefault,
            // ми можемо дозволити оновлення лише окремих полів,
            // якщо клієнт надсилає не повний набір даних.
            // Якщо потрібне повне оновлення, додайте всі перевірки тут.


            var clubRepository = new ClubRepository(connectionString);

            try
            {
                // Передаємо весь словник data та clubId в репозиторій
                var (success, error) = await clubRepository.UpdateClubAsync(clubId, data);

                if (success)
                {
                    await ResponseHelper.SendSuccessResponse(stream, $"Дані клубу з ID {clubId} успішно оновлено.");
                }
                else
                {
                    await ResponseHelper.SendErrorResponse(stream, error);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ClubService] Error updating club ID {clubId}: {ex.Message}\n{ex.StackTrace}");
                await ResponseHelper.SendErrorResponse(stream, "Помилка сервера при оновленні клубу.");
            }
        }
        // ---------------------------------------------
    }
}