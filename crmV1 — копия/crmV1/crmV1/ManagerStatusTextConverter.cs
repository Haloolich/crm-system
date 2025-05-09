// crmV1/Converters/ManagerStatusTextConverter.cs
using System;
using System.Globalization;
using Xamarin.Forms;

namespace crmV1.Converters
{
    public class ManagerStatusTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string status)
            {
                // Виконуємо переклад статусів
                switch (status.ToLower())
                {
                    case "hired":
                        return "Найнятий";
                    case "dismissed":
                        return "Звільнений";
                    // Додайте інші статуси, якщо вони є
                    default:
                        return status; // Повертаємо оригінал, якщо статус невідомий
                }
            }
            // Повертаємо порожній рядок або null, якщо вхідне значення не рядок
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Зворотне перетворення зазвичай не потрібне для відображення тексту
            throw new NotImplementedException();
        }
    }
}