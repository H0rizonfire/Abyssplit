using System.Text.RegularExpressions;

namespace AbyssusOverlay.Core;

public enum GameStore
{
    Steam,
    GamePass,
}

public sealed record GameBuildProfile(GameStore Store, string ProcessName, int GWorld, int GNames, int GUObjectArray);

public static class GameStoreExtensions
{
    public static string DisplayName(this GameStore store) => store switch
    {
        GameStore.Steam => "Steam",
        GameStore.GamePass => "Game Pass",
        _ => store.ToString(),
    };
}

public static class GameBuildVersion
{
    public const string SteamAppId = "1721110";

    public static string? TryRead(GameStore store, string? exePath)
    {
        if (string.IsNullOrEmpty(exePath))
            return null;

        try
        {
            return store switch
            {
                GameStore.Steam => TryReadSteamBuild(exePath),
                GameStore.GamePass => TryParseGamePassPackageVersion(exePath),
                _ => null,
            };
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string? TryReadSteamBuild(string exePath)
    {
        for (var dir = new DirectoryInfo(Path.GetDirectoryName(exePath)!); dir is not null; dir = dir.Parent)
        {
            if (!dir.Name.Equals("steamapps", StringComparison.OrdinalIgnoreCase))
                continue;

            var manifestPath = Path.Combine(dir.FullName, $"appmanifest_{SteamAppId}.acf");
            return File.Exists(manifestPath) ? ParseSteamBuildId(File.ReadAllText(manifestPath)) : null;
        }

        return null;
    }

    public static string? ParseSteamBuildId(string manifestText)
    {
        var match = Regex.Match(manifestText, "\"buildid\"\\s+\"(\\d+)\"");
        return match.Success ? $"build {match.Groups[1].Value}" : null;
    }

    public static string? TryParseGamePassPackageVersion(string exePath)
    {
        foreach (var segment in exePath.Split('\\', '/'))
        {
            var match = Regex.Match(segment, @"^[^_]+_(\d+(?:\.\d+){1,3})_[^_]+__[^_]+$");
            if (match.Success)
                return match.Groups[1].Value;
        }

        return null;
    }
}
