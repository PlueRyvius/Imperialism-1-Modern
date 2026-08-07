using Imperialism.Core;

namespace Imperialism.Content;

public static class WorldContentCompiler
{
    public static CompiledWorldContent Compile(WorldContentDocument document)
    {
        var package = CompilePackage(document);
        if (package.ScenarioKeys.Count != 1)
        {
            throw Error(
                "scenarios",
                "Compile(document) requires exactly one scenario; use Compile(document, scenarioKey) " +
                "or CompilePackage(document) for multi-scenario packages.");
        }

        return new CompiledWorldContent(
            package.GetWorld(package.ScenarioKeys[0]),
            package.Catalog);
    }

    public static CompiledWorldContent Compile(WorldContentDocument document, string scenarioKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scenarioKey);
        var package = CompilePackage(document);
        try
        {
            return new CompiledWorldContent(package.GetWorld(scenarioKey), package.Catalog);
        }
        catch (KeyNotFoundException exception)
        {
            throw Error("scenarioKey", exception.Message, exception);
        }
    }

    public static CompiledWorldPackage CompilePackage(WorldContentDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        ValidateEnvelope(document);

        var terrainContent = RequireArray(document.Terrains, "terrains");
        var civilianTypeContent = RequireArray(document.CivilianTypes, "civilianTypes");
        var commodityContent = RequireArray(document.Commodities, "commodities");
        var resourceContent = RequireArray(document.Resources, "resources");
        var facilityContent = RequireArray(document.ProductionFacilities, "productionFacilities");
        var recipeContent = RequireArray(document.ProductionRecipes, "productionRecipes");
        if (document.ResourceKeys is not null)
        {
            throw Error("resourceKeys", "This version uses resource definitions instead of resourceKeys.");
        }

        if (document.TerrainKeys is not null)
        {
            throw Error("terrainKeys", "This version uses terrain definitions instead of terrainKeys.");
        }

        var mapContent = document.Map ?? throw Error("map", "Value is required.");
        var countriesContent = RequireArray(document.Countries, "countries");
        var scenariosContent = RequireArray(document.Scenarios, "scenarios");

        ValidateKey(mapContent.Key, "map.key");
        if (string.IsNullOrWhiteSpace(mapContent.Name))
        {
            throw Error("map.name", "Value cannot be blank.");
        }

        if (scenariosContent.Length == 0)
        {
            throw Error("scenarios", "At least one scenario is required.");
        }

        var terrainIds = BuildTerrainKeyMap(terrainContent);
        var civilianTypeIds = BuildCivilianTypeKeyMap(civilianTypeContent);
        var civilianTypes = civilianTypeContent.Select((definition, index) =>
        {
            if (!Enum.IsDefined(definition!.Work))
            {
                throw Error($"civilianTypes[{index}].work", "Unknown work kind.");
            }

            try
            {
                return new CivilianTypeDefinition(
                    new CivilianTypeId(index), definition.Name, definition.WorkTurns, definition.Work);
            }
            catch (ArgumentException exception)
            {
                throw Error($"civilianTypes[{index}].workTurns", exception.Message, exception);
            }
        }).ToArray();
        var commodityIds = BuildCommodityKeyMap(commodityContent);
        var resourceIds = BuildResourceKeyMap(resourceContent);
        var facilityIds = BuildProductionFacilityKeyMap(facilityContent);
        var recipeIds = BuildProductionRecipeKeyMap(recipeContent);
        var provinceContent = RequireArray(mapContent.Provinces, "map.provinces");
        var seaZoneContent = RequireArray(mapContent.SeaZones, "map.seaZones");
        var provinceIds = BuildNamedKeyMap(provinceContent, "map.provinces");
        var seaZoneIds = BuildNamedKeyMap(seaZoneContent, "map.seaZones");
        var countryIds = BuildNamedKeyMap(countriesContent, "countries");
        var commodities = commodityContent.Select((definition, index) =>
            new CommodityDefinition(new CommodityId(index), definition.Name, definition.Category)).ToArray();
        var technologyContent = RequireArray(document.Technologies, "technologies");
        var technologyIds = BuildNamedKeyMap(technologyContent, "technologies");
        var technologies = technologyContent.Select((definition, index) =>
            new TechnologyDefinition(new TechnologyId(index), definition.Name)).ToArray();

        // After the technology map, because prospectable ground may name the
        // knowledge it takes to search: swamp, desert and tundra are open only
        // once a country has invested in Oil Drilling.
        var terrains = terrainContent.Select((definition, index) =>
        {
            ProspectingRule? prospecting = definition!.Prospecting is not { } rule
                ? null
                : new ProspectingRule(rule.RequiredTechnology is null
                    ? null
                    : new TechnologyId(FindKey(
                        technologyIds,
                        rule.RequiredTechnology,
                        $"terrains[{index}].prospecting.requiredTechnology")));
            return new TerrainDefinition(
                new TerrainId(index), definition.Name, definition.IsImprovable, prospecting);
        }).ToArray();
        var resources = resourceContent.Select((definition, index) =>
        {
            var commodity = new CommodityId(FindKey(
                commodityIds,
                definition.Commodity,
                $"resources[{index}].commodity"));
            if (definition.YieldPerTurn != 0)
            {
                throw Error(
                    $"resources[{index}].yieldPerTurn",
                    "This version uses yieldByDevelopmentLevel instead.");
            }

            var curve = RequireArray(
                definition.YieldByDevelopmentLevel,
                $"resources[{index}].yieldByDevelopmentLevel");
            TechnologyId? required = definition.RequiredTechnology is null
                ? null
                : new TechnologyId(FindKey(
                    technologyIds,
                    definition.RequiredTechnology,
                    $"resources[{index}].requiredTechnology"));
            CivilianTypeId? improvedBy = definition.ImprovedBy is null
                ? null
                : new CivilianTypeId(FindKey(
                    civilianTypeIds,
                    definition.ImprovedBy,
                    $"resources[{index}].improvedBy"));
            TechnologyId?[]? gates = null;
            if (definition.TechnologyByDevelopmentLevel is { } levels)
            {
                gates = new TechnologyId?[levels.Length];
                for (var level = 0; level < levels.Length; level++)
                {
                    gates[level] = levels[level] is not { } key
                        ? null
                        : new TechnologyId(FindKey(
                            technologyIds,
                            key,
                            $"resources[{index}].technologyByDevelopmentLevel[{level}]"));
                }
            }

            try
            {
                return new ResourceDefinition(
                    new ResourceId(index),
                    commodity,
                    curve,
                    required,
                    improvedBy,
                    definition.RequiresDiscovery,
                    gates);
            }
            catch (ArgumentException exception)
            {
                throw Error(
                    $"resources[{index}].yieldByDevelopmentLevel",
                    exception.Message,
                    exception);
            }
        }).ToArray();
        var extraction = CompileExtractionSettings(document.Extraction, commodityIds);
        var feeding = CompileFeedingSettings(document.Feeding, commodityIds);
        var startingDefaults = CompileStartingDefaults(
            document.StartingDefaults, facilityIds, technologyIds, commodityIds);
        var facilities = facilityContent.Select((definition, index) =>
        {
            var path = $"productionFacilities[{index}]";
            CapacityLadder? ladder = null;
            if (definition.CapacityLadder is { } rungs)
            {
                if (RequireArray(rungs.Rungs, $"{path}.capacityLadder.rungs").Length == 0)
                {
                    throw Error($"{path}.capacityLadder.rungs", "A capacity ladder needs at least one rung.");
                }

                try
                {
                    ladder = new CapacityLadder(rungs.Rungs, rungs.Increment);
                }
                catch (ArgumentException exception)
                {
                    throw Error($"{path}.capacityLadder", exception.Message, exception);
                }
            }

            try
            {
                return new ProductionFacilityDefinition(
                    new ProductionFacilityId(index),
                    definition.Name,
                    definition.CapacityMode,
                    ladder);
            }
            catch (ArgumentException exception)
            {
                throw Error($"{path}.capacityLadder", exception.Message, exception);
            }
        }).ToArray();

        var expansionCost = CompileCommodityQuantities(
            RequireArray(document.ExpansionCostPerCapacityPoint, "expansionCostPerCapacityPoint"),
            commodityIds,
            "expansionCostPerCapacityPoint");
        var migration = CompileMigration(document.Migration, commodityIds);
        TransportSettings? transport = null;
        if (document.Transport is { } transportContent)
        {
            var cost = CompileCommodityQuantities(
                RequireArray(transportContent.CostPerCapacityPoint, "transport.costPerCapacityPoint"),
                commodityIds,
                "transport.costPerCapacityPoint");
            if (cost.Length == 0)
            {
                throw Error(
                    "transport.costPerCapacityPoint",
                    "Capacity that costs nothing would make the railyard free.");
            }

            if (transportContent.LabourPerCapacityPoint < 0)
            {
                throw Error("transport.labourPerCapacityPoint", "Labour cannot be negative.");
            }

            try
            {
                transport = new TransportSettings(cost, transportContent.LabourPerCapacityPoint);
            }
            catch (ArgumentException exception)
            {
                throw Error("transport.costPerCapacityPoint", exception.Message, exception);
            }
        }
        var recipes = CompileProductionRecipes(recipeContent, facilityIds, commodityIds);

        MapDimensions dimensions;
        try
        {
            dimensions = new MapDimensions(mapContent.Width, mapContent.Height);
        }
        catch (Exception exception) when (exception is ArgumentOutOfRangeException or OverflowException)
        {
            throw Error("map", exception.Message, exception);
        }

        var cellContent = RequireArray(mapContent.Cells, "map.cells");
        if (cellContent.Length != dimensions.CellCount)
        {
            throw Error(
                "map.cells",
                $"Expected {dimensions.CellCount} cells for {dimensions.Width}x{dimensions.Height}, " +
                $"got {cellContent.Length}.");
        }

        var cells = new CellDefinition[cellContent.Length];
        for (var value = 0; value < cellContent.Length; value++)
        {
            cells[value] = CompileCell(
                cellContent[value],
                value,
                dimensions,
                terrainIds,
                resourceIds,
                provinceIds,
                seaZoneIds);
        }

        var provinces = provinceContent.Select((definition, index) =>
            new ProvinceDefinition(new ProvinceId(index), definition.Name)).ToArray();
        var seaZones = seaZoneContent.Select((definition, index) =>
            new SeaZoneDefinition(new SeaZoneId(index), definition.Name)).ToArray();
        MapDefinition map;
        try
        {
            map = new MapDefinition(dimensions, cells, provinces, seaZones, resources, terrains);
        }
        catch (ArgumentException exception)
        {
            throw Error("map", exception.Message, exception);
        }

        var countries = countriesContent.Select((definition, index) =>
            new CountryDefinition(new CountryId(index), definition.Name)).ToArray();
        var catalog = new WorldContentCatalog(
            terrainContent.Select(static item => item!.Key),
            resourceContent.Select(static item => item.Key),
            commodityContent.Select(static item => item.Key),
            provinceContent.Select(static item => item.Key),
            seaZoneContent.Select(static item => item.Key),
            countriesContent.Select(static item => item.Key),
            facilityContent.Select(static item => item.Key),
            recipeContent.Select(static item => item.Key),
            technologyContent.Select(static item => item.Key));
        var scenarioKeys = new string?[scenariosContent.Length];
        for (var index = 0; index < scenariosContent.Length; index++)
        {
            scenarioKeys[index] = scenariosContent[index]?.Key;
        }

        _ = BuildKeyMap(scenarioKeys, "scenarios");
        var worlds = new (string Key, WorldDefinition World)[scenariosContent.Length];
        for (var index = 0; index < scenariosContent.Length; index++)
        {
            var path = $"scenarios[{index}]";
            var scenarioContent = scenariosContent[index] ?? throw Error(path, "Value is required.");
            worlds[index] = (
                scenarioContent.Key,
                CompileScenario(
                    scenarioContent,
                    path,
                    map,
                    countries,
                    commodities,
                    facilities,
                    recipes,
                    provinceContent,
                    provinceIds,
                    countryIds,
                    commodityIds,
                    facilityIds,
                    extraction,
                    technologies,
                    technologyIds,
                    feeding,
                    startingDefaults,
                    expansionCost,
                    migration,
                    civilianTypes,
                    civilianTypeIds,
                    transport));
        }

        return new CompiledWorldPackage(mapContent.Key, mapContent.Name, catalog, worlds);
    }

    private static WorldDefinition CompileScenario(
        ScenarioContentDocument scenarioContent,
        string path,
        MapDefinition map,
        CountryDefinition[] countries,
        CommodityDefinition[] commodities,
        ProductionFacilityDefinition[] facilities,
        ProductionRecipeDefinition[] recipes,
        NamedContentDefinition[] provinceContent,
        IReadOnlyDictionary<string, int> provinceIds,
        IReadOnlyDictionary<string, int> countryIds,
        IReadOnlyDictionary<string, int> commodityIds,
        IReadOnlyDictionary<string, int> facilityIds,
        ExtractionSettings extraction,
        TechnologyDefinition[] technologies,
        IReadOnlyDictionary<string, int> technologyIds,
        FeedingSettings? feeding,
        StartingDefaults? startingDefaults,
        CommodityQuantity[] expansionCost,
        MigrationSettings? migration,
        CivilianTypeDefinition[] civilianTypes,
        IReadOnlyDictionary<string, int> civilianTypeIds,
        TransportSettings? transport)
    {
        var owners = CompileOwners(
            RequireArray(scenarioContent.ProvinceOwners, $"{path}.provinceOwners"),
            provinceContent,
            provinceIds,
            countryIds,
            path);
        var rails = CompileLinks(
            RequireArray(scenarioContent.Rails, $"{path}.rails"),
            $"{path}.rails");
        var capitals = CompileCapitals(
            RequireArray(scenarioContent.Capitals, $"{path}.capitals"),
            countryIds,
            path);
        var initialInventory = CompileInitialInventory(
            RequireArray(scenarioContent.InitialInventory, $"{path}.initialInventory"),
            countryIds,
            commodityIds,
            path);
        var productionCapacities = CompileProductionCapacities(
            RequireArray(scenarioContent.ProductionCapacities, $"{path}.productionCapacities"),
            countryIds,
            facilityIds,
            facilities,
            path);

        var cellDevelopment = CompileCellDevelopment(
            RequireArray(scenarioContent.CellDevelopment, $"{path}.cellDevelopment"),
            path);
        var ports = CompileCells(
            RequireArray(scenarioContent.Ports, $"{path}.ports"),
            $"{path}.ports");
        var depots = CompileCells(
            RequireArray(scenarioContent.Depots, $"{path}.depots"),
            $"{path}.depots");
        var workers = CompileWorkforce(
            RequireArray(scenarioContent.Workers, $"{path}.workers"),
            countryIds,
            path);
        var countryTechnologies = CompileCountryTechnologies(
            RequireArray(scenarioContent.CountryTechnologies, $"{path}.countryTechnologies"),
            countryIds,
            technologyIds,
            path);
        var civilians = CompileCivilians(
            RequireArray(scenarioContent.Civilians, $"{path}.civilians"),
            countryIds,
            civilianTypeIds,
            path);

        var transportCapacity = new List<InitialTransportCapacity>();
        var transportEntries = RequireArray(
            scenarioContent.TransportCapacity, $"{path}.transportCapacity");
        for (var index = 0; index < transportEntries.Length; index++)
        {
            var entryPath = $"{path}.transportCapacity[{index}]";
            var entry = transportEntries[index] ?? throw Error(entryPath, "Value is required.");
            if (entry.Capacity < 0)
            {
                throw Error($"{entryPath}.capacity", "Capacity cannot be negative.");
            }

            transportCapacity.Add(new InitialTransportCapacity(
                new CountryId(FindKey(countryIds, entry.Country, $"{entryPath}.country")),
                entry.Capacity));
        }

        var defaultStartCountries = new List<CountryId>();
        var defaultStartSeen = new HashSet<string>(StringComparer.Ordinal);
        var defaultStartContent = RequireArray(
            scenarioContent.DefaultStartCountries, $"{path}.defaultStartCountries");
        for (var index = 0; index < defaultStartContent.Length; index++) 
        {
            var entryPath = $"{path}.defaultStartCountries[{index}]";
            var key = defaultStartContent[index];
            if (!defaultStartSeen.Add(key ?? string.Empty))
            {
                throw Error(entryPath, "Country appears more than once.");
            }

            defaultStartCountries.Add(new CountryId(FindKey(countryIds, key, entryPath)));
        }

        if (defaultStartCountries.Count > 0 && startingDefaults is null)
        {
            throw Error(
                $"{path}.defaultStartCountries",
                "The world defines no startingDefaults for these countries to start from.");
        }

        if (string.IsNullOrWhiteSpace(scenarioContent.Name))
        {
            throw Error($"{path}.name", "Value cannot be blank.");
        }

        try
        {
            var scenario = new ScenarioDefinition(
                scenarioContent.Name,
                scenarioContent.StartingYear,
                owners,
                rails,
                capitals,
                initialInventory,
                productionCapacities,
                cellDevelopment,
                countryTechnologies,
                ports,
                depots,
                workers,
                defaultStartCountries,
                civilians,
                transportCapacity);
            return new WorldDefinition(
                map,
                countries,
                scenario,
                commodities,
                facilities,
                recipes,
                extraction,
                technologies,
                feeding,
                startingDefaults,
                expansionCost,
                migration,
                civilianTypes,
                transport);
        }
        catch (ArgumentException exception)
        {
            throw Error(path, exception.Message, exception);
        }
    }

    private static InitialCivilian[] CompileCivilians(
        CivilianContent?[] content,
        IReadOnlyDictionary<string, int> countryIds,
        IReadOnlyDictionary<string, int> civilianTypeIds,
        string path)
    {
        var result = new InitialCivilian[content.Length];
        for (var index = 0; index < content.Length; index++)
        {
            var entryPath = $"{path}.civilians[{index}]";
            var entry = content[index] ?? throw Error(entryPath, "Value is required.");
            if (entry.Cell < 0)
            {
                throw Error($"{entryPath}.cell", "Value cannot be negative.");
            }

            result[index] = new InitialCivilian(
                new CountryId(FindKey(countryIds, entry.Country, $"{entryPath}.country")),
                new CivilianTypeId(FindKey(civilianTypeIds, entry.Type, $"{entryPath}.type")),
                new CellIndex(entry.Cell));
        }

        return result;
    }

    private static InitialCellDevelopment[] CompileCellDevelopment(
        CellDevelopmentContent?[] content,
        string path)
    {
        var result = new InitialCellDevelopment[content.Length];
        for (var index = 0; index < content.Length; index++)
        {
            var entry = content[index] ??
                throw Error($"{path}.cellDevelopment[{index}]", "Value is required.");
            if (entry.Cell < 0)
            {
                throw Error($"{path}.cellDevelopment[{index}].cell", "Value cannot be negative.");
            }

            if (entry.Level <= 0)
            {
                throw Error(
                    $"{path}.cellDevelopment[{index}].level",
                    "Undeveloped is the absence of an entry, not level zero.");
            }

            result[index] = new InitialCellDevelopment(new CellIndex(entry.Cell), entry.Level);
        }

        return result;
    }

    private static CellIndex[] CompileCells(int[] content, string path)
    {
        var result = new CellIndex[content.Length];
        for (var index = 0; index < content.Length; index++)
        {
            if (content[index] < 0)
            {
                throw Error($"{path}[{index}]", "Value cannot be negative.");
            }

            result[index] = new CellIndex(content[index]);
        }

        return result;
    }

    private static InitialCountryTechnology[] CompileCountryTechnologies(
        CountryTechnologyContent?[] content,
        IReadOnlyDictionary<string, int> countryIds,
        IReadOnlyDictionary<string, int> technologyIds,
        string path)
    {
        var result = new InitialCountryTechnology[content.Length];
        for (var index = 0; index < content.Length; index++)
        {
            var entry = content[index] ??
                throw Error($"{path}.countryTechnologies[{index}]", "Value is required.");
            result[index] = new InitialCountryTechnology(
                new CountryId(FindKey(
                    countryIds,
                    entry.Country,
                    $"{path}.countryTechnologies[{index}].country")),
                new TechnologyId(FindKey(
                    technologyIds,
                    entry.Technology,
                    $"{path}.countryTechnologies[{index}].technology")));
        }

        return result;
    }

    private static InitialWorkforce[] CompileWorkforce(
        WorkforceContent?[] content,
        IReadOnlyDictionary<string, int> countryIds,
        string path)
    {
        var result = new InitialWorkforce[content.Length];
        for (var index = 0; index < content.Length; index++)
        {
            var entry = content[index] ??
                throw Error($"{path}.workers[{index}]", "Value is required.");
            var country = new CountryId(FindKey(
                countryIds,
                entry.Country,
                $"{path}.workers[{index}].country"));
            try
            {
                result[index] = new InitialWorkforce(
                    country,
                    entry.Untrained,
                    entry.Trained,
                    entry.Expert);
            }
            catch (ArgumentOutOfRangeException exception)
            {
                throw Error($"{path}.workers[{index}]", exception.Message, exception);
            }
        }

        return result;
    }

    /// <summary>
    /// The fair start a skirmish runs on. Absent means the world has no
    /// defaults, which is how every package before version 10 behaves.
    /// </summary>
    private static StartingDefaults? CompileStartingDefaults(
        StartingDefaultsContent? content,
        IReadOnlyDictionary<string, int> facilityIds,
        IReadOnlyDictionary<string, int> technologyIds,
        IReadOnlyDictionary<string, int> commodityIds)
    {
        if (content is null)
        {
            return null;
        }

        var capacities = new List<FacilityCapacityDefault>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var entries = RequireArray(content.ProductionCapacities, "startingDefaults.productionCapacities");
        for (var index = 0; index < entries.Length; index++)
        {
            var path = $"startingDefaults.productionCapacities[{index}]";
            var entry = entries[index] ?? throw Error(path, "Value is required.");
            if (!seen.Add(entry.Facility ?? string.Empty))
            {
                throw Error($"{path}.facility", "Facility appears more than once.");
            }

            if (entry.Quantity <= 0)
            {
                throw Error($"{path}.quantity", "A default production capacity must be positive.");
            }

            capacities.Add(new FacilityCapacityDefault(
                new ProductionFacilityId(FindKey(facilityIds, entry.Facility, $"{path}.facility")),
                entry.Quantity));
        }

        WorkforceDefault? workforce = null;
        if (content.Workforce is { } crew)
        {
            if (crew.Untrained < 0 || crew.Trained < 0 || crew.Expert < 0)
            {
                throw Error("startingDefaults.workforce", "Worker counts cannot be negative.");
            }

            workforce = new WorkforceDefault(crew.Untrained, crew.Trained, crew.Expert);
        }

        var technologies = new List<TechnologyId>();
        var known = new HashSet<string>(StringComparer.Ordinal);
        var declared = RequireArray(content.Technologies, "startingDefaults.technologies");
        for (var index = 0; index < declared.Length; index++)
        {
            var path = $"startingDefaults.technologies[{index}]";
            var key = declared[index] ?? throw Error(path, "Value is required.");
            if (!known.Add(key))
            {
                throw Error(path, "Technology appears more than once.");
            }

            technologies.Add(new TechnologyId(FindKey(technologyIds, key, path)));
        }

        if (content.TransportCapacity < 0)
        {
            throw Error("startingDefaults.transportCapacity", "Capacity cannot be negative.");
        }

        var inventory = CompileCommodityQuantities(
            RequireArray(content.Inventory, "startingDefaults.inventory"),
            commodityIds,
            "startingDefaults.inventory");

        try
        {
            return new StartingDefaults(
                capacities, workforce, technologies, content.TransportCapacity, inventory);
        }
        catch (ArgumentException exception)
        {
            throw Error("startingDefaults.inventory", exception.Message, exception);
        }
    }

    private static MigrationSettings? CompileMigration(
        MigrationContent? content,
        IReadOnlyDictionary<string, int> commodityIds)
    {
        if (content is null)
        {
            return null;
        }

        var cost = CompileCommodityQuantities(
            RequireArray(content.CostPerWorker, "migration.costPerWorker"),
            commodityIds,
            "migration.costPerWorker");

        try
        {
            return new MigrationSettings(cost, content.ProvincesPerRecruit);
        }
        catch (ArgumentException exception)
        {
            throw Error("migration", exception.Message, exception);
        }
    }

    private static FeedingSettings? CompileFeedingSettings(
        FeedingContentSettings? content,
        IReadOnlyDictionary<string, int> commodityIds)
    {
        if (content is null)
        {
            return null;
        }

        var cycleContent = RequireArray(content.PreferenceCycle, "feeding.preferenceCycle");
        var cycle = new FoodPreference[cycleContent.Length];
        for (var index = 0; index < cycleContent.Length; index++)
        {
            var entry = cycleContent[index] ??
                throw Error($"feeding.preferenceCycle[{index}]", "Value is required.");
            var accepted = RequireArray(entry.Accepted, $"feeding.preferenceCycle[{index}].accepted");
            try
            {
                cycle[index] = new FoodPreference(accepted.Select((key, position) =>
                    new CommodityId(FindKey(
                        commodityIds,
                        key,
                        $"feeding.preferenceCycle[{index}].accepted[{position}]"))));
            }
            catch (ArgumentException exception)
            {
                throw Error($"feeding.preferenceCycle[{index}].accepted", exception.Message, exception);
            }
        }

        CommodityId? cannedFood = content.CannedFood is null
            ? null
            : new CommodityId(FindKey(commodityIds, content.CannedFood, "feeding.cannedFood"));

        try
        {
            return new FeedingSettings(
                cycle,
                RequireArray(content.LabourByGrade, "feeding.labourByGrade"),
                cannedFood);
        }
        catch (ArgumentException exception)
        {
            throw Error("feeding", exception.Message, exception);
        }
    }

    private static ExtractionSettings CompileExtractionSettings(
        ExtractionContentSettings? content,
        IReadOnlyDictionary<string, int> commodityIds)
    {
        if (content is null)
        {
            throw Error("extraction", "Value is required.");
        }

        PortFishing? fishing = null;
        if (content.PortFishing is { } portFishing)
        {
            var commodity = new CommodityId(FindKey(
                commodityIds,
                portFishing.Commodity,
                "extraction.portFishing.commodity"));
            try
            {
                fishing = new PortFishing(commodity, portFishing.YieldPerAdjacentWaterTile);
            }
            catch (ArgumentOutOfRangeException exception)
            {
                throw Error(
                    "extraction.portFishing.yieldPerAdjacentWaterTile",
                    exception.Message,
                    exception);
            }
        }

        try
        {
            return new ExtractionSettings(content.CatchmentRadius, fishing);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw Error("extraction.catchmentRadius", exception.Message, exception);
        }
    }

    private static CellDefinition CompileCell(
        CellContentDocument? content,
        int value,
        MapDimensions dimensions,
        IReadOnlyDictionary<string, int> terrainIds,
        IReadOnlyDictionary<string, int> resourceIds,
        IReadOnlyDictionary<string, int> provinceIds,
        IReadOnlyDictionary<string, int> seaZoneIds)
    {
        var path = $"map.cells[{value}]";
        if (content is null)
        {
            throw Error(path, "Value is required.");
        }

        var terrain = FindKey(terrainIds, content.Terrain, $"{path}.terrain");
        var regionContent = content.Region ?? throw Error($"{path}.region", "Value is required.");
        var hasProvince = regionContent.Province is not null;
        var hasSeaZone = regionContent.SeaZone is not null;
        if (hasProvince && hasSeaZone)
        {
            throw Error($"{path}.region", "A cell cannot belong to both a province and a sea zone.");
        }

        var region = hasProvince
            ? CellRegion.ForProvince(new ProvinceId(FindKey(
                provinceIds,
                regionContent.Province,
                $"{path}.region.province")))
            : hasSeaZone
                ? CellRegion.ForSeaZone(new SeaZoneId(FindKey(
                    seaZoneIds,
                    regionContent.SeaZone,
                    $"{path}.region.seaZone")))
                : CellRegion.Unassigned;

        var resourceKeys = RequireArray(content.Resources, $"{path}.resources");
        if (resourceKeys.Length != resourceKeys.Distinct(StringComparer.Ordinal).Count())
        {
            throw Error($"{path}.resources", "Resource keys cannot contain duplicates.");
        }

        var resources = resourceKeys.Select((key, index) => new ResourceId(FindKey(
            resourceIds,
            key,
            $"{path}.resources[{index}]")));
        RiverPath? river = null;
        if (content.River is not null)
        {
            try
            {
                river = new RiverPath(content.River.First, content.River.Second);
            }
            catch (ArgumentException exception)
            {
                throw Error($"{path}.river", exception.Message, exception);
            }
        }

        var index = new CellIndex(value);
        return new CellDefinition(
            index,
            dimensions.GetCoordinate(index),
            new TerrainId(terrain),
            region,
            resources,
            content.HasSettlementSite ? SettlementSiteKind.Urban : SettlementSiteKind.None,
            river);
    }

    private static CountryId?[] CompileOwners(
        ProvinceOwnerContent?[] ownerContent,
        NamedContentDefinition[] provinces,
        IReadOnlyDictionary<string, int> provinceIds,
        IReadOnlyDictionary<string, int> countryIds,
        string scenarioPath)
    {
        var path = $"{scenarioPath}.provinceOwners";
        if (ownerContent.Length != provinces.Length)
        {
            throw Error(
                path,
                $"Every province requires one ownership entry; expected {provinces.Length}, " +
                $"got {ownerContent.Length}.");
        }

        var owners = new CountryId?[provinces.Length];
        var seen = new HashSet<int>();
        for (var index = 0; index < ownerContent.Length; index++)
        {
            var content = ownerContent[index] ??
                throw Error($"{path}[{index}]", "Value is required.");
            var province = FindKey(
                provinceIds,
                content.Province,
                $"{path}[{index}].province");
            if (!seen.Add(province))
            {
                throw Error(
                    $"{path}[{index}].province",
                    $"Province '{content.Province}' has more than one ownership entry.");
            }

            owners[province] = content.Country is null
                ? null
                : new CountryId(FindKey(
                    countryIds,
                    content.Country,
                    $"{path}[{index}].country"));
        }

        return owners;
    }

    private static CountryCapital[] CompileCapitals(
        CountryCapitalContent?[] capitalContent,
        IReadOnlyDictionary<string, int> countryIds,
        string scenarioPath)
    {
        var path = $"{scenarioPath}.capitals";
        var capitals = new CountryCapital[capitalContent.Length];
        for (var index = 0; index < capitalContent.Length; index++)
        {
            var content = capitalContent[index] ??
                throw Error($"{path}[{index}]", "Value is required.");
            if (content.Cell < 0)
            {
                throw Error($"{path}[{index}].cell", "Cell index cannot be negative.");
            }

            capitals[index] = new CountryCapital(
                new CountryId(FindKey(
                    countryIds,
                    content.Country,
                    $"{path}[{index}].country")),
                new CellIndex(content.Cell));
        }

        return capitals;
    }

    private static InitialCommodityStock[] CompileInitialInventory(
        InitialInventoryContent?[] inventoryContent,
        IReadOnlyDictionary<string, int> countryIds,
        IReadOnlyDictionary<string, int> commodityIds,
        string scenarioPath)
    {
        var path = $"{scenarioPath}.initialInventory";
        var inventory = new InitialCommodityStock[inventoryContent.Length];
        var seen = new HashSet<(int Country, int Commodity)>();
        for (var index = 0; index < inventoryContent.Length; index++)
        {
            var content = inventoryContent[index] ??
                throw Error($"{path}[{index}]", "Value is required.");
            var country = FindKey(countryIds, content.Country, $"{path}[{index}].country");
            var commodity = FindKey(commodityIds, content.Commodity, $"{path}[{index}].commodity");
            if (content.Quantity <= 0)
            {
                throw Error($"{path}[{index}].quantity", "Quantity must be positive.");
            }

            if (!seen.Add((country, commodity)))
            {
                throw Error(
                    $"{path}[{index}]",
                    "A country and commodity can have only one initial inventory entry.");
            }

            inventory[index] = new InitialCommodityStock(
                new CountryId(country),
                new CommodityId(commodity),
                content.Quantity);
        }

        return inventory;
    }

    private static InitialProductionCapacity[] CompileProductionCapacities(
        InitialProductionCapacityContent?[] capacityContent,
        IReadOnlyDictionary<string, int> countryIds,
        IReadOnlyDictionary<string, int> facilityIds,
        IReadOnlyList<ProductionFacilityDefinition> facilities,
        string scenarioPath)
    {
        var path = $"{scenarioPath}.productionCapacities";
        var capacities = new InitialProductionCapacity[capacityContent.Length];
        var seen = new HashSet<(int Country, int Facility)>();
        for (var index = 0; index < capacityContent.Length; index++)
        {
            var content = capacityContent[index] ?? throw Error($"{path}[{index}]", "Value is required.");
            var country = FindKey(countryIds, content.Country, $"{path}[{index}].country");
            var facility = FindKey(facilityIds, content.Facility, $"{path}[{index}].facility");
            if (content.Quantity <= 0)
            {
                throw Error($"{path}[{index}].quantity", "Quantity must be positive.");
            }

            if (facilities[facility].CapacityMode == ProductionCapacityMode.Unlimited)
            {
                throw Error($"{path}[{index}].facility", "Unlimited facilities cannot have stored capacity.");
            }

            if (!seen.Add((country, facility)))
            {
                throw Error($"{path}[{index}]", "A country and facility can have only one capacity entry.");
            }

            capacities[index] = new InitialProductionCapacity(
                new CountryId(country),
                new ProductionFacilityId(facility),
                content.Quantity);
        }

        return capacities;
    }

    private static ProductionRecipeDefinition[] CompileProductionRecipes(
        ProductionRecipeContentDefinition?[] recipeContent,
        IReadOnlyDictionary<string, int> facilityIds,
        IReadOnlyDictionary<string, int> commodityIds)
    {
        var recipes = new ProductionRecipeDefinition[recipeContent.Length];
        for (var index = 0; index < recipeContent.Length; index++)
        {
            var path = $"productionRecipes[{index}]";
            var content = recipeContent[index] ?? throw Error(path, "Value is required.");
            if (string.IsNullOrWhiteSpace(content.Name))
            {
                throw Error($"{path}.name", "Value cannot be blank.");
            }

            if (content.CapacityCost <= 0)
            {
                throw Error($"{path}.capacityCost", "Capacity cost must be positive.");
            }

            if (content.LabourCost <= 0)
            {
                throw Error($"{path}.labourCost", "Labour cost must be positive.");
            }

            var facility = FindKey(facilityIds, content.Facility, $"{path}.facility");
            var inputs = CompileCommodityQuantities(
                RequireArray(content.Inputs, $"{path}.inputs"),
                commodityIds,
                $"{path}.inputs");
            var outputs = CompileCommodityQuantities(
                RequireArray(content.Outputs, $"{path}.outputs"),
                commodityIds,
                $"{path}.outputs");
            if (inputs.Length == 0 || outputs.Length == 0)
            {
                throw Error(path, "A production recipe requires at least one input and one output.");
            }

            recipes[index] = new ProductionRecipeDefinition(
                new ProductionRecipeId(index),
                content.Name,
                new ProductionFacilityId(facility),
                content.CapacityCost,
                content.LabourCost,
                inputs,
                outputs);
        }

        return recipes;
    }

    private static CommodityQuantity[] CompileCommodityQuantities(
        CommodityQuantityContent?[] content,
        IReadOnlyDictionary<string, int> commodityIds,
        string path)
    {
        var result = new CommodityQuantity[content.Length];
        var seen = new HashSet<int>();
        for (var index = 0; index < content.Length; index++)
        {
            var item = content[index] ?? throw Error($"{path}[{index}]", "Value is required.");
            var commodity = FindKey(commodityIds, item.Commodity, $"{path}[{index}].commodity");
            if (item.Quantity <= 0)
            {
                throw Error($"{path}[{index}].quantity", "Quantity must be positive.");
            }

            if (!seen.Add(commodity))
            {
                throw Error($"{path}[{index}]", "A commodity can appear only once in this collection.");
            }

            result[index] = new CommodityQuantity(new CommodityId(commodity), item.Quantity);
        }

        return result;
    }

    private static CellLink[] CompileLinks(CellLinkContent?[] linkContent, string path)
    {
        var links = new CellLink[linkContent.Length];
        for (var index = 0; index < linkContent.Length; index++)
        {
            var content = linkContent[index] ?? throw Error($"{path}[{index}]", "Value is required.");
            if (content.First < 0 || content.Second < 0)
            {
                throw Error($"{path}[{index}]", "Cell indices cannot be negative.");
            }

            try
            {
                links[index] = new CellLink(new CellIndex(content.First), new CellIndex(content.Second));
            }
            catch (ArgumentException exception)
            {
                throw Error($"{path}[{index}]", exception.Message, exception);
            }
        }

        return links;
    }

    private static Dictionary<string, int> BuildCommodityKeyMap(
        CommodityContentDefinition?[] definitions)
    {
        const string path = "commodities";
        var keys = new string?[definitions.Length];
        for (var index = 0; index < definitions.Length; index++)
        {
            var definition = definitions[index] ?? throw Error($"{path}[{index}]", "Value is required.");
            if (string.IsNullOrWhiteSpace(definition.Name))
            {
                throw Error($"{path}[{index}].name", "Value cannot be blank.");
            }

            if (!Enum.IsDefined(definition.Category))
            {
                throw Error($"{path}[{index}].category", "Unknown commodity category.");
            }

            keys[index] = definition.Key;
        }

        return BuildKeyMap(keys, path);
    }

    private static Dictionary<string, int> BuildResourceKeyMap(
        ResourceContentDefinition?[] definitions)
    {
        const string path = "resources";
        var keys = new string?[definitions.Length];
        for (var index = 0; index < definitions.Length; index++)
        {
            var definition = definitions[index] ?? throw Error($"{path}[{index}]", "Value is required.");
            keys[index] = definition.Key;
        }

        return BuildKeyMap(keys, path);
    }

    private static Dictionary<string, int> BuildProductionFacilityKeyMap(
        ProductionFacilityContentDefinition?[] definitions)
    {
        const string path = "productionFacilities";
        var keys = new string?[definitions.Length];
        for (var index = 0; index < definitions.Length; index++)
        {
            var definition = definitions[index] ?? throw Error($"{path}[{index}]", "Value is required.");
            if (string.IsNullOrWhiteSpace(definition.Name))
            {
                throw Error($"{path}[{index}].name", "Value cannot be blank.");
            }

            if (!Enum.IsDefined(definition.CapacityMode))
            {
                throw Error($"{path}[{index}].capacityMode", "Unknown production capacity mode.");
            }

            keys[index] = definition.Key;
        }

        return BuildKeyMap(keys, path);
    }

    private static Dictionary<string, int> BuildProductionRecipeKeyMap(
        ProductionRecipeContentDefinition?[] definitions)
    {
        const string path = "productionRecipes";
        var keys = new string?[definitions.Length];
        for (var index = 0; index < definitions.Length; index++)
        {
            var definition = definitions[index] ?? throw Error($"{path}[{index}]", "Value is required.");
            keys[index] = definition.Key;
        }

        return BuildKeyMap(keys, path);
    }

    private static Dictionary<string, int> BuildTerrainKeyMap(
        TerrainContentDefinition?[] definitions)
    {
        const string path = "terrains";
        var keys = new string?[definitions.Length];
        for (var index = 0; index < definitions.Length; index++)
        {
            var definition = definitions[index] ?? throw Error($"{path}[{index}]", "Value is required.");
            if (string.IsNullOrWhiteSpace(definition.Name))
            {
                throw Error($"{path}[{index}].name", "Value cannot be blank.");
            }

            keys[index] = definition.Key;
        }

        return BuildKeyMap(keys, path, requireAtLeastOne: true);
    }

    private static Dictionary<string, int> BuildCivilianTypeKeyMap(
        CivilianTypeContentDefinition?[] definitions)
    {
        const string path = "civilianTypes";
        var keys = new string?[definitions.Length];
        for (var index = 0; index < definitions.Length; index++)
        {
            var definition = definitions[index] ?? throw Error($"{path}[{index}]", "Value is required.");
            if (string.IsNullOrWhiteSpace(definition.Name))
            {
                throw Error($"{path}[{index}].name", "Value cannot be blank.");
            }

            keys[index] = definition.Key;
        }

        return BuildKeyMap(keys, path);
    }

    private static Dictionary<string, int> BuildNamedKeyMap(
        NamedContentDefinition?[] definitions,
        string path)
    {
        var keys = new string[definitions.Length];
        for (var index = 0; index < definitions.Length; index++)
        {
            var definition = definitions[index] ?? throw Error($"{path}[{index}]", "Value is required.");
            if (string.IsNullOrWhiteSpace(definition.Name))
            {
                throw Error($"{path}[{index}].name", "Value cannot be blank.");
            }

            keys[index] = definition.Key;
        }

        return BuildKeyMap(keys, path);
    }

    private static Dictionary<string, int> BuildKeyMap(
        string?[] keys,
        string path,
        bool requireAtLeastOne = false)
    {
        if (requireAtLeastOne && keys.Length == 0)
        {
            throw Error(path, "At least one key is required.");
        }

        var result = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var index = 0; index < keys.Length; index++)
        {
            var key = keys[index];
            ValidateKey(key, $"{path}[{index}]");
            if (!result.TryAdd(key!, index))
            {
                throw Error($"{path}[{index}]", $"Duplicate key '{key}'.");
            }
        }

        return result;
    }

    private static int FindKey(
        IReadOnlyDictionary<string, int> ids,
        string? key,
        string path)
    {
        ValidateKey(key, path);
        return ids.TryGetValue(key!, out var id)
            ? id
            : throw Error(path, $"Unknown key '{key}'.");
    }

    private static void ValidateKey(string? key, string path)
    {
        if (string.IsNullOrEmpty(key) || key.Length > 128)
        {
            throw Error(path, "Keys must contain 1 to 128 characters.");
        }

        if (!IsLowerAsciiLetterOrDigit(key[0]) || !IsLowerAsciiLetterOrDigit(key[^1]) ||
            key.Any(static character =>
                !IsLowerAsciiLetterOrDigit(character) && character is not '-' and not '_' and not '.' and not '/'))
        {
            throw Error(
                path,
                "Keys must use lowercase ASCII letters, digits, '-', '_', '.', or '/', " +
                "and must begin and end with a letter or digit.");
        }
    }

    private static bool IsLowerAsciiLetterOrDigit(char character) =>
        character is >= 'a' and <= 'z' or >= '0' and <= '9';

    private static void ValidateEnvelope(WorldContentDocument document)
    {
        if (!string.Equals(document.Format, WorldContentCodec.FormatName, StringComparison.Ordinal))
        {
            throw Error("format", $"Expected '{WorldContentCodec.FormatName}'.");
        }

        if (document.FormatVersion != WorldContentCodec.CurrentVersion)
        {
            throw Error(
                "formatVersion",
                $"Unsupported version {document.FormatVersion}; this build supports " +
                $"version {WorldContentCodec.CurrentVersion}.");
        }
    }

    private static T[] RequireArray<T>(T[]? values, string path) =>
        values ?? throw Error(path, "Array is required.");

    private static ContentValidationException Error(string path, string message) => new(path, message);

    private static ContentValidationException Error(
        string path,
        string message,
        Exception innerException) => new(path, message, innerException);
}
