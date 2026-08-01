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
        SettlementSiteKind settlementSite = SettlementSiteKind.None)
    {
        if (!Enum.IsDefined(settlementSite))
        {
            throw new ArgumentOutOfRangeException(nameof(settlementSite));
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
        _resources = Array.AsReadOnly(resourceArray);
    }

    public CellIndex Index { get; }

    public HexCoord Coordinate { get; }

    public TerrainId Terrain { get; }

    public CellRegion Region { get; }

    public IReadOnlyList<ResourceId> Resources => _resources;

    public SettlementSiteKind SettlementSite { get; }
}
