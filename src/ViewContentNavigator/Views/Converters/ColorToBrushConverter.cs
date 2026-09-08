using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using ViewContentNavigator.Models;

namespace ViewContentNavigator.Views.Converters
{
    public sealed class ColorToBrushConverter : IValueConverter
    {
        private static readonly Brush Empty = Brushes.Transparent;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            switch (value)
            {
                case Color color:
                    return Freeze(new SolidColorBrush(color));
                case PaletteColor palette:
                    return Freeze(new SolidColorBrush(palette.ToMediaColor()));
                default:
                    return Empty;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            Binding.DoNothing;

        private static Brush Freeze(SolidColorBrush brush)
        {
            brush.Freeze();
            return brush;
        }
    }
}
