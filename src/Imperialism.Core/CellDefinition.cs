namespace Imperialism.Core;

public enum SettlementKind : byte
{
    None,
    Town,
    Capital,
}

public sealed class CellDefinition
{
    private readonly IReadOnlyList<ResourceId> _resources;

    public CellDefinition(
        CellIndex index,
        HexCoord coordinate,
        TerrainId terrain,
        CellRegion region,
        IEnumerable<ResourceId>? resources = null,
        SettlementKind settlement = SettlementKind.None)
    {
        if (!Enum.IsDefined(settlement))
        {
            throw new ArgumentOutOfRangeException(nameof(settlement));
        }

        var resourceArray = resources?.ToArray() ?? [];
        if (resourceArray.Distinct().Count() != resourceArray.Length)
        {
            throw new ArgumentException("A cell cannot list the same resource more than once.", nameof(resources));
        }

        Index = index;
        Coordinate = coordinate;
        Terrain = terrain;
        Region = region;
        Settlement = settlement;
        _resources = Array.AsReadOnly(resourceArray);
    }

    public CellIndex Index { get; }

    public HexCoord Coordinate { get; }

    public TerrainId Terrain { get; }

    public CellRegion Region { get; }

    public IReadOnlyList<ResourceId> Resources => _resources;

    public SettlementKind Settlement { get; }
}
