using crmV1.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel; // Для INotifyPropertyChanged якщо потрібне динамічне оновлення
using System.Windows.Input; // Для ICommand
using Xamarin.Forms; // Для Command, Page, DisplayAlert
using Xamarin.CommunityToolkit.ObjectModel; // Для AsyncCommand (потрібно встановити NuGet пакет Xamarin.CommunityToolkit)
using crmV1.Services;
using crmV1;

namespace crmV1.Services
{
    public class ManagerService
    {
        // Метод для отримання списку всіх менеджерів
        public async Task<List<Manager>> GetManagersAsync()
        {
            Debug.WriteLine("[ManagerService] Calling GetManagersAsync...");
            var requestData = new Dictionary<string, object>
            {
                { "action", "get_managers" }
            };

            Dictionary<string, object> response = null; // Ініціалізуємо response поза try
            try
            {
                // Рядок 87 у вашому об'єднаному коді, але помилка виникає ПІСЛЯ цього
                response = await ApiClient.SendRequestAsync(requestData);
            }
            catch (Exception apiEx)
            {
                // Обробка мережевих помилок або винятків під час самого виклику API
                Debug.WriteLine($"[ManagerService] API request failed: {apiEx.Message}\n{apiEx.StackTrace}");
                Device.BeginInvokeOnMainThread(async () =>
                {
                    // Використовуйте Application.Current.MainPage, щоб переконатися, що у нас є дійсний контекст сторінки
                    await Application.Current.MainPage?.DisplayAlert("Помилка зв'язку", $"Не вдалося підключитись до сервера: {apiEx.Message}", "OK");
                });
                return new List<Manager>(); // Повертаємо порожній список при помилці запиту API
            }


            // --- Початок розширеної обробки відповіді ---
            if (response == null)
            {
                // Цей випадок в ідеалі має бути перехоплений блоком try/catch вище, але включено для безпеки
                Debug.WriteLine("[ManagerService] Received null response from ApiClient.");
                Device.BeginInvokeOnMainThread(async () =>
                {
                    await Application.Current.MainPage?.DisplayAlert("Помилка зв'язку", "Не отримано відповідь від сервера.", "OK");
                });
                return new List<Manager>();
            }

            // Надійна перевірка ключа 'success' та його значення
            bool isSuccess = false;
            if (response.TryGetValue("success", out object successObj))
            {
                try
                {
                    // *** ЦЕ КЛЮЧОВА ЗМІНА ***
                    // Використовуємо Convert.ToBoolean, яке обробляє bool, string ("true"/"false"), int (0/1)
                    isSuccess = Convert.ToBoolean(successObj);
                }
                catch (Exception convertEx)
                {
                    // Обробка випадків, коли successObj не є дійсним представленням логічного значення
                    Debug.WriteLine($"[ManagerService] Could not convert 'success' value '{successObj}' (Type: {successObj?.GetType().Name}) to boolean: {convertEx.Message}");
                    Device.BeginInvokeOnMainThread(async () =>
                    {
                        await Application.Current.MainPage?.DisplayAlert("Помилка даних", "Отримано неочікуваний формат булевого значення 'success' від сервера.", "OK");
                    });
                    return new List<Manager>(); // Повертаємо порожній список при помилці формату даних
                }
            }
            else
            {
                Debug.WriteLine("[ManagerService] Response missing 'success' key.");
                Device.BeginInvokeOnMainThread(async () =>
                {
                    await Application.Current.MainPage?.DisplayAlert("Помилка даних", "Відповідь сервера не містить поле 'success'.", "OK");
                });
                return new List<Manager>();
            }


            // Тепер перевіряємо логічне значення, яке ми безпечно отримали
            if (isSuccess)
            {
                // Обробка успішної відповіді
                if (response.TryGetValue("data", out object dataObj) && dataObj is JArray managersJArray)
                {
                    Debug.WriteLine($"[ManagerService] Received {managersJArray.Count} managers JSON objects.");

                    var managersList = new List<Manager>();
                    foreach (var managerToken in managersJArray)
                    {
                        if (managerToken.Type == JTokenType.Object)
                        {
                            try
                            {
                                var manager = managerToken.ToObject<Manager>();
                                if (manager != null)
                                {
                                    managersList.Add(manager);
                                }
                                else
                                {
                                    Debug.WriteLine($"[ManagerService] Deserializing a manager object returned null. JSON: {managerToken.ToString(Newtonsoft.Json.Formatting.None)}");
                                }
                            }
                            catch (Exception innerDeserializeEx)
                            {
                                Debug.WriteLine($"[ManagerService] Error deserializing single manager object: {innerDeserializeEx.Message}");
                                Debug.WriteLine($"[ManagerService] Problematic manager JSON: {managerToken.ToString(Newtonsoft.Json.Formatting.None)}");
                            }
                        }
                        else
                        {
                            Debug.WriteLine($"[ManagerService] Unexpected JToken type in managers array: {managerToken.Type}. Expected Object. Token: {managerToken.ToString(Newtonsoft.Json.Formatting.None)}");
                        }
                    }

                    Debug.WriteLine($"[ManagerService] Finished deserializing managers. Added {managersList.Count} objects to list.");

                    // TODO: Завантажити клуби та встановити ClubName тут, якщо потрібно і не повертається сервером
                    // if (_clubs == null) { _clubs = await new ClubService().GetClubsAsync(); } // Потребує ClubService
                    // if (_clubs != null)
                    // {
                    //     foreach (var manager in managersList)
                    //     {
                    //         // Переконайтеся, що manager.ClubId існує та використовується правильно
                    //         manager.ClubName = _clubs.FirstOrDefault(c => c.ClubId == manager.ClubId)?.Name ?? $"Club ID: {manager.ClubId}";
                    //     }
                    // }
                    // Примітка: Якщо сервер *дійсно* повертає "club_name", переконайтеся, що ваша модель Manager має [JsonProperty("club_name")] string ClubName { get; set; }, і цей блок може бути зайвим. Ваша надана модель вже має це, тому цей блок може не знадобитися.

                    return managersList; // Повертаємо список
                }
                else
                {
                    Debug.WriteLine("[ManagerService] Response 'data' is missing or not a JArray.");
                    Device.BeginInvokeOnMainThread(async () =>
                    {
                        await Application.Current.MainPage?.DisplayAlert("Помилка даних", "Невірний формат даних менеджерів від сервера.", "OK");
                    });
                    return new List<Manager>(); // Повертаємо порожній список при помилці формату даних
                }
            }
            else
            {
                // Обробка випадку, коли 'success' дорівнює false
                string errorMessage = response.TryGetValue("message", out object msgObj) ? msgObj?.ToString() : "Сервер повідомив про помилку при отриманні менеджерів.";
                Debug.WriteLine($"[ManagerService] API reported failure: {errorMessage}");
                Device.BeginInvokeOnMainThread(async () =>
                {
                    await Application.Current.MainPage?.DisplayAlert("Помилка завантаження", $"Не вдалося завантажити список менеджерів: {errorMessage}", "OK");
                });
                return new List<Manager>(); // Повертаємо порожній список при помилці, повідомленій API
            }
            // --- Кінець розширеної обробки відповіді ---

        }

        // ... інші методи (UpdateManagerRoleAsync, UpdateManagerStatusAsync) ...
        // Ці методи також виконують подібну перевірку response["success"].
        // Ви повинні застосувати таку ж надійну перевірку за допомогою Convert.ToBoolean в цих методах також.

        public async Task<bool> UpdateManagerRoleAsync(int managerId, string newRole)
        {
            Debug.WriteLine($"[ManagerService] Calling UpdateManagerRoleAsync for ID {managerId} with role '{newRole}'...");
            var requestData = new Dictionary<string, object>
             {
                 { "action", "update_manager_role" },
                 { "manager_id", managerId },
                 { "role", newRole }
             };

            Dictionary<string, object> response = null;
            try
            {
                response = await ApiClient.SendRequestAsync(requestData);
            }
            catch (Exception apiEx)
            {
                Debug.WriteLine($"[ManagerService] API request failed for role update: {apiEx.Message}\n{apiEx.StackTrace}");
                // Не показуємо DisplayAlert тут, нехай ViewModel обробить виняток, який буде кинуто нижче
                throw new Exception($"Помилка підключення до сервера: {apiEx.Message}");
            }

            // --- Надійна перевірка success для методів Update ---
            bool isSuccess = false;
            if (response != null && response.TryGetValue("success", out object successObj))
            {
                try
                {
                    isSuccess = Convert.ToBoolean(successObj);
                }
                catch (Exception convertEx)
                {
                    Debug.WriteLine($"[ManagerService] Could not convert 'success' value '{successObj}' (Type: {successObj?.GetType().Name}) to boolean during role update: {convertEx.Message}");
                    throw new Exception("Невірний формат даних відповіді сервера (поле success).");
                }
            }
            else
            {
                Debug.WriteLine("[ManagerService] Response for role update is null or missing 'success' key.");
                throw new Exception("Не отримано коректну відповідь від сервера для оновлення ролі.");
            }
            // --- Кінець надійної перевірки success ---


            if (isSuccess)
            {
                Debug.WriteLine($"[ManagerService] Successfully updated role for manager ID {managerId}.");
                // Необов'язково: Перевірити повідомлення про успіх, якщо сервер його повертає
                // string successMessage = response.TryGetValue("message", out object msgObj) ? msgObj?.ToString() : "Роль успішно оновлено.";
                return true;
            }
            else
            {
                string errorMessage = response.TryGetValue("message", out object msgObj) ? msgObj?.ToString() : $"Невідома помилка сервера при оновленні ролі менеджера ID {managerId}.";
                Debug.WriteLine($"[ManagerService] Failed to update role: {errorMessage}");
                throw new Exception(errorMessage); // Кидаємо виняток, щоб ViewModel його перехопив
            }
        }

        public async Task<bool> UpdateManagerStatusAsync(int managerId, string newStatus)
        {
            Debug.WriteLine($"[ManagerService] Calling UpdateManagerStatusAsync for ID {managerId} with status '{newStatus}'...");
            var requestData = new Dictionary<string, object>
            {
                { "action", "update_manager_status" },
                { "manager_id", managerId },
                { "status", newStatus }
            };

            Dictionary<string, object> response = null;
            try
            {
                response = await ApiClient.SendRequestAsync(requestData);
            }
            catch (Exception apiEx)
            {
                Debug.WriteLine($"[ManagerService] API request failed for status update: {apiEx.Message}\n{apiEx.StackTrace}");
                throw new Exception($"Помилка підключення до сервера: {apiEx.Message}");
            }

            // --- Надійна перевірка success для методів Update ---
            bool isSuccess = false;
            if (response != null && response.TryGetValue("success", out object successObj))
            {
                try
                {
                    isSuccess = Convert.ToBoolean(successObj);
                }
                catch (Exception convertEx)
                {
                    Debug.WriteLine($"[ManagerService] Could not convert 'success' value '{successObj}' (Type: {successObj?.GetType().Name}) to boolean during status update: {convertEx.Message}");
                    throw new Exception("Невірний формат даних відповіді сервера (поле success).");
                }
            }
            else
            {
                Debug.WriteLine("[ManagerService] Response for status update is null or missing 'success' key.");
                throw new Exception("Не отримано коректну відповідь від сервера для оновлення статусу.");
            }
            // --- Кінець надійної перевірки success ---

            if (isSuccess)
            {
                Debug.WriteLine($"[ManagerService] Successfully updated status for manager ID {managerId} to '{newStatus}'.");
                // Необов'язково: Перевірити повідомлення про успіх
                // string successMessage = response.TryGetValue("message", out object msgObj) ? msgObj?.ToString() : $"Статус успішно оновлено на '{newStatus}'.";
                return true;
            }
            else
            {
                string errorMessage = response.TryGetValue("message", out object msgObj) ? msgObj?.ToString() : $"Невідома помилка сервера при оновленні статусу менеджера ID {managerId} на '{newStatus}'.";
                Debug.WriteLine($"[ManagerService] Failed to update status: {errorMessage}");
                throw new Exception(errorMessage); // Кидаємо виняток, щоб ViewModel його перехопив
            }
        }
        // Метод для отримання деталей конкретного менеджера (якщо потрібні додаткові дані, яких немає в списку)
        // Наразі модель Manager не має прихованих полів, тому цей метод може бути непотрібним,
        // якщо GetManagersAsync повертає всі потрібні дані, окрім логіна/пароля.
        // Але якщо DetailsPage має дозволяти редагування полів, яких немає у списку (наприклад, email),
        // тоді цей метод потрібен.
        // Враховуючи ваше ТЗ (інформація про менеджера, окрім пароля та логіна),
        // можливо, GetManagersAsync має повертати ВСЕ, крім логіна/пароля.
        // Якщо так, цей метод можна спростити або він буде непотрібним.
        /*
        public async Task<Manager> GetManagerDetailsAsync(int managerId)
        {
             Debug.WriteLine($"[ManagerService] Calling GetManagerDetailsAsync for ID {managerId}...");
             var requestData = new Dictionary<string, object>
             {
                 { "action", "get_manager_details" },
                 { "manager_id", managerId }
             };

             var response = await ApiClient.SendRequestAsync(requestData);

             if (response != null && response.TryGetValue("success", out object successObj) && (bool)successObj)
             {
                 if (response.TryGetValue("data", out object dataObj) && dataObj is JObject managerJObject)
                 {
                     Debug.WriteLine($"[ManagerService] Received details for manager ID {managerId}.");
                     try
                     {
                         var managerDetails = managerJObject.ToObject<Manager>();
                         // TODO: Встановити ClubName, якщо потрібно
                         return managerDetails;
                     }
                     catch (Exception deserializeEx)
                     {
                          Debug.WriteLine($"[ManagerService] Error deserializing manager JObject: {deserializeEx.Message}\n{deserializeEx.StackTrace}");
                         //TODO: Логування або обробка помилки десеріалізації
                         return null; // Повернути null при помилці
                     }
                 }
                 else
                 {
                     Debug.WriteLine($"[ManagerService] Response data for manager ID {managerId} is not a JObject or is missing.");
                     // TODO: Логування або обробка помилки формату даних
                     return null;
                 }
             }
             else
             {
                 string errorMessage = response != null && response.TryGetValue("message", out object msgObj) ? msgObj.ToString() : $"Невідома помилка сервера при отриманні деталей менеджера ID {managerId}.";
                 Debug.WriteLine($"[ManagerService] Failed to get manager details: {errorMessage}");
                 throw new Exception(errorMessage); // Кидаємо виняток
             }
        }
        */

        // Метод для оновлення ролі менеджер
    }
}

