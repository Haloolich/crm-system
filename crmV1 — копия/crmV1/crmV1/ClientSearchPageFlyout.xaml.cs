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
    public partial class ClientSearchPageFlyout : ContentPage
    {
        public ListView ListView;

        public ClientSearchPageFlyout()
        {
            InitializeComponent();

            BindingContext = new ClientSearchPageFlyoutViewModel();
            ListView = MenuItemsListView;
        }

        class ClientSearchPageFlyoutViewModel : INotifyPropertyChanged
        {
            public ObservableCollection<ClientSearchPageFlyoutMenuItem> MenuItems { get; set; }
            
            public ClientSearchPageFlyoutViewModel()
            {
                MenuItems = new ObservableCollection<ClientSearchPageFlyoutMenuItem>(new[]
                {
                    new ClientSearchPageFlyoutMenuItem { Id = 0, Title = "Page 1" },
                    new ClientSearchPageFlyoutMenuItem { Id = 1, Title = "Page 2" },
                    new ClientSearchPageFlyoutMenuItem { Id = 2, Title = "Page 3" },
                    new ClientSearchPageFlyoutMenuItem { Id = 3, Title = "Page 4" },
                    new ClientSearchPageFlyoutMenuItem { Id = 4, Title = "Page 5" },
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