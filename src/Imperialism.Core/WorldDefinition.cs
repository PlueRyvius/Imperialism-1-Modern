namespace Imperialism.Core;

public sealed class WorldDefinition
{
    private readonly IReadOnlyList<CountryDefinition> _countries;
    private readonly IReadOnlyList<CommodityDefinition> _commodities;
    private readonly IReadOnlyList<ProductionFacilityDefinition> _productionFacilities;
    private readonly IReadOnlyList<ProductionRecipeDefinition> _productionRecipes;
    private readonly IReadOnlyList<TechnologyDefinition> _technologies;

    public WorldDefinition(
        MapDefinition map,
        IEnumerable<CountryDefinition> countries,
        ScenarioDefinition scenario,
        IEnumerable<CommodityDefinition>? commodities = null,
        IEnumerable<ProductionFacilityDefinition>? productionFacilities = null,
        IEnumerable<ProductionRecipeDefinition>? productionRecipes = null,
        ExtractionSettings? extraction = null,
        IEnumerable<TechnologyDefinition>? technologies = null,
        FeedingSettings? feeding = null,
        StartingDefaults? startingDefaults = null,
        IEnumerable<CommodityQuantity>? expansionCostPerCapacityPoint = null)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(countries);
        ArgumentNullException.ThrowIfNull(scenario);
        var technologyArray = technologies?.ToArray() ?? [];
        if (technologyArray.Any(static technology => technology is null))
        {
            throw new ArgumentException("Technologies cannot contain null entries.", nameof(technologies));
        }

        for (var index = 0; index < technologyArray.Length; index++)
        {
            if (technologyArray[index].Id.Value != index)
            {
                throw new ArgumentException(
                    $"Modern technology IDs must be dense and ordered; expected {index}, " +
                    $"got {technologyArray[index].Id.Value}.",
                    nameof(technologies));
            }
        }

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

            if (resource.RequiredTechnology is { } required &&
                (uint)required.Value >= (uint)technologyArray.Length)
            {
                throw new ArgumentException(
                    $"Resource {resource.Id.Value} requires missing technology {required.Value}.",
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

        foreach (var development in scenario.InitialCellDevelopment)
        {
            if (!map.Dimensions.Contains(development.Cell))
            {
                throw new ArgumentException(
                    $"Initial development refers to cell {development.Cell} outside the map.",
                    nameof(scenario));
            }

            if (map[development.Cell].Region.Kind != CellRegionKind.Province)
            {
                throw new ArgumentException(
                    $"Initial development on cell {development.Cell} is not on land.",
                    nameof(scenario));
            }
        }

        foreach (var port in scenario.InitialPorts)
        {
            ValidatePortSite(map, port, nameof(scenario));
        }

        foreach (var depot in scenario.InitialDepots)
        {
            ValidateDepotSite(map, depot, nameof(scenario));
        }

        foreach (var known in scenario.InitialCountryTechnologies)
        {
            if ((uint)known.Country.Value >= (uint)countryArray.Length)
            {
                throw new ArgumentException(
                    $"Initial technology refers to missing country {known.Country.Value}.",
                    nameof(scenario));
            }

            if ((uint)known.Technology.Value >= (uint)technologyArray.Length)
            {
                throw new ArgumentException(
                    $"Initial technology refers to missing technology {known.Technology.Value}.",
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

        var extractionSettings = extraction ?? ExtractionSettings.Default;
        if (extractionSettings.PortFishing is { } fishing &&
            (uint)fishing.Commodity.Value >= (uint)commodityArray.Length)
        {
            throw new ArgumentException(
                $"Port fishing refers to missing commodity {fishing.Commodity.Value}.",
                nameof(extraction));
        }

        if (feeding is not null)
        {
            foreach (var commodity in feeding.PreferenceCycle
                .SelectMany(static preference => preference.Accepted)
                .Concat(feeding.CannedFood is { } canned ? [canned] : Array.Empty<CommodityId>()))
            {
                if ((uint)commodity.Value >= (uint)commodityArray.Length)
                {
                    throw new ArgumentException(
                        $"Feeding refers to missing commodity {commodity.Value}.",
                        nameof(feeding));
                }
            }
        }

        foreach (var workforce in scenario.InitialWorkforce)
        {
            if ((uint)workforce.Country.Value >= (uint)countryArray.Length)
            {
                throw new ArgumentException(
                    $"Initial workforce refers to missing country {workforce.Country.Value}.",
                    nameof(scenario));
            }
        }

        Map = map;
        Scenario = scenario;
        Extraction = extractionSettings;
        Feeding = feeding;
        StartingDefaults = startingDefaults;
        ExpansionCostPerCapacityPoint = Array.AsReadOnly(expansionCostPerCapacityPoint?.ToArray() ?? []);
        _technologies = Array.AsReadOnly(technologyArray);
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

    public IReadOnlyList<TechnologyDefinition> Technologies => _technologies;

    /// <summary>Null in a world whose workers never eat.</summary>
    public FeedingSettings? Feeding { get; }

    /// <summary>
    /// What a power begins with when the scenario is silent — the fair start a
    /// skirmish runs on. Applied only to
    /// <see cref="ScenarioDefinition.DefaultStartCountries"/>.
    /// </summary>
    public StartingDefaults? StartingDefaults { get; }

    /// <summary>
    /// What one point of production capacity costs to build. The manual is
    /// exact: one lumber and one steel per point, and expansion needs no
    /// labour. Empty means facilities cannot be expanded at all.
    /// </summary>
    public IReadOnlyList<CommodityQuantity> ExpansionCostPerCapacityPoint { get; }

    /// <summary>
    /// A port stands on land. Verified against every <c>port</c> record in the
    /// shipped corpus: 124 of 124 are on a land cell.
    /// </summary>
    /// <remarks>
    /// The manual also says ports always require access to water, and that holds
    /// across the whole corpus — but only when adjacency wraps east-west the way
    /// the 1997 grid does. <c>s3</c> puts a port on the last column of row 0
    /// whose sole water neighbour lies across the seam. This grid does not wrap,
    /// so Core cannot tell that port from a landlocked one and does not try; the
    /// legacy importer checks the rule with wrapping adjacency, where it means
    /// something.
    /// </remarks>
    internal static void ValidatePortSite(MapDefinition map, CellIndex cell, string parameterName) =>
        ValidateStructureSite(map, cell, "Port", parameterName);

    /// <summary>
    /// A depot stands on land. Every depot in the shipped corpus also sits on a
    /// railed cell, but rail is mutable state — tearing up a line would turn a
    /// legal world illegal — so that is not enforced here. The importer warns
    /// about it instead, where it is a statement about the source file.
    /// </summary>
    internal static void ValidateDepotSite(MapDefinition map, CellIndex cell, string parameterName) =>
        ValidateStructureSite(map, cell, "Depot", parameterName);

    private static void ValidateStructureSite(
        MapDefinition map,
        CellIndex cell,
        string description,
        string parameterName)
    {
        if (!map.Dimensions.Contains(cell))
        {
            throw new ArgumentException($"{description} cell {cell} is outside the map.", parameterName);
        }

        if (map[cell].Region.Kind != CellRegionKind.Province)
        {
            throw new ArgumentException($"{description} cell {cell} is not on land.", parameterName);
        }
    }

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
    private readonly int[] _cellDevelopment;
    private readonly bool[] _knownTechnologies;
    private readonly HashSet<CellIndex> _ports;
    private readonly HashSet<CellIndex> _depots;
    private readonly long[] _workers;
    private readonly long[] _sickWorkers;
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

        // Defaults first, so an explicit record still wins. This mirrors the
        // original, where a scenario that says nothing about a power's industry
        // gets the engine's fair start rather than nothing at all.
        if (definition.StartingDefaults is { } defaults)
        {
            foreach (var country in definition.Scenario.DefaultStartCountries)
            {
                foreach (var capacity in defaults.ProductionCapacities)
                {
                    _productionCapacities[
                        GetProductionCapacityOffset(country, capacity.Facility)] = capacity.Quantity;
                }
            }
        }

        foreach (var capacity in definition.Scenario.InitialProductionCapacities)
        {
            _productionCapacities[GetProductionCapacityOffset(capacity.Country, capacity.Facility)] = capacity.Quantity;
        }

        _cellDevelopment = new int[definition.Map.Dimensions.CellCount];
        foreach (var development in definition.Scenario.InitialCellDevelopment)
        {
            _cellDevelopment[development.Cell.Value] = development.Level;
        }

        _ports = definition.Scenario.InitialPorts.ToHashSet();
        _depots = definition.Scenario.InitialDepots.ToHashSet();
        _workers = new long[checked(definition.Countries.Count * WorkerGrades.Count)];
        if (definition.StartingDefaults?.Workforce is { } defaultWorkforce)
        {
            foreach (var country in definition.Scenario.DefaultStartCountries)
            {
                foreach (var grade in WorkerGrades.All)
                {
                    _workers[GetWorkerOffset(country, grade)] = defaultWorkforce[grade];
                }
            }
        }

        foreach (var workforce in definition.Scenario.InitialWorkforce)
        {
            foreach (var grade in WorkerGrades.All)
            {
                _workers[GetWorkerOffset(workforce.Country, grade)] = workforce[grade];
            }
        }

        // Everybody starts well. Illness is decided by what a workforce eats,
        // so it is runtime state a scenario cannot author: there is nothing to
        // read it from and nothing sensible to invent.
        _sickWorkers = new long[_workers.Length];
        _knownTechnologies = new bool[
            checked(definition.Countries.Count * definition.Technologies.Count)];
        foreach (var known in definition.Scenario.InitialCountryTechnologies)
        {
            _knownTechnologies[GetTechnologyOffset(known.Country, known.Technology)] = true;
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

    /// <summary>How far a cell has been improved. Zero is undeveloped.</summary>
    public int GetCellDevelopment(CellIndex cell)
    {
        ValidateCell(cell);
        return _cellDevelopment[cell.Value];
    }

    /// <summary>
    /// Sets a cell's improvement level. Land only: the original's own
    /// <c>deve</c> records never name an ocean cell in any shipped scenario.
    /// </summary>
    public void SetCellDevelopment(CellIndex cell, int level)
    {
        ValidateCell(cell);
        ArgumentOutOfRangeException.ThrowIfNegative(level);
        if (Definition.Map[cell].Region.Kind != CellRegionKind.Province)
        {
            throw new ArgumentException("Only land cells can be developed.", nameof(cell));
        }

        _cellDevelopment[cell.Value] = level;
    }

    public bool HasPort(CellIndex cell) => _ports.Contains(cell);

    public IReadOnlyList<CellIndex> GetPorts() => Array.AsReadOnly(_ports
        .OrderBy(static cell => cell.Value)
        .ToArray());

    public bool BuildPort(CellIndex cell)
    {
        WorldDefinition.ValidatePortSite(Definition.Map, cell, nameof(cell));
        return _ports.Add(cell);
    }

    public bool RemovePort(CellIndex cell) => _ports.Remove(cell);

    public long GetWorkers(CountryId country, WorkerGrade grade) =>
        _workers[GetWorkerOffset(country, grade)];

    public void SetWorkers(CountryId country, WorkerGrade grade, long count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        var offset = GetWorkerOffset(country, grade);
        _workers[offset] = count;

        // A grade can never hold more sick workers than workers. Starvation
        // shrinks a grade through this method, so clamping here is what stops
        // a country that lost its whole workforce still carrying invalids.
        _sickWorkers[offset] = Math.Min(_sickWorkers[offset], count);
    }

    /// <summary>
    /// Workers of this grade that ate the wrong thing at the last
    /// <see cref="TurnPhase.Feeding"/> and supply no labour until the next one.
    /// </summary>
    public long GetSickWorkers(CountryId country, WorkerGrade grade) =>
        _sickWorkers[GetWorkerOffset(country, grade)];

    /// <summary>
    /// Records how many of a country's workers fell ill, taking the cheapest
    /// grades first, and clears anyone who recovered.
    /// </summary>
    /// <remarks>
    /// **Which grade falls ill is a choice, not a finding**, and it mirrors the
    /// one starvation already makes: the cheapest workers, so illness costs the
    /// player least. Feeding walks its workforce by position in the preference
    /// cycle and never learns a worker's grade, so somebody has to decide. See
    /// <c>docs/formulas/feeding.md</c>.
    ///
    /// The count is rewritten in full rather than accumulated, which is what
    /// makes recovery automatic: a workforce that eats properly reports none
    /// sick and gets its whole pool back.
    /// </remarks>
    internal void SetSickWorkers(CountryId country, long sick)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(sick);
        foreach (var grade in WorkerGrades.All)
        {
            var offset = GetWorkerOffset(country, grade);
            var ill = Math.Min(_workers[offset], sick);
            _sickWorkers[offset] = ill;
            sick -= ill;
        }
    }

    public long GetTotalWorkers(CountryId country)
    {
        var total = 0L;
        foreach (var grade in WorkerGrades.All)
        {
            total = checked(total + _workers[GetWorkerOffset(country, grade)]);
        }

        return total;
    }

    /// <summary>
    /// What the workforce can do this turn: the pool <see cref="TurnPhase.Production"/>
    /// spends against each recipe's <see cref="ProductionRecipeDefinition.LabourCost"/>.
    /// Workers who fell ill at the last <see cref="TurnPhase.Feeding"/> are
    /// excluded. Zero when the world defines no feeding, which is also the case
    /// where production ignores labour entirely.
    /// </summary>
    /// <remarks>
    /// Illness is diagnosed after production has already run, so it is paid for
    /// on the following turn. That is the faithful ordering rather than a
    /// concession to the pipeline: food is eaten as the turn ends, and the arm
    /// icon the player allocates against must already know who is unwell.
    /// </remarks>
    public long GetAvailableLabour(CountryId country)
    {
        ValidateCountry(country);
        if (Definition.Feeding is not { } feeding)
        {
            return 0;
        }

        var labour = 0L;
        foreach (var grade in WorkerGrades.All)
        {
            var offset = GetWorkerOffset(country, grade);
            labour = checked(labour +
                ((_workers[offset] - _sickWorkers[offset]) * feeding.GetLabour(grade)));
        }

        return labour;
    }

    private int GetWorkerOffset(CountryId country, WorkerGrade grade)
    {
        ValidateCountry(country);
        if (!Enum.IsDefined(grade))
        {
            throw new ArgumentOutOfRangeException(nameof(grade));
        }

        return checked((country.Value * WorkerGrades.Count) + (int)grade);
    }

    public bool HasDepot(CellIndex cell) => _depots.Contains(cell);

    public IReadOnlyList<CellIndex> GetDepots() => Array.AsReadOnly(_depots
        .OrderBy(static cell => cell.Value)
        .ToArray());

    public bool BuildDepot(CellIndex cell)
    {
        WorldDefinition.ValidateDepotSite(Definition.Map, cell, nameof(cell));
        return _depots.Add(cell);
    }

    public bool RemoveDepot(CellIndex cell) => _depots.Remove(cell);

    public bool HasTechnology(CountryId country, TechnologyId technology) =>
        _knownTechnologies[GetTechnologyOffset(country, technology)];

    /// <summary>
    /// Grants knowledge outright. There is no research system yet, so this is
    /// how a technology is acquired at all; it is deliberately not tied to a
    /// cost, a turn, or a prerequisite.
    /// </summary>
    public void GrantTechnology(CountryId country, TechnologyId technology) =>
        _knownTechnologies[GetTechnologyOffset(country, technology)] = true;

    /// <summary>
    /// Takes up to <paramref name="quantity"/> out of the country's pending
    /// deliveries and returns how much was actually taken.
    /// </summary>
    /// <remarks>
    /// Workers eat food transported this turn before food already in the
    /// warehouse — one of the two documented same-resolution exceptions to
    /// deferred delivery. Entries are consumed oldest first so the order is
    /// deterministic, and one drained to nothing is removed rather than left as
    /// a zero-quantity record.
    /// </remarks>
    public long ConsumePending(CountryId recipient, CommodityId commodity, long quantity)
    {
        _ = GetInventoryOffset(recipient, commodity);
        ArgumentOutOfRangeException.ThrowIfNegative(quantity);
        var taken = 0L;
        for (var index = 0; index < _pendingDeliveries.Count && taken < quantity; index++)
        {
            var delivery = _pendingDeliveries[index];
            if (delivery.Recipient != recipient || delivery.Commodity != commodity)
            {
                continue;
            }

            var take = Math.Min(delivery.Quantity, quantity - taken);
            taken += take;
            if (take == delivery.Quantity)
            {
                _pendingDeliveries.RemoveAt(index--);
                continue;
            }

            _pendingDeliveries[index] = new PendingDelivery(
                delivery.Id,
                delivery.Recipient,
                delivery.Commodity,
                delivery.Quantity - take,
                delivery.Source);
        }

        return taken;
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

    private void ValidateCell(CellIndex cell)
    {
        if (!Definition.Map.Dimensions.Contains(cell))
        {
            throw new ArgumentOutOfRangeException(nameof(cell));
        }
    }

    private int GetTechnologyOffset(CountryId country, TechnologyId technology)
    {
        ValidateCountry(country);
        if ((uint)technology.Value >= (uint)Definition.Technologies.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(technology));
        }

        return checked((country.Value * Definition.Technologies.Count) + technology.Value);
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
