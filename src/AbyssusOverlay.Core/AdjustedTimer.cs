namespace AbyssusOverlay.Core;

public sealed class AdjustedTimer
{
    private float? _lastRunTime;
    private bool _wasLoading;
    private bool _wasInRun;
    private bool _hasStarted;
    private DateTime _lastUpdateUtc = DateTime.UtcNow;

    public float AdjustedRunTime { get; private set; }

    public bool HasStarted => _hasStarted;

    public bool WasLoadingAtLastUpdate => _wasLoading;

    public void Update(float rawRunTime, bool isInRun, bool isLoadingForStartGate, bool excludeFromTimer, bool countThroughStalledClock = false)
    {
        var nowUtc = DateTime.UtcNow;
        var wallClockDelta = (float)(nowUtc - _lastUpdateUtc).TotalSeconds;
        _lastUpdateUtc = nowUtc;

        var newRunStarted = isInRun && !_wasInRun;
        _wasInRun = isInRun;

        if (_lastRunTime is not { } lastRunTime || rawRunTime < lastRunTime || newRunStarted)
        {
            AdjustedRunTime = 0f;
            _hasStarted = false;
            _wasLoading = isLoadingForStartGate;
            _lastRunTime = rawRunTime;
            return;
        }

        if (!_hasStarted)
        {
            if (_wasLoading && !isLoadingForStartGate)
                _hasStarted = true;

            _wasLoading = isLoadingForStartGate;
            _lastRunTime = rawRunTime;
            return;
        }

        if (!excludeFromTimer)
        {
            var gameClockDelta = rawRunTime - lastRunTime;
            AdjustedRunTime += countThroughStalledClock && gameClockDelta <= 0f ? wallClockDelta : gameClockDelta;
        }

        _wasLoading = isLoadingForStartGate;
        _lastRunTime = rawRunTime;
    }

    public void Reset()
    {
        _lastRunTime = null;
        _wasLoading = false;
        _wasInRun = false;
        _hasStarted = false;
        _lastUpdateUtc = DateTime.UtcNow;
        AdjustedRunTime = 0f;
    }
}
