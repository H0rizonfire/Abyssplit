using System.Globalization;
using System.Windows.Data;
using AbyssusTimer.App.Engine;

namespace AbyssusTimer.App.Converters;

public sealed class RunCategoryToDisplayNameConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is RunCategory category ? category.DisplayName() : value?.ToString() ?? "";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
