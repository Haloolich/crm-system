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
    public partial class ManagersPageFlyout : ContentPage
    {
        public ListView ListView;

        public ManagersPageFlyout()
        {
            InitializeComponent();

            BindingContext = new ManagersPageFlyoutViewModel();
            ListView = MenuItemsListView;
        }

        class ManagersPageFlyoutViewModel : INotifyPropertyChanged
        {
            public ObservableCollection<ManagersPageFlyoutMenuItem> MenuItems { get; set; }
            
            public ManagersPageFlyoutViewModel()
            {
                MenuItems = new ObservableCollection<ManagersPageFlyoutMenuItem>(new[]
                {
                    new ManagersPageFlyoutMenuItem { Id = 0, Title = "Page 1" },
                    new ManagersPageFlyoutMenuItem { Id = 1, Title = "Page 2" },
                    new ManagersPageFlyoutMenuItem { Id = 2, Title = "Page 3" },
                    new ManagersPageFlyoutMenuItem { Id = 3, Title = "Page 4" },
                    new ManagersPageFlyoutMenuItem { Id = 4, Title = "Page 5" },
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