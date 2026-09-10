using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace AbyssusTimer.App.Interop;

internal sealed class GlobalHotkey : IDisposable
{
    private const int WM_HOTKEY = 0x0312;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(nint hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(nint hWnd, int id);

    private readonly nint _hwnd;
    private readonly int _id;
    private readonly HwndSource _source;
    private readonly Action _onPressed;

    public GlobalHotkey(Window window, int id, ModifierKeys modifiers, Key key, Action onPressed)
    {
        _id = id;
        _onPressed = onPressed;

        var helper = new WindowInteropHelper(window);
        helper.EnsureHandle();
        _hwnd = helper.Handle;

        _source = HwndSource.FromHwnd(_hwnd)!;
        _source.AddHook(WndProc);

        RegisterHotKey(_hwnd, _id, (uint)modifiers, (uint)KeyInterop.VirtualKeyFromKey(key));
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && wParam.ToInt32() == _id)
        {
            _onPressed();
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        UnregisterHotKey(_hwnd, _id);
        _source.RemoveHook(WndProc);
    }
}
