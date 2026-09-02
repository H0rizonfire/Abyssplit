using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using AbyssusTimer.App.Theme;

namespace AbyssusTimer.App.Converters;

public sealed class PrimaryTimerToBrushConverter : IValueConverter
{
    private static readonly Brush SelectedBrush = new SolidColorBrush(Palette.BrassLight);
    private static readonly Brush UnselectedBrush = new SolidColorBrush(Palette.TextPrimary);

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value?.ToString() == parameter as string ? SelectedBrush : UnselectedBrush;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
