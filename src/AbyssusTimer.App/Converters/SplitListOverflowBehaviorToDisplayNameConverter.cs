using System.Globalization;
using System.Windows.Data;
using AbyssusTimer.App.Engine;

namespace AbyssusTimer.App.Converters;

public sealed class SplitListOverflowBehaviorToDisplayNameConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        SplitListOverflowBehavior.Collapse => "Collapse",
        SplitListOverflowBehavior.Scroll => "Scroll",
        SplitListOverflowBehavior.FullList => "Full List",
        _ => value?.ToString() ?? "",
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
