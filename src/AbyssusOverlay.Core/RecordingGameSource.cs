namespace AbyssusOverlay.Core;

public sealed class RecordingGameSource : IGameSource
{
    private readonly IGameSource _inner;
    private readonly GameRecorder _recorder;
    private bool _recordedAttachFailure;
    private bool _isRecordedAsAttached;

    public RecordingGameSource(IGameSource inner, GameRecorder recorder)
    {
        _inner = inner;
        _recorder = recorder;

        _isRecordedAsAttached = inner.IsAttached;
    }

    public string? RecordingPath => _recorder.IsEnabled ? _recorder.FilePath : null;

    public GameRecorder Recorder => _recorder;

    public bool IsAttached => _inner.IsAttached;

    public string? ExecutablePath => _inner.ExecutablePath;

    public GameStore? Store => _inner.Store;

    public string? GameVersion => _inner.GameVersion;

    public bool IsProcessAlive() => _inner.IsProcessAlive();

    public bool TryAttach()
    {
        var attached = _inner.TryAttach();
        if (attached)
        {
            _recordedAttachFailure = false;
            _isRecordedAsAttached = true;
            var store = _inner.Store;
            _recorder.RecordAttach(store, store is { } s ? GameBuildVersion.TryRead(s, _inner.ExecutablePath) : null);
        }
        else if (!_recordedAttachFailure)
        {
            _recordedAttachFailure = true;
            _recorder.RecordAttachFailed();
        }

        return attached;
    }

    public void OnDetached()
    {
        _inner.OnDetached();

        if (_isRecordedAsAttached)
        {
            _isRecordedAsAttached = false;
            _recorder.RecordDetach();
        }
    }

    public bool TryReadFrame(out GameFrame frame)
    {
        if (_inner.TryReadFrame(out frame))
        {
            _recorder.RecordFrame(frame);
            return true;
        }

        _recorder.RecordReadFailed();
        return false;
    }

    public void Dispose()
    {
        _inner.Dispose();
        _recorder.Dispose();
    }
}
