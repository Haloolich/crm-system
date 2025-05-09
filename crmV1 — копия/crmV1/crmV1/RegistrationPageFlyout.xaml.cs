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
    public partial class RegistrationPageFlyout : ContentPage
    {
        public ListView ListView;

        public RegistrationPageFlyout()
        {
            InitializeComponent();

            BindingContext = new RegistrationPageFlyoutViewModel();
            ListView = MenuItemsListView;
        }

        private class RegistrationPageFlyoutViewModel : INotifyPropertyChanged
        {
            public ObservableCollection<RegistrationPageFlyoutMenuItem> MenuItems { get; set; }

            public RegistrationPageFlyoutViewModel()
            {
                MenuItems = new ObservableCollection<RegistrationPageFlyoutMenuItem>(new[]
                {
                    new RegistrationPageFlyoutMenuItem { Id = 0, Title = "Page 1" },
                    new RegistrationPageFlyoutMenuItem { Id = 1, Title = "Page 2" },
                    new RegistrationPageFlyoutMenuItem { Id = 2, Title = "Page 3" },
                    new RegistrationPageFlyoutMenuItem { Id = 3, Title = "Page 4" },
                    new RegistrationPageFlyoutMenuItem { Id = 4, Title = "Page 5" },
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