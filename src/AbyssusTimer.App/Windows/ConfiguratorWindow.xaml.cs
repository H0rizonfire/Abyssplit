using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using AbyssusTimer.App.Engine;
using AbyssusTimer.App.Interop;
using AbyssusTimer.App.Settings;

namespace AbyssusTimer.App.Windows;

public partial class ConfiguratorWindow : Window
{
    private readonly OverlayWindow _overlay;
    private readonly TimerEngine _engine;
    private readonly AppSettings _settings;

    private System.Windows.Forms.NotifyIcon? _trayIcon;
    private System.Windows.Forms.ToolStripMenuItem? _trayResetMenuItem;

    public ConfiguratorWindow(OverlayWindow overlay, TimerEngine engine, AppSettings settings)
    {
        InitializeComponent();
        _overlay = overlay;
        _engine = engine;
        _settings = settings;
        DataContext = engine;
        SoftwareRenderingCheckBox.IsChecked = settings.UseSoftwareRendering;
        InitializeTrayIcon();
    }

    private void InitializeTrayIcon()
    {
        System.Drawing.Icon icon;
        try
        {
            using var iconStream = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/Assets/AppIcon.ico"))?.Stream;
            icon = iconStream is not null ? new System.Drawing.Icon(iconStream) : System.Drawing.SystemIcons.Application;
        }
        catch (Exception ex)
        {
            AppLog.LogException("Failed to load tray icon image — falling back to the system default", ex);
            icon = System.Drawing.SystemIcons.Application;
        }

        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("Show Abyssplit", null, (_, _) => RestoreFromTray());
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        _trayResetMenuItem = (System.Windows.Forms.ToolStripMenuItem)menu.Items.Add("Reset Run", null, (_, _) => _engine.ResetRun());
        _trayResetMenuItem.Enabled = !_engine.IsInRun;
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => System.Windows.Application.Current.Shutdown());

        _trayIcon = new System.Windows.Forms.NotifyIcon
        {
            Icon = icon,
            Text = "Abyssplit",
            Visible = false,
            ContextMenuStrip = menu,
        };
        _trayIcon.DoubleClick += (_, _) => RestoreFromTray();

        StateChanged += (_, _) =>
        {
            if (WindowState == WindowState.Minimized)
                MinimizeToTray();
        };
        Closed += (_, _) => _trayIcon.Dispose();
        _engine.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(TimerEngine.IsInRun) && _trayResetMenuItem is not null)
                _trayResetMenuItem.Enabled = !_engine.IsInRun;
        };
    }

    private void MinimizeToTray()
    {
        Hide();
        if (_trayIcon is not null)
            _trayIcon.Visible = true;
    }

    private void RestoreFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
        if (_trayIcon is not null)
            _trayIcon.Visible = false;
    }

    public void ShowMinimizedToTray()
    {
        if (_trayIcon is not null)
            _trayIcon.Visible = true;
    }

    private void EditOverlayToggle_Click(object sender, RoutedEventArgs e)
    {
        var isEditing = ((ToggleButton)sender).IsChecked == true;
        _overlay.SetEditMode(isEditing);
    }

    private void ResetRun_Click(object sender, RoutedEventArgs e) => _engine.ResetRun();

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void SoftwareRendering_Changed(object sender, RoutedEventArgs e) =>
        _settings.UseSoftwareRendering = SoftwareRenderingCheckBox.IsChecked == true;

    private void TimerCard_Click(object sender, MouseButtonEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is string tag && Enum.TryParse<PrimaryTimerKind>(tag, out var kind))
            _engine.SelectedPrimaryTimer = kind;
    }

    private void HistoryRow_Click(object sender, MouseButtonEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is int index)
            _engine.SelectedRunHistoryDetailIndex = index;
    }

    private void HistoryRowCompare_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is int index)
            _engine.SetSpecificRunComparisonSource(index);
    }

    private void HistoryRowDelete_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is not int index)
            return;

        var runs = _engine.AllHistoricalRuns;
        if (index < 0 || index >= runs.Count)
            return;

        var dialog = new ConfirmDeleteRunWindow(runs[index].Label, _engine.IsHistoricalRunPersonalBest(index))
        {
            Owner = this,
        };
        if (dialog.ShowDialog() == true)
            _engine.DeleteHistoricalRun(index);
    }

    private void HistoryRowShare_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is not int index)
            return;

        var runs = _engine.AllHistoricalRuns;
        if (index < 0 || index >= runs.Count)
            return;

        var namePrompt = new RunnerNamePromptWindow(_engine.ExportRunnerName) { Owner = this };
        if (namePrompt.ShowDialog() != true)
            return;

        _engine.ExportRunnerName = namePrompt.RunnerName;

        var saveDialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Export Split File",
            Filter = "Abyssplit Split File (*.abysplit)|*.abysplit",
            FileName = _engine.BuildExportFileName(index, namePrompt.RunnerName),
        };
        if (saveDialog.ShowDialog(this) != true)
            return;

        try
        {
            _engine.ExportSplitFile(index, namePrompt.RunnerName, saveDialog.FileName);
        }
        catch (Exception ex)
        {
            AppLog.LogException("Failed to export split file", ex);
            MessageBox.Show(this, "Couldn't save the split file. Check that the location is writable and try again.",
                "Export Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void BrowseImportSplitFile_Click(object sender, RoutedEventArgs e)
    {
        var openDialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Import Split File",
            Filter = "Abyssplit Split File (*.abysplit)|*.abysplit|All files (*.*)|*.*",
        };
        if (openDialog.ShowDialog(this) != true)
            return;

        try
        {
            _engine.ImportSplitFile(openDialog.FileName);
        }
        catch (Exception ex)
        {
            AppLog.LogException("Failed to import split file", ex);
            MessageBox.Show(this, "That file couldn't be read as a split file. It may be corrupted or from an incompatible version.",
                "Import Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ClearImportSplitFile_Click(object sender, RoutedEventArgs e) => _engine.ClearImportedSplitFile();

    private void BrowseBackgroundImage_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Choose an Overlay Background Image",
            Filter = "Image files|*.png;*.jpg;*.jpeg;*.bmp;*.gif",
        };
        if (dialog.ShowDialog(this) == true)
            _engine.OverlayBackgroundImagePath = dialog.FileName;
    }

    private void ClearBackgroundImage_Click(object sender, RoutedEventArgs e) =>
        _engine.OverlayBackgroundImagePath = null;

    private void ColorSwatch_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is not string tag)
            return;

        var parts = tag.Split('|');
        if (parts.Length != 2)
            return;

        switch (parts[0])
        {
            case nameof(TimerEngine.OverlayBiomeTextColor):
                _engine.OverlayBiomeTextColor = parts[1];
                break;
            case nameof(TimerEngine.OverlayDepthTextColor):
                _engine.OverlayDepthTextColor = parts[1];
                break;
            case nameof(TimerEngine.OverlayFloorTextColor):
                _engine.OverlayFloorTextColor = parts[1];
                break;
            case nameof(TimerEngine.OverlayTitleTextColor):
                _engine.OverlayTitleTextColor = parts[1];
                break;
        }
    }

    private void OpenColorPicker_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is not string propertyName)
            return;

        var currentHex = propertyName switch
        {
            nameof(TimerEngine.OverlayBiomeTextColor) => _engine.OverlayBiomeTextColor,
            nameof(TimerEngine.OverlayDepthTextColor) => _engine.OverlayDepthTextColor,
            nameof(TimerEngine.OverlayFloorTextColor) => _engine.OverlayFloorTextColor,
            nameof(TimerEngine.OverlayTitleTextColor) => _engine.OverlayTitleTextColor,
            _ => "#FFFFFF",
        };

        System.Windows.Media.Color seedColor;
        try
        {
            seedColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(currentHex)!;
        }
        catch (FormatException)
        {
            seedColor = System.Windows.Media.Colors.White;
        }

        using var dialog = new System.Windows.Forms.ColorDialog
        {
            Color = System.Drawing.Color.FromArgb(seedColor.R, seedColor.G, seedColor.B),
            FullOpen = true,
        };

        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            return;

        var hex = $"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}";

        switch (propertyName)
        {
            case nameof(TimerEngine.OverlayBiomeTextColor):
                _engine.OverlayBiomeTextColor = hex;
                break;
            case nameof(TimerEngine.OverlayDepthTextColor):
                _engine.OverlayDepthTextColor = hex;
                break;
            case nameof(TimerEngine.OverlayFloorTextColor):
                _engine.OverlayFloorTextColor = hex;
                break;
            case nameof(TimerEngine.OverlayTitleTextColor):
                _engine.OverlayTitleTextColor = hex;
                break;
        }
    }

    private void About_Click(object sender, RoutedEventArgs e) =>
        new AboutWindow(_engine.AppVersionText) { Owner = this }.ShowDialog();

    private void OpenLatestRelease_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo($"{AppInfo.GitHubRepoUrl}/releases/latest") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            AppLog.LogException("Failed to open latest release page from update banner", ex);
        }
    }

    private void DismissUpdateBanner_Click(object sender, RoutedEventArgs e) =>
        UpdateBanner.Visibility = Visibility.Collapsed;

    private void ResetOverlayAppearance_Click(object sender, RoutedEventArgs e) =>
        _engine.ResetOverlayAppearanceToDefaults();

    private void DeletePersonalBest_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ConfirmDeletePersonalBestWindow(_engine.BestRunTimeText, _engine.NextFastestCompletedRunTimeText)
        {
            Owner = this,
        };
        if (dialog.ShowDialog() == true)
            _engine.DeletePersonalBest();
    }

    private void ReportIssue_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(AppLog.CurrentLogFilePath);
        }
        catch (Exception ex)
        {
            AppLog.LogException("Failed to copy log path to clipboard", ex);
        }

        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe")
            {
                ArgumentList = { $"/select,{AppLog.CurrentLogFilePath}" },
            });
        }
        catch (Exception ex)
        {
            AppLog.LogException("Failed to reveal log file in Explorer", ex);
        }

        try
        {
            var logExcerpt = AppLog.ReadTodayForReport(maxChars: 500);
            var body =
                $"**App version:** {_engine.AppVersionText}\n" +
                $"**OS:** {Environment.OSVersion.VersionString}\n" +
                $"**Full log file:** `{AppLog.CurrentLogFilePath}`\n" +
                "(Already copied to your clipboard, and the file is open/selected in Explorer — " +
                "attach it below if the excerpt doesn't have enough detail.)\n\n" +
                "**What happened:**\n\n\n" +
                "**Recent log excerpt:**\n```\n" + logExcerpt + "\n```";

            var url = $"{AppInfo.GitHubRepoUrl}/issues/new?title={Uri.EscapeDataString("Bug report")}&body={Uri.EscapeDataString(body)}";
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            AppLog.LogException("Failed to open Report an Issue page", ex);
        }
    }
}
