// crmV1/Models/Manager.cs
using crmV1.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel; // Для INotifyPropertyChanged якщо потрібне динамічне оновлення
using System.Windows.Input; // Для ICommand
using Xamarin.Forms; // Для Command, Page, DisplayAlert
using Xamarin.CommunityToolkit.ObjectModel; // Для AsyncCommand (потрібно встановити NuGet пакет Xamarin.CommunityToolkit)
using crmV1.Services;
using crmV1;

namespace crmV1.Models
{
    // Клас для представлення менеджера, отриманого з сервера
    public class Manager // : INotifyPropertyChanged // Додати, якщо потрібно оновлення в UI без перезавантаження списку
    {
        // public event PropertyChangedEventHandler PropertyChanged;
        // protected virtual void OnPropertyChanged(string propertyName)
        // {
        //     PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        // }

        [JsonProperty("manager_id")]
        public int ManagerId { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("phone_number")]
        public string PhoneNumber { get; set; }

        // Припускаємо, що сервер може повернути назву клубу або ми її отримаємо окремо
        [JsonProperty("club_id")]
        public int ClubId { get; set; }

        // Можна додати ClubName, якщо сервер його повертає, або отримувати його на клієнті
        // [JsonProperty("club_name")]

        [JsonProperty("club_name")] // <-- ДОДАНО
        public string ClubName { get; set; }

        [JsonProperty("role")]
        public string Role { get; set; } // Наприклад, "admin", "manager", "trainee"

        // Додамо поле для статусу
        [JsonProperty("status")]
        public string Status { get; set; } // Наприклад, "Hired", "Dismissed"

        // Приховані поля (не додаємо сюди login та password_hash з БД)

        // Допоміжні властивості для відображення в UI
        public string DisplayDetailsSummary => $"Клуб: {ClubName ?? ClubId.ToString()}, Роль: {Role}, Статус: {Status}";

        // Допоміжна властивість для визначення, чи менеджер звільнений
        public bool IsDismissed => Status?.Equals("Dismissed", StringComparison.OrdinalIgnoreCase) ?? false;
    }

    // Клас для групування менеджерів по клубах
    // Використовується для ListView.IsGroupingEnabled
}
// crmV1/Services/ManagerService.cs
