// ConsoleBookingApp.Models/Client.cs (Серверна модель)
using System;
using System.Collections.Generic;
using Newtonsoft.Json; // Важливо використовувати Newtonsoft.Json і на сервері

namespace ConsoleBookingApp.Models // Простір імен на сервері
{
    public class Client
    {
        [JsonProperty("client_id")]
        public int ClientId { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("phone_number")]
        public string PhoneNumber { get; set; }

        [JsonProperty("email")]
        public string Email { get; set; }

        [JsonProperty("birthday")]
        public DateTime DateOfBirth { get; set; }

        [JsonProperty("created_at")]
        public DateTime RegistrationDate { get; set; }

        // ВИПРАВЛЕННЯ CS1061: Додаємо цю властивість!
        // Вона потрібна, щоб Newtonsoft.Json міг включити список сесій
        // при серіалізації об'єкта Client на сервері.
        [JsonProperty("sessions")] // Атрибут для відповідності клієнтській моделі
        public List<ClientSession> Sessions { get; set; } // Тип має відповідати серверній моделі ClientSession

        // Не додаємо розрахункові властивості Age та YearsInClub сюди.
    }
    public class ClientSession
    {
        [JsonProperty("session_date")]
        public DateTime SessionDate { get; set; }

        [JsonProperty("session_type")]
        public string SessionType { get; set; }

        // Не додаємо FormattedDate сюди.
    }
}