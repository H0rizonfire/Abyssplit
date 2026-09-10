using System.Windows.Threading;

namespace AbyssusTimer.App.Engine;

internal static class DispatcherPump
{
    public static void Drain(Dispatcher dispatcher)
    {
        var frame = new DispatcherFrame();
        dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }
}
