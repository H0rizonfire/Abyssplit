using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AbyssusTimer.App.Converters;

public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string { Length: > 0 } ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
