namespace AbyssusOverlay.Core;

public sealed class RunSession
{
    private bool _wasInRun;
    private bool _wasRunSuccessful;
    private int _lastFloor;
    private int _lastRoom;
    private float _lastFloorSplitTime;
    private float _lastRoomSplitTime;
    private string _lastKnownBiomeName = "-";
    private List<RoomSplit> _currentFloorRooms = new();
    private readonly List<FloorSplit> _splits = new();

    public IReadOnlyList<FloorSplit> Splits => _splits;
    public IReadOnlyList<RoomSplit> CurrentFloorRooms => _currentFloorRooms;
    public bool IsRunActive { get; private set; }
    public float LastFloorSplitTime => _lastFloorSplitTime;
    public float LastRoomSplitTime => _lastRoomSplitTime;

    public event Action? RunStarted;
    public event Action<RoomSplit>? RoomSplitOccurred;
    public event Action<FloorSplit>? FloorSplitOccurred;

    public event Action<float>? RunEnded;

    public event Action<float>? RunCompleted;

    public void Update(bool isInRun, bool runSuccessful, int currentFloor, int currentRoom, float loadFreeTime, string currentBiomeName)
    {
        if (isInRun && !_wasInRun)
        {
            _splits.Clear();
            _currentFloorRooms = new List<RoomSplit>();
            _lastFloor = currentFloor;
            _lastRoom = currentRoom;
            _lastFloorSplitTime = 0f;
            _lastRoomSplitTime = 0f;
            _wasRunSuccessful = false;
            IsRunActive = true;
            RunStarted?.Invoke();
        }
        else if (!isInRun && _wasInRun)
        {
            IsRunActive = false;
            RunEnded?.Invoke(loadFreeTime);
        }

        if (runSuccessful && !_wasRunSuccessful)
            RunCompleted?.Invoke(loadFreeTime);

        _wasRunSuccessful = runSuccessful;

        if (isInRun && currentRoom != _lastRoom)
        {
            var roomSegment = loadFreeTime - _lastRoomSplitTime;
            var roomSplit = new RoomSplit(_lastRoom, roomSegment);
            _currentFloorRooms.Add(roomSplit);
            RoomSplitOccurred?.Invoke(roomSplit);

            _lastRoom = currentRoom;
            _lastRoomSplitTime = loadFreeTime;
        }

        if (isInRun && currentFloor != _lastFloor)
        {
            var floorSegment = loadFreeTime - _lastFloorSplitTime;
            var floorSplit = new FloorSplit(_lastFloor, floorSegment, _lastKnownBiomeName, _currentFloorRooms);
            _splits.Add(floorSplit);
            FloorSplitOccurred?.Invoke(floorSplit);

            _currentFloorRooms = new List<RoomSplit>();
            _lastFloor = currentFloor;
            _lastFloorSplitTime = loadFreeTime;
            _lastRoomSplitTime = loadFreeTime;
        }

        _lastKnownBiomeName = currentBiomeName;
        _wasInRun = isInRun;
    }

    public void ForceFloorSplit(float loadFreeTime, string currentBiomeName)
    {
        if (!IsRunActive)
            return;

        var floorSegment = loadFreeTime - _lastFloorSplitTime;
        var floorSplit = new FloorSplit(_lastFloor, floorSegment, currentBiomeName, _currentFloorRooms);
        _splits.Add(floorSplit);
        FloorSplitOccurred?.Invoke(floorSplit);

        _currentFloorRooms = new List<RoomSplit>();
        _lastFloorSplitTime = loadFreeTime;
        _lastRoomSplitTime = loadFreeTime;
    }

    public void ForceReset(int currentFloor, int currentRoom)
    {
        _splits.Clear();
        _currentFloorRooms = new List<RoomSplit>();
        _lastFloor = currentFloor;
        _lastRoom = currentRoom;
        _lastFloorSplitTime = 0f;
        _lastRoomSplitTime = 0f;
        _lastKnownBiomeName = "-";
        IsRunActive = true;
        _wasInRun = true;
        RunStarted?.Invoke();
    }
}
