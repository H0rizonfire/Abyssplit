using System.Text.Json;

namespace AbyssusOverlay.Core;

public sealed class PersonalBests
{
    private readonly string _filePath;
    private Dictionary<string, CategoryData> _categories;
    private string _activeCategory;

    public PersonalBests(string filePath, string initialCategory = "AnyPercent")
    {
        _filePath = filePath;
        _categories = new Dictionary<string, CategoryData>();
        _activeCategory = initialCategory;
        Load();
    }

    public static string DefaultFilePath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AbyssusOverlay",
        "personal_bests.json");

    public void SetActiveCategory(string category) => _activeCategory = category;

    public float? GetBestFloorSegment(int floorNumber) =>
        GetActiveCategory().BestFloorSegments.TryGetValue(floorNumber, out var value) ? value : null;

    public float? BestRunTime => GetActiveCategory().BestRunTime;

    public float? SumOfBest
    {
        get
        {
            var segments = GetActiveCategory().BestFloorSegments;
            return segments.Count > 0 ? segments.Values.Sum() : null;
        }
    }

    public bool TryRecordFloorSegment(int floorNumber, float segmentTime)
    {
        var category = GetActiveCategory();
        if (category.BestFloorSegments.TryGetValue(floorNumber, out var existing) && existing <= segmentTime)
            return false;

        category.BestFloorSegments[floorNumber] = segmentTime;
        Save();
        return true;
    }

    public bool TryRecordRunTime(float runTime)
    {
        var category = GetActiveCategory();
        if (category.BestRunTime is { } existing && existing <= runTime)
            return false;

        category.BestRunTime = runTime;
        Save();
        return true;
    }

    public void SetBestRunTime(float? value)
    {
        GetActiveCategory().BestRunTime = value;
        Save();
    }

    private CategoryData GetActiveCategory()
    {
        if (!_categories.TryGetValue(_activeCategory, out var data))
        {
            data = new CategoryData();
            _categories[_activeCategory] = data;
        }
        return data;
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_filePath))
                return;

            var json = File.ReadAllText(_filePath);
            var data = JsonSerializer.Deserialize<PersonalBestsData>(json);
            _categories = data?.Categories ?? new Dictionary<string, CategoryData>();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            _categories = new Dictionary<string, CategoryData>();
        }
    }

    private void Save()
    {
        try
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var data = new PersonalBestsData { Categories = _categories };
            File.WriteAllText(_filePath, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    private sealed class CategoryData
    {
        public Dictionary<int, float> BestFloorSegments { get; set; } = new();
        public float? BestRunTime { get; set; }
    }

    private sealed class PersonalBestsData
    {
        public Dictionary<string, CategoryData>? Categories { get; set; }
    }
}
