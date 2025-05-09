using System;
using Xamarin.Forms;
using System.Net.Sockets;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using Newtonsoft.Json.Linq; // Додано для роботи з JArray

namespace crmV1
{
    // Використовуйте існуючий клас Club, якщо він визначений в іншому місці
    // using YourNamespace.Club; // <- Якщо клас Club в іншому просторі імен

    // НОВИЙ клас моделі для елементів Picker
    public class PickerClubItem
    {
        [JsonProperty("club_id")] // Важливо: відповідає назві з сервера
        public int ClubId { get; set; }

        [JsonProperty("name")] // Важливо: відповідає назві з сервера
        public string ClubName { get; set; } // Перейменували, щоб уникнути плутанини з ToString

        // Метод ToString для відображення в Picker за замовчуванням
        public override string ToString()
        {
            return ClubName; // Повертаємо лише назву клубу
        }
    }


    public partial class RegistrationPage : ContentPage
    {
        private TcpClient _client;
        private NetworkStream _stream;
        private bool _isConnected = false;
        // Змінено: тепер зберігаємо список PickerClubItem
        private List<PickerClubItem> _clubItems;


        public RegistrationPage(TcpClient client, NetworkStream stream)
        {
            NavigationPage.SetHasNavigationBar(this, false); // Приховати NavigationBar
            InitializeComponent();
            _client = client;
            _stream = stream;
            _isConnected = true;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            if (!_isConnected)
            {
                errorMessageLabel.Text = "Немає з'єднання з сервером. Спробуйте пізніше.";
                errorMessageLabel.IsVisible = true;
                registerButton.IsEnabled = false;
                clubPicker.IsEnabled = false;
            }
            else
            {
                await LoadClubsAsync(); // Завантажуємо клуби при появі сторінки
            }
        }

        async void OnRegisterClicked(object sender, EventArgs e)
        {
            if (!_isConnected)
            {
                errorMessageLabel.Text = "Немає з'єднання з сервером.";
                errorMessageLabel.IsVisible = true;
                return;
            }

            string name = nameEntry.Text;
            string phone = phoneEntry.Text;
            string login = loginEntry.Text;
            string password = passwordEntry.Text;

            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(phone) || string.IsNullOrEmpty(login) || string.IsNullOrEmpty(password))
            {
                errorMessageLabel.Text = "Будь ласка, заповніть всі поля.";
                errorMessageLabel.IsVisible = true;
                return;
            }

            // Перевірка, чи обрано клуб
            // Змінено: перевіряємо, чи обрано PickerClubItem
            if (clubPicker.SelectedItem == null)
            {
                errorMessageLabel.Text = "Будь ласка, оберіть клуб.";
                errorMessageLabel.IsVisible = true;
                return;
            }

            // Отримуємо обраний елемент PickerClubItem та його ID
            // Змінено: приводимо до PickerClubItem
            PickerClubItem selectedClubItem = clubPicker.SelectedItem as PickerClubItem;
            int selectedClubId = selectedClubItem.ClubId; // Отримуємо ClubId з нового класу

            // Prepare the data to send to the server
            var data = new Dictionary<string, object>
            {
                { "action", "register" },
                { "name", name },
                { "phone", phone },
                { "login", login },
                { "password", password },
                { "club_id", selectedClubId } // Додаємо ID обраного клубу
            };

            await SendDataToServerAsync(data);
        }

        // Метод для завантаження списку клубів з сервера
        private async Task LoadClubsAsync()
        {
            if (!_isConnected)
            {
                errorMessageLabel.Text = "Немає з'єднання з сервером для завантаження клубів.";
                errorMessageLabel.IsVisible = true;
                return;
            }

            errorMessageLabel.IsVisible = false;
            loadingIndicator.IsRunning = true;
            loadingIndicator.IsVisible = true;
            clubPicker.IsEnabled = false;
            registerButton.IsEnabled = false;

            byte[] responseBuffer = null;
            int totalBytesRead = 0;
            string responseJson = null;

            try
            {
                // ... (код відправки запиту "get_clubs" - без змін) ...
                var requestData = new Dictionary<string, string>
                 {
                     { "action", "get_clubs" }
                 };

                string jsonData = JsonConvert.SerializeObject(requestData);
                byte[] buffer = Encoding.UTF8.GetBytes(jsonData);
                byte[] lengthBytes = BitConverter.GetBytes(buffer.Length);

                await _stream.WriteAsync(lengthBytes, 0, lengthBytes.Length);
                await _stream.WriteAsync(buffer, 0, buffer.Length);
                await _stream.FlushAsync();
                // ... (кінець коду відправки запиту) ...


                // ... (код отримання відповіді: читання довжини та даних - без змін) ...
                byte[] lengthBuffer = new byte[4];
                int bytesRead = await _stream.ReadAsync(lengthBuffer, 0, lengthBuffer.Length);
                if (bytesRead != 4)
                {
                    Console.WriteLine("Не вдалося прочитати довжину відповіді списку клубів.");
                    errorMessageLabel.Text = "Не вдалося отримати список клубів від сервера (помилка довжини).";
                    errorMessageLabel.IsVisible = true;
                    DisconnectFromServer();
                    return;
                }

                int responseLength = BitConverter.ToInt32(lengthBuffer, 0);

                if (responseLength <= 0)
                {
                    Console.WriteLine($"Отримана некоректна довжина відповіді списку клубів: {responseLength}");
                    errorMessageLabel.Text = "Отримана некоректна відповідь зі списком клубів.";
                    errorMessageLabel.IsVisible = true;
                    registerButton.IsEnabled = true;
                    return;
                }

                responseBuffer = new byte[responseLength];
                totalBytesRead = 0;

                while (totalBytesRead < responseLength)
                {
                    int readNow = await _stream.ReadAsync(responseBuffer, totalBytesRead, responseLength - totalBytesRead);
                    if (readNow == 0)
                    {
                        Console.WriteLine("Не вдалося прочитати всю відповідь списку клубів (потік закрився?).");
                        errorMessageLabel.Text = "Не вдалося отримати повний список клубів.";
                        errorMessageLabel.IsVisible = true;
                        DisconnectFromServer();
                        return;
                    }
                    totalBytesRead += readNow;
                }

                responseJson = Encoding.UTF8.GetString(responseBuffer, 0, totalBytesRead);
                Console.WriteLine($"Отримана відповідь зі списком клубів: {responseJson}");
                // ... (кінець коду отримання відповіді) ...


                // --- Змінено: Парсимо відповідь в Dictionary<string, object> та потім в List<PickerClubItem> ---
                var responseObject = JsonConvert.DeserializeObject<Dictionary<string, object>>(responseJson);

                // Перевіряємо, чи відповідь успішна і чи містить список клубів під ключем "clubs"
                if (responseObject != null &&
                    responseObject.TryGetValue("success", out object successValueObject) &&
                    successValueObject is string successString &&
                    successString == "true" &&
                    responseObject.TryGetValue("clubs", out object clubsArrayObject) && // Отримуємо значення за ключем "clubs"
                    clubsArrayObject is JArray clubsJArray) // Перевіряємо, чи це JArray (JSON масив)
                {
                    // Десеріалізуємо JArray в List<PickerClubItem>
                    // Тепер використовуємо новий клас
                    _clubItems = clubsJArray.ToObject<List<PickerClubItem>>();

                    if (_clubItems != null && _clubItems.Any())
                    {
                        // Прив'язуємо список PickerClubItem до Picker
                        clubPicker.ItemsSource = _clubItems;
                        clubPicker.IsEnabled = true; // Включаємо Picker, якщо дані завантажено
                        registerButton.IsEnabled = true; // Включаємо кнопку реєстрації
                        errorMessageLabel.IsVisible = false; // Сховати помилку

                        // Якщо в XAML є ItemDisplayBinding="Name", можна прибрати ToString() з PickerClubItem.
                        // Якщо в XAML ItemDisplayBinding="ClubName", то це вже коректно.
                        // Якщо в XAML немає ItemDisplayBinding, використовується ToString().
                        // Оскільки ми додали ItemDisplayBinding="{Binding Name}" раніше,
                        // або ItemDisplayBinding="{Binding ClubName}" після перейменування,
                        // це буде працювати. Якщо немає ItemDisplayBinding, потрібен ToString().
                    }
                    else
                    {
                        // Якщо список клубів порожній
                        errorMessageLabel.Text = "Немає доступних клубів для вибору.";
                        errorMessageLabel.IsVisible = true;
                        clubPicker.IsEnabled = false;
                        registerButton.IsEnabled = false;
                    }
                }
                else // Обробка помилки або неочікуваного формату відповіді
                {
                    // Спробуємо отримати повідомлення про помилку, якщо воно є
                    string errorMessage = responseObject != null &&
                                          responseObject.TryGetValue("message", out object msgValueObject) && msgValueObject is string msgString
                                         ? msgString
                                         : "Не вдалося отримати список клубів від сервера (неочікуваний формат або помилка).";

                    errorMessageLabel.Text = errorMessage;
                    errorMessageLabel.IsVisible = true;
                    clubPicker.IsEnabled = false;
                    registerButton.IsEnabled = false;

                    // Додаткове логування для налагодження неочікуваних відповідей
                    if (responseObject == null ||
                        !(responseObject.TryGetValue("success", out successValueObject) && successValueObject is string) ||
                        !(responseObject.TryGetValue("clubs", out clubsArrayObject) && clubsArrayObject is JArray)
                        )
                    {
                        Console.WriteLine($"Отримана неочікувана структура відповіді (відсутні 'success'/'clubs' або неправильний тип): {responseJson ?? "NULL"}");
                    }
                }
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"Помилка десеріалізації відповіді списку клубів JSON: {ex.Message}. Відповідь: {responseJson ?? "NULL"}");
                errorMessageLabel.Text = $"Помилка обробки списку клубів від сервера (некоректний JSON).";
                errorMessageLabel.IsVisible = true;
                clubPicker.IsEnabled = false;
                registerButton.IsEnabled = false;
            }
            // ... (решта catch блоків та finally - без змін) ...
            catch (IOException ex)
            {
                Console.WriteLine($"Помилка мережі при завантаженні клубів: {ex.Message}");
                errorMessageLabel.Text = $"Помилка мережі при завантаженні клубів.";
                errorMessageLabel.IsVisible = true;
                clubPicker.IsEnabled = false;
                registerButton.IsEnabled = false;
                DisconnectFromServer();
            }
            catch (SocketException ex)
            {
                Console.WriteLine($"Помилка сокету при завантаженні клубів: {ex.Message}");
                errorMessageLabel.Text = $"Помилка сокету при завантаженні клубів.";
                errorMessageLabel.IsVisible = true;
                clubPicker.IsEnabled = false;
                registerButton.IsEnabled = false;
                DisconnectFromServer();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Загальна помилка при завантаженні клубів: {ex.Message}");
                errorMessageLabel.Text = $"Помилка при завантаженні клубів: {ex.Message}";
                errorMessageLabel.IsVisible = true;
                clubPicker.IsEnabled = false;
                registerButton.IsEnabled = false;
                // DisconnectFromServer();
            }
            finally
            {
                loadingIndicator.IsRunning = false;
                loadingIndicator.IsVisible = false;
            }
        }

        // Метод для відправки даних на сервер (модифіковано для Dictionary<string, object>)
        private async Task SendDataToServerAsync(Dictionary<string, object> data)
        {
            // Оголошуємо змінні поза try/catch, щоб вони були доступні у catch
            byte[] responseBuffer = null;
            int totalBytesRead = 0;
            string responseJson = null;

            try
            {
                if (!_isConnected)
                {
                    errorMessageLabel.Text = "Немає з'єднання з сервером.";
                    errorMessageLabel.IsVisible = true;
                    return;
                }

                // JsonConvert.SerializeObject коректно обробляє Dictionary<string, object>
                string jsonData = JsonConvert.SerializeObject(data);
                Console.WriteLine($"Відправляю на сервер: {jsonData}"); // Для налагодження

                byte[] buffer = Encoding.UTF8.GetBytes(jsonData);
                byte[] lengthBytes = BitConverter.GetBytes(buffer.Length);

                // Send data length
                await _stream.WriteAsync(lengthBytes, 0, lengthBytes.Length);

                // Send JSON data
                await _stream.WriteAsync(buffer, 0, buffer.Length);
                await _stream.FlushAsync();

                // Receive response from server
                byte[] lengthBuffer = new byte[4];
                int bytesRead = await _stream.ReadAsync(lengthBuffer, 0, lengthBuffer.Length);
                if (bytesRead != 4)
                {
                    Console.WriteLine("Не вдалося прочитати довжину відповіді від сервера.");
                    errorMessageLabel.Text = "Не вдалося отримати відповідь від сервера (помилка довжини).";
                    errorMessageLabel.IsVisible = true;
                    DisconnectFromServer(); // Критична помилка протоколу
                    return;
                }

                int responseLength = BitConverter.ToInt32(lengthBuffer, 0);

                if (responseLength <= 0)
                {
                    Console.WriteLine($"Отримана некоректна довжина відповіді: {responseLength}");
                    errorMessageLabel.Text = "Отримана некоректна відповідь від сервера.";
                    errorMessageLabel.IsVisible = true;
                    // Не обов'язково роз'єднуватись
                    return;
                }

                responseBuffer = new byte[responseLength]; // Ініціалізуємо тут
                totalBytesRead = 0; // Скидаємо перед читанням

                while (totalBytesRead < responseLength)
                {
                    // Читаємо частинами, доки не прочитаємо всю відповідь
                    int readNow = await _stream.ReadAsync(responseBuffer, totalBytesRead, responseLength - totalBytesRead);
                    if (readNow == 0)
                    {
                        Console.WriteLine("Не вдалося прочитати всю відповідь від сервера (потік закрився?).");
                        errorMessageLabel.Text = "Не вдалося отримати повну відповідь від сервера.";
                        errorMessageLabel.IsVisible = true;
                        DisconnectFromServer();
                        return;
                    }
                    totalBytesRead += readNow;
                }

                responseJson = Encoding.UTF8.GetString(responseBuffer, 0, totalBytesRead);
                Console.WriteLine($"Отримана відповідь від сервера: {responseJson}"); // Для налагодження

                // Очікуємо відповідь з success та message.
                var responseObject = JsonConvert.DeserializeObject<Dictionary<string, object>>(responseJson);

                // Перевірка на успіх стала більш надійною
                // Використовуємо TryGetValue для безпечного доступу та перевірки типу
                bool isSuccess = responseObject != null &&
                                 responseObject.TryGetValue("success", out object successValueObject) && // successValueObject scoped to this expression
                                 successValueObject is string successString &&         // successString scoped to this expression
                                 successString == "true";

                if (isSuccess)
                {
                    string successMessage = responseObject.TryGetValue("message", out object msgValueObject) && msgValueObject is string msgString ? msgString : "Успіх!";
                    await DisplayAlert("Успіх", successMessage, "OK");
                    await Navigation.PopAsync(); // Повернутися назад після успішної реєстрації
                }
                else // Error case (including success != "true", missing key, or non-string value)
                {
                    // Try to get the error message if it exists and is a string
                    string errorMessage = responseObject != null &&
                                          responseObject.TryGetValue("message", out object msgValueObject) &&
                                          msgValueObject is string msgString
                                         ? msgString // Use message from server if found and is string
                                         : "Невідома помилка реєстрації."; // Default error message if 'message' key is missing or not string

                    errorMessageLabel.Text = errorMessage;
                    errorMessageLabel.IsVisible = true;

                    // --- Виправлена логіка логування неочікуваної структури ---
                    // Логуємо, якщо відповідь не null, але не містить ключ "success" АБО значення "success" не string
                    if (responseObject != null &&
                       (!responseObject.TryGetValue("success", out object successCheckValue) || !(successCheckValue is string)))
                    {
                        Console.WriteLine($"Отримана неочікувана структура відповіді (відсутній/неправильний 'success'): {responseJson ?? "NULL"}");
                    }
                    else if (responseObject == null)
                    {
                        // Логуємо окремо, якщо весь об'єкт відповіді null
                        Console.WriteLine($"Отримана неочікувана структура відповіді (відповідь null): {responseJson ?? "NULL"}");
                    }
                    // --- Кінець виправленої логіки логування ---

                }
            }
            catch (JsonException ex)
            {
                // responseJson доступний тут
                Console.WriteLine($"Помилка десеріалізації відповіді JSON: {ex.Message}. Відповідь: {responseJson ?? "NULL"}");
                errorMessageLabel.Text = $"Помилка обробки відповіді від сервера (некоректний JSON).";
                errorMessageLabel.IsVisible = true;
                // DisconnectFromServer(); // Можливо, роз'єднатись при помилці JSON
            }
            catch (IOException ex) // Помилки мережі/потоку
            {
                Console.WriteLine($"Помилка мережі при відправленні/отриманні даних: {ex.Message}");
                errorMessageLabel.Text = $"Помилка мережі: {ex.Message}";
                errorMessageLabel.IsVisible = true;
                DisconnectFromServer();
            }
            catch (SocketException ex) // Помилки сокету
            {
                Console.WriteLine($"Помилка сокету при відправленні/отриманні даних: {ex.Message}");
                errorMessageLabel.Text = $"Помилка сокету: {ex.Message}";
                errorMessageLabel.IsVisible = true;
                DisconnectFromServer();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Загальна помилка при відправленні даних на сервер: {ex.Message}");
                errorMessageLabel.Text = $"Помилка: {ex.Message}";
                errorMessageLabel.IsVisible = true;
                DisconnectFromServer(); // Роз'єднатись при невідомій помилці
            }
        }


        async void OnLoginTapped(object sender, EventArgs e)
        {
            await Navigation.PopAsync();
        }

        private void DisconnectFromServer()
        {
            if (!_isConnected) return;

            _isConnected = false;
            try
            {
                Console.WriteLine("Роз'єднання з сервером...");
                _stream?.Close();
                _client?.Close();
                Console.WriteLine("З'єднання закрито.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Помилка при закритті з'єднання: {ex.Message}");
            }
            finally
            {
                _client = null;
                _stream = null;
            }
        }

        // Можливо, не потрібно роз'єднуватись при зникненні, якщо сторінка
        // зникає при переході на іншу сторінку того ж додатка,
        // яка використовує те саме з'єднання.
        // Якщо кожна сторінка має своє з'єднання, тоді роз'єднання тут доречне.
        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            // DisconnectFromServer();
        }
    }
}