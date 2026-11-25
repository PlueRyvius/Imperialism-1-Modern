namespace Imperialism.Core;

public sealed class WorldDefinition
{
    private readonly IReadOnlyList<CountryDefinition> _countries;
    private readonly IReadOnlyList<CommodityDefinition> _commodities;
    private readonly IReadOnlyList<ProductionFacilityDefinition> _productionFacilities;
    private readonly IReadOnlyList<ProductionRecipeDefinition> _productionRecipes;
    private readonly IReadOnlyList<TechnologyDefinition> _technologies;
    private readonly IReadOnlyList<CivilianTypeDefinition> _civilianTypes;
    private readonly IReadOnlyList<ShipTypeDefinition> _shipTypes;

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
        IEnumerable<CommodityQuantity>? expansionCostPerCapacityPoint = null,
        MigrationSettings? migration = null,
        IEnumerable<CivilianTypeDefinition>? civilianTypes = null,
        TransportSettings? transport = null,
        ConstructionSettings? construction = null,
        ImprovementSettings? improvement = null,
        IEnumerable<ShipTypeDefinition>? shipTypes = null,
        ITradeMarket? trade = null,
        WorkerTrainingSettings? workerTraining = null,
        PowerSettings? power = null,
        DiplomacySettings? diplomacy = null)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(countries);
        ArgumentNullException.ThrowIfNull(scenario);
        var civilianTypeArray = civilianTypes?.ToArray() ?? [];
        if (civilianTypeArray.Any(static type => type is null))
        {
            throw new ArgumentException("Civilian types cannot contain null entries.", nameof(civilianTypes));
        }

        for (var index = 0; index < civilianTypeArray.Length; index++)
        {
            if (civilianTypeArray[index].Id.Value != index)
            {
                throw new ArgumentException(
                    $"Modern civilian type IDs must be dense and ordered; expected {index}, " +
                    $"got {civilianTypeArray[index].Id.Value}.",
                    nameof(civilianTypes));
            }
        }

        var shipTypeArray = shipTypes?.ToArray() ?? [];
        if (shipTypeArray.Any(static type => type is null))
        {
            throw new ArgumentException("Ship types cannot contain null entries.", nameof(shipTypes));
        }

        for (var index = 0; index < shipTypeArray.Length; index++)
        {
            if (shipTypeArray[index].Id.Value != index)
            {
                throw new ArgumentException(
                    $"Modern ship type IDs must be dense and ordered; expected {index}, " +
                    $"got {shipTypeArray[index].Id.Value}.",
                    nameof(shipTypes));
            }
        }

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

            // TechnologyDefinition already refuses a prerequisite at or past its
            // own id, so anything reaching here points backwards and is in range
            // by construction. This only has to catch a catalog too short to
            // contain the id it claims.
            foreach (var required in technologyArray[index].Prerequisites)
            {
                if ((uint)required.Value >= (uint)technologyArray.Length)
                {
                    throw new ArgumentException(
                        $"Technology {index} requires missing technology {required.Value}.",
                        nameof(technologies));
                }
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

        // The trade order decides which deals get cargo holds first, so two
        // commodities claiming the same slot would make the spending order depend on
        // the array's order after all -- which is the thing holding it explicitly is
        // meant to prevent.
        var tradeOrders = commodityArray
            .Where(static commodity => commodity?.TradeOrder is not null)
            .Select(static commodity => commodity!.TradeOrder!.Value)
            .ToArray();
        if (tradeOrders.Distinct().Count() != tradeOrders.Length)
        {
            throw new ArgumentException(
                "Two commodities cannot share a trade order; it decides which deals get " +
                "cargo holds first.",
                nameof(commodities));
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

        foreach (var facility in facilityArray)
        {
            if (facility.RequiredTechnology is { } required &&
                (uint)required.Value >= (uint)technologyArray.Length)
            {
                throw new ArgumentException(
                    $"Production facility {facility.Id.Value} requires missing technology {required.Value}.",
                    nameof(productionFacilities));
            }
        }

        foreach (var civilianType in civilianTypeArray)
        {
            if (civilianType.Purchase is not { } purchase)
            {
                continue;
            }

            if (purchase.Cost.Any(item => (uint)item.Commodity.Value >= (uint)commodityArray.Length))
            {
                throw new ArgumentException(
                    $"Civilian type {civilianType.Id.Value} is priced in a missing commodity.",
                    nameof(civilianTypes));
            }

            if (purchase.RequiredTechnology is { } required &&
                (uint)required.Value >= (uint)technologyArray.Length)
            {
                throw new ArgumentException(
                    $"Civilian type {civilianType.Id.Value} requires missing technology {required.Value}.",
                    nameof(civilianTypes));
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

            if (resource.ImprovedBy is { } improver &&
                (uint)improver.Value >= (uint)civilianTypeArray.Length)
            {
                throw new ArgumentException(
                    $"Resource {resource.Id.Value} is improved by missing civilian type {improver.Value}.",
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
            map,
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

        foreach (var treasury in scenario.InitialCash)
        {
            if ((uint)treasury.Country.Value >= (uint)countryArray.Length)
            {
                throw new ArgumentException(
                    $"Initial cash refers to missing country {treasury.Country.Value}.",
                    nameof(scenario));
            }
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

        foreach (var civilian in scenario.InitialCivilians)
        {
            if ((uint)civilian.Country.Value >= (uint)countryArray.Length)
            {
                throw new ArgumentException(
                    $"Initial civilian refers to missing country {civilian.Country.Value}.",
                    nameof(scenario));
            }

            if ((uint)civilian.Type.Value >= (uint)civilianTypeArray.Length)
            {
                throw new ArgumentException(
                    $"Initial civilian refers to missing civilian type {civilian.Type.Value}.",
                    nameof(scenario));
            }

            ValidateCivilianSite(map, civilian.Cell, nameof(scenario));
        }

        foreach (var ship in scenario.InitialShips)
        {
            if ((uint)ship.Country.Value >= (uint)countryArray.Length)
            {
                throw new ArgumentException(
                    $"Initial ship refers to missing country {ship.Country.Value}.",
                    nameof(scenario));
            }

            if ((uint)ship.Type.Value >= (uint)shipTypeArray.Length)
            {
                throw new ArgumentException(
                    $"Initial ship refers to missing ship type {ship.Type.Value}.",
                    nameof(scenario));
            }

            ArgumentOutOfRangeException.ThrowIfNegative(ship.Count);

            // The sea zone is deliberately not validated against the map. A ship's
            // zone is not the map's ocean zone byte -- the numberings are unrelated
            // and nothing maps one onto the other, so there is nothing to check it
            // against. See docs/scenario-semantics.md.
        }

        foreach (var army in scenario.InitialArmies)
        {
            if ((uint)army.Province.Value >= (uint)map.Provinces.Count)
            {
                throw new ArgumentException(
                    $"Initial army refers to missing province {army.Province.Value}.",
                    nameof(scenario));
            }

            ArgumentOutOfRangeException.ThrowIfNegative(army.Experience);
        }

        foreach (var ship in startingDefaults?.Ships ?? [])
        {
            if ((uint)ship.Type.Value >= (uint)shipTypeArray.Length)
            {
                throw new ArgumentException(
                    $"Starting defaults refer to missing ship type {ship.Type.Value}.",
                    nameof(startingDefaults));
            }
        }

        foreach (var cost in workerTraining?.Rules.SelectMany(static rule => rule.Cost) ?? [])
        {
            if ((uint)cost.Commodity.Value >= (uint)commodityArray.Length)
            {
                throw new ArgumentException(
                    $"Worker training refers to missing commodity {cost.Commodity.Value}.",
                    nameof(workerTraining));
            }
        }

        if (power is not null)
        {
            if ((uint)power.Fuel.Value >= (uint)commodityArray.Length)
            {
                throw new ArgumentException(
                    $"Power refers to missing commodity {power.Fuel.Value}.", nameof(power));
            }

            // A plant is held or not by its capacity, so an uncapped facility —
            // which every country holds without building — would hand every
            // country power for free. The refinery it is modelled on is capped.
            if ((uint)power.Plant.Value >= (uint)facilityArray.Length ||
                facilityArray[power.Plant.Value].CapacityMode != ProductionCapacityMode.Limited)
            {
                throw new ArgumentException(
                    $"Power needs a capacity-limited plant facility; {power.Plant.Value} is not one.",
                    nameof(power));
            }
        }

        Map = map;
        Scenario = scenario;
        Extraction = extractionSettings;
        Feeding = feeding;
        StartingDefaults = startingDefaults;
        ExpansionCostPerCapacityPoint = Array.AsReadOnly(expansionCostPerCapacityPoint?.ToArray() ?? []);
        Migration = migration;
        Transport = transport;
        Construction = construction;
        Improvement = improvement;
        Trade = trade;
        WorkerTraining = workerTraining;
        Power = power;
        Diplomacy = diplomacy;
        _technologies = Array.AsReadOnly(technologyArray);
        _civilianTypes = Array.AsReadOnly(civilianTypeArray);
        _shipTypes = Array.AsReadOnly(shipTypeArray);
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

    /// <summary>
    /// The kinds of civilian this world has. Empty means it has none, and
    /// nothing can be improved.
    /// </summary>
    public IReadOnlyList<CivilianTypeDefinition> CivilianTypes => _civilianTypes;

    /// <summary>
    /// The classes of ship this world has. Empty means it has no navy and no merchant
    /// marine, so nothing can be carried to market — which is how every world behaved
    /// before version 20.
    /// </summary>
    public IReadOnlyList<ShipTypeDefinition> ShipTypes => _shipTypes;

    /// <summary>
    /// How this world's prices answer to supply and demand, or null where prices never
    /// move — which is how every world behaved before version 20.
    /// </summary>
    /// <remarks>
    /// Null does not stop trading: a world with prices and no market still buys and sells
    /// at the opening price forever. That separation is deliberate, because the price
    /// <em>curve</em> is the guess and the prices themselves are transcribed.
    /// </remarks>
    public ITradeMarket? Trade { get; }

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
    /// How a country draws new workers into industry, or null where it
    /// cannot. See <see cref="MigrationSettings"/>.
    /// </summary>
    public MigrationSettings? Migration { get; }

    /// <summary>
    /// What it costs to carry commodities and to carry more, or null where the
    /// network has no limit at all — which is how every world behaved before
    /// capacity existed.
    /// </summary>
    public TransportSettings? Transport { get; }

    /// <summary>
    /// What an Engineer's constructions cost, or null where the world has no
    /// construction at all — which is how every world behaved before Engineers
    /// could build.
    /// </summary>
    public ConstructionSettings? Construction { get; }

    /// <summary>
    /// What raising a cell's development level costs, or null where improvement
    /// is free — which is how every world behaved before civilians were charged
    /// for their work.
    /// </summary>
    public ImprovementSettings? Improvement { get; }

    /// <summary>
    /// What the Trade School charges to raise a worker a grade, or null where
    /// nobody can be trained — which is how every world behaved before version 27.
    /// </summary>
    public WorkerTrainingSettings? WorkerTraining { get; }

    /// <summary>
    /// How a Power Plant turns fuel into labour, or null where there is no power —
    /// which is how every world behaved before version 27.
    /// </summary>
    public PowerSettings? Power { get; }

    /// <summary>
    /// What a consulate and an embassy cost, or null where neither can be bought — which
    /// is how every world behaved before version 30. Peace and pacts cost nothing and do
    /// not need it.
    /// </summary>
    public DiplomacySettings? Diplomacy { get; }

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

    /// <summary>
    /// A civilian stands on land. Whose land is a question for the rules that
    /// move it, not for the shape of the world: ownership changes with every
    /// war, and a legal world must not become illegal when a province falls.
    /// </summary>
    internal static void ValidateCivilianSite(MapDefinition map, CellIndex cell, string parameterName) =>
        ValidateStructureSite(map, cell, "Civilian", parameterName);

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

    // One bit per (country, cell): has a Prospector of this Great Power searched
    // this tile? Packed because the scale regression is 64,800 cells, where a
    // bool[] across 23 countries is 1.5 MB against 187 KB here.
    private readonly ulong[] _prospected;
    private readonly bool[] _knownTechnologies;
    private readonly HashSet<CellIndex> _ports;
    private readonly HashSet<CellIndex> _depots;
    private readonly long[] _workers;
    private readonly long[] _sickWorkers;
    private readonly long[] _transportCapacity;
    private readonly long[] _cash;
    private readonly long[] _worldPrice;
    private readonly long[] _ships;
    private readonly short[] _relationModes;
    private readonly short[] _relationTokens;
    private readonly short[] _relationPriorModes;
    private readonly byte[] _diplomaticTies;
    private readonly byte[] _tradePolicies;
    private readonly List<FleetState> _fleets;
    private System.Collections.ObjectModel.ReadOnlyCollection<FleetState>? _fleetsView;
    private readonly List<TaskForceState> _taskForces = [];
    private System.Collections.ObjectModel.ReadOnlyCollection<TaskForceState>? _taskForcesView;
    private readonly List<PendingDelivery> _pendingDeliveries = [];
    private readonly Dictionary<CivilianUnitId, CivilianUnit> _civilians = [];
    private long _nextDeliveryId = 1;
    private long _nextCivilianId = 1;
    private short _relationSequence;

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

        // Defaults first, so an explicit `ware` record still wins — the same
        // order the workforce, capacity and technology defaults use.
        if (definition.StartingDefaults is { } inventoryDefaults)
        {
            foreach (var country in definition.Scenario.DefaultStartCountries)
            {
                foreach (var stock in inventoryDefaults.Inventory)
                {
                    _availableInventory[GetInventoryOffset(country, stock.Commodity)] = stock.Quantity;
                }
            }
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
        _prospected = new ulong[
            checked((((long)definition.Countries.Count * definition.Map.Dimensions.CellCount) + 63) / 64)];
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

        _transportCapacity = new long[definition.Countries.Count];
        if (definition.StartingDefaults?.TransportCapacity is { } defaultCapacity)
        {
            foreach (var country in definition.Scenario.DefaultStartCountries)
            {
                _transportCapacity[country.Value] = defaultCapacity;
            }
        }

        foreach (var capacity in definition.Scenario.InitialTransportCapacity)
        {
            _transportCapacity[capacity.Country.Value] = capacity.Capacity;
        }

        // Defaults first, so an explicit `cash` record still wins — the same
        // order every other starting value uses.
        _cash = new long[definition.Countries.Count];
        if (definition.StartingDefaults?.Cash is { } defaultCash)
        {
            foreach (var country in definition.Scenario.DefaultStartCountries)
            {
                _cash[country.Value] = defaultCash;
            }
        }

        foreach (var treasury in definition.Scenario.InitialCash)
        {
            _cash[treasury.Country.Value] = treasury.Amount;
        }

        // One live price per commodity, seeded from the opening price and carried
        // across turns thereafter, because the manual makes the figure on the Bid and
        // Offers screen "the world market prices … during the previous turn". An
        // untraded commodity keeps a zero nothing ever reads.
        _worldPrice = new long[definition.Commodities.Count];
        for (var index = 0; index < _worldPrice.Length; index++)
        {
            _worldPrice[index] = definition.Commodities[index].WorldPrice ?? 0;
        }

        // Ships per (country, type). A dense grid rather than a list because the only
        // question anything asks is "how much cargo can this country move", which is a
        // sum over types.
        _ships = new long[
            checked(definition.Countries.Count * Math.Max(1, definition.ShipTypes.Count))];

        // Ship records add to one another rather than replacing, because the corpus
        // repeats a (country, type) combination freely — `s1` gives one power `8x2 8x1`
        // — so a fleet is a bag of records rather than a table.
        //
        // But a country the scenario equips at all ignores the default outright. That is
        // the same "an explicit record wins" rule the workforce, capacity, treasury and
        // knowledge follow; the difference is only that it takes a whole fleet at a time,
        // since adding a default Trader to an authored navy would invent a ship.
        var equipped = new bool[definition.Countries.Count];
        var fleets = new List<FleetState>();
        var nextFleetId = 1L;
        foreach (var ship in definition.Scenario.InitialShips)
        {
            equipped[ship.Country.Value] = true;
            var offset = (ship.Country.Value * definition.ShipTypes.Count) + ship.Type.Value;
            _ships[offset] = checked(_ships[offset] + ship.Count);
            // Only a resolved zone places a fleet. The raw legacy number is a
            // different numbering from the dense runtime id, so reading it as
            // one puts ships in the wrong sea wherever the legacy ids skip.
            var seaZone = ship.SeaZone;
            if (seaZone is { } zone && (uint)zone.Value >= (uint)definition.Map.SeaZones.Count)
            {
                throw new ArgumentException(
                    $"Ship record names sea zone {zone.Value}, but the map has {definition.Map.SeaZones.Count}.",
                    nameof(definition));
            }

            fleets.Add(new FleetState(
                new FleetId(nextFleetId++),
                ship.Country,
                ship.Type,
                ship.Count,
                seaZone));
        }

        _fleets = fleets;

        // The original country manager initializes its 23-by-23 mode table to
        // 4 and its separate effective-token table to -1. Scenario `rela`
        // records populate a third, raw-score table and do not replace either
        // of these runtime values.
        var relationSlots = checked(definition.Countries.Count * definition.Countries.Count);
        _relationModes = new short[relationSlots];
        _relationTokens = new short[relationSlots];
        _relationPriorModes = new short[relationSlots];
        Array.Fill(_relationModes, (short)CountryRelationMode.Standard);
        Array.Fill(_relationTokens, (short)-1);
        Array.Fill(_relationPriorModes, (short)CountryRelationMode.Standard);
        _relationSequence = definition.Scenario.InitialRelationSequence;
        foreach (var relationState in definition.Scenario.InitialRelationStates)
        {
            SetInitialRelationState(relationState);
        }

        // "At the beginning of the game, your Great Power already has embassies, which
        // include trade consulates, in every Great Power" (manual lines 3574-3577). A pair the
        // scenario authors at war starts without them, because a war between Great
        // Powers is what closes them.
        _diplomaticTies = new byte[relationSlots];

        // Every policy starts at None: the manual gives no Great Power a subsidy or a
        // boycott at the start, and the scenario format carries none.
        _tradePolicies = new byte[relationSlots];
        foreach (var owner in definition.Countries)
        {
            foreach (var host in definition.Countries)
            {
                if (owner.Id == host.Id || !owner.IsGreatPower || !host.IsGreatPower ||
                    GetRelationMode(owner.Id, host.Id) == CountryRelationMode.Hostile)
                {
                    continue;
                }

                _diplomaticTies[GetRelationOffset(owner.Id, host.Id)] =
                    (byte)((1 << (int)DiplomaticTie.Consulate) | (1 << (int)DiplomaticTie.Embassy));
            }
        }

        if (definition.StartingDefaults?.Ships is { Count: > 0 } fleet)
        {
            foreach (var country in definition.Scenario.DefaultStartCountries)
            {
                if (equipped[country.Value])
                {
                    continue;
                }

                foreach (var ship in fleet)
                {
                    _ships[(country.Value * definition.ShipTypes.Count) + ship.Type.Value] =
                        ship.Count;
                }
            }
        }

        // Everybody starts well. Illness is decided by what a workforce eats,
        // so it is runtime state a scenario cannot author: there is nothing to
        // read it from and nothing sensible to invent.
        _sickWorkers = new long[_workers.Length];
        _knownTechnologies = new bool[
            checked(definition.Countries.Count * definition.Technologies.Count)];

        // Defaults first, so an explicit record still wins — the same order the
        // workforce and capacity above use. The manual gives every power High
        // Pressure Steam Engine and Seed Drill whatever the scenario says.
        if (definition.StartingDefaults is { } technologyDefaults)
        {
            foreach (var country in definition.Scenario.DefaultStartCountries)
            {
                foreach (var technology in technologyDefaults.Technologies)
                {
                    _knownTechnologies[GetTechnologyOffset(country, technology)] = true;
                }
            }
        }

        foreach (var known in definition.Scenario.InitialCountryTechnologies)
        {
            _knownTechnologies[GetTechnologyOffset(known.Country, known.Technology)] = true;
        }

        foreach (var civilian in definition.Scenario.InitialCivilians)
        {
            _ = CreateCivilian(civilian.Country, civilian.Type, civilian.Cell);
        }
    }

    public WorldDefinition Definition { get; }

    public int CompletedTurnCount { get; private set; }

    public TurnDate CurrentDate { get; private set; }

    public int CurrentYear => CurrentDate.Year;

    /// <summary>Current effective-relation generation used by port access.</summary>
    public short RelationSequence => _relationSequence;

    public CountryRelationMode GetRelationMode(CountryId first, CountryId second) =>
        (CountryRelationMode)_relationModes[GetRelationOffset(first, second)];

    public short GetRelationToken(CountryId first, CountryId second) =>
        _relationTokens[GetRelationOffset(first, second)];

    /// <summary>
    /// Applies the original symmetric relation-mode setter. The mode's token is
    /// stamped with the current generation, so a hostile mode becomes effective
    /// after the next completed turn advances that generation.
    /// </summary>
    public void SetRelationMode(CountryId first, CountryId second, CountryRelationMode mode)
    {
        ValidateCountry(first);
        ValidateCountry(second);
        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode));
        }

        var firstOffset = GetRelationOffset(first, second);
        var secondOffset = GetRelationOffset(second, first);

        // Remember the mode in effect as this generation opened, once, so a second
        // change in the same turn cannot overwrite it. That is what lets peace take
        // effect next turn the same way war does: the mode changes now and the war
        // stays in effect until the generation advances.
        if (_relationTokens[firstOffset] != _relationSequence)
        {
            _relationPriorModes[firstOffset] = _relationModes[firstOffset];
            _relationPriorModes[secondOffset] = _relationModes[secondOffset];
        }

        _relationModes[firstOffset] = (short)mode;
        _relationModes[secondOffset] = (short)mode;
        _relationTokens[firstOffset] = _relationSequence;
        _relationTokens[secondOffset] = _relationSequence;
    }

    private void SetInitialRelationState(InitialRelationState state)
    {
        ValidateCountry(state.First);
        ValidateCountry(state.Second);
        var firstOffset = GetRelationOffset(state.First, state.Second);
        var secondOffset = GetRelationOffset(state.Second, state.First);
        _relationModes[firstOffset] = state.Mode;
        _relationModes[secondOffset] = state.Mode;
        _relationTokens[firstOffset] = state.Token;
        _relationTokens[secondOffset] = state.Token;
    }

    /// <summary>Whether the original sea-port predicate treats this pair as hostile.</summary>
    /// <remarks>
    /// A mode stamped in the current generation is not in effect yet; the mode before it
    /// is. For a war declared this turn that is the original's predicate exactly — the
    /// prior mode was not hostile. It also makes peace made this turn leave the war in
    /// effect until next turn: "any changes in diplomatic status take effect next turn"
    /// (manual lines 626-629). See <c>docs/formulas/diplomacy.md</c>.
    /// </remarks>
    public bool HasEffectiveHostility(CountryId first, CountryId second)
    {
        var offset = GetRelationOffset(first, second);
        var mode = _relationTokens[offset] == _relationSequence
            ? _relationPriorModes[offset]
            : _relationModes[offset];
        return mode == (short)CountryRelationMode.Hostile;
    }

    /// <summary>
    /// Whether <paramref name="owner"/> holds this tie in <paramref name="host"/>.
    /// </summary>
    /// <remarks>
    /// A tie granted this turn is held at once but cannot be built on until next turn:
    /// every overture is planned against the state the turn opened with, before any is
    /// carried out. That ordering is the whole of "the new consulate opens starting the
    /// turn after you make the overture" (manual lines 3516-3518), the same way
    /// <c>Investment</c> running last is the whole of "bought this turn, known next".
    /// </remarks>
    public bool HoldsDiplomaticTie(CountryId owner, CountryId host, DiplomaticTie tie) =>
        (_diplomaticTies[GetRelationOffset(owner, host)] & TieBit(tie)) != 0;

    /// <summary>Grants a tie. See <see cref="HoldsDiplomaticTie"/> for when it opens.</summary>
    internal void GrantDiplomaticTie(CountryId owner, CountryId host, DiplomaticTie tie) =>
        _diplomaticTies[GetRelationOffset(owner, host)] |= TieBit(tie);

    /// <summary>Removes a tie at once. Returns whether there was one to remove.</summary>
    internal bool RemoveDiplomaticTie(CountryId owner, CountryId host, DiplomaticTie tie)
    {
        var offset = GetRelationOffset(owner, host);
        var bit = TieBit(tie);
        if ((_diplomaticTies[offset] & bit) == 0)
        {
            return false;
        }

        _diplomaticTies[offset] &= (byte)~bit;
        return true;
    }

    /// <summary>
    /// The trade policy <paramref name="grantor"/> holds toward <paramref name="target"/>
    /// as things stand now.
    /// </summary>
    /// <remarks>
    /// The market is planned before any phase runs, so a policy set in this turn's
    /// <c>Diplomacy</c> is first read by next turn's market — the same ordering that makes a
    /// consulate bought this turn open next turn.
    /// </remarks>
    public TradePolicy GetTradePolicy(CountryId grantor, CountryId target) =>
        TradePolicy.Decode(_tradePolicies[GetRelationOffset(grantor, target)]);

    internal void SetTradePolicy(CountryId grantor, CountryId target, TradePolicy policy)
    {
        if (grantor == target)
        {
            throw new ArgumentException("A country holds no trade policy toward itself.", nameof(target));
        }

        _tradePolicies[GetRelationOffset(grantor, target)] = policy.Encode();
    }

    /// <summary>
    /// Whether no deal may clear between the pair: a war in effect, or a boycott either
    /// way. One gate with two causes — "a trade boycott goes into effect automatically
    /// between countries at war" (manual lines 3714-3715), and a chosen boycott means "no
    /// trade of any kind occurs between the two countries" (lines 3436-3437).
    /// </summary>
    public bool IsTradeBoycotted(CountryId first, CountryId second) =>
        HasEffectiveHostility(first, second) ||
        GetTradePolicy(first, second).Kind == TradePolicyKind.Boycott ||
        GetTradePolicy(second, first).Kind == TradePolicyKind.Boycott;

    private static byte TieBit(DiplomaticTie tie) =>
        Enum.IsDefined(tie)
            ? (byte)(1 << (int)tie)
            : throw new ArgumentOutOfRangeException(nameof(tie));

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

    /// <summary>
    /// Whether a Prospector of this country has searched this tile. Knowledge is
    /// per Great Power and permanent — "if a Prospector of your Great Power has
    /// already searched a tile, you see a small pickaxe and a red X" — so losing
    /// the province does not unlearn what is buried there, and gaining one does
    /// not inherit the last owner's survey.
    /// </summary>
    /// <remarks>
    /// This is the record of who has <em>looked</em>. What a country may act on
    /// is <see cref="CanSeeDeposits"/>, which is wider: a mine that has been dug
    /// is a structure standing on the ground and needs no survey to notice.
    /// </remarks>
    public bool HasProspected(CountryId country, CellIndex cell)
    {
        var bit = GetProspectedBit(country, cell);
        return (_prospected[bit >> 6] & (1UL << (int)(bit & 63))) != 0;
    }

    /// <summary>
    /// Whether this country can act on whatever hidden deposits a cell holds:
    /// either its own Prospectors have searched it, or somebody has already built
    /// on it and the workings are there to see.
    /// </summary>
    /// <remarks>
    /// The second clause is what makes conquest behave. Take a working mine and
    /// you may deepen it; take bare ground and you must still send a Prospector.
    /// It also removes the need to seed anything at world creation — a scenario
    /// that authored a development level has, by saying so, put a visible mine
    /// on the tile.
    /// </remarks>
    public bool CanSeeDeposits(CountryId country, CellIndex cell) =>
        GetCellDevelopment(cell) > 0 || HasProspected(country, cell);

    /// <summary>
    /// Records that this country has searched this tile, whether or not the
    /// search found anything. A fruitless search still counts: the original's
    /// toolbar counts down the tiles left to search, so a tile examined and
    /// found empty is no longer worth a second visit.
    /// </summary>
    public void SetProspected(CountryId country, CellIndex cell)
    {
        var bit = GetProspectedBit(country, cell);
        _prospected[bit >> 6] |= 1UL << (int)(bit & 63);
    }

    private long GetProspectedBit(CountryId country, CellIndex cell)
    {
        ValidateCountry(country);
        ValidateCell(cell);
        return ((long)country.Value * Definition.Map.Dimensions.CellCount) + cell.Value;
    }

    /// <summary>
    /// How many commodity units this country's network can move in a turn.
    /// "Transport capacity is the total number of commodities that your network
    /// can move each turn" — one point moves one unit of anything.
    /// </summary>
    public long GetTransportCapacity(CountryId country)
    {
        ValidateCountry(country);
        return _transportCapacity[country.Value];
    }

    public void SetTransportCapacity(CountryId country, long capacity)
    {
        ValidateCountry(country);
        ArgumentOutOfRangeException.ThrowIfNegative(capacity);
        _transportCapacity[country.Value] = capacity;
    }

    internal long[] CopyTransportCapacity() => _transportCapacity.ToArray();

    /// <summary>
    /// What one unit of this commodity fetches on the world market right now, or zero
    /// for a commodity that is never traded.
    /// </summary>
    /// <remarks>
    /// This is <em>world</em> state rather than per-country: "advances become available
    /// on a world-wide basis" is technology's rule, and the market's is the same shape —
    /// one price everybody sees. Per-country pricing arrives only with trade subsidies,
    /// which want diplomacy.
    /// <para>
    /// It persists across turns, which is the point: the manual makes this turn's
    /// starting figure "the world market prices for the commodities traded during the
    /// previous turn", so the market has a memory and a country can wait for a better
    /// one.
    /// </para>
    /// </remarks>
    public long GetWorldPrice(CommodityId commodity)
    {
        ValidateCommodity(commodity);
        return _worldPrice[commodity.Value];
    }

    public void SetWorldPrice(CommodityId commodity, long price)
    {
        ValidateCommodity(commodity);
        ArgumentOutOfRangeException.ThrowIfNegative(price);
        _worldPrice[commodity.Value] = price;
    }

    /// <summary>How many ships of this class this country owns.</summary>
    public long GetShipCount(CountryId country, ShipTypeId type)
    {
        ValidateCountry(country);
        ValidateShipType(type);
        return _ships[(country.Value * Definition.ShipTypes.Count) + type.Value];
    }

    public void SetShipCount(CountryId country, ShipTypeId type, long count)
    {
        ValidateCountry(country);
        ValidateShipType(type);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        _ships[(country.Value * Definition.ShipTypes.Count) + type.Value] = count;
    }

    /// <summary>
    /// Scenario-authored fleets in deterministic record order, followed by fleets
    /// the Shipyard has started, in the order they were built.
    /// </summary>
    /// <remarks>
    /// Ids are dense and never reused: fleet <c>n</c> is always at index
    /// <c>n - 1</c>, and a new fleet takes the next id. The view is the state's own
    /// and cannot be edited by a caller.
    /// </remarks>
    public IReadOnlyList<FleetState> Fleets => _fleetsView ??= _fleets.AsReadOnly();

    /// <summary>
    /// Delivers newly built ships to a country: they are counted at once, so they add
    /// their holds to the merchant marine, and they join a fleet.
    /// </summary>
    /// <remarks>
    /// Merchant hulls (null or zero firepower) join the first unplaced fleet of their
    /// class that is in no task force. Warships join the first fleet of their class in
    /// the capital's adjacent sea zone that is in no task force; when no such zone or
    /// capital exists, they remain unplaced. A fleet already in a task force is never
    /// merged into, because doing so would move the new ships with it.
    /// </remarks>
    internal FleetId DeliverShips(CountryId country, ShipTypeId type, long count)
    {
        ValidateCountry(country);
        ValidateShipType(type);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);

        var hull = Definition.ShipTypes[type.Value];
        var isMerchant = hull.Combat is null || hull.Combat.Firepower == 0;
        var seaZone = isMerchant ? null : GetCapitalSeaZone(country);

        var offset = (country.Value * Definition.ShipTypes.Count) + type.Value;
        var total = checked(_ships[offset] + count);
        foreach (var fleet in _fleets)
        {
            var matchingZone = isMerchant
                ? fleet.SeaZone is null
                : seaZone.HasValue && fleet.SeaZone == seaZone;
            if (fleet.Country == country && fleet.Type == type && matchingZone && fleet.TaskForce is null)
            {
                fleet.Count = checked(fleet.Count + count);
                _ships[offset] = total;
                return fleet.Id;
            }
        }

        var created = new FleetState(
            new FleetId(checked(_fleets.Count + 1L)), country, type, count, seaZone);
        _fleets.Add(created);
        _ships[offset] = total;
        return created.Id;
    }

    private SeaZoneId? GetCapitalSeaZone(CountryId country)
    {
        if (GetCountryCapital(country) is not { } capital)
        {
            return null;
        }

        var map = Definition.Map;
        SeaZoneId? lowest = null;
        foreach (var direction in HexDirections.All)
        {
            // The map's own adjacency, so a capital on a wrapping map's east or
            // west edge sees the water across the seam, as port access does.
            if (!map.TryGetNeighbor(capital, direction, out var neighborCell))
            {
                continue;
            }

            var neighbor = map[neighborCell];
            if (neighbor.Region.Kind != CellRegionKind.SeaZone)
            {
                continue;
            }

            var seaZone = neighbor.Region.SeaZone;
            if (lowest is null || seaZone.Value < lowest.Value.Value)
            {
                lowest = seaZone;
            }
        }

        return lowest;
    }

    public FleetState GetFleet(FleetId fleet)
    {
        var index = fleet.Value - 1;
        return (ulong)index < (ulong)_fleets.Count
            ? _fleets[(int)index]
            : throw new ArgumentOutOfRangeException(nameof(fleet));
    }

    /// <summary>
    /// Assembles whole, co-located fleet records into a task force. The original
    /// keeps ship attachment separate from its patrol, blockade, landing, and
    /// sailing state, so assembly has no mission side effect.
    /// </summary>
    public TaskForceState AssembleTaskForce(CountryId country, IEnumerable<FleetId> fleets)
    {
        ValidateCountry(country);
        ArgumentNullException.ThrowIfNull(fleets);
        var members = fleets.OrderBy(static fleet => fleet.Value).ToArray();
        if (members.Length == 0)
        {
            throw new ArgumentException("A task force needs at least one fleet.", nameof(fleets));
        }

        if (members.Distinct().Count() != members.Length)
        {
            throw new ArgumentException("A fleet can be selected only once.", nameof(fleets));
        }

        SeaZoneId? seaZone = null;
        foreach (var id in members)
        {
            var fleet = GetFleet(id);
            if (fleet.Country != country)
            {
                throw new InvalidOperationException("A task force cannot include a foreign fleet.");
            }

            if (fleet.SeaZone is not { } position)
            {
                throw new InvalidOperationException("An unpositioned fleet cannot join a task force.");
            }

            if (fleet.TaskForce is not null)
            {
                throw new InvalidOperationException("A fleet already belongs to a task force.");
            }

            if (seaZone is { } known && known != position)
            {
                throw new InvalidOperationException("Task-force fleets must occupy the same sea zone.");
            }

            seaZone = position;
        }

        var taskForce = new TaskForceState(
            new TaskForceId(checked(_taskForces.Count + 1L)),
            country,
            seaZone!.Value,
            members);
        foreach (var id in members)
        {
            GetFleet(id).TaskForce = taskForce.Id;
        }

        _taskForces.Add(taskForce);
        return taskForce;
    }

    /// <summary>Task forces in deterministic assembly order.</summary>
    public IReadOnlyList<TaskForceState> TaskForces => _taskForcesView ??= _taskForces.AsReadOnly();

    public TaskForceState GetTaskForce(TaskForceId taskForce)
    {
        var index = taskForce.Value - 1;
        return (ulong)index < (ulong)_taskForces.Count
            ? _taskForces[(int)index]
            : throw new ArgumentOutOfRangeException(nameof(taskForce));
    }

    /// <summary>
    /// Places a task force into the original's port-control qualifying patrol
    /// state. This alone has no tactical-combat or port-access side effect.
    /// </summary>
    public void PatrolTaskForce(CountryId country, TaskForceId taskForce)
    {
        ValidateCountry(country);
        var force = GetTaskForce(taskForce);
        if (force.Country != country)
        {
            throw new InvalidOperationException("A country cannot patrol with a foreign task force.");
        }

        if (force.PlannedSeaZone is not null)
        {
            throw new InvalidOperationException("A sailing leg must resolve before a task force can patrol.");
        }

        force.Activity = TaskForceActivity.Patrolling;
    }

    /// <summary>
    /// Plans one original-style strategic sailing leg. The destination is
    /// reduced to a shortest-path leg no longer than the slowest selected hull,
    /// then <see cref="ResolveTaskForceMoves"/> applies it separately.
    /// </summary>
    /// <remarks>
    /// The executable keeps planning (state 1) and the member-position update
    /// in distinct routines. Their relative place in the wider simultaneous
    /// turn is still unproven, so this headless boundary remains explicit.
    /// </remarks>
    public TaskForceMovePlan PlanTaskForceMove(
        CountryId country,
        TaskForceId taskForce,
        SeaZoneId destination)
    {
        ValidateCountry(country);
        if ((uint)destination.Value >= (uint)Definition.Map.SeaZones.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(destination));
        }

        var force = GetTaskForce(taskForce);
        if (force.Country != country)
        {
            throw new InvalidOperationException("A country cannot sail a foreign task force.");
        }

        if (force.PlannedSeaZone is not null)
        {
            throw new InvalidOperationException("A task force already has a sailing leg awaiting resolution.");
        }

        var maximumSeaZones = force.Fleets
            .Select(id => Definition.ShipTypes[GetFleet(id).Type.Value].SeaZones)
            .Min();
        var resolved = ResolveSailingLeg(force.SeaZone, destination, maximumSeaZones);
        force.PlannedSeaZone = resolved;
        // The original sailing planner switches the task force to state 1, so
        // it no longer qualifies as state-3 patrol control while in transit.
        force.Activity = TaskForceActivity.Idle;
        return new TaskForceMovePlan(
            force.Id,
            force.SeaZone,
            destination,
            resolved,
            maximumSeaZones);
    }

    /// <summary>
    /// Applies all planned sailing legs in deterministic task-force ID order.
    /// No combat, interception, or continuation order is implied.
    /// </summary>
    public IReadOnlyList<TaskForceMoveResolution> ResolveTaskForceMoves()
    {
        var resolutions = new List<TaskForceMoveResolution>();
        foreach (var force in _taskForces)
        {
            if (force.PlannedSeaZone is not { } destination)
            {
                continue;
            }

            var from = force.SeaZone;
            foreach (var fleetId in force.Fleets)
            {
                GetFleet(fleetId).SeaZone = destination;
            }

            force.SeaZone = destination;
            force.PlannedSeaZone = null;
            resolutions.Add(new TaskForceMoveResolution(force.Id, from, destination));
        }

        return resolutions.AsReadOnly();
    }

    private SeaZoneId ResolveSailingLeg(
        SeaZoneId origin,
        SeaZoneId destination,
        long maximumSeaZones)
    {
        if (maximumSeaZones <= 0 || origin == destination)
        {
            return origin;
        }

        var distance = new Dictionary<SeaZoneId, int> { [destination] = 0 };
        var frontier = new Queue<SeaZoneId>();
        frontier.Enqueue(destination);
        while (frontier.TryDequeue(out var current))
        {
            foreach (var neighbor in Definition.Map.SeaTopology.GetNeighbors(current))
            {
                if (distance.TryAdd(neighbor, checked(distance[current] + 1)))
                {
                    frontier.Enqueue(neighbor);
                }
            }
        }

        // An unreachable request produces a zero-length resolved leg, matching
        // the original planner's decreasing-distance walk without inventing a
        // modern refusal rule.
        if (!distance.TryGetValue(origin, out var remaining))
        {
            return origin;
        }

        var currentZone = origin;
        for (var step = 0L; step < maximumSeaZones && remaining > 0; step++)
        {
            currentZone = Definition.Map.SeaTopology.GetNeighbors(currentZone)
                .First(neighbor => distance.TryGetValue(neighbor, out var next) && next < remaining);
            remaining--;
        }

        return currentZone;
    }

    /// <summary>
    /// This country's merchant marine: the total cargo holds across every ship it owns.
    /// </summary>
    /// <remarks>
    /// "The merchant marine number represents the total cargo holds available in all the
    /// merchant ships owned by your Great Power. Each cargo hold can carry one unit of
    /// any trading commodity."
    /// <para>
    /// <b>Derived rather than stored</b>, so it cannot drift from the fleet. Warships
    /// contribute zero, which is what makes a navy and a merchant marine two different
    /// numbers built at the same shipyard. Minor nations own none, and the manual says
    /// so outright — nothing enforces that here, because it falls out of a scenario not
    /// giving them any.
    /// </para>
    /// <para>
    /// It is a <em>capacity</em>, not a stock: the pool spent during a turn is separate,
    /// because "each cargo hold can be used only once per turn" and refills next turn.
    /// </para>
    /// </remarks>
    public long GetMerchantMarine(CountryId country)
    {
        ValidateCountry(country);
        var total = 0L;
        var types = Definition.ShipTypes;
        for (var index = 0; index < types.Count; index++)
        {
            total = checked(total + (_ships[(country.Value * types.Count) + index] * types[index].Cargo));
        }

        return total;
    }

    /// <summary>
    /// This country's treasury. "Each Great Power begins the game with a limited
    /// amount of cash which is totally inadequate to meet its needs."
    /// </summary>
    /// <remarks>
    /// Income is gold and gems converting as the network carries them, and trade —
    /// which the manual calls the first of three and which is the larger of the two by
    /// far. Overseas profits, the third, want colonies. Outgoings are what an Engineer
    /// builds, what a civilian's improvement costs, and what technology is bought for.
    /// </remarks>
    public long GetCash(CountryId country)
    {
        ValidateCountry(country);
        return _cash[country.Value];
    }

    public void SetCash(CountryId country, long amount)
    {
        ValidateCountry(country);
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        _cash[country.Value] = amount;
    }

    public void AddCash(CountryId country, long amount)
    {
        ValidateCountry(country);
        ValidatePositiveQuantity(amount);
        _cash[country.Value] = checked(_cash[country.Value] + amount);
    }

    /// <summary>
    /// Spends from the treasury, or refuses and changes nothing. The same
    /// all-or-nothing shape <see cref="TryConsumeAvailable"/> uses: a structure
    /// half paid for is not a structure.
    /// </summary>
    public bool TrySpendCash(CountryId country, long amount)
    {
        ValidateCountry(country);
        ValidatePositiveQuantity(amount);
        if (_cash[country.Value] < amount)
        {
            return false;
        }

        _cash[country.Value] -= amount;
        return true;
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

    /// <summary>
    /// Puts a new civilian on the map and issues it an id. Ids are never
    /// reused, so an event naming a civilian that has since died still names
    /// only that one.
    /// </summary>
    public CivilianUnitId CreateCivilian(CountryId country, CivilianTypeId type, CellIndex cell)
    {
        ValidateCountry(country);
        ValidateCivilianType(type);
        WorldDefinition.ValidateCivilianSite(Definition.Map, cell, nameof(cell));
        var id = new CivilianUnitId(_nextCivilianId);
        _nextCivilianId = checked(_nextCivilianId + 1);
        _civilians[id] = new CivilianUnit(id, country, type, cell);
        return id;
    }

    public CivilianUnit? GetCivilian(CivilianUnitId unit) =>
        _civilians.TryGetValue(unit, out var civilian) ? civilian : null;

    /// <summary>Every civilian on the map, oldest first.</summary>
    public IReadOnlyList<CivilianUnit> GetCivilians() => Array.AsReadOnly(_civilians.Values
        .OrderBy(static civilian => civilian.Id.Value)
        .ToArray());

    public IReadOnlyList<CivilianUnit> GetCivilians(CountryId country)
    {
        ValidateCountry(country);
        return Array.AsReadOnly(_civilians.Values
            .Where(civilian => civilian.Country == country)
            .OrderBy(static civilian => civilian.Id.Value)
            .ToArray());
    }

    /// <summary>
    /// Removes a civilian. The manual's rule is that losing a province kills the
    /// civilians in it; Conflict is not modelled, so nothing calls this yet.
    /// </summary>
    public bool RemoveCivilian(CivilianUnitId unit) => _civilians.Remove(unit);

    /// <summary>
    /// Moves a civilian, cancelling any work it had begun. Legality — whose
    /// land, and how far — belongs to the phase that issues the order, not
    /// here; this is the primitive it commits through.
    /// </summary>
    public void MoveCivilian(CivilianUnitId unit, CellIndex cell)
    {
        var civilian = RequireCivilian(unit);
        WorldDefinition.ValidateCivilianSite(Definition.Map, cell, nameof(cell));
        _civilians[unit] = new CivilianUnit(unit, civilian.Country, civilian.Type, cell);
    }

    /// <summary>Sets a civilian to work where it stands, or clears its job with null.</summary>
    internal void SetCivilianWork(CivilianUnitId unit, CivilianWorkInProgress? work)
    {
        var civilian = RequireCivilian(unit);
        if (work is { } job && job.Cell != civilian.Cell)
        {
            throw new ArgumentException("A civilian works the tile it stands on.", nameof(work));
        }

        _civilians[unit] = new CivilianUnit(unit, civilian.Country, civilian.Type, civilian.Cell, work);
    }

    private CivilianUnit RequireCivilian(CivilianUnitId unit) =>
        _civilians.TryGetValue(unit, out var civilian)
            ? civilian
            : throw new ArgumentOutOfRangeException(nameof(unit), $"No civilian {unit} exists.");

    private void ValidateCivilianType(CivilianTypeId type)
    {
        if ((uint)type.Value >= (uint)Definition.CivilianTypes.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(type));
        }
    }

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
        link.Validate(Definition.Map, "Rail");
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

    private void ValidateCommodity(CommodityId commodity)
    {
        if ((uint)commodity.Value >= (uint)_worldPrice.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(commodity));
        }
    }

    private void ValidateShipType(ShipTypeId type)
    {
        if ((uint)type.Value >= (uint)Definition.ShipTypes.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(type));
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
        // The original keeps this as a signed 16-bit generation and only ever
        // compares it for equality with a stamped token, so it wraps. Throwing
        // here would abort a turn after every phase had already mutated state.
        _relationSequence = unchecked((short)(_relationSequence + 1));
        CurrentDate = CurrentDate.Next();
        CompletedTurnCount = checked(CompletedTurnCount + 1);
    }

    private int GetRelationOffset(CountryId first, CountryId second)
    {
        ValidateCountry(first);
        ValidateCountry(second);
        return checked((first.Value * Definition.Countries.Count) + second.Value);
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
