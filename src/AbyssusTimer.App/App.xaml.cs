using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using AbyssusTimer.App.Engine;
using AbyssusTimer.App.Interop;
using AbyssusTimer.App.Settings;
using AbyssusTimer.App.Windows;

namespace AbyssusTimer.App;

public partial class App : Application
{
    private AppSettings _settings = new();
    private TimerEngine? _engine;
    private OverlayWindow? _overlay;

    private Thread? _pollThread;
    private volatile bool _pollThreadRunning;
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(20);

#if TRUSTED_BUILD
    private DateTime _lastRenderTime = DateTime.MinValue;
    private const double RenderGapLogThresholdMs = 25.0;
#endif

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        AppLog.Initialize();
        DispatcherUnhandledException += (_, args) =>
        {
            AppLog.LogException("Unhandled UI-thread exception", args.Exception);
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
                AppLog.LogException("Unhandled exception", ex);
        };

        _settings = AppSettings.Load();

        if (_settings.TermsAcceptedVersion != TermsOfUse.CurrentVersion)
        {
            var termsWindow = new TermsOfUseWindow(isFirstRun: true);
            if (termsWindow.ShowDialog() != true)
            {
                Shutdown();
                return;
            }

            _settings.TermsAcceptedVersion = TermsOfUse.CurrentVersion;
            _settings.Save();
        }

#if TRUSTED_BUILD
        DebugConsole.Attach();

        DebugConsole.Log($"[RenderTier] {RenderCapability.Tier >> 16} (2=hardware, 1=partial, 0=software)");
#endif

        if (_settings.UseSoftwareRendering)
            RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.SoftwareOnly;

        Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.AboveNormal;
#if TRUSTED_BUILD
        DebugConsole.Log($"[ProcessPriority] set to {Process.GetCurrentProcess().PriorityClass}");
#endif

        HighResolutionTimer.Begin(1);

        _engine = new TimerEngine
        {
            PauseStopsTime = _settings.PauseStopsTime,
            AutoResetOnLobbyReturn = _settings.AutoResetOnLobbyReturn,
            SelectedPrimaryTimer = Enum.TryParse<PrimaryTimerKind>(_settings.PrimaryTimer, out var primaryTimer)
                ? primaryTimer
                : PrimaryTimerKind.LoadCutsceneFree,
            ActiveCategory = Enum.TryParse<RunCategory>(_settings.ActiveCategory, out var activeCategory)
                ? activeCategory
                : RunCategory.AnyPercent,
            ActivePlayerCount = Enum.TryParse<PlayerCount>(_settings.ActivePlayerCount, out var activePlayerCount)
                ? activePlayerCount
                : PlayerCount.Solo,
            ComparisonSource = Enum.TryParse<ComparisonSource>(_settings.ComparisonSource, out var comparisonSource)
                ? comparisonSource
                : ComparisonSource.Best,
            ExportRunnerName = _settings.ExportRunnerName,
            RunAtStartup = StartupRegistration.IsEnabled(),
            SplitDetailLevel = Enum.TryParse<SplitDetailLevel>(_settings.SplitDetailLevel, out var splitDetailLevel)
                ? splitDetailLevel
                : SplitDetailLevel.PerDepth,
            SplitListOverflowBehavior = Enum.TryParse<SplitListOverflowBehavior>(_settings.SplitListOverflowBehavior, out var splitListOverflowBehavior)
                ? splitListOverflowBehavior
                : SplitListOverflowBehavior.FullList,
            AutoDeleteRunHistory = _settings.AutoDeleteRunHistory,
            RunHistoryLimit = _settings.RunHistoryLimit,
            RunHistoryTrimStrategy = Enum.TryParse<RunHistoryTrimStrategy>(_settings.RunHistoryTrimStrategy, out var runHistoryTrimStrategy)
                ? runHistoryTrimStrategy
                : RunHistoryTrimStrategy.Slowest,
            ShowRawTimer = _settings.ShowRawTimer,
            ShowLoadFreeTimer = _settings.ShowLoadFreeTimer,
            ShowLoadCutsceneFreeTimer = _settings.ShowLoadCutsceneFreeTimer,
            ShowPreviousSegment = _settings.ShowPreviousSegment,
            OverlayOpacity = _settings.OverlayOpacity,
            OverlayScale = _settings.OverlayScale,
            OverlayBackgroundImagePath = _settings.OverlayBackgroundImagePath,
            OverlayBiomeFontSize = _settings.OverlayBiomeFontSize,
            OverlayDepthFontSize = _settings.OverlayDepthFontSize,
            OverlayFloorFontSize = _settings.OverlayFloorFontSize,
            OverlayTimesHaveBackground = _settings.OverlayTimesHaveBackground,
            OverlayBiomeTextColor = _settings.OverlayBiomeTextColor,
            OverlayDepthTextColor = _settings.OverlayDepthTextColor,
            OverlayFloorTextColor = _settings.OverlayFloorTextColor,
            OverlayTitleFontSize = _settings.OverlayTitleFontSize,
            OverlayTitleTextColor = _settings.OverlayTitleTextColor,
        };

        var overlayLeft = _settings.OverlayLeft;
        var overlayTop = _settings.OverlayTop;
        if (!IsPositionOnAnyScreen(overlayLeft, overlayTop))
        {
            overlayLeft = 120;
            overlayTop = 120;
        }

        _overlay = new OverlayWindow
        {
            Left = overlayLeft,
            Top = overlayTop,
            DataContext = _engine,
        };
        _overlay.Show();

        var configurator = new ConfiguratorWindow(_overlay, _engine, _settings);
        MainWindow = configurator;
        ShutdownMode = ShutdownMode.OnMainWindowClose;

        if (e.Args.Contains("--tray", StringComparer.OrdinalIgnoreCase))
            configurator.ShowMinimizedToTray();
        else
            configurator.Show();

        _pollThreadRunning = true;
        _pollThread = new Thread(PollLoop) { IsBackground = true, Name = "AbyssusTimer-Poll" };
        _pollThread.Start();

#if TRUSTED_BUILD
        CompositionTarget.Rendering += OnRendering;
#endif

        _ = _engine.CheckForUpdateAsync();
    }

    private static bool IsPositionOnAnyScreen(double left, double top) =>
        System.Windows.Forms.Screen.AllScreens.Any(screen =>
            screen.Bounds.Contains((int)left, (int)top));

    private void PollLoop()
    {
        while (_pollThreadRunning)
        {
            var tickStart = DateTime.UtcNow;

            try
            {
                _engine?.Tick();
            }
            catch
            {
            }

            var elapsed = DateTime.UtcNow - tickStart;
            var sleepTime = PollInterval - elapsed;
            if (sleepTime > TimeSpan.Zero)
                Thread.Sleep(sleepTime);
        }
    }

#if TRUSTED_BUILD
    private void OnRendering(object? sender, EventArgs e)
    {
        var now = DateTime.UtcNow;
        if (_lastRenderTime != DateTime.MinValue)
        {
            var gapMs = (now - _lastRenderTime).TotalMilliseconds;
            if (gapMs > RenderGapLogThresholdMs)
                DebugConsole.Log($"[RenderGap] {gapMs:F2}ms since the previous CompositionTarget.Rendering frame");
        }
        _lastRenderTime = now;
    }
#endif

    protected override void OnExit(ExitEventArgs e)
    {
        _pollThreadRunning = false;
        _pollThread?.Join(TimeSpan.FromSeconds(1));

#if TRUSTED_BUILD
        CompositionTarget.Rendering -= OnRendering;
#endif
        HighResolutionTimer.End(1);

        if (_overlay is not null)
        {
            _settings.OverlayLeft = _overlay.Left;
            _settings.OverlayTop = _overlay.Top;
        }

        if (_engine is not null)
        {
            _settings.PauseStopsTime = _engine.PauseStopsTime;
            _settings.AutoResetOnLobbyReturn = _engine.AutoResetOnLobbyReturn;
            _settings.PrimaryTimer = _engine.SelectedPrimaryTimer.ToString();
            _settings.ActiveCategory = _engine.ActiveCategory.ToString();
            _settings.ActivePlayerCount = _engine.ActivePlayerCount.ToString();
            _settings.ComparisonSource = _engine.ComparisonSource.ToString();
            _settings.ExportRunnerName = _engine.ExportRunnerName;
            _settings.SplitDetailLevel = _engine.SplitDetailLevel.ToString();
            _settings.SplitListOverflowBehavior = _engine.SplitListOverflowBehavior.ToString();
            _settings.AutoDeleteRunHistory = _engine.AutoDeleteRunHistory;
            _settings.RunHistoryLimit = _engine.RunHistoryLimit;
            _settings.RunHistoryTrimStrategy = _engine.RunHistoryTrimStrategy.ToString();
            _settings.ShowRawTimer = _engine.ShowRawTimer;
            _settings.ShowLoadFreeTimer = _engine.ShowLoadFreeTimer;
            _settings.ShowLoadCutsceneFreeTimer = _engine.ShowLoadCutsceneFreeTimer;
            _settings.ShowPreviousSegment = _engine.ShowPreviousSegment;
            _settings.OverlayOpacity = _engine.OverlayOpacity;
            _settings.OverlayScale = _engine.OverlayScale;
            _settings.OverlayBackgroundImagePath = _engine.OverlayBackgroundImagePath;
            _settings.OverlayBiomeFontSize = _engine.OverlayBiomeFontSize;
            _settings.OverlayDepthFontSize = _engine.OverlayDepthFontSize;
            _settings.OverlayFloorFontSize = _engine.OverlayFloorFontSize;
            _settings.OverlayTimesHaveBackground = _engine.OverlayTimesHaveBackground;
            _settings.OverlayBiomeTextColor = _engine.OverlayBiomeTextColor;
            _settings.OverlayDepthTextColor = _engine.OverlayDepthTextColor;
            _settings.OverlayFloorTextColor = _engine.OverlayFloorTextColor;
            _settings.OverlayTitleFontSize = _engine.OverlayTitleFontSize;
            _settings.OverlayTitleTextColor = _engine.OverlayTitleTextColor;
        }

        _settings.Save();
        _engine?.Dispose();

        base.OnExit(e);
    }
}
