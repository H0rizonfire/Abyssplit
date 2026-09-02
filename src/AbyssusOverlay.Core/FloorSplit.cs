namespace AbyssusOverlay.Core;

public readonly record struct FloorSplit(int FloorNumber, float SegmentTime, string BiomeName, IReadOnlyList<RoomSplit> Rooms);
