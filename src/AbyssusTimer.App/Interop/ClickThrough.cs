using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace AbyssusTimer.App.Interop;

internal static class ClickThrough
{
    private const int GWL_EXSTYLE = -20;
    private const long WS_EX_LAYERED = 0x00080000;
    private const long WS_EX_TRANSPARENT = 0x00000020;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint GetWindowLongPtr(nint hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SetWindowLongPtr(nint hWnd, int nIndex, nint dwNewLong);

    public static void SetEnabled(Window window, bool clickThroughEnabled)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == 0)
            return;

        var exStyle = (long)GetWindowLongPtr(hwnd, GWL_EXSTYLE);

        exStyle = clickThroughEnabled
            ? exStyle | WS_EX_TRANSPARENT | WS_EX_LAYERED
            : exStyle & ~WS_EX_TRANSPARENT;

        SetWindowLongPtr(hwnd, GWL_EXSTYLE, (nint)exStyle);
    }
}
