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

    public bool TryRecordFloorSegment(int floorNumber, float segmentTime) =>
        TryRecordFloorSegment(_activeCategory, floorNumber, segmentTime);

    public bool TryRecordFloorSegment(string categoryKey, int floorNumber, float segmentTime)
    {
        if (!IsPossibleTime(segmentTime))
            return false;

        var category = GetCategory(categoryKey);
        if (category.BestFloorSegments.TryGetValue(floorNumber, out var existing) && existing <= segmentTime)
            return false;

        category.BestFloorSegments[floorNumber] = segmentTime;
        Save();
        return true;
    }

    public bool TryRecordRunTime(float runTime) => TryRecordRunTime(_activeCategory, runTime);

    public bool TryRecordRunTime(string categoryKey, float runTime)
    {
        if (!IsPossibleTime(runTime))
            return false;

        var category = GetCategory(categoryKey);
        if (category.BestRunTime is { } existing && existing <= runTime)
            return false;

        category.BestRunTime = runTime;
        Save();
        return true;
    }

    public float? GetBestWeaponTime(string weaponAssetName) =>
        GetActiveCategory().BestWeaponTimes.TryGetValue(weaponAssetName, out var value) ? value : null;

    public bool TryRecordWeaponTime(string weaponAssetName, float runTime)
    {
        if (!IsPossibleTime(runTime))
            return false;

        var category = GetActiveCategory();
        if (category.BestWeaponTimes.TryGetValue(weaponAssetName, out var existing) && existing <= runTime)
            return false;

        category.BestWeaponTimes[weaponAssetName] = runTime;
        Save();
        return true;
    }

    public void SetBestRunTime(float? value)
    {
        GetActiveCategory().BestRunTime = value is { } time && IsPossibleTime(time) ? time : null;
        Save();
    }

    public static bool IsPossibleTime(float seconds) => seconds > 0f && float.IsFinite(seconds);

    public IReadOnlyCollection<string> CategoriesWithDroppedRunTime => _categoriesWithDroppedRunTime;
    private readonly List<string> _categoriesWithDroppedRunTime = new();

    public void SetBestRunTime(string categoryKey, float? value)
    {
        GetCategory(categoryKey).BestRunTime = value is { } time && IsPossibleTime(time) ? time : null;
        Save();
    }

    private CategoryData GetActiveCategory() => GetCategory(_activeCategory);

    private CategoryData GetCategory(string categoryKey)
    {
        if (!_categories.TryGetValue(categoryKey, out var data))
        {
            data = new CategoryData();
            _categories[categoryKey] = data;
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
            DropImpossibleValues();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            _categories = new Dictionary<string, CategoryData>();
        }
    }

    private void DropImpossibleValues()
    {
        foreach (var (key, category) in _categories)
        {
            if (category.BestRunTime is { } best && !IsPossibleTime(best))
            {
                category.BestRunTime = null;
                _categoriesWithDroppedRunTime.Add(key);
            }

            category.BestFloorSegments ??= new();
            category.BestWeaponTimes ??= new();
            foreach (var floor in category.BestFloorSegments.Where(kv => !IsPossibleTime(kv.Value)).Select(kv => kv.Key).ToList())
                category.BestFloorSegments.Remove(floor);
            foreach (var weapon in category.BestWeaponTimes.Where(kv => !IsPossibleTime(kv.Value)).Select(kv => kv.Key).ToList())
                category.BestWeaponTimes.Remove(weapon);
        }
    }

    private void Save()
    {
        try
        {
            var data = new PersonalBestsData { Categories = _categories };
            AtomicFile.WriteAllText(_filePath, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    private sealed class CategoryData
    {
        public Dictionary<int, float> BestFloorSegments { get; set; } = new();
        public float? BestRunTime { get; set; }

        public Dictionary<string, float> BestWeaponTimes { get; set; } = new();
    }

    private sealed class PersonalBestsData
    {
        public Dictionary<string, CategoryData>? Categories { get; set; }
    }
}
