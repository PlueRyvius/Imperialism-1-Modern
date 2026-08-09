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
        return new MapDefinition(
            dimensions,
            cells,
            seaZones:
            [
                new SeaZoneDefinition(new SeaZoneId(0), "West"),
                new SeaZoneDefinition(new SeaZoneId(1), "East"),
            ],
            wrapsHorizontally: wrapsHorizontally);
    }
}
