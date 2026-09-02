namespace AbyssusTimer.App.Interop;

internal static class TermsOfUse
{
    public const string CurrentVersion = "1.1";

    public const string Text =
        "Abyssplit is an unofficial, community-made speedrun timer for Abyssus. It is not " +
        "affiliated with, endorsed by, or connected to the developers or publisher of Abyssus " +
        "in any way.\n\n" +

        "WHAT THIS APP DOES\n" +
        "Abyssplit reads timing-relevant data directly from Abyssus's running process memory " +
        "(run state, floor/room progress, and timers) to power the split timer and overlay. It " +
        "does not read, modify, or interact with any other game data — health, damage, " +
        "inventory, currency, or anything else — and it never writes to the game's memory.\n\n" +

        "Your personal bests, run history, and settings are stored locally on your own computer. " +
        "Nothing is transmitted anywhere automatically. Data only leaves your computer when YOU " +
        "explicitly choose to — exporting a split file to share, or using \"Report an Issue\" to " +
        "open a GitHub issue.\n\n" +

        "YOUR RESPONSIBILITY\n" +
        "You are responsible for ensuring your use of this tool complies with Abyssus's own " +
        "terms of service and EULA. Abyssplit only reads memory for timing purposes and does " +
        "not alter gameplay in any way, but you should independently confirm this is acceptable " +
        "under the game's own rules before using it.\n\n" +

        "LICENSE\n" +
        "Abyssplit is provided under the PolyForm Noncommercial License 1.0.0 — you're free to " +
        "use, modify, and share it for noncommercial purposes. See LICENSE.md in the project " +
        "repository for the full terms. You may not sell Abyssplit or use it for commercial " +
        "purposes.\n\n" +

        "NO WARRANTY\n" +
        "Abyssplit is provided \"as is,\" with no warranty of any kind. The people who made it " +
        "are not liable for any damages, lost time, corrupted data, or other issues arising from " +
        "its use.";
}
