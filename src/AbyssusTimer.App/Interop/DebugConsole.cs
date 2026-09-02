using System.IO;

namespace AbyssusTimer.App.Interop;

internal static class DebugConsole
{
    private static StreamWriter? _fileWriter;

    public static string LogFilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AbyssusTimer",
        "diagnostics.log");

    public static void Attach()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(LogFilePath)!);
        _fileWriter = new StreamWriter(LogFilePath, append: true) { AutoFlush = true };

        Log($"=== AbyssusTimer diagnostic session started {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
    }

    public static void Log(string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss.fff}] {message}";
        _fileWriter?.WriteLine(line);
    }
}
