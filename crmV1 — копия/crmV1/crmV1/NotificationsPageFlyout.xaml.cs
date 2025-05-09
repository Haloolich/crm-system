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
    public partial class NotificationsPageFlyout : ContentPage
    {
        public ListView ListView;

        public NotificationsPageFlyout()
        {
            InitializeComponent();

            BindingContext = new NotificationsPageFlyoutViewModel();
            ListView = MenuItemsListView;
        }

        class NotificationsPageFlyoutViewModel : INotifyPropertyChanged
        {
            public ObservableCollection<NotificationsPageFlyoutMenuItem> MenuItems { get; set; }
            
            public NotificationsPageFlyoutViewModel()
            {
                MenuItems = new ObservableCollection<NotificationsPageFlyoutMenuItem>(new[]
                {
                    new NotificationsPageFlyoutMenuItem { Id = 0, Title = "Page 1" },
                    new NotificationsPageFlyoutMenuItem { Id = 1, Title = "Page 2" },
                    new NotificationsPageFlyoutMenuItem { Id = 2, Title = "Page 3" },
                    new NotificationsPageFlyoutMenuItem { Id = 3, Title = "Page 4" },
                    new NotificationsPageFlyoutMenuItem { Id = 4, Title = "Page 5" },
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