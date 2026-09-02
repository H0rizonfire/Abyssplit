using System.Text.Json;

namespace AbyssusOverlay.Core;

public sealed record RunAttempt(DateTime Timestamp, bool Completed, float FinalTime, Dictionary<int, float> FloorSegments, float? RawFinalTime = null, float? LoadFreeFinalTime = null);

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

    public IReadOnlyList<RunAttempt> GetAttempts() =>
        _attemptsByCategory.TryGetValue(_activeCategory, out var list) ? list : Array.Empty<RunAttempt>();

    public void RecordAttempt(RunAttempt attempt)
    {
        if (!_attemptsByCategory.TryGetValue(_activeCategory, out var list))
        {
            list = new List<RunAttempt>();
            _attemptsByCategory[_activeCategory] = list;
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
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            _attemptsByCategory = new Dictionary<string, List<RunAttempt>>();
        }
    }

    private void Save()
    {
        try
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var data = new RunHistoryData { AttemptsByCategory = _attemptsByCategory };
            File.WriteAllText(_filePath, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
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
