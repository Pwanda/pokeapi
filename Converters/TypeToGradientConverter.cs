using System.Globalization;
using Microsoft.Maui.Controls;
using pokeapi2.Helpers;

namespace pokeapi2.Converters
{
    public class TypeToGradientConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is List<string> types)
            {
                return TypeColors.GetTypeGradient(types);
            }
            return TypeColors.GetTypeGradient(new List<string>());
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}