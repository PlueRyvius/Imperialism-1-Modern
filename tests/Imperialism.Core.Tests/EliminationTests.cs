using Imperialism.Core;
using Xunit;

namespace Imperialism.Core.Tests;

/// <summary>
/// Row 6e: a country whose capital is captured, or which a capture leaves with no
/// province, is eliminated for good; whatever it still holds is in anarchy, keeps its
/// owner, and may be attacked by anyone without a war.
/// </summary>
/// <remarks>
/// Every capital capture here is uncontested, or fought against a side that fires
/// nothing, so no outcome depends on a casualty figure only the placeholder loss rule
/// produces. Chosen rules 38 onwards are in <c>docs/formulas/army.md</c>.
/// </remarks>
public sealed class EliminationTests
{
    private const int Armaments = 0;
    private const int Fish = 1;

    private const int Regulars = 0;
    private const int Sappers = 1;

    private const int Trader = 0;

    private const int Width = 8;

    private static readonly CountryId Home = new(0);
    private static readonly CountryId Other = new(1);
    private static readonly CountryId Third = new(2);
    private static readonly CivilianTypeId Farmer = new(0);

    // ---- 7. Capturing a capital -------------------------------------------

    [Fact]
    public void CapturingACapitalEliminatesItsOwnerAndLeavesTheRestInAnarchy()
    {
        var state = Prepared();
        var attacker = state.CreateRegiment(Home, Province(2), Type(Regulars));
        var kept = state.CreateRegiment(Other, Province(5), Type(Sappers));
        var farmer = state.CreateCivilian(Other, Farmer, new CellIndex(4));
        var cash = state.GetCash(Other);
        var fish = state.GetAvailableQuantity(Other, new CommodityId(Fish));
        var arms = state.GetAvailableQuantity(Other, new CommodityId(Armaments));
        Assert.Contains(state.TaskForces, static force => force.Country == Other);

        var events = Resolve(state, home: Attack(attacker, 3));

        // Captured, then eliminated — the capture is what eliminates.
        var captured = Assert.Single(events.OfType<ProvinceCapturedEvent>());
        var eliminated = Assert.Single(events.OfType<CountryEliminatedEvent>());
        Assert.True(captured.CapitalLost);
        Assert.True(IndexOf(events, captured) < IndexOf(events, eliminated));
        Assert.Equal(
            (TurnPhase.Conflict, Other, Home, 2, 2, 5L, 4, 2),
            (eliminated.Phase, eliminated.Country, eliminated.By, eliminated.ProvincesInAnarchy,
                eliminated.FleetsLost, eliminated.ShipsLost, eliminated.DiplomaticTiesClosed,
                eliminated.TradePoliciesCleared));

        Assert.True(state.IsEliminated(Other));
        Assert.Equal(3, state.GetEliminatedOnTurn(Other));
        Assert.False(state.IsEliminated(Home));

        // Anarchy keeps the owner; the captured capital is simply Home's.
        Assert.Equal(Other, state.GetProvinceOwner(Province(4)));
        Assert.True(state.IsInAnarchy(Province(4)));
        Assert.True(state.IsInAnarchy(Province(5)));
        Assert.False(state.IsInAnarchy(Province(3)));
        Assert.False(state.IsInAnarchy(Province(2)));
        Assert.False(state.IsInAnarchy(Province(7)));

        // Fleets, task forces and every merchant hull go down with it.
        Assert.DoesNotContain(state.Fleets, static fleet => fleet.Country == Other);
        Assert.DoesNotContain(state.TaskForces, static force => force.Country == Other);
        Assert.Equal(0, state.GetShipCount(Other, new ShipTypeId(Trader)));
        Assert.Equal(3, state.GetShipCount(Third, new ShipTypeId(Trader)));

        // Posts and pacts both ways, and every trade policy both ways.
        foreach (var tie in new[] { DiplomaticTie.Consulate, DiplomaticTie.Embassy, DiplomaticTie.Pact })
        {
            Assert.False(state.HoldsDiplomaticTie(Other, Third, tie));
            Assert.False(state.HoldsDiplomaticTie(Third, Other, tie));
        }

        Assert.True(state.HoldsDiplomaticTie(Home, Third, DiplomaticTie.Embassy));
        Assert.Equal(TradePolicy.None, state.GetTradePolicy(Other, Third));
        Assert.Equal(TradePolicy.None, state.GetTradePolicy(Third, Other));

        // Regiments and civilians stand; treasury and warehouse are untouched.
        Assert.Equal(Province(5), state.GetRegiment(kept)!.Province);
        Assert.NotNull(state.GetCivilian(farmer));
        Assert.Equal(cash, state.GetCash(Other));
        Assert.Equal(fish, state.GetAvailableQuantity(Other, new CommodityId(Fish)));
        Assert.Equal(arms, state.GetAvailableQuantity(Other, new CommodityId(Armaments)));
    }

    // ---- 8. The turn after ------------------------------------------------

    [Fact]
    public void AnEliminatedCountrysOrdersAreIgnoredAndItsLandsNeitherGatherNorEat()
    {
        var state = Eliminated();
        var fish = state.GetAvailableQuantity(Other, new CommodityId(Fish));
        var workers = state.GetTotalWorkers(Other);
        var cash = state.GetCash(Other);

        var events = Resolve(state, other: new CountryTurnOrders(
            Other,
            regimentPurchases: [new RegimentPurchaseOrder(ArmyCategory.RegularInfantry, 1)],
            declareWar: [Third]));

        var ignored = Assert.Single(events.OfType<EliminatedCountryOrdersIgnoredEvent>());
        Assert.Equal((Other, TurnPhase.Diplomacy), (ignored.Country, ignored.Phase));
        Assert.Empty(events.OfType<RegimentsPurchasedEvent>());
        Assert.Empty(events.OfType<RegimentPurchaseRefusedEvent>());
        Assert.Empty(events.OfType<WarDeclaredEvent>());

        var extracted = events.OfType<ResourceExtractedEvent>().Where(static item => item.Country == Other);
        Assert.All(extracted, static item => Assert.Equal(0, item.CollectedCellCount));
        Assert.DoesNotContain(events.OfType<WorkersFedEvent>(), static item => item.Country == Other);
        Assert.Equal(fish, state.GetAvailableQuantity(Other, new CommodityId(Fish)));
        Assert.Equal(workers, state.GetTotalWorkers(Other));
        Assert.Equal(cash, state.GetCash(Other));

        // Empty orders from an eliminated country say nothing at all.
        Assert.Empty(Resolve(state).OfType<EliminatedCountryOrdersIgnoredEvent>());
    }

    // ---- 9. Anarchy needs no war ------------------------------------------

    [Fact]
    public void ACountryNotAtWarMayAttackAnAnarchicProvinceAndItsLocalForcesDefend()
    {
        var state = Eliminated();
        Assert.False(state.HasEffectiveHostility(Third, Other));
        var defender = state.CreateRegiment(Other, Province(5), Type(Sappers), health: 50);
        var attackers = new[]
        {
            state.CreateRegiment(Third, Province(6), Type(Regulars)),
            state.CreateRegiment(Third, Province(6), Type(Regulars)),
        };

        var events = Resolve(state, third: Attack(attackers, 5));

        Assert.DoesNotContain(events.OfType<RegimentMoveRefusedEvent>(), static item => item.Country == Third);
        var battle = Assert.Single(events.OfType<BattleFoughtEvent>());
        Assert.Equal((Third, Other, Third), (battle.Attacker, battle.Defender, battle.Winner));
        Assert.Contains(battle.Participants, item => item.Regiment == defender);
        var captured = Assert.Single(events.OfType<ProvinceCapturedEvent>());
        Assert.Equal((Other, Third), (captured.PreviousOwner, captured.NewOwner));
        Assert.Equal(Third, state.GetProvinceOwner(Province(5)));
        Assert.False(state.IsInAnarchy(Province(5)));

        // Nothing eliminates twice.
        Assert.Empty(events.OfType<CountryEliminatedEvent>());
        Assert.Equal(3, state.GetEliminatedOnTurn(Other));
    }

    // ---- 10. The same turn ------------------------------------------------

    [Fact]
    public void LaterTheSameTurnTheVictimIsRefusedAndAThirdCountryNeedsNoWar()
    {
        var state = Prepared();
        var home = state.CreateRegiment(Home, Province(2), Type(Regulars));
        // The victim's order is an attack from 5 into Third's 6; it is refused for
        // elimination before war is even asked about, and its Sappers then stand in 5.
        var victim = state.CreateRegiment(Other, Province(5), Type(Sappers), health: 50);
        var helper = state.CreateRegiment(Third, Province(6), Type(Regulars));
        var third = state.CreateRegiment(Third, Province(6), Type(Regulars));

        // Countries attack in id order: Home takes the capital first.
        var events = Resolve(
            state,
            home: Attack(home, 3),
            other: Attack(victim, 6),
            third: Attack([third, helper], 5));

        Assert.Single(events.OfType<CountryEliminatedEvent>());
        var refused = Assert.Single(events.OfType<RegimentMoveRefusedEvent>());
        Assert.Equal((Other, RegimentMoveRefusal.CountryEliminated), (refused.Country, refused.Reason));
        Assert.False(state.HasEffectiveHostility(Third, Other));
        Assert.Equal(Third, state.GetProvinceOwner(Province(5)));
        Assert.Equal(Home, state.GetProvinceOwner(Province(3)));
    }

    // ---- 11. No province, no capital --------------------------------------

    [Fact]
    public void ACountryLeftWithNoProvinceIsEliminatedAndACapitallessOneWithLandIsNot()
    {
        var landed = Prepared(otherCapital: false);
        var first = landed.CreateRegiment(Home, Province(2), Type(Regulars));
        var events = Resolve(landed, home: Attack(first, 3));
        Assert.False(Assert.Single(events.OfType<ProvinceCapturedEvent>()).CapitalLost);
        Assert.Empty(events.OfType<CountryEliminatedEvent>());
        Assert.False(landed.IsEliminated(Other));
        Assert.False(landed.IsInAnarchy(Province(4)));

        var bare = Prepared(otherCapital: false);
        bare.SetProvinceOwner(Province(4), Home);
        bare.SetProvinceOwner(Province(5), Home);
        var last = bare.CreateRegiment(Home, Province(2), Type(Regulars));
        var eliminated = Assert.Single(Resolve(bare, home: Attack(last, 3)).OfType<CountryEliminatedEvent>());
        Assert.Equal(0, eliminated.ProvincesInAnarchy);
        Assert.True(bare.IsEliminated(Other));
    }

    // ---- 12. Diplomacy ----------------------------------------------------

    [Fact]
    public void DiplomacyNamingAnEliminatedCountryIsRefused()
    {
        var state = Eliminated();

        var events = Resolve(
            state,
            home: new CountryTurnOrders(
                Home,
                overtures: [new DiplomaticOverture(DiplomaticOvertureKind.Peace, Other)]),
            third: new CountryTurnOrders(
                Third,
                declareWar: [Other],
                overtures: [new DiplomaticOverture(DiplomaticOvertureKind.Consulate, Other)],
                tradePolicies: [new TradePolicyOrder(Other, TradePolicy.Boycott)]));

        Assert.Equal(
            WarDeclarationRefusal.CountryEliminated,
            Assert.Single(events.OfType<WarDeclarationRefusedEvent>()).Reason);
        Assert.All(
            events.OfType<DiplomaticOvertureRefusedEvent>(),
            static item => Assert.Equal(DiplomaticOvertureRefusal.CountryEliminated, item.Reason));
        Assert.Equal(2, events.OfType<DiplomaticOvertureRefusedEvent>().Count());
        Assert.Equal(
            TradePolicyRefusal.CountryEliminated,
            Assert.Single(events.OfType<TradePolicyRefusedEvent>()).Reason);
        Assert.Equal(TradePolicy.None, state.GetTradePolicy(Third, Other));
    }

    // ---- 13. Bought, then eliminated --------------------------------------

    [Fact]
    public void ARegimentTheVictimBoughtEarlierTheSameTurnIsLostInDelivery()
    {
        var state = Prepared();
        var attacker = state.CreateRegiment(Home, Province(2), Type(Regulars));
        var before = state.GetRegiments(Other).Count;

        var events = Resolve(
            state,
            home: Attack(attacker, 3),
            other: new CountryTurnOrders(
                Other, regimentPurchases: [new RegimentPurchaseOrder(ArmyCategory.RegularInfantry, 1)]));

        var bought = Assert.Single(events.OfType<RegimentsPurchasedEvent>());
        Assert.Equal(TurnPhase.Production, bought.Phase);
        Assert.Single(events.OfType<CountryEliminatedEvent>());
        var lost = Assert.Single(events.OfType<RegimentDeliveryLostEvent>());
        Assert.Equal((Other, TurnPhase.Delivery), (lost.Country, lost.Phase));
        Assert.DoesNotContain(events.OfType<RegimentsDeliveredEvent>(), static item => item.Country == Other);
        Assert.Equal(before, state.GetRegiments(Other).Count);
    }

    // ---- 14. Determinism --------------------------------------------------

    [Fact]
    public void TheSameSeedEliminatesTheSameWay()
    {
        static (string[] Events, bool Eliminated, long Health) Run(ulong seed)
        {
            var state = Prepared();
            var attackers = new[]
            {
                state.CreateRegiment(Home, Province(2), Type(Regulars)),
                state.CreateRegiment(Home, Province(2), Type(Regulars)),
            };
            _ = state.CreateRegiment(Other, Province(3), Type(Sappers), health: 50);
            var events = Resolve(state, seed, home: Attack(attackers, 3));
            return (
                events.Select(static item => item.ToString()).ToArray(),
                state.IsEliminated(Other),
                attackers.Sum(id => state.GetRegiment(id)?.Health ?? 0));
        }

        var first = Run(11);
        var second = Run(11);
        Assert.True(first.Eliminated);
        Assert.Equal(first.Events, second.Events);
        Assert.Equal(first.Health, second.Health);
    }

    // ---- Helpers ----------------------------------------------------------

    private static int IndexOf(IReadOnlyList<TurnEvent> events, TurnEvent item) =>
        events.Select((value, index) => (value, index)).First(pair => ReferenceEquals(pair.value, item)).index;

    private static ArmyTypeId Type(int value) => new(value);

    private static ProvinceId Province(int value) => new(value);

    private static CountryTurnOrders Attack(RegimentId regiment, int province) =>
        Attack([regiment], province);

    private static CountryTurnOrders Attack(RegimentId[] regiments, int province) => new(
        Home, regimentMoves: [new RegimentMoveOrder(regiments, Province(province))]);

    private static IReadOnlyList<TurnEvent> Resolve(
        WorldState state,
        ulong seed = 0,
        CountryTurnOrders? home = null,
        CountryTurnOrders? other = null,
        CountryTurnOrders? third = null) =>
        TurnResolver.Resolve(
            state,
            new TurnOrders(
            [
                Rebind(home, Home),
                Rebind(other, Other),
                Rebind(third, Third),
            ]),
            seed).Events;

    /// <summary>Orders are filed by position, so an attack built for Home may stand for anyone.</summary>
    private static CountryTurnOrders Rebind(CountryTurnOrders? orders, CountryId country) =>
        orders is null || orders.Country == country
            ? orders ?? new CountryTurnOrders(country)
            : new CountryTurnOrders(country, regimentMoves: orders.RegimentMoves);

    /// <summary>A prepared world after Other's capital has fallen on turn 3.</summary>
    private static WorldState Eliminated()
    {
        var state = Prepared();
        var attacker = state.CreateRegiment(Home, Province(2), Type(Regulars));
        _ = Assert.Single(Resolve(state, home: Attack(attacker, 3)).OfType<CountryEliminatedEvent>());
        return state;
    }

    /// <summary>
    /// The fixture after two turns: Home's declared war on Other in effect, Other's placed fleet
    /// assembled into a task force, Other subsidising Third and Third boycotting Other.
    /// The Great Powers' posts at each other stand from the start, except Home's and
    /// Other's, which the war closed.
    /// </summary>
    private static WorldState Prepared(bool otherCapital = true)
    {
        var state = CreateState(otherCapital);
        var placed = state.Fleets.Single(static fleet => fleet.Country == Other && fleet.SeaZone is not null);
        _ = TurnResolver.Resolve(
            state,
            new TurnOrders(
            [
                new CountryTurnOrders(Home, declareWar: [Other]),
                new CountryTurnOrders(
                    Other,
                    naval: [NavalOrder.Assemble([placed.Id], NavalMission.None)],
                    tradePolicies: [new TradePolicyOrder(Third, TradePolicy.Subsidy(5))]),
                new CountryTurnOrders(Third, tradePolicies: [new TradePolicyOrder(Other, TradePolicy.Boycott)]),
            ]),
            0);

        // The declaration takes effect as the next turn opens.
        _ = TurnResolver.Resolve(state, TurnOrders.Empty(3), 0);
        Assert.True(state.HasEffectiveHostility(Home, Other));
        Assert.False(state.HoldsDiplomaticTie(Home, Other, DiplomaticTie.Consulate));
        Assert.Equal(TradePolicy.Subsidy(5), state.GetTradePolicy(Other, Third));
        Assert.Equal(TradePolicy.Boycott, state.GetTradePolicy(Third, Other));
        Assert.Single(state.TaskForces, static force => force.Country == Other);
        return state;
    }

    /// <summary>
    /// Eight farm tiles in a row, one province each, and a ninth cell of sea. Home holds
    /// 0–2 with its capital on 0; Other holds 3–5 with its capital on 3; Third holds 6
    /// with its capital there; 7 has no owner. All three are Great Powers.
    /// </summary>
    private static WorldState CreateState(bool otherCapital)
    {
        var dimensions = new MapDimensions(Width + 1, 1);
        var cells = Enumerable.Range(0, Width + 1)
            .Select(index => new CellDefinition(
                new CellIndex(index),
                dimensions.GetCoordinate(new CellIndex(index)),
                new TerrainId(0),
                index == Width
                    ? CellRegion.ForSeaZone(new SeaZoneId(0))
                    : CellRegion.ForProvince(new ProvinceId(index)),
                index is >= 1 and <= 5 ? [new ResourceId(0)] : null,
                index is 0 or 3 or 6 ? SettlementSiteKind.Urban : SettlementSiteKind.None))
            .ToArray();

        var map = new MapDefinition(
            dimensions,
            cells,
            Enumerable.Range(0, Width)
                .Select(static index => new ProvinceDefinition(new ProvinceId(index), $"Province {index}")),
            [new SeaZoneDefinition(new SeaZoneId(0), "Sea")],
            [new ResourceDefinition(new ResourceId(0), new CommodityId(Fish), [1])],
            [new TerrainDefinition(new TerrainId(0), "Farm")]);

        CountryId? Owner(int index) => index switch
        {
            <= 2 => Home,
            <= 5 => Other,
            6 => Third,
            _ => null,
        };

        CountryId[] countries = [Home, Other, Third];
        CountryCapital[] capitals = otherCapital
            ?
            [
                new CountryCapital(Home, new CellIndex(0)),
                new CountryCapital(Other, new CellIndex(3)),
                new CountryCapital(Third, new CellIndex(6)),
            ]
            :
            [
                new CountryCapital(Home, new CellIndex(0)),
                new CountryCapital(Third, new CellIndex(6)),
            ];

        var scenario = new ScenarioDefinition(
            "Elimination",
            1816,
            Enumerable.Range(0, Width).Select(Owner),
            initialCountryCapitals: capitals,
            initialInventory: countries
                .Select(static country => new InitialCommodityStock(country, new CommodityId(Fish), 1000))
                .Append(new InitialCommodityStock(Other, new CommodityId(Armaments), 10)),
            initialWorkforce: [new InitialWorkforce(Other, 3, 0, 0)],
            initialCash: countries.Select(static country => new InitialCash(country, 5000)),
            initialShips:
            [
                new InitialShip(Other, new ShipTypeId(Trader), 0, 2, new SeaZoneId(0)),
                new InitialShip(Other, new ShipTypeId(Trader), 0, 3),
                new InitialShip(Third, new ShipTypeId(Trader), 0, 3),
            ]);

        static ArmyCombatStats Stats(decimal firepower, long defence, long? entrenched) =>
            new(firepower, firepower, 5, null, defence, entrenched, 4);

        ArmyTypeDefinition[] catalogue =
        [
            new(Type(Regulars), "Regulars", ArmyCategory.RegularInfantry, 1,
                purchase: new(WorkerGrade.Untrained, [new CommodityQuantity(new CommodityId(Armaments), 1)], 500),
                arms: 1, stats: Stats(10, 5, 6)),
            new(Type(Sappers), "Sappers", ArmyCategory.CombatEngineers, 1, stats: Stats(0, 3, 4)),
        ];

        var definition = new WorldDefinition(
            map,
            [
                new CountryDefinition(Home, "Home", isGreatPower: true),
                new CountryDefinition(Other, "Other", isGreatPower: true),
                new CountryDefinition(Third, "Third", isGreatPower: true),
            ],
            scenario,
            [
                new CommodityDefinition(new CommodityId(Armaments), "Armaments", CommodityCategory.Goods),
                new CommodityDefinition(new CommodityId(Fish), "Fish", CommodityCategory.Raw),
            ],
            [],
            [],
            extraction: new ExtractionSettings(Width),
            feeding: new FeedingSettings([new FoodPreference([new CommodityId(Fish)])], [1, 2, 4]),
            civilianTypes: [new CivilianTypeDefinition(Farmer, "Farmer", 3)],
            shipTypes: [new ShipTypeDefinition(new ShipTypeId(Trader), "Trader", cargo: 1, seaZones: 2)],
            armyTypes: catalogue,
            diplomacy: new DiplomacySettings(500, 5000, [5, 10]));
        return new WorldState(definition);
    }
}
