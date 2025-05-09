// crmV1/Views/ClientSearchPage.xaml.cs
using System;
using System.Collections.Generic;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;
using crmV1.Services;
using crmV1.Models;
using System.Threading.Tasks;
using System.Diagnostics; // Для Debug.WriteLine
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Linq;

namespace crmV1.Views
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class ClientSearchPage : ContentPage
    {
        public ClientSearchPage()
        {
            InitializeComponent();
            NavigationPage.SetHasNavigationBar(this, false);
            searchResultsStack.IsVisible = false;
            clientInfoStack.IsVisible = false;
            sessionsStack.IsVisible = false;
            notFoundMessageLabel.IsVisible = false;
            BindingContext = null;
        }

        private async void OnSearchButtonClicked(object sender, EventArgs e)
        {
            string phoneNumber = phoneNumberEntry.Text?.Trim();

            clientInfoStack.IsVisible = false;
            sessionsStack.IsVisible = false;
            notFoundMessageLabel.IsVisible = false;
            BindingContext = null; // Очистити BindingContext
            // sessionsListView.ItemsSource = null; Очистити ItemsSource ListView

            searchResultsStack.IsVisible = true;

            if (string.IsNullOrEmpty(phoneNumber))
            {
                await DisplayAlert("Помилка вводу", "Будь ласка, введіть номер телефону для пошуку.", "OK");
                searchResultsStack.IsVisible = false;
                return;
            }

            loadingIndicator.IsVisible = true;
            loadingIndicator.IsRunning = true;
            searchButton.IsEnabled = false;
            phoneNumberEntry.IsEnabled = false;

            try
            {
                var requestData = new Dictionary<string, object>
                {
                    { "action", "search_client_by_phone" },
                    { "phone_number", phoneNumber }
                };

                Debug.WriteLine($"[ClientSearchPage] Sending search request for phone: {phoneNumber}");
                var response = await ApiClient.SendRequestAsync(requestData);
                Debug.WriteLine($"[ClientSearchPage] Received search response. Success: {response?["success"]}");

                if (response != null && response.ContainsKey("success") && (bool)response["success"])
                {
                    if (response.TryGetValue("client", out object clientObject) && clientObject != null)
                    {
                        Debug.WriteLine("[ClientSearchPage] 'client' object found in response.");
                        Client client = null;
                        if (clientObject is JObject clientJObject)
                        {
                            client = clientJObject.ToObject<Client>();
                        }
                        else
                        {
                            // Залишимо цю гілку, але з логів видно, що приходить JObject, тому ця гілка не повинна виконуватись при успіху
                            try
                            {
                                string clientJson = JsonConvert.SerializeObject(clientObject);
                                client = JsonConvert.DeserializeObject<Client>(clientJson);
                                Debug.WriteLine($"[ClientSearchPage] Successfully deserialized client from non-JObject type.");
                            }
                            catch (Exception deserEx)
                            {
                                Debug.WriteLine($"[ClientSearchPage] Failed to deserialize client from non-JObject type: {deserEx.Message}");
                            }
                        }

                        if (client != null)
                        {
                            Debug.WriteLine($"[ClientSearchPage] Successfully deserialized client: {client.Name}");

                            // --- ДОДАНО: Рядки для налагодження стану сесій ---
                            Debug.WriteLine($"[ClientSearchPage] Deserialized client object state:");
                            Debug.WriteLine($"[ClientSearchPage]   Name: {client.Name}");
                            Debug.WriteLine($"[ClientSearchPage]   PhoneNumber: {client.PhoneNumber}");
                            Debug.WriteLine($"[ClientSearchPage]   Email: {client.Email}");
                            Debug.WriteLine($"[ClientSearchPage]   DateOfBirth: {client.DateOfBirth}");
                            Debug.WriteLine($"[ClientSearchPage]   RegistrationDate: {client.RegistrationDate}");
                            Debug.WriteLine($"[ClientSearchPage]   Sessions list is null? {client.Sessions == null}");
                            if (client.Sessions != null)
                            {
                                Debug.WriteLine($"[ClientSearchPage]   Sessions count: {client.Sessions.Count}");
                                if (client.Sessions.Count > 0)
                                {
                                    Debug.WriteLine($"[ClientSearchPage]   First session date: {client.Sessions[0].SessionDate}, type: {client.Sessions[0].SessionType}");
                                }
                            }
                            Debug.WriteLine($"[ClientSearchPage]   Calculated HasSessions property: {client.HasSessions}");
                            // --- КІНЕЦЬ НАЛАГОДЖУВАЛЬНИХ РЯДКІВ ---


                            BindingContext = client; // Встановлюємо BindingContext

                            clientInfoStack.IsVisible = true; // Показуємо інфо про клієнта

                            // Видимість sessionsStack та ItemsSource ListView тепер контролюються Binding в XAML
                            sessionsStack.IsVisible = client.HasSessions; // НЕ потрібний тут
                            sessionsListView.ItemsSource = client.Sessions; // НЕ потрібний тут


                            notFoundMessageLabel.IsVisible = false;

                        }
                        else
                        {
                            Debug.WriteLine("[ClientSearchPage] Client object found in response, but failed to deserialize.");
                            notFoundMessageLabel.Text = "Помилка обробки даних клієнта з сервера.";
                            notFoundMessageLabel.IsVisible = true;
                            clientInfoStack.IsVisible = false;
                            sessionsStack.IsVisible = false;
                            BindingContext = null;
                            sessionsListView.ItemsSource = null;
                        }

                    }
                    else // success=true, but client is null or missing
                    {
                        string message = response.ContainsKey("message") ? response["message"].ToString() : "Клієнта з таким номером не знайдено.";
                        Debug.WriteLine($"[ClientSearchPage] Client not found. Message: {message}");
                        notFoundMessageLabel.Text = message;
                        notFoundMessageLabel.IsVisible = true;
                        clientInfoStack.IsVisible = false;
                        sessionsStack.IsVisible = false;
                        BindingContext = null;
                        sessionsListView.ItemsSource = null;
                    }
                }
                else // success=false
                {
                    string errorMessage = response != null && response.ContainsKey("message") ? response["message"].ToString() : "Невідома помилка сервера.";
                    Debug.WriteLine($"[ClientSearchPage] API error response (success=false). Message: {errorMessage}");
                    await DisplayAlert("Помилка сервера", $"Не вдалося виконати пошук: {errorMessage}", "OK");
                    notFoundMessageLabel.IsVisible = false; // Ховаємо повідомлення "не знайдено" при помилці сервера
                    clientInfoStack.IsVisible = false;
                    sessionsStack.IsVisible = false;
                    BindingContext = null;
                    sessionsListView.ItemsSource = null;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[КРИТИЧНА ПОМИЛКА] Client search failed with exception: {ex.GetType().Name} - {ex.Message}\n{ex.StackTrace}");
                await DisplayAlert("Помилка", $"Сталася помилка під час пошуку: {ex.Message}", "OK");

                notFoundMessageLabel.Text = $"Сталася помилка: {ex.Message}";
                notFoundMessageLabel.IsVisible = true; // Показуємо це повідомлення
                clientInfoStack.IsVisible = false;
                sessionsStack.IsVisible = false;
                BindingContext = null;
            }
            finally
            {
                loadingIndicator.IsVisible = false;
                loadingIndicator.IsRunning = false;
                searchButton.IsEnabled = true;
                phoneNumberEntry.IsEnabled = true;

                // Ховаємо загальний контейнер результатів, якщо нічого не відображається
                if (!clientInfoStack.IsVisible && !notFoundMessageLabel.IsVisible)
                {
                    searchResultsStack.IsVisible = false;
                }
                Debug.WriteLine("[ClientSearchPage] Search process finished.");
            }
        }
    }
    public class Client
    {
        [JsonProperty("client_id")]
        public int ClientId { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("phone_number")]
        public string PhoneNumber { get; set; }

        [JsonProperty("email")]
        public string Email { get; set; }

        [JsonProperty("birthday")]
        public DateTime DateOfBirth { get; set; }

        [JsonProperty("created_at")]
        public DateTime RegistrationDate { get; set; }

        [JsonProperty("sessions")]
        // Забезпечуємо, що Sessions ніколи не буде null, ініціалізуючи його порожнім списком
        public List<ClientSession> Sessions { get; set; } = new List<ClientSession>();

        // Розраховувана властивість: Вік клієнта
        [JsonIgnore]
        public int Age
        {
            get
            {
                if (DateOfBirth == default(DateTime) || DateOfBirth == DateTime.MinValue)
                {
                    return 0; // Якщо дата народження не встановлена, вік 0
                }
                int age = DateTime.Today.Year - DateOfBirth.Year;
                if (DateOfBirth.Date > DateTime.Today.AddYears(-age).Date)
                {
                    age--;
                }
                return Math.Max(0, age);
            }
        }

        // Розраховувана властивість: Років в клубі
        [JsonIgnore]
        public int YearsInClub
        {
            get
            {
                if (RegistrationDate == default(DateTime) || RegistrationDate == DateTime.MinValue)
                {
                    return 0; // Якщо дата реєстрації не встановлена, років в клубі 0
                }

                int years = DateTime.Today.Year - RegistrationDate.Year;

                if (RegistrationDate.Date > DateTime.Today.AddYears(-years).Date)
                {
                    years--;
                }
                return Math.Max(0, years);
            }
        }

        // --- НОВІ ВЛАСТИВОСТІ ДЛЯ ВІДОБРАЖЕННЯ З ОБРОБКОЮ "НЕВІДОМО" ---

        [JsonIgnore]
        public string DisplayName => string.IsNullOrWhiteSpace(Name) ? "Невідомо" : Name;

        [JsonIgnore]
        public string DisplayPhoneNumber => string.IsNullOrWhiteSpace(PhoneNumber) ? "Невідомо" : PhoneNumber;

        [JsonIgnore]
        public string DisplayEmail => string.IsNullOrWhiteSpace(Email) ? "Невідомо" : Email;

        [JsonIgnore]
        public string DisplayDateOfBirthAndAge
        {
            get
            {
                // Перевіряємо, чи дата має значення за замовчуванням
                if (DateOfBirth == default(DateTime) || DateOfBirth == DateTime.MinValue)
                {
                    return "Невідомо";
                }
                // Якщо дата є, відображаємо дату та вік
                return $"{DateOfBirth:dd.MM.yyyy} ({Age} р.)";
            }
        }

        [JsonIgnore]
        public string DisplayRegistrationDateAndYearsInClub
        {
            get
            {
                // Перевіряємо, чи дата має значення за замовчуванням
                if (RegistrationDate == default(DateTime) || RegistrationDate == DateTime.MinValue)
                {
                    return "Невідомо";
                }
                // Якщо дата є, відображаємо дату та роки в клубі
                // Можливо, варто відображати саму дату реєстрації, а не "Років в клубі"?
                // Якщо потрібно відображати "В клубі з [дата]", тоді:
                return $"{RegistrationDate:dd.MM.yyyy} ({YearsInClub} р. в клубі)";
                // Якщо потрібно відображати тільки "Років в клубі: [кількість]", тоді:
                // return $"{YearsInClub} р. в клубі"; // Цей варіант простіший для XAML, використовуйте StringFormat
            }
        }

        // Додаткова властивість, щоб перевіряти, чи є сесії для відображення секції
        [JsonIgnore]
        public bool HasSessions => Sessions != null && Sessions.Any();

        // Додаткова властивість для відображення "В клубі з:" або "Років в клубі:"
        [JsonIgnore]
        public string DisplayYearsInClubString
        {
            get
            {
                if (RegistrationDate == default(DateTime) || RegistrationDate == DateTime.MinValue)
                {
                    return "Невідомо";
                }
                // Можна повернути тільки розраховані роки для StringFormat в XAML
                return $"{YearsInClub}"; // Повертаємо тільки число років як рядок
            }
        }
    }
    public class ClientSession
    {
        // Відповідає колонці 'session_date'
        [JsonProperty("session_date")]
        public DateTime SessionDate { get; set; }

        // Відповідає колонці 'session_type'
        [JsonProperty("session_type")]
        public string SessionType { get; set; }

        // Допоміжна властивість для форматованого відображення дати в UI
        [JsonIgnore] // Не потрібно серіалізувати назад на сервер
        public string FormattedDate => SessionDate.ToString("dd.MM.yyyy");
    }
}