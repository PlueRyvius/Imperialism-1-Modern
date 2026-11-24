using Imperialism.Core;
using Xunit;

namespace Imperialism.Core.Tests;

/// <summary>
/// Every hex walk asks the map for a neighbour, and the map alone decides
/// whether the east and west edges touch. A rule that wrapped on its own would
/// let a port on a seamless map reach a sea on the far side of the world.
/// </summary>
/// <remarks>
/// Port access is measured through the deposit on the port's own tile: with a
/// catchment radius of zero, that tile is collected only if the port itself is
/// a collection point, so the count moves exactly with whether access was
/// granted. Fishing-port counts cannot discriminate here because fishing water
/// is counted on the unwrapped grid either way.
/// </remarks>
public sealed class MapSeamTests
{
    private const int Fish = 0;

    [Fact]
    public void ANeighbourAcrossTheWestEdgeExistsOnlyWhereTheMapHasASeam()
    {
        var withoutSeam = CreateState(wrapsHorizontally: false).Definition.Map;
        var withSeam = CreateState(wrapsHorizontally: true).Definition.Map;

        Assert.False(withoutSeam.TryGetNeighbor(new CellIndex(0), HexDirection.West, out _));
        Assert.True(withSeam.TryGetNeighbor(new CellIndex(0), HexDirection.West, out var across));
        Assert.Equal(new CellIndex(3), across);

        // An interior neighbour is the same either way.
        Assert.True(withoutSeam.TryGetNeighbor(new CellIndex(1), HexDirection.West, out var inside));
        Assert.Equal(new CellIndex(0), inside);
    }

    [Theory]
    [InlineData(true, 1, 0)]
    [InlineData(false, 0, 1)]
    public void ARiverPortReachesTheSeaAcrossTheEdgeOnlyWhereTheMapHasASeam(
        bool wrapsHorizontally,
        int expectedCollectedCells,
        int expectedStrandedCells)
    {
        // Cell 0 is a river port whose only sea is cell 3, on the far side of
        // the west edge. Its river has no mouth, so the seam is its only route.
        var state = CreateState(wrapsHorizontally);

        var extraction = Assert.Single(
            TurnResolver.Resolve(state, TurnOrders.Empty(1), 0)
                .Events.OfType<ResourceExtractedEvent>());

        Assert.Equal(expectedCollectedCells, extraction.CollectedCellCount);
        Assert.Equal(expectedStrandedCells, extraction.StrandedCellCount);
    }

    [Theory]
    [InlineData(true, 2)]
    [InlineData(false, 1)]
    public void APortFishesWaterAcrossTheEdgeOnlyWhereTheMapHasASeam(
        bool wrapsHorizontally,
        int expectedFishingPorts)
    {
        // The capital fishes the river on cell 0 either way. The port on cell 0
        // has no water on its own side, so it fishes only if the sea on cell 3
        // counts as its neighbour.
        var state = CreateState(wrapsHorizontally);

        var extraction = Assert.Single(
            TurnResolver.Resolve(state, TurnOrders.Empty(1), 0)
                .Events.OfType<ResourceExtractedEvent>());

        Assert.Equal(expectedFishingPorts, extraction.FishingPortCount);
        Assert.Equal(0, extraction.StrandedPortCount);
    }

    [Theory]
    [InlineData(true, 1, 0)]
    [InlineData(false, 0, 1)]
    public void ACatchmentReachesAcrossTheEdgeOnlyWhereTheMapHasASeam(
        bool wrapsHorizontally,
        int expectedCollectedCells,
        int expectedStrandedCells)
    {
        // The capital sits on column 0 with a radius of one; the only deposit
        // is on the last column, one step west across the edge.
        var state = CreateCatchmentState(wrapsHorizontally);

        var extraction = Assert.Single(
            TurnResolver.Resolve(state, TurnOrders.Empty(1), 0)
                .Events.OfType<ResourceExtractedEvent>());

        Assert.Equal(expectedCollectedCells, extraction.CollectedCellCount);
        Assert.Equal(expectedStrandedCells, extraction.StrandedCellCount);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ARailLinkAcrossTheEdgeIsAdjacentOnlyWhereTheMapHasASeam(bool wrapsHorizontally)
    {
        // Cells 0 and 3 are the first and last columns of a 4x1 map: one step
        // apart across the seam, three along the row.
        var state = CreateCatchmentState(wrapsHorizontally);
        var across = new CellLink(new CellIndex(0), new CellIndex(3));

        Assert.Equal(wrapsHorizontally, state.Definition.Map.AreAdjacent(across.First, across.Second));
        if (wrapsHorizontally)
        {
            Assert.True(state.BuildRail(across));
            Assert.True(state.HasRail(across));
        }
        else
        {
            Assert.Throws<ArgumentException>(() => state.BuildRail(across));
        }
    }

    [Theory]
    [InlineData(true, null)]
    [InlineData(false, CivilianOrderRefusal.RailNeedsAnAdjacentTile)]
    public void AnEngineerLaysRailAcrossTheEdgeOnlyWhereTheMapHasASeam(
        bool wrapsHorizontally,
        CivilianOrderRefusal? expectedRefusal)
    {
        // The Engineer stands on cell 0; cell 3 is its west neighbour only
        // across the seam.
        var state = CreateEngineerState(wrapsHorizontally, seaAtLastColumn: false);

        var ordered = ResolveBuild(state, 3, EngineerConstruction.Rail);
        _ = TurnResolver.Resolve(state, TurnOrders.Empty(1), 0);

        Assert.Equal(
            expectedRefusal,
            ordered.Events.OfType<CivilianOrderRefusedEvent>().SingleOrDefault()?.Reason);
        Assert.Equal(
            wrapsHorizontally,
            state.HasRail(new CellLink(new CellIndex(0), new CellIndex(3))));
    }

    [Theory]
    [InlineData(true, null)]
    [InlineData(false, CivilianOrderRefusal.PortNeedsWater)]
    public void AnEngineerBuildsAPortOnWaterAcrossTheEdgeOnlyWhereTheMapHasASeam(
        bool wrapsHorizontally,
        CivilianOrderRefusal? expectedRefusal)
    {
        // Cell 0 has no river and no water on its own side; the sea on cell 3
        // is its neighbour only across the seam.
        var state = CreateEngineerState(wrapsHorizontally, seaAtLastColumn: true);

        var ordered = ResolveBuild(state, 0, EngineerConstruction.Port);
        _ = TurnResolver.Resolve(state, TurnOrders.Empty(1), 0);

        Assert.Equal(
            expectedRefusal,
            ordered.Events.OfType<CivilianOrderRefusedEvent>().SingleOrDefault()?.Reason);
        Assert.Equal(wrapsHorizontally, state.HasPort(new CellIndex(0)));
    }

    [Fact]
    public void SeaTopologyNeighbourListsCannotBeWrittenThrough()
    {
        var map = CreateState(wrapsHorizontally: true).Definition.Map;
        var neighbours = map.SeaTopology.GetNeighbors(new SeaZoneId(0));

        Assert.IsNotType<SeaZoneId[]>(neighbours);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<SeaZoneId>)neighbours).Add(new SeaZoneId(0)));
    }

    private static WorldState CreateState(bool wrapsHorizontally)
    {
        const int width = 4;
        var dimensions = new MapDimensions(width, 1);
        var cells = new CellDefinition[width];
        for (var index = 0; index < width; index++)
        {
            cells[index] = new CellDefinition(
                new CellIndex(index),
                dimensions.GetCoordinate(new CellIndex(index)),
                new TerrainId(0),
                index == 3
                    ? CellRegion.ForSeaZone(new SeaZoneId(0))
                    : CellRegion.ForProvince(new ProvinceId(index)),
                index == 0 ? [new ResourceId(0)] : null,
                index == 1 ? SettlementSiteKind.Urban : SettlementSiteKind.None,
                index == 0
                    ? new RiverPath(RiverEndpoint.EastUpper, RiverEndpoint.Source)
                    : null);
        }

        var map = new MapDefinition(
            dimensions,
            cells,
            Enumerable.Range(0, 3)
                .Select(static index => new ProvinceDefinition(new ProvinceId(index), $"Province {index}")),
            [new SeaZoneDefinition(new SeaZoneId(0), "Far Sea")],
            [new ResourceDefinition(new ResourceId(0), new CommodityId(Fish), [1])],
            wrapsHorizontally: wrapsHorizontally);
        var scenario = new ScenarioDefinition(
            "Seam",
            1815,
            [new CountryId(0), new CountryId(0), new CountryId(0)],
            null,
            [new CountryCapital(new CountryId(0), new CellIndex(1))],
            null,
            null,
            null,
            null,
            [new CellIndex(0)]);
        var definition = new WorldDefinition(
            map,
            [new CountryDefinition(new CountryId(0), "Country 0")],
            scenario,
            [new CommodityDefinition(new CommodityId(Fish), "Fish", CommodityCategory.Raw)],
            null,
            null,
            new ExtractionSettings(0, new PortFishing(new CommodityId(Fish), 1)));
        return new WorldState(definition);
    }

    private static TurnResolution ResolveBuild(
        WorldState state,
        int cell,
        EngineerConstruction structure) =>
        TurnResolver.Resolve(
            state,
            new TurnOrders(
            [
                new CountryTurnOrders(
                    new CountryId(0),
                    engineerWork:
                    [
                        new EngineerOrder(new CivilianUnitId(1), new CellIndex(cell), structure),
                    ]),
            ]),
            0);

    /// <summary>
    /// A 4x1 strip of owned plains with an Engineer on cell 0, and optionally
    /// a sea on the last column.
    /// </summary>
    private static WorldState CreateEngineerState(bool wrapsHorizontally, bool seaAtLastColumn)
    {
        const int width = 4;
        const int plains = 0;
        const int ocean = 1;
        var dimensions = new MapDimensions(width, 1);
        var cells = new CellDefinition[width];
        for (var index = 0; index < width; index++)
        {
            var sea = seaAtLastColumn && index == width - 1;
            cells[index] = new CellDefinition(
                new CellIndex(index),
                dimensions.GetCoordinate(new CellIndex(index)),
                new TerrainId(sea ? ocean : plains),
                sea
                    ? CellRegion.ForSeaZone(new SeaZoneId(0))
                    : CellRegion.ForProvince(new ProvinceId(index)),
                null,
                index == 1 ? SettlementSiteKind.Urban : SettlementSiteKind.None);
        }

        var map = new MapDefinition(
            dimensions,
            cells,
            Enumerable.Range(0, width)
                .Select(static index => new ProvinceDefinition(new ProvinceId(index), $"Province {index}")),
            seaAtLastColumn ? [new SeaZoneDefinition(new SeaZoneId(0), "Far Sea")] : [],
            [],
            [
                new TerrainDefinition(new TerrainId(plains), "Plains", true, rail: RailRule.Unrestricted),
                new TerrainDefinition(new TerrainId(ocean), "Ocean"),
            ],
            wrapsHorizontally: wrapsHorizontally);
        var scenario = new ScenarioDefinition(
            "Seam engineer",
            1815,
            Enumerable.Repeat<CountryId?>(new CountryId(0), width),
            initialCountryCapitals: [new CountryCapital(new CountryId(0), new CellIndex(1))],
            initialCivilians:
            [
                new InitialCivilian(new CountryId(0), new CivilianTypeId(0), new CellIndex(0)),
            ],
            initialCash: [new InitialCash(new CountryId(0), 10_000)]);
        return new WorldState(new WorldDefinition(
            map,
            [new CountryDefinition(new CountryId(0), "Country 0")],
            scenario,
            [new CommodityDefinition(new CommodityId(Fish), "Fish", CommodityCategory.Raw)],
            civilianTypes:
            [
                new CivilianTypeDefinition(
                    new CivilianTypeId(0), "Engineer", 1, CivilianWorkKind.Construct),
            ],
            construction: new ConstructionSettings(100, 100)));
    }

    private static WorldState CreateCatchmentState(bool wrapsHorizontally)
    {
        const int width = 4;
        var dimensions = new MapDimensions(width, 1);
        var cells = new CellDefinition[width];
        for (var index = 0; index < width; index++)
        {
            cells[index] = new CellDefinition(
                new CellIndex(index),
                dimensions.GetCoordinate(new CellIndex(index)),
                new TerrainId(0),
                CellRegion.ForProvince(new ProvinceId(index)),
                index == width - 1 ? [new ResourceId(0)] : null,
                index == 0 ? SettlementSiteKind.Urban : SettlementSiteKind.None);
        }

        var map = new MapDefinition(
            dimensions,
            cells,
            Enumerable.Range(0, width)
                .Select(static index => new ProvinceDefinition(new ProvinceId(index), $"Province {index}")),
            [],
            [new ResourceDefinition(new ResourceId(0), new CommodityId(Fish), [1])],
            wrapsHorizontally: wrapsHorizontally);
        var scenario = new ScenarioDefinition(
            "Seam catchment",
            1815,
            Enumerable.Repeat<CountryId?>(new CountryId(0), width),
            null,
            [new CountryCapital(new CountryId(0), new CellIndex(0))]);
        var definition = new WorldDefinition(
            map,
            [new CountryDefinition(new CountryId(0), "Country 0")],
            scenario,
            [new CommodityDefinition(new CommodityId(Fish), "Fish", CommodityCategory.Raw)],
            null,
            null,
            new ExtractionSettings(1));
        return new WorldState(definition);
    }
}
