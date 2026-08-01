using Imperialism.Content;
using Imperialism.Core;

namespace Imperialism.Presentation;

public sealed class MapCellView
{
    internal MapCellView(
        CellDefinition definition,
        string terrainKey,
        IEnumerable<string> resourceKeys,
        string? regionKey,
        string? regionName,
        string? ownerKey,
        string? ownerName,
        CountryId? capitalCountry)
    {
        Definition = definition;
        TerrainKey = terrainKey;
        ResourceKeys = Array.AsReadOnly(resourceKeys.ToArray());
        RegionKey = regionKey;
        RegionName = regionName;
        OwnerKey = ownerKey;
        OwnerName = ownerName;
        CapitalCountry = capitalCountry;
    }

    public CellDefinition Definition { get; }

    public CellIndex Index => Definition.Index;

    public HexCoord Coordinate => Definition.Coordinate;

    public string TerrainKey { get; }

    public IReadOnlyList<string> ResourceKeys { get; }

    public CellRegionKind RegionKind => Definition.Region.Kind;

    public string? RegionKey { get; }

    public string? RegionName { get; }

    public string? OwnerKey { get; }

    public string? OwnerName { get; }

    public SettlementSiteKind SettlementSite => Definition.SettlementSite;

    public RiverPath? River => Definition.River;

    public CountryId? CapitalCountry { get; }
}

public sealed class MapViewSnapshot
{
    private readonly IReadOnlyList<MapCellView> _cells;
    private readonly IReadOnlyList<CellLink> _rails;

    private MapViewSnapshot(
        string mapKey,
        string mapName,
        string scenarioKey,
        WorldDefinition world,
        IEnumerable<MapCellView> cells)
    {
        MapKey = mapKey;
        MapName = mapName;
        ScenarioKey = scenarioKey;
        ScenarioName = world.Scenario.Name;
        StartingYear = world.Scenario.StartingYear;
        Dimensions = world.Map.Dimensions;
        _cells = Array.AsReadOnly(cells.ToArray());
        _rails = Array.AsReadOnly(world.Scenario.InitialRailLinks.ToArray());
    }

    public string MapKey { get; }

    public string MapName { get; }

    public string ScenarioKey { get; }

    public string ScenarioName { get; }

    public int StartingYear { get; }

    public MapDimensions Dimensions { get; }

    public IReadOnlyList<MapCellView> Cells => _cells;

    public IReadOnlyList<CellLink> Rails => _rails;

    public MapCellView this[CellIndex index] => Dimensions.Contains(index)
        ? _cells[index.Value]
        : throw new ArgumentOutOfRangeException(nameof(index));

    public static MapViewSnapshot Create(
        CompiledWorldPackage package,
        string scenarioKey)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentException.ThrowIfNullOrWhiteSpace(scenarioKey);
        var world = package.GetWorld(scenarioKey);
        var capitalCountries = world.Scenario.InitialCountryCapitals
            .ToDictionary(static capital => capital.Cell, static capital => capital.Country);

        var cells = new MapCellView[world.Map.Cells.Count];
        foreach (var cell in world.Map.Cells)
        {
            string? regionKey = null;
            string? regionName = null;
            CountryId? owner = null;
            if (cell.Region.Kind == CellRegionKind.Province)
            {
                var province = cell.Region.Province;
                regionKey = package.Catalog.GetKey(province);
                regionName = world.Map.Provinces[province.Value].Name;
                owner = world.Scenario.InitialProvinceOwners[province.Value];
            }
            else if (cell.Region.Kind == CellRegionKind.SeaZone)
            {
                var seaZone = cell.Region.SeaZone;
                regionKey = package.Catalog.GetKey(seaZone);
                regionName = world.Map.SeaZones[seaZone.Value].Name;
            }

            cells[cell.Index.Value] = new MapCellView(
                cell,
                package.Catalog.GetKey(cell.Terrain),
                cell.Resources.Select(package.Catalog.GetKey),
                regionKey,
                regionName,
                owner.HasValue ? package.Catalog.GetKey(owner.Value) : null,
                owner.HasValue ? world.Countries[owner.Value.Value].Name : null,
                capitalCountries.TryGetValue(cell.Index, out var capitalCountry)
                    ? capitalCountry
                    : null);
        }

        return new MapViewSnapshot(
            package.MapKey,
            package.MapName,
            scenarioKey,
            world,
            cells);
    }
}
