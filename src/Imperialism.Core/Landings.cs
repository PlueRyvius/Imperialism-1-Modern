namespace Imperialism.Core;

/// <summary>
/// Row 6d: carrying regiments across water to a landing site (task-force state 5). One
/// instance lives for one turn's attacks, holding each site's spent capacity and whether
/// its interception battle has been fought.
/// </summary>
/// <remarks>
/// <b>Three parts are placeholders until a PC trace</b> (<c>wanted-values.md</c> §8): the
/// capacity reading (the arms in each hull's build bill), drowning, and the interception
/// battle. See <c>docs/formulas/army.md</c>, rules 31 onwards, and
/// <c>docs/formulas/naval.md</c>, rules 29 onwards.
/// </remarks>
internal sealed class LandingPlanner
{
    private readonly WorldState _state;
    private readonly int _turnNumber;
    private readonly ulong _seed;
    private readonly INavalBattleResolver _resolver;
    private readonly NavalWarPlanner.Ordinals _ordinals;
    private readonly Dictionary<TaskForceId, long> _used = [];
    private readonly HashSet<TaskForceId> _engaged = [];
    private readonly CommodityId? _arms;

    public LandingPlanner(
        WorldState state,
        int turnNumber,
        ulong seed,
        INavalBattleResolver resolver,
        NavalWarPlanner.Ordinals ordinals)
    {
        _state = state;
        _turnNumber = turnNumber;
        _seed = seed;
        _resolver = resolver;
        _ordinals = ordinals;
        _arms = ArmsCommodity(state.Definition.ArmyTypes);
    }

    /// <summary>
    /// The country's lowest-id task force holding a landing site on the province, established
    /// on an earlier turn and still on a sea zone the province touches; null when none is.
    /// "You cannot move land forces on the turn the landing site is established."
    /// </summary>
    public TaskForceState? ReadySite(CountryId country, ProvinceId destination) =>
        _state.TaskForces
            .Where(force => force.Country == country &&
                            force.Activity == TaskForceActivity.Landing &&
                            force.LandingTarget == destination &&
                            force.LandingSince < _turnNumber &&
                            NavalPlanner.ProvinceTouchesSeaZone(_state.Definition.Map, destination, force.SeaZone))
            .OrderBy(static force => force.Id.Value)
            .FirstOrDefault();

    /// <summary>The sea zones any cell of a province touches, in id order.</summary>
    public static IReadOnlyList<SeaZoneId> CoastalZones(MapDefinition map, ProvinceId province) =>
        map.GetCells(province)
            .SelectMany(cell => PortAccess.AdjacentSeaZones(map, cell))
            .Distinct()
            .OrderBy(static zone => zone.Value)
            .ToArray();

    /// <summary>
    /// Whether any sea zone the source province touches reaches the site's zone. No distance
    /// limit (chosen, army rule 32): the fleet already sailed there.
    /// </summary>
    public static bool HasSeaPath(WorldState state, ProvinceId from, SeaZoneId siteZone)
    {
        var map = state.Definition.Map;
        return CoastalZones(map, from)
            .Any(zone => NavalPlanner.SeaZoneDistance(map.SeaTopology, zone, siteZone) is not null);
    }

    /// <summary>A landing's load: the regiments' armaments, a type with none (a General) counting 0.</summary>
    public static long Load(IEnumerable<ArmyTypeDefinition> types) =>
        types.Aggregate(0L, static (total, type) => checked(total + (type.Arms ?? 0)));

    /// <summary>What the site can still carry this turn: its live capacity less what earlier landings spent.</summary>
    public long Available(TaskForceState site) =>
        Math.Max(0, Capacity(site) - _used.GetValueOrDefault(site.Id));

    /// <summary>
    /// A site's capacity: Σ over its live fleets of count × the armaments in the hull's build
    /// bill — the owner's reading that a warship's arms set "the force size that can be landed
    /// at a beachhead on hostile soil in one turn". Merchants carry none. A placeholder.
    /// </summary>
    public long Capacity(TaskForceState site)
    {
        if (_arms is not { } arms || !_state.TryGetTaskForce(site.Id, out var live))
        {
            return 0;
        }

        var total = 0L;
        foreach (var id in live.Fleets)
        {
            if (!_state.TryGetFleet(id, out var fleet))
            {
                continue;
            }

            var perHull = _state.Definition.ShipTypes[fleet.Type.Value].BuildCost
                .Where(item => item.Commodity == arms)
                .Sum(static item => item.Quantity);
            total = checked(total + (fleet.Count * perHull));
        }

        return total;
    }

    /// <summary>
    /// Carries an accepted landing across: the site's interception battle if this is its first
    /// landing this turn, then drowning against what the site can still carry. Returns the
    /// regiments that reach the beach, in id order; empty when the landing does not happen.
    /// </summary>
    public Regiment[] Land(
        CountryId country,
        RegimentMoveOrder order,
        TaskForceState site,
        Regiment[] attackers,
        long load,
        List<TurnEvent> events)
    {
        var from = attackers[0].Province;
        var zone = site.SeaZone;
        if (_engaged.Add(site.Id))
        {
            Intercept(country, site, zone, events);
        }

        var standing = _state.TryGetTaskForce(site.Id, out var live) && live.Activity == TaskForceActivity.Landing;
        var available = standing ? Available(site) : 0;

        // Drowning (army rule 34, a placeholder): from the end of the roster until the rest fit.
        var kept = attackers.Length;
        var drownedLoad = 0L;
        while (kept > 0 && load - drownedLoad > available)
        {
            kept--;
            drownedLoad = checked(drownedLoad + (TypeOf(attackers[kept]).Arms ?? 0));
        }

        if (!standing)
        {
            kept = 0;
        }

        if (kept < attackers.Length)
        {
            var drowned = attackers[kept..];
            foreach (var regiment in drowned)
            {
                _ = _state.RemoveRegiment(regiment.Id);
            }

            events.Add(new RegimentsDrownedEvent(
                _turnNumber, country, site.Id, drowned.Select(static regiment => regiment.Id), order.Destination));
        }

        if (kept == 0)
        {
            return [];
        }

        var landed = attackers[..kept];
        var used = Load(landed.Select(TypeOf));
        _used[site.Id] = checked(_used.GetValueOrDefault(site.Id) + used);
        events.Add(new RegimentsLandedEvent(
            _turnNumber,
            country,
            site.Id,
            landed.Select(static regiment => regiment.Id),
            from,
            order.Destination,
            used,
            Available(site)));
        return landed;
    }

    /// <summary>
    /// Naval rule 31: every effectively hostile country with task forces in the site's zone,
    /// whatever they are doing, attacks the site's own task forces there, one country at a time
    /// in id order, until the site is gone. 6c's <c>Fight</c>, naval salt and shared ordinals.
    /// </summary>
    private void Intercept(CountryId country, TaskForceState site, SeaZoneId zone, List<TurnEvent> events)
    {
        var hostiles = _state.TaskForces
            .Where(force => force.SeaZone == zone && force.Country != country)
            .Select(static force => force.Country)
            .Distinct()
            .Where(other => _state.HasEffectiveHostility(country, other))
            .OrderBy(static other => other.Value)
            .ToArray();
        foreach (var hostile in hostiles)
        {
            if (!_state.TryGetTaskForce(site.Id, out var live) || live.Activity != TaskForceActivity.Landing)
            {
                return;
            }

            var attackForces = NavalWarPlanner.ForcesIn(_state, hostile, zone);
            var defendForces = NavalWarPlanner.ForcesIn(_state, country, zone);
            if (attackForces.Length == 0 || defendForces.Length == 0)
            {
                continue;
            }

            var fought = NavalWarPlanner.Fight(
                _state,
                NavalBattleKind.Landing,
                zone,
                hostile,
                attackForces.SelectMany(static force => force.Fleets).ToArray(),
                country,
                defendForces.SelectMany(static force => force.Fleets).ToArray(),
                _turnNumber,
                _seed,
                _resolver,
                _ordinals,
                out var attackerWon);
            if (fought is null)
            {
                continue;
            }

            events.Add(fought);
            NavalWarPlanner.StandDown(_state, attackerWon ? defendForces : attackForces);
        }
    }

    private ArmyTypeDefinition TypeOf(Regiment regiment) => _state.Definition.ArmyTypes[regiment.Type.Value];

    /// <summary>
    /// Core has no commodity lookup by key, and the Armoury charges a regiment's bill without
    /// naming armaments. So the armaments commodity is the one every purchasable armed type's
    /// bill holds in exactly its <see cref="ArmyTypeDefinition.Arms"/> quantity; null when that
    /// does not single one out, and then no site carries anything.
    /// </summary>
    internal static CommodityId? ArmsCommodity(IReadOnlyList<ArmyTypeDefinition> types)
    {
        HashSet<CommodityId>? candidates = null;
        foreach (var type in types)
        {
            if (type.Purchase is not { } purchase || type.Arms is not > 0)
            {
                continue;
            }

            var matching = purchase.Bill
                .Where(item => item.Quantity == type.Arms)
                .Select(static item => item.Commodity);
            if (candidates is null)
            {
                candidates = [.. matching];
            }
            else
            {
                candidates.IntersectWith(matching);
            }
        }

        return candidates is { Count: 1 } ? candidates.Single() : null;
    }
}
