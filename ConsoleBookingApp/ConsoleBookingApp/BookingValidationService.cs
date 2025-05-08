using System;
using System.Collections.Generic;
using System.Globalization;

namespace ConsoleBookingApp.Services
{
    public static class BookingValidationService
    {
        public static bool TryParseSessionDate(string dateStr, out DateTime date)
        {
            return DateTime.TryParseExact(dateStr, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
        }

        public static bool TryParseBookingTime(string startTimeStr, string endTimeStr, out TimeSpan startTime, out TimeSpan endTime)
        {
            startTime = default; endTime = default;
            if (TimeSpan.TryParse(startTimeStr, out startTime) && TimeSpan.TryParse(endTimeStr, out endTime) && endTime > startTime)
            {
                return true;
            }
            return false;
        }

        public static bool IsValidZoneCount(string countStr, out int count)
        {
            if (int.TryParse(countStr, out count) && count > 0)
            {
                return true;
            }
            count = 0;
            return false;
        }

        public static bool IsValidSessionType(string sessionType)
        {
            var validTypes = new HashSet<string> { "PS", "VR", "Quest" };
            return !string.IsNullOrWhiteSpace(sessionType) && validTypes.Contains(sessionType);
        }

        public static bool IsValidId(string idStr, out int id)
        {
            if (int.TryParse(idStr, out id) && id > 0) // Зазвичай ID > 0
            {
                return true;
            }
            id = 0;
            return false;
        }

        public static bool IsValidOrderData(
         Dictionary<string, string> data,
         out int sessionId,
         out int managerId,
         out int clubId)
        {
            // Ініціалізація значень за замовчуванням
            sessionId = 0;
            managerId = 0;
            clubId = 0;

            // Використовуємо TryGetValue для безпечного отримання рядка та TryParse для перетворення
            if (!data.TryGetValue("session_id", out string sessionIdStr) ||
                !int.TryParse(sessionIdStr, out sessionId) || sessionId <= 0)
            {
                Console.WriteLine("[ВАЛІДАЦІЯ] Невірний або відсутній session_id.");
                return false;
            }

            if (!data.TryGetValue("manager_id", out string managerIdStr) ||
                !int.TryParse(managerIdStr, out managerId) || managerId <= 0)
            {
                Console.WriteLine($"[ВАЛІДАЦІЯ] Невірний або відсутній manager_id для session_id {sessionId}.");
                return false;
            }

            if (!data.TryGetValue("club_id", out string clubIdStr) ||
                !int.TryParse(clubIdStr, out clubId) || clubId <= 0)
            {
                Console.WriteLine($"[ВАЛІДАЦІЯ] Невірний або відсутній club_id для session_id {sessionId}.");
                return false;
            }

            // Якщо всі значення коректно отримані та розпарсились
            return true;
        }
        public static bool IsValidPaymentData(
        Dictionary<string, string> data,
        out int sessionId,
        out int managerId,
        out int clubId,
        out decimal amount,
        out string paymentMethod,
        out DateTime paymentTime)
        {
            // ... (код IsValidPaymentData з попередньої відповіді) ...
            // Він коректно парсить string значення amount, payment_method, payment_time
            // та викликає IsValidOrderData для session_id, manager_id, club_id
            sessionId = 0; managerId = 0; clubId = 0; amount = 0m; paymentMethod = null; paymentTime = DateTime.MinValue;

            if (!IsValidOrderData(data, out sessionId, out managerId, out clubId)) return false;

            if (!data.TryGetValue("amount", out string amountStr) || !decimal.TryParse(amountStr, NumberStyles.Any, CultureInfo.InvariantCulture, out amount) || amount <= 0)
            {
                Console.WriteLine($"[ВАЛІДАЦІЯ] Невірна або відсутня amount для session_id {sessionId}. Отримано: '{amountStr ?? "null"}'");
                return false;
            }

            if (!data.TryGetValue("payment_method", out string methodStr) || string.IsNullOrWhiteSpace(methodStr))
            {
                Console.WriteLine($"[ВАЛІДАЦІЯ] Невірний або відсутній payment_method для session_id {sessionId}.");
                paymentMethod = null;
                return false;
            }
            paymentMethod = methodStr;

            if (!data.TryGetValue("payment_time", out string timeStr) || !DateTime.TryParseExact(timeStr, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out paymentTime))
            {
                Console.WriteLine($"[ВАЛІДАЦІЯ] Невірний або відсутній payment_time для session_id {sessionId}. Отримано: '{timeStr ?? "null"}'");
                return false;
            }

            return true;
        }
        public static bool IsValidAvailabilityCheckData(
        Dictionary<string, string> data,
        out DateTime checkDate,
        out TimeSpan checkStartTime,
        out TimeSpan checkEndTime,
        out int clubId)
        {
            checkDate = DateTime.MinValue;
            checkStartTime = TimeSpan.Zero;
            checkEndTime = TimeSpan.Zero;
            clubId = 0;

            // Валідація club_id
            if (!data.TryGetValue("club_id", out string clubIdStr) || !int.TryParse(clubIdStr, out clubId) || clubId <= 0)
            {
                Console.WriteLine($"[ВАЛІДАЦІЯ] Невірний або відсутній club_id для перевірки доступності. Отримано: '{clubIdStr ?? "null"}'");
                return false;
            }

            // Валідація date
            if (!data.TryGetValue("date", out string dateStr) || !DateTime.TryParseExact(dateStr, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out checkDate))
            {
                Console.WriteLine($"[ВАЛІДАЦІЯ] Невірна або відсутня date для перевірки доступності. Отримано: '{dateStr ?? "null"}'");
                return false;
            }

            // Валідація start_time (використовуємо TimeSpan для представлення часу без дати)
            if (!data.TryGetValue("start_time", out string startTimeStr) || !TimeSpan.TryParseExact(startTimeStr, @"hh\:mm\:ss", CultureInfo.InvariantCulture, out checkStartTime))
            {
                // Спробуємо інший формат, якщо hh:mm:ss не спрацював (наприклад, якщо клієнт шле тільки hh:mm)
                if (!TimeSpan.TryParseExact(startTimeStr, @"hh\:mm", CultureInfo.InvariantCulture, out checkStartTime))
                {
                    Console.WriteLine($"[ВАЛІДАЦІЯ] Невірний або відсутній start_time для перевірки доступності. Очікується формат HH:mm:ss або HH:mm. Отримано: '{startTimeStr ?? "null"}'");
                    return false;
                }
            }


            // Валідація end_time (використовуємо TimeSpan)
            if (!data.TryGetValue("end_time", out string endTimeStr) || !TimeSpan.TryParseExact(endTimeStr, @"hh\:mm\:ss", CultureInfo.InvariantCulture, out checkEndTime))
            {
                if (!TimeSpan.TryParseExact(endTimeStr, @"hh\:mm", CultureInfo.InvariantCulture, out checkEndTime))
                {
                    Console.WriteLine($"[ВАЛІДАЦІЯ] Невірний або відсутній end_time для перевірки доступності. Очікується формат HH:mm:ss або HH:mm. Отримано: '{endTimeStr ?? "null"}'");
                    return false;
                }
            }


            // Додаткова логічна валідація: час початку має бути раніше часу кінця
            // Цю перевірку можна також зробити в обробнику, але можна і тут.
            // Якщо тривалість сеансу 0 або від'ємна, це недійсний проміжок.
            // if (checkStartTime >= checkEndTime)
            // {
            //     Console.WriteLine($"[ВАЛІДАЦІЯ] start_time ({checkStartTime}) має бути раніше end_time ({checkEndTime}).");
            //     return false; // Можна повернути false або обробляти окремо в сервісі
            // }
            // Переніс цю перевірку в Service, щоб дати більш специфічне повідомлення.


            return true;
        }
        public static bool IsValidDateAndClubData(
        Dictionary<string, string> data,
        out DateTime checkDate,
        out int clubId)
        {
            checkDate = DateTime.MinValue;
            clubId = 0;

            // Валідація club_id
            if (!data.TryGetValue("club_id", out string clubIdStr) || !int.TryParse(clubIdStr, out clubId) || clubId <= 0)
            {
                Console.WriteLine($"[ВАЛІДАЦІЯ] Невірний або відсутній club_id. Отримано: '{clubIdStr ?? "null"}'");
                return false;
            }

            // Валідація date
            if (!data.TryGetValue("date", out string dateStr) || !DateTime.TryParseExact(dateStr, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out checkDate))
            {
                Console.WriteLine($"[ВАЛІДАЦІЯ] Невірна або відсутня date. Очікується формат YYYY-MM-DD. Отримано: '{dateStr ?? "null"}'");
                return false;
            }

            return true;
        }
        public static bool TryParseDecimal(string value, out decimal result)
        {
            // Використовуємо CultureInfo.InvariantCulture для стабільного парсингу числа з точкою як десятковим роздільником
            return decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out result);
        }
        public static bool IsValidOrder(string sessionIdStr, string managerIdStr, string clubIdStr, out int sessionId, out int managerId, out int clubId)
        {
            // Ініціалізація значень за замовчуванням
            sessionId = 0;
            managerId = 0;
            clubId = 0;

            // !!! Нова логіка валідації: спробувати розпарсити кожен рядок окремо
            bool isSessionIdValid = int.TryParse(sessionIdStr, out sessionId) && sessionId > 0;
            bool isManagerIdValid = int.TryParse(managerIdStr, out managerId) && managerId > 0;
            bool isClubIdValid = int.TryParse(clubIdStr, out clubId) && clubId > 0;

            // Повертаємо true, тільки якщо всі три значення коректно розпарсились і є більшими за 0
            return isSessionIdValid && isManagerIdValid && isClubIdValid;
        }
        public static bool IsValidName(string name)
        {
            return !string.IsNullOrWhiteSpace(name); // Проста перевірка на порожнечу
        }

        public static bool IsValidPhoneNumber(string phone)
        {
            // Додайте сюди складнішу перевірку формату за потреби
            return !string.IsNullOrWhiteSpace(phone);
        }
    }
}