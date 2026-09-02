using System.Collections;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AbyssusTimer.App.Converters;

public sealed class EmptyCollectionToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is ICollection { Count: 0 } or null ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
