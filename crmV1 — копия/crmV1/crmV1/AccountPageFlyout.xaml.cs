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
    public partial class AccountPageFlyout : ContentPage
    {
        public ListView ListView;

        public AccountPageFlyout()
        {
            InitializeComponent();

            BindingContext = new AccountPageFlyoutViewModel();
            ListView = MenuItemsListView;
        }

        private class AccountPageFlyoutViewModel : INotifyPropertyChanged
        {
            public ObservableCollection<AccountPageFlyoutMenuItem> MenuItems { get; set; }

            public AccountPageFlyoutViewModel()
            {
                MenuItems = new ObservableCollection<AccountPageFlyoutMenuItem>(new[]
                {
                    new AccountPageFlyoutMenuItem { Id = 0, Title = "Page 1" },
                    new AccountPageFlyoutMenuItem { Id = 1, Title = "Page 2" },
                    new AccountPageFlyoutMenuItem { Id = 2, Title = "Page 3" },
                    new AccountPageFlyoutMenuItem { Id = 3, Title = "Page 4" },
                    new AccountPageFlyoutMenuItem { Id = 4, Title = "Page 5" },
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