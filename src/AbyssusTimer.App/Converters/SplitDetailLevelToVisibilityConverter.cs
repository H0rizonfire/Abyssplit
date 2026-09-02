using System.Globalization;
using System.Windows;
using System.Windows.Data;
using AbyssusTimer.App.Engine;

namespace AbyssusTimer.App.Converters;

public sealed class SplitDetailLevelToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is SplitDetailLevel.Total ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
