using Imperialism.Core;
using Xunit;

namespace Imperialism.Core.Tests;

/// <summary>
/// Declaring war and commanding fleets through turn orders, on a 5x1 strip: the
/// capital, an inland province, a coastal port, then two sea zones. The port's only
/// water is sea zone 0 ("Home Waters"); sea zone 1 ("Open Sea") lies beyond it.
/// </summary>
/// <remarks>
/// Country 0 owns every province. Country 1 owns no land and only ships, which is all
/// a blockader needs. See <c>docs/formulas/naval.md</c>.
/// </remarks>
public sealed class NavalOrdersTests
{
    private static readonly CountryId Owner = new(0);
    private static readonly CountryId Raider = new(1);
    private static readonly SeaZoneId HomeWaters = new(0);
    private static readonly SeaZoneId OpenSea = new(1);

    [Fact]
    public void WarDeclaredThenAPatrolCutsThePortUntilTheOwnersWarshipPatrolsOrThePatrolSailsAway()
    {
        // Fleet 1 is the raider's, already beside the port; fleet 2 is the owner's,
        // out in the open sea.
        var state = CreateState(
            Ship(Raider, HomeWaters),
            Ship(Owner, OpenSea));

        // Turn 1: war is declared and the port still fishes.
        var declared = Resolve(state, raider: new CountryTurnOrders(Raider, declareWar: [Owner]));
        var war = Assert.Single(declared.Events.OfType<WarDeclaredEvent>());
        Assert.Equal((Raider, Owner), (war.Country, war.Target));
        Assert.Equal(CountryRelationMode.Hostile, state.GetRelationMode(Owner, Raider));
        Assert.Equal((1, 0), Ports(declared));
        Assert.True(state.HasEffectiveHostility(Raider, Owner));

        // Turn 2: assemble and patrol in one order. Hostility is in effect, the
        // patrol is in place, and the port loses its route this very turn.
        var blockaded = Resolve(state, raider: new CountryTurnOrders(
            Raider, naval: [NavalOrder.Assemble([new FleetId(1)], NavalMission.Patrol)]));
        var assembled = Assert.Single(blockaded.Events.OfType<TaskForceAssembledEvent>());
        Assert.Equal((Raider, new TaskForceId(1), HomeWaters), (assembled.Country, assembled.TaskForce, assembled.SeaZone));
        Assert.Single(blockaded.Events.OfType<TaskForcePatrolOrderedEvent>());
        Assert.Equal((0, 1), Ports(blockaded));

        // Turn 3: no orders. The patrol persists.
        Assert.Equal((0, 1), Ports(Resolve(state)));

        // Turn 4: the owner's warship sails in. It arrives idle, so it does not
        // contest the zone yet and the port stays cut.
        var arrived = Resolve(state, owner: new CountryTurnOrders(
            Owner, naval: [NavalOrder.Assemble([new FleetId(2)], NavalMission.MoveTo(HomeWaters))]));
        var leg = Assert.Single(arrived.Events.OfType<TaskForceSailedEvent>());
        Assert.Equal((OpenSea, HomeWaters), (leg.From, leg.To));
        Assert.Equal(TaskForceActivity.Idle, state.GetTaskForce(new TaskForceId(2)).Activity);
        Assert.Equal((0, 1), Ports(arrived));

        // Turn 5: the owner's warship patrols, and the port regains its route.
        Assert.Equal((1, 0), Ports(Resolve(state, owner: new CountryTurnOrders(
            Owner, naval: [NavalOrder.Command(new TaskForceId(2), NavalMission.Patrol)]))));

        // Turn 6: the owner sails away again and the port is cut again.
        Assert.Equal((0, 1), Ports(Resolve(state, owner: new CountryTurnOrders(
            Owner, naval: [NavalOrder.Command(new TaskForceId(2), NavalMission.MoveTo(OpenSea))]))));

        // Turn 7: the raider's patrol sails away, and the port is open with nobody
        // guarding it.
        var lifted = Resolve(state, raider: new CountryTurnOrders(
            Raider, naval: [NavalOrder.Command(new TaskForceId(1), NavalMission.MoveTo(OpenSea))]));
        Assert.Equal((1, 0), Ports(lifted));
        Assert.Equal(TaskForceActivity.Idle, state.GetTaskForce(new TaskForceId(1)).Activity);
        Assert.Equal(OpenSea, state.GetTaskForce(new TaskForceId(1)).SeaZone);
    }

    [Fact]
    public void AWarDeclaredThisTurnCutsNothingUntilNextTurn()
    {
        var state = CreateState(Ship(Raider, HomeWaters));

        // Declared and patrolling in the same turn: "the war begins on the next turn".
        var first = Resolve(state, raider: new CountryTurnOrders(
            Raider,
            declareWar: [Owner],
            naval: [NavalOrder.Assemble([new FleetId(1)], NavalMission.Patrol)]));
        Assert.Equal(TaskForceActivity.Patrolling, state.GetTaskForce(new TaskForceId(1)).Activity);
        Assert.Equal((1, 0), Ports(first));

        // Nothing new ordered: the war is now in effect and the patrol still stands.
        Assert.Equal((0, 1), Ports(Resolve(state)));
    }

    [Fact]
    public void APatrolPersistsAcrossTurnsWithoutBeingReordered()
    {
        var state = CreateState(Ship(Raider, HomeWaters));
        _ = Resolve(state, raider: new CountryTurnOrders(
            Raider, naval: [NavalOrder.Assemble([new FleetId(1)], NavalMission.Patrol)]));

        for (var turn = 0; turn < 4; turn++)
        {
            var resolution = Resolve(state);
            Assert.Empty(resolution.Events.OfType<TaskForcePatrolOrderedEvent>());
            Assert.Equal(TaskForceActivity.Patrolling, state.GetTaskForce(new TaskForceId(1)).Activity);
        }
    }

    [Fact]
    public void AMovedTaskForceArrivesIdleAndStaysIdleUntilOrderedToPatrol()
    {
        var state = CreateState(Ship(Raider, OpenSea));
        _ = Resolve(state, raider: new CountryTurnOrders(
            Raider, naval: [NavalOrder.Assemble([new FleetId(1)], NavalMission.Patrol)]));
        Assert.Equal(TaskForceActivity.Patrolling, state.GetTaskForce(new TaskForceId(1)).Activity);

        var moved = Resolve(state, raider: new CountryTurnOrders(
            Raider, naval: [NavalOrder.Command(new TaskForceId(1), NavalMission.MoveTo(HomeWaters))]));

        var force = state.GetTaskForce(new TaskForceId(1));
        Assert.Single(moved.Events.OfType<TaskForceSailedEvent>());
        Assert.Equal(HomeWaters, force.SeaZone);
        Assert.Equal(TaskForceActivity.Idle, force.Activity);
        Assert.All(force.Fleets, id => Assert.Equal(HomeWaters, state.GetFleet(id).SeaZone));

        _ = Resolve(state);
        Assert.Equal(TaskForceActivity.Idle, force.Activity);
    }

    [Fact]
    public void ATaskForceTakesOneMissionOrMoveATurn()
    {
        var state = CreateState(Ship(Raider, HomeWaters));

        // Assembling with a patrol spends this turn's mission; the move after it is
        // refused, and the task force stays where it is, on patrol.
        var resolution = Resolve(state, raider: new CountryTurnOrders(
            Raider,
            naval:
            [
                NavalOrder.Assemble([new FleetId(1)], NavalMission.Patrol),
                NavalOrder.Command(new TaskForceId(1), NavalMission.MoveTo(OpenSea)),
                NavalOrder.Command(new TaskForceId(1), NavalMission.Patrol),
            ]));

        var refusals = resolution.Events.OfType<NavalOrderRefusedEvent>().ToArray();
        Assert.Equal(2, refusals.Length);
        Assert.All(refusals, refused => Assert.Equal(NavalOrderRefusal.AlreadyOrderedThisTurn, refused.Reason));
        Assert.All(refusals, refused => Assert.Equal(new TaskForceId(1), refused.TaskForce));
        Assert.Empty(resolution.Events.OfType<TaskForceSailedEvent>());
        var force = state.GetTaskForce(new TaskForceId(1));
        Assert.Equal((HomeWaters, TaskForceActivity.Patrolling), (force.SeaZone, force.Activity));

        // Next turn the move is allowed.
        var next = Resolve(state, raider: new CountryTurnOrders(
            Raider, naval: [NavalOrder.Command(new TaskForceId(1), NavalMission.MoveTo(OpenSea))]));
        Assert.Empty(next.Events.OfType<NavalOrderRefusedEvent>());
        Assert.Equal(OpenSea, force.SeaZone);
    }

    [Fact]
    public void AMoveSpendsTheTurnSoAPatrolAfterItIsRefused()
    {
        var state = CreateState(Ship(Raider, OpenSea));
        var resolution = Resolve(state, raider: new CountryTurnOrders(
            Raider,
            naval:
            [
                NavalOrder.Assemble([new FleetId(1)], NavalMission.MoveTo(HomeWaters)),
                NavalOrder.Command(new TaskForceId(1), NavalMission.Patrol),
            ]));

        Assert.Equal(
            NavalOrderRefusal.AlreadyOrderedThisTurn,
            Assert.Single(resolution.Events.OfType<NavalOrderRefusedEvent>()).Reason);
        var force = state.GetTaskForce(new TaskForceId(1));
        Assert.Equal((HomeWaters, TaskForceActivity.Idle), (force.SeaZone, force.Activity));
    }

    [Fact]
    public void ACommandWithNoMissionSpendsNothing()
    {
        var state = CreateState(Ship(Raider, HomeWaters));
        var resolution = Resolve(state, raider: new CountryTurnOrders(
            Raider,
            naval:
            [
                NavalOrder.Assemble([new FleetId(1)], NavalMission.None),
                NavalOrder.Command(new TaskForceId(1), NavalMission.None),
                NavalOrder.Command(new TaskForceId(1), NavalMission.Patrol),
            ]));

        Assert.Empty(resolution.Events.OfType<NavalOrderRefusedEvent>());
        Assert.Single(resolution.Events.OfType<TaskForceAssembledEvent>());
        Assert.Equal(TaskForceActivity.Patrolling, state.GetTaskForce(new TaskForceId(1)).Activity);
    }

    [Fact]
    public void EveryInvalidAssemblyIsRefusedWithItsReasonAndChangesNothing()
    {
        // 1: raider at home; 2: raider in the open sea; 3: owner at home;
        // 4: raider, unplaced; 5: raider at home, already assembled below.
        var cases = new (FleetId[] Fleets, NavalMission Mission, NavalOrderRefusal Reason)[]
        {
            ([], NavalMission.None, NavalOrderRefusal.NoFleetsSelected),
            ([new(1), new(1)], NavalMission.None, NavalOrderRefusal.FleetSelectedTwice),
            ([new(1), new(99)], NavalMission.None, NavalOrderRefusal.NoSuchFleet),
            ([new(3)], NavalMission.None, NavalOrderRefusal.FleetNotYours),
            ([new(4)], NavalMission.None, NavalOrderRefusal.FleetNotPlaced),
            ([new(5)], NavalMission.None, NavalOrderRefusal.FleetAlreadyInTaskForce),
            ([new(1), new(2)], NavalMission.None, NavalOrderRefusal.FleetsInDifferentSeaZones),

            // All or nothing: a mission that would be refused stops the grouping too.
            ([new(1)], NavalMission.MoveTo(new SeaZoneId(7)), NavalOrderRefusal.NoSuchSeaZone),
            ([new(1)], NavalMission.MoveTo(HomeWaters), NavalOrderRefusal.AlreadyInThatSeaZone),
        };

        foreach (var (fleets, mission, reason) in cases)
        {
            var state = CreateState(
                Ship(Raider, HomeWaters),
                Ship(Raider, OpenSea),
                Ship(Owner, HomeWaters),
                new InitialShip(Raider, new ShipTypeId(0), 0, 1, null),
                Ship(Raider, HomeWaters));
            _ = state.AssembleTaskForce(Raider, [new FleetId(5)]);

            var resolution = Resolve(state, raider: new CountryTurnOrders(
                Raider, naval: [NavalOrder.Assemble(fleets, mission)]));

            var refused = Assert.Single(resolution.Events.OfType<NavalOrderRefusedEvent>());
            Assert.Equal((NavalOrderKind.Assemble, (TaskForceId?)null, reason), (refused.Kind, refused.TaskForce, refused.Reason));
            Assert.Empty(resolution.Events.OfType<TaskForceAssembledEvent>());
            Assert.Single(state.TaskForces);
            Assert.All(
                new[] { 1, 2, 3, 4 },
                id => Assert.Null(state.GetFleet(new FleetId(id)).TaskForce));
        }
    }

    [Fact]
    public void EveryInvalidCommandIsRefusedWithItsReasonAndChangesNothing()
    {
        var cases = new (TaskForceId TaskForce, NavalMission Mission, NavalOrderRefusal Reason)[]
        {
            (new(9), NavalMission.Patrol, NavalOrderRefusal.NoSuchTaskForce),
            (new(2), NavalMission.Patrol, NavalOrderRefusal.TaskForceNotYours),
            (new(2), NavalMission.None, NavalOrderRefusal.TaskForceNotYours),
            (new(1), NavalMission.MoveTo(new SeaZoneId(7)), NavalOrderRefusal.NoSuchSeaZone),
            (new(1), NavalMission.MoveTo(HomeWaters), NavalOrderRefusal.AlreadyInThatSeaZone),
        };

        foreach (var (taskForce, mission, reason) in cases)
        {
            var state = CreateState(Ship(Raider, HomeWaters), Ship(Owner, HomeWaters));
            _ = state.AssembleTaskForce(Raider, [new FleetId(1)]);
            _ = state.AssembleTaskForce(Owner, [new FleetId(2)]);

            var resolution = Resolve(state, raider: new CountryTurnOrders(
                Raider, naval: [NavalOrder.Command(taskForce, mission)]));

            var refused = Assert.Single(resolution.Events.OfType<NavalOrderRefusedEvent>());
            Assert.Equal((NavalOrderKind.Command, (TaskForceId?)taskForce, reason), (refused.Kind, refused.TaskForce, refused.Reason));
            Assert.Empty(resolution.Events.OfType<TaskForceSailedEvent>());
            Assert.Empty(resolution.Events.OfType<TaskForcePatrolOrderedEvent>());
            Assert.All(state.TaskForces, force =>
            {
                Assert.Equal(HomeWaters, force.SeaZone);
                Assert.Equal(TaskForceActivity.Idle, force.Activity);
            });
        }
    }

    [Fact]
    public void EveryInvalidDeclarationIsRefusedWithItsReasonAndChangesNothing()
    {
        var state = CreateState();
        var resolution = Resolve(state, raider: new CountryTurnOrders(
            Raider, declareWar: [Raider, new CountryId(5), Owner, Owner]));

        Assert.Equal(
            [
                (Raider, WarDeclarationRefusal.Itself),
                (new CountryId(5), WarDeclarationRefusal.NoSuchCountry),
                (Owner, WarDeclarationRefusal.AlreadyDeclaredThisTurn),
            ],
            resolution.Events.OfType<WarDeclarationRefusedEvent>()
                .Select(static refused => (refused.Target, refused.Reason)));
        Assert.Single(resolution.Events.OfType<WarDeclaredEvent>());
        Assert.Equal(CountryRelationMode.Standard, state.GetRelationMode(Raider, Raider));

        // Next turn the war is in effect. Declaring it again is refused, and does not
        // restamp the war — which would put its start back a turn.
        var token = state.GetRelationToken(Raider, Owner);
        var again = Resolve(state, owner: new CountryTurnOrders(Owner, declareWar: [Raider]));
        Assert.Equal(
            WarDeclarationRefusal.AlreadyAtWar,
            Assert.Single(again.Events.OfType<WarDeclarationRefusedEvent>()).Reason);
        Assert.Equal(token, state.GetRelationToken(Raider, Owner));
        Assert.True(state.HasEffectiveHostility(Raider, Owner));
    }

    [Fact]
    public void AMutualDeclarationInOneTurnCountsOnceForTheLowerCountryId()
    {
        var state = CreateState();
        var resolution = Resolve(
            state,
            owner: new CountryTurnOrders(Owner, declareWar: [Raider]),
            raider: new CountryTurnOrders(Raider, declareWar: [Owner]));

        var declared = Assert.Single(resolution.Events.OfType<WarDeclaredEvent>());
        Assert.Equal(Owner, declared.Country);
        var refused = Assert.Single(resolution.Events.OfType<WarDeclarationRefusedEvent>());
        Assert.Equal((Raider, WarDeclarationRefusal.AlreadyAtWar), (refused.Country, refused.Reason));
    }

    [Fact]
    public void ADeclarationMayTargetACountryThatOwnsNothing()
    {
        // Core cannot tell a minor nation from a Great Power, and the manual lets
        // either be declared on; country 1 here holds no province at all.
        var state = CreateState();
        var resolution = Resolve(state, owner: new CountryTurnOrders(Owner, declareWar: [Raider]));
        Assert.Single(resolution.Events.OfType<WarDeclaredEvent>());
    }

    [Fact]
    public void OrdersResolveInCountryIdOrderThenListOrderAndAllMovesAfterAllOrders()
    {
        static TurnResolution Run(WorldState state) => Resolve(
            state,
            owner: new CountryTurnOrders(
                Owner,
                naval:
                [
                    NavalOrder.Assemble([new FleetId(2)], NavalMission.MoveTo(HomeWaters)),
                    NavalOrder.Assemble([new FleetId(3)], NavalMission.Patrol),
                ]),
            raider: new CountryTurnOrders(
                Raider,
                naval:
                [
                    NavalOrder.Assemble([new FleetId(1)], NavalMission.MoveTo(OpenSea)),
                ]));

        var first = CreateState(Ship(Raider, HomeWaters), Ship(Owner, OpenSea), Ship(Owner, OpenSea));
        var second = CreateState(Ship(Raider, HomeWaters), Ship(Owner, OpenSea), Ship(Owner, OpenSea));
        var one = Run(first);
        var two = Run(second);

        // Country 0's two orders take ids 1 and 2 in list order although the raider's
        // fleet has the lowest id; the raider's task force is 3.
        Assert.Equal(
            [(Owner, new TaskForceId(1)), (Owner, new TaskForceId(2)), (Raider, new TaskForceId(3))],
            one.Events.OfType<TaskForceAssembledEvent>().Select(static e => (e.Country, e.TaskForce)));

        // Every leg lands after every order, in task-force id order.
        var conflict = one.Events.Where(static e => e.Phase == TurnPhase.Conflict).ToArray();
        var lastOrder = Array.FindLastIndex(
            conflict, static e => e is TaskForceAssembledEvent or TaskForcePatrolOrderedEvent);
        var firstLeg = Array.FindIndex(conflict, static e => e is TaskForceSailedEvent);
        Assert.True(lastOrder < firstLeg);
        Assert.Equal(
            [new TaskForceId(1), new TaskForceId(3)],
            one.Events.OfType<TaskForceSailedEvent>().Select(static e => e.TaskForce));

        Assert.Equal(Describe(one), Describe(two));
    }

    [Fact]
    public void AMoveBeyondTheSlowestHullsReachIsRefusedAndOneWithinItSails()
    {
        // Sea zones: 0 Home, 1 Open, 2 Far; a one-zone hull reaches Open and not Far.
        // "If the new zone is out of range you see a red slashed circle cursor; you
        // cannot move the fleet that far in one turn" (manual lines 1916-1917).
        var state = CreateState(
            zones: 3,
            ships: [Ship(Raider, HomeWaters), Ship(Raider, HomeWaters)]);
        var far = new SeaZoneId(2);

        var assembled = Resolve(state, raider: new CountryTurnOrders(
            Raider, naval: [NavalOrder.Assemble([new FleetId(1)], NavalMission.MoveTo(far))]));
        var refusedAssembly = Assert.Single(assembled.Events.OfType<NavalOrderRefusedEvent>());
        Assert.Equal((NavalOrderKind.Assemble, NavalOrderRefusal.OutOfRange), (refusedAssembly.Kind, refusedAssembly.Reason));
        Assert.Empty(state.TaskForces);

        _ = state.AssembleTaskForce(Raider, [new FleetId(1)]);
        var commanded = Resolve(state, raider: new CountryTurnOrders(
            Raider,
            naval:
            [
                NavalOrder.Command(new TaskForceId(1), NavalMission.MoveTo(far)),

                // The refusal spent nothing, so a move within reach may follow it.
                NavalOrder.Command(new TaskForceId(1), NavalMission.MoveTo(OpenSea)),
            ]));
        var refusedCommand = Assert.Single(commanded.Events.OfType<NavalOrderRefusedEvent>());
        Assert.Equal((NavalOrderKind.Command, NavalOrderRefusal.OutOfRange), (refusedCommand.Kind, refusedCommand.Reason));
        var leg = Assert.Single(commanded.Events.OfType<TaskForceSailedEvent>());
        Assert.Equal((HomeWaters, OpenSea, OpenSea), (leg.From, leg.To, leg.RequestedDestination));
        Assert.Equal(OpenSea, state.GetTaskForce(new TaskForceId(1)).SeaZone);
    }

    [Fact]
    public void ANullNavalOrderIsMalformedButAnImpossibleOneIsNot()
    {
        Assert.Throws<ArgumentException>(() =>
            new CountryTurnOrders(Owner, naval: [null!]));
        Assert.Throws<ArgumentNullException>(() => NavalOrder.Command(new TaskForceId(1), null!));
        var orders = new CountryTurnOrders(
            Owner,
            declareWar: [Owner, Owner],
            naval: [NavalOrder.Assemble([], NavalMission.None)]);
        Assert.Equal(2, orders.DeclareWar.Count);
        Assert.Single(orders.Naval);
    }

    private static IReadOnlyList<string> Describe(TurnResolution resolution) =>
        resolution.Events.Select(static e => e switch
        {
            TaskForceAssembledEvent a => $"A {a.Country} {a.TaskForce.Value} {a.SeaZone} {string.Join(',', a.Fleets.Select(f => f.Value))}",
            TaskForcePatrolOrderedEvent p => $"P {p.Country} {p.TaskForce.Value} {p.SeaZone}",
            TaskForceSailedEvent s => $"S {s.Country} {s.TaskForce.Value} {s.From} {s.To} {s.RequestedDestination}",
            NavalOrderRefusedEvent r => $"R {r.Country} {r.Kind} {r.TaskForce} {r.Reason}",
            _ => e.GetType().Name,
        }).ToArray();

    private static (int Fishing, int Stranded) Ports(TurnResolution resolution)
    {
        var extraction = resolution.Events.OfType<ResourceExtractedEvent>().Single();
        return (extraction.FishingPortCount, extraction.StrandedPortCount);
    }

    private static TurnResolution Resolve(
        WorldState state, CountryTurnOrders? owner = null, CountryTurnOrders? raider = null) =>
        TurnResolver.Resolve(
            state,
            new TurnOrders([owner ?? new CountryTurnOrders(Owner), raider ?? new CountryTurnOrders(Raider)]),
            0);

    private static InitialShip Ship(CountryId country, SeaZoneId zone) =>
        new(country, new ShipTypeId(0), 0, 1, zone);

    private static WorldState CreateState(params InitialShip[] ships) => CreateState(2, ships);

    /// <summary>
    /// Capital, inland, port, then <paramref name="zones"/> sea cells in a row, each its
    /// own sea zone. Ships carry one zone a turn, so a leg is never longer than one.
    /// </summary>
    private static WorldState CreateState(int zones, InitialShip[] ships)
    {
        var width = 3 + zones;
        var dimensions = new MapDimensions(width, 1);
        var cells = new CellDefinition[width];
        for (var index = 0; index < width; index++)
        {
            cells[index] = new CellDefinition(
                new CellIndex(index),
                dimensions.GetCoordinate(new CellIndex(index)),
                new TerrainId(0),
                index >= 3
                    ? CellRegion.ForSeaZone(new SeaZoneId(index - 3))
                    : CellRegion.ForProvince(new ProvinceId(index)),
                null,
                index == 0 ? SettlementSiteKind.Urban : SettlementSiteKind.None,
                null);
        }

        string[] names = ["Home Waters", "Open Sea", "Far Sea"];
        var map = new MapDefinition(
            dimensions,
            cells,
            [
                new ProvinceDefinition(new ProvinceId(0), "Capital"),
                new ProvinceDefinition(new ProvinceId(1), "Inland"),
                new ProvinceDefinition(new ProvinceId(2), "Shore"),
            ],
            Enumerable.Range(0, zones)
                .Select(zone => new SeaZoneDefinition(new SeaZoneId(zone), names[zone])),
            [new ResourceDefinition(new ResourceId(0), new CommodityId(0), [1])]);

        var scenario = new ScenarioDefinition(
            "Naval orders",
            1815,
            [Owner, Owner, Owner],
            [new CellLink(new CellIndex(0), new CellIndex(1)), new CellLink(new CellIndex(1), new CellIndex(2))],
            [new CountryCapital(Owner, new CellIndex(0))],
            null,
            null,
            null,
            null,
            [new CellIndex(2)],
            initialShips: ships);

        return new WorldState(new WorldDefinition(
            map,
            [
                new CountryDefinition(Owner, "Owner"),
                new CountryDefinition(Raider, "Raider"),
            ],
            scenario,
            [new CommodityDefinition(new CommodityId(0), "Fish", CommodityCategory.Raw)],
            null,
            null,
            new ExtractionSettings(1, new PortFishing(new CommodityId(0), 1)),
            shipTypes: [new ShipTypeDefinition(new ShipTypeId(0), "Frigate", seaZones: 1)]));
    }
}
