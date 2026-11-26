using Imperialism.Core;

namespace Imperialism.Presentation;

/// <summary>
/// One country's orders while the player is still giving them: mutable, detached
/// from <see cref="WorldState"/>, and turned into Core's inert
/// <see cref="CountryTurnOrders"/> only by <see cref="Build"/>.
/// </summary>
/// <remarks>
/// <b>The draft never refuses a quantity.</b> An over-ask is kept exactly as given
/// and goes to Core, which trims or refuses it and says why in the turn's events;
/// <see cref="OrdersPreview"/> only warns. What the draft does reject is an id the
/// world does not have, or trade in a commodity with no world price, because those
/// are a screen's bug and not a player's choice.
/// <para>
/// A row set to zero keeps its place. The Industry and Transport screens are
/// priority lists, and a slider pulled to nothing and pushed back up should not
/// fall to the bottom; <see cref="Build"/> leaves the zero rows out, because Core
/// reads an absent row as "none".
/// </para>
/// <para>
/// A civilian holds at most one order — deploy, work or an Engineer's job — and a
/// new one replaces the old, which is the original's cursor: clicking the map again
/// re-orders the selected unit. The draft keeps them by unit id and checks neither
/// the unit nor the target against the world as it stands; that is
/// <see cref="OrdersPreview.CreateUnitOrders"/>'s warning and Core's ruling.
/// </para>
/// <para>
/// Diplomacy (row 4e-1) is three lists: overtures and declarations of war, each held
/// once per (kind, target) because Core refuses a repeat, and one trade policy per
/// target, a new one replacing the old in place — the trade policies screen's icon
/// on the country. Clicking an icon again takes the order back (manual 3466–3469 and
/// 3688–3690), which is <see cref="SetOverture"/> or <see cref="SetDeclareWar"/> with
/// false, and <see cref="ClearTradePolicy"/>.
/// </para>
/// <para>
/// The Armoury and the Shipyard (row 4e-2) are one row per category or hull class,
/// kept in place like the Industry screen's rows: the dialog's arrows under each
/// picture. A regiment move names a set of regiments and a province; a regiment is
/// in at most one move, so a newer move takes its regiments out of any older one,
/// and a move left with no regiments goes — the Garrison book's selection and the
/// map cursor. Whether a move marches, rides the rail, attacks or lands is the
/// destination's to decide, as in Core, and <see cref="OrdersPreview"/>'s to show.
/// </para>
/// <para>
/// Naval orders (row 4e-2 part B) are one list, in the order given, because Core
/// resolves a country's naval orders in list order. An assembly names a set of fleets
/// and a mission; a fleet is in at most one drafted assembly, so a newer assembly
/// sharing any fleet with older ones takes the first one's place and the rest go. A
/// command names a task force and a mission, one per task force, a newer one replacing
/// the older in place — the original's select-then-click, which re-orders the selected
/// task force. Whether the fleets exist, are this country's and lie together is
/// <see cref="OrdersPreview"/>'s warning and Core's ruling, never the draft's.
/// </para>
/// <para>
/// The Trade School, the Power Plant and the University (row 4e-3). The Trade School is
/// two counts, one per step a worker may be raised; the Power Plant is one count of
/// fuel to burn; the University is one row per civilian type, kept in place like the
/// Armoury's, and a set of civilians to disband. A disband is that civilian's one order
/// this turn, because Core refuses a civilian named twice: disbanding a unit takes back
/// its deploy, work or Engineer order, and giving it one takes back its disband.
/// </para>
/// </remarks>
public sealed class OrdersDraft
{
    private readonly WorldDefinition _world;
    private readonly List<(ProductionRecipeId Recipe, long Cycles)> _production = [];
    private readonly List<ProductionFacilityId> _expansions = [];
    private readonly List<(CommodityId Commodity, long Quantity)> _transport = [];
    private readonly List<TechnologyId> _technology = [];
    private readonly List<(CommodityId Commodity, long Quantity)> _offers = [];
    private readonly List<(CommodityId Commodity, long Quantity)> _bids = [];
    private readonly SortedDictionary<long, DraftUnitOrder> _unitOrders = [];
    private readonly List<DiplomaticOverture> _overtures = [];
    private readonly List<CountryId> _declareWar = [];
    private readonly List<TradePolicyOrder> _tradePolicies = [];
    private readonly List<(ArmyCategory Category, long Count)> _regimentPurchases = [];
    private readonly List<(ShipTypeId Type, long Count)> _shipBuilds = [];
    private readonly List<DraftRegimentMove> _regimentMoves = [];
    private readonly List<DraftNavalOrder> _naval = [];
    private readonly List<(CivilianTypeId Type, long Count)> _civilianPurchases = [];
    private readonly SortedSet<long> _disbands = [];
    private long _recruitWorkers;
    private long _buildTransportCapacity;
    private long _trainUntrained;
    private long _trainTrained;
    private long _burnFuel;

    public OrdersDraft(WorldDefinition world, CountryId country)
    {
        ArgumentNullException.ThrowIfNull(world);
        if ((uint)country.Value >= (uint)world.Countries.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(country));
        }

        _world = world;
        Country = country;
    }

    public CountryId Country { get; }

    /// <summary>Production rows in priority order, zero rows included.</summary>
    public IReadOnlyList<(ProductionRecipeId Recipe, long Cycles)> Production => _production.AsReadOnly();

    public IReadOnlyList<ProductionFacilityId> Expansions => _expansions.AsReadOnly();

    /// <summary>Transport sliders in the player's order, zero rows included.</summary>
    public IReadOnlyList<(CommodityId Commodity, long Quantity)> Transport => _transport.AsReadOnly();

    public IReadOnlyList<TechnologyId> BuyTechnology => _technology.AsReadOnly();

    public IReadOnlyList<(CommodityId Commodity, long Quantity)> TradeOffers => _offers.AsReadOnly();

    public IReadOnlyList<(CommodityId Commodity, long Quantity)> TradeBids => _bids.AsReadOnly();

    /// <summary>Each civilian's one order this turn, in unit-id order.</summary>
    public IReadOnlyList<DraftUnitOrder> UnitOrders => _unitOrders.Values.ToArray();

    /// <summary>The order drafted for this civilian, or null.</summary>
    public DraftUnitOrder? GetUnitOrder(CivilianUnitId unit) =>
        _unitOrders.TryGetValue(unit.Value, out var order) ? order : null;

    /// <summary>Overtures in the order the treasury pays for them.</summary>
    public IReadOnlyList<DiplomaticOverture> Overtures => _overtures.AsReadOnly();

    /// <summary>Countries to declare war on, in the order the player clicked them.</summary>
    public IReadOnlyList<CountryId> DeclareWar => _declareWar.AsReadOnly();

    /// <summary>One trade policy per target, in the order first set.</summary>
    public IReadOnlyList<TradePolicyOrder> TradePolicies => _tradePolicies.AsReadOnly();

    /// <summary>The Armoury's rows, one per category, in the order first set.</summary>
    public IReadOnlyList<(ArmyCategory Category, long Count)> RegimentPurchases => _regimentPurchases.AsReadOnly();

    /// <summary>The Shipyard's rows, one per hull class, in the order first set.</summary>
    public IReadOnlyList<(ShipTypeId Type, long Count)> ShipBuilds => _shipBuilds.AsReadOnly();

    /// <summary>Regiment moves in the order given; no regiment is in two.</summary>
    public IReadOnlyList<DraftRegimentMove> RegimentMoves => _regimentMoves.AsReadOnly();

    /// <summary>The drafted move a regiment is in, if any.</summary>
    public DraftRegimentMove? GetRegimentMove(RegimentId regiment) =>
        _regimentMoves.FirstOrDefault(move => move.Regiments.Contains(regiment));

    /// <summary>Naval orders in the order Core resolves them; no fleet is in two assemblies.</summary>
    public IReadOnlyList<DraftNavalOrder> NavalOrders => _naval.AsReadOnly();

    /// <summary>The drafted assembly a fleet is in, if any.</summary>
    public DraftNavalOrder? GetNavalAssembly(FleetId fleet) =>
        _naval.FirstOrDefault(order => order.Kind == DraftNavalOrderKind.Assemble && order.Fleets.Contains(fleet));

    /// <summary>The drafted command for a task force, if any.</summary>
    public DraftNavalOrder? GetNavalCommand(TaskForceId taskForce) =>
        _naval.FirstOrDefault(order => order.Kind == DraftNavalOrderKind.Command && order.TaskForce == taskForce);

    /// <summary>Untrained workers asked of the Capitol this turn.</summary>
    public long RecruitWorkers
    {
        get => _recruitWorkers;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            _recruitWorkers = value;
        }
    }

    /// <summary>Capacity points ordered from the railyard this turn.</summary>
    public long BuildTransportCapacity
    {
        get => _buildTransportCapacity;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            _buildTransportCapacity = value;
        }
    }

    /// <summary>Fuel the Power Plant is to burn this turn.</summary>
    public long BurnFuel
    {
        get => _burnFuel;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            _burnFuel = value;
        }
    }

    /// <summary>The University's rows, one per civilian type, in the order first set.</summary>
    public IReadOnlyList<(CivilianTypeId Type, long Count)> CivilianPurchases => _civilianPurchases.AsReadOnly();

    /// <summary>Civilians to disband this turn, in unit-id order.</summary>
    public IReadOnlyList<CivilianUnitId> Disbands =>
        _disbands.Select(static value => new CivilianUnitId(checked((int)value))).ToArray();

    /// <summary>Whether this civilian is drafted to be disbanded.</summary>
    public bool IsDisbanding(CivilianUnitId unit) => _disbands.Contains(unit.Value);

    /// <summary>
    /// Workers of this grade sent to the Trade School to be raised one grade. Only the
    /// untrained and the trained have a step; an expert has nowhere to go.
    /// </summary>
    public long GetTraining(WorkerGrade from) => from switch
    {
        WorkerGrade.Untrained => _trainUntrained,
        WorkerGrade.Trained => _trainTrained,
        _ => 0,
    };

    /// <summary>The Trade School's arrows for one step.</summary>
    public void SetTraining(WorkerGrade from, long count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        switch (from)
        {
            case WorkerGrade.Untrained:
                _trainUntrained = count;
                break;
            case WorkerGrade.Trained:
                _trainTrained = count;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(from), "Only the untrained and the trained can be raised.");
        }
    }

    /// <summary>The University's arrows for a civilian type.</summary>
    public void SetCivilianPurchase(CivilianTypeId type, long count)
    {
        RequireIndex(type.Value, _world.CivilianTypes.Count, nameof(type));
        Set(_civilianPurchases, type, count);
    }

    public void ClearCivilianPurchases() => _civilianPurchases.Clear();

    /// <summary>
    /// Disband a civilian this turn, or take the disband back. Disbanding takes back the
    /// civilian's other drafted order: Core accepts one order per civilian.
    /// </summary>
    /// <remarks>
    /// The manual makes the click final — "unlike most orders, you cannot take this order
    /// back" (1196) — because the original disbands at once. Here nothing happens until
    /// <c>Production</c> resolves it, so taking it back before End Turn is allowed; a
    /// screen that wants the original's feel asks for confirmation and offers no undo.
    /// </remarks>
    public void SetDisband(CivilianUnitId unit, bool disband)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(unit.Value);
        if (disband)
        {
            _unitOrders.Remove(unit.Value);
            _disbands.Add(unit.Value);
        }
        else
        {
            _disbands.Remove(unit.Value);
        }
    }

    /// <summary>True when <see cref="Build"/> would submit nothing at all.</summary>
    public bool IsEmpty => Build().IsEmpty;

    /// <summary>Sets a recipe's cycles; a new recipe joins the bottom of the list.</summary>
    public void SetProduction(ProductionRecipeId recipe, long cycles)
    {
        RequireIndex(recipe.Value, _world.ProductionRecipes.Count, nameof(recipe));
        Set(_production, recipe, cycles);
    }

    public bool MoveProductionUp(ProductionRecipeId recipe) => Move(_production, recipe, -1);

    public bool MoveProductionDown(ProductionRecipeId recipe) => Move(_production, recipe, +1);

    /// <summary>Orders or cancels one rung of expansion for a facility.</summary>
    public void SetExpansion(ProductionFacilityId facility, bool expand)
    {
        RequireIndex(facility.Value, _world.ProductionFacilities.Count, nameof(facility));
        Toggle(_expansions, facility, expand);
    }

    /// <summary>Sets a transport slider; a new commodity joins the bottom of the list.</summary>
    public void SetTransport(CommodityId commodity, long quantity)
    {
        RequireIndex(commodity.Value, _world.Commodities.Count, nameof(commodity));
        Set(_transport, commodity, quantity);
    }

    public bool MoveTransportUp(CommodityId commodity) => Move(_transport, commodity, -1);

    public bool MoveTransportDown(CommodityId commodity) => Move(_transport, commodity, +1);

    /// <summary>
    /// Marks a technology for purchase or clears it — the Investment screen's
    /// "Purchasing" button, which the manual lets a player click again to cancel.
    /// </summary>
    public void SetTechnologyPurchase(TechnologyId technology, bool buy)
    {
        RequireIndex(technology.Value, _world.Technologies.Count, nameof(technology));
        Toggle(_technology, technology, buy);
    }

    public void SetOffer(CommodityId commodity, long quantity)
    {
        RequireTradable(commodity);
        Set(_offers, commodity, quantity);
    }

    public void SetBid(CommodityId commodity, long quantity)
    {
        RequireTradable(commodity);
        Set(_bids, commodity, quantity);
    }

    /// <summary>Move a civilian to a tile and leave it idle there; replaces its order.</summary>
    public void SetDeploy(CivilianUnitId unit, CellIndex cell) =>
        SetUnitOrder(new DraftUnitOrder(unit, UnitOrderKind.Deploy, cell, null));

    /// <summary>
    /// Send a civilian to improve or search a tile — its type decides which — in the
    /// one click the original's hammer and eye cursors take; replaces its order.
    /// </summary>
    public void SetWork(CivilianUnitId unit, CellIndex cell) =>
        SetUnitOrder(new DraftUnitOrder(unit, UnitOrderKind.Work, cell, null));

    /// <summary>
    /// An Engineer's job, with <see cref="EngineerOrder"/>'s fields: rail towards an
    /// adjacent tile, or a depot, port or fort on its own; replaces its order.
    /// </summary>
    public void SetEngineer(CivilianUnitId unit, CellIndex cell, EngineerConstruction structure)
    {
        if (!Enum.IsDefined(structure))
        {
            throw new ArgumentOutOfRangeException(nameof(structure));
        }

        SetUnitOrder(new DraftUnitOrder(unit, UnitOrderKind.Engineer, cell, structure));
    }

    /// <summary>Takes back a civilian's order; true when there was one.</summary>
    public bool ClearUnitOrder(CivilianUnitId unit) => _unitOrders.Remove(unit.Value);

    /// <summary>
    /// Makes or takes back an overture: a scroll clicked on a country, or its icon
    /// clicked again (manual 3466–3469). A new overture joins the end of the list,
    /// which is the order Core pays for them.
    /// </summary>
    public void SetOverture(DiplomaticOvertureKind kind, CountryId target, bool offer)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        RequireIndex(target.Value, _world.Countries.Count, nameof(target));
        Toggle(_overtures, new DiplomaticOverture(kind, target), offer);
    }

    /// <summary>Declares war on a country this turn, or takes the declaration back.</summary>
    public void SetDeclareWar(CountryId target, bool declare)
    {
        RequireIndex(target.Value, _world.Countries.Count, nameof(target));
        Toggle(_declareWar, target, declare);
    }

    /// <summary>
    /// Sets this turn's trade policy order toward a country: a subsidy at one of the
    /// world's percentages, a boycott, or <see cref="TradePolicy.None"/> to cancel what
    /// stands. Replaces any order already drafted for that country, in its place.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// A subsidy at a percentage the world's screen has no button for.
    /// </exception>
    public void SetTradePolicy(CountryId target, TradePolicy policy)
    {
        RequireIndex(target.Value, _world.Countries.Count, nameof(target));
        if (!Enum.IsDefined(policy.Kind))
        {
            throw new ArgumentOutOfRangeException(nameof(policy));
        }

        if (policy.Kind == TradePolicyKind.Subsidy &&
            !(_world.Diplomacy?.SubsidyPercents.Contains(policy.Percent) ?? false))
        {
            throw new ArgumentException(
                "Only a percentage the world offers has a button on the trade policies screen.",
                nameof(policy));
        }

        var order = new TradePolicyOrder(target, policy);
        var index = _tradePolicies.FindIndex(row => row.Target == target);
        if (index >= 0)
        {
            _tradePolicies[index] = order;
        }
        else
        {
            _tradePolicies.Add(order);
        }
    }

    /// <summary>The trade policy order drafted toward this country, or null.</summary>
    public TradePolicy? GetTradePolicy(CountryId target)
    {
        var index = _tradePolicies.FindIndex(row => row.Target == target);
        return index >= 0 ? _tradePolicies[index].Policy : null;
    }

    /// <summary>Takes back the drafted policy change; true when there was one.</summary>
    public bool ClearTradePolicy(CountryId target) =>
        _tradePolicies.RemoveAll(row => row.Target == target) > 0;

    /// <summary>Takes back every overture, declaration and policy change.</summary>
    public void ClearDiplomacy()
    {
        _overtures.Clear();
        _declareWar.Clear();
        _tradePolicies.Clear();
    }

    /// <summary>
    /// The Armoury's arrows for a category: how many regiments of its best type to
    /// raise. Which type that is, is read as the turn opens; the draft keeps only the
    /// category, as Core's order does.
    /// </summary>
    public void SetRegimentPurchase(ArmyCategory category, long count)
    {
        if (!Enum.IsDefined(category))
        {
            throw new ArgumentOutOfRangeException(nameof(category));
        }

        Set(_regimentPurchases, category, count);
    }

    /// <summary>The Shipyard's arrows for a hull class.</summary>
    public void SetShipBuild(ShipTypeId type, long count)
    {
        RequireIndex(type.Value, _world.ShipTypes.Count, nameof(type));
        Set(_shipBuilds, type, count);
    }

    /// <summary>
    /// Order these regiments to a province. Each regiment named leaves any older move
    /// it was in, so the newest order for a regiment is the one that stands.
    /// </summary>
    public void SetRegimentMove(IEnumerable<RegimentId> regiments, ProvinceId destination)
    {
        ArgumentNullException.ThrowIfNull(regiments);
        RequireIndex(destination.Value, _world.Map.Provinces.Count, nameof(destination));
        var named = regiments.Distinct().ToArray();
        if (named.Length == 0)
        {
            throw new ArgumentException("A move names at least one regiment.", nameof(regiments));
        }

        foreach (var regiment in named)
        {
            _ = ClearRegimentMove(regiment);
        }

        _regimentMoves.Add(new DraftRegimentMove(Array.AsReadOnly(named), destination));
    }

    /// <summary>Takes one regiment out of its drafted move; a move left empty goes.</summary>
    public bool ClearRegimentMove(RegimentId regiment)
    {
        var index = _regimentMoves.FindIndex(move => move.Regiments.Contains(regiment));
        if (index < 0)
        {
            return false;
        }

        var rest = _regimentMoves[index].Regiments.Where(other => other != regiment).ToArray();
        if (rest.Length == 0)
        {
            _regimentMoves.RemoveAt(index);
        }
        else
        {
            _regimentMoves[index] = _regimentMoves[index] with { Regiments = Array.AsReadOnly(rest) };
        }

        return true;
    }

    /// <summary>
    /// Group these fleets into a task force and give it a mission, in one order. An older
    /// assembly sharing any of these fleets is replaced: the first in its place, the rest
    /// removed, so the newest selection is the one that stands.
    /// </summary>
    public void SetNavalAssembly(IEnumerable<FleetId> fleets, NavalMission mission)
    {
        ArgumentNullException.ThrowIfNull(fleets);
        RequireMission(mission);
        var named = fleets.Distinct().ToArray();
        if (named.Length == 0)
        {
            throw new ArgumentException("An assembly names at least one fleet.", nameof(fleets));
        }

        var order = new DraftNavalOrder(DraftNavalOrderKind.Assemble, Array.AsReadOnly(named), null, mission);
        var first = _naval.FindIndex(row => row.Kind == DraftNavalOrderKind.Assemble && row.Fleets.Intersect(named).Any());
        if (first < 0)
        {
            _naval.Add(order);
            return;
        }

        _naval[first] = order;
        for (var index = _naval.Count - 1; index > first; index--)
        {
            if (_naval[index].Kind == DraftNavalOrderKind.Assemble && _naval[index].Fleets.Intersect(named).Any())
            {
                _naval.RemoveAt(index);
            }
        }
    }

    /// <summary>Give a task force its mission or move; a newer command replaces the older in place.</summary>
    public void SetNavalCommand(TaskForceId taskForce, NavalMission mission)
    {
        RequireMission(mission);
        var order = new DraftNavalOrder(DraftNavalOrderKind.Command, Array.Empty<FleetId>(), taskForce, mission);
        var index = _naval.FindIndex(row => row.Kind == DraftNavalOrderKind.Command && row.TaskForce == taskForce);
        if (index >= 0)
        {
            _naval[index] = order;
        }
        else
        {
            _naval.Add(order);
        }
    }

    /// <summary>Takes back the drafted assembly this fleet is in, whole.</summary>
    public bool ClearNavalAssembly(FleetId fleet) =>
        _naval.RemoveAll(row => row.Kind == DraftNavalOrderKind.Assemble && row.Fleets.Contains(fleet)) > 0;

    /// <summary>Takes back the drafted command for a task force.</summary>
    public bool ClearNavalCommand(TaskForceId taskForce) =>
        _naval.RemoveAll(row => row.Kind == DraftNavalOrderKind.Command && row.TaskForce == taskForce) > 0;

    public void ClearNavalOrders() => _naval.Clear();

    public void ClearRegimentPurchases() => _regimentPurchases.Clear();

    public void ClearShipBuilds() => _shipBuilds.Clear();

    public void ClearRegimentMoves() => _regimentMoves.Clear();

    /// <summary>The inert orders Core resolves. Zero rows are left out.</summary>
    public CountryTurnOrders Build() => new(
        Country,
        production: _production.Where(static row => row.Cycles > 0)
            .Select(static row => new ProductionOrder(row.Recipe, row.Cycles)),
        expansions: _expansions.Select(static facility => new ProductionExpansionOrder(facility)),
        recruitWorkers: _recruitWorkers,
        transport: _transport.Where(static row => row.Quantity > 0)
            .Select(static row => new TransportAllocationOrder(row.Commodity, row.Quantity)),
        buildTransportCapacity: _buildTransportCapacity,
        deployments: _unitOrders.Values
            .Where(static order => order.Kind == UnitOrderKind.Deploy)
            .Select(static order => new CivilianDeployOrder(order.Unit, order.Cell)),
        civilianWork: _unitOrders.Values
            .Where(static order => order.Kind == UnitOrderKind.Work)
            .Select(static order => new CivilianWorkOrder(order.Unit, order.Cell)),
        engineerWork: _unitOrders.Values
            .Where(static order => order.Kind == UnitOrderKind.Engineer)
            .Select(static order => new EngineerOrder(order.Unit, order.Cell, order.Structure!.Value)),
        buyTechnology: _technology,
        tradeOffers: _offers.Where(static row => row.Quantity > 0)
            .Select(static row => new TradeOrder(row.Commodity, row.Quantity)),
        tradeBids: _bids.Where(static row => row.Quantity > 0)
            .Select(static row => new TradeOrder(row.Commodity, row.Quantity)),
        declareWar: _declareWar,
        overtures: _overtures,
        tradePolicies: _tradePolicies,
        shipBuilds: _shipBuilds.Where(static row => row.Count > 0)
            .Select(static row => new ShipBuildOrder(row.Type, row.Count)),
        regimentPurchases: _regimentPurchases.Where(static row => row.Count > 0)
            .Select(static row => new RegimentPurchaseOrder(row.Category, row.Count)),
        regimentMoves: _regimentMoves.Select(static move => new RegimentMoveOrder(move.Regiments, move.Destination)),
        naval: _naval.Select(static order => order.Kind == DraftNavalOrderKind.Assemble
            ? NavalOrder.Assemble(order.Fleets, order.Mission)
            : NavalOrder.Command(order.TaskForce!.Value, order.Mission)),
        burnFuel: _burnFuel,
        training: new WorkerTrainingOrder(_trainUntrained, _trainTrained),
        civilianPurchases: _civilianPurchases.Where(static row => row.Count > 0)
            .Select(static row => new CivilianPurchaseOrder(row.Type, row.Count)),
        civilianDisbands: Disbands);

    /// <summary>
    /// A whole turn's orders: this draft in its own country's slot and empty orders
    /// for every other country, because nothing plays the rivals yet.
    /// </summary>
    public TurnOrders BuildTurnOrders() => new(Enumerable.Range(0, _world.Countries.Count)
        .Select(index => index == Country.Value
            ? Build()
            : new CountryTurnOrders(new CountryId(index))));

    /// <summary>
    /// What survives End Turn, following the manual where it speaks. See
    /// <c>docs/gui-shell.md</c>, "What an order remembers".
    /// </summary>
    /// <remarks>
    /// Kept: production at a mill or factory — "all production orders at factories
    /// and mills are saved to the next turn … the building 'remembers' how many
    /// output units you wanted" (manual 2863–2867) — kept as asked rather than clamped,
    /// because the building keeps trying to fill the order. Transport sliders are kept
    /// too, which is chosen: the manual is silent.
    /// <para>
    /// Cleared: production at a facility with no capacity limit, which is the food
    /// processing centre ("orders to this centre are not saved", 2651–2652); the
    /// railyard ("these orders are not saved", 2903–2904); technology, because a
    /// purchase cannot be cancelled once End Turn is clicked (4125–4126) and is known
    /// from then on. Facility expansion, recruitment and trade offers and bids are
    /// cleared by a chosen rule, because the manual does not say.
    /// </para>
    /// <para>
    /// Cleared, from the manual: every civilian's order. A unit "appears normally in
    /// the cycle next turn" to be ordered again (1206–1214); one whose job takes more
    /// than one turn drops out of the cycle while it works and "returns to the cycle
    /// automatically" when it finishes (1065–1069). Core keeps that job running with
    /// no new order, so nothing needs carrying here.
    /// </para>
    /// <para>
    /// Cleared, from the manual: overtures and declarations of war, which are made when
    /// End Turn is clicked — a declaration is delivered "as soon as you click the End
    /// Turn button" (3478–3479) and a consulate "opens starting the turn after you make
    /// the overture" (3516–3518); only "before the end of the turn" can one be taken back
    /// (3466–3469). A trade policy order too: what the player sets is a <em>change</em>,
    /// cancellable "before the end of the turn" (3688–3690), and Core keeps the policy
    /// standing from then on, so re-sending it would only repeat a change already made.
    /// </para>
    /// <para>
    /// Cleared, chosen with the manual's support: the Armoury's and the Shipyard's rows.
    /// The manual says they "work exactly like the University" (4874), where an order is
    /// one unit, its worker "deducted from your work force permanently" when given, and
    /// the unit appears "next turn" (4866–4872); it never says the order is saved, as it
    /// does for mills (2863). Carrying it would buy the same regiments again every turn.
    /// </para>
    /// <para>
    /// Cleared, from the manual: regiment moves. Rail capacity limits what moves "in one
    /// turn" (1757–1758), and a garrison told to do nothing does so "this turn only" and
    /// "appears normally in the cycle next turn" (1830–1831): a move is that turn's.
    /// </para>
    /// <para>
    /// Cleared: every naval order. A move, from the manual: a fleet moves no further than
    /// it can "in one turn" (1915–1917) and takes a mission or a move a turn (1887–1889),
    /// so a move is that turn's; Done keeps a fleet where it is "for that turn"
    /// (1970–1974). A landing site, from the manual: troops cross on "any subsequent
    /// turns, as long as you maintain the landing site by keeping ships in that sea zone"
    /// (1943–1945), and Core restamps a re-ordered site, so carrying the order would keep
    /// the site unusable for ever. A patrol and a blockade, chosen: the manual does not
    /// say, and Core keeps either mission standing until the task force is told to move,
    /// so re-sending it would only spend the turn repeating it. An assembly, from Core:
    /// a task force is permanent once assembled, so the order has nothing left to do.
    /// </para>
    /// <para>
    /// Cleared, from the manual: the University's rows and disbands. Civilians ordered
    /// "appear on the Terrain Map at the start of the next turn" (1168–1169), the expert
    /// "deducted from your work force permanently" when the order is given (4868–4869),
    /// so the order is that turn's and carrying it would buy the same civilians again. A
    /// disband removes the civilian, so it has nothing left to name. Cleared, from the
    /// manual: the Trade School, which "orders it to be educated … this turn"
    /// (4888–4890) and takes the trainee "out of the labour pool for the turn" (2592);
    /// re-sending the count would train a fresh batch every turn. Cleared, chosen: the
    /// Power Plant's fuel. Power "cannot be sold or stored" (2656) and is "created in the
    /// Power Plant in the same turn it is used" (2663), but the manual never says whether
    /// the burn order is saved, as it says for mills (2863).
    /// </para>
    /// </remarks>
    public void AdvanceTurn()
    {
        _production.RemoveAll(row =>
            _world.ProductionFacilities[_world.ProductionRecipes[row.Recipe.Value].Facility.Value]
                .CapacityMode == ProductionCapacityMode.Unlimited);
        _expansions.Clear();
        _recruitWorkers = 0;
        _buildTransportCapacity = 0;
        _technology.Clear();
        _offers.Clear();
        _bids.Clear();
        _unitOrders.Clear();
        ClearDiplomacy();
        _regimentPurchases.Clear();
        _shipBuilds.Clear();
        _regimentMoves.Clear();
        _naval.Clear();
        _trainUntrained = 0;
        _trainTrained = 0;
        _burnFuel = 0;
        _civilianPurchases.Clear();
        _disbands.Clear();
    }

    /// <summary>A mission naming a sea zone, country or province the world lacks is a screen's bug.</summary>
    private void RequireMission(NavalMission mission)
    {
        ArgumentNullException.ThrowIfNull(mission);
        switch (mission)
        {
            case NavalMission.MoveMission move:
                RequireIndex(move.Destination.Value, _world.Map.SeaZones.Count, nameof(mission));
                break;
            case NavalMission.BlockadeMission blockade:
                RequireIndex(blockade.Target.Value, _world.Countries.Count, nameof(mission));
                break;
            case NavalMission.LandingMission landing:
                RequireIndex(landing.Target.Value, _world.Map.Provinces.Count, nameof(mission));
                break;
        }
    }

    private void SetUnitOrder(DraftUnitOrder order)
    {
        if (!_world.Map.Dimensions.Contains(order.Cell))
        {
            throw new ArgumentOutOfRangeException(nameof(order), "The cell is not on this world's map.");
        }

        _disbands.Remove(order.Unit.Value);
        _unitOrders[order.Unit.Value] = order;
    }

    private void RequireTradable(CommodityId commodity)
    {
        RequireIndex(commodity.Value, _world.Commodities.Count, nameof(commodity));
        if (!_world.Commodities[commodity.Value].IsTradable)
        {
            throw new ArgumentException(
                "Only a commodity with a world price has a row on the Bid and Offers screen.",
                nameof(commodity));
        }
    }

    private static void RequireIndex(int value, int count, string name)
    {
        if ((uint)value >= (uint)count)
        {
            throw new ArgumentOutOfRangeException(name);
        }
    }

    private static void Set<TKey>(List<(TKey Key, long Quantity)> rows, TKey key, long quantity)
        where TKey : struct
    {
        ArgumentOutOfRangeException.ThrowIfNegative(quantity);
        var index = rows.FindIndex(row => row.Key.Equals(key));
        if (index >= 0)
        {
            rows[index] = (key, quantity);
        }
        else
        {
            rows.Add((key, quantity));
        }
    }

    private static bool Move<TKey>(List<(TKey Key, long Quantity)> rows, TKey key, int step)
        where TKey : struct, IEquatable<TKey>
    {
        var index = rows.FindIndex(row => row.Key.Equals(key));
        var target = index + step;
        if (index < 0 || target < 0 || target >= rows.Count)
        {
            return false;
        }

        (rows[index], rows[target]) = (rows[target], rows[index]);
        return true;
    }

    private static void Toggle<TKey>(List<TKey> rows, TKey key, bool present)
        where TKey : struct, IEquatable<TKey>
    {
        var has = rows.Contains(key);
        if (present && !has)
        {
            rows.Add(key);
        }
        else if (!present && has)
        {
            rows.Remove(key);
        }
    }
}

/// <summary>Which of Core's three civilian order lists a drafted order goes to.</summary>
public enum UnitOrderKind : byte
{
    Deploy,
    Work,
    Engineer,
}

/// <summary>
/// One civilian's drafted order. <see cref="Structure"/> is set exactly when
/// <see cref="Kind"/> is <see cref="UnitOrderKind.Engineer"/>.
/// </summary>
public sealed record DraftUnitOrder(
    CivilianUnitId Unit,
    UnitOrderKind Kind,
    CellIndex Cell,
    EngineerConstruction? Structure);

/// <summary>One drafted regiment move: the regiments, together, to a province.</summary>
public sealed record DraftRegimentMove(IReadOnlyList<RegimentId> Regiments, ProvinceId Destination);

/// <summary>Which of Core's two naval orders a drafted one becomes.</summary>
public enum DraftNavalOrderKind : byte
{
    Assemble,
    Command,
}

/// <summary>
/// One drafted naval order. <see cref="Fleets"/> is set for an assembly and empty for a
/// command; <see cref="TaskForce"/> is set exactly for a command.
/// </summary>
public sealed record DraftNavalOrder(
    DraftNavalOrderKind Kind,
    IReadOnlyList<FleetId> Fleets,
    TaskForceId? TaskForce,
    NavalMission Mission);
