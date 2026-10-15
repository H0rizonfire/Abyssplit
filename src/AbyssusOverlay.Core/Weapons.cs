namespace AbyssusOverlay.Core;

public sealed record Weapon(string AssetName, string DisplayName);

public static class Weapons
{
    public static readonly IReadOnlyList<Weapon> All =
    [
        new("PA_EngineRifle", "Engine Rifle"),
        new("PA_Shotgun", "Shotgun"),
        new("PA_TeslaGun", "Tesla Gun"),
        new("PA_BrineRifle", "Brine Revolver"),
        new("PA_BoomerangGun", "Disc Thrower"),
        new("PA_CombatBow", "Combat Bow"),
        new("PA_RocketLauncher", "Plasma Launcher"),
        new("PA_FishDiety", "Fish Deity"),
        new("PA_HarpoonGun", "Harpoon Gun"),
    ];

    private static readonly Dictionary<string, Weapon> ByAssetName =
        All.ToDictionary(weapon => weapon.AssetName, StringComparer.Ordinal);

    public static Weapon? FromAssetName(string? assetName) =>
        assetName is not null && ByAssetName.TryGetValue(assetName, out var weapon) ? weapon : null;
}
