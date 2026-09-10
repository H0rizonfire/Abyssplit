namespace AbyssusOverlay.Core;

public readonly record struct GameFrame(
    AbyssusStats Stats,
    string? CurrentEliteName,
    string? CurrentHeraldName,
    bool IsCutscenePlaying,
    string? CurrentCutsceneAssetName)
{
    public static readonly GameFrame Empty = new(AbyssusStats.Empty, null, null, false, null);
}
