using Imperialism.Content;
using Imperialism.Core;
using Xunit;

namespace Imperialism.Presentation.Tests;

/// <summary>
/// Row 4e-2: the Armoury, the Shipyard and regiment moves in the draft, and the previews
/// that restate Core's planners for them. Every preview warning is checked against the
/// event Core actually emits for the same orders, and every effect against the state the
/// turn leaves behind.
/// </summary>
public sealed class MilitaryOrdersTests
{
    private const string Scenario = "scenario.demo";
    private static readonly CountryId Blue = new(0);
    private static readonly CountryId Red = new(1);

    private static readonly ShipTypeId Trader = new(0);
    private static readonly ShipTypeId Frigate = new(1);
    private static readonly ShipTypeId Ironclad = new(2);

    private static readonly ProvinceId Home = new(0);
    private static readonly ProvinceId Far = new(1);
    private static readonly ProvinceId West = new(2);
    private static readonly ProvinceId East = new(3);

    /// <summary>
    /// A six-cell row: Blue's capital in Home (0), then West (1) and East (2), both
    /// Blue's and railed back to Home; Red's capital in Far (3); and the ocean (4, 5).
    /// Blue holds two arms, six lumber, two Regulars and a Militia at home, and one
    /// Regular in East. A Regular costs an untrained worker, one arms and $500.
    /// </summary>
    private static CompiledWorldPackage Compile(long blueCash = 5000, long blueTransport = 40, bool diplomacy = false)
    {
        var document = TurnReportFixture.CreateDocument();
        if (diplomacy)
        {
            // Posts priced, and a Minor Nation with no ground that Blue holds no post in.
            document.Diplomacy = new DiplomacyContentSettings
            {
                ConsulateCost = 500, EmbassyCost = 5000, SubsidyPercents = [5, 10],
            };
            document.Countries =
            [
                .. document.Countries,
                new CountryContentDefinition { Key = "country.green", Name = "Green Duchy", IsGreatPower = false },
            ];
        }

        document.Commodities =
        [
            .. document.Commodities,
            new CommodityContentDefinition
            {
                Key = "commodity.arms",
                Name = "Arms",
                Category = CommodityCategory.Material,
                WorldPrice = 300,
                TradeOrder = 2,
            },
        ];
        document.ShipTypes =
        [
            document.ShipTypes[0],
            new ShipTypeContentDefinition
            {
                Key = "ship.frigate",
                Name = "Frigate",
                SeaZones = 2,
                BuildCost = [new CommodityQuantityContent { Commodity = "commodity.lumber", Quantity = 4 }],
                Combat = new ShipCombatContent { Firepower = 100, Range = 1, Armour = 10, HullScale = 600, BattleSpeed = 2 },
            },
            new ShipTypeContentDefinition
            {
                Key = "ship.ironclad",
                Name = "Ironclad",
                SeaZones = 2,
                BuildCost = [new CommodityQuantityContent { Commodity = "commodity.lumber", Quantity = 1 }],
                RequiredTechnology = "technology.dynamite",
                Combat = new ShipCombatContent { Firepower = 300, Range = 2, Armour = 40, HullScale = 1200, BattleSpeed = 2 },
            },
        ];

        var stats = new ArmyStatsContent
        {
            FirepowerVsInfantry = 12.5m, FirepowerVsMounted = 12.5m, Range = 5,
            Defence = 5, EntrenchedDefence = 6, Movement = 4,
        };
        document.ArmyTypes =
        [
            new ArmyTypeContentDefinition
            {
                Key = "army.militia", Name = "Militia", Category = ArmyCategory.Militia, Era = 1,
                MovesByRail = false, LeavesProvince = false, Stats = stats,
            },
            new ArmyTypeContentDefinition
            {
                Key = "army.regulars",
                Name = "Regulars",
                Category = ArmyCategory.RegularInfantry,
                Era = 1,
                Purchase = new ArmyPurchaseContent
                {
                    WorkerGrade = WorkerGrade.Untrained,
                    Bill = [new CommodityQuantityContent { Commodity = "commodity.arms", Quantity = 1 }],
                    Cash = 500,
                },
                Arms = 1,
                Stats = stats,
            },
        ];

        document.Map.Width = 6;
        document.Map.Provinces =
        [
            .. document.Map.Provinces,
            new NamedContentDefinition { Key = "province.west", Name = "West Province" },
            new NamedContentDefinition { Key = "province.east", Name = "East Province" },
        ];
        CellContentDocument Land(string province, string? resource = null, bool site = false) => new()
        {
            Terrain = "terrain.plains",
            Region = new CellRegionContent { Province = province },
            Resources = resource is null ? [] : [resource],
            HasSettlementSite = site,
        };
        CellContentDocument Sea() => new()
        {
            Terrain = "terrain.ocean",
            Region = new CellRegionContent { SeaZone = "sea.north" },
        };
        document.Map.Cells =
        [
            Land("province.home", "resource.grain", site: true),
            Land("province.west"),
            Land("province.east"),
            Land("province.far", "resource.timber", site: true),
            Sea(),
            Sea(),
        ];

        foreach (var scenario in document.Scenarios)
        {
            scenario.ProvinceOwners =
            [
                .. scenario.ProvinceOwners,
                new ProvinceOwnerContent { Province = "province.west", Country = "country.blue" },
                new ProvinceOwnerContent { Province = "province.east", Country = "country.blue" },
            ];
            scenario.Capitals =
            [
                new CountryCapitalContent { Country = "country.blue", Cell = 0 },
                new CountryCapitalContent { Country = "country.red", Cell = 3 },
            ];
            scenario.Rails =
            [
                new CellLinkContent { First = 0, Second = 1 },
                new CellLinkContent { First = 1, Second = 2 },
            ];
            scenario.Cash =
            [
                new CountryCashContent { Country = "country.blue", Amount = blueCash },
                new CountryCashContent { Country = "country.red", Amount = 5000 },
            ];
            scenario.TransportCapacity =
            [
                new TransportCapacityContent { Country = "country.blue", Capacity = blueTransport },
                new TransportCapacityContent { Country = "country.red", Capacity = 40 },
            ];
            scenario.InitialInventory =
            [
                .. scenario.InitialInventory,
                new InitialInventoryContent { Country = "country.blue", Commodity = "commodity.arms", Quantity = 2 },
                new InitialInventoryContent { Country = "country.blue", Commodity = "commodity.lumber", Quantity = 6 },
            ];
            scenario.Armies =
            [
                new ArmyContent { Province = "province.home", Type = 1 },
                new ArmyContent { Province = "province.home", Type = 1 },
                new ArmyContent { Province = "province.home", Type = 0 },
                new ArmyContent { Province = "province.east", Type = 1 },
            ];
        }

        return WorldContentCompiler.CompilePackage(document);
    }

    private sealed class Game(CompiledWorldPackage package)
    {
        public CompiledWorldPackage Package { get; } = package;

        public WorldState State { get; } = new(package.GetWorld(Scenario));

        public OrdersDraft Draft { get; } = new(package.GetWorld(Scenario), Blue);

        public int Turn { get; private set; } = 1;

        public OrdersPreview Preview() => OrdersPreview.Create(Package, Scenario, State, Draft);

        /// <summary>The two Regulars at home, the Militia, and the Regular in East.</summary>
        public (RegimentId First, RegimentId Second, RegimentId Militia, RegimentId Forward) Regiments()
        {
            var held = State.GetRegiments(Blue);
            var home = held.Where(item => item.Province == Home && item.Type.Value == 1).ToArray();
            return (
                home[0].Id,
                home[1].Id,
                held.Single(item => item.Type.Value == 0).Id,
                held.Single(item => item.Province == East).Id);
        }

        /// <summary>Resolves the draft, checks the preview against Core, and advances.</summary>
        public IReadOnlyList<TurnEvent> EndTurn()
        {
            var preview = Preview();
            var events = TurnResolver.Resolve(State, Draft.BuildTurnOrders(), (ulong)Turn).Events;
            AssertPreviewMatches(preview, events);
            Draft.AdvanceTurn();
            Turn++;
            return events;
        }
    }

    /// <summary>
    /// The preview's warning for every drafted Armoury row, hull and move is the refusal
    /// Core gives it, its verb is Core's, and its cash and rail are what Core charged.
    /// </summary>
    private static void AssertPreviewMatches(OrdersPreview preview, IReadOnlyList<TurnEvent> events)
    {
        var purchases = events
            .Where(item => item is RegimentsPurchasedEvent { Country.Value: 0 } or
                RegimentPurchaseRefusedEvent { Country.Value: 0 })
            .Select(item => item switch
            {
                RegimentsPurchasedEvent bought => (bought.Category, (RegimentPurchaseRefusal?)null),
                RegimentPurchaseRefusedEvent refused => (refused.Category, refused.Reason),
                _ => throw new InvalidOperationException(),
            })
            .OrderBy(static item => item.Category);
        Assert.Equal(
            purchases,
            preview.Armoury.Categories.Where(static row => row.Drafted > 0).Select(static row => (row.Category, row.Warning?.Refusal)));
        Assert.Equal(
            events.OfType<RegimentsPurchasedEvent>().Where(static item => item.Country == Blue).Sum(static item => item.CashPaid),
            preview.Armoury.CashClaimed);

        var hulls = events
            .Where(item => item is ShipsBuiltEvent { Country.Value: 0 } or ShipBuildRefusedEvent { Country.Value: 0 })
            .Select(item => item switch
            {
                ShipsBuiltEvent built => (built.Type.Value, (ShipBuildRefusal?)null),
                ShipBuildRefusedEvent refused => (refused.Type.Value, refused.Reason),
                _ => throw new InvalidOperationException(),
            })
            .OrderBy(static item => item.Value);
        Assert.Equal(
            hulls,
            preview.Shipyard.Hulls.Where(static row => row.Drafted > 0).Select(static row => (row.TypeIndex, row.Warning?.Refusal)));

        // Core moves before it attacks, so its events come in that order; the preview
        // lists moves in draft order. Match each drafted move to its own event.
        var moved = events.OfType<RegimentsMovedEvent>().Where(static item => item.Country == Blue).ToArray();
        var refusedMoves = events.OfType<RegimentMoveRefusedEvent>().Where(static item => item.Country == Blue).ToArray();
        foreach (var move in preview.ArmyMoves.Moves)
        {
            var refusal = refusedMoves.SingleOrDefault(item => item.Regiments.SequenceEqual(move.Regiments));
            Assert.Equal(refusal?.Reason, move.Warning);
            if (move.Warning is null && move.Verb is ArmyMoveVerb.March or ArmyMoveVerb.Rail)
            {
                var done = moved.Single(item => item.Regiments.SequenceEqual(move.Regiments));
                Assert.Equal(move.Verb == ArmyMoveVerb.Rail ? RegimentMoveMode.Rail : RegimentMoveMode.March, done.Mode);
                Assert.Equal(done.CapacityUsed, move.CapacityUsed);
            }
        }

        Assert.Equal(moved.Sum(static item => item.CapacityUsed), preview.ArmyMoves.RailCommitted);
    }

    [Fact]
    public void BuildMapsEachRowOneToOneAndANewClickReplacesTheOld()
    {
        var game = new Game(Compile());
        var (first, second, _, _) = game.Regiments();
        var draft = game.Draft;

        draft.SetRegimentPurchase(ArmyCategory.RegularInfantry, 2);
        draft.SetRegimentPurchase(ArmyCategory.LightCavalry, 1);
        draft.SetRegimentPurchase(ArmyCategory.RegularInfantry, 1);
        draft.SetShipBuild(Frigate, 1);
        draft.SetShipBuild(Trader, 0);
        draft.SetRegimentMove([first, second], West);
        draft.SetRegimentMove([second], East);

        var orders = draft.Build();
        Assert.Equal(
            [new RegimentPurchaseOrder(ArmyCategory.RegularInfantry, 1), new RegimentPurchaseOrder(ArmyCategory.LightCavalry, 1)],
            orders.RegimentPurchases);
        Assert.Equal([new ShipBuildOrder(Frigate, 1)], orders.ShipBuilds);
        Assert.Equal(2, orders.RegimentMoves.Count);
        Assert.Equal([first], orders.RegimentMoves[0].Regiments);
        Assert.Equal(West, orders.RegimentMoves[0].Destination);
        Assert.Equal([second], orders.RegimentMoves[1].Regiments);
        Assert.Equal(East, orders.RegimentMoves[1].Destination);
        Assert.Equal(East, draft.GetRegimentMove(second)!.Destination);

        Assert.True(draft.ClearRegimentMove(first));
        Assert.False(draft.ClearRegimentMove(first));
        Assert.Single(draft.RegimentMoves);
        Assert.Throws<ArgumentOutOfRangeException>(() => draft.SetRegimentPurchase(ArmyCategory.RegularInfantry, -1));
        Assert.Throws<ArgumentException>(() => draft.SetRegimentMove([], West));

        draft.ClearRegimentPurchases();
        draft.ClearShipBuilds();
        draft.ClearRegimentMoves();
        Assert.True(draft.IsEmpty);
    }

    [Fact]
    public void ArmouryShipyardAndMovesAreAllClearedAtEndTurn()
    {
        var game = new Game(Compile());
        var (first, _, _, _) = game.Regiments();
        game.Draft.SetRegimentPurchase(ArmyCategory.RegularInfantry, 1);
        game.Draft.SetShipBuild(Frigate, 1);
        game.Draft.SetRegimentMove([first], West);

        game.EndTurn();

        Assert.Empty(game.Draft.RegimentPurchases);
        Assert.Empty(game.Draft.ShipBuilds);
        Assert.Empty(game.Draft.RegimentMoves);
        Assert.True(game.Draft.IsEmpty);
    }

    [Theory]
    [InlineData(2, 5000, null)]
    [InlineData(3, 5000, RegimentPurchaseRefusal.NotEnoughCommodities)]
    [InlineData(5, 5000, RegimentPurchaseRefusal.NotEnoughWorkers)]
    [InlineData(2, 900, RegimentPurchaseRefusal.NotEnoughCash)]
    public void TheArmouryPreviewRefusesWhatCoreRefuses(long count, long cash, RegimentPurchaseRefusal? expected)
    {
        var game = new Game(Compile(blueCash: cash));
        var before = game.State.GetRegiments(Blue).Count;
        game.Draft.SetRegimentPurchase(ArmyCategory.RegularInfantry, count);
        game.Draft.SetRegimentPurchase(ArmyCategory.Militia, 1);

        var preview = game.Preview().Armoury;
        Assert.Equal(expected, preview.Categories.Single(row => row.Category == ArmyCategory.RegularInfantry).Warning);
        Assert.Equal(
            RegimentPurchaseRefusal.NotPurchasable,
            preview.Categories.Single(row => row.Category == ArmyCategory.Militia).Warning);
        Assert.Equal(expected is null ? count * 500 : 0, preview.CashClaimed);

        game.EndTurn();

        Assert.Equal(before + (expected is null ? count : 0), game.State.GetRegiments(Blue).Count);
    }

    [Fact]
    public void TheShipyardPreviewRefusesWhatCoreRefuses()
    {
        var game = new Game(Compile());
        game.Draft.SetShipBuild(Frigate, 1);
        game.Draft.SetShipBuild(Ironclad, 1);

        var preview = game.Preview().Shipyard;
        Assert.Null(preview.Hulls[Frigate.Value].Warning);
        Assert.True(preview.Hulls[Frigate.Value].IsWarship);
        Assert.False(preview.Hulls[Trader.Value].IsWarship);
        Assert.True(preview.Hulls[Ironclad.Value].IsGated);
        Assert.Equal(ShipBuildRefusal.TechnologyNotKnown, preview.Hulls[Ironclad.Value].Warning);

        game.EndTurn();

        Assert.Equal(1, game.State.Fleets.Where(fleet => fleet.Country == Blue && fleet.Type == Frigate).Sum(static fleet => fleet.Count));
        Assert.Equal(2, game.State.GetAvailableQuantity(Blue, Lumber(game)));

        // Two more frigates want eight lumber against two.
        game.Draft.SetShipBuild(Frigate, 2);
        Assert.Equal(ShipBuildRefusal.NotEnoughCommodities, game.Preview().Shipyard.Hulls[Frigate.Value].Warning);
        game.EndTurn();
    }

    [Theory]
    [InlineData(1500, null, 0)]
    [InlineData(1400, DiplomaticOvertureRefusal.NotEnoughCash, 400)]
    public void TheArmouryIsPaidBeforeOverturesAndTheTreasuryAfterBothIsTheResolvedOne(
        long cash, DiplomaticOvertureRefusal? expected, long after)
    {
        // Core pays the Armoury in Production and plans overtures against what it
        // leaves, so $1,400 buys two Regulars and then cannot open a $500 consulate.
        var game = new Game(Compile(blueCash: cash, diplomacy: true));
        var green = new CountryId(2);
        game.Draft.SetRegimentPurchase(ArmyCategory.RegularInfantry, 2);
        game.Draft.SetOverture(DiplomaticOvertureKind.Consulate, green, true);
        var preview = game.Preview().Diplomacy;
        Assert.Equal(cash, preview.CashAtOpen);
        Assert.Equal(1000, preview.ClaimedBeforeDiplomacy);
        Assert.Equal(1000, preview.ClaimedByArmoury);
        Assert.Equal(expected, preview.Overtures.Single().Warning);
        Assert.Equal(after, preview.CashAfterOvertures);

        var events = game.EndTurn();
        Assert.Equal(
            expected,
            events.OfType<DiplomaticOvertureRefusedEvent>().SingleOrDefault(item => item.Country == Blue)?.Reason);
        Assert.Equal(expected is null, game.State.HoldsDiplomaticTie(Blue, green, DiplomaticTie.Consulate));
        Assert.Equal(preview.CashAfterOvertures, game.State.GetCash(Blue));
    }

    [Fact]
    public void AShipBuildTakesItsBillOutOfWhatTheMarketMaySell()
    {
        var game = new Game(Compile());
        game.Draft.SetOffer(Lumber(game), 6);
        Assert.Equal(6, LumberRow(game.Preview()).Sellable);
        Assert.False(LumberRow(game.Preview()).IsOverOffered);

        game.Draft.SetShipBuild(Frigate, 1);
        var row = LumberRow(game.Preview());
        Assert.Equal(2, row.Sellable);
        Assert.True(row.IsOverOffered);

        // The Armoury's bill leaves the warehouse before the market too, and its cash
        // before any bid.
        game.Draft.SetRegimentPurchase(ArmyCategory.RegularInfantry, 2);
        var trade = game.Preview().Trade;
        Assert.Equal(0, trade.Rows.Single(item => item.CommodityKey == "commodity.arms").Sellable);
        Assert.Equal(1000, trade.ClaimedByArmoury);
        Assert.Equal(1000, game.Preview().Technology.ClaimedByArmoury);

        game.EndTurn();
    }

    [Fact]
    public void MarchesRailMovesAndRefusalsAgreeWithCore()
    {
        var game = new Game(Compile());
        var (first, second, militia, forward) = game.Regiments();
        game.Draft.SetRegimentMove([first], West);
        game.Draft.SetRegimentMove([second], East);
        game.Draft.SetRegimentMove([militia], West);
        game.Draft.SetRegimentMove([forward], Far);

        var preview = game.Preview().ArmyMoves;
        Assert.Equal(40, preview.RailCapacity);
        Assert.Equal(
            [
                (ArmyMoveVerb.March, 0L, (RegimentMoveRefusal?)null),
                (ArmyMoveVerb.Rail, 5L, null),
                (ArmyMoveVerb.March, 0L, RegimentMoveRefusal.CannotLeaveProvince),
                (ArmyMoveVerb.Attack, 0L, RegimentMoveRefusal.NotAtWar),
            ],
            preview.Moves.Select(static move => (move.Verb, move.CapacityUsed, move.Warning)));
        Assert.Equal(5, preview.RailCommitted);

        game.EndTurn();

        Assert.Equal(West, game.State.GetRegiment(first)!.Province);
        Assert.Equal(East, game.State.GetRegiment(second)!.Province);
        Assert.Equal(Home, game.State.GetRegiment(militia)!.Province);
        Assert.Equal(East, game.State.GetRegiment(forward)!.Province);
    }

    [Fact]
    public void ARailMoveBeyondTheBudgetIsRefusedWhileAMarchStillGoes()
    {
        var game = new Game(Compile(blueTransport: 4));
        var (first, second, _, _) = game.Regiments();
        game.Draft.SetRegimentMove([first], West);
        game.Draft.SetRegimentMove([second], East);

        var moves = game.Preview().ArmyMoves.Moves;
        Assert.Null(moves[0].Warning);
        Assert.Equal(RegimentMoveRefusal.NotEnoughRailCapacity, moves[1].Warning);

        game.EndTurn();

        Assert.Equal(West, game.State.GetRegiment(first)!.Province);
        Assert.Equal(Home, game.State.GetRegiment(second)!.Province);
    }

    [Fact]
    public void AnAttackIsRefusedBeforeTheWarAndFoughtOnceItIsInEffect()
    {
        var game = new Game(Compile());
        var (_, _, _, forward) = game.Regiments();

        game.Draft.SetDeclareWar(Red, true);
        game.Draft.SetRegimentMove([forward], Far);
        Assert.Equal(RegimentMoveRefusal.NotAtWar, game.Preview().ArmyMoves.Moves[0].Warning);
        game.EndTurn();
        Assert.Equal(Red, game.State.GetProvinceOwner(Far));

        game.Draft.SetRegimentMove([forward], Far);
        var move = game.Preview().ArmyMoves.Moves[0];
        Assert.Equal(ArmyMoveVerb.Attack, move.Verb);
        Assert.Null(move.Warning);
        var events = game.EndTurn();

        Assert.Contains(events, item => item is ProvinceCapturedEvent { Province.Value: 1 });
        Assert.Equal(Blue, game.State.GetProvinceOwner(Far));
    }

    [Fact]
    public void TheDestinationListGivesEachProvinceItsVerb()
    {
        var game = new Game(Compile());
        var (first, _, _, _) = game.Regiments();

        var view = OrdersPreview.CreateArmyMoves(game.Package, Scenario, game.State, game.Draft, [first]);
        var byProvince = view.Destinations.ToDictionary(static item => item.Province);
        Assert.False(byProvince.ContainsKey(Home));
        Assert.Equal(ArmyMoveVerb.March, byProvince[West].Verb);
        Assert.Equal(ArmyMoveVerb.Rail, byProvince[East].Verb);
        Assert.Equal(5, byProvince[East].CapacityUsed);
        Assert.Equal(ArmyMoveVerb.Attack, byProvince[Far].Verb);
        Assert.Equal(RegimentMoveRefusal.NotAdjacent, byProvince[Far].Warning);

        game.Draft.SetRegimentMove([first], West);
        view = OrdersPreview.CreateArmyMoves(game.Package, Scenario, game.State, game.Draft, [first]);
        Assert.True(view.Destinations.Single(item => item.Province == West).IsDrafted);
    }

    [Fact]
    public void AnUntouchedDraftStillResolvesLikeEmptyOrders()
    {
        var package = Compile();
        var world = package.GetWorld(Scenario);
        var draft = new OrdersDraft(world, Blue);
        var before = new WorldState(world);
        var after = new WorldState(world);
        var expected = TurnReportView.Create(
            package, Scenario, before, TurnResolver.Resolve(before, TurnOrders.Empty(world.Countries.Count), 1));
        var actual = TurnReportView.Create(
            package, Scenario, after, TurnResolver.Resolve(after, draft.BuildTurnOrders(), 1));
        Assert.Equal(expected.EventCount, actual.EventCount);
        Assert.Equal(
            expected.Phases.SelectMany(static phase => phase.Lines.Select(line => $"{phase.Phase}:{line}")),
            actual.Phases.SelectMany(static phase => phase.Lines.Select(line => $"{phase.Phase}:{line}")));
        Assert.Equal(before.GetCash(Blue), after.GetCash(Blue));
        Assert.Equal(before.GetRegiments(Blue).Count, after.GetRegiments(Blue).Count);
    }

    private static CommodityId Lumber(Game game) =>
        game.State.Definition.Commodities.Single(static item => item.Name == "Lumber").Id;

    private static TradeRowPreview LumberRow(OrdersPreview preview) =>
        preview.Trade.Rows.Single(static row => row.CommodityKey == "commodity.lumber");
}
