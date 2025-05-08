// Ваш SessionDetails.cs з попередньої відповіді
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleBookingApp
{
    public class SessionDetails // Цей клас використовуємо як DTO
    {
        public int SessionId { get; set; }
        public int ClientId { get; set; } // Можливо, не потрібен клієнту, але зручно на сервері
        public DateTime SessionDate { get; set; } // Можливо, не потрібен клієнту, але зручно на сервері
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; } // Можливо, не потрібен клієнту для підсумку, але є в БД
        public int NumPeople { get; set; } // Можливо, не потрібен клієнту для підсумку
        public string Notes { get; set; } // Можливо, не потрібен клієнту для підсумку
        public string SessionType { get; set; } // Можливо, не потрібен клієнту для підсумку
        public string PaymentStatus { get; set; }
        public decimal CalculatePrice { get; set; } // Розрахована ціна (можливо, не потрібна клієнту для підсумку)
        public decimal FinalPrice { get; set; }     // Фактично оплачена ціна (ПОТРІБНА ДЛЯ ПІДСУМКІВ!)
        public string ClientName { get; set; } // ПОТРІБНА КЛІЄНТУ
        public string ClientPhone { get; set; } // Можливо, не потрібен
        public string ManagerName { get; set; } // Можливо, не потрібен клієнту для підсумку
        public int ClubId { get; set; } // Можливо, не потрібен

        // --- ПОТРІБНЕ ПОЛЕ ---
        public string PaymentMethod { get; set; } // Метод оплати
        // ---------------------

        // Можливо, також знадобиться час оплати, якщо він є в БД і потрібен
        // public DateTime? PaymentTime { get; set; }
    }
}