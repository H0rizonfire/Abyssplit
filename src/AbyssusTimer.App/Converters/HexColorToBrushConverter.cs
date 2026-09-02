using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using AbyssusTimer.App.Theme;

namespace AbyssusTimer.App.Converters;

public sealed class HexColorToBrushConverter : IValueConverter
{
    private static readonly Brush Fallback = new SolidColorBrush(Palette.TextPrimary);

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string hex)
        {
            try
            {
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)!);
            }
            catch (FormatException)
            {
            }
        }

        return Fallback;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
