using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace TaskPlanner.App;

/// <summary>
/// Collapses when the bound value is true; the inverse of the built-in BooleanToVisibilityConverter.
/// </summary>
public sealed class InverseBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool boolValue = value is bool typedValue && typedValue;
        return boolValue ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
