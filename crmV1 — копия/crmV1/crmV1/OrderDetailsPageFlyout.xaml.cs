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
    public partial class OrderDetailsPageFlyout : ContentPage
    {
        public ListView ListView;

        public OrderDetailsPageFlyout()
        {
            InitializeComponent();

            BindingContext = new OrderDetailsPageFlyoutViewModel();
            ListView = MenuItemsListView;
        }

        private class OrderDetailsPageFlyoutViewModel : INotifyPropertyChanged
        {
            public ObservableCollection<OrderDetailsPageFlyoutMenuItem> MenuItems { get; set; }

            public OrderDetailsPageFlyoutViewModel()
            {
                MenuItems = new ObservableCollection<OrderDetailsPageFlyoutMenuItem>(new[]
                {
                    new OrderDetailsPageFlyoutMenuItem { Id = 0, Title = "Page 1" },
                    new OrderDetailsPageFlyoutMenuItem { Id = 1, Title = "Page 2" },
                    new OrderDetailsPageFlyoutMenuItem { Id = 2, Title = "Page 3" },
                    new OrderDetailsPageFlyoutMenuItem { Id = 3, Title = "Page 4" },
                    new OrderDetailsPageFlyoutMenuItem { Id = 4, Title = "Page 5" },
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