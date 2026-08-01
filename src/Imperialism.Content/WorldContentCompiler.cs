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

        var terrainKeys = RequireArray(document.TerrainKeys, "terrainKeys");
        var resourceKeys = RequireArray(document.ResourceKeys, "resourceKeys");
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

        var terrainIds = BuildKeyMap(terrainKeys, "terrainKeys", requireAtLeastOne: true);
        var resourceIds = BuildKeyMap(resourceKeys, "resourceKeys");
        var provinceContent = RequireArray(mapContent.Provinces, "map.provinces");
        var seaZoneContent = RequireArray(mapContent.SeaZones, "map.seaZones");
        var provinceIds = BuildNamedKeyMap(provinceContent, "map.provinces");
        var seaZoneIds = BuildNamedKeyMap(seaZoneContent, "map.seaZones");
        var countryIds = BuildNamedKeyMap(countriesContent, "countries");

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
            map = new MapDefinition(dimensions, cells, provinces, seaZones);
        }
        catch (ArgumentException exception)
        {
            throw Error("map", exception.Message, exception);
        }

        var countries = countriesContent.Select((definition, index) =>
            new CountryDefinition(new CountryId(index), definition.Name)).ToArray();
        var catalog = new WorldContentCatalog(
            terrainKeys,
            resourceKeys,
            provinceContent.Select(static item => item.Key),
            seaZoneContent.Select(static item => item.Key),
            countriesContent.Select(static item => item.Key));
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
                    provinceContent,
                    provinceIds,
                    countryIds));
        }

        return new CompiledWorldPackage(mapContent.Key, mapContent.Name, catalog, worlds);
    }

    private static WorldDefinition CompileScenario(
        ScenarioContentDocument scenarioContent,
        string path,
        MapDefinition map,
        CountryDefinition[] countries,
        NamedContentDefinition[] provinceContent,
        IReadOnlyDictionary<string, int> provinceIds,
        IReadOnlyDictionary<string, int> countryIds)
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
                capitals);
            return new WorldDefinition(map, countries, scenario);
        }
        catch (ArgumentException exception)
        {
            throw Error(path, exception.Message, exception);
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
