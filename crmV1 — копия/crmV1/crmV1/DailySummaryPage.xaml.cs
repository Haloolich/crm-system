using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;
using crmV1.Models;
using crmV1.Services; // Припустимо, що ApiClient знаходиться тут
using Newtonsoft.Json.Linq;
using System.Diagnostics;

// ВИПРАВЛЕНО: Уточніть простір імен, якщо у вас кілька NavigationPage
// Зазвичай достатньо використати повне ім'я: Xamarin.Forms.NavigationPage
// Або можна додати псевдонім, якщо вам дійсно потрібні обидва простори імен
// наприклад: using FormsNav = Xamarin.Forms.NavigationPage;
// та використовувати FormsNav.SetHasNavigationBar(...);

// Переконайтеся, що цей простір імен правильний
namespace crmV1.Views
{
    // ВИПРАВЛЕНО: Partial клас для роботи з XAML
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class DailySummaryPage : ContentPage
    {
        private DateTime _selectedDate;
        private int _clubId;
        private int _managerId; // Можливо, не потрібен для цього запиту, але краще передати

        public ObservableCollection<DailySummarySession> DailySessions { get; set; } = new ObservableCollection<DailySummarySession>();

        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        // ДОДАНО: Лічильник скасованих сесій
        private int _cancelledSessionsCount = 0;

        public DailySummaryPage(int managerId, int clubId, DateTime initialDate)
        {
            // ВИПРАВЛЕНО: Виклик InitializeComponent()
            InitializeComponent();

            // ВИПРАВЛЕНО: Уточнення NavigationPage
            Xamarin.Forms.NavigationPage.SetHasNavigationBar(this, false);

            _managerId = managerId;
            _clubId = clubId;
            _selectedDate = initialDate; // Встановлюємо початкову дату

            // ВИПРАВЛЕНО: Прив'язка ListView - x:Name 'dailySessionsList' тепер має бути доступним
            dailySessionsList.ItemsSource = DailySessions; // Прив'язуємо список до колекції

            UpdateSelectedDateLabel(); // Відображаємо початкову дату

            // Встановлюємо BindingContext, якщо потрібно для конвертерів або інших прив'язок
            // this.BindingContext = this; // Не потрібно для цього сценарію
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            Debug.WriteLine("[DailySummaryPage] OnAppearing called.");
            await LoadDailySummaryAsync(); // Завантажуємо дані при появі сторінки
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            Debug.WriteLine("[DailySummaryPage] OnDisappearing called. Cancelling ongoing requests.");
            CancelLoading(); // Скасовуємо поточні операції при відході
        }

        private void CancelLoading()
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = new CancellationTokenSource();
        }

        private void UpdateSelectedDateLabel()
        {
            // ВИПРАВЛЕНО: selectedDateLabel тепер доступний
            selectedDateLabel.Text = _selectedDate.ToString("dd.MM.yyyy");
            // ВИПРАВЛЕНО: datePicker тепер доступний
            datePicker.Date = _selectedDate; // Синхронізуємо DatePicker
        }

        private void OpenDatePicker(object sender, EventArgs e)
        {
            // ВИПРАВЛЕНО: overlay та datePicker тепер доступні
            overlay.IsVisible = true;
            datePicker.IsVisible = true;
            datePicker.Focus();
        }

        private void CloseDatePicker(object sender, EventArgs e)
        {
            // ВИПРАВЛЕНО: overlay та datePicker тепер доступні
            overlay.IsVisible = false;
            datePicker.IsVisible = false;
        }


        private async void OnDateSelected(object sender, DateChangedEventArgs e)
        {
            Debug.WriteLine($"[DailySummaryPage] OnDateSelected: {_selectedDate.ToShortDateString()} -> {e.NewDate.ToShortDateString()}");
            CancelLoading(); // Скасовуємо попередні запити
            _selectedDate = e.NewDate;
            UpdateSelectedDateLabel();
            CloseDatePicker(null, null); // Ховаємо DatePicker та оверлей
            await LoadDailySummaryAsync(); // Перезавантажуємо дані за нову дату
        }

        private async void PreviousDate(object sender, EventArgs e)
        {
            Debug.WriteLine("[DailySummaryPage] PreviousDate clicked.");
            CancelLoading();
            _selectedDate = _selectedDate.AddDays(-1);
            UpdateSelectedDateLabel();
            await LoadDailySummaryAsync();
        }

        private async void NextDate(object sender, EventArgs e)
        {
            Debug.WriteLine("[DailySummaryPage] NextDate clicked.");
            CancelLoading();
            _selectedDate = _selectedDate.AddDays(1);
            UpdateSelectedDateLabel();
            await LoadDailySummaryAsync();
        }

        private async Task LoadDailySummaryAsync()
        {
            Debug.WriteLine($"[DailySummaryPage] Starting LoadDailySummaryAsync for date {_selectedDate.ToShortDateString()}, Club {_clubId}");

            var cancellationToken = _cancellationTokenSource.Token;

            // Очищаємо попередні дані та показуємо індикатор
            Device.BeginInvokeOnMainThread(() =>
            {
                loadingIndicator.IsVisible = true;
                loadingIndicator.IsRunning = true;
                DailySessions.Clear(); // Очищаємо список

                // Скидаємо лічильники та Label'и перед завантаженням
                _cancelledSessionsCount = 0; // Скидаємо лічильник скасованих
                cancelledSessionsCountLabel.Text = "0"; // Оновлюємо Label скасованих

                totalSessionsLabel.Text = "0"; // Це буде лічильник НЕ скасованих (буде оновлено пізніше)
                totalAmountLabel.Text = "0.00 ₴";
                cashAmountLabel.Text = "0.00 ₴";
                cardAmountLabel.Text = "0.00 ₴";
                // otherAmountLabel.Text = "0.00 ₴"; // Якщо є інші
                Debug.WriteLine("[DailySummaryPage] UI cleared, loading started.");
            });


            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                var requestData = new Dictionary<string, object>
                 {
                     { "action", "get_daily_summary" },
                     { "club_id", _clubId },
                     { "date", _selectedDate.ToString("yyyy-MM-dd") }
                 };


                Debug.WriteLine("[DailySummaryPage] Sending API request: get_daily_summary...");
                var response = await ApiClient.SendRequestAsync(requestData /*, cancellationToken */);
                Debug.WriteLine("[DailySummaryPage] Received API response.");


                cancellationToken.ThrowIfCancellationRequested();

                // --- ДОДАНО: Оголошення локальних змінних для підрахунків ---
                decimal totalPaidAmount = 0; // Сума тільки для Paid сесій
                decimal cashPaidAmount = 0; // Сума готівкою тільки для Paid сесій
                decimal cardPaidAmount = 0; // Сума карткою тільки для Paid сесій
                                            // Dictionary для інших методів оплати (тільки для Paid сесій)
                var otherPaidPaymentMethodsTotal = new Dictionary<string, decimal>();

                // ДОДАНО: Локальна змінна для підрахунку НЕ скасованих сесій для відображення
                int totalSessionsCount = 0; // <-- Оголошення змінної тут
                                            // КІНЕЦЬ ДОДАНО


                if (response != null && response.ContainsKey("success") && (bool)response["success"])
                {
                    Debug.WriteLine("[DailySummaryPage] get_daily_summary response success = true.");
                    if (response.TryGetValue("sessions", out object sessionsObject) && sessionsObject is JArray sessionsJArray)
                    {
                        Debug.WriteLine($"[DailySummaryPage] Received {sessionsJArray.Count} sessions.");

                        var receivedSessionsToDisplay = new List<DailySummarySession>(); // Список сесій ТІЛЬКИ для відображення

                        // Ітеруємо по об'єктах сесій з JArray
                        foreach (var sessionToken in sessionsJArray)
                        {
                            try
                            {
                                // Спробуємо десеріалізувати JSON об'єкт напряму в модель DailySummarySession
                                var sessionData = sessionToken.ToObject<DailySummarySession>();

                                if (sessionData != null) // Перевіряємо, чи десеріалізація була успішною
                                {
                                    // --- ЛОГІКА ФІЛЬТРАЦІЇ ЗА СТАТУСОМ ---
                                    if (sessionData.PaymentStatus.Equals("Cancelled", StringComparison.OrdinalIgnoreCase))
                                    {
                                        // Це скасована сесія
                                        _cancelledSessionsCount++; // Збільшуємо лічильник скасованих
                                        Debug.WriteLine($"[DailySummaryPage] Skipping cancelled session: {sessionData.Time} - {sessionData.ClientName}.");
                                        continue; // Пропускаємо цю сесію в основній логіці (не додаємо до списку, не рахуємо суми)
                                    }
                                    // --- КІНЕЦЬ ЛОГІКИ ФІЛЬТРАЦІЇ ---


                                    // Це не скасована сесія, додаємо її до списку для відображення
                                    receivedSessionsToDisplay.Add(sessionData);

                                    // Розраховуємо підсумки ТІЛЬКИ для Paid сесій (як і раніше)
                                    if (sessionData.PaymentStatus.Equals("Paid", StringComparison.OrdinalIgnoreCase))
                                    {
                                        totalPaidAmount += sessionData.Amount;

                                        // Сума по методам оплати (безпечно обробляємо null та порожні рядки)
                                        switch (sessionData.PaymentMethod?.Trim().ToLower())
                                        {
                                            case "cash":
                                                cashPaidAmount += sessionData.Amount;
                                                break;
                                            case "card":
                                                cardPaidAmount += sessionData.Amount;
                                                break;
                                            default:
                                                // Додаємо до інших, якщо метод не "Cash", "Card"
                                                if (!string.IsNullOrEmpty(sessionData.PaymentMethod) && sessionData.PaymentMethod.Trim().ToLower() != "не вказано") // Ігноруємо "Не вказано"
                                                {
                                                    if (!otherPaidPaymentMethodsTotal.ContainsKey(sessionData.PaymentMethod))
                                                        otherPaidPaymentMethodsTotal[sessionData.PaymentMethod] = 0;
                                                    otherPaidPaymentMethodsTotal[sessionData.PaymentMethod] += sessionData.Amount;
                                                }
                                                break;
                                        }
                                    }
                                    // --- Кінець розрахунку підсумків ---

                                    Debug.WriteLine($"[DailySummaryPage] Successfully processed session for display: {sessionData.Time} - {sessionData.ClientName}. Status: {sessionData.PaymentStatus}, Amount: {sessionData.Amount:C}, Method: {sessionData.PaymentMethod}. NeedsAttention: {sessionData.NeedsAttention}."); // Детальний лог успіху
                                }
                                else
                                {
                                    // Лог, якщо десеріалізація конкретного токена не вдалася
                                    Debug.WriteLine($"[ПОМИЛКА КЛІЄНТ] Failed to deserialize session token into DailySummarySession: {sessionToken.ToString()}");
                                }
                            }
                            catch (Exception sessionProcessEx)
                            {
                                // Ловимо помилки, які виникли під час обробки ОДНОГО токена сесії
                                Debug.WriteLine($"[ПОМИЛКА КЛІЄНТ] Error processing session data token: {sessionProcessEx.Message}\n{sessionProcessEx.StackTrace}. Token data: {sessionToken.ToString()}. Skipping token.");
                            }
                        } // Кінець циклу по сесіях JArray

                        // totalSessionsCount - тепер це лічильник НЕ скасованих сесій, які ми зібрали для відображення
                        totalSessionsCount = receivedSessionsToDisplay.Count; // <-- Тут змінна вже оголошена


                        // Сортуємо сесії для відображення за часом
                        var sortedSessionsToDisplay = receivedSessionsToDisplay.OrderBy(s =>
                        {
                            // Безпечно парсимо час для сортування, обробляємо можливі помилки
                            if (TimeSpan.TryParseExact(s.Time, @"hh\:mm", CultureInfo.InvariantCulture, out TimeSpan ts))
                            {
                                return ts;
                            }
                            return TimeSpan.MaxValue; // Сесії з помилковим часом в кінець списку
                        }).ToList();


                        // Оновлюємо UI в головному потоці
                        Device.BeginInvokeOnMainThread(() =>
                        {
                            // Додаємо відсортовані сесії в ObservableCollection
                            DailySessions.Clear(); // Очищаємо перед додаванням
                            foreach (var session in sortedSessionsToDisplay)
                            {
                                DailySessions.Add(session);
                            }

                            // Оновлюємо Labelи підсумків в UI потоці
                            totalSessionsLabel.Text = totalSessionsCount.ToString(); // <-- Використовуємо оголошену змінну
                            cancelledSessionsCountLabel.Text = _cancelledSessionsCount.ToString(); // Оновлюємо Label скасованих

                            totalAmountLabel.Text = $"{totalPaidAmount:C}"; // Загальна сума оплачених
                            cashAmountLabel.Text = $"{cashPaidAmount:C}"; // Сума готівкою оплачених
                            cardAmountLabel.Text = $"{cardPaidAmount:C}"; // Сума карткою оплачених

                            // TODO: Додати відображення інших методів оплати
                            Debug.WriteLine($"[DailySummaryPage] UI updated. Total sessions displayed: {DailySessions.Count}, Cancelled sessions: {_cancelledSessionsCount}, Total Paid Amount: {totalPaidAmount:C}");
                        });
                    }
                    else
                    {
                        // Обробка помилки: ключ 'sessions' відсутній або невірний формат
                        Debug.WriteLine("[ПОМИЛКА КЛІЄНТ] get_daily_summary response does not contain 'sessions' JArray or it's not a JArray.");
                        string errorMessage = response.ContainsKey("message") ? response["message"].ToString() : "Невірна структура відповіді від сервера (відсутній список сесій).";
                        Device.BeginInvokeOnMainThread(async () => await DisplayAlert("Помилка даних", errorMessage, "OK"));
                    }
                }
                else
                {
                    // Обробка помилки відповіді сервера (success=false)
                    string errorMessage = response != null && response.ContainsKey("message") ? response["message"].ToString() : "Невідома помилка сервера.";
                    Debug.WriteLine($"[ПОМИЛКА КЛІЄНТ] get_daily_summary response success = false. Message: {errorMessage}");
                    Device.BeginInvokeOnMainThread(async () => await DisplayAlert("Помилка сервера", $"Не вдалося завантажити підсумок: {errorMessage}", "OK"));
                }

            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine("[DailySummaryPage] LoadDailySummaryAsync was cancelled.");
                Device.BeginInvokeOnMainThread(() =>
                {
                    loadingIndicator.IsVisible = false;
                    loadingIndicator.IsRunning = false;
                    Debug.WriteLine("[DailySummaryPage] loadingIndicator hidden (cancelled).");
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[КРИТИЧНА ПОМИЛКА КЛІЄНТ] Помилка при завантаженні та обробці підсумку дня: {ex.Message}\n{ex.StackTrace}");
                Device.BeginInvokeOnMainThread(async () =>
                {
                    await DisplayAlert("Критична помилка", $"Не вдалося завантажити дані підсумку: {ex.Message}", "OK");
                    loadingIndicator.IsVisible = false;
                    loadingIndicator.IsRunning = false;
                });
            }
            finally
            {
                Debug.WriteLine("[DailySummaryPage] Finishing LoadDailySummaryAsync.");
                if (!cancellationToken.IsCancellationRequested)
                {
                    Device.BeginInvokeOnMainThread(() =>
                    {
                        if (loadingIndicator.IsRunning)
                        {
                            loadingIndicator.IsVisible = false;
                            loadingIndicator.IsRunning = false;
                            Debug.WriteLine("[DailySummaryPage] loadingIndicator hidden (finally).");
                        }
                    });
                }
                else
                {
                    Debug.WriteLine("[DailySummaryPage] loadingIndicator not hidden (cancelled).");
                }
            }
        }

        // ... решта методів DailySummaryPage.xaml.cs ...
        // Метод для обробки натискання на елемент списку (якщо потрібно)
        /*
        private async void OnSessionTapped(object sender, EventArgs e)
        {
            if (sender is Frame frame && frame.BindingContext is DailySummarySession session)
            {
                 Debug.WriteLine($"[DailySummaryPage] Tapped on session: {session.SessionId}");
                 // TODO: Можливо, відкрити сторінку деталей сесії
                 // await Navigation.PushAsync(new SessionDetailsPage(session.SessionId, _clubId, _managerId));
            }
        }
        */
    }
    public class DailySummarySession // Можна наслідуватись від BaseModel, якщо використовуєте PropertyChanged
    {
        public int SessionId { get; set; } // Додамо ID, може знадобитись
        public string Time { get; set; } // Час сесії (HH:mm)
        public string ClientName { get; set; } // Ім'я клієнта
        public string PaymentStatus { get; set; } // Статус оплати
        public decimal Amount { get; set; } // Сума оплати
        public string PaymentMethod { get; set; } // Метод оплати (Cash, Card, etc.)
        public string SessionType { get; set; } // Тип сесії (VR, PS, Quest)

        // Розраховуване поле для визначення, чи потрібна увага
        // Статус "New" та "Pending" є стандартними неоплаченими/очікуючими.
        // Все інше (Paid, Cancelled, Refunded, etc.) може потребувати уваги або перегляду.
        public bool NeedsAttention
        {
            get
            {
                // Перевіряємо, чи статус НЕ дорівнює "New" або "Pending" (без врахування регістру)
                return !string.Equals(PaymentStatus, "New", StringComparison.OrdinalIgnoreCase) &&
                       !string.Equals(PaymentStatus, "Pending", StringComparison.OrdinalIgnoreCase);
            }
        }

        // Можливо, властивість для фонового кольору, яку можна встановити в логіці
        // або через Converter на базі NeedsAttention
        // public Color BackgroundColor => NeedsAttention ? Color.FromHex("#FFEB3B") : Color.Transparent; // Приклад жовтого

        // Для простоти, можемо додати форматований рядок
        public string DisplayString => $"{Time} - {ClientName} ({PaymentStatus}, {Amount:C} by {PaymentMethod})";

        // Можна додати конструктор
        public DailySummarySession()
        {
            // Ініціалізація за замовчуванням
            Time = "N/A";
            ClientName = "Невідомо";
            PaymentStatus = "Невідомо";
            Amount = 0;
            PaymentMethod = "Невідомо";
            SessionType = "Невідомо";
        }
    }
}