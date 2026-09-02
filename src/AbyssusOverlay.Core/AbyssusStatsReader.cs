namespace AbyssusOverlay.Core;

public sealed class AbyssusStatsReader
{
    private const int MaxLevelIdsToRead = 8;

    private readonly GameProcess _process;

    public AbyssusStatsReader(GameProcess process) => _process = process;

    public bool TryReadStats(out AbyssusStats stats)
    {
        stats = AbyssusStats.Empty;

        if (!_process.IsAttached)
            return false;

        var gWorldAddress = _process.ModuleBase + GameOffsets.GWorld;

        if (!_process.TryReadPointer(gWorldAddress, out var world) || world == 0)
            return false;

        if (!_process.TryReadPointer(world + GameOffsets.World_GameState, out var gameState) || gameState == 0)
            return false;

        if (!_process.TryReadPointer(gameState + GameOffsets.GameState_PlayerArray, out var playerArrayData) || playerArrayData == 0)
            return false;

        if (!_process.TryReadPointer(playerArrayData, out var playerState) || playerState == 0)
            return false;

        _process.TryReadInt32(playerState + GameOffsets.PlayerState_GoldCollected, out var gold);
        _process.TryReadInt32(playerState + GameOffsets.PlayerState_LevelReached, out var level);
        _process.TryReadInt32(playerState + GameOffsets.PlayerState_RoomReached, out var room);
        _process.TryReadFloat(playerState + GameOffsets.PlayerState_RunTime, out var runTime);
        _process.TryReadBool(playerState + GameOffsets.PlayerState_RunSuccessful, out var runSuccessful);

        _process.TryReadPointer(gameState + GameOffsets.GameState_CommonLoadingScreen, out var commonLoadingScreen);
        var isLoading = commonLoadingScreen != 0;

        _process.TryReadBool(gameState + GameOffsets.GameState_IsInRun, out var isInRun);

        var isPaused = false;
        if (_process.TryReadPointer(world + GameOffsets.World_PersistentLevel, out var persistentLevel) && persistentLevel != 0
            && _process.TryReadPointer(persistentLevel + GameOffsets.Level_WorldSettings, out var worldSettings) && worldSettings != 0
            && _process.TryReadPointer(worldSettings + GameOffsets.WorldSettings_PauserPlayerState, out var pauserPlayerState))
        {
            isPaused = pauserPlayerState != 0;
        }

        var namePool = new NamePool(_process, _process.ModuleBase + GameOffsets.GNames);

        var health = 0f;
        var levelIds = Array.Empty<string>();

        if (_process.TryReadPointer(playerState + GameOffsets.PlayerState_PawnPrivate, out var pawn) && pawn != 0)
        {
            if (_process.TryReadPointer(pawn + GameOffsets.Pawn_HealthComponent, out var healthComponent) && healthComponent != 0)
            {
                _process.TryReadFloat(healthComponent + GameOffsets.HealthComponent_CurrentHealth, out health);
            }

            if (_process.TryReadPointer(pawn + GameOffsets.Pawn_CachedLevelGenerator, out var levelGenerator) && levelGenerator != 0)
            {
                levelIds = ReadLevelIds(levelGenerator + GameOffsets.LevelGenerator_CurrentLevelIds, namePool);
            }
        }

        stats = new AbyssusStats(gold, level, room, runTime, health, isLoading, isInRun, runSuccessful, isPaused, levelIds);
        return true;
    }

    private string[] ReadLevelIds(nint arrayFieldAddress, NamePool namePool)
    {
        if (!_process.TryReadArrayHeader(arrayFieldAddress, out var data, out var count) || data == 0 || count <= 0)
            return Array.Empty<string>();

        var toRead = Math.Min(count, MaxLevelIdsToRead);
        var results = new List<string>(toRead);

        for (var i = 0; i < toRead; i++)
        {
            if (!_process.TryReadFName(data + i * 8, out var comparisonIndex, out var number))
                continue;

            results.Add(namePool.TryResolve(comparisonIndex, number, out var name) ? name : $"<unresolved {comparisonIndex}:{number}>");
        }

        return results.ToArray();
    }
}
