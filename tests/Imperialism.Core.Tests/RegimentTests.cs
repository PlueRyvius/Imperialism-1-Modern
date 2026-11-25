using Imperialism.Core;
using Xunit;

namespace Imperialism.Core.Tests;

/// <summary>
/// The Armoury and deployment: buying regiments with a worker, a bill and cash, and
/// moving them by march to an adjacent province or by rail further.
/// </summary>
/// <remarks>
/// The fixture's catalogue is a six-row excerpt of the executable's thirty, with the
/// executable's own prices; nothing here depends on the excerpt being complete. The
/// chosen rules are in <c>docs/formulas/army.md</c>: 1–11 here, 12 onwards for the
/// battles in <see cref="LandBattleTests"/>.
/// </remarks>
public sealed class RegimentTests
{
    private const int Armaments = 0;
    private const int Horses = 1;
    private const int Fuel = 2;
    private const int Grain = 3;
    private const int Fish = 4;
    private const int Meat = 5;

    private const int Minutemen = 0;
    private const int Hussars = 1;
    private const int Scouts = 2;
    private const int Regulars = 3;
    private const int Grenadiers = 4;
    private const int Artillery = 5;

    private const int Width = 8;

    private static readonly CountryId Home = new(0);
    private static readonly CountryId Other = new(1);
    private static readonly TechnologyId Bessemer = new(0);

    /// <summary>The capital's province. Rail runs 0–1–2; 3 to 6 are Home's and off it; 7 is Other's.</summary>
    private static readonly ProvinceId CapitalProvince = new(0);

    // ---- Buying -----------------------------------------------------------

    [Fact]
    public void ARegularCostsAnUntrainedWorkerAnArmamentAndFiveHundred()
    {
        var state = CreateState(untrained: 3, armaments: 10, cash: 5000);

        var events = Resolve(state, Orders(buy: [new(ArmyCategory.RegularInfantry, 2)]));

        var bought = Assert.Single(events.OfType<RegimentsPurchasedEvent>());
        Assert.Equal(TurnPhase.Production, bought.Phase);
        Assert.Equal(Type(Regulars), bought.Type);
        Assert.Equal(2, bought.Count);
        Assert.Equal(WorkerGrade.Untrained, bought.WorkerGrade);
        Assert.Equal(2, bought.WorkersTaken);
        Assert.Equal(1000, bought.CashPaid);
        Assert.Equal(new CommodityQuantity(new CommodityId(Armaments), 2), Assert.Single(bought.Paid));
        Assert.Equal(8, Stock(state, Armaments));
        Assert.Equal(4000, state.GetCash(Home));
        Assert.Equal(1, state.GetWorkers(Home, WorkerGrade.Untrained));
    }

    [Fact]
    public void ACavalryRegimentAlsoTakesAHorse()
    {
        var state = CreateState(untrained: 1, armaments: 10, horses: 3, cash: 5000);

        var events = Resolve(state, Orders(buy: [new(ArmyCategory.LightCavalry, 1)]));

        var bought = Assert.Single(events.OfType<RegimentsPurchasedEvent>());
        Assert.Equal(Type(Hussars), bought.Type);
        Assert.Equal(9, Stock(state, Armaments));
        Assert.Equal(2, Stock(state, Horses));
        Assert.Equal(4900, state.GetCash(Home));
    }

    [Fact]
    public void AHeavierTypeTakesATrainedWorkerAndNotAnUntrainedOne()
    {
        var state = CreateState(untrained: 5, trained: 1, armaments: 10, cash: 5000);

        Resolve(state, Orders(buy: [new(ArmyCategory.HeavyInfantry, 1)]));

        Assert.Equal(5, state.GetWorkers(Home, WorkerGrade.Untrained));
        Assert.Equal(0, state.GetWorkers(Home, WorkerGrade.Trained));
    }

    [Fact]
    public void ACategoryBuildsTheNextEraTheTurnAfterItsTechnologyIsBought()
    {
        var state = CreateState(untrained: 4, armaments: 10, horses: 10, cash: 10_000);

        var first = Resolve(state, Orders(
            buy: [new(ArmyCategory.LightCavalry, 1)], buyTechnology: [Bessemer]));
        var second = Resolve(state, Orders(buy: [new(ArmyCategory.LightCavalry, 1)]));

        Assert.Equal(Type(Hussars), Assert.Single(first.OfType<RegimentsPurchasedEvent>()).Type);
        var scouts = Assert.Single(second.OfType<RegimentsPurchasedEvent>());
        Assert.Equal(Type(Scouts), scouts.Type);
        Assert.Equal(2000, scouts.CashPaid);
        Assert.Equal(
            [new CommodityQuantity(new CommodityId(Armaments), 2), new CommodityQuantity(new CommodityId(Horses), 1)],
            scouts.Paid);
    }

    [Fact]
    public void AnEraOneCategoryNeedsNoTechnology()
    {
        var state = CreateState(trained: 1, armaments: 10, cash: 5000);

        var events = Resolve(state, Orders(buy: [new(ArmyCategory.HeavyArtillery, 1)]));

        Assert.Equal(Type(Artillery), Assert.Single(events.OfType<RegimentsPurchasedEvent>()).Type);
    }

    [Theory]
    [InlineData(0, 10, 5000, RegimentPurchaseRefusal.NotEnoughWorkers)]
    [InlineData(3, 1, 5000, RegimentPurchaseRefusal.NotEnoughCommodities)]
    [InlineData(3, 10, 999, RegimentPurchaseRefusal.NotEnoughCash)]
    public void AShortageOfAnyOneRefusesTheOrderAndChargesNothing(
        long untrained, long armaments, long cash, RegimentPurchaseRefusal reason)
    {
        var state = CreateState(untrained: untrained, armaments: armaments, cash: cash);

        var events = Resolve(state, Orders(buy: [new(ArmyCategory.RegularInfantry, 2)]));

        AssertRefusedAndUncharged(state, events, reason, untrained, armaments, cash);
    }

    [Fact]
    public void ARefusedOrderDoesNotStopALaterOne()
    {
        var state = CreateState(untrained: 1, armaments: 10, cash: 5000);

        var events = Resolve(state, Orders(buy:
        [
            new(ArmyCategory.RegularInfantry, 2),
            new(ArmyCategory.RegularInfantry, 1),
        ]));

        Assert.Equal(
            RegimentPurchaseRefusal.NotEnoughWorkers,
            Assert.Single(events.OfType<RegimentPurchaseRefusedEvent>()).Reason);
        Assert.Equal(1, Assert.Single(events.OfType<RegimentsPurchasedEvent>()).Count);
        Assert.Single(state.GetRegiments(Home));
    }

    [Fact]
    public void ACategoryMayRepeatAndEachOrderSpendsWhatTheLastLeft()
    {
        var state = CreateState(untrained: 2, armaments: 10, cash: 5000);

        var events = Resolve(state, Orders(buy:
        [
            new(ArmyCategory.RegularInfantry, 1),
            new(ArmyCategory.RegularInfantry, 1),
            new(ArmyCategory.RegularInfantry, 1),
        ]));

        Assert.Equal(2, events.OfType<RegimentsPurchasedEvent>().Count());
        Assert.Equal(
            RegimentPurchaseRefusal.NotEnoughWorkers,
            Assert.Single(events.OfType<RegimentPurchaseRefusedEvent>()).Reason);
    }

    [Fact]
    public void MilitiaIsNotBuiltInTheArmoury()
    {
        var state = CreateState(untrained: 3, armaments: 10, cash: 5000);

        var events = Resolve(state, Orders(buy: [new(ArmyCategory.Militia, 1)]));

        AssertRefusedAndUncharged(state, events, RegimentPurchaseRefusal.NotPurchasable, 3, 10, 5000);
    }

    [Fact]
    public void AWorldWithNoCatalogueBuildsNothing()
    {
        var state = CreateState(untrained: 3, armaments: 10, cash: 5000, withCatalogue: false);

        var events = Resolve(state, Orders(buy: [new(ArmyCategory.RegularInfantry, 1)]));

        AssertRefusedAndUncharged(state, events, RegimentPurchaseRefusal.NotPurchasable, 3, 10, 5000);
    }

    [Fact]
    public void AnOverflowingOrderIsRefusedRatherThanThrown()
    {
        var state = CreateState(untrained: 3, armaments: 10, cash: 5000);

        var events = Resolve(state, Orders(buy: [new(ArmyCategory.RegularInfantry, long.MaxValue)]));

        AssertRefusedAndUncharged(state, events, RegimentPurchaseRefusal.NotEnoughWorkers, 3, 10, 5000);
    }

    [Fact]
    public void OnlyHealthyWorkersCanBeSent()
    {
        // Only fish in the warehouse and half the workforce wants meat, so after the
        // first turn one of the two untrained workers is sick.
        var state = CreateState(untrained: 2, armaments: 10, cash: 5000, halfWantMeat: true);
        Resolve(state, Orders());
        Assert.Equal(1, state.GetSickWorkers(Home, WorkerGrade.Untrained));

        var events = Resolve(state, Orders(buy: [new(ArmyCategory.RegularInfantry, 2)]));

        AssertRefusedAndUncharged(state, events, RegimentPurchaseRefusal.NotEnoughWorkers, 2, 10, 5000);
    }

    [Fact]
    public void TheWorkerLeavesTheLabourPoolTheTurnTheRegimentIsBought()
    {
        // Two untrained workers are two labour, one cycle of the fixture's recipe. One
        // goes to the Armoury before any recipe runs, so no cycle can be paid for.
        var bought = CreateState(untrained: 2, armaments: 10, cash: 5000, grain: 10);
        var control = CreateState(untrained: 2, armaments: 10, cash: 5000, grain: 10);

        var withPurchase = Resolve(bought, Orders(
            buy: [new(ArmyCategory.RegularInfantry, 1)], production: [new(new ProductionRecipeId(0), 5)]));
        var without = Resolve(control, Orders(production: [new(new ProductionRecipeId(0), 5)]));

        Assert.DoesNotContain(
            withPurchase.OfType<ProductionCompletedEvent>(),
            static produced => produced.CompletedCycles > 0);
        Assert.Equal(1, Assert.Single(without.OfType<ProductionCompletedEvent>()).CompletedCycles);
    }

    // ---- Delivery ---------------------------------------------------------

    [Fact]
    public void ABoughtRegimentStandsInTheCapitalsProvinceFromTheDeliveryPhase()
    {
        var state = CreateState(untrained: 3, armaments: 10, cash: 5000);

        var events = Resolve(state, Orders(buy: [new(ArmyCategory.RegularInfantry, 2)]));

        var delivered = Assert.Single(events.OfType<RegimentsDeliveredEvent>());
        Assert.Equal(TurnPhase.Delivery, delivered.Phase);
        Assert.Equal(CapitalProvince, delivered.Province);
        Assert.Equal(Type(Regulars), delivered.Type);
        var regiments = state.GetRegiments(Home);
        Assert.Equal(delivered.Regiments, regiments.Select(static regiment => regiment.Id));
        Assert.All(regiments, regiment =>
        {
            Assert.Equal(CapitalProvince, regiment.Province);
            Assert.Equal(Type(Regulars), regiment.Type);
            Assert.Equal(Home, regiment.Owner);
        });
    }

    [Fact]
    public void ARegimentCannotMoveTheTurnItIsBought()
    {
        // Learn the id the purchase hands out from a twin, then order that id to march
        // in the same turn. Conflict runs before Delivery, so it does not exist yet.
        var twin = CreateState(untrained: 1, armaments: 10, cash: 5000);
        Resolve(twin, Orders(buy: [new(ArmyCategory.RegularInfantry, 1)]));
        var predicted = Assert.Single(twin.GetRegiments(Home)).Id;

        var state = CreateState(untrained: 1, armaments: 10, cash: 5000);
        var events = Resolve(state, Orders(
            buy: [new(ArmyCategory.RegularInfantry, 1)],
            move: [new([predicted], Province(1))]));

        Assert.Equal(
            RegimentMoveRefusal.NoSuchRegiment,
            Assert.Single(events.OfType<RegimentMoveRefusedEvent>()).Reason);
        Assert.Equal(CapitalProvince, Assert.Single(state.GetRegiments(Home)).Province);
    }

    [Fact]
    public void WithoutACapitalNothingIsBought()
    {
        var state = CreateState(untrained: 3, armaments: 10, cash: 5000, withCapital: false);

        var events = Resolve(state, Orders(buy: [new(ArmyCategory.RegularInfantry, 1)]));

        AssertRefusedAndUncharged(state, events, RegimentPurchaseRefusal.NoCapital, 3, 10, 5000);
    }

    [Fact]
    public void AScenarioArmyBecomesARegimentOfItsProvincesOwner()
    {
        // Experience is in hundredths here: two medals in the record are 200.
        var state = CreateState(armies: [new(Province(3), Type(Regulars), 200), new(Province(7), Type(Hussars), 0)]);

        var home = Assert.Single(state.GetRegiments(Home));
        Assert.Equal(Province(3), home.Province);
        Assert.Equal(200, home.Experience);
        Assert.Equal(2, home.Medals);
        Assert.Equal(Regiment.MaxHealth, home.Health);
        Assert.Equal(Type(Hussars), Assert.Single(state.GetRegiments(Other)).Type);
    }

    // ---- Moving -----------------------------------------------------------

    [Fact]
    public void AMarchToAnAdjacentOwnProvinceIsFree()
    {
        var state = CreateState(capacity: 0, armies: [Army(3, Regulars)]);
        var regiment = Single(state);

        var events = Resolve(state, Orders(move: [new([regiment], Province(4))]));

        var moved = Assert.Single(events.OfType<RegimentsMovedEvent>());
        Assert.Equal(TurnPhase.Conflict, moved.Phase);
        Assert.Equal(RegimentMoveMode.March, moved.Mode);
        Assert.Equal(0, moved.CapacityUsed);
        Assert.Equal(Province(3), moved.From);
        Assert.Equal(Province(4), state.GetRegiment(regiment)!.Province);
    }

    [Fact]
    public void ANonAdjacentProvinceOffTheRailIsRefused()
    {
        var state = CreateState(capacity: 100, armies: [Army(0, Regulars)]);
        var regiment = Single(state);

        var events = Resolve(state, Orders(move: [new([regiment], Province(4))]));

        AssertMoveRefused(state, events, RegimentMoveRefusal.NotConnectedByRail, regiment, CapitalProvince);
    }

    [Fact]
    public void RailWithExactlyEnoughCapacityCarriesTheWholeOrder()
    {
        var state = CreateState(capacity: 10, armies: [Army(0, Regulars), Army(0, Regulars)]);
        var regiments = state.GetRegiments(Home).Select(static regiment => regiment.Id).ToArray();

        var events = Resolve(state, Orders(move: [new(regiments, Province(2))]));

        var moved = Assert.Single(events.OfType<RegimentsMovedEvent>());
        Assert.Equal(RegimentMoveMode.Rail, moved.Mode);
        Assert.Equal(10, moved.CapacityUsed);
        Assert.All(state.GetRegiments(Home), regiment => Assert.Equal(Province(2), regiment.Province));
    }

    [Fact]
    public void OnePointShortRefusesTheWholeOrder()
    {
        var state = CreateState(capacity: 9, armies: [Army(0, Regulars), Army(0, Regulars)]);
        var regiments = state.GetRegiments(Home).Select(static regiment => regiment.Id).ToArray();

        var events = Resolve(state, Orders(move: [new(regiments, Province(2))]));

        Assert.Equal(
            RegimentMoveRefusal.NotEnoughRailCapacity,
            Assert.Single(events.OfType<RegimentMoveRefusedEvent>()).Reason);
        Assert.Empty(events.OfType<RegimentsMovedEvent>());
        Assert.All(state.GetRegiments(Home), regiment => Assert.Equal(CapitalProvince, regiment.Province));
    }

    [Fact]
    public void TheRailCostIsFivePerArmamentOfEachRegiment()
    {
        // Artillery carries two armaments, so ten capacity for one gun.
        var state = CreateState(capacity: 10, armies: [Army(0, Artillery)]);

        var events = Resolve(state, Orders(move: [new([Single(state)], Province(2))]));

        Assert.Equal(10, Assert.Single(events.OfType<RegimentsMovedEvent>()).CapacityUsed);
    }

    [Fact]
    public void TheRailBudgetIsSpentAcrossOrdersAndNotCarriedOver()
    {
        var state = CreateState(capacity: 5, armies: [Army(0, Regulars), Army(0, Regulars)]);
        var regiments = state.GetRegiments(Home).Select(static regiment => regiment.Id).ToArray();

        var first = Resolve(state, Orders(move:
        [
            new([regiments[0]], Province(2)),
            new([regiments[1]], Province(2)),
        ]));
        var second = Resolve(state, Orders(move: [new([regiments[1]], Province(2))]));

        Assert.Single(first.OfType<RegimentsMovedEvent>());
        Assert.Equal(
            RegimentMoveRefusal.NotEnoughRailCapacity,
            Assert.Single(first.OfType<RegimentMoveRefusedEvent>()).Reason);
        Assert.Single(second.OfType<RegimentsMovedEvent>());
        Assert.Empty(second.OfType<RegimentMoveRefusedEvent>());
    }

    [Fact]
    public void TheRailBudgetAndCommodityTransportDoNotDrawOnEachOther()
    {
        // Five grain tiles and five capacity: commodity transport takes all of it, and
        // a regiment still rides five capacity of rail beside it.
        var moving = CreateState(capacity: 5, armies: [Army(0, Regulars)]);
        var control = CreateState(capacity: 5, armies: [Army(0, Regulars)]);
        TransportAllocationOrder[] allocation = [new(new CommodityId(Grain), 5)];

        var withMove = Resolve(moving, Orders(
            transport: allocation, move: [new([Single(moving)], Province(2))]));
        var without = Resolve(control, Orders(transport: allocation));

        Assert.Equal(5, Assert.Single(withMove.OfType<RegimentsMovedEvent>()).CapacityUsed);
        var carried = Assert.Single(withMove.OfType<CommoditiesTransportedEvent>());
        var controlCarried = Assert.Single(without.OfType<CommoditiesTransportedEvent>());
        Assert.Equal(new CommodityQuantity(new CommodityId(Grain), 5), Assert.Single(carried.Moved));
        Assert.Equal(controlCarried.Moved, carried.Moved);
        Assert.Equal(controlCarried.Wasted, carried.Wasted);
        Assert.Equal(5, moving.GetTransportCapacity(Home));
    }

    [Fact]
    public void MilitiaIsRefusedOnTheMarchAndByRail()
    {
        var state = CreateState(capacity: 100, armies: [Army(0, Minutemen)]);
        var regiment = Single(state);

        var march = Resolve(state, Orders(move: [new([regiment], Province(1))]));
        var rail = Resolve(state, Orders(move: [new([regiment], Province(2))]));

        AssertMoveRefused(state, march, RegimentMoveRefusal.CannotLeaveProvince, regiment, CapitalProvince);
        AssertMoveRefused(state, rail, RegimentMoveRefusal.CannotLeaveProvince, regiment, CapitalProvince);
    }

    [Fact]
    public void ATypeThatMayLeaveButNotRideMarchesAndIsRefusedTheRail()
    {
        // No imported type is shaped like this — militia is false on both flags — so
        // only a test can show the rail check stands on its own.
        var state = CreateState(capacity: 100, regularsMoveByRail: false, armies: [Army(0, Regulars)]);
        var regiment = Single(state);

        var rail = Resolve(state, Orders(move: [new([regiment], Province(2))]));
        AssertMoveRefused(state, rail, RegimentMoveRefusal.CannotMoveByRail, regiment, CapitalProvince);

        var march = Resolve(state, Orders(move: [new([regiment], Province(1))]));
        Assert.Equal(RegimentMoveMode.March, Assert.Single(march.OfType<RegimentsMovedEvent>()).Mode);
        Assert.Equal(Province(1), state.GetRegiment(regiment)!.Province);
    }

    [Fact]
    public void ARegimentMovesOnceATurn()
    {
        var state = CreateState(capacity: 100, armies: [Army(0, Regulars)]);
        var regiment = Single(state);

        var events = Resolve(state, Orders(move:
        [
            new([regiment], Province(1)),
            new([regiment], Province(2)),
        ]));

        Assert.Single(events.OfType<RegimentsMovedEvent>());
        Assert.Equal(
            RegimentMoveRefusal.AlreadyMoved,
            Assert.Single(events.OfType<RegimentMoveRefusedEvent>()).Reason);
        Assert.Equal(Province(1), state.GetRegiment(regiment)!.Province);
    }

    [Fact]
    public void AnOrderNamingAMovedRegimentIsRefusedWhole()
    {
        var state = CreateState(capacity: 100, armies: [Army(0, Regulars), Army(0, Regulars)]);
        var regiments = state.GetRegiments(Home).Select(static regiment => regiment.Id).ToArray();

        var events = Resolve(state, Orders(move:
        [
            new([regiments[0]], Province(1)),
            new(regiments, Province(1)),
        ]));

        Assert.Equal(
            RegimentMoveRefusal.AlreadyMoved,
            Assert.Single(events.OfType<RegimentMoveRefusedEvent>()).Reason);
        Assert.Equal(CapitalProvince, state.GetRegiment(regiments[1])!.Province);
    }

    [Fact]
    public void AnOrderIntoAForeignProvinceIsAnAttackAndNeedsAWar()
    {
        // Since 6b the destination decides the verb: foreign ground is an attack, and
        // with no war in effect it is refused rather than marched into.
        var state = CreateState(capacity: 100, armies: [Army(6, Regulars)]);
        var regiment = Single(state);

        var events = Resolve(state, Orders(move: [new([regiment], Province(7))]));

        AssertMoveRefused(state, events, RegimentMoveRefusal.NotAtWar, regiment, Province(6));
        Assert.Equal(Other, state.GetProvinceOwner(Province(7)));
    }

    [Fact]
    public void AnAttackIsNeverMadeByRail()
    {
        // Home's rail reaches nowhere near province 7, but the refusal is about
        // adjacency first: there is no attack from further than next door.
        var state = CreateState(capacity: 100, armies: [Army(0, Regulars)]);
        var regiment = Single(state);

        var events = Resolve(state, Orders(move: [new([regiment], Province(7))]));

        AssertMoveRefused(state, events, RegimentMoveRefusal.NotAdjacent, regiment, CapitalProvince);
    }

    [Fact]
    public void AnotherCountrysRegimentCannotBeOrdered()
    {
        var state = CreateState(capacity: 100, armies: [Army(7, Regulars)]);
        var theirs = Assert.Single(state.GetRegiments(Other)).Id;

        var events = Resolve(state, Orders(move: [new([theirs], Province(6))]));

        AssertMoveRefused(state, events, RegimentMoveRefusal.NotYours, theirs, Province(7));
    }

    [Fact]
    public void RegimentsInDifferentProvincesCannotMoveAsOne()
    {
        var state = CreateState(capacity: 100, armies: [Army(0, Regulars), Army(1, Regulars)]);
        var regiments = state.GetRegiments(Home).Select(static regiment => regiment.Id).ToArray();

        var events = Resolve(state, Orders(move: [new(regiments, Province(2))]));

        Assert.Equal(
            RegimentMoveRefusal.NotTogether,
            Assert.Single(events.OfType<RegimentMoveRefusedEvent>()).Reason);
    }

    [Fact]
    public void AMalformedMoveIsRefusedWithAnEventNotThrown()
    {
        var state = CreateState(capacity: 100, armies: [Army(0, Regulars)]);
        var regiment = Single(state);

        var events = Resolve(state, Orders(move:
        [
            new([], Province(1)),
            new([regiment, regiment], Province(1)),
            new([new RegimentId(99)], Province(1)),
            new([regiment], new ProvinceId(Width + 5)),
            new([regiment], CapitalProvince),
        ]));

        Assert.Equal(
            [
                RegimentMoveRefusal.NoRegiments,
                RegimentMoveRefusal.DuplicateRegiment,
                RegimentMoveRefusal.NoSuchRegiment,
                RegimentMoveRefusal.UnknownProvince,
                RegimentMoveRefusal.SameProvince,
            ],
            events.OfType<RegimentMoveRefusedEvent>().Select(static refused => refused.Reason));
        Assert.Equal(CapitalProvince, state.GetRegiment(regiment)!.Province);
    }

    [Fact]
    public void NoOrdersMoveNobodyAndSpendNothing()
    {
        var state = CreateState(untrained: 2, armaments: 10, cash: 5000, armies: [Army(0, Regulars)]);

        var events = Resolve(state, Orders());

        Assert.Empty(events.OfType<RegimentsPurchasedEvent>());
        Assert.Empty(events.OfType<RegimentsMovedEvent>());
        Assert.Empty(events.OfType<RegimentsDeliveredEvent>());
        Assert.Equal(CapitalProvince, Assert.Single(state.GetRegiments(Home)).Province);
        Assert.Equal(5000, state.GetCash(Home));
    }

    // ---- Adjacency --------------------------------------------------------

    [Fact]
    public void ProvinceAdjacencyMakesNoWrapAssumption()
    {
        // Five columns by two rows, one province per column. Odd-r rows reach only
        // the next column, so a province borders its two neighbours and, without a
        // seam, the edges border nothing beyond.
        var flat = ColumnMap(5, 2, wraps: false);
        var seam = ColumnMap(5, 2, wraps: true);
        var adjacency = new ProvinceAdjacency(flat);

        Assert.Equal([Province(1)], adjacency.GetNeighbours(Province(0)));
        Assert.Equal([Province(1), Province(3)], adjacency.GetNeighbours(Province(2)));
        Assert.Equal([Province(3)], adjacency.GetNeighbours(Province(4)));
        Assert.False(adjacency.AreAdjacent(Province(0), Province(4)));
        Assert.False(adjacency.AreAdjacent(Province(0), Province(2)));
        Assert.True(new ProvinceAdjacency(seam).AreAdjacent(Province(0), Province(4)));
    }

    [Fact]
    public void ACatalogueMustBeDenseAndAScenarioArmyMustNameAType()
    {
        Assert.Throws<ArgumentException>(() => CreateState(
            armies: [new(Province(0), new ArmyTypeId(40), 0)]));
    }

    // ---- Fixture ----------------------------------------------------------

    private static MapDefinition ColumnMap(int width, int height, bool wraps)
    {
        var dimensions = new MapDimensions(width, height);
        var cells = Enumerable.Range(0, width * height)
            .Select(index =>
            {
                var cell = new CellIndex(index);
                var coordinate = dimensions.GetCoordinate(cell);
                return new CellDefinition(
                    cell, coordinate, new TerrainId(0), CellRegion.ForProvince(new ProvinceId(coordinate.Column)));
            })
            .ToArray();
        return new MapDefinition(
            dimensions,
            cells,
            Enumerable.Range(0, width).Select(static index => new ProvinceDefinition(new ProvinceId(index), $"P{index}")),
            wrapsHorizontally: wraps);
    }

    private static void AssertRefusedAndUncharged(
        WorldState state,
        IReadOnlyList<TurnEvent> events,
        RegimentPurchaseRefusal reason,
        long untrained,
        long armaments,
        long cash)
    {
        var refused = Assert.Single(events.OfType<RegimentPurchaseRefusedEvent>());
        Assert.Equal(reason, refused.Reason);
        Assert.Equal(TurnPhase.Production, refused.Phase);
        Assert.Empty(events.OfType<RegimentsPurchasedEvent>());
        Assert.Empty(events.OfType<RegimentsDeliveredEvent>());
        Assert.Equal(untrained, state.GetWorkers(Home, WorkerGrade.Untrained));
        Assert.Equal(armaments, Stock(state, Armaments));
        Assert.Equal(cash, state.GetCash(Home));
        Assert.Empty(state.GetRegiments(Home));
    }

    private static void AssertMoveRefused(
        WorldState state,
        IReadOnlyList<TurnEvent> events,
        RegimentMoveRefusal reason,
        RegimentId regiment,
        ProvinceId stillIn)
    {
        var refused = Assert.Single(events.OfType<RegimentMoveRefusedEvent>());
        Assert.Equal(reason, refused.Reason);
        Assert.Equal(TurnPhase.Conflict, refused.Phase);
        Assert.Equal(Home, refused.Country);
        Assert.Empty(events.OfType<RegimentsMovedEvent>());
        Assert.Equal(stillIn, state.GetRegiment(regiment)!.Province);
    }

    private static ArmyTypeId Type(int value) => new(value);

    private static ProvinceId Province(int value) => new(value);

    private static InitialArmy Army(int province, int type) => new(Province(province), Type(type), 0);

    private static RegimentId Single(WorldState state) => Assert.Single(state.GetRegiments(Home)).Id;

    private static long Stock(WorldState state, int commodity) =>
        state.GetAvailableQuantity(Home, new CommodityId(commodity));

    private static CountryTurnOrders Orders(
        IEnumerable<RegimentPurchaseOrder>? buy = null,
        IEnumerable<RegimentMoveOrder>? move = null,
        IEnumerable<ProductionOrder>? production = null,
        IEnumerable<TransportAllocationOrder>? transport = null,
        IEnumerable<TechnologyId>? buyTechnology = null) =>
        new(
            Home,
            production: production,
            transport: transport,
            buyTechnology: buyTechnology,
            regimentPurchases: buy,
            regimentMoves: move);

    private static IReadOnlyList<TurnEvent> Resolve(WorldState state, CountryTurnOrders orders) =>
        TurnResolver.Resolve(state, new TurnOrders([orders, new CountryTurnOrders(Other)]), 0).Events;

    /// <summary>
    /// Eight farm tiles in a row, one province each. Home holds 0–6 with its capital
    /// on 0, rail 0–1–2, and grain on 1–5; Other holds 7 with its capital there.
    /// </summary>
    private static WorldState CreateState(
        long untrained = 0,
        long trained = 0,
        long armaments = 0,
        long horses = 0,
        long grain = 0,
        long cash = 0,
        long capacity = 0,
        bool halfWantMeat = false,
        bool withCapital = true,
        bool withCatalogue = true,
        bool regularsMoveByRail = true,
        InitialArmy[]? armies = null)
    {
        var dimensions = new MapDimensions(Width, 1);
        var cells = Enumerable.Range(0, Width)
            .Select(index => new CellDefinition(
                new CellIndex(index),
                dimensions.GetCoordinate(new CellIndex(index)),
                new TerrainId(0),
                CellRegion.ForProvince(new ProvinceId(index)),
                index is >= 1 and <= 5 ? [new ResourceId(0)] : null,
                index is 0 or 7 ? SettlementSiteKind.Urban : SettlementSiteKind.None))
            .ToArray();

        var map = new MapDefinition(
            dimensions,
            cells,
            Enumerable.Range(0, Width)
                .Select(static index => new ProvinceDefinition(new ProvinceId(index), $"Province {index}")),
            [],
            [new ResourceDefinition(new ResourceId(0), new CommodityId(Grain), [1])],
            [new TerrainDefinition(new TerrainId(0), "Farm")]);

        var stock = new List<InitialCommodityStock> { new(Home, new CommodityId(Fish), 1000) };
        foreach (var (commodity, quantity) in new[] { (Armaments, armaments), (Horses, horses), (Grain, grain) })
        {
            if (quantity > 0)
            {
                stock.Add(new InitialCommodityStock(Home, new CommodityId(commodity), quantity));
            }
        }

        var owners = Enumerable.Range(0, Width).Select(static index => (CountryId?)(index == 7 ? Other : Home));
        var scenario = new ScenarioDefinition(
            "Armoury",
            1816,
            owners,
            initialCountryCapitals: withCapital
                ? [new CountryCapital(Home, new CellIndex(0)), new CountryCapital(Other, new CellIndex(7))]
                : [new CountryCapital(Other, new CellIndex(7))],
            initialInventory: stock,
            initialWorkforce: untrained + trained > 0 ? [new InitialWorkforce(Home, untrained, trained, 0)] : [],
            initialCash: cash > 0 ? [new InitialCash(Home, cash)] : [],
            initialRailLinks:
            [
                new CellLink(new CellIndex(0), new CellIndex(1)),
                new CellLink(new CellIndex(1), new CellIndex(2)),
            ],
            initialTransportCapacity: capacity > 0 ? [new InitialTransportCapacity(Home, capacity)] : [],
            initialArmies: armies ?? []);

        static CommodityQuantity Item(int commodity, long quantity) => new(new CommodityId(commodity), quantity);
        static ArmyCombatStats Stats() => new(10, 10, 5, null, 5, 6, 4);

        ArmyTypeDefinition[] catalogue =
        [
            new(Type(Minutemen), "Minutemen", ArmyCategory.Militia, 1,
                movesByRail: false, leavesProvince: false, stats: Stats()),
            new(Type(Hussars), "Hussars", ArmyCategory.LightCavalry, 1,
                purchase: new(WorkerGrade.Untrained, [Item(Armaments, 1), Item(Horses, 1)], 100), arms: 1),
            new(Type(Scouts), "Scouts", ArmyCategory.LightCavalry, 2, Bessemer,
                new(WorkerGrade.Untrained, [Item(Armaments, 2), Item(Horses, 1)], 2000), arms: 2),
            new(Type(Regulars), "Regulars", ArmyCategory.RegularInfantry, 1,
                purchase: new(WorkerGrade.Untrained, [Item(Armaments, 1)], 500), arms: 1,
                movesByRail: regularsMoveByRail),
            new(Type(Grenadiers), "Grenadiers", ArmyCategory.HeavyInfantry, 1,
                purchase: new(WorkerGrade.Trained, [Item(Armaments, 1)], 1000), arms: 1),
            new(Type(Artillery), "Artillery", ArmyCategory.HeavyArtillery, 1,
                purchase: new(WorkerGrade.Trained, [Item(Armaments, 2)], 1000), arms: 2),
        ];

        var definition = new WorldDefinition(
            map,
            [new CountryDefinition(Home, "Home"), new CountryDefinition(Other, "Other")],
            scenario,
            [
                new CommodityDefinition(new CommodityId(Armaments), "Armaments", CommodityCategory.Goods),
                new CommodityDefinition(new CommodityId(Horses), "Horses", CommodityCategory.Material),
                new CommodityDefinition(new CommodityId(Fuel), "Fuel", CommodityCategory.Material),
                new CommodityDefinition(new CommodityId(Grain), "Grain", CommodityCategory.Raw),
                new CommodityDefinition(new CommodityId(Fish), "Fish", CommodityCategory.Raw),
                new CommodityDefinition(new CommodityId(Meat), "Meat", CommodityCategory.Raw),
            ],
            [new ProductionFacilityDefinition(
                new ProductionFacilityId(0), "Mill", ProductionCapacityMode.Unlimited)],
            [new ProductionRecipeDefinition(
                new ProductionRecipeId(0),
                "Horse breeding",
                new ProductionFacilityId(0),
                1,
                2,
                [Item(Grain, 1)],
                [Item(Horses, 1)])],
            extraction: new ExtractionSettings(Width),
            technologies: [new TechnologyDefinition(Bessemer, "Bessemer Converter", cost: 1000)],
            feeding: new FeedingSettings(
                halfWantMeat
                    ? [new FoodPreference([new CommodityId(Fish)]), new FoodPreference([new CommodityId(Meat)])]
                    : [new FoodPreference([new CommodityId(Fish)])],
                [1, 2, 4]),
            transport: new TransportSettings([Item(Armaments, 1)]),
            armyTypes: withCatalogue ? catalogue : null);
        return new WorldState(definition);
    }
}
