// crmV1/Views/ManagersPage.xaml.cs

using System;
// ... інші using ...
using Xamarin.Forms;
using Xamarin.Forms.Xaml;
using crmV1.ViewModels; // using ViewModel
using System.Diagnostics;
using crmV1; // using для ManagerDetailPage (перехід на цю сторінку з ViewModel)


namespace crmV1.Views
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class ManagersPage : ContentPage
    {
        // Приватне поле для ViewModel
        private ManagersViewModel viewModel;

        // Конструктор
        public ManagersPage(/*int managerId, int clubId*/)
        {
            InitializeComponent();
            NavigationPage.SetHasNavigationBar(this, false);
            // Ініціалізуємо ViewModel і прив'язуємо його до BindingContext
            viewModel = new ManagersViewModel(this); // Передаємо посилання на поточну сторінку в ViewModel
            BindingContext = viewModel;
        }

        // Метод, що викликається при появі сторінки на екрані
        protected override async void OnAppearing()
        {
            base.OnAppearing();

            // Запускаємо команду завантаження даних при появі сторінки
            await viewModel.LoadManagersCommand.ExecuteAsync();
        }

        // !!! ВИДАЛІТЬ ЦЕЙ МЕТОД !!!
        // Навігація та логіка "Детальніше" тепер повністю обробляються командою NavigateToManagerDetailsCommand у ViewModel.
        // private async void OnManagerSelected(object sender, ItemTappedEventArgs e)
        // {
        // Цей метод більше не потрібен. Його логіка перенесена у ViewModel.
        // }

        // >>> ВИДАЛІТЬ АБО ЗАКОМЕНТУЙТЕ ЦЕЙ МЕТОД, якщо він був обробником події ViewModel <<<
        // private async void ViewModel_NavigateToManagerDetailsRequested(object sender, int managerId)
        // {
        //     // ... код навігації
        // }
        // >>> КІНЕЦЬ ВИДАЛЕННЯ МЕТОДУ ОБРОБНИКА <<<
    }
}