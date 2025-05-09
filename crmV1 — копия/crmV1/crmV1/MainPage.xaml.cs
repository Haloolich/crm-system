using System;
using System.Collections.Generic;
using System.Collections.ObjectModel; // Використовуємо ObservableCollection
using System.Linq;
using System.Text;
using System.Threading; // Додано для CancellationToken, CancellationTokenSource
using System.Threading.Tasks;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;
using crmV1.Services; // Додаємо простір імен для ApiClient
using crmV1.Models; // Додаємо простір імен для SessionAvailability
using System.Diagnostics; // Для Debug.WriteLine
using System.Globalization; // Для TimeSpan.TryParseExact та CultureInfo
using Newtonsoft.Json.Linq; // Додано для роботи з JArray
using crmV1.Views;

namespace crmV1
{
    public partial class MainPage : ContentPage
    {
        private bool _hasNewSessions = false;
        public bool HasNewSessions
        {
            get => _hasNewSessions;
            set
            {
                // Оновлюємо значення лише якщо воно змінилося
                if (_hasNewSessions != value)
                {
                    _hasNewSessions = value;
                    // Оновлюємо візуальний стан іконок в головному потоці UI
                    Device.BeginInvokeOnMainThread(() =>
                    {
                        UpdateNotificationIcons();
                    });
                }
            }
        }

        private DateTime _currentDate = DateTime.Today;
        private bool _isInitialized = false; // Флаг для першого завантаження даних
        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource(); // Для скасування поточних запитів


        // Використовуємо ObservableCollection для автоматичного оновлення UI
        public ObservableCollection<SessionAvailability> SessionSlots { get; set; }

        public int ManagerId { get; set; }
        public int ClubId { get; set; }
        public string UserRole { get; private set; }
        public bool IsAdmin { get; private set; }

        // Список стандартних часових слотів (можна перенести в конфіг або отримати з сервера)
        // Припускаємо, що слоти мають тривалість 1 година
        private List<string> DefaultTimeSlots = new List<string>
        {
            "14:00", "15:00","16:00", "17:00", "18:00", "19:00", "20:00", "21:00"
        };


        public MainPage(int managerId, int clubId, string userRole)
        {
            InitializeComponent();
            NavigationPage.SetHasNavigationBar(this, false);

            ManagerId = managerId;
            ClubId = clubId;
            UserRole = userRole ?? "manager"; // Записуємо отриману роль, якщо null, використовуємо 'manager'
            IsAdmin = UserRole.Equals("admin", StringComparison.OrdinalIgnoreCase); // Встановлюємо IsAdmin
            this.BindingContext = this;

            // Ініціалізуємо колекцію (можна тут, або в ResetSessionSlots)
            SessionSlots = new ObservableCollection<SessionAvailability>();
            // Прив'язуємо ListView до колекції
            sessionList.ItemsSource = SessionSlots;

            sideMenu.TranslationX = -250; // Ховаємо меню

            // Оновлюємо початкову дату на UI
            UpdateSelectedDateLabel();
            UpdateNotificationIcons();

            // !!! ВИДАЛЕНО виклик LoadAvailabilityDataAsync() з конструктора !!!
            // Завантаження буде в OnAppearing
            Debug.WriteLine("[MainPage] Constructor finished.");
            Debug.WriteLine($"[MainPage] Constructor finished. Role: {UserRole}, IsAdmin: {IsAdmin}");
        }

        // !!! ДОДАНО метод OnAppearing для першого завантаження !!!
        protected override async void OnAppearing()
        {
            base.OnAppearing();
            Debug.WriteLine("[MainPage] OnAppearing called. Loading data and checking for new sessions.");

            // Скасовуємо попередні запити перед початком нових операцій
            CancelLoading();

            // Виконуємо обидві асинхронні операції послідовно
            await LoadAvailabilityDataAsync(); // Завантажує слоти та занятість
            await CheckForNewSessionsAsync(); // Перевіряє наявність нових сесій та оновить значки в кінці
        }

        // !!! ДОДАНО метод OnDisappearing для скасування запитів при відході зі сторінки !!!
        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            Debug.WriteLine("[MainPage] OnDisappearing called. Cancelling ongoing requests.");
            CancelLoading(); // Скасовуємо поточні операції завантаження
        }

        // Метод для скасування поточних операцій завантаження
        private void CancelLoading()
        {
            // Створюємо новий токен скасування
            _cancellationTokenSource?.Cancel(); // Скасовуємо попередні операції
            _cancellationTokenSource?.Dispose(); // Звільняємо ресурси попереднього токена
            _cancellationTokenSource = new CancellationTokenSource(); // Створюємо новий токен для майбутніх операцій
        }


        private void UpdateSelectedDateLabel()
        {
            selectedDateLabel.Text = _currentDate.ToString("dd.MM.yyyy");
        }

        // Метод для відкриття DatePicker
        private void OpenDatePicker(object sender, EventArgs e)
        {
            datePicker.Focus(); // Відкриваємо стандартний вибір дати
        }
        private async Task CheckForNewSessionsAsync()
        {
            Debug.WriteLine($"[MainPage] Starting CheckForNewSessionsAsync for Club {ClubId}");

            var cancellationToken = _cancellationTokenSource.Token;

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                var requestData = new Dictionary<string, object>
                {
                    { "action", "check_new_sessions_count" }, // Новий action
                    { "club_id", ClubId }
                };

                Debug.WriteLine("[MainPage] Sending API request: check_new_sessions_count...");
                var response = await ApiClient.SendRequestAsync(requestData /*, cancellationToken */); // Якщо ApiClient підтримує CancellationToken

                cancellationToken.ThrowIfCancellationRequested(); // Перевірка скасування після відповіді

                Debug.WriteLine("[MainPage] Received API response for check_new_sessions_count.");

                if (response != null && response.ContainsKey("success") && (bool)response["success"])
                {
                    Debug.WriteLine("[MainPage] check_new_sessions_count response success = true.");

                    // Перевіряємо, чи поле 'data' і 'new_sessions_count' існують і не є null
                    if (response.TryGetValue("data", out object dataObj) && dataObj is JObject dataJObject &&
                        dataJObject.TryGetValue("new_sessions_count", out JToken countToken) && countToken.Type != JTokenType.Null)
                    {
                        int newSessionsCount = 0;
                        // Безпечне перетворення з JToken в int
                        if (countToken.Type == JTokenType.Integer)
                        {
                            newSessionsCount = countToken.ToObject<int>();
                        }
                        else if (countToken.Type == JTokenType.String && int.TryParse(countToken.ToString(), out int parsedCount))
                        {
                            newSessionsCount = parsedCount;
                        }
                        // Якщо countToken іншого типу, newSessionsCount залишиться 0

                        Debug.WriteLine($"[MainPage] New sessions count received: {newSessionsCount}");

                        // Оновлюємо властивість, яка запустить оновлення іконок
                        // Робимо це в головному потоці UI
                        Device.BeginInvokeOnMainThread(() =>
                        {
                            HasNewSessions = newSessionsCount > 0;
                        });

                    }
                    else
                    {
                        Debug.WriteLine("[ПОПЕРЕДЖЕННЯ КЛІЄНТ] check_new_sessions_count response data format error: missing new_sessions_count.");
                        // Якщо дані не отримані коректно, вважаємо, що нових сесій немає, щоб не залишати активний значок
                        Device.BeginInvokeOnMainThread(() =>
                        {
                            HasNewSessions = false;
                        });
                    }
                }
                else
                {
                    // Обробка помилки з сервера
                    string errorMessage = response != null && response.ContainsKey("message") ? response["message"].ToString() : "Невідома помилка сервера при перевірці нових сесій.";
                    Debug.WriteLine($"[ПОМИЛКА КЛІЄНТ] check_new_sessions_count response success = false. Message: {errorMessage}");
                    // При помилці перевірки, краще показати стандартний значок
                    Device.BeginInvokeOnMainThread(() =>
                    {
                        HasNewSessions = false;
                    });
                    // Можна вивести DisplayAlert, але це може бути занадто часто
                    // Device.BeginInvokeOnMainThread(async () => await DisplayAlert("Помилка", $"Не вдалося перевірити нові сесії: {errorMessage}", "OK"));
                }
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine("[MainPage] CheckForNewSessionsAsync was cancelled.");
                // При скасуванні скидаємо стан іконок на дефолтний
                Device.BeginInvokeOnMainThread(() =>
                {
                    HasNewSessions = false;
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[КРИТИЧНА ПОМИЛКА КЛІЄНТ] Помилка при перевірці нових сесій: {ex.Message}\n{ex.StackTrace}");
                Device.BeginInvokeOnMainThread(() =>
                {
                    HasNewSessions = false; // При помилці скидаємо стан
                                            // Можна вивести DisplayAlert, якщо помилка критична
                                            // DisplayAlert("Критична помилка", $"Не вдалося перевірити нові сесії: {ex.Message}", "OK");
                });
            }
            finally
            {
                Debug.WriteLine("[MainPage] Finishing CheckForNewSessionsAsync.");
                // Індикатор завантаження вже керується LoadAvailabilityDataAsync,
                // тому тут його ховати не потрібно. Цей метод лише оновлює значок.
            }
        }

        private void UpdateNotificationIcons()
        {
            Debug.WriteLine($"[MainPage] Updating notification icons. HasNewSessions: {HasNewSessions}");

            string iconSource = _hasNewSessions ? "drawable/notif_new.png" : "drawable/notif.png";

            // Перевіряємо, чи елементи UI існують (вони створюються в InitializeComponent)
            //notificationsIcon - ImageButton у верхній панелі
            if (notificationsIcon != null)
            {
                notificationsIcon.Source = iconSource;
                Debug.WriteLine($"[MainPage] Updated notificationsIcon source to: {iconSource}");
            }
            else
            {
                Debug.WriteLine("[MainPage] notificationsIcon is null. Cannot update its source.");
            }

            //menuNotificationIcon - Image у боковому меню
            if (menuNotificationIcon != null)
            {
                menuNotificationIcon.Source = iconSource;
                Debug.WriteLine($"[MainPage] Updated menuNotificationIcon source to: {iconSource}");
            }
            else
            {
                Debug.WriteLine("[MainPage] menuNotificationIcon is null. Cannot update its source.");
            }

            // Тут можна додати оновлення будь-яких інших елементів UI, пов'язаних зі станом нових сповіщень
            // наприклад, зміна кольору тексту кнопки "Сповіщення" у меню тощо, якщо ви додали x:Name до кнопки.
            // Приклад: якщо у вас є <Button x:Name="menuNotificationsButton" Text="Сповіщення" ...>
            // if (menuNotificationsButton != null)
            // {
            //     menuNotificationsButton.TextColor = HasNewSessions ? Color.FromHex("#FFD700") : Color.White; // Золотистий або білий
            // }
        }
        private async void OnSearchClicked(object sender, EventArgs e)
        {
            Debug.WriteLine("[MainPage] OnSearchClicked called. Navigating to ClientSearchPage.");
            // Створюємо екземпляр нової сторінки пошуку
            var searchPage = new ClientSearchPage();

            // Здійснюємо перехід на сторінку пошуку
            await Navigation.PushAsync(searchPage);
        }

        private async void OpenMenu(object sender, EventArgs e)
        {
            overlay.IsVisible = true;
            sideMenu.IsVisible = true;
            await sideMenu.TranslateTo(0, 0, 250, Easing.CubicOut);
        }

        private async void CloseMenu(object sender, EventArgs e)
        {
            await sideMenu.TranslateTo(-250, 0, 250, Easing.CubicIn);
            overlay.IsVisible = false;
            sideMenu.IsVisible = false;
        }

        // Перехід на попередній день
        private async void PreviousDate(object sender, EventArgs e) // Змінено на async void
        {
            Debug.WriteLine("[MainPage] PreviousDate clicked.");
            CancelLoading(); // Скасовуємо попередні запити
            _currentDate = _currentDate.AddDays(-1);
            UpdateSelectedDateLabel();
            await LoadAvailabilityDataAsync(); // Чекаємо перезавантаження даних
        }

        // Перехід на наступний день
        private async void NextDate(object sender, EventArgs e) // Змінено на async void
        {
            Debug.WriteLine("[MainPage] NextDate clicked.");
            CancelLoading(); // Скасовуємо попередні запити
            _currentDate = _currentDate.AddDays(1);
            UpdateSelectedDateLabel();
            await LoadAvailabilityDataAsync(); // Чекаємо перезавантаження даних
        }

        // Обробник вибору дати з DatePicker
        private async void OnDateSelected(object sender, DateChangedEventArgs e) // Змінено на async void
        {
            Debug.WriteLine($"[MainPage] OnDateSelected: {_currentDate.ToShortDateString()} -> {e.NewDate.ToShortDateString()}");
            CancelLoading(); // Скасовуємо попередні запити
            _currentDate = e.NewDate;
            UpdateSelectedDateLabel();
            datePicker.IsVisible = false; // Ховаємо DatePicker після вибору
            await LoadAvailabilityDataAsync(); // Чекаємо перезавантаження даних
        }

        // !!! ДОДАНО окремий метод для очищення та початкового заповнення колекції !!!
        private Task ResetSessionSlotsAsync()
        {
            // Виконуємо очищення колекції в головному потоці UI
            // Використовуємо InvokeOnMainThreadAsync, який повертає Task
            return Device.InvokeOnMainThreadAsync(() =>
            {
                
                loadingIndicator.IsVisible = true;
                loadingIndicator.IsRunning = true;  //Показуємо індикатор завантаження
                SessionSlots.Clear(); // Очищаємо список перед завантаженням нових даних
                Debug.WriteLine("[MainPage] SessionSlots cleared.");
                // Не заповнюємо початковими слотами тут
            });
        }


        /// <summary>
        /// Завантажує дані про доступність зон для поточної дати та клубу
        /// шляхом отримання списку всіх сесій на дату та розподілу їх по слотах.
        /// Оновлює SessionSlots колекцію.
        /// </summary>
        private async Task LoadAvailabilityDataAsync()
        {
            Debug.WriteLine($"[MainPage] Starting LoadAvailabilityDataAsync (optimized) for date {_currentDate.ToShortDateString()}, Club {ClubId}");

            // Отримуємо токен скасування для цієї операції
            var cancellationToken = _cancellationTokenSource.Token;

            try
            {
                // !!! ВАЖЛИВО: Спочатку очищаємо колекцію в UI потоці і ЧЕКАЄМО !!!
                // Початкові слоти додамо після отримання даних.
                await ResetSessionSlotsAsync();
                Debug.WriteLine("[MainPage] SessionSlots reset finished. Proceeding with API call.");

                // Перевірка скасування після очищення, перед відправкою запитів
                cancellationToken.ThrowIfCancellationRequested();

                // --- КРОК 1: Надсилаємо ОДИН запит на сервер для отримання ВСІХ сесій на дату ---
                var requestData = new Dictionary<string, object>
                {
                    { "action", "get_sessions_by_date" }, // <-- Новий тип запиту
                    { "club_id", ClubId },
                    { "date", _currentDate.ToString("yyyy-MM-dd") }
                };

                Debug.WriteLine("[MainPage] Sending API request: get_sessions_by_date...");
                // Передаємо cancellationToken до SendRequestAsync, якщо він підтримує
                var response = await ApiClient.SendRequestAsync(requestData /*, cancellationToken */); // Надсилаємо ОДИН запит
                Debug.WriteLine("[MainPage] Received API response for get_sessions_by_date.");

                // !!! Перевірка скасування після отримання відповіді !!!
                cancellationToken.ThrowIfCancellationRequested();


                // --- КРОК 2: Обробляємо відповідь ---
                if (response != null && response.ContainsKey("success") && (bool)response["success"])
                {
                    Debug.WriteLine("[MainPage] get_sessions_by_date response success = true.");

                    // Перевіряємо, чи відповідь містить список сесій
                    if (response.TryGetValue("sessions", out object sessionsObject) && sessionsObject is JArray sessionsJArray) // Використовуємо JArray
                    {
                        Debug.WriteLine($"[MainPage] Received {sessionsJArray.Count} sessions.");

                        // Створюємо словник для агрегації зайнятих місць по часових слотах
                        // Ключ: StartTime слота (TimeSpan), Значення: Tuple<PaidVr, PendingVr, PaidPs, PendingPs>
                        // ВИКОРИСТОВУЄМО Tuple, але звертаємося через ItemN
                        var slotOccupancy = new Dictionary<TimeSpan, Tuple<int, int, int, int>>();

                        // Ініціалізуємо словник нулями для всіх DefaultTimeSlots
                        foreach (var timeStr in DefaultTimeSlots)
                        {
                            if (TimeSpan.TryParseExact(timeStr, @"hh\:mm", CultureInfo.InvariantCulture, out TimeSpan startTimeSlot))
                            {
                                // Ініціалізуємо Tuple нулями
                                slotOccupancy[startTimeSlot] = new Tuple<int, int, int, int>(0, 0, 0, 0);
                            }
                            else
                            {
                                Debug.WriteLine($"[ПОМИЛКА КЛІЄНТ] Failed to parse DefaultTimeSlot '{timeStr}'. This slot will not be processed.");
                                // Можливо, додати SessionAvailability в стані помилки?
                            }
                        }

                        // Проходимося по отриманих сесіях та агрегуємо зайняті місця по відповідних часових слотах
                        foreach (var sessionToken in sessionsJArray) // JArray містить JObject для кожної сесії
                        {
                            try
                            {
                                var sessionData = sessionToken.ToObject<Dictionary<string, object>>(); // Перетворюємо JObject в словник

                                // Перевіряємо, чи є необхідні дані сесії
                                if (sessionData != null &&
                                    sessionData.TryGetValue("start_time", out object startTimeObj) && startTimeObj is string startTimeStr &&
                                    sessionData.TryGetValue("end_time", out object endTimeObj) && endTimeObj is string endTimeStr &&
                                    sessionData.TryGetValue("num_people", out object numPeopleObj) &&
                                    sessionData.TryGetValue("session_type", out object sessionTypeObj) && sessionTypeObj is string sessionType &&
                                    sessionData.TryGetValue("payment_status", out object paymentStatusObj) && paymentStatusObj is string paymentStatus)
                                {
                                    int numPeople = 0;
                                    // Перевіряємо тип num_people (може бути long або int)
                                    if (numPeopleObj is long numPeopleLong) numPeople = (int)numPeopleLong;
                                    else if (numPeopleObj is int numPeopleInt) numPeople = numPeopleInt;
                                    // Додано обробку випадку, якщо numPeopleObj не long і не int
                                    else { Debug.WriteLine($"[ПОМИЛКА КЛІЄНТ] Unexpected type for num_people in session data for session {(sessionData.TryGetValue("session_id", out var sid_obj) ? sid_obj : "N/A")}: {numPeopleObj?.GetType().Name}"); continue; }


                                    // Парсимо час початку та кінця сесії
                                    if (TimeSpan.TryParseExact(startTimeStr, @"hh\:mm\:ss", CultureInfo.InvariantCulture, out TimeSpan sessionStartTime) &&
                                        TimeSpan.TryParseExact(endTimeStr, @"hh\:mm\:ss", CultureInfo.InvariantCulture, out TimeSpan sessionEndTime) &&
                                        numPeople > 0 && !string.IsNullOrEmpty(sessionType) && !string.IsNullOrEmpty(paymentStatus))
                                    {
                                        // Визначаємо статус
                                        bool isPaid = paymentStatus == "Paid";
                                        bool isPending = paymentStatus == "Pending";
                                        bool isVrQuest = sessionType == "VR" || sessionType == "Quest";
                                        bool isPs = sessionType == "PS";

                                        if (!isPaid && !isPending)
                                        {
                                            Debug.WriteLine($"[ПОПЕРЕДЖЕННЯ КЛІЄНТ] Session {(sessionData.TryGetValue("session_id", out var sid_obj) ? sid_obj : "N/A")} has unexpected payment_status: {paymentStatus}. Skipping.");
                                            continue; // Пропускаємо сесії з іншим статусом
                                        }
                                        if (!isVrQuest && !isPs)
                                        {
                                            Debug.WriteLine($"[ПОПЕРЕДЖЕННЯ КЛІЄНТ] Session {(sessionData.TryGetValue("session_id", out var sid_obj) ? sid_obj : "N/A")} has unexpected session_type: {sessionType}. Skipping.");
                                            continue; // Пропускаємо сесії з іншим типом
                                        }


                                        // Агрегуємо зайняті місця по всіх часових слотах, які перетинаються з цією сесією
                                        // Перебираємо ключі DefaultTimeSlots
                                        foreach (var timeStrSlot in DefaultTimeSlots)
                                        {
                                            // Парсимо час початку слота
                                            if (TimeSpan.TryParseExact(timeStrSlot, @"hh\:mm", CultureInfo.InvariantCulture, out TimeSpan startTimeSlot))
                                            {
                                                TimeSpan endTimeSlot = startTimeSlot.Add(TimeSpan.FromHours(1)); // Припускаємо 1-годинні слоти

                                                // Перевіряємо перетин: [sessionStartTime, sessionEndTime] ПЕРЕТИНАЄТЬСЯ з [startTimeSlot, endTimeSlot]
                                                // якщо (sessionStartTime < endTimeSlot) AND (sessionEndTime > startTimeSlot)
                                                if (sessionStartTime < endTimeSlot && sessionEndTime > startTimeSlot)
                                                {
                                                    // Отримуємо поточні значення для цього слота
                                                    var currentOccupancy = slotOccupancy.ContainsKey(startTimeSlot) ? slotOccupancy[startTimeSlot] : new Tuple<int, int, int, int>(0, 0, 0, 0);

                                                    // Оновлюємо значення відповідно до типу зони та статусу
                                                    int newPaidVr = currentOccupancy.Item1;
                                                    int newPendingVr = currentOccupancy.Item2;
                                                    int newPaidPs = currentOccupancy.Item3;
                                                    int newPendingPs = currentOccupancy.Item4;

                                                    if (isVrQuest)
                                                    {
                                                        if (isPaid) newPaidVr += numPeople;
                                                        else if (isPending) newPendingVr += numPeople;
                                                    }
                                                    else if (isPs)
                                                    {
                                                        if (isPaid) newPaidPs += numPeople;
                                                        else if (isPending) newPendingPs += numPeople;
                                                    }

                                                    // Зберігаємо оновлені значення назад у словник
                                                    slotOccupancy[startTimeSlot] = new Tuple<int, int, int, int>(newPaidVr, newPendingVr, newPaidPs, newPendingPs);
                                                    // ВИПРАВЛЕНО: Синтаксис інтерполяції рядка
                                                    Debug.WriteLine($"[MainPage] Slot {startTimeSlot.ToString(@"hh\:mm")} updated for session {(sessionData.TryGetValue("session_id", out var sid_obj_inner) ? sid_obj_inner : "N/A")}. Current: PaidVR={slotOccupancy[startTimeSlot].Item1}, PendingVR={slotOccupancy[startTimeSlot].Item2}, PaidPS={slotOccupancy[startTimeSlot].Item3}, PendingPS={slotOccupancy[startTimeSlot].Item4}");
                                                }
                                            }
                                            else
                                            {
                                                Debug.WriteLine($"[ПОМИЛКА КЛІЄНТ] Failed to parse DefaultTimeSlot '{timeStrSlot}' during session processing.");
                                            }
                                        } // Кінець циклу по DefaultTimeSlots
                                    }
                                    else
                                    {
                                        Debug.WriteLine($"[ПОМИЛКА КЛІЄНТ] Failed to parse time/validate fields for session {(sessionData.TryGetValue("session_id", out var sid_obj) ? sid_obj : "N/A")}. Skipping.");
                                    }
                                }
                                else
                                {
                                    Debug.WriteLine($"[ПОМИЛКА КЛІЄНТ] Missing required fields in session data from server for session {(sessionData.TryGetValue("session_id", out var sid_obj) ? sid_obj : "N/A")}. Skipping.");
                                }
                            }
                            catch (Exception sessionProcessEx)
                            {
                                // ВИПРАВЛЕНО: Синтаксис інтерполяції рядка
                                Debug.WriteLine($"[ПОМИЛКА КЛІЄНТ] Error processing session data: {sessionProcessEx.Message}\n{sessionProcessEx.StackTrace}. Skipping session.");
                            }
                        } // Кінець циклу по сесіях JArray

                        // --- КРОК 3: Заповнюємо SessionSlots на основі агрегованих даних ---

                        // Додаємо слоти в колекцію UI в головному потоці
                        Device.BeginInvokeOnMainThread(() =>
                        {
                            // Сортуємо слоти за часом для коректного відображення
                            var sortedTimeSlots = DefaultTimeSlots
                               .Select(timeStr => TimeSpan.TryParseExact(timeStr, @"hh\:mm", CultureInfo.InvariantCulture, out TimeSpan ts) ? (TimeSpan?)ts : null)
                               .Where(ts => ts.HasValue)
                               .OrderBy(ts => ts.Value)
                               .ToList();

                            foreach (var startTimeSlot in sortedTimeSlots)
                            {
                                // Знаходимо або використовуємо нульові значення зайнятості для цього слота
                                var occupancy = slotOccupancy.TryGetValue(startTimeSlot.Value, out var o) ? o : new Tuple<int, int, int, int>(0, 0, 0, 0);

                                // Створюємо новий об'єкт SessionAvailability
                                var sessionItem = new SessionAvailability(startTimeSlot.Value.ToString(@"hh\:mm"));
                                // IsTimeParsedSuccessfully вже встановлено в конструкторі

                                // Встановлюємо отримані дані про зайнятість (це оновить візуальний стан через сеттери)
                                // !!! Звертаємося до елементів Tuple через ItemN !!!
                                sessionItem.PaidVrQuest = occupancy.Item1;
                                sessionItem.PendingVrQuest = occupancy.Item2;
                                sessionItem.PaidPs = occupancy.Item3;
                                sessionItem.PendingPs = occupancy.Item4;

                                // Додаємо в ObservableCollection
                                SessionSlots.Add(sessionItem);
                                // ВИПРАВЛЕНО: Синтаксис інтерполяції рядка
                                Debug.WriteLine($"[MainPage] Added SessionSlot to UI: {sessionItem.Time} (PaidVR:{sessionItem.PaidVrQuest}, PendingVR:{sessionItem.PendingVrQuest}, PaidPS:{sessionItem.PaidPs}, PendingPS:{sessionItem.PendingPs})");
                            }
                            Debug.WriteLine($"[MainPage] Finished populating SessionSlots UI with {SessionSlots.Count} items.");

                        }); // Кінець Device.BeginInvokeOnMainThread


                    }
                    else
                    {
                        Debug.WriteLine("[ПОМИЛКА КЛІЄНТ] get_sessions_by_date response does not contain 'sessions' JArray.");
                        string errorMessage = response.ContainsKey("message") ? response["message"].ToString() : "Невідома помилка формату даних сесій.";
                        Device.BeginInvokeOnMainThread(async () => await DisplayAlert("Помилка даних", errorMessage, "OK"));
                        // TODO: Позначити слоти як помилка
                    }
                }
                else
                {
                    // Обробка помилки відповіді сервера
                    string errorMessage = response != null && response.ContainsKey("message") ? response["message"].ToString() : "Невідома помилка сервера.";
                    Debug.WriteLine($"[ПОМИЛКА КЛІЄНТ] get_sessions_by_date response success = false. Message: {errorMessage}");
                    Device.BeginInvokeOnMainThread(async () => await DisplayAlert("Помилка сервера", $"Не вдалося завантажити сесії: {errorMessage}", "OK"));
                    // TODO: Позначити слоти як помилка
                }

            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine("[MainPage] LoadAvailabilityDataAsync (optimized) was cancelled.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[КРИТИЧНА ПОМИЛКА КЛІЄНТ] Помилка при завантаженні та обробці сесій: {ex.Message}\n{ex.StackTrace}");
                Device.BeginInvokeOnMainThread(async () =>
                {
                    await DisplayAlert("Критична помилка", $"Не вдалося завантажити дані: {ex.Message}", "OK");
                });
                // TODO: Позначити всі слоти як помилка
            }
            finally
            {
                Debug.WriteLine("[MainPage] Finishing LoadAvailabilityDataAsync (optimized).");
                if (!cancellationToken.IsCancellationRequested)
                {
                    Device.BeginInvokeOnMainThread(() =>
                    {
                        loadingIndicator.IsVisible = true;
                        loadingIndicator.IsRunning = false;
                        Debug.WriteLine("[MainPage] loadingIndicator.IsRunning = false (finally)");
                    });
                }
                else
                {
                    Debug.WriteLine("[MainPage] loadingIndicator not hidden (cancelled).");
                }
            }
        }

        private async void OnSessionTapped(object sender, EventArgs e)
        {
            Debug.WriteLine("[MainPage] OnSessionTapped called.");
            // CommandParameter тепер передає об'єкт SessionAvailability
            if (sender is Frame frame && frame.BindingContext is SessionAvailability session)
            {
                Debug.WriteLine($"[MainPage] Tapped on session slot: {session.Time}");
                // Передаємо всю потрібну інформацію на сторінку бронювань
                // session.Time - це рядок "HH:mm"
                // session.StartTime/EndTime - це TimeSpan, що містить час без дати
                // _currentDate - це DateTime, що містить вибрану дату
                // ClubId, ManagerId - вже є в класі

                // Вам може знадобитися передати session.StartTime/EndTime замість або на додаток до session.Time
                await Navigation.PushAsync(new CurrentBookingsPage(ManagerId, ClubId, _currentDate, session.Time /*, session.StartTime, session.EndTime */));
            }
            else
            {
                Debug.WriteLine("[MainPage] OnSessionTapped: CommandParameter is not a SessionAvailability object.");
            }
        }

        // ... решта методів залишаються без змін
        private void OnAddClicked(object sender, EventArgs e)
        {
            // TODO: Реалізуйте додавання замовлення
            DisplayAlert("Add", "Add clicked!", "OK");
        }
        private async void OnNewClicked(object sender, EventArgs e)
        {
            // TODO: Реалізуйте додавання замовлення
            await Navigation.PushAsync(new NotificationsPage(ManagerId, ClubId));
        }
        private async void OnAccountClicked(object sender, EventArgs e)
        {
            // Закриваємо меню, якщо кнопка натиснута з меню
            CloseMenu(sender, e);
            // Переходимо на нову сторінку акаунту, передаючи ID менеджера та клубу
            await Navigation.PushAsync(new AccountPage(ManagerId, ClubId));
        }
        private void OnOrdersClicked(object sender, EventArgs e)
        {
            // TODO: Реалізуйте перегляд замовлень
            DisplayAlert("Orders", "Orders clicked!", "OK");
        }
        private async void OnAnalyticsClicked(object sender, EventArgs e)
        {
            CloseMenu(sender, e); // Закриваємо меню перед виконанням дії
            await Navigation.PushAsync(new AnalyticsPage(ManagerId, ClubId));
            // TODO: Додати перехід на сторінку Аналітики (Navigation.PushAsync(new AnalyticsPage(ManagerId, ClubId)));
        }
        private async void OnDailySummaryClicked(object sender, EventArgs e)
        {
            CloseMenu(sender, e); // Закриваємо меню перед виконанням дії

            // Переходимо на сторінку Підсумку дня, передаючи ID менеджера, клубу та поточну вибрану дату
            await Navigation.PushAsync(new Views.DailySummaryPage(ManagerId, ClubId, _currentDate));
        }

        private async void OnClubsClicked(object sender, EventArgs e)
        {
            CloseMenu(sender, e); // Закриваємо меню перед виконанням дії
            await Navigation.PushAsync(new ClubsPage());
            // TODO: Додати перехід на сторінку Клубів (Navigation.PushAsync(new ClubsPage(ManagerId, ClubId)));
        }

        private async void OnManagersClicked(object sender, EventArgs e)
        {
            CloseMenu(sender, e); // Закриваємо меню перед виконанням дії
            await Navigation.PushAsync(new Views.ManagersPage(/*ManagerId, ClubId*/));
            // TODO: Додати перехід на сторінку Менеджерів (Navigation.PushAsync(new ManagersPage(ManagerId, ClubId)));
        }
    }
}