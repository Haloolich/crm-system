// ConsoleBookingApp.Models/Manager.cs
// Це модель для використання НА СЕРВЕРІ. Вона має містити всі поля з БД.
// Клієнтська модель (у Xamarin.Forms проекті) не повинна містити login та password_hash.

using Newtonsoft.Json; // Можливо, потрібен, якщо серіалізуєте цю модель для внутрішнього використання

namespace ConsoleBookingApp.Models // Переконайтесь, що namespace правильний
{
    public class Manager
    {
        public int ManagerId { get; set; }
        public string Name { get; set; }
        public string PhoneNumber { get; set; }
        public string Login { get; set; } // Це поле є у БД, але НЕ відправляється клієнту
        public string PasswordHash { get; set; } // Це поле є у БД, але НЕ відправляється клієнту
        public int ClubId { get; set; } // ID клубу
        public string Role { get; set; } // Роль менеджера (напр., "admin", "manager")
        public string Status { get; set; } // Статус менеджера (напр., "Hired", "Dismissed")

        // Додамо властивість для назви клубу, яка буде заповнюватися при отриманні даних з JOIN в репозиторії
        // Ця властивість НЕ ОБОВ'ЯЗКОВО відповідає полю в БД, але зручна для передачі даних далі
        public string ClubName { get; set; }

        // Конструктор за замовчуванням (може бути потрібен для деяких ORM або десеріалізації)
        public Manager() { }

        // TODO: Якщо ви використовуєте цей клас як ДТО для клієнта,
        // то ПОТРІБНО створити ОКРЕМИЙ клас для клієнта БЕЗ Login та PasswordHash.
        // Або вручну створювати об'єкти/словники для відправки клієнту, виключаючи ці поля.
        // У HandleGetManagersAsync ми будемо вручну формувати дані без цих полів.
    }

    // Клас для історії змін менеджера (вже був у вашому коді)
    public class ShiftHistoryItem
    {
        [JsonProperty("shift_id")]
        public int ShiftId { get; set; }

        [JsonProperty("shift_date")]
        public string ShiftDate { get; set; } // Можна використовувати DateTime, але string простіше для серіалізації/десеріалізації

        [JsonProperty("start_time")]
        public string StartTime { get; set; }

        [JsonProperty("end_time")]
        public string EndTime { get; set; } // Nullable, якщо зміна ще відкрита

        [JsonProperty("worked_hours")]
        public decimal? WorkedHours { get; set; } // Nullable, якщо зміна ще відкрита
    }
    public class Role
    {
        public int Id { get; set; }
        public string Name { get; set; }
        // Забезпечуємо коректну серіалізацію/десеріалізацію,
        // хоча Newtonsoft.Json зазвичай справляється з публічними властивостями
        // Якщо використовується .NET Core 3.1+ з System.Text.Json, можливо, знадобиться [JsonPropertyName].
    }
}