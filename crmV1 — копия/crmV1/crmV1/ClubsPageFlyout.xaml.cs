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
    public partial class ClubsPageFlyout : ContentPage
    {
        public ListView ListView;

        public ClubsPageFlyout()
        {
            InitializeComponent();

            BindingContext = new ClubsPageFlyoutViewModel();
            ListView = MenuItemsListView;
        }

        class ClubsPageFlyoutViewModel : INotifyPropertyChanged
        {
            public ObservableCollection<ClubsPageFlyoutMenuItem> MenuItems { get; set; }
            
            public ClubsPageFlyoutViewModel()
            {
                MenuItems = new ObservableCollection<ClubsPageFlyoutMenuItem>(new[]
                {
                    new ClubsPageFlyoutMenuItem { Id = 0, Title = "Page 1" },
                    new ClubsPageFlyoutMenuItem { Id = 1, Title = "Page 2" },
                    new ClubsPageFlyoutMenuItem { Id = 2, Title = "Page 3" },
                    new ClubsPageFlyoutMenuItem { Id = 3, Title = "Page 4" },
                    new ClubsPageFlyoutMenuItem { Id = 4, Title = "Page 5" },
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