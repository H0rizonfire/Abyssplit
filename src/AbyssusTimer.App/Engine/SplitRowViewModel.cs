namespace AbyssusTimer.App.Engine;

public sealed record RoomRowViewModel(int RoomNumber, string Label, string TimeText);

public sealed record DepthRowViewModel(string Label, string TimeText, string StatusText, IReadOnlyList<RoomRowViewModel> Rooms, string? DeltaText, DeltaSeverity? DeltaSeverity, bool ShowRoomsInOverlay);

public sealed record BiomeGroupViewModel(string BiomeName, string TotalTimeText, IReadOnlyList<DepthRowViewModel> Depths, bool ShowDepthsInOverlay);
