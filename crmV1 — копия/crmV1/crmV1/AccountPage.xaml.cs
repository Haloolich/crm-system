// crmV1/AccountPage.xaml.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;
using Newtonsoft.Json; // Для роботи з JSON
using Newtonsoft.Json.Linq; // Для роботи з JObject
using System.Diagnostics; // Для Debug.WriteLine
using crmV1.Services; // Додайте using для вашого ApiClient


namespace crmV1
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class AccountPage : ContentPage
    {
        private int _managerId;
        private int _clubId; // ClubId менеджера, можливо, знадобиться

        // Клас для представлення даних менеджера з сервера
        // Зверніть увагу: імена властивостей мають відповідати ключам у Dictionary,
        // що повертає сервер у полі "data", враховуючи snake_case.
        public class ManagerAccountData
        {
            [JsonProperty("manager_id")] // Використовуємо анотацію, якщо ім'я властивості в C# відрізняється від JSON ключа
            public int ManagerId { get; set; }

            [JsonProperty("name")]
            public string Name { get; set; }

            [JsonProperty("phone_number")]
            public string PhoneNumber { get; set; }

            [JsonProperty("role")] // Не редагується
            public string Role { get; set; }

            [JsonProperty("club_name")] // Не редагується
            public string ClubName { get; set; }
        }

        // Тут можна зберігати поточні дані менеджера після завантаження
        private ManagerAccountData _currentManagerData;

        public AccountPage(int managerId, int clubId)
        {
            InitializeComponent();
            // Приховуємо стандартний заголовок Xamarin.Forms
           NavigationPage.SetHasNavigationBar(this, false); // Зазвичай це робиться на кореневій сторінці NavigationPage

            _managerId = managerId;
            _clubId = clubId; // Зберігаємо ClubId

            // Завантажуємо дані менеджера при відкритті сторінки
            // Викликаємо асинхронний метод при запуску сторінки
            // Використовуємо Device.BeginInvokeOnMainThread, бо це оновлення UI
            // та асинхронна операція, запущена з конструктора.
            Device.BeginInvokeOnMainThread(async () => await LoadManagerData(_managerId));
        }

        // Метод для завантаження даних менеджера з сервера
        private async Task LoadManagerData(int managerId)
        {
            // Показуємо індикатор завантаження (опціонально)
            activityIndicator.IsRunning = true;
            activityIndicator.IsVisible = true;
            Content.IsEnabled = false; // Вимикаємо вміст сторінки

            try
            {
                // Створюємо запит для сервера
                // --- ВИПРАВЛЕННЯ: Змінено Dictionary<string, string> на Dictionary<string, object> ---
                var requestData = new Dictionary<string, object> // <--- Ось тут виправлення
                {
                    { "action", "get_account_data" },
                    { "manager_id", managerId } // Тепер managerId може бути int
                };

                Debug.WriteLine($"[AccountPage] Sending request: {JsonConvert.SerializeObject(requestData)}"); // Лог запиту

                // --- ВИКЛИК API КЛІЄНТА ---
                // ApiClient.SendRequestAsync тепер очікує Dictionary<string, object>
                Dictionary<string, object> response = await ApiClient.SendRequestAsync(requestData);
                // --- КІНЕЦЬ ВИКЛИКУ API КЛІЄНТА ---

                Debug.WriteLine($"[AccountPage] Received response: {JsonConvert.SerializeObject(response)}"); // Лог відповіді

                // 4. Обробка відповіді сервера
                if (response != null && response.TryGetValue("success", out object successObj) && bool.TryParse(successObj.ToString(), out bool success) && success)
                {
                    // Дані успішно завантажено
                    if (response.TryGetValue("data", out object dataObj)) // Перевіряємо тільки наявність поля "data"
                    {
                        // ТЕПЕРЬ ПЕРЕВІРЯЄМО ЧИ ЦЕ JObject (типово для вкладених об'єктів в Newtonsoft.Json)
                        if (dataObj is JObject managerJObject)
                        {
                            try
                            {
                                // Безпосередньо десеріалізуємо JObject у наш клас ManagerAccountData
                                Debug.WriteLine($"[AccountPage] Received 'data' is JObject. Attempting to deserialize to ManagerAccountData.");
                                _currentManagerData = managerJObject.ToObject<ManagerAccountData>();

                                if (_currentManagerData != null)
                                {
                                    // Відображаємо дані на сторінці
                                    nameEntry.Text = _currentManagerData.Name;
                                    phoneEntry.Text = _currentManagerData.PhoneNumber;
                                    roleLabel.Text = _currentManagerData.Role; // Роль не редагується
                                    clubLabel.Text = _currentManagerData.ClubName; // Клуб не редагується
                                    Debug.WriteLine("[AccountPage] Manager data deserialized and loaded successfully.");
                                }
                                else
                                {
                                    // Цей блок спрацьовує, якщо toObject повернув null (рідко, але можливо при проблемах маппінгу)
                                    Debug.WriteLine("[AccountPage] Failed to deserialize manager data from JObject: toObject returned null.");
                                    await DisplayAlert("Помилка", "Не вдалося обробити дані менеджера з сервера (десеріалізація повернула null).", "OK");
                                }
                            }
                            catch (JsonException jsonEx)
                            {
                                // Цей блок спрацьовує, якщо під час десеріалізації JObject виникла помилка JSON
                                Debug.WriteLine($"[AccountPage] JSON Deserialization Error from JObject: {jsonEx.Message}.");
                                await DisplayAlert("Помилка", $"Помилка при обробці даних менеджера: {jsonEx.Message}", "OK");
                                _currentManagerData = null; // Забезпечуємо, що дані порожні при помилці
                            }
                            catch (Exception innerEx)
                            {
                                // Ловимо будь-які інші непередбачені помилки під час обробки даних з JObject
                                Debug.WriteLine($"[AccountPage] Unexpected error processing manager JObject: {innerEx.Message}. StackTrace: {innerEx.StackTrace}");
                                await DisplayAlert("Помилка", $"Виникла непередбачена помилка при обробці даних менеджера: {innerEx.Message}", "OK");
                                _currentManagerData = null;
                            }
                        }
                        else
                        {
                            // Цей блок означає, що "data" ключ існує, але його значення НЕ JObject
                            Debug.WriteLine($"[AccountPage] Response 'data' field exists but is not a JObject. Actual Type: {dataObj?.GetType().Name ?? "null"}.");
                            await DisplayAlert("Помилка", "Невірний формат даних менеджера з сервера (поле 'data' має неочікуваний тип).", "OK"); // Уточнено повідомлення
                            _currentManagerData = null;
                        }
                    }
                    else
                    {
                        // Цей блок спрацьовує, якщо поле "data" відсутнє повністю
                        Debug.WriteLine("[AccountPage] Response 'data' field is missing entirely.");
                        await DisplayAlert("Помилка", "Невірний формат даних менеджера з сервера (відсутнє поле 'data').", "OK"); // Уточнено повідомлення
                        _currentManagerData = null;
                    }
                }
                else
                {
                    // Сервер повернув помилку або помилка ApiClient
                    string errorMessage = "Невідома помилка при завантаженні даних.";
                    if (response != null && response.TryGetValue("message", out object messageObj))
                    {
                        errorMessage = messageObj.ToString();
                    }
                    Debug.WriteLine($"[AccountPage] Server returned error or ApiClient error: {errorMessage}");
                    await DisplayAlert("Помилка завантаження", errorMessage, "OK");
                    _currentManagerData = null; // Забезпечуємо, що дані порожні при помилці
                                                // Можливо, повернутись на попередню сторінку, якщо дані критично важливі:
                                                // await Navigation.PopAsync();
                }
            }
            catch (Exception ex) // Помилка на рівні мережі або обробки (хоча ApiClient має ловити більшість)
            {
                Debug.WriteLine($"[AccountPage] Unexpected Exception during LoadManagerData: {ex.Message}. StackTrace: {ex.StackTrace}");
                // ApiClient має повертати Dictionary з помилкою, тому цей catch може бути менш частим
                await DisplayAlert("Помилка", $"Виникла непередбачена помилка при завантаженні даних: {ex.Message}", "OK");
                _currentManagerData = null;
                // await Navigation.PopAsync();
            }
            finally
            {
                activityIndicator.IsRunning = false;
                activityIndicator.IsVisible = false;
                Content.IsEnabled = true;
            }
        }

        // Обробник натискання кнопки "Зберегти"
        private async void OnSaveClicked(object sender, EventArgs e)
        {
            // Перевіряємо, чи дані менеджера були завантажені
            if (_currentManagerData == null)
            {
                await DisplayAlert("Помилка", "Дані менеджера ще не завантажені. Спробуйте пізніше.", "OK");
                return;
            }

            // Отримуємо оновлені дані з полів вводу
            string updatedName = nameEntry.Text?.Trim(); // Видаляємо пробіли
            string updatedPhone = phoneEntry.Text?.Trim(); // Видаляємо пробіли

            // Перевірка на валідність даних
            if (string.IsNullOrWhiteSpace(updatedName))
            {
                await DisplayAlert("Помилка збереження", "Ім'я не може бути порожнім.", "OK");
                return;
            }
            if (string.IsNullOrWhiteSpace(updatedPhone))
            {
                await DisplayAlert("Помилка збереження", "Номер телефону не може бути порожнім.", "OK");
                return;
            }
            // TODO: Додати більш детальну валідацію формату номеру телефону, якщо потрібно.

            // Перевіряємо, чи дані дійсно змінились перед відправкою на сервер
            // Порівнюємо з локальною копією даних, яка відображає стан ДО збереження
            if (updatedName == _currentManagerData.Name && updatedPhone == _currentManagerData.PhoneNumber)
            {
                await DisplayAlert("Збереження", "Дані не були змінені.", "OK");
                return;
            }

            // Показуємо індикатор збереження
            saveButton.IsEnabled = false; // Вимикаємо кнопку
            activityIndicator.IsRunning = true;
            activityIndicator.IsVisible = true;
            Content.IsEnabled = false; // Вимикаємо вміст сторінки

            try
            {
                // Створюємо запит для сервера
                // --- ВИПРАВЛЕННЯ: Змінено Dictionary<string, string> на Dictionary<string, object> ---
                var requestData = new Dictionary<string, object> // <--- Ось тут виправлення
                {
                    { "action", "update_account_data" },
                    { "manager_id", _managerId }, // Тепер _managerId може бути int
                    { "name", updatedName }, // Рядки залишаються рядками
                    { "phone_number", updatedPhone } // Рядки залишаються рядками
                };

                // --- ВИКЛИК API КЛІЄНТА ---
                // ApiClient.SendRequestAsync тепер очікує Dictionary<string, object>
                Dictionary<string, object> response = await ApiClient.SendRequestAsync(requestData);
                Debug.WriteLine($"[AccountPage] Received response from update: {JsonConvert.SerializeObject(response)}");
                // --- КІНЕЦЬ ВИКЛИКУ API КЛІЄНТА ---


                // 4. Обробка відповіді сервера
                // ApiClient тепер гарантовано повертає Dictionary<string, object> з полем "success"
                bool success = response.TryGetValue("success", out object successObj) && bool.TryParse(successObj?.ToString(), out bool parsedSuccess) && parsedSuccess;
                string message = response.TryGetValue("message", out object messageObj) ? messageObj?.ToString() : "Невідоме повідомлення від сервера.";


                if (success)
                {
                    // Оновлення успішне
                    Debug.WriteLine($"[AccountPage] Update successful: {message}");
                    await DisplayAlert("Успіх", message, "OK");

                    // Оновлюємо локальні дані, щоб вони відображали збережений стан
                    _currentManagerData.Name = updatedName;
                    _currentManagerData.PhoneNumber = updatedPhone;
                    Debug.WriteLine($"Manager data updated successfully locally for ID: {_managerId}");

                    // Можливо, після збереження автоматично закрити сторінку:
                    // await Navigation.PopAsync();
                }
                else
                {
                    // Сервер повернув помилку або помилка ApiClient
                    Debug.WriteLine($"[AccountPage] Update failed: {message}");
                    await DisplayAlert("Помилка збереження", message, "OK");
                }
            }
            catch (Exception ex)
            {
                // Помилка на рівні мережі або обробки (має бути рідко завдяки ApiClient)
                Debug.WriteLine($"[AccountPage] Unexpected Exception during OnSaveClicked: {ex.Message}. StackTrace: {ex.StackTrace}");
                await DisplayAlert("Помилка", $"Виникла непередбачена помилка при збереженні даних: {ex.Message}", "OK");
            }
            finally
            {
                // Приховуємо індикатор збереження
                saveButton.IsEnabled = true; // Вмикаємо кнопку
                activityIndicator.IsRunning = false;
                activityIndicator.IsVisible = false;
                Content.IsEnabled = true; // Вмикаємо вміст сторінки
            }
        }

        // Обробник натискання кнопки "Вийти на роботу"
        private async void OnStartWorkClicked(object sender, EventArgs e)
        {
            // TODO: Реалізуйте логіку "Вийти на роботу"
            // Це буде відправка нового типу запиту на сервер ("start_work")
            // який запише в таблицю manager_shifts дату та час початку зміни для даного менеджера.

            // Приклад запиту (припускаємо, що сервер очікує ці дані)
            // --- ВИКОРИСТОВУЄМО Dictionary<string, object> ---
            // var requestData = new Dictionary<string, object>
            // {
            //     { "action", "start_work" },
            //     { "manager_id", _managerId }, // int
            //     // Передаємо дату та час у форматі, який очікує сервер
            //     { "shift_date", DateTime.Today.ToString("yyyy-MM-dd") }, // string
            //     { "start_time", DateTime.Now.ToString("HH:mm:ss") }     // string
            // };

            // Показуємо індикатор (опціонально)
            // startWorkButton.IsEnabled = false;
            // activityIndicator.IsRunning = true; activityIndicator.IsVisible = true; Content.IsEnabled = false;

            // try
            // {
            //      // Викликаємо ApiClient
            //      Dictionary<string, object> response = await ApiClient.SendRequestAsync(requestData);
            //      Debug.WriteLine($"[AccountPage] Received response for start_work: {JsonConvert.SerializeObject(response)}");

            //      // Обробка відповіді від ApiClient
            //      bool success = response.TryGetValue("success", out object successObj) && bool.TryParse(successObj?.ToString(), out bool parsedSuccess) && parsedSuccess;
            //      string message = response.TryGetValue("message", out object messageObj) ? messageObj?.ToString() : "Невідоме повідомлення від сервера.";

            //      if (success)
            //      {
            //           Debug.WriteLine($"[AccountPage] Start work successful: {message}");
            //           await DisplayAlert("Успіх", message, "OK");
            //           // TODO: Оновити UI, наприклад, змінити текст кнопки на "Завершити роботу"
            //      }
            //      else
            //      {
            //           Debug.WriteLine($"[AccountPage] Start work failed: {message}");
            //           await DisplayAlert("Помилка", message, "OK");
            //      }
            // }
            // catch (Exception ex) // Ловимо неочікувані помилки (рідко, ApiClient має ловити більшість)
            // {
            //      Debug.WriteLine($"[AccountPage] Unexpected Error during start_work: {ex.Message}. StackTrace: {ex.StackTrace}");
            //      await DisplayAlert("Помилка", $"Виникла помилка при спробі вийти на роботу: {ex.Message}", "OK");
            // }
            // finally
            // {
            //      // Приховуємо індикатор
            //      startWorkButton.IsEnabled = true;
            //      activityIndicator.IsRunning = false; activityIndicator.IsVisible = false; Content.IsEnabled = true;
            // }

            // Тимчасовий DisplayAlert поки функціонал не реалізовано повністю
            await Navigation.PushAsync(new ShiftPage(_managerId));
        }

        // TODO: Можливо, додати кнопку "Завершити роботу" та метод OnEndWorkClicked
        // який відправлятиме час завершення зміни на сервер і оновлюватиме запис у manager_shifts.
        // Цей функціонал залежить від бізнес-логіки.
    }
}