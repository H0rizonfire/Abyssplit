namespace AbyssusOverlay.Core;

public interface IGameSource : IDisposable
{
    bool IsAttached { get; }

    string? ExecutablePath { get; }

    GameStore? Store => null;

    string? GameVersion => null;

    bool IsProcessAlive();

    bool TryAttach();

    void OnDetached();

    bool TryReadFrame(out GameFrame frame);
}
