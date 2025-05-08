using ConsoleBookingApp.Data;
using MySqlConnector;
using System;
using System.Collections.Generic;
using System.Data;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace ConsoleBookingApp.Services
{
    public class ClientOperationResult
    {
        public bool Success { get; set; }
        public int ClientId { get; set; } = -1;
        public bool WasCreated { get; set; } = false;
        public string ErrorMessage { get; set; }
    }

    public static class ClientService
    {
        public class ClientResult
        {
            public bool Success { get; set; }
            public int ClientId { get; set; }
            public string ErrorMessage { get; set; }
        }
        public static async Task HandleSearchClientByPhoneAsync(NetworkStream stream, string connectionString, Dictionary<string, object> requestData)
        {
            string phoneNumber = null;
            if (requestData != null && requestData.TryGetValue("phone_number", out object phoneObj) && phoneObj is string phoneStr)
            {
                phoneNumber = phoneStr;
            }

            if (string.IsNullOrWhiteSpace(phoneNumber))
            {
                Console.WriteLine("Received search_client_by_phone request with missing or empty phone_number.");
                await ResponseHelper.SendErrorResponse(stream, "Необхідно вказати номер телефону для пошуку.");
                return;
            }

            Console.WriteLine($"Searching for client with phone number: {phoneNumber}");

            try
            {
                // ВИПРАВЛЕННЯ: Створюємо ЕКЗЕМПЛЯР BookingRepository
                var clientRepository = new BookingRepository(connectionString); // Рядок 49 з вашої помилки CS0723/CS0712

                // Викликаємо НЕ СТАТИЧНІ методи на створеному екземплярі
                var client = await clientRepository.GetClientByPhoneAsync(phoneNumber);

                if (client != null)
                {
                    Console.WriteLine($"Client found: {client.Name} (ID: {client.ClientId}). Getting sessions...");
                    // Викликаємо НЕ СТАТИЧНИЙ метод на створеному екземплярі
                    var sessions = await clientRepository.GetSessionsByClientIdAsync(client.ClientId);
                    Console.WriteLine($"Found {sessions.Count} sessions for client {client.ClientId}.");

                    // ПЕРЕВІРТЕ: Чи має ваша СЕРВЕРНА модель Client (ConsoleBookingApp.Models.Client)
                    // властивість: public List<ClientSession> Sessions { get; set; }
                    // Якщо так, присвойте отриманий список сесій:
                    client.Sessions = sessions; // Це дозволить Newtonsoft.Json серіалізувати Sessions автоматично

                    // Якщо серверна модель Client НЕ має властивості Sessions,
                    // використовуйте підхід зі збиранням Dictionary вручну, як було показано раніше:
                    // var responseData = new Dictionary<string, object> { ... { "client", new Dictionary<string, object> { ..., {"sessions", sessions} } } };

                    // Формуємо відповідь, серіалізуючи об'єкт client (припускаючи, що він має Sessions)
                    var responseData = new Dictionary<string, object>
                    {
                        { "success", true },
                        { "message", "Клієнта знайдено" },
                        { "client", client } // Серіалізуємо об'єкт Client разом з його Sessions
                    };

                    await ResponseHelper.SendJsonResponse(stream, responseData);
                    Console.WriteLine($"[{phoneNumber}] Client data and sessions sent successfully.");
                }
                else
                {
                    Console.WriteLine($"Client with phone number {phoneNumber} not found.");
                    await ResponseHelper.SendJsonResponse(stream, new Dictionary<string, object>
                    {
                        { "success", true },
                        { "message", "Клієнта з таким номером не знайдено." },
                        { "client", null }
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing search_client_by_phone for {phoneNumber}: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                await ResponseHelper.SendErrorResponse(stream, $"Помилка сервера під час пошуку клієнта: {ex.Message}");
            }
        }
        public static async Task<ClientResult> FindOrCreateClientAsync(string connectionString, string clientName, string phone, MySqlConnection connection, MySqlTransaction transaction = null)
        {
            if (connection == null || connection.State != ConnectionState.Open) return new ClientResult { Success = false, ErrorMessage = "Database connection is required." };

            try
            {
                int clientId = await BookingRepository.FindClientByPhoneAsync(connectionString, phone, connection, transaction);
                if (clientId != -1)
                {
                    // Опціонально: Оновити ім'я існуючого клієнта
                    // await BookingRepository.UpdateClientAsync(connectionString, clientId, clientName, phone, connection, transaction);
                    return new ClientResult { Success = true, ClientId = clientId };
                }
                else
                {
                    clientId = await BookingRepository.CreateClientAsync(connectionString, clientName, phone, connection, transaction);
                    if (clientId > 0) return new ClientResult { Success = true, ClientId = clientId };
                    else return new ClientResult { Success = false, ErrorMessage = "Failed to create new client." };
                }
            }
            catch (Exception ex)
            {
                return new ClientResult { Success = false, ErrorMessage = $"Error during client lookup/creation: {ex.Message}" };
            }
        }
    }
}