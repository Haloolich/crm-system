﻿using System;
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
    public partial class ClubDetailsPageFlyout : ContentPage
    {
        public ListView ListView;

        public ClubDetailsPageFlyout()
        {
            InitializeComponent();

            BindingContext = new ClubDetailsPageFlyoutViewModel();
            ListView = MenuItemsListView;
        }

        class ClubDetailsPageFlyoutViewModel : INotifyPropertyChanged
        {
            public ObservableCollection<ClubDetailsPageFlyoutMenuItem> MenuItems { get; set; }
            
            public ClubDetailsPageFlyoutViewModel()
            {
                MenuItems = new ObservableCollection<ClubDetailsPageFlyoutMenuItem>(new[]
                {
                    new ClubDetailsPageFlyoutMenuItem { Id = 0, Title = "Page 1" },
                    new ClubDetailsPageFlyoutMenuItem { Id = 1, Title = "Page 2" },
                    new ClubDetailsPageFlyoutMenuItem { Id = 2, Title = "Page 3" },
                    new ClubDetailsPageFlyoutMenuItem { Id = 3, Title = "Page 4" },
                    new ClubDetailsPageFlyoutMenuItem { Id = 4, Title = "Page 5" },
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