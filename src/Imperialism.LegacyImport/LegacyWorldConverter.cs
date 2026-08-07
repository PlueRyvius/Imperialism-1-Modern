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

/// <summary>
/// One legacy terrain code's key stem, display name, improvability, and whether
/// a Prospector may search it — and if so, at what price in knowledge.
/// </summary>
/// <remarks>
/// <paramref name="Prospecting"/> is a tri-state and the middle case is the easy
/// one to lose: <see cref="LegacyProspecting.No"/> means the ground hides
/// nothing, <see cref="LegacyProspecting.Open"/> means search it from turn one,
/// and <see cref="LegacyProspecting.NeedsOilDrilling"/> means the eye cursor
/// appears only once the country has invested.
/// </remarks>
internal readonly record struct LegacyTerrain(
    string Name,
    string DisplayName,
    bool IsImprovable,
    LegacyProspecting Prospecting = LegacyProspecting.No);

internal enum LegacyProspecting : byte
{
    No,
    Open,
    NeedsOilDrilling,
}

public static class LegacyWorldConverter
{
    /// <summary>
    /// The seventeen legacy terrain codes, their display names from the
    /// manual's Terrain Tiles Table, and whether a civilian can improve them.
    /// </summary>
    /// <remarks>
    /// The codes and the table line up one for one — fourteen land types plus
    /// town, capital and ocean — which is what lets "dry plains" be identified
    /// with code 1 despite our key for it being <c>clear</c>.
    /// <para>
    /// Improvability is the manual's: the table gives every terrain a civilian
    /// worker, and dry plains, horse ranch and scrub forest get "None". Towns
    /// and capitals admit only the Engineer, who builds rather than improves,
    /// and the manual says a capital already produces at the highest level it
    /// can. The corpus corroborates without exception — of 481 <c>deve</c>
    /// records across five scenarios, none lands on any of these.
    /// </para>
    /// <para>
    /// Prospectability is the same table read for its second column. Barren
    /// hills and mountains list "Miner, Prospector" and swamp, desert and tundra
    /// list "Driller, Prospector"; every other terrain names no Prospector at
    /// all, because it announces what it holds by being what it is. The oil
    /// three are gated: "when your country invests in Oil Drilling technology,
    /// the eye cursor appears over unprospected swamps, deserts, and tundra as
    /// well."
    /// </para>
    /// </remarks>
    private static readonly IReadOnlyDictionary<byte, LegacyTerrain> Terrains =
        new Dictionary<byte, LegacyTerrain>
        {
            [0] = new("ocean", "Ocean", false),
            [1] = new("clear", "Dry Plains", false),
            [2] = new("cotton", "Plantation", true),
            [3] = new("cattle-ranch", "Open Range", true),
            [4] = new("horse-ranch", "Horse Ranch", false),
            [5] = new("grain-farm", "Farm", true),
            [6] = new("orchard", "Orchard", true),
            [7] = new("wool-hill", "Fertile Hills", true),
            [8] = new("hill", "Barren Hills", true, LegacyProspecting.Open),
            [9] = new("mountain", "Mountains", true, LegacyProspecting.Open),
            [10] = new("swamp", "Swamp", true, LegacyProspecting.NeedsOilDrilling),
            [11] = new("desert", "Desert", true, LegacyProspecting.NeedsOilDrilling),
            [12] = new("tundra", "Tundra", true, LegacyProspecting.NeedsOilDrilling),
            [13] = new("forest", "Hardwood Forest", true),
            [14] = new("town", "Town", false),
            [15] = new("scrub-forest", "Scrub Forest", false),
            [16] = new("capital", "Capital", false),
        };

    /// <summary>
    /// The manual's Benefits of Technology Table, in printed order. **The order
    /// is load-bearing**: a <c>tech</c> record is <c>[country, id]</c> with a
    /// 1-based id and nothing naming it, and this list is what an id is resolved
    /// against.
    /// </summary>
    /// <remarks>
    /// The mapping was tested against the corpus before anything was built on
    /// it. Of the four originals carrying both <c>tech</c> and <c>deve</c>
    /// records, 379 authored levels are permitted by the technologies their
    /// owner holds and 4 are not — all four the same deposit, timber at Level
    /// III, in one country of <c>s1</c>. The decisive case is <c>s3</c>, whose
    /// powers hold **unequal** sets (9, 13 and 14 technologies), and which
    /// produces no contradiction at all: a wrong ordering would fire at once on
    /// the power holding only nine. See <c>docs/formulas/technology.md</c>.
    /// <para>
    /// Only the entries this engine can act on are given a gate below. Regiments,
    /// ships, the Refinery and rail-through-terrain are named here so the
    /// numbering is right and modelled nowhere.
    /// </para>
    /// </remarks>
    private static readonly IReadOnlyList<string> TechnologyTable =
    [
        "High Pressure Steam Engine",
        "Seed Drill",
        "Cotton Gin",
        "Streamlined Hulls",
        "Square-Set Timbering",
        "Iron Railroad Bridge",
        "Feed Grasses",
        "Spinning Jenny",
        "Paddlewheels",
        "Steel and Iron Plows",
        "Bessemer Converter",
        "Compound Steam Engine",
        "Rifled Artillery",
        "Breech-Loading Rifles",
        "Advanced Iron Working",
        "Power Loom",
        "Mechanical Reaper",
        "Commercial Fertiliser",
        "Oil Drilling",
        "Barbed Wire",
        "Steel Armour Plate",
        "Large Artillery",
        "Dynamite",
        "Marine Engineering",
        "Machine Guns",
        "Chemistry",
        "Improved Range-Finding",
        "Internal Combustion",
    ];

    /// <summary>
    /// Oil Drilling, which gates prospecting swamp, desert and tundra. Held as
    /// its table position so the key and the gate cannot drift apart.
    /// </summary>
    private const int OilDrillingPosition = 19;

    /// <summary>
    /// The two technologies every power starts with, whatever the scenario says:
    /// "every player always starts with the first two technologies listed below:
    /// High Pressure Steam Engine and Seed Drill".
    /// </summary>
    /// <remarks>
    /// This is one of the seven engine defaults <c>docs/formulas/_index.md</c>
    /// calls unrecoverable from the corpus, and it is recovered — from the
    /// manual. A skirmish carries no <c>tech</c> record and its powers still
    /// start able to farm.
    /// </remarks>
    private static readonly int[] StartingTechnologyPositions = [1, 2];

    /// <summary>
    /// The Benefits of Technology Table read as a ladder: what it takes to raise
    /// each deposit to level 1, 2 and 3, as positions in
    /// <see cref="TechnologyTable"/>. Zero means the rung is ungated, which is
    /// true only of a mine opening at Level I.
    /// </summary>
    /// <remarks>
    /// Cross-checked row by row against the seven gates already transcribed in
    /// <c>docs/reference/manual-mechanics.md</c>; every one agrees. Fish and
    /// horses are absent because no civilian improves them at all.
    /// </remarks>
    private static readonly IReadOnlyDictionary<byte, int[]> ResourceTechnologyLadders =
        new Dictionary<byte, int[]>
        {
            [17] = [2, 10, 17],  // grain:     Seed Drill, Steel and Iron Plows, Mechanical Reaper
            [18] = [2, 10, 18],  // fruit:     Seed Drill, Steel and Iron Plows, Commercial Fertiliser
            [0] = [3, 8, 16],    // cotton:    Cotton Gin, Spinning Jenny, Power Loom
            [1] = [7, 8, 16],    // wool:      Feed Grasses, Spinning Jenny, Power Loom
            [20] = [7, 20, 26],  // livestock: Feed Grasses, Barbed Wire, Chemistry
            [2] = [6, 12, 23],   // timber:    Iron RR Bridge, Compound Steam Engine, Dynamite
            [3] = [0, 5, 23],    // coal:      none, Square-Set Timbering, Dynamite
            [4] = [0, 5, 23],    // iron
            [21] = [0, 5, 23],   // gems
            [22] = [0, 5, 23],   // gold
            [6] = [19, 26, 28],  // oil:       Oil Drilling, Chemistry, Internal Combustion
        };

    private static string TechnologyKey(int position) =>
        $"technology.{TechnologyTable[position - 1].ToLowerInvariant().Replace(' ', '-')}";

    /// <summary>
    /// Deposits a Prospector must find first: "coal, iron, gold, gems, and oil
    /// must be found by a Prospector before they can be exploited by your other
    /// civilians". Everything else is announced by its terrain.
    /// </summary>
    private static readonly IReadOnlySet<byte> HiddenResources =
        new HashSet<byte>
        {
            3,  // coal
            4,  // iron
            6,  // oil
            21, // gems
            22, // gold
        };

    /// <summary>
    /// The civilian types this content declares, in the order the 1997
    /// <c>civi</c> record numbers them.
    /// </summary>
    /// <remarks>
    /// Codes 0 to 5 are the six the corpus ships, identified from where they
    /// stand: type 4 is the only one found in towns, where the manual says only
    /// the Engineer may work; type 5 is found on fertile hills and open range,
    /// which are the Rancher's two terrains; type 2 on plantations, farms and
    /// orchards, which are the Farmer's three; type 3 in hardwood forest. The
    /// skirmishes settle the last pair — <c>s11</c> and <c>s15</c> give each of
    /// the seven powers exactly one type 1 and one type 4, a Prospector and an
    /// Engineer.
    /// <para>
    /// The Driller is appended because the Resource Development Table names it
    /// as oil's improver and the deposits must be able to refer to it, even
    /// though no <c>civi</c> record in the corpus is one. The Developer and the
    /// Fisherman are left out: neither improves anything, so nothing would
    /// reference them.
    /// </para>
    /// </remarks>
    private static readonly IReadOnlyList<(string Name, string DisplayName, CivilianWorkKind Work)> CivilianTypes =
    [
        ("miner", "Miner", CivilianWorkKind.Improve),
        ("prospector", "Prospector", CivilianWorkKind.Prospect),
        ("farmer", "Farmer", CivilianWorkKind.Improve),
        ("forester", "Forester", CivilianWorkKind.Improve),
        ("engineer", "Engineer", CivilianWorkKind.Improve),
        ("rancher", "Rancher", CivilianWorkKind.Improve),
        ("driller", "Oil Driller", CivilianWorkKind.Improve),
    ];

    /// <summary>
    /// How many turns a civilian's work takes. <b>This is a guess</b> — the one
    /// number in this phase with nothing behind it. See
    /// <c>docs/formulas/development.md</c>.
    /// </summary>
    private const int CivilianWorkTurns = 1;

    /// <summary>
    /// The manual's Resource Development Table read the other way: which
    /// civilian raises each deposit. Fish has none, and horses are absent from
    /// the table entirely, which agrees with the horse ranch admitting no
    /// worker.
    /// </summary>
    private static readonly IReadOnlyDictionary<byte, string> ResourceImprovers =
        new Dictionary<byte, string>
        {
            [0] = "farmer",   // cotton
            [1] = "rancher",  // wool
            [2] = "forester", // timber
            [3] = "miner",    // coal
            [4] = "miner",    // iron
            [6] = "driller",  // oil
            [17] = "farmer",  // grain
            [18] = "farmer",  // fruit
            [20] = "rancher", // livestock
            [21] = "miner",   // gems
            [22] = "miner",   // gold
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

    /// <summary>
    /// The manual's Resource Development Table, keyed by legacy deposit code.
    /// Transcribed rather than derived: the slope differs per deposit and two
    /// deposits have no improvement at all, so no single formula covers them.
    /// See <c>docs/reference/manual-mechanics.md</c>.
    /// </summary>
    private static readonly IReadOnlyDictionary<byte, long[]> ResourceYieldCurves =
        new Dictionary<byte, long[]>
        {
            [0] = WorldContentCodec.CultivatedYieldByDevelopmentLevel,      // cotton
            [1] = WorldContentCodec.CultivatedYieldByDevelopmentLevel,      // wool
            [2] = WorldContentCodec.CultivatedYieldByDevelopmentLevel,      // forest / timber
            [3] = WorldContentCodec.HeavyMineralYieldByDevelopmentLevel,    // coal
            [4] = WorldContentCodec.HeavyMineralYieldByDevelopmentLevel,    // iron
            [5] = WorldContentCodec.UnimprovableYieldByDevelopmentLevel,    // horses
            [6] = WorldContentCodec.HeavyMineralYieldByDevelopmentLevel,    // oil
            [17] = WorldContentCodec.CultivatedYieldByDevelopmentLevel,     // grain
            [18] = WorldContentCodec.CultivatedYieldByDevelopmentLevel,     // fruit
            [19] = WorldContentCodec.UnimprovableYieldByDevelopmentLevel,   // fish
            [20] = WorldContentCodec.CultivatedYieldByDevelopmentLevel,     // cattle / livestock
            [21] = WorldContentCodec.PreciousMineralYieldByDevelopmentLevel, // gems
            [22] = WorldContentCodec.PreciousMineralYieldByDevelopmentLevel, // gold
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

    private static readonly IReadOnlyDictionary<uint, string> WarehouseCommodityNames =
        new Dictionary<uint, string>
        {
            [0] = "cotton",
            [1] = "wool",
            [2] = "timber",
            [3] = "coal",
            [4] = "iron",
            [5] = "horses",
            [6] = "oil",
            [7] = "canned-food",
            [8] = "fabric",
            [9] = "lumber",
            [10] = "paper",
            [11] = "steel",
            [12] = "fuel",
            [13] = "clothing",
            [14] = "furniture",
            [15] = "hardware",
            [16] = "armaments",
            [17] = "grain",
            [18] = "fruit",
            [19] = "fish",
            [20] = "livestock",
        };

    private static readonly IReadOnlyDictionary<uint, string> CapacityFacilityNames =
        new Dictionary<uint, string>
        {
            [0] = "textile-mill",
            [1] = "clothing-factory",
            [2] = "steel-mill",
            [3] = "metal-works",
            [4] = "lumber-mill",
            [5] = "furniture-factory",
            [6] = "oil-refinery",
        };

    private static readonly HashSet<string> ConvertedScenarioTags =
        new(
            [
                "cnam", "pnam", "zone", "year", "capa", "ware", "deve", "port", "rail", "labo",
                "civi", "tech", "tran", "cash",
            ],
            StringComparer.Ordinal);

    /// <summary>
    /// The original's food rules: half the workers want grain, a quarter fruit,
    /// and the rest livestock or fish. Expressed as a repeating cycle of four so
    /// that any headcount splits in those proportions without a rounding rule.
    /// </summary>
    private static FeedingContentSettings CreateStandardFeeding() => new()
    {
        PreferenceCycle =
        [
            new FoodPreferenceContent { Accepted = ["commodity.grain"] },
            new FoodPreferenceContent { Accepted = ["commodity.fruit"] },
            new FoodPreferenceContent { Accepted = ["commodity.grain"] },
            new FoodPreferenceContent { Accepted = ["commodity.livestock", "commodity.fish"] },
        ],
        LabourByGrade = [1, 2, 4],
        CannedFood = "commodity.canned-food",
    };

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
        var initialInventory = ReadInitialInventory(scenario, countryKeys, report);
        var productionCapacities = ReadProductionCapacities(scenario, countryKeys, report);
        var cellDevelopment = ReadCellDevelopment(scenario, map, report);
        var ports = ReadPorts(scenario, map, report);
        var depots = ReadDepots(scenario, map, report);
        var workers = ReadWorkforce(scenario, countryKeys, report);
        var countryTechnologies = ReadCountryTechnologies(scenario, countryKeys, report);
        var transportCapacity = ReadTransportCapacity(scenario, countryKeys, report);
        var countryCash = ReadCountryCash(scenario, countryKeys, report);
        var civilians = ReadCivilians(scenario, map, countryKeys, report);
        var title = string.IsNullOrWhiteSpace(info?.Title)
            ? $"Legacy {options.PackageKey}"
            : info.Title;
        var document = new WorldContentDocument
        {
            Terrains = terrainCodes.Select(TerrainDefinitionFor).ToArray(),
            CivilianTypes = CivilianTypes.Select(static type => new CivilianTypeContentDefinition
            {
                Key = $"civilian.{type.Name}",
                Name = type.DisplayName,
                WorkTurns = CivilianWorkTurns,
                Work = type.Work,
            }).ToArray(),

            // The manual's whole table, in printed order, because a tech record
            // is a bare 1-based index into it.
            Technologies = TechnologyTable.Select(static (name, offset) => new NamedContentDefinition
            {
                Key = TechnologyKey(offset + 1),
                Name = name,
            }).ToArray(),
            Commodities = CreateStandardCommodities(),
            ProductionFacilities = CreateStandardProductionFacilities(),
            ProductionRecipes = CreateStandardProductionRecipes(),
            ExpansionCostPerCapacityPoint = CreateStandardExpansionCost(),
            Migration = CreateStandardMigration(),
            Resources = resourceCodes.Select(code => new ResourceContentDefinition
            {
                Key = resourceKeys[code],
                Commodity = $"commodity.{ResourceCommodityNames[code]}",

                // The 1997 map records which deposit sits on a cell and never
                // its output, so the curve comes from the manual's Resource
                // Development Table rather than from the file. No deposit
                // declares a RequiredTechnology: the manual gates improvement
                // *levels* behind technology and never extraction from a
                // deposit that is already open, which is a different hook —
                // TechnologyByDevelopmentLevel below.
                YieldByDevelopmentLevel = [.. ResourceYieldCurves[code]],

                // Which civilian raises this deposit, from the manual's
                // Resource Development Table. Null for fish, which the table
                // gives no worker, and for horses, which it omits.
                ImprovedBy = ResourceImprovers.TryGetValue(code, out var improver)
                    ? $"civilian.{improver}"
                    : null,

                // Coal, iron, gold, gems and oil are on the map and invisible
                // to their owner until a Prospector has searched the tile.
                RequiresDiscovery = HiddenResources.Contains(code),

                // What each rung of this deposit's curve costs in knowledge.
                // Index 0 is the level a tile starts at and is always ungated.
                TechnologyByDevelopmentLevel = ResourceTechnologyLadders
                    .TryGetValue(code, out var ladder)
                    ? [null, .. ladder.Select(static step =>
                        step == 0 ? null : TechnologyKey(step))]
                    : null,
            }).ToArray(),
            Feeding = CreateStandardFeeding(),

            // The two the manual gives every power outright. Nothing else here
            // is defaulted: a shipped scenario authors its own industry and
            // workforce, so this block exists purely to carry the knowledge no
            // record ever states.
            StartingDefaults = new StartingDefaultsContent
            {
                Technologies = [.. StartingTechnologyPositions.Select(TechnologyKey)],
                TransportCapacity = DefaultTransportCapacity,
                Inventory = CreateStandardStartingStock(),
                Cash = DefaultStartingCash,
            },
            Transport = CreateStandardTransport(),
            Extraction = new ExtractionContentSettings
            {
                CatchmentRadius = WorldContentCodec.DefaultCatchmentRadius,

                // Coast and river alike give a port one unit of fish per turn.
                // Fish is the one resource no civilian unit improves, so it has
                // no place in the development table and arrives this way instead.
                PortFishing = new PortFishingContent
                {
                    Commodity = "commodity.fish",
                    YieldPerAdjacentWaterTile = WorldContentCodec.DefaultPortFishYieldPerWaterTile,
                },
            },
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
                    InitialInventory = initialInventory,
                    ProductionCapacities = productionCapacities,
                    CellDevelopment = cellDevelopment,
                    Ports = ports,
                    Depots = depots,
                    Workers = workers,
                    Civilians = civilians,
                    CountryTechnologies = countryTechnologies,
                    TransportCapacity = transportCapacity,
                    Cash = countryCash,

                    // Every power the scenario gives a workforce to. `labo` is
                    // the one record that names the Great Powers and only them
                    // — seven in every shipped scenario — so it is how the
                    // importer tells them from the minor nations without
                    // guessing. They are the powers the manual's starting two
                    // technologies belong to.
                    DefaultStartCountries = [.. workers
                        .Select(static item => item.Country)
                        .Distinct(StringComparer.Ordinal)],
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

    /// <summary>
    /// Converts <c>deve</c> records into starting development levels. The record
    /// is <c>[cell, level 1-3]</c>, verified across the corpus; a cell reference
    /// is a linear row-major index, not a coordinate pair. Levels outside 1-3
    /// are reported rather than clamped, since a value the original never writes
    /// means the reading is wrong, not that the file is unusual.
    /// </summary>
    /// <remarks>
    /// A cell may carry more than one record: <c>s1</c> does it three times, as
    /// <c>[2,1]</c>, <c>[1,1]</c> and <c>[2,1]</c>. That is shipped data, so it
    /// is legal by definition and treating it as corruption would be the wrong
    /// rule. The highest level wins, on the grounds that development is a level
    /// a cell has rather than a stack of separate works, so the largest record
    /// is the only one consistent with all of them. Last-record-wins is the
    /// alternative reading and just two cells in one file tell them apart, so
    /// the choice is recorded here rather than presented as settled.
    /// </remarks>
    private static CellDevelopmentContent[] ReadCellDevelopment(
        ScenarioDocument scenario,
        MapDocument map,
        LegacyImportReport report)
    {
        const int maximumLegacyLevel = 3;
        var byCell = new Dictionary<uint, int>();
        var order = new List<uint>();
        foreach (var (record, index) in scenario.Records.Select(static (record, index) => (record, index)))
        {
            if (record.Tag != "deve")
            {
                continue;
            }

            var path = $"scenario.records[{index}]";
            if (record.Fields.Count != 2)
            {
                report.Add(LegacyImportSeverity.Error, "scenario.invalid-deve", path, "A deve record must contain a cell and a level.");
                continue;
            }

            var cell = record.Fields[0];
            var level = record.Fields[1];
            if (cell >= (uint)map.Cells.Count)
            {
                report.Add(LegacyImportSeverity.Error, "scenario.invalid-deve-cell", path, $"Development refers to cell {cell} outside the map.");
                continue;
            }

            if (map.Cells[(int)cell].IsOcean)
            {
                report.Add(LegacyImportSeverity.Error, "scenario.deve-on-ocean", path, $"Development refers to ocean cell {cell}.");
                continue;
            }

            if (level == 0 || level > maximumLegacyLevel)
            {
                report.Add(LegacyImportSeverity.Warning, "scenario.unexpected-deve-level", path, $"Development level {level} is outside the corpus range 1-{maximumLegacyLevel}; no level was emitted.");
                continue;
            }

            if (byCell.TryGetValue(cell, out var existing))
            {
                var kept = Math.Max(existing, (int)level);
                report.Add(
                    LegacyImportSeverity.Warning,
                    "scenario.repeated-deve",
                    path,
                    $"Cell {cell} is developed more than once ({existing} and {level}); kept {kept}.");
                byCell[cell] = kept;
                continue;
            }

            byCell.Add(cell, (int)level);
            order.Add(cell);
        }

        return order
            .Select(cell => new CellDevelopmentContent { Cell = (int)cell, Level = byCell[cell] })
            .ToArray();
    }

    /// <summary>
    /// Converts <c>tran</c> records into starting transport capacity. The record
    /// is <c>[country, capacity]</c> — one number for the whole network, matching
    /// the manual's single shared capacity bar.
    /// </summary>
    /// <remarks>
    /// A scenario that carries none leaves every power on the engine's default,
    /// which is a guess; see <see cref="DefaultTransportCapacity"/>. The values
    /// a mission does author are authored design and must not be read as
    /// gameplay constants.
    /// </remarks>
    private static TransportCapacityContent[] ReadTransportCapacity(
        ScenarioDocument scenario,
        IReadOnlyDictionary<uint, string> countryKeys,
        LegacyImportReport report)
    {
        var result = new List<TransportCapacityContent>();
        var seen = new HashSet<uint>();
        foreach (var (record, index) in scenario.Records.Select(static (record, index) => (record, index)))
        {
            if (record.Tag != "tran")
            {
                continue;
            }

            var path = $"scenario.records[{index}]";
            if (record.Fields.Count != 2)
            {
                report.Add(
                    LegacyImportSeverity.Error,
                    "scenario.invalid-tran",
                    path,
                    "A tran record must contain a country and a capacity.");
                continue;
            }

            var country = record.Fields[0];
            if (!countryKeys.TryGetValue(country, out var countryKey))
            {
                report.Add(
                    LegacyImportSeverity.Error,
                    "scenario.invalid-tran-country",
                    path,
                    $"Transport capacity refers to unknown country {country}.");
                continue;
            }

            if (!seen.Add(country))
            {
                report.Add(
                    LegacyImportSeverity.Warning,
                    "scenario.repeated-tran",
                    path,
                    $"Country {country} has more than one transport capacity record.");
                continue;
            }

            result.Add(new TransportCapacityContent
            {
                Country = countryKey,
                Capacity = record.Fields[1],
            });
        }

        return result.ToArray();
    }

    /// <summary>
    /// Converts <c>cash</c> records into starting treasuries. The record is
    /// <c>[country, amount]</c> — the same two-field shape as <c>tran</c>.
    /// </summary>
    /// <remarks>
    /// A scenario that carries none leaves every power on the engine's default,
    /// which is a guess; see <see cref="DefaultStartingCash"/>. What a mission
    /// authors is authored design and must not be read as a gameplay constant:
    /// <c>s1</c>, <c>s13</c> and <c>s14</c> give their seven powers 1,500 to
    /// 10,000 apiece and <c>s3</c> spans 1,500 to 15,000.
    /// </remarks>
    private static CountryCashContent[] ReadCountryCash(
        ScenarioDocument scenario,
        IReadOnlyDictionary<uint, string> countryKeys,
        LegacyImportReport report)
    {
        var result = new List<CountryCashContent>();
        var seen = new HashSet<uint>();
        foreach (var (record, index) in scenario.Records.Select(static (record, index) => (record, index)))
        {
            if (record.Tag != "cash")
            {
                continue;
            }

            var path = $"scenario.records[{index}]";
            if (record.Fields.Count != 2)
            {
                report.Add(
                    LegacyImportSeverity.Error,
                    "scenario.invalid-cash",
                    path,
                    "A cash record must contain a country and an amount.");
                continue;
            }

            var country = record.Fields[0];
            if (!countryKeys.TryGetValue(country, out var countryKey))
            {
                report.Add(
                    LegacyImportSeverity.Error,
                    "scenario.invalid-cash-country",
                    path,
                    $"Cash refers to unknown country {country}.");
                continue;
            }

            if (!seen.Add(country))
            {
                report.Add(
                    LegacyImportSeverity.Warning,
                    "scenario.repeated-cash",
                    path,
                    $"Country {country} has more than one cash record.");
                continue;
            }

            result.Add(new CountryCashContent
            {
                Country = countryKey,
                Amount = record.Fields[1],
            });
        }

        return result.ToArray();
    }

    /// <summary>
    /// Converts <c>tech</c> records into starting knowledge. The record is
    /// <c>[country, id]</c>, where the id is a **1-based index into the manual's
    /// Benefits of Technology Table** — see <see cref="TechnologyTable"/> for the
    /// corpus evidence behind that reading.
    /// </summary>
    /// <remarks>
    /// A scenario grants technology on top of the two every power starts with,
    /// so a skirmish carrying no <c>tech</c> record at all is not a power that
    /// knows nothing. `s10`, `s11` and `s15` do exactly that.
    /// </remarks>
    private static CountryTechnologyContent[] ReadCountryTechnologies(
        ScenarioDocument scenario,
        IReadOnlyDictionary<uint, string> countryKeys,
        LegacyImportReport report)
    {
        var result = new List<CountryTechnologyContent>();
        var seen = new HashSet<(uint Country, uint Technology)>();
        foreach (var (record, index) in scenario.Records.Select(static (record, index) => (record, index)))
        {
            if (record.Tag != "tech")
            {
                continue;
            }

            var path = $"scenario.records[{index}]";
            if (record.Fields.Count != 2)
            {
                report.Add(
                    LegacyImportSeverity.Error,
                    "scenario.invalid-tech",
                    path,
                    "A tech record must contain a country and a technology.");
                continue;
            }

            var country = record.Fields[0];
            var technology = record.Fields[1];
            if (!countryKeys.TryGetValue(country, out var countryKey))
            {
                report.Add(
                    LegacyImportSeverity.Error,
                    "scenario.invalid-tech-country",
                    path,
                    $"Technology refers to unknown country {country}.");
                continue;
            }

            if (technology == 0 || technology > (uint)TechnologyTable.Count)
            {
                report.Add(
                    LegacyImportSeverity.Warning,
                    "scenario.unknown-tech-id",
                    path,
                    $"Technology {technology} is outside the manual's table of " +
                    $"{TechnologyTable.Count}; no knowledge was granted.");
                continue;
            }

            if (!seen.Add((country, technology)))
            {
                report.Add(
                    LegacyImportSeverity.Warning,
                    "scenario.repeated-tech",
                    path,
                    $"Country {country} is granted technology {technology} more than once.");
                continue;
            }

            result.Add(new CountryTechnologyContent
            {
                Country = countryKey,
                Technology = TechnologyKey((int)technology),
            });
        }

        return result.ToArray();
    }

    /// <summary>
    /// Converts <c>port</c> records into port sites. The record is a single
    /// linear cell index. Every one of the corpus's 124 ports names a land cell,
    /// and the 45 with no adjacent sea all carry a river, so the manual's "ports
    /// always require access to water" holds without exception and is enforced
    /// rather than merely reported.
    /// </summary>
    private static int[] ReadPorts(
        ScenarioDocument scenario,
        MapDocument map,
        LegacyImportReport report)
    {
        var result = new List<int>();
        var seen = new HashSet<uint>();
        foreach (var (record, index) in scenario.Records.Select(static (record, index) => (record, index)))
        {
            if (record.Tag != "port")
            {
                continue;
            }

            var path = $"scenario.records[{index}]";
            if (record.Fields.Count != 1)
            {
                report.Add(LegacyImportSeverity.Error, "scenario.invalid-port", path, "A port record must contain a single cell.");
                continue;
            }

            var cell = record.Fields[0];
            if (cell >= (uint)map.Cells.Count)
            {
                report.Add(LegacyImportSeverity.Error, "scenario.invalid-port-cell", path, $"Port refers to cell {cell} outside the map.");
                continue;
            }

            if (map.Cells[(int)cell].IsOcean)
            {
                report.Add(LegacyImportSeverity.Error, "scenario.port-on-ocean", path, $"Port refers to ocean cell {cell}.");
                continue;
            }

            // Repeats are collapsed rather than rejected: deve records taught
            // that the corpus repeats things, and a second port on one cell is
            // the same port either way.
            if (!seen.Add(cell))
            {
                report.Add(LegacyImportSeverity.Warning, "scenario.repeated-port", path, $"Cell {cell} carries more than one port record.");
                continue;
            }

            if (!TouchesWater(map, (int)cell))
            {
                report.Add(LegacyImportSeverity.Warning, "scenario.landlocked-port", path, $"Port cell {cell} touches neither sea nor a river.");
            }

            result.Add((int)cell);
        }

        return result.ToArray();
    }

    /// <summary>
    /// Converts <c>civi</c> records into starting civilians. The record is
    /// <c>[type, cell]</c> and names no owner.
    /// </summary>
    /// <remarks>
    /// The owner comes from the province the cell sits in, which the corpus
    /// supports without exception: all 210 records across the ten scenarios
    /// stand on owned land, and every one of those owners is a country holding
    /// a capital. Unowned land is therefore treated as an error rather than
    /// tolerated — nothing shipped does it, and a civilian nobody owns could
    /// never be given an order.
    /// <para>
    /// Stacking is allowed: <c>s1</c> gives one power two Miners, and nothing
    /// says a tile holds only one worker.
    /// </para>
    /// </remarks>
    private static CivilianContent[] ReadCivilians(
        ScenarioDocument scenario,
        MapDocument map,
        IReadOnlyDictionary<uint, string> countryKeys,
        LegacyImportReport report)
    {
        var result = new List<CivilianContent>();
        foreach (var (record, index) in scenario.Records.Select(static (record, index) => (record, index)))
        {
            if (record.Tag != "civi")
            {
                continue;
            }

            var path = $"scenario.records[{index}]";
            if (record.Fields.Count != 2)
            {
                report.Add(
                    LegacyImportSeverity.Error,
                    "scenario.invalid-civi",
                    path,
                    "A civi record must contain a type and a cell.");
                continue;
            }

            var type = record.Fields[0];
            if (type >= (uint)CivilianTypes.Count)
            {
                report.Add(
                    LegacyImportSeverity.Error,
                    "scenario.invalid-civi-type",
                    path,
                    $"Civilian refers to unknown type {type}.");
                continue;
            }

            var cell = record.Fields[1];
            if (cell >= (uint)map.Cells.Count)
            {
                report.Add(
                    LegacyImportSeverity.Error,
                    "scenario.invalid-civi-cell",
                    path,
                    $"Civilian refers to cell {cell} outside the map.");
                continue;
            }

            var source = map.Cells[(int)cell];
            if (source.IsOcean)
            {
                report.Add(
                    LegacyImportSeverity.Error,
                    "scenario.civi-on-ocean",
                    path,
                    $"Civilian refers to ocean cell {cell}.");
                continue;
            }

            if (source.NationZoneA == byte.MaxValue ||
                !countryKeys.TryGetValue(source.NationZoneA, out var countryKey))
            {
                report.Add(
                    LegacyImportSeverity.Error,
                    "scenario.civi-on-unowned-land",
                    path,
                    $"Civilian stands on cell {cell}, which no known country owns.");
                continue;
            }

            result.Add(new CivilianContent
            {
                Country = countryKey,
                Type = $"civilian.{CivilianTypes[(int)type].Name}",
                Cell = (int)cell,
            });
        }

        return result.ToArray();
    }

    /// <summary>
    /// Converts <c>labo</c> records into starting workforces. The record is
    /// <c>[country, untrained, trained, expert]</c>.
    /// </summary>
    /// <remarks>
    /// The grade order is settled by the data rather than assumed: <c>s1</c>
    /// gives country 2 <c>[60, 5, 0]</c>, which reads as a backward power with
    /// sixty untrained labourers and no experts. Reversed it would be a power
    /// with sixty experts and nobody to train, which no scenario would author.
    /// Every shipped scenario carries all seven records.
    /// </remarks>
    private static WorkforceContent[] ReadWorkforce(
        ScenarioDocument scenario,
        IReadOnlyDictionary<uint, string> countryKeys,
        LegacyImportReport report)
    {
        var result = new List<WorkforceContent>();
        var seen = new HashSet<uint>();
        foreach (var (record, index) in scenario.Records.Select(static (record, index) => (record, index)))
        {
            if (record.Tag != "labo")
            {
                continue;
            }

            var path = $"scenario.records[{index}]";
            if (record.Fields.Count != 4)
            {
                report.Add(LegacyImportSeverity.Error, "scenario.invalid-labo", path, "A labo record must contain a country and three worker counts.");
                continue;
            }

            var country = record.Fields[0];
            if (!countryKeys.TryGetValue(country, out var countryKey))
            {
                report.Add(LegacyImportSeverity.Error, "scenario.invalid-labo-country", path, $"Workforce refers to unknown country {country}.");
                continue;
            }

            if (!seen.Add(country))
            {
                report.Add(LegacyImportSeverity.Warning, "scenario.repeated-labo", path, $"Country {country} has more than one workforce record.");
                continue;
            }

            if (record.Fields.Skip(1).All(static value => value == 0))
            {
                continue;
            }

            result.Add(new WorkforceContent
            {
                Country = countryKey,
                Untrained = record.Fields[1],
                Trained = record.Fields[2],
                Expert = record.Fields[3],
            });
        }

        return result.ToArray();
    }

    /// <summary>
    /// Converts <c>rail</c> records into rail depots.
    /// </summary>
    /// <remarks>
    /// The tag is misleading: the map's own rail byte already carries the track,
    /// and these records are the depots built on it. The corpus says so twice
    /// over. They are a strict subset of railed cells — 76 of 310 in <c>s1</c>,
    /// 28 of 125 in <c>s3</c>, 25 of 81 in <c>s9</c> — and **no depot in any
    /// shipped scenario sits within two tiles of another**, which is exactly the
    /// spacing the manual recommends so that each tile is gathered once.
    /// </remarks>
    private static int[] ReadDepots(
        ScenarioDocument scenario,
        MapDocument map,
        LegacyImportReport report)
    {
        var result = new List<int>();
        var seen = new HashSet<uint>();
        foreach (var (record, index) in scenario.Records.Select(static (record, index) => (record, index)))
        {
            if (record.Tag != "rail")
            {
                continue;
            }

            var path = $"scenario.records[{index}]";
            if (record.Fields.Count != 1)
            {
                report.Add(LegacyImportSeverity.Error, "scenario.invalid-depot", path, "A rail depot record must contain a single cell.");
                continue;
            }

            var cell = record.Fields[0];
            if (cell >= (uint)map.Cells.Count)
            {
                report.Add(LegacyImportSeverity.Error, "scenario.invalid-depot-cell", path, $"Depot refers to cell {cell} outside the map.");
                continue;
            }

            if (map.Cells[(int)cell].IsOcean)
            {
                report.Add(LegacyImportSeverity.Error, "scenario.depot-on-ocean", path, $"Depot refers to ocean cell {cell}.");
                continue;
            }

            if (!seen.Add(cell))
            {
                report.Add(LegacyImportSeverity.Warning, "scenario.repeated-depot", path, $"Cell {cell} carries more than one depot record.");
                continue;
            }

            // Every corpus depot stands on track. Warn rather than reject: the
            // last two rules stated this confidently were both wrong.
            if (map.Cells[(int)cell].Rail == 0)
            {
                report.Add(LegacyImportSeverity.Warning, "scenario.depot-without-rail", path, $"Depot cell {cell} carries no rail.");
            }

            result.Add((int)cell);
        }

        return result.ToArray();
    }

    /// <summary>
    /// Whether a legacy cell has sea beside it or a river running through it.
    /// </summary>
    /// <remarks>
    /// Adjacency wraps east-west, because the 1997 grid does. That is not a
    /// detail: <c>s3</c> puts a port on the last column whose only water lies
    /// across the seam, and without the wrap it reads as landlocked. With the
    /// wrap, every one of the corpus's 124 ports touches water.
    /// </remarks>
    private static bool TouchesWater(MapDocument map, int cell)
    {
        if (map.Cells[cell].River != 0)
        {
            return true;
        }

        var width = map.Width;
        var height = map.Height;
        var x = cell % width;
        var y = cell / width;
        var odd = (y & 1) != 0;
        ReadOnlySpan<(int DeltaX, int DeltaY)> steps =
        [
            (odd ? 1 : 0, -1),
            (1, 0),
            (odd ? 1 : 0, 1),
            (odd ? 0 : -1, 1),
            (-1, 0),
            (odd ? 0 : -1, -1),
        ];

        foreach (var (deltaX, deltaY) in steps)
        {
            var neighborY = y + deltaY;
            if (neighborY < 0 || neighborY >= height)
            {
                continue;
            }

            var neighborX = ((x + deltaX) % width + width) % width;
            if (map.Cells[(neighborY * width) + neighborX].IsOcean)
            {
                return true;
            }
        }

        return false;
    }

    private static InitialInventoryContent[] ReadInitialInventory(
        ScenarioDocument scenario,
        IReadOnlyDictionary<uint, string> countryKeys,
        LegacyImportReport report)
    {
        var result = new List<InitialInventoryContent>();
        var seen = new HashSet<(uint Country, uint Commodity)>();
        foreach (var (record, index) in scenario.Records.Select(static (record, index) => (record, index)))
        {
            if (record.Tag != "ware")
            {
                continue;
            }

            var path = $"scenario.records[{index}]";
            if (record.Fields.Count != 3)
            {
                report.Add(LegacyImportSeverity.Error, "scenario.invalid-ware", path, "A ware record must contain country, commodity, and quantity values.");
                continue;
            }

            var country = record.Fields[0];
            var commodity = record.Fields[1];
            if (!countryKeys.TryGetValue(country, out var countryKey))
            {
                report.Add(LegacyImportSeverity.Error, "scenario.invalid-ware-country", path, $"Warehouse stock refers to unknown country {country}.");
                continue;
            }

            if (!WarehouseCommodityNames.TryGetValue(commodity, out var commodityName))
            {
                report.Add(LegacyImportSeverity.Warning, "scenario.unknown-ware-commodity", path, $"Warehouse stock uses unknown commodity code {commodity}; no stock was emitted.");
                continue;
            }

            if (!seen.Add((country, commodity)))
            {
                report.Add(LegacyImportSeverity.Error, "scenario.duplicate-ware", path, "Warehouse stock repeats a country and commodity pair.");
                continue;
            }

            var quantity = record.Fields[2];
            if (quantity == 0)
            {
                continue;
            }

            result.Add(new InitialInventoryContent
            {
                Country = countryKey,
                Commodity = $"commodity.{commodityName}",
                Quantity = quantity,
            });
        }

        return result.ToArray();
    }

    private static InitialProductionCapacityContent[] ReadProductionCapacities(
        ScenarioDocument scenario,
        IReadOnlyDictionary<uint, string> countryKeys,
        LegacyImportReport report)
    {
        var result = new List<InitialProductionCapacityContent>();
        var seen = new HashSet<(uint Country, uint Facility)>();
        foreach (var (record, index) in scenario.Records.Select(static (record, index) => (record, index)))
        {
            if (record.Tag != "capa")
            {
                continue;
            }

            var path = $"scenario.records[{index}]";
            if (record.Fields.Count != 3)
            {
                report.Add(LegacyImportSeverity.Error, "scenario.invalid-capa", path, "A capa record must contain country, industry, and capacity values.");
                continue;
            }

            var country = record.Fields[0];
            var facility = record.Fields[1];
            if (!countryKeys.TryGetValue(country, out var countryKey))
            {
                report.Add(LegacyImportSeverity.Error, "scenario.invalid-capa-country", path, $"Production capacity refers to unknown country {country}.");
                continue;
            }

            if (!CapacityFacilityNames.TryGetValue(facility, out var facilityName))
            {
                report.Add(LegacyImportSeverity.Warning, "scenario.unknown-capa-industry", path, $"Production capacity uses unknown industry code {facility}; no capacity was emitted.");
                continue;
            }

            if (!seen.Add((country, facility)))
            {
                report.Add(LegacyImportSeverity.Error, "scenario.duplicate-capa", path, "Production capacity repeats a country and facility pair.");
                continue;
            }

            var quantity = record.Fields[2];
            if (quantity == 0)
            {
                continue;
            }

            result.Add(new InitialProductionCapacityContent
            {
                Country = countryKey,
                Facility = $"facility.{facilityName}",
                Quantity = quantity,
            });
        }

        return result.ToArray();
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
        foreach (var group in map.Cells.GroupBy(static cell => cell.Terrain).Where(group => !Terrains.ContainsKey(group.Key)))
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

    private static string TerrainKey(byte code) => Terrains.TryGetValue(code, out var terrain)
        ? $"terrain.{terrain.Name}"
        : $"terrain.legacy-unknown-{code.ToString("D3", CultureInfo.InvariantCulture)}";

    /// <summary>
    /// An unknown terrain code is not improvable. Nothing is known about the
    /// ground, and guessing that a civilian may work it would silently invent a
    /// rule about a tile we cannot even name.
    /// </summary>
    private static TerrainContentDefinition TerrainDefinitionFor(byte code) =>
        Terrains.TryGetValue(code, out var terrain)
            ? new TerrainContentDefinition
            {
                Key = TerrainKey(code),
                Name = terrain.DisplayName,
                IsImprovable = terrain.IsImprovable,
                Prospecting = terrain.Prospecting switch
                {
                    LegacyProspecting.Open => new ProspectingContent(),
                    LegacyProspecting.NeedsOilDrilling => new ProspectingContent
                    {
                        RequiredTechnology = TechnologyKey(OilDrillingPosition),
                    },
                    _ => null,
                },
            }
            : new TerrainContentDefinition
            {
                Key = TerrainKey(code),
                Name = $"Unknown terrain {code.ToString(CultureInfo.InvariantCulture)}",
                IsImprovable = false,
            };

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

            // Gold and gems are the manual's only two, and it prices both.
            // Everything else reaches the warehouse.
            CashPerUnit = CashPerUnit.TryGetValue(key, out var rate) ? rate : null,
        };

    private static ProductionFacilityContentDefinition[] CreateStandardProductionFacilities() =>
    [
        Facility("textile-mill", "Textile Mill", ProductionCapacityMode.Limited, MillLadder),
        Facility("clothing-factory", "Clothing Factory", ProductionCapacityMode.Limited, FactoryLadder),
        Facility("steel-mill", "Steel Mill", ProductionCapacityMode.Limited, MillLadder),
        Facility("metal-works", "Metal Works", ProductionCapacityMode.Limited, FactoryLadder),
        Facility("lumber-mill", "Lumber Mill", ProductionCapacityMode.Limited, MillLadder),
        Facility("furniture-factory", "Furniture Factory", ProductionCapacityMode.Limited, FactoryLadder),
        Facility("oil-refinery", "Oil Refinery", ProductionCapacityMode.Limited, FactoryLadder),
        Facility("food-processing", "Food Processing", ProductionCapacityMode.Unlimited),
    ];

    /// <summary>
    /// "For mills, which start at capacity 2, the improvement levels are 4, 8,
    /// 16, 24 and then continue to increase by eight at a time."
    /// </summary>
    private static CapacityLadderContent MillLadder => new()
    {
        Rungs = [2, 4, 8, 16, 24],
        Increment = 8,
    };

    /// <summary>
    /// "For factories, which start at capacity 1, the improvement levels are 2,
    /// 4, 8, 12 and then continue to increase four at a time."
    /// </summary>
    private static CapacityLadderContent FactoryLadder => new()
    {
        Rungs = [1, 2, 4, 8, 12],
        Increment = 4,
    };

    /// <summary>
    /// The Capitol's terms: "the comforts of a developing economy: canned foods,
    /// clothing, and furniture", and a limit of "one-fourth of the number of
    /// provinces you own, rounded down".
    /// </summary>
    /// <remarks>
    /// **One of each per worker is a guess.** The manual names the three
    /// commodities and never says how much of any of them, so this is a real
    /// economic constant nobody has measured. See
    /// <c>docs/formulas/migration.md</c>; do not cite it as evidence.
    /// </remarks>
    private static MigrationContent CreateStandardMigration() => new()
    {
        CostPerWorker =
        [
            Quantity("canned-food", 1),
            Quantity("clothing", 1),
            Quantity("furniture", 1),
        ],
        ProvincesPerRecruit = 4,
    };

    /// <summary>
    /// "For each point of capacity built, you pay one lumber and one steel from
    /// your Warehouse." Expansion requires no labour.
    /// </summary>
    private static CommodityQuantityContent[] CreateStandardExpansionCost() =>
    [
        Quantity("lumber", 1),
        Quantity("steel", 1),
    ];

    /// <summary>
    /// The railyard: "as with other industrial expansion, increasing transport
    /// capacity requires both lumber and steel", so it takes the same rate the
    /// manual prices industrial capacity at.
    /// </summary>
    /// <remarks>
    /// The difference is labour. Expanding a mill needs none; the railyard needs
    /// "steel, lumber, and available labour". The manual never says how much, so
    /// the rate is the same total-input-units rule every recipe's labour cost
    /// follows — two inputs, two labour. See <c>docs/formulas/transport.md</c>.
    /// </remarks>
    private static TransportContentSettings CreateStandardTransport() => new()
    {
        CostPerCapacityPoint =
        [
            Quantity("lumber", 1),
            Quantity("steel", 1),
        ],
        LabourPerCapacityPoint = 2,
    };

    /// <summary>
    /// What a power's network carries before it builds anything.
    /// </summary>
    /// <remarks>
    /// <b>A guess, and the only one in the transport system.</b> A skirmish
    /// carries no <c>tran</c> record at all, so the corpus attests only that the
    /// engine supplies a value; the missions that do carry one are authored
    /// special cases this project has a standing rule against mining. Zero was
    /// the alternative and would leave every imported skirmish unable to move
    /// anything off its own land. Do not cite this number as evidence.
    /// </remarks>
    private const int DefaultTransportCapacity = 20;

    /// <summary>
    /// What a power's treasury holds on turn one.
    /// </summary>
    /// <remarks>
    /// <b>That there is a treasury at all is the manual's</b>: "each Great Power
    /// begins the game with a limited amount of cash which is totally inadequate
    /// to meet its needs." <b>The amount is a guess.</b>
    /// <para>
    /// Five of the ten shipped scenarios carry no <c>cash</c> record and five
    /// author 1,500 to 15,000 apiece — <c>s3</c> alone spans that whole range
    /// across its seven powers — so there is no constant in the corpus to find,
    /// and this project has a standing rule against mining authored missions for
    /// one. The number below is invented to sit in that spread rather than
    /// derived from it: enough to build a couple of structures and not a network.
    /// Do not cite it as evidence. See <c>docs/formulas/money.md</c>.
    /// </para>
    /// </remarks>
    private const int DefaultStartingCash = 5000;

    /// <summary>
    /// What a unit of gold and a unit of gems are worth when the network carries
    /// them. <b>The manual prices both outright</b>: "each unit of gold
    /// transported increases your cash by $200"; "transported gems convert to
    /// cash at $500 per unit."
    /// </summary>
    /// <remarks>
    /// Keyed by commodity name rather than by deposit code because the manual
    /// attaches the conversion to the transporting rather than to the mining.
    /// </remarks>
    private static readonly IReadOnlyDictionary<string, long> CashPerUnit =
        new Dictionary<string, long>(StringComparer.Ordinal)
        {
            ["gold"] = 200,
            ["gems"] = 500,
        };

    /// <summary>
    /// What a power finds in its warehouse on turn one.
    /// </summary>
    /// <remarks>
    /// <b>That there is a stockpile at all is the manual's, and so are the two
    /// commodities</b>: "you must construct a lumber and steel mill with your
    /// <em>initial stockpiles of lumber and steel</em>, or you may be forced to
    /// beg for lumber and steel from other Great Powers." A power starting with
    /// an empty warehouse could do neither.
    /// <para>
    /// <b>The quantity is a guess.</b> It matters more than it looks: a country
    /// with an empty warehouse and a small network cannot buy the railyard that
    /// would let it carry the materials to fill the warehouse, and the soak
    /// shows it never escapes. See <c>docs/formulas/transport.md</c>.
    /// </para>
    /// </remarks>
    private static CommodityQuantityContent[] CreateStandardStartingStock() =>
    [
        Quantity("lumber", 20),
        Quantity("steel", 20),
    ];

    private static ProductionRecipeContentDefinition[] CreateStandardProductionRecipes() =>
    [
        Recipe("fabric-from-cotton", "Fabric from Cotton", "textile-mill", [("cotton", 2)], [("fabric", 1)]),
        Recipe("fabric-from-wool", "Fabric from Wool", "textile-mill", [("wool", 2)], [("fabric", 1)]),
        Recipe("clothing-from-fabric", "Clothing", "clothing-factory", [("fabric", 2)], [("clothing", 1)]),
        Recipe("steel-from-coal-and-iron", "Steel", "steel-mill", [("coal", 1), ("iron", 1)], [("steel", 1)]),
        Recipe("hardware-from-steel", "Hardware", "metal-works", [("steel", 2)], [("hardware", 1)]),
        Recipe("armaments-from-steel", "Armaments", "metal-works", [("steel", 2)], [("armaments", 1)]),
        Recipe("lumber-from-timber", "Lumber", "lumber-mill", [("timber", 2)], [("lumber", 1)]),
        Recipe("paper-from-timber", "Paper", "lumber-mill", [("timber", 2)], [("paper", 1)]),
        Recipe("furniture-from-lumber", "Furniture", "furniture-factory", [("lumber", 2)], [("furniture", 1)]),
        Recipe("fuel-from-oil", "Fuel", "oil-refinery", [("oil", 2)], [("fuel", 1)]),
        Recipe("canned-food-from-fish", "Canned Food from Fish", "food-processing", [("grain", 2), ("fruit", 1), ("fish", 1)], [("canned-food", 2)]),
        Recipe("canned-food-from-livestock", "Canned Food from Livestock", "food-processing", [("grain", 2), ("fruit", 1), ("livestock", 1)], [("canned-food", 2)]),
    ];

    private static ProductionFacilityContentDefinition Facility(
        string key,
        string name,
        ProductionCapacityMode capacityMode,
        CapacityLadderContent? capacityLadder = null) => new()
        {
            Key = $"facility.{key}",
            Name = name,
            CapacityMode = capacityMode,
            CapacityLadder = capacityLadder,
        };

    /// <summary>
    /// Labour is not passed in because no original recipe needs it to be: the
    /// manual prices clothing at two fabric and two labour, and every recipe the
    /// original ships spends exactly two input units per unit of output, so the
    /// input total reproduces that rate throughout. See
    /// <c>docs/formulas/production.md</c>.
    /// </summary>
    private static ProductionRecipeContentDefinition Recipe(
        string key,
        string name,
        string facility,
        IEnumerable<(string Commodity, long Quantity)> inputs,
        IEnumerable<(string Commodity, long Quantity)> outputs)
    {
        var inputArray = inputs.ToArray();
        return new ProductionRecipeContentDefinition
        {
            Key = $"recipe.{key}",
            Name = name,
            Facility = $"facility.{facility}",
            CapacityCost = 1,
            LabourCost = inputArray.Sum(static item => item.Quantity),
            Inputs = inputArray.Select(static item => Quantity(item.Commodity, item.Quantity)).ToArray(),
            Outputs = outputs.Select(static item => Quantity(item.Commodity, item.Quantity)).ToArray(),
        };
    }

    private static CommodityQuantityContent Quantity(string commodity, long quantity) => new()
    {
        Commodity = $"commodity.{commodity}",
        Quantity = quantity,
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
