namespace AbyssusOverlay.Core;

public static class GameOffsets
{
    public const string ProcessName = "RGame-Win64-Shipping";

    public const int GWorld = 0xA9912C8;
    public const int GNames = 0xA71FD80;

    public const int World_GameState = 0x1B0;
    public const int GameState_PlayerArray = 0x2C0;

    public const int World_PersistentLevel = 0x30;
    public const int Level_WorldSettings = 0x2B0;
    public const int WorldSettings_PauserPlayerState = 0x4A8;

    public const int GameState_IsInRun = 0x0420;

    public const int GameState_CommonLoadingScreen = 0x10F0;

    public const int PlayerState_PawnPrivate = 0x320;
    public const int PlayerState_GoldCollected = 0x390;
    public const int PlayerState_RunTime = 0x414;
    public const int PlayerState_LevelReached = 0x418;
    public const int PlayerState_RoomReached = 0x41C;

    public const int PlayerState_RunSuccessful = 0x410;

    public const int Pawn_HealthComponent = 0x340;
    public const int HealthComponent_CurrentHealth = 0x200;

    public const int Pawn_CachedLevelGenerator = 0x4768;

    public const int LevelGenerator_CurrentLevelIds = 0x348;

    public const int LevelGenerator_LastLevelIds = 0x368;

    public const int LevelGenerator_LobbyElevatorSequenceActive = 0x3D2;

    public const int GUObjectArray = 0xA803880;

    public const string GamePassProcessName = "RGame-WinGDK-Shipping";
    public const int GamePassGWorld = 0xA08FC78;
    public const int GamePassGNames = 0x9E1F9C0;
    public const int GamePassGUObjectArray = 0x9F03550;

    public static readonly GameBuildProfile SteamBuild = new(GameStore.Steam, ProcessName, GWorld, GNames, GUObjectArray);
    public static readonly GameBuildProfile GamePassBuild = new(GameStore.GamePass, GamePassProcessName, GamePassGWorld, GamePassGNames, GamePassGUObjectArray);

    public static readonly IReadOnlyList<GameBuildProfile> SupportedBuilds = [SteamBuild, GamePassBuild];

    public const string? LastVerifiedGameVersion = null;

    public static class PlausibilityBounds
    {
        public const int MinLevelReached = 0;
        public const int MaxLevelReached = 50;

        public const int MinRoomReached = 0;
        public const int MaxRoomReached = 30;

        public const float MinRunTime = 0f;
        public const float MaxRunTime = 6 * 3600f;

        public const int MinGoldCollected = 0;
        public const int MaxGoldCollected = 1_000_000;

        public const float MinPlayerHealth = -1f;
        public const float MaxPlayerHealth = 10_000f;
    }
}
