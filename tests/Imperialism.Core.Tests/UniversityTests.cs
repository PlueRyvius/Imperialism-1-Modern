using Imperialism.Core;
using Xunit;

namespace Imperialism.Core.Tests;

/// <summary>
/// The University: buying civilians with expert workers, paper and cash, and
/// disbanding them back into the workforce.
/// </summary>
/// <remarks>
/// The expert is the manual's — "one expert worker for each new civilian",
/// "deducted from your work force permanently" (lines 2676–2682, 2699–2709). The
/// two paper and $1,000 used here are the importer's guess, mirroring the Trade
/// School's dear promotion, and nothing in these tests depends on the figures being
/// right. See <c>docs/formulas/university.md</c> for the seven chosen rules.
/// </remarks>
public sealed class UniversityTests
{
    private const int Grain = 0;
    private const int Fabric = 1;
    private const int Clothing = 2;
    private const int Paper = 3;
    private const int Fish = 4;
    private const int Meat = 5;

    private const int Miner = 0;
    private const int Rancher = 1;
    private const int Developer = 2;

    private static readonly CountryId Home = new(0);
    private static readonly CountryId Other = new(1);
    private static readonly CellIndex Capital = new(0);
    private static readonly CellIndex Mine = new(1);
    private static readonly CellIndex Abroad = new(2);
    private static readonly TechnologyId FeedGrasses = new(0);
    private static readonly ProductionRecipeId ClothingRecipe = new(0);

    // ---- Buying -----------------------------------------------------------

    [Fact]
    public void AMinerCostsAnExpertTwoPaperAndAThousand()
    {
        var state = CreateState(experts: 2, paper: 10, cash: 5000);

        var events = Resolve(state, Orders(buy: [new(Type(Miner), 1)]));

        var bought = Assert.Single(events.OfType<CiviliansPurchasedEvent>());
        Assert.Equal(TurnPhase.Production, bought.Phase);
        Assert.Equal(1, bought.Count);
        Assert.Equal(1, bought.ExpertsTaken);
        Assert.Equal(1000, bought.CashPaid);
        Assert.Equal(new CommodityQuantity(new CommodityId(Paper), 2), Assert.Single(bought.Paid));
        Assert.Equal(8, Stock(state, Paper));
        Assert.Equal(4000, state.GetCash(Home));
        Assert.Equal(1, state.GetWorkers(Home, WorkerGrade.Expert));
    }

    [Fact]
    public void TheExpertLeavesTheLabourPoolTheTurnTheCivilianIsBought()
    {
        // Two experts are eight labour, four clothing cycles. One goes to the
        // University before any recipe runs, so four labour is left: two cycles.
        var bought = CreateState(experts: 2, paper: 10, cash: 5000, fabric: 100);
        var control = CreateState(experts: 2, paper: 10, cash: 5000, fabric: 100);

        var withPurchase = Resolve(bought, Orders(
            buy: [new(Type(Miner), 1)], production: [new(ClothingRecipe, 10)]));
        var without = Resolve(control, Orders(production: [new(ClothingRecipe, 10)]));

        Assert.Equal(2, Assert.Single(withPurchase.OfType<ProductionCompletedEvent>()).CompletedCycles);
        Assert.Equal(4, Assert.Single(without.OfType<ProductionCompletedEvent>()).CompletedCycles);
    }

    [Fact]
    public void TheExpertIsGoneForGood()
    {
        var state = CreateState(experts: 2, paper: 10, cash: 5000, fabric: 100);

        Resolve(state, Orders(buy: [new(Type(Miner), 1)]));
        var second = Resolve(state, Orders(production: [new(ClothingRecipe, 10)]));

        Assert.Equal(1, state.GetWorkers(Home, WorkerGrade.Expert));
        Assert.Equal(2, Assert.Single(second.OfType<ProductionCompletedEvent>()).CompletedCycles);
    }

    [Fact]
    public void ABoughtCivilianArrivesOnTheCapitalInTheDeliveryPhase()
    {
        var state = CreateState(experts: 2, paper: 10, cash: 5000);

        var events = Resolve(state, Orders(buy: [new(Type(Miner), 2)]));

        var delivered = Assert.Single(events.OfType<CiviliansDeliveredEvent>());
        Assert.Equal(TurnPhase.Delivery, delivered.Phase);
        Assert.Equal(Capital, delivered.Cell);
        Assert.Equal(2, delivered.Units.Count);
        var civilians = state.GetCivilians(Home);
        Assert.Equal(2, civilians.Count);
        Assert.All(civilians, civilian =>
        {
            Assert.Equal(Type(Miner), civilian.Type);
            Assert.Equal(Capital, civilian.Cell);
            Assert.Null(civilian.Work);
        });
        Assert.Equal(delivered.Units, civilians.Select(static civilian => civilian.Id));
    }

    [Fact]
    public void ABoughtCivilianTakesOrdersFromTheNextTurn()
    {
        var state = CreateState(experts: 1, paper: 10, cash: 5000);

        Resolve(state, Orders(buy: [new(Type(Miner), 1)]));
        var miner = Assert.Single(state.GetCivilians(Home)).Id;
        var next = Resolve(state, Orders(work: [new(miner, Mine)]));

        Assert.Empty(next.OfType<CivilianOrderRefusedEvent>());
        Assert.Equal(miner, Assert.Single(next.OfType<CivilianWorkBegunEvent>()).Unit);
    }

    [Fact]
    public void ABoughtCivilianCannotBeOrderedTheTurnItIsBought()
    {
        // Learn the id the purchase hands out from a twin, then give that id an order
        // in the same turn as the purchase. It does not exist yet when Development
        // runs, so the order is refused — which is why delivery waits for the
        // Delivery phase rather than ending Construction.
        var twin = CreateState(experts: 1, paper: 10, cash: 5000);
        Resolve(twin, Orders(buy: [new(Type(Miner), 1)]));
        var predicted = Assert.Single(twin.GetCivilians(Home)).Id;

        var state = CreateState(experts: 1, paper: 10, cash: 5000);
        var events = Resolve(state, Orders(buy: [new(Type(Miner), 1)], work: [new(predicted, Mine)]));

        Assert.Equal(predicted, Assert.Single(state.GetCivilians(Home)).Id);
        var refused = Assert.Single(events.OfType<CivilianOrderRefusedEvent>());
        Assert.Equal(CivilianOrderRefusal.NoSuchCivilian, refused.Reason);
        Assert.Empty(events.OfType<CivilianWorkBegunEvent>());
    }

    [Fact]
    public void ATypeMayRepeatAndEachOrderStandsAlone()
    {
        var state = CreateState(experts: 2, paper: 10, cash: 5000);

        var events = Resolve(state, Orders(buy: [new(Type(Miner), 1), new(Type(Miner), 1)]));

        Assert.Equal(2, events.OfType<CiviliansPurchasedEvent>().Count());
        Assert.Equal(2, events.OfType<CiviliansDeliveredEvent>().Count());
        Assert.Equal(2, state.GetCivilians(Home).Count);
        Assert.Equal(0, state.GetWorkers(Home, WorkerGrade.Expert));
        Assert.Equal(3000, state.GetCash(Home));
    }

    // ---- Technology -------------------------------------------------------

    [Fact]
    public void ARancherNeedsFeedGrassesAndBuyingItOpensTheUniversityNextTurn()
    {
        // Bought this turn, known next turn: Investment runs last, and the University
        // reads knowledge as the turn opened.
        var state = CreateState(experts: 2, paper: 10, cash: 5000);

        var first = Resolve(state, Orders(buy: [new(Type(Rancher), 1)], buyTechnology: [FeedGrasses]));

        var refused = Assert.Single(first.OfType<CivilianPurchaseRefusedEvent>());
        Assert.Equal(CivilianPurchaseRefusal.TechnologyNotKnown, refused.Reason);
        Assert.True(state.HasTechnology(Home, FeedGrasses));
        Assert.Equal(4500, state.GetCash(Home));
        Assert.Equal(10, Stock(state, Paper));
        Assert.Equal(2, state.GetWorkers(Home, WorkerGrade.Expert));

        var second = Resolve(state, Orders(buy: [new(Type(Rancher), 1)]));

        Assert.Empty(second.OfType<CivilianPurchaseRefusedEvent>());
        Assert.Equal(Type(Rancher), Assert.Single(second.OfType<CiviliansPurchasedEvent>()).Type);
        Assert.Equal(Type(Rancher), Assert.Single(state.GetCivilians(Home)).Type);
    }

    [Fact]
    public void AnUngatedCivilianNeedsNoTechnology()
    {
        var state = CreateState(experts: 1, paper: 10, cash: 5000);

        var events = Resolve(state, Orders(buy: [new(Type(Miner), 1)]));

        Assert.Empty(events.OfType<CivilianPurchaseRefusedEvent>());
    }

    // ---- Refusals ---------------------------------------------------------

    [Fact]
    public void ATypeWithNoPriceIsNotForSale()
    {
        var state = CreateState(experts: 2, paper: 10, cash: 5000);

        var events = Resolve(state, Orders(buy: [new(Type(Developer), 1)]));

        AssertRefusedAndUncharged(state, events, CivilianPurchaseRefusal.NotPurchasable, 2, 10, 5000);
    }

    [Fact]
    public void ACountryWithNoCapitalCannotBuy()
    {
        var state = CreateState(experts: 2, paper: 10, cash: 5000);
        state.SetCountryCapital(Home, null);

        var events = Resolve(state, Orders(buy: [new(Type(Miner), 1)]));

        AssertRefusedAndUncharged(state, events, CivilianPurchaseRefusal.NoCapital, 2, 10, 5000);
    }

    [Fact]
    public void TooFewExpertsIsRefused()
    {
        var state = CreateState(experts: 1, paper: 10, cash: 5000);

        var events = Resolve(state, Orders(buy: [new(Type(Miner), 2)]));

        AssertRefusedAndUncharged(state, events, CivilianPurchaseRefusal.NotEnoughExperts, 1, 10, 5000);
    }

    [Fact]
    public void TooLittlePaperIsRefused()
    {
        var state = CreateState(experts: 2, paper: 3, cash: 5000);

        var events = Resolve(state, Orders(buy: [new(Type(Miner), 2)]));

        AssertRefusedAndUncharged(state, events, CivilianPurchaseRefusal.NotEnoughCommodities, 2, 3, 5000);
    }

    [Fact]
    public void TooLittleCashIsRefused()
    {
        var state = CreateState(experts: 2, paper: 10, cash: 1999);

        var events = Resolve(state, Orders(buy: [new(Type(Miner), 2)]));

        AssertRefusedAndUncharged(state, events, CivilianPurchaseRefusal.NotEnoughCash, 2, 10, 1999);
    }

    [Fact]
    public void ExactlyAffordableIsStillAccepted()
    {
        var state = CreateState(experts: 2, paper: 4, cash: 2000);

        var events = Resolve(state, Orders(buy: [new(Type(Miner), 2)]));

        Assert.Empty(events.OfType<CivilianPurchaseRefusedEvent>());
        Assert.Equal(0, Stock(state, Paper));
        Assert.Equal(0, state.GetCash(Home));
        Assert.Equal(0, state.GetWorkers(Home, WorkerGrade.Expert));
    }

    [Fact]
    public void ARefusedOrderDoesNotStopALaterOne()
    {
        // The first asks for four paper and three is all there is; the second asks
        // for two and gets it.
        var state = CreateState(experts: 2, paper: 3, cash: 5000);

        var events = Resolve(state, Orders(buy: [new(Type(Miner), 2), new(Type(Miner), 1)]));

        var refused = Assert.Single(events.OfType<CivilianPurchaseRefusedEvent>());
        Assert.Equal(CivilianPurchaseRefusal.NotEnoughCommodities, refused.Reason);
        Assert.Equal(2, refused.Requested);
        Assert.Equal(1, Assert.Single(events.OfType<CiviliansPurchasedEvent>()).Count);
        Assert.Equal(1, Stock(state, Paper));
        Assert.Equal(4000, state.GetCash(Home));
        Assert.Single(state.GetCivilians(Home));
    }

    [Fact]
    public void AnOverflowingCountIsRefusedRatherThanAbortingTheTurn()
    {
        var state = CreateState(experts: 2, paper: 10, cash: 5000);

        var events = Resolve(state, Orders(buy: [new(Type(Miner), long.MaxValue)]));

        AssertRefusedAndUncharged(state, events, CivilianPurchaseRefusal.NotEnoughExperts, 2, 10, 5000);
    }

    [Fact]
    public void AnOverflowingPaperBillIsRefusedRatherThanAbortingTheTurn()
    {
        // Enough experts that the expert check passes; the paper total would overflow.
        var state = CreateState(experts: 4, paper: 10, cash: 5000, paperPerCivilian: long.MaxValue);

        var events = Resolve(state, Orders(buy: [new(Type(Miner), 2)]));

        AssertRefusedAndUncharged(state, events, CivilianPurchaseRefusal.NotEnoughCommodities, 4, 10, 5000);
    }

    [Fact]
    public void UnknownTypesAndEmptyCountsAreRefused()
    {
        var state = CreateState(experts: 2, paper: 10, cash: 5000);

        var events = Resolve(state, Orders(buy: [new(Type(40), 1), new(Type(Miner), 0)]));

        Assert.Equal(
            [CivilianPurchaseRefusal.UnknownType, CivilianPurchaseRefusal.NonPositiveCount],
            events.OfType<CivilianPurchaseRefusedEvent>().Select(static refused => refused.Reason));
        Assert.Empty(events.OfType<CiviliansPurchasedEvent>());
    }

    [Fact]
    public void OnlyHealthyExpertsCanBeSent()
    {
        // Only fish in the warehouse and half the workforce wants meat, so after
        // the first turn one of the two experts is sick and supplies nothing.
        var state = CreateState(experts: 2, paper: 10, cash: 5000, halfWantMeat: true);
        Resolve(state, Orders());
        Assert.Equal(1, state.GetSickWorkers(Home, WorkerGrade.Expert));

        var events = Resolve(state, Orders(buy: [new(Type(Miner), 2)]));

        AssertRefusedAndUncharged(state, events, CivilianPurchaseRefusal.NotEnoughExperts, 2, 10, 5000);
    }

    // ---- Disbanding -------------------------------------------------------

    [Fact]
    public void DisbandingReturnsAnExpertWhoWorksFromTheNextTurn()
    {
        var state = CreateState(fabric: 100, civilians: [new(Home, Type(Miner), Capital)]);
        var unit = Assert.Single(state.GetCivilians(Home)).Id;

        var first = Resolve(state, Orders(disband: [unit], production: [new(ClothingRecipe, 10)]));

        var disbanded = Assert.Single(first.OfType<CivilianDisbandedEvent>());
        Assert.Equal(unit, disbanded.Unit);
        Assert.Equal(Type(Miner), disbanded.Type);
        Assert.Equal(1, disbanded.ExpertsReturned);
        Assert.Equal(TurnPhase.Production, disbanded.Phase);
        Assert.Empty(state.GetCivilians(Home));
        Assert.Null(state.GetCivilian(unit));
        Assert.Equal(1, state.GetWorkers(Home, WorkerGrade.Expert));
        Assert.Equal(0, Assert.Single(first.OfType<ProductionCompletedEvent>()).CompletedCycles);

        var second = Resolve(state, Orders(production: [new(ClothingRecipe, 10)]));

        Assert.Equal(2, Assert.Single(second.OfType<ProductionCompletedEvent>()).CompletedCycles);
    }

    [Fact]
    public void DisbandingRefundsNothing()
    {
        var state = CreateState(paper: 5, cash: 700, civilians: [new(Home, Type(Miner), Capital)]);
        var unit = Assert.Single(state.GetCivilians(Home)).Id;

        Resolve(state, Orders(disband: [unit]));

        Assert.Equal(5, Stock(state, Paper));
        Assert.Equal(700, state.GetCash(Home));
    }

    [Fact]
    public void AnUnpurchasableTypeStillDisbandsIntoAnExpert()
    {
        var state = CreateState(civilians: [new(Home, Type(Developer), Capital)]);
        var unit = Assert.Single(state.GetCivilians(Home)).Id;

        var events = Resolve(state, Orders(disband: [unit]));

        Assert.Single(events.OfType<CivilianDisbandedEvent>());
        Assert.Equal(1, state.GetWorkers(Home, WorkerGrade.Expert));
    }

    [Fact]
    public void AnotherCountrysCivilianOrAMissingOneCannotBeDisbanded()
    {
        var state = CreateState(civilians: [new(Other, Type(Miner), Abroad)]);
        var theirs = Assert.Single(state.GetCivilians(Other)).Id;

        var events = Resolve(state, Orders(disband: [theirs, new CivilianUnitId(999)]));

        Assert.Equal(
            [CivilianDisbandRefusal.NotYours, CivilianDisbandRefusal.NoSuchCivilian],
            events.OfType<CivilianDisbandRefusedEvent>().Select(static refused => refused.Reason));
        Assert.Empty(events.OfType<CivilianDisbandedEvent>());
        Assert.NotNull(state.GetCivilian(theirs));
        Assert.Equal(0, state.GetWorkers(Home, WorkerGrade.Expert));
    }

    [Fact]
    public void DisbandingAndOrderingTheSameCivilianIsAMalformedBundle()
    {
        var unit = new CivilianUnitId(1);

        Assert.Throws<ArgumentException>(() => Orders(disband: [unit], work: [new(unit, Mine)]));
        Assert.Throws<ArgumentException>(() => Orders(disband: [unit, unit]));
    }

    [Fact]
    public void NoOrdersSpendNothing()
    {
        var state = CreateState(experts: 2, paper: 10, cash: 5000);

        var events = Resolve(state, Orders());

        Assert.Empty(events.OfType<CiviliansPurchasedEvent>());
        Assert.Empty(events.OfType<CivilianPurchaseRefusedEvent>());
        Assert.Empty(events.OfType<CiviliansDeliveredEvent>());
        Assert.Empty(events.OfType<CivilianDisbandedEvent>());
        Assert.Equal(2, state.GetWorkers(Home, WorkerGrade.Expert));
    }

    // ---- Fixture ----------------------------------------------------------

    private static void AssertRefusedAndUncharged(
        WorldState state,
        IReadOnlyList<TurnEvent> events,
        CivilianPurchaseRefusal reason,
        long experts,
        long paper,
        long cash)
    {
        var refused = Assert.Single(events.OfType<CivilianPurchaseRefusedEvent>());
        Assert.Equal(reason, refused.Reason);
        Assert.Equal(TurnPhase.Production, refused.Phase);
        Assert.Empty(events.OfType<CiviliansPurchasedEvent>());
        Assert.Empty(events.OfType<CiviliansDeliveredEvent>());
        Assert.Equal(experts, state.GetWorkers(Home, WorkerGrade.Expert));
        Assert.Equal(paper, Stock(state, Paper));
        Assert.Equal(cash, state.GetCash(Home));
        Assert.Empty(state.GetCivilians(Home));
    }

    private static CivilianTypeId Type(int value) => new(value);

    private static long Stock(WorldState state, int commodity) =>
        state.GetAvailableQuantity(Home, new CommodityId(commodity));

    private static CountryTurnOrders Orders(
        IEnumerable<CivilianPurchaseOrder>? buy = null,
        IEnumerable<CivilianUnitId>? disband = null,
        IEnumerable<CivilianWorkOrder>? work = null,
        IEnumerable<ProductionOrder>? production = null,
        IEnumerable<TechnologyId>? buyTechnology = null) =>
        new(
            Home,
            production: production,
            civilianWork: work,
            buyTechnology: buyTechnology,
            civilianPurchases: buy,
            civilianDisbands: disband);

    private static IReadOnlyList<TurnEvent> Resolve(WorldState state, CountryTurnOrders orders) =>
        TurnResolver.Resolve(state, new TurnOrders([orders, new CountryTurnOrders(Other)]), 0).Events;

    /// <summary>
    /// Three farm tiles in a row: Home's capital, Home's grain tile, and Other's
    /// capital. Nobody but the experts named here works.
    /// </summary>
    private static WorldState CreateState(
        long experts = 0,
        long paper = 0,
        long cash = 0,
        long fabric = 0,
        long paperPerCivilian = 2,
        bool halfWantMeat = false,
        InitialCivilian[]? civilians = null)
    {
        const int width = 3;
        var dimensions = new MapDimensions(width, 1);
        var cells = Enumerable.Range(0, width)
            .Select(index => new CellDefinition(
                new CellIndex(index),
                dimensions.GetCoordinate(new CellIndex(index)),
                new TerrainId(0),
                CellRegion.ForProvince(new ProvinceId(index)),
                index == 1 ? [new ResourceId(0)] : null,
                index == 1 ? SettlementSiteKind.None : SettlementSiteKind.Urban))
            .ToArray();

        var map = new MapDefinition(
            dimensions,
            cells,
            Enumerable.Range(0, width)
                .Select(static index => new ProvinceDefinition(new ProvinceId(index), $"Province {index}")),
            [],
            [
                new ResourceDefinition(
                    new ResourceId(0),
                    new CommodityId(Grain),
                    [1, 2, 3, 4],
                    null,
                    Type(Miner)),
            ],
            [new TerrainDefinition(new TerrainId(0), "Farm", isImprovable: true)]);

        var stock = new List<InitialCommodityStock> { new(Home, new CommodityId(Fish), 1000) };
        foreach (var (commodity, quantity) in new[] { (Fabric, fabric), (Paper, paper) })
        {
            if (quantity > 0)
            {
                stock.Add(new InitialCommodityStock(Home, new CommodityId(commodity), quantity));
            }
        }

        var scenario = new ScenarioDefinition(
            "University",
            1856,
            [Home, Home, Other],
            initialCountryCapitals:
            [
                new CountryCapital(Home, Capital),
                new CountryCapital(Other, Abroad),
            ],
            initialInventory: stock,
            initialWorkforce: experts > 0 ? [new InitialWorkforce(Home, 0, 0, experts)] : [],
            initialCivilians: civilians ?? [],
            initialCash: cash > 0 ? [new InitialCash(Home, cash)] : []);

        CivilianPurchase Price(TechnologyId? gate = null) => new(
            1, [new CommodityQuantity(new CommodityId(Paper), paperPerCivilian)], 1000, gate);

        var definition = new WorldDefinition(
            map,
            [new CountryDefinition(Home, "Home"), new CountryDefinition(Other, "Other")],
            scenario,
            [
                new CommodityDefinition(new CommodityId(Grain), "Grain", CommodityCategory.Raw),
                new CommodityDefinition(new CommodityId(Fabric), "Fabric", CommodityCategory.Material),
                new CommodityDefinition(new CommodityId(Clothing), "Clothing", CommodityCategory.Material),
                new CommodityDefinition(new CommodityId(Paper), "Paper", CommodityCategory.Material),
                new CommodityDefinition(new CommodityId(Fish), "Fish", CommodityCategory.Raw),
                new CommodityDefinition(new CommodityId(Meat), "Meat", CommodityCategory.Raw),
            ],
            [new ProductionFacilityDefinition(
                new ProductionFacilityId(0), "Clothing Factory", ProductionCapacityMode.Unlimited)],
            [new ProductionRecipeDefinition(
                ClothingRecipe,
                "Clothing",
                new ProductionFacilityId(0),
                1,
                2,
                [new CommodityQuantity(new CommodityId(Fabric), 2)],
                [new CommodityQuantity(new CommodityId(Clothing), 1)])],
            extraction: new ExtractionSettings(0),
            technologies: [new TechnologyDefinition(FeedGrasses, "Feed Grasses", cost: 500)],
            feeding: new FeedingSettings(
                halfWantMeat
                    ? [new FoodPreference([new CommodityId(Fish)]), new FoodPreference([new CommodityId(Meat)])]
                    : [new FoodPreference([new CommodityId(Fish)])],
                [1, 2, 4]),
            civilianTypes:
            [
                new CivilianTypeDefinition(Type(Miner), "Miner", 3, purchase: Price()),
                new CivilianTypeDefinition(Type(Rancher), "Rancher", 3, purchase: Price(FeedGrasses)),
                new CivilianTypeDefinition(Type(Developer), "Developer", 3),
            ]);
        return new WorldState(definition);
    }
}
