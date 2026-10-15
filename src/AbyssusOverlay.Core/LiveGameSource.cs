namespace AbyssusOverlay.Core;

public sealed class LiveGameSource : IGameSource
{
    private readonly GameProcess _process = new();
    private readonly AbyssusStatsReader _reader;
    private CutsceneTracker? _cutsceneTracker;
    private HeraldTracker? _heraldTracker;
    private EliteTracker? _eliteTracker;
    private NamePool? _namePool;
    private GameBuildProfile? _attachedBuild;

    public LiveGameSource() => _reader = new AbyssusStatsReader(_process);

    public bool IsAttached => _process.IsAttached;

    public string? ExecutablePath => _process.ExecutablePath;

    public GameStore? Store => _process.IsAttached ? _attachedBuild?.Store : null;

    public string? GameVersion => _process.IsAttached ? _gameVersion : null;
    private volatile string? _gameVersion;
    private CancellationTokenSource? _gameVersionRead;
    private static readonly TimeSpan GameVersionRetryInterval = TimeSpan.FromSeconds(3);

    public bool IsProcessAlive() => _process.IsProcessAlive();

    public bool TryAttach()
    {
        _attachedBuild = GameOffsets.SupportedBuilds.FirstOrDefault(build => _process.TryAttach(build.ProcessName));
        if (_attachedBuild is null)
            return false;

        _reader.Build = _attachedBuild;
        var namePool = new NamePool(_process, _process.ModuleBase + _attachedBuild.GNames);
        _namePool = namePool;
        var objectArray = new GUObjectArrayReader(_process, _process.ModuleBase + _attachedBuild.GUObjectArray);
        _cutsceneTracker = new CutsceneTracker(_process, namePool, objectArray);
        _cutsceneTracker.Start();
        _heraldTracker = new HeraldTracker(namePool, objectArray);
        _heraldTracker.Start();
        _eliteTracker = new EliteTracker(namePool, objectArray);
        _eliteTracker.Start();
        StartGameVersionRead(objectArray, namePool);
        return true;
    }

    private void StartGameVersionRead(GUObjectArrayReader objectArray, NamePool namePool)
    {
        StopGameVersionRead();
        var read = new CancellationTokenSource();
        _gameVersionRead = read;
        var token = read.Token;
        Task.Run(async () =>
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    if (GameProjectVersion.TryRead(_process, objectArray, namePool) is { } version)
                    {
                        if (!token.IsCancellationRequested)
                            _gameVersion = version;
                        return;
                    }
                    await Task.Delay(GameVersionRetryInterval, token);
                }
            }
            catch (OperationCanceledException)
            {
            }
        });
    }

    private void StopGameVersionRead()
    {
        _gameVersionRead?.Cancel();
        _gameVersionRead?.Dispose();
        _gameVersionRead = null;
        _gameVersion = null;
    }

    public void OnDetached()
    {
        StopGameVersionRead();
        _cutsceneTracker?.Stop();
        _heraldTracker?.Stop();
        _eliteTracker?.Stop();
    }

    public bool TryReadFrame(out GameFrame frame)
    {
        frame = GameFrame.Empty;

        if (!_reader.TryReadStats(out var stats))
            return false;

        string? cutsceneAssetName = null;
        if (_cutsceneTracker is not null && _namePool is not null && _cutsceneTracker.TryGetCurrentSequenceAssetName(_namePool, out var assetName))
            cutsceneAssetName = assetName;

        var isCutscenePlaying = _cutsceneTracker?.IsCutscenePlaying() ?? false;

        frame = new GameFrame(stats, _eliteTracker?.CurrentEliteName, _heraldTracker?.CurrentHeraldName, isCutscenePlaying, cutsceneAssetName);
        return true;
    }

    public void Dispose()
    {
        StopGameVersionRead();
        _cutsceneTracker?.Dispose();
        _heraldTracker?.Dispose();
        _eliteTracker?.Dispose();
        _process.Dispose();
    }
}
