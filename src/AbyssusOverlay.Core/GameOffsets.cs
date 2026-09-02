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
}
