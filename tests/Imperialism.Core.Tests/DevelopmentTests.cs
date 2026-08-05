using Imperialism.Core;
using Xunit;

namespace Imperialism.Core.Tests;

/// <summary>
/// The Development phase: civilians moving, working, and raising a tile's level.
/// </summary>
/// <remarks>
/// The fixture is a 4x1 strip owned by country 0 except for the last cell,
/// which country 1 holds so that entering foreign ground can be refused. Cell 2
/// is dry plains carrying grain — the manual's own example of a tile that
/// yields a farm product and admits no farmer — which is the case that separates
/// terrain-based improvability from deposit-based.
/// </remarks>
public sealed class DevelopmentTests
{
    private const int Farm = 0;
    private const int DryPlains = 1;
    private const int Grain = 0;
    private const int Timber = 1;
    private const int Farmer = 0;
    private const int Forester = 1;

    [Fact]
    public void AFarmerRaisesAGrainTileAndThatTurnsHarvestReapsTheNewRate()
    {
        var state = CreateState();
        var farmer = Assert.Single(state.GetCivilians()).Id;

        // Turn one: the order is taken and the work begins.
        var first = Resolve(state, Work(farmer, 1));
        Assert.Equal(
            new CellIndex(1),
            Assert.Single(first.Events.OfType<CivilianWorkBegunEvent>()).Cell);
        Assert.Empty(first.Events.OfType<CellDevelopedEvent>());
        Assert.Equal(0, state.GetCellDevelopment(new CellIndex(1)));

        // Three grain tiles at level 0, one unit each.
        Assert.Equal(3, Collected(first, Grain));

        // Turn two: the work finishes, and Extraction runs later in the same
        // turn, so this turn's harvest is already at the new rate.
        var second = Resolve(state, TurnOrders.Empty(2));
        var developed = Assert.Single(second.Events.OfType<CellDevelopedEvent>());
        Assert.Equal((0, 1), (developed.FromLevel, developed.ToLevel));
        Assert.Equal(farmer, developed.Unit);
        Assert.Equal(1, state.GetCellDevelopment(new CellIndex(1)));
        Assert.Equal(4, Collected(second, Grain));

        // And the farmer is idle again, free to be sent somewhere else.
        Assert.False(state.GetCivilian(farmer)!.IsBusy);
    }

    /// <summary>
    /// The reason terrain gained attributes at all. Cell 2 carries grain, and
    /// grain names a Farmer, and the ground still refuses the work.
    /// </summary>
    [Fact]
    public void DryPlainsCarryingGrainCannotBeImprovedByAFarmer()
    {
        var state = CreateState();
        var farmer = Assert.Single(state.GetCivilians()).Id;

        var result = Resolve(state, Work(farmer, 2));

        Assert.Equal(
            CivilianOrderRefusal.TerrainCannotBeImproved,
            Assert.Single(result.Events.OfType<CivilianOrderRefusedEvent>()).Reason);
        Assert.False(state.GetCivilian(farmer)!.IsBusy);
    }

    /// <summary>
    /// A world that declares no terrain attributes cannot improve anything.
    /// "Unknown" and "unimprovable" reach the same answer here on purpose: a
    /// silent default of improvable would invent permission out of an omission.
    /// </summary>
    [Fact]
    public void AWorldWithNoTerrainTableImprovesNothing()
    {
        var state = CreateState(withTerrainAttributes: false);
        var farmer = Assert.Single(state.GetCivilians()).Id;

        var result = Resolve(state, Work(farmer, 1));

        Assert.Equal(
            CivilianOrderRefusal.TerrainCannotBeImproved,
            Assert.Single(result.Events.OfType<CivilianOrderRefusedEvent>()).Reason);
    }

    [Fact]
    public void ACivilianRefusesADepositItsTypeDoesNotWork()
    {
        var state = CreateState(civilianType: Forester);
        var forester = Assert.Single(state.GetCivilians()).Id;

        var result = Resolve(state, Work(forester, 1));

        Assert.Equal(
            CivilianOrderRefusal.NoDepositThisCivilianWorks,
            Assert.Single(result.Events.OfType<CivilianOrderRefusedEvent>()).Reason);
    }

    /// <summary>
    /// The manual bars a civilian from another Great Power's land outright. With
    /// no diplomacy modelled the rule narrows to a country's own territory,
    /// which can only refuse more than the original did, never less.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ACivilianMayNotEnterAnotherCountrysTerritory(bool working)
    {
        var state = CreateState();
        var farmer = Assert.Single(state.GetCivilians()).Id;

        var result = Resolve(state, working ? Work(farmer, 3) : Deploy(farmer, 3));

        Assert.Equal(
            CivilianOrderRefusal.TargetNotYourTerritory,
            Assert.Single(result.Events.OfType<CivilianOrderRefusedEvent>()).Reason);
        Assert.Equal(new CellIndex(0), state.GetCivilian(farmer)!.Cell);
    }

    [Fact]
    public void ATileAtTheTopOfItsCurveTakesNoMoreWork()
    {
        var state = CreateState(initialDevelopment: [(1, 3)]);
        var farmer = Assert.Single(state.GetCivilians()).Id;

        var result = Resolve(state, Work(farmer, 1));

        Assert.Equal(
            CivilianOrderRefusal.AlreadyFullyDeveloped,
            Assert.Single(result.Events.OfType<CivilianOrderRefusedEvent>()).Reason);
    }

    /// <summary>
    /// Orders are written before the turn resolves, so an order for a civilian
    /// that was busy when they were written cannot be honoured — even though its
    /// job finishes during this very phase.
    /// </summary>
    [Fact]
    public void ACivilianPartWayThroughAJobRefusesANewOrder()
    {
        var state = CreateState(workTurns: 2);
        var farmer = Assert.Single(state.GetCivilians()).Id;

        _ = Resolve(state, Work(farmer, 1));
        var second = Resolve(state, Deploy(farmer, 2));

        Assert.Equal(
            CivilianOrderRefusal.AlreadyWorking,
            Assert.Single(second.Events.OfType<CivilianOrderRefusedEvent>()).Reason);
        Assert.Equal(new CellIndex(1), state.GetCivilian(farmer)!.Cell);
    }

    /// <summary>
    /// The duration is the one number in this system with nothing behind it, so
    /// it is worth pinning that it is read from content rather than assumed.
    /// </summary>
    [Fact]
    public void WorkTakesAsManyTurnsAsTheCivilianTypeDeclares()
    {
        var state = CreateState(workTurns: 3);
        var farmer = Assert.Single(state.GetCivilians()).Id;

        var first = Resolve(state, Work(farmer, 1));
        Assert.Equal(3, Assert.Single(first.Events.OfType<CivilianWorkBegunEvent>()).TurnsRequired);

        Assert.Empty(Resolve(state, TurnOrders.Empty(2)).Events.OfType<CellDevelopedEvent>());
        Assert.Empty(Resolve(state, TurnOrders.Empty(2)).Events.OfType<CellDevelopedEvent>());
        Assert.Single(Resolve(state, TurnOrders.Empty(2)).Events.OfType<CellDevelopedEvent>());
        Assert.Equal(1, state.GetCellDevelopment(new CellIndex(1)));
    }

    /// <summary>
    /// Deploying moves and does nothing else. The manual gives it its own
    /// cursor, which is what says moving and working are alternatives.
    /// </summary>
    [Fact]
    public void DeployingMovesTheCivilianWithoutSettingItToWork()
    {
        var state = CreateState();
        var farmer = Assert.Single(state.GetCivilians()).Id;

        var result = Resolve(state, Deploy(farmer, 1));

        var moved = Assert.Single(result.Events.OfType<CivilianDeployedEvent>());
        Assert.Equal((new CellIndex(0), new CellIndex(1)), (moved.From, moved.To));
        Assert.False(state.GetCivilian(farmer)!.IsBusy);
        Assert.Empty(result.Events.OfType<CellDevelopedEvent>());
    }

    [Fact]
    public void ACivilianTakesOneOrderATurn()
    {
        var unit = new CivilianUnitId(1);

        Assert.Throws<ArgumentException>(() => new CountryTurnOrders(
            new CountryId(0),
            deployments: [new CivilianDeployOrder(unit, new CellIndex(1))],
            civilianWork: [new CivilianWorkOrder(unit, new CellIndex(1))]));
        Assert.Throws<ArgumentException>(() => new CountryTurnOrders(
            new CountryId(0),
            civilianWork:
            [
                new CivilianWorkOrder(unit, new CellIndex(1)),
                new CivilianWorkOrder(unit, new CellIndex(2)),
            ]));
    }

    [Fact]
    public void OrdersForAMissingOrForeignCivilianAreRefusedRatherThanThrown()
    {
        var state = CreateState();
        var farmer = Assert.Single(state.GetCivilians()).Id;

        var missing = Resolve(state, Deploy(new CivilianUnitId(99), 1));
        Assert.Equal(
            CivilianOrderRefusal.NoSuchCivilian,
            Assert.Single(missing.Events.OfType<CivilianOrderRefusedEvent>()).Reason);

        var foreign = Resolve(state, new TurnOrders(
        [
            new CountryTurnOrders(new CountryId(0)),
            new CountryTurnOrders(
                new CountryId(1),
                deployments: [new CivilianDeployOrder(farmer, new CellIndex(3))]),
        ]));
        Assert.Equal(
            CivilianOrderRefusal.NotYours,
            Assert.Single(foreign.Events.OfType<CivilianOrderRefusedEvent>()).Reason);
    }

    /// <summary>
    /// A tile can change hands while a civilian is working it. Finishing a job
    /// that is no longer legal frees the worker and raises nothing, rather than
    /// improving ground somebody else now owns.
    /// </summary>
    [Fact]
    public void WorkOnATileLostMidJobFinishesWithoutRaisingIt()
    {
        var state = CreateState(workTurns: 2);
        var farmer = Assert.Single(state.GetCivilians()).Id;

        _ = Resolve(state, Work(farmer, 1));
        state.SetProvinceOwner(new ProvinceId(1), new CountryId(1));
        _ = Resolve(state, TurnOrders.Empty(2));
        var finished = Resolve(state, TurnOrders.Empty(2));

        Assert.Equal(
            CivilianOrderRefusal.TargetNotYourTerritory,
            Assert.Single(finished.Events.OfType<CivilianOrderRefusedEvent>()).Reason);
        Assert.Equal(0, state.GetCellDevelopment(new CellIndex(1)));
        Assert.False(state.GetCivilian(farmer)!.IsBusy);
    }

    [Fact]
    public void ScenarioCiviliansTakeIdsInOrderAndStandWhereDeclared()
    {
        var state = CreateState(civilians:
        [
            new InitialCivilian(new CountryId(0), new CivilianTypeId(Farmer), new CellIndex(0)),
            new InitialCivilian(new CountryId(0), new CivilianTypeId(Forester), new CellIndex(1)),
            new InitialCivilian(new CountryId(1), new CivilianTypeId(Farmer), new CellIndex(3)),
        ]);

        var civilians = state.GetCivilians();

        Assert.Equal([1L, 2L, 3L], civilians.Select(static item => item.Id.Value));
        Assert.Equal(
            [new CivilianTypeId(Farmer), new CivilianTypeId(Forester), new CivilianTypeId(Farmer)],
            civilians.Select(static item => item.Type));
        Assert.Equal(new CellIndex(3), Assert.Single(state.GetCivilians(new CountryId(1))).Cell);
    }

    [Fact]
    public void ACivilianCannotBePlacedOnWaterOrOutsideTheMap()
    {
        Assert.Throws<ArgumentException>(() => CreateState(civilians:
            [new InitialCivilian(new CountryId(0), new CivilianTypeId(Farmer), new CellIndex(4))]));
        Assert.Throws<ArgumentException>(() => CreateState(civilians:
            [new InitialCivilian(new CountryId(0), new CivilianTypeId(Farmer), new CellIndex(9))]));
    }

    [Fact]
    public void ACivilianTypeMustTakeAtLeastOneTurnToWork()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CivilianTypeDefinition(new CivilianTypeId(0), "Farmer", 0));
    }

    private static TurnOrders Work(CivilianUnitId unit, int cell) => new(
    [
        new CountryTurnOrders(
            new CountryId(0),
            civilianWork: [new CivilianWorkOrder(unit, new CellIndex(cell))]),
        new CountryTurnOrders(new CountryId(1)),
    ]);

    private static TurnOrders Deploy(CivilianUnitId unit, int cell) => new(
    [
        new CountryTurnOrders(
            new CountryId(0),
            deployments: [new CivilianDeployOrder(unit, new CellIndex(cell))]),
        new CountryTurnOrders(new CountryId(1)),
    ]);

    private static TurnResolution Resolve(WorldState state, TurnOrders orders) =>
        TurnResolver.Resolve(state, orders, 0);

    private static long Collected(TurnResolution resolution, int commodity) => resolution.Events
        .OfType<ResourceExtractedEvent>()
        .Where(static item => item.Country == new CountryId(0))
        .SelectMany(static item => item.Collected)
        .Where(item => item.Commodity == new CommodityId(commodity))
        .Sum(static item => item.Quantity);

    /// <summary>
    /// Four cells in a row, all country 0's but the last: capital farm, farm,
    /// dry plains, and a foreign farm. Cell 4 is sea, so a civilian has
    /// somewhere illegal to be sent. Every land cell carries grain except the
    /// foreign one, which carries timber as well.
    /// </summary>
    private static WorldState CreateState(
        int civilianType = Farmer,
        int workTurns = 1,
        bool withTerrainAttributes = true,
        (int Cell, int Level)[]? initialDevelopment = null,
        InitialCivilian[]? civilians = null)
    {
        const int width = 5;
        var dimensions = new MapDimensions(width, 1);
        var terrains = new[] { Farm, Farm, DryPlains, Farm, Farm };
        var cells = new CellDefinition[width];
        for (var index = 0; index < width; index++)
        {
            cells[index] = new CellDefinition(
                new CellIndex(index),
                dimensions.GetCoordinate(new CellIndex(index)),
                new TerrainId(index == 4 ? Farm : terrains[index]),
                index == 4
                    ? CellRegion.ForSeaZone(new SeaZoneId(0))
                    : CellRegion.ForProvince(new ProvinceId(index)),
                index == 4 ? null : [new ResourceId(index == 3 ? Timber : Grain)],
                index == 0 ? SettlementSiteKind.Urban : SettlementSiteKind.None);
        }

        var map = new MapDefinition(
            dimensions,
            cells,
            Enumerable.Range(0, 4)
                .Select(static index => new ProvinceDefinition(new ProvinceId(index), $"Province {index}")),
            [new SeaZoneDefinition(new SeaZoneId(0), "Open Sea")],
            [
                new ResourceDefinition(
                    new ResourceId(Grain),
                    new CommodityId(Grain),
                    [1, 2, 3, 4],
                    null,
                    new CivilianTypeId(Farmer)),
                new ResourceDefinition(
                    new ResourceId(Timber),
                    new CommodityId(Timber),
                    [1, 2, 3, 4],
                    null,
                    new CivilianTypeId(Forester)),
            ],
            withTerrainAttributes
                ?
                [
                    new TerrainDefinition(new TerrainId(Farm), "Farm", isImprovable: true),
                    new TerrainDefinition(new TerrainId(DryPlains), "Dry Plains"),
                ]
                : null);

        var scenario = new ScenarioDefinition(
            "Development",
            1815,
            [new CountryId(0), new CountryId(0), new CountryId(0), new CountryId(1)],
            initialCountryCapitals: [new CountryCapital(new CountryId(0), new CellIndex(0))],
            initialCellDevelopment: initialDevelopment
                ?.Select(static item => new InitialCellDevelopment(new CellIndex(item.Cell), item.Level)),
            initialCivilians: civilians ??
            [
                new InitialCivilian(
                    new CountryId(0), new CivilianTypeId(civilianType), new CellIndex(0)),
            ]);

        var definition = new WorldDefinition(
            map,
            [
                new CountryDefinition(new CountryId(0), "Country 0"),
                new CountryDefinition(new CountryId(1), "Country 1"),
            ],
            scenario,
            [
                new CommodityDefinition(new CommodityId(Grain), "Grain", CommodityCategory.Raw),
                new CommodityDefinition(new CommodityId(Timber), "Timber", CommodityCategory.Raw),
            ],
            extraction: new ExtractionSettings(width),
            civilianTypes:
            [
                new CivilianTypeDefinition(new CivilianTypeId(Farmer), "Farmer", workTurns),
                new CivilianTypeDefinition(new CivilianTypeId(Forester), "Forester", workTurns),
            ]);
        return new WorldState(definition);
    }
}
