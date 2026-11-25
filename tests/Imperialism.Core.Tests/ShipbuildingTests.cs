using Imperialism.Core;
using Xunit;

namespace Imperialism.Core.Tests;

/// <summary>
/// The Shipyard: commodities in, hulls out, and nothing else spent.
/// </summary>
/// <remarks>
/// Two Great Powers on a strip with one sea zone. Home builds; Rival sells coal and
/// owns three Traders, so Home's own merchant marine is what a bid turns on. The bills
/// are the executable's own for the three hulls used: a Trader is 4 lumber and 2
/// fabric, a Frigate 5 lumber, 2 fabric and 2 arms, and a Paddlewheeler is gated
/// behind Paddlewheels (its steel is left off the fixture's bill, since nothing here
/// needs a third input).
/// </remarks>
public sealed class ShipbuildingTests
{
    private const int Lumber = 0;
    private const int Fabric = 1;
    private const int Arms = 2;
    private const int Coal = 3;
    private const int Fish = 4;

    private const int Trader = 0;
    private const int Frigate = 1;
    private const int Paddlewheeler = 2;

    private const long StartingCash = 10_000;

    private static readonly CountryId Home = new(0);
    private static readonly CountryId Rival = new(1);
    private static readonly TechnologyId Paddlewheels = new(0);

    [Fact]
    public void AHullWhoseTechnologyIsNotKnownIsRefusedAndChargesNothing()
    {
        var state = CreateState(stock: [(Lumber, 100), (Coal, 100)]);

        var result = Resolve(state, Build(Paddlewheeler, 1));

        var refused = Assert.Single(result.Events.OfType<ShipBuildRefusedEvent>());
        Assert.Equal(ShipBuildRefusal.TechnologyNotKnown, refused.Reason);
        Assert.Empty(result.Events.OfType<ShipsBuiltEvent>());
        Assert.Equal(100, Stock(state, Lumber));
        Assert.Equal(100, Stock(state, Coal));
        Assert.Equal(0, state.GetShipCount(Home, new ShipTypeId(Paddlewheeler)));
    }

    [Fact]
    public void KnowingTheTechnologyOpensTheHull()
    {
        var state = CreateState(stock: [(Lumber, 100), (Coal, 100)], knowsPaddlewheels: true);

        var result = Resolve(state, Build(Paddlewheeler, 1));

        Assert.Single(result.Events.OfType<ShipsBuiltEvent>());
        Assert.Equal(1, state.GetShipCount(Home, new ShipTypeId(Paddlewheeler)));
        Assert.Equal(94, Stock(state, Lumber));
        Assert.Equal(90, Stock(state, Coal));
    }

    /// <summary>
    /// Knowledge is read as the turn opens, and research lands in Investment, which runs
    /// last. So a technology bought this turn opens the hull next turn.
    /// </summary>
    [Fact]
    public void ATechnologyBoughtThisTurnOpensTheHullNextTurn()
    {
        var state = CreateState(stock: [(Lumber, 100), (Coal, 100)]);

        var first = Resolve(state, new CountryTurnOrders(
            Home,
            buyTechnology: [Paddlewheels],
            shipBuilds: [new ShipBuildOrder(new ShipTypeId(Paddlewheeler), 1)]));
        Assert.Single(first.Events.OfType<TechnologyPurchasedEvent>());
        Assert.Equal(
            ShipBuildRefusal.TechnologyNotKnown,
            Assert.Single(first.Events.OfType<ShipBuildRefusedEvent>()).Reason);

        var second = Resolve(state, Build(Paddlewheeler, 1));
        Assert.Single(second.Events.OfType<ShipsBuiltEvent>());
    }

    [Fact]
    public void AnOrderShortOfOneCommodityIsRefusedAndChargesNothing()
    {
        // All the lumber two Traders need, and one fabric short.
        var state = CreateState(stock: [(Lumber, 8), (Fabric, 3)]);

        var result = Resolve(state, Build(Trader, 2));

        var refused = Assert.Single(result.Events.OfType<ShipBuildRefusedEvent>());
        Assert.Equal(ShipBuildRefusal.NotEnoughCommodities, refused.Reason);
        Assert.Equal(2, refused.Requested);
        Assert.Equal(8, Stock(state, Lumber));
        Assert.Equal(3, Stock(state, Fabric));
        Assert.Equal(0, state.GetShipCount(Home, new ShipTypeId(Trader)));
    }

    [Fact]
    public void TheExactBillIsAccepted()
    {
        var state = CreateState(stock: [(Lumber, 8), (Fabric, 4)]);

        var result = Resolve(state, Build(Trader, 2));

        var built = Assert.Single(result.Events.OfType<ShipsBuiltEvent>());
        Assert.Equal((new ShipTypeId(Trader), 2L), (built.Type, built.Count));
        Assert.Equal(
            [
                new CommodityQuantity(new CommodityId(Lumber), 8),
                new CommodityQuantity(new CommodityId(Fabric), 4),
            ],
            built.Paid);
        Assert.Equal(0, Stock(state, Lumber));
        Assert.Equal(0, Stock(state, Fabric));
        Assert.Equal(2, state.GetShipCount(Home, new ShipTypeId(Trader)));
        Assert.Empty(result.Events.OfType<ShipBuildRefusedEvent>());
    }

    /// <summary>
    /// <b>All or nothing</b> — a chosen rule. Materials for two of three hulls build
    /// none of them, and the order after it is still tried against what is left.
    /// </summary>
    [Fact]
    public void AnOrderTheWarehouseCannotCoverIsRefusedWholeNotTrimmed()
    {
        var state = CreateState(stock: [(Lumber, 8), (Fabric, 4)]);

        var result = Resolve(state, new CountryTurnOrders(
            Home,
            shipBuilds:
            [
                new ShipBuildOrder(new ShipTypeId(Trader), 3),
                new ShipBuildOrder(new ShipTypeId(Trader), 1),
            ]));

        var refused = Assert.Single(result.Events.OfType<ShipBuildRefusedEvent>());
        Assert.Equal((3L, ShipBuildRefusal.NotEnoughCommodities), (refused.Requested, refused.Reason));
        var built = Assert.Single(result.Events.OfType<ShipsBuiltEvent>());
        Assert.Equal(1, built.Count);
        Assert.Equal(1, state.GetShipCount(Home, new ShipTypeId(Trader)));
        Assert.Equal(4, Stock(state, Lumber));
        Assert.Equal(2, Stock(state, Fabric));
    }

    [Fact]
    public void AnOverflowingCountIsRefusedNotThrown()
    {
        var state = CreateState(stock: [(Lumber, long.MaxValue / 2), (Fabric, long.MaxValue / 2)]);

        var result = Resolve(state, Build(Trader, long.MaxValue));

        Assert.Equal(
            ShipBuildRefusal.NotEnoughCommodities,
            Assert.Single(result.Events.OfType<ShipBuildRefusedEvent>()).Reason);
        Assert.Equal(long.MaxValue / 2, Stock(state, Lumber));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void ANonPositiveCountIsRefused(long count)
    {
        var state = CreateState(stock: [(Lumber, 8), (Fabric, 4)]);

        var result = Resolve(state, Build(Trader, count));

        Assert.Equal(
            ShipBuildRefusal.NonPositiveCount,
            Assert.Single(result.Events.OfType<ShipBuildRefusedEvent>()).Reason);
        Assert.Equal(8, Stock(state, Lumber));
    }

    [Fact]
    public void AnUnknownHullIsRefused()
    {
        var state = CreateState(stock: [(Lumber, 8), (Fabric, 4)]);

        var result = Resolve(state, Build(99, 1));

        Assert.Equal(
            ShipBuildRefusal.UnknownType,
            Assert.Single(result.Events.OfType<ShipBuildRefusedEvent>()).Reason);
    }

    /// <summary>
    /// "Warships require armaments." The Frigate's lumber and fabric are not enough.
    /// </summary>
    [Fact]
    public void AWarshipNeedsArms()
    {
        var unarmed = CreateState(stock: [(Lumber, 50), (Fabric, 50), (Arms, 1)]);
        var refused = Resolve(unarmed, Build(Frigate, 1));
        Assert.Equal(
            ShipBuildRefusal.NotEnoughCommodities,
            Assert.Single(refused.Events.OfType<ShipBuildRefusedEvent>()).Reason);
        Assert.Equal(1, Stock(unarmed, Arms));

        var armed = CreateState(stock: [(Lumber, 50), (Fabric, 50), (Arms, 2)]);
        Assert.Single(Resolve(armed, Build(Frigate, 1)).Events.OfType<ShipsBuiltEvent>());
        Assert.Equal(0, Stock(armed, Arms));
        Assert.Equal(1, armed.GetShipCount(Home, new ShipTypeId(Frigate)));

        // A warship carries nothing, so it adds no holds.
        Assert.Equal(0, armed.GetMerchantMarine(Home));
    }

    /// <summary>
    /// The ship's holds join the merchant marine at the end of Construction, which runs
    /// after Trade. So the market that clears this turn never sees them, and next turn's
    /// does. That is the manual's "all new units … are announced … at the start of the
    /// next turn".
    /// </summary>
    [Fact]
    public void NewHoldsCountInNextTurnsTradeButNotThisTurns()
    {
        var state = CreateState(stock: [(Lumber, 8), (Fabric, 4)], rivalCoal: 50);
        Assert.Equal(0, state.GetMerchantMarine(Home));

        var first = Resolve(
            state,
            new CountryTurnOrders(
                Home,
                tradeBids: [new TradeOrder(new CommodityId(Coal), 2)],
                shipBuilds: [new ShipBuildOrder(new ShipTypeId(Trader), 1)]),
            new CountryTurnOrders(Rival, tradeOffers: [new TradeOrder(new CommodityId(Coal), 10)]));

        Assert.Empty(first.Events.OfType<CommodityTradedEvent>());
        Assert.Contains(
            first.Events.OfType<TradeUnfilledEvent>(),
            item => item.Country == Home && item.Reason == TradeRefusal.NoMerchantCapacity);
        Assert.Single(first.Events.OfType<ShipsBuiltEvent>());
        Assert.Equal(2, state.GetMerchantMarine(Home));

        var second = Resolve(
            state,
            new CountryTurnOrders(Home, tradeBids: [new TradeOrder(new CommodityId(Coal), 2)]),
            new CountryTurnOrders(Rival, tradeOffers: [new TradeOrder(new CommodityId(Coal), 10)]));

        var deal = Assert.Single(second.Events.OfType<CommodityTradedEvent>());
        Assert.Equal((Home, 2L, Home), (deal.Buyer, deal.Quantity, deal.HoldsPaidBy));
    }

    /// <summary>
    /// Materials promised to a hull cannot also be sold: the Shipyard is planned with the
    /// other builds, before the market clears.
    /// </summary>
    [Fact]
    public void LumberPromisedToAHullIsNotOffered()
    {
        var state = CreateState(stock: [(Lumber, 4), (Fabric, 2)]);

        var result = Resolve(state, new CountryTurnOrders(
            Home,
            tradeOffers: [new TradeOrder(new CommodityId(Lumber), 4)],
            shipBuilds: [new ShipBuildOrder(new ShipTypeId(Trader), 1)]),
            new CountryTurnOrders(Rival, tradeBids: [new TradeOrder(new CommodityId(Lumber), 4)]));

        Assert.Empty(result.Events.OfType<CommodityTradedEvent>());
        Assert.Single(result.Events.OfType<ShipsBuiltEvent>());
        Assert.Equal(0, Stock(state, Lumber));
    }

    [Fact]
    public void NewShipsMergeWithAnUnplacedFleetOfTheSameClass()
    {
        var state = CreateState(
            stock: [(Lumber, 100), (Fabric, 100), (Arms, 100)],
            homeShips:
            [
                (Trader, 2, new SeaZoneId(0)),
                (Trader, 1, null),
            ]);

        // Rival's three Traders are fleet 1; Home's placed fleet is 2, unplaced is 3.
        Assert.Equal(3, state.Fleets.Count);

        var result = Resolve(state, new CountryTurnOrders(
            Home,
            shipBuilds:
            [
                new ShipBuildOrder(new ShipTypeId(Trader), 3),
                new ShipBuildOrder(new ShipTypeId(Frigate), 1),
            ]));

        var built = result.Events.OfType<ShipsBuiltEvent>().ToArray();
        Assert.Equal(2, built.Length);

        // The Traders joined the unplaced fleet, not the one at sea.
        Assert.Equal(new FleetId(3), built[0].Fleet);
        Assert.Equal(4, state.GetFleet(new FleetId(3)).Count);
        Assert.Equal(2, state.GetFleet(new FleetId(2)).Count);
        Assert.Equal(6, state.GetShipCount(Home, new ShipTypeId(Trader)));

        // No Frigate fleet existed, so one was started with the next dense id: unplaced,
        // in no task force.
        Assert.Equal(new FleetId(4), built[1].Fleet);
        Assert.Equal(4, state.Fleets.Count);
        var frigates = state.GetFleet(new FleetId(4));
        Assert.Equal((Home, new ShipTypeId(Frigate), 1L), (frigates.Country, frigates.Type, frigates.Count));
        Assert.Null(frigates.SeaZone);
        Assert.Null(frigates.TaskForce);

        // A second Frigate next turn joins that new fleet rather than starting another.
        var again = Resolve(state, Build(Frigate, 1));
        Assert.Equal(new FleetId(4), Assert.Single(again.Events.OfType<ShipsBuiltEvent>()).Fleet);
        Assert.Equal(2, state.GetFleet(new FleetId(4)).Count);
        Assert.Equal(4, state.Fleets.Count);
    }

    [Fact]
    public void TheFleetRosterCannotBeEditedByACaller()
    {
        var state = CreateState();

        Assert.Throws<NotSupportedException>(() => ((IList<FleetState>)state.Fleets).Clear());
    }

    /// <summary>
    /// "Ships do not require workers", and the build bills carry no cash. A production
    /// order that needs the whole labour pool still runs in full beside a build.
    /// </summary>
    [Fact]
    public void BuildingSpendsNoLabourAndNoCash()
    {
        var state = CreateState(stock: [(Lumber, 8), (Fabric, 4), (Coal, 4)]);

        var result = Resolve(state, new CountryTurnOrders(
            Home,
            production: [new ProductionOrder(new ProductionRecipeId(0), 2)],
            shipBuilds: [new ShipBuildOrder(new ShipTypeId(Trader), 2)]));

        Assert.Single(result.Events.OfType<ShipsBuiltEvent>());
        var produced = Assert.Single(result.Events.OfType<ProductionCompletedEvent>());
        Assert.Equal(2, produced.CompletedCycles);
        Assert.Equal(4, produced.LabourUsed);
        Assert.Equal(StartingCash, state.GetCash(Home));
        Assert.Equal(4, state.GetWorkers(Home, WorkerGrade.Untrained));
    }

    private static CountryTurnOrders Build(int type, long count) =>
        new(Home, shipBuilds: [new ShipBuildOrder(new ShipTypeId(type), count)]);

    private static long Stock(WorldState state, int commodity) =>
        state.GetAvailableQuantity(Home, new CommodityId(commodity));

    private static TurnResolution Resolve(WorldState state, params CountryTurnOrders[] orders)
    {
        var byCountry = Enumerable.Range(0, state.Definition.Countries.Count)
            .Select(index => orders.FirstOrDefault(item => item.Country.Value == index)
                ?? new CountryTurnOrders(new CountryId(index)))
            .ToArray();
        return TurnResolver.Resolve(state, new TurnOrders(byCountry), 0);
    }

    private static WorldState CreateState(
        (int Commodity, long Quantity)[]? stock = null,
        bool knowsPaddlewheels = false,
        long rivalCoal = 0,
        (int Type, long Count, SeaZoneId? Zone)[]? homeShips = null)
    {
        var dimensions = new MapDimensions(3, 1);
        var map = new MapDefinition(
            dimensions,
            [
                new CellDefinition(new CellIndex(0), dimensions.GetCoordinate(new CellIndex(0)),
                    new TerrainId(0), CellRegion.ForProvince(new ProvinceId(0)), null,
                    SettlementSiteKind.Urban),
                new CellDefinition(new CellIndex(1), dimensions.GetCoordinate(new CellIndex(1)),
                    new TerrainId(0), CellRegion.ForProvince(new ProvinceId(1)), null,
                    SettlementSiteKind.Urban),
                new CellDefinition(new CellIndex(2), dimensions.GetCoordinate(new CellIndex(2)),
                    new TerrainId(0), CellRegion.ForSeaZone(new SeaZoneId(0))),
            ],
            [
                new ProvinceDefinition(new ProvinceId(0), "Home"),
                new ProvinceDefinition(new ProvinceId(1), "Rival"),
            ],
            [new SeaZoneDefinition(new SeaZoneId(0), "Channel")],
            [],
            [new TerrainDefinition(new TerrainId(0), "Plains")]);

        var inventory = (stock ?? [])
            .Select(static item => new InitialCommodityStock(Home, new CommodityId(item.Commodity), item.Quantity))
            .Concat(
            [
                new InitialCommodityStock(Home, new CommodityId(Fish), 500),
                new InitialCommodityStock(Rival, new CommodityId(Fish), 500),
            ]);
        if (rivalCoal > 0)
        {
            inventory = inventory.Append(new InitialCommodityStock(Rival, new CommodityId(Coal), rivalCoal));
        }

        var ships = new List<InitialShip> { new(Rival, new ShipTypeId(Trader), 0, 3) };
        ships.AddRange((homeShips ?? []).Select(static item =>
            new InitialShip(Home, new ShipTypeId(item.Type), 0, item.Count, item.Zone)));

        var scenario = new ScenarioDefinition(
            "Shipyard",
            1840,
            [Home, Rival],
            initialCountryCapitals:
            [
                new CountryCapital(Home, new CellIndex(0)),
                new CountryCapital(Rival, new CellIndex(1)),
            ],
            initialInventory: inventory,
            initialWorkforce:
            [
                new InitialWorkforce(Home, 4, 0, 0),
                new InitialWorkforce(Rival, 4, 0, 0),
            ],
            initialCash: [new InitialCash(Home, StartingCash), new InitialCash(Rival, StartingCash)],
            initialCountryTechnologies: knowsPaddlewheels
                ? [new InitialCountryTechnology(Home, Paddlewheels)]
                : [],
            initialShips: ships);

        return new WorldState(new WorldDefinition(
            map,
            [
                new CountryDefinition(Home, "Home", isGreatPower: true),
                new CountryDefinition(Rival, "Rival", isGreatPower: true),
            ],
            scenario,
            [
                new CommodityDefinition(
                    new CommodityId(Lumber), "Lumber", CommodityCategory.Material,
                    worldPrice: 300, tradeOrder: 10),
                new CommodityDefinition(new CommodityId(Fabric), "Fabric", CommodityCategory.Material),
                new CommodityDefinition(new CommodityId(Arms), "Arms", CommodityCategory.Goods),
                new CommodityDefinition(
                    new CommodityId(Coal), "Coal", CommodityCategory.Raw,
                    worldPrice: 100, tradeOrder: 13),
                new CommodityDefinition(new CommodityId(Fish), "Fish", CommodityCategory.Raw),
            ],
            [
                new ProductionFacilityDefinition(
                    new ProductionFacilityId(0), "Armoury", ProductionCapacityMode.Unlimited),
            ],
            [
                new ProductionRecipeDefinition(
                    new ProductionRecipeId(0),
                    "Arms",
                    new ProductionFacilityId(0),
                    capacityCost: 1,
                    labourCost: 2,
                    [new CommodityQuantity(new CommodityId(Coal), 2)],
                    [new CommodityQuantity(new CommodityId(Arms), 1)]),
            ],
            technologies: [new TechnologyDefinition(Paddlewheels, "Paddlewheels", cost: 1_000)],
            feeding: new FeedingSettings(
                [new FoodPreference([new CommodityId(Fish)])],
                [1, 2, 4]),
            shipTypes:
            [
                new ShipTypeDefinition(
                    new ShipTypeId(Trader), "Trader", cargo: 2, seaZones: 1,
                    buildCost:
                    [
                        new CommodityQuantity(new CommodityId(Lumber), 4),
                        new CommodityQuantity(new CommodityId(Fabric), 2),
                    ]),
                new ShipTypeDefinition(
                    new ShipTypeId(Frigate), "Frigate", seaZones: 3,
                    buildCost:
                    [
                        new CommodityQuantity(new CommodityId(Lumber), 5),
                        new CommodityQuantity(new CommodityId(Fabric), 2),
                        new CommodityQuantity(new CommodityId(Arms), 2),
                    ]),
                new ShipTypeDefinition(
                    new ShipTypeId(Paddlewheeler), "Paddlewheeler", cargo: 8, seaZones: 1,
                    buildCost:
                    [
                        new CommodityQuantity(new CommodityId(Lumber), 6),
                        new CommodityQuantity(new CommodityId(Coal), 10),
                    ],
                    requiredTechnology: Paddlewheels),
            ],
            trade: new ProportionalTradeMarket()));
    }
}
