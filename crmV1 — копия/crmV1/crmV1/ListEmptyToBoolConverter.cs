using System;
using System.Collections; // Для перевірки на IEnumerable
using System.Globalization;
using Xamarin.Forms;

namespace crmV1.Converters // Переконайтесь, що namespace правильний
{
    public class ListEmptyToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Перевіряємо, чи значення є колекцією і чи не null
            if (value is IEnumerable collection)
            {
                // Перевіряємо, чи колекція порожня
                // Можна використати Linq Count(), але це може бути неефективно для великих колекцій.
                // Простіша перевірка - спробувати отримати перший елемент.
                // Якщо колекція порожня, GetEnumerator().MoveNext() поверне false.
                var enumerator = collection.GetEnumerator();
                bool isEmpty = !enumerator.MoveNext();
                // Якщо потрібно інвертувати (true, якщо НЕ порожній), додайте параметр:
                // if (parameter != null && bool.TryParse(parameter.ToString(), out bool invert) && invert) return !isEmpty;
                return isEmpty; // Повертаємо true, якщо колекція порожня
            }

            // Якщо значення null або не є колекцією, вважаємо, що "порожнє" для відображення повідомлення
            return true;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // ConvertBack не потрібен для цього конвертера (одностороннє прив'язування)
            throw new NotImplementedException();
        }
    }
}