using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AbyssusTimer.App.Engine;
using AbyssusTimer.App.Interop;
using AbyssusTimer.App.Theme;

namespace AbyssusTimer.App.Windows;

public partial class OverlayWindow : Window
{
    private static readonly Brush EditModeBorderBrush = new SolidColorBrush(Palette.TealGlow);

    private bool _editModeEnabled;

    public OverlayWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => ClickThrough.SetEnabled(this, clickThroughEnabled: !_editModeEnabled);

        DataContextChanged += (_, args) =>
        {
            if (args.OldValue is TimerEngine oldEngine)
            {
                oldEngine.SplitGroups.CollectionChanged -= SplitGroups_CollectionChanged;
                oldEngine.PropertyChanged -= Engine_PropertyChanged;
            }
            if (args.NewValue is TimerEngine newEngine)
            {
                newEngine.SplitGroups.CollectionChanged += SplitGroups_CollectionChanged;
                newEngine.PropertyChanged += Engine_PropertyChanged;
                UpdateScale(newEngine);
                UpdateBackground(newEngine);
            }
        };
    }

    private void Engine_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not TimerEngine engine)
            return;

        switch (e.PropertyName)
        {
            case nameof(TimerEngine.OverlayOpacity):
            case nameof(TimerEngine.OverlayBackgroundImagePath):
                UpdateBackground(engine);
                break;
            case nameof(TimerEngine.OverlayScale):
                UpdateScale(engine);
                break;
        }
    }

    private void UpdateScale(TimerEngine engine) =>
        RootBorder.LayoutTransform = new ScaleTransform(engine.OverlayScale, engine.OverlayScale);

    private void UpdateBackground(TimerEngine engine)
    {
        var alpha = (byte)Math.Clamp(engine.OverlayOpacity * 255, 0, 255);
        var path = engine.OverlayBackgroundImagePath;

        if (!string.IsNullOrEmpty(path) && File.Exists(path))
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(path, UriKind.Absolute);
                bitmap.EndInit();
                bitmap.Freeze();
                RootBorder.Background = new ImageBrush(bitmap) { Stretch = Stretch.UniformToFill, Opacity = alpha / 255.0 };
                return;
            }
            catch (Exception ex) when (ex is IOException or NotSupportedException or UriFormatException)
            {
                AppLog.LogException("Overlay background image failed to load — falling back to solid color", ex);
            }
        }

        RootBorder.Background = new SolidColorBrush(Color.FromArgb(alpha, Palette.Background.R, Palette.Background.G, Palette.Background.B));
    }

    private void SplitGroups_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (DataContext is not TimerEngine { SplitListOverflowBehavior: SplitListOverflowBehavior.Scroll })
            return;

        Dispatcher.BeginInvoke(() => SplitsScrollViewer.ScrollToEnd(), System.Windows.Threading.DispatcherPriority.ContextIdle);
    }

    public void SetEditMode(bool enabled)
    {
        _editModeEnabled = enabled;
        ClickThrough.SetEnabled(this, clickThroughEnabled: !enabled);
        RootBorder.BorderBrush = enabled ? EditModeBorderBrush : Brushes.Transparent;
    }

    private void RootBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_editModeEnabled)
            DragMove();
    }
}
