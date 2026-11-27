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
    /// <summary>
    /// The original's opening Transport screen for Great Britain, <c>s1</c>, Spring
    /// 1882: carried / available per slider, 119 of 120 capacity. Measured on the PC
    /// (screenshot O30). Fish + livestock and cotton + wool are one slider each there.
    /// <b>Open (b) difference:</b> the original carries one horse and we carry none. No
    /// facility or recipe in imported content consumes horses — the manual gives them
    /// one use, the Armoury's regiments (lines 2371–2383) — so the source of that one
    /// unit of demand is not identified. The test expects ours, 0 and 118 of 120.
    /// </summary>
    private static readonly (string Slider, string[] Commodities, long Carried, long Available)[] GreatBritainS1Screen =
    [
        ("fish + livestock", ["livestock", "fish"], 15, 19),
        ("fruit", ["fruit"], 15, 31),
        ("grain", ["grain"], 30, 63),
        ("timber", ["timber"], 16, 16),
        ("coal", ["coal"], 8, 18),
        ("iron", ["iron"], 8, 8),
        ("cotton + wool", ["cotton", "wool"], 26, 45),
        ("horses", ["horses"], 1, 3),
    ];

    private const long GreatBritainS1Capacity = 120;

    /// <summary>
    /// Great Britain's <c>s1</c> economy as the turn opens. <b>Everything in this block
    /// is ASSUMED until the PC reports GB's real figures</b>; replace the values here
    /// and the test re-checks the rule against the screen above. Each assumption is
    /// the one the screen implies, or a value inside the range the screen allows.
    /// </summary>
    private static class GreatBritainS1Assumed
    {
        // ASSUMED: 60 workers, the only headcount that eats 30/15/15 with no food in
        // the warehouse.
        public const long Workers = 60;

        // ASSUMED: how the two shared sliders split between their commodities.
        public static readonly (string Commodity, long Available)[] Split =
        [
            ("livestock", 12), ("fish", 7), ("cotton", 30), ("wool", 15),
        ];

        // ASSUMED: mill capacity, in cycles. Timber and iron are capped by what is
        // available, so any lumber mill of 8 or more and any steel mill of 8 or more
        // reproduce the screen; 10 and 12 are chosen to show the caps biting. The
        // textile mill must be exactly 13 for 26 of 45 with an empty warehouse.
        public const long LumberMill = 10;
        public const long SteelMill = 12;
        public const long TextileMill = 13;

        // ASSUMED: no raw commodity in the warehouse.
        public static readonly (string Commodity, long Quantity)[] Stock = [];
    }

    [Fact]
    public void OpeningTransportMatchesTheOriginalGreatBritainS1Screen()
    {
        var inputs = GreatBritainS1Inputs(GreatBritainS1Capacity, out var id);

        var rows = TransportDefault.Allocate(inputs).ToDictionary(static row => row.Commodity, static row => row.Quantity);

        foreach (var (slider, commodities, carried, available) in GreatBritainS1Screen)
        {
            Assert.True(
                available == commodities.Sum(name => inputs.Available[id[name].Value]),
                $"fixture availability for {slider}");
            // The open horse difference: nothing in our content draws horses.
            var expected = slider == "horses" ? 0 : carried;
            Assert.True(expected == commodities.Sum(name => rows[id[name]]), $"carried on {slider}");
        }

        // The original's 119 less its one unexplained horse.
        Assert.Equal(118, rows.Values.Sum());
        Assert.Equal(0, rows[id["lumber"]] + rows[id["steel"]]);
    }

    /// <summary>
    /// D12: the original's underlines on the same screen. Timber, coal and iron red,
    /// horses none, the rest green. Red needs demand past Available (timber 16/16), so
    /// demand is not capped there, while the fill still is.
    /// </summary>
    [Fact]
    public void TheDemandLineReproducesTheOriginalGreatBritainS1Underlines()
    {
        var inputs = GreatBritainS1Inputs(GreatBritainS1Capacity, out var id);
        var line = TransportDefault.DemandLine(inputs);
        var rows = TransportDefault.Allocate(inputs).ToDictionary(static row => row.Commodity, static row => row.Quantity);

        string Underline(string[] commodities)
        {
            var demand = commodities.Sum(name => line.Demand[id[name].Value]);
            var carried = commodities.Sum(name => rows[id[name]]);
            return demand <= 0 ? "none" : carried >= demand ? "green" : "red";
        }

        Assert.Equal(
            ["green", "green", "green", "red", "red", "red", "green", "none"],
            GreatBritainS1Screen.Select(row => Underline(row.Commodities)));

        // The mills want their whole capacity: lumber 10 cycles, steel 12, textile 13.
        Assert.Equal(20, line.Demand[id["timber"].Value]);
        Assert.Equal((12L, 12L), (line.Demand[id["coal"].Value], line.Demand[id["iron"].Value]));
        Assert.Equal(26, line.Demand[id["cotton"].Value] + line.Demand[id["wool"].Value]);

        // The fill reads Carry, which is unchanged: capped at Available, coal held to iron.
        Assert.All(inputs.Rows, commodity => Assert.Equal(line.Carry[commodity.Value], rows[commodity]));
        Assert.All(inputs.Rows, commodity => Assert.True(line.Demand[commodity.Value] >= line.Carry[commodity.Value]));
    }

    [Fact]
    public void UncappedMillDemandIsNetOfStock()
    {
        // Three iron in the warehouse: the steel mill's 12 cycles want 12 coal and 9 iron.
        var plain = GreatBritainS1Inputs(GreatBritainS1Capacity, out var id);
        var stock = plain.Stock.ToArray();
        stock[id["iron"].Value] = 3;

        var line = TransportDefault.DemandLine(plain with { Stock = stock });

        Assert.Equal((12L, 9L), (line.Demand[id["coal"].Value], line.Demand[id["iron"].Value]));
        Assert.Equal((11L, 8L), (line.Carry[id["coal"].Value], line.Carry[id["iron"].Value]));
    }

    /// <summary>
    /// Idle demand (D12) must not reserve warehouse stock that a later mill sharing the
    /// input would use. A mill blocked for want of B leaves the one A in stock to the
    /// second mill, which then needs nothing carried, as before D12. The blocked mill's
    /// own A and B are still wanted, net only of what the runnable cycles left (codex-3's gate
    /// probe on https://github.com/PlueRyvius/Imperialism-1-Modern/pull/95).
    /// </summary>
    [Fact]
    public void IdleDemandDoesNotReserveStockFromALaterMillSharingTheInput()
    {
        var a = new CommodityId(0);
        var b = new CommodityId(1);
        var inputs = new TransportDemandInputs(10, 0, [], [
            new TransportMillDemand(1, [
                (1L, (IReadOnlyList<CommodityQuantity>)
                    new CommodityQuantity[] { new(a, 1), new(b, 1) })]),
            new TransportMillDemand(1, [
                (1L, (IReadOnlyList<CommodityQuantity>)
                    new CommodityQuantity[] { new(a, 1) })])
        ], [1L, 0L], [1L, 0L], [a, b]);

        var carriedA = TransportDefault.Allocate(inputs).Where(row => row.Commodity == a).Sum(row => row.Quantity);
        var line = TransportDefault.DemandLine(inputs);

        Assert.Equal(0, carriedA);
        Assert.Equal(0, line.Carry[a.Value]);
        Assert.Equal(1, line.Demand[a.Value]);
        Assert.Equal(1, line.Demand[b.Value]);
    }

    [Fact]
    public void DemandOverCapacityIsTrimmedInRowOrderFoodFirst()
    {
        // Chosen rule: the original's screen opens under capacity, so it cannot say.
        var inputs = GreatBritainS1Inputs(capacity: 70, out var id);

        var rows = TransportDefault.Allocate(inputs);

        Assert.Equal(70, rows.Sum(static row => row.Quantity));
        Assert.Equal(60, rows.Where(row => row.Commodity.Value <= id["fish"].Value).Sum(static row => row.Quantity));
        Assert.Equal(10, rows.Single(row => row.Commodity == id["timber"]).Quantity);
        Assert.Equal(0, rows.Single(row => row.Commodity == id["iron"]).Quantity);
    }

    [Fact]
    public void WarehouseStockComesOffDemandBeforeAnythingIsCarried()
    {
        var plain = GreatBritainS1Inputs(GreatBritainS1Capacity, out var id);
        var stock = plain.Stock.ToArray();
        stock[id["grain"].Value] = 12;
        stock[id["iron"].Value] = 3;
        stock[id["cotton"].Value] = 6;

        var rows = TransportDefault.Allocate(plain with { Stock = stock })
            .ToDictionary(static row => row.Commodity, static row => row.Quantity);

        Assert.Equal(18, rows[id["grain"]]);
        // Iron stock lets the steel mill run 11 cycles: 8 iron carried, 11 coal.
        Assert.Equal(8, rows[id["iron"]]);
        Assert.Equal(11, rows[id["coal"]]);
        Assert.Equal(20, rows[id["cotton"]] + rows[id["wool"]]);
    }

    [Fact]
    public void APlayerDraftOpensAtDemandAndLeavesSpareCapacityUnspent()
    {
        var package = OrdersFixture.Compile(document =>
        {
            document.Resources =
            [
                .. document.Resources,
                new ResourceContentDefinition
                {
                    Key = "resource.blue-timber", Commodity = "commodity.timber", YieldByDevelopmentLevel = [3],
                },
            ];
            document.Map.Cells[0].Resources = [.. document.Map.Cells[0].Resources, "resource.blue-timber"];
            foreach (var scenario in document.Scenarios)
            {
                scenario.InitialInventory = [];
                scenario.TransportCapacity =
                [
                    new TransportCapacityContent { Country = "country.blue", Capacity = 40 },
                    new TransportCapacityContent { Country = "country.red", Capacity = 40 },
                ];
            }
        });
        var state = NewState(package);

        var draft = new OrdersDraft(state, Blue);

        // Seven workers want 7 grain and 2 is gathered; the two-cycle lumber mill wants
        // 4 timber and 3 is gathered, enough for one cycle. 36 of 40 stays unspent.
        Assert.Equal(
            ["commodity.grain", "commodity.lumber", "commodity.timber", "commodity.steel", "commodity.canned-food"],
            draft.Transport.Select(row => package.Catalog.GetKey(row.Commodity)));
        Assert.Equal([2L, 0L, 2L, 0L, 0L], draft.Transport.Select(static row => row.Quantity));

        // A player's edit replaces the default, including pulling a row to zero.
        var grain = Commodity(package, "commodity.grain");
        var timber = Commodity(package, "commodity.timber");
        draft.SetTransport(grain, 0);
        draft.SetTransport(timber, 3);
        Assert.Equal([new TransportAllocationOrder(timber, 3)], draft.Build().Transport);
    }

    [Fact]
    public void AStockedWarehouseOpensWithNothingToCarry()
    {
        // OrdersFixture's Blue holds 10 grain for 7 workers and 10 timber for a
        // two-cycle mill, so no row has demand left.
        var package = OrdersFixture.Compile(document =>
        {
            foreach (var scenario in document.Scenarios)
            {
                scenario.TransportCapacity =
                [
                    new TransportCapacityContent { Country = "country.blue", Capacity = 40 },
                    new TransportCapacityContent { Country = "country.red", Capacity = 40 },
                ];
            }
        });

        var draft = new OrdersDraft(NewState(package), Blue);

        Assert.All(draft.Transport, static row => Assert.Equal(0, row.Quantity));
        Assert.Empty(draft.Build().Transport);
    }

    [Fact]
    public void ATrimmedDefaultIsWhatCoreCarries()
    {
        var package = OrdersFixture.Compile(document =>
        {
            foreach (var scenario in document.Scenarios)
            {
                scenario.InitialInventory = [];
            }
        });
        var state = NewState(package);

        var draft = new OrdersDraft(state, Blue);
        var preview = OrdersPreview.Create(package, Scenario, state, draft).Transport;

        Assert.Equal(1, state.GetTransportCapacity(Blue));
        Assert.Equal(1, draft.Transport.Single(row => row.Commodity == Commodity(package, "commodity.grain")).Quantity);
        Assert.Equal(1, preview.Used);

        var resolution = TurnResolver.Resolve(state, draft.BuildTurnOrders(), 1);
        var carried = Assert.Single(
            resolution.Events.OfType<CommoditiesTransportedEvent>(), item => item.Country == Blue);
        Assert.Equal(1, carried.CapacityUsed);
    }

    [Fact]
    public void AWorldWithoutATransportLimitOpensWithNoRows()
    {
        var package = OrdersFixture.Compile(static document => document.Transport = null);

        Assert.Empty(new OrdersDraft(NewState(package), Blue).Transport);
    }

    private static TransportDemandInputs GreatBritainS1Inputs(long capacity, out Dictionary<string, CommodityId> id)
    {
        string[] names =
        [
            "grain", "fruit", "livestock", "fish", "timber", "coal", "iron", "cotton", "wool", "horses",
            "lumber", "paper", "steel", "fabric",
        ];
        var ids = names.Select(static (name, index) => (name, id: new CommodityId(index)))
            .ToDictionary(static pair => pair.name, static pair => pair.id);
        id = ids;

        var available = new long[names.Length];
        foreach (var (_, commodities, _, total) in GreatBritainS1Screen.Where(static row => row.Commodities.Length == 1))
        {
            available[ids[commodities[0]].Value] = total;
        }

        foreach (var (commodity, quantity) in GreatBritainS1Assumed.Split)
        {
            available[ids[commodity].Value] = quantity;
        }

        var stock = new long[names.Length];
        foreach (var (commodity, quantity) in GreatBritainS1Assumed.Stock)
        {
            stock[ids[commodity].Value] = quantity;
        }

        CommodityQuantity In(string name, long quantity) => new(ids[name], quantity);

        return new TransportDemandInputs(
            capacity,
            GreatBritainS1Assumed.Workers,
            // The importer's cycle: grain, fruit, grain, then livestock or fish.
            [[ids["grain"]], [ids["fruit"]], [ids["grain"]], [ids["livestock"], ids["fish"]]],
            // The importer's three mills and their recipes, in its order; capacities assumed.
            [
                new TransportMillDemand(GreatBritainS1Assumed.LumberMill, [(1, [In("timber", 2)]), (1, [In("timber", 2)])]),
                new TransportMillDemand(GreatBritainS1Assumed.SteelMill, [(1, [In("coal", 1), In("iron", 1)])]),
                new TransportMillDemand(GreatBritainS1Assumed.TextileMill, [(1, [In("cotton", 2)]), (1, [In("wool", 2)])]),
            ],
            stock,
            available,
            names.Select(name => ids[name]).ToArray());
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
