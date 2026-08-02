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
    private const int Iron = 2;

    /// <summary>Commodity index inside the port fixture, which has only fish.</summary>
    private const int Fish = 0;

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
    public void AYieldCurveMustDescribeSomethingWorthCollecting()
    {
        // Zero at the undeveloped level is the whole point of a mine, so it is
        // allowed; zero at every level is not, because nothing would ever come
        // of it.
        _ = new ResourceDefinition(new ResourceId(0), new CommodityId(0), [0, 2]);
        Assert.Throws<ArgumentException>(() =>
            new ResourceDefinition(new ResourceId(0), new CommodityId(0), []));
        Assert.Throws<ArgumentException>(() =>
            new ResourceDefinition(new ResourceId(0), new CommodityId(0), [0, 0]));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ResourceDefinition(new ResourceId(0), new CommodityId(0), [1, -1]));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ExtractionSettings(-1));
        Assert.Equal(1, ExtractionSettings.Default.CatchmentRadius);
    }

    [Fact]
    public void YieldHoldsAtTheTopOfTheCurveRatherThanThrowing()
    {
        var deposit = new ResourceDefinition(new ResourceId(0), new CommodityId(0), [1, 2, 4, 8]);

        Assert.Equal(1, deposit.GetYield(0));
        Assert.Equal(8, deposit.GetYield(3));
        Assert.Equal(8, deposit.GetYield(9));
        Assert.Equal(3, deposit.MaxDevelopmentLevel);
        Assert.Throws<ArgumentOutOfRangeException>(() => deposit.GetYield(-1));
    }

    [Fact]
    public void ImprovingACellDoublesWhatItHandsOver()
    {
        var state = CreateState(depositCells: [(2, Grain)]);

        // Cell 2 carries grain on the [2, 4, 8, 16] curve.
        Assert.Equal(2, Harvest(state, Grain));
        state.SetCellDevelopment(new CellIndex(2), 1);
        Assert.Equal(4, Harvest(state, Grain));
        state.SetCellDevelopment(new CellIndex(2), 2);
        Assert.Equal(8, Harvest(state, Grain));
        state.SetCellDevelopment(new CellIndex(2), 3);
        Assert.Equal(16, Harvest(state, Grain));
    }

    [Fact]
    public void AMineGivesNothingUntilItHasBeenDug()
    {
        // Iron's curve starts at zero: connected and owned is not enough.
        var state = CreateState(depositCells: [(2, Iron)]);

        var before = Assert.Single(
            TurnResolver.Resolve(state, TurnOrders.Empty(2), 0)
                .Events.OfType<ResourceExtractedEvent>());
        Assert.Equal(1, before.CollectedCellCount);
        Assert.Empty(before.Collected);

        state.SetCellDevelopment(new CellIndex(2), 1);

        Assert.Equal(3, Harvest(state, Iron));
    }

    [Fact]
    public void ADepositNobodyKnowsHowToWorkYieldsNothing()
    {
        var state = CreateState(depositCells: [(2, Grain)], gateGrainBehindTechnology: true);

        Assert.Equal(0, Harvest(state, Grain));

        state.GrantTechnology(new CountryId(0), new TechnologyId(0));

        Assert.Equal(2, Harvest(state, Grain));
    }

    [Fact]
    public void AScenarioCanStartCellsAlreadyImprovedAndCountriesAlreadyInformed()
    {
        var state = CreateState(
            depositCells: [(2, Coal)],
            initialDevelopment: [(2, 2)],
            gateGrainBehindTechnology: true,
            startingTechnology: true);

        Assert.Equal(2, state.GetCellDevelopment(new CellIndex(2)));
        Assert.True(state.HasTechnology(new CountryId(0), new TechnologyId(0)));
        Assert.False(state.HasTechnology(new CountryId(1), new TechnologyId(0)));

        // Coal's curve is [3, 6, 12, 24], so a level-2 cell hands over 12.
        Assert.Equal(12, Harvest(state, Coal));
    }

    [Fact]
    public void OnlyLandCanBeDeveloped()
    {
        var state = CreateState(depositCells: []);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            state.SetCellDevelopment(new CellIndex(0), -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            state.GetCellDevelopment(new CellIndex(99)));
    }

    [Fact]
    public void ACoastalPortFishesTheSeaBesideIt()
    {
        // Cell 2 is land with the sea at cell 3, railed back to the capital.
        var state = CreatePortState(ports: [2], extraRails: [(0, 1), (1, 2)]);

        var extraction = Assert.Single(
            TurnResolver.Resolve(state, TurnOrders.Empty(2), 0)
                .Events.OfType<ResourceExtractedEvent>());

        Assert.Equal(1, extraction.FishingPortCount);
        Assert.Equal(0, extraction.StrandedPortCount);
        Assert.Equal(
            new CommodityQuantity(new CommodityId(Fish), 1),
            Assert.Single(extraction.Collected));
    }

    [Fact]
    public void ARiverPortFishesJustLikeACoastalOne()
    {
        // Cell 1 touches no sea at all; its only water is the river on cell 2.
        // 45 of the corpus's 124 ports are inland like this, so a rule that gave
        // them nothing would quietly waste a third of the ports ever authored.
        var state = CreatePortState(ports: [1], extraRails: [(0, 1)]);

        var extraction = Assert.Single(
            TurnResolver.Resolve(state, TurnOrders.Empty(2), 0)
                .Events.OfType<ResourceExtractedEvent>());

        Assert.Equal(1, extraction.FishingPortCount);
        Assert.Equal(
            new CommodityQuantity(new CommodityId(Fish), 1),
            Assert.Single(extraction.Collected));
    }

    [Fact]
    public void APortOffTheNetworkStrandsItsCatch()
    {
        // Cell 2 has sea beside it but no rail home, and cell 1 is sea-locked
        // from it, so nothing carries the catch to the capital.
        var state = CreatePortState(ports: [2]);

        var extraction = Assert.Single(
            TurnResolver.Resolve(state, TurnOrders.Empty(2), 0)
                .Events.OfType<ResourceExtractedEvent>());

        Assert.Equal(0, extraction.FishingPortCount);
        Assert.Equal(1, extraction.StrandedPortCount);
        Assert.Empty(extraction.Collected);
        Assert.Equal(
            new CommodityQuantity(new CommodityId(Fish), 1),
            Assert.Single(extraction.Stranded));
    }

    [Fact]
    public void APortPaysOnlyTheCountryHoldingIt()
    {
        var state = CreatePortState(ports: [2], extraRails: [(0, 1), (1, 2)]);
        state.SetProvinceOwner(new ProvinceId(2), new CountryId(1));

        var extraction = Assert.Single(
            TurnResolver.Resolve(state, TurnOrders.Empty(2), 0)
                .Events.OfType<ResourceExtractedEvent>());

        Assert.Equal(new CountryId(1), extraction.Country);
        Assert.Equal(0, extraction.FishingPortCount);
        Assert.Equal(1, extraction.StrandedPortCount);
    }

    [Fact]
    public void AWorldWithoutFishingRulesGetsNothingFromItsPorts()
    {
        var state = CreatePortState(ports: [2], extraRails: [(0, 1), (1, 2)], withFishing: false);

        Assert.Empty(TurnResolver.Resolve(state, TurnOrders.Empty(2), 0)
            .Events.OfType<ResourceExtractedEvent>());
    }

    [Fact]
    public void APortMustStandOnLand()
    {
        var state = CreatePortState(ports: []);

        // Cell 3 is open sea.
        Assert.Throws<ArgumentException>(() => state.BuildPort(new CellIndex(3)));
        Assert.True(state.BuildPort(new CellIndex(2)));
        Assert.False(state.BuildPort(new CellIndex(2)));
        Assert.True(state.HasPort(new CellIndex(2)));
        Assert.True(state.RemovePort(new CellIndex(2)));
        Assert.False(state.HasPort(new CellIndex(2)));
    }

    [Fact]
    public void FishingYieldMustBeWorthHaving()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PortFishing(new CommodityId(0), 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PortFishing(new CommodityId(0), -1));
    }

    /// <summary>
    /// A 4x1 strip: capital, a cell whose only water is the river next door, a
    /// coastal cell, and the sea. One layout covers a river port and a sea port.
    /// </summary>
    private static WorldState CreatePortState(
        int[] ports,
        (int First, int Second)[]? extraRails = null,
        bool withFishing = true)
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
                null,
                index == 0 ? SettlementSiteKind.Urban : SettlementSiteKind.None,
                index == 2 ? new RiverPath(RiverEndpoint.WestLower, RiverEndpoint.EastUpper) : null);
        }

        var map = new MapDefinition(
            dimensions,
            cells,
            [
                new ProvinceDefinition(new ProvinceId(0), "Capital"),
                new ProvinceDefinition(new ProvinceId(1), "Inland"),
                new ProvinceDefinition(new ProvinceId(2), "Shore"),
            ],
            [new SeaZoneDefinition(new SeaZoneId(0), "Open Sea")],
            [new ResourceDefinition(new ResourceId(0), new CommodityId(Fish), [1])]);

        var rails = new List<CellLink>();
        foreach (var (first, second) in extraRails ?? [])
        {
            rails.Add(new CellLink(new CellIndex(first), new CellIndex(second)));
        }

        var scenario = new ScenarioDefinition(
            "Ports",
            1815,
            [new CountryId(0), new CountryId(0), new CountryId(0)],
            rails,
            [new CountryCapital(new CountryId(0), new CellIndex(0))],
            null,
            null,
            null,
            null,
            ports.Select(static cell => new CellIndex(cell)));

        var definition = new WorldDefinition(
            map,
            [
                new CountryDefinition(new CountryId(0), "Country 0"),
                new CountryDefinition(new CountryId(1), "Country 1"),
            ],
            scenario,
            [new CommodityDefinition(new CommodityId(Fish), "Fish", CommodityCategory.Raw)],
            null,
            null,
            new ExtractionSettings(
                1,
                withFishing ? new PortFishing(new CommodityId(Fish), 1) : null));
        return new WorldState(definition);
    }

    /// <summary>Resolves one turn and returns what reached the warehouse.</summary>
    private static long Harvest(WorldState state, int commodity)
    {
        var before = state.GetAvailableQuantity(new CountryId(0), new CommodityId(commodity));
        _ = TurnResolver.Resolve(state, TurnOrders.Empty(2), 0);
        return state.GetAvailableQuantity(new CountryId(0), new CommodityId(commodity)) - before;
    }

    private static WorldState CreateState(
        (int Cell, int Resource)[] depositCells,
        (int First, int Second)[]? extraRails = null,
        int catchmentRadius = 1,
        bool withMill = false,
        (int Cell, int Level)[]? initialDevelopment = null,
        bool gateGrainBehindTechnology = false,
        bool startingTechnology = false)
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
                // Distinct yields keep a mixed-up commodity index visible, and
                // the curves double the way the surface and subsurface ones do.
                new ResourceDefinition(
                    new ResourceId(Grain),
                    new CommodityId(Grain),
                    [2, 4, 8, 16],
                    gateGrainBehindTechnology ? new TechnologyId(0) : null),
                new ResourceDefinition(new ResourceId(Coal), new CommodityId(Coal), [3, 6, 12, 24]),

                // The one deposit here that behaves like a real mine.
                new ResourceDefinition(new ResourceId(Iron), new CommodityId(Iron), [0, 3, 6, 12]),
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
                : null,
            (initialDevelopment ?? [])
                .Select(static item => new InitialCellDevelopment(new CellIndex(item.Cell), item.Level)),
            startingTechnology
                ? [new InitialCountryTechnology(new CountryId(0), new TechnologyId(0))]
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
                new CommodityDefinition(new CommodityId(Iron), "Iron", CommodityCategory.Raw),
            ],
            facilities,
            recipes,
            new ExtractionSettings(catchmentRadius),
            [new TechnologyDefinition(new TechnologyId(0), "Mechanised Farming")]);
        return new WorldState(definition);
    }
}
