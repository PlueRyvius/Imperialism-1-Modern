namespace Imperialism.Core;

/// <summary>
/// Inert orders submitted by one country. Typed order collections will be
/// added here as their systems enter Phase 3; this object never executes itself.
/// </summary>
public sealed class CountryTurnOrders
{
    private readonly IReadOnlyList<ProductionOrder> _production;
    private readonly IReadOnlyList<ProductionExpansionOrder> _expansions;
    private readonly IReadOnlyList<CivilianDeployOrder> _deployments;
    private readonly IReadOnlyList<CivilianWorkOrder> _civilianWork;
    private readonly IReadOnlyList<EngineerOrder> _engineerWork;
    private readonly IReadOnlyList<TransportAllocationOrder> _transport;
    private readonly IReadOnlyList<TechnologyId> _buyTechnology;
    private readonly IReadOnlyList<TradeOrder> _tradeOffers;
    private readonly IReadOnlyList<TradeOrder> _tradeBids;

    public CountryTurnOrders(
        CountryId country,
        IEnumerable<ProductionOrder>? production = null,
        IEnumerable<ProductionExpansionOrder>? expansions = null,
        long recruitWorkers = 0,
        IEnumerable<CivilianDeployOrder>? deployments = null,
        IEnumerable<CivilianWorkOrder>? civilianWork = null,
        IEnumerable<TransportAllocationOrder>? transport = null,
        long buildTransportCapacity = 0,
        IEnumerable<EngineerOrder>? engineerWork = null,
        IEnumerable<TechnologyId>? buyTechnology = null,
        IEnumerable<TradeOrder>? tradeOffers = null,
        IEnumerable<TradeOrder>? tradeBids = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(recruitWorkers);
        ArgumentOutOfRangeException.ThrowIfNegative(buildTransportCapacity);
        var productionArray = production?.ToArray() ?? [];
        if (productionArray.Any(static item => item.RequestedCycles <= 0))
        {
            throw new ArgumentException("Production order cycles must be positive.", nameof(production));
        }

        if (productionArray.Select(static item => item.Recipe).Distinct().Count() != productionArray.Length)
        {
            throw new ArgumentException("Production orders cannot repeat a recipe.", nameof(production));
        }

        var expansionArray = expansions?.ToArray() ?? [];
        if (expansionArray.Select(static item => item.Facility).Distinct().Count() != expansionArray.Length)
        {
            throw new ArgumentException(
                "A facility cannot be expanded twice in one turn.", nameof(expansions));
        }

        var deployArray = deployments?.ToArray() ?? [];
        var workArray = civilianWork?.ToArray() ?? [];
        var engineerArray = engineerWork?.ToArray() ?? [];
        if (engineerArray.Any(static item => !Enum.IsDefined(item.Structure)))
        {
            throw new ArgumentOutOfRangeException(nameof(engineerWork));
        }

        // One order per civilian per turn. The manual's cursor table gives
        // "deploy to this tile, no work this turn" its own cursor, so moving and
        // working are alternatives rather than a sequence — and the Engineer's
        // two cursors are alternatives to each other for the same reason.
        var ordered = deployArray.Select(static item => item.Unit)
            .Concat(workArray.Select(static item => item.Unit))
            .Concat(engineerArray.Select(static item => item.Unit))
            .ToArray();
        if (ordered.Distinct().Count() != ordered.Length)
        {
            throw new ArgumentException(
                "A civilian can be given only one order a turn.",
                nameof(civilianWork));
        }

        var transportArray = transport?.ToArray() ?? [];
        if (transportArray.Any(static item => item.Quantity <= 0))
        {
            throw new ArgumentException(
                "A transport allocation must be positive; leaving a commodity off is how you move none.",
                nameof(transport));
        }

        if (transportArray.Select(static item => item.Commodity).Distinct().Count() != transportArray.Length)
        {
            throw new ArgumentException(
                "A commodity has one slider, so it cannot be allocated twice.",
                nameof(transport));
        }

        // A technology is bought once or not at all, so naming it twice is a
        // malformed order rather than a refusal — the same standing as repeating
        // a recipe. It cannot be a refusal because the second entry has no
        // distinct meaning to refuse.
        var buyArray = buyTechnology?.ToArray() ?? [];
        if (buyArray.Distinct().Count() != buyArray.Length)
        {
            throw new ArgumentException(
                "A technology cannot be bought twice in one turn.",
                nameof(buyTechnology));
        }

        // One row per commodity on the Bid and Offers screen, so a commodity cannot
        // be offered or bid twice — the same standing as repeating a recipe. Offering
        // and bidding the same commodity in one turn is allowed and deliberately so:
        // the manual's screen has both boxes on every row, and a country arbitraging
        // its own warehouse is its own business.
        var offerArray = tradeOffers?.ToArray() ?? [];
        if (offerArray.Select(static item => item.Commodity).Distinct().Count() != offerArray.Length)
        {
            throw new ArgumentException(
                "A commodity has one offer box, so it cannot be offered twice.",
                nameof(tradeOffers));
        }

        var bidArray = tradeBids?.ToArray() ?? [];
        if (bidArray.Select(static item => item.Commodity).Distinct().Count() != bidArray.Length)
        {
            throw new ArgumentException(
                "A commodity has one bid box, so it cannot be bid twice.",
                nameof(tradeBids));
        }

        Country = country;
        RecruitWorkers = recruitWorkers;
        BuildTransportCapacity = buildTransportCapacity;
        _buyTechnology = Array.AsReadOnly(buyArray);
        _tradeOffers = Array.AsReadOnly(offerArray);
        _tradeBids = Array.AsReadOnly(bidArray);
        _production = Array.AsReadOnly(productionArray);
        _expansions = Array.AsReadOnly(expansionArray);
        _deployments = Array.AsReadOnly(deployArray);
        _civilianWork = Array.AsReadOnly(workArray);
        _engineerWork = Array.AsReadOnly(engineerArray);
        _transport = Array.AsReadOnly(transportArray);
    }

    public CountryId Country { get; }

    /// <summary>Production requests in explicit allocation-priority order.</summary>
    public IReadOnlyList<ProductionOrder> Production => _production;

    /// <summary>Facilities to build one rung larger this turn.</summary>
    public IReadOnlyList<ProductionExpansionOrder> Expansions => _expansions;

    /// <summary>
    /// Untrained workers to draw into industry through the Capitol. Capped by
    /// the country's size and by what it can pay; see
    /// <see cref="MigrationSettings"/>.
    /// </summary>
    public long RecruitWorkers { get; }

    /// <summary>Civilians to move this turn without setting them to work.</summary>
    public IReadOnlyList<CivilianDeployOrder> Deployments => _deployments;

    /// <summary>Civilians to set to work improving a tile this turn.</summary>
    public IReadOnlyList<CivilianWorkOrder> CivilianWork => _civilianWork;

    /// <summary>Engineers to set to work building the transport network this turn.</summary>
    public IReadOnlyList<EngineerOrder> EngineerWork => _engineerWork;

    /// <summary>
    /// The Transport screen's sliders, in explicit allocation-priority order. A
    /// commodity left off moves nothing, which is what a slider at zero means.
    /// </summary>
    public IReadOnlyList<TransportAllocationOrder> Transport => _transport;

    /// <summary>
    /// Points of transport capacity to buy at the railyard. Zero is the ordinary
    /// case: "since it is unlikely that you will want to increase transport
    /// capacity every turn, these orders are not saved."
    /// </summary>
    public long BuildTransportCapacity { get; }

    /// <summary>
    /// Technologies to invest in this turn, in the order the treasury pays for
    /// them. A list rather than one entry because the manual lets a player invest
    /// in several before ending the turn.
    /// </summary>
    /// <remarks>
    /// <b>The order matters only when the money runs out.</b> There is no pooling
    /// and no preflight: entries are read in turn and the first one the treasury
    /// cannot cover is refused, along with everything dearer after it. That is the
    /// same bargain two Engineers of one country already make.
    /// <para>
    /// It does <em>not</em> matter for prerequisites. Buying a technology and the
    /// thing built on it in one turn never works, whichever order they are listed
    /// in, because the research finishes after the turn ends.
    /// </para>
    /// </remarks>
    public IReadOnlyList<TechnologyId> BuyTechnology => _buyTechnology;

    /// <summary>
    /// Commodities offered for sale this turn, from the Bid and Offers screen.
    /// </summary>
    /// <remarks>
    /// A ceiling rather than a promise, the same way a transport slider is: "you cannot
    /// sell items you do not own or that you have ordered industry to use this turn", so
    /// the planner trims an offer to what the warehouse actually has left after
    /// production has claimed its inputs. What no buyer takes stays where it is.
    /// </remarks>
    public IReadOnlyList<TradeOrder> TradeOffers => _tradeOffers;

    /// <summary>
    /// Commodities bid for this turn. Also a ceiling: "wanting a resource such as coal,
    /// for example, and bidding on it, does not guarantee that your Great Power receives
    /// coal that turn."
    /// </summary>
    public IReadOnlyList<TradeOrder> TradeBids => _tradeBids;
}

/// <summary>
/// One row of the Bid and Offers screen: how much of a commodity to sell, or to try to
/// buy.
/// </summary>
/// <remarks>
/// There is no price on it. The world market sets one price per commodity per turn and
/// a country cannot name its own — "it is impossible to predict the final price for this
/// turn, because the buy bids and sell offers which determine the price come from all
/// the countries in the game, not just from your own Great Power." Per-pair pricing
/// arrives with trade subsidies, which want diplomacy.
/// </remarks>
public readonly record struct TradeOrder
{
    public TradeOrder(CommodityId commodity, long quantity)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(quantity),
                "A quantity of zero is a row left blank; leave the commodity off instead.");
        }

        Commodity = commodity;
        Quantity = quantity;
    }

    public CommodityId Commodity { get; }

    public long Quantity { get; }
}

/// <summary>
/// A request to build one facility up to its next size. The manual gives no way
/// to skip a rung or to choose a target, so the order carries only the facility.
/// </summary>
public readonly record struct ProductionExpansionOrder(ProductionFacilityId Facility);

/// <summary>
/// One Transport-screen slider: move up to this much of this commodity onto the
/// network this turn.
/// </summary>
/// <remarks>
/// A ceiling, not a demand. Orders are written before the turn resolves and a
/// player cannot know exactly what the land will yield, so the planner trims to
/// what was actually gathered and then to the capacity left — the same way a
/// production order is trimmed to inputs and capacity.
/// </remarks>
public readonly record struct TransportAllocationOrder(CommodityId Commodity, long Quantity);

/// <summary>
/// A request to build transport capacity at the railyard. Unlike a mill there is
/// no ladder and no target size: "you can build as much transport capacity as you
/// want, provided you have steel, lumber, and available labour", so the order
/// names a number of points.
/// </summary>
public readonly record struct TransportExpansionOrder(long Points);

/// <summary>
/// Move a civilian to a tile and leave it idle there. Distance is not part of
/// the order because there is nothing to spend: the manual gives civilians
/// unlimited movement and no movement-point model to build.
/// </summary>
public readonly record struct CivilianDeployOrder(CivilianUnitId Unit, CellIndex Cell);

/// <summary>
/// Set a civilian to work improving a tile. The civilian moves there in the
/// same order — the original's hammer cursor does both in one click.
/// </summary>
public readonly record struct CivilianWorkOrder(CivilianUnitId Unit, CellIndex Cell);

/// <summary>
/// Set an Engineer to work building the transport network, at a named tile.
/// </summary>
/// <remarks>
/// <b>This is the one order whose meaning the civilian's type does not settle.</b>
/// Everywhere else in this engine what a civilian does follows from what it is;
/// the manual calls the Engineer "the only civilian with multiple functions" and
/// gives it two working cursors, so the choice has to live somewhere and this is
/// where it lives.
/// <para>
/// <see cref="Cell"/> is the tile the player clicked, and it is what selects
/// between them: an <em>adjacent</em> tile lays rail towards it, and the
/// Engineer's <em>own</em> tile opens the construction dialog.
/// <see cref="Structure"/> must agree — <see cref="EngineerConstruction.Rail"/>
/// for an adjacent tile and a structure for its own — and the planner refuses
/// the order rather than guessing when they disagree.
/// </para>
/// <para>
/// Unlike <see cref="CivilianWorkOrder"/> this does <b>not</b> move the
/// Engineer. The original's track cursor builds <em>from</em> where it stands,
/// so moving first would silently change which tile the line starts at. Deploy
/// it, then order the work.
/// </para>
/// </remarks>
public readonly record struct EngineerOrder(
    CivilianUnitId Unit,
    CellIndex Cell,
    EngineerConstruction Structure);

public readonly record struct ProductionOrder
{
    public ProductionOrder(ProductionRecipeId recipe, long requestedCycles)
    {
        if (requestedCycles <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(requestedCycles), "Requested cycles must be positive.");
        }

        Recipe = recipe;
        RequestedCycles = requestedCycles;
    }

    public ProductionRecipeId Recipe { get; }

    public long RequestedCycles { get; }
}

/// <summary>A dense, country-id-ordered set of simultaneous turn submissions.</summary>
public sealed class TurnOrders
{
    private readonly IReadOnlyList<CountryTurnOrders> _byCountry;

    public TurnOrders(IEnumerable<CountryTurnOrders> byCountry)
    {
        ArgumentNullException.ThrowIfNull(byCountry);
        var orders = byCountry.ToArray();
        if (orders.Any(static item => item is null))
        {
            throw new ArgumentException("Country orders cannot contain null entries.", nameof(byCountry));
        }

        for (var index = 0; index < orders.Length; index++)
        {
            if (orders[index].Country.Value != index)
            {
                throw new ArgumentException(
                    $"Country orders must be dense and ordered; expected {index}, " +
                    $"got {orders[index].Country.Value}.",
                    nameof(byCountry));
            }
        }

        _byCountry = Array.AsReadOnly(orders);
    }

    public int Count => _byCountry.Count;

    public CountryTurnOrders this[CountryId country] =>
        (uint)country.Value < (uint)_byCountry.Count
            ? _byCountry[country.Value]
            : throw new ArgumentOutOfRangeException(nameof(country));

    public static TurnOrders Empty(int countryCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(countryCount);
        return new TurnOrders(Enumerable.Range(0, countryCount)
            .Select(static index => new CountryTurnOrders(new CountryId(index))));
    }
}
