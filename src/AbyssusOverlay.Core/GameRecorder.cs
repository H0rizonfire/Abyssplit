using System.Text.Json;

namespace AbyssusOverlay.Core;

public sealed class GameRecorder : IDisposable
{
    public const int FormatVersion = 1;
    public static readonly TimeSpan DefaultMaxAge = TimeSpan.FromDays(14);
    public const long DefaultMaxTotalBytes = 200L * 1024 * 1024;

    private const int FlushEveryLines = 50;

    private readonly Func<DateTime> _utcNow;
    private readonly DateTime _startedUtc;
    private readonly ArrayBufferWriterStream _line = new();
    private FileStream? _writer;
    private int _linesSinceFlush;
    private GameFrame? _previous;

    public string? FilePath { get; }
    public bool IsEnabled => _writer is not null;

    private GameRecorder(string filePath, FileStream writer, Func<DateTime> utcNow, DateTime startedUtc)
    {
        FilePath = filePath;
        _writer = writer;
        _utcNow = utcNow;
        _startedUtc = startedUtc;
    }

    private GameRecorder()
    {
        _utcNow = () => DateTime.UtcNow;
    }

    public static GameRecorder Open(string directory, string appVersion, Func<DateTime>? utcNow = null, TimeSpan? maxAge = null, long? maxTotalBytes = null)
    {
        var clock = utcNow ?? (() => DateTime.UtcNow);
        try
        {
            var startedUtc = clock();
            Directory.CreateDirectory(directory);
            Prune(directory, startedUtc, maxAge ?? DefaultMaxAge, maxTotalBytes ?? DefaultMaxTotalBytes);

            var path = Path.Combine(directory, $"recording-{startedUtc.ToLocalTime():yyyyMMdd-HHmmss-fff}.jsonl");
            var writer = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, bufferSize: 64 * 1024);
            var recorder = new GameRecorder(path, writer, clock, startedUtc);
            recorder.WriteLine(json =>
            {
                json.WriteString("type", "header");
                json.WriteNumber("format", FormatVersion);
                json.WriteString("app", appVersion);
                json.WriteString("startedUtc", startedUtc.ToUniversalTime().ToString("O"));
            }, flush: true);
            return recorder;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new GameRecorder();
        }
    }

    public static void Prune(string directory, DateTime utcNow, TimeSpan maxAge, long maxTotalBytes)
    {
        var files = new DirectoryInfo(directory).GetFiles("recording-*.jsonl")
            .OrderBy(f => f.LastWriteTimeUtc)
            .ToList();

        foreach (var file in files.Where(f => utcNow - f.LastWriteTimeUtc > maxAge).ToList())
        {
            TryDelete(file);
            files.Remove(file);
        }

        var total = files.Sum(f => f.Length);
        foreach (var file in files)
        {
            if (total <= maxTotalBytes)
                break;

            total -= file.Length;
            TryDelete(file);
        }
    }

    private static void TryDelete(FileInfo file)
    {
        try
        {
            file.Delete();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    public void RecordAttach(GameStore? store, string? version)
    {
        _previous = null;
        WriteLine(json =>
        {
            json.WriteString("type", "attach");
            json.WriteNumber("t", ElapsedMicroseconds());
            if (store is { } s)
                json.WriteString("store", s.DisplayName());
            if (version is not null)
                json.WriteString("version", version);
        }, flush: true);
    }

    public void RecordAttachFailed() =>
        WriteLine(json =>
        {
            json.WriteString("type", "attachFailed");
            json.WriteNumber("t", ElapsedMicroseconds());
        }, flush: true);

    public void RecordDetach()
    {
        _previous = null;
        WriteLine(json =>
        {
            json.WriteString("type", "detach");
            json.WriteNumber("t", ElapsedMicroseconds());
        }, flush: true);
    }

    public void RecordContext(bool pauseStopsTime, bool autoResetOnLobbyReturn, string category, string playerCount) =>
        WriteLine(json =>
        {
            json.WriteString("type", "context");
            json.WriteNumber("t", ElapsedMicroseconds());
            json.WriteBoolean("pauseStopsTime", pauseStopsTime);
            json.WriteBoolean("autoReset", autoResetOnLobbyReturn);
            json.WriteString("category", category);
            json.WriteString("playerCount", playerCount);
        }, flush: true);

    public void RecordResetRun() =>
        WriteLine(json =>
        {
            json.WriteString("type", "resetRun");
            json.WriteNumber("t", ElapsedMicroseconds());
        }, flush: true);

    public void RecordReadFailed() =>
        WriteLine(json =>
        {
            json.WriteNumber("t", ElapsedMicroseconds());
            json.WriteNumber("x", 1);
        }, flush: false);

    public void RecordFrame(in GameFrame frame)
    {
        var previous = _previous;
        var current = frame;
        WriteLine(json =>
        {
            json.WriteNumber("t", ElapsedMicroseconds());
            WriteChangedFields(json, previous, current);
        }, flush: false);
        _previous = frame;
    }

    private static void WriteChangedFields(Utf8JsonWriter json, GameFrame? previous, GameFrame current)
    {
        var s = current.Stats;
        var p = previous?.Stats;

        if (p is null || p.Value.GoldCollected != s.GoldCollected) json.WriteNumber("g", s.GoldCollected);
        if (p is null || p.Value.LevelReached != s.LevelReached) json.WriteNumber("l", s.LevelReached);
        if (p is null || p.Value.RoomReached != s.RoomReached) json.WriteNumber("r", s.RoomReached);
        if (p is null || !p.Value.RunTime.Equals(s.RunTime)) json.WriteNumber("rt", s.RunTime);
        if (p is null || !p.Value.PlayerHealth.Equals(s.PlayerHealth)) json.WriteNumber("hp", s.PlayerHealth);
        if (p is null || p.Value.IsLoading != s.IsLoading) json.WriteBoolean("ld", s.IsLoading);
        if (p is null || p.Value.IsInRun != s.IsInRun) json.WriteBoolean("ir", s.IsInRun);
        if (p is null || p.Value.RunSuccessful != s.RunSuccessful) json.WriteBoolean("rs", s.RunSuccessful);
        if (p is null || p.Value.IsPaused != s.IsPaused) json.WriteBoolean("p", s.IsPaused);
        if (p is null || !p.Value.CurrentLevelIds.SequenceEqual(s.CurrentLevelIds))
        {
            json.WriteStartArray("ids");
            foreach (var id in s.CurrentLevelIds)
                json.WriteStringValue(id);
            json.WriteEndArray();
        }

        if (p is null || p.Value.WeaponAssetName != s.WeaponAssetName) WriteNullableString(json, "w", s.WeaponAssetName);

        if (previous is null || previous.Value.CurrentEliteName != current.CurrentEliteName) WriteNullableString(json, "el", current.CurrentEliteName);
        if (previous is null || previous.Value.CurrentHeraldName != current.CurrentHeraldName) WriteNullableString(json, "he", current.CurrentHeraldName);
        if (previous is null || previous.Value.IsCutscenePlaying != current.IsCutscenePlaying) json.WriteBoolean("cp", current.IsCutscenePlaying);
        if (previous is null || previous.Value.CurrentCutsceneAssetName != current.CurrentCutsceneAssetName) WriteNullableString(json, "ca", current.CurrentCutsceneAssetName);
    }

    private static void WriteNullableString(Utf8JsonWriter json, string name, string? value)
    {
        if (value is null)
            json.WriteNull(name);
        else
            json.WriteString(name, value);
    }

    private long ElapsedMicroseconds() => (_utcNow() - _startedUtc).Ticks / (TimeSpan.TicksPerMillisecond / 1000);

    private void WriteLine(Action<Utf8JsonWriter> writeBody, bool flush)
    {
        if (_writer is null)
            return;

        try
        {
            _line.Reset();
            using (var json = new Utf8JsonWriter(_line))
            {
                json.WriteStartObject();
                writeBody(json);
                json.WriteEndObject();
            }

            _writer.Write(_line.WrittenSpan);
            _writer.WriteByte((byte)'\n');
            if (flush || ++_linesSinceFlush >= FlushEveryLines)
            {
                _writer.Flush();
                _linesSinceFlush = 0;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ObjectDisposedException)
        {
            Disable();
        }
    }

    private void Disable()
    {
        try
        {
            _writer?.Dispose();
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
        }

        _writer = null;
    }

    public void Dispose() => Disable();

    private sealed class ArrayBufferWriterStream : Stream
    {
        private byte[] _buffer = new byte[512];
        private int _length;

        public ReadOnlySpan<byte> WrittenSpan => _buffer.AsSpan(0, _length);

        public void Reset() => _length = 0;

        public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            if (_length + buffer.Length > _buffer.Length)
                Array.Resize(ref _buffer, Math.Max(_buffer.Length * 2, _length + buffer.Length));

            buffer.CopyTo(_buffer.AsSpan(_length));
            _length += buffer.Length;
        }

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => _length;
        public override long Position { get => _length; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
