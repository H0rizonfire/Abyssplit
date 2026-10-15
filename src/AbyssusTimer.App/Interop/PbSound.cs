using System.IO;
using System.Media;
using System.Windows;
using System.Windows.Media;

namespace AbyssusTimer.App.Interop;

internal static class PbSound
{
    public const string FileFilter = "Audio files|*.wav;*.mp3;*.wma;*.m4a;*.aac|All files|*.*";

    private static MediaPlayer? _player;

    public static void Play(string? path, double volume)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (path is not null && !File.Exists(path))
        {
            AppLog.Log($"PB sound file not found ({path}); played the default sound instead.");
            path = null;
        }
        path ??= DefaultSoundFile();
        if (path is null || dispatcher is null)
        {
            SystemSounds.Asterisk.Play();
            return;
        }

        dispatcher.BeginInvoke(() =>
        {
            try
            {
                if (_player is null)
                {
                    _player = new MediaPlayer();
                    _player.MediaFailed += (_, e) =>
                    {
                        AppLog.LogException("PB sound file couldn't be played; played the default sound instead", e.ErrorException);
                        SystemSounds.Asterisk.Play();
                    };
                }
                _player.Volume = Math.Clamp(volume, 0, 1);
                _player.Open(new Uri(path));
                _player.Play();
            }
            catch (Exception ex)
            {
                AppLog.LogException("PB sound file couldn't be played; played the default sound instead", ex);
                SystemSounds.Asterisk.Play();
            }
        });
    }

    private static string? DefaultSoundFile()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"AppEvents\Schemes\Apps\.Default\SystemAsterisk\.Current");
            var path = key?.GetValue(null) as string;
            if (string.IsNullOrWhiteSpace(path))
                return null;
            path = Environment.ExpandEnvironmentVariables(path);
            return File.Exists(path) ? path : null;
        }
        catch (Exception ex)
        {
            AppLog.LogException("Couldn't find the Windows default sound file", ex);
            return null;
        }
    }
}
