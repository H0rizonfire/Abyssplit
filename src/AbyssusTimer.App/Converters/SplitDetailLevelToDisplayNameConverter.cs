using System.Globalization;
using System.Windows.Data;
using AbyssusTimer.App.Engine;

namespace AbyssusTimer.App.Converters;

public sealed class SplitDetailLevelToDisplayNameConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        SplitDetailLevel.Total => "Total",
        SplitDetailLevel.PerDepth => "Per-Depth",
        SplitDetailLevel.PerRoom => "Per-Room",
        _ => value?.ToString() ?? "",
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
