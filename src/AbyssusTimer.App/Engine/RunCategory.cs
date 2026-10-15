namespace AbyssusTimer.App.Engine;

public enum RunCategory
{
    AnyPercent,
    AllBosses,
    TrueEnding,
    AnyPercentGlitchless,
    TrueEndingGlitchless,
    NoSoulWheel,
    AllWeaponPercent,
    AllWeaponPercentTrueEnding,
    Tas,
}

public static class RunCategoryExtensions
{
    public static string DisplayName(this RunCategory category) => category switch
    {
        RunCategory.AnyPercent => "Any%",
        RunCategory.AllBosses => "All Bosses",
        RunCategory.TrueEnding => "True Ending",
        RunCategory.AnyPercentGlitchless => "Any% Glitchless",
        RunCategory.TrueEndingGlitchless => "True Ending Glitchless",
        RunCategory.NoSoulWheel => "No Soul Wheel",
        RunCategory.AllWeaponPercent => "All Weapon%",
        RunCategory.AllWeaponPercentTrueEnding => "All Weapon% True Ending",
        RunCategory.Tas => "TAS",
        _ => category.ToString(),
    };

    public static bool IsAllWeapon(this RunCategory category) =>
        category is RunCategory.AllWeaponPercent or RunCategory.AllWeaponPercentTrueEnding;

    public static RunCategory SingleRunCategory(this RunCategory category) =>
        category == RunCategory.AllWeaponPercentTrueEnding ? RunCategory.TrueEnding : RunCategory.AnyPercent;
}
