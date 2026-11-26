using Imperialism.Content;
using Imperialism.Core;
using Imperialism.Presentation;
using Xunit;
using static Imperialism.Presentation.Tests.OrdersFixture;

namespace Imperialism.Presentation.Tests;

/// <summary>
/// The draft, its previews, and what survives End Turn. Every preview test
/// resolves a real turn and reads the events, so a preview that drifts from the
/// engine fails here rather than on a player's screen.
/// </summary>
public sealed class OrdersDraftTests
{
    [Fact]
    public void APlayerDraftStartsWithFoodFirstThenWorldCommodityOrder()
    {
        var package = OrdersFixture.Compile(document =>
        {
            var commodities = document.Commodities.ToArray();
            document.Commodities =
            [
                commodities.Single(static item => item.Key == "commodity.lumber"),
                commodities.Single(static item => item.Key == "commodity.grain"),
                .. commodities.Where(static item => item.Key is not ("commodity.grain" or "commodity.lumber")),
            ];
            document.Feeding!.CannedFood = "commodity.canned-food";
            foreach (var scenario in document.Scenarios)
            {
                scenario.TransportCapacity =
                [
                    new TransportCapacityContent { Country = "country.blue", Capacity = 4 },
                    new TransportCapacityContent { Country = "country.red", Capacity = 40 },
                ];
            }
        });
        var state = NewState(package);

        var draft = new OrdersDraft(state, Blue);

        Assert.Equal(
            ["commodity.grain", "commodity.canned-food", "commodity.lumber", "commodity.timber", "commodity.steel"],
            draft.Transport.Select(row => package.Catalog.GetKey(row.Commodity)));
        Assert.Equal([4L, 0L, 0L, 0L, 0L], draft.Transport.Select(static row => row.Quantity));

        // Editing the default remains an ordinary draft edit, including setting a
        // seeded request to zero.
        var lumber = Commodity(package, "commodity.lumber");
        draft.SetTransport(Commodity(package, "commodity.grain"), 0);
        draft.SetTransport(lumber, 2);
        Assert.Equal([new TransportAllocationOrder(lumber, 2)], draft.Build().Transport);
    }

    [Fact]
    public void APlayerDraftUsesRemainingCapacityWhenHarvestIsNotPreviewed()
    {
        var package = OrdersFixture.Compile();
        var state = NewState(package);

        var draft = new OrdersDraft(state, Blue);
        var preview = OrdersPreview.Create(package, Scenario, state, draft).Transport;

        // The transport preview reports shared capacity but does not forecast
        // Extraction, so the chosen first-turn request uses that capacity ceiling.
        Assert.Equal(1, state.GetTransportCapacity(Blue));
        Assert.Equal(1, draft.Transport.Sum(static row => row.Quantity));
        Assert.All(draft.Transport, row => Assert.InRange(row.Quantity, 0, state.GetTransportCapacity(Blue)));
        Assert.Equal(1, preview.Used);

        var resolution = TurnResolver.Resolve(state, draft.BuildTurnOrders(), 1);
        var carried = Assert.Single(
            resolution.Events.OfType<CommoditiesTransportedEvent>(), item => item.Country == Blue);
        Assert.Equal(1, carried.CapacityUsed);
    }

    [Fact]
    public void TransportAllocationsSubmittedLastTurnRemainInTheNextDraft()
    {
        var package = OrdersFixture.Compile();
        var state = NewState(package);
        var draft = new OrdersDraft(state, Blue);
        var grain = Commodity(package, "commodity.grain");
        var steel = Commodity(package, "commodity.steel");
        draft.SetTransport(grain, 3);
        draft.SetTransport(steel, 2);
        var submitted = draft.Build().Transport;

        draft.AdvanceTurn();

        Assert.Equal(submitted, draft.Build().Transport);
        Assert.Equal([3L, 2L], draft.Transport.Where(row => row.Commodity == grain || row.Commodity == steel)
            .Select(static row => row.Quantity));
    }

    [Fact]
    public void BuildMapsEveryDraftedFieldOneToOneAndLeavesZeroRowsOut()
    {
        var package = Compile();
        var draft = new OrdersDraft(package.GetWorld(Scenario), Blue);
        var lumber = Recipe(package, "recipe.lumber");
        var canned = Recipe(package, "recipe.canned-food");
        var grain = Commodity(package, "commodity.grain");
        var timber = Commodity(package, "commodity.timber");
        var steel = Commodity(package, "commodity.steel");
        var lumberGoods = Commodity(package, "commodity.lumber");
        var mill = Facility(package, "facility.lumber-mill");
        var alpha = Technology(package, "technology.a");

        draft.SetProduction(lumber, 3);
        draft.SetProduction(canned, 0);
        draft.SetProduction(canned, 4);
        Assert.True(draft.MoveProductionUp(canned));
        Assert.False(draft.MoveProductionUp(canned));
        draft.SetExpansion(mill, true);
        draft.RecruitWorkers = 2;
        draft.BuildTransportCapacity = 7;
        draft.SetTransport(grain, 5);
        draft.SetTransport(timber, 0);
        draft.SetTransport(steel, 2);
        Assert.True(draft.MoveTransportDown(grain));
        draft.SetTechnologyPurchase(alpha, true);
        draft.SetOffer(lumberGoods, 6);
        draft.SetOffer(steel, 0);
        draft.SetBid(steel, 3);

        var orders = draft.Build();

        Assert.Equal(Blue, orders.Country);
        Assert.Equal([new ProductionOrder(canned, 4), new ProductionOrder(lumber, 3)], orders.Production);
        Assert.Equal([new ProductionExpansionOrder(mill)], orders.Expansions);
        Assert.Equal(2, orders.RecruitWorkers);
        Assert.Equal(7, orders.BuildTransportCapacity);
        // The zero timber row kept its place in the draft and is absent from the orders.
        Assert.Equal([timber, grain, steel], draft.Transport.Select(static row => row.Commodity));
        Assert.Equal(
            [new TransportAllocationOrder(grain, 5), new TransportAllocationOrder(steel, 2)],
            orders.Transport);
        Assert.Equal([alpha], orders.BuyTechnology);
        Assert.Equal([new TradeOrder(lumberGoods, 6)], orders.TradeOffers);
        Assert.Equal([new TradeOrder(steel, 3)], orders.TradeBids);
        Assert.Empty(orders.Deployments);
        Assert.Empty(orders.CivilianWork);
        Assert.Empty(orders.EngineerWork);
        Assert.Empty(orders.ShipBuilds);
        Assert.Empty(orders.Naval);
        Assert.Empty(orders.Overtures);
        Assert.Empty(orders.RegimentPurchases);
        Assert.Equal(0, orders.BurnFuel);

        var turn = draft.BuildTurnOrders();
        Assert.Equal(2, turn.Count);
        Assert.Equal(orders.Production, turn[Blue].Production);
        Assert.Equal(orders.TradeBids, turn[Blue].TradeBids);
        Assert.True(turn[Red].IsEmpty);
    }

    [Fact]
    public void TradeRowsExistOnlyForCommoditiesWithAWorldPrice()
    {
        var package = Compile();
        var draft = new OrdersDraft(package.GetWorld(Scenario), Blue);

        Assert.Throws<ArgumentException>(() => draft.SetBid(Commodity(package, "commodity.grain"), 1));
        Assert.Throws<ArgumentException>(() => draft.SetOffer(Commodity(package, "commodity.timber"), 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => draft.SetTransport(Commodity(package, "commodity.steel"), -1));
    }

    [Fact]
    public void AFreshDraftIsEmptyAndResolvesExactlyLikeEmptyOrders()
    {
        // GameSession.EndTurn submits the draft now; an untouched one must leave the
        // --smoke-turn output exactly where TurnOrders.Empty had it.
        var package = WorldContentCompiler.CompilePackage(TurnReportFixture.CreateDocument());
        var world = package.GetWorld("scenario.demo");
        var draft = new OrdersDraft(world, new CountryId(0));
        Assert.True(draft.IsEmpty);
        var orders = draft.BuildTurnOrders();
        Assert.All(Enumerable.Range(0, orders.Count), index => Assert.True(orders[new CountryId(index)].IsEmpty));

        var before = new WorldState(world);
        var after = new WorldState(world);
        var expected = TurnReportView.Create(
            package, "scenario.demo", before, TurnResolver.Resolve(before, TurnOrders.Empty(2), 1));
        var actual = TurnReportView.Create(
            package, "scenario.demo", after, TurnResolver.Resolve(after, orders, 1));

        Assert.Equal(expected.EventCount, actual.EventCount);
        Assert.Equal(Lines(expected), Lines(actual));
    }

    [Fact]
    public void TheIndustryPreviewAgreesWithWhatTheTurnDoes()
    {
        var package = Compile();
        var state = NewState(package);
        var draft = new OrdersDraft(state.Definition, Blue);
        var lumber = Recipe(package, "recipe.lumber");
        var canned = Recipe(package, "recipe.canned-food");
        var mill = Facility(package, "facility.lumber-mill");
        var refinery = Facility(package, "facility.refinery");

        // Labour is 4×1 + 2×2 + 1×4 = 12. Canned food takes 6 of it; lumber asks five
        // cycles of a mill with two; the railyard asks ten points with 2 labour left;
        // the Capitol asks five recruits of a one-province country.
        draft.SetProduction(canned, 3);
        draft.SetProduction(lumber, 5);
        draft.SetExpansion(mill, true);
        draft.SetExpansion(refinery, true);
        draft.BuildTransportCapacity = 10;
        draft.RecruitWorkers = 5;

        var preview = OrdersPreview.Create(package, Scenario, state, draft).Industry;

        Assert.True(preview.IsLabourLimited);
        Assert.Equal(12, preview.LabourAvailable);
        Assert.Equal(12, preview.LabourCommitted);
        var cannedRow = preview.Recipes[0];
        Assert.Equal((3L, 3L, RecipeLimit.None, true), (cannedRow.RequestedCycles, cannedRow.ExpectedCycles, cannedRow.LimitedBy, cannedRow.IsNotSaved));
        var lumberRow = preview.Recipes[1];
        Assert.Equal((5L, 2L, RecipeLimit.FacilityCapacity), (lumberRow.RequestedCycles, lumberRow.ExpectedCycles, lumberRow.LimitedBy));
        Assert.True(lumberRow.IsOverAsked);
        var millRow = preview.Facilities.Single(row => row.FacilityKey == "facility.lumber-mill");
        Assert.Equal((2L, 2L, 4L, ExpansionOutcome.Expands), (millRow.Capacity, millRow.CapacityCommitted, millRow.NextRung, millRow.Expansion));
        Assert.Equal([2L, 2L], millRow.ExpansionCost.Select(static item => item.Quantity));
        var refineryRow = preview.Facilities.Single(row => row.FacilityKey == "facility.refinery");
        Assert.Equal((true, ExpansionOutcome.TechnologyNotKnown), (refineryRow.IsGatedByTechnology, refineryRow.Expansion));
        Assert.Equal("Dynamite", refineryRow.RequiredTechnologyName);
        Assert.Null(preview.Facilities.Single(row => row.FacilityKey == "facility.food-processing").Capacity);
        Assert.Equal((10L, 2L), (preview.Railyard!.RequestedPoints, preview.Railyard.ExpectedPoints));
        Assert.Equal((1L, 5L, 1L), (preview.Recruitment!.Cap, preview.Recruitment.Requested, preview.Recruitment.ExpectedRecruits));

        Assert.True(preview.Railyard.IsShort);
        Assert.True(preview.Recruitment.IsShort);

        // The preview kept every over-ask; Core gets them as given.
        Assert.Equal(5, draft.Build().Production.Single(order => order.Recipe == lumber).RequestedCycles);

        var events = TurnResolver.Resolve(state, draft.BuildTurnOrders(), 1).Events;
        var produced = events.OfType<ProductionCompletedEvent>().Where(e => e.Country == Blue).ToArray();
        Assert.Equal(cannedRow.ExpectedCycles, produced.Single(e => e.Recipe == canned).CompletedCycles);
        Assert.Equal(lumberRow.ExpectedCycles, produced.Single(e => e.Recipe == lumber).CompletedCycles);
        Assert.Equal(millRow.NextRung, events.OfType<FacilityExpandedEvent>().Single(e => e.Facility == mill).ToCapacity);
        Assert.Equal(
            FacilityExpansionRefusal.TechnologyNotKnown,
            events.OfType<FacilityExpansionRefusedEvent>().Single(e => e.Facility == refinery).Reason);
        var built = events.OfType<TransportCapacityBuiltEvent>().Single(e => e.Country == Blue);
        Assert.Equal(preview.Railyard.ExpectedPoints, built.ToCapacity - built.FromCapacity);
        Assert.Equal(preview.LabourCommitted, produced.Sum(e => e.LabourUsed) + built.LabourUsed);
        Assert.Equal(
            preview.Recruitment.ExpectedRecruits,
            events.OfType<WorkersRecruitedEvent>().Single(e => e.Country == Blue).Recruited);
    }

    [Fact]
    public void TheTransportPreviewAgreesWithWhatTheTurnCarries()
    {
        var package = Compile();
        var state = NewState(package);
        var draft = new OrdersDraft(state.Definition, Blue);
        draft.SetTransport(Commodity(package, "commodity.grain"), 5);
        draft.SetTransport(Commodity(package, "commodity.timber"), 3);

        var preview = OrdersPreview.Create(package, Scenario, state, draft).Transport;

        Assert.Equal((false, 1L, 1L), (preview.IsUnlimited, preview.Capacity, preview.Used));
        Assert.Equal([1L, 0L], preview.Sliders.Select(static slider => slider.Committed));
        Assert.True(preview.IsOverAsked);
        Assert.False(preview.IsZeroCapacity);
        Assert.All(preview.Sliders, slider => Assert.True(slider.IsShort));

        var carried = TurnResolver.Resolve(state, draft.BuildTurnOrders(), 1).Events
            .OfType<CommoditiesTransportedEvent>().Single(e => e.Country == Blue);
        Assert.Equal(preview.Capacity, carried.CapacityAvailable);
        Assert.Equal(preview.Used, carried.CapacityUsed);
    }

    [Fact]
    public void TheTechnologyPreviewAgreesWithWhatInvestmentBuys()
    {
        var package = Compile();
        var state = NewState(package);
        var draft = new OrdersDraft(state.Definition, Blue);
        var alpha = Technology(package, "technology.a");
        var beta = Technology(package, "technology.b");
        var delta = Technology(package, "technology.d");
        draft.SetTechnologyPurchase(delta, true);
        draft.SetTechnologyPurchase(alpha, true);
        draft.SetTechnologyPurchase(beta, true);

        var preview = OrdersPreview.Create(package, Scenario, state, draft).Technology;

        // Seed Drill is known, Dynamite unpriced, Beta wants Alpha, Gamma is not yet.
        Assert.Equal(["technology.a", "technology.d"], preview.Offered.Select(static row => row.TechnologyKey));
        Assert.Equal(5000, preview.Cash);
        Assert.Equal(5500, preview.CommittedCost);
        Assert.True(preview.IsOverCash);
        Assert.Equal(
            [("technology.d", true, true), ("technology.a", true, false), ("technology.b", false, false)],
            preview.Purchases.Select(static row => (row.TechnologyKey, row.IsOffered, row.IsAffordable)));

        var events = TurnResolver.Resolve(state, draft.BuildTurnOrders(), 1).Events;
        Assert.Equal([delta], events.OfType<TechnologyPurchasedEvent>().Select(static e => e.Technology));
        Assert.Equal(
            [(alpha, TechnologyPurchaseRefusal.NotEnoughCash), (beta, TechnologyPurchaseRefusal.PrerequisiteNotKnown)],
            events.OfType<TechnologyPurchaseRefusedEvent>().Select(static e => (e.Technology, e.Reason)));
    }

    [Fact]
    public void TheTradePreviewSellsWhatIndustryLeavesAndTheMarketAgrees()
    {
        var package = Compile();
        var state = NewState(package);
        var draft = new OrdersDraft(state.Definition, Blue);
        var lumber = Commodity(package, "commodity.lumber");
        var steel = Commodity(package, "commodity.steel");

        // Expanding the mill claims 2 lumber of 10, so 8 can be sold.
        draft.SetExpansion(Facility(package, "facility.lumber-mill"), true);
        draft.SetOffer(lumber, 20);
        draft.SetBid(steel, 1);

        var preview = OrdersPreview.Create(package, Scenario, state, draft).Trade;

        var row = preview.Rows.Single(item => item.CommodityKey == "commodity.lumber");
        Assert.Equal((300L, 10L, 2L, 8L, 20L), (row.WorldPrice, row.Stock, row.ClaimedByIndustry, row.Sellable, row.Offer));
        Assert.True(row.IsOverOffered);
        Assert.Equal(300, preview.TotalBidCost);
        Assert.Equal(["commodity.lumber", "commodity.steel", "commodity.canned-food"], preview.Rows.Select(static item => item.CommodityKey));
        Assert.Equal(0, preview.MerchantMarine);
        Assert.False(preview.IsOverBidLimit);

        var red = new CountryTurnOrders(Red, tradeBids: [new TradeOrder(lumber, 20)]);
        var events = TurnResolver.Resolve(state, new TurnOrders([draft.Build(), red]), 1).Events;
        Assert.Equal(row.Sellable, events.OfType<CommodityTradedEvent>()
            .Where(e => e.Seller == Blue && e.Commodity == lumber).Sum(static e => e.Quantity));
    }

    [Fact]
    public void EndTurnKeepsWhatTheManualSavesAndClearsTheRest()
    {
        var package = Compile();
        var draft = new OrdersDraft(package.GetWorld(Scenario), Blue);
        var lumber = Recipe(package, "recipe.lumber");
        var grain = Commodity(package, "commodity.grain");
        var steel = Commodity(package, "commodity.steel");
        draft.SetProduction(Recipe(package, "recipe.canned-food"), 3);
        draft.SetProduction(lumber, 50);
        draft.SetExpansion(Facility(package, "facility.lumber-mill"), true);
        draft.RecruitWorkers = 1;
        draft.BuildTransportCapacity = 4;
        draft.SetTransport(steel, 2);
        draft.SetTransport(grain, 5);
        draft.SetTechnologyPurchase(Technology(package, "technology.a"), true);
        draft.SetOffer(steel, 1);
        draft.SetBid(Commodity(package, "commodity.lumber"), 1);

        draft.AdvanceTurn();

        // A mill remembers the order as given, over-ask and all (manual 2863–2867);
        // food processing does not (2651–2652).
        Assert.Equal([(lumber, 50L)], draft.Production);
        Assert.Equal([(steel, 2L), (grain, 5L)], draft.Transport);
        Assert.Empty(draft.Expansions);
        Assert.Equal(0, draft.RecruitWorkers);
        Assert.Equal(0, draft.BuildTransportCapacity);
        Assert.Empty(draft.BuyTechnology);
        Assert.Empty(draft.TradeOffers);
        Assert.Empty(draft.TradeBids);
    }

    [Fact]
    public void ABidOnMoreThanFourCommoditiesIsWarnedAndStillSubmitted()
    {
        var document = TurnReportFixture.CreateDocument();
        document.Commodities =
        [
            .. document.Commodities,
            .. Enumerable.Range(2, 4).Select(index => new CommodityContentDefinition
            {
                Key = $"commodity.extra-{index}", Name = $"Extra {index}", Category = CommodityCategory.Material,
                WorldPrice = 300, TradeOrder = index,
            }),
        ];
        var package = WorldContentCompiler.CompilePackage(document);
        var state = new WorldState(package.GetWorld("scenario.demo"));
        var draft = new OrdersDraft(state.Definition, Blue);
        foreach (var commodity in state.Definition.Commodities.Where(static item => item.IsTradable))
        {
            draft.SetBid(commodity.Id, 1);
        }

        var preview = OrdersPreview.Create(package, "scenario.demo", state, draft).Trade;

        Assert.Equal(5, preview.BidCount);
        Assert.True(preview.IsOverBidLimit);
        Assert.Equal(5, draft.Build().TradeBids.Count);
    }

    private static string[] Lines(TurnReportView report) =>
        report.Phases.SelectMany(static phase => phase.Lines.Select(line => $"{phase.Phase}:{line}")).ToArray();
}
