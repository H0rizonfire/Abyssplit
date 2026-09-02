namespace AbyssusTimer.App.Interop;

internal static class StartupRegistration
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Abyssplit";

    public static void SetEnabled(bool enabled)
    {
        using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        if (key is null)
            return;

        if (enabled)
        {
            var exePath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exePath))
                return;
            key.SetValue(ValueName, $"\"{exePath}\" --tray");
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }

    public static bool IsEnabled()
    {
        using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return key?.GetValue(ValueName) is not null;
    }
}
