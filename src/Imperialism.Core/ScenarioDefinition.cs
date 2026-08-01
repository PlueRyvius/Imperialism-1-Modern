namespace Imperialism.Core;

public sealed class ScenarioDefinition
{
    private readonly IReadOnlyList<CountryId?> _initialProvinceOwners;
    private readonly IReadOnlyList<CellLink> _initialRailLinks;
    private readonly IReadOnlyList<CountryCapital> _initialCountryCapitals;

    public ScenarioDefinition(
        string name,
        int startingYear,
        IEnumerable<CountryId?> initialProvinceOwners,
        IEnumerable<CellLink>? initialRailLinks = null,
        IEnumerable<CountryCapital>? initialCountryCapitals = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(initialProvinceOwners);
        var railArray = initialRailLinks?.ToArray() ?? [];
        var capitalArray = initialCountryCapitals?.ToArray() ?? [];
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

        Name = name;
        StartingYear = startingYear;
        _initialProvinceOwners = Array.AsReadOnly(initialProvinceOwners.ToArray());
        _initialRailLinks = Array.AsReadOnly(railArray);
        _initialCountryCapitals = Array.AsReadOnly(capitalArray);
    }

    public string Name { get; }

    public int StartingYear { get; }

    public IReadOnlyList<CountryId?> InitialProvinceOwners => _initialProvinceOwners;

    public IReadOnlyList<CellLink> InitialRailLinks => _initialRailLinks;

    public IReadOnlyList<CountryCapital> InitialCountryCapitals => _initialCountryCapitals;
}
