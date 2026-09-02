namespace AbyssusOverlay.Core;

public sealed class EliteTracker : IDisposable
{
    private static readonly TimeSpan ScanCooldown = TimeSpan.FromSeconds(2);

    private static readonly IReadOnlyDictionary<string, string> EliteClassNames = new Dictionary<string, string>
    {
        ["BP_Elite_Golem_Sentry_C"] = "Elite Overseer",
        ["BP_SubmarineEnemy_Elite_C"] = "Elite Engine",
        ["BP_EliteGardenEnemy_C"] = "Elite Berserker",
        ["BP_EliteSanctuaryEnemy_C"] = "Elite Catalyst",
    };

    private readonly NamePool _namePool;
    private readonly GUObjectArrayReader _objectArray;

    private volatile string? _currentEliteName;
    private CancellationTokenSource? _cts;
    private Task? _backgroundLoop;

    public EliteTracker(NamePool namePool, GUObjectArrayReader objectArray)
    {
        _namePool = namePool;
        _objectArray = objectArray;
    }

    public string? CurrentEliteName => _currentEliteName;

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
        _currentEliteName = null;
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

                    if (_objectArray.TryGetClassName(obj, _namePool, out var className) && EliteClassNames.TryGetValue(className, out var displayName))
                    {
                        found = displayName;
                        break;
                    }
                }

                _currentEliteName = found;
            }
            catch
            {
            }

            Thread.Sleep(ScanCooldown);
        }
    }

    public void Dispose() => Stop();
}
