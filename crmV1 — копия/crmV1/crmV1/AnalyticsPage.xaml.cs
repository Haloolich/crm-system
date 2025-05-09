using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using crmV1.Services; // Припускаємо, що ApiClient знаходиться в цьому просторі імен
using Newtonsoft.Json.Linq; // Для парсингу складної відповіді
using Xamarin.Forms;
using Xamarin.Forms.Xaml;
using System.Globalization; // Додано для форматування Decimal

namespace crmV1
{
    // Допоміжний клас для представлення опції звіту (можна залишити в цьому ж файлі)
    public class ReportOption : INotifyPropertyChanged
    {
        public string Id { get; set; } // Унікальний ідентифікатор для сервера (напр., "revenue", "active_clients")
        public string Name { get; set; } // Ім'я для відображення в UI
        public bool RequiresPeriod { get; set; } // Чи вимагає цей звіт вказання періоду

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    [XamlCompilation(XamlCompilationOptions.Compile)] // Додано для компіляції XAML
    public partial class AnalyticsPage : ContentPage, INotifyPropertyChanged // Імплементуємо INotifyPropertyChanged
    {
        private int _managerId;
        private int _clubId;

        // Список опцій звітів
        public ObservableCollection<ReportOption> ReportOptions { get; set; }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                if (_isBusy != value)
                {
                    _isBusy = value;
                    OnPropertyChanged();
                }
            }
        }

        public AnalyticsPage(int managerId, int clubId)
        {
            InitializeComponent();
            NavigationPage.SetHasNavigationBar(this, false);

            _managerId = managerId;
            _clubId = clubId;

            // Ініціалізуємо список опцій звітів
            ReportOptions = new ObservableCollection<ReportOption>
            {
                new ReportOption { Id = "revenue", Name = "Дохід за період", RequiresPeriod = true, IsSelected = false },
                new ReportOption { Id = "active_clients_period", Name = "Найактивніші клієнти за період", RequiresPeriod = true, IsSelected = false },
                new ReportOption { Id = "active_clients_all_time", Name = "Найактивніші клієнти за весь час", RequiresPeriod = false, IsSelected = false }, // Не вимагає періоду
                new ReportOption { Id = "manager_performance", Name = "Продуктивність менеджерів", RequiresPeriod = true, IsSelected = false },
                new ReportOption { Id = "popular_sessions", Name = "Популярні сеанси за період (тип/час)", RequiresPeriod = true, IsSelected = false },
                new ReportOption { Id = "average_people", Name = "Середня кількість людей за період", RequiresPeriod = true, IsSelected = false },
                new ReportOption { Id = "cancelled_sessions", Name = "Кількість скасованих сеансів за період", RequiresPeriod = true, IsSelected = false }
                 // TODO: Додайте інші звіти тут, напр:
                 // new ReportOption { Id = "discounts_report", Name = "Використання знижок за період", RequiresPeriod = true, IsSelected = false }
            };

            // Встановлюємо BindingContext для прив'язок в XAML (для ReportOptions та IsBusy)
            this.BindingContext = this;

            // Встановлюємо дати за замовчуванням (наприклад, поточний місяць)
            startDatePicker.Date = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            endDatePicker.Date = DateTime.Today;
        }

        // Обробник натискання кнопки "Сформувати звіт(и)"
        private async void OnGenerateReportClicked(object sender, EventArgs e)
        {
            // Перевіряємо, чи вже йде процес
            if (IsBusy)
            {
                await DisplayAlert("Зачекайте", "Зараз формується інший звіт.", "OK");
                return;
            }

            // Отримуємо обрані звіти
            var selectedReports = ReportOptions.Where(op => op.IsSelected).ToList();

            if (!selectedReports.Any())
            {
                await DisplayAlert("Увага", "Будь ласка, виберіть хоча б один звіт.", "OK");
                return;
            }

            // Отримуємо обраний період
            DateTime startDate = startDatePicker.Date;
            DateTime endDate = endDatePicker.Date;

            // Валідація періоду, якщо обрано хоча б один звіт, що вимагає періоду
            // Увага: звіт "Найактивніші клієнти за весь час" НЕ вимагає періоду.
            // Ми перевіряємо, чи обрано *хоча б один* звіт, що вимагає період.
            if (selectedReports.Any(op => op.RequiresPeriod))
            {
                if (startDate > endDate)
                {
                    await DisplayAlert("Помилка", "Дата початку не може бути пізніше за дату закінчення.", "OK");
                    return;
                }
                // Можливо, додати перевірку, що період не надто великий
            }
            // Якщо обрано ЛИШЕ звіти, які НЕ вимагають періоду (напр., тільки "за весь час"),
            // тоді валідація startDate > endDate не потрібна, і дати можуть бути default.
            // Серверна частина ReportingService вже обробляє DateTime.MinValue/MaxValue
            // або відсутність фільтру дат для звітів "за весь час".


            // Очищаємо попередні результати
            reportResultsEditor.Text = "Формування звітів...";
            IsBusy = true; // Показуємо індикатор завантаження

            try
            {
                // Готуємо дані для запиту на сервер
                var requestData = new Dictionary<string, object>
                {
                    { "action", "generate_reports" }, // Нова дія для сервера
                    { "club_id", _clubId },
                    { "manager_id", _managerId }, // Можливо, потрібно для деяких звітів
                    // Надсилаємо дати, навіть якщо деякі звіти їх не потребують.
                    // Сервер вирішить, як їх використовувати.
                    { "start_date", startDate.ToString("yyyy-MM-dd") },
                    { "end_date", endDate.ToString("yyyy-MM-dd") },
                    // Передаємо список Id обраних звітів
                    { "report_ids", selectedReports.Select(op => op.Id).ToList() }
                };

                // Надсилаємо запит на сервер
                var response = await ApiClient.SendRequestAsync(requestData);

                // Обробка відповіді
                if (response != null && response.TryGetValue("success", out object successObj) && successObj.ToString() == "true")
                {
                    // Сервер повертає поле 'reports', яке є JObject (складним об'єктом)
                    if (response.TryGetValue("reports", out object reportsObj) && reportsObj is JObject reportsData)
                    {
                        // <--- СТВОРЮЄМО StringBuilder для збору всіх результатів ---
                        StringBuilder resultsBuilder = new StringBuilder();
                        // Прибираємо загальний заголовок періоду, якщо він не потрібен або дублюється
                        // resultsBuilder.AppendLine($"Звіти за період з {startDate:dd.MM.yyyy} по {endDate:dd.MM.yyyy}:");
                        // resultsBuilder.AppendLine("-------------------------------------");

                        // <--- ПРОХОДИМОСЯ ПО ВСІХ ОБРАНИМ ЗВІТАМ ---
                        // Перебираємо reportOption, щоб відобразити їх в порядку, як на UI
                        foreach (var reportOption in selectedReports)
                        {
                            resultsBuilder.AppendLine($"\n--- {reportOption.Name} ---"); // <--- Заголовок для кожного звіту

                            // Шукаємо дані для цього звіту за його Id у відповіді
                            if (reportsData.TryGetValue(reportOption.Id, out JToken reportResultToken))
                            {
                                // <--- ДОДАЄМО РЕЗУЛЬТАТ КОЖНОГО ЗВІТУ ДО resultsBuilder ---
                                // Тут логіка парсингу та форматування для кожного типу звіту
                                try // Додано try-catch для обробки помилок парсингу конкретного звіту на клієнті
                                {
                                    switch (reportOption.Id)
                                    {
                                        case "revenue":
                                            if (reportResultToken is JObject revenueData)
                                            {
                                                // Використовуємо InvariantCulture для форматування decimal, щоб уникнути коми замість крапки
                                                resultsBuilder.AppendLine($"Загальний дохід: {revenueData["total"]?.ToObject<decimal>().ToString("N2", CultureInfo.InvariantCulture)} грн.");
                                                resultsBuilder.AppendLine($"Готівка: {revenueData["cash"]?.ToObject<decimal>().ToString("N2", CultureInfo.InvariantCulture)} грн.");
                                                resultsBuilder.AppendLine($"Картка: {revenueData["card"]?.ToObject<decimal>().ToString("N2", CultureInfo.InvariantCulture)} грн.");
                                                resultsBuilder.AppendLine($"Онлайн: {revenueData["online"]?.ToObject<decimal>().ToString("N2", CultureInfo.InvariantCulture)} грн.");
                                            }
                                            else { resultsBuilder.AppendLine("Некоректний формат даних для звіту про дохід."); }
                                            break;
                                        case "active_clients_period":
                                        case "active_clients_all_time":
                                            if (reportResultToken is JArray clientsArray)
                                            {
                                                if (clientsArray.Any())
                                                {
                                                    foreach (var clientToken in clientsArray)
                                                    {
                                                        if (clientToken is JObject clientData)
                                                        {
                                                            resultsBuilder.AppendLine($"- {clientData["name"]?.ToString()}: {clientData["bookings"]?.ToObject<long>()} бронювань");
                                                        }
                                                    }
                                                }
                                                else { resultsBuilder.AppendLine("Дані відсутні."); }
                                            }
                                            else { resultsBuilder.AppendLine("Некоректний формат даних для звіту про клієнтів."); }
                                            break;
                                        case "manager_performance":
                                            if (reportResultToken is JArray managersArray)
                                            {
                                                if (managersArray.Any())
                                                {
                                                    foreach (var managerToken in managersArray)
                                                    {
                                                        if (managerToken is JObject managerData)
                                                        {
                                                            resultsBuilder.AppendLine($"- {managerData["manager_name"]?.ToString()}: {managerData["bookings"]?.ToObject<long>()} бронювань");
                                                        }
                                                    }
                                                }
                                                else { resultsBuilder.AppendLine("Дані відсутні."); }
                                            }
                                            else { resultsBuilder.AppendLine("Некоректний формат даних для звіту про продуктивність менеджерів."); }
                                            break;
                                        case "popular_sessions":
                                            if (reportResultToken is JObject popularSessionsData)
                                            {
                                                if (popularSessionsData.HasValues) // Перевірка, чи є дані в об'єкті
                                                {
                                                    foreach (var pair in popularSessionsData)
                                                    {
                                                        // Намагаємося отримати число, якщо можливо, інакше просто текст
                                                        string valueStr = pair.Value?.ToString();
                                                        if (pair.Value.Type == JTokenType.Integer || pair.Value.Type == JTokenType.Float)
                                                        {
                                                            valueStr = pair.Value.ToObject<long>().ToString(); // Або double/decimal
                                                        }
                                                        resultsBuilder.AppendLine($"- {pair.Key}: {valueStr} бронювань");
                                                    }
                                                }
                                                else { resultsBuilder.AppendLine("Дані відсутні."); }
                                            }
                                            else { resultsBuilder.AppendLine("Некоректний формат даних для звіту про популярні сеанси."); }
                                            break;
                                        case "average_people":
                                            // Результат може бути числом або рядком повідомлення про помилку
                                            if (reportResultToken != null)
                                            {
                                                if (reportResultToken.Type == JTokenType.Float || reportResultToken.Type == JTokenType.Integer)
                                                {
                                                    resultsBuilder.AppendLine(reportResultToken.ToObject<decimal>().ToString("N2", CultureInfo.InvariantCulture));
                                                }
                                                else // Якщо це повідомлення про помилку з сервера (рядок)
                                                {
                                                    resultsBuilder.AppendLine(reportResultToken.ToString());
                                                }
                                            }
                                            else { resultsBuilder.AppendLine("Дані відсутні."); }
                                            break;
                                        case "cancelled_sessions":
                                            // Результат може бути числом або рядком повідомлення про помилку
                                            if (reportResultToken != null)
                                            {
                                                if (reportResultToken.Type == JTokenType.Integer)
                                                {
                                                    resultsBuilder.AppendLine(reportResultToken.ToObject<long>().ToString());
                                                }
                                                else // Якщо це повідомлення про помилку з сервера (рядок)
                                                {
                                                    resultsBuilder.AppendLine(reportResultToken.ToString());
                                                }
                                            }
                                            else { resultsBuilder.AppendLine("Дані відсутні."); }
                                            break;
                                        // TODO: Додайте кейси для інших звітів тут
                                        default:
                                            resultsBuilder.AppendLine($"Невідомий формат даних: {reportResultToken?.ToString()}");
                                            break;
                                    }
                                }
                                catch (Exception parseEx)
                                {
                                    // Обробка помилок парсингу на клієнті для конкретного звіту
                                    Console.WriteLine($"[AnalyticsPage] Error parsing result for report '{reportOption.Id}': {parseEx.Message}");
                                    resultsBuilder.AppendLine($"Помилка обробки даних: {parseEx.Message}");
                                }
                                // -------------------------------------------------------------
                            }
                            else
                            {
                                // Якщо сервер не повернув дані для цього звіту
                                resultsBuilder.AppendLine("Дані звіту відсутні (сервер не повернув).");
                            }
                        }
                        resultsBuilder.AppendLine("\n-------------------------------------"); // Додаємо розділювач в кінці

                        // <--- ПРИСВОЮЄМО ЗІБРАНИЙ РЕЗУЛЬТАТ У EDITOR ---
                        reportResultsEditor.Text = resultsBuilder.ToString();
                        // -----------------------------------------------
                    }
                    else
                    {
                        // Сервер не повернув очікуване поле 'reports'
                        string message = response.TryGetValue("message", out object msgObj) ? msgObj.ToString() : "Сервер повернув успіх, але без даних звітів.";
                        reportResultsEditor.Text = $"Помилка: {message}";
                        await DisplayAlert("Помилка даних", message, "OK");
                    }
                }
                else
                {
                    // Обробка помилки відповіді сервера
                    string errorMessage = response != null && response.TryGetValue("message", out object msgObj) ? msgObj.ToString() : "Невідома помилка сервера.";
                    reportResultsEditor.Text = $"Помилка: {errorMessage}";
                    await DisplayAlert("Помилка сервера", $"Не вдалося сформувати звіт: {errorMessage}", "OK");
                }
            }
            catch (Exception ex)
            {
                // Загальна помилка (мережі, парсингу на клієнті)
                Console.WriteLine($"[AnalyticsPage] Error generating reports: {ex.Message}\n{ex.StackTrace}");
                reportResultsEditor.Text = $"Критична помилка: Не вдалося сформувати звіт.\n{ex.Message}";
                await DisplayAlert("Критична помилка", $"Не вдалося сформувати звіт: {ex.Message}", "OK");
            }
            finally
            {
                IsBusy = false; // Ховаємо індикатор завантаження
            }
        }

        // Реалізація INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}