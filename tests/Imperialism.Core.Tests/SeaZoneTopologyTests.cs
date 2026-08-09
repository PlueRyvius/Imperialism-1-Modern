using Imperialism.Core;
using Xunit;

namespace Imperialism.Core.Tests;

public sealed class SeaZoneTopologyTests
{
    [Fact]
    public void TopologyAddsOneReciprocalLinkForAdjacentDistinctZones()
    {
        var map = CreateMap(new MapDimensions(2, 1), wrapsHorizontally: false,
            [new SeaZoneId(0), new SeaZoneId(1)]);

        Assert.Equal([new SeaZoneId(1)], map.SeaTopology.GetNeighbors(new SeaZoneId(0)));
        Assert.Equal([new SeaZoneId(0)], map.SeaTopology.GetNeighbors(new SeaZoneId(1)));
    }

    [Fact]
    public void HorizontalSeamIsExplicitRatherThanAMapSizeRule()
    {
        var dimensions = new MapDimensions(257, 129);
        var zones = Enumerable.Repeat<SeaZoneId?>(null, dimensions.CellCount).ToArray();
        zones[dimensions.GetIndex(new HexCoord(0, 64)).Value] = new SeaZoneId(0);
        zones[dimensions.GetIndex(new HexCoord(256, 64)).Value] = new SeaZoneId(1);

        var withoutSeam = CreateMap(dimensions, wrapsHorizontally: false, zones);
        var withSeam = CreateMap(dimensions, wrapsHorizontally: true, zones);

        Assert.Empty(withoutSeam.SeaTopology.GetNeighbors(new SeaZoneId(0)));
        Assert.Equal([new SeaZoneId(1)], withSeam.SeaTopology.GetNeighbors(new SeaZoneId(0)));
        Assert.Equal([new SeaZoneId(0)], withSeam.SeaTopology.GetNeighbors(new SeaZoneId(1)));
    }

    [Fact]
    public void TopologyPreservesOriginalCellAndDirectionEncounterOrder()
    {
        // From the first cell, the original probes east (zone 2) before
        // south-east (zone 1).  Numeric sorting would incorrectly return 1,2.
        var map = CreateMap(new MapDimensions(2, 2), wrapsHorizontally: false,
            [new SeaZoneId(0), new SeaZoneId(2), new SeaZoneId(1), null]);

        Assert.Equal(
            [new SeaZoneId(2), new SeaZoneId(1)],
            map.SeaTopology.GetNeighbors(new SeaZoneId(0)));
    }

    private static MapDefinition CreateMap(
        MapDimensions dimensions,
        bool wrapsHorizontally,
        IEnumerable<SeaZoneId?> zones)
    {
        var cells = zones.Select((zone, index) => new CellDefinition(
            new CellIndex(index),
            dimensions.GetCoordinate(new CellIndex(index)),
            new TerrainId(0),
            zone is { } value ? CellRegion.ForSeaZone(value) : CellRegion.Unassigned));
        var seaZoneCount = zones
            .Where(static zone => zone.HasValue)
            .Select(static zone => zone!.Value.Value)
            .DefaultIfEmpty(-1)
            .Max() + 1;
        var seaZones = Enumerable.Range(0, seaZoneCount)
            .Select(index => new SeaZoneDefinition(new SeaZoneId(index), $"Zone {index}"));

        return new MapDefinition(
            dimensions,
            cells,
            seaZones: seaZones,
            wrapsHorizontally: wrapsHorizontally);
    }
}
