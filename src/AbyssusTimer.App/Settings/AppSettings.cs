using System.IO;
using System.Text.Json;

namespace AbyssusTimer.App.Settings;

public sealed class AppSettings
{
    public string TermsAcceptedVersion { get; set; } = "";

    public double OverlayLeft { get; set; } = 120;
    public double OverlayTop { get; set; } = 120;

    public double OverlayOpacity { get; set; } = 0.90;

    public double OverlayScale { get; set; } = 1.0;

    public string? OverlayBackgroundImagePath { get; set; }

    public double OverlayBiomeFontSize { get; set; } = 11;
    public double OverlayDepthFontSize { get; set; } = 11;
    public double OverlayFloorFontSize { get; set; } = 10;

    public bool OverlayTimesHaveBackground { get; set; }

    public string OverlayBiomeTextColor { get; set; } = "#C98F3F";
    public string OverlayDepthTextColor { get; set; } = "#8A9994";
    public string OverlayFloorTextColor { get; set; } = "#56635E";

    public double OverlayTitleFontSize { get; set; } = 11;
    public string OverlayTitleTextColor { get; set; } = "#C98F3F";

    public bool ShowRawTimer { get; set; } = true;
    public bool ShowLoadFreeTimer { get; set; } = true;
    public bool ShowLoadCutsceneFreeTimer { get; set; } = true;
    public bool ShowPreviousSegment { get; set; } = true;

    public bool PauseStopsTime { get; set; }
    public bool AutoResetOnLobbyReturn { get; set; } = true;

    public bool UseSoftwareRendering { get; set; } = true;

    public string PrimaryTimer { get; set; } = nameof(Engine.PrimaryTimerKind.LoadCutsceneFree);

    public string ActiveCategory { get; set; } = nameof(Engine.RunCategory.AnyPercent);

    public string ActivePlayerCount { get; set; } = nameof(Engine.PlayerCount.Solo);

    public string ComparisonSource { get; set; } = nameof(Engine.ComparisonSource.Best);

    public string ExportRunnerName { get; set; } = "";

    public string SplitDetailLevel { get; set; } = nameof(Engine.SplitDetailLevel.PerDepth);

    public string SplitListOverflowBehavior { get; set; } = nameof(Engine.SplitListOverflowBehavior.FullList);

    public bool AutoDeleteRunHistory { get; set; } = true;

    public int RunHistoryLimit { get; set; } = 50;

    public string RunHistoryTrimStrategy { get; set; } = nameof(Engine.RunHistoryTrimStrategy.Slowest);

    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AbyssusTimer",
        "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath))
                return new AppSettings();

            var json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return new AppSettings();
        }
    }

    public void Save()
    {
        try
        {
            var directory = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
