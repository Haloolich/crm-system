using System;
using System.Globalization;
using Xamarin.Forms;

namespace crmV1.Converters
{
    public class BoolToColorConverter : IValueConverter
    {
        public Color TrueColor { get; set; } = Color.Default;
        public Color FalseColor { get; set; } = Color.Default;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? TrueColor : FalseColor;
            }
            return FalseColor; // Повертаємо FalseColor за замовчуванням, якщо значення не булеве
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
    public class BoolToFontAttributesConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue && boolValue)
            {
                return FontAttributes.Bold; // Якщо true, робимо текст жирним
            }
            return FontAttributes.None; // Якщо false або не булеве, звичайний текст
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

}