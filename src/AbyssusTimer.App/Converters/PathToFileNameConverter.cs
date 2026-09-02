using System.Globalization;
using System.Windows.Data;

namespace AbyssusTimer.App.Converters;

public sealed class PathToFileNameConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string path && path.Length > 0 ? System.IO.Path.GetFileName(path) : "No image selected";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
