using System.Globalization;
using System.Windows.Data;
using AbyssusTimer.App.Engine;

namespace AbyssusTimer.App.Converters;

public sealed class SplitListOverflowBehaviorToMaxHeightConverter : IValueConverter
{
    private const double ScrollMaxHeight = 240;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is SplitListOverflowBehavior.Scroll ? ScrollMaxHeight : double.PositiveInfinity;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
