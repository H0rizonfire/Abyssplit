namespace AbyssusOverlay.Core;

public interface IGameSource : IDisposable
{
    bool IsAttached { get; }

    string? ExecutablePath { get; }

    bool IsProcessAlive();

    bool TryAttach();

    void OnDetached();

    bool TryReadFrame(out GameFrame frame);
}
