// crmV1/ShiftPage.xaml.cs
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
using crmV1.Services; // Ваш ApiClient

namespace crmV1
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class ShiftPage : ContentPage
    {
        private int _managerId;
        private int? _currentOpenShiftId = null; // Зберігатиме ID поточної відкритої зміни (null, якщо немає)

        // Клас для представлення історії зміни
        public class ShiftHistoryItem
        {
            [JsonProperty("shift_id")]
            public int ShiftId { get; set; }
            [JsonProperty("shift_date")]
            public string ShiftDate { get; set; } // Можна використовувати DateTime, але для простоти string
            [JsonProperty("start_time")]
            public string StartTime { get; set; }
            [JsonProperty("end_time")]
            public string EndTime { get; set; } // Nullable, якщо зміна ще відкрита
            [JsonProperty("worked_hours")]
            public decimal? WorkedHours { get; set; } // Nullable, якщо зміна ще відкрита
        }


        public ShiftPage(int managerId)
        {
            InitializeComponent();
            // Приховуємо стандартний заголовок Xamarin.Forms, якщо NavigationPage використовується на кореневому рівні
            NavigationPage.SetHasNavigationBar(this, false);

            _managerId = managerId;

            // Завантажуємо початковий статус зміни та історію при відкритті сторінки
            // Використовуємо OnAppearing для гарантії, що навігація завершена
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await LoadShiftData();
        }

        // Метод для завантаження статусу поточної зміни та історії
        private async Task LoadShiftData()
        {
            ShowLoading(true);

            try
            {
                var requestData = new Dictionary<string, object>
                {
                    { "action", "get_shift_status_and_history" },
                    { "manager_id", _managerId }
                };

                Debug.WriteLine($"[ShiftPage] Sending request: {JsonConvert.SerializeObject(requestData)}");

                Dictionary<string, object> response = await ApiClient.SendRequestAsync(requestData);

                Debug.WriteLine($"[ShiftPage] Received response: {JsonConvert.SerializeObject(response)}");

                bool success = response.TryGetValue("success", out object successObj) && bool.TryParse(successObj?.ToString(), out bool parsedSuccess) && parsedSuccess;
                string message = response.TryGetValue("message", out object messageObj) ? messageObj?.ToString() : "Невідоме повідомлення від сервера.";

                if (success)
                {
                    // Обробка успішної відповіді
                    bool isShiftOpen = false;
                    int? openShiftId = null;
                    List<ShiftHistoryItem> history = new List<ShiftHistoryItem>();

                    if (response.TryGetValue("data", out object dataObj) && dataObj is JObject dataJObject)
                    {
                        // Отримуємо статус відкритої зміни
                        if (dataJObject.TryGetValue("is_shift_open", out JToken isShiftOpenToken) && isShiftOpenToken.Type == JTokenType.Boolean)
                        {
                            isShiftOpen = isShiftOpenToken.ToObject<bool>();
                        }

                        // Отримуємо ID відкритої зміни, якщо вона є
                        if (dataJObject.TryGetValue("open_shift_id", out JToken openShiftIdToken) && openShiftIdToken.Type != JTokenType.Null)
                        {
                            // Використовуємо TryParse або ToObject<int?> для безпечного перетворення
                            if (openShiftIdToken.Type == JTokenType.Integer)
                            {
                                openShiftId = openShiftIdToken.ToObject<int>();
                            }
                            else if (openShiftIdToken.Type == JTokenType.String && int.TryParse(openShiftIdToken.ToString(), out int parsedId))
                            {
                                openShiftId = parsedId;
                            }
                        }

                        // Отримуємо історію змін
                        if (dataJObject.TryGetValue("last_shifts", out JToken lastShiftsToken) && lastShiftsToken.Type == JTokenType.Array)
                        {
                            try
                            {
                                history = lastShiftsToken.ToObject<List<ShiftHistoryItem>>();
                                Debug.WriteLine($"[ShiftPage] Loaded {history?.Count ?? 0} history items.");
                            }
                            catch (JsonException jsonEx)
                            {
                                Debug.WriteLine($"[ShiftPage] JSON Deserialization Error for last_shifts: {jsonEx.Message}");
                                // Продовжуємо, але історія буде порожньою
                                history = new List<ShiftHistoryItem>();
                            }
                        }
                    }

                    // Оновлюємо локальний стан та UI
                    _currentOpenShiftId = openShiftId;
                    UpdateShiftStateUI(isShiftOpen);
                    DisplayShiftHistory(history);
                }
                else
                {
                    // Помилка з сервера
                    Debug.WriteLine($"[ShiftPage] Load shift data failed: {message}");
                    //await DisplayAlert("Помилка завантаження", message, "OK"); // Коментуємо або прибираємо, щоб не заважало
                    UpdateShiftStateUI(false); // Припускаємо, що зміни немає, якщо не вдалося завантажити
                    DisplayShiftHistory(new List<ShiftHistoryItem>()); // Очищуємо або показуємо порожній стан
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ShiftPage] Unexpected Exception during LoadShiftData: {ex.Message}. StackTrace: {ex.StackTrace}");
                await DisplayAlert("Помилка", $"Виникла непередбачена помилка при завантаженні даних: {ex.Message}", "OK");
                UpdateShiftStateUI(false);
                DisplayShiftHistory(new List<ShiftHistoryItem>());
            }
            finally
            {
                ShowLoading(false);
            }
        }

        // Оновлює стан кнопок та лейбла статусу
        private void UpdateShiftStateUI(bool isShiftOpen)
        {
            if (isShiftOpen)
            {
                shiftStatusLabel.Text = $"Зміна відкрита. ID: {_currentOpenShiftId ?? 0}";
                openShiftButton.IsEnabled = false;
                closeShiftButton.IsEnabled = true;
                // ВИПРАВЛЕННЯ: Color.Parse -> Color.FromHex
                shiftStatusLabel.TextColor = Color.FromHex("#4CAF50"); // Зелений колір
            }
            else
            {
                shiftStatusLabel.Text = "Зміна закрита.";
                openShiftButton.IsEnabled = true;
                closeShiftButton.IsEnabled = false;
                // ВИПРАВЛЕННЯ: Color.Parse -> Color.FromHex
                shiftStatusLabel.TextColor = Color.FromHex("#F44336"); // Червоний колір
                _currentOpenShiftId = null; // Забезпечуємо, що ID закритої зміни скинуто
            }
        }

        // Відображає історію змін у StackLayout
        private void DisplayShiftHistory(List<ShiftHistoryItem> history)
        {
            historyStackLayout.Children.Clear(); // Очищуємо попередні елементи

            if (history == null || !history.Any())
            {
                historyStackLayout.Children.Add(new Label { Text = "Історія змін відсутня.", FontAttributes = FontAttributes.Italic });
                return;
            }

            // ВИПРАВЛЕННЯ: Переписуємо створення елементів, використовуючи синтаксис C#
            foreach (var shift in history)
            {
                // Створюємо StackLayout для вмісту одного запису історії
                var shiftStack = new StackLayout { Spacing = 5 };

                // Додаємо Label для дати та часу початку
                shiftStack.Children.Add(new Label { Text = $"Дата: {shift.ShiftDate}" });
                shiftStack.Children.Add(new Label { Text = $"Початок: {shift.StartTime}" });

                // Додаємо Label для часу кінця та відпрацьованих годин,
                // показуючи їх тільки якщо вони існують
                if (!string.IsNullOrEmpty(shift.EndTime))
                {
                    shiftStack.Children.Add(new Label { Text = $"Кінець: {shift.EndTime}" });
                }
                else
                {
                    shiftStack.Children.Add(new Label { Text = "Зміна ще відкрита", FontAttributes = FontAttributes.Italic });
                }

                if (shift.WorkedHours.HasValue)
                {
                    shiftStack.Children.Add(new Label { Text = $"Відпрацьовано: {shift.WorkedHours.Value} год" });
                }

                // Отримуємо стиль Frame з ресурсів
                var frameStyle = (Style)Resources["Frame"];

                // Створюємо Frame та застосовуємо стиль
                var shiftFrame = new Frame();
                shiftFrame.Style = frameStyle;

                // Вкладаємо StackLayout з даними зміни у Frame
                shiftFrame.Content = shiftStack;

                // Додаємо Frame до батьківського historyStackLayout
                historyStackLayout.Children.Add(shiftFrame);
            }
            // ВИДАЛЕНО: Залишки XAML синтаксису та коментарів про Converter
        }

        // Обробник кнопки "Відкрити зміну"
        private async void OnOpenShiftClicked(object sender, EventArgs e)
        {
            // Перевірка на всяк випадок, хоча кнопка має бути неактивною
            if (_currentOpenShiftId.HasValue)
            {
                await DisplayAlert("Помилка", "У вас вже є відкрита зміна.", "OK");
                return;
            }

            ShowLoading(true);

            try
            {
                var requestData = new Dictionary<string, object>
                {
                    { "action", "open_shift" },
                    { "manager_id", _managerId }
                    // Сервер має сам взяти поточну дату/час
                    // Можна передати: { "shift_date", DateTime.Today.ToString("yyyy-MM-dd") }
                    // { "start_time", DateTime.Now.ToString("HH:mm:ss") }
                };

                Debug.WriteLine($"[ShiftPage] Sending open_shift request: {JsonConvert.SerializeObject(requestData)}");
                Dictionary<string, object> response = await ApiClient.SendRequestAsync(requestData);
                Debug.WriteLine($"[ShiftPage] Received response for open_shift: {JsonConvert.SerializeObject(response)}");


                bool success = response.TryGetValue("success", out object successObj) && bool.TryParse(successObj?.ToString(), out bool parsedSuccess) && parsedSuccess;
                string message = response.TryGetValue("message", out object messageObj) ? messageObj?.ToString() : "Невідоме повідомлення від сервера.";

                if (success)
                {
                    // Отримуємо новий ShiftId з відповіді
                    if (response.TryGetValue("data", out object dataObj) && dataObj is JObject dataJObject &&
                        dataJObject.TryGetValue("shift_id", out JToken shiftIdToken) && shiftIdToken.Type != JTokenType.Null)
                    {
                        // Використовуємо TryParse або ToObject<int> для безпечного перетворення
                        if (shiftIdToken.Type == JTokenType.Integer)
                        {
                            _currentOpenShiftId = shiftIdToken.ToObject<int>();
                        }
                        else if (shiftIdToken.Type == JTokenType.String && int.TryParse(shiftIdToken.ToString(), out int parsedId))
                        {
                            _currentOpenShiftId = parsedId;
                        }
                        else
                        {
                            // Якщо ShiftId не отримано, це проблема, вважаємо, що відкриття не відбулось коректно
                            success = false;
                            message = "Зміну відкрито, але ID зміни не отримано в очікуваному форматі."; // Уточнено повідомлення
                            _currentOpenShiftId = null; // Не маємо ID, не можемо закрити
                        }
                    }
                    else
                    {
                        // Якщо поле data або shift_id відсутнє
                        success = false;
                        message = "Сервер не повернув ID відкритої зміни.";
                        _currentOpenShiftId = null;
                    }

                    if (success)
                    {
                        Debug.WriteLine($"[ShiftPage] Open shift successful. New Shift ID: {_currentOpenShiftId}");
                        await DisplayAlert("Успіх", message, "OK");
                        // Оновлюємо UI стан та перезавантажуємо історію (нова відкрита зміна має з'явитись)
                        UpdateShiftStateUI(true);
                        // await LoadShiftData(); // Перезавантажуємо всі дані, включаючи історію - це викликається в ShowLoading(false)
                    }
                    else
                    {
                        // Помилка, але success=true була від ApiClient
                        Debug.WriteLine($"[ShiftPage] Open shift logical error: {message}");
                        await DisplayAlert("Помилка", message, "OK");
                        UpdateShiftStateUI(false); // Забезпечуємо правильний стан
                                                   // await LoadShiftData(); // Перезавантажуємо, щоб перевірити реальний стан - це викликається в ShowLoading(false)
                    }
                }
                else
                {
                    // Помилка з сервера або ApiClient
                    Debug.WriteLine($"[ShiftPage] Open shift failed: {message}");
                    await DisplayAlert("Помилка", message, "OK");
                    // Завантажуємо дані ще раз, щоб перевірити поточний стан на сервері
                    // await LoadShiftData(); // це викликається в ShowLoading(false)
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ShiftPage] Unexpected Exception during OnOpenShiftClicked: {ex.Message}. StackTrace: {ex.StackTrace}");
                await DisplayAlert("Помилка", $"Виникла непередбачена помилка при відкритті зміни: {ex.Message}", "OK");
                // Завантажуємо дані ще раз, щоб перевірити поточний стан на сервері
                // await LoadShiftData(); // це викликається в ShowLoading(false)
            }
            finally
            {
                // Після будь-якої операції (успіх чи помилка), перезавантажуємо дані,
                // щоб переконатись, що UI відповідає реальному стану на сервері
                // та ховаємо індикатор.
                // Використовуємо Device.BeginInvokeOnMainThread, бо це відбувається в finally
                Device.BeginInvokeOnMainThread(async () => await LoadShiftData());
            }
        }

        // Обробник кнопки "Закрити зміну"
        private async void OnCloseShiftClicked(object sender, EventArgs e)
        {
            // Перевіряємо, чи є активна зміна (за _currentOpenShiftId)
            if (!_currentOpenShiftId.HasValue || _currentOpenShiftId.Value <= 0)
            {
                await DisplayAlert("Помилка", "Немає відкритої зміни для закриття.", "OK");
                UpdateShiftStateUI(false); // На всяк випадок оновлюємо UI
                return;
            }

            ShowLoading(true);

            try
            {
                var requestData = new Dictionary<string, object>
                {
                    { "action", "close_shift" },
                    { "manager_id", _managerId },
                    { "shift_id", _currentOpenShiftId.Value } // Передаємо ID зміни, яку закриваємо
                    // Сервер має сам взяти поточний час завершення
                    // Можна передати: { "end_time", DateTime.Now.ToString("HH:mm:ss") }
                };

                Debug.WriteLine($"[ShiftPage] Sending close_shift request: {JsonConvert.SerializeObject(requestData)}");
                Dictionary<string, object> response = await ApiClient.SendRequestAsync(requestData);
                Debug.WriteLine($"[ShiftPage] Received response for close_shift: {JsonConvert.SerializeObject(response)}");

                bool success = response.TryGetValue("success", out object successObj) && bool.TryParse(successObj?.ToString(), out bool parsedSuccess) && parsedSuccess;
                string message = response.TryGetValue("message", out object messageObj) ? messageObj?.ToString() : "Невідоме повідомлення від сервера.";

                if (success)
                {
                    Debug.WriteLine($"[ShiftPage] Close shift successful: {message}");
                    // Можливо, сервер поверне відпрацьовані години в message або data
                    // decimal? workedHours = null;
                    // if (response.TryGetValue("data", out object dataObj) && dataObj is JObject dataJObject &&
                    //     dataJObject.TryGetValue("worked_hours", out JToken hoursToken) && hoursToken.Type != JTokenType.Null)
                    // {
                    //     if (decimal.TryParse(hoursToken.ToString(), out decimal parsedHours))
                    //     {
                    //         workedHours = parsedHours;
                    //     }
                    // }
                    // if (workedHours.HasValue) { message += $" Відпрацьовано: {workedHours.Value} год."; }

                    await DisplayAlert("Успіх", message, "OK");

                    // Зміна успішно закрита, скидаємо ID відкритої зміни та оновлюємо UI
                    _currentOpenShiftId = null;
                    UpdateShiftStateUI(false);
                    // await LoadShiftData(); // Перезавантажуємо всі дані, включаючи історію - це викликається в ShowLoading(false)
                }
                else
                {
                    // Помилка з сервера або ApiClient
                    Debug.WriteLine($"[ShiftPage] Close shift failed: {message}");
                    await DisplayAlert("Помилка", message, "OK");
                    // Завантажуємо дані ще раз, щоб перевірити поточний стан на сервері
                    // await LoadShiftData(); // це викликається в ShowLoading(false)
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ShiftPage] Unexpected Exception during OnCloseShiftClicked: {ex.Message}. StackTrace: {ex.StackTrace}");
                await DisplayAlert("Помилка", $"Виникла непередбачена помилка при закритті зміни: {ex.Message}", "OK");
                // Завантажуємо дані ще раз, щоб перевірити поточний стан на сервері
                // await LoadShiftData(); // це викликається в ShowLoading(false)
            }
            finally
            {
                // Після будь-якої операції (успіх чи помилка), перезавантажуємо дані,
                // щоб переконатись, що UI відповідає реальному стану на сервері
                // та ховаємо індикатор.
                // Використовуємо Device.BeginInvokeOnMainThread, бо це відбувається в finally
                Device.BeginInvokeOnMainThread(async () => await LoadShiftData());
            }
        }


        // Хелпер для показу/приховування індикатора
        private void ShowLoading(bool isLoading)
        {
            activityIndicator.IsRunning = isLoading;
            activityIndicator.IsVisible = isLoading;
            // Відключаємо взаємодію з основним контентом, якщо показуємо індикатор
            // ВИПРАВЛЕННЯ: Доступ до ScrollView за його x:Name
            contentScrollView.IsEnabled = !isLoading;

            // Також можна вимкнути кнопки окремо, щоб вони не натискались
            openShiftButton.IsEnabled = !isLoading && !_currentOpenShiftId.HasValue;
            closeShiftButton.IsEnabled = !isLoading && _currentOpenShiftId.HasValue;
        }

        // ВИДАЛЕНО: Залишки коду та приклад Converter
    }
}