namespace AbyssusOverlay.Core;

public sealed class CutsceneTracker : IDisposable
{
    private const int LevelSequenceActor_SequencePlayer = 0x2E8;
    private const int LevelSequenceActor_LevelSequenceAsset = 0x2F0;
    private const int MovieSceneSequencePlayer_Status = 0x290;
    private const byte Status_Playing = 1;

    private static readonly TimeSpan FastRescanDelay = TimeSpan.FromMilliseconds(100);

    private static readonly TimeSpan FullRescanInterval = TimeSpan.FromSeconds(3);

    private static readonly TimeSpan ClassResolutionRetryDelay = TimeSpan.FromMilliseconds(500);

    private static readonly string[] SequenceActorClassNames = { "LevelSequenceActor", "ReplicatedLevelSequenceActor" };

    private readonly GameProcess _process;
    private readonly NamePool _namePool;
    private readonly GUObjectArrayReader _objectArray;

    private volatile nint[] _knownSequenceActorAddresses = Array.Empty<nint>();
    private CancellationTokenSource? _cts;
    private Task? _backgroundLoop;

    public bool FastPathActive { get; private set; }
    public TimeSpan LastScanDuration { get; private set; }
    public bool LastScanWasFullRescan { get; private set; }

    public CutsceneTracker(GameProcess process, NamePool namePool, GUObjectArrayReader objectArray)
    {
        _process = process;
        _namePool = namePool;
        _objectArray = objectArray;
    }

    public void Start()
    {
        if (_backgroundLoop is not null)
            return;

        _cts = new CancellationTokenSource();
        _backgroundLoop = Task.Run(() => RunLoop(_cts.Token));
    }

    public void Stop()
    {
        _cts?.Cancel();
        _backgroundLoop = null;
        _knownSequenceActorAddresses = Array.Empty<nint>();
        FastPathActive = false;
    }

    private void RunLoop(CancellationToken token)
    {
        var classPointers = new HashSet<nint>();

        while (!token.IsCancellationRequested && classPointers.Count == 0)
        {
            try
            {
                foreach (var className in SequenceActorClassNames)
                {
                    if (_objectArray.FindClassPointerByName(className, _namePool) is { } classPointer)
                        classPointers.Add(classPointer);
                }
            }
            catch
            {
            }

            if (classPointers.Count == 0)
                Thread.Sleep(ClassResolutionRetryDelay);
        }

        if (token.IsCancellationRequested)
            return;

        FastPathActive = true;

        var knownMatches = new List<nint>();
        var scannedUpToIndex = 0;
        var lastFullRescan = DateTime.MinValue;

        while (!token.IsCancellationRequested)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var doFullRescan = DateTime.UtcNow - lastFullRescan >= FullRescanInterval;

            try
            {
                if (doFullRescan)
                {
                    knownMatches.Clear();
                    scannedUpToIndex = 0;
                    lastFullRescan = DateTime.UtcNow;
                }

                foreach (var obj in _objectArray.EnumerateObjectsWithClassIn(classPointers, scannedUpToIndex))
                {
                    if (!knownMatches.Contains(obj))
                        knownMatches.Add(obj);
                }

                if (_objectArray.TryGetObjectCount(out var currentCount))
                    scannedUpToIndex = currentCount;

                _knownSequenceActorAddresses = knownMatches.ToArray();
            }
            catch
            {
            }

            LastScanDuration = stopwatch.Elapsed;
            LastScanWasFullRescan = doFullRescan;
            Thread.Sleep(FastRescanDelay);
        }
    }

    public bool IsCutscenePlaying()
    {
        foreach (var address in _knownSequenceActorAddresses)
        {
            if (!_process.TryReadPointer(address + LevelSequenceActor_SequencePlayer, out var player) || player == 0)
                continue;

            var statusBuffer = new byte[1];
            if (_process.TryReadBytes(player + MovieSceneSequencePlayer_Status, statusBuffer) && statusBuffer[0] == Status_Playing)
                return true;
        }

        return false;
    }

    public bool TryGetCurrentSequenceAssetName(NamePool namePool, out string assetName)
    {
        assetName = string.Empty;

        foreach (var address in _knownSequenceActorAddresses)
        {
            if (!_process.TryReadPointer(address + LevelSequenceActor_SequencePlayer, out var player) || player == 0)
                continue;

            var statusBuffer = new byte[1];
            if (!_process.TryReadBytes(player + MovieSceneSequencePlayer_Status, statusBuffer) || statusBuffer[0] != Status_Playing)
                continue;

            if (!_process.TryReadPointer(address + LevelSequenceActor_LevelSequenceAsset, out var asset) || asset == 0)
                continue;

            if (_objectArray.TryGetOwnName(asset, namePool, out assetName))
                return true;
        }

        return false;
    }

    public void Dispose() => Stop();
}
