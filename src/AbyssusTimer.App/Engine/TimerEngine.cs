using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Threading;
using AbyssusOverlay.Core;
using AbyssusTimer.App.Interop;

namespace AbyssusTimer.App.Engine;

public sealed class TimerEngine : INotifyPropertyChanged, IDisposable
{
    private readonly object _tickLock = new();
    private readonly Dispatcher _uiDispatcher = Dispatcher.CurrentDispatcher;

    private readonly GameProcess _process = new();
    private readonly AbyssusStatsReader _reader;
    private readonly AdjustedTimer _adjustedTimer = new();
    private readonly AdjustedTimer _loadAndCutsceneFreeTimer = new();
    private readonly RunSession _session = new();
    private readonly PersonalBests _personalBests;
    private readonly RunHistory _runHistory;
    private readonly List<(FloorSplit Split, bool IsNewBest, bool IsBossRoom, string? EliteName, bool IsRoyalAbyssContent, bool IsTorakaContent, IReadOnlyList<string?> RoomHeraldNames)> _annotatedFloorSplits = new();

    private readonly Dictionary<string, (int Attempts, int Completed)> _sessionStats = new();

    private CutsceneTracker? _cutsceneTracker;
    private HeraldTracker? _heraldTracker;
    private EliteTracker? _eliteTracker;
    private NamePool? _namePool;
    private AbyssusStats _lastStats = AbyssusStats.Empty;
    private bool _currentRunCompleted;

    private bool _wasAttachedForLog;
    private DateTime _lastTickExceptionLogTime = DateTime.MinValue;

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

    private bool _hasSeenLobbyThisSession;
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

    public string AppVersionText { get; } =
        $"v{System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "?"}";

    private bool _isUpdateAvailable;
    public bool IsUpdateAvailable
    {
        get => _isUpdateAvailable;
        private set => SetField(ref _isUpdateAvailable, value);
    }

    private string? _latestVersionText;
    public string? LatestVersionText
    {
        get => _latestVersionText;
        private set => SetField(ref _latestVersionText, value);
    }

    public async Task CheckForUpdateAsync()
    {
        var latest = await UpdateChecker.GetLatestVersionAsync();
        if (latest is null)
            return;

        var currentVersionText = AppVersionText.TrimStart('v', 'V');
        if (!Version.TryParse(latest, out var latestVersion) || !Version.TryParse(currentVersionText, out var currentVersion))
            return;

        if (latestVersion > currentVersion)
        {
            LatestVersionText = latest;
            IsUpdateAvailable = true;
        }
    }

    public TimerEngine()
    {
        _reader = new AbyssusStatsReader(_process);

        var pbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AbyssusTimer",
            "personal_bests.json");
        _personalBests = new PersonalBests(pbPath, $"{_activeCategory}|{_activePlayerCount}");

        var historyPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AbyssusTimer",
            "run_history.json");
        _runHistory = new RunHistory(historyPath, $"{_activeCategory}|{_activePlayerCount}");

        _session.RunStarted += () =>
        {
            AppLog.Log($"Run started — {_activeCategory}|{_activePlayerCount}");
            _annotatedFloorSplits.Clear();
            _currentRunCompleted = false;

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
            var isNewBest = _personalBests.TryRecordFloorSegment(split.FloorNumber, split.SegmentTime);
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
                && _cutsceneTracker is not null && _namePool is not null
                && _cutsceneTracker.TryGetCurrentSequenceAssetName(_namePool, out var cutsceneName)
                && string.Equals(cutsceneName, TorakaTrueEndingTransitionCutscene, StringComparison.OrdinalIgnoreCase);

            if (isTrueEndingTransition)
                _hasDetectedTorakaSecretPhase = true;
            else
                _currentRunCompleted = true;
        };
        _session.RunEnded += finalTime =>
        {
            AppLog.Log($"Run ended — {_activeCategory}|{_activePlayerCount} completed={_currentRunCompleted} finalTime={finalTime:F2}s");

            if (_currentRunCompleted)
                _personalBests.TryRecordRunTime(finalTime);
            PreviousRunWasSuccessful = _currentRunCompleted;
            PreviousRunTime = finalTime;
            OnPropertyChanged(nameof(BestRunTimeText));
            OnPropertyChanged(nameof(HasBestRunTime));

            var sessionKey = $"{_activeCategory}|{_activePlayerCount}";
            var sessionStats = _sessionStats.TryGetValue(sessionKey, out var existingStats) ? existingStats : (Attempts: 0, Completed: 0);
            sessionStats.Attempts++;
            if (_currentRunCompleted)
                sessionStats.Completed++;
            _sessionStats[sessionKey] = sessionStats;
            OnPropertyChanged(nameof(SessionStatsText));

            var finalSegmentTime = finalTime - _session.LastFloorSplitTime;
            var finalBiomeName = _currentDepthBossDisplayName ?? _currentDepthBiomeName ?? "-";
            var finalIsNewBest = _currentRunCompleted && _personalBests.TryRecordFloorSegment(_lastStats.LevelReached, finalSegmentTime);
            if (finalIsNewBest)
                OnPropertyChanged(nameof(SumOfBestText));
            var finalSplit = new FloorSplit(_lastStats.LevelReached, finalSegmentTime, finalBiomeName, _session.CurrentFloorRooms);
            var finalRoomHeraldNames = _currentDepthRoomHeraldNames.Append(_currentRoomHeraldName).ToArray();
            _annotatedFloorSplits.Add((finalSplit, finalIsNewBest, _currentDepthBossDisplayName is not null, _currentDepthEliteName, _currentDepthIsRoyalAbyssContent, _currentDepthIsTorakaContent, finalRoomHeraldNames));

            var floorSegments = _annotatedFloorSplits.ToDictionary(entry => entry.Split.FloorNumber, entry => entry.Split.SegmentTime);
            _runHistory.RecordAttempt(new RunAttempt(DateTime.Now, _currentRunCompleted, finalTime, floorSegments, _lastStats.RunTime, _adjustedTimer.AdjustedRunTime));
            OnPropertyChanged(nameof(AllHistoricalRuns));
            NotifyStatsChanged();
            PruneRunHistoryIfNeeded();

            if (AutoResetOnLobbyReturn)
            {
                _adjustedTimer.Reset();
                _loadAndCutsceneFreeTimer.Reset();
            }
        };
    }

    public bool PauseStopsTime { get; set; }
    public bool AutoResetOnLobbyReturn { get; set; } = true;

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
            if (IsInRun || _activeCategory == value)
                return;

            _activeCategory = value;
            UpdatePersonalBestsCategory();
            OnPropertyChanged(nameof(ActiveCategory));
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
            if (IsInRun || _activePlayerCount == value)
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
            if (IsPreviewingOverlay)
                OnPropertyChanged(nameof(OverlayDisplaySplitGroups));
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
            if (IsPreviewingOverlay)
                OnPropertyChanged(nameof(OverlayDisplaySplitGroups));
        }
    }
    private SplitListOverflowBehavior _splitListOverflowBehavior = SplitListOverflowBehavior.FullList;

    public IReadOnlyList<SplitListOverflowBehavior> AllSplitListOverflowBehaviors { get; } = Enum.GetValues<SplitListOverflowBehavior>();

    public double OverlayOpacity { get => _overlayOpacity; set => SetField(ref _overlayOpacity, value); }
    private double _overlayOpacity = 0.90;

    public double OverlayScale { get => _overlayScale; set => SetField(ref _overlayScale, value); }
    private double _overlayScale = 1.0;

    public string? OverlayBackgroundImagePath { get => _overlayBackgroundImagePath; set => SetField(ref _overlayBackgroundImagePath, value); }
    private string? _overlayBackgroundImagePath;

    public double OverlayBiomeFontSize { get => _overlayBiomeFontSize; set => SetField(ref _overlayBiomeFontSize, value); }
    private double _overlayBiomeFontSize = 11;

    public double OverlayDepthFontSize { get => _overlayDepthFontSize; set => SetField(ref _overlayDepthFontSize, value); }
    private double _overlayDepthFontSize = 11;

    public double OverlayFloorFontSize { get => _overlayFloorFontSize; set => SetField(ref _overlayFloorFontSize, value); }
    private double _overlayFloorFontSize = 10;

    public bool OverlayTimesHaveBackground { get => _overlayTimesHaveBackground; set => SetField(ref _overlayTimesHaveBackground, value); }
    private bool _overlayTimesHaveBackground;

    public string OverlayBiomeTextColor { get => _overlayBiomeTextColor; set => SetField(ref _overlayBiomeTextColor, value); }
    private string _overlayBiomeTextColor = "#C98F3F";

    public string OverlayDepthTextColor { get => _overlayDepthTextColor; set => SetField(ref _overlayDepthTextColor, value); }
    private string _overlayDepthTextColor = "#8A9994";

    public string OverlayFloorTextColor { get => _overlayFloorTextColor; set => SetField(ref _overlayFloorTextColor, value); }
    private string _overlayFloorTextColor = "#56635E";

    public double OverlayTitleFontSize { get => _overlayTitleFontSize; set => SetField(ref _overlayTitleFontSize, value); }
    private double _overlayTitleFontSize = 11;

    public string OverlayTitleTextColor { get => _overlayTitleTextColor; set => SetField(ref _overlayTitleTextColor, value); }
    private string _overlayTitleTextColor = "#C98F3F";

    public bool IsPreviewingOverlay
    {
        get => _isPreviewingOverlay;
        set
        {
            if (_isPreviewingOverlay == value)
                return;
            _isPreviewingOverlay = value;
            OnPropertyChanged(nameof(IsPreviewingOverlay));
            OnPropertyChanged(nameof(OverlayDisplaySplitGroups));
            OnPropertyChanged(nameof(OverlayDisplayBiomeName));
            OnPropertyChanged(nameof(OverlayDisplayPrimaryTimeText));
        }
    }
    private bool _isPreviewingOverlay;

    public IReadOnlyList<BiomeGroupViewModel> OverlayDisplaySplitGroups =>
        IsPreviewingOverlay ? BuildPreviewSplitGroups() : SplitGroups;

    public string OverlayDisplayBiomeName => IsPreviewingOverlay ? "Sunken Gardens" : CurrentBiomeName;

    public string OverlayDisplayPrimaryTimeText => IsPreviewingOverlay ? "8:42.15" : PrimaryTimeText;

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
        OverlayOpacity = 0.90;
        OverlayScale = 1.0;
        OverlayBackgroundImagePath = null;
        OverlayBiomeFontSize = 11;
        OverlayDepthFontSize = 11;
        OverlayFloorFontSize = 10;
        OverlayTimesHaveBackground = false;
        OverlayBiomeTextColor = "#C98F3F";
        OverlayDepthTextColor = "#8A9994";
        OverlayFloorTextColor = "#56635E";
        OverlayTitleFontSize = 11;
        OverlayTitleTextColor = "#C98F3F";
    }

    private void UpdatePersonalBestsCategory()
    {
        var key = $"{_activeCategory}|{_activePlayerCount}";
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
        OnPropertyChanged(nameof(SelectedRunHistoryDetailRows));
    }

    private void NotifyStatsChanged()
    {
        OnPropertyChanged(nameof(StatsPerLocationRows));
        OnPropertyChanged(nameof(StatsPbProgressionRows));
        OnPropertyChanged(nameof(StatsAttemptsReachedRows));
        OnPropertyChanged(nameof(StatsLoadCutsceneOverheadText));
    }

    public IReadOnlyList<RunCategory> AllCategories { get; } = Enum.GetValues<RunCategory>();
    public IReadOnlyList<PlayerCount> AllPlayerCounts { get; } = Enum.GetValues<PlayerCount>();

    public IReadOnlyList<HistoricalRunOption> AllHistoricalRuns => _runHistory.GetAttempts()
        .Select((attempt, index) => new HistoricalRunOption(index,
            $"{attempt.Timestamp:g} — {(attempt.Completed ? "Completed" : "Incomplete")} — {FormatTimer(attempt.FinalTime)}"))
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
                rows.Add(new PbMilestoneRow(attempt.Timestamp.ToString("g"), FormatTimer(attempt.FinalTime), improvementText));
                runningBest = attempt.FinalTime;
            }

            return rows;
        }
    }

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
            attempt.LoadFreeFinalTime);

        File.WriteAllText(filePath, System.Text.Json.JsonSerializer.Serialize(file, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
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

    public IReadOnlyList<RunHistoryDetailRow> SelectedRunHistoryDetailRows
    {
        get
        {
            if (_selectedRunHistoryDetailIndex is not { } index)
                return Array.Empty<RunHistoryDetailRow>();

            var attempts = _runHistory.GetAttempts();
            if (index < 0 || index >= attempts.Count)
                return Array.Empty<RunHistoryDetailRow>();

            return attempts[index].FloorSegments
                .OrderBy(kv => kv.Key)
                .Select(kv => new RunHistoryDetailRow($"Depth {DisplayDepthNumber(kv.Key)}", FormatTimer(kv.Value)))
                .ToList();
        }
    }

    public string PrimaryTimeText => SelectedPrimaryTimer switch
    {
        PrimaryTimerKind.Raw => RawRunTimeText,
        PrimaryTimerKind.LoadFree => LoadFreeTimeText,
        PrimaryTimerKind.LoadCutsceneFree => LoadCutsceneFreeTimeText,
        _ => RawRunTimeText,
    };

    public bool IsAttached { get => _isAttached; private set => SetField(ref _isAttached, value); }
    private bool _isAttached;

    public bool IsInRun { get => _isInRun; private set => SetField(ref _isInRun, value); }
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
            _adjustedTimer.Reset();
            _loadAndCutsceneFreeTimer.Reset();
            _session.ForceReset(_lastStats.LevelReached, _lastStats.RoomReached);
        }
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
        if (!_process.IsAttached || !_process.IsProcessAlive())
        {
            _cutsceneTracker?.Stop();
            _heraldTracker?.Stop();
            _eliteTracker?.Stop();

            if (_wasAttachedForLog)
            {
                AppLog.Log("Detached from Abyssus.");
                _wasAttachedForLog = false;
            }

            if (!_process.TryAttach(GameOffsets.ProcessName))
            {
                _adjustedTimer.Reset();
                _loadAndCutsceneFreeTimer.Reset();
                _uiDispatcher.BeginInvoke(() => IsAttached = false);
                return;
            }

            var namePool = new NamePool(_process, _process.ModuleBase + GameOffsets.GNames);
            _namePool = namePool;
            var objectArray = new GUObjectArrayReader(_process, _process.ModuleBase + GameOffsets.GUObjectArray);
            _cutsceneTracker = new CutsceneTracker(_process, namePool, objectArray);
            _cutsceneTracker.Start();
            _heraldTracker = new HeraldTracker(namePool, objectArray);
            _heraldTracker.Start();
            _eliteTracker = new EliteTracker(namePool, objectArray);
            _eliteTracker.Start();

            AppLog.Log("Attached to Abyssus.");
            _wasAttachedForLog = true;
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
            var statsReadStopwatch = Stopwatch.StartNew();
            if (!_reader.TryReadStats(out var stats))
                return;
            statsReadMs = statsReadStopwatch.Elapsed.TotalMilliseconds;

            _lastStats = stats;

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

            var currentEliteReading = _eliteTracker?.CurrentEliteName;
            var currentHeraldReading = _heraldTracker?.CurrentHeraldName;
            if (currentEliteReading != _lastDumpedEliteName || currentHeraldReading != _lastDumpedHeraldName)
            {
                _lastDumpedEliteName = currentEliteReading;
                _lastDumpedHeraldName = currentHeraldReading;
                DebugConsole.Log($"[TrackerDiag] IsInRun={stats.IsInRun} Depth={stats.LevelReached} Elite={currentEliteReading ?? "-"} Herald={currentHeraldReading ?? "-"}");
            }
#endif

            cutsceneCheckStopwatch.Restart();
            string? currentCutsceneAssetName = null;
            if (_cutsceneTracker is not null && _namePool is not null && _cutsceneTracker.TryGetCurrentSequenceAssetName(_namePool, out var cutsceneAssetNameValue))
                currentCutsceneAssetName = cutsceneAssetNameValue;
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
            }

            var restOfTickStopwatch = Stopwatch.StartNew();

            var pauseExclusion = PauseStopsTime && stats.IsPaused;
            var countThroughStall = !PauseStopsTime;
            var notInRunExclusion = !stats.IsInRun;

            var notYetSeenLobbyExclusion = !_hasSeenLobbyThisSession;

            _adjustedTimer.Update(stats.RunTime, stats.IsInRun, stats.IsLoading, stats.IsLoading || pauseExclusion || notInRunExclusion || _currentRunCompleted || notYetSeenLobbyExclusion, countThroughStall);

            cutsceneCheckStopwatch.Restart();
            var isCutscenePlaying = _cutsceneTracker?.IsCutscenePlaying() ?? false;
            cutsceneCheckMs += cutsceneCheckStopwatch.Elapsed.TotalMilliseconds;
            _loadAndCutsceneFreeTimer.Update(stats.RunTime, stats.IsInRun, stats.IsLoading, stats.IsLoading || isCutscenePlaying || pauseExclusion || notInRunExclusion || _currentRunCompleted || notYetSeenLobbyExclusion, countThroughStall);

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
                _currentDepthEliteName = _eliteTracker?.CurrentEliteName;

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
                _currentRoomHeraldName = _heraldTracker?.CurrentHeraldName;
            }

            var currentBiomeName = _currentDepthBossDisplayName ?? _currentDepthBiomeName ?? "-";

            _wasBossRoomLastTick = outgoingDepthWasBossRoom;
            _wasEliteNameLastTick = outgoingDepthEliteName;
            _wasRoyalAbyssContentLastTick = outgoingDepthIsRoyalAbyssContent;
            _wasTorakaContentLastTick = outgoingDepthIsTorakaContent;

            if (!_hasSeenLobbyThisSession && stats.CurrentLevelIds.Any(id => id.Equals("Lobby", StringComparison.OrdinalIgnoreCase)))
                _hasSeenLobbyThisSession = true;

            if (_hasSeenLobbyThisSession)
                _session.Update(stats.IsInRun, stats.RunSuccessful, stats.LevelReached, stats.RoomReached, _loadAndCutsceneFreeTimer.AdjustedRunTime, currentBiomeName);

            var uiIsInRun = stats.IsInRun;
            var uiIsLoading = stats.IsLoading;
            var uiIsCutscenePlaying = isCutscenePlaying;
            var uiIsPaused = stats.IsPaused;
            var uiRawRunTimeText = FormatTimer(stats.RunTime);
            var uiLoadFreeTimeText = FormatTimer(_adjustedTimer.AdjustedRunTime);
            var uiLoadCutsceneFreeTimeText = FormatTimer(_loadAndCutsceneFreeTimer.AdjustedRunTime);
            var uiCurrentBiomeName = _hasSeenLobbyThisSession ? currentBiomeName : "-";

            restOfTickMs = restOfTickStopwatch.Elapsed.TotalMilliseconds;

            List<BiomeGroupViewModel>? uiSplitGroups = null;
            if (DateTime.UtcNow - _lastSplitsRebuildTime >= SplitsRebuildInterval)
            {
                _lastSplitsRebuildTime = DateTime.UtcNow;
                var rebuildSplitsStopwatch = Stopwatch.StartNew();
                uiSplitGroups = BuildSplitGroups(stats, currentBiomeName, _currentDepthBossDisplayName is not null, _currentDepthEliteName, _currentDepthIsRoyalAbyssContent, _currentDepthIsTorakaContent);
                rebuildSplitsMs = rebuildSplitsStopwatch.Elapsed.TotalMilliseconds;
            }

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

        if (_session.IsRunActive && _hasSeenLobbyThisSession)
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

        if (_session.IsRunActive && _hasSeenLobbyThisSession && result.Count > 0)
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

    private static string FormatTimer(float totalSeconds)
    {
        if (totalSeconds < 0)
            totalSeconds = 0;

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
        _cutsceneTracker?.Dispose();
        _heraldTracker?.Dispose();
        _eliteTracker?.Dispose();
        _process.Dispose();
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
