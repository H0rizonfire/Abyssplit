using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using AbyssusTimer.App.Engine;
using AbyssusTimer.App.Theme;

namespace AbyssusTimer.App.Converters;

public sealed class DeltaSeverityToBrushConverter : IValueConverter
{
    private static readonly Brush AheadBrush = new SolidColorBrush(Palette.Toxic);
    private static readonly Brush CloseBrush = new SolidColorBrush(Palette.Warning);
    private static readonly Brush BehindBrush = new SolidColorBrush(Palette.DangerText);
    private static readonly Brush NeutralBrush = new SolidColorBrush(Palette.TextMuted);

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        DeltaSeverity.Ahead => AheadBrush,
        DeltaSeverity.Close => CloseBrush,
        DeltaSeverity.Behind => BehindBrush,
        _ => NeutralBrush,
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
