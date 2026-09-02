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
    float? LoadFreeFinalTime = null)
{
    public const int CurrentSchemaVersion = 2;

    public const string FileExtension = ".abysplit";
}
