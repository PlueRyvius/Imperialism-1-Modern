using Imperialism.Content;
using Imperialism.Core;
using Xunit;

namespace Imperialism.Presentation.Tests;

/// <summary>
/// Row 4e-3: the Trade School, the Power Plant and the University in the draft, and the
/// previews that restate Core's <c>Production</c> planning for them. Every warning is
/// checked against the event Core emits for the same orders, and every claim against the
/// turn Core resolves.
/// </summary>
public sealed class InstitutionOrdersTests
{
    private static readonly CountryId Blue = OrdersFixture.Blue;
    private static readonly CountryId Red = OrdersFixture.Red;

    private static readonly CivilianTypeId Farmer = new(0);
    private static readonly CivilianTypeId Engineer = new(1);
    private static readonly CivilianTypeId Driller = new(2);
    private static readonly CivilianTypeId Developer = new(3);

    private static readonly CivilianUnitId BlueFarmer = new(1);
    private static readonly CivilianUnitId RedFarmer = new(2);

    /// <summary>
    /// <see cref="OrdersFixture"/>'s economy with paper, coal, a Power Plant gated behind
    /// Dynamite (the fixture's stand-in for Oil Drilling, which Blue does not hold), the
    /// Trade School's two steps, four civilian types and a Regular. Blue has 4 untrained,
    /// 2 trained and 3 experts — 20 labour at 1/2/4 — a Farmer on its capital, and Red a
    /// Farmer on its own.
    /// </summary>
    private static CompiledWorldPackage Compile(
        long paper = 8,
        long coal = 150,
        long cash = 5000,
        long plant = 2,
        long powerPerFuel = 4,
        bool power = true,
        bool trainedStep = true) => OrdersFixture.Compile(document =>
    {
        document.Commodities =
        [
            .. document.Commodities,
            new CommodityContentDefinition
            {
                Key = "commodity.paper", Name = "Paper", Category = CommodityCategory.Material,
                WorldPrice = 300, TradeOrder = 4,
            },
            new CommodityContentDefinition { Key = "commodity.coal", Name = "Coal", Category = CommodityCategory.Raw },
        ];
        document.ProductionFacilities =
        [
            .. document.ProductionFacilities,
            new ProductionFacilityContentDefinition
            {
                Key = "facility.power-plant",
                Name = "Power Plant",
                CapacityLadder = new CapacityLadderContent { Rungs = [2, 4], Increment = 4 },
                RequiredTechnology = "technology.dynamite",
            },
        ];
        if (power)
        {
            document.Power = new PowerContentSettings
            {
                Fuel = "commodity.coal", Facility = "facility.power-plant",
                PowerPerFuel = powerPerFuel, MaxFuelPerTurn = 100,
            };
        }

        document.WorkerTraining =
        [
            new WorkerTrainingContent
            {
                From = WorkerGrade.Untrained, CashCost = 100,
                Cost = [new CommodityQuantityContent { Commodity = "commodity.paper", Quantity = 1 }],
            },
            .. trainedStep
                ? new[]
                {
                    new WorkerTrainingContent
                    {
                        From = WorkerGrade.Trained, CashCost = 1000,
                        Cost = [new CommodityQuantityContent { Commodity = "commodity.paper", Quantity = 2 }],
                    },
                }
                : [],
        ];

        CivilianPurchaseContent Price(string? technology = null) => new()
        {
            ExpertWorkers = 1,
            Commodities = [new CommodityQuantityContent { Commodity = "commodity.paper", Quantity = 1 }],
            Cash = 500,
            RequiredTechnology = technology,
        };
        document.CivilianTypes =
        [
            new CivilianTypeContentDefinition { Key = "civilian.farmer", Name = "Farmer", WorkTurns = 3, Purchase = Price() },
            new CivilianTypeContentDefinition { Key = "civilian.engineer", Name = "Engineer", WorkTurns = 3, Purchase = Price() },
            new CivilianTypeContentDefinition
            {
                Key = "civilian.driller", Name = "Driller", WorkTurns = 3, Purchase = Price("technology.dynamite"),
            },
            new CivilianTypeContentDefinition { Key = "civilian.developer", Name = "Developer", WorkTurns = 3 },
        ];
        document.ArmyTypes =
        [
            new ArmyTypeContentDefinition
            {
                Key = "army.regulars",
                Name = "Regulars",
                Category = ArmyCategory.RegularInfantry,
                Era = 1,
                Purchase = new ArmyPurchaseContent
                {
                    WorkerGrade = WorkerGrade.Untrained,
                    Bill = [new CommodityQuantityContent { Commodity = "commodity.steel", Quantity = 1 }],
                    Cash = 500,
                },
                Arms = 1,
            },
        ];

        foreach (var scenario in document.Scenarios)
        {
            scenario.InitialInventory =
            [
                .. scenario.InitialInventory.Where(static item => item.Commodity != "commodity.grain"),
                Stock("commodity.grain", 30),
                Stock("commodity.paper", paper),
                Stock("commodity.coal", coal),
            ];
            scenario.Workers =
            [
                new WorkforceContent { Country = "country.blue", Untrained = 4, Trained = 2, Expert = 3 },
                new WorkforceContent { Country = "country.red", Untrained = 4, Trained = 2, Expert = 1 },
            ];
            scenario.Cash =
            [
                new CountryCashContent { Country = "country.blue", Amount = cash },
                new CountryCashContent { Country = "country.red", Amount = 20000 },
            ];
            if (plant > 0)
            {
                scenario.ProductionCapacities =
                [
                    .. scenario.ProductionCapacities,
                    new InitialProductionCapacityContent
                    {
                        Country = "country.blue", Facility = "facility.power-plant", Quantity = plant,
                    },
                ];
            }

            scenario.Civilians =
            [
                new CivilianContent { Country = "country.blue", Type = "civilian.farmer", Cell = 0 },
                new CivilianContent { Country = "country.red", Type = "civilian.farmer", Cell = 1 },
            ];
        }
    });

    private static InitialInventoryContent Stock(string commodity, long quantity) =>
        new() { Country = "country.blue", Commodity = commodity, Quantity = quantity };

    private sealed class Game(CompiledWorldPackage package)
    {
        public CompiledWorldPackage Package { get; } = package;

        public WorldState State { get; } = OrdersFixture.NewState(package);

        public OrdersDraft Draft { get; } = new(package.GetWorld(OrdersFixture.Scenario), Blue);

        public int Turn { get; private set; } = 1;

        public OrdersPreview Preview() => OrdersPreview.Create(Package, OrdersFixture.Scenario, State, Draft);

        public CommodityId Commodity(string key) => OrdersFixture.Commodity(Package, key);

        /// <summary>Resolves the draft, checks every row-4e-3 warning against Core, and advances.</summary>
        public IReadOnlyList<TurnEvent> EndTurn(CountryTurnOrders? red = null)
        {
            var preview = Preview();
            var orders = red is null ? Draft.BuildTurnOrders() : new TurnOrders([Draft.Build(), red]);
            var events = TurnResolver.Resolve(State, orders, (ulong)Turn).Events;
            AssertPreviewMatches(preview, events);
            Draft.AdvanceTurn();
            Turn++;
            return events;
        }
    }

    /// <summary>
    /// Every drafted Trade School step, University row, disband and fuel order carries the
    /// refusal Core gives it, and every accepted one claims what Core charged.
    /// </summary>
    private static void AssertPreviewMatches(OrdersPreview preview, IReadOnlyList<TurnEvent> events)
    {
        var training = events
            .Where(static item => item is WorkersTrainedEvent { Country.Value: 0 } or WorkerTrainingRefusedEvent { Country.Value: 0 })
            .Select(static item => item switch
            {
                WorkersTrainedEvent trained => (trained.From, (WorkerTrainingRefusal?)null),
                WorkerTrainingRefusedEvent refused => (refused.From, refused.Reason),
                _ => throw new InvalidOperationException(),
            });
        Assert.Equal(
            training,
            preview.TradeSchool.Steps.Where(static step => step.Drafted > 0).Select(static step => (step.From, step.Warning?.Refusal)));
        Assert.Equal(
            events.OfType<WorkersTrainedEvent>().Where(static item => item.Country == Blue).Sum(static item => item.CashPaid),
            preview.TradeSchool.CashClaimed);

        var purchases = events
            .Where(static item => item is CiviliansPurchasedEvent { Country.Value: 0 } or CivilianPurchaseRefusedEvent { Country.Value: 0 })
            .Select(static item => item switch
            {
                CiviliansPurchasedEvent bought => (bought.Type, (CivilianPurchaseRefusal?)null),
                CivilianPurchaseRefusedEvent refused => (refused.Type, refused.Reason),
                _ => throw new InvalidOperationException(),
            })
            .OrderBy(static item => item.Type.Value);
        Assert.Equal(
            purchases,
            preview.University.Types.Where(static row => row.Drafted > 0).Select(static row => (row.Type, row.Warning?.Refusal)));
        var bought = events.OfType<CiviliansPurchasedEvent>().Where(static item => item.Country == Blue).ToArray();
        Assert.Equal(bought.Sum(static item => item.CashPaid), preview.University.CashClaimed);
        Assert.Equal(bought.Sum(static item => item.ExpertsTaken), preview.University.ExpertsTaken);

        var disbands = events
            .Where(static item => item is CivilianDisbandedEvent { Country.Value: 0 } or CivilianDisbandRefusedEvent { Country.Value: 0 })
            .Select(static item => item switch
            {
                CivilianDisbandedEvent done => (done.Unit, (CivilianDisbandRefusal?)null, done.ExpertsReturned),
                CivilianDisbandRefusedEvent refused => (refused.Unit, refused.Reason, 0L),
                _ => throw new InvalidOperationException(),
            })
            .OrderBy(static item => item.Unit.Value);
        Assert.Equal(
            disbands,
            preview.University.Disbands.Select(static row => (row.Unit, row.Warning?.Refusal, row.ExpertsReturned)));

        var generated = events.OfType<PowerGeneratedEvent>().SingleOrDefault(static item => item.Country == Blue);
        var refusedPower = events.OfType<PowerRefusedEvent>().SingleOrDefault(static item => item.Country == Blue);
        Assert.Equal(refusedPower?.Reason, preview.PowerPlant.Warning?.Refusal);
        Assert.Equal(generated?.PowerMade ?? 0, preview.PowerPlant.PowerMade);
        Assert.Equal(generated?.PowerSpent ?? 0, preview.PowerPlant.PowerSpent);
        Assert.Equal(generated?.PowerWasted ?? 0, preview.PowerPlant.PowerLost);
    }

    [Fact]
    public void BuildMapsEachRowOneToOneAndADisbandReplacesTheCivilianOrder()
    {
        var game = new Game(Compile());
        var draft = game.Draft;
        draft.SetTraining(WorkerGrade.Untrained, 3);
        draft.SetTraining(WorkerGrade.Trained, 1);
        draft.SetTraining(WorkerGrade.Untrained, 2);
        draft.BurnFuel = 5;
        draft.SetCivilianPurchase(Engineer, 2);
        draft.SetCivilianPurchase(Farmer, 1);
        draft.SetCivilianPurchase(Engineer, 1);
        draft.SetCivilianPurchase(Driller, 0);

        var orders = draft.Build();
        Assert.Equal(new WorkerTrainingOrder(2, 1), orders.Training);
        Assert.Equal(5, orders.BurnFuel);
        Assert.Equal([new CivilianPurchaseOrder(Engineer, 1), new CivilianPurchaseOrder(Farmer, 1)], orders.CivilianPurchases);
        Assert.Throws<ArgumentOutOfRangeException>(() => draft.SetTraining(WorkerGrade.Expert, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => draft.SetTraining(WorkerGrade.Untrained, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => draft.BurnFuel = -1);
        Assert.Throws<ArgumentOutOfRangeException>(() => draft.SetCivilianPurchase(new CivilianTypeId(9), 1));

        // Core takes one order per civilian, so a disband and a work order replace each other.
        draft.SetWork(BlueFarmer, new CellIndex(0));
        draft.SetDisband(BlueFarmer, true);
        Assert.True(draft.IsDisbanding(BlueFarmer));
        Assert.Null(draft.GetUnitOrder(BlueFarmer));
        Assert.Equal([BlueFarmer], draft.Build().CivilianDisbands);
        draft.SetWork(BlueFarmer, new CellIndex(0));
        Assert.False(draft.IsDisbanding(BlueFarmer));
        Assert.Empty(draft.Build().CivilianDisbands);
        draft.ClearUnitOrder(BlueFarmer);

        draft.SetTraining(WorkerGrade.Untrained, 0);
        draft.SetTraining(WorkerGrade.Trained, 0);
        draft.BurnFuel = 0;
        draft.ClearCivilianPurchases();
        Assert.True(draft.IsEmpty);
    }

    [Fact]
    public void EveryInstitutionOrderIsClearedAtEndTurn()
    {
        var game = new Game(Compile());
        game.Draft.SetTraining(WorkerGrade.Untrained, 1);
        game.Draft.SetTraining(WorkerGrade.Trained, 1);
        game.Draft.BurnFuel = 1;
        game.Draft.SetCivilianPurchase(Engineer, 1);
        game.Draft.SetDisband(BlueFarmer, true);

        game.EndTurn();

        Assert.Equal(0, game.Draft.GetTraining(WorkerGrade.Untrained));
        Assert.Equal(0, game.Draft.GetTraining(WorkerGrade.Trained));
        Assert.Equal(0, game.Draft.BurnFuel);
        Assert.Empty(game.Draft.CivilianPurchases);
        Assert.Empty(game.Draft.Disbands);
        Assert.True(game.Draft.IsEmpty);
    }

    [Theory]
    [InlineData(WorkerGrade.Untrained, 4, 8, 5000, true, null)]
    [InlineData(WorkerGrade.Untrained, 5, 8, 5000, true, WorkerTrainingRefusal.NotEnoughWorkers)]
    [InlineData(WorkerGrade.Untrained, 3, 2, 5000, true, WorkerTrainingRefusal.NotEnoughCommodities)]
    [InlineData(WorkerGrade.Untrained, 3, 8, 250, true, WorkerTrainingRefusal.NotEnoughCash)]
    [InlineData(WorkerGrade.Trained, 1, 8, 5000, false, WorkerTrainingRefusal.NotOffered)]
    public void TheTradeSchoolPreviewRefusesWhatCoreRefuses(
        WorkerGrade from, long count, long paper, long cash, bool trainedStep, WorkerTrainingRefusal? expected)
    {
        var game = new Game(Compile(paper: paper, cash: cash, trainedStep: trainedStep));
        var workers = game.State.GetWorkers(Blue, from);
        game.Draft.SetTraining(from, count);

        var step = game.Preview().TradeSchool.Steps.Single(row => row.From == from);
        Assert.Equal(expected, step.Warning?.Refusal);
        Assert.False(step.Warning?.OrdersSetAside ?? false);
        Assert.Equal(expected is null ? count : 0, step.LabourWithheld);

        game.EndTurn();

        Assert.Equal(workers - (expected is null ? count : 0), game.State.GetWorkers(Blue, from));
    }

    [Fact]
    public void TheTradeSchoolShowsEachStepsPriceAndItsTrainees()
    {
        var game = new Game(Compile());
        game.Draft.SetTraining(WorkerGrade.Untrained, 2);
        game.Draft.SetTraining(WorkerGrade.Trained, 1);

        var school = game.Preview().TradeSchool;
        var (untrained, trained) = (school.Steps[0], school.Steps[1]);
        Assert.Equal((WorkerGrade.Untrained, (WorkerGrade?)WorkerGrade.Trained), (untrained.From, untrained.To));
        Assert.Equal(("commodity.paper", 1L), (untrained.CostPerWorker.Single().CommodityKey, untrained.CostPerWorker.Single().Quantity));
        Assert.Equal((100L, 1L, 4L), (untrained.CashPerWorker, untrained.LabourPerWorker, untrained.Healthy));
        Assert.Equal((1000L, 2L, 2L, 2L), (trained.CashPerWorker, trained.LabourPerWorker, trained.Healthy, trained.LabourWithheld));
        Assert.Equal(1200, school.CashClaimed);
        Assert.Equal(4, school.LabourWithheld);

        game.EndTurn();

        // The trainees work at their new grade from this turn on.
        Assert.Equal((2L, 3L, 4L), (
            game.State.GetWorkers(Blue, WorkerGrade.Untrained),
            game.State.GetWorkers(Blue, WorkerGrade.Trained),
            game.State.GetWorkers(Blue, WorkerGrade.Expert)));
    }

    [Theory]
    [InlineData(50, 150, 2, true, null)]
    [InlineData(101, 150, 2, true, PowerRefusal.OverTurnLimit)]
    [InlineData(100, 150, 2, true, null)]
    [InlineData(50, 10, 2, true, PowerRefusal.NotEnoughFuel)]
    [InlineData(1, 150, 0, true, PowerRefusal.NoPowerPlant)]
    [InlineData(1, 150, 2, false, PowerRefusal.NotModelled)]
    public void ThePowerPlantPreviewRefusesWhatCoreRefuses(
        long fuel, long coal, long plant, bool power, PowerRefusal? expected)
    {
        var game = new Game(Compile(coal: coal, plant: plant, power: power));
        game.Draft.BurnFuel = fuel;

        var view = game.Preview().PowerPlant;
        Assert.Equal(power, view.IsModelled);
        Assert.Equal(expected, view.Warning?.Refusal);
        Assert.Equal(expected is null ? fuel * 4 : 0, view.PowerMade);

        game.EndTurn();

        // Refused, not trimmed: an order over the cap burns nothing at all.
        var coalId = game.Commodity("commodity.coal");
        Assert.Equal(coal - (expected is null ? fuel : 0), game.State.GetAvailableQuantity(Blue, coalId));
    }

    [Fact]
    public void RecipesDrawPowerBeforeLabourAndWhatIsLeftIsLost()
    {
        var game = new Game(Compile());
        game.Draft.BurnFuel = 2;
        game.Draft.SetProduction(OrdersFixture.Recipe(game.Package, "recipe.lumber"), 1);

        var preview = game.Preview();
        var view = preview.PowerPlant;
        Assert.Equal(("commodity.coal", "facility.power-plant", 2L), (view.FuelKey, view.PlantKey, view.PlantCapacity));
        Assert.Equal(("technology.dynamite", true), (view.RequiredTechnologyKey, view.IsGatedByTechnology));
        Assert.Equal((4L, 100L, 150L), (view.PowerPerFuel, view.MaxFuelPerTurn, view.FuelHeld));
        Assert.Equal((8L, 2L, 6L), (view.PowerMade, view.PowerSpent, view.PowerLost));

        // The recipe's two labour came from the plant, so the pool is untouched.
        Assert.Equal(0, preview.Industry.LabourCommitted);

        var events = game.EndTurn();
        Assert.Contains(events, item => item is PowerGeneratedEvent { Country.Value: 0, PowerMade: 8, PowerSpent: 2 });
    }

    [Fact]
    public void PowerLetsRecipesRunPastTheLabourPool()
    {
        // The Trade School, the University and the Armoury take 16 of the 20 labour and
        // canned food two more, so lumber gets one cycle of its two; four power carries
        // the rest, as Core limits a recipe by power plus labour.
        var game = new Game(Compile());
        var lumber = OrdersFixture.Recipe(game.Package, "recipe.lumber");
        game.Draft.SetTraining(WorkerGrade.Untrained, 2);
        game.Draft.SetTraining(WorkerGrade.Trained, 2);
        game.Draft.SetCivilianPurchase(Farmer, 2);
        game.Draft.SetRegimentPurchase(ArmyCategory.RegularInfantry, 2);
        game.Draft.SetProduction(OrdersFixture.Recipe(game.Package, "recipe.canned-food"), 1);
        game.Draft.SetProduction(lumber, 2);

        RecipePreview Lumber() => game.Preview().Industry.Recipes.Single(static row => row.RecipeKey == "recipe.lumber");
        Assert.Equal((1L, RecipeLimit.Labour), (Lumber().ExpectedCycles, Lumber().LimitedBy));
        game.Draft.BurnFuel = 1;
        var recipe = Lumber();
        Assert.Equal(2, recipe.ExpectedCycles);
        Assert.Equal((4L, 4L), (game.Preview().PowerPlant.PowerMade, game.Preview().PowerPlant.PowerSpent));

        var events = game.EndTurn();
        Assert.Equal(
            recipe.ExpectedCycles,
            events.OfType<ProductionCompletedEvent>().Single(item => item.Country == Blue && item.Recipe == lumber).CompletedCycles);
    }

    [Theory]
    [InlineData(1, 1, 8, 5000, null, null)]
    [InlineData(2, 2, 8, 5000, null, CivilianPurchaseRefusal.NotEnoughExperts)]
    [InlineData(4, 0, 8, 5000, CivilianPurchaseRefusal.NotEnoughExperts, null)]
    [InlineData(2, 1, 2, 5000, null, CivilianPurchaseRefusal.NotEnoughCommodities)]
    [InlineData(2, 1, 8, 1200, null, CivilianPurchaseRefusal.NotEnoughCash)]
    public void TheUniversityPreviewRefusesWhatCoreRefusesInOrder(
        long farmers, long engineers, long paper, long cash,
        CivilianPurchaseRefusal? farmerRefusal, CivilianPurchaseRefusal? engineerRefusal)
    {
        var game = new Game(Compile(paper: paper, cash: cash));
        game.Draft.SetCivilianPurchase(Farmer, farmers);
        game.Draft.SetCivilianPurchase(Engineer, engineers);
        game.Draft.SetCivilianPurchase(Driller, 1);
        game.Draft.SetCivilianPurchase(Developer, 1);

        var university = game.Preview().University;
        Assert.Equal(farmerRefusal, university.Types[Farmer.Value].Warning?.Refusal);
        Assert.Equal(engineers == 0 ? null : engineerRefusal, university.Types[Engineer.Value].Warning?.Refusal);
        Assert.Equal(CivilianPurchaseRefusal.TechnologyNotKnown, university.Types[Driller.Value].Warning?.Refusal);
        Assert.Equal(CivilianPurchaseRefusal.NotPurchasable, university.Types[Developer.Value].Warning?.Refusal);
        Assert.True(university.Types[Driller.Value].IsGatedByTechnology);
        Assert.Equal("technology.dynamite", university.Types[Driller.Value].RequiredTechnologyKey);
        Assert.False(university.Types[Developer.Value].IsForSale);
        Assert.Equal(new CellIndex(0), university.ArrivesAt);

        var accepted = (farmerRefusal is null ? farmers : 0) + (engineerRefusal is null ? engineers : 0);
        Assert.Equal(accepted, university.ExpertsTaken);
        Assert.Equal(accepted * 500, university.CashClaimed);
        Assert.Equal(accepted * 4, university.LabourWithheld);

        var experts = game.State.GetWorkers(Blue, WorkerGrade.Expert);
        var events = game.EndTurn();

        // The expert leaves at once and for good; the civilian stands on the capital
        // from Delivery and takes its first order next turn.
        Assert.Equal(experts - accepted, game.State.GetWorkers(Blue, WorkerGrade.Expert));
        Assert.Equal(
            accepted,
            events.OfType<CiviliansDeliveredEvent>().Where(item => item.Country == Blue && item.Cell == university.ArrivesAt)
                .Sum(static item => item.Units.Count));
    }

    [Fact]
    public void ADisbandReturnsOneExpertNextTurnAndRefundsNothing()
    {
        var game = new Game(Compile());
        game.Draft.SetDisband(BlueFarmer, true);
        game.Draft.SetDisband(RedFarmer, true);
        game.Draft.SetDisband(new CivilianUnitId(99), true);

        var preview = game.Preview();
        var disbands = preview.University.Disbands;
        Assert.Equal([BlueFarmer, RedFarmer, new CivilianUnitId(99)], disbands.Select(static row => row.Unit));
        Assert.Equal(("Farmer", 1L, (CivilianDisbandRefusal?)null), (disbands[0].TypeName, disbands[0].ExpertsReturned, disbands[0].Warning?.Refusal));
        Assert.Equal(CivilianDisbandRefusal.NotYours, disbands[1].Warning?.Refusal);
        Assert.Equal((null, CivilianDisbandRefusal.NoSuchCivilian), (disbands[2].TypeName, disbands[2].Warning?.Refusal));
        Assert.All(disbands, static row => Assert.Equal(0, row.Refunded));

        // The returning expert is not in this turn's pool.
        Assert.Equal(20, preview.Industry.LabourAvailable);
        var cash = game.State.GetCash(Blue);

        game.EndTurn();

        Assert.Null(game.State.GetCivilian(BlueFarmer));
        Assert.NotNull(game.State.GetCivilian(RedFarmer));
        Assert.Equal(4, game.State.GetWorkers(Blue, WorkerGrade.Expert));
        Assert.Equal(cash, game.State.GetCash(Blue));
    }

    /// <summary>
    /// The gap the Armoury row left: the warehouse, the labour pool and the treasury count
    /// every claim in Core's order — the Trade School, the University, the Armoury, the
    /// fuel, power before people in the recipes, the railyard, the Capitol, then the market
    /// and every later cash claim — and one resolved turn agrees with all of them.
    /// </summary>
    [Fact]
    public void OneTurnOfEveryProductionClaimAgreesWithCore()
    {
        var game = new Game(Compile());
        var paper = game.Commodity("commodity.paper");
        var steel = game.Commodity("commodity.steel");
        var lumber = game.Commodity("commodity.lumber");
        var draft = game.Draft;
        draft.SetTraining(WorkerGrade.Untrained, 2);                       // 2 paper, $200, 2 labour
        draft.SetTraining(WorkerGrade.Trained, 2);                         // 4 paper, $2,000, 4 labour
        draft.SetCivilianPurchase(Farmer, 1);                              // 1 paper, $500, 4 labour
        draft.SetRegimentPurchase(ArmyCategory.RegularInfantry, 2);        // 2 steel, $1,000, 2 labour
        draft.BurnFuel = 1;                                                // 4 power
        draft.SetProduction(OrdersFixture.Recipe(game.Package, "recipe.lumber"), 2);      // 4 labour
        draft.SetProduction(OrdersFixture.Recipe(game.Package, "recipe.canned-food"), 1); // 2 labour
        draft.BuildTransportCapacity = 10;
        draft.RecruitWorkers = 1;
        draft.SetOffer(paper, 10);
        draft.SetOffer(steel, 10);
        draft.SetOffer(lumber, 10);
        draft.SetTechnologyPurchase(OrdersFixture.Technology(game.Package, "technology.a"), true);

        var preview = game.Preview();
        Assert.Equal((2200L, 6L), (preview.TradeSchool.CashClaimed, preview.TradeSchool.LabourWithheld));
        Assert.Equal(500, preview.University.CashClaimed);
        Assert.Equal(1000, preview.Armoury.CashClaimed);
        Assert.Equal((4L, 4L, 0L), (preview.PowerPlant.PowerMade, preview.PowerPlant.PowerSpent, preview.PowerPlant.PowerLost));

        // 20 labour: 12 withheld, 2 left over from the recipes after power's 4, so the
        // railyard's six points are bound by labour, not by its 8 steel or 10 lumber.
        Assert.Equal(20, preview.Industry.LabourAvailable);
        Assert.Equal(6, preview.Industry.Railyard!.ExpectedPoints);
        Assert.Equal(20, preview.Industry.LabourCommitted);

        var rows = preview.Trade.Rows.ToDictionary(static row => row.CommodityKey);
        Assert.Equal((8L, 7L, 1L), (rows["commodity.paper"].Stock, rows["commodity.paper"].ClaimedByIndustry, rows["commodity.paper"].Sellable));
        Assert.Equal((10L, 8L, 2L), (rows["commodity.steel"].Stock, rows["commodity.steel"].ClaimedByIndustry, rows["commodity.steel"].Sellable));
        Assert.Equal(4, rows["commodity.lumber"].Sellable);

        // Every cash claim before the bids and the research, in Core's order.
        Assert.Equal((2200L, 500L, 1000L), (preview.Trade.ClaimedByTradeSchool, preview.Trade.ClaimedByUniversity, preview.Trade.ClaimedByArmoury));
        Assert.Equal((2200L, 500L, 1000L), (preview.Technology.ClaimedByTradeSchool, preview.Technology.ClaimedByUniversity, preview.Technology.ClaimedByArmoury));
        Assert.Equal(3700, preview.Diplomacy.ClaimedBeforeDiplomacy);
        Assert.Equal((2200L, 500L), (preview.Diplomacy.ClaimedByTradeSchool, preview.Diplomacy.ClaimedByUniversity));
        Assert.Equal((2200L, 500L), (preview.Civilians.ClaimedByTradeSchool, preview.Civilians.ClaimedByUniversity));

        // $5,000 less $3,700 leaves $1,300 for research: Alpha's $1,000 is affordable.
        Assert.False(preview.Technology.IsOverCash);
        // Delta's $4,500 more is not, once the Production claims are counted.
        draft.SetTechnologyPurchase(OrdersFixture.Technology(game.Package, "technology.d"), true);
        Assert.True(game.Preview().Technology.IsOverCash);
        draft.SetTechnologyPurchase(OrdersFixture.Technology(game.Package, "technology.d"), false);

        var red = new CountryTurnOrders(Red, tradeBids: [new TradeOrder(paper, 10), new TradeOrder(steel, 10), new TradeOrder(lumber, 10)]);
        var events = game.EndTurn(red);

        Assert.Equal(
            preview.Industry.Railyard.ExpectedPoints,
            events.OfType<TransportCapacityBuiltEvent>().Where(item => item.Country == Blue).Sum(static item => item.ToCapacity - item.FromCapacity));
        Assert.Equal(preview.Industry.Recruitment!.ExpectedRecruits, events.OfType<WorkersRecruitedEvent>().Where(item => item.Country == Blue).Sum(static item => item.Recruited));
        foreach (var commodity in new[] { paper, steel, lumber })
        {
            var row = preview.Trade.Rows.Single(item => item.CommodityKey == game.Package.Catalog.GetKey(commodity));
            Assert.Equal(row.Sellable, events.OfType<CommodityTradedEvent>()
                .Where(item => item.Seller == Blue && item.Commodity == commodity).Sum(static item => item.Quantity));
        }

        Assert.Contains(events, item => item is TechnologyPurchasedEvent { Country.Value: 0 });
    }
}
