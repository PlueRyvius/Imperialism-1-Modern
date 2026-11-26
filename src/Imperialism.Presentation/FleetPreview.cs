using Imperialism.Content;
using Imperialism.Core;

namespace Imperialism.Presentation;

/// <summary>Which mission a naval order carries, readable outside Core.</summary>
public enum NavalMissionKind : byte
{
    /// <summary>Done: nothing this turn.</summary>
    None,

    Patrol,

    Blockade,

    Landing,

    Move,
}

/// <summary>One of the country's fleet records.</summary>
/// <param name="Holds">Cargo holds: the hull's cargo times the count.</param>
/// <param name="Reach">Sea zones the hull may sail in one turn.</param>
/// <param name="SeaZone">Null for an unplaced fleet, which cannot join a task force.</param>
public sealed record FleetRowView(
    FleetId Id,
    int TypeIndex,
    string TypeName,
    long Count,
    long Holds,
    long Reach,
    SeaZoneId? SeaZone,
    string? SeaZoneKey,
    TaskForceId? TaskForce);

/// <summary>One of the country's task forces as the turn opens.</summary>
/// <param name="Reach">The slowest hull's sea zones a turn, which is the task force's.</param>
/// <param name="LandingUsableThisTurn">
/// The site was established on an earlier turn, so troops may cross to it this turn
/// (manual 1943–1945).
/// </param>
public sealed record TaskForceRowView(
    TaskForceId Id,
    SeaZoneId SeaZone,
    string SeaZoneKey,
    TaskForceActivity Activity,
    string? BlockadeTargetKey,
    string? LandingTargetKey,
    int? LandingSince,
    bool LandingUsableThisTurn,
    IReadOnlyList<FleetId> Fleets,
    long Ships,
    long Holds,
    long Reach);

/// <summary>One drafted naval order and what Core would make of it.</summary>
/// <param name="TargetKey">The blockaded country, landing province or destination zone.</param>
/// <param name="SeaZone">Where the order acts from, when the fleets or task force can be found.</param>
/// <param name="Reach">The slowest hull's reach, for a move whose fleets can be found.</param>
/// <param name="Distance">Sea zones to the destination of a move; null when no sea path joins them.</param>
/// <param name="HasEffect">
/// False only for an accepted command with no mission, which Core passes over without an
/// event; an assembly with no mission still assembles.
/// </param>
/// <param name="LandingUsableFromTurn">
/// For an accepted landing site, the first turn troops may cross: the next one, even when
/// re-ordering a site already held (manual 1943; Core restamps it).
/// </param>
/// <param name="RestampsLandingSite">The task force already holds a site, which this order resets.</param>
/// <param name="Warning">
/// Core's refusal, or <see cref="OrderWarning{TRefusal}.SetAside"/> for every order of an
/// eliminated country, whose orders Core never reads.
/// </param>
public sealed record DraftedNavalOrderView(
    DraftNavalOrderKind Kind,
    IReadOnlyList<FleetId> Fleets,
    TaskForceId? TaskForce,
    NavalMissionKind Mission,
    string? TargetKey,
    SeaZoneId? SeaZone,
    long? Reach,
    long? Distance,
    bool HasEffect,
    int? LandingUsableFromTurn,
    bool RestampsLandingSite,
    OrderWarning<NavalOrderRefusal>? Warning);

/// <summary>
/// The fleet screen, a preview: the country's fleets and task forces as the turn opens,
/// and each drafted naval order with Core's expected result or refusal.
/// </summary>
/// <remarks>
/// A restatement of Core's <c>NavalPlanner</c> for one country, in list order: an
/// assembly checks its fleets, then its mission, all or nothing; a command checks the task
/// force, passes over a mission of None, refuses a second order to one task force this
/// turn, then checks its mission. A move must lie within the slowest hull's reach by a
/// sea path (manual 1915–1917). Every other country is taken to submit nothing, so a
/// battle or an interception after the orders is not previewed. A task force an
/// assembly creates has no id until the turn resolves, so a command cannot name it.
/// </remarks>
/// <param name="OrdersSetAside">
/// The drafting country is eliminated: Core sets every order aside, so nothing here happens.
/// Every drafted order then carries <see cref="OrderWarning{TRefusal}.SetAside"/> rather than
/// the refusal its vanished fleets would earn, and the fleet and task-force lists are empty,
/// because elimination sinks every hull (army rule 40).
/// </param>
public sealed record FleetView(
    int TurnNumber,
    bool OrdersSetAside,
    IReadOnlyList<FleetRowView> Fleets,
    IReadOnlyList<TaskForceRowView> TaskForces,
    IReadOnlyList<DraftedNavalOrderView> Orders);

/// <summary>A turn-local restatement of Core's <c>NavalPlanner.ResolveConflict</c>, for one country.</summary>
internal sealed class NavalOrderSimulation
{
    private readonly WorldState _state;
    private readonly CountryId _country;
    private readonly HashSet<FleetId> _assembled = [];
    private readonly HashSet<TaskForceId> _ordered = [];

    private NavalOrderSimulation(WorldState state, CountryId country)
    {
        _state = state;
        _country = country;
    }

    public static IReadOnlyList<DraftedNavalOrderView> Run(
        CompiledWorldPackage package, WorldState state, CountryId country, IEnumerable<DraftNavalOrder> orders)
    {
        var simulation = new NavalOrderSimulation(state, country);
        var views = orders.Select(order => simulation.Resolve(package, order));
        if (state.IsEliminated(country))
        {
            // Core empties the orders before NavalPlanner runs, so none is refused: each is
            // set aside, and nothing it would have reached or restamped happens.
            views = views.Select(static view => view with
            {
                HasEffect = false,
                LandingUsableFromTurn = null,
                RestampsLandingSite = false,
                Warning = OrderWarning<NavalOrderRefusal>.SetAside,
            });
        }

        return Array.AsReadOnly(views.ToArray());
    }

    public static NavalMissionKind KindOf(NavalMission mission) => mission switch
    {
        NavalMission.BlockadeMission => NavalMissionKind.Blockade,
        NavalMission.LandingMission => NavalMissionKind.Landing,
        NavalMission.MoveMission => NavalMissionKind.Move,
        _ when ReferenceEquals(mission, NavalMission.Patrol) => NavalMissionKind.Patrol,
        _ => NavalMissionKind.None,
    };

    private DraftedNavalOrderView Resolve(CompiledWorldPackage package, DraftNavalOrder order)
    {
        var mission = KindOf(order.Mission);
        var target = order.Mission switch
        {
            NavalMission.BlockadeMission blockade => package.Catalog.GetKey(blockade.Target),
            NavalMission.LandingMission landing => package.Catalog.GetKey(landing.Target),
            NavalMission.MoveMission move => package.Catalog.GetKey(move.Destination),
            _ => null,
        };

        SeaZoneId? zone = null;
        IReadOnlyList<FleetId> fleets = [];
        NavalOrderRefusal? refusal;
        var hasEffect = true;
        TaskForceState? force = null;
        if (order.Kind == DraftNavalOrderKind.Assemble)
        {
            refusal = CheckAssembly(order.Fleets, out var shared);
            if (refusal is null)
            {
                zone = shared;
                fleets = order.Fleets;
                refusal = CheckMission(order.Mission, shared, fleets);
            }
        }
        else if (!_state.TryGetTaskForce(order.TaskForce!.Value, out force))
        {
            refusal = NavalOrderRefusal.NoSuchTaskForce;
        }
        else if (force.Country != _country)
        {
            refusal = NavalOrderRefusal.TaskForceNotYours;
        }
        else
        {
            zone = force.SeaZone;
            fleets = force.Fleets;
            if (mission == NavalMissionKind.None)
            {
                refusal = null;
                hasEffect = false;
            }
            else if (_ordered.Contains(force.Id) || force.PlannedSeaZone is not null)
            {
                refusal = NavalOrderRefusal.AlreadyOrderedThisTurn;
            }
            else
            {
                refusal = CheckMission(order.Mission, force.SeaZone, fleets);
            }
        }

        long? reach = null;
        long? distance = null;
        if (order.Mission is NavalMission.MoveMission sail && zone is { } from && fleets.Count > 0 &&
            (uint)sail.Destination.Value < (uint)_state.Definition.Map.SeaZones.Count)
        {
            reach = Reach(fleets);
            distance = SeaZoneDistance(_state.Definition.Map.SeaTopology, from, sail.Destination);
        }

        if (refusal is null)
        {
            if (order.Kind == DraftNavalOrderKind.Assemble)
            {
                _assembled.UnionWith(order.Fleets);
            }
            else if (hasEffect)
            {
                _ = _ordered.Add(force!.Id);
            }
        }

        var accepted = refusal is null && hasEffect;
        var sitesLanding = accepted && mission == NavalMissionKind.Landing;
        return new DraftedNavalOrderView(
            order.Kind,
            order.Fleets,
            order.TaskForce,
            mission,
            target,
            zone,
            reach,
            distance,
            hasEffect,
            sitesLanding ? _state.CompletedTurnCount + 2 : null,
            sitesLanding && force is { Activity: TaskForceActivity.Landing },
            refusal is { } reason ? reason : null);
    }

    private NavalOrderRefusal? CheckAssembly(IReadOnlyList<FleetId> fleets, out SeaZoneId seaZone)
    {
        seaZone = default;
        if (fleets.Count == 0)
        {
            return NavalOrderRefusal.NoFleetsSelected;
        }

        if (fleets.Distinct().Count() != fleets.Count)
        {
            return NavalOrderRefusal.FleetSelectedTwice;
        }

        SeaZoneId? shared = null;
        foreach (var id in fleets.OrderBy(static fleet => fleet.Value))
        {
            if (!_state.TryGetFleet(id, out var fleet))
            {
                return NavalOrderRefusal.NoSuchFleet;
            }

            if (fleet.Country != _country)
            {
                return NavalOrderRefusal.FleetNotYours;
            }

            if (fleet.SeaZone is not { } position)
            {
                return NavalOrderRefusal.FleetNotPlaced;
            }

            // An earlier order in this list may already have put it in a task force.
            if (fleet.TaskForce is not null || _assembled.Contains(id))
            {
                return NavalOrderRefusal.FleetAlreadyInTaskForce;
            }

            if (shared is { } known && known != position)
            {
                return NavalOrderRefusal.FleetsInDifferentSeaZones;
            }

            shared = position;
        }

        seaZone = shared!.Value;
        return null;
    }

    private NavalOrderRefusal? CheckMission(NavalMission mission, SeaZoneId current, IReadOnlyList<FleetId> fleets)
    {
        var map = _state.Definition.Map;
        switch (mission)
        {
            case NavalMission.BlockadeMission blockade:
                if ((uint)blockade.Target.Value >= (uint)_state.Definition.Countries.Count)
                {
                    return NavalOrderRefusal.NoSuchCountry;
                }

                if (blockade.Target == _country)
                {
                    return NavalOrderRefusal.BlockadeOwnCountry;
                }

                if (_state.GetCountryCapital(blockade.Target) is not { } capital)
                {
                    return NavalOrderRefusal.TargetHasNoCapital;
                }

                return ArmyMoveSimulation.AdjacentSeaZones(map, capital).Contains(current)
                    ? null
                    : NavalOrderRefusal.CapitalNotOnThisSeaZone;

            case NavalMission.LandingMission landing:
                if ((uint)landing.Target.Value >= (uint)map.Provinces.Count)
                {
                    return NavalOrderRefusal.UnknownProvince;
                }

                if (_state.GetProvinceOwner(landing.Target) == _country)
                {
                    return NavalOrderRefusal.LandingOnOwnProvince;
                }

                return ArmyMoveSimulation.CoastalZones(map, landing.Target).Contains(current)
                    ? null
                    : NavalOrderRefusal.ProvinceNotOnThisSeaZone;

            case NavalMission.MoveMission move:
                if ((uint)move.Destination.Value >= (uint)map.SeaZones.Count)
                {
                    return NavalOrderRefusal.NoSuchSeaZone;
                }

                if (move.Destination == current)
                {
                    return NavalOrderRefusal.AlreadyInThatSeaZone;
                }

                var distance = SeaZoneDistance(map.SeaTopology, current, move.Destination);
                return distance is { } zones && zones <= Reach(fleets) ? null : NavalOrderRefusal.OutOfRange;

            default:
                return null;
        }
    }

    private long Reach(IReadOnlyList<FleetId> fleets) =>
        fleets.Min(id => _state.Definition.ShipTypes[_state.GetFleet(id).Type.Value].SeaZones);

    /// <summary>Sea zones between two zones by breadth-first search, or null when no path joins them.</summary>
    internal static long? SeaZoneDistance(SeaZoneTopology topology, SeaZoneId from, SeaZoneId to)
    {
        var distance = new Dictionary<SeaZoneId, long> { [from] = 0 };
        var frontier = new Queue<SeaZoneId>([from]);
        while (frontier.TryDequeue(out var current))
        {
            if (current == to)
            {
                return distance[current];
            }

            foreach (var next in topology.GetNeighbors(current))
            {
                if (distance.TryAdd(next, distance[current] + 1))
                {
                    frontier.Enqueue(next);
                }
            }
        }

        return null;
    }
}

public sealed partial class OrdersPreview
{
    /// <summary>The fleet screen: fleets, task forces and the drafted naval orders.</summary>
    public FleetView Fleet { get; private init; } = null!;

    private static FleetView PreviewFleet(CompiledWorldPackage package, WorldState state, OrdersDraft draft)
    {
        var world = state.Definition;
        var turnNumber = state.CompletedTurnCount + 1;
        var fleets = state.Fleets
            .Where(fleet => fleet.Country == draft.Country)
            .OrderBy(static fleet => fleet.Id.Value)
            .Select(fleet =>
            {
                var hull = world.ShipTypes[fleet.Type.Value];
                return new FleetRowView(
                    fleet.Id,
                    fleet.Type.Value,
                    hull.Name,
                    fleet.Count,
                    hull.Cargo * fleet.Count,
                    hull.SeaZones,
                    fleet.SeaZone,
                    fleet.SeaZone is { } zone ? package.Catalog.GetKey(zone) : null,
                    fleet.TaskForce);
            })
            .ToArray();
        var forces = state.TaskForces
            .Where(force => force.Country == draft.Country)
            .Select(force =>
            {
                var members = force.Fleets.Select(state.GetFleet).ToArray();
                return new TaskForceRowView(
                    force.Id,
                    force.SeaZone,
                    package.Catalog.GetKey(force.SeaZone),
                    force.Activity,
                    force.BlockadeTarget is { } blockaded ? package.Catalog.GetKey(blockaded) : null,
                    force.LandingTarget is { } landed ? package.Catalog.GetKey(landed) : null,
                    force.LandingSince,
                    force.LandingSince < turnNumber,
                    force.Fleets,
                    members.Sum(static fleet => fleet.Count),
                    members.Sum(fleet => world.ShipTypes[fleet.Type.Value].Cargo * fleet.Count),
                    members.Min(fleet => world.ShipTypes[fleet.Type.Value].SeaZones));
            })
            .ToArray();
        return new FleetView(
            turnNumber,
            state.IsEliminated(draft.Country),
            Array.AsReadOnly(fleets),
            Array.AsReadOnly(forces),
            NavalOrderSimulation.Run(package, state, draft.Country, draft.NavalOrders));
    }
}
