using ConsoleBookingApp.Data;
using MySqlConnector;
using System;
using System.Threading.Tasks;

namespace ConsoleBookingApp.Services
{
    public class BookingPriceResult
    {
        public decimal CalculatedPrice { get; set; }
        public decimal FinalPrice { get; set; }
        public int? AppliedDiscountId { get; set; } // Зберігаємо ID знижки, що була застосована
        public int AppliedDiscountPercent { get; set; } // Зберігаємо відсоток
    }

    public static class PricingService
    {
        public class PricingResult
        {
            public decimal CalculatedPrice { get; set; }
            public decimal FinalPrice { get; set; }
            public int? AppliedDiscountId { get; set; }
        }
        public static async Task<PricingResult> CalculateBookingPriceAsync(string connectionString, string sessionType, int clubId, TimeSpan startTime, TimeSpan endTime, int? discountId, MySqlConnection connection, MySqlTransaction transaction = null)
        {
            decimal pricePerHour = await BookingRepository.GetPricePerHourAsync(connectionString, sessionType, clubId, connection, transaction);
            if (pricePerHour < 0) throw new Exception("Could not retrieve base price.");

            TimeSpan duration = endTime - startTime;
            // Припускаємо, що ціна НЕ залежить від кількості людей, а лише від тривалості
            decimal calculatedPrice = pricePerHour * (decimal)duration.TotalHours;
            decimal finalPrice = calculatedPrice;
            int? appliedDiscountId = null;
            int discountPercent = 0;

            if (discountId.HasValue)
            {
                discountPercent = await BookingRepository.GetDiscountPercentAsync(connectionString, discountId.Value, clubId, connection, transaction);
                if (discountPercent > 0)
                {
                    finalPrice = calculatedPrice * (1 - (decimal)discountPercent / 100);
                    appliedDiscountId = discountId;
                }
            }

            Console.WriteLine($"[PricingService] Base Price/Hour: {pricePerHour}, Duration: {duration.TotalHours}h");
            Console.WriteLine($"[PricingService] Calculated Price: {calculatedPrice:F2}");
            if (appliedDiscountId.HasValue) Console.WriteLine($"[PricingService] Applied Discount ID: {appliedDiscountId}, Percent: {discountPercent}%, Final Price: {finalPrice:F2}");
            else Console.WriteLine($"[PricingService] No discount applied. Final Price: {finalPrice:F2}");

            return new PricingResult
            {
                CalculatedPrice = calculatedPrice,
                FinalPrice = finalPrice,
                AppliedDiscountId = appliedDiscountId
            };
        }
    }
}