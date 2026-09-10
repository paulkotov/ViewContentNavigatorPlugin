using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ViewContentNavigator.Views.Converters
{
    public sealed class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var flag = value is bool b && b;
            var options = (parameter as string) ?? string.Empty;

            if (options.IndexOf("Invert", StringComparison.OrdinalIgnoreCase) >= 0)
                flag = !flag;

            if (!flag)
                return options.IndexOf("Hidden", StringComparison.OrdinalIgnoreCase) >= 0
                    ? Visibility.Hidden
                    : Visibility.Collapsed;

            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is Visibility v && v == Visibility.Visible;
    }
}
