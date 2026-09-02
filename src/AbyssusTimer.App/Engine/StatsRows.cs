namespace AbyssusTimer.App.Engine;

public sealed record LocationStatsRow(string Label, int Attempts, string BestText, string AverageText, string WorstText, string ConsistencyText);

public sealed record PbMilestoneRow(string DateText, string TimeText, string? ImprovementText);

public sealed record AttemptsReachedRow(string Label, int TotalCount, int CompletedCount);
