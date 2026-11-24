using Imperialism.Content;
using Imperialism.Core;

namespace Imperialism.Presentation;

/// <summary>One commodity's standing in a country's warehouse.</summary>
public sealed record CommodityStockView(
    string CommodityKey,
    string CommodityName,
    CommodityCategory Category,
    long Available,
    long WorldPrice,
    bool IsTradable);

/// <summary>
/// One grade of the workforce. The manual shows these on the Industry screen's
/// left border with the grade carried by the colour of the worker's coverall.
/// </summary>
public sealed record WorkforceGradeView(WorkerGrade Grade, long Total, long Sick)
{
    public long Healthy => Total - Sick;
}

/// <summary>
/// Everything a country's status border shows, detached from the state it was
/// read from.
/// </summary>
/// <remarks>
/// This exists so the client never computes a game number. The border formats
/// what is here and does no arithmetic, which is the presentation boundary
/// <c>docs/architecture.md</c> asks for rather than a convention anyone has to
/// remember.
///
/// It deliberately never touches <see cref="MapDefinition.Cells"/>. The border
/// refreshes on every navigation and every state change, while
/// <see cref="WorldViewState"/> already pays the per-cell cost once; walking the
/// map again here would make opening a screen cost the whole world. The work is
/// proportional to the commodity, technology, and ship-type counts instead,
/// which is around sixty lookups whatever the map size.
/// </remarks>
public sealed class CountryStatusView
{
    private readonly IReadOnlyList<WorkforceGradeView> _workforce;
    private readonly IReadOnlyList<CommodityStockView> _warehouse;
    private readonly IReadOnlyList<string> _technologyKeys;

    private CountryStatusView(
        CountryId country,
        string countryKey,
        string countryName,
        bool isGreatPower,
        string scenarioKey,
        string scenarioName,
        TurnDate currentDate,
        int completedTurnCount,
        long cash,
        long availableLabour,
        long transportCapacity,
        long merchantMarine,
        long totalWorkers,
        IEnumerable<WorkforceGradeView> workforce,
        IEnumerable<CommodityStockView> warehouse,
        IEnumerable<string> technologyKeys)
    {
        Country = country;
        CountryKey = countryKey;
        CountryName = countryName;
        IsGreatPower = isGreatPower;
        ScenarioKey = scenarioKey;
        ScenarioName = scenarioName;
        CurrentDate = currentDate;
        CompletedTurnCount = completedTurnCount;
        Cash = cash;
        AvailableLabour = availableLabour;
        TransportCapacity = transportCapacity;
        MerchantMarine = merchantMarine;
        TotalWorkers = totalWorkers;
        _workforce = Array.AsReadOnly(workforce.ToArray());
        _warehouse = Array.AsReadOnly(warehouse.ToArray());
        _technologyKeys = Array.AsReadOnly(technologyKeys.ToArray());
    }

    public CountryId Country { get; }

    public string CountryKey { get; }

    public string CountryName { get; }

    public bool IsGreatPower { get; }

    public string ScenarioKey { get; }

    public string ScenarioName { get; }

    public TurnDate CurrentDate { get; }

    public int CurrentYear => CurrentDate.Year;

    public int CompletedTurnCount { get; }

    public long Cash { get; }

    /// <summary>Labour this country can spend, which excludes the sick.</summary>
    public long AvailableLabour { get; }

    public long TransportCapacity { get; }

    /// <summary>Cargo holds, derived from the fleet rather than stored.</summary>
    public long MerchantMarine { get; }

    public long TotalWorkers { get; }

    /// <summary>Always three entries, lowest grade first.</summary>
    public IReadOnlyList<WorkforceGradeView> Workforce => _workforce;

    /// <summary>Every commodity in the world, in catalog order, whether stocked or not.</summary>
    public IReadOnlyList<CommodityStockView> Warehouse => _warehouse;

    /// <summary>Only the technologies this country knows.</summary>
    public IReadOnlyList<string> TechnologyKeys => _technologyKeys;

    public static CountryStatusView Create(
        CompiledWorldPackage package,
        string scenarioKey,
        WorldState state,
        CountryId country)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentException.ThrowIfNullOrWhiteSpace(scenarioKey);
        ArgumentNullException.ThrowIfNull(state);
        var world = package.GetWorld(scenarioKey);
        if (!ReferenceEquals(world, state.Definition))
        {
            throw new ArgumentException(
                "The runtime state must belong to the selected package scenario.",
                nameof(state));
        }

        if ((uint)country.Value >= (uint)world.Countries.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(country));
        }

        var definition = world.Countries[country.Value];
        var workforce = new WorkforceGradeView[WorkerGrades.Count];
        for (var index = 0; index < WorkerGrades.All.Length; index++)
        {
            var grade = WorkerGrades.All[index];
            workforce[index] = new WorkforceGradeView(
                grade,
                state.GetWorkers(country, grade),
                state.GetSickWorkers(country, grade));
        }

        var warehouse = new CommodityStockView[world.Commodities.Count];
        for (var index = 0; index < world.Commodities.Count; index++)
        {
            var commodity = world.Commodities[index];
            var price = state.GetWorldPrice(commodity.Id);
            warehouse[index] = new CommodityStockView(
                package.Catalog.GetKey(commodity.Id),
                commodity.Name,
                commodity.Category,
                state.GetAvailableQuantity(country, commodity.Id),
                price,
                commodity.WorldPrice.HasValue);
        }

        var technologies = new List<string>();
        foreach (var technology in world.Technologies)
        {
            if (state.HasTechnology(country, technology.Id))
            {
                technologies.Add(package.Catalog.GetKey(technology.Id));
            }
        }

        return new CountryStatusView(
            country,
            package.Catalog.GetKey(country),
            definition.Name,
            definition.IsGreatPower,
            scenarioKey,
            world.Scenario.Name,
            state.CurrentDate,
            state.CompletedTurnCount,
            state.GetCash(country),
            state.GetAvailableLabour(country),
            state.GetTransportCapacity(country),
            state.GetMerchantMarine(country),
            state.GetTotalWorkers(country),
            workforce,
            warehouse,
            technologies);
    }
}
