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
    public partial class ShiftPageFlyout : ContentPage
    {
        public ListView ListView;

        public ShiftPageFlyout()
        {
            InitializeComponent();

            BindingContext = new ShiftPageFlyoutViewModel();
            ListView = MenuItemsListView;
        }

        private class ShiftPageFlyoutViewModel : INotifyPropertyChanged
        {
            public ObservableCollection<ShiftPageFlyoutMenuItem> MenuItems { get; set; }

            public ShiftPageFlyoutViewModel()
            {
                MenuItems = new ObservableCollection<ShiftPageFlyoutMenuItem>(new[]
                {
                    new ShiftPageFlyoutMenuItem { Id = 0, Title = "Page 1" },
                    new ShiftPageFlyoutMenuItem { Id = 1, Title = "Page 2" },
                    new ShiftPageFlyoutMenuItem { Id = 2, Title = "Page 3" },
                    new ShiftPageFlyoutMenuItem { Id = 3, Title = "Page 4" },
                    new ShiftPageFlyoutMenuItem { Id = 4, Title = "Page 5" },
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