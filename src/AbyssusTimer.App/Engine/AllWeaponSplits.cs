using AbyssusOverlay.Core;

namespace AbyssusTimer.App.Engine;

public sealed record WeaponGroupViewModel(
    string Name,
    string TimeText,
    string? NoteText,
    bool IsCurrent,
    bool IsPending,
    IReadOnlyList<BiomeGroupViewModel> BiomeGroups,
    bool ShowBiomesInOverlay)
{
    public double RowOpacity => IsPending ? 0.45 : 1.0;
}

public static class AllWeaponSplits
{
    public static List<WeaponGroupViewModel> Build(
        IReadOnlyList<WeaponRunResult> results,
        string? currentWeapon,
        bool isInRun,
        float currentRunTime,
        IReadOnlyList<IReadOnlyList<BiomeGroupViewModel>> runGroups,
        bool preloadAllWeapons,
        SplitDetailLevel detailLevel,
        SplitListOverflowBehavior overflowBehavior,
        Func<float, string> formatTime)
    {
        var currentRunNumber = isInRun ? results.Count : (int?)null;

        IReadOnlyList<BiomeGroupViewModel> GroupsFor(int runNumber) =>
            runNumber < runGroups.Count ? runGroups[runNumber] : Array.Empty<BiomeGroupViewModel>();

        bool ShowBiomes(bool isCurrent) =>
            detailLevel != SplitDetailLevel.Total
            && (isCurrent || overflowBehavior != SplitListOverflowBehavior.Collapse);

        WeaponGroupViewModel RowForRun(int runNumber)
        {
            var isCurrent = runNumber == currentRunNumber;
            string? weapon;
            string timeText;
            string? note;
            if (isCurrent)
            {
                weapon = currentWeapon;
                timeText = formatTime(currentRunTime);
                note = NoteForCurrentRun(results, currentWeapon);
            }
            else
            {
                var result = results[runNumber];
                weapon = result.WeaponAssetName;
                timeText = formatTime(result.Time);
                note = RunNote(result);
            }

            return new WeaponGroupViewModel(DisplayName(weapon), timeText, note, isCurrent, IsPending: false, GroupsFor(runNumber), ShowBiomes(isCurrent));
        }

        var runNumbers = Enumerable.Range(0, results.Count + (isInRun ? 1 : 0)).ToList();
        if (!preloadAllWeapons)
            return runNumbers.Select(RowForRun).ToList();

        var rows = new List<WeaponGroupViewModel>();
        var placed = new HashSet<int>();
        foreach (var weapon in Weapons.All)
        {
            var slotRun = runNumbers.FirstOrDefault(n => n < results.Count && results[n].CountsTowardTotal && results[n].WeaponAssetName == weapon.AssetName, -1);
            if (slotRun < 0 && currentRunNumber is { } current && currentWeapon == weapon.AssetName)
                slotRun = current;
            if (slotRun < 0)
                slotRun = runNumbers.FirstOrDefault(n => n < results.Count && !results[n].Completed && results[n].WeaponAssetName == weapon.AssetName, -1);

            if (slotRun >= 0)
            {
                placed.Add(slotRun);
                rows.Add(RowForRun(slotRun));
            }
            else
            {
                rows.Add(new WeaponGroupViewModel(weapon.DisplayName, "-", null, IsCurrent: false, IsPending: true, Array.Empty<BiomeGroupViewModel>(), ShowBiomesInOverlay: false));
            }
        }

        rows.AddRange(runNumbers.Where(n => !placed.Contains(n)).Select(RowForRun));
        return rows;
    }

    public static string DisplayName(string? weaponAssetName) =>
        Weapons.FromAssetName(weaponAssetName)?.DisplayName ?? weaponAssetName ?? "Unknown weapon";

    public static string? RunNote(WeaponRunResult result) =>
        !result.Completed ? "ended"
        : result.WrongEnding ? "normal ending, not counted"
        : result.CountsTowardTotal ? null
        : Weapons.FromAssetName(result.WeaponAssetName) is null ? "not counted"
        : "repeat";

    private static string? NoteForCurrentRun(IReadOnlyList<WeaponRunResult> results, string? currentWeapon) =>
        Weapons.FromAssetName(currentWeapon) is null ? "not counted"
        : results.Any(r => r.CountsTowardTotal && r.WeaponAssetName == currentWeapon) ? "repeat"
        : null;
}
