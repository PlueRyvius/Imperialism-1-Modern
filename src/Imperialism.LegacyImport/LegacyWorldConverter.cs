using System.Globalization;
using System.Text.RegularExpressions;
using Imperialism.Content;
using Imperialism.Core;
using Imperialism.Formats;

namespace Imperialism.LegacyImport;

public sealed record LegacyImportOptions(string PackageKey);

public sealed record LegacyImportResult(
    WorldContentDocument? Document,
    LegacyImportReport Report)
{
    public bool Success => Document is not null && !Report.HasErrors;
}

public static class LegacyWorldConverter
{
    private static readonly IReadOnlyDictionary<byte, string> TerrainNames =
        new Dictionary<byte, string>
        {
            [0] = "ocean",
            [1] = "clear",
            [2] = "cotton",
            [3] = "cattle-ranch",
            [4] = "horse-ranch",
            [5] = "grain-farm",
            [6] = "orchard",
            [7] = "wool-hill",
            [8] = "hill",
            [9] = "mountain",
            [10] = "swamp",
            [11] = "desert",
            [12] = "tundra",
            [13] = "forest",
            [14] = "town",
            [15] = "scrub-forest",
            [16] = "capital",
        };

    private static readonly IReadOnlyDictionary<byte, string> ResourceNames =
        new Dictionary<byte, string>
        {
            [0] = "cotton",
            [1] = "wool",
            [2] = "forest",
            [3] = "coal",
            [4] = "iron",
            [5] = "horses",
            [6] = "oil",
            [17] = "grain",
            [18] = "fruit",
            [19] = "fish",
            [20] = "cattle",
            [21] = "gems",
            [22] = "gold",
        };

    private static readonly IReadOnlyDictionary<byte, string> ResourceCommodityNames =
        new Dictionary<byte, string>
        {
            [0] = "cotton",
            [1] = "wool",
            [2] = "timber",
            [3] = "coal",
            [4] = "iron",
            [5] = "horses",
            [6] = "oil",
            [17] = "grain",
            [18] = "fruit",
            [19] = "fish",
            [20] = "livestock",
            [21] = "gems",
            [22] = "gold",
        };

    private static readonly HashSet<string> ConvertedScenarioTags =
        new(["cnam", "pnam", "zone", "year"], StringComparer.Ordinal);

    public static LegacyImportResult Convert(
        MapDocument map,
        ScenarioDocument scenario,
        ScenarioInfoDocument? info,
        string packageKey) =>
        Convert(map, scenario, info, new LegacyImportOptions(packageKey));

    public static LegacyImportResult Convert(
        MapDocument map,
        ScenarioDocument scenario,
        ScenarioInfoDocument? info,
        LegacyImportOptions options)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(options);

        var report = new LegacyImportReport();
        if (!IsValidPackageKey(options.PackageKey))
        {
            report.Add(
                LegacyImportSeverity.Error,
                "package.invalid-key",
                "packageKey",
                "Package keys must use 1-96 lowercase ASCII letters, digits, hyphens, underscores, or dots, and begin and end with a letter or digit.");
            return new LegacyImportResult(null, report);
        }

        var countryNames = ReadNames(scenario, "cnam", "country", report);
        var provinceNames = ReadNames(scenario, "pnam", "province", report);
        var zoneNames = ReadNames(scenario, "zone", "zone", report);
        var year = ReadYear(scenario, report);

        foreach (var group in scenario.Records
                     .Where(record => !ConvertedScenarioTags.Contains(record.Tag))
                     .GroupBy(static record => record.Tag, StringComparer.Ordinal))
        {
            report.Defer($"scenario.tag.{group.Key}", group.Count());
        }

        if (scenario.TrailingBytes.Length > 0)
        {
            report.Defer("scenario.trailing-bytes", scenario.TrailingBytes.Length);
        }

        report.Defer("map.trailer-records", map.Profile.TrailerRecordCount);
        if (info is not null)
        {
            report.Defer("inf.overview-sections", 1);
            report.Defer("inf.country-briefings", info.CountrySections.Count);
            report.Defer("inf.metadata-values", info.Metadata.Count);
        }

        var mapProvinceIds = map.Cells
            .Where(static cell => !cell.IsOcean)
            .Select(static cell => (uint)cell.Province)
            .ToHashSet();
        var provinceIds = mapProvinceIds
            .Concat(provinceNames.Keys)
            .Distinct()
            .Order()
            .ToArray();
        var provinceOwners = ReadProvinceOwners(map, report);
        var capitalCells = ReadCapitalCells(map, report);
        var countryIds = countryNames.Keys
            .Concat(provinceOwners.Values.Where(static owner => owner.HasValue).Select(static owner => owner!.Value))
            .Concat(capitalCells.Keys)
            .Distinct()
            .Order()
            .ToArray();
        var countryNamespaceSize = countryIds.Length == 0 ? 0u : checked(countryIds[^1] + 1);
        var cellSeaZoneIds = new uint[map.Cells.Count];
        for (var index = 0; index < map.Cells.Count; index++)
        {
            var cell = map.Cells[index];
            if (!cell.IsOcean)
            {
                continue;
            }

            if (cell.NationZoneA < countryNamespaceSize)
            {
                report.Add(
                    LegacyImportSeverity.Error,
                    "map.invalid-sea-zone-reference",
                    $"map.cells[{index}]",
                    $"Ocean region value {cell.NationZoneA} is below the country namespace size {countryNamespaceSize}.");
                cellSeaZoneIds[index] = cell.NationZoneA;
            }
            else
            {
                cellSeaZoneIds[index] = cell.NationZoneA - countryNamespaceSize;
            }
        }

        var seaZoneIds = map.Cells
            .Select((cell, index) => (cell, index))
            .Where(static item => item.cell.IsOcean)
            .Select(item => cellSeaZoneIds[item.index])
            .Distinct()
            .Order()
            .ToArray();
        var seaZoneSet = seaZoneIds.ToHashSet();
        var unusedZoneRecords = scenario.Records.Count(record =>
            record.Tag == "zone" &&
            (record.Fields.Count == 0 || !seaZoneSet.Contains(record.Fields[0])));
        report.Defer("scenario.unused-zone-records", unusedZoneRecords);

        var provinceKeys = provinceIds.ToDictionary(static id => id, ProvinceKey);
        var seaZoneKeys = seaZoneIds.ToDictionary(static id => id, SeaZoneKey);
        var countryKeys = countryIds.ToDictionary(static id => id, CountryKey);
        var terrainCodes = map.Cells.Select(static cell => cell.Terrain).Distinct().Order().ToArray();
        var terrainKeys = terrainCodes.ToDictionary(static code => code, TerrainKey);
        var resourceCodes = map.Cells
            .SelectMany(static cell => new[] { cell.ResourceA, cell.ResourceB })
            .Where(static code => code != byte.MaxValue && ResourceNames.ContainsKey(code))
            .Distinct()
            .Order()
            .ToArray();
        var resourceKeys = resourceCodes.ToDictionary(static code => code, ResourceKey);

        WarnUnknownCodes(map, report);

        var cells = new CellContentDocument[map.Cells.Count];
        for (var index = 0; index < cells.Length; index++)
        {
            var source = map.Cells[index];
            if (source.NationZoneA != source.NationZoneB)
            {
                report.Add(
                    LegacyImportSeverity.Warning,
                    "map.nation-mirror-mismatch",
                    $"map.cells[{index}]",
                    $"Nation bytes differ ({source.NationZoneA} versus {source.NationZoneB}); the first value was used.");
            }

            var region = source.IsOcean
                ? new CellRegionContent { SeaZone = seaZoneKeys[cellSeaZoneIds[index]] }
                : new CellRegionContent { Province = provinceKeys[source.Province] };
            var resources = new List<string>(2);
            AddResource(source.ResourceA, resources, resourceKeys);
            AddResource(source.ResourceB, resources, resourceKeys);
            if (resources.Count == 2 && resources[0] == resources[1])
            {
                resources.RemoveAt(1);
                report.Add(
                    LegacyImportSeverity.Warning,
                    "map.duplicate-resource",
                    $"map.cells[{index}]",
                    "Both legacy resource slots contain the same resource; one modern deposit was emitted.");
            }

            cells[index] = new CellContentDocument
            {
                Terrain = terrainKeys[source.Terrain],
                Region = region,
                Resources = resources.ToArray(),
                HasSettlementSite = source.TownType is 34 or 35,
                River = DecodeRiver(source.River),
            };
        }

        var countries = countryIds.Select(id => new NamedContentDefinition
        {
            Key = countryKeys[id],
            Name = FindName(countryNames, id, "Country", report),
        }).ToArray();
        var provinces = provinceIds.Select(id => new NamedContentDefinition
        {
            Key = provinceKeys[id],
            Name = FindName(provinceNames, id, "Province", report),
        }).ToArray();
        var seaZones = seaZoneIds.Select(id => new NamedContentDefinition
        {
            Key = seaZoneKeys[id],
            Name = FindName(zoneNames, id, "Sea Zone", report),
        }).ToArray();

        var ownerContent = provinceIds.Select(id => new ProvinceOwnerContent
        {
            Province = provinceKeys[id],
            Country = provinceOwners.GetValueOrDefault(id) is { } owner && countryKeys.TryGetValue(owner, out var key)
                ? key
                : null,
        }).ToArray();
        var capitals = capitalCells.OrderBy(static pair => pair.Key).Select(pair => new CountryCapitalContent
        {
            Country = countryKeys[pair.Key],
            Cell = pair.Value,
        }).ToArray();
        var rails = ReadReciprocalRails(map, report);
        var title = string.IsNullOrWhiteSpace(info?.Title)
            ? $"Legacy {options.PackageKey}"
            : info.Title;
        var document = new WorldContentDocument
        {
            TerrainKeys = terrainCodes.Select(code => terrainKeys[code]).ToArray(),
            Commodities = CreateStandardCommodities(),
            Resources = resourceCodes.Select(code => new ResourceContentDefinition
            {
                Key = resourceKeys[code],
                Commodity = $"commodity.{ResourceCommodityNames[code]}",
            }).ToArray(),
            Map = new MapContentDocument
            {
                Key = $"map.legacy.{options.PackageKey}",
                Name = title,
                Width = map.Width,
                Height = map.Height,
                Provinces = provinces,
                SeaZones = seaZones,
                Cells = cells,
            },
            Countries = countries,
            Scenarios =
            [
                new ScenarioContentDocument
                {
                    Key = $"scenario.legacy.{options.PackageKey}",
                    Name = title,
                    StartingYear = year ?? 0,
                    ProvinceOwners = ownerContent,
                    Rails = rails,
                    Capitals = capitals,
                },
            ],
        };

        if (!report.HasErrors)
        {
            try
            {
                _ = WorldContentCompiler.Compile(document);
            }
            catch (ContentValidationException exception)
            {
                report.Add(
                    LegacyImportSeverity.Error,
                    "content.validation-failed",
                    exception.Path,
                    exception.Message);
            }
        }

        return new LegacyImportResult(report.HasErrors ? null : document, report);
    }

    private static Dictionary<uint, string> ReadNames(
        ScenarioDocument scenario,
        string tag,
        string description,
        LegacyImportReport report)
    {
        var result = new Dictionary<uint, string>();
        foreach (var (record, index) in scenario.Records.Select(static (record, index) => (record, index)))
        {
            if (record.Tag != tag)
            {
                continue;
            }

            if (record.Fields.Count != 1 || string.IsNullOrWhiteSpace(record.Name))
            {
                report.Add(
                    LegacyImportSeverity.Error,
                    $"scenario.invalid-{tag}",
                    $"scenario.records[{index}]",
                    $"The {description} name record must have one ID and a nonblank name.");
                continue;
            }

            var id = record.Fields[0];
            if (result.TryGetValue(id, out var existing) && existing != record.Name)
            {
                report.Add(
                    LegacyImportSeverity.Error,
                    $"scenario.conflicting-{tag}",
                    $"scenario.{tag}.{id.ToString(CultureInfo.InvariantCulture)}",
                    $"Legacy {description} ID {id} has conflicting names.");
            }
            else
            {
                result[id] = record.Name;
            }
        }

        return result;
    }

    private static int? ReadYear(ScenarioDocument scenario, LegacyImportReport report)
    {
        var values = new List<uint>();
        foreach (var (record, index) in scenario.Records.Select(static (record, index) => (record, index)))
        {
            if (record.Tag != "year")
            {
                continue;
            }

            if (record.Fields.Count != 1)
            {
                report.Add(
                    LegacyImportSeverity.Error,
                    "scenario.invalid-year",
                    $"scenario.records[{index}]",
                    "A year record must contain exactly one value.");
                continue;
            }

            values.Add(record.Fields[0]);
        }

        if (values.Count == 0)
        {
            report.Add(LegacyImportSeverity.Error, "scenario.missing-year", "scenario.year", "A starting year is required.");
            return null;
        }

        if (values.Distinct().Count() != 1)
        {
            report.Add(LegacyImportSeverity.Error, "scenario.conflicting-year", "scenario.year", "Duplicate year records disagree.");
            return null;
        }

        if (values[0] > int.MaxValue)
        {
            report.Add(LegacyImportSeverity.Error, "scenario.year-out-of-range", "scenario.year", "The starting year exceeds the modern integer range.");
            return null;
        }

        return (int)values[0];
    }

    private static Dictionary<uint, uint?> ReadProvinceOwners(MapDocument map, LegacyImportReport report)
    {
        var owners = new Dictionary<uint, uint?>();
        for (var index = 0; index < map.Cells.Count; index++)
        {
            var cell = map.Cells[index];
            if (cell.IsOcean)
            {
                continue;
            }

            var province = (uint)cell.Province;
            uint? owner = cell.NationZoneA == byte.MaxValue ? null : cell.NationZoneA;
            if (owners.TryGetValue(province, out var existing) && existing != owner)
            {
                report.Add(
                    LegacyImportSeverity.Error,
                    "map.conflicting-province-owner",
                    $"map.province.{province.ToString(CultureInfo.InvariantCulture)}",
                    $"Province {province} contains cells with conflicting owners.");
            }
            else
            {
                owners[province] = owner;
            }
        }

        return owners;
    }

    private static Dictionary<uint, int> ReadCapitalCells(MapDocument map, LegacyImportReport report)
    {
        var capitals = new Dictionary<uint, int>();
        for (var index = 0; index < map.Cells.Count; index++)
        {
            var cell = map.Cells[index];
            if (cell.TownType != 35)
            {
                continue;
            }

            if (cell.IsOcean || cell.NationZoneA == byte.MaxValue)
            {
                report.Add(
                    LegacyImportSeverity.Error,
                    "map.invalid-capital",
                    $"map.cells[{index}]",
                    "A capital must be a land cell with a country owner.");
                continue;
            }

            var country = (uint)cell.NationZoneA;
            if (!capitals.TryAdd(country, index))
            {
                report.Add(
                    LegacyImportSeverity.Error,
                    "map.duplicate-capital",
                    $"map.country.{country.ToString(CultureInfo.InvariantCulture)}",
                    $"Country {country} has more than one capital cell.");
            }
        }

        return capitals;
    }

    private static CellLinkContent[] ReadReciprocalRails(MapDocument map, LegacyImportReport report)
    {
        var links = new List<CellLinkContent>();
        for (var index = 0; index < map.Cells.Count; index++)
        {
            var source = map.Cells[index];
            if ((source.Rail & 0xc0) != 0)
            {
                report.Add(
                    LegacyImportSeverity.Warning,
                    "map.unknown-rail-bits",
                    $"map.cells[{index}].rail",
                    $"Rail value {source.Rail} has bits outside the six known directions; those bits were ignored.");
            }

            foreach (var direction in RailDirections)
            {
                if ((source.Rail & direction.Bit) == 0)
                {
                    continue;
                }

                var neighbour = GetNeighbour(index, map.Width, map.Height, direction.Bit);
                if (neighbour is null || (map.Cells[neighbour.Value].Rail & direction.OppositeBit) == 0)
                {
                    report.Add(
                        LegacyImportSeverity.Warning,
                        "map.asymmetric-rail-endpoint",
                        $"map.cells[{index}].rail.{direction.Name}",
                        "The legacy rail endpoint has no reciprocal neighbour and was dropped.");
                    continue;
                }

                if (index >= neighbour.Value)
                {
                    continue;
                }

                if (source.IsOcean || map.Cells[neighbour.Value].IsOcean)
                {
                    report.Add(
                        LegacyImportSeverity.Error,
                        "map.invalid-rail-reference",
                        $"map.cells[{index}].rail.{direction.Name}",
                        "A reciprocal rail link refers to an ocean cell.");
                    continue;
                }

                links.Add(new CellLinkContent { First = index, Second = neighbour.Value });
            }
        }

        return links.ToArray();
    }

    private static int? GetNeighbour(int index, int width, int height, byte bit)
    {
        var x = index % width;
        var y = index / width;
        var odd = (y & 1) != 0;
        var (dx, dy) = bit switch
        {
            1 => (odd ? 1 : 0, -1),
            2 => (1, 0),
            4 => (odd ? 1 : 0, 1),
            8 => (odd ? 0 : -1, 1),
            16 => (-1, 0),
            32 => (odd ? 0 : -1, -1),
            _ => throw new ArgumentOutOfRangeException(nameof(bit)),
        };
        var targetX = x + dx;
        var targetY = y + dy;
        return (uint)targetX < (uint)width && (uint)targetY < (uint)height
            ? checked((targetY * width) + targetX)
            : null;
    }

    private static void WarnUnknownCodes(MapDocument map, LegacyImportReport report)
    {
        foreach (var group in map.Cells.GroupBy(static cell => cell.Terrain).Where(group => !TerrainNames.ContainsKey(group.Key)))
        {
            report.Add(
                LegacyImportSeverity.Warning,
                "map.unknown-terrain-code",
                $"map.terrain-code.{group.Key}",
                $"Terrain code {group.Key} is unknown; a numeric placeholder terrain key was emitted.",
                group.Count());
        }

        foreach (var group in map.Cells
                     .SelectMany(static cell => new[] { cell.ResourceA, cell.ResourceB })
                     .Where(static code => code != byte.MaxValue)
                     .GroupBy(static code => code)
                     .Where(group => !ResourceNames.ContainsKey(group.Key)))
        {
            report.Add(
                LegacyImportSeverity.Warning,
                "map.unknown-resource-code",
                $"map.resource-code.{group.Key}",
                $"Resource code {group.Key} is unknown; no resource feature was inferred.",
                group.Count());
        }

        foreach (var group in map.Cells
                     .Select(static cell => cell.TownType)
                     .Where(static code => code is not 0 and not 34 and not 35)
                     .GroupBy(static code => code))
        {
            report.Add(
                LegacyImportSeverity.Warning,
                "map.unknown-town-code",
                $"map.town-code.{group.Key}",
                $"Town code {group.Key} is unknown; no settlement feature was inferred.",
                group.Count());
        }

        foreach (var group in map.Cells
                     .Select(static cell => cell.River)
                     .Where(static code => code != 0 && !LegacyRiverCodes.KnownPaths.ContainsKey(code))
                     .GroupBy(static code => code))
        {
            report.Add(
                LegacyImportSeverity.Warning,
                "map.unknown-river-code",
                $"map.river-code.{group.Key}",
                $"River code {group.Key} is unknown; no river path was inferred.",
                group.Count());
        }
    }

    private static RiverPathContent? DecodeRiver(byte code) =>
        LegacyRiverCodes.TryDecode(code, out var path)
            ? new RiverPathContent { First = path.First, Second = path.Second }
            : null;

    private static void AddResource(
        byte code,
        ICollection<string> resources,
        IReadOnlyDictionary<byte, string> resourceKeys)
    {
        if (resourceKeys.TryGetValue(code, out var key))
        {
            resources.Add(key);
        }
    }

    private static string FindName(
        IReadOnlyDictionary<uint, string> names,
        uint id,
        string description,
        LegacyImportReport report)
    {
        if (names.TryGetValue(id, out var name))
        {
            return name;
        }

        report.Add(
            LegacyImportSeverity.Warning,
            "scenario.missing-name",
            $"{description.ToLowerInvariant().Replace(' ', '-')}.{id.ToString(CultureInfo.InvariantCulture)}",
            $"{description} {id} has no legacy name; a deterministic fallback was used.");
        return $"Legacy {description} {id.ToString(CultureInfo.InvariantCulture)}";
    }

    private static bool IsValidPackageKey(string key) =>
        key.Length is >= 1 and <= 96 &&
        Regex.IsMatch(key, "^[a-z0-9](?:[a-z0-9._-]*[a-z0-9])?$", RegexOptions.CultureInvariant);

    private static string TerrainKey(byte code) => TerrainNames.TryGetValue(code, out var name)
        ? $"terrain.{name}"
        : $"terrain.legacy-unknown-{code.ToString("D3", CultureInfo.InvariantCulture)}";

    private static string ResourceKey(byte code) => $"resource.{ResourceNames[code]}";

    private static CommodityContentDefinition[] CreateStandardCommodities() =>
    [
        Commodity("grain", "Grain", CommodityCategory.Raw),
        Commodity("livestock", "Livestock", CommodityCategory.Raw),
        Commodity("fruit", "Fruit", CommodityCategory.Raw),
        Commodity("fish", "Fish", CommodityCategory.Raw),
        Commodity("cotton", "Cotton", CommodityCategory.Raw),
        Commodity("wool", "Wool", CommodityCategory.Raw),
        Commodity("horses", "Horses", CommodityCategory.Raw),
        Commodity("timber", "Timber", CommodityCategory.Raw),
        Commodity("coal", "Coal", CommodityCategory.Raw),
        Commodity("iron", "Iron", CommodityCategory.Raw),
        Commodity("oil", "Oil", CommodityCategory.Raw),
        Commodity("gold", "Gold", CommodityCategory.Raw),
        Commodity("gems", "Gems", CommodityCategory.Raw),
        Commodity("canned-food", "Canned Food", CommodityCategory.Material),
        Commodity("fabric", "Fabric", CommodityCategory.Material),
        Commodity("paper", "Paper", CommodityCategory.Material),
        Commodity("lumber", "Lumber", CommodityCategory.Material),
        Commodity("steel", "Steel", CommodityCategory.Material),
        Commodity("fuel", "Fuel", CommodityCategory.Material),
        Commodity("clothing", "Clothing", CommodityCategory.Goods),
        Commodity("furniture", "Furniture", CommodityCategory.Goods),
        Commodity("hardware", "Hardware", CommodityCategory.Goods),
        Commodity("armaments", "Armaments", CommodityCategory.Goods),
    ];

    private static CommodityContentDefinition Commodity(
        string key,
        string name,
        CommodityCategory category) => new()
        {
            Key = $"commodity.{key}",
            Name = name,
            Category = category,
        };

    private static string CountryKey(uint id) => $"country.legacy.{id.ToString("D3", CultureInfo.InvariantCulture)}";

    private static string ProvinceKey(uint id) => $"province.legacy.{id.ToString("D5", CultureInfo.InvariantCulture)}";

    private static string SeaZoneKey(uint id) => $"sea-zone.legacy.{id.ToString("D3", CultureInfo.InvariantCulture)}";

    private static readonly (byte Bit, byte OppositeBit, string Name)[] RailDirections =
    [
        (1, 8, "north-east"),
        (2, 16, "east"),
        (4, 32, "south-east"),
        (8, 1, "south-west"),
        (16, 2, "west"),
        (32, 4, "north-west"),
    ];
}
