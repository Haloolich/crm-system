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
    public partial class CurrentBookingsPageFlyout : ContentPage
    {
        public ListView ListView;

        public CurrentBookingsPageFlyout()
        {
            InitializeComponent();

            BindingContext = new CurrentBookingsPageFlyoutViewModel();
            ListView = MenuItemsListView;
        }

        private class CurrentBookingsPageFlyoutViewModel : INotifyPropertyChanged
        {
            public ObservableCollection<CurrentBookingsPageFlyoutMenuItem> MenuItems { get; set; }

            public CurrentBookingsPageFlyoutViewModel()
            {
                MenuItems = new ObservableCollection<CurrentBookingsPageFlyoutMenuItem>(new[]
                {
                    new CurrentBookingsPageFlyoutMenuItem { Id = 0, Title = "Page 1" },
                    new CurrentBookingsPageFlyoutMenuItem { Id = 1, Title = "Page 2" },
                    new CurrentBookingsPageFlyoutMenuItem { Id = 2, Title = "Page 3" },
                    new CurrentBookingsPageFlyoutMenuItem { Id = 3, Title = "Page 4" },
                    new CurrentBookingsPageFlyoutMenuItem { Id = 4, Title = "Page 5" },
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