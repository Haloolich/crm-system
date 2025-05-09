using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;
using crmV1.Models; // !!! Переконайтесь, що цей using вказує на правильний простір імен з класом Club !!!
using crmV1.Services;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json; // Для роботи з JArray, JObject

namespace crmV1
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class ClubsPage : ContentPage
    {
        public ObservableCollection<Club> ClubsList { get; set; }

        public ClubsPage()
        {
            InitializeComponent();
            NavigationPage.SetHasNavigationBar(this, false);
            Title = "Клуби";

            ClubsList = new ObservableCollection<Club>();
            clubsListView.ItemsSource = ClubsList;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            Debug.WriteLine("[ClubsPage] OnAppearing called. Loading clubs...");
            await LoadClubsAsync();
        }

        /// <summary>
        /// Завантажує список клубів з сервера та оновлює ListView.
        /// </summary>
        private async Task LoadClubsAsync()
        {
            loadingIndicator.IsVisible = true;
            loadingIndicator.IsRunning = true;
            ClubsList.Clear(); // Очищаємо список перед завантаженням

            try
            {
                var requestData = new Dictionary<string, object>
                {
                    { "action", "get_all_clubs" }
                };

                Debug.WriteLine("[ClubsPage] Sending get_all_clubs request...");
                var response = await ApiClient.SendRequestAsync(requestData);
                Debug.WriteLine($"[ClubsPage] Received response for get_all_clubs: {response?.GetType().Name ?? "null"}");


                if (response != null && response.ContainsKey("success"))
                {
                    bool isSuccess = false;
                    try { isSuccess = Convert.ToBoolean(response["success"]); } catch { }

                    if (isSuccess)
                    {
                        // !!! ВИПРАВЛЕННЯ: Більш надійна перевірка наявності та типу поля "clubs" !!!
                        if (response.TryGetValue("clubs", out object clubsObject))
                        {
                            if (clubsObject is JArray clubsJArray)
                            {
                                Debug.WriteLine($"[ClubsPage] Received {clubsJArray.Count} club items.");
                                // Перетворюємо кожен JObject на об'єкт Club та додаємо до колекції
                                foreach (var clubToken in clubsJArray)
                                {
                                    try
                                    {
                                        // Використовуємо Club, тому клас Club має бути доступний
                                        var club = clubToken.ToObject<Club>();
                                        if (club != null)
                                        {
                                            ClubsList.Add(club);
                                            Debug.WriteLine($"[ClubsPage] Added club: {club.Name}");
                                        }
                                        else
                                        {
                                            Debug.WriteLine("[ClubsPage] Failed to deserialize a club object from one item.");
                                        }
                                    }
                                    catch (Exception deserializeEx)
                                    {
                                        Debug.WriteLine($"[ClubsPage] Error deserializing club object: {deserializeEx.Message}");
                                    }
                                }
                                Debug.WriteLine($"[ClubsPage] Finished loading {ClubsList.Count} clubs into collection.");
                            }
                            else if (clubsObject == null)
                            {
                                // Сервер повернув success=true, але поле "clubs" є null.
                                // Це може означати, що клубів немає. Це не помилка формату.
                                Debug.WriteLine("[ClubsPage] Server response success=true, 'clubs' key is null. Assuming no clubs found.");
                                // Можна показати повідомлення "Клуби відсутні" або залишити список порожнім
                            }
                            else
                            {
                                // Сервер повернув success=true, але поле "clubs" має неочікуваний тип.
                                Debug.WriteLine($"[ClubsPage] Server response success=true, but 'clubs' is unexpected type: {clubsObject.GetType().Name}.");
                                await DisplayAlert("Помилка даних", "Сервер повернув неочікуваний формат списку клубів.", "OK");
                            }
                        }
                        else
                        {
                            // Сервер повернув success=true, але ключ "clubs" повністю відсутній.
                            // Це також може означати, що клубів немає, або це помилка контракту API.
                            // Припускаємо, що якщо success=true, але ключа "clubs" немає, то клубів просто немає.
                            Debug.WriteLine("[ClubsPage] Server response success=true, but missing 'clubs' key. Assuming no clubs found.");
                        }
                    }
                    else
                    {
                        // Сервер повідомив про помилку (success=false)
                        string errorMessage = response.ContainsKey("message") ? response["message"].ToString() : "Не вдалося отримати список клубів (невідома причина).";
                        Debug.WriteLine($"[ClubsPage] Server reported error: {errorMessage}");
                        await DisplayAlert("Помилка сервера", errorMessage, "OK");
                    }
                }
                else
                {
                    // Неочікуваний формат відповіді сервера (відсутнє поле "success")
                    Debug.WriteLine("[ClubsPage] Server response has unexpected format (missing 'success' key).");
                    await DisplayAlert("Помилка", "Неочікуваний формат відповіді від сервера.", "OK");
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ClubsPage] Critical error loading clubs: {ex.Message}\n{ex.StackTrace}");
                await DisplayAlert("Критична помилка", $"Не вдалося завантажити клуби: {ex.Message}", "OK");
            }
            finally
            {
                loadingIndicator.IsVisible = false;
                loadingIndicator.IsRunning = false;
            }
        }

        // Обробник натискання кнопки "Додати новий клуб"
        private async void OnAddClubClicked(object sender, EventArgs e)
        {
            Debug.WriteLine("[ClubsPage] OnAddClubClicked called.");
            await Navigation.PushAsync(new AddClubPage());
        }

        // Обробник натискання кнопки "Видалити" для конкретного клубу
        private async void OnDeleteClicked(object sender, EventArgs e)
        {
            // CommandParameter - це об'єкт Club. Використовуємо Club.
            if (sender is Button button && button.CommandParameter is Club club)
            {
                Debug.WriteLine($"[ClubsPage] OnDeleteClicked called for club: {club.Name} (ID: {club.ClubId})");

                bool confirmed = await DisplayAlert("Підтвердження видалення", $"Ви впевнені, що хочете видалити клуб \"{club.Name}\"?", "Так", "Скасувати");

                if (confirmed)
                {
                    loadingIndicator.IsVisible = true;
                    loadingIndicator.IsRunning = true;

                    try
                    {
                        var requestData = new Dictionary<string, object>
                        {
                            { "action", "delete_club" },
                            { "club_id", club.ClubId }
                        };

                        Debug.WriteLine($"[ClubsPage] Sending delete_club request for ID: {club.ClubId}...");
                        var response = await ApiClient.SendRequestAsync(requestData);
                        Debug.WriteLine($"[ClubsPage] Received response for delete_club: {response?.GetType().Name ?? "null"}");

                        if (response != null && response.ContainsKey("success"))
                        {
                            bool isSuccess = false;
                            try { isSuccess = Convert.ToBoolean(response["success"]); } catch { }

                            if (isSuccess)
                            {
                                Debug.WriteLine($"[ClubsPage] Club {club.ClubId} deleted successfully by server.");
                                string successMessage = response.ContainsKey("message") ? response["message"].ToString() : "Клуб успішно видалено.";
                                await DisplayAlert("Успіх", successMessage, "OK");

                                // Видаляємо клуб з локальної колекції, щоб оновити UI
                                ClubsList.Remove(club);
                                Debug.WriteLine($"[ClubsPage] Removed club {club.ClubId} from local list.");
                            }
                            else
                            {
                                string errorMessage = response.ContainsKey("message") ? response["message"].ToString() : "Не вдалося видалити клуб (невідома причина).";
                                Debug.WriteLine($"[ClubsPage] Server reported delete error: {errorMessage}");
                                await DisplayAlert("Помилка сервера", errorMessage, "OK");
                            }
                        }
                        else
                        {
                            Debug.WriteLine("[ClubsPage] Server delete response has unexpected format.");
                            await DisplayAlert("Помилка", "Неочікуваний формат відповіді від сервера.", "OK");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ClubsPage] Critical error deleting club {club.ClubId}: {ex.Message}\n{ex.StackTrace}");
                        await DisplayAlert("Критична помилка", $"Не вдалося видалити клуб: {ex.Message}", "OK");
                    }
                    finally
                    {
                        loadingIndicator.IsVisible = false;
                        loadingIndicator.IsRunning = false;
                    }
                }
                else
                {
                    Debug.WriteLine($"[ClubsPage] Delete for club {club.ClubId} cancelled by user.");
                }
            }
            else
            {
                Debug.WriteLine("[ClubsPage] OnDeleteClicked: CommandParameter is not a Club object.");
            }
        }

        // Обробник натискання кнопки "Детальніше" для конкретного клубу
        private async void OnDetailsClicked(object sender, EventArgs e)
        {
            // CommandParameter - це об'єкт Club. Використовуємо Club.
            if (sender is Button button && button.CommandParameter is Club club)
            {
                Debug.WriteLine($"[ClubsPage] OnDetailsClicked called for club: {club.Name} (ID: {club.ClubId})");
                // Переходимо на сторінку деталей/редагування, передаючи ID клубу
                await Navigation.PushAsync(new ClubDetailsPage(club.ClubId));
            }
            else
            {
                Debug.WriteLine("[ClubsPage] OnDetailsClicked: CommandParameter is not a Club object.");
            }
        }
    }
    public class Club
    {
        [JsonProperty("club_id")]
        public int ClubId { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("address")]
        public string Address { get; set; }

        [JsonProperty("phone_number")]
        public string PhoneNumber { get; set; }

        [JsonProperty("email")]
        public string Email { get; set; }

        [JsonProperty("created_at")]
        public DateTime CreatedAt { get; set; }

        [JsonProperty("max_ps_zones")]
        public int MaxPsZones { get; set; }

        [JsonProperty("max_vr_quest_zones")]
        public int MaxVrQuestZones { get; set; }

        [JsonProperty("status")] // Поле статусу
        public string Status { get; set; } = "Open"; // Встановлюємо значення за замовчуванням

        // Конструктор за замовчуванням потрібен для десеріалізації JSON
        public Club() { }

        // Конструктор для створення нового об'єкта перед відправкою на сервер
        public Club(string name, string address, string phoneNumber, string email, int maxPsZones, int maxVrQuestZones, string status = "Open")
        {
            Name = name;
            Address = address;
            PhoneNumber = phoneNumber;
            Email = email;
            MaxPsZones = maxPsZones;
            MaxVrQuestZones = maxVrQuestZones;
            Status = status;
            // ClubId та CreatedAt встановлюються сервером
        }

        // Можливо, вам знадобиться перевизначити ToString() для зручного дебагу або відображення
        public override string ToString()
        {
            return $"Club [ID={ClubId}, Name={Name}, Status={Status}, PS Zones={MaxPsZones}, VR Zones={MaxVrQuestZones}]";
        }
    }
    public class ClubService
    {
        // Приклад методу GetClubsAsync. ВІН ПОВИНЕН ВИКЛИКАТИ ВАШ APIClient.
        // Цей код - просто приклад структури, НЕ ПРАЦЮЮЧИЙ ВИКЛИК API.
        public async Task<List<Club>> GetClubsAsync()
        {
            Debug.WriteLine("[ClubService] Calling GetClubsAsync...");
            var requestData = new Dictionary<string, object>
             {
                 { "action", "get_all_clubs" }
             };

            // TODO: ЗАМІНІТЬ ЦЕ НА РЕАЛЬНИЙ ВИКЛИК ВАШОГО APIClient
            // Dictionary<string, object> response = await ApiClient.SendRequestAsync(requestData);
            // ... Обробка відповіді, перевірка success, десеріалізація data -> List<Club> ...

            // Приклад повернення фейкових даних, якщо у вас ще немає реального виклику API
            await Task.Delay(100); // Імітація затримки мережі
            var fakeClubsJson = @"
                {
                  'success': true,
                  'message': 'Список клубів завантажено (фейк).',
                  'clubs': [
                    { 'club_id': 1, 'name': 'Fake Club A' },
                    { 'club_id': 2, 'name': 'Fake Club B' }
                  ]
                }";

            try
            {
                var response = JsonConvert.DeserializeObject<Dictionary<string, object>>(fakeClubsJson);
                if (response != null && response.TryGetValue("success", out object successObj) && Convert.ToBoolean(successObj))
                {
                    if (response.TryGetValue("clubs", out object clubsObj) && clubsObj is JArray clubsJArray)
                    {
                        var clubsList = clubsJArray.ToObject<List<Club>>();
                        Debug.WriteLine($"[ClubService] Successfully loaded {clubsList?.Count} fake clubs.");
                        return clubsList ?? new List<Club>(); // Повертаємо список або порожній список
                    }
                }
                Debug.WriteLine("[ClubService] Failed to load fake clubs data.");
                return new List<Club>();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ClubService] Error processing fake clubs data: {ex.Message}");
                return new List<Club>();
            }
            // --- КІНЕЦЬ ПРИКЛАДУ-ЗАГЛУШКИ ---
        }
    }
}