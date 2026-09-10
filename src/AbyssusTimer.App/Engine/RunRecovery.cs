using System.IO;
using System.Text.Json;
using AbyssusOverlay.Core;

namespace AbyssusTimer.App.Engine;

internal sealed class RunRecoveryData
{
    public DateTime Timestamp { get; set; }
    public string Category { get; set; } = "";
    public string PlayerCount { get; set; } = "";
    public Dictionary<int, float> FloorSegments { get; set; } = new();
}

internal sealed class RunRecovery
{
    private readonly string _filePath;

    public RunRecovery(string filePath) => _filePath = filePath;

    public void Save(RunRecoveryData data)
    {
        try
        {
            AtomicFile.WriteAllText(_filePath, JsonSerializer.Serialize(data));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    public RunRecoveryData? Load()
    {
        try
        {
            if (!File.Exists(_filePath))
                return null;

            return JsonSerializer.Deserialize<RunRecoveryData>(File.ReadAllText(_filePath));
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public void Clear()
    {
        try
        {
            if (File.Exists(_filePath))
                File.Delete(_filePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
