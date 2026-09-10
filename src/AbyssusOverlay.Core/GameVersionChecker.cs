using System.Diagnostics;

namespace AbyssusOverlay.Core;

public static class GameVersionChecker
{
    public static bool? TryCheckVersion(string? exePath, string? lastVerifiedVersion)
    {
        if (string.IsNullOrEmpty(exePath) || string.IsNullOrEmpty(lastVerifiedVersion))
            return null;

        try
        {
            var info = FileVersionInfo.GetVersionInfo(exePath);
            var actual = info.ProductVersion ?? info.FileVersion;
            return string.IsNullOrWhiteSpace(actual) ? null : actual.Trim() == lastVerifiedVersion;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
