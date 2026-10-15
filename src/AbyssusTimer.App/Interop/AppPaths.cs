using System.IO;

namespace AbyssusTimer.App.Interop;

internal static class AppPaths
{
    private static readonly string Default = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AbyssusTimer");

    private static readonly string ShadowDefault = Default + "-shadow";

    public static string DataDirectory { get; private set; } = Default;

    public static bool IsShadow { get; private set; }

    public static void ConfigureFromArgs(IEnumerable<string> args)
    {
        if (!args.Contains("--shadow", StringComparer.OrdinalIgnoreCase))
            return;

        IsShadow = true;
        DataDirectory = ShadowDefault;
    }
}
