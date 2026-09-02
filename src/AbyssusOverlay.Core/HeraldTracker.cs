namespace AbyssusOverlay.Core;

public sealed class HeraldTracker : IDisposable
{
    private static readonly TimeSpan ScanCooldown = TimeSpan.FromSeconds(2);

    private static readonly IReadOnlyDictionary<string, string> HeraldClassNames = new Dictionary<string, string>
    {
        ["BP_GolemPrototype_C"] = "Golem Prototype",
        ["BP_GardensChieftain_C"] = "Primal Chieftain",
        ["BP_TruebornChampion_C"] = "Trueborn Champion",
    };

    public static IReadOnlyCollection<string> AllHeraldNames { get; } = HeraldClassNames.Values.ToHashSet();

    private readonly NamePool _namePool;
    private readonly GUObjectArrayReader _objectArray;

    private volatile string? _currentHeraldName;
    private CancellationTokenSource? _cts;
    private Task? _backgroundLoop;

    public HeraldTracker(NamePool namePool, GUObjectArrayReader objectArray)
    {
        _namePool = namePool;
        _objectArray = objectArray;
    }

    public string? CurrentHeraldName => _currentHeraldName;

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
        _currentHeraldName = null;
    }

    private void RunLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                string? found = null;
                foreach (var obj in _objectArray.EnumerateObjects())
                {
                    if (_objectArray.IsClassDefaultObject(obj))
                        continue;

                    if (_objectArray.TryGetClassName(obj, _namePool, out var className) && HeraldClassNames.TryGetValue(className, out var displayName))
                    {
                        found = displayName;
                        break;
                    }
                }

                _currentHeraldName = found;
            }
            catch
            {
            }

            Thread.Sleep(ScanCooldown);
        }
    }

    public void Dispose() => Stop();
}
