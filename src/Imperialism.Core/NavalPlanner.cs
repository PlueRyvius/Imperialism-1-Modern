namespace Imperialism.Core;

/// <summary>
/// Turns declarations of war and fleet orders into calls on the naval state that
/// already existed — <see cref="WorldState.SetRelationMode"/>,
/// <see cref="WorldState.AssembleTaskForce"/>, <see cref="WorldState.PatrolTaskForce"/>,
/// <see cref="WorldState.PlanTaskForceMove"/> and
/// <see cref="WorldState.ResolveTaskForceMoves"/>.
/// </summary>
/// <remarks>
/// <b>Player input never throws here.</b> Every condition those methods throw on is
/// checked first and refused with an event, which is a chosen rule and the reason this
/// class exists rather than the resolver calling them directly. See
/// <c>docs/formulas/naval.md</c>.
/// </remarks>
internal static class NavalPlanner
{
    /// <summary>
    /// The <c>Diplomacy</c> phase: countries in id order, declarations in list order.
    /// </summary>
    /// <remarks>
    /// Nothing needs to wait: the relation token stamped now is what makes the war begin
    /// next turn, because <see cref="WorldState.HasEffectiveHostility"/> ignores a mode
    /// stamped in the current generation.
    /// </remarks>
    public static IReadOnlyList<TurnEvent> ResolveDiplomacy(
        WorldState state, TurnOrders orders, int turnNumber)
    {
        var events = new List<TurnEvent>();
        var countryCount = state.Definition.Countries.Count;
        for (var index = 0; index < countryCount; index++)
        {
            var country = new CountryId(index);
            var declared = new HashSet<CountryId>();
            foreach (var target in orders[country].DeclareWar)
            {
                var refusal =
                    target.Value >= countryCount ? WarDeclarationRefusal.NoSuchCountry
                    : target == country ? WarDeclarationRefusal.Itself
                    : declared.Contains(target) ? WarDeclarationRefusal.AlreadyDeclaredThisTurn
                    : state.GetRelationMode(country, target) == CountryRelationMode.Hostile
                        ? WarDeclarationRefusal.AlreadyAtWar
                        : (WarDeclarationRefusal?)null;
                if (refusal is { } reason)
                {
                    events.Add(new WarDeclarationRefusedEvent(turnNumber, country, target, reason));
                    continue;
                }

                state.SetRelationMode(country, target, CountryRelationMode.Hostile);
                _ = declared.Add(target);
                events.Add(new WarDeclaredEvent(turnNumber, country, target));

                // A declaration breaks a pact and, between Great Powers, closes the posts
                // both ways. Cut now; every reader plans against the state the turn
                // opened with, so the loss is first felt next turn, when the war begins.
                events.AddRange(DiplomacyPlanner.CutTiesForWar(state, country, target, turnNumber));
            }
        }

        return events;
    }

    /// <summary>
    /// The <c>Conflict</c> phase: countries in id order, orders in list order, and every
    /// sailing leg resolved once after all of them — a chosen rule.
    /// </summary>
    public static IReadOnlyList<TurnEvent> ResolveConflict(
        WorldState state, TurnOrders orders, int turnNumber)
    {
        var events = new List<TurnEvent>();
        var ordered = new HashSet<TaskForceId>();
        var requested = new Dictionary<TaskForceId, SeaZoneId>();
        for (var index = 0; index < state.Definition.Countries.Count; index++)
        {
            var country = new CountryId(index);
            foreach (var order in orders[country].Naval)
            {
                switch (order)
                {
                    case NavalOrder.AssembleOrder assemble:
                        Assemble(state, country, assemble, turnNumber, ordered, requested, events);
                        break;
                    case NavalOrder.CommandOrder command:
                        Command(state, country, command, turnNumber, ordered, requested, events);
                        break;
                    default:
                        throw new InvalidOperationException(
                            $"Unhandled naval order {order.GetType().Name}.");
                }
            }
        }

        // After every order, so no country's fleet arrives before another country has
        // given its orders. The resolver walks task forces in id order.
        foreach (var leg in state.ResolveTaskForceMoves())
        {
            var force = state.GetTaskForce(leg.TaskForce);
            events.Add(new TaskForceSailedEvent(
                turnNumber,
                force.Country,
                leg.TaskForce,
                leg.From,
                leg.To,
                requested.TryGetValue(leg.TaskForce, out var destination) ? destination : leg.To));
        }

        return events;
    }

    private static void Assemble(
        WorldState state,
        CountryId country,
        NavalOrder.AssembleOrder order,
        int turnNumber,
        HashSet<TaskForceId> ordered,
        Dictionary<TaskForceId, SeaZoneId> requested,
        List<TurnEvent> events)
    {
        var refusal = CheckAssembly(state, country, order.Fleets, out var seaZone);

        // All or nothing: a mission that would be refused stops the grouping too, so a
        // refused order never leaves a half-done task force behind.
        refusal ??= CheckMission(state, order.Mission, seaZone, order.Fleets);
        if (refusal is { } reason)
        {
            events.Add(new NavalOrderRefusedEvent(
                turnNumber, country, NavalOrderKind.Assemble, null, reason));
            return;
        }

        var force = state.AssembleTaskForce(country, order.Fleets);
        events.Add(new TaskForceAssembledEvent(
            turnNumber, country, force.Id, force.SeaZone, force.Fleets));
        Apply(state, country, force, order.Mission, turnNumber, ordered, requested, events);
    }

    private static void Command(
        WorldState state,
        CountryId country,
        NavalOrder.CommandOrder order,
        int turnNumber,
        HashSet<TaskForceId> ordered,
        Dictionary<TaskForceId, SeaZoneId> requested,
        List<TurnEvent> events)
    {
        NavalOrderRefusal? refusal = null;
        TaskForceState? force = null;
        if ((ulong)(order.TaskForce.Value - 1) >= (ulong)state.TaskForces.Count)
        {
            refusal = NavalOrderRefusal.NoSuchTaskForce;
        }
        else
        {
            force = state.GetTaskForce(order.TaskForce);
            if (force.Country != country)
            {
                refusal = NavalOrderRefusal.TaskForceNotYours;
            }
            else if (order.Mission is NavalMission.NoMission)
            {
                // Nothing to do, and nothing spent: a command with no mission leaves
                // the task force free for a real one later in the same list.
                return;
            }
            else if (ordered.Contains(force.Id) || force.PlannedSeaZone is not null)
            {
                refusal = NavalOrderRefusal.AlreadyOrderedThisTurn;
            }
            else
            {
                refusal = CheckMission(state, order.Mission, force.SeaZone, force.Fleets);
            }
        }

        if (refusal is { } reason)
        {
            events.Add(new NavalOrderRefusedEvent(
                turnNumber, country, NavalOrderKind.Command, order.TaskForce, reason));
            return;
        }

        Apply(state, country, force!, order.Mission, turnNumber, ordered, requested, events);
    }

    private static NavalOrderRefusal? CheckAssembly(
        WorldState state, CountryId country, IReadOnlyList<FleetId> fleets, out SeaZoneId seaZone)
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

        // The same checks AssembleTaskForce makes, in the same order, so the reason
        // reported is the one it would have thrown on.
        SeaZoneId? shared = null;
        foreach (var id in fleets.OrderBy(static fleet => fleet.Value))
        {
            if ((ulong)(id.Value - 1) >= (ulong)state.Fleets.Count)
            {
                return NavalOrderRefusal.NoSuchFleet;
            }

            var fleet = state.GetFleet(id);
            if (fleet.Country != country)
            {
                return NavalOrderRefusal.FleetNotYours;
            }

            if (fleet.SeaZone is not { } position)
            {
                return NavalOrderRefusal.FleetNotPlaced;
            }

            if (fleet.TaskForce is not null)
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

    private static NavalOrderRefusal? CheckMission(
        WorldState state, NavalMission mission, SeaZoneId current, IReadOnlyList<FleetId> fleets)
    {
        if (mission is not NavalMission.MoveMission move)
        {
            return null;
        }

        if ((uint)move.Destination.Value >= (uint)state.Definition.Map.SeaZones.Count)
        {
            return NavalOrderRefusal.NoSuchSeaZone;
        }

        if (move.Destination == current)
        {
            return NavalOrderRefusal.AlreadyInThatSeaZone;
        }

        // The manual's interface never lets a player give this order: "If the new zone
        // is out of range you see a red slashed circle cursor; you cannot move the f leet
        // that far in one turn." So an order beyond the slowest hull's reach, or to a zone
        // no sea path reaches, is refused rather than handed to the executable's clamp.
        var reach = fleets.Min(id => state.Definition.ShipTypes[state.GetFleet(id).Type.Value].SeaZones);
        var distance = SeaZoneDistance(state.Definition.Map.SeaTopology, current, move.Destination);
        return distance is { } zones && zones <= reach ? null : NavalOrderRefusal.OutOfRange;
    }

    /// <summary>Sea zones between two zones, or null when no sea path joins them.</summary>
    private static long? SeaZoneDistance(SeaZoneTopology topology, SeaZoneId from, SeaZoneId to)
    {
        var distance = new Dictionary<SeaZoneId, long> { [from] = 0 };
        var frontier = new Queue<SeaZoneId>();
        frontier.Enqueue(from);
        while (frontier.TryDequeue(out var current))
        {
            if (current == to)
            {
                return distance[current];
            }

            foreach (var neighbor in topology.GetNeighbors(current))
            {
                if (distance.TryAdd(neighbor, distance[current] + 1))
                {
                    frontier.Enqueue(neighbor);
                }
            }
        }

        return null;
    }

    private static void Apply(
        WorldState state,
        CountryId country,
        TaskForceState force,
        NavalMission mission,
        int turnNumber,
        HashSet<TaskForceId> ordered,
        Dictionary<TaskForceId, SeaZoneId> requested,
        List<TurnEvent> events)
    {
        switch (mission)
        {
            case NavalMission.NoMission:
                return;
            case NavalMission.PatrolMission:
                state.PatrolTaskForce(country, force.Id);
                events.Add(new TaskForcePatrolOrderedEvent(
                    turnNumber, country, force.Id, force.SeaZone));
                break;
            case NavalMission.MoveMission move:
                // The plan switches the task force out of patrol at once, and the leg
                // lands later in this phase; it arrives idle either way.
                _ = state.PlanTaskForceMove(country, force.Id, move.Destination);
                requested[force.Id] = move.Destination;
                break;
            default:
                throw new InvalidOperationException(
                    $"Unhandled naval mission {mission.GetType().Name}.");
        }

        _ = ordered.Add(force.Id);
    }
}
