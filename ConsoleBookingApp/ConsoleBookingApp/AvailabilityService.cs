using ConsoleBookingApp.Data;
using MySqlConnector;
using System;
using System.Data;
using System.Threading.Tasks;

namespace ConsoleBookingApp.Services
{
    public static class AvailabilityService
    {
        public static async Task<bool> CheckAvailabilityAsync(string connectionString, DateTime sessionDate, TimeSpan startTime, TimeSpan endTime, string sessionType, int requestedZones, int clubId, int? editingSessionId, MySqlConnection connection = null)
        {
            bool ownConnection = connection == null;
            MySqlConnection conn = connection ?? new MySqlConnection(connectionString);
            try
            {
                if (ownConnection) await conn.OpenAsync();

                string sessionTypeGroup = GetSessionTypeGroup(sessionType);
                int bookedZones = await BookingRepository.GetBookedZonesAsync(connectionString, sessionDate, startTime, endTime, clubId, sessionTypeGroup, editingSessionId, conn);
                int maxZones = await GetMaxZonesForClubAsync(connectionString, clubId, sessionTypeGroup, conn); // Потрібно реалізувати

                Console.WriteLine($"[AvailabilityCheck] Club: {clubId}, Date: {sessionDate:yyyy-MM-dd}, Time: {startTime:hh\\:mm}-{endTime:hh\\:mm}, TypeGroup: {sessionTypeGroup}");
                Console.WriteLine($"[AvailabilityCheck] Max Zones: {maxZones}, Booked Zones (excluding {editingSessionId?.ToString() ?? "none"}): {bookedZones}, Requested Zones: {requestedZones}");

                if (maxZones < 0) // Помилка отримання ліміту
                {
                    Console.WriteLine($"[AvailabilityCheck] Failed to get max zones limit.");
                    return false; // Недоступно, якщо ліміт невідомий
                }

                bool available = (bookedZones + requestedZones) <= maxZones;
                Console.WriteLine($"[AvailabilityCheck] Result: {(available ? "Available" : "Not Available")}");
                return available;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AvailabilityCheck] Error: {ex.Message}");
                return false; // Вважаємо недоступним при помилці
            }
            finally
            {
                if (ownConnection && conn?.State == ConnectionState.Open) await conn.CloseAsync();
            }
        }
        // Ці методи мають бути реалізовані або переміщені, якщо вже є
        private static string GetSessionTypeGroup(string sessionType)
        {
            if (sessionType.Equals("PS", StringComparison.OrdinalIgnoreCase)) return "PS";
            if (sessionType.Equals("VR", StringComparison.OrdinalIgnoreCase) || sessionType.Equals("Quest", StringComparison.OrdinalIgnoreCase)) return "VR_QUEST";
            return "UNKNOWN";
        }
        private static async Task<int> GetMaxZonesForClubAsync(string connectionString, int clubId, string sessionTypeGroup, MySqlConnection connection, MySqlTransaction transaction = null)
        {
            // ВАЖЛИВО: Реалізуйте цю логіку
            string query = "";
            if (sessionTypeGroup == "PS") query = "SELECT max_ps_zones FROM clubs WHERE club_id = @clubId";
            else if (sessionTypeGroup == "VR_QUEST") query = "SELECT max_vr_quest_zones FROM clubs WHERE club_id = @clubId";
            else return -1;

            try
            {
                using (var cmd = new MySqlCommand(query, connection, transaction))
                {
                    cmd.Parameters.AddWithValue("@clubId", clubId);
                    object result = await cmd.ExecuteScalarAsync();
                    if (result != null && result != DBNull.Value && int.TryParse(result.ToString(), out int maxZones)) return maxZones;
                    else return -1; // Помилка отримання/парсингу
                }
            }
            catch { return -1; } // Помилка запиту
        }
    }
}