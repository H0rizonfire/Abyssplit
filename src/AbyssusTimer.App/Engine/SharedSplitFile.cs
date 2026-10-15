namespace AbyssusTimer.App.Engine;

public sealed record SharedSplitFile(
    int SchemaVersion,
    string RunnerName,
    string Category,
    string PlayerCount,
    DateTime Timestamp,
    bool Completed,
    float FinalTime,
    Dictionary<int, float> FloorSegments,
    string AppVersion,
    float? RawFinalTime = null,
    float? LoadFreeFinalTime = null,
    string? Weapon = null,
    string? GameVersion = null,
    string? GameBuild = null,
    List<AbyssusOverlay.Core.WeaponRunResult>? WeaponRuns = null,
    float? BetweenRunsTime = null)
{
    public const int CurrentSchemaVersion = 3;

    public const string FileExtension = ".abysplit";
}
