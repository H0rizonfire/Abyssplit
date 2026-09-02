using System.Globalization;
using System.Windows.Data;
using AbyssusTimer.App.Engine;

namespace AbyssusTimer.App.Converters;

public sealed class ComparisonSourceToDisplayNameConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        ComparisonSource.SpecificRun => "Specific Run",
        ComparisonSource.ImportedFile => "Imported File",
        ComparisonSource other => other.ToString(),
        _ => value?.ToString() ?? "",
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
