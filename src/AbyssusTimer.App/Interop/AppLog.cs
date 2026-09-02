using System.IO;
using System.Linq;

namespace AbyssusTimer.App.Interop;

internal static class AppLog
{
    private const int RetainedDays = 7;

    public static string LogsDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AbyssusTimer",
        "logs");

    public static string CurrentLogFilePath { get; } =
        Path.Combine(LogsDirectory, $"{DateTime.Now:yyyy-MM-dd}.log");

    private static StreamWriter? _fileWriter;

    public static void Initialize()
    {
        try
        {
            Directory.CreateDirectory(LogsDirectory);
            _fileWriter = new StreamWriter(CurrentLogFilePath, append: true) { AutoFlush = true };
            Prune();
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "?";
            Log($"=== AbyssusTimer session started {DateTime.Now:yyyy-MM-dd HH:mm:ss} — v{version} on {Environment.OSVersion.VersionString} ===");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    public static void Log(string message)
    {
        try
        {
            _fileWriter?.WriteLine($"[{DateTime.Now:HH:mm:ss}] {message}");
        }
        catch (IOException)
        {
        }
    }

    public static void LogException(string context, Exception ex) =>
        Log($"[EXCEPTION] {context}: {ex}");

    public static string ReadTodayForReport(int maxChars = 4000)
    {
        try
        {
            _fileWriter?.Flush();
            var text = File.ReadAllText(CurrentLogFilePath);
            return text.Length <= maxChars ? text : text[^maxChars..];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return "(could not read log file)";
        }
    }

    private static void Prune()
    {
        try
        {
            var files = Directory.GetFiles(LogsDirectory, "*.log")
                .Select(path => new FileInfo(path))
                .OrderByDescending(f => f.Name)
                .ToList();

            foreach (var stale in files.Skip(RetainedDays))
                stale.Delete();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
