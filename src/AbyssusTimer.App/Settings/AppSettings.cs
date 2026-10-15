using System.IO;
using System.Text.Json;
using AbyssusOverlay.Core;
using AbyssusTimer.App.Interop;

namespace AbyssusTimer.App.Settings;

public sealed class AppSettings
{
    public string TermsAcceptedVersion { get; set; } = "";

    public double OverlayLeft { get; set; } = 120;
    public double OverlayTop { get; set; } = 120;

    public double OverlayOpacity { get; set; } = OverlayAppearanceDefaults.Opacity;

    public double OverlayScale { get; set; } = OverlayAppearanceDefaults.Scale;

    public string? OverlayBackgroundImagePath { get; set; }

    public double OverlayBiomeFontSize { get; set; } = OverlayAppearanceDefaults.BiomeFontSize;
    public double OverlayDepthFontSize { get; set; } = OverlayAppearanceDefaults.DepthFontSize;
    public double OverlayFloorFontSize { get; set; } = OverlayAppearanceDefaults.FloorFontSize;

    public bool OverlayTimesHaveBackground { get; set; } = OverlayAppearanceDefaults.TimesHaveBackground;

    public string OverlayBiomeTextColor { get; set; } = OverlayAppearanceDefaults.BiomeTextColor;
    public string OverlayDepthTextColor { get; set; } = OverlayAppearanceDefaults.DepthTextColor;
    public string OverlayFloorTextColor { get; set; } = OverlayAppearanceDefaults.FloorTextColor;

    public double OverlayWeaponFontSize { get; set; } = OverlayAppearanceDefaults.WeaponFontSize;
    public string OverlayWeaponTextColor { get; set; } = OverlayAppearanceDefaults.WeaponTextColor;

    public double OverlayStatusFontSize { get; set; } = OverlayAppearanceDefaults.StatusFontSize;
    public string OverlayStatusTextColor { get; set; } = OverlayAppearanceDefaults.StatusTextColor;

    public double OverlayTitleFontSize { get; set; } = OverlayAppearanceDefaults.TitleFontSize;
    public string OverlayTitleTextColor { get; set; } = OverlayAppearanceDefaults.TitleTextColor;

    public bool ShowRawTimer { get; set; } = true;
    public bool ShowLoadFreeTimer { get; set; } = true;
    public bool ShowLoadCutsceneFreeTimer { get; set; } = true;
    public bool ShowPreviousSegment { get; set; } = true;

    public bool PauseStopsTime { get; set; }
    public bool AutoResetOnLobbyReturn { get; set; } = true;

    public bool CountAllWeaponRunsAsAnyPercent { get; set; }

    public bool AllWeaponPreloadChecklist { get; set; }

    public int PbChartRunCount { get; set; } = 25;

    public bool PlaySoundOnNewPb { get; set; } = true;

    public string? PbSoundPath { get; set; }

    public double PbSoundVolume { get; set; } = 0.5;

    public bool AlwaysShowOverlay { get; set; }

    public bool ResetRunHotkeyEnabled { get; set; }

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

    private static string FilePath => Path.Combine(AppPaths.DataDirectory, "settings.json");

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
            AtomicFile.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
