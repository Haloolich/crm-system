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
    public partial class AddClubPageFlyout : ContentPage
    {
        public ListView ListView;

        public AddClubPageFlyout()
        {
            InitializeComponent();

            BindingContext = new AddClubPageFlyoutViewModel();
            ListView = MenuItemsListView;
        }

        class AddClubPageFlyoutViewModel : INotifyPropertyChanged
        {
            public ObservableCollection<AddClubPageFlyoutMenuItem> MenuItems { get; set; }
            
            public AddClubPageFlyoutViewModel()
            {
                MenuItems = new ObservableCollection<AddClubPageFlyoutMenuItem>(new[]
                {
                    new AddClubPageFlyoutMenuItem { Id = 0, Title = "Page 1" },
                    new AddClubPageFlyoutMenuItem { Id = 1, Title = "Page 2" },
                    new AddClubPageFlyoutMenuItem { Id = 2, Title = "Page 3" },
                    new AddClubPageFlyoutMenuItem { Id = 3, Title = "Page 4" },
                    new AddClubPageFlyoutMenuItem { Id = 4, Title = "Page 5" },
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