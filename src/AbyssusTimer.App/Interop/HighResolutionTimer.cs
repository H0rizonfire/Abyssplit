using System.Runtime.InteropServices;

namespace AbyssusTimer.App.Interop;

internal static class HighResolutionTimer
{
    [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
    private static extern uint TimeBeginPeriod(uint uPeriod);

    [DllImport("winmm.dll", EntryPoint = "timeEndPeriod")]
    private static extern uint TimeEndPeriod(uint uPeriod);

    public static void Begin(uint periodMs) => TimeBeginPeriod(periodMs);

    public static void End(uint periodMs) => TimeEndPeriod(periodMs);
}
