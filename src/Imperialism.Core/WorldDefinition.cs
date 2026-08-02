namespace Imperialism.Core;

public sealed class WorldDefinition
{
    private readonly IReadOnlyList<CountryDefinition> _countries;
    private readonly IReadOnlyList<CommodityDefinition> _commodities;
    private readonly IReadOnlyList<ProductionFacilityDefinition> _productionFacilities;
    private readonly IReadOnlyList<ProductionRecipeDefinition> _productionRecipes;

    public WorldDefinition(
        MapDefinition map,
        IEnumerable<CountryDefinition> countries,
        ScenarioDefinition scenario,
        IEnumerable<CommodityDefinition>? commodities = null,
        IEnumerable<ProductionFacilityDefinition>? productionFacilities = null,
        IEnumerable<ProductionRecipeDefinition>? productionRecipes = null,
        ExtractionSettings? extraction = null)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(countries);
        ArgumentNullException.ThrowIfNull(scenario);
        var countryArray = countries.ToArray();
        var commodityArray = commodities?.ToArray() ?? [];
        var facilityArray = productionFacilities?.ToArray() ?? [];
        var recipeArray = productionRecipes?.ToArray() ?? [];
        if (countryArray.Any(static country => country is null))
        {
            throw new ArgumentException("Countries cannot contain null entries.", nameof(countries));
        }

        if (commodityArray.Any(static commodity => commodity is null))
        {
            throw new ArgumentException("Commodities cannot contain null entries.", nameof(commodities));
        }

        if (facilityArray.Any(static facility => facility is null))
        {
            throw new ArgumentException("Production facilities cannot contain null entries.", nameof(productionFacilities));
        }

        if (recipeArray.Any(static recipe => recipe is null))
        {
            throw new ArgumentException("Production recipes cannot contain null entries.", nameof(productionRecipes));
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

        for (var index = 0; index < commodityArray.Length; index++)
        {
            if (commodityArray[index].Id.Value != index)
            {
                throw new ArgumentException(
                    $"Modern commodity IDs must be dense and ordered; expected {index}, " +
                    $"got {commodityArray[index].Id.Value}.",
                    nameof(commodities));
            }
        }

        for (var index = 0; index < facilityArray.Length; index++)
        {
            if (facilityArray[index].Id.Value != index)
            {
                throw new ArgumentException(
                    $"Modern production facility IDs must be dense and ordered; expected {index}, " +
                    $"got {facilityArray[index].Id.Value}.",
                    nameof(productionFacilities));
            }
        }

        for (var index = 0; index < recipeArray.Length; index++)
        {
            var recipe = recipeArray[index];
            if (recipe.Id.Value != index)
            {
                throw new ArgumentException(
                    $"Modern production recipe IDs must be dense and ordered; expected {index}, got {recipe.Id.Value}.",
                    nameof(productionRecipes));
            }

            if ((uint)recipe.Facility.Value >= (uint)facilityArray.Length)
            {
                throw new ArgumentException(
                    $"Production recipe {index} refers to missing facility {recipe.Facility.Value}.",
                    nameof(productionRecipes));
            }

            foreach (var quantity in recipe.Inputs.Concat(recipe.Outputs))
            {
                if ((uint)quantity.Commodity.Value >= (uint)commodityArray.Length)
                {
                    throw new ArgumentException(
                        $"Production recipe {index} refers to missing commodity {quantity.Commodity.Value}.",
                        nameof(productionRecipes));
                }
            }
        }

        foreach (var resource in map.Resources)
        {
            if ((uint)resource.Commodity.Value >= (uint)commodityArray.Length)
            {
                throw new ArgumentException(
                    $"Resource {resource.Id.Value} refers to missing commodity {resource.Commodity.Value}.",
                    nameof(map));
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

        foreach (var stock in scenario.InitialInventory)
        {
            if ((uint)stock.Country.Value >= (uint)countryArray.Length)
            {
                throw new ArgumentException(
                    $"Initial inventory refers to missing country {stock.Country.Value}.",
                    nameof(scenario));
            }

            if ((uint)stock.Commodity.Value >= (uint)commodityArray.Length)
            {
                throw new ArgumentException(
                    $"Initial inventory refers to missing commodity {stock.Commodity.Value}.",
                    nameof(scenario));
            }
        }

        foreach (var capacity in scenario.InitialProductionCapacities)
        {
            if ((uint)capacity.Country.Value >= (uint)countryArray.Length)
            {
                throw new ArgumentException(
                    $"Initial production capacity refers to missing country {capacity.Country.Value}.",
                    nameof(scenario));
            }

            if ((uint)capacity.Facility.Value >= (uint)facilityArray.Length)
            {
                throw new ArgumentException(
                    $"Initial production capacity refers to missing facility {capacity.Facility.Value}.",
                    nameof(scenario));
            }

            if (facilityArray[capacity.Facility.Value].CapacityMode != ProductionCapacityMode.Limited)
            {
                throw new ArgumentException(
                    $"Unlimited facility {capacity.Facility.Value} cannot have stored capacity.",
                    nameof(scenario));
            }
        }

        try
        {
            _ = checked(countryArray.Length * commodityArray.Length);
        }
        catch (OverflowException exception)
        {
            throw new ArgumentException(
                "Country and commodity counts produce an inventory larger than the runtime can index.",
                nameof(commodities),
                exception);
        }

        try
        {
            _ = checked(countryArray.Length * facilityArray.Length);
        }
        catch (OverflowException exception)
        {
            throw new ArgumentException(
                "Country and facility counts produce a capacity array larger than the runtime can index.",
                nameof(productionFacilities),
                exception);
        }

        Map = map;
        Scenario = scenario;
        Extraction = extraction ?? ExtractionSettings.Default;
        _countries = Array.AsReadOnly(countryArray);
        _commodities = Array.AsReadOnly(commodityArray);
        _productionFacilities = Array.AsReadOnly(facilityArray);
        _productionRecipes = Array.AsReadOnly(recipeArray);
    }

    public MapDefinition Map { get; }

    public IReadOnlyList<CountryDefinition> Countries => _countries;

    public IReadOnlyList<CommodityDefinition> Commodities => _commodities;

    public IReadOnlyList<ProductionFacilityDefinition> ProductionFacilities => _productionFacilities;

    public IReadOnlyList<ProductionRecipeDefinition> ProductionRecipes => _productionRecipes;

    public ScenarioDefinition Scenario { get; }

    public ExtractionSettings Extraction { get; }

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
    private readonly long[] _availableInventory;
    private readonly long[] _productionCapacities;
    private readonly List<PendingDelivery> _pendingDeliveries = [];
    private long _nextDeliveryId = 1;

    public WorldState(WorldDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        Definition = definition;
        CurrentDate = new TurnDate(definition.Scenario.StartingYear, 1);
        _provinceOwners = definition.Scenario.InitialProvinceOwners.ToArray();
        _railLinks = definition.Scenario.InitialRailLinks.ToHashSet();
        _countryCapitals = new CellIndex?[definition.Countries.Count];
        _railConnectivity = new RailConnectivityIndex?[definition.Countries.Count];
        _availableInventory = new long[checked(definition.Countries.Count * definition.Commodities.Count)];
        _productionCapacities = new long[checked(definition.Countries.Count * definition.ProductionFacilities.Count)];
        foreach (var capital in definition.Scenario.InitialCountryCapitals)
        {
            _countryCapitals[capital.Country.Value] = capital.Cell;
        }

        foreach (var stock in definition.Scenario.InitialInventory)
        {
            _availableInventory[GetInventoryOffset(stock.Country, stock.Commodity)] = stock.Quantity;
        }

        foreach (var capacity in definition.Scenario.InitialProductionCapacities)
        {
            _productionCapacities[GetProductionCapacityOffset(capacity.Country, capacity.Facility)] = capacity.Quantity;
        }
    }

    public WorldDefinition Definition { get; }

    public int CompletedTurnCount { get; private set; }

    public TurnDate CurrentDate { get; private set; }

    public int CurrentYear => CurrentDate.Year;

    public long GetAvailableQuantity(CountryId country, CommodityId commodity) =>
        _availableInventory[GetInventoryOffset(country, commodity)];

    public long? GetProductionCapacity(CountryId country, ProductionFacilityId facility)
    {
        var offset = GetProductionCapacityOffset(country, facility);
        return Definition.ProductionFacilities[facility.Value].CapacityMode == ProductionCapacityMode.Unlimited
            ? null
            : _productionCapacities[offset];
    }

    public void SetProductionCapacity(CountryId country, ProductionFacilityId facility, long quantity)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(quantity);
        var offset = GetProductionCapacityOffset(country, facility);
        if (Definition.ProductionFacilities[facility.Value].CapacityMode == ProductionCapacityMode.Unlimited)
        {
            throw new InvalidOperationException("Unlimited production facilities do not store capacity.");
        }

        _productionCapacities[offset] = quantity;
    }

    public void AddAvailableQuantity(CountryId country, CommodityId commodity, long quantity)
    {
        ValidatePositiveQuantity(quantity);
        var offset = GetInventoryOffset(country, commodity);
        _availableInventory[offset] = checked(_availableInventory[offset] + quantity);
    }

    public bool TryConsumeAvailable(CountryId country, CommodityId commodity, long quantity)
    {
        ValidatePositiveQuantity(quantity);
        var offset = GetInventoryOffset(country, commodity);
        if (_availableInventory[offset] < quantity)
        {
            return false;
        }

        _availableInventory[offset] -= quantity;
        return true;
    }

    public DeliveryId QueuePendingDelivery(
        CountryId recipient,
        CommodityId commodity,
        long quantity,
        PendingDeliverySource source)
    {
        _ = GetInventoryOffset(recipient, commodity);
        ValidatePositiveQuantity(quantity);
        if (!Enum.IsDefined(source))
        {
            throw new ArgumentOutOfRangeException(nameof(source));
        }

        var id = new DeliveryId(_nextDeliveryId);
        _nextDeliveryId = checked(_nextDeliveryId + 1);
        _pendingDeliveries.Add(new PendingDelivery(id, recipient, commodity, quantity, source));
        return id;
    }

    public bool CancelPendingDelivery(DeliveryId delivery)
    {
        var index = _pendingDeliveries.FindIndex(item => item.Id == delivery);
        if (index < 0)
        {
            return false;
        }

        _pendingDeliveries.RemoveAt(index);
        return true;
    }

    public IReadOnlyList<PendingDelivery> GetPendingDeliveries() =>
        Array.AsReadOnly(_pendingDeliveries.ToArray());

    public long GetPendingQuantity(CountryId recipient, CommodityId commodity)
    {
        _ = GetInventoryOffset(recipient, commodity);
        var quantity = 0L;
        foreach (var delivery in _pendingDeliveries)
        {
            if (delivery.Recipient == recipient && delivery.Commodity == commodity)
            {
                quantity = checked(quantity + delivery.Quantity);
            }
        }

        return quantity;
    }

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

        // A capital may only sit in a province its country owns (the same
        // invariant the WorldDefinition constructor enforces), so a province
        // changing hands strips the previous owner's capital if it stood here.
        if (previousOwner.HasValue)
        {
            var capital = _countryCapitals[previousOwner.Value.Value];
            if (capital.HasValue &&
                Definition.Map[capital.Value].Region.Kind == CellRegionKind.Province &&
                Definition.Map[capital.Value].Region.Province == province)
            {
                _countryCapitals[previousOwner.Value.Value] = null;
            }
        }
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

            if (_provinceOwners[Definition.Map[cell.Value].Region.Province.Value] != country)
            {
                throw new ArgumentException(
                    "A capital must be in one of the country's own provinces.", nameof(cell));
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

    internal IReadOnlyList<PendingDelivery> CommitPendingDeliveries()
    {
        if (_pendingDeliveries.Count == 0)
        {
            return Array.Empty<PendingDelivery>();
        }

        var additions = new long[_availableInventory.Length];
        foreach (var delivery in _pendingDeliveries)
        {
            var offset = GetInventoryOffset(delivery.Recipient, delivery.Commodity);
            additions[offset] = checked(additions[offset] + delivery.Quantity);
        }

        for (var offset = 0; offset < additions.Length; offset++)
        {
            if (additions[offset] != 0)
            {
                _ = checked(_availableInventory[offset] + additions[offset]);
            }
        }

        var committed = _pendingDeliveries.ToArray();
        for (var offset = 0; offset < additions.Length; offset++)
        {
            _availableInventory[offset] += additions[offset];
        }

        _pendingDeliveries.Clear();
        return Array.AsReadOnly(committed);
    }

    private int GetInventoryOffset(CountryId country, CommodityId commodity)
    {
        ValidateCountry(country);
        if ((uint)commodity.Value >= (uint)Definition.Commodities.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(commodity));
        }

        return checked((country.Value * Definition.Commodities.Count) + commodity.Value);
    }

    internal long[] CopyAvailableInventory() => _availableInventory.ToArray();

    internal void PreflightInventoryChanges(long[] productionDeltas)
    {
        ArgumentNullException.ThrowIfNull(productionDeltas);
        if (productionDeltas.Length != _availableInventory.Length)
        {
            throw new ArgumentException("Production inventory delta has the wrong length.", nameof(productionDeltas));
        }

        var final = new long[_availableInventory.Length];
        for (var offset = 0; offset < final.Length; offset++)
        {
            final[offset] = checked(_availableInventory[offset] + productionDeltas[offset]);
            if (final[offset] < 0)
            {
                throw new InvalidOperationException("Production cannot make available inventory negative.");
            }
        }

        foreach (var delivery in _pendingDeliveries)
        {
            var offset = GetInventoryOffset(delivery.Recipient, delivery.Commodity);
            final[offset] = checked(final[offset] + delivery.Quantity);
        }
    }

    internal void CommitProduction(long[] inventoryDeltas)
    {
        for (var offset = 0; offset < inventoryDeltas.Length; offset++)
        {
            _availableInventory[offset] += inventoryDeltas[offset];
        }
    }

    private int GetProductionCapacityOffset(CountryId country, ProductionFacilityId facility)
    {
        ValidateCountry(country);
        if ((uint)facility.Value >= (uint)Definition.ProductionFacilities.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(facility));
        }

        return checked((country.Value * Definition.ProductionFacilities.Count) + facility.Value);
    }

    private static void ValidatePositiveQuantity(long quantity)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be positive.");
        }
    }
}
