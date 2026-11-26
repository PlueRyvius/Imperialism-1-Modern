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
/// Civilians, the Engineer, ships, war, diplomacy and regiments are not drafted
/// here: they are rows 4d and 4e, and their <see cref="CountryTurnOrders"/> fields
/// stay empty.
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
        buyTechnology: _technology,
        tradeOffers: _offers.Where(static row => row.Quantity > 0)
            .Select(static row => new TradeOrder(row.Commodity, row.Quantity)),
        tradeBids: _bids.Where(static row => row.Quantity > 0)
            .Select(static row => new TradeOrder(row.Commodity, row.Quantity)));

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
