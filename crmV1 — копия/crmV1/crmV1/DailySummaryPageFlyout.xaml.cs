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
    public partial class DailySummaryPageFlyout : ContentPage
    {
        public ListView ListView;

        public DailySummaryPageFlyout()
        {
            InitializeComponent();

            BindingContext = new DailySummaryPageFlyoutViewModel();
            ListView = MenuItemsListView;
        }

        private class DailySummaryPageFlyoutViewModel : INotifyPropertyChanged
        {
            public ObservableCollection<DailySummaryPageFlyoutMenuItem> MenuItems { get; set; }

            public DailySummaryPageFlyoutViewModel()
            {
                MenuItems = new ObservableCollection<DailySummaryPageFlyoutMenuItem>(new[]
                {
                    new DailySummaryPageFlyoutMenuItem { Id = 0, Title = "Page 1" },
                    new DailySummaryPageFlyoutMenuItem { Id = 1, Title = "Page 2" },
                    new DailySummaryPageFlyoutMenuItem { Id = 2, Title = "Page 3" },
                    new DailySummaryPageFlyoutMenuItem { Id = 3, Title = "Page 4" },
                    new DailySummaryPageFlyoutMenuItem { Id = 4, Title = "Page 5" },
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