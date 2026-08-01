namespace Imperialism.Core;

public enum SettlementSiteKind : byte
{
    None,
    Urban,
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
        SettlementSiteKind settlementSite = SettlementSiteKind.None,
        RiverPath? river = null)
    {
        if (!Enum.IsDefined(settlementSite))
        {
            throw new ArgumentOutOfRangeException(nameof(settlementSite));
        }

        if (river is { } path && !path.IsValid)
        {
            throw new ArgumentException("The river path must join two distinct, defined endpoints.", nameof(river));
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
        SettlementSite = settlementSite;
        River = river;
        _resources = Array.AsReadOnly(resourceArray);
    }

    public CellIndex Index { get; }

    public HexCoord Coordinate { get; }

    public TerrainId Terrain { get; }

    public CellRegion Region { get; }

    public IReadOnlyList<ResourceId> Resources => _resources;

    public SettlementSiteKind SettlementSite { get; }

    public RiverPath? River { get; }
}
