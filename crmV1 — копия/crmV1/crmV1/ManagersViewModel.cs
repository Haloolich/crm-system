// crmV1/ViewModels/ManagersViewModel.cs
using crmV1.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Windows.Input;
using Xamarin.Forms;
using Xamarin.CommunityToolkit.ObjectModel; // Переконайтесь, що цей using є
using crmV1.Services; // Переконайтесь, що цей using є (для ManagerService та ClubService)
using crmV1.Converters; // Додано using для конвертерів
using System.Globalization; // Додано using для CultureInfo

namespace crmV1.ViewModels
{
    // Клас, що відповідає за логіку та дані сторінки менеджерів
    public class ManagersViewModel : BaseViewModel
    {
        // >>> ПОЛЯ ДЛЯ ЕКЗЕМПЛЯРІВ СЕРВІСІВ (якщо вони не статичні) <<<
        private readonly ManagerService _managerService;
        private readonly ClubService _clubService;
        // >>> КІНЕЦЬ ПОЛІВ ДЛЯ СЕРВІСІВ <<<


        // !!! ВИДАЛІТЬ ЦЕ НЕВИКОРИСТОВУВАНЕ ПОЛЕ !!!
        // private readonly Page _currentPage;

        // >>> ПОЛЕ ДЛЯ ПОСИЛАННЯ НА СТОРІНКУ ДЛЯ НАВІГАЦІЇ ТА DISPLAYALERT <<<
        private Page _page;
        // >>> КІНЕЦЬ ПОЛЯ <<<


        public ObservableCollection<Grouping<string, Manager>> ManagersGrouped { get; } = new ObservableCollection<Grouping<string, Manager>>();

        // >>> ЗМІНЕНО: Команда для переходу на сторінку деталей менеджера (приймає int ID) <<<
        public Command<int> NavigateToManagerDetailsCommand { get; }
        // >>> КІНЕЦЬ ЗМІН <<<

        // Команда для показу деталей менеджера у спливаючому вікні (якщо потрібна)
        public AsyncCommand<Manager> ShowDetailsCommand { get; }


        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        private int _totalManagerCount;
        public int TotalManagerCount
        {
            get => _totalManagerCount;
            set => SetProperty(ref _totalManagerCount, value);
        }

        // Видимість повідомлення "Немає менеджерів" (залежить від IsLoading та TotalManagerCount)
        public bool ShowNoManagersMessage => !IsLoading && TotalManagerCount == 0;

        // Команди для UI (завантаження, звільнення, найму)
        public AsyncCommand LoadManagersCommand { get; }
        public AsyncCommand<Manager> DismissManagerCommand { get; }
        public AsyncCommand<Manager> HireManagerCommand { get; }


        // Конструктор
        public ManagersViewModel(Page page) // Приймаємо Page тут
        {
            // >>> ПРИСВОЮЄМО ПОЛЕ <<<
            _page = page;
            // >>> КІНЕЦЬ ПРИСВОЮВАННЯ <<<

            // >>> ІНІЦІАЛІЗАЦІЯ СЕРВІСІВ (якщо вони не статичні) <<<
            _managerService = new ManagerService(); // TODO: Переконайтесь, що ManagerService існує і коректно ініціалізується
            _clubService = new ClubService(); // TODO: Переконайтесь, що ClubService існує і коректно ініціалізується
            // >>> КІНЕЦЬ ІНІЦІАЛІЗАЦІЇ <<<


            // Ініціалізація команд
            LoadManagersCommand = new AsyncCommand(ExecuteLoadManagersCommand);

            // >>> ЗМІНЕНО: Ініціалізація команди НАВІГАЦІЇ - вона викликає ExecuteNavigateToManagerDetailsCommand з int ID !!! <<<
            NavigateToManagerDetailsCommand = new Command<int>(async (managerId) => await ExecuteNavigateToManagerDetailsCommand(managerId));
            // >>> КІНЕЦЬ ЗМІН <<<

            // Ініціалізація команди SHOW DETAILS (якщо ви хочете мати окрему кнопку для спливаючого вікна)
            ShowDetailsCommand = new AsyncCommand<Manager>(ExecuteShowDetailsCommand);

            // Ініціалізація команд для звільнення/найму
            DismissManagerCommand = new AsyncCommand<Manager>(ExecuteDismissManagerCommand);
            HireManagerCommand = new AsyncCommand<Manager>(ExecuteHireManagerCommand);


            // Початкове завантаження може бути тут, або в OnAppearing сторінки
            // Task.Run(async () => await ExecuteLoadManagersCommand());
        }

        // >>> ЗМІНЕНО МЕТОД ВИКОНАННЯ КОМАНДИ НАВІГАЦІЇ ДО ДЕТАЛЕЙ <<<
        // Цей метод викликається командою NavigateToManagerDetailsCommand, отримує int managerIdToEdit
        private async Task ExecuteNavigateToManagerDetailsCommand(int managerIdToEdit)
        {
            // Перевіряємо IsLoading на початку, щоб не запускати навігацію під час завантаження
            if (IsLoading)
                return;

            // Валідуємо ID (тепер це int, а не Manager об'єкт)
            if (managerIdToEdit > 0)
            {
                Debug.WriteLine($"[ManagersViewModel] Navigating to details for manager ID: {managerIdToEdit}");
                // Виконуємо навігацію, використовуючи поле _page
                if (_page != null)
                {
                    // Переходимо на сторінку деталей, передаючи отриманий ID
                    // Переконайтесь, що ManagerDetailPage знаходиться в просторі імен crmV1
                    // або додайте відповідний using у ManagersViewModel.cs
                    await _page.Navigation.PushAsync(new crmV1.ManagerDetailPage(managerIdToEdit));
                }
                else
                {
                    Debug.WriteLine("[ManagersViewModel] Page reference is null in ViewModel. Cannot navigate.");
                    // Обробка помилки (напр., MessageCenter або кинути виняток)
                    await (Application.Current.MainPage)?.DisplayAlert("Системна помилка", "Не вдалося виконати навігацію (об'єкт сторінки недоступний).", "OK");
                }
            }
            else
            {
                // Цей лог означатиме, що CommandParameter був Binding ManagerId, але він <= 0
                Debug.WriteLine($"[ManagersViewModel] Cannot navigate. Manager ID is invalid or not provided by binding: {managerIdToEdit}");
                await (_page ?? Application.Current.MainPage)?.DisplayAlert("Помилка", "ID менеджера недійсний або не отримано.", "OK");
            }
        }
        // >>> КІНЕЦЬ ЗМІНЕНОГО МЕТОДУ НАВІГАЦІЇ <<<


        // Метод для завантаження та групування менеджерів (викликається LoadManagersCommand)
        private async Task ExecuteLoadManagersCommand()
        {
            // Перевіряємо IsLoading на початку, щоб уникнути подвійного завантаження
            if (IsLoading)
                return;

            IsLoading = true;
            // Очищаємо колекцію ОДРАЗУ, ЩОБ UI ОНОВИВСЯ
            Device.BeginInvokeOnMainThread(() => ManagersGrouped.Clear());


            List<Manager> managers = new List<Manager>();
            List<Club> clubs = null;

            try
            {
                // --- Фонова робота (отримання та обробка даних) ---

                // >>> ВИКЛИКАЄМО ЧЕРЕЗ ПОЛЯ ЕКЗЕМПЛЯРІВ СЕРВІСІВ (якщо вони не статичні) <<<
                clubs = await _clubService.GetClubsAsync(); // TODO: Переконайтесь, що метод існує
                managers = await _managerService.GetManagersAsync(); // TODO: Переконайтесь, що метод існує
                // >>> КІНЕЦЬ ВИКЛИКІВ <<<


                // Встановлюємо ClubName, якщо API не повернуло його
                if (clubs != null && clubs.Any())
                {
                    foreach (var manager in managers)
                    {
                        if (string.IsNullOrEmpty(manager.ClubName) || manager.ClubName.StartsWith("Club ID:")) // Перевіряємо, чи ClubName не встановлено або є "невідомим"
                        {
                            var club = clubs.FirstOrDefault(c => c.ClubId == manager.ClubId);
                            manager.ClubName = club?.Name ?? $"Club ID: {manager.ClubId} (Невідомий)";
                        }
                    }
                }
                else
                {
                    // Якщо список клубів не завантажено або порожній,
                    // використовуємо ClubId для назви групи або зазначаємо, що невідомо
                    foreach (var manager in managers)
                    {
                        if (string.IsNullOrEmpty(manager.ClubName)) // Встановлюємо, тільки якщо ClubName ще не встановлено сервером
                        {
                            manager.ClubName = $"Club ID: {manager.ClubId} (Невідомий клуб)";
                        }
                    }
                }

                // Розділяємо та групуємо менеджерів
                var hiredManagers = managers.Where(m => !m.IsDismissed).ToList();
                var dismissedManagers = managers.Where(m => m.IsDismissed).ToList();

                var groupedHired = hiredManagers
                    .OrderBy(m => m.ClubName)
                    .ThenBy(m => m.Name)
                    .GroupBy(m => m.ClubName)
                    .Select(group => new Grouping<string, Manager>(group.Key, group))
                    .ToList();

                var sortedDismissed = dismissedManagers.OrderBy(m => m.Name).ToList();


                // --- Оновлення UI (ТІЛЬКИ на головному потоці) ---
                Device.BeginInvokeOnMainThread(() =>
                {
                    // Колекцію вже очистили на початку методу

                    foreach (var group in groupedHired)
                    {
                        ManagersGrouped.Add(group);
                    }

                    if (sortedDismissed.Any())
                    {
                        ManagersGrouped.Add(new Grouping<string, Manager>("Звільнені Менеджери", sortedDismissed));
                    }

                    TotalManagerCount = managers.Count; // Оновлюємо кількість після завантаження
                    Debug.WriteLine($"[ManagersViewModel] UI Updated. Total Managers: {TotalManagerCount}, Groups: {ManagersGrouped.Count}");
                });

                // Показуємо попередження про клуби (на головному потоці)
                if (managers.Any() && (clubs == null || !clubs.Any()))
                {
                    Device.BeginInvokeOnMainThread(async () =>
                    {
                        // Використовуємо поле _page для DisplayAlert
                        await (_page?.DisplayAlert("Помилка", "Не вдалося завантажити список клубів. Назви клубів можуть відображатись як ID.", "OK") ?? Task.FromResult(false)); // Безпечний виклик DisplayAlert
                    });
                }

                Debug.WriteLine($"[ManagersViewModel] Background processing finished.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ManagersViewModel] Error loading managers: {ex.Message}\n{ex.StackTrace}");
                // Оновлення UI при помилці (на головному потоці)
                Device.BeginInvokeOnMainThread(async () =>
                {
                    // Колекцію вже очистили на початку
                    TotalManagerCount = 0; // Встановлюємо 0, щоб показати повідомлення "Немає менеджерів"
                                           // Використовуємо поле _page для DisplayAlert
                    await (_page?.DisplayAlert("Помилка завантаження", $"Не вдалося завантажити список менеджерів: {ex.Message}", "OK") ?? Task.FromResult(false)); // Безпечний виклик DisplayAlert
                });
            }
            finally
            {
                // Встановлюємо IsLoading в false в кінці, незалежно від успіху
                IsLoading = false;
                // ShowNoManagersMessage оновить UI завдяки SetProperty override при зміні IsLoading або TotalManagerCount
            }
        }

        // Метод для обробки натискання кнопки "Детальніше" (показує спливаюче вікно)
        // Цей метод використовується командою ShowDetailsCommand.
        // Якщо кнопка "Детальніше" має переходити на нову сторінку, то ця команда (ShowDetailsCommand)
        // та прив'язка до неї в XAML, ймовірно, зайві.
        private async Task ExecuteShowDetailsCommand(Manager manager)
        {
            if (IsLoading) // Не дозволяти під час завантаження
                return;

            if (manager == null)
                return;

            Debug.WriteLine($"[ManagersViewModel] ShowDetailsCommand for manager ID {manager.ManagerId}");

            string statusText = new ManagerStatusTextConverter().Convert(manager.Status, typeof(string), null, CultureInfo.CurrentCulture)?.ToString() ?? manager.Status;

            string detailsMessage = $"ID: {manager.ManagerId}\n" +
                                   $"Ім'я: {manager.Name}\n" +
                                   $"Телефон: {manager.PhoneNumber}\n" +
                                   $"Клуб: {manager.ClubName}\n" +
                                   $"Поточна роль: {manager.Role}\n" +
                                   $"Статус: {statusText}";

            // Використовуємо поле _page для DisplayAlert
            await (_page?.DisplayAlert("Деталі менеджера", detailsMessage, "OK") ?? Task.FromResult(false)); // Безпечний виклик DisplayAlert
        }

        // Метод для обробки натискання кнопки "Звільнити" (викликається DismissManagerCommand)
        private async Task ExecuteDismissManagerCommand(Manager manager)
        {
            if (IsLoading) // Не дозволяти під час завантаження
                return;

            if (manager == null || manager.IsDismissed)
                return;

            // CS0019 виправлено
            // Використовуємо поле _page для DisplayAlert
            bool confirm = await (_page?.DisplayAlert("Підтвердження", $"Ви впевнені, що хочете звільнити менеджера {manager.Name}?", "Так", "Скасувати") ?? Task.FromResult(false)); // Безпечний виклик DisplayAlert


            if (confirm)
            {
                IsLoading = true; // Встановлюємо IsLoading до виклику сервісу
                try
                {
                    // >>> ВИКЛИКАЄМО ЧЕРЕЗ ПОЛЕ ЕКЗЕМПЛЯРА СЕРВІСУ <<<
                    bool success = await _managerService.UpdateManagerStatusAsync(manager.ManagerId, "Dismissed"); // TODO: Переконайтесь, що метод існує
                    // >>> КІНЕЦЬ ВИКЛИКУ <<<

                    if (success)
                    {
                        Debug.WriteLine($"[ManagersViewModel] Successfully dismissed manager ID {manager.ManagerId}. Reloading list.");
                        // Перезавантажуємо весь список, щоб оновити групування та статус UI
                        await ExecuteLoadManagersCommand(); // Перезавантажить список
                                                            // Використовуємо поле _page для DisplayAlert
                        await (_page?.DisplayAlert("Успіх", $"{manager.Name} звільнений.", "OK") ?? Task.FromResult(false)); // Безпечний виклик DisplayAlert
                    }
                    else
                    {
                        // Обробка помилки на сервері (напр., менеджер не знайдений)
                        Debug.WriteLine($"[ManagersViewModel] Failed to dismiss manager ID {manager.ManagerId}. Service returned false.");
                        // Використовуємо поле _page для DisplayAlert
                        await (_page?.DisplayAlert("Помилка", $"Не вдалося звільнити менеджера {manager.Name}.", "OK") ?? Task.FromResult(false)); // Безпечний виклик DisplayAlert
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ManagersViewModel] Error dismissing manager: {ex.Message}\n{ex.StackTrace}");
                    // Використовуємо поле _page для DisplayAlert
                    await (_page?.DisplayAlert("Помилка", $"Не вдалося звільнити менеджера: {ex.Message}", "OK") ?? Task.FromResult(false)); // Безпечний виклик DisplayAlert
                }
                finally
                {
                    IsLoading = false; // Вимикаємо IsLoading в кінці
                }
            }
        }

        // Метод для обробки натискання кнопки "Найняти" (викликається HireManagerCommand)
        private async Task ExecuteHireManagerCommand(Manager manager)
        {
            if (IsLoading) // Не дозволяти під час завантаження
                return;

            if (manager == null || !manager.IsDismissed)
                return;

            // CS0019 виправлено
            // Використовуємо поле _page для DisplayAlert
            bool confirm = await (_page?.DisplayAlert("Підтвердження", $"Ви впевнені, що хочете найняти менеджера {manager.Name} назад?", "Так", "Скасувати") ?? Task.FromResult(false)); // Безпечний виклик DisplayAlert


            if (confirm)
            {
                IsLoading = true; // Встановлюємо IsLoading до виклику сервісу
                try
                {
                    // >>> ВИКЛИКАЄМО ЧЕРЕЗ ПОЛЕ ЕКЗЕМПЛЯРА СЕРВІСУ <<<
                    bool success = await _managerService.UpdateManagerStatusAsync(manager.ManagerId, "Hired"); // TODO: Переконайтесь, що метод існує
                    // >>> КІНЕЦЬ ВИКЛИКУ <<<


                    if (success)
                    {
                        Debug.WriteLine($"[ManagersViewModel] Successfully hired manager ID {manager.ManagerId}. Reloading list.");
                        // Перезавантажуємо весь список, щоб оновити групування та статус UI
                        await ExecuteLoadManagersCommand(); // Перезавантажить список
                                                            // Використовуємо поле _page для DisplayAlert
                        await (_page?.DisplayAlert("Успіх", $"{manager.Name} знову найнятий.", "OK") ?? Task.FromResult(false)); // Безпечний виклик DisplayAlert
                    }
                    else
                    {
                        // Обробка помилки на сервері
                        Debug.WriteLine($"[ManagersViewModel] Failed to hire manager ID {manager.ManagerId}. Service returned false.");
                        // Використовуємо поле _page для DisplayAlert
                        await (_page?.DisplayAlert("Помилка", $"Не вдалося найняти менеджера {manager.Name}.", "OK") ?? Task.FromResult(false)); // Безпечний виклик DisplayAlert
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ManagersViewModel] Error hiring manager: {ex.Message}\n{ex.StackTrace}");
                    // Використовуємо поле _page для DisplayAlert
                    await (_page?.DisplayAlert("Помилка", $"Не вдалося найняти менеджера: {ex.Message}", "OK") ?? Task.FromResult(false)); // Безпечний виклик DisplayAlert
                }
                finally
                {
                    IsLoading = false; // Вимикаємо IsLoading в кінці
                }
            }
        }


        // Оновлення видимості повідомлення про відсутність менеджерів при зміні TotalManagerCount або IsLoading
        // Цей метод вже був і має працювати коректно для оновлення ShowNoManagersMessage
        protected override bool SetProperty<T>(ref T backingStore, T value, [System.Runtime.CompilerServices.CallerMemberName] string propertyName = "")
        {
            bool changed = base.SetProperty(ref backingStore, value, propertyName);
            if (changed && (propertyName == nameof(IsLoading) || propertyName == nameof(TotalManagerCount)))
            {
                OnPropertyChanged(nameof(ShowNoManagersMessage));
            }
            return changed;
        }

        // Переконайтесь, що BaseViewModel реалізує INotifyPropertyChanged та має SetProperty/OnPropertyChanged
        // ... (BaseViewModel тут, якщо ви його включили) ...
    }

    // Переконайтесь, що клас Grouping доступний.
    // Якщо він не з Xamarin.CommunityToolkit.ObjectModel, вам потрібно мати його визначення.
    // ... (Grouping тут, якщо потрібно) ...
// Переконайтесь, що клас Grouping доступний.
 // Якщо він не з Xamarin.CommunityToolkit.ObjectModel, вам потрібно мати його визначення.

// Приклад BaseViewModel (залиште, якщо у вас є, інакше використовуйте цей)
public class BaseViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual bool SetProperty<T>(ref T backingStore, T value,
            [System.Runtime.CompilerServices.CallerMemberName] string propertyName = "")
        {
            if (EqualityComparer<T>.Default.Equals(backingStore, value))
                return false;

            backingStore = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    // Групування для ObservableCollection (переконайтесь, що цей клас доступний, напр. з Xamarin.CommunityToolkit.ObjectModel)
    // Якщо ви використовуєте Xamarin.CommunityToolkit.ObjectModel, він вже там є.
    // Якщо ні, вам потрібно створити свій власний клас Grouping.
    // namespace Xamarin.CommunityToolkit.ObjectModel
    // {
    //     public class Grouping<TKey, TItem> : ObservableCollection<TItem>
    //     {
    //         public TKey Key { get; private set; }
    //         public Grouping(TKey key, IEnumerable<TItem> items)
    //         {
    //             Key = key;
    //             foreach (var item in items)
    //             {
    //                 Items.Add(item);
    //             }
    //         }
    //     }
    // }
}