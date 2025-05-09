using crmV1.Services; // !!! Впевніться, що цей using вказує на папку з вашим ApiClient !!!
// using System.Net.Sockets; // Не потрібен прямо тут, використовується в ApiClient
using Newtonsoft.Json; // Для роботи з JSON
using Newtonsoft.Json.Linq; // Для роботи з JObject
using System;
using System.Collections.Generic;
using System.Diagnostics; // Використовуємо Debug.WriteLine для логування в режимі налагодження
// using System.IO; // Не потрібен прямо тут
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;


namespace crmV1
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class BookingPage : ContentPage
    {
        // Видалено ServerIP, ServerPort, TcpClient, NetworkStream, _isConnected, _connectionCts звідси.
        // Управління з'єднанням тепер повністю в ApiClient для кожного запиту.

        private string _sessionTime; // Наприклад, "14:00-15:00"
        private DateTime _selectedDate;

        private int _managerId;
        private int _clubId; // Зберігаємо ID клубу

        // --- Конструктор приймає clubId ---
        public BookingPage(string sessionTime, DateTime selectedDate, int managerId, int clubId)
        {
            InitializeComponent();
            NavigationPage.SetHasNavigationBar(this, false); // Зазвичай це робиться на кореневій сторінці NavigationPage

            _sessionTime = sessionTime;
            _selectedDate = selectedDate;
            _managerId = managerId;
            _clubId = clubId;

            sessionInfoLabel.Text = $"Бронювання на {_selectedDate:dd.MM.yyyy} на час {_sessionTime}";

            // Ініціалізуємо поля часу з sessionTime
            if (!string.IsNullOrEmpty(_sessionTime) && _sessionTime.Contains("-"))
            {
                var times = _sessionTime.Split('-');
                if (times.Length == 2)
                {
                    string start = times[0].Trim();
                    string end = times[1].Trim();
                    TimeSpan tempSpan;

                    // ВИПРАВЛЕННЯ: Використовуємо новий метод IsValidTimeFormatOnly для ініціалізації,
                    // щоб перевірити лише формат, а не діапазон
                    if (IsValidTimeFormatOnly(start, out tempSpan))
                        startTimeEntry.Text = start;
                    else
                        Debug.WriteLine($"[BookingPage] Initial start time '{start}' from sessionTime does NOT match expected format(s).");

                    if (IsValidTimeFormatOnly(end, out tempSpan))
                        endTimeEntry.Text = end;
                    else
                        Debug.WriteLine($"[BookingPage] Initial end time '{end}' from sessionTime does NOT match expected format(s).");
                }
            }

            // Ініціалізація не потребує асинхронного підключення тут,
            // кожен запит через ApiClient буде встановлювати своє з'єднання.
            // InitializeAsync(); // Цей метод тепер не потрібен
        }

        // InitializeAsync метод більше не потрібен, оскільки ApiClient керує з'єднаннями

        // ConnectToServer метод більше не потрібен тут

        private const string PS_VALUE = "PS";
        private const string QUEST_VALUE = "Quest";

        private string GetSelectedSessionType()
        {
            // ВИПРАВЛЕННЯ: Використовуємо !ReferenceEquals(element, null) для безпечної перевірки
            if (!ReferenceEquals(VR, null) && VR.IsChecked) return "VR";
            if (!ReferenceEquals(PS, null) && PS.IsChecked) return PS_VALUE;
            if (!ReferenceEquals(Quest, null) && Quest.IsChecked) return QUEST_VALUE;


            // Якщо жоден не вибраний або UI не готовий
            Debug.WriteLine("[BookingPage][GetSelectedSessionType] No session type selected or UI elements are null. Returning 'VR' as default.");
            return "VR"; // Default value
        }

        // --- НОВИЙ метод для перевірки формату часу без перевірки діапазону ---
        // Корисно для ініціалізації полів, де діапазон не має значення
        private bool IsValidTimeFormatOnly(string time, out TimeSpan timeSpan)
        {
            timeSpan = TimeSpan.Zero;
            if (string.IsNullOrEmpty(time))
            {
                Debug.WriteLine("[BookingPage][IsValidTimeFormatOnly] Input time string is null or empty.");
                return false;
            }

            string trimmedTime = time.Trim();

            // --- ДОДАНО ДЕТАЛЬНЕ ЛОГУВАННЯ СИМВОЛІВ ---
            Debug.WriteLine($"[BookingPage][IsValidTimeFormatOnly] Analyzing string '{trimmedTime}' (Length: {trimmedTime.Length}):");
            for (int i = 0; i < trimmedTime.Length; i++)
            {
                Debug.WriteLine($"  Char [{i}]: '{trimmedTime[i]}' (Unicode: U+{(int)trimmedTime[i]:X4})");
            }
            // --- КІНЕЦЬ ДЕТАЛЬНОГО ЛОГУВАННЯ ---


            // 1. Спроба парсингу за масивом форматів
            string[] formats = { @"HH\:mm", @"H\:mm", "HH:mm", "H:mm" };
            bool parseSuccess = TimeSpan.TryParseExact(trimmedTime, formats, CultureInfo.InvariantCulture, TimeSpanStyles.None, out timeSpan);

            if (parseSuccess)
            {
                Debug.WriteLine($"[BookingPage][IsValidTimeFormatOnly] Parse using multiple formats successful. Result: {timeSpan}.");
                return true; // Успіх
            }

            Debug.WriteLine($"[BookingPage][IsValidTimeFormatOnly] Failed parse using multiple formats for '{trimmedTime}'.");


            // 2. РЕЗЕРВ: Спроба ручного парсингу для формату HH:mm
            Debug.WriteLine("[BookingPage][IsValidTimeFormatOnly] Trying manual parse as HH:mm...");
            if (trimmedTime.Length == 5 && trimmedTime[2] == ':')
            {
                if (int.TryParse(trimmedTime.Substring(0, 2), out int hour) &&
                    int.TryParse(trimmedTime.Substring(3, 2), out int minute))
                {
                    // Перевірка валідності години та хвилини
                    if (hour >= 0 && hour <= 23 && minute >= 0 && minute <= 59)
                    {
                        timeSpan = new TimeSpan(hour, minute, 0);
                        Debug.WriteLine($"[BookingPage][IsValidTimeFormatOnly] Manual parse successful. Result: {timeSpan}.");
                        return true; // Ручний парсинг успішний
                    }
                    else
                    {
                        Debug.WriteLine($"[BookingPage][IsValidTimeFormatOnly] Manual parse failed: Invalid hour ({hour}) or minute ({minute}) range.");
                    }
                }
                else
                {
                    Debug.WriteLine($"[BookingPage][IsValidTimeFormatOnly] Manual parse failed: Could not parse parts as integers.");
                }
            }
            else
            {
                Debug.WriteLine($"[BookingPage][IsValidTimeFormatOnly] Manual parse skipped: String length is not 5 or missing colon at index 2.");
            }


            // Якщо жоден спосіб не вдався
            Debug.WriteLine($"[BookingPage][IsValidTimeFormatOnly] Final parse failed for '{trimmedTime}'.");
            timeSpan = TimeSpan.Zero; // Переконуємось, що повернуто TimeSpan.Zero при невдачі
            return false;
        }


        // --- ПОКРАЩЕНА ЛОГІКА ВАЛІДАЦІЇ ЧАСУ З ПЕРЕВІРКОЮ ДІАПАЗОНУ ---
        // Використовуємо масив форматів для гнучкості парсингу, з резервним ручним парсингом
        private bool IsValidTime(string time, out TimeSpan timeSpan)
        {
            timeSpan = TimeSpan.Zero; // Ініціалізуємо timeSpan значенням за замовчуванням

            if (string.IsNullOrEmpty(time))
            {
                Debug.WriteLine($"[BookingPage][IsValidTime] Input time string is null or empty.");
                return false;
            }

            string trimmedTime = time.Trim();
            Debug.WriteLine($"[BookingPage][IsValidTime] Attempting parse using multiple formats for: '{trimmedTime}'"); // Лог вхідного рядка

            // --- ДОДАНО ДЕТАЛЬНЕ ЛОГУВАННЯ СИМВОЛІВ ---
            Debug.WriteLine($"[BookingPage][IsValidTime] Analyzing string '{trimmedTime}' (Length: {trimmedTime.Length}):");
            for (int i = 0; i < trimmedTime.Length; i++)
            {
                Debug.WriteLine($"  Char [{i}]: '{trimmedTime[i]}' (Unicode: U+{(int)trimmedTime[i]:X4})");
            }
            // --- КІНЕЦЬ ДЕТАЛЬНОГО ЛОГУВАННЯ ---


            // 1. Спроба парсингу за масивом форматів
            string[] formats = { @"HH\:mm", @"H\:mm", "HH:mm", "H:mm" }; // Дозволяємо "ЧЧ:ММ" та "Ч:ММ" з/без екранування
            bool parseSuccess = TimeSpan.TryParseExact(trimmedTime, formats, CultureInfo.InvariantCulture, TimeSpanStyles.None, out timeSpan);


            if (!parseSuccess)
            {
                Debug.WriteLine($"[BookingPage][IsValidTime] Failed parse using multiple formats for '{trimmedTime}'. Trying manual parse..."); // Лог невдачі парсингу, переходимо до ручного

                // 2. РЕЗЕРВ: Спроба ручного парсингу для формату HH:mm, якщо TryParseExact не вдався
                if (trimmedTime.Length == 5 && trimmedTime[2] == ':')
                {
                    if (int.TryParse(trimmedTime.Substring(0, 2), out int hour) &&
                        int.TryParse(trimmedTime.Substring(3, 2), out int minute))
                    {
                        // Перевірка валідності години та хвилини
                        if (hour >= 0 && hour <= 23 && minute >= 0 && minute <= 59)
                        {
                            timeSpan = new TimeSpan(hour, minute, 0);
                            Debug.WriteLine($"[BookingPage][IsValidTime] Manual parse successful. Result: {timeSpan}.");
                            parseSuccess = true; // Ручний парсинг успішний
                        }
                        else
                        {
                            Debug.WriteLine($"[BookingPage][IsValidTime] Manual parse failed: Invalid hour ({hour}) or minute ({minute}) range.");
                        }
                    }
                    else
                    {
                        Debug.WriteLine($"[BookingPage][IsValidTime] Manual parse failed: Could not parse parts as integers.");
                    }
                }
                else
                {
                    Debug.WriteLine($"[BookingPage][IsValidTime] Manual parse skipped: String length is not 5 or missing colon at index 2.");
                }
            }

            // Якщо парсинг (будь-яким методом) не вдався
            if (!parseSuccess)
            {
                Debug.WriteLine($"[BookingPage][IsValidTime] Final parse failed for '{trimmedTime}'.");
                timeSpan = TimeSpan.Zero; // Переконуємось, що повернуто TimeSpan.Zero при невдачі
                return false;
            }


            Debug.WriteLine($"[BookingPage][IsValidTime] Time successfully parsed: {timeSpan}."); // Лог успішного парсингу (після парсингу будь-яким методом)

            // 3. Перевірка діапазону часу (14:00 - 22:00)
            TimeSpan minTime = new TimeSpan(14, 0, 0); // 14:00
            TimeSpan maxTime = new TimeSpan(22, 0, 0); // 22:00

            // Дозволяємо час рівно о 14:00 і рівно о 22:00
            bool isRangeValid = timeSpan >= minTime && timeSpan <= maxTime;
            if (!isRangeValid)
            {
                Debug.WriteLine($"[BookingPage][IsValidTime] Parsed time {timeSpan} is outside the allowed range {minTime}-{maxTime}."); // Лог поза діапазоном
                // Розпарсено успішно, але поза допустимим діапазоном
                return false;
            }

            Debug.WriteLine($"[BookingPage][IsValidTime] Parsed time {timeSpan} is within the allowed range."); // Лог в діапазоні
            return true;
        }


        private async void OnConfirmBookingClicked(object sender, EventArgs e)
        {

            Debug.WriteLine("[OnConfirmBookingClicked] Confirm booking button clicked.");

            // Показуємо індикатор зайнятості та вимикаємо елементи UI
            activityIndicator.IsRunning = true;
            activityIndicator.IsVisible = true;
            Content.IsEnabled = false; // Disable the entire page content


            try
            {
                string clientName = clientNameEntry.Text?.Trim();
                string phone = phoneEntry.Text?.Trim();
                string zoneCountString = zoneCountEntry.Text?.Trim();
                string startTimeText = startTimeEntry.Text?.Trim();
                string endTimeText = endTimeEntry.Text?.Trim();
                string sessionType = GetSelectedSessionType();
                string notes = notesEditor.Text?.Trim() ?? string.Empty;

                Debug.WriteLine($"[OnConfirmBookingClicked] Validating input data...");
                if (string.IsNullOrEmpty(clientName) || string.IsNullOrEmpty(phone) || string.IsNullOrEmpty(zoneCountString) || string.IsNullOrEmpty(startTimeText) || string.IsNullOrEmpty(endTimeText))
                {
                    await DisplayAlert("Помилка", "Будь ласка, заповніть всі обов'язкові поля.", "OK");
                    return;
                }

                TimeSpan startTimeSpan;
                Debug.WriteLine($"[OnConfirmBookingClicked] Validating start time: '{startTimeText}'");
                if (!IsValidTime(startTimeText, out startTimeSpan))
                {
                    await DisplayAlert("Помилка", "Невірний формат часу початку або час поза діапазоном 14:00-22:00. Будь ласка, введіть час у форматі гг:хх.", "OK");
                    return;
                }

                TimeSpan endTimeSpan;
                Debug.WriteLine($"[OnConfirmBookingClicked] Validating end time: '{endTimeText}'");
                if (!IsValidTime(endTimeText, out endTimeSpan))
                {
                    await DisplayAlert("Помилка", "Невірний формат часу закінчення або час поза діапазоном 14:00-22:00. Будь ласка, введіть час у форматі гг:хх.", "OK");
                    return;
                }

                if (startTimeSpan >= endTimeSpan)
                {
                    Debug.WriteLine($"[OnConfirmBookingClicked] Invalid time order: Start {startTimeSpan} >= End {endTimeSpan}");
                    await DisplayAlert("Помилка", "Час закінчення має бути пізніше часу початку.", "OK");
                    return;
                }

                int zoneCountInt;
                Debug.WriteLine($"[OnConfirmBookingClicked] Validating zone count: '{zoneCountString}'");
                if (!int.TryParse(zoneCountString, out zoneCountInt) || zoneCountInt <= 0)
                {
                    Debug.WriteLine($"[OnConfirmBookingClicked] Failed to parse zone count '{zoneCountString}' or value <= 0.");
                    await DisplayAlert("Помилка", "Невірний формат кількості зон. Будь ласка, введіть додатне число.", "OK");
                    return;
                }

                string startTimeFormatted = startTimeSpan.ToString(@"hh\:mm");
                string endTimeFormatted = endTimeSpan.ToString(@"hh\:mm");
                Debug.WriteLine($"[OnConfirmBookingClicked] Time formatted for server: Start='{startTimeFormatted}', End='{endTimeFormatted}'");

                // Перевірка доступності зон через ApiClient
                Debug.WriteLine("[OnConfirmBookingClicked] Starting availability check...");
                if (!await IsAvailabilityValid(_selectedDate.ToString("yyyy-MM-dd"), startTimeFormatted, endTimeFormatted, sessionType, zoneCountInt))
                {
                    Debug.WriteLine("[OnConfirmBookingClicked] Availability check failed or indicated unavailable.");
                    return; // Exit if availability check failed (message is shown inside IsAvailabilityValid)
                }
                Debug.WriteLine("[OnConfirmBookingClicked] Availability check successful.");

                // --- Використовуємо Dictionary<string, object> для запиту бронювання ---
                var bookingData = new Dictionary<string, object>
                {
                    { "action", "add_booking" },
                    { "session_date", _selectedDate.ToString("yyyy-MM-dd") },
                    { "start_time", startTimeFormatted },
                    { "end_time", endTimeFormatted },
                    { "client_name", clientName },
                    { "phone", phone },
                    { "zone_count", zoneCountInt },
                    { "session_type", sessionType },
                    { "notes", notes },
                    { "manager_id", _managerId },
                    { "club_id", _clubId }
                    // TODO: Discount
                };

                // --- Виклик ApiClient для відправки запиту бронювання ---
                Dictionary<string, object> response = null;
                try
                {
                    Debug.WriteLine($"[OnConfirmBookingClicked] Sending add_booking request via ApiClient...");
                    response = await ApiClient.SendRequestAsync(bookingData);
                    Debug.WriteLine($"[OnConfirmBookingClicked] Received raw response object from ApiClient: {response}"); // Логуємо сам об'єкт відповіді

                }
                catch (Exception apiEx)
                {
                    // ... обробка помилки ApiClient ...
                    Debug.WriteLine($"[OnConfirmBookingClicked] ERROR during ApiClient request for add_booking: {apiEx.GetType().Name} - {apiEx.Message}\n{apiEx.StackTrace}");
                    await DisplayAlert("Помилка зв'язку/даних", $"Виникла помилка при відправці запиту або обробці відповіді: {apiEx.Message}", "OK");
                    // Важливо: Якщо ApiClient кинув виняток, response буде null. Логіка обробки response нижче має це враховувати.
                    // Переходимо до обробки null response
                    // НЕ return; тут, щоб логіка finally спрацювала
                }

                // --- Process response from ApiClient (ПОКРАЩЕНА ЛОГІКА З ДОДАТКОВИМ ЛОГУВАННЯМ) ---
                bool isBookingSuccess = false;
                string serverMessage = "Невідоме повідомлення від сервера.";

                if (response != null)
                {
                    Debug.WriteLine($"[OnConfirmBookingClicked] Processing non-null response.");

                    // Логування вмісту відповіді перед парсингом
                    Debug.WriteLine($"[OnConfirmBookingClicked] Full response dictionary content: {JsonConvert.SerializeObject(response)}");

                    if (response.TryGetValue("message", out object messageObj) && messageObj != null)
                    {
                        serverMessage = messageObj.ToString();
                        Debug.WriteLine($"[OnConfirmBookingClicked] Extracted server message: '{serverMessage}'");
                    }
                    else
                    {
                        Debug.WriteLine("[OnConfirmBookingClicked] WARNING: Response missing 'message' field. Using default.");
                    }

                    if (response.TryGetValue("success", out object successObj) && successObj != null)
                    {
                        // ДОДАЄМО ДЕТАЛЬНЕ ЛОГУВАННЯ ПОЛЯ 'success'
                        Debug.WriteLine($"[OnConfirmBookingClicked] Found 'success' field. Object type: {successObj.GetType().Name}, Value: '{successObj}'");

                        try
                        {
                            bool? parsedSuccess = JToken.FromObject(successObj).ToObject<bool?>(); // <-- Перевірте це місце в налагоджувачі

                            if (parsedSuccess.HasValue)
                            {
                                isBookingSuccess = parsedSuccess.Value; // <-- Перевірте це місце в налагоджувачі
                                Debug.WriteLine($"[OnConfirmBookingClicked] Parsed 'success' field as boolean: {isBookingSuccess}");
                            }
                            else
                            {
                                Debug.WriteLine($"[OnConfirmBookingClicked] WARNING: 'success' field value '{successObj}' could not be parsed as bool?. Setting isBookingSuccess to false.");
                                isBookingSuccess = false;
                                if (serverMessage == "Невідоме повідомлення від сервера.") serverMessage = $"Помилка обробки відповіді: Некоректний формат поля 'success' ('{successObj}').";
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[OnConfirmBookingClicked] ERROR converting 'success' field '{successObj}' using JToken.ToObject<bool?>(): {ex.GetType().Name} - {ex.Message}\n{ex.StackTrace}");
                            isBookingSuccess = false;
                            if (serverMessage == "Невідоме повідомлення від сервера.") serverMessage = $"Помилка обробки відповіді при парсингу 'success': {ex.Message}";
                        }
                    }
                    else
                    {
                        Debug.WriteLine("[OnConfirmBookingClicked] WARNING: Response missing 'success' field. Setting isBookingSuccess to false.");
                        isBookingSuccess = false;
                        if (serverMessage == "Невідоме повідомлення від сервера.") serverMessage = "Помилка обробки відповіді: Відсутнє поле 'success'.";
                    }
                }
                else // response is null
                {
                    Debug.WriteLine("[OnConfirmBookingClicked] ERROR: ApiClient.SendRequestAsync returned null response.");
                    isBookingSuccess = false;
                    serverMessage = "Помилка зв'язку: Не отримано відповіді від сервера.";
                }


                if (isBookingSuccess)
                {
                    Debug.WriteLine($"[OnConfirmBookingClicked] Booking successful (UI message): {serverMessage}");
                    await DisplayAlert("Успіх", serverMessage, "OK"); // Заголовок "Успіх"
                    await Navigation.PopAsync();
                }
                else
                {
                    Debug.WriteLine($"[OnConfirmBookingClicked] Booking failed (UI message): {serverMessage}");
                    await DisplayAlert("Помилка бронювання", serverMessage, "OK"); // Заголовок "Помилка бронювання"
                                                                                   // Не закриваємо сторінку
                }
            }
            finally
            {
                // Ховаємо індикатор зайнятості та вмикаємо елементи UI
                activityIndicator.IsRunning = false;
                activityIndicator.IsVisible = false;
                Content.IsEnabled = true;
                Debug.WriteLine("[OnConfirmBookingClicked] Method finished.");
            }
        }

        // ... (інші методи без змін) ...

        // Unified TextChanged handler for time entries
        // ВИПРАВЛЕННЯ: Покращений TextChanged для кращого форматування та очищення вводу
        private void OnTimeEntryTextChanged(object sender, TextChangedEventArgs e)
        {
            var entry = (Entry)sender;
            var newText = e.NewTextValue ?? string.Empty;
            var oldText = e.OldTextValue ?? string.Empty;

            // --- Покращена логіка форматування ---
            // Видаляємо всі символи, крім цифр та двокрапки
            var allowedChars = newText.Where(c => char.IsDigit(c) || c == ':').ToList();

            // Обмежуємо кількість двокрапок до однієї
            int colonCount = allowedChars.Count(c => c == ':');
            if (colonCount > 1)
            {
                // Якщо більше однієї двокрапки, залишаємо тільки першу
                int firstColonIndex = allowedChars.IndexOf(':');
                if (firstColonIndex != -1) // Якщо двокрапка була
                {
                    // Видаляємо всі двокрапки, крім першої, яка відповідає першій позиції в allowedChars
                    allowedChars = allowedChars.Where((c, index) => c != ':' || index == firstColonIndex).ToList();
                }
                else
                {
                    // Якщо двокрапки не було в allowedChars, але чомусь colonCount > 1 (що дивно), просто скидаємо
                    allowedChars = allowedChars.Where(char.IsDigit).ToList(); // Залишаємо тільки цифри
                }
            }

            // Видаляємо двокрапку на початку або в кінці, якщо вона там є
            if (allowedChars.Count > 0 && allowedChars[0] == ':') allowedChars.RemoveAt(0);
            // ВИПРАВЛЕННЯ: Використовуємо класичний індекс для доступу з кінця
            if (allowedChars.Count > 0 && allowedChars.Count - 1 >= 0 && allowedChars[allowedChars.Count - 1] == ':') allowedChars.RemoveAt(allowedChars.Count - 1);


            var filteredText = new string(allowedChars.ToArray());

            string finalFormattedText = filteredText; // Починаємо з відфільтрованого тексту

            // Автоматично додаємо ':' після перших двох цифр, якщо їх є щонайменше дві, немає двокрапки,
            // і введення збільшує кількість символів (щоб не додавати `:` при видаленні).
            // Також додаємо ':' тільки якщо перші дві цифри валідні для години (00-23).
            // ВИПРАВЛЕННЯ: Логіка додавання двокрапки стала безпечнішою.
            if (filteredText.Length >= 2 && !filteredText.Contains(":") && newText.Length > oldText.Length)
            {
                // Перевіряємо, чи перші дві цифри можуть бути годиною
                if (int.TryParse(filteredText.Substring(0, 2), out int hour) && hour >= 0 && hour <= 23)
                {
                    finalFormattedText = filteredText.Insert(2, ":");
                }
            }

            // Обмежуємо довжину до 5 символів (HH:mm)
            if (finalFormattedText.Length > 5)
            {
                finalFormattedText = finalFormattedText.Substring(0, 5);
            }

            // Додаткова перевірка: якщо довжина 5, переконатись, що вона має структуру dd:dd
            if (finalFormattedText.Length == 5 && finalFormattedText.IndexOf(':') != 2)
            {
                // Якщо структура не dd:dd, спробуємо "полагодити"
                var tempDigits = new string(finalFormattedText.Where(char.IsDigit).ToArray());
                if (tempDigits.Length >= 4) // Має бути хоча б 4 цифри
                {
                    finalFormattedText = tempDigits.Insert(2, ":"); // Форматуємо як dd:dd
                    if (finalFormattedText.Length > 5) finalFormattedText = finalFormattedText.Substring(0, 5); // Знову обмежуємо довжину
                    Debug.WriteLine($"[BookingPage][OnTimeEntryTextChanged] Corrected invalid 5-char structure to: '{finalFormattedText}'");
                }
                else
                {
                    // Недостатньо цифр для формату dd:dd, залишаємо тільки цифри
                    finalFormattedText = tempDigits;
                    Debug.WriteLine($"[BookingPage][OnTimeEntryTextChanged] Cannot correct invalid 5-char structure, reverted to digits: '{finalFormattedText}'");
                }
            }


            // Оновлюємо Entry лише якщо текст відрізняється, щоб уникнути нескінченного циклу
            // Використовуємо Device.BeginInvokeOnMainThread для безпеки, особливо якщо змінюється Text
            if (entry.Text != finalFormattedText)
            {
                // Запам'ятовуємо поточну позицію курсора перед зміною тексту
                int cursorPosition = entry.CursorPosition;
                int oldLength = entry.Text?.Length ?? 0;

                // Зберігаємо finalFilteredText для доступу в лямбді
                string finalFilteredTextLambda = finalFormattedText; // Копіюємо для лямбди

                Device.BeginInvokeOnMainThread(() =>
                {
                    entry.Text = finalFilteredTextLambda;

                    // Намагаємось зберегти позицію курсора, якщо можливо
                    if (entry.Text != null) // Ensure text is not null before setting position
                    {
                        // Коригуємо позицію курсора після форматування
                        // Якщо додалася двокрапка (або символи видали/додали), коригуємо позицію
                        int currentLength = entry.Text.Length;
                        int offset = currentLength - oldLength;
                        entry.CursorPosition = Math.Min(currentLength, Math.Max(0, cursorPosition + offset));

                        // Додаткова корекція, якщо курсор був біля двокрапки, а вона зникла/з'явилася
                        if (oldText.Contains(":") && !entry.Text.Contains(":") && cursorPosition > oldText.IndexOf(':') && oldText.IndexOf(':') != -1)
                        {
                            // Якщо двокрапка була і курсор був після неї, а двокрапка зникла, зміщуємо на 1 назад
                            entry.CursorPosition = Math.Max(0, entry.CursorPosition - 1);
                        }
                        else if (!oldText.Contains(":") && entry.Text.Contains(":") && cursorPosition >= entry.Text.IndexOf(':') && entry.Text.IndexOf(':') != -1)
                        {
                            // Якщо двокрапки не було, а вона з'явилася перед курсором, зміщуємо на 1 вперед
                            if (cursorPosition == entry.Text.IndexOf(':'))
                            {
                                entry.CursorPosition = Math.Min(entry.Text.Length, cursorPosition + 1);
                            }
                            else
                            {
                                // Курсор був ПІСЛЯ місця, де з'явилася двокрапка
                                entry.CursorPosition = Math.Min(entry.Text.Length, cursorPosition + 1);
                            }
                        }
                    }
                    else
                    {
                        entry.CursorPosition = 0; // Якщо текст став null, скидаємо позицію
                    }
                });
            }
        }

        // Call this new method from both OnStartTimeTextChanged and OnEndTimeTextChanged
        private void OnStartTimeTextChanged(object sender, TextChangedEventArgs e)
        {
            OnTimeEntryTextChanged(sender, e);
        }

        private void OnEndTimeTextChanged(object sender, TextChangedEventArgs e)
        {
            OnTimeEntryTextChanged(sender, e);
        }



        // --- Availability Check using ApiClient ---
        // MODIFIED: Use ApiClient for availability check
        private async Task<bool> IsAvailabilityValid(string sessionDate, string startTime, string endTime, string sessionType, int zoneCount)
        {
            Debug.WriteLine("[IsAvailabilityValid] Availability check started via ApiClient.");

            // Use Dictionary<string, object> for availability request
            var availabilityData = new Dictionary<string, object>
                {
                    { "action", "check_availability" },
                    { "session_date", sessionDate }, // YYYY-MM-DD string
                    { "start_time", startTime },     // HH:mm string
                    { "end_time", endTime },       // HH:mm string
                    { "session_type", sessionType }, // string
                    { "zone_count", zoneCount }, // int
                    { "club_id", _clubId } // int
                };

            // --- Call ApiClient to send availability request ---
            Debug.WriteLine($"[IsAvailabilityValid] Sending check_availability request via ApiClient..."); // Avoid logging the full request here
            Dictionary<string, object> response = null;
            try
            {
                response = await ApiClient.SendRequestAsync(availabilityData);
                Debug.WriteLine($"[IsAvailabilityValid] Received response from ApiClient: {JsonConvert.SerializeObject(response)}");
            }
            catch (Exception apiEx) // ApiClient повинен ловити мережеві помилки, але на всяк випадок
            {
                Debug.WriteLine($"[IsAvailabilityValid] Error during ApiClient request: {apiEx.GetType().Name} - {apiEx.Message}\n{apiEx.StackTrace}");
                await DisplayAlert("Помилка зв'язку", $"Помилка при відправці запиту доступності: {apiEx.Message}", "OK");
                return false; // Вважаємо недоступним через помилку зв'язку
            }


            // --- Process response from ApiClient ---
            // ApiClient is now guaranteed to return a Dictionary<string, object> with a "success" field
            // Use safe parsing
            bool success = response.TryGetValue("success", out object successObj) && successObj != null && bool.TryParse(successObj.ToString(), out bool parsedSuccess) && parsedSuccess;
            string message = response.TryGetValue("message", out object messageObj) && messageObj != null ? messageObj.ToString() : "Невідоме повідомлення від сервера.";


            if (success)
            {
                // Якщо success == true, відповідь успішна, очікуємо поле "available"
                if (response.TryGetValue("available", out object availableObj) && availableObj != null)
                {
                    Debug.WriteLine($"[IsAvailabilityValid] Received 'available' field. Type: {availableObj.GetType().Name}, Value: '{availableObj}'");
                    try
                    {
                        // Safely convert value to bool
                        // First convert object to JToken, then to Nullable<bool>
                        bool? available = JToken.FromObject(availableObj).ToObject<bool?>();

                        if (available.HasValue)
                        {
                            Debug.WriteLine($"[IsAvailabilityValid] 'available' field parsed as: {available.Value}");

                            // --- ВИПРАВЛЕННЯ: ДОДАНО ПОВІДОМЛЕННЯ ПРО НЕДОСТУПНІСТЬ ---
                            if (!available.Value)
                            {
                                Debug.WriteLine("[IsAvailabilityValid] Server reported not available.");
                                // Показуємо повідомлення від сервера, якщо воно є, або стандартне
                                string unavailableMessage = message; // Використовуємо message від сервера
                                if (string.IsNullOrEmpty(unavailableMessage) || unavailableMessage == "Невідоме повідомлення від сервера.")
                                {
                                    unavailableMessage = "На жаль, на обраний час немає достатньо вільних зон.";
                                }
                                await DisplayAlert("Недоступно", unavailableMessage, "OK");
                            }
                            // --- КІНЕЦЬ ВИПРАВЛЕННЯ ---

                            return available.Value; // Повертаємо отримане значення (true/false)
                        }
                        else
                        {
                            Debug.WriteLine($"[IsAvailabilityValid] JToken.ToObject<bool?>() returned null for 'available' field. Unexpected format.");
                            await DisplayAlert("Помилка даних", $"Сервер повернув неочікуваний формат даних для поля 'available'.", "OK");
                            return false; // Cannot process -> consider unavailable
                        }
                    }
                    catch (Exception ex) // Catch errors during conversion to bool?
                    {
                        Debug.WriteLine($"[IsAvailabilityValid] ERROR converting 'available' field ('{availableObj}') to bool: {ex.GetType().Name} - {ex.Message}\n{ex.StackTrace}");
                        await DisplayAlert("Помилка даних", $"Сервер повернув неочікуваний формат даних для поля 'available'.", "OK");
                        return false; // Error processing -> consider unavailable
                    }
                }
                else
                {
                    // Успішна відповідь, але відсутнє поле "available"
                    Debug.WriteLine("[IsAvailabilityValid] Successful response from server, but 'available' field is missing.");
                    await DisplayAlert("Помилка даних", $"Сервер повернув відповідь у неочікуваному форматі (відсутнє поле 'available').", "OK");
                    return false; // Consider unavailable
                }
            }
            else // Якщо success == false (включаючи мережеві помилки від ApiClient)
            {
                // Сервер повернув помилку доступності (success=false) або ApiClient повідомив про помилку зв'язку
                Debug.WriteLine($"[IsAvailabilityValid] Availability check returned error: {message}");
                await DisplayAlert("Помилка доступності", message, "OK");
                return false; // Unavailable
            }
        }

        // Метод для безпечного закриття з'єднання (ТЕПЕР НЕ ПОТРІБЕН, КЕРУЄТЬСЯ APIClient)
        // private void Disconnect() { ... }


        // Метод викликається при закритті сторінки (наприклад, кнопкою "Назад")
        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            // Оскільки ApiClient створює нове з'єднання для кожного запиту,
            // нам не потрібно явно закривати з'єднання при зникненні сторінки.
            // Ресурси звільняться автоматично після завершення запиту.
            Debug.WriteLine("[BookingPage][OnDisappearing] Page is disappearing.");
        }


        private async void OnCalculateButtonClicked(object sender, EventArgs e)
        {
            Debug.WriteLine("[OnCalculateButtonClicked] Calculate button clicked.");
            string startTimeText = startTimeEntry.Text?.Trim();
            string endTimeText = endTimeEntry.Text?.Trim();
            string zoneCountString = zoneCountEntry.Text?.Trim();
            string sessionType = GetSelectedSessionType();

            Debug.WriteLine($"[OnCalculateButtonClicked] Input data: Start='{startTimeText}', End='{endTimeText}', Zones='{zoneCountString}', Type='{sessionType}'");

            // Валідація обов'язкових полів для розрахунку
            if (string.IsNullOrEmpty(startTimeText) || string.IsNullOrEmpty(endTimeText) || string.IsNullOrEmpty(zoneCountString))
            {
                await DisplayAlert("Помилка", "Будь ласка, заповніть час початку, закінчення та кількість зон для розрахунку.", "OK");
                return;
            }

            // --- Використовуємо IsValidTime для валідації часу ---
            TimeSpan startTimeSpan;
            Debug.WriteLine($"[OnCalculateButtonClicked] Validating start time using IsValidTime: '{startTimeText}'");
            // Use IsValidTime, but adapt the error message for calculation
            if (!IsValidTime(startTimeText, out startTimeSpan))
            {
                await DisplayAlert("Помилка розрахунку", "Невірний формат часу початку або час поза діапазоном 14:00-22:00 для розрахунку. Будь ласка, введіть час у форматі гг:хх.", "OK");
                return;
            }
            Debug.WriteLine($"[OnCalculateButtonClicked] Start time parsed and validated for calculation: {startTimeSpan}");


            TimeSpan endTimeSpan;
            Debug.WriteLine($"[OnCalculateButtonClicked] Validating end time using IsValidTime: '{endTimeText}'");
            // Use IsValidTime, but adapt the error message for calculation
            if (!IsValidTime(endTimeText, out endTimeSpan))
            {
                await DisplayAlert("Помилка розрахунку", "Невірний формат часу закінчення або час поза діапазоном 14:00-22:00 для розрахунку. Будь ласка, введіть час у форматі гг:хх.", "OK");
                return;
            }
            Debug.WriteLine($"[OnCalculateButtonClicked] End time parsed and validated for calculation: {endTimeSpan}");

            // Додаткова валідація порядку початку/кінця для розрахунку
            TimeSpan duration = endTimeSpan - startTimeSpan;
            if (duration <= TimeSpan.Zero)
            {
                Debug.WriteLine($"[OnCalculateButtonClicked] Calculation duration <= 0 ({duration}).");
                await DisplayAlert("Помилка розрахунку", "Час закінчення має бути пізніше часу початку для розрахунку.", "OK");
                return;
            }
            Debug.WriteLine($"[OnCalculateButtonClicked] Calculation duration: {duration.TotalMinutes} min.");


            // Валідація кількості зон
            int zoneCountInt;
            Debug.WriteLine($"[OnCalculateButtonClicked] Attempting to parse zone count: '{zoneCountString}'");
            if (!int.TryParse(zoneCountString, out zoneCountInt) || zoneCountInt <= 0)
            {
                Debug.WriteLine($"[OnCalculateButtonClicked] Invalid zone count format for calculation: '{zoneCountString}'");
                await DisplayAlert("Помилка розрахунку", "Невірний формат кількості зон для розрахунку. Будь ласка, введіть додатне число.", "OK");
                return;
            }
            Debug.WriteLine($"[OnCalculateButtonClicked] Zone count parsed for calculation: {zoneCountInt}");

            // TODO: Для точного розрахунку вартості, краще відправити запит на сервер!
            // Приклад запиту на сервер для розрахунку вартості (якщо сервер підтримує таку дію):
            // var priceRequestData = new Dictionary<string, object>
            // {
            //      { "action", "calculate_price" }, // Assume you have such action on the server
            //      { "session_date", _selectedDate.ToString("yyyy-MM-dd") },
            //      { "start_time", startTimeSpan.ToString(@"HH\:mm") },
            //      { "end_time", endTimeSpan.ToString(@"HH\:mm") },
            //      { "zone_count", zoneCountInt },
            //      { "session_type", sessionType },
            //      { "club_id", _clubId }
            //      // TODO: Add discount ID if present
            // };
            // Dictionary<string, object> priceResponse = await ApiClient.SendRequestAsync(priceRequestData);
            // Process priceResponse, get final_price from "data" field, show to user.
            // Show activity indicator during server request.


            // Temporary calculation on client side:
            decimal ratePerHourPerZone = 0;
            if (sessionType == "VR" || sessionType == "Quest")
            {
                if (startTimeSpan.Hours >= 14 && startTimeSpan.Hours < 18) ratePerHourPerZone = 200; // Day
                else if (startTimeSpan.Hours >= 18 && startTimeSpan.Hours < 22) ratePerHourPerZone = 250; // Evening
                else ratePerHourPerZone = 200; // Default
            }
            else if (sessionType == "PS")
            {
                ratePerHourPerZone = 150; // Placeholder
            }

            if (ratePerHourPerZone > 0)
            {
                decimal totalCost = (decimal)duration.TotalHours * zoneCountInt * ratePerHourPerZone;
                // TODO: Add discount logic if calculated on client side
                Debug.WriteLine($"[OnCalculateButtonClicked] Calculation finished: Type={sessionType}, Zones={zoneCountInt}, Duration={duration.TotalMinutes} min, Rate={ratePerHourPerZone}/hour/zone, Cost={totalCost:F2} UAH.");
                await DisplayAlert("Розрахунок", $"Тип: {sessionType}\nЗони: {zoneCountInt}\nТривалість: {duration.TotalMinutes} хв\nОрієнтовна вартість: {totalCost:F2} грн", "OK");
            }
            else
            {
                Debug.WriteLine($"[OnCalculateButtonClicked] Rate for session type {sessionType} is not defined.");
                await DisplayAlert("Помилка розрахунку", $"Не вдалося розрахувати вартість для типу '{sessionType}'. Тариф не визначено.", "OK");
            }
            Debug.WriteLine("[OnCalculateButtonClicked] Method finished.");
        }
    }
}