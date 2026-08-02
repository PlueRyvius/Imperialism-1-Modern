namespace Imperialism.Core;

public sealed class ScenarioDefinition
{
    private readonly IReadOnlyList<CountryId?> _initialProvinceOwners;
    private readonly IReadOnlyList<CellLink> _initialRailLinks;
    private readonly IReadOnlyList<CountryCapital> _initialCountryCapitals;
    private readonly IReadOnlyList<InitialCommodityStock> _initialInventory;
    private readonly IReadOnlyList<InitialProductionCapacity> _initialProductionCapacities;

    public ScenarioDefinition(
        string name,
        int startingYear,
        IEnumerable<CountryId?> initialProvinceOwners,
        IEnumerable<CellLink>? initialRailLinks = null,
        IEnumerable<CountryCapital>? initialCountryCapitals = null,
        IEnumerable<InitialCommodityStock>? initialInventory = null,
        IEnumerable<InitialProductionCapacity>? initialProductionCapacities = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(initialProvinceOwners);
        var railArray = initialRailLinks?.ToArray() ?? [];
        var capitalArray = initialCountryCapitals?.ToArray() ?? [];
        var inventoryArray = initialInventory?.ToArray() ?? [];
        var capacityArray = initialProductionCapacities?.ToArray() ?? [];
        if (capacityArray.Any(static item => item.Quantity <= 0))
        {
            throw new ArgumentException(
                "Initial production capacity quantities must be positive.",
                nameof(initialProductionCapacities));
        }
        if (railArray.Length != railArray.Distinct().Count())
        {
            throw new ArgumentException("Initial rail links cannot contain duplicates.", nameof(initialRailLinks));
        }

        if (capitalArray.Select(static capital => capital.Country).Distinct().Count() != capitalArray.Length)
        {
            throw new ArgumentException(
                "A country cannot have more than one initial capital.",
                nameof(initialCountryCapitals));
        }

        if (capitalArray.Select(static capital => capital.Cell).Distinct().Count() != capitalArray.Length)
        {
            throw new ArgumentException(
                "A cell cannot be the initial capital of more than one country.",
                nameof(initialCountryCapitals));
        }

        if (inventoryArray.Select(static stock => (stock.Country, stock.Commodity)).Distinct().Count() !=
            inventoryArray.Length)
        {
            throw new ArgumentException(
                "Initial inventory cannot contain duplicate country and commodity entries.",
                nameof(initialInventory));
        }

        if (capacityArray.Select(static item => (item.Country, item.Facility)).Distinct().Count() !=
            capacityArray.Length)
        {
            throw new ArgumentException(
                "Initial production capacities cannot contain duplicate country and facility entries.",
                nameof(initialProductionCapacities));
        }

        Name = name;
        StartingYear = startingYear;
        _initialProvinceOwners = Array.AsReadOnly(initialProvinceOwners.ToArray());
        _initialRailLinks = Array.AsReadOnly(railArray);
        _initialCountryCapitals = Array.AsReadOnly(capitalArray);
        _initialInventory = Array.AsReadOnly(inventoryArray);
        _initialProductionCapacities = Array.AsReadOnly(capacityArray);
    }

    public string Name { get; }

    public int StartingYear { get; }

    public IReadOnlyList<CountryId?> InitialProvinceOwners => _initialProvinceOwners;

    public IReadOnlyList<CellLink> InitialRailLinks => _initialRailLinks;

    public IReadOnlyList<CountryCapital> InitialCountryCapitals => _initialCountryCapitals;

    public IReadOnlyList<InitialCommodityStock> InitialInventory => _initialInventory;

    public IReadOnlyList<InitialProductionCapacity> InitialProductionCapacities =>
        _initialProductionCapacities;
}
