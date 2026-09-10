using static AbyssusOverlay.Core.GameOffsets.PlausibilityBounds;

namespace AbyssusOverlay.Core;

public static class AbyssusStatsValidator
{
    public static bool TryValidate(in AbyssusStats stats, out string? reason)
    {
        if (stats.LevelReached < MinLevelReached || stats.LevelReached > MaxLevelReached)
        {
            reason = $"LevelReached={stats.LevelReached} outside [{MinLevelReached},{MaxLevelReached}]";
            return false;
        }

        if (stats.RoomReached < MinRoomReached || stats.RoomReached > MaxRoomReached)
        {
            reason = $"RoomReached={stats.RoomReached} outside [{MinRoomReached},{MaxRoomReached}]";
            return false;
        }

        if (stats.RunTime < MinRunTime || stats.RunTime > MaxRunTime)
        {
            reason = $"RunTime={stats.RunTime} outside [{MinRunTime},{MaxRunTime}]";
            return false;
        }

        if (stats.GoldCollected < MinGoldCollected || stats.GoldCollected > MaxGoldCollected)
        {
            reason = $"GoldCollected={stats.GoldCollected} outside [{MinGoldCollected},{MaxGoldCollected}]";
            return false;
        }

        if (stats.PlayerHealth < MinPlayerHealth || stats.PlayerHealth > MaxPlayerHealth)
        {
            reason = $"PlayerHealth={stats.PlayerHealth} outside [{MinPlayerHealth},{MaxPlayerHealth}]";
            return false;
        }

        reason = null;
        return true;
    }
}
