// Club.cs
using Newtonsoft.Json;

namespace ConsoleBookingApp.Models
{
    public class Club
    {
        [JsonProperty("club_id")]
        public int ClubId { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("address")]
        public string Address { get; set; }

        [JsonProperty("phone_number")]
        public string PhoneNumber { get; set; }

        [JsonProperty("email")]
        public string Email { get; set; }

        [JsonProperty("max_ps_zones")]
        public int MaxPsZones { get; set; }

        [JsonProperty("max_vr_quest_zones")]
        public int MaxVrQuestZones { get; set; }

        [JsonProperty("status")] // <-- ДОДАНО
        public string Status { get; set; }

        // created_at зазвичай не потрібен у моделі для передачі клієнту

        // Конструктор (опціонально)
        public Club() { }
    }
}