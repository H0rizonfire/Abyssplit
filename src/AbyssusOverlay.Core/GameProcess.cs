using System.Diagnostics;
using System.Runtime.InteropServices;

namespace AbyssusOverlay.Core;

public sealed class GameProcess : IDisposable
{
    private const int ProcessVmRead = 0x0010;
    private const int ProcessQueryInformation = 0x0400;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ReadProcessMemory(nint hProcess, nint lpBaseAddress, byte[] lpBuffer, int dwSize, out int lpNumberOfBytesRead);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(nint hObject);

    private nint _handle;
    private int _processId;

    public nint ModuleBase { get; private set; }
    public bool IsAttached => _handle != 0;

    public bool TryAttach(string processName)
    {
        Detach();

        var shortName = processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? processName[..^4]
            : processName;

        var candidates = Process.GetProcessesByName(shortName);
        if (candidates.Length == 0)
            return false;

        using var process = candidates[0];
        var mainModule = process.MainModule;
        if (mainModule is null)
            return false;

        _processId = process.Id;
        _handle = OpenProcess(ProcessVmRead | ProcessQueryInformation, false, _processId);
        if (_handle == 0)
            return false;

        ModuleBase = mainModule.BaseAddress;
        return true;
    }

    public void Detach()
    {
        if (_handle != 0)
        {
            CloseHandle(_handle);
            _handle = 0;
        }

        ModuleBase = 0;
    }

    public bool IsProcessAlive()
    {
        if (!IsAttached)
            return false;

        try
        {
            using var process = Process.GetProcessById(_processId);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    public bool TryReadBytes(nint address, byte[] buffer)
    {
        if (!IsAttached || address == 0)
            return false;

        return ReadProcessMemory(_handle, address, buffer, buffer.Length, out var bytesRead) && bytesRead == buffer.Length;
    }

    public bool TryReadPointer(nint address, out nint value)
    {
        var buffer = new byte[8];
        if (!TryReadBytes(address, buffer))
        {
            value = 0;
            return false;
        }

        value = (nint)BitConverter.ToInt64(buffer, 0);
        return true;
    }

    public bool TryReadInt32(nint address, out int value)
    {
        var buffer = new byte[4];
        if (!TryReadBytes(address, buffer))
        {
            value = 0;
            return false;
        }

        value = BitConverter.ToInt32(buffer, 0);
        return true;
    }

    public bool TryReadFloat(nint address, out float value)
    {
        var buffer = new byte[4];
        if (!TryReadBytes(address, buffer))
        {
            value = 0f;
            return false;
        }

        value = BitConverter.ToSingle(buffer, 0);
        return true;
    }

    public bool TryReadBool(nint address, out bool value)
    {
        var buffer = new byte[1];
        if (!TryReadBytes(address, buffer))
        {
            value = false;
            return false;
        }

        value = buffer[0] != 0;
        return true;
    }

    public bool TryReadFName(nint address, out int comparisonIndex, out int number)
    {
        var buffer = new byte[8];
        if (!TryReadBytes(address, buffer))
        {
            comparisonIndex = 0;
            number = 0;
            return false;
        }

        comparisonIndex = BitConverter.ToInt32(buffer, 0);
        number = BitConverter.ToInt32(buffer, 4);
        return true;
    }

    public bool TryReadArrayHeader(nint address, out nint data, out int count)
    {
        if (!TryReadPointer(address, out data))
        {
            count = 0;
            return false;
        }

        var countBuffer = new byte[4];
        if (!TryReadBytes(address + 8, countBuffer))
        {
            count = 0;
            return false;
        }

        count = BitConverter.ToInt32(countBuffer, 0);
        return true;
    }

    public void Dispose() => Detach();
}
