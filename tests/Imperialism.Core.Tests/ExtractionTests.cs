using Imperialism.Core;
using Xunit;

namespace Imperialism.Core.Tests;

/// <summary>
/// A 5x1 strip keeps the hex geometry honest but trivial to reason about: with
/// a single row only the east and west neighbours exist, so a catchment radius
/// of one reaches exactly one cell either side of a collection point.
/// </summary>
public sealed class ExtractionTests
{
    private const int Grain = 0;
    private const int Coal = 1;

    [Fact]
    public void DepositsOnTheCapitalRailNetworkAreGatheredWithinTheCatchment()
    {
        // Rail joins cells 0-1, so collection points are {0,1} and radius 1
        // widens that to {0,1,2}. Cell 2 carries grain, cells 3 and 4 do not.
        var state = CreateState(depositCells: [(2, Grain)]);

        var result = TurnResolver.Resolve(state, TurnOrders.Empty(2), 0);

        var extraction = Assert.Single(result.Events.OfType<ResourceExtractedEvent>());
        Assert.Equal(new CountryId(0), extraction.Country);
        Assert.Equal(1, extraction.CollectedCellCount);
        Assert.Equal(0, extraction.StrandedCellCount);
        Assert.Equal(
            new CommodityQuantity(new CommodityId(Grain), 2),
            Assert.Single(extraction.Collected));
        Assert.Empty(extraction.Stranded);
    }

    [Fact]
    public void DepositsBeyondTheCatchmentAreReportedStrandedRatherThanDropped()
    {
        var state = CreateState(depositCells: [(2, Grain), (3, Grain), (4, Coal)]);

        var result = TurnResolver.Resolve(state, TurnOrders.Empty(2), 0);

        var extraction = Assert.Single(result.Events.OfType<ResourceExtractedEvent>());
        Assert.Equal(1, extraction.CollectedCellCount);
        Assert.Equal(2, extraction.StrandedCellCount);
        Assert.Equal(
            new CommodityQuantity(new CommodityId(Grain), 2),
            Assert.Single(extraction.Collected));
        Assert.Equal(
            [
                new CommodityQuantity(new CommodityId(Grain), 2),
                new CommodityQuantity(new CommodityId(Coal), 3),
            ],
            extraction.Stranded);
        Assert.Equal(2, state.GetAvailableQuantity(new CountryId(0), new CommodityId(Grain)));
        Assert.Equal(0, state.GetAvailableQuantity(new CountryId(0), new CommodityId(Coal)));
    }

    [Fact]
    public void GatheredOutputReachesTheWarehouseThroughDeliveryNotDirectly()
    {
        var state = CreateState(depositCells: [(2, Grain)]);

        var result = TurnResolver.Resolve(state, TurnOrders.Empty(2), 0);

        // Extraction queues; Delivery commits. Both happen inside one turn, so
        // the stock is present afterwards, but Production ran before Delivery
        // and therefore could not have consumed it.
        var phases = result.Events.Select(static item => item.Phase).ToArray();
        Assert.True(
            Array.IndexOf(phases, TurnPhase.Production) <
            Array.IndexOf(phases, TurnPhase.Extraction));
        var delivered = Assert.Single(result.Events.OfType<CommodityDeliveredEvent>());
        Assert.Equal(PendingDeliverySource.Extraction, delivered.Delivery.Source);
        Assert.Equal(2, delivered.Delivery.Quantity);
        Assert.Empty(state.GetPendingDeliveries());
        Assert.Equal(2, state.GetAvailableQuantity(new CountryId(0), new CommodityId(Grain)));
    }

    [Fact]
    public void ThisTurnsHarvestOnlyFeedsNextTurnsProduction()
    {
        var state = CreateState(depositCells: [(2, Grain)], withMill: true);
        var orders = new TurnOrders(
        [
            new CountryTurnOrders(
                new CountryId(0),
                [new ProductionOrder(new ProductionRecipeId(0), 1)]),
            new CountryTurnOrders(new CountryId(1)),
        ]);

        var first = TurnResolver.Resolve(state, orders, 0);
        var second = TurnResolver.Resolve(state, orders, 0);

        // Turn 1 opens with an empty warehouse, so the mill idles even though
        // grain is gathered later in the same resolution.
        Assert.Equal(0, first.Events.OfType<ProductionCompletedEvent>().Single().CompletedCycles);
        Assert.Equal(1, second.Events.OfType<ProductionCompletedEvent>().Single().CompletedCycles);
    }

    [Fact]
    public void RailThatNoLongerReachesTheCapitalGathersNothing()
    {
        // Cells 3 and 4 are railed to each other but not to the capital's
        // component, so their deposits are stranded despite sitting on rail.
        var state = CreateState(
            depositCells: [(3, Grain), (4, Grain)],
            extraRails: [(3, 4)]);

        var result = TurnResolver.Resolve(state, TurnOrders.Empty(2), 0);

        var extraction = Assert.Single(result.Events.OfType<ResourceExtractedEvent>());
        Assert.Equal(0, extraction.CollectedCellCount);
        Assert.Equal(2, extraction.StrandedCellCount);
        Assert.Empty(extraction.Collected);
        Assert.Equal(
            new CommodityQuantity(new CommodityId(Grain), 4),
            Assert.Single(extraction.Stranded));
    }

    [Fact]
    public void ACountryWithoutACapitalGathersNothing()
    {
        var state = CreateState(depositCells: [(0, Grain), (1, Grain)]);
        state.SetCountryCapital(new CountryId(0), null);

        var result = TurnResolver.Resolve(state, TurnOrders.Empty(2), 0);

        var extraction = Assert.Single(result.Events.OfType<ResourceExtractedEvent>());
        Assert.Equal(0, extraction.CollectedCellCount);
        Assert.Equal(2, extraction.StrandedCellCount);
        Assert.Empty(extraction.Collected);
    }

    [Fact]
    public void ACellInRangeOfTwoCollectionPointsPaysOnce()
    {
        // Cell 2 neighbours cell 1 (a rail cell) and cell 3; railing 2-3 puts it
        // inside the catchment twice over. Overlapping coverage is wasted, not
        // doubled.
        var state = CreateState(depositCells: [(2, Grain)], extraRails: [(1, 2), (2, 3)]);

        var result = TurnResolver.Resolve(state, TurnOrders.Empty(2), 0);

        var extraction = Assert.Single(result.Events.OfType<ResourceExtractedEvent>());
        Assert.Equal(1, extraction.CollectedCellCount);
        Assert.Equal(
            new CommodityQuantity(new CommodityId(Grain), 2),
            Assert.Single(extraction.Collected));
    }

    [Fact]
    public void SeveralDepositsOnOneCellEachContributeTheirOwnYield()
    {
        var state = CreateState(depositCells: [(1, Grain), (1, Coal)]);

        var result = TurnResolver.Resolve(state, TurnOrders.Empty(2), 0);

        var extraction = Assert.Single(result.Events.OfType<ResourceExtractedEvent>());
        Assert.Equal(1, extraction.CollectedCellCount);
        Assert.Equal(
            [
                new CommodityQuantity(new CommodityId(Grain), 2),
                new CommodityQuantity(new CommodityId(Coal), 3),
            ],
            extraction.Collected);
    }

    [Fact]
    public void ZeroRadiusGathersOnlyTheConnectionPointsThemselves()
    {
        var state = CreateState(depositCells: [(1, Grain), (2, Grain)], catchmentRadius: 0);

        var result = TurnResolver.Resolve(state, TurnOrders.Empty(2), 0);

        var extraction = Assert.Single(result.Events.OfType<ResourceExtractedEvent>());
        Assert.Equal(1, extraction.CollectedCellCount);
        Assert.Equal(1, extraction.StrandedCellCount);
    }

    [Fact]
    public void ADepositPaysOnlyTheCountryHoldingItsProvince()
    {
        var state = CreateState(depositCells: [(2, Grain)]);
        state.SetProvinceOwner(new ProvinceId(2), new CountryId(1));

        var result = TurnResolver.Resolve(state, TurnOrders.Empty(2), 0);

        // Country 1 owns the cell but has no capital, so nobody gathers it,
        // and country 0 must not gather a province it no longer holds.
        Assert.DoesNotContain(
            result.Events.OfType<ResourceExtractedEvent>(),
            static item => item.Country == new CountryId(0));
        var other = Assert.Single(result.Events.OfType<ResourceExtractedEvent>());
        Assert.Equal(new CountryId(1), other.Country);
        Assert.Equal(1, other.StrandedCellCount);
    }

    [Fact]
    public void ResourceYieldMustBePositive()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ResourceDefinition(new ResourceId(0), new CommodityId(0), 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ResourceDefinition(new ResourceId(0), new CommodityId(0), -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ExtractionSettings(-1));
        Assert.Equal(1, ExtractionSettings.Default.CatchmentRadius);
    }

    private static WorldState CreateState(
        (int Cell, int Resource)[] depositCells,
        (int First, int Second)[]? extraRails = null,
        int catchmentRadius = 1,
        bool withMill = false)
    {
        const int width = 5;
        var dimensions = new MapDimensions(width, 1);
        var cells = new CellDefinition[width];
        for (var index = 0; index < width; index++)
        {
            var deposits = depositCells
                .Where(deposit => deposit.Cell == index)
                .Select(static deposit => new ResourceId(deposit.Resource))
                .ToArray();
            cells[index] = new CellDefinition(
                new CellIndex(index),
                dimensions.GetCoordinate(new CellIndex(index)),
                new TerrainId(0),
                CellRegion.ForProvince(new ProvinceId(index)),
                deposits,
                index == 0 ? SettlementSiteKind.Urban : SettlementSiteKind.None);
        }

        var map = new MapDefinition(
            dimensions,
            cells,
            Enumerable.Range(0, width)
                .Select(static index =>
                    new ProvinceDefinition(new ProvinceId(index), $"Province {index}")),
            [],
            [
                // Distinct yields keep a mixed-up commodity index visible.
                new ResourceDefinition(new ResourceId(Grain), new CommodityId(Grain), 2),
                new ResourceDefinition(new ResourceId(Coal), new CommodityId(Coal), 3),
            ]);

        var rails = new List<CellLink> { new(new CellIndex(0), new CellIndex(1)) };
        foreach (var (first, second) in extraRails ?? [])
        {
            var link = new CellLink(new CellIndex(first), new CellIndex(second));
            if (!rails.Contains(link))
            {
                rails.Add(link);
            }
        }

        var scenario = new ScenarioDefinition(
            "Extraction",
            1815,
            Enumerable.Repeat<CountryId?>(new CountryId(0), width),
            rails,
            [new CountryCapital(new CountryId(0), new CellIndex(0))],
            null,
            withMill
                ? [new InitialProductionCapacity(new CountryId(0), new ProductionFacilityId(0), 10)]
                : null);

        var facilities = withMill
            ? new[]
            {
                new ProductionFacilityDefinition(
                    new ProductionFacilityId(0),
                    "Food Mill",
                    ProductionCapacityMode.Limited),
            }
            : [];
        var recipes = withMill
            ? new[]
            {
                new ProductionRecipeDefinition(
                    new ProductionRecipeId(0),
                    "Mill Grain",
                    new ProductionFacilityId(0),
                    1,
                    [new CommodityQuantity(new CommodityId(Grain), 2)],
                    [new CommodityQuantity(new CommodityId(Coal), 1)]),
            }
            : [];

        var definition = new WorldDefinition(
            map,
            [
                new CountryDefinition(new CountryId(0), "Country 0"),
                new CountryDefinition(new CountryId(1), "Country 1"),
            ],
            scenario,
            [
                new CommodityDefinition(new CommodityId(Grain), "Grain", CommodityCategory.Raw),
                new CommodityDefinition(new CommodityId(Coal), "Coal", CommodityCategory.Raw),
            ],
            facilities,
            recipes,
            new ExtractionSettings(catchmentRadius));
        return new WorldState(definition);
    }
}
