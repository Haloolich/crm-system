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
    public partial class ManagerDetailPageFlyout : ContentPage
    {
        public ListView ListView;

        public ManagerDetailPageFlyout()
        {
            InitializeComponent();

            BindingContext = new ManagerDetailPageFlyoutViewModel();
            ListView = MenuItemsListView;
        }

        class ManagerDetailPageFlyoutViewModel : INotifyPropertyChanged
        {
            public ObservableCollection<ManagerDetailPageFlyoutMenuItem> MenuItems { get; set; }
            
            public ManagerDetailPageFlyoutViewModel()
            {
                MenuItems = new ObservableCollection<ManagerDetailPageFlyoutMenuItem>(new[]
                {
                    new ManagerDetailPageFlyoutMenuItem { Id = 0, Title = "Page 1" },
                    new ManagerDetailPageFlyoutMenuItem { Id = 1, Title = "Page 2" },
                    new ManagerDetailPageFlyoutMenuItem { Id = 2, Title = "Page 3" },
                    new ManagerDetailPageFlyoutMenuItem { Id = 3, Title = "Page 4" },
                    new ManagerDetailPageFlyoutMenuItem { Id = 4, Title = "Page 5" },
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