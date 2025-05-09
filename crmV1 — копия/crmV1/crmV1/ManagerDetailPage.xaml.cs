using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using crmV1.Services; // Переконайтесь, що ApiClient доступний

namespace crmV1
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class ManagerDetailPage : ContentPage // Змінено назву класу
    {
        private int _managerId;

        // --- Класи для Ролі та Клубу (використовуються в Picker) ---
        // Ці класи мають відповідати структурі об'єктів у списках "available_roles" та "available_clubs" від сервера
        public class Role
        {
            [JsonProperty("id")] // Очікуємо поле "id" з сервера
            public int Id { get; set; }
            [JsonProperty("name")] // Очікуємо поле "name" з сервера
            public string Name { get; set; }
            // Додаємо ToString() для зручного відображення в Picker, якщо не використовуємо ItemDisplayBinding
            public override string ToString() => Name;
        }

        public class Club
        {
            [JsonProperty("id")] // Очікуємо поле "id" з сервера
            public int Id { get; set; }
            [JsonProperty("name")] // Очікуємо поле "name" з сервера
            public string Name { get; set; }
            // Додаємо ToString() для зручного відображення в Picker, якщо не використовуємо ItemDisplayBinding
            public override string ToString() => Name;
        }
        // --- Кінець класів для Ролі та Клубу ---


        // --- Клас для представлення повних даних менеджера з сервера (для ЗАВАНТАЖЕННЯ) ---
        // Цей клас має відповідати структурі поля "data" у відповіді сервера на запит завантаження деталей
        public class ManagerDetailsResponseData
        {
            [JsonProperty("manager_id")]
            public int ManagerId { get; set; }

            [JsonProperty("name")]
            public string Name { get; set; }

            [JsonProperty("phone_number")]
            public string PhoneNumber { get; set; }

            [JsonProperty("current_role_id")] // ID поточної ролі менеджера
            public int CurrentRoleId { get; set; }

            [JsonProperty("current_club_id")] // ID поточного клубу менеджера
            public int CurrentClubId { get; set; }

            [JsonProperty("available_roles")] // Список всіх доступних ролей
            public List<Role> AvailableRoles { get; set; }

            [JsonProperty("available_clubs")] // Список всіх доступних клубів
            public List<Club> AvailableClubs { get; set; }
        }
        // --- Кінець класу для даних завантаження ---


        // Зберігаємо завантажені дані менеджера та списки, щоб можна було порівняти при збереженні
        private ManagerDetailsResponseData _originalManagerData; // Зберігаємо дані як були завантажені
        private List<Role> _availableRoles;
        private List<Club> _availableClubs;



        // Конструктор приймає ID менеджера, деталі якого потрібно показати/редагувати
        public ManagerDetailPage(int managerId)
        {
            InitializeComponent();
            NavigationPage.SetHasNavigationBar(this, false); // Приховуємо стандартний заголовок

            _managerId = managerId;

            // Завантажуємо дані менеджера та списки доступних ролей/клубів при відкритті сторінки
            // Використовуємо BeginInvokeOnMainThread, бо це асинхронна операція, запущена з конструктора,
            // яка оновлює UI.
            Device.BeginInvokeOnMainThread(async () => await LoadManagerData(_managerId));

            // Підписуватись на Picker.SelectedIndexChanged не обов'язково, якщо не потрібна
            // додаткова логіка при виборі, але можна залишити для логування чи налагодження.
            // rolePicker.SelectedIndexChanged += Picker_SelectedIndexChanged;
            // clubPicker.SelectedIndexChanged += Picker_SelectedIndexChanged;
        }

        // (Опціонально) Обробник зміни вибраного елементу в будь-якому Picker
        // private void Picker_SelectedIndexChanged(object sender, EventArgs e)
        // {
        //     Debug.WriteLine($"Picker {((Picker)sender).AutomationId ?? (sender == rolePicker ? "Role" : "Club")} SelectedIndexChanged: {((Picker)sender).SelectedItem}");
        // }


        // Метод для завантаження даних менеджера, ролей та клубів з сервера
        private async Task LoadManagerData(int managerId)
        {
            // Показуємо індикатор та блокуємо UI
            activityIndicator.IsRunning = true;
            activityIndicator.IsVisible = true;
            Content.IsEnabled = false; // Блокуємо основний вміст сторінки

            try
            {
                // Створюємо запит для сервера - запитуємо деталі менеджера зі списками
                var requestData = new Dictionary<string, object>
                {
                    { "action", "get_manager_details" }, // Унікальна дія для сервера
                    { "manager_id", managerId }
                };

                Debug.WriteLine($"[ManagerDetailPage] Sending load request: {JsonConvert.SerializeObject(requestData)}");

                // --- ВИКЛИК API КЛІЄНТА ---
                Dictionary<string, object> response = await ApiClient.SendRequestAsync(requestData);
                // ApiClient.SendRequestAsync має повертати Dictionary<string, object>
                Debug.WriteLine($"[ManagerDetailPage] Received load response: {JsonConvert.SerializeObject(response)}");
                // --- КІНЕЦЬ ВИКЛИКУ API КЛІЄНТА ---

                // 4. Обробка відповіді сервера
                // ApiClient тепер гарантовано повертає Dictionary<string, object> з полем "success"
                bool success = response.TryGetValue("success", out object successObj) && bool.TryParse(successObj?.ToString(), out bool parsedSuccess) && parsedSuccess;
                string message = response.TryGetValue("message", out object messageObj) ? messageObj?.ToString() : "Невідоме повідомлення від сервера.";


                if (success)
                {
                    // Дані успішно завантажено
                    if (response.TryGetValue("data", out object dataObj) && dataObj is JObject managerJObject)
                    {
                        try
                        {
                            // Десеріалізуємо JObject у наш клас ManagerDetailsResponseData
                            _originalManagerData = managerJObject.ToObject<ManagerDetailsResponseData>();

                            if (_originalManagerData != null)
                            {
                                // Зберігаємо списки та встановлюємо джерело даних для Picker
                                _availableRoles = _originalManagerData.AvailableRoles;
                                _availableClubs = _originalManagerData.AvailableClubs;

                                if (_availableRoles != null)
                                {
                                    rolePicker.ItemsSource = _availableRoles;
                                    // Знаходимо об'єкт Role за його ID та встановлюємо його як SelectedItem
                                    var currentRole = _availableRoles.FirstOrDefault(r => r.Id == _originalManagerData.CurrentRoleId);
                                    if (currentRole != null)
                                    {
                                        rolePicker.SelectedItem = currentRole;
                                    }
                                    else
                                    {
                                        Debug.WriteLine($"[ManagerDetailPage] Warning: Could not find current role with ID {_originalManagerData.CurrentRoleId} in available list.");
                                        // Опціонально: встановити перший елемент або вивести помилку
                                    }
                                }
                                else
                                {
                                    Debug.WriteLine("[ManagerDetailPage] Warning: available_roles list is null.");
                                }


                                if (_availableClubs != null)
                                {
                                    clubPicker.ItemsSource = _availableClubs;
                                    // Знаходимо об'єкт Club за його ID та встановлюємо його як SelectedItem
                                    var currentClub = _availableClubs.FirstOrDefault(c => c.Id == _originalManagerData.CurrentClubId);
                                    if (currentClub != null)
                                    {
                                        clubPicker.SelectedItem = currentClub;
                                    }
                                    else
                                    {
                                        Debug.WriteLine($"[ManagerDetailPage] Warning: Could not find current club with ID {_originalManagerData.CurrentClubId} in available list.");
                                        // Опціонально: встановити перший елемент або вивести помилку
                                    }
                                }
                                else
                                {
                                    Debug.WriteLine("[ManagerDetailPage] Warning: available_clubs list is null.");
                                }


                                // Відображаємо дані менеджера в полях
                                nameEntry.Text = _originalManagerData.Name;
                                phoneEntry.Text = _originalManagerData.PhoneNumber;


                                Debug.WriteLine("[ManagerDetailPage] Manager details loaded and UI updated successfully.");
                            }
                            else
                            {
                                Debug.WriteLine("[ManagerDetailPage] Failed to deserialize manager details: toObject returned null.");
                                await DisplayAlert("Помилка", "Не вдалося обробити дані менеджера з сервера (десеріалізація повернула null).", "OK");
                                // При помилці десеріалізації краще закрити сторінку, бо немає з чим працювати
                                await Navigation.PopAsync();
                            }
                        }
                        catch (JsonException jsonEx)
                        {
                            Debug.WriteLine($"[ManagerDetailPage] JSON Deserialization Error from JObject: {jsonEx.Message}.");
                            await DisplayAlert("Помилка", $"Помилка при обробці даних менеджера: {jsonEx.Message}", "OK");
                            await Navigation.PopAsync(); // Закрити сторінку
                        }
                        catch (Exception innerEx)
                        {
                            Debug.WriteLine($"[ManagerDetailPage] Unexpected error processing manager JObject: {innerEx.Message}. StackTrace: {innerEx.StackTrace}");
                            await DisplayAlert("Помилка", $"Виникла непередбачена помилка при обробці даних менеджера: {innerEx.Message}", "OK");
                            await Navigation.PopAsync(); // Закрити сторінку
                        }
                    }
                    else
                    {
                        Debug.WriteLine($"[ManagerDetailPage] Response 'data' field is missing or not JObject. Actual Type: {dataObj?.GetType().Name ?? "null"}.");
                        string msg = response.TryGetValue("message", out object msgObj) ? msgObj?.ToString() : "Невірний формат даних менеджера з сервера (відсутнє поле 'data' або невірний тип).";
                        await DisplayAlert("Помилка завантаження", msg, "OK");
                        await Navigation.PopAsync(); // Закрити сторінку
                    }
                }
                else
                {
                    // Сервер повернув помилку або помилка ApiClient
                    Debug.WriteLine($"[ManagerDetailPage] Server returned error: {message}");
                    await DisplayAlert("Помилка завантаження", message, "OK");
                    // Якщо не вдалося завантажити дані, немає сенсу залишатись на сторінці
                    await Navigation.PopAsync();
                }
            }
            catch (Exception ex) // Помилка на рівні мережі або обробки
            {
                Debug.WriteLine($"[ManagerDetailPage] Unexpected Exception during LoadManagerData: {ex.Message}. StackTrace: {ex.StackTrace}");
                // ApiClient має ловити більшість мережевих помилок і повертати їх у Dictionary
                // Але цей catch на всякий випадок.
                await DisplayAlert("Помилка", $"Виникла непередбачена помилка при завантаженні даних: {ex.Message}", "OK");
                await Navigation.PopAsync(); // Закрити сторінку
            }
            finally
            {
                // Приховуємо індикатор та вмикаємо UI (якщо сторінка ще не закрита)
                activityIndicator.IsRunning = false;
                activityIndicator.IsVisible = false;
                Content.IsEnabled = true;
            }
        }

        // Обробник натискання кнопки "Зберегти"
        private async void OnSaveClicked(object sender, EventArgs e)
        {
            // Перевіряємо, чи дані менеджера та списки були успішно завантажені раніше
            if (_originalManagerData == null || _availableRoles == null || _availableClubs == null)
            {
                await DisplayAlert("Помилка", "Дані ще не завантажені повністю або сталася помилка завантаження.", "OK");
                return;
            }

            // Отримуємо оновлені дані з полів вводу та Picker
            string updatedName = nameEntry.Text?.Trim();
            string updatedPhone = phoneEntry.Text?.Trim();
            // Отримуємо вибрані об'єкти з Picker. SelectedItem повертає null, якщо нічого не вибрано.
            Role selectedRole = rolePicker.SelectedItem as Role;
            Club selectedClub = clubPicker.SelectedItem as Club;

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
            if (selectedRole == null)
            {
                await DisplayAlert("Помилка збереження", "Будь ласка, виберіть роль.", "OK");
                return;
            }
            if (selectedClub == null)
            {
                await DisplayAlert("Помилка збереження", "Будь ласка, виберіть клуб.", "OK");
                return;
            }
            // TODO: Додати більш детальну валідацію формату номеру телефону, якщо потрібно.

            // Перевіряємо, чи дані дійсно змінились перед відправкою на сервер
            // Порівнюємо поточні значення з тими, що були завантажені (_originalManagerData)
            bool isDataChanged = updatedName != _originalManagerData.Name ||
                                 updatedPhone != _originalManagerData.PhoneNumber ||
                                 selectedRole.Id != _originalManagerData.CurrentRoleId ||
                                 selectedClub.Id != _originalManagerData.CurrentClubId;

            if (!isDataChanged)
            {
                await DisplayAlert("Збереження", "Дані не були змінені.", "OK");
                return;
            }

            // Показуємо індикатор збереження
            saveButton.IsEnabled = false; // Вимикаємо кнопку "Зберегти"
            activityIndicator.IsRunning = true;
            activityIndicator.IsVisible = true;
            Content.IsEnabled = false; // Блокуємо основний вміст сторінки

            try
            {
                // Створюємо запит для сервера - надсилаємо оновлені дані
                var requestData = new Dictionary<string, object>
                {
                    { "action", "update_manager_details" }, // Унікальна дія для сервера
                    { "manager_id", _managerId },
                    { "name", updatedName },
                    { "phone_number", updatedPhone },
                    { "role_id", selectedRole.Id }, // Надсилаємо ID вибраної ролі
                    { "club_id", selectedClub.Id }  // Надсилаємо ID вибраного клубу
                };

                Debug.WriteLine($"[ManagerDetailPage] Sending update request: {JsonConvert.SerializeObject(requestData)}");

                // --- ВИКЛИК API КЛІЄНТА ---
                Dictionary<string, object> response = await ApiClient.SendRequestAsync(requestData);
                Debug.WriteLine($"[ManagerDetailPage] Received update response: {JsonConvert.SerializeObject(response)}");
                // --- КІНЕЦЬ ВИКЛИКУ API КЛІЄНТА ---

                // 4. Обробка відповіді сервера
                bool success = response.TryGetValue("success", out object successObj) && bool.TryParse(successObj?.ToString(), out bool parsedSuccess) && parsedSuccess;
                string message = response.TryGetValue("message", out object messageObj) ? messageObj?.ToString() : "Невідоме повідомлення від сервера.";


                if (success)
                {
                    // Оновлення успішне
                    Debug.WriteLine($"[ManagerDetailPage] Update successful: {message}");
                    await DisplayAlert("Успіх", message, "OK");

                    // Оновлюємо локальні дані (_originalManagerData), щоб вони відображали збережений стан
                    // Це важливо для коректної роботи перевірки isDataChanged при повторному натисканні "Зберегти"
                    _originalManagerData.Name = updatedName;
                    _originalManagerData.PhoneNumber = updatedPhone;
                    _originalManagerData.CurrentRoleId = selectedRole.Id;
                    _originalManagerData.CurrentClubId = selectedClub.Id;
                    // Списки available_roles та available_clubs зазвичай не змінюються при збереженні менеджера, тому їх оновлювати не потрібно.

                    // Можливо, після збереження автоматично повернутись на попередню сторінку зі списком:
                    // await Navigation.PopAsync();

                    // Якщо потрібно оновити дані на сторінці зі списком після повернення,
                    // це можна зробити за допомогою MessagingCenter або події.
                }
                else
                {
                    // Сервер повернув помилку або помилка ApiClient
                    Debug.WriteLine($"[ManagerDetailPage] Update failed: {message}");
                    await DisplayAlert("Помилка збереження", message, "OK");
                }
            }
            catch (Exception ex)
            {
                // Помилка на рівні мережі або обробки
                Debug.WriteLine($"[ManagerDetailPage] Unexpected Exception during OnSaveClicked: {ex.Message}. StackTrace: {ex.StackTrace}");
                // ApiClient має ловити більшість мережевих помилок і повертати їх у Dictionary
                // Але цей catch на всякий випадок.
                await DisplayAlert("Помилка", $"Виникла непередбачена помилка при збереженні даних: {ex.Message}", "OK");
            }
            finally
            {
                // Приховуємо індикатор та вмикаємо UI
                saveButton.IsEnabled = true; // Вмикаємо кнопку "Зберегти"
                activityIndicator.IsRunning = false;
                activityIndicator.IsVisible = false;
                Content.IsEnabled = true; // Вмикаємо основний вміст сторінки
            }
        }

        // Метод OnStartWorkClicked відсутній, як було запрошено.
    }
}