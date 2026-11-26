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
/// Ships, regiments, the University, the Trade School and the Power Plant are not
/// drafted here yet: they are rows 4e-2 and 4e-3, and their
/// <see cref="CountryTurnOrders"/> fields stay empty.
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
    private long _recruitWorkers;
    private long _buildTransportCapacity;

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
        tradePolicies: _tradePolicies);

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
    }

    private void SetUnitOrder(DraftUnitOrder order)
    {
        if (!_world.Map.Dimensions.Contains(order.Cell))
        {
            throw new ArgumentOutOfRangeException(nameof(order), "The cell is not on this world's map.");
        }

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
        where TKey : struct, IEquatable<TKey>
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
