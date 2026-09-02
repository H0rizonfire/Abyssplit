using System.Text.RegularExpressions;

namespace AbyssusOverlay.Core;

public static class LevelContext
{
    private const string StartingRoomMarker = "_Starting_Room";
    private const string BossRoomPrefix = "BossRoom_";
    private const string EliteEnemySuffix = "_EventRoom_EliteEnemy";
    private const string VoidLobbyMarker = "Void_Lobby";
    private static readonly Regex VariantSuffixRegex = new(@"_Variant_\d+$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static string ExtractBiomeName(IReadOnlyList<string> currentLevelIds)
    {
        foreach (var id in currentLevelIds)
        {
            var markerIndex = id.IndexOf(StartingRoomMarker, StringComparison.Ordinal);
            if (markerIndex > 0)
                return id[..markerIndex];
        }

        return currentLevelIds.Count > 0 ? string.Join(", ", currentLevelIds) : "-";
    }

    public static bool TryExtractStartingRoomBiome(IReadOnlyList<string> currentLevelIds, out string biomeName)
    {
        foreach (var id in currentLevelIds)
        {
            var markerIndex = id.IndexOf(StartingRoomMarker, StringComparison.Ordinal);
            if (markerIndex > 0)
            {
                biomeName = id[..markerIndex];
                return true;
            }
        }

        biomeName = "";
        return false;
    }

    public static bool TryExtractBossIdentifier(IReadOnlyList<string> currentLevelIds, out string bossIdentifier)
    {
        foreach (var id in currentLevelIds)
        {
            var prefixIndex = id.IndexOf(BossRoomPrefix, StringComparison.OrdinalIgnoreCase);
            if (prefixIndex < 0)
                continue;

            var afterPrefix = id[(prefixIndex + BossRoomPrefix.Length)..];
            bossIdentifier = VariantSuffixRegex.Replace(afterPrefix, "");
            return true;
        }

        bossIdentifier = "";
        return false;
    }

    public static bool TryExtractEliteEncounterBiome(IReadOnlyList<string> currentLevelIds, out string biomeName)
    {
        foreach (var id in currentLevelIds)
        {
            var suffixIndex = id.IndexOf(EliteEnemySuffix, StringComparison.OrdinalIgnoreCase);
            if (suffixIndex > 0)
            {
                biomeName = id[..suffixIndex];
                return true;
            }
        }

        biomeName = "";
        return false;
    }

    public static bool HasVoidLobbyMarker(IReadOnlyList<string> currentLevelIds)
    {
        foreach (var id in currentLevelIds)
            if (id.Contains(VoidLobbyMarker, StringComparison.OrdinalIgnoreCase))
                return true;

        return false;
    }
}
