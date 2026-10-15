using System.Text;
using System.Text.RegularExpressions;

namespace AbyssusOverlay.Core;

public static partial class GameProjectVersion
{
    private const string SettingsClassName = "GeneralProjectSettings";
    private const int ProjectVersionOffset = 0xB8;
    private const int MaxLength = 64;

    public static string? TryRead(GameProcess process, GUObjectArrayReader objects, NamePool names)
    {
        try
        {
            foreach (var obj in objects.EnumerateObjects())
            {
                if (!objects.IsClassDefaultObject(obj) || !objects.TryGetClassName(obj, names, out var className) || className != SettingsClassName)
                    continue;

                return TryReadFString(process, obj + ProjectVersionOffset) is { } text && LooksLikeVersion().IsMatch(text) ? text : null;
            }
            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string? TryReadFString(GameProcess process, nint address)
    {
        if (!process.TryReadArrayHeader(address, out var data, out var count) || data == 0 || count <= 1 || count > MaxLength)
            return null;

        var buffer = new byte[count * 2];
        return process.TryReadBytes(data, buffer) ? Encoding.Unicode.GetString(buffer).TrimEnd('\0').Trim() : null;
    }

    [GeneratedRegex(@"^\d+(\.\d+){1,3}$")]
    private static partial Regex LooksLikeVersion();
}
