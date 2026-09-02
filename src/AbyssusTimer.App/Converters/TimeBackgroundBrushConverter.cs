using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using AbyssusTimer.App.Theme;

namespace AbyssusTimer.App.Converters;

public sealed class TimeBackgroundBrushConverter : IValueConverter
{
    private static readonly Brush ChipBrush = new SolidColorBrush(Palette.SurfaceRaised);

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? ChipBrush : Brushes.Transparent;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
