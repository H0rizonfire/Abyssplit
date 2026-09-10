namespace AbyssusTimer.App.Engine;

public sealed record LocationStatsRow(string Label, int Attempts, string BestText, string AverageText, string WorstText, string ConsistencyText);

public sealed record PbMilestoneRow(string DateText, string TimeText, string? ImprovementText, float FinalTimeSeconds);

public sealed record AttemptsReachedRow(string Label, int TotalCount, int CompletedCount);

public sealed record PbChartMarker(double X, double Y, string DateText, string TimeText);
