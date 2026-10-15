namespace AbyssusOverlay.Core;

public sealed record WeaponRunResult(string? WeaponAssetName, float Time, bool Completed, bool CountsTowardTotal, bool WrongEnding = false);

public enum AllWeaponAttemptState
{
    NotStarted,
    InRun,
    BetweenRuns,
    Completed,
    EndedIncomplete,
}

public sealed class AllWeaponAttempt
{
    public static int RequiredWeapons => Weapons.All.Count;

    private readonly List<WeaponRunResult> _results = new();
    private readonly HashSet<string> _completedWeapons = new(StringComparer.Ordinal);
    private DateTime? _lastTickUtc;

    public AllWeaponAttemptState State { get; private set; } = AllWeaponAttemptState.NotStarted;
    public IReadOnlyList<WeaponRunResult> Results => _results;
    public IReadOnlyCollection<string> CompletedWeapons => _completedWeapons;
    public string? CurrentWeaponAssetName { get; private set; }

    public float BetweenRunsTime { get; private set; }

    public bool IsActive => State is AllWeaponAttemptState.InRun or AllWeaponAttemptState.BetweenRuns;
    public bool IsFinished => State is AllWeaponAttemptState.Completed or AllWeaponAttemptState.EndedIncomplete;

    public void Tick(DateTime tickUtc, bool excludeFromTime)
    {
        if (_lastTickUtc is { } last && State == AllWeaponAttemptState.BetweenRuns && !excludeFromTime)
            BetweenRunsTime += (float)(tickUtc - last).TotalSeconds;

        _lastTickUtc = tickUtc;
    }

    public void RunTimerStarted(string? weaponAssetName)
    {
        if (State is not (AllWeaponAttemptState.NotStarted or AllWeaponAttemptState.BetweenRuns))
            return;

        CurrentWeaponAssetName = weaponAssetName;
        State = AllWeaponAttemptState.InRun;
    }

    public void RunCompleted(float runTime, bool wrongEnding = false)
    {
        if (State != AllWeaponAttemptState.InRun)
            return;

        var known = Weapons.FromAssetName(CurrentWeaponAssetName) is not null;
        var counts = known && !wrongEnding && _completedWeapons.Add(CurrentWeaponAssetName!);
        _results.Add(new WeaponRunResult(CurrentWeaponAssetName, runTime, Completed: true, CountsTowardTotal: counts, WrongEnding: wrongEnding));

        CurrentWeaponAssetName = null;
        State = _completedWeapons.Count >= RequiredWeapons ? AllWeaponAttemptState.Completed : AllWeaponAttemptState.BetweenRuns;
    }

    public void RunEndedIncomplete(float runTimeSoFar)
    {
        if (State != AllWeaponAttemptState.InRun)
            return;

        _results.Add(new WeaponRunResult(CurrentWeaponAssetName, runTimeSoFar, Completed: false, CountsTowardTotal: false));
        CurrentWeaponAssetName = null;
        State = AllWeaponAttemptState.EndedIncomplete;
    }

    public void Abandon()
    {
        if (State == AllWeaponAttemptState.BetweenRuns)
            State = AllWeaponAttemptState.EndedIncomplete;
    }

    public float Total(float currentRunTime = 0f) =>
        _results.Sum(r => r.Time) + BetweenRunsTime + (State == AllWeaponAttemptState.InRun ? currentRunTime : 0f);
}
