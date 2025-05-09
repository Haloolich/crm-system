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
    public partial class AnalyticsPageFlyout : ContentPage
    {
        public ListView ListView;

        public AnalyticsPageFlyout()
        {
            InitializeComponent();

            BindingContext = new AnalyticsPageFlyoutViewModel();
            ListView = MenuItemsListView;
        }

        class AnalyticsPageFlyoutViewModel : INotifyPropertyChanged
        {
            public ObservableCollection<AnalyticsPageFlyoutMenuItem> MenuItems { get; set; }
            
            public AnalyticsPageFlyoutViewModel()
            {
                MenuItems = new ObservableCollection<AnalyticsPageFlyoutMenuItem>(new[]
                {
                    new AnalyticsPageFlyoutMenuItem { Id = 0, Title = "Page 1" },
                    new AnalyticsPageFlyoutMenuItem { Id = 1, Title = "Page 2" },
                    new AnalyticsPageFlyoutMenuItem { Id = 2, Title = "Page 3" },
                    new AnalyticsPageFlyoutMenuItem { Id = 3, Title = "Page 4" },
                    new AnalyticsPageFlyoutMenuItem { Id = 4, Title = "Page 5" },
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