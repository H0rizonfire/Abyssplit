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
        RunCategory.Tas => "TAS",
        _ => category.ToString(),
    };
}
