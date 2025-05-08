using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleBookingApp // Переконайтесь, що namespace правильний
{
    public static class ResponseHelper
    {
        private const int LogDataLengthLimit = 200; // Обмежимо довжину логу даних

        // Надсилає Dictionary<string, object>
        public static async Task SendJsonResponse(NetworkStream stream, Dictionary<string, object> data)
        {
            string json = "{}";
            byte[] responseBytes = Array.Empty<byte>();
            int responseLength = 0;
            string logPrefix = "[ResponseHelper SendJsonResponse]";

            try
            {
                json = JsonConvert.SerializeObject(data, Formatting.None,
                           new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
                responseBytes = Encoding.UTF8.GetBytes(json);
                responseLength = responseBytes.Length;

                // --- ДОДАНО ДЕТАЛЬНЕ ЛОГУВАННЯ ПЕРЕД ВІДПРАВКОЮ ---
                Console.WriteLine($"{logPrefix} Готується до відправки:");
                Console.WriteLine($"{logPrefix}   JSON ({responseLength} bytes): {TruncateForLog(json)}"); // Логуємо частину JSON
                // --- КІНЕЦЬ ЛОГУВАННЯ ---

                byte[] responseLengthBytes = BitConverter.GetBytes(responseLength);
                Console.WriteLine($"{logPrefix}   Довжина для відправки: {responseLength} (Bytes: {BitConverter.ToString(responseLengthBytes)})");

                if (stream == null || !stream.CanWrite)
                {
                    Console.WriteLine($"{logPrefix} ПОМИЛКА: Потік null або не для запису ПЕРЕД відправкою.");
                    return;
                }

                Console.WriteLine($"{logPrefix} Відправка довжини...");
                await stream.WriteAsync(responseLengthBytes, 0, responseLengthBytes.Length);
                Console.WriteLine($"{logPrefix} Довжину надіслано.");

                if (responseLength > 0)
                {
                    Console.WriteLine($"{logPrefix} Відправка даних JSON...");
                    await stream.WriteAsync(responseBytes, 0, responseBytes.Length);
                    Console.WriteLine($"{logPrefix} Дані JSON надіслано.");
                }
                await stream.FlushAsync();
                Console.WriteLine($"{logPrefix} Потік очищено (Flush). Відправка завершена.");
            }
            // ... (решта коду SendJsonResponse з catch блоками без змін) ...
            catch (JsonException jsonEx) { Console.WriteLine($"{logPrefix} ПОМИЛКА серіалізації JSON: {jsonEx.Message}. Дані: {string.Join(", ", data.Select(kv => kv.Key + "=" + (kv.Value?.ToString() ?? "null")))}"); /* ... спроба відправити помилку ... */ }
            catch (ObjectDisposedException ode) { Console.WriteLine($"{logPrefix} ПОМИЛКА відправки: Потік закрито. {ode.Message}"); }
            catch (IOException ioEx) { Console.WriteLine($"{logPrefix} ПОМИЛКА IO відправки: {ioEx.Message}"); }
            catch (Exception ex) { Console.WriteLine($"{logPrefix} ЗАГАЛЬНА ПОМИЛКА відправки: {ex.GetType().Name}: {ex.Message}"); }
        }

        // Надсилає готовий JSON-рядок
        public static async Task SendRawJsonResponse(NetworkStream stream, string jsonResponse)
        {
            byte[] responseBytes = Array.Empty<byte>();
            int responseLength = 0;
            string logPrefix = "[ResponseHelper SendRawJsonResponse]";

            try
            {
                if (string.IsNullOrEmpty(jsonResponse))
                {
                    jsonResponse = "[]";
                    Console.WriteLine($"{logPrefix} Увага: Вхідний рядок JSON був null/порожній, надсилається '[]'.");
                }

                responseBytes = Encoding.UTF8.GetBytes(jsonResponse);
                responseLength = responseBytes.Length;

                // --- ДОДАНО ДЕТАЛЬНЕ ЛОГУВАННЯ ПЕРЕД ВІДПРАВКОЮ ---
                Console.WriteLine($"{logPrefix} Готується до відправки:");
                Console.WriteLine($"{logPrefix}   RAW JSON ({responseLength} bytes): {TruncateForLog(jsonResponse)}"); // Логуємо частину JSON
                // --- КІНЕЦЬ ЛОГУВАННЯ ---

                byte[] responseLengthBytes = BitConverter.GetBytes(responseLength);
                Console.WriteLine($"{logPrefix}   Довжина для відправки: {responseLength} (Bytes: {BitConverter.ToString(responseLengthBytes)})");

                if (stream == null || !stream.CanWrite)
                {
                    Console.WriteLine($"{logPrefix} ПОМИЛКА: Потік null або не для запису ПЕРЕД відправкою.");
                    return;
                }

                Console.WriteLine($"{logPrefix} Відправка довжини...");
                await stream.WriteAsync(responseLengthBytes, 0, responseLengthBytes.Length);
                Console.WriteLine($"{logPrefix} Довжину надіслано.");

                if (responseLength > 0)
                {
                    Console.WriteLine($"{logPrefix} Відправка даних RAW JSON...");
                    await stream.WriteAsync(responseBytes, 0, responseBytes.Length);
                    Console.WriteLine($"{logPrefix} Дані RAW JSON надіслано.");
                }
                await stream.FlushAsync();
                Console.WriteLine($"{logPrefix} Потік очищено (Flush). Відправка завершена.");
            }
            // ... (решта коду SendRawJsonResponse з catch блоками без змін) ...
            catch (ObjectDisposedException ode) { Console.WriteLine($"{logPrefix} ПОМИЛКА відправки raw JSON: Потік закрито. {ode.Message}"); }
            catch (IOException ioEx) { Console.WriteLine($"{logPrefix} ПОМИЛКА IO відправки raw JSON: {ioEx.Message}"); }
            catch (Exception ex) { Console.WriteLine($"{logPrefix} ЗАГАЛЬНА ПОМИЛКА відправки raw JSON: {ex.GetType().Name}: {ex.Message}"); }
        }

        // --- Методи SendErrorResponse і SendSuccessResponse (використовують SendJsonResponse) ---
        public static async Task SendErrorResponse(NetworkStream stream, string message)
        {
            Dictionary<string, object> responseData = new Dictionary<string, object> { { "success", "false" }, { "message", message } };
            Console.WriteLine($"[ResponseHelper] Формування Error Response: {message}"); // Додано лог перед викликом
            await SendJsonResponse(stream, responseData);
        }

        public static async Task SendSuccessResponse(NetworkStream stream, string message)
        {
            Dictionary<string, object> responseData = new Dictionary<string, object> { { "success", "true" }, { "message", message } };
            Console.WriteLine($"[ResponseHelper] Формування Success Response: {message}"); // Додано лог перед викликом
            await SendJsonResponse(stream, responseData);
        }

        // Допоміжний метод для скорочення довгих рядків у логах
        private static string TruncateForLog(string value, int maxLength = LogDataLengthLimit)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
            {
                return value ?? string.Empty;
            }
            return value.Substring(0, maxLength) + "... (truncated)";
        }
    }
}