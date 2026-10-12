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
        return true;
    }

    public void OnDetached()
    {
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
        _cutsceneTracker?.Dispose();
        _heraldTracker?.Dispose();
        _eliteTracker?.Dispose();
        _process.Dispose();
    }
}
