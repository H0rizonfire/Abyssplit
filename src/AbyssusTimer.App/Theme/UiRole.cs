using System.Windows;

namespace AbyssusTimer.App.Theme;

public static class UiRole
{
    public static readonly DependencyProperty NameProperty = DependencyProperty.RegisterAttached(
        "Name", typeof(string), typeof(UiRole), new PropertyMetadata(null));

    public static string? GetName(DependencyObject element) => (string?)element.GetValue(NameProperty);

    public static void SetName(DependencyObject element, string? value) => element.SetValue(NameProperty, value);
}
