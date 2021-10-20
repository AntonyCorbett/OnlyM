using System;
using System.Windows;
using System.Windows.Data;

namespace OnlyM.CustomControls.Converters
{
    public class BorderThicknessToStrokeThicknessConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value == null)
            {
                return 1.0;
            }

            var thickness = (Thickness)value;
            return (thickness.Bottom + thickness.Left + thickness.Right + thickness.Top) / 4;
        }

        public object ConvertBack(object? value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value == null)
            {
                return new Thickness(1.0);
            }

            var thick = (int)value;
            return new Thickness(thick, thick, thick, thick);
        }
    }
}
