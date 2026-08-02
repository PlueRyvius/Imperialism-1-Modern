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
        string? regionName)
    {
        Definition = definition;
        TerrainKey = terrainKey;
        ResourceKeys = Array.AsReadOnly(resourceKeys.ToArray());
        RegionKey = regionKey;
        RegionName = regionName;
    }

    public CellDefinition Definition { get; }

    public CellIndex Index => Definition.Index;

    public HexCoord Coordinate => Definition.Coordinate;

    public string TerrainKey { get; }

    public IReadOnlyList<string> ResourceKeys { get; }

    public CellRegionKind RegionKind => Definition.Region.Kind;

    public string? RegionKey { get; }

    public string? RegionName { get; }

    public SettlementSiteKind SettlementSite => Definition.SettlementSite;

    public RiverPath? River => Definition.River;
}

public sealed class MapViewDefinition
{
    private readonly IReadOnlyList<MapCellView> _cells;

    private MapViewDefinition(
        string mapKey,
        string mapName,
        MapDimensions dimensions,
        IEnumerable<MapCellView> cells)
    {
        MapKey = mapKey;
        MapName = mapName;
        Dimensions = dimensions;
        _cells = Array.AsReadOnly(cells.ToArray());
    }

    public string MapKey { get; }

    public string MapName { get; }

    public MapDimensions Dimensions { get; }

    public IReadOnlyList<MapCellView> Cells => _cells;

    public MapCellView this[CellIndex index] => Dimensions.Contains(index)
        ? _cells[index.Value]
        : throw new ArgumentOutOfRangeException(nameof(index));

    public static MapViewDefinition Create(CompiledWorldPackage package)
    {
        ArgumentNullException.ThrowIfNull(package);
        var world = package.GetWorld(package.ScenarioKeys[0]);
        var cells = new MapCellView[world.Map.Cells.Count];
        foreach (var cell in world.Map.Cells)
        {
            string? regionKey = null;
            string? regionName = null;
            if (cell.Region.Kind == CellRegionKind.Province)
            {
                var province = cell.Region.Province;
                regionKey = package.Catalog.GetKey(province);
                regionName = world.Map.Provinces[province.Value].Name;
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
                regionName);
        }

        return new MapViewDefinition(
            package.MapKey,
            package.MapName,
            world.Map.Dimensions,
            cells);
    }
}
