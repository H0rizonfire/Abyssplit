using System.Text.Json;

namespace AbyssusOverlay.Core;

public sealed record RunAttempt(
    DateTime Timestamp,
    bool Completed,
    float FinalTime,
    Dictionary<int, float> FloorSegments,
    float? RawFinalTime = null,
    float? LoadFreeFinalTime = null,
    List<WeaponRunResult>? WeaponRuns = null,
    float? BetweenRunsTime = null,
    string? Weapon = null,
    string? GameVersion = null,
    string? GameBuild = null);

public sealed class RunHistory
{
    private readonly string _filePath;
    private Dictionary<string, List<RunAttempt>> _attemptsByCategory;
    private string _activeCategory;

    public RunHistory(string filePath, string initialCategory)
    {
        _filePath = filePath;
        _attemptsByCategory = new Dictionary<string, List<RunAttempt>>();
        _activeCategory = initialCategory;
        Load();
    }

    public void SetActiveCategory(string category) => _activeCategory = category;

    public IReadOnlyList<RunAttempt> GetAttempts() => GetAttempts(_activeCategory);

    public IReadOnlyList<RunAttempt> GetAttempts(string categoryKey) =>
        _attemptsByCategory.TryGetValue(categoryKey, out var list) ? list : Array.Empty<RunAttempt>();

    public void RecordAttempt(RunAttempt attempt) => RecordAttempt(_activeCategory, attempt);

    public void RecordAttempt(string categoryKey, RunAttempt attempt)
    {
        if (!_attemptsByCategory.TryGetValue(categoryKey, out var list))
        {
            list = new List<RunAttempt>();
            _attemptsByCategory[categoryKey] = list;
        }

        list.Add(attempt);
        Save();
    }

    public void DeleteAttempt(int index)
    {
        if (!_attemptsByCategory.TryGetValue(_activeCategory, out var list) || index < 0 || index >= list.Count)
            return;

        list.RemoveAt(index);
        Save();
    }

    public void DeleteAttempts(IEnumerable<int> indices)
    {
        if (!_attemptsByCategory.TryGetValue(_activeCategory, out var list))
            return;

        var removedAny = false;
        foreach (var index in indices.Distinct().OrderByDescending(i => i))
        {
            if (index < 0 || index >= list.Count)
                continue;
            list.RemoveAt(index);
            removedAny = true;
        }

        if (removedAny)
            Save();
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_filePath))
                return;

            var json = File.ReadAllText(_filePath);
            var data = JsonSerializer.Deserialize<RunHistoryData>(json);
            _attemptsByCategory = data?.AttemptsByCategory ?? new Dictionary<string, List<RunAttempt>>();
            MarkImpossibleRecords();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            _attemptsByCategory = new Dictionary<string, List<RunAttempt>>();
        }
    }

    private void MarkImpossibleRecords()
    {
        foreach (var list in _attemptsByCategory.Values)
        {
            for (var i = 0; i < list.Count; i++)
            {
                var attempt = list[i];
                var segments = attempt.FloorSegments ?? new Dictionary<int, float>();
                var badSegments = segments.Where(kv => !PersonalBests.IsPossibleTime(kv.Value)).Select(kv => kv.Key).ToList();
                var impossibleCompletion = attempt.Completed && !PersonalBests.IsPossibleTime(attempt.FinalTime);
                if (badSegments.Count == 0 && !impossibleCompletion && attempt.FloorSegments is not null)
                    continue;

                foreach (var floor in badSegments)
                    segments.Remove(floor);
                list[i] = attempt with { Completed = attempt.Completed && !impossibleCompletion, FloorSegments = segments };
            }
        }
    }

    private void Save()
    {
        try
        {
            var data = new RunHistoryData { AttemptsByCategory = _attemptsByCategory };
            AtomicFile.WriteAllText(_filePath, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    private sealed class RunHistoryData
    {
        public Dictionary<string, List<RunAttempt>>? AttemptsByCategory { get; set; }
    }
}
