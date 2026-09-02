namespace AbyssusOverlay.Core;

public readonly record struct AbyssusStats(
    int GoldCollected,
    int LevelReached,
    int RoomReached,
    float RunTime,
    float PlayerHealth,
    bool IsLoading,
    bool IsInRun,
    bool RunSuccessful,
    bool IsPaused,
    IReadOnlyList<string> CurrentLevelIds)
{
    public static readonly AbyssusStats Empty =
        new(0, 0, 0, 0f, 0f, false, false, false, false, Array.Empty<string>());
}
