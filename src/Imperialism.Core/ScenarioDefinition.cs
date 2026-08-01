namespace Imperialism.Core;

public sealed class ScenarioDefinition
{
    private readonly IReadOnlyList<CountryId?> _initialProvinceOwners;

    public ScenarioDefinition(
        string name,
        int startingYear,
        IEnumerable<CountryId?> initialProvinceOwners)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(initialProvinceOwners);
        Name = name;
        StartingYear = startingYear;
        _initialProvinceOwners = Array.AsReadOnly(initialProvinceOwners.ToArray());
    }

    public string Name { get; }

    public int StartingYear { get; }

    public IReadOnlyList<CountryId?> InitialProvinceOwners => _initialProvinceOwners;
}
