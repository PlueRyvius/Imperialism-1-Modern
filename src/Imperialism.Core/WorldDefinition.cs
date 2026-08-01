namespace Imperialism.Core;

public sealed class WorldDefinition
{
    private readonly IReadOnlyList<CountryDefinition> _countries;

    public WorldDefinition(
        MapDefinition map,
        IEnumerable<CountryDefinition> countries,
        ScenarioDefinition scenario)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(countries);
        ArgumentNullException.ThrowIfNull(scenario);
        var countryArray = countries.ToArray();
        if (countryArray.Any(static country => country is null))
        {
            throw new ArgumentException("Countries cannot contain null entries.", nameof(countries));
        }

        for (var index = 0; index < countryArray.Length; index++)
        {
            if (countryArray[index].Id.Value != index)
            {
                throw new ArgumentException(
                    $"Modern country IDs must be dense and ordered; expected {index}, " +
                    $"got {countryArray[index].Id.Value}.",
                    nameof(countries));
            }
        }

        if (scenario.InitialProvinceOwners.Count != map.Provinces.Count)
        {
            throw new ArgumentException(
                $"Scenario has {scenario.InitialProvinceOwners.Count} province owners for " +
                $"{map.Provinces.Count} provinces.",
                nameof(scenario));
        }

        for (var province = 0; province < scenario.InitialProvinceOwners.Count; province++)
        {
            var owner = scenario.InitialProvinceOwners[province];
            if (owner.HasValue && (uint)owner.Value.Value >= (uint)countryArray.Length)
            {
                throw new ArgumentException(
                    $"Province {province} refers to missing country {owner.Value.Value}.",
                    nameof(scenario));
            }
        }

        Map = map;
        Scenario = scenario;
        _countries = Array.AsReadOnly(countryArray);
    }

    public MapDefinition Map { get; }

    public IReadOnlyList<CountryDefinition> Countries => _countries;

    public ScenarioDefinition Scenario { get; }
}

public sealed class WorldState
{
    private readonly CountryId?[] _provinceOwners;

    public WorldState(WorldDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        Definition = definition;
        CurrentYear = definition.Scenario.StartingYear;
        _provinceOwners = definition.Scenario.InitialProvinceOwners.ToArray();
    }

    public WorldDefinition Definition { get; }

    public int CurrentYear { get; internal set; }

    public CountryId? GetProvinceOwner(ProvinceId province)
    {
        ValidateProvince(province);
        return _provinceOwners[province.Value];
    }

    public void SetProvinceOwner(ProvinceId province, CountryId? owner)
    {
        ValidateProvince(province);
        if (owner.HasValue && (uint)owner.Value.Value >= (uint)Definition.Countries.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(owner));
        }

        _provinceOwners[province.Value] = owner;
    }

    private void ValidateProvince(ProvinceId province)
    {
        if ((uint)province.Value >= (uint)_provinceOwners.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(province));
        }
    }
}
