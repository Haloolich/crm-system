using Xamarin.Forms;

namespace crmV1
{
    public partial class App : Application
    {
        public static string ServerIP { get; set; } = "192.168.0.103";  // Default IP
        public static int ServerPort { get; set; } = 8888;
        public App()
        {
            InitializeComponent();

            MainPage = new NavigationPage(new LoginPage()) //  LoginPage тепер головна сторінка
            {
                BarBackgroundColor = Color.Transparent, // Зробити фон прозорим
            };
        }

        protected override void OnStart()
        {
        }

        protected override void OnSleep()
        {
        }

        protected override void OnResume()
        {
        }
    }
}