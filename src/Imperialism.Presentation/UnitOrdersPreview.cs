using Imperialism.Content;
using Imperialism.Core;

namespace Imperialism.Presentation;

/// <summary>What a civilian's order would do, as the map's cursor names it.</summary>
public enum UnitOrderVerb : byte
{
    Deploy,
    Improve,
    Prospect,
    Rail,
    Depot,
    Port,
    Fort,
}

/// <summary>
/// One thing a civilian could be told to do this turn and what Core would make of
/// it. <see cref="Warning"/> is Core's own refusal reason where Core would refuse
/// and charge nothing; the preview never refuses, it only says so.
/// </summary>
/// <param name="CashCost">
/// What the order costs if accepted: the next rung's improvement price, the dearer
/// end of a rail link, or the structure's price. Zero for a deploy, a search, or a
/// world that prices nothing. Shown on a warned target too, so a player can see what
/// it would have cost; Core charges it only when it accepts.
/// </param>
/// <param name="IsDrafted">True when this is the order the draft holds for the unit.</param>
public sealed record UnitTargetView(
    CellIndex Cell,
    UnitOrderVerb Verb,
    long CashCost,
    CivilianOrderRefusal? Warning,
    bool IsDrafted)
{
    public bool IsAccepted => Warning is null;
}

/// <summary>
/// Everything one civilian of the drafting country could be ordered to do this turn.
/// </summary>
/// <remarks>
/// <b>A preview, not a ruling.</b> Deploy and the civilian's work are listed for
/// every land cell the country owns as the turn opens; an Engineer's rail for every
/// neighbouring cell and its depot, port and fort for its own. A cell outside that
/// list — another country's, or the sea — is still answered by
/// <see cref="Evaluate"/>, which is what a click on the map asks.
/// </remarks>
public sealed class UnitOrdersView
{
    private readonly CivilianOrderSimulation.Evaluator _evaluator;

    internal UnitOrdersView(
        CivilianPreview civilian,
        long cashBeforeOrder,
        IReadOnlyList<UnitTargetView> targets,
        CivilianOrderSimulation.Evaluator evaluator)
    {
        Civilian = civilian;
        CashBeforeOrder = cashBeforeOrder;
        Targets = targets;
        _evaluator = evaluator;
    }

    public CivilianPreview Civilian { get; }

    /// <summary>
    /// Cash left for this civilian's own job once everything Core charges before it
    /// has been charged: the overtures, the bids, every other civilian's work order, and — for an
    /// Engineer — the lower-numbered Engineers' jobs.
    /// </summary>
    public long CashBeforeOrder { get; }

    public IReadOnlyList<UnitTargetView> Targets { get; }

    /// <summary>What ordering this civilian to this cell, with this verb, would meet.</summary>
    public UnitTargetView Evaluate(CellIndex cell, UnitOrderVerb verb) => _evaluator(cell, verb);
}

/// <summary>One of the drafting country's civilians, for the map to draw.</summary>
/// <param name="TurnsRemaining">Turns left on the job under way, or null when idle.</param>
/// <param name="Order">The drafted order's expected outcome, or null when it has none.</param>
public sealed record CivilianPreview(
    CivilianUnitId Unit,
    string TypeName,
    CivilianWorkKind Work,
    CellIndex Cell,
    bool IsBusy,
    int? TurnsRemaining,
    UnitTargetView? Order);

/// <summary>
/// The drafting country's civilians and what their drafted orders will cost.
/// </summary>
/// <remarks>
/// Cash in the order Core spends it: Diplomacy pays for overtures first, so
/// <see cref="ClaimedByOvertures"/> comes off before anything; then Trade pays for
/// bids before Development, so <see cref="ClaimedByBids"/> — every bid at world
/// price, as if all of them fill — comes off next. That is the worst case: a bid that finds no seller costs
/// nothing, and what the market <em>sells</em> is credited before Development too,
/// so a warning of <see cref="CivilianOrderRefusal.NotEnoughCash"/> can be one Core
/// does not issue. Investment pays for research after Development, so it never
/// claims cash from a civilian; the civilians claim from it.
/// </remarks>
/// <param name="OtherOrders">Drafted orders for units the country does not hold.</param>
public sealed record CivilianOrdersPreview(
    long CashAtOpen,
    long ClaimedByOvertures,
    long ClaimedByBids,
    long CashBeforeCivilians,
    long CommittedCost,
    IReadOnlyList<CivilianPreview> Civilians,
    IReadOnlyList<(CivilianUnitId Unit, UnitTargetView Order)> OtherOrders)
{
    /// <summary>Paid at the Armoury in <c>Production</c>, the first claim of all.</summary>
    public long ClaimedByArmoury { get; init; }
}

/// <summary>
/// A turn-local restatement of Core's <c>DevelopmentPlanner</c> and
/// <c>EngineerPlanner</c>, which are internal. It walks the same steps in the same
/// order on an overlay and never touches the <see cref="WorldState"/>.
/// </summary>
/// <remarks>
/// Core's order, and so this one's: work under way advances first, so a job
/// finishing this turn raises its tile, reveals its search or stands its structure
/// <em>before</em> any new order is read; the set of busy civilians is taken before
/// that, so a civilian finishing this turn is still refused as already working. Then
/// a country's deployments, then its work orders, then its Engineers' orders, each
/// list in the order given — which for a draft is unit-id order. Cash comes off as
/// each order is accepted, and a fort accepted earlier counts against the province's
/// cap for a later one.
/// </remarks>
internal sealed class CivilianOrderSimulation
{
    internal delegate UnitTargetView Evaluator(CellIndex cell, UnitOrderVerb verb);

    private readonly WorldState _state;
    private readonly WorldDefinition _world;
    private readonly CountryId _country;
    private readonly HashSet<CivilianUnitId> _busyAtStart = [];
    private readonly Dictionary<CellIndex, int> _development = [];
    private readonly HashSet<CellIndex> _prospected = [];
    private readonly HashSet<CellIndex> _depots = [];
    private readonly HashSet<CellIndex> _ports = [];
    private readonly HashSet<CellLink> _rails = [];
    private readonly Dictionary<ProvinceId, int> _fortLevels = [];
    private readonly Dictionary<ProvinceId, int> _fortJobs = [];

    private CivilianOrderSimulation(WorldState state, CountryId country, long cash)
    {
        _state = state;
        _world = state.Definition;
        _country = country;
        Cash = cash;
        foreach (var civilian in state.GetCivilians())
        {
            if (civilian.IsBusy)
            {
                _busyAtStart.Add(civilian.Id);
            }
        }

        AdvanceWorkInProgress();
    }

    public long Cash { get; private set; }

    /// <summary>
    /// Runs <paramref name="orders"/> in Core's order, stopping before the first one
    /// <paramref name="stopBefore"/> names, and returns each run order's outcome.
    /// </summary>
    public static (CivilianOrderSimulation Simulation, IReadOnlyList<(DraftUnitOrder Order, UnitTargetView Outcome)> Outcomes) Run(
        WorldState state,
        CountryId country,
        long cash,
        IEnumerable<DraftUnitOrder> orders,
        Func<DraftUnitOrder, bool>? stopBefore = null)
    {
        var simulation = new CivilianOrderSimulation(state, country, cash);
        var outcomes = new List<(DraftUnitOrder, UnitTargetView)>();
        foreach (var order in InCoreOrder(orders))
        {
            if (stopBefore?.Invoke(order) == true)
            {
                break;
            }

            outcomes.Add((order, simulation.Apply(order.Unit, VerbOf(state.Definition, state, order), order.Cell, isDrafted: true)));
        }

        return (simulation, outcomes);
    }

    /// <summary>Deployments, then work, then Engineer jobs, each by unit id.</summary>
    public static IEnumerable<DraftUnitOrder> InCoreOrder(IEnumerable<DraftUnitOrder> orders) => orders
        .OrderBy(static order => order.Kind)
        .ThenBy(static order => order.Unit.Value);

    public static UnitOrderVerb VerbOf(WorldDefinition world, WorldState state, DraftUnitOrder order) => order.Kind switch
    {
        UnitOrderKind.Deploy => UnitOrderVerb.Deploy,
        UnitOrderKind.Engineer => order.Structure switch
        {
            EngineerConstruction.Rail => UnitOrderVerb.Rail,
            EngineerConstruction.Depot => UnitOrderVerb.Depot,
            EngineerConstruction.Port => UnitOrderVerb.Port,
            _ => UnitOrderVerb.Fort,
        },
        _ => WorkVerb(world, state.GetCivilian(order.Unit)?.Type),
    };

    /// <summary>
    /// A work order searches for a Prospector and improves for everybody else; an
    /// Engineer given one is refused as the wrong cursor, and is shown as Improve.
    /// </summary>
    public static UnitOrderVerb WorkVerb(WorldDefinition world, CivilianTypeId? type) =>
        type is { } id && world.CivilianTypes[id.Value].Work == CivilianWorkKind.Prospect
            ? UnitOrderVerb.Prospect
            : UnitOrderVerb.Improve;

    /// <summary>What the order would meet now, without applying it.</summary>
    public UnitTargetView Evaluate(CivilianUnitId unit, UnitOrderVerb verb, CellIndex cell, bool isDrafted)
    {
        var (cost, refusal) = Judge(unit, verb, cell);
        return new UnitTargetView(cell, verb, cost, refusal, isDrafted);
    }

    /// <summary>Judges the order and, if Core would accept it, applies what accepting does.</summary>
    public UnitTargetView Apply(CivilianUnitId unit, UnitOrderVerb verb, CellIndex cell, bool isDrafted)
    {
        var view = Evaluate(unit, verb, cell, isDrafted);
        if (view.Warning is null)
        {
            Cash -= view.CashCost;
            if (verb == UnitOrderVerb.Fort)
            {
                var province = ProvinceOf(cell);
                _fortJobs[province] = _fortJobs.GetValueOrDefault(province) + 1;
            }
        }

        return view;
    }

    private (long Cost, CivilianOrderRefusal? Refusal) Judge(CivilianUnitId unit, UnitOrderVerb verb, CellIndex cell)
    {
        if (_state.GetCivilian(unit) is not { } civilian)
        {
            return (0, CivilianOrderRefusal.NoSuchCivilian);
        }

        var cost = PriceOf(civilian, verb, cell);
        if (civilian.Country != _country)
        {
            return (cost, CivilianOrderRefusal.NotYours);
        }

        if (_busyAtStart.Contains(unit))
        {
            return (cost, CivilianOrderRefusal.AlreadyWorking);
        }

        var refusal = verb switch
        {
            UnitOrderVerb.Deploy => Entry(cell),
            UnitOrderVerb.Improve or UnitOrderVerb.Prospect => Entry(cell) ?? Work(_country, civilian.Type, cell),
            _ => Construction(civilian, verb, cell),
        };

        if (refusal is null && cost > 0 && Cash < cost)
        {
            refusal = CivilianOrderRefusal.NotEnoughCash;
        }

        return (cost, refusal);
    }

    private long PriceOf(CivilianUnit civilian, UnitOrderVerb verb, CellIndex cell)
    {
        if (!_world.Map.Dimensions.Contains(cell))
        {
            return 0;
        }

        switch (verb)
        {
            case UnitOrderVerb.Improve:
                return _world.CivilianTypes[civilian.Type.Value].Work == CivilianWorkKind.Improve &&
                    _world.Improvement is { } improvement
                        ? improvement.GetCashCost(Development(cell) + 1)
                        : 0;
            case UnitOrderVerb.Rail:
                if (!_world.Map.Dimensions.Contains(civilian.Cell))
                {
                    return 0;
                }

                var map = _world.Map;
                return Math.Max(
                    map.GetTerrain(map[civilian.Cell].Terrain)?.Rail?.CashCost ?? 0,
                    map.GetTerrain(map[cell].Terrain)?.Rail?.CashCost ?? 0);
            case UnitOrderVerb.Depot:
                return _world.Construction?.GetCashCost(EngineerConstruction.Depot) ?? 0;
            case UnitOrderVerb.Port:
                return _world.Construction?.GetCashCost(EngineerConstruction.Port) ?? 0;
            case UnitOrderVerb.Fort:
                return _world.Construction?.FortCashCost ?? 0;
            default:
                return 0;
        }
    }

    private CivilianOrderRefusal? Construction(CivilianUnit engineer, UnitOrderVerb verb, CellIndex cell)
    {
        if (_world.Construction is not { } settings)
        {
            return CivilianOrderRefusal.NothingCanBeBuiltInThisWorld;
        }

        if (_world.CivilianTypes[engineer.Type.Value].Work != CivilianWorkKind.Construct)
        {
            return CivilianOrderRefusal.NotAnEngineer;
        }

        if ((Entry(engineer.Cell) ?? Entry(cell)) is { } entry)
        {
            return entry;
        }

        if (verb == UnitOrderVerb.Rail)
        {
            if (cell == engineer.Cell || !_world.Map.AreAdjacent(engineer.Cell, cell))
            {
                return CivilianOrderRefusal.RailNeedsAnAdjacentTile;
            }
        }
        else if (cell != engineer.Cell)
        {
            return CivilianOrderRefusal.StructureNeedsTheEngineersOwnTile;
        }

        return verb switch
        {
            UnitOrderVerb.Rail => Rail(engineer.Cell, cell),
            UnitOrderVerb.Depot => Depot(cell),
            UnitOrderVerb.Port => Port(cell),
            _ => settings.FortCashCost is null
                ? CivilianOrderRefusal.FortsNotPricedInThisWorld
                : Fort(cell, countJobsUnderWay: true),
        };
    }

    /// <summary>Core's <c>AdvanceWorkInProgress</c>, applied to the overlay.</summary>
    private void AdvanceWorkInProgress()
    {
        foreach (var civilian in _state.GetCivilians())
        {
            if (civilian.Work is not { } job || _state.IsEliminated(civilian.Country))
            {
                continue;
            }

            if (job.TurnsRemaining > 1)
            {
                if (civilian.Country == _country &&
                    job.Construction is { Kind: EngineerConstruction.Fort } fort)
                {
                    var province = ProvinceOf(fort.Target);
                    _fortJobs[province] = _fortJobs.GetValueOrDefault(province) + 1;
                }

                continue;
            }

            if (job.Construction is { } construction)
            {
                if (Finishing(civilian.Country, job.Cell, construction) is null)
                {
                    Complete(job.Cell, construction);
                }

                continue;
            }

            if (Entry(civilian.Country, job.Cell) is not null ||
                Work(civilian.Country, civilian.Type, job.Cell) is not null)
            {
                continue;
            }

            if (_world.CivilianTypes[civilian.Type.Value].Work == CivilianWorkKind.Prospect)
            {
                if (civilian.Country == _country)
                {
                    _prospected.Add(job.Cell);
                }
            }
            else
            {
                _development[job.Cell] = Development(job.Cell) + 1;
            }
        }
    }

    private CivilianOrderRefusal? Finishing(CountryId owner, CellIndex cell, EngineerJob job) => job.Kind switch
    {
        EngineerConstruction.Rail => Entry(owner, cell) ?? Entry(owner, job.Target) ?? RailFor(owner, cell, job.Target),
        EngineerConstruction.Depot => Entry(owner, job.Target) ?? DepotFor(owner, job.Target),
        EngineerConstruction.Port => Entry(owner, job.Target) ?? Port(job.Target),
        _ => Entry(owner, job.Target) ?? Fort(job.Target, countJobsUnderWay: false),
    };

    private void Complete(CellIndex cell, EngineerJob job)
    {
        switch (job.Kind)
        {
            case EngineerConstruction.Rail:
                _rails.Add(new CellLink(cell, job.Target));
                break;
            case EngineerConstruction.Depot:
                _depots.Add(job.Target);
                break;
            case EngineerConstruction.Port:
                _ports.Add(job.Target);
                break;
            default:
                var province = ProvinceOf(job.Target);
                _fortLevels[province] = Math.Min(WorldState.MaxFortLevel, FortLevel(province) + 1);
                break;
        }
    }

    private int Development(CellIndex cell) =>
        _development.TryGetValue(cell, out var level) ? level : _state.GetCellDevelopment(cell);

    private int FortLevel(ProvinceId province) =>
        _fortLevels.TryGetValue(province, out var level) ? level : _state.GetFortLevel(province);

    private bool CanSeeDeposits(CountryId country, CellIndex cell) =>
        Development(cell) > 0 ||
        _state.HasProspected(country, cell) ||
        country == _country && _prospected.Contains(cell);

    private bool HasProspected(CountryId country, CellIndex cell) =>
        _state.HasProspected(country, cell) || country == _country && _prospected.Contains(cell);

    private ProvinceId ProvinceOf(CellIndex cell) => _world.Map[cell].Region.Province;

    private CivilianOrderRefusal? Entry(CellIndex cell) => Entry(_country, cell);

    private CivilianOrderRefusal? Entry(CountryId country, CellIndex cell)
    {
        var map = _world.Map;
        if (!map.Dimensions.Contains(cell))
        {
            return CivilianOrderRefusal.TargetOffMap;
        }

        var region = map[cell].Region;
        if (region.Kind != CellRegionKind.Province)
        {
            return CivilianOrderRefusal.TargetNotLand;
        }

        return _state.GetProvinceOwner(region.Province) == country
            ? null
            : CivilianOrderRefusal.TargetNotYourTerritory;
    }

    private CivilianOrderRefusal? Work(CountryId country, CivilianTypeId type, CellIndex cell) =>
        _world.CivilianTypes[type.Value].Work switch
        {
            CivilianWorkKind.Construct => CivilianOrderRefusal.NotAnEngineer,
            CivilianWorkKind.Prospect => Prospecting(country, cell),
            _ => Improvement(country, type, cell),
        };

    private CivilianOrderRefusal? Improvement(CountryId country, CivilianTypeId type, CellIndex cell)
    {
        var map = _world.Map;
        if (map.GetTerrain(map[cell].Terrain) is not { IsImprovable: true })
        {
            return CivilianOrderRefusal.TerrainCannotBeImproved;
        }

        var level = Development(cell);
        bool worked = false, undiscovered = false, unknown = false;
        foreach (var resourceId in map[cell].Resources)
        {
            var resource = map.Resources[resourceId.Value];
            if (resource.ImprovedBy != type)
            {
                continue;
            }

            if (resource.RequiresDiscovery && !CanSeeDeposits(country, cell))
            {
                undiscovered = true;
                continue;
            }

            worked = true;
            if (level >= resource.MaxDevelopmentLevel)
            {
                continue;
            }

            if (resource.GetRequiredTechnology(level + 1) is { } required &&
                !_state.HasTechnology(country, required))
            {
                unknown = true;
                continue;
            }

            return null;
        }

        if (worked)
        {
            return unknown
                ? CivilianOrderRefusal.ImprovementTechnologyNotKnown
                : CivilianOrderRefusal.AlreadyFullyDeveloped;
        }

        return undiscovered
            ? CivilianOrderRefusal.DepositNotYetDiscovered
            : CivilianOrderRefusal.NoDepositThisCivilianWorks;
    }

    private CivilianOrderRefusal? Prospecting(CountryId country, CellIndex cell)
    {
        var map = _world.Map;
        if (map.GetTerrain(map[cell].Terrain)?.Prospecting is not { } rule)
        {
            return CivilianOrderRefusal.TerrainCannotBeProspected;
        }

        if (rule.RequiredTechnology is { } required && !_state.HasTechnology(country, required))
        {
            return CivilianOrderRefusal.ProspectingTechnologyNotKnown;
        }

        return HasProspected(country, cell) ? CivilianOrderRefusal.AlreadyProspected : null;
    }

    private CivilianOrderRefusal? Rail(CellIndex from, CellIndex to) => RailFor(_country, from, to);

    private CivilianOrderRefusal? RailFor(CountryId country, CellIndex from, CellIndex to)
    {
        var link = new CellLink(from, to);
        if (_state.HasRail(link) || _rails.Contains(link))
        {
            return CivilianOrderRefusal.RailAlreadyBuilt;
        }

        return RailGate(country, from) ?? RailGate(country, to);
    }

    private CivilianOrderRefusal? Depot(CellIndex cell) => DepotFor(_country, cell);

    private CivilianOrderRefusal? DepotFor(CountryId country, CellIndex cell) =>
        _state.HasDepot(cell) || _depots.Contains(cell)
            ? CivilianOrderRefusal.DepotAlreadyBuilt
            : RailGate(country, cell);

    private CivilianOrderRefusal? Port(CellIndex cell)
    {
        if (_state.HasPort(cell) || _ports.Contains(cell))
        {
            return CivilianOrderRefusal.PortAlreadyBuilt;
        }

        var map = _world.Map;
        if (map[cell].River is not null)
        {
            return null;
        }

        foreach (var direction in HexDirections.All)
        {
            if (map.TryGetNeighbor(cell, direction, out var neighbor) &&
                map[neighbor].Region.Kind == CellRegionKind.SeaZone)
            {
                return null;
            }
        }

        return CivilianOrderRefusal.PortNeedsWater;
    }

    private CivilianOrderRefusal? Fort(CellIndex cell, bool countJobsUnderWay)
    {
        var province = ProvinceOf(cell);
        var level = FortLevel(province) + (countJobsUnderWay ? _fortJobs.GetValueOrDefault(province) : 0);
        return level >= WorldState.MaxFortLevel ? CivilianOrderRefusal.FortAtMaximum : null;
    }

    private CivilianOrderRefusal? RailGate(CountryId country, CellIndex cell)
    {
        var map = _world.Map;
        if (map.GetTerrain(map[cell].Terrain)?.Rail is not { } rule)
        {
            return CivilianOrderRefusal.TerrainCannotCarryRail;
        }

        return rule.RequiredTechnology is { } required && !_state.HasTechnology(country, required)
            ? CivilianOrderRefusal.ConstructionTechnologyNotKnown
            : null;
    }
}

public sealed partial class OrdersPreview
{
    /// <summary>
    /// What one of the drafting country's civilians could be ordered to do this turn,
    /// target by target, against what the rest of the draft already claims.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// The unit does not exist or belongs to another country.
    /// </exception>
    public static UnitOrdersView CreateUnitOrders(
        CompiledWorldPackage package,
        string scenarioKey,
        WorldState state,
        OrdersDraft draft,
        CivilianUnitId unit)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentException.ThrowIfNullOrWhiteSpace(scenarioKey);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(draft);
        if (!ReferenceEquals(package.GetWorld(scenarioKey), state.Definition))
        {
            throw new ArgumentException(
                "The runtime state must belong to the selected package scenario.",
                nameof(state));
        }

        if (state.GetCivilian(unit) is not { } civilian || civilian.Country != draft.Country)
        {
            throw new ArgumentException("The unit must be one of the drafting country's civilians.", nameof(unit));
        }

        var world = state.Definition;
        var map = world.Map;
        var country = draft.Country;
        var before = CashBeforeCivilians(state, draft);
        var others = draft.UnitOrders.Where(order => order.Unit != unit).ToArray();
        var drafted = draft.GetUnitOrder(unit);
        UnitOrderVerb? drafts = drafted is null ? null : CivilianOrderSimulation.VerbOf(world, state, drafted);

        // One simulation per position this unit's order could take in Core's order:
        // everything that sorts ahead of it has been charged; nothing after it has.
        var simulations = new Dictionary<UnitOrderKind, CivilianOrderSimulation>();
        CivilianOrderSimulation At(UnitOrderKind kind)
        {
            if (!simulations.TryGetValue(kind, out var simulation))
            {
                simulation = CivilianOrderSimulation.Run(
                    state, country, before, others,
                    order => order.Kind > kind || order.Kind == kind && order.Unit.Value > unit.Value).Simulation;
                simulations[kind] = simulation;
            }

            return simulation;
        }

        UnitTargetView Evaluate(CellIndex cell, UnitOrderVerb verb) =>
            At(KindOf(verb)).Evaluate(
                unit, verb, cell,
                isDrafted: drafted is not null && drafted.Cell == cell && drafts == verb);

        var type = world.CivilianTypes[civilian.Type.Value];
        var targets = new List<UnitTargetView>();
        if (type.Work == CivilianWorkKind.Construct)
        {
            foreach (var direction in HexDirections.All)
            {
                if (map.Dimensions.Contains(civilian.Cell) &&
                    map.TryGetNeighbor(civilian.Cell, direction, out var neighbor))
                {
                    targets.Add(Evaluate(neighbor, UnitOrderVerb.Rail));
                }
            }

            targets.Add(Evaluate(civilian.Cell, UnitOrderVerb.Depot));
            targets.Add(Evaluate(civilian.Cell, UnitOrderVerb.Port));
            targets.Add(Evaluate(civilian.Cell, UnitOrderVerb.Fort));
        }

        var work = CivilianOrderSimulation.WorkVerb(world, civilian.Type);
        for (var index = 0; index < map.Dimensions.CellCount; index++)
        {
            var cell = new CellIndex(index);
            var region = map[cell].Region;
            if (region.Kind != CellRegionKind.Province || state.GetProvinceOwner(region.Province) != country)
            {
                continue;
            }

            targets.Add(Evaluate(cell, UnitOrderVerb.Deploy));
            if (type.Work != CivilianWorkKind.Construct)
            {
                targets.Add(Evaluate(cell, work));
            }
        }

        var own = type.Work == CivilianWorkKind.Construct ? UnitOrderKind.Engineer : UnitOrderKind.Work;
        var order = drafted is null ? null : Evaluate(drafted.Cell, drafts!.Value);
        return new UnitOrdersView(
            Describe(world, civilian, order),
            At(own).Cash,
            Array.AsReadOnly(targets.ToArray()),
            Evaluate);
    }

    private static UnitOrderKind KindOf(UnitOrderVerb verb) => verb switch
    {
        UnitOrderVerb.Deploy => UnitOrderKind.Deploy,
        UnitOrderVerb.Improve or UnitOrderVerb.Prospect => UnitOrderKind.Work,
        _ => UnitOrderKind.Engineer,
    };

    /// <summary>
    /// Cash as Development finds it, at worst: the Armoury paid in Production, the
    /// drafted overtures paid in Diplomacy, every bid paid at world price, no sale
    /// credited. All run before Development.
    /// </summary>
    private static long CashBeforeCivilians(WorldState state, OrdersDraft draft) =>
        Math.Max(
            0,
            state.GetCash(draft.Country) - ClaimedByArmoury(state, draft) - ClaimedByOvertures(state, draft) -
            ClaimedByBids(state, draft));

    private static long ClaimedByBids(WorldState state, OrdersDraft draft) => draft.TradeBids
        .Where(static row => row.Quantity > 0)
        .Sum(row => row.Quantity * state.GetWorldPrice(row.Commodity));

    private static CivilianOrdersPreview PreviewCivilians(WorldState state, OrdersDraft draft)
    {
        var world = state.Definition;
        var country = draft.Country;
        var before = CashBeforeCivilians(state, draft);
        var (_, outcomes) = CivilianOrderSimulation.Run(state, country, before, draft.UnitOrders);
        var byUnit = outcomes.ToDictionary(static row => row.Order.Unit, static row => row.Outcome);
        var civilians = state.GetCivilians(country)
            .Select(civilian => Describe(world, civilian, byUnit.GetValueOrDefault(civilian.Id)))
            .ToArray();
        var held = civilians.Select(static civilian => civilian.Unit).ToHashSet();
        var others = outcomes
            .Where(row => !held.Contains(row.Order.Unit))
            .Select(static row => (row.Order.Unit, row.Outcome))
            .ToArray();
        return new CivilianOrdersPreview(
            state.GetCash(country),
            ClaimedByOvertures(state, draft),
            ClaimedByBids(state, draft),
            before,
            outcomes.Where(static row => row.Outcome.IsAccepted).Sum(static row => row.Outcome.CashCost),
            Array.AsReadOnly(civilians),
            Array.AsReadOnly(others))
        {
            ClaimedByArmoury = ClaimedByArmoury(state, draft),
        };
    }

    private static CivilianPreview Describe(WorldDefinition world, CivilianUnit civilian, UnitTargetView? order)
    {
        var type = world.CivilianTypes[civilian.Type.Value];
        return new CivilianPreview(
            civilian.Id,
            type.Name,
            type.Work,
            civilian.Cell,
            civilian.IsBusy,
            civilian.Work?.TurnsRemaining,
            order);
    }
}
