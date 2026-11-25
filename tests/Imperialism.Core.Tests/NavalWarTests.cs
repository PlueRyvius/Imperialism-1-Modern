using Imperialism.Core;
using Xunit;

namespace Imperialism.Core.Tests;

/// <summary>
/// Row 6c: naval battles, the blockade, interception and escorts, on a 7x1 strip.
/// </summary>
/// <remarks>
/// Cells, west to east: 0 Albion's port ("Shore"), 1 sea zone "Albion Waters",
/// 2 Albion's capital, 3 sea zone "Mid", 4 sea zone "Bordurian Waters", 5 Borduria's
/// capital, 6 Corsica's capital, inland. So Albion's capital touches Albion Waters and
/// Mid, its port touches Albion Waters only, and Borduria's capital touches Bordurian
/// Waters only. All three are Great Powers; between two of them the buyer carries.
/// <para>
/// The draws come from <c>SplitMix64</c> seeded by the turn seed, and Core exposes no
/// way to substitute a stub generator, so a test that needs a given outcome searches the
/// turn seeds for one. See <c>docs/formulas/naval.md</c>.
/// </para>
/// </remarks>
public sealed class NavalWarTests
{
    private static readonly CountryId Albion = new(0);
    private static readonly CountryId Borduria = new(1);
    private static readonly CountryId Corsica = new(2);
    private static readonly SeaZoneId AlbionWaters = new(0);
    private static readonly SeaZoneId Mid = new(1);
    private static readonly SeaZoneId BordurianWaters = new(2);

    private const int Trader = 0;
    private const int Frigate = 1;
    private const int Battleship = 2;
    private const int Short = 3;
    private const int Longbow = 4;
    private const int Hulk = 5;

    private const int Coal = 0;
    private const int Steel = 1;
    private const int Fish = 2;

    // ---- The blockade order ---------------------------------------------

    [Fact]
    public void ABlockadeIsAcceptedAgainstACapitalItsZoneTouchesAndAMoveClearsIt()
    {
        var state = CreateState(Ship(Corsica, Frigate, BordurianWaters));
        var ordered = Resolve(state, 0, new CountryTurnOrders(
            Corsica, naval: [NavalOrder.Assemble([new FleetId(1)], NavalMission.Blockade(Borduria))]));

        var blockade = Assert.Single(ordered.Events.OfType<TaskForceBlockadeOrderedEvent>());
        Assert.Equal((Corsica, new TaskForceId(1), BordurianWaters, Borduria),
            (blockade.Country, blockade.TaskForce, blockade.SeaZone, blockade.Target));
        var force = state.GetTaskForce(new TaskForceId(1));
        Assert.Equal((TaskForceActivity.Blockading, (CountryId?)Borduria), (force.Activity, force.BlockadeTarget));

        // It persists with no orders, like a patrol.
        _ = Resolve(state, 0);
        Assert.Equal(TaskForceActivity.Blockading, force.Activity);

        // A second mission the same turn is refused: a blockade spends the turn.
        var twice = Resolve(state, 0, new CountryTurnOrders(
            Corsica,
            naval:
            [
                NavalOrder.Command(new TaskForceId(1), NavalMission.Blockade(Borduria)),
                NavalOrder.Command(new TaskForceId(1), NavalMission.Patrol),
            ]));
        Assert.Equal(NavalOrderRefusal.AlreadyOrderedThisTurn,
            Assert.Single(twice.Events.OfType<NavalOrderRefusedEvent>()).Reason);

        _ = Resolve(state, 0, new CountryTurnOrders(
            Corsica, naval: [NavalOrder.Command(new TaskForceId(1), NavalMission.MoveTo(Mid))]));
        Assert.Equal((TaskForceActivity.Idle, (CountryId?)null, Mid), (force.Activity, force.BlockadeTarget, force.SeaZone));
    }

    [Fact]
    public void EveryInvalidBlockadeIsRefusedWithItsReasonAndChangesNothing()
    {
        var state = CreateState(Ship(Corsica, Frigate, Mid), Ship(Borduria, Frigate, BordurianWaters));
        _ = state.AssembleTaskForce(Borduria, [new FleetId(2)]);
        state.SetCountryCapital(Borduria, null);

        var result = Resolve(
            state,
            0,
            new CountryTurnOrders(
                Corsica,
                naval:
                [
                    NavalOrder.Assemble([new FleetId(1)], NavalMission.Blockade(new CountryId(9))),
                    NavalOrder.Assemble([new FleetId(1)], NavalMission.Blockade(Corsica)),
                    NavalOrder.Assemble([new FleetId(1)], NavalMission.Blockade(Borduria)),
                ]),
            new CountryTurnOrders(
                Borduria,
                naval: [NavalOrder.Command(new TaskForceId(1), NavalMission.Blockade(Albion))]));

        Assert.Equal(
            [
                // Countries in id order. Albion's capital touches Albion Waters and Mid,
                // never Bordurian Waters.
                (Borduria, NavalOrderRefusal.CapitalNotOnThisSeaZone),
                (Corsica, NavalOrderRefusal.NoSuchCountry),
                (Corsica, NavalOrderRefusal.BlockadeOwnCountry),
                (Corsica, NavalOrderRefusal.TargetHasNoCapital),
            ],
            result.Events.OfType<NavalOrderRefusedEvent>().Select(static e => (e.Country, e.Reason)));
        Assert.Single(state.TaskForces);
        Assert.Equal(TaskForceActivity.Idle, state.GetTaskForce(new TaskForceId(1)).Activity);
        Assert.Null(state.GetFleet(new FleetId(1)).TaskForce);

        // From Mid, Albion's capital is in reach.
        var accepted = Resolve(state, 0, new CountryTurnOrders(
            Corsica, naval: [NavalOrder.Assemble([new FleetId(1)], NavalMission.Blockade(Albion))]));
        Assert.Single(accepted.Events.OfType<TaskForceBlockadeOrderedEvent>());
        Assert.Empty(accepted.Events.OfType<NavalOrderRefusedEvent>());
    }

    [Fact]
    public void ABlockadeDoesNotCutAPortButAPatrolDoes()
    {
        var state = CreateState(Ship(Corsica, Frigate, AlbionWaters));
        AtWar(state, Corsica, Albion);

        // The executable's state 6 does not qualify for the port predicate.
        var blockading = Resolve(state, 0, new CountryTurnOrders(
            Corsica, naval: [NavalOrder.Assemble([new FleetId(1)], NavalMission.Blockade(Albion))]));
        Assert.Equal(TaskForceActivity.Blockading, state.GetTaskForce(new TaskForceId(1)).Activity);
        Assert.Equal(0, Stranded(blockading));

        var patrolling = Resolve(state, 0, new CountryTurnOrders(
            Corsica, naval: [NavalOrder.Command(new TaskForceId(1), NavalMission.Patrol)]));
        Assert.Equal(1, Stranded(patrolling));
    }

    // ---- Who fights whom ------------------------------------------------

    [Fact]
    public void TwoHostilePatrolsInOneZoneFightAndTheLowerIdAttacks()
    {
        var battle = Assert.Single(Encounter(NavalMission.Patrol, NavalMission.Patrol, war: true));
        Assert.Equal((Borduria, Corsica, Mid, NavalBattleKind.Patrol),
            (battle.Attacker, battle.Defender, battle.SeaZone, battle.Kind));
    }

    [Fact]
    public void APatrolFindsABlockadeAndAttacksIt()
    {
        var battle = Assert.Single(Encounter(NavalMission.Blockade(Albion), NavalMission.Patrol, war: true));
        Assert.Equal((Corsica, Borduria), (battle.Attacker, battle.Defender));
    }

    [Fact]
    public void APatrolFindsAnIdleTaskForce()
    {
        var battle = Assert.Single(Encounter(NavalMission.None, NavalMission.Patrol, war: true));
        Assert.Equal((Corsica, Borduria), (battle.Attacker, battle.Defender));
    }

    [Fact]
    public void TwoBlockadesDoNotSeekEachOther() =>
        Assert.Empty(Encounter(NavalMission.Blockade(Albion), NavalMission.Blockade(Albion), war: true));

    [Fact]
    public void TwoIdleTaskForcesDoNotSeekEachOther() =>
        Assert.Empty(Encounter(NavalMission.None, NavalMission.None, war: true));

    [Fact]
    public void ANonHostilePairDoesNotFight() =>
        Assert.Empty(Encounter(NavalMission.Patrol, NavalMission.Patrol, war: false));

    [Fact]
    public void AnAnchoredFleetIsNotEngagedByAPatrol()
    {
        var state = CreateState(Ship(Borduria, Frigate, Mid), Ship(Corsica, Frigate, Mid));
        AtWar(state, Borduria, Corsica);
        var result = Resolve(state, 0, new CountryTurnOrders(
            Corsica, naval: [NavalOrder.Assemble([new FleetId(2)], NavalMission.Patrol)]));
        Assert.Empty(result.Events.OfType<NavalBattleFoughtEvent>());
        Assert.Equal(1, state.GetFleet(new FleetId(1)).Count);
    }

    // ---- The resolver ---------------------------------------------------

    [Fact]
    public void InTheOpeningRoundOnlyTheLongerRangeFiresSoTheOutrangedSideTakesNothingBack()
    {
        // Equal guns and hulls; the patrol attacks and would fire first, but its range
        // is 1 against 10. The Longbow's opening shot sinks it before it ever fires.
        var state = CreateState(Ship(Borduria, Longbow, Mid), Ship(Corsica, Short, Mid));
        AtWar(state, Borduria, Corsica);
        _ = state.AssembleTaskForce(Borduria, [new FleetId(1)]);
        var result = Resolve(state, 0, new CountryTurnOrders(
            Corsica, naval: [NavalOrder.Assemble([new FleetId(2)], NavalMission.Patrol)]));

        var battle = Assert.Single(result.Events.OfType<NavalBattleFoughtEvent>());
        Assert.Equal((Corsica, Borduria, 1), (battle.Attacker, battle.Winner, battle.Rounds));
        Assert.Equal(
            [
                new NavalBattleParticipant(new FleetId(2), new ShipTypeId(Short), BattleSide.Attacker, 1, 1),
                new NavalBattleParticipant(new FleetId(1), new ShipTypeId(Longbow), BattleSide.Defender, 1, 0),
            ],
            battle.Participants);
        Assert.Equal(1, state.GetFleet(new FleetId(1)).Count);
        Assert.False(state.TryGetFleet(new FleetId(2), out _));
    }

    [Fact]
    public void TheSameSeedFightsTheSameBattleAndADifferentSeedCanDiffer()
    {
        static string Fight(ulong seed)
        {
            var state = CreateState(Ship(Borduria, Frigate, Mid, 3), Ship(Corsica, Frigate, Mid, 3));
            AtWar(state, Borduria, Corsica);
            var result = Resolve(
                state,
                seed,
                new CountryTurnOrders(Borduria, naval: [NavalOrder.Assemble([new FleetId(1)], NavalMission.Patrol)]),
                new CountryTurnOrders(Corsica, naval: [NavalOrder.Assemble([new FleetId(2)], NavalMission.Patrol)]));
            var battle = result.Events.OfType<NavalBattleFoughtEvent>().Single();
            return $"{battle.Winner} {battle.Rounds} " +
                string.Join(',', battle.Participants.Select(static p => $"{p.Fleet.Value}:{p.ShipsSunk}"));
        }

        Assert.Equal(Fight(7), Fight(7));
        Assert.Contains(Enumerable.Range(1, 40), seed => Fight((ulong)seed) != Fight(0));
    }

    [Fact]
    public void SinkingTheLastShipRemovesTheFleetAndDissolvesTheTaskForceWithoutReusingIds()
    {
        var state = CreateState(
            Ship(Borduria, Frigate, Mid), Ship(Corsica, Battleship, Mid), Ship(Corsica, Frigate, Mid));
        AtWar(state, Borduria, Corsica);
        _ = state.AssembleTaskForce(Borduria, [new FleetId(1)]);
        Assert.Equal(1, state.GetShipCount(Borduria, new ShipTypeId(Frigate)));

        var result = Resolve(state, 0, new CountryTurnOrders(
            Corsica, naval: [NavalOrder.Assemble([new FleetId(2)], NavalMission.Patrol)]));
        Assert.Equal(Corsica, Assert.Single(result.Events.OfType<NavalBattleFoughtEvent>()).Winner);

        Assert.False(state.TryGetFleet(new FleetId(1), out _));
        Assert.False(state.TryGetTaskForce(new TaskForceId(1), out _));
        Assert.Throws<ArgumentOutOfRangeException>(() => state.GetFleet(new FleetId(1)));
        Assert.Equal([2, 3, 4, 5], state.Fleets.Select(static f => f.Id.Value));
        Assert.Equal([new TaskForceId(2)], state.TaskForces.Select(static f => f.Id));
        Assert.Equal(0, state.GetShipCount(Borduria, new ShipTypeId(Frigate)));

        // The winner keeps its mission; a new task force takes a new id.
        Assert.Equal(TaskForceActivity.Patrolling, state.GetTaskForce(new TaskForceId(2)).Activity);
        Assert.Equal(new TaskForceId(3), state.AssembleTaskForce(Corsica, [new FleetId(3)]).Id);

        // A command to the dissolved task force is refused, never thrown.
        var refused = Resolve(state, 0, new CountryTurnOrders(
            Borduria, naval: [NavalOrder.Command(new TaskForceId(1), NavalMission.Patrol)]));
        Assert.Equal(NavalOrderRefusal.NoSuchTaskForce,
            Assert.Single(refused.Events.OfType<NavalOrderRefusedEvent>()).Reason);
    }

    [Fact]
    public void ABeatenPatrolGoesIdleAndThePortItCutIsConnectedTheSameTurn()
    {
        // Corsica's frigate and a hulk patrol beside Albion's port. Albion's battleship
        // lies anchored there, which a patrol does not engage, so the port is cut.
        var state = CreateState(
            Ship(Corsica, Frigate, AlbionWaters), Ship(Corsica, Hulk, AlbionWaters), Ship(Albion, Battleship, AlbionWaters));
        AtWar(state, Albion, Corsica);
        var cut = Resolve(state, 0, new CountryTurnOrders(
            Corsica, naval: [NavalOrder.Assemble([new FleetId(1), new FleetId(2)], NavalMission.Patrol)]));
        Assert.Empty(cut.Events.OfType<NavalBattleFoughtEvent>());
        Assert.Equal(1, Stranded(cut));

        // Assembled with no mission it is an idle task force, which a patrol does find.
        var broken = Resolve(state, 0, new CountryTurnOrders(
            Albion, naval: [NavalOrder.Assemble([new FleetId(3)], NavalMission.None)]));
        var battle = Assert.Single(broken.Events.OfType<NavalBattleFoughtEvent>());
        Assert.Equal((Corsica, Albion), (battle.Attacker, battle.Winner));

        // The hulk has no combat stats, stands aside and survives; the task force is idle.
        Assert.DoesNotContain(battle.Participants, static p => p.Fleet == new FleetId(2));
        var survivor = state.GetTaskForce(new TaskForceId(1));
        Assert.Equal((TaskForceActivity.Idle, new FleetId(2)), (survivor.Activity, Assert.Single(survivor.Fleets)));
        Assert.Equal(0, Stranded(broken));
    }

    // ---- Interception ---------------------------------------------------

    [Fact]
    public void InterceptionNeedsTheCarrierAtWarWithTheInterceptor()
    {
        // Albion sells to Borduria and Borduria, the buyer, carries.
        var atWarWithCarrier = Enumerable.Range(0, 40)
            .Count(seed => Interception((ulong)seed, NavalMission.Patrol, enemyOf: Borduria).Intercepted is not null);
        Assert.InRange(atWarWithCarrier, 1, 39);

        // At war with the seller only: the cargo is Borduria's to carry, and untouched.
        Assert.All(Enumerable.Range(0, 40), seed =>
        {
            var (_, result, intercepted) = Interception((ulong)seed, NavalMission.Patrol, enemyOf: Albion);
            Assert.Null(intercepted);
            Assert.Single(result.Events.OfType<CommodityTradedEvent>());
        });

        // No war at all.
        Assert.All(Enumerable.Range(0, 40), seed =>
            Assert.Null(Interception((ulong)seed, NavalMission.Patrol, enemyOf: null).Intercepted));
    }

    [Fact]
    public void ABlockadeInterceptsMoreOftenThanAPatrol()
    {
        var patrol = Enumerable.Range(0, 200)
            .Count(seed => Interception((ulong)seed, NavalMission.Patrol, Borduria).Intercepted is not null);
        var blockade = Enumerable.Range(0, 200)
            .Count(seed => Interception((ulong)seed, NavalMission.Blockade(Borduria), Borduria).Intercepted is not null);
        Assert.True(blockade > patrol, $"blockade {blockade}, patrol {patrol}");
        Assert.InRange(patrol, 30, 70);
        Assert.InRange(blockade, 80, 120);
    }

    [Fact]
    public void ATurnedBackCargoReturnsToTheSellerAndTheMoneyStands()
    {
        var (state, _, intercepted) = FirstOutcome(InterceptionOutcome.TurnedBack);
        Assert.Equal((Corsica, Borduria, Albion, Borduria, TaskForceActivity.Blockading),
            (intercepted.Interceptor, intercepted.Carrier, intercepted.Seller, intercepted.Buyer, intercepted.Mission));
        Assert.Equal(100, Available(state, Albion, Coal));
        Assert.Equal(0, Available(state, Borduria, Coal));
        Assert.Equal(0, Available(state, Corsica, Coal));
        AssertCashSettled(state);
        Assert.Equal(3, Traders(state, Borduria));
    }

    [Fact]
    public void ACapturedCargoGoesToTheInterceptor()
    {
        var (state, _, _) = FirstOutcome(InterceptionOutcome.Captured);
        Assert.Equal(98, Available(state, Albion, Coal));
        Assert.Equal(0, Available(state, Borduria, Coal));
        Assert.Equal(2, Available(state, Corsica, Coal));
        AssertCashSettled(state);
        Assert.Equal(3, Traders(state, Borduria));
    }

    [Fact]
    public void ASunkMerchantLosesTheCargoAndOneHull()
    {
        var (state, _, intercepted) = FirstOutcome(InterceptionOutcome.Sunk);
        Assert.Equal(new ShipTypeId(Trader), intercepted.HullSunk);
        Assert.Equal(98, Available(state, Albion, Coal));
        Assert.Equal(0, Available(state, Borduria, Coal));
        Assert.Equal(0, Available(state, Corsica, Coal));
        AssertCashSettled(state);
        Assert.Equal(2, Traders(state, Borduria));
        Assert.Equal(2, state.GetShipCount(Borduria, new ShipTypeId(Trader)));
    }

    [Fact]
    public void AnUninterceptedDealIsDeliveredAndATurnWithNoWarHasNoNavalEvents()
    {
        var (state, result, intercepted) = Interception(0, NavalMission.Patrol, enemyOf: null);
        Assert.Null(intercepted);
        Assert.DoesNotContain(result.Events, static e => e is NavalBattleFoughtEvent or MerchantInterceptedEvent);
        Assert.DoesNotContain(result.Events, static e => e.Phase == TurnPhase.TradeCancellation && e is not TurnPhaseCompletedEvent);
        Assert.Equal(2, Available(state, Borduria, Coal));
        AssertCashSettled(state);
    }

    // ---- Escorts --------------------------------------------------------

    [Fact]
    public void EscortsFightOncePerTaskForceAndCarrierEvenAcrossSeveralDeals()
    {
        // Two deals, both carried by Borduria; its frigate lies anchored by its capital.
        var (_, result) = EscortTurn(0, Battleship, escortZone: BordurianWaters, escortAssembled: false);
        Assert.Equal(2, result.Events.OfType<CommodityTradedEvent>().Count());
        var escort = Assert.Single(result.Events.OfType<NavalBattleFoughtEvent>());
        Assert.Equal((NavalBattleKind.Escort, TurnPhase.TradeCancellation, Corsica, Borduria, Corsica),
            (escort.Kind, escort.Phase, escort.Attacker, escort.Defender, escort.Winner));
        Assert.Contains(escort.Participants, static p => p.Side == BattleSide.Defender && p.ShipsSunk == 1);
    }

    [Fact]
    public void ABeatenInterceptorGoesIdleAndInterceptsNothingMore()
    {
        Assert.All(Enumerable.Range(0, 20), seed =>
        {
            var (state, result) = EscortTurn((ulong)seed, Frigate, escortZone: BordurianWaters, escortAssembled: false, escortType: Battleship);
            var escort = Assert.Single(result.Events.OfType<NavalBattleFoughtEvent>());
            Assert.Equal(Borduria, escort.Winner);
            Assert.Empty(result.Events.OfType<MerchantInterceptedEvent>());
            Assert.Equal(TaskForceActivity.Idle, state.GetTaskForce(new TaskForceId(1)).Activity);
            Assert.Equal(4, Available(state, Borduria, Coal) + Available(state, Borduria, Steel));
        });
    }

    [Fact]
    public void EscortsAwayFromTheCapitalOrInATaskForceDoNotEscort()
    {
        var (_, away) = EscortTurn(0, Battleship, escortZone: Mid, escortAssembled: false);
        Assert.Empty(away.Events.OfType<NavalBattleFoughtEvent>());

        // Assembled beside the capital it is an idle task force, which the patrol finds
        // in Conflict — a patrol battle, not an escort.
        var (_, assembled) = EscortTurn(0, Battleship, escortZone: BordurianWaters, escortAssembled: true);
        Assert.Equal(NavalBattleKind.Patrol, Assert.Single(assembled.Events.OfType<NavalBattleFoughtEvent>()).Kind);
    }

    // ---- Helpers --------------------------------------------------------

    private static IReadOnlyList<NavalBattleFoughtEvent> Encounter(NavalMission borduria, NavalMission corsica, bool war)
    {
        var state = CreateState(Ship(Borduria, Frigate, Mid), Ship(Corsica, Frigate, Mid));
        if (war)
        {
            AtWar(state, Borduria, Corsica);
        }

        var result = Resolve(
            state,
            0,
            new CountryTurnOrders(Borduria, naval: [NavalOrder.Assemble([new FleetId(1)], borduria)]),
            new CountryTurnOrders(Corsica, naval: [NavalOrder.Assemble([new FleetId(2)], corsica)]));
        Assert.Equal(2, state.TaskForces.Count + result.Events.OfType<NavalBattleFoughtEvent>()
            .Sum(static b => b.Participants.Count(static p => p.ShipsSunk == p.ShipsBefore)));
        return result.Events.OfType<NavalBattleFoughtEvent>().ToArray();
    }

    /// <summary>
    /// Corsica's frigate lies in Bordurian Waters on <paramref name="mission"/>, at war
    /// with <paramref name="enemyOf"/>; Albion then sells Borduria 2 coal under the given
    /// turn seed.
    /// </summary>
    private static (WorldState State, TurnResolution Result, MerchantInterceptedEvent? Intercepted) Interception(
        ulong seed, NavalMission mission, CountryId? enemyOf)
    {
        var state = CreateState(Ship(Corsica, Frigate, BordurianWaters));
        if (enemyOf is { } enemy)
        {
            AtWar(state, Corsica, enemy);
        }

        _ = Resolve(state, 0, new CountryTurnOrders(
            Corsica, naval: [NavalOrder.Assemble([new FleetId(1)], mission)]));
        var result = Resolve(state, seed, Offer(Albion, (Coal, 2)), Bid(Borduria, (Coal, 2)));
        Assert.Single(result.Events.OfType<CommodityTradedEvent>());
        return (state, result, result.Events.OfType<MerchantInterceptedEvent>().SingleOrDefault());
    }

    private static (WorldState State, TurnResolution Result, MerchantInterceptedEvent Intercepted) FirstOutcome(
        InterceptionOutcome outcome)
    {
        foreach (var seed in Enumerable.Range(0, 200))
        {
            var run = Interception((ulong)seed, NavalMission.Blockade(Borduria), Borduria);
            if (run.Intercepted is { } intercepted && intercepted.Outcome == outcome)
            {
                return (run.State, run.Result, intercepted);
            }
        }

        Assert.Fail($"No seed below 200 gave {outcome}.");
        throw new InvalidOperationException();
    }

    /// <summary>
    /// Corsica patrols Bordurian Waters at war with Borduria, which carries two deals
    /// from Albion and has one escort ship of <paramref name="escortType"/>.
    /// </summary>
    private static (WorldState State, TurnResolution Result) EscortTurn(
        ulong seed, int raiderType, SeaZoneId escortZone, bool escortAssembled, int escortType = Frigate)
    {
        var state = CreateState(
            Ship(Corsica, raiderType, BordurianWaters),
            Ship(Corsica, Hulk, BordurianWaters),
            Ship(Borduria, escortType, escortZone));
        AtWar(state, Corsica, Borduria);
        _ = Resolve(state, 0, new CountryTurnOrders(
            Corsica, naval: [NavalOrder.Assemble([new FleetId(1), new FleetId(2)], NavalMission.Patrol)]));
        if (escortAssembled)
        {
            _ = state.AssembleTaskForce(Borduria, [new FleetId(3)]);
        }

        var result = Resolve(state, seed, Offer(Albion, (Coal, 2), (Steel, 2)), Bid(Borduria, (Coal, 2), (Steel, 2)));
        return (state, result);
    }

    private static void AssertCashSettled(WorldState state)
    {
        Assert.Equal(10_000 + 200, state.GetCash(Albion));
        Assert.Equal(10_000 - 200, state.GetCash(Borduria));
    }

    private static long Traders(WorldState state, CountryId country) =>
        state.Fleets.Where(f => f.Country == country && f.Type == new ShipTypeId(Trader)).Sum(static f => f.Count);

    private static long Available(WorldState state, CountryId country, int commodity) =>
        state.GetAvailableQuantity(country, new CommodityId(commodity));

    private static int Stranded(TurnResolution resolution) =>
        resolution.Events.OfType<ResourceExtractedEvent>().Single(static e => e.Country == Albion).StrandedPortCount;

    private static void AtWar(WorldState state, CountryId first, CountryId second)
    {
        state.SetRelationMode(first, second, CountryRelationMode.Hostile);
        _ = Resolve(state, 0);
        Assert.True(state.HasEffectiveHostility(first, second));
    }

    private static CountryTurnOrders Offer(CountryId country, params (int Commodity, long Quantity)[] goods) =>
        new(country, tradeOffers: goods.Select(static g => new TradeOrder(new CommodityId(g.Commodity), g.Quantity)));

    private static CountryTurnOrders Bid(CountryId country, params (int Commodity, long Quantity)[] goods) =>
        new(country, tradeBids: goods.Select(static g => new TradeOrder(new CommodityId(g.Commodity), g.Quantity)));

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
    /// The strip described on the class, with the given placed ships first (so they take
    /// fleet ids from 1) and three unplaced Traders each for Albion and Borduria.
    /// </summary>
    private static WorldState CreateState(params InitialShip[] ships)
    {
        const int width = 7;
        var dimensions = new MapDimensions(width, 1);
        CellRegion[] regions =
        [
            CellRegion.ForProvince(new ProvinceId(0)),
            CellRegion.ForSeaZone(AlbionWaters),
            CellRegion.ForProvince(new ProvinceId(1)),
            CellRegion.ForSeaZone(Mid),
            CellRegion.ForSeaZone(BordurianWaters),
            CellRegion.ForProvince(new ProvinceId(2)),
            CellRegion.ForProvince(new ProvinceId(3)),
        ];
        var cells = Enumerable.Range(0, width)
            .Select(index => new CellDefinition(
                new CellIndex(index),
                dimensions.GetCoordinate(new CellIndex(index)),
                new TerrainId(0),
                regions[index],
                null,
                index is 2 or 5 or 6 ? SettlementSiteKind.Urban : SettlementSiteKind.None,
                null))
            .ToArray();
        var map = new MapDefinition(
            dimensions,
            cells,
            [
                new ProvinceDefinition(new ProvinceId(0), "Shore"),
                new ProvinceDefinition(new ProvinceId(1), "Albion"),
                new ProvinceDefinition(new ProvinceId(2), "Borduria"),
                new ProvinceDefinition(new ProvinceId(3), "Corsica"),
            ],
            [
                new SeaZoneDefinition(AlbionWaters, "Albion Waters"),
                new SeaZoneDefinition(Mid, "Mid"),
                new SeaZoneDefinition(BordurianWaters, "Bordurian Waters"),
            ],
            [new ResourceDefinition(new ResourceId(0), new CommodityId(Fish), [1])]);

        var scenario = new ScenarioDefinition(
            "Naval war",
            1840,
            [Albion, Albion, Borduria, Corsica],
            initialCountryCapitals:
            [
                new CountryCapital(Albion, new CellIndex(2)),
                new CountryCapital(Borduria, new CellIndex(5)),
                new CountryCapital(Corsica, new CellIndex(6)),
            ],
            initialInventory:
            [
                new InitialCommodityStock(Albion, new CommodityId(Coal), 100),
                new InitialCommodityStock(Albion, new CommodityId(Steel), 100),
            ],
            initialPorts: [new CellIndex(0)],
            initialCash: [new InitialCash(Albion, 10_000), new InitialCash(Borduria, 10_000), new InitialCash(Corsica, 10_000)],
            initialShips:
            [
                .. ships,
                new InitialShip(Albion, new ShipTypeId(Trader), 0, 3),
                new InitialShip(Borduria, new ShipTypeId(Trader), 0, 3),
            ]);

        return new WorldState(new WorldDefinition(
            map,
            [
                new CountryDefinition(Albion, "Albion", isGreatPower: true),
                new CountryDefinition(Borduria, "Borduria", isGreatPower: true),
                new CountryDefinition(Corsica, "Corsica", isGreatPower: true),
            ],
            scenario,
            [
                new CommodityDefinition(new CommodityId(Coal), "Coal", CommodityCategory.Raw, worldPrice: 100, tradeOrder: 13),
                new CommodityDefinition(new CommodityId(Steel), "Steel", CommodityCategory.Material, worldPrice: 300, tradeOrder: 9),
                new CommodityDefinition(new CommodityId(Fish), "Fish", CommodityCategory.Raw),
            ],
            extraction: new ExtractionSettings(1, new PortFishing(new CommodityId(Fish), 1)),
            shipTypes:
            [
                new ShipTypeDefinition(new ShipTypeId(Trader), "Trader", cargo: 2, seaZones: 3, combat: new ShipCombatStats(0, 0, 0, 600, 0)),
                new ShipTypeDefinition(new ShipTypeId(Frigate), "Frigate", seaZones: 3, combat: new ShipCombatStats(3, 5, 10, 900, 4, 35)),
                new ShipTypeDefinition(new ShipTypeId(Battleship), "Battleship", seaZones: 3, combat: new ShipCombatStats(10, 8, 50, 2800, 3)),
                new ShipTypeDefinition(new ShipTypeId(Short), "Short", seaZones: 3, combat: new ShipCombatStats(50, 1, 0, 600, 1)),
                new ShipTypeDefinition(new ShipTypeId(Longbow), "Longbow", seaZones: 3, combat: new ShipCombatStats(50, 10, 0, 600, 1)),
                new ShipTypeDefinition(new ShipTypeId(Hulk), "Hulk", seaZones: 3),
            ],
            trade: new ProportionalTradeMarket()));
    }
}
