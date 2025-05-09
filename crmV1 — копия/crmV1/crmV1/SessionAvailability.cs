using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Xamarin.Forms;
using System.Globalization;
using System.Diagnostics;


namespace crmV1.Models
{
    public class SessionAvailability : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public string Time { get; }
        public TimeSpan StartTime { get; }
        public TimeSpan EndTime { get; }
        public bool IsTimeParsedSuccessfully { get; }


        private const int MaxVrQuestZones = 3;
        private const int MaxPsZones = 1;

        // !!! НОВІ ПРИВАТНІ ПОЛЯ для Paid та Pending !!!
        private int _paidVrQuest;
        private int _pendingVrQuest;
        private int _paidPs;
        private int _pendingPs;

        // Властивості, що оновлюються з сервера
        // !!! НОВІ ВЛАСТИВОСТІ для Paid та Pending !!!
        public int PaidVrQuest
        {
            get => _paidVrQuest;
            set
            {
                if (_paidVrQuest != value)
                {
                    Debug.WriteLine($"[SessionAvailability:{Time}] PaidVrQuest changing from {_paidVrQuest} to {value}");
                    _paidVrQuest = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(TotalOccupiedVrQuest)); // Сповіщаємо про зміну загальної кількості
                    OnPropertyChanged(nameof(AvailableVrQuest));     // Сповіщаємо про зміну вільної кількості
                    UpdateVisualState(); // Оновлюємо візуальне представлення
                }
                else { Debug.WriteLine($"[SessionAvailability:{Time}] PaidVrQuest set to same value: {value}"); }
            }
        }

        public int PendingVrQuest
        {
            get => _pendingVrQuest;
            set
            {
                if (_pendingVrQuest != value)
                {
                    Debug.WriteLine($"[SessionAvailability:{Time}] PendingVrQuest changing from {_pendingVrQuest} to {value}");
                    _pendingVrQuest = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(TotalOccupiedVrQuest)); // Сповіщаємо про зміну загальної кількості
                    OnPropertyChanged(nameof(AvailableVrQuest));     // Сповіщаємо про зміну вільної кількості
                    UpdateVisualState(); // Оновлюємо візуальне представлення
                }
                else { Debug.WriteLine($"[SessionAvailability:{Time}] PendingVrQuest set to same value: {value}"); }
            }
        }

        public int PaidPs
        {
            get => _paidPs;
            set
            {
                if (_paidPs != value)
                {
                    Debug.WriteLine($"[SessionAvailability:{Time}] PaidPs changing from {_paidPs} to {value}");
                    _paidPs = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(TotalOccupiedPs)); // Сповіщаємо про зміну загальної кількості
                    OnPropertyChanged(nameof(AvailablePs));     // Сповіщаємо про зміну вільної кількості
                    UpdateVisualState(); // Оновлюємо візуальне представлення
                }
                else { Debug.WriteLine($"[SessionAvailability:{Time}] PaidPs set to same value: {value}"); }
            }
        }

        public int PendingPs
        {
            get => _pendingPs;
            set
            {
                if (_pendingPs != value)
                {
                    Debug.WriteLine($"[SessionAvailability:{Time}] PendingPs changing from {_pendingPs} to {value}");
                    _pendingPs = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(TotalOccupiedPs)); // Сповіщаємо про зміну загальної кількості
                    OnPropertyChanged(nameof(AvailablePs));     // Сповіщаємо про зміну вільної кількості
                    UpdateVisualState(); // Оновлюємо візуальне представлення
                }
                else { Debug.WriteLine($"[SessionAvailability:{Time}] PendingPs set to same value: {value}"); }
            }
        }

        // Властивості для загальної кількості зайнятих (сума Paid + Pending)
        public int TotalOccupiedVrQuest => PaidVrQuest + PendingVrQuest;
        public int TotalOccupiedPs => PaidPs + PendingPs;


        // Властивості для вільних зон (розраховуються на основі TotalOccupied)
        public int AvailableVrQuest => Math.Max(0, MaxVrQuestZones - TotalOccupiedVrQuest);
        public int AvailablePs => Math.Max(0, MaxPsZones - TotalOccupiedPs);


        // Властивості для візуального представлення (списки джерел зображень)
        private List<ImageSource> _vrZoneImages;
        public List<ImageSource> VrZoneImages
        {
            get => _vrZoneImages;
            private set
            {
                if (_vrZoneImages != value)
                {
                    _vrZoneImages = value;
                    OnPropertyChanged();
                }
            }
        }

        private ImageSource _psZoneImage;
        public ImageSource PsZoneImage
        {
            get => _psZoneImage;
            private set
            {
                if (_psZoneImage != value)
                {
                    _psZoneImage = value;
                    OnPropertyChanged();
                }
            }
        }


        public SessionAvailability(string time)
        {
            Time = time;
            TimeSpan parsedTime;
            if (TimeSpan.TryParseExact(time, @"hh\:mm", CultureInfo.InvariantCulture, out parsedTime))
            {
                StartTime = parsedTime;
                EndTime = parsedTime.Add(TimeSpan.FromHours(1));
                IsTimeParsedSuccessfully = true;
                Debug.WriteLine($"[SessionAvailability:{Time}] Successfully parsed time '{time}'. Start: {StartTime}, End: {EndTime}");
            }
            else
            {
                StartTime = TimeSpan.Zero;
                EndTime = TimeSpan.Zero;
                IsTimeParsedSuccessfully = false;
                System.Diagnostics.Debug.WriteLine($"[ERROR SessionAvailability:{Time}] Failed to parse time string '{time}'.");
            }

            // Ініціалізуємо стани 0
            _paidVrQuest = 0;
            _pendingVrQuest = 0;
            _paidPs = 0;
            _pendingPs = 0;

            UpdateVisualState(initial: true); // Ініціалізуємо візуальний стан
        }

        /// <summary>
        /// Оновлює візуальні властивості (списки зображень) на основі кількості зайнятих зон.
        /// </summary>
        private void UpdateVisualState(bool initial = false)
        {
            Debug.WriteLine($"[SessionAvailability:{Time}] UpdateVisualState called. Initial: {initial}, PaidVR: {PaidVrQuest}, PendingVR: {PendingVrQuest}, AvailableVR: {AvailableVrQuest}, PaidPS: {PaidPs}, PendingPS: {PendingPs}, AvailablePS: {AvailablePs}");

            var vrImages = new List<ImageSource>();

            // Якщо час не розпарсився, показуємо стан помилки
            if (!IsTimeParsedSuccessfully)
            {
                VrZoneImages = new List<ImageSource>() { ImageSource.FromFile("add.png") }; // Наприклад, один значок помилки
                PsZoneImage = ImageSource.FromFile("add.png");
                Debug.WriteLine($"[SessionAvailability:{Time}] UpdateVisualState: Time not parsed successfully. Showing error/empty state.");
                return;
            }

            // --- Логіка додавання зображень VR/Quest: Оплачені, Очікують, Вільні ---

            // Додаємо зображення для ОПЛАЧЕНИХ VR/Quest зон
            Debug.WriteLine($"[SessionAvailability:{Time}] Adding {PaidVrQuest} paid VR icons.");
            for (int i = 0; i < PaidVrQuest; i++)
            {
                vrImages.Add(ImageSource.FromFile("paid_vr.png")); // !!! Використовуйте ім'я вашого значка ОПЛАЧЕНОЇ VR зони !!!
            }

            // Додаємо зображення для VR/Quest зон, що ОЧІКУЮТЬ оплати
            Debug.WriteLine($"[SessionAvailability:{Time}] Adding {PendingVrQuest} pending VR icons.");
            for (int i = 0; i < PendingVrQuest; i++)
            {
                vrImages.Add(ImageSource.FromFile("vr.png")); // !!! Використовуйте ім'я вашого значка VR зони В ОЧІКУВАННІ !!!
            }

            // Додаємо зображення для ВІЛЬНИХ VR/Quest зон
            Debug.WriteLine($"[SessionAvailability:{Time}] Adding {AvailableVrQuest} free VR icons. Total VR slots: {MaxVrQuestZones}.");
            for (int i = 0; i < AvailableVrQuest; i++)
            {
                vrImages.Add(ImageSource.FromFile("booking_vr.png")); // !!! Використовуйте ім'я вашого значка ВІЛЬНОЇ VR зони !!!
            }

            // Перевірка: загальна кількість значків VR має бути MaxVrQuestZones
            if (vrImages.Count != MaxVrQuestZones)
            {
                Debug.WriteLine($"[ПОПЕРЕДЖЕННЯ SessionAvailability:{Time}] VR icons count ({vrImages.Count}) does not match max zones ({MaxVrQuestZones})!");
                // Це може статися, якщо логіка розрахунку AvailableVrQuest або TotalOccupied неправильна,
                // або якщо дані з сервера дивні (хоча серверний запит має запобігти > Max).
                // Обрізаємо або доповнюємо, щоб завжди було MaxVrQuestZones значків
                while (vrImages.Count > MaxVrQuestZones) vrImages.RemoveAt(vrImages.Count - 1);
                while (vrImages.Count < MaxVrQuestZones) vrImages.Add(ImageSource.FromFile("add.png")); // Заповнюємо значками помилки, якщо менше ніж Max
            }


            // Оновлюємо список зображень VR.
            VrZoneImages = vrImages.ToList(); // Створюємо новий список для оновлення UI


            // --- Визначаємо зображення для PS зони: Оплачено, Очікує, Вільно ---

            // Перевірка: загальна кількість PS зон
            if (TotalOccupiedPs > MaxPsZones)
            {
                Debug.WriteLine($"[ПОПЕРЕДЖЕННЯ SessionAvailability:{Time}] PS occupied zones ({TotalOccupiedPs}) exceeds max ({MaxPsZones})!");
                PsZoneImage = ImageSource.FromFile("add.png"); // Значок помилки для PS зони
                Debug.WriteLine($"[SessionAvailability:{Time}] PS state: ERROR (overbooked)");
            }
            else if (PaidPs > 0) // Спочатку перевіряємо оплачені
            {
                PsZoneImage = ImageSource.FromFile("paid_ps.png"); // !!! Використовуйте ім'я вашого значка ОПЛАЧЕНОЇ PS зони !!!
                Debug.WriteLine($"[SessionAvailability:{Time}] PS state: PAID -> ps_paid.png");
            }
            else if (PendingPs > 0) // Якщо не оплачено, перевіряємо, чи є в очікуванні
            {
                PsZoneImage = ImageSource.FromFile("ps.png"); // !!! Використовуйте ім'я вашого значка PS зони В ОЧІКУВАННІ !!!
                Debug.WriteLine($"[SessionAvailability:{Time}] PS state: PENDING -> ps_pending.png");
            }
            else // Якщо ні оплачено, ні в очікуванні
            {
                PsZoneImage = ImageSource.FromFile("booking_ps.png"); // !!! Використовуйте ім'я вашого значка ВІЛЬНОЇ PS зони !!!
                Debug.WriteLine($"[SessionAvailability:{Time}] PS state: FREE -> ps_free.png");
            }

            Debug.WriteLine($"[SessionAvailability:{Time}] Visual State Updated. Final VR Images Count: {VrZoneImages?.Count ?? 0}, PS Image Source: {PsZoneImage?.ClassId ?? PsZoneImage?.ToString() ?? "null"}");
        }

        // Допоміжний метод для INotifyPropertyChanged
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            Debug.WriteLine($"[SessionAvailability:{Time}] PropertyChanged fired for: {propertyName}");
        }
    }
}