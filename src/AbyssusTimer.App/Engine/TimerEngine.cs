using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Threading;
using AbyssusOverlay.Core;
using AbyssusTimer.App.Interop;
using AbyssusTimer.App.Settings;

namespace AbyssusTimer.App.Engine;

public sealed class TimerEngine : INotifyPropertyChanged, IDisposable
{
    private readonly object _tickLock = new();
    private readonly Dispatcher _uiDispatcher = Dispatcher.CurrentDispatcher;

    internal Dispatcher UiDispatcherForTests => _uiDispatcher;

    private readonly IGameSource _gameSource;
    private AdjustedTimer _adjustedTimer;
    private AdjustedTimer _loadAndCutsceneFreeTimer;

    private RunHold? _runHold;
    private sealed record RunHold(float RunTime, int LevelReached, int RoomReached, float RunTimeAtDrop, AdjustedTimer StandbyLoadFree, AdjustedTimer StandbyLoadCutsceneFree);

    private const float RunHoldNewRunClockSeconds = 10f;

    private float? _midRunEvidenceStartRunTime;
    private bool _joinedMidRunThisAttach;
    private const float MidRunEvidenceClockSeconds = 1f;

    public bool IsJoinedMidRun { get => _isJoinedMidRun; private set => SetField(ref _isJoinedMidRun, value); }
    private bool _isJoinedMidRun;

    private float _previousTickRunTime;
    private bool _previousTickIsInRun;
    private int _previousTickLevelReached;
    private int _previousTickRoomReached;

    private readonly Func<DateTime> _clock;
    private DateTime _tickUtc;
    private readonly RunSession _session = new();
    private readonly PersonalBests _personalBests;
    private readonly RunHistory _runHistory;
    private readonly RunRecovery _runRecovery;
    private readonly List<(FloorSplit Split, bool IsNewBest, bool IsBossRoom, string? EliteName, bool IsRoyalAbyssContent, bool IsTorakaContent, IReadOnlyList<string?> RoomHeraldNames)> _annotatedFloorSplits = new();

    private RunRecoveryData? _currentRunRecovery;

    private readonly Dictionary<string, (int Attempts, int Completed)> _sessionStats = new();

    private AbyssusStats _lastStats = AbyssusStats.Empty;

    private string? _lastCutsceneAssetName;
    private bool _currentRunCompleted;

    private AllWeaponAttempt? _allWeaponAttempt;

    private bool _currentRunIsAllWeapon;

    private string? _currentRunWeapon;
    private string? _currentRunAnyPercentKey;

    private readonly HashSet<string> _defeatedBiomeBosses = new(StringComparer.Ordinal);

    private readonly List<IReadOnlyList<BiomeGroupViewModel>> _allWeaponRunGroups = new();
    private int? _allWeaponLiveRunNumber;

    private volatile bool _forceSplitsRebuild;

    private bool _wasAttachedForLog;
    private DateTime _lastTickExceptionLogTime = DateTime.MinValue;

    private volatile string _gameBuildText = "Not detected this session";
    public string GameBuildText => _gameBuildText;

    private string? RecordedGameBuild => _gameSource.Store is null ? null : _gameBuildText;

    private bool _hasLoggedGameVersionThisAttach;

    public string? RecordingPath => (_gameSource as RecordingGameSource)?.RecordingPath;

    private DateTime _lastSplitsRebuildTime = DateTime.MinValue;
    private static readonly TimeSpan SplitsRebuildInterval = TimeSpan.FromMilliseconds(250);

    private const double TickTimingLogThresholdMs = 10.0;

#if TRUSTED_BUILD
    private DateTime _lastTickStartTime = DateTime.MinValue;
    private const double TickGapLogThresholdMs = 35.0;

    private string _lastDumpedLevelIds = "";
    private bool? _lastDumpedIsInRun;
    private int _lastDumpedRoomReached = int.MinValue;
    private int _lastDumpedLevelReachedForDiag = int.MinValue;
    private bool? _lastDumpedRunSuccessful;

    private string? _lastDumpedEliteName;
    private string? _lastDumpedHeraldName;

    private string? _lastDumpedCutsceneAssetName;
#endif

    private static readonly Dictionary<string, string> BiomeDisplayNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Boatyard"] = "Abandoned Temple",
        ["Void"] = "Royal Abyss",
    };

    private static readonly Dictionary<string, string> BiomeBaseBossNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Boatyard"] = "The Golemancer",
        ["Submarine"] = "Captain Gwaelod",
        ["Gardens"] = "General Kri'su",
        ["Sanctuary"] = "Highpriest Un'glu",
    };

    private const string TorakaIdentifierNormalized = "toraka";

    private string _latchedInternalBiome = "";
    private bool _wasBossRoomLastTick;

    private int _lastObservedLevelReached = int.MinValue;

    private bool _hasSeenLobbySinceAttach;
    private bool _currentDepthClassified;
    private string? _currentDepthBiomeName;
    private string? _currentDepthBossDisplayName;

    private string? _currentDepthEliteName;
    private string? _wasEliteNameLastTick;

    private bool _hasEnteredRoyalAbyss;

    private bool _currentDepthIsRoyalAbyssContent;
    private bool _wasRoyalAbyssContentLastTick;

    private string? _currentRoomHeraldName;
    private readonly List<string?> _currentDepthRoomHeraldNames = new();

    private bool _currentDepthIsTorakaContent;
    private bool _wasTorakaContentLastTick;

    private bool _hasDetectedTorakaSecretPhase;
    private const string TorakaTrueEndingTransitionCutscene = "LS_Toraka_Death_TransitionToSecretphase";
    private const string TorakaSecretPhaseDeathCutscene = "LS_Toraka_Secretphase_Death";

    public event PropertyChangedEventHandler? PropertyChanged;

    public string AppVersionText { get; internal set; } =
        $"v{System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "?"}";

    private bool _isUpdateAvailable;
    public bool IsUpdateAvailable
    {
        get => _isUpdateAvailable;
        internal set => SetField(ref _isUpdateAvailable, value);
    }

    private string? _latestVersionText;
    public string? LatestVersionText
    {
        get => _latestVersionText;
        internal set => SetField(ref _latestVersionText, value);
    }

    public async Task<bool?> CheckForUpdateAsync()
    {
        var latest = await UpdateChecker.GetLatestVersionAsync();
        if (latest is null)
            return null;

        var currentVersionText = AppVersionText.TrimStart('v', 'V');
        if (!Version.TryParse(latest, out var latestVersion) || !Version.TryParse(currentVersionText, out var currentVersion))
            return null;

        if (latestVersion > currentVersion)
        {
            LatestVersionText = latest;
            IsUpdateAvailable = true;
            return true;
        }

        return false;
    }

    private bool _isPossibleGameMismatch;
    public bool IsPossibleGameMismatch
    {
        get => _isPossibleGameMismatch;
        internal set => SetField(ref _isPossibleGameMismatch, value);
    }

    private string? _gameMismatchDetailText;
    public string? GameMismatchDetailText
    {
        get => _gameMismatchDetailText;
        internal set => SetField(ref _gameMismatchDetailText, value);
    }

    private bool _hasLoggedGameMismatchThisAttach;
    private int _consecutiveImplausibleTicks;

    private const int ImplausibleTicksThreshold = 3;

    private int _consecutivePlausibleTicksAfterMismatch;
    private bool _hasLoggedReadsValidAgainThisAttach;

    private const int PlausibleTicksBeforeLoggingRecovery = 50;

    private void ReportPossibleGameMismatch(string reason)
    {
        IsPossibleGameMismatch = true;
        GameMismatchDetailText = reason;

        if (!_hasLoggedGameMismatchThisAttach)
        {
            AppLog.Log($"[Possible mismatch] {reason}");
            _hasLoggedGameMismatchThisAttach = true;
        }
    }

    public TimerEngine(IGameSource? gameSource = null, string? dataDirectory = null, Func<DateTime>? utcNow = null)
    {
        _clock = utcNow ?? (() => DateTime.UtcNow);
        _tickUtc = _clock();
        OnNewPbSound = () => PbSound.Play(PbSoundPath, PbSoundVolume);
        _adjustedTimer = new AdjustedTimer(() => _tickUtc);
        _loadAndCutsceneFreeTimer = new AdjustedTimer(() => _tickUtc);
        var dataDir = dataDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AbyssusTimer");

        _gameSource = gameSource ?? new RecordingGameSource(
            new LiveGameSource(),
            GameRecorder.Open(Path.Combine(dataDir, "recordings"), AppVersionText, () => _tickUtc));

        var pbPath = Path.Combine(dataDir, "personal_bests.json");
        _personalBests = new PersonalBests(pbPath, DataKey(_activeCategory, _activePlayerCount));

        var historyPath = Path.Combine(dataDir, "run_history.json");
        _runHistory = new RunHistory(historyPath, DataKey(_activeCategory, _activePlayerCount));

        foreach (var key in _personalBests.CategoriesWithDroppedRunTime)
        {
            var restored = _runHistory.GetAttempts(key)
                .Where(a => a.Completed && PersonalBests.IsPossibleTime(a.FinalTime))
                .Select(a => (float?)a.FinalTime)
                .Min();
            _personalBests.SetBestRunTime(key, restored);
            AppLog.Log($"Removed an impossible PB (0:00 or less) for {key}; restored {(restored is { } t ? FormatTimer(t) : "none")} from History.");
        }

        var recoveryPath = Path.Combine(dataDir, "run_recovery.json");
        _runRecovery = new RunRecovery(recoveryPath);

        var recovered = _runRecovery.Load();
        if (recovered is not null)
        {
            _runHistory.SetActiveCategory($"{recovered.Category}|{recovered.PlayerCount}");
            _runHistory.RecordAttempt(new RunAttempt(recovered.Timestamp, Completed: false, recovered.FloorSegments.Values.Sum(), recovered.FloorSegments));
            _runHistory.SetActiveCategory(DataKey(_activeCategory, _activePlayerCount));
            _runRecovery.Clear();
            AppLog.Log($"Recovered an interrupted run — {recovered.Category}|{recovered.PlayerCount}, {recovered.FloorSegments.Count} floor(s).");
        }

        _session.RunStarted += () =>
        {
            AppLog.Log($"Run started — {_activeCategory}|{_activePlayerCount} weapon={_lastStats.WeaponAssetName ?? "(none yet)"}");
            _annotatedFloorSplits.Clear();
            _currentRunCompleted = false;
            _allWeaponLiveRunNumber = null;
            _defeatedBiomeBosses.Clear();
            PublishAllBossesProgress();
            _currentRunWeapon = _lastStats.WeaponAssetName;
            _currentRunIsAllWeapon = _activeCategory.IsAllWeapon();
            _currentRunAnyPercentKey = _currentRunIsAllWeapon && CountAllWeaponRunsAsAnyPercent
                ? DataKey(_activeCategory.SingleRunCategory(), _activePlayerCount)
                : null;

            if (!_currentRunIsAllWeapon || _currentRunAnyPercentKey is not null)
            {
                _currentRunRecovery = new RunRecoveryData
                {
                    Timestamp = DateTime.Now,
                    Category = (_currentRunIsAllWeapon ? _activeCategory.SingleRunCategory() : _activeCategory).ToString(),
                    PlayerCount = _activePlayerCount.ToString(),
                };
                _runRecovery.Save(_currentRunRecovery);
            }

            _lastObservedLevelReached = int.MinValue;
            _currentDepthClassified = false;
            _currentDepthBiomeName = null;
            _currentDepthBossDisplayName = null;
            _latchedInternalBiome = "";
            _wasBossRoomLastTick = false;
            _hasEnteredRoyalAbyss = false;
            _currentDepthIsRoyalAbyssContent = false;
            _wasRoyalAbyssContentLastTick = false;
            _currentDepthIsTorakaContent = false;
            _wasTorakaContentLastTick = false;
            _hasDetectedTorakaSecretPhase = false;
            _currentDepthEliteName = null;
            _wasEliteNameLastTick = null;
            _currentRoomHeraldName = null;
            _currentDepthRoomHeraldNames.Clear();
        };
        _session.RoomSplitOccurred += _ =>
        {
            _currentDepthRoomHeraldNames.Add(_currentRoomHeraldName);
            _currentRoomHeraldName = null;
        };
        _session.FloorSplitOccurred += split =>
        {
            if (_currentRunRecovery is not null)
            {
                _currentRunRecovery.FloorSegments[split.FloorNumber] = split.SegmentTime;
                _runRecovery.Save(_currentRunRecovery);
            }

            if (BiomeBaseBossNames.ContainsValue(split.BiomeName) && _defeatedBiomeBosses.Add(split.BiomeName))
            {
                AppLog.Log($"[All Bosses] {split.BiomeName} defeated ({_defeatedBiomeBosses.Count}/{BiomeBaseBossNames.Count}).");
                PublishAllBossesProgress();
            }

            var isNewBest = RecordFloorSegmentForCurrentRun(split.FloorNumber, split.SegmentTime);
            if (isNewBest)
                OnPropertyChanged(nameof(SumOfBestText));
            var roomHeraldNames = _currentDepthRoomHeraldNames.ToArray();
            _currentDepthRoomHeraldNames.Clear();
            _annotatedFloorSplits.Add((split, isNewBest, _wasBossRoomLastTick, _wasEliteNameLastTick, _wasRoyalAbyssContentLastTick, _wasTorakaContentLastTick, roomHeraldNames));

#if TRUSTED_BUILD
            DebugConsole.Log($"[MemDiag] FloorSplit #{split.FloorNumber} — annotatedFloorSplits={_annotatedFloorSplits.Count} SplitGroups={SplitGroups.Count} managedHeapMB={GC.GetTotalMemory(false) / (1024 * 1024)}");
#endif
        };
        _session.RunCompleted += _ =>
        {
            var isTrueEndingTransition = _currentDepthIsTorakaContent
                && string.Equals(_lastCutsceneAssetName, TorakaTrueEndingTransitionCutscene, StringComparison.OrdinalIgnoreCase);

            if (isTrueEndingTransition)
            {
                _hasDetectedTorakaSecretPhase = true;
            }
            else
            {
                _currentRunCompleted = true;
                OnAllWeaponRunCompleted(_loadAndCutsceneFreeTimer.AdjustedRunTime);
            }
        };
        _session.RunEnded += finalTime =>
        {
            AppLog.Log($"Run ended — {_activeCategory}|{_activePlayerCount} completed={_currentRunCompleted} finalTime={finalTime:F2}s weapon={_lastStats.WeaponAssetName ?? "(none)"}");

            _currentRunRecovery = null;
            _runRecovery.Clear();

            if (_currentRunCompleted && RecordRunTimeForCurrentRun(finalTime) && PlaySoundOnNewPb)
                OnNewPbSound();
            PreviousRunWasSuccessful = _currentRunCompleted;
            PreviousRunTime = finalTime;
            OnPropertyChanged(nameof(BestRunTimeText));
            OnPropertyChanged(nameof(HasBestRunTime));

            var sessionKey = _currentRunIsAllWeapon ? _currentRunAnyPercentKey : $"{_activeCategory}|{_activePlayerCount}";
            if (sessionKey is not null)
                CountSessionAttempt(sessionKey, _currentRunCompleted);

            var finalSegmentTime = finalTime - _session.LastFloorSplitTime;
            var finalBiomeName = _currentDepthBossDisplayName ?? _currentDepthBiomeName ?? "-";
            var finalIsNewBest = _currentRunCompleted && RecordFloorSegmentForCurrentRun(_lastStats.LevelReached, finalSegmentTime);
            if (finalIsNewBest)
                OnPropertyChanged(nameof(SumOfBestText));
            var finalSplit = new FloorSplit(_lastStats.LevelReached, finalSegmentTime, finalBiomeName, _session.CurrentFloorRooms);
            var finalRoomHeraldNames = _currentDepthRoomHeraldNames.Append(_currentRoomHeraldName).ToArray();
            _annotatedFloorSplits.Add((finalSplit, finalIsNewBest, _currentDepthBossDisplayName is not null, _currentDepthEliteName, _currentDepthIsRoyalAbyssContent, _currentDepthIsTorakaContent, finalRoomHeraldNames));

            var floorSegments = _annotatedFloorSplits
                .GroupBy(entry => entry.Split.FloorNumber)
                .ToDictionary(group => group.Key, group => group.Sum(entry => entry.Split.SegmentTime));
            RecordHistoryForCurrentRun(new RunAttempt(DateTime.Now, _currentRunCompleted, finalTime, floorSegments, _lastStats.RunTime, _adjustedTimer.AdjustedRunTime,
                Weapon: _currentRunWeapon, GameVersion: _gameSource.GameVersion, GameBuild: RecordedGameBuild));
            OnPropertyChanged(nameof(AllHistoricalRuns));
            NotifyStatsChanged();
            PruneRunHistoryIfNeeded();

            if (_allWeaponAttempt is { IsActive: true } && !_currentRunCompleted)
                EndAllWeaponAttemptEarly(finalTime, "the run ended without a kill");

            if (_currentRunIsAllWeapon)
                _forceSplitsRebuild = true;

            if (AutoResetOnLobbyReturn)
            {
                _adjustedTimer.Reset();
                _loadAndCutsceneFreeTimer.Reset();
            }
        };
    }

    private static string DataKey(RunCategory category, PlayerCount playerCount) =>
        category.IsAllWeapon()
            ? $"{category}Attempts|{playerCount}"
            : $"{category}|{playerCount}";

    private bool RecordFloorSegmentForCurrentRun(int floorNumber, float segmentTime) =>
        !_currentRunIsAllWeapon
            ? _personalBests.TryRecordFloorSegment(floorNumber, segmentTime)
            : _currentRunAnyPercentKey is { } key && _personalBests.TryRecordFloorSegment(key, floorNumber, segmentTime);

    private bool RecordRunTimeForCurrentRun(float runTime) =>
        !_currentRunIsAllWeapon
            ? _personalBests.TryRecordRunTime(runTime)
            : _currentRunAnyPercentKey is { } key && _personalBests.TryRecordRunTime(key, runTime);

    private void RecordHistoryForCurrentRun(RunAttempt attempt)
    {
        if (!_currentRunIsAllWeapon)
            _runHistory.RecordAttempt(attempt);
        else if (_currentRunAnyPercentKey is { } key)
            _runHistory.RecordAttempt(key, attempt);
    }

    private void CountSessionAttempt(string sessionKey, bool completed)
    {
        var sessionStats = _sessionStats.TryGetValue(sessionKey, out var existingStats) ? existingStats : (Attempts: 0, Completed: 0);
        sessionStats.Attempts++;
        if (completed)
            sessionStats.Completed++;
        _sessionStats[sessionKey] = sessionStats;
        OnPropertyChanged(nameof(SessionStatsText));
    }

    public bool CountAllWeaponRunsAsAnyPercent { get; set; }

    internal void RequestSplitsRebuildForTests() => _forceSplitsRebuild = true;

    internal AllWeaponAttempt? AllWeaponAttemptForTests => _allWeaponAttempt;

    public bool IsAllWeaponAttemptActive => _allWeaponAttempt?.IsActive == true;

    public bool CanChangeCategory => !IsInRun && !IsAllWeaponAttemptActive;

    public bool IsAllBossesView => _activeCategory == RunCategory.AllBosses;

    public string AllBossesProgressText { get => _allBossesProgressText; private set { SetField(ref _allBossesProgressText, value); OnPropertyChanged("OverlayDisplayAllBossesProgressText"); } }
    private string _allBossesProgressText = $"Biome bosses 0 / {BiomeBaseBossNames.Count}";

    public string? AllBossesRemainingText { get => _allBossesRemainingText; private set { SetField(ref _allBossesRemainingText, value); OnPropertyChanged("OverlayDisplayAllBossesRemainingText"); } }
    private string? _allBossesRemainingText = "Still needed: " + string.Join(", ", BiomeBaseBossNames.Values.Select(ShortBossName));

    public bool AllBossesAllDefeated { get => _allBossesAllDefeated; private set { SetField(ref _allBossesAllDefeated, value); OnPropertyChanged("OverlayDisplayAllBossesAllDefeated"); } }
    private bool _allBossesAllDefeated;

    private static string ShortBossName(string name) => name switch
    {
        "The Golemancer" => "Golemancer",
        "Captain Gwaelod" => "Gwaelod",
        "General Kri'su" => "Kri'su",
        "Highpriest Un'glu" => "Un'glu",
        _ => name,
    };

    private void PublishAllBossesProgress()
    {
        var remaining = BiomeBaseBossNames.Values.Where(name => !_defeatedBiomeBosses.Contains(name)).ToList();
        var allDefeated = remaining.Count == 0;
        var progress = allDefeated
            ? $"All {BiomeBaseBossNames.Count} biome bosses down — Royal Abyss next"
            : $"Biome bosses {_defeatedBiomeBosses.Count} / {BiomeBaseBossNames.Count}";
        var remainingText = allDefeated ? null : "Still needed: " + string.Join(", ", remaining.Select(ShortBossName));
        _uiDispatcher.BeginInvoke(() =>
        {
            AllBossesProgressText = progress;
            AllBossesRemainingText = remainingText;
            AllBossesAllDefeated = allDefeated;
        });
    }

    public bool IsAllWeaponView => _activeCategory.IsAllWeapon();

    public bool OverlaySplitsVisible => SplitDetailLevel != SplitDetailLevel.Total || IsAllWeaponView;

    public bool AllWeaponPreloadChecklist
    {
        get => _allWeaponPreloadChecklist;
        set
        {
            if (_allWeaponPreloadChecklist == value)
                return;
            _allWeaponPreloadChecklist = value;
            OnPropertyChanged(nameof(AllWeaponPreloadChecklist));
            if (IsPreviewingOverlay)
                OnPropertyChanged(nameof(OverlayDisplayWeaponGroups));
            _forceSplitsRebuild = true;
        }
    }
    private volatile bool _allWeaponPreloadChecklist;

    public ObservableCollection<WeaponGroupViewModel> WeaponGroups { get; } = new();

    public string AllWeaponTotalText { get => _allWeaponTotalText; private set { SetField(ref _allWeaponTotalText, value); OnPropertyChanged("OverlayDisplayAllWeaponTotalText"); } }
    private string _allWeaponTotalText = FormatTimer(0f);

    public string AllWeaponBetweenRunsText { get => _allWeaponBetweenRunsText; private set { SetField(ref _allWeaponBetweenRunsText, value); OnPropertyChanged("OverlayDisplayAllWeaponBetweenRunsText"); } }
    private string _allWeaponBetweenRunsText = FormatTimer(0f);

    public string AllWeaponProgressText { get => _allWeaponProgressText; private set { SetField(ref _allWeaponProgressText, value); OnPropertyChanged("OverlayDisplayAllWeaponProgressText"); } }
    private string _allWeaponProgressText = $"0 / {AllWeaponAttempt.RequiredWeapons} weapons";

    public string? AllWeaponCurrentWeaponName { get => _allWeaponCurrentWeaponName; private set => SetField(ref _allWeaponCurrentWeaponName, value); }
    private string? _allWeaponCurrentWeaponName;

    private (List<WeaponGroupViewModel>? Groups, string Total, string BetweenRuns, string Progress, string? CurrentWeapon) BuildAllWeaponDisplay(List<BiomeGroupViewModel>? biomeGroups)
    {
        var attempt = _allWeaponAttempt;
        var liveRunTime = _loadAndCutsceneFreeTimer.AdjustedRunTime;

        if (biomeGroups is not null && _allWeaponLiveRunNumber is { } live && attempt is not null)
        {
            while (_allWeaponRunGroups.Count <= live)
                _allWeaponRunGroups.Add(Array.Empty<BiomeGroupViewModel>());
            _allWeaponRunGroups[live] = biomeGroups;
        }

        var inRun = attempt?.State == AllWeaponAttemptState.InRun;
        var groups = biomeGroups is null ? null : AllWeaponSplits.Build(
            attempt?.Results ?? Array.Empty<WeaponRunResult>(),
            attempt?.CurrentWeaponAssetName,
            inRun,
            liveRunTime,
            _allWeaponRunGroups,
            AllWeaponPreloadChecklist,
            SplitDetailLevel,
            SplitListOverflowBehavior,
            FormatTimer);

        return (
            groups,
            FormatTimer(attempt?.Total(liveRunTime) ?? 0f),
            FormatTimer(attempt?.BetweenRunsTime ?? 0f),
            $"{attempt?.CompletedWeapons.Count ?? 0} / {AllWeaponAttempt.RequiredWeapons} weapons",
            inRun ? AllWeaponSplits.DisplayName(attempt!.CurrentWeaponAssetName) : null);
    }

    private void OnAllWeaponRunTimerStarted(string? weaponAssetName)
    {
        if (_allWeaponAttempt is not { IsActive: true })
        {
            _allWeaponAttempt = new AllWeaponAttempt();
            _allWeaponAttempt.Tick(_tickUtc, excludeFromTime: true);
            _allWeaponRunGroups.Clear();
            AppLog.Log($"All Weapon% attempt started — {_activePlayerCount}");
            OnPropertyChanged(nameof(IsAllWeaponAttemptActive));
            OnPropertyChanged(nameof(CanChangeCategory));
        }

        _allWeaponLiveRunNumber = _allWeaponAttempt.Results.Count;
        _allWeaponAttempt.RunTimerStarted(weaponAssetName);
    }

    private void OnAllWeaponRunCompleted(float runTime)
    {
        if (_allWeaponAttempt is not { State: AllWeaponAttemptState.InRun } attempt)
            return;

        var weapon = attempt.CurrentWeaponAssetName;

        var wrongEnding = _activeCategory == RunCategory.AllWeaponPercentTrueEnding && !_hasDetectedTorakaSecretPhase;
        if (wrongEnding)
            _currentRunAnyPercentKey = null;

        attempt.RunCompleted(runTime, wrongEnding);
        _forceSplitsRebuild = true;
        AppLog.Log($"All Weapon% run completed — weapon={weapon ?? "(unknown)"} time={runTime:F2}s weapons={attempt.CompletedWeapons.Count}/{AllWeaponAttempt.RequiredWeapons}{(wrongEnding ? " (normal ending — doesn't count for True Ending)" : "")}");

        if (!wrongEnding && Weapons.FromAssetName(weapon) is not null)
            _personalBests.TryRecordWeaponTime(weapon!, runTime);

        if (attempt.IsFinished)
            RecordAllWeaponAttempt(attempt);
    }

    private void EndAllWeaponAttemptEarly(float runTimeSoFar, string reason)
    {
        if (_allWeaponAttempt is not { IsActive: true } attempt)
            return;

        if (attempt.State == AllWeaponAttemptState.InRun)
            attempt.RunEndedIncomplete(runTimeSoFar);
        else
            attempt.Abandon();

        AppLog.Log($"All Weapon% attempt ended early — {reason}");
        RecordAllWeaponAttempt(attempt);
    }

    private void RecordAllWeaponAttempt(AllWeaponAttempt attempt)
    {
        var completed = attempt.State == AllWeaponAttemptState.Completed;
        var total = attempt.Total();
        AppLog.Log($"All Weapon% attempt recorded — completed={completed} total={total:F2}s weapons={attempt.CompletedWeapons.Count}/{AllWeaponAttempt.RequiredWeapons} runs={attempt.Results.Count} betweenRuns={attempt.BetweenRunsTime:F2}s");

        _runHistory.RecordAttempt(new RunAttempt(
            DateTime.Now,
            completed,
            total,
            new Dictionary<int, float>(),
            WeaponRuns: attempt.Results.ToList(),
            BetweenRunsTime: attempt.BetweenRunsTime,
            GameVersion: _gameSource.GameVersion,
            GameBuild: RecordedGameBuild));

        if (completed && _personalBests.TryRecordRunTime(total) && PlaySoundOnNewPb)
            OnNewPbSound();

        CountSessionAttempt($"{_activeCategory}|{_activePlayerCount}", completed);
        OnPropertyChanged(nameof(IsAllWeaponAttemptActive));
        OnPropertyChanged(nameof(CanChangeCategory));
        OnPropertyChanged(nameof(BestRunTimeText));
        OnPropertyChanged(nameof(HasBestRunTime));
        OnPropertyChanged(nameof(AllHistoricalRuns));
        NotifyStatsChanged();
        PruneRunHistoryIfNeeded();
    }

    public bool PauseStopsTime { get; set; }
    public bool AutoResetOnLobbyReturn { get; set; } = true;

    public bool PlaySoundOnNewPb { get; set; } = true;

    public string? PbSoundPath
    {
        get => _pbSoundPath;
        set
        {
            SetField(ref _pbSoundPath, string.IsNullOrWhiteSpace(value) ? null : value);
            OnPropertyChanged(nameof(PbSoundName));
        }
    }
    private string? _pbSoundPath;

    public string PbSoundName => _pbSoundPath is { } path ? Path.GetFileName(path) : "Windows default sound";

    public double PbSoundVolume
    {
        get => _pbSoundVolume;
        set => SetField(ref _pbSoundVolume, double.IsFinite(value) ? Math.Clamp(value, 0, 1) : 0.5);
    }
    private double _pbSoundVolume = 0.5;

    public void TestPbSound() => PbSound.Play(PbSoundPath, PbSoundVolume);

    internal Action OnNewPbSound { get; set; }

    public bool ShowRawTimer { get => _showRawTimer; set => SetField(ref _showRawTimer, value); }
    private bool _showRawTimer = true;
    public bool ShowLoadFreeTimer { get => _showLoadFreeTimer; set => SetField(ref _showLoadFreeTimer, value); }
    private bool _showLoadFreeTimer = true;
    public bool ShowLoadCutsceneFreeTimer { get => _showLoadCutsceneFreeTimer; set => SetField(ref _showLoadCutsceneFreeTimer, value); }
    private bool _showLoadCutsceneFreeTimer = true;
    public bool ShowPreviousSegment { get => _showPreviousSegment; set => SetField(ref _showPreviousSegment, value); }
    private bool _showPreviousSegment = true;

    public bool RunAtStartup
    {
        get => _runAtStartup;
        set
        {
            if (_runAtStartup == value)
                return;
            _runAtStartup = value;
            OnPropertyChanged(nameof(RunAtStartup));
            try
            {
                StartupRegistration.SetEnabled(value);
            }
            catch (Exception ex)
            {
                AppLog.LogException("Failed to update run-at-startup registration", ex);
            }
        }
    }
    private bool _runAtStartup;

    public PrimaryTimerKind SelectedPrimaryTimer
    {
        get => _selectedPrimaryTimer;
        set
        {
            if (IsInRun || _selectedPrimaryTimer == value)
                return;

            _selectedPrimaryTimer = value;
            OnPropertyChanged(nameof(SelectedPrimaryTimer));
            OnPropertyChanged(nameof(PrimaryTimeText));
            OnPropertyChanged(nameof(OverlayDisplayPrimaryTimeText));
        }
    }
    private volatile PrimaryTimerKind _selectedPrimaryTimer = PrimaryTimerKind.LoadCutsceneFree;

    public RunCategory ActiveCategory
    {
        get => _activeCategory;
        set
        {
            if (IsInRun || IsAllWeaponAttemptActive || _activeCategory == value)
                return;

            _activeCategory = value;
            UpdatePersonalBestsCategory();
            OnPropertyChanged(nameof(ActiveCategory));
            OnPropertyChanged(nameof(IsAllWeaponView));
            OnPropertyChanged(nameof(IsAllBossesView));
            OnPropertyChanged(nameof(OverlaySplitsVisible));
            OnPropertyChanged(nameof(OverlayDisplayPrimaryTimeText));
            OnPropertyChanged(nameof(OverlayDisplayBiomeName));
            _forceSplitsRebuild = true;
            OnPropertyChanged(nameof(BestRunTimeText));
            OnPropertyChanged(nameof(HasBestRunTime));
            OnPropertyChanged(nameof(SumOfBestText));
        }
    }
    private RunCategory _activeCategory = RunCategory.AnyPercent;

    public PlayerCount ActivePlayerCount
    {
        get => _activePlayerCount;
        set
        {
            if (IsInRun || IsAllWeaponAttemptActive || _activePlayerCount == value)
                return;

            _activePlayerCount = value;
            UpdatePersonalBestsCategory();
            OnPropertyChanged(nameof(ActivePlayerCount));
            OnPropertyChanged(nameof(BestRunTimeText));
            OnPropertyChanged(nameof(HasBestRunTime));
            OnPropertyChanged(nameof(SumOfBestText));
        }
    }
    private PlayerCount _activePlayerCount = PlayerCount.Solo;

    public ComparisonSource ComparisonSource
    {
        get => _comparisonSource;
        set
        {
            if (_comparisonSource == value)
                return;
            _comparisonSource = value;
            OnPropertyChanged(nameof(ComparisonSource));
            OnPropertyChanged(nameof(ActiveSpecificRunIndex));
        }
    }
    private ComparisonSource _comparisonSource = ComparisonSource.Best;

    public IReadOnlyList<ComparisonSource> AllComparisonSources { get; } = Enum.GetValues<ComparisonSource>();

    public SplitDetailLevel SplitDetailLevel
    {
        get => _splitDetailLevel;
        set
        {
            if (IsInRun || _splitDetailLevel == value)
                return;
            _splitDetailLevel = value;
            OnPropertyChanged(nameof(SplitDetailLevel));
            OnPropertyChanged(nameof(OverlaySplitsVisible));
            _forceSplitsRebuild = true;
            if (IsPreviewingOverlay)
            {
                OnPropertyChanged(nameof(OverlayDisplaySplitGroups));
                OnPropertyChanged(nameof(OverlayDisplayWeaponGroups));
            }
        }
    }
    private SplitDetailLevel _splitDetailLevel = SplitDetailLevel.PerDepth;

    public IReadOnlyList<SplitDetailLevel> AllSplitDetailLevels { get; } = Enum.GetValues<SplitDetailLevel>();

    public SplitListOverflowBehavior SplitListOverflowBehavior
    {
        get => _splitListOverflowBehavior;
        set
        {
            if (IsInRun || _splitListOverflowBehavior == value)
                return;
            _splitListOverflowBehavior = value;
            OnPropertyChanged(nameof(SplitListOverflowBehavior));
            _forceSplitsRebuild = true;
            if (IsPreviewingOverlay)
            {
                OnPropertyChanged(nameof(OverlayDisplaySplitGroups));
                OnPropertyChanged(nameof(OverlayDisplayWeaponGroups));
            }
        }
    }
    private SplitListOverflowBehavior _splitListOverflowBehavior = SplitListOverflowBehavior.FullList;

    public IReadOnlyList<SplitListOverflowBehavior> AllSplitListOverflowBehaviors { get; } = Enum.GetValues<SplitListOverflowBehavior>();

    public double OverlayOpacity { get => _overlayOpacity; set => SetField(ref _overlayOpacity, value); }
    private double _overlayOpacity = OverlayAppearanceDefaults.Opacity;

    public double OverlayScale { get => _overlayScale; set => SetField(ref _overlayScale, value); }
    private double _overlayScale = OverlayAppearanceDefaults.Scale;

    public string? OverlayBackgroundImagePath { get => _overlayBackgroundImagePath; set => SetField(ref _overlayBackgroundImagePath, value); }
    private string? _overlayBackgroundImagePath;

    public double OverlayBiomeFontSize { get => _overlayBiomeFontSize; set => SetField(ref _overlayBiomeFontSize, value); }
    private double _overlayBiomeFontSize = OverlayAppearanceDefaults.BiomeFontSize;

    public double OverlayDepthFontSize { get => _overlayDepthFontSize; set => SetField(ref _overlayDepthFontSize, value); }
    private double _overlayDepthFontSize = OverlayAppearanceDefaults.DepthFontSize;

    public double OverlayFloorFontSize { get => _overlayFloorFontSize; set => SetField(ref _overlayFloorFontSize, value); }
    private double _overlayFloorFontSize = OverlayAppearanceDefaults.FloorFontSize;

    public bool OverlayTimesHaveBackground { get => _overlayTimesHaveBackground; set => SetField(ref _overlayTimesHaveBackground, value); }
    private bool _overlayTimesHaveBackground = OverlayAppearanceDefaults.TimesHaveBackground;

    public string OverlayBiomeTextColor { get => _overlayBiomeTextColor; set => SetField(ref _overlayBiomeTextColor, value); }
    private string _overlayBiomeTextColor = OverlayAppearanceDefaults.BiomeTextColor;

    public string OverlayDepthTextColor { get => _overlayDepthTextColor; set => SetField(ref _overlayDepthTextColor, value); }
    private string _overlayDepthTextColor = OverlayAppearanceDefaults.DepthTextColor;

    public string OverlayFloorTextColor { get => _overlayFloorTextColor; set => SetField(ref _overlayFloorTextColor, value); }
    private string _overlayFloorTextColor = OverlayAppearanceDefaults.FloorTextColor;

    public double OverlayWeaponFontSize { get => _overlayWeaponFontSize; set => SetField(ref _overlayWeaponFontSize, value); }
    private double _overlayWeaponFontSize = OverlayAppearanceDefaults.WeaponFontSize;

    public string OverlayWeaponTextColor { get => _overlayWeaponTextColor; set => SetField(ref _overlayWeaponTextColor, value); }
    private string _overlayWeaponTextColor = OverlayAppearanceDefaults.WeaponTextColor;

    public double OverlayStatusFontSize { get => _overlayStatusFontSize; set => SetField(ref _overlayStatusFontSize, value); }
    private double _overlayStatusFontSize = OverlayAppearanceDefaults.StatusFontSize;

    public string OverlayStatusTextColor { get => _overlayStatusTextColor; set => SetField(ref _overlayStatusTextColor, value); }
    private string _overlayStatusTextColor = OverlayAppearanceDefaults.StatusTextColor;

    public double OverlayTitleFontSize { get => _overlayTitleFontSize; set => SetField(ref _overlayTitleFontSize, value); }
    private double _overlayTitleFontSize = OverlayAppearanceDefaults.TitleFontSize;

    public string OverlayTitleTextColor { get => _overlayTitleTextColor; set => SetField(ref _overlayTitleTextColor, value); }
    private string _overlayTitleTextColor = OverlayAppearanceDefaults.TitleTextColor;

    public bool IsPreviewingOverlay
    {
        get => _isPreviewingOverlay;
        set
        {
            if (_isPreviewingOverlay == value)
                return;
            _isPreviewingOverlay = value;
            OnPropertyChanged(nameof(IsPreviewingOverlay));
            foreach (var name in OverlayDisplayProperties)
                OnPropertyChanged(name);
        }
    }
    private bool _isPreviewingOverlay;

    public IReadOnlyList<BiomeGroupViewModel> OverlayDisplaySplitGroups =>
        IsPreviewingOverlay ? BuildPreviewSplitGroups() : SplitGroups;

    public string OverlayDisplayBiomeName =>
        IsPreviewingOverlay ? "Sunken Gardens"
        : IsAllWeaponView && AllWeaponCurrentWeaponName is { } weapon ? $"{weapon} · {CurrentBiomeName}"
        : !IsInRun && IsAttached ? "Lobby"
        : CurrentBiomeName;

    public IReadOnlyList<WeaponGroupViewModel> OverlayDisplayWeaponGroups => IsPreviewingOverlay ? BuildPreviewWeaponGroups() : WeaponGroups;
    public string OverlayDisplayAllWeaponBetweenRunsText => IsPreviewingOverlay ? "0:42.10" : AllWeaponBetweenRunsText;
    public string OverlayDisplayAllWeaponProgressText => IsPreviewingOverlay ? $"1 / {AllWeaponAttempt.RequiredWeapons} weapons" : AllWeaponProgressText;
    public string OverlayDisplayAllBossesProgressText => IsPreviewingOverlay ? $"Biome bosses 2 / {BiomeBaseBossNames.Count}" : AllBossesProgressText;
    public string? OverlayDisplayAllBossesRemainingText => IsPreviewingOverlay ? "Still needed: Gwaelod, Un'glu" : AllBossesRemainingText;
    public bool OverlayDisplayAllBossesAllDefeated => !IsPreviewingOverlay && AllBossesAllDefeated;

    private List<WeaponGroupViewModel> BuildPreviewWeaponGroups() =>
        AllWeaponSplits.Build(
            new[] { new WeaponRunResult("PA_CombatBow", 441.3f, Completed: true, CountsTowardTotal: true) },
            "PA_Shotgun",
            isInRun: true,
            currentRunTime: 522.15f,
            new IReadOnlyList<BiomeGroupViewModel>[] { Array.Empty<BiomeGroupViewModel>(), BuildPreviewSplitGroups() },
            AllWeaponPreloadChecklist,
            SplitDetailLevel,
            SplitListOverflowBehavior,
            FormatTimer);

    private static readonly string[] OverlayDisplayProperties =
    {
        nameof(OverlayDisplaySplitGroups), nameof(OverlayDisplayBiomeName), nameof(OverlayDisplayPrimaryTimeText),
        nameof(OverlayDisplayWeaponGroups), nameof(OverlayDisplayAllWeaponBetweenRunsText), nameof(OverlayDisplayAllWeaponProgressText),
        nameof(OverlayDisplayAllBossesProgressText), nameof(OverlayDisplayAllBossesRemainingText), nameof(OverlayDisplayAllBossesAllDefeated),
    };

    public string OverlayDisplayPrimaryTimeText =>
        IsPreviewingOverlay ? "8:42.15"
        : IsAllWeaponView ? AllWeaponTotalText
        : PrimaryTimeText;

    private List<BiomeGroupViewModel> BuildPreviewSplitGroups()
    {
        bool ShowRooms(bool isInProgress) =>
            SplitDetailLevel == SplitDetailLevel.PerRoom
            && (SplitListOverflowBehavior != SplitListOverflowBehavior.Collapse || isInProgress);

        var depths = new List<DepthRowViewModel>
        {
            new("Depth 1", "1:52.10", "", new List<RoomRowViewModel>
            {
                new(1, "Room 1", "0:24.10"),
                new(2, "Room 2", "0:31.55"),
                new(3, "Room 3", "0:28.90"),
                new(4, "Room 4", "0:27.55"),
            }, "-0:01.84", DeltaSeverity.Ahead, ShowRoomsInOverlay: ShowRooms(isInProgress: false)),
            new("Depth 2", "2:15.67", "", new List<RoomRowViewModel>
            {
                new(1, "Room 1", "0:33.20"),
                new(2, "Room 2", "0:35.80"),
                new(3, "Room 3", "0:30.12"),
                new(4, "Room 4", "0:36.55"),
            }, "+0:03.21", DeltaSeverity.Behind, ShowRoomsInOverlay: ShowRooms(isInProgress: false)),
            new("Depth 3", "2:39.55", "", new List<RoomRowViewModel>
            {
                new(1, "Room 1", "0:38.44"),
                new(2, "Room 2", "0:41.10"),
                new(3, "Room 3", "0:35.01"),
                new(4, "Room 4", "0:45.00"),
            }, null, null, ShowRoomsInOverlay: ShowRooms(isInProgress: true)),
        };

        return new List<BiomeGroupViewModel>
        {
            new("Sunken Gardens", "6:47.32", depths, ShowDepthsInOverlay: true),
        };
    }

    public void ResetOverlayAppearanceToDefaults()
    {
        OverlayOpacity = OverlayAppearanceDefaults.Opacity;
        OverlayScale = OverlayAppearanceDefaults.Scale;
        OverlayBackgroundImagePath = null;
        OverlayBiomeFontSize = OverlayAppearanceDefaults.BiomeFontSize;
        OverlayDepthFontSize = OverlayAppearanceDefaults.DepthFontSize;
        OverlayFloorFontSize = OverlayAppearanceDefaults.FloorFontSize;
        OverlayTimesHaveBackground = OverlayAppearanceDefaults.TimesHaveBackground;
        OverlayBiomeTextColor = OverlayAppearanceDefaults.BiomeTextColor;
        OverlayDepthTextColor = OverlayAppearanceDefaults.DepthTextColor;
        OverlayFloorTextColor = OverlayAppearanceDefaults.FloorTextColor;
        OverlayTitleFontSize = OverlayAppearanceDefaults.TitleFontSize;
        OverlayTitleTextColor = OverlayAppearanceDefaults.TitleTextColor;
        OverlayWeaponFontSize = OverlayAppearanceDefaults.WeaponFontSize;
        OverlayWeaponTextColor = OverlayAppearanceDefaults.WeaponTextColor;
        OverlayStatusFontSize = OverlayAppearanceDefaults.StatusFontSize;
        OverlayStatusTextColor = OverlayAppearanceDefaults.StatusTextColor;
    }

    private void UpdatePersonalBestsCategory()
    {
        var key = DataKey(_activeCategory, _activePlayerCount);
        _personalBests.SetActiveCategory(key);
        _runHistory.SetActiveCategory(key);
        OnPropertyChanged(nameof(SessionStatsText));

        _selectedHistoricalRunIndex = null;
        OnPropertyChanged(nameof(SelectedHistoricalRunIndex));
        OnPropertyChanged(nameof(ActiveSpecificRunIndex));
        OnPropertyChanged(nameof(AllHistoricalRuns));
        NotifyStatsChanged();

        _selectedRunHistoryDetailIndex = null;
        OnPropertyChanged(nameof(SelectedRunHistoryDetailIndex));
        OnPropertyChanged(nameof(SelectedRunHistoryDetailSummary));
        OnPropertyChanged(nameof(SelectedRunHistoryDetailInfo));
        OnPropertyChanged(nameof(SelectedRunHistoryDetailRows));
    }

    private void NotifyStatsChanged()
    {
        OnPropertyChanged(nameof(StatsPerLocationRows));
        OnPropertyChanged(nameof(StatsPbProgressionRows));
        OnPropertyChanged(nameof(StatsPbProgressionChartPoints));
        OnPropertyChanged(nameof(StatsPbProgressionChartMarkers));
        OnPropertyChanged(nameof(StatsPbProgressionChartMaxTimeText));
        OnPropertyChanged(nameof(StatsPbProgressionChartMinTimeText));
        OnPropertyChanged(nameof(StatsAttemptsReachedRows));
        OnPropertyChanged(nameof(StatsLoadCutsceneOverheadText));
    }

    public IReadOnlyList<RunCategory> AllCategories { get; } = Enum.GetValues<RunCategory>();
    public IReadOnlyList<PlayerCount> AllPlayerCounts { get; } = Enum.GetValues<PlayerCount>();

    public IReadOnlyList<HistoricalRunOption> AllHistoricalRuns => _runHistory.GetAttempts()
        .Select((attempt, index) =>
        {
            var time = FormatTimer(attempt.FinalTime);
            var result = $"{(attempt.Completed ? "Completed" : "Incomplete")} — {time}";
            return new HistoricalRunOption(index,
                $"{attempt.Timestamp:g} — {result}",
                $"{attempt.Timestamp:g}",
                result,
                $"{time} — {attempt.Timestamp:MMM d}, {attempt.Timestamp:t}{(attempt.Completed ? "" : " · incomplete")}");
        })
        .ToList();

    public IReadOnlyList<LocationStatsRow> StatsPerLocationRows
    {
        get
        {
            var byFloor = new Dictionary<int, List<float>>();
            foreach (var attempt in _runHistory.GetAttempts())
            {
                if (attempt.FloorSegments.Count == 0)
                    continue;

                var abandonedFloor = attempt.Completed ? (int?)null : attempt.FloorSegments.Keys.Max();
                foreach (var (floor, time) in attempt.FloorSegments)
                {
                    if (floor == abandonedFloor)
                        continue;
                    if (!byFloor.TryGetValue(floor, out var list))
                        byFloor[floor] = list = new List<float>();
                    list.Add(time);
                }
            }

            return byFloor.OrderBy(kv => kv.Key).Select(kv =>
            {
                var times = kv.Value;
                var average = times.Average();
                var variance = times.Select(t => (t - average) * (t - average)).Average();
                var stdDev = MathF.Sqrt(variance);
                return new LocationStatsRow(
                    $"Depth {DisplayDepthNumber(kv.Key)}",
                    times.Count,
                    FormatTimer(times.Min()),
                    FormatTimer(average),
                    FormatTimer(times.Max()),
                    FormatTimer(stdDev));
            }).ToList();
        }
    }

    public IReadOnlyList<PbMilestoneRow> StatsPbProgressionRows
    {
        get
        {
            var rows = new List<PbMilestoneRow>();
            float? runningBest = null;
            foreach (var attempt in _runHistory.GetAttempts().Where(a => a.Completed).OrderBy(a => a.Timestamp))
            {
                if (runningBest is { } best && attempt.FinalTime >= best)
                    continue;

                var improvementText = runningBest is { } previousBest ? $"-{FormatTimer(previousBest - attempt.FinalTime)}" : null;
                rows.Add(new PbMilestoneRow(attempt.Timestamp.ToString("g"), FormatTimer(attempt.FinalTime), improvementText, attempt.FinalTime));
                runningBest = attempt.FinalTime;
            }

            return rows;
        }
    }

    private const double PbChartWidth = 300;
    private const double PbChartHeight = 60;

    private List<PbChartMarker> ComputePbChartMarkers()
    {
        var runs = PbChartRuns();
        var markers = new List<PbChartMarker>();
        if (runs.Count < 2)
            return markers;

        var min = runs.Min(r => r.Attempt.FinalTime);
        var max = runs.Max(r => r.Attempt.FinalTime);
        var range = max - min;

        for (var i = 0; i < runs.Count; i++)
        {
            var x = PbChartWidth * i / (runs.Count - 1);
            var t = range > 0 ? (runs[i].Attempt.FinalTime - min) / range : 0.5;
            var y = PbChartHeight * (1 - t);
            markers.Add(new PbChartMarker(x, y, runs[i].Attempt.Timestamp.ToString("g"), FormatTimer(runs[i].Attempt.FinalTime), runs[i].IsPb));
        }

        return markers;
    }

    private List<(RunAttempt Attempt, bool IsPb)> PbChartRuns()
    {
        var runs = new List<(RunAttempt Attempt, bool IsPb)>();
        float? runningBest = null;
        foreach (var attempt in _runHistory.GetAttempts().Where(a => a.Completed).OrderBy(a => a.Timestamp))
        {
            var isPb = runningBest is not { } best || attempt.FinalTime < best;
            if (isPb)
                runningBest = attempt.FinalTime;
            runs.Add((attempt, isPb));
        }

        return runs.Count <= PbChartRunCount ? runs : runs.GetRange(runs.Count - PbChartRunCount, PbChartRunCount);
    }

    public int PbChartRunCount
    {
        get => _pbChartRunCount;
        set
        {
            if (_pbChartRunCount == value || !AllPbChartRunCounts.Contains(value))
                return;
            _pbChartRunCount = value;
            OnPropertyChanged(nameof(PbChartRunCount));
            NotifyStatsChanged();
        }
    }
    private int _pbChartRunCount = 25;

    public IReadOnlyList<int> AllPbChartRunCounts { get; } = [10, 25, 50, 100];

    public System.Windows.Media.PointCollection StatsPbProgressionChartPoints
    {
        get
        {
            var points = new System.Windows.Media.PointCollection();
            foreach (var marker in ComputePbChartMarkers())
                points.Add(new System.Windows.Point(marker.X, marker.Y));
            return points;
        }
    }

    public IReadOnlyList<PbChartMarker> StatsPbProgressionChartMarkers => ComputePbChartMarkers();

    public string StatsPbProgressionChartMaxTimeText => PbChartRuns() is { Count: > 0 } runs ? FormatTimer(runs.Max(r => r.Attempt.FinalTime)) : FormatTimer(0f);
    public string StatsPbProgressionChartMinTimeText => PbChartRuns() is { Count: > 0 } runs ? FormatTimer(runs.Min(r => r.Attempt.FinalTime)) : FormatTimer(0f);

    public IReadOnlyList<AttemptsReachedRow> StatsAttemptsReachedRows
    {
        get
        {
            var byFloor = new Dictionary<int, (int Total, int Completed)>();
            foreach (var attempt in _runHistory.GetAttempts())
            {
                if (attempt.FloorSegments.Count == 0)
                    continue;

                var lastFloor = attempt.FloorSegments.Keys.Max();
                var (total, completed) = byFloor.TryGetValue(lastFloor, out var existing) ? existing : (0, 0);
                byFloor[lastFloor] = (total + 1, completed + (attempt.Completed ? 1 : 0));
            }

            return byFloor.OrderBy(kv => kv.Key)
                .Select(kv => new AttemptsReachedRow($"Depth {DisplayDepthNumber(kv.Key)}", kv.Value.Total, kv.Value.Completed))
                .ToList();
        }
    }

    public string StatsLoadCutsceneOverheadText
    {
        get
        {
            var withData = _runHistory.GetAttempts().Where(a => a.RawFinalTime is not null).ToList();
            if (withData.Count == 0)
                return "No data yet — only attempts recorded from now on include this breakdown.";

            var totalOverhead = withData.Sum(a => a.RawFinalTime!.Value - a.FinalTime);
            var averageOverhead = totalOverhead / withData.Count;
            return $"{FormatTimer(totalOverhead)} total across {withData.Count} attempt{(withData.Count == 1 ? "" : "s")} — {FormatTimer(averageOverhead)} average per attempt.";
        }
    }

    public int? SelectedHistoricalRunIndex
    {
        get => _selectedHistoricalRunIndex;
        set
        {
            if (_selectedHistoricalRunIndex == value)
                return;
            _selectedHistoricalRunIndex = value;
            OnPropertyChanged(nameof(SelectedHistoricalRunIndex));
            OnPropertyChanged(nameof(ActiveSpecificRunIndex));
        }
    }
    private int? _selectedHistoricalRunIndex;

    public int? ActiveSpecificRunIndex => ComparisonSource == ComparisonSource.SpecificRun ? SelectedHistoricalRunIndex : null;

    private float? GetSelectedHistoricalRunFloorSegment(int floorNumber)
    {
        if (_selectedHistoricalRunIndex is not { } index)
            return null;

        var attempts = _runHistory.GetAttempts();
        if (index < 0 || index >= attempts.Count)
            return null;

        return attempts[index].FloorSegments.TryGetValue(floorNumber, out var value) ? value : null;
    }

    public void SetSpecificRunComparisonSource(int index)
    {
        SelectedHistoricalRunIndex = index;
        ComparisonSource = ComparisonSource.SpecificRun;
    }

    public string ExportRunnerName { get => _exportRunnerName; set => SetField(ref _exportRunnerName, value); }
    private string _exportRunnerName = "";

    public string BuildExportFileName(int historicalRunIndex, string runnerName)
    {
        var attempts = _runHistory.GetAttempts();
        var timeText = historicalRunIndex >= 0 && historicalRunIndex < attempts.Count
            ? FormatTimer(attempts[historicalRunIndex].FinalTime).Replace(':', '_').Replace('.', '_')
            : "run";
        var safeRunnerName = string.IsNullOrWhiteSpace(runnerName) ? "Runner" : runnerName;
        var raw = $"{safeRunnerName}_{ActiveCategory}_{ActivePlayerCount}_{timeText}{SharedSplitFile.FileExtension}";
        foreach (var invalidChar in Path.GetInvalidFileNameChars())
            raw = raw.Replace(invalidChar, '_');
        return raw;
    }

    public void ExportSplitFile(int historicalRunIndex, string runnerName, string filePath)
    {
        var attempts = _runHistory.GetAttempts();
        if (historicalRunIndex < 0 || historicalRunIndex >= attempts.Count)
            throw new ArgumentOutOfRangeException(nameof(historicalRunIndex));

        var attempt = attempts[historicalRunIndex];
        var file = new SharedSplitFile(
            SharedSplitFile.CurrentSchemaVersion,
            string.IsNullOrWhiteSpace(runnerName) ? "Anonymous" : runnerName.Trim(),
            ActiveCategory.ToString(),
            ActivePlayerCount.ToString(),
            attempt.Timestamp,
            attempt.Completed,
            attempt.FinalTime,
            new Dictionary<int, float>(attempt.FloorSegments),
            AppVersionText,
            attempt.RawFinalTime,
            attempt.LoadFreeFinalTime,
            attempt.Weapon,
            attempt.GameVersion,
            attempt.GameBuild,
            attempt.WeaponRuns,
            attempt.BetweenRunsTime);

        File.WriteAllText(filePath, System.Text.Json.JsonSerializer.Serialize(file, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    }

    public string BuildSplitsSummaryText(int historicalRunIndex)
    {
        var attempts = _runHistory.GetAttempts();
        if (historicalRunIndex < 0 || historicalRunIndex >= attempts.Count)
            throw new ArgumentOutOfRangeException(nameof(historicalRunIndex));

        var attempt = attempts[historicalRunIndex];
        var lines = new List<string>
        {
            $"Abyssplit {AppVersionText} — {ActiveCategory.DisplayName()} {ActivePlayerCount} — {FormatTimer(attempt.FinalTime)} ({(attempt.Completed ? "Completed" : "Incomplete")})",
            $"{attempt.Timestamp:g}",
        };
        lines.AddRange(SubmissionDetails(attempt));
        lines.AddRange(DetailRows(attempt).Select(row => $"{row.Label}: {row.TimeText}"));

        return string.Join(Environment.NewLine, lines);
    }

    private SharedSplitFile? _importedSplitFile;

    public bool HasImportedSplitFile => _importedSplitFile is not null;

    public string? ImportedSplitDisplayText => _importedSplitFile is not { } file
        ? null
        : $"{file.RunnerName}'s {file.Category} {file.PlayerCount} — {FormatTimer(file.FinalTime)} ({(file.Completed ? "completed" : "incomplete")})";

    public void ImportSplitFile(string filePath)
    {
        var json = File.ReadAllText(filePath);
        var file = System.Text.Json.JsonSerializer.Deserialize<SharedSplitFile>(json)
            ?? throw new InvalidDataException("File did not contain a valid split file.");
        if (string.IsNullOrWhiteSpace(file.RunnerName) || file.FloorSegments is null)
            throw new InvalidDataException("File is missing required split data.");

        _importedSplitFile = file;
        OnPropertyChanged(nameof(HasImportedSplitFile));
        OnPropertyChanged(nameof(ImportedSplitDisplayText));
        ComparisonSource = ComparisonSource.ImportedFile;
    }

    public void ClearImportedSplitFile()
    {
        _importedSplitFile = null;
        OnPropertyChanged(nameof(HasImportedSplitFile));
        OnPropertyChanged(nameof(ImportedSplitDisplayText));
        if (ComparisonSource == ComparisonSource.ImportedFile)
            ComparisonSource = ComparisonSource.Best;
    }

    public bool IsHistoricalRunPersonalBest(int index)
    {
        var attempts = _runHistory.GetAttempts();
        if (index < 0 || index >= attempts.Count)
            return false;

        var attempt = attempts[index];
        return attempt.Completed && _personalBests.BestRunTime is { } best && attempt.FinalTime == best;
    }

    public void DeleteHistoricalRun(int index)
    {
        var wasActiveComparisonTarget = _comparisonSource == ComparisonSource.SpecificRun && _selectedHistoricalRunIndex == index;

        _runHistory.DeleteAttempt(index);

        _selectedHistoricalRunIndex = AdjustIndexAfterDelete(_selectedHistoricalRunIndex, index);
        OnPropertyChanged(nameof(SelectedHistoricalRunIndex));
        OnPropertyChanged(nameof(ActiveSpecificRunIndex));

        _selectedRunHistoryDetailIndex = AdjustIndexAfterDelete(_selectedRunHistoryDetailIndex, index);
        OnPropertyChanged(nameof(SelectedRunHistoryDetailIndex));
        OnPropertyChanged(nameof(SelectedRunHistoryDetailSummary));
        OnPropertyChanged(nameof(SelectedRunHistoryDetailInfo));
        OnPropertyChanged(nameof(SelectedRunHistoryDetailRows));

        OnPropertyChanged(nameof(AllHistoricalRuns));
        NotifyStatsChanged();

        if (wasActiveComparisonTarget)
            ComparisonSource = ComparisonSource.Best;
    }

    private static int? AdjustIndexAfterDelete(int? current, int deletedIndex) => current switch
    {
        null => null,
        var i when i == deletedIndex => null,
        var i when i > deletedIndex => i - 1,
        var i => i,
    };

    public bool AutoDeleteRunHistory
    {
        get => _autoDeleteRunHistory;
        set
        {
            if (_autoDeleteRunHistory == value)
                return;
            _autoDeleteRunHistory = value;
            OnPropertyChanged(nameof(AutoDeleteRunHistory));
            PruneRunHistoryIfNeeded();
        }
    }
    private bool _autoDeleteRunHistory = true;

    public int RunHistoryLimit
    {
        get => _runHistoryLimit;
        set
        {
            if (_runHistoryLimit == value)
                return;
            _runHistoryLimit = value;
            OnPropertyChanged(nameof(RunHistoryLimit));
            PruneRunHistoryIfNeeded();
        }
    }
    private int _runHistoryLimit = 50;

    public IReadOnlyList<int> AllRunHistoryLimits { get; } = new[] { 10, 25, 50, 100, 250, 500 };

    public RunHistoryTrimStrategy RunHistoryTrimStrategy
    {
        get => _runHistoryTrimStrategy;
        set
        {
            if (_runHistoryTrimStrategy == value)
                return;
            _runHistoryTrimStrategy = value;
            OnPropertyChanged(nameof(RunHistoryTrimStrategy));
            PruneRunHistoryIfNeeded();
        }
    }
    private RunHistoryTrimStrategy _runHistoryTrimStrategy = RunHistoryTrimStrategy.Slowest;

    public IReadOnlyList<RunHistoryTrimStrategy> AllRunHistoryTrimStrategies { get; } = Enum.GetValues<RunHistoryTrimStrategy>();

    private void PruneRunHistoryIfNeeded()
    {
        if (!AutoDeleteRunHistory)
            return;

        var attempts = _runHistory.GetAttempts();
        var excessCount = attempts.Count - RunHistoryLimit;
        if (excessCount <= 0)
            return;

        var bestTime = _personalBests.BestRunTime;
        var deletableIndices = Enumerable.Range(0, attempts.Count)
            .Where(i => !(attempts[i].Completed && bestTime is { } best && attempts[i].FinalTime == best))
            .ToList();

        var orderedForDeletion = RunHistoryTrimStrategy switch
        {
            RunHistoryTrimStrategy.Slowest => deletableIndices.OrderByDescending(i => attempts[i].FinalTime),
            _ => deletableIndices.OrderBy(i => attempts[i].Timestamp),
        };
        var indicesToDelete = orderedForDeletion.Take(excessCount).ToList();
        if (indicesToDelete.Count == 0)
            return;

        var wasActiveComparisonTarget = _comparisonSource == ComparisonSource.SpecificRun
            && _selectedHistoricalRunIndex is { } activeIndex && indicesToDelete.Contains(activeIndex);

        _runHistory.DeleteAttempts(indicesToDelete);

        _selectedHistoricalRunIndex = AdjustIndexAfterBulkDelete(_selectedHistoricalRunIndex, indicesToDelete);
        OnPropertyChanged(nameof(SelectedHistoricalRunIndex));
        OnPropertyChanged(nameof(ActiveSpecificRunIndex));

        _selectedRunHistoryDetailIndex = AdjustIndexAfterBulkDelete(_selectedRunHistoryDetailIndex, indicesToDelete);
        OnPropertyChanged(nameof(SelectedRunHistoryDetailIndex));
        OnPropertyChanged(nameof(SelectedRunHistoryDetailSummary));
        OnPropertyChanged(nameof(SelectedRunHistoryDetailInfo));
        OnPropertyChanged(nameof(SelectedRunHistoryDetailRows));

        OnPropertyChanged(nameof(AllHistoricalRuns));
        NotifyStatsChanged();

        if (wasActiveComparisonTarget)
            ComparisonSource = ComparisonSource.Best;
    }

    private static int? AdjustIndexAfterBulkDelete(int? current, IReadOnlyList<int> deletedIndices)
    {
        if (current is not { } value)
            return null;
        if (deletedIndices.Contains(value))
            return null;
        return value - deletedIndices.Count(i => i < value);
    }

    public int? SelectedRunHistoryDetailIndex
    {
        get => _selectedRunHistoryDetailIndex;
        set
        {
            if (_selectedRunHistoryDetailIndex == value)
                return;
            _selectedRunHistoryDetailIndex = value;
            OnPropertyChanged(nameof(SelectedRunHistoryDetailIndex));
            OnPropertyChanged(nameof(SelectedRunHistoryDetailSummary));
            OnPropertyChanged(nameof(SelectedRunHistoryDetailInfo));
            OnPropertyChanged(nameof(SelectedRunHistoryDetailRows));
        }
    }
    private int? _selectedRunHistoryDetailIndex;

    public string? SelectedRunHistoryDetailSummary
    {
        get
        {
            var options = AllHistoricalRuns;
            return _selectedRunHistoryDetailIndex is { } index && index >= 0 && index < options.Count
                ? options[index].Label
                : null;
        }
    }

    public string? SelectedRunHistoryDetailInfo => SelectedAttempt is { } attempt
        ? string.Join(Environment.NewLine, SubmissionDetails(attempt))
        : null;

    private RunAttempt? SelectedAttempt
    {
        get
        {
            var attempts = _runHistory.GetAttempts();
            return _selectedRunHistoryDetailIndex is { } index && index >= 0 && index < attempts.Count ? attempts[index] : null;
        }
    }

    public IReadOnlyList<RunHistoryDetailRow> SelectedRunHistoryDetailRows =>
        SelectedAttempt is { } attempt ? DetailRows(attempt) : Array.Empty<RunHistoryDetailRow>();

    private IReadOnlyList<RunHistoryDetailRow> DetailRows(RunAttempt attempt)
    {
        if (attempt.WeaponRuns is { } weaponRuns)
        {
            return weaponRuns
                .Select(run => new RunHistoryDetailRow(
                    AllWeaponSplits.DisplayName(run.WeaponAssetName) + (AllWeaponSplits.RunNote(run) is { } note ? $" ({note})" : ""),
                    FormatTimer(run.Time)))
                .Append(new RunHistoryDetailRow("Between runs", FormatTimer(attempt.BetweenRunsTime ?? 0f)))
                .ToList();
        }

        return attempt.FloorSegments
            .OrderBy(kv => kv.Key)
            .Select(kv => new RunHistoryDetailRow($"Depth {DisplayDepthNumber(kv.Key)}", FormatTimer(kv.Value)))
            .ToList();
    }

    private IEnumerable<string> SubmissionDetails(RunAttempt attempt)
    {
        if (attempt.WeaponRuns is not null)
            yield return $"Attempt total {FormatTimer(attempt.FinalTime)}";
        else
        {
            var times = new List<string> { $"Load+Cutscene-Free {FormatTimer(attempt.FinalTime)}" };
            if (attempt.LoadFreeFinalTime is { } loadFree)
                times.Add($"Load-Free {FormatTimer(loadFree)}");
            if (attempt.RawFinalTime is { } igt)
                times.Add($"IGT {FormatTimer(igt)}");
            yield return string.Join(" · ", times);
        }

        if (attempt.Weapon is { } weapon)
            yield return $"Weapon: {AllWeaponSplits.DisplayName(weapon)}";
        if (attempt.GameVersion is { } gameVersion)
            yield return $"Game version: {gameVersion}";
        if (attempt.GameBuild is { } gameBuild)
            yield return $"Store build: {gameBuild}";
    }

    public string PrimaryTimeText => SelectedPrimaryTimer switch
    {
        PrimaryTimerKind.Raw => RawRunTimeText,
        PrimaryTimerKind.LoadFree => LoadFreeTimeText,
        PrimaryTimerKind.LoadCutsceneFree => LoadCutsceneFreeTimeText,
        _ => RawRunTimeText,
    };

    public bool IsAttached
    {
        get => _isAttached;
        private set
        {
            if (_isAttached == value)
                return;
            _isAttached = value;
            OnPropertyChanged(nameof(IsAttached));
            OnPropertyChanged(nameof(ShouldOverlayBeVisible));
        }
    }
    private bool _isAttached;

    public bool IsConfiguratorVisible
    {
        get => _isConfiguratorVisible;
        set
        {
            if (_isConfiguratorVisible == value)
                return;
            _isConfiguratorVisible = value;
            OnPropertyChanged(nameof(IsConfiguratorVisible));
            OnPropertyChanged(nameof(ShouldOverlayBeVisible));
        }
    }
    private bool _isConfiguratorVisible;

    public bool AlwaysShowOverlay
    {
        get => _alwaysShowOverlay;
        set
        {
            if (_alwaysShowOverlay == value)
                return;
            _alwaysShowOverlay = value;
            OnPropertyChanged(nameof(AlwaysShowOverlay));
            OnPropertyChanged(nameof(ShouldOverlayBeVisible));
        }
    }
    private bool _alwaysShowOverlay;

    public bool ShouldOverlayBeVisible => AlwaysShowOverlay || IsAttached || IsConfiguratorVisible;

    public bool IsInRun
    {
        get => _isInRun;
        private set
        {
            if (_isInRun == value)
                return;
            SetField(ref _isInRun, value);
            OnPropertyChanged(nameof(CanChangeCategory));
        }
    }
    private bool _isInRun;

    public bool IsLoading { get => _isLoading; private set => SetField(ref _isLoading, value); }
    private bool _isLoading;

    public bool IsCutscenePlaying { get => _isCutscenePlaying; private set => SetField(ref _isCutscenePlaying, value); }
    private bool _isCutscenePlaying;

    public bool IsPaused { get => _isPaused; private set => SetField(ref _isPaused, value); }
    private bool _isPaused;

    public string RawRunTimeText { get => _rawRunTimeText; private set => SetField(ref _rawRunTimeText, value); }
    private string _rawRunTimeText = "0:00.00";

    public string LoadFreeTimeText { get => _loadFreeTimeText; private set => SetField(ref _loadFreeTimeText, value); }
    private string _loadFreeTimeText = "0:00.00";

    public string LoadCutsceneFreeTimeText { get => _loadCutsceneFreeTimeText; private set => SetField(ref _loadCutsceneFreeTimeText, value); }
    private string _loadCutsceneFreeTimeText = "0:00.00";

    public string CurrentBiomeName { get => _currentBiomeName; private set => SetField(ref _currentBiomeName, value); }
    private string _currentBiomeName = "-";

    public string? PreviousRunTimeText => _previousRunTime is { } t
        ? FormatTimer(t) + (PreviousRunWasSuccessful ? "" : " (incomplete)")
        : null;

    private float? PreviousRunTime
    {
        get => _previousRunTime;
        set { _previousRunTime = value; OnPropertyChanged(nameof(PreviousRunTimeText)); }
    }
    private float? _previousRunTime;

    public bool PreviousRunWasSuccessful
    {
        get => _previousRunWasSuccessful;
        private set { _previousRunWasSuccessful = value; OnPropertyChanged(nameof(PreviousRunWasSuccessful)); OnPropertyChanged(nameof(PreviousRunTimeText)); }
    }
    private bool _previousRunWasSuccessful;

    public string BestRunTimeText => _personalBests.BestRunTime is { } best ? FormatTimer(best) : "-";

    public bool HasBestRunTime => _personalBests.BestRunTime.HasValue;

    public float? NextFastestCompletedRunTime => _runHistory.GetAttempts()
        .Where(a => a.Completed && a.FinalTime != _personalBests.BestRunTime)
        .Select(a => (float?)a.FinalTime)
        .Min();

    public string? NextFastestCompletedRunTimeText => NextFastestCompletedRunTime is { } t ? FormatTimer(t) : null;

    public void DeletePersonalBest()
    {
        _personalBests.SetBestRunTime(NextFastestCompletedRunTime);
        OnPropertyChanged(nameof(BestRunTimeText));
        OnPropertyChanged(nameof(HasBestRunTime));
    }

    public string SumOfBestText => _personalBests.SumOfBest is { } sob ? FormatTimer(sob) : "-";

    public string SessionStatsText
    {
        get
        {
            var key = $"{_activeCategory}|{_activePlayerCount}";
            if (!_sessionStats.TryGetValue(key, out var stats) || stats.Attempts == 0)
                return "No attempts yet this session";

            var rate = (double)stats.Completed / stats.Attempts * 100;
            var attemptWord = stats.Attempts == 1 ? "attempt" : "attempts";
            return $"{stats.Attempts} {attemptWord} · {rate:F0}% completion";
        }
    }

    public ObservableCollection<BiomeGroupViewModel> SplitGroups { get; } = new();

    public void ResetRun()
    {
        lock (_tickLock)
        {
            EndAllWeaponAttemptEarly(_loadAndCutsceneFreeTimer.AdjustedRunTime, "reset");
            _runHold = null;
            _adjustedTimer.Reset();
            _loadAndCutsceneFreeTimer.Reset();
            _session.ForceReset(_lastStats.LevelReached, _lastStats.RoomReached);
            (_gameSource as RecordingGameSource)?.Recorder.RecordResetRun();
        }
    }

    private AbyssusStats ApplyRunHold(AbyssusStats raw, GameFrame frame)
    {
        var previousRunTime = _previousTickRunTime;
        var previousIsInRun = _previousTickIsInRun;
        var previousLevel = _previousTickLevelReached;
        var previousRoom = _previousTickRoomReached;
        _previousTickRunTime = raw.RunTime;
        _previousTickIsInRun = raw.IsInRun;
        _previousTickLevelReached = raw.LevelReached;
        _previousTickRoomReached = raw.RoomReached;

        if (_runHold is null)
        {
            if (!(_hasSeenLobbySinceAttach && _session.IsRunActive && raw.IsInRun && previousIsInRun && raw.RunTime < previousRunTime))
                return raw;

            _runHold = new RunHold(previousRunTime, previousLevel, previousRoom, raw.RunTime,
                new AdjustedTimer(() => _tickUtc), new AdjustedTimer(() => _tickUtc));
            AppLog.Log($"[Run hold] The game's run clock went back from {previousRunTime:F2}s to {raw.RunTime:F2}s while still in a run (depth {previousLevel} -> {raw.LevelReached}) — holding the run's last values until it ends.");
        }

        var held = _runHold;

        if (!raw.IsInRun)
        {
            _runHold = null;
            AppLog.Log("[Run hold] The run ended; it was recorded with its held values.");
            return raw with { RunTime = held.RunTime, LevelReached = held.LevelReached, RoomReached = held.RoomReached };
        }

        var inLobby = raw.CurrentLevelIds.Any(id => id.Equals("Lobby", StringComparison.OrdinalIgnoreCase));
        if (!inLobby && raw.RunTime - held.RunTimeAtDrop >= RunHoldNewRunClockSeconds)
        {
            _runHold = null;
            AppLog.Log($"[Run hold] The game's clock kept running outside the Lobby ({raw.RunTime:F2}s) — treating it as a new run. The previous run was recorded with its held values.");
            _lastStats = raw with { IsInRun = false, RunTime = held.RunTime, LevelReached = held.LevelReached, RoomReached = held.RoomReached };
            _session.Update(false, raw.RunSuccessful, held.LevelReached, held.RoomReached, _loadAndCutsceneFreeTimer.AdjustedRunTime,
                _currentDepthBossDisplayName ?? _currentDepthBiomeName ?? "-");
            _adjustedTimer = held.StandbyLoadFree;
            _loadAndCutsceneFreeTimer = held.StandbyLoadCutsceneFree;
            _currentRunCompleted = false;
            return raw;
        }

        return raw with { RunTime = held.RunTime, LevelReached = held.LevelReached, RoomReached = held.RoomReached };
    }

    private void UpdateJoinedMidRun(AbyssusStats stats)
    {
        if (_hasSeenLobbySinceAttach)
        {
            _midRunEvidenceStartRunTime = null;
            _joinedMidRunThisAttach = false;
        }
        else if (!_joinedMidRunThisAttach)
        {
            if (stats.IsInRun && AbyssusStatsValidator.TryValidate(stats, out _))
            {
                _midRunEvidenceStartRunTime ??= stats.RunTime;
                if (stats.RunTime - _midRunEvidenceStartRunTime >= MidRunEvidenceClockSeconds)
                {
                    _joinedMidRunThisAttach = true;
                    AppLog.Log("Joined mid-run — this run can't be timed (only IGT moves); timing starts with the next run from the Lobby.");
                }
            }
            else
            {
                _midRunEvidenceStartRunTime = null;
            }
        }

        if (_joinedMidRunThisAttach == _publishedJoinedMidRun)
            return;
        var joined = _publishedJoinedMidRun = _joinedMidRunThisAttach;
        _uiDispatcher.BeginInvoke(() => IsJoinedMidRun = joined);
    }

    private bool _publishedJoinedMidRun;

    private (bool PauseStopsTime, bool AutoReset, RunCategory Category, PlayerCount PlayerCount)? _recordedContext;

    private void RecordContextIfChanged()
    {
        if (_gameSource is not RecordingGameSource recording)
            return;

        var context = (PauseStopsTime, AutoResetOnLobbyReturn, _activeCategory, _activePlayerCount);
        if (_recordedContext == context)
            return;

        _recordedContext = context;
        recording.Recorder.RecordContext(PauseStopsTime, AutoResetOnLobbyReturn, _activeCategory.ToString(), _activePlayerCount.ToString());
    }

    public void Tick()
    {
#if TRUSTED_BUILD
        var tickStartTime = DateTime.UtcNow;
        if (_lastTickStartTime != DateTime.MinValue)
        {
            var gapMs = (tickStartTime - _lastTickStartTime).TotalMilliseconds;
            if (gapMs > TickGapLogThresholdMs)
                DebugConsole.Log($"[TickGap] {gapMs:F2}ms since the previous Tick() started (poll loop targets ~20ms)");
        }
        _lastTickStartTime = tickStartTime;
#endif

        lock (_tickLock)
        {
        _tickUtc = _clock();

        if (!_gameSource.IsAttached || !_gameSource.IsProcessAlive())
        {
            EndAllWeaponAttemptEarly(_loadAndCutsceneFreeTimer.AdjustedRunTime, "the game closed");
            _gameSource.OnDetached();

            if (_wasAttachedForLog)
            {
                AppLog.Log("Detached from Abyssus.");
                _wasAttachedForLog = false;
            }

            if (!_gameSource.TryAttach())
            {
                _adjustedTimer.Reset();
                _loadAndCutsceneFreeTimer.Reset();
                _publishedJoinedMidRun = false;
                _uiDispatcher.BeginInvoke(() =>
                {
                    IsAttached = false;
                    IsJoinedMidRun = false;
                });
                return;
            }

            if (_gameSource.Store is { } store)
            {
                var version = GameBuildVersion.TryRead(store, _gameSource.ExecutablePath);
                _gameBuildText = version is null ? store.DisplayName() : $"{store.DisplayName()} ({version})";
                AppLog.Log($"Attached to Abyssus ({_gameBuildText}).");
            }
            else
            {
                AppLog.Log("Attached to Abyssus.");
            }

            _wasAttachedForLog = true;
            _hasLoggedGameVersionThisAttach = false;

            IsPossibleGameMismatch = false;
            GameMismatchDetailText = null;
            _hasLoggedGameMismatchThisAttach = false;
            _consecutiveImplausibleTicks = 0;
            _consecutivePlausibleTicksAfterMismatch = 0;
            _hasLoggedReadsValidAgainThisAttach = false;
            _hasSeenLobbySinceAttach = false;
            _runHold = null;
            _previousTickIsInRun = false;
            _midRunEvidenceStartRunTime = null;
            _joinedMidRunThisAttach = false;

            if (GameVersionChecker.TryCheckVersion(_gameSource.ExecutablePath, GameOffsets.LastVerifiedGameVersion) == false)
            {
                ReportPossibleGameMismatch(
                    $"Attached game's version doesn't match the last-verified build ({GameOffsets.LastVerifiedGameVersion}) — Abyssus may have updated.");
            }
        }

        _uiDispatcher.BeginInvoke(() => IsAttached = true);

        var tickStopwatch = Stopwatch.StartNew();
        var statsReadMs = 0.0;
        var cutsceneCheckMs = 0.0;
        var restOfTickMs = 0.0;
        var rebuildSplitsMs = 0.0;
        var cutsceneCheckStopwatch = new Stopwatch();

        try
        {
            RecordContextIfChanged();

            var statsReadStopwatch = Stopwatch.StartNew();
            if (!_gameSource.TryReadFrame(out var frame))
                return;
            statsReadMs = statsReadStopwatch.Elapsed.TotalMilliseconds;

            var rawStats = frame.Stats;
            var stats = ApplyRunHold(rawStats, frame);
            _lastStats = stats;
            _lastCutsceneAssetName = frame.CurrentCutsceneAssetName;

            if (!_hasSeenLobbySinceAttach)
            {
                _consecutiveImplausibleTicks = 0;
            }
            else if (AbyssusStatsValidator.TryValidate(stats, out var implausibleReason))
            {
                _consecutiveImplausibleTicks = 0;

                if (IsPossibleGameMismatch && !_hasLoggedReadsValidAgainThisAttach
                    && ++_consecutivePlausibleTicksAfterMismatch >= PlausibleTicksBeforeLoggingRecovery)
                {
                    AppLog.Log("[Possible mismatch] Game data has read as valid again for 1 s.");
                    _hasLoggedReadsValidAgainThisAttach = true;
                }
            }
            else
            {
                _consecutivePlausibleTicksAfterMismatch = 0;
                if (++_consecutiveImplausibleTicks >= ImplausibleTicksThreshold)
                    ReportPossibleGameMismatch($"Implausible game data read ({implausibleReason}) — possible memory-offset mismatch after a game update.");
            }

#if TRUSTED_BUILD
            var joinedLevelIds = string.Join(" | ", stats.CurrentLevelIds);
            if (joinedLevelIds != _lastDumpedLevelIds)
            {
                _lastDumpedLevelIds = joinedLevelIds;
                DebugConsole.Log($"[RoomIds] Depth={stats.LevelReached} -> {joinedLevelIds}");
            }

            if (stats.IsInRun != _lastDumpedIsInRun || stats.RoomReached != _lastDumpedRoomReached || stats.LevelReached != _lastDumpedLevelReachedForDiag || stats.RunSuccessful != _lastDumpedRunSuccessful)
            {
                _lastDumpedIsInRun = stats.IsInRun;
                _lastDumpedRoomReached = stats.RoomReached;
                _lastDumpedLevelReachedForDiag = stats.LevelReached;
                _lastDumpedRunSuccessful = stats.RunSuccessful;
                DebugConsole.Log($"[RoomDiag] IsInRun={stats.IsInRun} Depth={stats.LevelReached} Room={stats.RoomReached} RunSuccessful={stats.RunSuccessful}");
            }

            var currentEliteReading = frame.CurrentEliteName;
            var currentHeraldReading = frame.CurrentHeraldName;
            if (currentEliteReading != _lastDumpedEliteName || currentHeraldReading != _lastDumpedHeraldName)
            {
                _lastDumpedEliteName = currentEliteReading;
                _lastDumpedHeraldName = currentHeraldReading;
                DebugConsole.Log($"[TrackerDiag] IsInRun={stats.IsInRun} Depth={stats.LevelReached} Elite={currentEliteReading ?? "-"} Herald={currentHeraldReading ?? "-"}");
            }
#endif

            cutsceneCheckStopwatch.Restart();
            var currentCutsceneAssetName = frame.CurrentCutsceneAssetName;
            cutsceneCheckMs += cutsceneCheckStopwatch.Elapsed.TotalMilliseconds;
#if TRUSTED_BUILD
            if (currentCutsceneAssetName != _lastDumpedCutsceneAssetName)
            {
                _lastDumpedCutsceneAssetName = currentCutsceneAssetName;
                DebugConsole.Log($"[CutsceneDiag] IsInRun={stats.IsInRun} Depth={stats.LevelReached} PlayingSequence={currentCutsceneAssetName ?? "-"}");
            }
#endif

            if (_hasDetectedTorakaSecretPhase && !_currentRunCompleted
                && string.Equals(currentCutsceneAssetName, TorakaSecretPhaseDeathCutscene, StringComparison.OrdinalIgnoreCase))
            {
                _currentRunCompleted = true;
                OnAllWeaponRunCompleted(_loadAndCutsceneFreeTimer.AdjustedRunTime);
            }

            _allWeaponAttempt?.Tick(_tickUtc, excludeFromTime: stats.IsLoading || (PauseStopsTime && stats.IsPaused));

            var restOfTickStopwatch = Stopwatch.StartNew();

            var pauseExclusion = PauseStopsTime && stats.IsPaused;
            var countThroughStall = !PauseStopsTime && stats.IsPaused;
            var notInRunExclusion = !stats.IsInRun;

            var notYetSeenLobbyExclusion = !_hasSeenLobbySinceAttach;

            var heldExclusion = _runHold is not null;
            _adjustedTimer.Update(stats.RunTime, stats.IsInRun, stats.IsLoading, stats.IsLoading || pauseExclusion || notInRunExclusion || _currentRunCompleted || notYetSeenLobbyExclusion || heldExclusion, countThroughStall);

            cutsceneCheckStopwatch.Restart();
            var isCutscenePlaying = frame.IsCutscenePlaying;
            cutsceneCheckMs += cutsceneCheckStopwatch.Elapsed.TotalMilliseconds;
            var runTimerHadStarted = _loadAndCutsceneFreeTimer.HasStarted;
            _loadAndCutsceneFreeTimer.Update(stats.RunTime, stats.IsInRun, stats.IsLoading, stats.IsLoading || isCutscenePlaying || pauseExclusion || notInRunExclusion || _currentRunCompleted || notYetSeenLobbyExclusion || heldExclusion, countThroughStall);

            if (_runHold is { } hold)
            {
                var rawPauseExclusion = PauseStopsTime && rawStats.IsPaused;
                var rawCountThroughStall = !PauseStopsTime && rawStats.IsPaused;
                hold.StandbyLoadFree.Update(rawStats.RunTime, rawStats.IsInRun, rawStats.IsLoading, rawStats.IsLoading || rawPauseExclusion || !rawStats.IsInRun, rawCountThroughStall);
                hold.StandbyLoadCutsceneFree.Update(rawStats.RunTime, rawStats.IsInRun, rawStats.IsLoading, rawStats.IsLoading || isCutscenePlaying || rawPauseExclusion || !rawStats.IsInRun, rawCountThroughStall);
            }

            if (_session.IsRunActive && stats.WeaponAssetName is { } runWeapon)
                _currentRunWeapon = runWeapon;

            if (!_hasLoggedGameVersionThisAttach && _gameSource.GameVersion is { } gameVersion)
            {
                _hasLoggedGameVersionThisAttach = true;
                AppLog.Log($"Game version {gameVersion}.");
            }

            if (!runTimerHadStarted && _loadAndCutsceneFreeTimer.HasStarted && _currentRunIsAllWeapon
                && stats.IsInRun && _session.IsRunActive && _hasSeenLobbySinceAttach && !_currentRunCompleted)
            {
                OnAllWeaponRunTimerStarted(stats.WeaponAssetName);
            }

            var outgoingDepthWasBossRoom = _currentDepthBossDisplayName is not null;
            var outgoingDepthEliteName = _currentDepthEliteName;
            var outgoingDepthIsRoyalAbyssContent = _currentDepthIsRoyalAbyssContent;
            var outgoingDepthIsTorakaContent = _currentDepthIsTorakaContent;
            if (stats.LevelReached != _lastObservedLevelReached)
            {
                if (!_currentDepthClassified)
                    _latchedInternalBiome = "";

                _lastObservedLevelReached = stats.LevelReached;
                _currentDepthClassified = false;
                _currentDepthBiomeName = null;
                _currentDepthBossDisplayName = null;
                _currentDepthEliteName = null;
                _currentDepthIsRoyalAbyssContent = false;
                _currentDepthIsTorakaContent = false;
            }

            if (!_hasEnteredRoyalAbyss && LevelContext.HasVoidLobbyMarker(stats.CurrentLevelIds))
                _hasEnteredRoyalAbyss = true;

            if (_currentDepthEliteName is null)
                _currentDepthEliteName = frame.CurrentEliteName;

            if (!_currentDepthClassified && LevelContext.TryExtractBossIdentifier(stats.CurrentLevelIds, out var bossIdentifier))
            {
                _currentDepthBossDisplayName = ResolveBossDisplayName(bossIdentifier);
                _currentDepthClassified = true;

                if (Normalize(bossIdentifier) == TorakaIdentifierNormalized)
                    _currentDepthIsTorakaContent = true;
            }
            else if (!_currentDepthClassified && LevelContext.TryExtractStartingRoomBiome(stats.CurrentLevelIds, out var rawBiome))
            {
                _latchedInternalBiome = rawBiome;
                _currentDepthBiomeName = TranslateBiomeDisplayName(rawBiome);
                _currentDepthClassified = true;
            }
            else if (!_currentDepthClassified && _hasEnteredRoyalAbyss)
            {
                _currentDepthBiomeName = "Royal Abyss";
                _currentDepthClassified = true;
                _currentDepthIsRoyalAbyssContent = true;
            }

            if (_currentDepthIsTorakaContent)
            {
                if (_currentRoomHeraldName is null)
                    _currentRoomHeraldName = _hasDetectedTorakaSecretPhase ? "Phase 2" : "Phase 1";
            }
            else if (_currentRoomHeraldName is null)
            {
                _currentRoomHeraldName = frame.CurrentHeraldName;
            }

            var currentBiomeName = _currentDepthBossDisplayName ?? _currentDepthBiomeName ?? "-";

            _wasBossRoomLastTick = outgoingDepthWasBossRoom;
            _wasEliteNameLastTick = outgoingDepthEliteName;
            _wasRoyalAbyssContentLastTick = outgoingDepthIsRoyalAbyssContent;
            _wasTorakaContentLastTick = outgoingDepthIsTorakaContent;

            if (!_hasSeenLobbySinceAttach && stats.CurrentLevelIds.Any(id => id.Equals("Lobby", StringComparison.OrdinalIgnoreCase)))
                _hasSeenLobbySinceAttach = true;

            UpdateJoinedMidRun(stats);

            if (_hasSeenLobbySinceAttach)
                _session.Update(stats.IsInRun, stats.RunSuccessful, stats.LevelReached, stats.RoomReached, _loadAndCutsceneFreeTimer.AdjustedRunTime, currentBiomeName);

            var uiIsInRun = stats.IsInRun;
            var uiIsLoading = stats.IsLoading;
            var uiIsCutscenePlaying = isCutscenePlaying;
            var uiIsPaused = stats.IsPaused;
            var uiRawRunTimeText = FormatTimer(stats.RunTime);
            var uiLoadFreeTimeText = FormatTimer(_adjustedTimer.AdjustedRunTime);
            var uiLoadCutsceneFreeTimeText = FormatTimer(_loadAndCutsceneFreeTimer.AdjustedRunTime);
            var uiCurrentBiomeName = _hasSeenLobbySinceAttach ? currentBiomeName : "-";

            restOfTickMs = restOfTickStopwatch.Elapsed.TotalMilliseconds;

            List<BiomeGroupViewModel>? uiSplitGroups = null;
            if (DateTime.UtcNow - _lastSplitsRebuildTime >= SplitsRebuildInterval || _forceSplitsRebuild)
            {
                _forceSplitsRebuild = false;
                _lastSplitsRebuildTime = DateTime.UtcNow;
                var rebuildSplitsStopwatch = Stopwatch.StartNew();
                uiSplitGroups = BuildSplitGroups(stats, currentBiomeName, _currentDepthBossDisplayName is not null, _currentDepthEliteName, _currentDepthIsRoyalAbyssContent, _currentDepthIsTorakaContent);
                rebuildSplitsMs = rebuildSplitsStopwatch.Elapsed.TotalMilliseconds;
            }

            var isAllWeaponView = IsAllWeaponView;
            var uiAllWeapon = isAllWeaponView ? BuildAllWeaponDisplay(uiSplitGroups) : default;

            _uiDispatcher.BeginInvoke(() =>
            {
                IsInRun = uiIsInRun;
                IsLoading = uiIsLoading;
                IsCutscenePlaying = uiIsCutscenePlaying;
                IsPaused = uiIsPaused;
                RawRunTimeText = uiRawRunTimeText;
                LoadFreeTimeText = uiLoadFreeTimeText;
                LoadCutsceneFreeTimeText = uiLoadCutsceneFreeTimeText;
                CurrentBiomeName = uiCurrentBiomeName;
                OnPropertyChanged(nameof(PrimaryTimeText));
                if (isAllWeaponView)
                {
                    AllWeaponTotalText = uiAllWeapon.Total;
                    AllWeaponBetweenRunsText = uiAllWeapon.BetweenRuns;
                    AllWeaponProgressText = uiAllWeapon.Progress;
                    AllWeaponCurrentWeaponName = uiAllWeapon.CurrentWeapon;
                }
                if (uiSplitGroups is not null)
                {
                    var weaponGroups = uiAllWeapon.Groups ?? new List<WeaponGroupViewModel>();
                    for (var i = 0; i < weaponGroups.Count; i++)
                    {
                        if (i >= WeaponGroups.Count)
                            WeaponGroups.Add(weaponGroups[i]);
                        else if (!Equals(WeaponGroups[i], weaponGroups[i]))
                            WeaponGroups[i] = weaponGroups[i];
                    }
                    while (WeaponGroups.Count > weaponGroups.Count)
                        WeaponGroups.RemoveAt(WeaponGroups.Count - 1);
                }

                OnPropertyChanged(nameof(OverlayDisplayPrimaryTimeText));
                OnPropertyChanged(nameof(OverlayDisplayBiomeName));

                if (uiSplitGroups is not null)
                {
                    var firstChangedIndex = Math.Max(0, SplitGroups.Count - 1);
                    for (var i = firstChangedIndex; i < uiSplitGroups.Count; i++)
                    {
                        if (i < SplitGroups.Count)
                            SplitGroups[i] = uiSplitGroups[i];
                        else
                            SplitGroups.Add(uiSplitGroups[i]);
                    }
                    while (SplitGroups.Count > uiSplitGroups.Count)
                        SplitGroups.RemoveAt(SplitGroups.Count - 1);
                }
            });
        }
        catch (Exception ex)
        {
            if (DateTime.UtcNow - _lastTickExceptionLogTime > TimeSpan.FromSeconds(5))
            {
                AppLog.LogException("Tick() read failure", ex);
                _lastTickExceptionLogTime = DateTime.UtcNow;
            }
        }
        finally
        {
            var totalMs = tickStopwatch.Elapsed.TotalMilliseconds;
            if (totalMs > TickTimingLogThresholdMs)
                DebugConsole.Log($"[TickTiming] total={totalMs:F2}ms statsRead={statsReadMs:F2}ms cutsceneChecks={cutsceneCheckMs:F2}ms restOfTick={restOfTickMs:F2}ms rebuildSplits={rebuildSplitsMs:F2}ms");
        }
        }
    }

    private sealed record DepthEntryData(
        int FloorNumber,
        string DisplayName,
        bool IsBossRoom,
        float SegmentTime,
        bool? IsNewBest,
        IReadOnlyList<RoomSplit> Rooms,
        int? InProgressRoomNumber,
        float InProgressRoomTime,
        string? EliteName,
        bool IsRoyalAbyssContent,
        bool IsTorakaContent,
        IReadOnlyList<string?> RoomHeraldNames,
        string? InProgressRoomHeraldName);

    private List<BiomeGroupViewModel> BuildSplitGroups(AbyssusStats stats, string currentBiomeName, bool isCurrentlyBossRoom, string? currentEliteName, bool isCurrentlyRoyalAbyssContent, bool isCurrentlyTorakaContent)
    {
        var entries = new List<DepthEntryData>();
        foreach (var (floorSplit, isNewBest, isBossRoom, eliteName, isRoyalAbyssContent, isTorakaContent, roomHeraldNames) in _annotatedFloorSplits)
            entries.Add(new DepthEntryData(floorSplit.FloorNumber, floorSplit.BiomeName, isBossRoom, floorSplit.SegmentTime, isNewBest, floorSplit.Rooms, null, 0f, eliteName, isRoyalAbyssContent, isTorakaContent, roomHeraldNames, null));

        if (_session.IsRunActive && _hasSeenLobbySinceAttach)
        {
            var floorInProgress = _loadAndCutsceneFreeTimer.AdjustedRunTime - _session.LastFloorSplitTime;
            var roomInProgress = _loadAndCutsceneFreeTimer.AdjustedRunTime - _session.LastRoomSplitTime;
            entries.Add(new DepthEntryData(stats.LevelReached, currentBiomeName, isCurrentlyBossRoom, floorInProgress, null, _session.CurrentFloorRooms, stats.RoomReached, roomInProgress, currentEliteName, isCurrentlyRoyalAbyssContent, isCurrentlyTorakaContent, _currentDepthRoomHeraldNames, _currentRoomHeraldName));
        }

        var groups = new List<List<DepthEntryData>>();
        foreach (var entry in entries)
        {
            var bothRoyalAbyssContent = entry.IsRoyalAbyssContent && groups.Count > 0 && groups[^1][0].IsRoyalAbyssContent;
            var bothTorakaContent = entry.IsTorakaContent && groups.Count > 0 && groups[^1][0].IsTorakaContent;
            var canMergeWithPrevious = groups.Count > 0
                && (bothRoyalAbyssContent
                    || bothTorakaContent
                    || (!entry.IsBossRoom && !groups[^1][0].IsBossRoom && groups[^1][0].DisplayName == entry.DisplayName));

            if (canMergeWithPrevious)
                groups[^1].Add(entry);
            else
                groups.Add(new List<DepthEntryData> { entry });
        }

        var result = new List<BiomeGroupViewModel>(groups.Count);
        foreach (var group in groups)
            result.Add(BuildBiomeGroup(group));

        if (_session.IsRunActive && _hasSeenLobbySinceAttach && result.Count > 0)
            result[^1] = result[^1] with { ShowDepthsInOverlay = true };

        return result;
    }

    private BiomeGroupViewModel BuildBiomeGroup(List<DepthEntryData> group)
    {
        var totalTime = 0f;
        foreach (var entry in group)
            totalTime += entry.SegmentTime;

        var isRoomBased = group[0].IsRoyalAbyssContent || group[0].IsTorakaContent;
        var depths = isRoomBased
            ? BuildRoomBasedRows(group)
            : group.Select(BuildDepthRow).ToList();

        var groupTitle = group[0].IsRoyalAbyssContent ? "Royal Abyss" : group[0].DisplayName;

        var showDepthsInOverlay = SplitListOverflowBehavior != SplitListOverflowBehavior.Collapse;

        return new BiomeGroupViewModel(groupTitle, FormatTimer(totalTime), depths, showDepthsInOverlay);
    }

    private List<DepthRowViewModel> BuildRoomBasedRows(List<DepthEntryData> group)
    {
        var roomsWithNames = new List<(float SegmentTime, string? HeraldName, bool IsInProgress)>();
        foreach (var entry in group)
        {
            for (var i = 0; i < entry.Rooms.Count; i++)
            {
                var heraldName = i < entry.RoomHeraldNames.Count ? entry.RoomHeraldNames[i] : null;
                roomsWithNames.Add((entry.Rooms[i].SegmentTime, heraldName, false));
            }

            if (entry.InProgressRoomNumber is not null)
                roomsWithNames.Add((entry.InProgressRoomTime, entry.InProgressRoomHeraldName, true));
        }

        var roomGroups = new List<List<(float SegmentTime, string? HeraldName, bool IsInProgress)>>();
        foreach (var room in roomsWithNames)
        {
            if (roomGroups.Count > 0 && roomGroups[^1][0].HeraldName == room.HeraldName)
                roomGroups[^1].Add(room);
            else
                roomGroups.Add(new List<(float SegmentTime, string? HeraldName, bool IsInProgress)> { room });
        }

        var resolvedNames = roomGroups.Select(g => g[0].HeraldName).Where(n => n is not null).ToHashSet();
        var missingHeraldNames = HeraldTracker.AllHeraldNames.Except(resolvedNames!).ToList();
        var inferredName = missingHeraldNames.Count == 1 ? missingHeraldNames[0] : null;

        var rows = new List<DepthRowViewModel>();
        var position = 0;
        foreach (var roomGroup in roomGroups)
        {
            position++;
            var groupTime = roomGroup.Sum(r => r.SegmentTime);
            var label = roomGroup[0].HeraldName ?? inferredName ?? $"Herald {position}";
            var status = roomGroup[^1].IsInProgress ? "in progress" : "";
            rows.Add(new DepthRowViewModel(label, FormatTimer(groupTime), status, Array.Empty<RoomRowViewModel>(), null, null, ShowRoomsInOverlay: false));
        }

        return rows;
    }

    private const float CloseThresholdSeconds = 3f;

    private DepthRowViewModel BuildDepthRow(DepthEntryData entry)
    {
        var best = _personalBests.GetBestFloorSegment(entry.FloorNumber);
        var status = entry.IsNewBest switch
        {
            true => "NEW PB",
            false when best.HasValue => $"PB {FormatTimer(best.Value)}",
            null => "in progress",
            _ => "",
        };

        string? deltaText = null;
        DeltaSeverity? deltaSeverity = null;
        var comparisonTime = ComparisonSource switch
        {
            ComparisonSource.Best => best,
            ComparisonSource.Previous => GetPreviousRunFloorSegment(entry.FloorNumber),
            ComparisonSource.SpecificRun => GetSelectedHistoricalRunFloorSegment(entry.FloorNumber),
            ComparisonSource.ImportedFile => _importedSplitFile?.FloorSegments.TryGetValue(entry.FloorNumber, out var importedTime) == true ? importedTime : null,
            _ => null,
        };
        if (comparisonTime is { } comparison)
        {
            var delta = entry.SegmentTime - comparison;
            deltaText = FormatDelta(delta);
            deltaSeverity = delta > 0
                ? DeltaSeverity.Behind
                : delta >= -CloseThresholdSeconds
                    ? DeltaSeverity.Close
                    : DeltaSeverity.Ahead;
        }

        var rooms = new List<RoomRowViewModel>(entry.Rooms.Count + 1);
        var position = 0;

        if (entry.IsBossRoom)
        {
            float? bossRoomTime = null;
            foreach (var room in entry.Rooms)
                bossRoomTime = (bossRoomTime ?? 0f) + room.SegmentTime;

            if (entry.InProgressRoomNumber is not null)
                bossRoomTime = (bossRoomTime ?? 0f) + entry.InProgressRoomTime;

            if (bossRoomTime is not null)
                rooms.Add(new RoomRowViewModel(1, "Room 1", FormatTimer(bossRoomTime.Value)));
        }
        else
        {
            var totalRoomCount = entry.Rooms.Count + (entry.InProgressRoomNumber is not null ? 1 : 0);
            var needsFold = totalRoomCount > 4;
            var room4Label = entry.EliteName ?? "Room 4";

            float? room4CombinedTime = null;
            foreach (var room in entry.Rooms)
            {
                position++;
                if (needsFold && position >= 4)
                    room4CombinedTime = (room4CombinedTime ?? 0f) + room.SegmentTime;
                else
                    rooms.Add(new RoomRowViewModel(position, position == 4 ? room4Label : $"Room {position}", FormatTimer(room.SegmentTime)));
            }

            if (entry.InProgressRoomNumber is not null)
            {
                position++;
                if (needsFold && position >= 4)
                    room4CombinedTime = (room4CombinedTime ?? 0f) + entry.InProgressRoomTime;
                else
                    rooms.Add(new RoomRowViewModel(position, position == 4 ? room4Label : $"Room {position}", FormatTimer(entry.InProgressRoomTime)));
            }

            if (room4CombinedTime is not null)
                rooms.Add(new RoomRowViewModel(4, room4Label, FormatTimer(room4CombinedTime.Value)));
        }

        var depthLabel = $"Depth {DisplayDepthNumber(entry.FloorNumber)}";

        var isInProgress = entry.IsNewBest is null;
        var showRoomsInOverlay = SplitDetailLevel == SplitDetailLevel.PerRoom
            && (SplitListOverflowBehavior != SplitListOverflowBehavior.Collapse || isInProgress);

        return new DepthRowViewModel(depthLabel, FormatTimer(entry.SegmentTime), status, rooms, deltaText, deltaSeverity, showRoomsInOverlay);
    }

    private static int DisplayDepthNumber(int rawFloorNumber) => rawFloorNumber + 1;

    private static string TranslateBiomeDisplayName(string internalName) =>
        BiomeDisplayNames.TryGetValue(internalName, out var display) ? display : internalName;

    private string ResolveBossDisplayName(string bossIdentifier)
    {
        if (Normalize(bossIdentifier) == TorakaIdentifierNormalized)
            return "To'raka, King of the Abyss";

        return BiomeBaseBossNames.TryGetValue(_latchedInternalBiome, out var name) ? name : "Herald";
    }

    private static string Normalize(string s)
    {
        Span<char> buffer = stackalloc char[s.Length];
        var count = 0;
        foreach (var c in s)
            if (char.IsLetter(c))
                buffer[count++] = char.ToLowerInvariant(c);

        return new string(buffer[..count]);
    }

    internal static string FormatTimer(float totalSeconds)
    {
        if (totalSeconds < 0)
            totalSeconds = 0;

        if (totalSeconds >= 3600)
        {
            var hours = (int)(totalSeconds / 3600);
            var remainder = totalSeconds - hours * 3600;
            var wholeMinutes = (int)(remainder / 60);
            return $"{hours}:{wholeMinutes:00}:{remainder - wholeMinutes * 60:00.00}";
        }

        var minutes = (int)(totalSeconds / 60);
        var seconds = totalSeconds - minutes * 60;
        return $"{minutes}:{seconds:00.00}";
    }

    private static string FormatDelta(float delta)
    {
        var sign = delta < 0 ? "-" : "+";
        return $"{sign}{FormatTimer(Math.Abs(delta))}";
    }

    private float? GetPreviousRunFloorSegment(int floorNumber)
    {
        var attempts = _runHistory.GetAttempts();
        if (attempts.Count == 0)
            return null;

        return attempts[^1].FloorSegments.TryGetValue(floorNumber, out var value) ? value : null;
    }

    public void Dispose()
    {
        _gameSource.Dispose();
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return;

        field = value;
        OnPropertyChanged(propertyName);
    }

    private void OnPropertyChanged(string? propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
