using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace crmV1
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class LoginPageFlyout : ContentPage
    {
        public ListView ListView;

        public LoginPageFlyout()
        {
            InitializeComponent();

            BindingContext = new LoginPageFlyoutViewModel();
            ListView = MenuItemsListView;
        }

        private class LoginPageFlyoutViewModel : INotifyPropertyChanged
        {
            public ObservableCollection<LoginPageFlyoutMenuItem> MenuItems { get; set; }

            public LoginPageFlyoutViewModel()
            {
                MenuItems = new ObservableCollection<LoginPageFlyoutMenuItem>(new[]
                {
                    new LoginPageFlyoutMenuItem { Id = 0, Title = "Page 1" },
                    new LoginPageFlyoutMenuItem { Id = 1, Title = "Page 2" },
                    new LoginPageFlyoutMenuItem { Id = 2, Title = "Page 3" },
                    new LoginPageFlyoutMenuItem { Id = 3, Title = "Page 4" },
                    new LoginPageFlyoutMenuItem { Id = 4, Title = "Page 5" },
                });
            }

            #region INotifyPropertyChanged Implementation
            public event PropertyChangedEventHandler PropertyChanged;
            void OnPropertyChanged([CallerMemberName] string propertyName = "")
            {
                if (PropertyChanged == null)
                    return;

                PropertyChanged.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
            #endregion
        }
    }
}