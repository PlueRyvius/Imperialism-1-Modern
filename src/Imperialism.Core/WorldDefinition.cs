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

        MapDefinition.ValidateLinks(
            scenario.InitialRailLinks,
            map.Dimensions,
            "Rail",
            nameof(scenario));
        foreach (var rail in scenario.InitialRailLinks)
        {
            ValidateLandLink(map, rail, "Rail", nameof(scenario));
        }

        foreach (var capital in scenario.InitialCountryCapitals)
        {
            if ((uint)capital.Country.Value >= (uint)countryArray.Length)
            {
                throw new ArgumentException(
                    $"Initial capital refers to missing country {capital.Country}.",
                    nameof(scenario));
            }

            if (!map.Dimensions.Contains(capital.Cell))
            {
                throw new ArgumentException(
                    $"Initial capital for country {capital.Country} is outside the map.",
                    nameof(scenario));
            }

            var cell = map[capital.Cell];
            if (cell.SettlementSite != SettlementSiteKind.Urban ||
                cell.Region.Kind != CellRegionKind.Province)
            {
                throw new ArgumentException(
                    $"Initial capital for country {capital.Country} must be an urban province cell.",
                    nameof(scenario));
            }

            var owner = scenario.InitialProvinceOwners[cell.Region.Province.Value];
            if (owner != capital.Country)
            {
                throw new ArgumentException(
                    $"Initial capital for country {capital.Country} is not in one of its provinces.",
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

    internal static void ValidateLandLink(
        MapDefinition map,
        CellLink link,
        string description,
        string parameterName)
    {
        if (map[link.First].Region.Kind != CellRegionKind.Province ||
            map[link.Second].Region.Kind != CellRegionKind.Province)
        {
            throw new ArgumentException($"{description} links must join two land cells.", parameterName);
        }
    }
}

public sealed class WorldState
{
    private readonly CountryId?[] _provinceOwners;
    private readonly HashSet<CellLink> _railLinks;
    private readonly CellIndex?[] _countryCapitals;
    private readonly RailConnectivityIndex?[] _railConnectivity;

    public WorldState(WorldDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        Definition = definition;
        CurrentDate = new TurnDate(definition.Scenario.StartingYear, 1);
        _provinceOwners = definition.Scenario.InitialProvinceOwners.ToArray();
        _railLinks = definition.Scenario.InitialRailLinks.ToHashSet();
        _countryCapitals = new CellIndex?[definition.Countries.Count];
        _railConnectivity = new RailConnectivityIndex?[definition.Countries.Count];
        foreach (var capital in definition.Scenario.InitialCountryCapitals)
        {
            _countryCapitals[capital.Country.Value] = capital.Cell;
        }
    }

    public WorldDefinition Definition { get; }

    public int CompletedTurnCount { get; private set; }

    public TurnDate CurrentDate { get; private set; }

    public int CurrentYear => CurrentDate.Year;

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

        var previousOwner = _provinceOwners[province.Value];
        if (previousOwner == owner)
        {
            return;
        }

        _provinceOwners[province.Value] = owner;
        InvalidateRailConnectivity(previousOwner);
        InvalidateRailConnectivity(owner);
    }

    public bool HasRail(CellLink link) => _railLinks.Contains(link);

    public IReadOnlyList<CellLink> GetRailLinks() => Array.AsReadOnly(_railLinks
        .OrderBy(static link => link.First.Value)
        .ThenBy(static link => link.Second.Value)
        .ToArray());

    /// <summary>
    /// Returns a cached immutable snapshot of rail components wholly inside the
    /// country's currently owned provinces. The snapshot remains valid after
    /// later state mutations, while the next query rebuilds lazily.
    /// </summary>
    public RailConnectivityIndex GetRailConnectivity(CountryId country)
    {
        ValidateCountry(country);
        return _railConnectivity[country.Value] ??=
            RailConnectivityIndex.Create(
                Definition.Map,
                _provinceOwners,
                _railLinks,
                country);
    }

    public bool BuildRail(CellLink link)
    {
        link.Validate(Definition.Map.Dimensions, "Rail");
        WorldDefinition.ValidateLandLink(Definition.Map, link, "Rail", nameof(link));
        var changed = _railLinks.Add(link);
        if (changed)
        {
            Array.Clear(_railConnectivity);
        }

        return changed;
    }

    public bool RemoveRail(CellLink link)
    {
        var changed = _railLinks.Remove(link);
        if (changed)
        {
            Array.Clear(_railConnectivity);
        }

        return changed;
    }

    public CellIndex? GetCountryCapital(CountryId country)
    {
        ValidateCountry(country);
        return _countryCapitals[country.Value];
    }

    public void SetCountryCapital(CountryId country, CellIndex? cell)
    {
        ValidateCountry(country);
        if (cell.HasValue)
        {
            if (!Definition.Map.Dimensions.Contains(cell.Value) ||
                Definition.Map[cell.Value].SettlementSite != SettlementSiteKind.Urban ||
                Definition.Map[cell.Value].Region.Kind != CellRegionKind.Province)
            {
                throw new ArgumentException("A capital must be an urban province cell.", nameof(cell));
            }

            if (_countryCapitals.Where((value, index) => index != country.Value).Contains(cell))
            {
                throw new ArgumentException("A cell cannot be the capital of more than one country.", nameof(cell));
            }
        }

        _countryCapitals[country.Value] = cell;
    }

    private void ValidateProvince(ProvinceId province)
    {
        if ((uint)province.Value >= (uint)_provinceOwners.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(province));
        }
    }

    private void ValidateCountry(CountryId country)
    {
        if ((uint)country.Value >= (uint)_countryCapitals.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(country));
        }
    }

    private void InvalidateRailConnectivity(CountryId? country)
    {
        if (country.HasValue)
        {
            _railConnectivity[country.Value.Value] = null;
        }
    }

    internal void CompleteTurn()
    {
        CurrentDate = CurrentDate.Next();
        CompletedTurnCount = checked(CompletedTurnCount + 1);
    }
}
