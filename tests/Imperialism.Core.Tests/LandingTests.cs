using Imperialism.Core;
using Xunit;

namespace Imperialism.Core.Tests;

/// <summary>
/// Row 6d: a task force holds a landing site on a province (task-force state 5), and from
/// the next turn regiments cross to it from any coast with a sea path, up to the arms its
/// hulls were built with, through an interception battle and drowning, into 6b's battle.
/// </summary>
/// <remarks>
/// The capacity reading, drowning and the interception battle are placeholders until a PC
/// trace (<c>wanted-values.md</c> §8), so nothing here pins a figure only the placeholder
/// naval resolver produces: where a battle's outcome matters, the test searches seeds for
/// the shape it needs and asserts the rule on whatever it finds. Chosen rules are naval
/// 29 onwards and army 31 onwards.
/// <para>
/// The fixture is one row of ten cells:
/// <c>0 Inland(H, capital) · 1 Harbour(H) · 2 West sea · 3 East sea · 4 Beach(O, capital)
/// · 5 Border(H) · 6 Citadel(O, port) · 7 Lake sea · 8 Islet(H) · 9 Freeport(T, capital)</c>.
/// Lake joins no other sea.
/// </para>
/// </remarks>
public sealed class LandingTests
{
    private const int Armaments = 0;
    private const int Horses = 1;
    private const int Coal = 2;
    private const int Fish = 3;

    private const int Trader = 0;
    private const int Frigate = 1;
    private const int Lighter = 2;
    private const int Longbow = 3;
    private const int Hulk = 4;
    private const int Battleship = 5;

    private const int Minutemen = 0;
    private const int Regulars = 1;
    private const int Cannons = 2;
    private const int Bulwark = 3;
    private const int General = 4;

    private static readonly CountryId Home = new(0);
    private static readonly CountryId Other = new(1);
    private static readonly CountryId Third = new(2);

    private static readonly ProvinceId Inland = new(0);
    private static readonly ProvinceId Harbour = new(1);
    private static readonly ProvinceId Beach = new(2);
    private static readonly ProvinceId Border = new(3);
    private static readonly ProvinceId Citadel = new(4);
    private static readonly ProvinceId Islet = new(5);

    private static readonly SeaZoneId West = new(0);
    private static readonly SeaZoneId East = new(1);
    private static readonly SeaZoneId Lake = new(2);

    // ---- 1. The order ----------------------------------------------------

    [Fact]
    public void ALandingIsAcceptedOnAProvinceItsZoneTouchesAndAMoveClearsIt()
    {
        var state = CreateState(Ship(Home, Hulk, East));
        var ordered = Resolve(state, 0, Naval(Home, NavalOrder.Assemble([new FleetId(1)], NavalMission.Landing(Beach))));

        var landing = Assert.Single(ordered.Events.OfType<TaskForceLandingOrderedEvent>());
        Assert.Equal((Home, new TaskForceId(1), East, Beach, TurnPhase.Conflict),
            (landing.Country, landing.TaskForce, landing.SeaZone, landing.Target, landing.Phase));
        var force = state.GetTaskForce(new TaskForceId(1));
        Assert.Equal((TaskForceActivity.Landing, (ProvinceId?)Beach, (int?)landing.TurnNumber),
            (force.Activity, force.LandingTarget, force.LandingSince));

        // It persists with no orders, like a patrol or a blockade.
        _ = Resolve(state, 0);
        Assert.Equal((TaskForceActivity.Landing, (int?)landing.TurnNumber), (force.Activity, force.LandingSince));

        _ = Resolve(state, 0, Naval(Home, NavalOrder.Command(new TaskForceId(1), NavalMission.MoveTo(West))));
        Assert.Equal((TaskForceActivity.Idle, (ProvinceId?)null, (int?)null, West),
            (force.Activity, force.LandingTarget, force.LandingSince, force.SeaZone));
    }

    // ---- 2. Its refusals -------------------------------------------------

    [Fact]
    public void EveryInvalidLandingIsRefusedWithItsReasonAndChangesNothing()
    {
        var state = CreateState(Ship(Home, Hulk, East), Ship(Home, Hulk, West));
        _ = state.AssembleTaskForce(Home, [new FleetId(2)]);

        var result = Resolve(
            state,
            0,
            Naval(
                Home,
                NavalOrder.Assemble([new FleetId(1)], NavalMission.Landing(new ProvinceId(99))),
                NavalOrder.Assemble([new FleetId(1)], NavalMission.Landing(Border)),
                NavalOrder.Assemble([new FleetId(1)], NavalMission.Landing(Citadel)),
                // Harbour touches West, but it is Home's own.
                NavalOrder.Command(new TaskForceId(1), NavalMission.Landing(Harbour))));

        Assert.Equal(
            [
                NavalOrderRefusal.UnknownProvince,
                NavalOrderRefusal.LandingOnOwnProvince,
                NavalOrderRefusal.ProvinceNotOnThisSeaZone,
                NavalOrderRefusal.LandingOnOwnProvince,
            ],
            result.Events.OfType<NavalOrderRefusedEvent>().Select(static e => e.Reason));
        Assert.Empty(result.Events.OfType<TaskForceLandingOrderedEvent>());
        var force = Assert.Single(state.TaskForces);
        Assert.Equal((TaskForceActivity.Idle, (ProvinceId?)null), (force.Activity, force.LandingTarget));
        Assert.Null(state.GetFleet(new FleetId(1)).TaskForce);
    }

    // ---- 3. Not the turn it is established --------------------------------

    [Fact]
    public void TroopsCannotCrossTheTurnTheSiteIsEstablishedButMayTheNext()
    {
        var state = AtWar(CreateState(Ship(Home, Hulk, East)));
        var regiment = state.CreateRegiment(Home, Harbour, Type(Regulars));

        var same = Resolve(
            state,
            0,
            new CountryTurnOrders(
                Home,
                naval: [NavalOrder.Assemble([new FleetId(1)], NavalMission.Landing(Beach))],
                regimentMoves: [new([regiment], Beach)]));
        Assert.Single(same.Events.OfType<TaskForceLandingOrderedEvent>());
        AssertRefused(same.Events, RegimentMoveRefusal.NotAdjacent);
        Assert.Equal(Harbour, state.GetRegiment(regiment)!.Province);

        var next = Resolve(state, 0, Move(Home, Beach, regiment));
        Assert.Single(next.Events.OfType<RegimentsLandedEvent>());
        Assert.Equal(Home, state.GetProvinceOwner(Beach));
    }

    // ---- 4. An undefended landing ----------------------------------------

    [Fact]
    public void AnUndefendedLandingTakesTheProvinceAndTheSitePersists()
    {
        var state = ReadySite(Ship(Home, Hulk, East, count: 2));
        var regulars = state.CreateRegiment(Home, Harbour, Type(Regulars));
        var cannons = state.CreateRegiment(Home, Harbour, Type(Cannons));
        var general = state.CreateRegiment(Home, Harbour, Type(General));

        var events = Resolve(state, 0, Move(Home, Beach, regulars, cannons, general)).Events;

        // Two Hulks built with three armaments each carry six; the General carries none.
        var landed = Assert.Single(events.OfType<RegimentsLandedEvent>());
        Assert.Equal((Home, new TaskForceId(1), Harbour, Beach, 3L, 3L),
            (landed.Country, landed.TaskForce, landed.From, landed.Destination, landed.ArmsUsed, landed.CapacityLeft));
        Assert.Equal([regulars, cannons, general], landed.Regiments);
        Assert.Empty(events.OfType<RegimentsDrownedEvent>());
        Assert.Empty(events.OfType<NavalBattleFoughtEvent>());

        var captured = Assert.Single(events.OfType<ProvinceCapturedEvent>());
        Assert.True(captured.Uncontested);
        Assert.True(captured.CapitalLost);
        Assert.True(Index(events, landed) < Index(events, captured));
        Assert.Equal(Home, state.GetProvinceOwner(Beach));
        Assert.All([regulars, cannons, general], id => Assert.Equal(Beach, state.GetRegiment(id)!.Province));

        // The site is a fleet mission and outlives the landing (chosen).
        var force = state.GetTaskForce(new TaskForceId(1));
        Assert.Equal((TaskForceActivity.Landing, (ProvinceId?)Beach), (force.Activity, force.LandingTarget));
    }

    // ---- 5. Capacity -----------------------------------------------------

    [Fact]
    public void ALoadOneArmsPointOverIsRefusedWhole()
    {
        var state = ReadySite(Ship(Home, Hulk, East));
        var regiments = Enumerable.Range(0, 4).Select(_ => state.CreateRegiment(Home, Harbour, Type(Regulars))).ToArray();

        var events = Resolve(state, 0, Move(Home, Beach, regiments)).Events;

        AssertRefused(events, RegimentMoveRefusal.NotEnoughLandingCapacity);
        Assert.Empty(events.OfType<RegimentsLandedEvent>());
        Assert.Empty(events.OfType<RegimentsDrownedEvent>());
        Assert.All(regiments, id => Assert.Equal(Harbour, state.GetRegiment(id)!.Province));
        Assert.Equal(Other, state.GetProvinceOwner(Beach));

        // Three fit exactly.
        var fits = Resolve(state, 0, Move(Home, Beach, regiments[..3])).Events;
        Assert.Equal(0, Assert.Single(fits.OfType<RegimentsLandedEvent>()).CapacityLeft);
    }

    [Fact]
    public void TwoLandingsThroughOneSiteSpendItsCapacityCumulatively()
    {
        var state = ReadySite(Ship(Home, Hulk, East));
        _ = state.CreateRegiment(Other, Beach, Type(Bulwark)); // holds, so Beach stays Other's
        var first = Enumerable.Range(0, 2).Select(_ => state.CreateRegiment(Home, Harbour, Type(Regulars))).ToArray();
        var second = Enumerable.Range(0, 2).Select(_ => state.CreateRegiment(Home, Harbour, Type(Regulars))).ToArray();

        var events = Resolve(
            state,
            0,
            new CountryTurnOrders(Home, regimentMoves: [new(first, Beach), new(second, Beach)])).Events;

        var landed = Assert.Single(events.OfType<RegimentsLandedEvent>());
        Assert.Equal(first, landed.Regiments);
        Assert.Equal((2L, 1L), (landed.ArmsUsed, landed.CapacityLeft));
        Assert.Single(events.OfType<BattleFoughtEvent>());
        AssertRefused(events, RegimentMoveRefusal.NotEnoughLandingCapacity);

        // Capacity is per turn: next turn the site carries three again.
        var again = Resolve(state, 0, Move(Home, Beach, second)).Events;
        Assert.Equal(1, Assert.Single(again.OfType<RegimentsLandedEvent>()).CapacityLeft);
    }

    // ---- 6. What wins over a landing -------------------------------------

    [Fact]
    public void NoCoastNoSeaPathNoWarAndMilitiaAreRefusedBeforeAnyLanding()
    {
        var state = ReadySite(Ship(Home, Hulk, East));
        var inland = state.CreateRegiment(Home, Inland, Type(Regulars));
        var islet = state.CreateRegiment(Home, Islet, Type(Regulars));
        var militia = state.CreateRegiment(Home, Harbour, Type(Minutemen));

        Assert.Equal(
            [
                RegimentMoveRefusal.NotCoastal,
                RegimentMoveRefusal.NoSeaPath,
                RegimentMoveRefusal.CannotLeaveProvince,
            ],
            Refusals(Resolve(state, 0, new CountryTurnOrders(
                Home,
                regimentMoves: [new([inland], Beach), new([islet], Beach), new([militia], Beach)])).Events));

        // Without a war the same landing is refused, and nothing crosses.
        var peace = ReadySite(war: false, Ship(Home, Hulk, East));
        var regiment = peace.CreateRegiment(Home, Harbour, Type(Regulars));
        var events = Resolve(peace, 0, Move(Home, Beach, regiment)).Events;
        AssertRefused(events, RegimentMoveRefusal.NotAtWar);
        Assert.Empty(events.OfType<RegimentsLandedEvent>());
        Assert.Equal(Harbour, peace.GetRegiment(regiment)!.Province);
    }

    // ---- 7. Next door is still 6b ----------------------------------------

    [Fact]
    public void AnAdjacentAttackIgnoresTheSiteAndItsEnemies()
    {
        var state = ReadySite(Ship(Home, Hulk, East), Ship(Other, Longbow, East));
        var regiments = Enumerable.Range(0, 5).Select(_ => state.CreateRegiment(Home, Border, Type(Regulars))).ToArray();

        var events = Resolve(state, 0, Move(Home, Beach, regiments)).Events;

        // Five arms through a three-arms site would be refused; next door needs no ship.
        Assert.Empty(events.OfType<RegimentMoveRefusedEvent>());
        Assert.Empty(events.OfType<RegimentsLandedEvent>());
        Assert.Empty(events.OfType<NavalBattleFoughtEvent>());
        Assert.Single(events.OfType<ProvinceCapturedEvent>());
        Assert.Equal(Home, state.GetProvinceOwner(Beach));
    }

    // ---- 8. The interception battle, won --------------------------------

    [Fact]
    public void AnIdleEnemyInTheSitesZoneFightsTheLandingAndTheLostHoldsDrownFromTheEnd()
    {
        var (state, events, regiments) = FirstLanding(static (landed, drowned, battle) =>
            battle.Winner == Home && drowned is not null && landed is not null);

        var battle = Assert.Single(events.OfType<NavalBattleFoughtEvent>());
        Assert.Equal((NavalBattleKind.Landing, East, Other, Home, TurnPhase.Conflict),
            (battle.Kind, battle.SeaZone, battle.Attacker, battle.Defender, battle.Phase));

        // Capacity after the battle: three per surviving Lighter.
        var lighters = state.Fleets.Where(f => f.Country == Home && f.Type == new ShipTypeId(Lighter)).Sum(static f => f.Count);
        var drowned = Assert.Single(events.OfType<RegimentsDrownedEvent>());
        var landed = Assert.Single(events.OfType<RegimentsLandedEvent>());
        Assert.Equal(regiments[^drowned.Regiments.Count..], drowned.Regiments);
        Assert.Equal(regiments[..^drowned.Regiments.Count], landed.Regiments);
        Assert.Equal(lighters * 3, landed.ArmsUsed + landed.CapacityLeft);
        Assert.True(landed.ArmsUsed <= lighters * 3);
        Assert.True(landed.ArmsUsed + 1 > lighters * 3 - landed.CapacityLeft);
        Assert.All(drowned.Regiments, id => Assert.Null(state.GetRegiment(id)));

        // Sea, then the drowning, then the beach.
        Assert.True(Index(events, battle) < Index(events, drowned));
        Assert.True(Index(events, drowned) < Index(events, landed));
        Assert.Equal(Home, state.GetProvinceOwner(Beach));
    }

    [Fact]
    public void TheInterceptionIsFoughtOncePerSitePerTurn()
    {
        var (_, events, _) = FirstLanding(
            static (landed, _, battle) => battle.Winner == Home && landed is not null,
            secondOrder: true);

        Assert.Single(events.OfType<NavalBattleFoughtEvent>());
    }

    // ---- 9. The interception battle, lost --------------------------------

    [Fact]
    public void ALostInterceptionDrownsEveryoneAndNobodyLands()
    {
        var (state, events, regiments) = FirstLanding(
            static (_, _, battle) => battle.Winner == Other,
            outgunned: true);

        var drowned = Assert.Single(events.OfType<RegimentsDrownedEvent>());
        Assert.Equal(regiments, drowned.Regiments);
        Assert.Empty(events.OfType<RegimentsLandedEvent>());
        Assert.Empty(events.OfType<BattleFoughtEvent>());
        Assert.Empty(events.OfType<ProvinceCapturedEvent>());
        Assert.All(regiments, id => Assert.Null(state.GetRegiment(id)));
        Assert.Equal(Other, state.GetProvinceOwner(Beach));
        Assert.DoesNotContain(state.TaskForces, f => f.Activity == TaskForceActivity.Landing);
    }

    // ---- 10. Arriving intercepts; lying there does not -------------------

    [Fact]
    public void AHostileSailingIntoTheSitesZoneFightsItButOneAlreadyThereDoesNot()
    {
        var arriving = ReadySite(Ship(Home, Lighter, East), Ship(Other, Frigate, West));
        _ = arriving.AssembleTaskForce(Other, [new FleetId(2)]);
        var sailed = Resolve(arriving, 0, Naval(Other, NavalOrder.Command(new TaskForceId(2), NavalMission.MoveTo(East))));
        var battle = Assert.Single(sailed.Events.OfType<NavalBattleFoughtEvent>());
        Assert.Equal((NavalBattleKind.Landing, East, Other, Home), (battle.Kind, battle.SeaZone, battle.Attacker, battle.Defender));
        Assert.True(Index(sailed.Events, sailed.Events.OfType<TaskForceSailedEvent>().Single()) < Index(sailed.Events, battle));

        var waiting = ReadySite(Ship(Home, Lighter, East), Ship(Other, Frigate, East));
        _ = waiting.AssembleTaskForce(Other, [new FleetId(2)]);
        Assert.Empty(Resolve(waiting, 0).Events.OfType<NavalBattleFoughtEvent>());
        Assert.Empty(Resolve(waiting, 0).Events.OfType<NavalBattleFoughtEvent>());
    }

    // ---- 11. A site neither cuts a port nor raids ------------------------

    [Fact]
    public void ALandingSiteCutsNoPortWhereAPatrolDoes()
    {
        var landing = ReadyLakeSite(NavalMission.Landing(Citadel));
        Assert.Equal(TaskForceActivity.Landing, Assert.Single(landing.State.TaskForces).Activity);
        Assert.Equal(0, StrandedPorts(Resolve(landing.State, 0), Other));

        var patrol = ReadyLakeSite(NavalMission.Patrol);
        Assert.Equal(1, StrandedPorts(Resolve(patrol.State, 0), Other));
    }

    [Fact]
    public void ALandingSiteBesideACapitalInterceptsNoMerchant()
    {
        var patrolled = 0;
        foreach (var seed in Enumerable.Range(0, 40).Select(static s => (ulong)s))
        {
            Assert.Empty(Raid(seed, NavalMission.Landing(Beach)).OfType<MerchantInterceptedEvent>());
            patrolled += Raid(seed, NavalMission.Patrol).OfType<MerchantInterceptedEvent>().Count();
        }

        // The control: a patrol in the same zone does intercept on some of those seeds.
        Assert.True(patrolled > 0);
    }

    // ---- 12. Determinism -------------------------------------------------

    [Fact]
    public void TheSameSeedLandsTheSameWay()
    {
        foreach (var seed in new ulong[] { 1, 17, 99 })
        {
            Assert.Equal(Summary(LandingTurn(seed).Events), Summary(LandingTurn(seed).Events));
        }
    }

    // ---- Helpers ---------------------------------------------------------

    /// <summary>
    /// Home holds a site on Beach from East with one Battleship and three Lighters (nine
    /// arms), and lands nine Regulars from Harbour; Other has two Frigates idle in East.
    /// Outgunned, Home has only three Lighters and Other three Longbows.
    /// Returns the first seed's turn whose outcome matches.
    /// </summary>
    private static (WorldState State, IReadOnlyList<TurnEvent> Events, RegimentId[] Regiments) FirstLanding(
        Func<RegimentsLandedEvent?, RegimentsDrownedEvent?, NavalBattleFoughtEvent, bool> wanted,
        bool secondOrder = false,
        bool outgunned = false)
    {
        foreach (var seed in Enumerable.Range(0, 300).Select(static s => (ulong)s))
        {
            var (state, events, regiments) = LandingTurn(seed, secondOrder, outgunned);
            var battle = events.OfType<NavalBattleFoughtEvent>().FirstOrDefault();
            if (battle is not null && wanted(
                    events.OfType<RegimentsLandedEvent>().FirstOrDefault(),
                    events.OfType<RegimentsDrownedEvent>().FirstOrDefault(),
                    battle))
            {
                return (state, events, regiments);
            }
        }

        Assert.Fail("No seed below 300 gave the wanted interception.");
        throw new InvalidOperationException();
    }

    private static (WorldState State, IReadOnlyList<TurnEvent> Events, RegimentId[] Regiments) LandingTurn(
        ulong seed, bool secondOrder = false, bool outgunned = false)
    {
        // Outgunned: three Lighters and no escort against three Longbows.
        var state = ReadySite(
            Ship(Home, outgunned ? Lighter : Battleship, East),
            Ship(Home, Lighter, East, count: outgunned ? 2 : 3),
            Ship(Other, outgunned ? Longbow : Frigate, East, count: outgunned ? 3 : 2));
        _ = state.AssembleTaskForce(Other, [new FleetId(3)]);
        var regiments = Enumerable.Range(0, 9).Select(_ => state.CreateRegiment(Home, Harbour, Type(Regulars))).ToArray();
        RegimentMoveOrder[] moves = secondOrder
            ? [new(regiments[..4], Beach), new(regiments[4..], Beach)]
            : [new(regiments, Beach)];
        var events = Resolve(state, seed, new CountryTurnOrders(Home, regimentMoves: moves)).Events;
        return (state, events, regiments);
    }

    private static IEnumerable<string> Summary(IReadOnlyList<TurnEvent> events) =>
        events.Select(static e => e switch
        {
            NavalBattleFoughtEvent b => $"naval {b.Winner} {b.Rounds} " +
                string.Join(",", b.Participants.Select(static p => $"{p.Fleet}:{p.ShipsSunk}")),
            RegimentsDrownedEvent d => $"drowned {string.Join(",", d.Regiments)}",
            RegimentsLandedEvent l => $"landed {string.Join(",", l.Regiments)} {l.ArmsUsed} {l.CapacityLeft}",
            BattleFoughtEvent b => $"land {b.Winner} {b.Rounds} {string.Join(",", b.Killed)}",
            _ => e.GetType().Name,
        });

    /// <summary>Other buys coal from Third and carries it; Home, at war with Other, lies in East on the mission.</summary>
    private static IReadOnlyList<TurnEvent> Raid(ulong seed, NavalMission mission)
    {
        var state = AtWar(CreateState(Ship(Home, Frigate, East)));
        _ = Resolve(state, 0, Naval(Home, NavalOrder.Assemble([new FleetId(1)], mission)));
        var result = Resolve(
            state,
            seed,
            new CountryTurnOrders(Third, tradeOffers: [new TradeOrder(new CommodityId(Coal), 2)]),
            new CountryTurnOrders(Other, tradeBids: [new TradeOrder(new CommodityId(Coal), 2)]));
        Assert.Single(result.Events.OfType<CommodityTradedEvent>());
        return result.Events;
    }

    private static (WorldState State, TurnResolution Result) ReadyLakeSite(NavalMission mission)
    {
        var state = AtWar(CreateState(Ship(Home, Frigate, Lake)));
        var result = Resolve(state, 0, Naval(Home, NavalOrder.Assemble([new FleetId(1)], mission)));
        return (state, result);
    }

    private static int StrandedPorts(TurnResolution resolution, CountryId country) =>
        resolution.Events.OfType<ResourceExtractedEvent>().Single(e => e.Country == country).StrandedPortCount;

    /// <summary>At war, Home's fleet 1 assembled as task force 1 holding a site on Beach, one turn old.</summary>
    private static WorldState ReadySite(params InitialShip[] ships) => ReadySite(war: true, ships);

    private static WorldState ReadySite(bool war, params InitialShip[] ships)
    {
        var state = CreateState(ships);
        if (war)
        {
            AtWar(state);
        }

        var home = ships.Select(static (ship, index) => (ship, index))
            .Where(static item => item.ship.Country == Home)
            .Select(static item => new FleetId(item.index + 1))
            .ToArray();
        var ordered = Resolve(state, 0, Naval(Home, NavalOrder.Assemble(home, NavalMission.Landing(Beach))));
        Assert.Single(ordered.Events.OfType<TaskForceLandingOrderedEvent>());
        return state;
    }

    private static WorldState AtWar(WorldState state)
    {
        state.SetRelationMode(Home, Other, CountryRelationMode.Hostile);
        _ = Resolve(state, 0);
        Assert.True(state.HasEffectiveHostility(Home, Other));
        return state;
    }

    private static int Index(IReadOnlyList<TurnEvent> events, TurnEvent wanted) =>
        events.Select(static (e, i) => (e, i)).Single(item => ReferenceEquals(item.e, wanted)).i;

    private static void AssertRefused(IReadOnlyList<TurnEvent> events, RegimentMoveRefusal reason)
    {
        var refused = Assert.Single(events.OfType<RegimentMoveRefusedEvent>());
        Assert.Equal((reason, TurnPhase.Conflict), (refused.Reason, refused.Phase));
    }

    private static RegimentMoveRefusal[] Refusals(IReadOnlyList<TurnEvent> events) =>
        events.OfType<RegimentMoveRefusedEvent>().Select(static e => e.Reason).ToArray();

    private static ArmyTypeId Type(int value) => new(value);

    private static CountryTurnOrders Naval(CountryId country, params NavalOrder[] orders) => new(country, naval: orders);

    private static CountryTurnOrders Move(CountryId country, ProvinceId destination, params RegimentId[] regiments) =>
        new(country, regimentMoves: [new(regiments, destination)]);

    private static TurnResolution Resolve(WorldState state, ulong seed, params CountryTurnOrders[] orders) =>
        TurnResolver.Resolve(
            state,
            new TurnOrders(Enumerable.Range(0, state.Definition.Countries.Count)
                .Select(index => orders.FirstOrDefault(order => order.Country.Value == index)
                    ?? new CountryTurnOrders(new CountryId(index)))),
            seed);

    private static InitialShip Ship(CountryId country, int type, SeaZoneId zone, long count = 1) =>
        new(country, new ShipTypeId(type), 0, count, zone);

    /// <summary>
    /// The coast described on the class. Placed ships come first, so they take fleet ids
    /// from 1; Other and Third each have three unplaced Traders for the market.
    /// </summary>
    private static WorldState CreateState(params InitialShip[] ships)
    {
        const int width = 10;
        var dimensions = new MapDimensions(width, 1);
        CellRegion[] regions =
        [
            CellRegion.ForProvince(Inland),
            CellRegion.ForProvince(Harbour),
            CellRegion.ForSeaZone(West),
            CellRegion.ForSeaZone(East),
            CellRegion.ForProvince(Beach),
            CellRegion.ForProvince(Border),
            CellRegion.ForProvince(Citadel),
            CellRegion.ForSeaZone(Lake),
            CellRegion.ForProvince(Islet),
            CellRegion.ForProvince(new ProvinceId(6)),
        ];
        var cells = Enumerable.Range(0, width)
            .Select(index => new CellDefinition(
                new CellIndex(index),
                dimensions.GetCoordinate(new CellIndex(index)),
                new TerrainId(0),
                regions[index],
                null,
                index is 0 or 4 or 9 ? SettlementSiteKind.Urban : SettlementSiteKind.None,
                null))
            .ToArray();
        var map = new MapDefinition(
            dimensions,
            cells,
            [
                new ProvinceDefinition(Inland, "Inland"),
                new ProvinceDefinition(Harbour, "Harbour"),
                new ProvinceDefinition(Beach, "Beach"),
                new ProvinceDefinition(Border, "Border"),
                new ProvinceDefinition(Citadel, "Citadel"),
                new ProvinceDefinition(Islet, "Islet"),
                new ProvinceDefinition(new ProvinceId(6), "Freeport"),
            ],
            [
                new SeaZoneDefinition(West, "West"),
                new SeaZoneDefinition(East, "East"),
                new SeaZoneDefinition(Lake, "Lake"),
            ],
            [new ResourceDefinition(new ResourceId(0), new CommodityId(Fish), [1])]);

        var scenario = new ScenarioDefinition(
            "Landings",
            1840,
            [Home, Home, Other, Home, Other, Home, Third],
            initialCountryCapitals:
            [
                new CountryCapital(Home, new CellIndex(0)),
                new CountryCapital(Other, new CellIndex(4)),
                new CountryCapital(Third, new CellIndex(9)),
            ],
            initialInventory: [new InitialCommodityStock(Third, new CommodityId(Coal), 100)],
            initialPorts: [new CellIndex(6)],
            initialCash: [new InitialCash(Home, 10_000), new InitialCash(Other, 10_000), new InitialCash(Third, 10_000)],
            initialShips:
            [
                .. ships,
                new InitialShip(Other, new ShipTypeId(Trader), 0, 3),
                new InitialShip(Third, new ShipTypeId(Trader), 0, 3),
            ]);

        static ArmyCombatStats Stats(decimal firepower, long defence, long? entrenched) =>
            new(firepower, firepower, 5, null, defence, entrenched, 4);

        static ArmyPurchase Bill(long arms, long horses = 0) => new(
            WorkerGrade.Untrained,
            horses == 0
                ? [new CommodityQuantity(new CommodityId(Armaments), arms)]
                : [new CommodityQuantity(new CommodityId(Armaments), arms), new CommodityQuantity(new CommodityId(Horses), horses)],
            500);

        static IEnumerable<CommodityQuantity> Arms(long arms) => [new CommodityQuantity(new CommodityId(Armaments), arms)];

        return new WorldState(new WorldDefinition(
            map,
            [
                new CountryDefinition(Home, "Home", isGreatPower: true),
                new CountryDefinition(Other, "Other", isGreatPower: true),
                new CountryDefinition(Third, "Third", isGreatPower: true),
            ],
            scenario,
            [
                new CommodityDefinition(new CommodityId(Armaments), "Armaments", CommodityCategory.Goods),
                new CommodityDefinition(new CommodityId(Horses), "Horses", CommodityCategory.Raw),
                new CommodityDefinition(new CommodityId(Coal), "Coal", CommodityCategory.Raw, worldPrice: 100, tradeOrder: 13),
                new CommodityDefinition(new CommodityId(Fish), "Fish", CommodityCategory.Raw),
            ],
            extraction: new ExtractionSettings(1, new PortFishing(new CommodityId(Fish), 1)),
            shipTypes:
            [
                new ShipTypeDefinition(new ShipTypeId(Trader), "Trader", cargo: 2, seaZones: 3, combat: new ShipCombatStats(0, 0, 0, 600, 0)),
                new ShipTypeDefinition(new ShipTypeId(Frigate), "Frigate", seaZones: 3, buildCost: Arms(2), combat: new ShipCombatStats(3, 5, 10, 900, 4, 35)),
                new ShipTypeDefinition(new ShipTypeId(Lighter), "Lighter", seaZones: 3, buildCost: Arms(3), combat: new ShipCombatStats(0, 0, 0, 600, 0)),
                new ShipTypeDefinition(new ShipTypeId(Longbow), "Longbow", seaZones: 3, combat: new ShipCombatStats(50, 10, 0, 600, 1)),
                new ShipTypeDefinition(new ShipTypeId(Hulk), "Hulk", seaZones: 3, buildCost: Arms(3)),
                new ShipTypeDefinition(new ShipTypeId(Battleship), "Battleship", seaZones: 3, combat: new ShipCombatStats(10, 8, 50, 2800, 3)),
            ],
            trade: new ProportionalTradeMarket(),
            armyTypes:
            [
                new(Type(Minutemen), "Minutemen", ArmyCategory.Militia, 1,
                    movesByRail: false, leavesProvince: false, stats: Stats(5, 4, 5)),
                new(Type(Regulars), "Regulars", ArmyCategory.RegularInfantry, 1, purchase: Bill(1), arms: 1, stats: Stats(10, 5, 6)),
                new(Type(Cannons), "Cannons", ArmyCategory.LightArtillery, 1, purchase: Bill(2, horses: 2), arms: 2, stats: Stats(10, 5, null)),
                new(Type(Bulwark), "Bulwark", ArmyCategory.HeavyInfantry, 1, stats: Stats(0, 1, 100)),
                new(Type(General), "General", ArmyCategory.General, 1),
            ]));
    }
}
