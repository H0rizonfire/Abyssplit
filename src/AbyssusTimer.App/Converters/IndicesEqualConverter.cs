using System.Globalization;
using System.Windows.Data;

namespace AbyssusTimer.App.Converters;

public sealed class IndicesEqualConverter : IMultiValueConverter
{
    public object Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture) =>
        values.Length == 2 && values[0] is int a && values[1] is int b && a == b;

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
