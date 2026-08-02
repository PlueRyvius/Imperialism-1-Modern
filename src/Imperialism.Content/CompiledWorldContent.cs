using Imperialism.Core;

namespace Imperialism.Content;

public sealed class CompiledWorldContent
{
    public CompiledWorldContent(WorldDefinition world, WorldContentCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(catalog);
        World = world;
        Catalog = catalog;
    }

    public WorldDefinition World { get; }

    public WorldContentCatalog Catalog { get; }
}

public sealed class CompiledWorldPackage
{
    private readonly IReadOnlyList<string> _scenarioKeys;
    private readonly IReadOnlyDictionary<string, WorldDefinition> _worlds;

    internal CompiledWorldPackage(
        string mapKey,
        string mapName,
        WorldContentCatalog catalog,
        IEnumerable<(string Key, WorldDefinition World)> worlds)
    {
        MapKey = mapKey;
        MapName = mapName;
        Catalog = catalog;
        var worldArray = worlds.ToArray();
        _scenarioKeys = Array.AsReadOnly(worldArray.Select(static item => item.Key).ToArray());
        _worlds = new System.Collections.ObjectModel.ReadOnlyDictionary<string, WorldDefinition>(
            worldArray.ToDictionary(
                static item => item.Key,
                static item => item.World,
                StringComparer.Ordinal));
    }

    public string MapKey { get; }

    public string MapName { get; }

    public WorldContentCatalog Catalog { get; }

    public IReadOnlyList<string> ScenarioKeys => _scenarioKeys;

    public WorldDefinition GetWorld(string scenarioKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scenarioKey);
        return _worlds.TryGetValue(scenarioKey, out var world)
            ? world
            : throw new KeyNotFoundException($"Unknown scenario key '{scenarioKey}'.");
    }
}

public sealed class WorldContentCatalog
{
    private readonly IReadOnlyList<string> _terrainKeys;
    private readonly IReadOnlyList<string> _resourceKeys;
    private readonly IReadOnlyList<string> _commodityKeys;
    private readonly IReadOnlyList<string> _provinceKeys;
    private readonly IReadOnlyList<string> _seaZoneKeys;
    private readonly IReadOnlyList<string> _countryKeys;
    private readonly IReadOnlyDictionary<string, int> _terrainIds;
    private readonly IReadOnlyDictionary<string, int> _resourceIds;
    private readonly IReadOnlyDictionary<string, int> _commodityIds;
    private readonly IReadOnlyDictionary<string, int> _provinceIds;
    private readonly IReadOnlyDictionary<string, int> _seaZoneIds;
    private readonly IReadOnlyDictionary<string, int> _countryIds;

    internal WorldContentCatalog(
        IEnumerable<string> terrainKeys,
        IEnumerable<string> resourceKeys,
        IEnumerable<string> commodityKeys,
        IEnumerable<string> provinceKeys,
        IEnumerable<string> seaZoneKeys,
        IEnumerable<string> countryKeys)
    {
        _terrainKeys = Freeze(terrainKeys);
        _resourceKeys = Freeze(resourceKeys);
        _commodityKeys = Freeze(commodityKeys);
        _provinceKeys = Freeze(provinceKeys);
        _seaZoneKeys = Freeze(seaZoneKeys);
        _countryKeys = Freeze(countryKeys);
        _terrainIds = Index(_terrainKeys);
        _resourceIds = Index(_resourceKeys);
        _commodityIds = Index(_commodityKeys);
        _provinceIds = Index(_provinceKeys);
        _seaZoneIds = Index(_seaZoneKeys);
        _countryIds = Index(_countryKeys);
    }

    public IReadOnlyList<string> TerrainKeys => _terrainKeys;

    public IReadOnlyList<string> ResourceKeys => _resourceKeys;

    public IReadOnlyList<string> CommodityKeys => _commodityKeys;

    public IReadOnlyList<string> ProvinceKeys => _provinceKeys;

    public IReadOnlyList<string> SeaZoneKeys => _seaZoneKeys;

    public IReadOnlyList<string> CountryKeys => _countryKeys;

    public string GetKey(TerrainId id) => Get(_terrainKeys, id.Value, nameof(id));

    public string GetKey(ResourceId id) => Get(_resourceKeys, id.Value, nameof(id));

    public string GetKey(CommodityId id) => Get(_commodityKeys, id.Value, nameof(id));

    public string GetKey(ProvinceId id) => Get(_provinceKeys, id.Value, nameof(id));

    public string GetKey(SeaZoneId id) => Get(_seaZoneKeys, id.Value, nameof(id));

    public string GetKey(CountryId id) => Get(_countryKeys, id.Value, nameof(id));

    public TerrainId GetTerrainId(string key) => new(Get(_terrainIds, key));

    public ResourceId GetResourceId(string key) => new(Get(_resourceIds, key));

    public CommodityId GetCommodityId(string key) => new(Get(_commodityIds, key));

    public ProvinceId GetProvinceId(string key) => new(Get(_provinceIds, key));

    public SeaZoneId GetSeaZoneId(string key) => new(Get(_seaZoneIds, key));

    public CountryId GetCountryId(string key) => new(Get(_countryIds, key));

    private static IReadOnlyList<string> Freeze(IEnumerable<string> values) =>
        Array.AsReadOnly(values.ToArray());

    private static IReadOnlyDictionary<string, int> Index(IReadOnlyList<string> values) =>
        new System.Collections.ObjectModel.ReadOnlyDictionary<string, int>(
            values.Select(static (key, index) => (key, index))
                .ToDictionary(static item => item.key, static item => item.index, StringComparer.Ordinal));

    private static string Get(IReadOnlyList<string> values, int index, string parameterName) =>
        (uint)index < (uint)values.Count
            ? values[index]
            : throw new ArgumentOutOfRangeException(parameterName);

    private static int Get(IReadOnlyDictionary<string, int> values, string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return values.TryGetValue(key, out var value)
            ? value
            : throw new KeyNotFoundException($"Unknown content key '{key}'.");
    }
}
