using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace OnlyMSlideManager.Converters;

[ValueConversion(typeof(bool), typeof(Thickness))]
public class BooleanToBorderThicknessConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value != null && (bool)value)
        {
            return new Thickness(2);
        }

        return new Thickness(0);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}