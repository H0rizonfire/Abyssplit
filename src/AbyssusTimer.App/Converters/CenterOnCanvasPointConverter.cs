using System.Globalization;
using System.Windows.Data;

namespace AbyssusTimer.App.Converters;

public sealed class CenterOnCanvasPointConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is double coordinate && parameter is string sizeText && double.TryParse(sizeText, NumberStyles.Float, CultureInfo.InvariantCulture, out var size)
            ? coordinate - size / 2
            : value ?? 0.0;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
