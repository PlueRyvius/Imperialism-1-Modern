namespace Imperialism.Core;

/// <summary>One ship as a naval battle sees it: a fleet of N ships gives N of these.</summary>
internal readonly record struct NavalCombatant(FleetId Fleet, ShipCombatStats Stats);

/// <summary>Which ships were sunk, in the order the rosters were given.</summary>
internal sealed record NavalBattleResult(
    bool AttackerWon,
    int Rounds,
    IReadOnlyList<bool> AttackersSunk,
    IReadOnlyList<bool> DefendersSunk);

/// <summary>
/// Resolves one naval battle. Behind an interface like <see cref="IBattleResolver"/>,
/// because the shipped rule is a placeholder: the executable's task-force interaction
/// routine at <c>0x005578A0</c> is not yet traced.
/// </summary>
internal interface INavalBattleResolver
{
    NavalBattleResult Resolve(
        IReadOnlyList<NavalCombatant> attackers,
        IReadOnlyList<NavalCombatant> defenders,
        IBattleRandom random);
}

/// <summary>
/// A <b>placeholder</b> naval resolver in the shape of the land battle loop.
/// </summary>
/// <remarks>
/// Borrowed from row 6b's recovered land loop: the health snapshot, "both sides hold a
/// ship above half its snapshot", attacker first, the 1-or-2 weight, the draw modulo 7
/// and the scale array [70, 80, 90, 90]. <b>Everything naval is chosen</b>: health is the
/// class's hull scale, shot = weight × multiplier × firepower × scale, damage =
/// max(1, shot × (100 − armour) / 100), the opening volley by range, and the 20-round
/// stop as a defender win. See <c>docs/formulas/naval.md</c>, chosen rules 13–15.
/// </remarks>
internal sealed class StrategicNavalBattleResolver : INavalBattleResolver
{
    public static readonly StrategicNavalBattleResolver Instance = new();

    /// <summary>A chosen stop. A battle stopped here is the defender's.</summary>
    public const int MaxRounds = 20;

    private static readonly long[] Scale = [70, 80, 90, 90];

    public NavalBattleResult Resolve(
        IReadOnlyList<NavalCombatant> attackers,
        IReadOnlyList<NavalCombatant> defenders,
        IBattleRandom random)
    {
        ArgumentNullException.ThrowIfNull(attackers);
        ArgumentNullException.ThrowIfNull(defenders);
        ArgumentNullException.ThrowIfNull(random);

        var attackHealth = attackers.Select(static ship => Math.Max(1, ship.Stats.HullScale)).ToArray();
        var defendHealth = defenders.Select(static ship => Math.Max(1, ship.Stats.HullScale)).ToArray();
        var attackSnapshot = attackHealth.ToArray();
        var defendSnapshot = defendHealth.ToArray();

        // "Superior range is the most important statistic": in the opening round only a
        // ship that outranges or matches the enemy's longest gun fires (chosen rule 14).
        var attackReach = defenders.Count == 0 ? 0 : defenders.Max(static ship => ship.Stats.Range);
        var defendReach = attackers.Count == 0 ? 0 : attackers.Max(static ship => ship.Stats.Range);

        var rounds = 0;
        var stopped = false;
        while (HasEligible(attackHealth, attackSnapshot) && HasEligible(defendHealth, defendSnapshot))
        {
            if (rounds == MaxRounds)
            {
                stopped = true;
                break;
            }

            Volley(attackers, attackHealth, attackSnapshot, defendHealth, defenders, rounds, attackReach, random);
            Volley(defenders, defendHealth, defendSnapshot, attackHealth, attackers, rounds, defendReach, random);
            rounds++;
        }

        var attackerWon = !stopped && HasEligible(attackHealth, attackSnapshot);
        return new NavalBattleResult(
            attackerWon,
            rounds,
            Array.AsReadOnly(attackHealth.Select(static health => health <= 0).ToArray()),
            Array.AsReadOnly(defendHealth.Select(static health => health <= 0).ToArray()));
    }

    private static bool HasEligible(long[] health, long[] snapshot)
    {
        for (var index = 0; index < health.Length; index++)
        {
            if (health[index] > snapshot[index] / 2)
            {
                return true;
            }
        }

        return false;
    }

    private static void Volley(
        IReadOnlyList<NavalCombatant> firers,
        long[] firerHealth,
        long[] firerSnapshot,
        long[] targetHealth,
        IReadOnlyList<NavalCombatant> targets,
        int round,
        long enemyReach,
        IBattleRandom random)
    {
        var live = new List<int>(targetHealth.Length);
        for (var index = 0; index < firers.Count; index++)
        {
            var firer = firers[index].Stats;

            // A merchant never fires, and in the opening round neither does a ship the
            // enemy outranges. Neither consumes a draw.
            if (firerHealth[index] <= 0 || firer.Firepower == 0 || (round == 0 && firer.Range < enemyReach))
            {
                continue;
            }

            live.Clear();
            for (var target = 0; target < targetHealth.Length; target++)
            {
                if (targetHealth[target] > 0)
                {
                    live.Add(target);
                }
            }

            if (live.Count == 0)
            {
                return;
            }

            var weight = firerHealth[index] > firerSnapshot[index] / 2 ? 2 : 1;
            var multiplier = 1 + (long)(random.Next() % 7);
            var scale = Scale[Math.Min(round, Scale.Length - 1)];
            var shot = checked(weight * multiplier * firer.Firepower * scale);
            var chosen = live[(int)(random.Next() % (ulong)live.Count)];
            var armour = Math.Clamp(targets[chosen].Stats.Armour, 0, 100);
            var damage = Math.Max(1, shot * (100 - armour) / 100);
            targetHealth[chosen] = Math.Max(0, targetHealth[chosen] - damage);
        }
    }
}

/// <summary>
/// Naval battles in <c>Conflict</c>, and interception with its escort battles in
/// <c>TradeCancellation</c>. Every rule here beyond the manual's prose is chosen and
/// labelled in <c>docs/formulas/naval.md</c>, rules 11–28, and the landing rules from 29.
/// </summary>
internal static class NavalWarPlanner
{
    /// <summary>Salts the turn seed for naval battles, so they never share land's streams (chosen rule 28).</summary>
    public const ulong BattleSalt = 0x4E4156414C424154UL; // "NAVALBAT"

    /// <summary>Salts the turn seed for interception draws, one stream per deal (chosen rule 28).</summary>
    public const ulong InterceptionSalt = 0x494E544552434550UL; // "INTERCEP"

    /// <summary>A patrol's chance, in percent, to intercept a deal at a capital it lies beside (chosen rule 22).</summary>
    public const int PatrolChance = 25;

    /// <summary>A blockade's chance, in percent, to intercept a deal of the capital it blockades (chosen rule 22).</summary>
    public const int BlockadeChance = 50;

    /// <summary>Hands out naval battle ordinals across both phases of one turn.</summary>
    internal sealed class Ordinals
    {
        private int _next;

        public int Next() => _next++;
    }

    /// <summary>
    /// The <c>Conflict</c> battles: zones in id order, then effectively hostile pairs
    /// a &lt; b with task forces there, fought when at least one side patrols — or when
    /// one side arrived by a move this turn and the other holds a landing site there
    /// (naval rule 30). Pairs resolve against live state, so ships sunk in one are gone
    /// for the next, and each pair fights at most once in this step.
    /// </summary>
    /// <param name="arrived">Task forces that arrived by a move this turn; turn-local.</param>
    public static IReadOnlyList<TurnEvent> ResolveBattles(
        WorldState state,
        int turnNumber,
        ulong seed,
        INavalBattleResolver resolver,
        Ordinals ordinals,
        IReadOnlySet<TaskForceId>? arrived = null)
    {
        arrived ??= new HashSet<TaskForceId>();
        var events = new List<TurnEvent>();
        var zones = state.TaskForces
            .Select(static force => force.SeaZone)
            .Distinct()
            .OrderBy(static zone => zone.Value)
            .ToArray();
        foreach (var zone in zones)
        {
            var countries = state.TaskForces
                .Where(force => force.SeaZone == zone)
                .Select(static force => force.Country)
                .Distinct()
                .OrderBy(static country => country.Value)
                .ToArray();
            for (var first = 0; first < countries.Length; first++)
            {
                for (var second = first + 1; second < countries.Length; second++)
                {
                    var a = countries[first];
                    var b = countries[second];
                    if (!state.HasEffectiveHostility(a, b))
                    {
                        continue;
                    }

                    var forcesA = ForcesIn(state, a, zone);
                    var forcesB = ForcesIn(state, b, zone);
                    if (forcesA.Length == 0 || forcesB.Length == 0)
                    {
                        continue;
                    }

                    // Only a patrol seeks battle; two blockades or idle forces pass. The
                    // manual's one exception: a task force sailing into a zone holding a
                    // hostile landing site intercepts it the turn it arrives (1889–1891).
                    var patrolA = forcesA.Any(static force => force.Activity == TaskForceActivity.Patrolling);
                    var patrolB = forcesB.Any(static force => force.Activity == TaskForceActivity.Patrolling);
                    var kind = NavalBattleKind.Patrol;
                    bool aAttacks;
                    if (patrolA || patrolB)
                    {
                        aAttacks = patrolA;
                    }
                    else
                    {
                        var interceptA = forcesA.Any(force => arrived.Contains(force.Id)) && HoldsLanding(forcesB);
                        var interceptB = forcesB.Any(force => arrived.Contains(force.Id)) && HoldsLanding(forcesA);
                        if (!interceptA && !interceptB)
                        {
                            continue;
                        }

                        // Both arrived and both hold sites: the lower id attacks (chosen).
                        kind = NavalBattleKind.Landing;
                        aAttacks = interceptA;
                    }

                    var (attacker, attackForces, defender, defendForces) = aAttacks
                        ? (a, forcesA, b, forcesB)
                        : (b, forcesB, a, forcesA);
                    var fought = Fight(
                        state,
                        kind,
                        zone,
                        attacker,
                        attackForces.SelectMany(static force => force.Fleets).ToArray(),
                        defender,
                        defendForces.SelectMany(static force => force.Fleets).ToArray(),
                        turnNumber,
                        seed,
                        resolver,
                        ordinals,
                        out var attackerWon);
                    if (fought is null)
                    {
                        continue;
                    }

                    events.Add(fought);
                    StandDown(state, attackerWon ? defendForces : attackForces);
                }
            }
        }

        return events;
    }

    /// <summary>
    /// The <c>TradeCancellation</c> phase: each deal in the order the market settled it,
    /// tried by each eligible interceptor in task-force-id order until one succeeds.
    /// </summary>
    public static IReadOnlyList<TurnEvent> ResolveInterception(
        WorldState state,
        IReadOnlyList<(PlannedTrade Deal, DeliveryId Delivery)> deals,
        int turnNumber,
        ulong seed,
        INavalBattleResolver resolver,
        Ordinals ordinals)
    {
        var events = new List<TurnEvent>();
        if (deals.Count == 0 || state.TaskForces.Count == 0)
        {
            return events;
        }

        var map = state.Definition.Map;
        var beaten = new HashSet<TaskForceId>();
        var escorted = new HashSet<(TaskForceId, CountryId)>();
        for (var index = 0; index < deals.Count; index++)
        {
            var (deal, delivery) = deals[index];
            var carrier = deal.HoldsPaidBy;
            var sellerCapital = state.GetCountryCapital(deal.Seller);
            var buyerCapital = state.GetCountryCapital(deal.Buyer);

            // The route runs capital to capital and is exposed only at its two ends
            // (chosen rule 20). An inland capital exposes nothing.
            var exposed = new HashSet<SeaZoneId>();
            if (sellerCapital is { } sellerCell)
            {
                exposed.UnionWith(PortAccess.AdjacentSeaZones(map, sellerCell));
            }

            if (buyerCapital is { } buyerCell)
            {
                exposed.UnionWith(PortAccess.AdjacentSeaZones(map, buyerCell));
            }

            IBattleRandom? draws = null;
            foreach (var force in state.TaskForces.ToArray())
            {
                if (beaten.Contains(force.Id) || !state.TryGetTaskForce(force.Id, out _))
                {
                    continue;
                }

                // The carrier, not the buyer or seller, is the one that must be at war
                // with the interceptor (chosen rule 21).
                if (force.Country == carrier || !state.HasEffectiveHostility(force.Country, carrier))
                {
                    continue;
                }

                var chance = Chance(state, force, deal, exposed);
                if (chance == 0)
                {
                    continue;
                }

                // The first attempt on a carrier meets its escorts, once per
                // (task force, carrier) this turn (chosen rule 24).
                if (escorted.Add((force.Id, carrier)) && Escorts(state, carrier) is { Length: > 0 } escorts)
                {
                    var fought = Fight(
                        state,
                        NavalBattleKind.Escort,
                        force.SeaZone,
                        force.Country,
                        force.Fleets.ToArray(),
                        carrier,
                        escorts,
                        turnNumber,
                        seed,
                        resolver,
                        ordinals,
                        out var interceptorWon);
                    if (fought is not null)
                    {
                        events.Add(fought);
                        if (!interceptorWon)
                        {
                            _ = beaten.Add(force.Id);
                            if (state.TryGetTaskForce(force.Id, out var survivor))
                            {
                                StandDown(state, [survivor]);
                            }

                            continue;
                        }
                    }
                }

                draws ??= SplitMix64.ForBattle(seed ^ InterceptionSalt, index);
                if ((int)(draws.Next() % 100) >= chance)
                {
                    continue;
                }

                var outcome = (InterceptionOutcome)(draws.Next() % 3);
                var mission = force.Activity;
                _ = state.CancelPendingDelivery(delivery);
                ShipTypeId? hullSunk = null;
                switch (outcome)
                {
                    case InterceptionOutcome.TurnedBack:
                        _ = state.QueuePendingDelivery(
                            deal.Seller, deal.Commodity, deal.Quantity, PendingDeliverySource.Trade);
                        break;
                    case InterceptionOutcome.Captured:
                        _ = state.QueuePendingDelivery(
                            force.Country, deal.Commodity, deal.Quantity, PendingDeliverySource.Trade);
                        break;
                    case InterceptionOutcome.Sunk:
                        hullSunk = SinkMerchant(state, carrier);
                        break;
                }

                // Settled money stands (chosen rule 26): nothing here touches cash.
                events.Add(new MerchantInterceptedEvent(
                    turnNumber,
                    force.Country,
                    force.Id,
                    carrier,
                    deal.Seller,
                    deal.Buyer,
                    deal.Commodity,
                    deal.Quantity,
                    outcome,
                    mission,
                    hullSunk));
                break;
            }
        }

        return events;
    }

    /// <summary>Whether a class is a merchant: it carries cargo and never fires.</summary>
    internal static bool IsMerchant(ShipTypeDefinition type) =>
        type.Cargo > 0 && (type.Combat?.Firepower ?? 0) == 0;

    private static bool HoldsLanding(TaskForceState[] forces) =>
        forces.Any(static force => force.Activity == TaskForceActivity.Landing);

    internal static TaskForceState[] ForcesIn(WorldState state, CountryId country, SeaZoneId zone) =>
        state.TaskForces.Where(force => force.Country == country && force.SeaZone == zone).ToArray();

    private static int Chance(
        WorldState state, TaskForceState force, PlannedTrade deal, HashSet<SeaZoneId> exposed)
    {
        switch (force.Activity)
        {
            case TaskForceActivity.Patrolling:
                return exposed.Contains(force.SeaZone) ? PatrolChance : 0;
            case TaskForceActivity.Blockading:
                if (force.BlockadeTarget is not { } target || (target != deal.Seller && target != deal.Buyer))
                {
                    return 0;
                }

                // A blockade acts on the target's capital as it stands now: a capital
                // lost or moved since the order leaves the blockade on open water.
                return state.GetCountryCapital(target) is { } capital &&
                    PortAccess.AdjacentSeaZones(state.Definition.Map, capital).Contains(force.SeaZone)
                    ? BlockadeChance
                    : 0;
            default:
                return 0;
        }
    }

    /// <summary>
    /// A carrier's escorts: its warship fleets in no task force, lying in a zone that
    /// touches its capital (chosen rule 24). Damage is not modelled, so "fully
    /// repaired" is every ship.
    /// </summary>
    private static FleetId[] Escorts(WorldState state, CountryId carrier)
    {
        if (state.GetCountryCapital(carrier) is not { } capital)
        {
            return [];
        }

        var home = PortAccess.AdjacentSeaZones(state.Definition.Map, capital).ToHashSet();
        return state.Fleets
            .Where(fleet => fleet.Country == carrier &&
                fleet.TaskForce is null &&
                fleet.SeaZone is { } zone && home.Contains(zone) &&
                (state.Definition.ShipTypes[fleet.Type.Value].Combat?.Firepower ?? 0) > 0)
            .Select(static fleet => fleet.Id)
            .ToArray();
    }

    /// <summary>
    /// One merchant hull lost: from the carrier's merchant fleet with the lowest id that
    /// has a ship (chosen rule 27), or else from the abstract hulls the fair start gives
    /// without a fleet record, in class order.
    /// </summary>
    private static ShipTypeId? SinkMerchant(WorldState state, CountryId carrier)
    {
        var types = state.Definition.ShipTypes;
        foreach (var fleet in state.Fleets.OrderBy(static fleet => fleet.Id.Value))
        {
            if (fleet.Country == carrier && IsMerchant(types[fleet.Type.Value]))
            {
                var type = fleet.Type;
                state.SinkShips(fleet.Id, 1);
                return type;
            }
        }

        for (var index = 0; index < types.Count; index++)
        {
            var type = new ShipTypeId(index);
            if (!IsMerchant(types[index]))
            {
                continue;
            }

            var recorded = state.Fleets
                .Where(fleet => fleet.Country == carrier && fleet.Type == type)
                .Sum(static fleet => fleet.Count);
            if (state.GetShipCount(carrier, type) > recorded)
            {
                state.SinkAbstractShip(carrier, type);
                return type;
            }
        }

        return null;
    }

    /// <summary>A beaten side's surviving task forces go idle; a broken patrol or blockade (chosen rule 17).</summary>
    internal static void StandDown(WorldState state, IEnumerable<TaskForceState> forces)
    {
        foreach (var force in forces)
        {
            if (state.TryGetTaskForce(force.Id, out var live))
            {
                live.Activity = TaskForceActivity.Idle;
            }
        }
    }

    /// <summary>
    /// Fights one battle between two sets of fleets and sinks the losses. Returns null,
    /// fighting nothing, when either side has no ship with combat stats.
    /// </summary>
    internal static NavalBattleFoughtEvent? Fight(
        WorldState state,
        NavalBattleKind kind,
        SeaZoneId zone,
        CountryId attacker,
        IReadOnlyList<FleetId> attackFleets,
        CountryId defender,
        IReadOnlyList<FleetId> defendFleets,
        int turnNumber,
        ulong seed,
        INavalBattleResolver resolver,
        Ordinals ordinals,
        out bool attackerWon)
    {
        attackerWon = false;
        var attackers = Combatants(state, attackFleets);
        var defenders = Combatants(state, defendFleets);
        if (attackers.Length == 0 || defenders.Length == 0)
        {
            return null;
        }

        var result = resolver.Resolve(attackers, defenders, SplitMix64.ForBattle(seed ^ BattleSalt, ordinals.Next()));
        attackerWon = result.AttackerWon;
        var participants = new List<NavalBattleParticipant>();
        var sinkings = new List<(FleetId Fleet, long Count)>();
        Tally(state, attackers, result.AttackersSunk, BattleSide.Attacker, participants, sinkings);
        Tally(state, defenders, result.DefendersSunk, BattleSide.Defender, participants, sinkings);
        foreach (var (fleet, count) in sinkings)
        {
            state.SinkShips(fleet, count);
        }

        return new NavalBattleFoughtEvent(
            turnNumber,
            kind,
            zone,
            attacker,
            defender,
            participants,
            attackerWon ? attacker : defender,
            result.Rounds);
    }

    /// <summary>
    /// One combatant per ship, fleets in id order. A class with no combat stats stands
    /// aside, as a General does on land: it has nothing to fight with or be hit by.
    /// </summary>
    private static NavalCombatant[] Combatants(WorldState state, IReadOnlyList<FleetId> fleets)
    {
        var combatants = new List<NavalCombatant>();
        foreach (var id in fleets.OrderBy(static fleet => fleet.Value))
        {
            if (!state.TryGetFleet(id, out var fleet) ||
                state.Definition.ShipTypes[fleet.Type.Value].Combat is not { } stats)
            {
                continue;
            }

            for (var ship = 0L; ship < fleet.Count; ship++)
            {
                combatants.Add(new NavalCombatant(id, stats));
            }
        }

        return [.. combatants];
    }

    private static void Tally(
        WorldState state,
        NavalCombatant[] combatants,
        IReadOnlyList<bool> sunk,
        BattleSide side,
        List<NavalBattleParticipant> participants,
        List<(FleetId Fleet, long Count)> sinkings)
    {
        foreach (var group in combatants.Select((ship, index) => (ship.Fleet, Sunk: sunk[index])).GroupBy(static item => item.Fleet))
        {
            var before = group.LongCount();
            var lost = group.LongCount(static item => item.Sunk);
            participants.Add(new NavalBattleParticipant(
                group.Key, state.GetFleet(group.Key).Type, side, before, lost));
            if (lost > 0)
            {
                sinkings.Add((group.Key, lost));
            }
        }
    }
}
