using Imperialism.Content;
using Imperialism.Core;
using Xunit;

namespace Imperialism.Presentation.Tests;

/// <summary>
/// Row 4e-2 part B: naval orders in the draft, and the fleet preview that restates Core's
/// <c>NavalPlanner</c> for them. Every preview warning is checked against the refusal Core
/// actually emits for the same orders, and every accepted order against the event and the
/// task-force state the turn leaves behind.
/// </summary>
public sealed class NavalOrdersTests
{
    private const string Scenario = "scenario.demo";
    private static readonly CountryId Blue = new(0);
    private static readonly CountryId Red = new(1);

    private static readonly ProvinceId Home = new(0);
    private static readonly ProvinceId Far = new(1);

    private static readonly SeaZoneId SeaA = new(0);
    private static readonly SeaZoneId SeaB = new(1);
    private static readonly SeaZoneId SeaC = new(2);
    private static readonly SeaZoneId SeaD = new(3);

    /// <summary>
    /// A seven-cell row: Blue's capital in Home (0), then the sea A (1), B (2) and C (3),
    /// Red's capital in Far (4), a sea D (5) with no water route to the others, and more of
    /// Far (6). So A–B–C is a chain, C and D touch Far, and A touches Home. Blue has two
    /// Frigates and a Gunboat in A, a Frigate in C, a Frigate in D and three Traders
    /// unplaced; Red has a Frigate in B. A Frigate reaches two zones a turn, a Gunboat one.
    /// </summary>
    private static CompiledWorldPackage Compile()
    {
        var document = TurnReportFixture.CreateDocument();
        var combat = new ShipCombatContent { Firepower = 100, Range = 1, Armour = 10, HullScale = 600, BattleSpeed = 2 };
        document.ShipTypes =
        [
            new ShipTypeContentDefinition { Key = "ship.trader", Name = "Trader", Cargo = 2, SeaZones = 1 },
            new ShipTypeContentDefinition { Key = "ship.frigate", Name = "Frigate", SeaZones = 2, Combat = combat },
            new ShipTypeContentDefinition { Key = "ship.gunboat", Name = "Gunboat", SeaZones = 1, Combat = combat },
        ];

        document.Map.Width = 7;
        document.Map.SeaZones =
        [
            new NamedContentDefinition { Key = "sea.a", Name = "Sea A" },
            new NamedContentDefinition { Key = "sea.b", Name = "Sea B" },
            new NamedContentDefinition { Key = "sea.c", Name = "Sea C" },
            new NamedContentDefinition { Key = "sea.d", Name = "Sea D" },
        ];
        CellContentDocument Land(string province, string? resource = null, bool site = false) => new()
        {
            Terrain = "terrain.plains",
            Region = new CellRegionContent { Province = province },
            Resources = resource is null ? [] : [resource],
            HasSettlementSite = site,
        };
        CellContentDocument Sea(string zone) => new()
        {
            Terrain = "terrain.ocean",
            Region = new CellRegionContent { SeaZone = zone },
        };
        document.Map.Cells =
        [
            Land("province.home", "resource.grain", site: true),
            Sea("sea.a"),
            Sea("sea.b"),
            Sea("sea.c"),
            Land("province.far", "resource.timber", site: true),
            Sea("sea.d"),
            Land("province.far"),
        ];

        foreach (var scenario in document.Scenarios)
        {
            scenario.Capitals =
            [
                new CountryCapitalContent { Country = "country.blue", Cell = 0 },
                new CountryCapitalContent { Country = "country.red", Cell = 4 },
            ];
            scenario.Ships =
            [
                new ShipContent { Country = "country.blue", Type = "ship.frigate", SeaZoneKey = "sea.a", Count = 2 },
                new ShipContent { Country = "country.blue", Type = "ship.gunboat", SeaZoneKey = "sea.a", Count = 1 },
                new ShipContent { Country = "country.blue", Type = "ship.frigate", SeaZoneKey = "sea.c", Count = 1 },
                new ShipContent { Country = "country.blue", Type = "ship.frigate", SeaZoneKey = "sea.d", Count = 1 },
                new ShipContent { Country = "country.blue", Type = "ship.trader", Count = 3 },
                new ShipContent { Country = "country.red", Type = "ship.frigate", SeaZoneKey = "sea.b", Count = 1 },
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

        /// <summary>
        /// The fleets by label: "A2" Blue's two Frigates in A, "AG" the Gunboat, "C" and "D"
        /// the lone Frigates, "T" the Traders, "R" Red's Frigate, "X" a fleet that does not exist.
        /// </summary>
        public FleetId Fleet(string label)
        {
            FleetId Find(CountryId country, string type, SeaZoneId? zone) => State.Fleets.Single(fleet =>
                fleet.Country == country && State.Definition.ShipTypes[fleet.Type.Value].Name == type &&
                fleet.SeaZone == zone).Id;
            return label switch
            {
                "A2" => Find(Blue, "Frigate", SeaA),
                "AG" => Find(Blue, "Gunboat", SeaA),
                "C" => Find(Blue, "Frigate", SeaC),
                "D" => Find(Blue, "Frigate", SeaD),
                "T" => Find(Blue, "Trader", null),
                "R" => Find(Red, "Frigate", SeaB),
                "X" => new FleetId(99),
                _ => throw new ArgumentOutOfRangeException(nameof(label)),
            };
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

    private static NavalMission Mission(string spec) => spec switch
    {
        "none" => NavalMission.None,
        "patrol" => NavalMission.Patrol,
        "move:a" => NavalMission.MoveTo(SeaA),
        "move:b" => NavalMission.MoveTo(SeaB),
        "move:c" => NavalMission.MoveTo(SeaC),
        "blockade:red" => NavalMission.Blockade(Red),
        "blockade:blue" => NavalMission.Blockade(Blue),
        "landing:far" => NavalMission.Landing(Far),
        "landing:home" => NavalMission.Landing(Home),
        _ => throw new ArgumentOutOfRangeException(nameof(spec)),
    };

    private static NavalMissionKind KindOf(string spec) => spec.Split(':')[0] switch
    {
        "none" => NavalMissionKind.None,
        "patrol" => NavalMissionKind.Patrol,
        "move" => NavalMissionKind.Move,
        "blockade" => NavalMissionKind.Blockade,
        _ => NavalMissionKind.Landing,
    };

    /// <summary>
    /// Blue's refusals are the preview's warnings, one for one in list order; each accepted
    /// order with an effect is the event Core gave it, in the same order.
    /// </summary>
    private static void AssertPreviewMatches(OrdersPreview preview, IReadOnlyList<TurnEvent> events)
    {
        var orders = preview.Fleet.Orders;
        Assert.Equal(
            events.OfType<NavalOrderRefusedEvent>().Where(static item => item.Country == Blue)
                .Select(static item => (item.Kind == NavalOrderKind.Assemble, item.Reason)),
            orders.Where(static order => order.Warning is not null)
                .Select(static order => (order.Kind == DraftNavalOrderKind.Assemble, order.Warning!.Value.Refusal!.Value)));

        var accepted = orders.Where(static order => order.Warning is null).ToArray();
        Assert.Equal(
            accepted.Count(static order => order.Kind == DraftNavalOrderKind.Assemble),
            events.OfType<TaskForceAssembledEvent>().Count(static item => item.Country == Blue));

        var effects = events
            .Where(static item => item is TaskForcePatrolOrderedEvent { Country.Value: 0 } or
                TaskForceBlockadeOrderedEvent { Country.Value: 0 } or
                TaskForceLandingOrderedEvent { Country.Value: 0 })
            .Select(static item => item switch
            {
                TaskForcePatrolOrderedEvent => NavalMissionKind.Patrol,
                TaskForceBlockadeOrderedEvent => NavalMissionKind.Blockade,
                _ => NavalMissionKind.Landing,
            });
        Assert.Equal(
            effects,
            accepted.Where(static order => order.HasEffect && order.Mission is
                    NavalMissionKind.Patrol or NavalMissionKind.Blockade or NavalMissionKind.Landing)
                .Select(static order => order.Mission));
        Assert.Equal(
            accepted.Count(static order => order.Mission == NavalMissionKind.Move),
            events.OfType<TaskForceSailedEvent>().Count(static item => item.Country == Blue));
    }

    [Fact]
    public void BuildKeepsListOrderAndANewerOrderReplacesTheOlder()
    {
        var game = new Game(Compile());
        var draft = game.Draft;
        var (a2, ag, c) = (game.Fleet("A2"), game.Fleet("AG"), game.Fleet("C"));
        var force = new TaskForceId(7);

        draft.SetNavalAssembly([a2, ag], NavalMission.Patrol);
        draft.SetNavalCommand(force, NavalMission.Patrol);
        draft.SetNavalAssembly([c], NavalMission.None);
        draft.SetNavalCommand(force, NavalMission.MoveTo(SeaB));

        // Sharing the Gunboat with the first and the Frigate in C with the third: the
        // newer selection takes the first one's place and the third goes.
        draft.SetNavalAssembly([ag, c, c], NavalMission.MoveTo(SeaB));

        var orders = draft.Build().Naval;
        Assert.Equal(2, orders.Count);
        var assemble = Assert.IsType<NavalOrder.AssembleOrder>(orders[0]);
        Assert.Equal([ag, c], assemble.Fleets);
        Assert.Equal(SeaB, Assert.IsType<NavalMission.MoveMission>(assemble.Mission).Destination);
        var command = Assert.IsType<NavalOrder.CommandOrder>(orders[1]);
        Assert.Equal(force, command.TaskForce);
        Assert.Equal(SeaB, Assert.IsType<NavalMission.MoveMission>(command.Mission).Destination);
        Assert.Null(draft.GetNavalAssembly(a2));
        Assert.Same(draft.NavalOrders[0], draft.GetNavalAssembly(c));
        Assert.Same(draft.NavalOrders[1], draft.GetNavalCommand(force));

        Assert.Throws<ArgumentException>(() => draft.SetNavalAssembly([], NavalMission.Patrol));
        Assert.Throws<ArgumentOutOfRangeException>(() => draft.SetNavalCommand(force, NavalMission.MoveTo(new SeaZoneId(4))));
        Assert.Throws<ArgumentOutOfRangeException>(() => draft.SetNavalCommand(force, NavalMission.Blockade(new CountryId(2))));
        Assert.Throws<ArgumentOutOfRangeException>(() => draft.SetNavalCommand(force, NavalMission.Landing(new ProvinceId(2))));

        Assert.True(draft.ClearNavalAssembly(ag));
        Assert.False(draft.ClearNavalAssembly(ag));
        Assert.False(draft.IsEmpty);
        Assert.True(draft.ClearNavalCommand(force));
        Assert.True(draft.IsEmpty);

        draft.SetNavalCommand(force, NavalMission.Patrol);
        draft.ClearNavalOrders();
        Assert.True(draft.IsEmpty);
    }

    [Fact]
    public void EveryNavalOrderIsClearedAtEndTurn()
    {
        var game = new Game(Compile());
        game.Draft.SetNavalAssembly([game.Fleet("C")], NavalMission.Landing(Far));

        game.EndTurn();

        Assert.Empty(game.Draft.NavalOrders);
        Assert.True(game.Draft.IsEmpty);
    }

    [Fact]
    public void TheFleetScreenListsFleetsAndTaskForcesWithTheirShipsAndHolds()
    {
        var game = new Game(Compile());
        var view = game.Preview().Fleet;
        Assert.False(view.OrdersSetAside);
        Assert.Equal(1, view.TurnNumber);
        Assert.Equal(5, view.Fleets.Count);
        Assert.Empty(view.TaskForces);

        var traders = view.Fleets.Single(row => row.Id == game.Fleet("T"));
        Assert.Equal(("Trader", 3L, 6L, (SeaZoneId?)null, (string?)null), (traders.TypeName, traders.Count, traders.Holds, traders.SeaZone, traders.SeaZoneKey));
        var frigates = view.Fleets.Single(row => row.Id == game.Fleet("A2"));
        Assert.Equal((2L, 0L, 2L, "sea.a"), (frigates.Count, frigates.Holds, frigates.Reach, frigates.SeaZoneKey));

        game.Draft.SetNavalAssembly([game.Fleet("A2"), game.Fleet("AG")], NavalMission.Patrol);
        game.EndTurn();

        var force = Assert.Single(game.Preview().Fleet.TaskForces);
        Assert.Equal("sea.a", force.SeaZoneKey);
        Assert.Equal(TaskForceActivity.Patrolling, force.Activity);
        Assert.Equal((3L, 0L, 1L), (force.Ships, force.Holds, force.Reach));
        Assert.Equal(new[] { game.Fleet("A2"), game.Fleet("AG") }.OrderBy(static id => id.Value), force.Fleets);
        Assert.Equal(force.Id, game.Preview().Fleet.Fleets.Single(row => row.Id == game.Fleet("AG")).TaskForce);
    }

    /// <summary>One assembly, its mission, and the warning Core's refusal matches.</summary>
    [Theory]
    [InlineData("A2", "patrol", null)]
    [InlineData("A2", "none", null)]
    [InlineData("A2", "move:c", null)]
    [InlineData("A2+AG", "move:c", NavalOrderRefusal.OutOfRange)]
    [InlineData("A2+AG", "move:b", null)]
    [InlineData("D", "move:c", NavalOrderRefusal.OutOfRange)]
    [InlineData("A2", "move:a", NavalOrderRefusal.AlreadyInThatSeaZone)]
    [InlineData("C", "blockade:red", null)]
    [InlineData("A2", "blockade:red", NavalOrderRefusal.CapitalNotOnThisSeaZone)]
    [InlineData("A2", "blockade:blue", NavalOrderRefusal.BlockadeOwnCountry)]
    [InlineData("C", "landing:far", null)]
    [InlineData("D", "landing:far", null)]
    [InlineData("A2", "landing:home", NavalOrderRefusal.LandingOnOwnProvince)]
    [InlineData("A2", "landing:far", NavalOrderRefusal.ProvinceNotOnThisSeaZone)]
    [InlineData("A2+C", "patrol", NavalOrderRefusal.FleetsInDifferentSeaZones)]
    [InlineData("T", "patrol", NavalOrderRefusal.FleetNotPlaced)]
    [InlineData("R", "patrol", NavalOrderRefusal.FleetNotYours)]
    [InlineData("X", "patrol", NavalOrderRefusal.NoSuchFleet)]
    public void AnAssemblyPreviewsWhatCoreDoes(string fleets, string mission, NavalOrderRefusal? expected)
    {
        var game = new Game(Compile());
        var named = fleets.Split('+').Select(game.Fleet).ToArray();
        game.Draft.SetNavalAssembly(named, Mission(mission));

        var order = Assert.Single(game.Preview().Fleet.Orders);
        Assert.Equal(expected, order.Warning);
        Assert.Equal(KindOf(mission), order.Mission);

        var events = game.EndTurn();
        var refused = events.OfType<NavalOrderRefusedEvent>().SingleOrDefault(static item => item.Country == Blue);
        Assert.Equal(expected, refused?.Reason);
        var forces = game.State.TaskForces.Where(static force => force.Country == Blue).ToArray();
        if (expected is not null)
        {
            // All or nothing: a refused mission leaves no task force behind.
            Assert.Empty(forces);
            return;
        }

        var force = Assert.Single(forces);
        Assert.Equal(named.OrderBy(static id => id.Value), force.Fleets);
        Assert.Equal(order.Mission switch
        {
            NavalMissionKind.Patrol => TaskForceActivity.Patrolling,
            NavalMissionKind.Blockade => TaskForceActivity.Blockading,
            NavalMissionKind.Landing => TaskForceActivity.Landing,
            _ => TaskForceActivity.Idle,
        }, force.Activity);
        if (order.Mission == NavalMissionKind.Move)
        {
            Assert.Equal(((NavalMission.MoveMission)Mission(mission)).Destination, force.SeaZone);
            Assert.True(order.Distance <= order.Reach);
        }
    }

    [Fact]
    public void AMoveShowsItsReachAndTheDistanceOrThatNoSeaPathJoinsThem()
    {
        var game = new Game(Compile());
        game.Draft.SetNavalAssembly([game.Fleet("A2"), game.Fleet("AG")], NavalMission.MoveTo(SeaC));
        game.Draft.SetNavalAssembly([game.Fleet("D")], NavalMission.MoveTo(SeaC));

        var orders = game.Preview().Fleet.Orders;
        Assert.Equal((1L, 2L, SeaA, "sea.c"), (orders[0].Reach!.Value, orders[0].Distance!.Value, orders[0].SeaZone!.Value, orders[0].TargetKey));
        Assert.Equal(2L, orders[1].Reach);
        Assert.Null(orders[1].Distance);
        Assert.All(orders, static order => Assert.Equal(NavalOrderRefusal.OutOfRange, order.Warning));
        game.EndTurn();
    }

    [Fact]
    public void ACommandMovesPatrolsOrDoesNothingAndAnUnknownTaskForceIsRefused()
    {
        var game = new Game(Compile());
        game.Draft.SetNavalAssembly([game.Fleet("A2")], NavalMission.None);
        game.EndTurn();
        var force = game.State.TaskForces.Single(static item => item.Country == Blue).Id;

        // A command of None is accepted and passes over without an event.
        game.Draft.SetNavalCommand(force, NavalMission.None);
        game.Draft.SetNavalCommand(new TaskForceId(99), NavalMission.Patrol);
        var orders = game.Preview().Fleet.Orders;
        Assert.Null(orders[0].Warning);
        Assert.False(orders[0].HasEffect);
        Assert.Equal(NavalOrderRefusal.NoSuchTaskForce, orders[1].Warning);
        var events = game.EndTurn();
        Assert.DoesNotContain(events, static item => item is TaskForcePatrolOrderedEvent or TaskForceSailedEvent);
        Assert.Equal(TaskForceActivity.Idle, game.State.GetTaskForce(force).Activity);

        game.Draft.SetNavalCommand(force, NavalMission.MoveTo(SeaC));
        var sail = Assert.Single(game.Preview().Fleet.Orders);
        Assert.Equal((2L, 2L), (sail.Reach!.Value, sail.Distance!.Value));
        events = game.EndTurn();
        var sailed = Assert.Single(events.OfType<TaskForceSailedEvent>());
        Assert.Equal((SeaA, SeaC), (sailed.From, sailed.To));
        Assert.Equal(SeaC, game.State.GetTaskForce(force).SeaZone);

        // Arrived idle; now beside Red's capital, it may blockade it.
        game.Draft.SetNavalCommand(force, NavalMission.Blockade(Red));
        Assert.Null(Assert.Single(game.Preview().Fleet.Orders).Warning);
        game.EndTurn();
        var row = Assert.Single(game.Preview().Fleet.TaskForces);
        Assert.Equal((TaskForceActivity.Blockading, "country.red"), (row.Activity, row.BlockadeTargetKey));

        // A move lifts the blockade; the task force arrives idle.
        game.Draft.SetNavalCommand(force, NavalMission.MoveTo(SeaB));
        Assert.Null(Assert.Single(game.Preview().Fleet.Orders).Warning);
        game.EndTurn();
        Assert.Equal((SeaB, TaskForceActivity.Idle), (game.State.GetTaskForce(force).SeaZone, game.State.GetTaskForce(force).Activity));
    }

    [Fact]
    public void ALandingSiteIsUsableFromTheNextTurnAndReOrderingItRestampsIt()
    {
        var game = new Game(Compile());
        game.Draft.SetNavalAssembly([game.Fleet("C")], NavalMission.Landing(Far));
        var order = Assert.Single(game.Preview().Fleet.Orders);
        Assert.Equal((2, false, "province.far"), (order.LandingUsableFromTurn!.Value, order.RestampsLandingSite, order.TargetKey));
        game.EndTurn();

        var force = game.State.TaskForces.Single(static item => item.Country == Blue);
        Assert.Equal((Far, 1), (force.LandingTarget!.Value, force.LandingSince!.Value));
        var row = Assert.Single(game.Preview().Fleet.TaskForces);
        Assert.Equal((TaskForceActivity.Landing, "province.far", true), (row.Activity, row.LandingTargetKey, row.LandingUsableThisTurn));

        // Re-ordering the same site makes it a new one, unusable again this turn.
        game.Draft.SetNavalCommand(force.Id, NavalMission.Landing(Far));
        order = Assert.Single(game.Preview().Fleet.Orders);
        Assert.Equal((3, true), (order.LandingUsableFromTurn!.Value, order.RestampsLandingSite));
        game.EndTurn();
        Assert.Equal(2, game.State.GetTaskForce(force.Id).LandingSince);
        Assert.True(Assert.Single(game.Preview().Fleet.TaskForces).LandingUsableThisTurn);

        // A move gives the site up.
        game.Draft.SetNavalCommand(force.Id, NavalMission.MoveTo(SeaB));
        game.EndTurn();
        Assert.Null(game.State.GetTaskForce(force.Id).LandingTarget);
    }

    [Fact]
    public void AFleetAssembledEarlierInTheListIsAlreadyInATaskForce()
    {
        // The draft never holds two assemblies sharing a fleet, so the overlay is reached
        // only through a fleet that is already in a task force as the turn opens.
        var game = new Game(Compile());
        game.Draft.SetNavalAssembly([game.Fleet("A2")], NavalMission.Patrol);
        game.EndTurn();

        game.Draft.SetNavalAssembly([game.Fleet("A2"), game.Fleet("AG")], NavalMission.Patrol);
        Assert.Equal(NavalOrderRefusal.FleetAlreadyInTaskForce, Assert.Single(game.Preview().Fleet.Orders).Warning);
        game.EndTurn();
    }

    [Fact]
    public void AnUntouchedDraftStillResolvesLikeEmptyOrders()
    {
        var package = Compile();
        var world = package.GetWorld(Scenario);
        var draft = new OrdersDraft(world, Blue);
        Assert.Empty(draft.Build().Naval);
        var before = new WorldState(world);
        var after = new WorldState(world);
        Assert.Empty(OrdersPreview.Create(package, Scenario, after, draft).Fleet.Orders);
        var expected = TurnReportView.Create(
            package, Scenario, before, TurnResolver.Resolve(before, TurnOrders.Empty(world.Countries.Count), 1));
        var actual = TurnReportView.Create(
            package, Scenario, after, TurnResolver.Resolve(after, draft.BuildTurnOrders(), 1));
        Assert.Equal(expected.EventCount, actual.EventCount);
        Assert.Equal(
            expected.Phases.SelectMany(static phase => phase.Lines.Select(line => $"{phase.Phase}:{line}")),
            actual.Phases.SelectMany(static phase => phase.Lines.Select(line => $"{phase.Phase}:{line}")));
        Assert.Equal(before.TaskForces.Count, after.TaskForces.Count);
    }
}
