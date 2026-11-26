using Imperialism.Content;
using Imperialism.Core;

namespace Imperialism.Presentation;

/// <summary>
/// One Armoury category as the turn opens: the best type it builds, that type's price,
/// and what Core would say to the drafted order.
/// </summary>
/// <param name="TypeIndex">The type the category builds, or null when it builds none.</param>
/// <param name="Unavailable">
/// Why one regiment of this category could not be bought before anything else is
/// counted — <see cref="RegimentPurchaseRefusal.NotPurchasable"/> or
/// <see cref="RegimentPurchaseRefusal.NoCapital"/> — or null.
/// </param>
/// <param name="Drafted">The drafted count; zero rows are not sent.</param>
/// <param name="Warning">
/// Core's refusal of the drafted order, or that Core sets it aside because the country is
/// eliminated; null if it is accepted.
/// </param>
/// <param name="WorkersTaken">
/// Workers of <see cref="WorkerGrade"/> the drafted purchase takes out of the workforce for
/// good — one per regiment, from <c>Production</c> on — or zero when it is refused or set
/// aside. Not labour: <see cref="ArmouryView.LabourWithheld"/> prices them.
/// </param>
public sealed record ArmouryCategoryView(
    ArmyCategory Category,
    int? TypeIndex,
    string? TypeName,
    WorkerGrade? WorkerGrade,
    IReadOnlyList<CommodityAmountView> Bill,
    long CashCost,
    long Arms,
    RegimentPurchaseRefusal? Unavailable,
    long Drafted,
    OrderWarning<RegimentPurchaseRefusal>? Warning,
    long WorkersTaken);

/// <summary>A regiment the drafting country holds, for the Garrison book.</summary>
/// <param name="DraftedDestinationKey">The province its drafted move names, if any.</param>
public sealed record RegimentView(
    RegimentId Id,
    int TypeIndex,
    string TypeName,
    ArmyCategory Category,
    string ProvinceKey,
    long Health,
    long MaxHealth,
    long Experience,
    long Medals,
    bool LeavesProvince,
    bool MovesByRail,
    long Arms,
    string? DraftedDestinationKey);

/// <summary>
/// The Armoury dialog and the Garrison book: every category, and every regiment held.
/// </summary>
/// <remarks>
/// The Armoury is planned in <c>Production</c> before any recipe and before the market,
/// after the Trade School and the University, so its <see cref="CashClaimed"/> joins
/// theirs as the first claim on the treasury — overtures, bids, civilians and research
/// all see it gone — its bill is gone from the warehouse before any recipe reads it,
/// and its workers' labour is gone from the pool. Healthy workers are the turn's
/// workers less the sick, less the Trade School's trainees and the University's experts,
/// so no worker is counted twice; the cash and warehouse it checks are what those two
/// left.
/// </remarks>
public sealed record ArmouryView(
    long CashClaimed,
    long LabourWithheld,
    IReadOnlyList<ArmouryCategoryView> Categories,
    IReadOnlyList<RegimentView> Regiments);

/// <summary>One hull class at the Shipyard.</summary>
/// <param name="IsGated">The country does not know <see cref="TechnologyKey"/> as the turn opens.</param>
/// <param name="Warning">Core's refusal of the drafted order, or that Core sets it aside; null if accepted.</param>
public sealed record ShipyardRowView(
    int TypeIndex,
    string Name,
    bool IsWarship,
    long Cargo,
    long SeaZones,
    IReadOnlyList<CommodityAmountView> Bill,
    string? TechnologyKey,
    bool IsGated,
    long Drafted,
    OrderWarning<ShipBuildRefusal>? Warning)
{
    /// <summary>The display name of <see cref="TechnologyKey"/>, for the player.</summary>
    public string? TechnologyName { get; init; }
}

/// <summary>
/// The Shipyard dialog. Hulls cost materials and never cash or labour; they are paid
/// after the railyard and before the Capitol, from a warehouse that already holds this
/// turn's output, and the market cannot sell what they take.
/// </summary>
public sealed record ShipyardView(IReadOnlyList<ShipyardRowView> Hulls);

/// <summary>What clicking a province with selected regiments would order.</summary>
public enum ArmyMoveVerb : byte
{
    /// <summary>To an adjacent own province: free.</summary>
    March,

    /// <summary>To a distant own province, on a shared rail network.</summary>
    Rail,

    /// <summary>Into an adjacent province the country does not own.</summary>
    Attack,

    /// <summary>Across water from a landing site to a province not adjacent.</summary>
    Landing,
}

/// <summary>One drafted move and what Core would make of it.</summary>
/// <remarks>
/// An eliminated country's moves are all <see cref="OrderWarning{TRefusal}.SetAside"/> and
/// spend no capacity: Core reads none of them, and its regiments stay where they stand.
/// </remarks>
/// <param name="CapacityUsed">Rail capacity a rail move spends; zero otherwise.</param>
public sealed record DraftedRegimentMoveView(
    IReadOnlyList<RegimentId> Regiments,
    string DestinationKey,
    ArmyMoveVerb Verb,
    long CapacityUsed,
    OrderWarning<RegimentMoveRefusal>? Warning);

/// <summary>
/// The drafted moves, with the rail budget they share.
/// </summary>
/// <remarks>
/// Core resolves every move before any attack, each pass in list order, and so does
/// this. An attack's outcome is a battle the preview does not fight, and a landing's
/// site may be attacked first, so an accepted attack or landing is accepted into
/// battle, nothing more; later attacks are judged against the owners as the turn
/// opened.
/// </remarks>
public sealed record ArmyMovesPreview(
    long RailCapacity,
    long RailCommitted,
    IReadOnlyList<DraftedRegimentMoveView> Moves);

/// <summary>One province as a destination for the selected regiments.</summary>
public sealed record ArmyDestinationView(
    ProvinceId Province,
    string ProvinceKey,
    string? OwnerKey,
    ArmyMoveVerb Verb,
    long CapacityUsed,
    bool IsDrafted,
    OrderWarning<RegimentMoveRefusal>? Warning);

/// <summary>
/// Every province the selected regiments could be sent to, each with its verb and the
/// refusal Core would give were this the newest order for them.
/// </summary>
public sealed record ArmyMoveView(
    IReadOnlyList<RegimentId> Selected,
    IReadOnlyList<ArmyDestinationView> Destinations);

/// <summary>A turn-local restatement of Core's <c>ArmouryPlanner</c>, for one country.</summary>
internal sealed class ArmourySimulation
{
    private ArmourySimulation(int commodityCount) => Bill = new long[commodityCount];

    public List<(ArmyCategory Category, long Count, ArmyTypeDefinition? Type, OrderWarning<RegimentPurchaseRefusal>? Refusal)> Orders
    { get; } = [];

    public long CashClaimed { get; private set; }

    /// <summary>Commodities the accepted orders take, by commodity id.</summary>
    public long[] Bill { get; }

    /// <summary>Labour the accepted orders' workers would have given this turn.</summary>
    public long LabourWithheld { get; private set; }

    /// <summary>
    /// Plans the rows against <paramref name="available"/>, <paramref name="healthy"/> and
    /// <paramref name="cash"/> as the Trade School and the University left them, and takes
    /// what it buys out of all three, as Core's <c>ProductionPlanner</c> does.
    /// </summary>
    public static ArmourySimulation Run(
        WorldState state,
        CountryId country,
        IEnumerable<(ArmyCategory, long)> rows,
        long[] available,
        long[] healthy,
        ref long cash)
    {
        var world = state.Definition;
        var simulation = new ArmourySimulation(world.Commodities.Count);
        var setAside = state.IsEliminated(country);
        foreach (var (category, count) in rows)
        {
            // Build() sends only positive rows, so a zero row never reaches Core.
            if (count <= 0)
            {
                continue;
            }

            var type = ResolveCategory(state, country, category);

            // Core sets an eliminated country's orders aside before Production plans
            // anything, so nothing is bought and nothing is claimed.
            if (setAside)
            {
                simulation.Orders.Add((category, count, type, OrderWarning<RegimentPurchaseRefusal>.SetAside));
                continue;
            }

            var refusal = Check(state, country, type, count, available, healthy, cash);
            simulation.Orders.Add((category, count, type, refusal));
            if (refusal is not null)
            {
                continue;
            }

            var price = type!.Purchase!;
            foreach (var item in price.Bill)
            {
                available[item.Commodity.Value] -= item.Quantity * count;
                simulation.Bill[item.Commodity.Value] += item.Quantity * count;
            }

            cash -= price.CashCost * count;
            simulation.CashClaimed += price.CashCost * count;
            healthy[(int)price.WorkerGrade] -= count;
            if (world.Feeding is { } feeding)
            {
                simulation.LabourWithheld += count * feeding.GetLabour(price.WorkerGrade);
            }
        }

        return simulation;
    }

    /// <summary>Core's rule: the highest era of the category the country may buy.</summary>
    public static ArmyTypeDefinition? ResolveCategory(WorldState state, CountryId country, ArmyCategory category)
    {
        ArmyTypeDefinition? best = null;
        foreach (var type in state.Definition.ArmyTypes)
        {
            if (type.Category != category || type.Purchase is null ||
                type.RequiredTechnology is { } required && !state.HasTechnology(country, required))
            {
                continue;
            }

            if (best is null || type.Era > best.Era)
            {
                best = type;
            }
        }

        return best;
    }

    public static RegimentPurchaseRefusal? Check(
        WorldState state,
        CountryId country,
        ArmyTypeDefinition? type,
        long count,
        long[] available,
        long[] healthy,
        long cash)
    {
        if (count <= 0)
        {
            return RegimentPurchaseRefusal.NonPositiveCount;
        }

        if (type?.Purchase is not { } price)
        {
            return RegimentPurchaseRefusal.NotPurchasable;
        }

        if (state.GetCountryCapital(country) is null)
        {
            return RegimentPurchaseRefusal.NoCapital;
        }

        if (!CanAfford(1, count, healthy[(int)price.WorkerGrade]))
        {
            return RegimentPurchaseRefusal.NotEnoughWorkers;
        }

        if (price.Bill.Any(item => !CanAfford(item.Quantity, count, available[item.Commodity.Value])))
        {
            return RegimentPurchaseRefusal.NotEnoughCommodities;
        }

        return CanAfford(price.CashCost, count, cash) ? null : RegimentPurchaseRefusal.NotEnoughCash;
    }

    public static bool CanAfford(long perUnit, long count, long held) =>
        held >= 0 && (perUnit <= 0 || count <= held / perUnit);
}

/// <summary>
/// A turn-local restatement of Core's <c>RegimentMovePlanner</c> and the checks in
/// <c>LandBattlePlanner</c>, for one country: every move in list order, then every
/// attack in list order, sharing the regiments already ordered and the rail budget.
/// </summary>
internal sealed class ArmyMoveSimulation
{
    private readonly WorldState _state;
    private readonly CountryId _country;
    private readonly HashSet<RegimentId> _acted = [];
    private readonly Dictionary<TaskForceId, long> _landed = [];
    private readonly CommodityId? _arms;
    private readonly IReadOnlySet<TaskForceId> _commanded;
    private long _railLeft;

    private ArmyMoveSimulation(WorldState state, CountryId country, IReadOnlySet<TaskForceId> commanded)
    {
        _state = state;
        _country = country;
        _commanded = commanded;
        _railLeft = state.GetTransportCapacity(country);
        _arms = ArmsCommodity(state.Definition.ArmyTypes);
    }

    public long RailCommitted { get; private set; }

    /// <summary>Results in the orders' own order.</summary>
    public (ArmyMoveVerb Verb, long CapacityUsed, OrderWarning<RegimentMoveRefusal>? Refusal)[] Results { get; private set; } = [];

    /// <param name="commanded">
    /// Task forces the draft's naval orders command this turn
    /// (<see cref="NavalOrderSimulation.Commanded"/>); none of them is a ready site.
    /// </param>
    public static ArmyMoveSimulation Run(
        WorldState state, CountryId country, IReadOnlyList<DraftRegimentMove> moves, IReadOnlySet<TaskForceId> commanded)
    {
        var simulation = new ArmyMoveSimulation(state, country, commanded);
        var results = new (ArmyMoveVerb Verb, long, OrderWarning<RegimentMoveRefusal>?)[moves.Count];
        var provinceCount = state.Definition.Map.Provinces.Count;
        var attacks = new List<int>();
        for (var index = 0; index < moves.Count; index++)
        {
            var destination = moves[index].Destination;
            if ((uint)destination.Value < (uint)provinceCount && state.GetProvinceOwner(destination) != country)
            {
                attacks.Add(index);
                continue;
            }

            results[index] = simulation.Move(moves[index]);
        }

        foreach (var index in attacks)
        {
            results[index] = simulation.Attack(moves[index]);
        }

        // Core sets an eliminated country's orders aside before anything resolves, so
        // every move keeps its verb and none of them happens or spends the rail.
        if (state.IsEliminated(country))
        {
            for (var index = 0; index < results.Length; index++)
            {
                results[index] = (results[index].Verb, 0, OrderWarning<RegimentMoveRefusal>.SetAside);
            }

            simulation.RailCommitted = 0;
        }

        simulation.Results = results;
        return simulation;
    }

    private (ArmyMoveVerb, long, OrderWarning<RegimentMoveRefusal>?) Move(DraftRegimentMove move)
    {
        var (common, from, named) = CheckCommon(move);
        var adjacent = from is { } source && _state.Definition.ProvinceAdjacency.AreAdjacent(source, move.Destination);
        var verb = adjacent || from is null ? ArmyMoveVerb.March : ArmyMoveVerb.Rail;
        if (common is { } refusal)
        {
            return (verb, 0, refusal);
        }

        if (verb == ArmyMoveVerb.March)
        {
            Act(move);
            return (verb, 0, null);
        }

        if (!ShareRailComponent(from!.Value, move.Destination))
        {
            return (verb, 0, RegimentMoveRefusal.NotConnectedByRail);
        }

        if (named.Any(static type => !type.MovesByRail))
        {
            return (verb, 0, RegimentMoveRefusal.CannotMoveByRail);
        }

        var arms = 0L;
        foreach (var type in named)
        {
            arms += type.Arms ?? 0;
            if (_railLeft < 0 || arms > _railLeft / ArmyCapacityPerArmament)
            {
                return (verb, 0, RegimentMoveRefusal.NotEnoughRailCapacity);
            }
        }

        var cost = arms * ArmyCapacityPerArmament;
        _railLeft -= cost;
        RailCommitted += cost;
        Act(move);
        return (verb, cost, null);
    }

    private (ArmyMoveVerb, long, OrderWarning<RegimentMoveRefusal>?) Attack(DraftRegimentMove move)
    {
        var (common, from, named) = CheckCommon(move);
        var adjacent = from is null || _state.Definition.ProvinceAdjacency.AreAdjacent(from.Value, move.Destination);
        if (common is { } refusal)
        {
            return (adjacent ? ArmyMoveVerb.Attack : ArmyMoveVerb.Landing, 0, refusal);
        }

        if (adjacent)
        {
            var war = CheckOwnerAndWar(move.Destination);
            if (war is null)
            {
                Act(move);
            }

            return (ArmyMoveVerb.Attack, 0, war);
        }

        if (ReadySite(move.Destination) is not { } site)
        {
            return (ArmyMoveVerb.Attack, 0, RegimentMoveRefusal.NotAdjacent);
        }

        var map = _state.Definition.Map;
        var coastal = CoastalZones(map, from!.Value);
        if (coastal.Count == 0)
        {
            return (ArmyMoveVerb.Landing, 0, RegimentMoveRefusal.NotCoastal);
        }

        if (!coastal.Any(zone => Reaches(map.SeaTopology, zone, site.SeaZone)))
        {
            return (ArmyMoveVerb.Landing, 0, RegimentMoveRefusal.NoSeaPath);
        }

        if (CheckOwnerAndWar(move.Destination) is { } owner)
        {
            return (ArmyMoveVerb.Landing, 0, owner);
        }

        var load = named.Sum(static type => type.Arms ?? 0);
        if (load > Math.Max(0, Capacity(site) - _landed.GetValueOrDefault(site.Id)))
        {
            return (ArmyMoveVerb.Landing, 0, RegimentMoveRefusal.NotEnoughLandingCapacity);
        }

        _landed[site.Id] = _landed.GetValueOrDefault(site.Id) + load;
        Act(move);
        return (ArmyMoveVerb.Landing, 0, null);
    }

    /// <summary>Core's <c>RegimentMovePlanner.CapacityPerArmament</c> (manual 1761–1762).</summary>
    public const long ArmyCapacityPerArmament = 5;

    private void Act(DraftRegimentMove move)
    {
        foreach (var regiment in move.Regiments)
        {
            _ = _acted.Add(regiment);
        }
    }

    private (RegimentMoveRefusal?, ProvinceId?, IReadOnlyList<ArmyTypeDefinition>) CheckCommon(DraftRegimentMove move)
    {
        var world = _state.Definition;
        if (move.Regiments.Count == 0)
        {
            return (RegimentMoveRefusal.NoRegiments, null, []);
        }

        if (move.Regiments.Distinct().Count() != move.Regiments.Count)
        {
            return (RegimentMoveRefusal.DuplicateRegiment, null, []);
        }

        var regiments = new List<Regiment>();
        foreach (var id in move.Regiments)
        {
            if (_state.GetRegiment(id) is not { } regiment)
            {
                return (RegimentMoveRefusal.NoSuchRegiment, null, []);
            }

            if (regiment.Owner != _country)
            {
                return (RegimentMoveRefusal.NotYours, null, []);
            }

            if (_acted.Contains(id))
            {
                return (RegimentMoveRefusal.AlreadyMoved, null, []);
            }

            regiments.Add(regiment);
        }

        var types = regiments.Select(regiment => world.ArmyTypes[regiment.Type.Value]).ToArray();
        var from = regiments[0].Province;
        if (regiments.Any(regiment => regiment.Province != from))
        {
            return (RegimentMoveRefusal.NotTogether, from, types);
        }

        if (types.Any(static type => !type.LeavesProvince))
        {
            return (RegimentMoveRefusal.CannotLeaveProvince, from, types);
        }

        if ((uint)move.Destination.Value >= (uint)world.Map.Provinces.Count)
        {
            return (RegimentMoveRefusal.UnknownProvince, from, types);
        }

        return move.Destination == from
            ? (RegimentMoveRefusal.SameProvince, from, types)
            : (null, from, types);
    }

    private RegimentMoveRefusal? CheckOwnerAndWar(ProvinceId destination)
    {
        var owner = _state.GetProvinceOwner(destination);
        if (owner == _country)
        {
            return RegimentMoveRefusal.AlreadyOwn;
        }

        return owner is { } target && !_state.IsEliminated(target) && !_state.HasEffectiveHostility(_country, target)
            ? RegimentMoveRefusal.NotAtWar
            : null;
    }

    private bool ShareRailComponent(ProvinceId from, ProvinceId to)
    {
        var rail = _state.GetRailConnectivity(_country);
        var map = _state.Definition.Map;
        var components = map.GetCells(from)
            .Select(cell => rail.GetComponentId(cell))
            .OfType<int>()
            .ToHashSet();
        return components.Count > 0 &&
               map.GetCells(to).Any(cell => rail.GetComponentId(cell) is { } component && components.Contains(component));
    }

    /// <summary>
    /// Core's ready landing site, read after the naval orders as Core's <c>Conflict</c>
    /// reads it: lowest id, set before this turn, touching the target, and not commanded
    /// this turn. A task force an assembly creates this turn is never ready, because its
    /// landing is stamped this turn, so only task forces that exist now are candidates.
    /// </summary>
    private TaskForceState? ReadySite(ProvinceId destination)
    {
        var map = _state.Definition.Map;
        var turnNumber = _state.CompletedTurnCount + 1;
        return _state.TaskForces
            .Where(force => force.Country == _country &&
                            force.Activity == TaskForceActivity.Landing &&
                            force.LandingTarget == destination &&
                            force.LandingSince < turnNumber &&
                            !_commanded.Contains(force.Id) &&
                            map.GetCells(destination).Any(cell => AdjacentSeaZones(map, cell).Contains(force.SeaZone)))
            .OrderBy(static force => force.Id.Value)
            .FirstOrDefault();
    }

    private long Capacity(TaskForceState site)
    {
        if (_arms is not { } arms)
        {
            return 0;
        }

        var total = 0L;
        foreach (var id in site.Fleets)
        {
            if (_state.TryGetFleet(id, out var fleet))
            {
                var perHull = _state.Definition.ShipTypes[fleet.Type.Value].BuildCost
                    .Where(item => item.Commodity == arms)
                    .Sum(static item => item.Quantity);
                total += fleet.Count * perHull;
            }
        }

        return total;
    }

    internal static IReadOnlyList<SeaZoneId> CoastalZones(MapDefinition map, ProvinceId province) =>
        map.GetCells(province).SelectMany(cell => AdjacentSeaZones(map, cell)).Distinct().ToArray();

    internal static IEnumerable<SeaZoneId> AdjacentSeaZones(MapDefinition map, CellIndex cell)
    {
        foreach (var direction in HexDirections.All.ToArray())
        {
            if (map.TryGetNeighbor(cell, direction, out var neighbour) &&
                map[neighbour].Region.Kind == CellRegionKind.SeaZone)
            {
                yield return map[neighbour].Region.SeaZone;
            }
        }
    }

    private static bool Reaches(SeaZoneTopology topology, SeaZoneId from, SeaZoneId to)
    {
        var seen = new HashSet<SeaZoneId> { from };
        var frontier = new Queue<SeaZoneId>([from]);
        while (frontier.TryDequeue(out var current))
        {
            if (current == to)
            {
                return true;
            }

            foreach (var next in topology.GetNeighbors(current))
            {
                if (seen.Add(next))
                {
                    frontier.Enqueue(next);
                }
            }
        }

        return false;
    }

    /// <summary>Core's armaments commodity: the one every armed type's bill holds in exactly its arms.</summary>
    private static CommodityId? ArmsCommodity(IReadOnlyList<ArmyTypeDefinition> types)
    {
        HashSet<CommodityId>? candidates = null;
        foreach (var type in types)
        {
            if (type.Purchase is not { } purchase || type.Arms is not > 0)
            {
                continue;
            }

            var matching = purchase.Bill.Where(item => item.Quantity == type.Arms).Select(static item => item.Commodity);
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

public sealed partial class OrdersPreview
{
    /// <summary>The Armoury dialog and the Garrison book.</summary>
    public ArmouryView Armoury { get; private init; } = null!;

    /// <summary>The Shipyard dialog.</summary>
    public ShipyardView Shipyard { get; private init; } = null!;

    /// <summary>The drafted regiment moves.</summary>
    public ArmyMovesPreview ArmyMoves { get; private init; } = null!;

    private static ArmourySimulation SimulateArmoury(WorldState state, OrdersDraft draft) =>
        SimulateProductionClaims(state, draft).Armoury;

    /// <summary>Cash the Armoury takes in <c>Production</c>, after the Trade School and the University.</summary>
    private static long ClaimedByArmoury(WorldState state, OrdersDraft draft) =>
        SimulateArmoury(state, draft).CashClaimed;

    /// <summary>
    /// What clicking each province with these regiments selected would order, as the
    /// newest move for them: the draft's other moves stand, and these regiments leave
    /// whatever move they were in.
    /// </summary>
    public static ArmyMoveView CreateArmyMoves(
        CompiledWorldPackage package,
        string scenarioKey,
        WorldState state,
        OrdersDraft draft,
        IEnumerable<RegimentId> selected)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentException.ThrowIfNullOrWhiteSpace(scenarioKey);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(selected);
        var world = package.GetWorld(scenarioKey);
        if (!ReferenceEquals(world, state.Definition))
        {
            throw new ArgumentException(
                "The runtime state must belong to the selected package scenario.",
                nameof(state));
        }

        var regiments = selected.Distinct().ToArray();
        var others = draft.RegimentMoves
            .Select(move => move with { Regiments = move.Regiments.Except(regiments).ToArray() })
            .Where(static move => move.Regiments.Count > 0)
            .ToList();
        var commanded = NavalOrderSimulation.Commanded(package, state, draft.Country, draft.NavalOrders);
        var origin = regiments.Length > 0 && state.GetRegiment(regiments[0]) is { } first ? first.Province : (ProvinceId?)null;
        var destinations = new List<ArmyDestinationView>();
        for (var value = 0; value < world.Map.Provinces.Count; value++)
        {
            var province = new ProvinceId(value);
            if (province == origin)
            {
                continue;
            }

            var candidate = new DraftRegimentMove(regiments, province);
            var simulation = ArmyMoveSimulation.Run(state, draft.Country, [.. others, candidate], commanded);
            var (verb, cost, refusal) = simulation.Results[^1];
            var owner = state.GetProvinceOwner(province);
            destinations.Add(new ArmyDestinationView(
                province,
                package.Catalog.GetKey(province),
                owner is { } country ? package.Catalog.GetKey(country) : null,
                verb,
                cost,
                draft.RegimentMoves.Any(move => move.Destination == province && regiments.All(move.Regiments.Contains)),
                refusal));
        }

        return new ArmyMoveView(Array.AsReadOnly(regiments), Array.AsReadOnly(destinations.ToArray()));
    }

    private static ArmouryView PreviewArmoury(
        CompiledWorldPackage package, WorldState state, OrdersDraft draft, Names names, ArmourySimulation drafted)
    {
        var world = state.Definition;
        var country = draft.Country;
        var categories = new List<ArmouryCategoryView>();
        foreach (var category in Enum.GetValues<ArmyCategory>())
        {
            var type = ArmourySimulation.ResolveCategory(state, country, category);
            var unavailable = type?.Purchase is null
                ? RegimentPurchaseRefusal.NotPurchasable
                : state.GetCountryCapital(country) is null ? RegimentPurchaseRefusal.NoCapital : (RegimentPurchaseRefusal?)null;
            var count = draft.RegimentPurchases.FirstOrDefault(row => row.Category == category).Count;
            var index = drafted.Orders.FindIndex(row => row.Category == category);
            categories.Add(new ArmouryCategoryView(
                category,
                type?.Id.Value,
                type?.Name,
                type?.Purchase?.WorkerGrade,
                type?.Purchase is { } purchase ? names.Amounts(purchase.Bill) : [],
                type?.Purchase?.CashCost ?? 0,
                type?.Arms ?? 0,
                unavailable,
                count,
                index >= 0 ? drafted.Orders[index].Refusal : null,
                // Core takes one healthy worker per regiment it buys, and none for a row it refuses.
                index >= 0 && drafted.Orders[index].Refusal is null ? drafted.Orders[index].Count : 0));
        }

        var regiments = state.GetRegiments(country)
            .Select(regiment =>
            {
                var type = world.ArmyTypes[regiment.Type.Value];
                var move = draft.GetRegimentMove(regiment.Id);
                return new RegimentView(
                    regiment.Id,
                    regiment.Type.Value,
                    type.Name,
                    type.Category,
                    package.Catalog.GetKey(regiment.Province),
                    regiment.Health,
                    Regiment.MaxHealth,
                    regiment.Experience,
                    regiment.Medals,
                    type.LeavesProvince,
                    type.MovesByRail,
                    type.Arms ?? 0,
                    move is null ? null : package.Catalog.GetKey(move.Destination));
            })
            .ToArray();
        return new ArmouryView(
            drafted.CashClaimed,
            drafted.LabourWithheld,
            Array.AsReadOnly(categories.ToArray()),
            Array.AsReadOnly(regiments));
    }

    /// <summary>
    /// Core's <c>ShipyardPlanner</c> for one country, against the warehouse the
    /// industry preview has reached: after recipes (output included), expansion and the
    /// railyard. Takes what it builds out of <paramref name="available"/> and adds it to
    /// <paramref name="claimed"/>.
    /// </summary>
    private static ShipyardView PreviewShipyard(
        WorldState state, OrdersDraft draft, Names names, long[] available, long[] claimed)
    {
        var world = state.Definition;
        var country = draft.Country;
        var warnings = new Dictionary<ShipTypeId, OrderWarning<ShipBuildRefusal>?>();
        var setAside = state.IsEliminated(country);
        foreach (var (type, count) in draft.ShipBuilds)
        {
            if (count <= 0)
            {
                continue;
            }

            // Core sets an eliminated country's orders aside: nothing is built or claimed.
            if (setAside)
            {
                warnings[type] = OrderWarning<ShipBuildRefusal>.SetAside;
                continue;
            }

            var hull = world.ShipTypes[type.Value];
            ShipBuildRefusal? refusal = null;
            if (hull.RequiredTechnology is { } required && !state.HasTechnology(country, required))
            {
                refusal = ShipBuildRefusal.TechnologyNotKnown;
            }
            else if (hull.BuildCost.Any(item => !ArmourySimulation.CanAfford(
                         item.Quantity, count, available[item.Commodity.Value])))
            {
                refusal = ShipBuildRefusal.NotEnoughCommodities;
            }
            else
            {
                foreach (var item in hull.BuildCost)
                {
                    available[item.Commodity.Value] -= item.Quantity * count;
                    claimed[item.Commodity.Value] += item.Quantity * count;
                }
            }

            warnings[type] = refusal;
        }

        var hulls = world.ShipTypes
            .Select(hull => new ShipyardRowView(
                hull.Id.Value,
                hull.Name,
                // Core's merchant test: cargo and no firepower. Anything else is a warship.
                !(hull.Cargo > 0 && (hull.Combat?.Firepower ?? 0) == 0),
                hull.Cargo,
                hull.SeaZones,
                names.Amounts(hull.BuildCost),
                hull.RequiredTechnology is { } technology ? names.Key(technology) : null,
                hull.RequiredTechnology is { } gate && !state.HasTechnology(country, gate),
                draft.ShipBuilds.FirstOrDefault(row => row.Type == hull.Id).Count,
                warnings.GetValueOrDefault(hull.Id))
            {
                TechnologyName = hull.RequiredTechnology is { } named
                    ? state.Definition.Technologies[named.Value].Name
                    : null,
            })
            .ToArray();
        return new ShipyardView(Array.AsReadOnly(hulls));
    }

    private static ArmyMovesPreview PreviewArmyMoves(CompiledWorldPackage package, WorldState state, OrdersDraft draft)
    {
        var simulation = ArmyMoveSimulation.Run(
            state,
            draft.Country,
            draft.RegimentMoves,
            NavalOrderSimulation.Commanded(package, state, draft.Country, draft.NavalOrders));
        var moves = draft.RegimentMoves
            .Select((move, index) => new DraftedRegimentMoveView(
                move.Regiments,
                package.Catalog.GetKey(move.Destination),
                simulation.Results[index].Verb,
                simulation.Results[index].CapacityUsed,
                simulation.Results[index].Refusal))
            .ToArray();
        return new ArmyMovesPreview(
            state.GetTransportCapacity(draft.Country),
            simulation.RailCommitted,
            Array.AsReadOnly(moves));
    }
}
