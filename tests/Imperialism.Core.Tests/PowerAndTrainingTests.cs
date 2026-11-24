using Imperialism.Core;
using Xunit;

namespace Imperialism.Core.Tests;

/// <summary>
/// The Power Plant and the Trade School, the two things on the Industry screen that
/// change how much labour a country has rather than spending it.
/// </summary>
/// <remarks>
/// The numbers are the original's own help resources: "1 fuel → 6 power, with up to
/// 100 fuel per turn", and "untrained worker + 1 paper + $100 → trained worker; trained
/// worker + 2 paper + $1,000 → expert worker". The shape is the manual's: power "is used
/// in advance of any of your human labour" and cannot be stored, and training "takes him
/// out of the labour pool for the turn". See <c>docs/formulas/production.md</c> and
/// <c>docs/formulas/migration.md</c>.
/// </remarks>
public sealed class PowerAndTrainingTests
{
    private const int Fabric = 0;
    private const int Clothing = 1;
    private const int Paper = 2;
    private const int Fuel = 3;

    /// <summary>What the workforce eats, and nothing else touches.</summary>
    private const int Fish = 4;

    private static readonly CountryId Home = new(0);
    private static readonly ProductionRecipeId ClothingRecipe = new(0);
    private static readonly ProductionFacilityId PowerPlant = new(1);

    // ---- Power -------------------------------------------------------------

    [Fact]
    public void EachUnitOfFuelAddsSixLabour()
    {
        // Nobody works here at all, so every cycle is paid for in power: ten fuel is
        // sixty labour, which is thirty cycles at two labour each.
        var state = CreateState(fuel: 10, fabric: 100);

        var events = Resolve(state, Orders(burnFuel: 10, production: [new(ClothingRecipe, 100)]));

        var power = Assert.Single(events.OfType<PowerGeneratedEvent>());
        Assert.Equal(10, power.FuelBurned);
        Assert.Equal(60, power.PowerMade);
        Assert.Equal(60, power.PowerSpent);
        Assert.Equal(0, power.PowerWasted);
        Assert.Equal(TurnPhase.Production, power.Phase);
        Assert.Equal(30, Assert.Single(events.OfType<ProductionCompletedEvent>()).CompletedCycles);
        Assert.Equal(0, Stock(state, Fuel));
    }

    [Fact]
    public void BurningMoreThanTheTurnLimitIsRefusedAndBurnsNothing()
    {
        // Refused, not trimmed to the hundred: a chosen rule, and the same
        // all-or-nothing shape a technology purchase has.
        var state = CreateState(fuel: 200, fabric: 100);

        var events = Resolve(state, Orders(burnFuel: 101, production: [new(ClothingRecipe, 100)]));

        var refused = Assert.Single(events.OfType<PowerRefusedEvent>());
        Assert.Equal(PowerRefusal.OverTurnLimit, refused.Reason);
        Assert.Equal(101, refused.FuelRequested);
        Assert.Empty(events.OfType<PowerGeneratedEvent>());
        Assert.Equal(200, Stock(state, Fuel));
        Assert.Equal(0, Assert.Single(events.OfType<ProductionCompletedEvent>()).CompletedCycles);
    }

    [Fact]
    public void ExactlyTheTurnLimitBurns()
    {
        var state = CreateState(fuel: 200);

        var events = Resolve(state, Orders(burnFuel: 100));

        Assert.Equal(600, Assert.Single(events.OfType<PowerGeneratedEvent>()).PowerMade);
        Assert.Equal(100, Stock(state, Fuel));
    }

    [Fact]
    public void WithoutAPowerPlantNothingBurns()
    {
        var state = CreateState(fuel: 50, plantCapacity: 0, fabric: 100);

        var events = Resolve(state, Orders(burnFuel: 10, production: [new(ClothingRecipe, 5)]));

        Assert.Equal(PowerRefusal.NoPowerPlant, Assert.Single(events.OfType<PowerRefusedEvent>()).Reason);
        Assert.Equal(50, Stock(state, Fuel));
        Assert.Equal(0, Assert.Single(events.OfType<ProductionCompletedEvent>()).CompletedCycles);
    }

    [Fact]
    public void BurningMoreFuelThanTheWarehouseHoldsIsRefused()
    {
        var state = CreateState(fuel: 9);

        var events = Resolve(state, Orders(burnFuel: 10));

        Assert.Equal(PowerRefusal.NotEnoughFuel, Assert.Single(events.OfType<PowerRefusedEvent>()).Reason);
        Assert.Equal(9, Stock(state, Fuel));
    }

    [Fact]
    public void AWorldWithoutPowerRefusesToBurn()
    {
        var state = CreateState(fuel: 10, withPower: false);

        var events = Resolve(state, Orders(burnFuel: 10));

        Assert.Equal(PowerRefusal.NotModelled, Assert.Single(events.OfType<PowerRefusedEvent>()).Reason);
        Assert.Equal(10, Stock(state, Fuel));
    }

    [Fact]
    public void PowerIsSpentBeforeAnyHumanLabour()
    {
        // Twelve power and ten workers; three clothing cycles want six labour. Power
        // goes first, so all six come from the plant and the ten workers are left
        // whole — which the railyard, the one other thing drawing on the pool, then
        // spends in full on five points. Draw people first and the railyard would
        // find four labour and build two.
        var state = CreateState(untrained: 10, fuel: 2, fabric: 100, paper: 100, withRailyard: true);

        var events = Resolve(state, Orders(
            burnFuel: 2,
            production: [new(ClothingRecipe, 3)],
            buildTransportCapacity: 5));

        var power = Assert.Single(events.OfType<PowerGeneratedEvent>());
        Assert.Equal(12, power.PowerMade);
        Assert.Equal(6, power.PowerSpent);
        Assert.Equal(6, power.PowerWasted);
        Assert.Equal(3, Assert.Single(events.OfType<ProductionCompletedEvent>()).CompletedCycles);
        Assert.Equal(10, Assert.Single(events.OfType<TransportCapacityBuiltEvent>()).LabourUsed);
        Assert.Equal(5, state.GetTransportCapacity(Home));
    }

    [Fact]
    public void PowerAndPeopleTogetherPayForOneRecipe()
    {
        // Six power and three workers make nine labour, four cycles, with the last
        // point unspendable — the pool is one total, as the manual says.
        var state = CreateState(untrained: 3, fuel: 1, fabric: 100);

        var events = Resolve(state, Orders(burnFuel: 1, production: [new(ClothingRecipe, 10)]));

        Assert.Equal(4, Assert.Single(events.OfType<ProductionCompletedEvent>()).CompletedCycles);
        Assert.Equal(6, Assert.Single(events.OfType<PowerGeneratedEvent>()).PowerSpent);
    }

    [Fact]
    public void UnspentPowerIsGoneByTheNextTurn()
    {
        // Sixty power made and nothing asked of it. The next turn, with no burn and
        // nobody working, has nothing to spend.
        var state = CreateState(fuel: 10, fabric: 100);

        var first = Resolve(state, Orders(burnFuel: 10));
        var second = Resolve(state, Orders(production: [new(ClothingRecipe, 30)]));

        Assert.Equal(60, Assert.Single(first.OfType<PowerGeneratedEvent>()).PowerWasted);
        Assert.Empty(second.OfType<PowerGeneratedEvent>());
        Assert.Equal(0, Assert.Single(second.OfType<ProductionCompletedEvent>()).CompletedCycles);
        Assert.Equal(100, Stock(state, Fabric));
    }

    [Fact]
    public void BurningNothingSaysNothing()
    {
        var state = CreateState(fuel: 10);

        var events = Resolve(state, Orders());

        Assert.Empty(events.OfType<PowerGeneratedEvent>());
        Assert.Empty(events.OfType<PowerRefusedEvent>());
        Assert.Equal(10, Stock(state, Fuel));
    }

    // ---- Training ----------------------------------------------------------

    [Fact]
    public void TrainingAnUntrainedWorkerCostsOnePaperAndAHundred()
    {
        var state = CreateState(untrained: 3, paper: 10, cash: 1000);

        var events = Resolve(state, Orders(training: new(2, 0)));

        var trained = Assert.Single(events.OfType<WorkersTrainedEvent>());
        Assert.Equal(WorkerGrade.Untrained, trained.From);
        Assert.Equal(WorkerGrade.Trained, trained.To);
        Assert.Equal(2, trained.Trained);
        Assert.Equal(200, trained.CashPaid);
        Assert.Equal(new CommodityQuantity(new CommodityId(Paper), 2), Assert.Single(trained.Paid));
        Assert.Equal(8, Stock(state, Paper));
        Assert.Equal(800, state.GetCash(Home));
        Assert.Equal(1, state.GetWorkers(Home, WorkerGrade.Untrained));
        Assert.Equal(2, state.GetWorkers(Home, WorkerGrade.Trained));
    }

    [Fact]
    public void TrainingATrainedWorkerCostsTwoPaperAndAThousand()
    {
        var state = CreateState(trained: 2, paper: 10, cash: 5000);

        var events = Resolve(state, Orders(training: new(0, 2)));

        var trained = Assert.Single(events.OfType<WorkersTrainedEvent>());
        Assert.Equal(WorkerGrade.Expert, trained.To);
        Assert.Equal(6, Stock(state, Paper));
        Assert.Equal(3000, state.GetCash(Home));
        Assert.Equal(0, state.GetWorkers(Home, WorkerGrade.Trained));
        Assert.Equal(2, state.GetWorkers(Home, WorkerGrade.Expert));
    }

    [Fact]
    public void TraineesSupplyNothingThisTurnAndTheirNewGradeNextTurn()
    {
        // Four untrained workers, two sent to school. This turn the other two supply
        // two labour: one cycle. Next turn two untrained and two trained supply
        // 2 + 4 = 6: three cycles.
        var state = CreateState(untrained: 4, paper: 10, cash: 1000, fabric: 100);

        var first = Resolve(state, Orders(training: new(2, 0), production: [new(ClothingRecipe, 10)]));
        var second = Resolve(state, Orders(production: [new(ClothingRecipe, 10)]));

        Assert.Equal(1, Assert.Single(first.OfType<ProductionCompletedEvent>()).CompletedCycles);
        Assert.Equal(3, Assert.Single(second.OfType<ProductionCompletedEvent>()).CompletedCycles);
    }

    [Fact]
    public void TraineesStillEat()
    {
        var state = CreateState(untrained: 4, paper: 10, cash: 1000);

        var events = Resolve(state, Orders(training: new(2, 0)));

        var fed = Assert.Single(events.OfType<WorkersFedEvent>());
        Assert.Equal(4, fed.WellFed);
        Assert.Equal(996, Stock(state, Fish));
    }

    [Fact]
    public void TooLittlePaperIsRefusedAndChargesNothing()
    {
        var state = CreateState(untrained: 4, paper: 1, cash: 1000, fabric: 100);

        var events = Resolve(state, Orders(training: new(2, 0), production: [new(ClothingRecipe, 10)]));

        AssertRefusedAndUncharged(
            state, events, WorkerTrainingRefusal.NotEnoughCommodities, paper: 1, cash: 1000, untrained: 4);
    }

    [Fact]
    public void TooLittleCashIsRefusedAndChargesNothing()
    {
        var state = CreateState(untrained: 4, paper: 10, cash: 199, fabric: 100);

        var events = Resolve(state, Orders(training: new(2, 0), production: [new(ClothingRecipe, 10)]));

        AssertRefusedAndUncharged(
            state, events, WorkerTrainingRefusal.NotEnoughCash, paper: 10, cash: 199, untrained: 4);
    }

    [Fact]
    public void TooFewWorkersIsRefusedAndChargesNothing()
    {
        var state = CreateState(untrained: 4, paper: 10, cash: 1000, fabric: 100);

        var events = Resolve(state, Orders(training: new(5, 0), production: [new(ClothingRecipe, 10)]));

        AssertRefusedAndUncharged(
            state, events, WorkerTrainingRefusal.NotEnoughWorkers, paper: 10, cash: 1000, untrained: 4);
    }

    [Fact]
    public void AWorldWithoutATradeSchoolRefusesTraining()
    {
        var state = CreateState(untrained: 4, paper: 10, cash: 1000, withTraining: false);

        var events = Resolve(state, Orders(training: new(1, 0)));

        Assert.Equal(
            WorkerTrainingRefusal.NotOffered,
            Assert.Single(events.OfType<WorkerTrainingRefusedEvent>()).Reason);
        Assert.Equal(4, state.GetWorkers(Home, WorkerGrade.Untrained));
    }

    [Fact]
    public void EachStepIsPaidForSeparatelyAndInGradeOrder()
    {
        // $1,050 covers one untrained worker at $100 and not a trained one at $1,000
        // after it. The first is charged and the second refused whole.
        var state = CreateState(untrained: 1, trained: 1, paper: 10, cash: 1050);

        var events = Resolve(state, Orders(training: new(1, 1)));

        Assert.Equal(WorkerGrade.Untrained, Assert.Single(events.OfType<WorkersTrainedEvent>()).From);
        var refused = Assert.Single(events.OfType<WorkerTrainingRefusedEvent>());
        Assert.Equal(WorkerGrade.Trained, refused.From);
        Assert.Equal(WorkerTrainingRefusal.NotEnoughCash, refused.Reason);
        Assert.Equal(950, state.GetCash(Home));
        Assert.Equal(9, Stock(state, Paper));
        Assert.Equal(2, state.GetWorkers(Home, WorkerGrade.Trained));
        Assert.Equal(0, state.GetWorkers(Home, WorkerGrade.Expert));
    }

    [Fact]
    public void ANegativeOrderIsMalformedRatherThanRefused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new WorkerTrainingOrder(-1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CountryTurnOrders(Home, burnFuel: -1));
    }

    private static void AssertRefusedAndUncharged(
        WorldState state,
        IReadOnlyList<TurnEvent> events,
        WorkerTrainingRefusal reason,
        long paper,
        long cash,
        long untrained)
    {
        var refused = Assert.Single(events.OfType<WorkerTrainingRefusedEvent>());
        Assert.Equal(reason, refused.Reason);
        Assert.Equal(TurnPhase.Production, refused.Phase);
        Assert.Empty(events.OfType<WorkersTrainedEvent>());
        Assert.Equal(paper, Stock(state, Paper));
        Assert.Equal(cash, state.GetCash(Home));
        Assert.Equal(untrained, state.GetWorkers(Home, WorkerGrade.Untrained));
        Assert.Equal(0, state.GetWorkers(Home, WorkerGrade.Trained));

        // Nobody left the pool either: four untrained workers, two cycles.
        Assert.Equal(2, Assert.Single(events.OfType<ProductionCompletedEvent>()).CompletedCycles);
    }

    private static long Stock(WorldState state, int commodity) =>
        state.GetAvailableQuantity(Home, new CommodityId(commodity));

    private static CountryTurnOrders Orders(
        long burnFuel = 0,
        WorkerTrainingOrder training = default,
        IEnumerable<ProductionOrder>? production = null,
        long buildTransportCapacity = 0) =>
        new(
            Home,
            production: production,
            buildTransportCapacity: buildTransportCapacity,
            burnFuel: burnFuel,
            training: training);

    private static IReadOnlyList<TurnEvent> Resolve(WorldState state, CountryTurnOrders orders) =>
        TurnResolver.Resolve(state, new TurnOrders([orders]), 0).Events;

    private static WorldState CreateState(
        long untrained = 0,
        long trained = 0,
        long fabric = 0,
        long paper = 0,
        long fuel = 0,
        long cash = 0,
        long plantCapacity = 1,
        bool withPower = true,
        bool withTraining = true,
        bool withRailyard = false)
    {
        var map = new MapDefinition(
            new MapDimensions(1, 1),
            [new CellDefinition(
                new CellIndex(0),
                new HexCoord(0, 0),
                new TerrainId(0),
                CellRegion.Unassigned)]);

        var stock = new List<InitialCommodityStock>
        {
            new(Home, new CommodityId(Fish), 1000),
        };
        foreach (var (commodity, quantity) in new[] { (Fabric, fabric), (Paper, paper), (Fuel, fuel) })
        {
            if (quantity > 0)
            {
                stock.Add(new InitialCommodityStock(Home, new CommodityId(commodity), quantity));
            }
        }

        var workforce = untrained + trained > 0
            ? new[] { new InitialWorkforce(Home, untrained, trained, 0) }
            : [];

        var scenario = new ScenarioDefinition(
            "Industry",
            1856,
            [],
            initialInventory: stock,
            initialProductionCapacities: plantCapacity > 0
                ? [new InitialProductionCapacity(Home, PowerPlant, plantCapacity)]
                : [],
            initialWorkforce: workforce,
            initialCash: cash > 0 ? [new InitialCash(Home, cash)] : []);

        // The factory is uncapped so every cap here is labour or the warehouse. The
        // plant is capped, like the refinery it is modelled on, so that holding one
        // means something.
        var facilities = new[]
        {
            new ProductionFacilityDefinition(
                new ProductionFacilityId(0), "Clothing Factory", ProductionCapacityMode.Unlimited),
            new ProductionFacilityDefinition(PowerPlant, "Power Plant", ProductionCapacityMode.Limited),
        };

        var recipes = new[]
        {
            new ProductionRecipeDefinition(
                ClothingRecipe,
                "Clothing",
                new ProductionFacilityId(0),
                1,
                2,
                [new CommodityQuantity(new CommodityId(Fabric), 2)],
                [new CommodityQuantity(new CommodityId(Clothing), 1)]),
        };

        return new WorldState(new WorldDefinition(
            map,
            [new CountryDefinition(Home, "Home")],
            scenario,
            [
                new CommodityDefinition(new CommodityId(Fabric), "Fabric", CommodityCategory.Material),
                new CommodityDefinition(new CommodityId(Clothing), "Clothing", CommodityCategory.Material),
                new CommodityDefinition(new CommodityId(Paper), "Paper", CommodityCategory.Material),
                new CommodityDefinition(new CommodityId(Fuel), "Fuel", CommodityCategory.Material),
                new CommodityDefinition(new CommodityId(Fish), "Fish", CommodityCategory.Raw),
            ],
            facilities,
            recipes,
            feeding: new FeedingSettings([new FoodPreference([new CommodityId(Fish)])], [1, 2, 4]),
            transport: withRailyard
                ? new TransportSettings([new CommodityQuantity(new CommodityId(Paper), 1)], 2)
                : null,
            workerTraining: withTraining
                ? new WorkerTrainingSettings(
                [
                    new WorkerTrainingRule(
                        WorkerGrade.Untrained, [new CommodityQuantity(new CommodityId(Paper), 1)], 100),
                    new WorkerTrainingRule(
                        WorkerGrade.Trained, [new CommodityQuantity(new CommodityId(Paper), 2)], 1000),
                ])
                : null,
            power: withPower
                ? new PowerSettings(new CommodityId(Fuel), PowerPlant, 6, 100)
                : null));
    }
}
