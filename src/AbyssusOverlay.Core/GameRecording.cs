using System.Text.Json;

namespace AbyssusOverlay.Core;

public abstract record RecordingEvent(long Microseconds);
public sealed record RecordedAttach(long Microseconds, GameStore? Store, string? Version) : RecordingEvent(Microseconds);
public sealed record RecordedAttachFailed(long Microseconds) : RecordingEvent(Microseconds);
public sealed record RecordedDetach(long Microseconds) : RecordingEvent(Microseconds);
public sealed record RecordedFrame(long Microseconds, GameFrame Frame) : RecordingEvent(Microseconds);
public sealed record RecordedReadFailed(long Microseconds) : RecordingEvent(Microseconds);
public sealed record RecordedContext(long Microseconds, bool PauseStopsTime, bool AutoResetOnLobbyReturn, string Category, string PlayerCount) : RecordingEvent(Microseconds);
public sealed record RecordedResetRun(long Microseconds) : RecordingEvent(Microseconds);

public sealed class GameRecording
{
    public required int Format { get; init; }
    public required string AppVersion { get; init; }
    public required DateTime StartedUtc { get; init; }
    public required IReadOnlyList<RecordingEvent> Events { get; init; }

    public bool WasTruncated { get; init; }

    public static GameRecording Load(string path)
    {
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using Stream content = path.EndsWith(".gz", StringComparison.OrdinalIgnoreCase)
            ? new System.IO.Compression.GZipStream(file, System.IO.Compression.CompressionMode.Decompress)
            : file;
        using var reader = new StreamReader(content);

        var lines = new List<string>();
        while (reader.ReadLine() is { } line)
            lines.Add(line);

        return Parse(lines);
    }

    public static GameRecording Parse(IReadOnlyList<string> lines)
    {
        if (lines.Count == 0)
            throw new InvalidDataException("Recording is empty.");

        using var headerDoc = JsonDocument.Parse(lines[0]);
        var header = headerDoc.RootElement;
        if (!header.TryGetProperty("type", out var headerType) || headerType.GetString() != "header")
            throw new InvalidDataException("Recording doesn't start with a header line.");

        var format = header.GetProperty("format").GetInt32();
        if (format > GameRecorder.FormatVersion)
            throw new NotSupportedException($"Recording format {format} is newer than this build supports ({GameRecorder.FormatVersion}).");

        var events = new List<RecordingEvent>();
        var current = GameFrame.Empty;
        var truncated = false;

        for (var i = 1; i < lines.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
                continue;

            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(lines[i]);
            }
            catch (JsonException) when (i == lines.Count - 1)
            {
                truncated = true;
                break;
            }

            using (doc)
            {
                var line = doc.RootElement;
                var t = line.GetProperty("t").GetInt64();
                var type = line.TryGetProperty("type", out var typeProperty) ? typeProperty.GetString() : null;

                switch (type)
                {
                    case null when line.TryGetProperty("x", out _):
                        events.Add(new RecordedReadFailed(t));
                        break;
                    case null:
                        current = ApplyChanges(current, line);
                        events.Add(new RecordedFrame(t, current));
                        break;
                    case "attach":
                        events.Add(new RecordedAttach(t, ParseStore(line), OptionalString(line, "version")));
                        break;
                    case "attachFailed":
                        events.Add(new RecordedAttachFailed(t));
                        break;
                    case "detach":
                        events.Add(new RecordedDetach(t));
                        break;
                    case "context":
                        events.Add(new RecordedContext(t,
                            line.GetProperty("pauseStopsTime").GetBoolean(),
                            line.GetProperty("autoReset").GetBoolean(),
                            line.GetProperty("category").GetString()!,
                            line.GetProperty("playerCount").GetString()!));
                        break;
                    case "resetRun":
                        events.Add(new RecordedResetRun(t));
                        break;
                    default:
                        break;
                }
            }
        }

        return new GameRecording
        {
            Format = format,
            AppVersion = header.GetProperty("app").GetString() ?? "",
            StartedUtc = DateTime.Parse(header.GetProperty("startedUtc").GetString()!, null, System.Globalization.DateTimeStyles.RoundtripKind),
            Events = events,
            WasTruncated = truncated,
        };
    }

    private static GameFrame ApplyChanges(GameFrame previous, JsonElement line)
    {
        var s = previous.Stats;
        var stats = new AbyssusStats(
            line.TryGetProperty("g", out var g) ? g.GetInt32() : s.GoldCollected,
            line.TryGetProperty("l", out var l) ? l.GetInt32() : s.LevelReached,
            line.TryGetProperty("r", out var r) ? r.GetInt32() : s.RoomReached,
            line.TryGetProperty("rt", out var rt) ? rt.GetSingle() : s.RunTime,
            line.TryGetProperty("hp", out var hp) ? hp.GetSingle() : s.PlayerHealth,
            line.TryGetProperty("ld", out var ld) ? ld.GetBoolean() : s.IsLoading,
            line.TryGetProperty("ir", out var ir) ? ir.GetBoolean() : s.IsInRun,
            line.TryGetProperty("rs", out var rs) ? rs.GetBoolean() : s.RunSuccessful,
            line.TryGetProperty("p", out var p) ? p.GetBoolean() : s.IsPaused,
            line.TryGetProperty("ids", out var ids) ? ids.EnumerateArray().Select(id => id.GetString()!).ToArray() : s.CurrentLevelIds,
            line.TryGetProperty("w", out var w) ? w.GetString() : s.WeaponAssetName);

        return new GameFrame(
            stats,
            line.TryGetProperty("el", out var el) ? el.GetString() : previous.CurrentEliteName,
            line.TryGetProperty("he", out var he) ? he.GetString() : previous.CurrentHeraldName,
            line.TryGetProperty("cp", out var cp) ? cp.GetBoolean() : previous.IsCutscenePlaying,
            line.TryGetProperty("ca", out var ca) ? ca.GetString() : previous.CurrentCutsceneAssetName);
    }

    private static GameStore? ParseStore(JsonElement line) =>
        OptionalString(line, "store") switch
        {
            "Steam" => GameStore.Steam,
            "Game Pass" => GameStore.GamePass,
            _ => null,
        };

    private static string? OptionalString(JsonElement line, string name) =>
        line.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
