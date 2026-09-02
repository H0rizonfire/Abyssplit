using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AbyssusTimer.App.Converters;

public sealed class ComparisonSourceToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value?.ToString() == parameter as string ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
