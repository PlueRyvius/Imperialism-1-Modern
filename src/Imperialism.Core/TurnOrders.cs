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

    public CountryTurnOrders(
        CountryId country,
        IEnumerable<ProductionOrder>? production = null,
        IEnumerable<ProductionExpansionOrder>? expansions = null,
        long recruitWorkers = 0,
        IEnumerable<CivilianDeployOrder>? deployments = null,
        IEnumerable<CivilianWorkOrder>? civilianWork = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(recruitWorkers);
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

        // One order per civilian per turn. The manual's cursor table gives
        // "deploy to this tile, no work this turn" its own cursor, so moving and
        // working are alternatives rather than a sequence.
        var ordered = deployArray.Select(static item => item.Unit)
            .Concat(workArray.Select(static item => item.Unit))
            .ToArray();
        if (ordered.Distinct().Count() != ordered.Length)
        {
            throw new ArgumentException(
                "A civilian can be given only one order a turn.",
                nameof(civilianWork));
        }

        Country = country;
        RecruitWorkers = recruitWorkers;
        _production = Array.AsReadOnly(productionArray);
        _expansions = Array.AsReadOnly(expansionArray);
        _deployments = Array.AsReadOnly(deployArray);
        _civilianWork = Array.AsReadOnly(workArray);
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
}

/// <summary>
/// A request to build one facility up to its next size. The manual gives no way
/// to skip a rung or to choose a target, so the order carries only the facility.
/// </summary>
public readonly record struct ProductionExpansionOrder(ProductionFacilityId Facility);

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
