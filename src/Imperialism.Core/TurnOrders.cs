namespace Imperialism.Core;

/// <summary>
/// Inert orders submitted by one country. Typed order collections will be
/// added here as their systems enter Phase 3; this object never executes itself.
/// </summary>
public sealed class CountryTurnOrders
{
    private readonly IReadOnlyList<ProductionOrder> _production;
    private readonly IReadOnlyList<ProductionExpansionOrder> _expansions;

    public CountryTurnOrders(
        CountryId country,
        IEnumerable<ProductionOrder>? production = null,
        IEnumerable<ProductionExpansionOrder>? expansions = null)
    {
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

        Country = country;
        _production = Array.AsReadOnly(productionArray);
        _expansions = Array.AsReadOnly(expansionArray);
    }

    public CountryId Country { get; }

    /// <summary>Production requests in explicit allocation-priority order.</summary>
    public IReadOnlyList<ProductionOrder> Production => _production;

    /// <summary>Facilities to build one rung larger this turn.</summary>
    public IReadOnlyList<ProductionExpansionOrder> Expansions => _expansions;
}

/// <summary>
/// A request to build one facility up to its next size. The manual gives no way
/// to skip a rung or to choose a target, so the order carries only the facility.
/// </summary>
public readonly record struct ProductionExpansionOrder(ProductionFacilityId Facility);

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
