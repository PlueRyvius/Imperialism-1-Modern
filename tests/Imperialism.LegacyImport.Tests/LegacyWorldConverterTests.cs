using Imperialism.Content;
using Imperialism.Core;
using Imperialism.Formats;
using Imperialism.LegacyImport;
using Xunit;

namespace Imperialism.LegacyImport.Tests;

public sealed class LegacyWorldConverterTests
{
    public static TheoryData<byte, RiverEndpoint, RiverEndpoint> RiverCodes => new()
    {
        { 11, RiverEndpoint.NorthEast, RiverEndpoint.SouthEast },
        { 12, RiverEndpoint.NorthEast, RiverEndpoint.SouthWest },
        { 13, RiverEndpoint.NorthEast, RiverEndpoint.WestUpper },
        { 14, RiverEndpoint.NorthEast, RiverEndpoint.WestLower },
        { 15, RiverEndpoint.SouthWest, RiverEndpoint.EastUpper },
        { 16, RiverEndpoint.SouthWest, RiverEndpoint.EastLower },
        { 17, RiverEndpoint.EastUpper, RiverEndpoint.WestUpper },
        { 18, RiverEndpoint.EastLower, RiverEndpoint.WestUpper },
        { 19, RiverEndpoint.EastUpper, RiverEndpoint.WestLower },
        { 20, RiverEndpoint.EastLower, RiverEndpoint.WestLower },
        { 21, RiverEndpoint.EastUpper, RiverEndpoint.NorthWest },
        { 22, RiverEndpoint.EastLower, RiverEndpoint.NorthWest },
        { 23, RiverEndpoint.SouthEast, RiverEndpoint.WestUpper },
        { 24, RiverEndpoint.SouthEast, RiverEndpoint.WestLower },
        { 25, RiverEndpoint.SouthEast, RiverEndpoint.NorthWest },
        { 26, RiverEndpoint.SouthWest, RiverEndpoint.NorthWest },
        { 43, RiverEndpoint.NorthEast, RiverEndpoint.Source },
        { 44, RiverEndpoint.EastUpper, RiverEndpoint.Source },
        { 45, RiverEndpoint.EastLower, RiverEndpoint.Source },
        { 46, RiverEndpoint.SouthEast, RiverEndpoint.Source },
        { 47, RiverEndpoint.SouthWest, RiverEndpoint.Source },
        { 48, RiverEndpoint.WestUpper, RiverEndpoint.Source },
        { 49, RiverEndpoint.WestLower, RiverEndpoint.Source },
        { 50, RiverEndpoint.NorthWest, RiverEndpoint.Source },
        { 51, RiverEndpoint.NorthEast, RiverEndpoint.Mouth },
        { 52, RiverEndpoint.EastUpper, RiverEndpoint.Mouth },
        { 53, RiverEndpoint.EastLower, RiverEndpoint.Mouth },
        { 54, RiverEndpoint.SouthEast, RiverEndpoint.Mouth },
        { 55, RiverEndpoint.SouthWest, RiverEndpoint.Mouth },
        { 56, RiverEndpoint.WestUpper, RiverEndpoint.Mouth },
        { 57, RiverEndpoint.WestLower, RiverEndpoint.Mouth },
        { 58, RiverEndpoint.NorthWest, RiverEndpoint.Mouth },
    };

    [Theory]
    [MemberData(nameof(RiverCodes))]
    public void AllKnownRiverCodesMapToDocumentedShapes(
        byte code,
        RiverEndpoint first,
        RiverEndpoint second)
    {
        Assert.True(LegacyRiverCodes.TryDecode(code, out var path));
        Assert.Equal(new RiverPath(first, second), path);
    }

    [Fact]
    public void UnknownAndZeroRiverCodesAreNotInferred()
    {
        Assert.False(LegacyRiverCodes.TryDecode(0, out _));
        Assert.False(LegacyRiverCodes.TryDecode(42, out _));
        Assert.False(LegacyRiverCodes.TryDecode(255, out _));
        Assert.Equal(32, LegacyRiverCodes.KnownPaths.Count);
    }

    [Fact]
    public void EveryLegacyDepositMapsToItsSemanticRawCommodity()
    {
        var expected = new (byte Code, string Resource, string Commodity)[]
        {
            (0, "resource.cotton", "commodity.cotton"),
            (1, "resource.wool", "commodity.wool"),
            (2, "resource.forest", "commodity.timber"),
            (3, "resource.coal", "commodity.coal"),
            (4, "resource.iron", "commodity.iron"),
            (5, "resource.horses", "commodity.horses"),
            (6, "resource.oil", "commodity.oil"),
            (17, "resource.grain", "commodity.grain"),
            (18, "resource.fruit", "commodity.fruit"),
            (19, "resource.fish", "commodity.fish"),
            (20, "resource.cattle", "commodity.livestock"),
            (21, "resource.gems", "commodity.gems"),
            (22, "resource.gold", "commodity.gold"),
        };
        var cells = expected.Select(item => new HexCell
        {
            Terrain = 1,
            Province = 0,
            NationZoneA = 0,
            NationZoneB = 0,
            ResourceA = item.Code,
        }).ToArray();
        var scenario = new ScenarioDocument(
        [
            Record("year", 1815),
            NameRecord("cnam", 0, "Country"),
            NameRecord("pnam", 0, "Province"),
        ]);

        var result = LegacyWorldConverter.Convert(
            CreateMap(cells.Length, 1, cells),
            scenario,
            null,
            "resource-map");

        Assert.True(result.Success);
        var document = Assert.IsType<WorldContentDocument>(result.Document);
        var mappings = document.Resources.ToDictionary(static item => item.Key, static item => item.Commodity);
        foreach (var item in expected)
        {
            Assert.Equal(item.Commodity, mappings[item.Resource]);
        }
    }

    [Fact]
    public void ConverterImportsViewerSliceAndReportsDeferredInformation()
    {
        var map = CreateMap(
            2,
            2,
            new HexCell
            {
                Terrain = 1,
                Province = 10,
                NationZoneA = 2,
                NationZoneB = 2,
                TownType = 35,
                Rail = 2,
                ResourceA = 3,
            },
            new HexCell
            {
                Terrain = 8,
                Province = 10,
                NationZoneA = 2,
                NationZoneB = 2,
                TownType = 34,
                Rail = 16,
            },
            new HexCell
            {
                Terrain = 0,
                Province = ushort.MaxValue,
                NationZoneA = 11,
                NationZoneB = 11,
            },
            new HexCell
            {
                Terrain = 13,
                Province = 11,
                NationZoneA = 3,
                NationZoneB = 3,
                River = 49,
            });
        var scenario = new ScenarioDocument(
        [
            Record("year", 1815),
            Record("year", 1815),
            NameRecord("cnam", 2, "Blue"),
            NameRecord("cnam", 3, "Green"),
            NameRecord("pnam", 10, "West"),
            NameRecord("pnam", 11, "East"),
            NameRecord("zone", 7, "Northern Sea"),
            NameRecord("zone", 40, "Port City"),
            new ScenarioRecord("tech", [2u, 1u]),
            new ScenarioRecord("rail", [0u]),
        ]);
        var info = new ScenarioInfoDocument(
            "Fixture Campaign",
            "Overview",
            Enumerable.Repeat("Briefing", 7),
            Enumerable.Range(1, 8));

        var result = LegacyWorldConverter.Convert(map, scenario, info, "fixture");

        Assert.True(result.Success);
        var document = Assert.IsType<WorldContentDocument>(result.Document);
        Assert.Equal("Fixture Campaign", document.Map.Name);
        Assert.Equal("map.legacy.fixture", document.Map.Key);
        Assert.Equal(["country.legacy.002", "country.legacy.003"], document.Countries.Select(static item => item.Key));
        Assert.Equal(["province.legacy.00010", "province.legacy.00011"], document.Map.Provinces.Select(static item => item.Key));
        Assert.Single(document.Map.SeaZones);
        Assert.Equal("sea-zone.legacy.007", document.Map.SeaZones[0].Key);
        Assert.Equal("resource.coal", Assert.Single(document.Map.Cells[0].Resources));
        Assert.Equal(23, document.Commodities.Length);
        Assert.Equal(13, document.Commodities.Count(static item => item.Category == CommodityCategory.Raw));
        Assert.Equal(6, document.Commodities.Count(static item => item.Category == CommodityCategory.Material));
        Assert.Equal(4, document.Commodities.Count(static item => item.Category == CommodityCategory.Goods));
        Assert.Equal("commodity.coal", Assert.Single(document.Resources).Commodity);
        var compiled = WorldContentCompiler.Compile(document);
        Assert.Equal(
            new RiverPath(RiverEndpoint.WestLower, RiverEndpoint.Source),
            compiled.World.Map.Cells[3].River);
        Assert.Equal(
            new CommodityId(8),
            Assert.Single(compiled.World.Map.Resources).Commodity);
        Assert.Single(document.Scenarios[0].Rails);
        Assert.Equal(0, document.Scenarios[0].Rails[0].First);
        Assert.Equal(1, document.Scenarios[0].Rails[0].Second);
        Assert.Equal(0, document.Scenarios[0].Capitals[0].Cell);
        Assert.Equal(1, result.Report.DeferredCounts["scenario.unused-zone-records"]);

        // The rail record is a depot, not track, so it is converted rather than
        // deferred. The map's own rail byte is where the track comes from, and
        // it is still read into Rails above.
        Assert.DoesNotContain("scenario.tag.rail", result.Report.DeferredCounts.Keys);
        Assert.Equal([0], document.Scenarios[0].Depots);

        // tech is converted now too. Id 1 is the first row of the manual's
        // table, which every power holds anyway; granting it again is harmless
        // and is what the record says.
        Assert.DoesNotContain("scenario.tag.tech", result.Report.DeferredCounts.Keys);
        Assert.Equal(
            "technology.high-pressure-steam-engine",
            Assert.Single(document.Scenarios[0].CountryTechnologies).Technology);
        Assert.Equal(2, result.Report.DeferredCounts["map.trailer-records"]);
        Assert.Equal(7, result.Report.DeferredCounts["inf.country-briefings"]);
        Assert.Equal(8, result.Report.DeferredCounts["inf.metadata-values"]);
        Assert.DoesNotContain("Port City", result.Report.ToJson(), StringComparison.Ordinal);
        Assert.Equal(
            WorldContentCodec.Encode(document),
            WorldContentCodec.Encode(LegacyWorldConverter.Convert(map, scenario, info, "fixture").Document!));
    }

    [Fact]
    public void ConverterEmitsEvidenceBackedProductionCatalogAndScenarioEconomy()
    {
        var scenario = new ScenarioDocument(
        [
            Record("year", 1815),
            NameRecord("cnam", 0, "Country"),
            NameRecord("pnam", 0, "Province"),
            Record("ware", 0, 0, 9),
            Record("ware", 0, 11, 4),
            Record("ware", 0, 12, 0),
            Record("capa", 0, 0, 3),
            Record("capa", 0, 2, 7),
            Record("capa", 0, 6, 1),
        ]);

        var result = LegacyWorldConverter.Convert(
            CreateMap(1, 1, LandCell(0, 0)),
            scenario,
            null,
            "production");

        Assert.True(result.Success);
        var document = result.Document!;
        Assert.Equal(8, document.ProductionFacilities.Length);
        Assert.Equal(12, document.ProductionRecipes.Length);
        Assert.Equal(2, document.Scenarios[0].InitialInventory.Length);
        Assert.Equal(3, document.Scenarios[0].ProductionCapacities.Length);
        Assert.DoesNotContain("scenario.tag.ware", result.Report.DeferredCounts.Keys);
        Assert.DoesNotContain("scenario.tag.capa", result.Report.DeferredCounts.Keys);

        var compiled = WorldContentCompiler.Compile(document);
        Assert.Equal(9, compiled.World.Scenario.InitialInventory[0].Quantity);
        Assert.Equal(7, compiled.World.Scenario.InitialProductionCapacities[1].Quantity);
        Assert.Equal(
            ProductionCapacityMode.Unlimited,
            compiled.World.ProductionFacilities[7].CapacityMode);
        var food = compiled.World.ProductionRecipes[10];
        Assert.Equal(3, food.Inputs.Count);
        Assert.Equal(2, Assert.Single(food.Outputs).Quantity);
        Assert.Equal("recipe.canned-food-from-fish", compiled.Catalog.GetKey(food.Id));
    }

    [Fact]
    public void UnknownLegacyProductionCodesAreReportedWithoutInventingContent()
    {
        var scenario = new ScenarioDocument(
        [
            Record("year", 1815),
            NameRecord("cnam", 0, "Country"),
            NameRecord("pnam", 0, "Province"),
            Record("ware", 0, 99, 4),
            Record("capa", 0, 99, 4),
        ]);

        var result = LegacyWorldConverter.Convert(
            CreateMap(1, 1, LandCell(0, 0)),
            scenario,
            null,
            "unknown-production");

        Assert.True(result.Success);
        Assert.Empty(result.Document!.Scenarios[0].InitialInventory);
        Assert.Empty(result.Document.Scenarios[0].ProductionCapacities);
        Assert.Contains(result.Report.Diagnostics, static item => item.Code == "scenario.unknown-ware-commodity");
        Assert.Contains(result.Report.Diagnostics, static item => item.Code == "scenario.unknown-capa-industry");
    }

    [Fact]
    public void EveryStandardProductionRecipeMatchesDocumentedRatiosAndSharedFacilities()
    {
        var result = LegacyWorldConverter.Convert(
            CreateMap(1, 1, LandCell(0, 0)),
            new ScenarioDocument(
            [
                Record("year", 1815),
                NameRecord("cnam", 0, "Country"),
                NameRecord("pnam", 0, "Province"),
            ]),
            null,
            "recipes");
        var actual = result.Document!.ProductionRecipes.Select(static recipe => (
            recipe.Key,
            recipe.Facility,
            Inputs: string.Join(",", recipe.Inputs.Select(static item => $"{item.Commodity}:{item.Quantity}")),
            Outputs: string.Join(",", recipe.Outputs.Select(static item => $"{item.Commodity}:{item.Quantity}"))))
            .ToArray();

        var expected = new[]
        {
            ("recipe.fabric-from-cotton", "facility.textile-mill", "commodity.cotton:2", "commodity.fabric:1"),
            ("recipe.fabric-from-wool", "facility.textile-mill", "commodity.wool:2", "commodity.fabric:1"),
            ("recipe.clothing-from-fabric", "facility.clothing-factory", "commodity.fabric:2", "commodity.clothing:1"),
            ("recipe.steel-from-coal-and-iron", "facility.steel-mill", "commodity.coal:1,commodity.iron:1", "commodity.steel:1"),
            ("recipe.hardware-from-steel", "facility.metal-works", "commodity.steel:2", "commodity.hardware:1"),
            ("recipe.armaments-from-steel", "facility.metal-works", "commodity.steel:2", "commodity.armaments:1"),
            ("recipe.lumber-from-timber", "facility.lumber-mill", "commodity.timber:2", "commodity.lumber:1"),
            ("recipe.paper-from-timber", "facility.lumber-mill", "commodity.timber:2", "commodity.paper:1"),
            ("recipe.furniture-from-lumber", "facility.furniture-factory", "commodity.lumber:2", "commodity.furniture:1"),
            ("recipe.fuel-from-oil", "facility.oil-refinery", "commodity.oil:2", "commodity.fuel:1"),
            ("recipe.canned-food-from-fish", "facility.food-processing", "commodity.grain:2,commodity.fruit:1,commodity.fish:1", "commodity.canned-food:2"),
            ("recipe.canned-food-from-livestock", "facility.food-processing", "commodity.grain:2,commodity.fruit:1,commodity.livestock:1", "commodity.canned-food:2"),
        };

        Assert.Equal(expected, actual);
        Assert.All(result.Document.ProductionRecipes, static recipe => Assert.Equal(1, recipe.CapacityCost));

        // The manual's build ladders, and one lumber plus one steel per point.
        // Mills start at 2 and factories at 1, which is also exactly what the
        // skirmish scenarios give every power.
        var byKey = result.Document.ProductionFacilities.ToDictionary(
            static facility => facility.Key, StringComparer.Ordinal);
        Assert.Equal([2, 4, 8, 16, 24], byKey["facility.textile-mill"].CapacityLadder!.Rungs);
        Assert.Equal(8, byKey["facility.textile-mill"].CapacityLadder!.Increment);
        Assert.Equal([1, 2, 4, 8, 12], byKey["facility.clothing-factory"].CapacityLadder!.Rungs);
        Assert.Equal(4, byKey["facility.clothing-factory"].CapacityLadder!.Increment);

        // Food processing is uncapped, so it can never be built larger.
        Assert.Null(byKey["facility.food-processing"].CapacityLadder);

        Assert.Equal(
            [("commodity.lumber", 1L), ("commodity.steel", 1L)],
            result.Document.ExpansionCostPerCapacityPoint
                .Select(static item => (item.Commodity, item.Quantity)));

        // The manual prices one recipe outright — a unit of clothing costs two
        // fabric and two labour — and every recipe above spends exactly two
        // input units per unit of output, so that one quote fixes them all.
        // Canned food is the only one that differs, and only because its cycle
        // makes two units at once. See docs/formulas/production.md.
        Assert.Equal(
            2,
            result.Document.ProductionRecipes
                .Single(static recipe => recipe.Key == "recipe.clothing-from-fabric").LabourCost);
        Assert.All(result.Document.ProductionRecipes, static recipe => Assert.Equal(
            2 * recipe.Outputs.Sum(static item => item.Quantity),
            recipe.LabourCost));
        Assert.All(result.Document.ProductionRecipes, static recipe => Assert.Equal(
            recipe.Inputs.Sum(static item => item.Quantity),
            recipe.LabourCost));
    }

    [Fact]
    public void AsymmetricRailEndpointIsDroppedWithOneWarning()
    {
        var map = CreateMap(
            2,
            1,
            LandCell(1, 0) with { Rail = 2 },
            LandCell(2, 0));

        var result = LegacyWorldConverter.Convert(map, Scenario(1815), null, "rail-test");

        Assert.True(result.Success);
        Assert.Empty(result.Document!.Scenarios[0].Rails);
        Assert.Equal(
            1,
            result.Report.Diagnostics
                .Where(static item => item.Code == "map.asymmetric-rail-endpoint")
                .Sum(static item => item.Count));
    }

    [Fact]
    public void UnknownCodesWarnWithoutInventingFeatures()
    {
        var map = CreateMap(
            1,
            1,
            LandCell(1, 0) with
            {
                Terrain = 99,
                ResourceA = 99,
                TownType = 33,
                River = 99,
            });

        var result = LegacyWorldConverter.Convert(map, Scenario(1815), null, "unknowns");

        Assert.True(result.Success);
        var cell = result.Document!.Map.Cells[0];
        Assert.Equal("terrain.legacy-unknown-099", cell.Terrain);
        Assert.Empty(cell.Resources);
        Assert.False(cell.HasSettlementSite);
        Assert.Null(cell.River);
        Assert.Contains(result.Report.Diagnostics, static item => item.Code == "map.unknown-terrain-code");
        Assert.Contains(result.Report.Diagnostics, static item => item.Code == "map.unknown-resource-code");
        Assert.Contains(result.Report.Diagnostics, static item => item.Code == "map.unknown-town-code");
        Assert.Contains(result.Report.Diagnostics, static item => item.Code == "map.unknown-river-code");
    }

    [Fact]
    public void MissingNamesUseDeterministicFallbacks()
    {
        var result = LegacyWorldConverter.Convert(
            CreateMap(1, 1, LandCell(12, 4)),
            Scenario(1815),
            null,
            "fallbacks");

        Assert.True(result.Success);
        Assert.Equal("Legacy Country 4", result.Document!.Countries[0].Name);
        Assert.Equal("Legacy Province 12", result.Document.Map.Provinces[0].Name);
        Assert.Equal(2, result.Report.Diagnostics.Count(static item => item.Code == "scenario.missing-name"));
    }

    [Fact]
    public void ConflictingProvinceOwnersBlockOutput()
    {
        var result = LegacyWorldConverter.Convert(
            CreateMap(2, 1, LandCell(1, 0), LandCell(1, 1)),
            Scenario(1815),
            null,
            "owners");

        Assert.False(result.Success);
        Assert.Null(result.Document);
        Assert.Contains(result.Report.Diagnostics, static item => item.Code == "map.conflicting-province-owner");
    }

    [Fact]
    public void IdenticalDuplicateYearsAreAcceptedButConflictingYearsBlockOutput()
    {
        var map = CreateMap(1, 1, LandCell(1, 0));
        var identical = new ScenarioDocument([Record("year", 1815), Record("year", 1815)]);
        var conflicting = new ScenarioDocument([Record("year", 1815), Record("year", 1882)]);

        Assert.True(LegacyWorldConverter.Convert(map, identical, null, "same-year").Success);
        var result = LegacyWorldConverter.Convert(map, conflicting, null, "different-year");
        Assert.False(result.Success);
        Assert.Contains(result.Report.Diagnostics, static item => item.Code == "scenario.conflicting-year");
    }

    [Fact]
    public void DuplicateCapitalsAndInvalidPackageKeysBlockOutput()
    {
        var map = CreateMap(
            2,
            1,
            LandCell(1, 0) with { TownType = 35 },
            LandCell(2, 0) with { TownType = 35 });

        var duplicate = LegacyWorldConverter.Convert(map, Scenario(1815), null, "capitals");
        Assert.False(duplicate.Success);
        Assert.Contains(duplicate.Report.Diagnostics, static item => item.Code == "map.duplicate-capital");

        var invalidKey = LegacyWorldConverter.Convert(map, Scenario(1815), null, "Invalid Key");
        Assert.False(invalidKey.Success);
        Assert.Contains(invalidKey.Report.Diagnostics, static item => item.Code == "package.invalid-key");
    }

    [Fact]
    public void DeveRecordsBecomeStartingDevelopmentLevels()
    {
        var map = CreateMap(3, 1, LandCell(1, 0), LandCell(1, 0), OceanCell());
        var scenario = new ScenarioDocument(
        [
            Record("year", 1815),
            Record("deve", 0, 2),
            Record("deve", 1, 3),
        ]);

        var result = LegacyWorldConverter.Convert(map, scenario, null, "development");

        Assert.True(result.Success);
        var development = result.Document!.Scenarios[0].CellDevelopment;
        Assert.Equal([(0, 2), (1, 3)], development.Select(static item => (item.Cell, item.Level)));

        // The tag is converted now, so it must no longer be counted as deferred.
        Assert.DoesNotContain("scenario.tag.deve", result.Report.DeferredCounts.Keys);
    }

    [Fact]
    public void ARepeatedDeveCellKeepsTheHighestLevelAndSaysSo()
    {
        // s1 ships three cells developed twice, so this is legal data rather
        // than corruption and must not fail the import.
        var map = CreateMap(2, 1, LandCell(1, 0), LandCell(1, 0));
        var scenario = new ScenarioDocument(
        [
            Record("year", 1815),
            Record("deve", 0, 2),
            Record("deve", 0, 1),
        ]);

        var result = LegacyWorldConverter.Convert(map, scenario, null, "repeated");

        Assert.True(result.Success);
        var entry = Assert.Single(result.Document!.Scenarios[0].CellDevelopment);
        Assert.Equal(0, entry.Cell);
        Assert.Equal(2, entry.Level);
        Assert.Contains(result.Report.Diagnostics, static item => item.Code == "scenario.repeated-deve");
    }

    [Fact]
    public void DevelopmentOffTheMapOrOffTheCorpusLevelRangeIsRejected()
    {
        var map = CreateMap(2, 1, LandCell(1, 0), OceanCell());

        var offMap = LegacyWorldConverter.Convert(
            map,
            new ScenarioDocument([Record("year", 1815), Record("deve", 9, 1)]),
            null,
            "off-map");
        Assert.False(offMap.Success);
        Assert.Contains(offMap.Report.Diagnostics, static item => item.Code == "scenario.invalid-deve-cell");

        var onOcean = LegacyWorldConverter.Convert(
            map,
            new ScenarioDocument([Record("year", 1815), Record("deve", 1, 1)]),
            null,
            "on-ocean");
        Assert.False(onOcean.Success);
        Assert.Contains(onOcean.Report.Diagnostics, static item => item.Code == "scenario.deve-on-ocean");

        // Level 4 appears nowhere in the corpus; a value the original never
        // writes means the reading is wrong, so it is reported, not clamped.
        var tooHigh = LegacyWorldConverter.Convert(
            map,
            new ScenarioDocument([Record("year", 1815), Record("deve", 0, 4)]),
            null,
            "too-high");
        Assert.True(tooHigh.Success);
        Assert.Empty(tooHigh.Document!.Scenarios[0].CellDevelopment);
        Assert.Contains(
            tooHigh.Report.Diagnostics,
            static item => item.Code == "scenario.unexpected-deve-level");
    }

    [Fact]
    public void EachDepositTakesItsCurveFromTheManualsDevelopmentTable()
    {
        var map = CreateMap(
            4,
            1,
            LandCell(1, 0) with { ResourceA = 3 },
            LandCell(1, 0) with { ResourceA = 17 },
            LandCell(1, 0) with { ResourceA = 22 },
            LandCell(1, 0) with { ResourceA = 5 });

        var result = LegacyWorldConverter.Convert(map, Scenario(1815), null, "yields");

        Assert.True(result.Success);
        var byKey = result.Document!.Resources.ToDictionary(
            static item => item.Key,
            static item => item.YieldByDevelopmentLevel);

        // Coal and iron run 0/2/4/6; gold and gems 0/1/2/3; cultivated ground
        // 1/2/3/4; horses and fish have no improvement at all.
        Assert.Equal([0, 2, 4, 6], byKey["resource.coal"]);
        Assert.Equal([1, 2, 3, 4], byKey["resource.grain"]);
        Assert.Equal([0, 1, 2, 3], byKey["resource.gold"]);
        Assert.Equal([1], byKey["resource.horses"]);

        // RequiredTechnology gates *extraction* from an open deposit, which the
        // manual never does. Technology gates the improvement level instead —
        // a different hook, checked separately below.
        Assert.All(result.Document.Resources, static item => Assert.Null(item.RequiredTechnology));

        // The manual's whole table, in printed order, because a tech record is a
        // bare 1-based index into it.
        Assert.Equal(28, result.Document.Technologies.Length);
        Assert.Equal("technology.high-pressure-steam-engine", result.Document.Technologies[0].Key);
        Assert.Equal("technology.seed-drill", result.Document.Technologies[1].Key);
        Assert.Equal("technology.oil-drilling", result.Document.Technologies[18].Key);
        Assert.Equal("technology.internal-combustion", result.Document.Technologies[27].Key);
    }

    /// <summary>
    /// The Benefits of Technology Table read as a ladder. Index 0 is the level a
    /// tile starts at and is always ungated; a mine opening at Level I is the
    /// manual's one other ungated rung.
    /// </summary>
    [Fact]
    public void EachDepositNamesTheTechnologyEachRungCosts()
    {
        var expected = new (byte Code, string Resource, string?[] Ladder)[]
        {
            (17, "resource.grain",
                [null, "technology.seed-drill", "technology.steel-and-iron-plows",
                    "technology.mechanical-reaper"]),
            (0, "resource.cotton",
                [null, "technology.cotton-gin", "technology.spinning-jenny",
                    "technology.power-loom"]),
            (2, "resource.forest",
                [null, "technology.iron-railroad-bridge", "technology.compound-steam-engine",
                    "technology.dynamite"]),
            (3, "resource.coal",
                [null, null, "technology.square-set-timbering", "technology.dynamite"]),
            (6, "resource.oil",
                [null, "technology.oil-drilling", "technology.chemistry",
                    "technology.internal-combustion"]),

            // No civilian improves either, so neither has a ladder at all.
            (5, "resource.horses", null!),
            (19, "resource.fish", null!),
        };
        var cells = expected.Select(item => new HexCell
        {
            Terrain = 1,
            Province = 0,
            NationZoneA = 0,
            NationZoneB = 0,
            ResourceA = item.Code,
        }).ToArray();
        var scenario = new ScenarioDocument(
        [
            Record("year", 1815),
            NameRecord("cnam", 0, "Country"),
            NameRecord("pnam", 0, "Province"),
        ]);

        var result = LegacyWorldConverter.Convert(
            CreateMap(cells.Length, 1, cells), scenario, null, "ladder-map");

        Assert.True(result.Success);
        var byKey = result.Document!.Resources.ToDictionary(
            static item => item.Key,
            static item => item.TechnologyByDevelopmentLevel);
        foreach (var item in expected)
        {
            Assert.Equal(item.Ladder, byKey[item.Resource]);
        }

        // Every gate named must exist in the catalog, or a deposit would refer
        // to knowledge nothing declares.
        var declared = result.Document.Technologies.Select(static item => item.Key).ToHashSet();
        Assert.All(
            expected.Where(static item => item.Ladder is not null)
                .SelectMany(static item => item.Ladder)
                .Where(static key => key is not null),
            key => Assert.Contains(key!, declared));
    }

    /// <summary>
    /// "Every player always starts with the first two technologies listed below:
    /// High Pressure Steam Engine and Seed Drill." No record states it, which is
    /// why it arrives as a default rather than as scenario content.
    /// </summary>
    [Fact]
    public void EveryPowerStartsWithTheManualsFirstTwoTechnologies()
    {
        var scenario = new ScenarioDocument(
        [
            Record("year", 1815),
            NameRecord("cnam", 0, "Country"),
            NameRecord("pnam", 0, "Province"),
            Record("labo", 0, 4, 2, 1),
        ]);

        var result = LegacyWorldConverter.Convert(
            CreateMap(2, 1, LandCell(0, 0), OceanCell()), scenario, null, "defaults-map");

        Assert.True(result.Success, result.Report.ToHumanReadable());
        Assert.Equal(
            ["technology.high-pressure-steam-engine", "technology.seed-drill"],
            result.Document!.StartingDefaults!.Technologies);

        // labo is the one record naming the Great Powers and only them, so it
        // is how the importer tells them from minor nations without guessing.
        Assert.Equal(
            [result.Document.Countries[0].Key],
            result.Document.Scenarios[0].DefaultStartCountries);
    }

    /// <summary>
    /// <c>tran</c> is <c>[country, capacity]</c> — one number for the whole
    /// network, matching the manual's single shared capacity bar.
    /// </summary>
    [Fact]
    public void TranRecordsBecomeStartingTransportCapacity()
    {
        var scenario = new ScenarioDocument(
        [
            Record("year", 1815),
            NameRecord("cnam", 0, "Country"),
            NameRecord("pnam", 0, "Province"),
            Record("tran", 0, 15),
        ]);

        var result = LegacyWorldConverter.Convert(
            CreateMap(2, 1, LandCell(0, 0), OceanCell()), scenario, null, "tran-map");

        Assert.True(result.Success, result.Report.ToHumanReadable());
        var capacity = Assert.Single(result.Document!.Scenarios[0].TransportCapacity);
        Assert.Equal(15, capacity.Capacity);
        Assert.DoesNotContain("scenario.tag.tran", result.Report.DeferredCounts.Keys);

        // The railyard's price, and the one build that also wants labour.
        Assert.Equal(
            ["commodity.lumber", "commodity.steel"],
            result.Document.Transport!.CostPerCapacityPoint.Select(static item => item.Commodity));
        Assert.Equal(2, result.Document.Transport.LabourPerCapacityPoint);
    }

    /// <summary>
    /// A scenario that carries no <c>tran</c> leaves its powers on the engine's
    /// default, which is a guess — see <c>docs/formulas/transport.md</c>. It is
    /// pinned here so that changing it is a deliberate act.
    /// </summary>
    [Fact]
    public void AScenarioWithNoTranLeavesEveryPowerOnTheDefault()
    {
        var scenario = new ScenarioDocument(
        [
            Record("year", 1815),
            NameRecord("cnam", 0, "Country"),
            NameRecord("pnam", 0, "Province"),
            Record("labo", 0, 4, 2, 1),
        ]);

        var result = LegacyWorldConverter.Convert(
            CreateMap(2, 1, LandCell(0, 0), OceanCell()), scenario, null, "no-tran-map");

        Assert.True(result.Success, result.Report.ToHumanReadable());
        Assert.Empty(result.Document!.Scenarios[0].TransportCapacity);
        Assert.Equal(20, result.Document.StartingDefaults!.TransportCapacity);

        // "You must construct a lumber and steel mill with your initial
        // stockpiles of lumber and steel." The commodities are the manual's;
        // the quantity is a guess.
        Assert.Equal(
            ["commodity.lumber", "commodity.steel"],
            result.Document.StartingDefaults.Inventory.Select(static item => item.Commodity));
    }

    /// <summary>
    /// A power's own <c>ware</c> records beat the default stockpile, the same way
    /// <c>labo</c> beats the default workforce.
    /// </summary>
    [Fact]
    public void AWareRecordBeatsTheDefaultStockpile()
    {
        var scenario = new ScenarioDocument(
        [
            Record("year", 1815),
            NameRecord("cnam", 0, "Country"),
            NameRecord("pnam", 0, "Province"),
            Record("labo", 0, 4, 2, 1),

            // Warehouse commodity 9 is lumber; 8 is fabric.
            Record("ware", 0, 9, 3),
        ]);

        var result = LegacyWorldConverter.Convert(
            CreateMap(2, 1, LandCell(0, 0), OceanCell()), scenario, null, "ware-map");

        Assert.True(result.Success, result.Report.ToHumanReadable());
        var compiled = WorldContentCompiler.Compile(result.Document!);
        var state = new WorldState(compiled.World);
        var lumber = compiled.Catalog.GetCommodityId("commodity.lumber");

        Assert.Equal(3, state.GetAvailableQuantity(new CountryId(0), lumber));
    }

    /// <summary>
    /// <c>tech</c> is <c>[country, id]</c>, the id a 1-based index into the
    /// manual's table. See <c>docs/formulas/technology.md</c> for the corpus
    /// check behind that reading.
    /// </summary>
    [Fact]
    public void TechRecordsBecomeStartingKnowledge()
    {
        var scenario = new ScenarioDocument(
        [
            Record("year", 1815),
            NameRecord("cnam", 0, "Country"),
            NameRecord("pnam", 0, "Province"),
            Record("tech", 0, 5),
            Record("tech", 0, 23),
            Record("tech", 0, 99),
        ]);

        var result = LegacyWorldConverter.Convert(
            CreateMap(2, 1, LandCell(0, 0), OceanCell()), scenario, null, "tech-map");

        Assert.True(result.Success, result.Report.ToHumanReadable());
        Assert.Equal(
            ["technology.square-set-timbering", "technology.dynamite"],
            result.Document!.Scenarios[0].CountryTechnologies
                .Select(static item => item.Technology));

        // An id past the end of the table is reported and dropped rather than
        // inventing a technology nobody can name.
        Assert.Contains(result.Report.Diagnostics, item => item.Code == "scenario.unknown-tech-id");
        Assert.DoesNotContain("scenario.tag.tech", result.Report.DeferredCounts.Keys);
    }

    /// <summary>
    /// Converts the real corpus. Every rule in this file was written against
    /// these files, and two of them were wrong until these files said so — the
    /// repeated <c>deve</c> cell and the seam-crossing port in <c>s3</c>. A
    /// synthetic fixture would have agreed with both mistakes.
    /// </summary>
    [Fact]
    public void TheWholeShippedCorpusConvertsWhenItIsConfigured()
    {
        var directory = Environment.GetEnvironmentVariable("IMPERIALISM_SCENARIO_DIR");
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        var expectedPorts = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["s1"] = 49,
            ["s3"] = 21,
            ["s9"] = 13,
            ["s12"] = 13,
            ["s13"] = 10,
            ["s14"] = 10,
        };

        // Depots are a strict subset of railed cells: s1 has 310 railed cells
        // and 76 depots. The generated tutorial worlds ship none at all, which
        // is why zero is an expected value rather than a broken import.
        var expectedDepots = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["s1"] = 76,
            ["s3"] = 28,
            ["s9"] = 25,
            ["s12"] = 24,
            ["s13"] = 2,
            ["s14"] = 2,
            ["s10"] = 0,
            ["s11"] = 0,
            ["s15"] = 0,
        };

        // Every civi record in the corpus, counted before any of this was
        // built. 210 across the ten files, all on owned land.
        var expectedCivilians = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["s1"] = 35,
            ["s3"] = 32,
            ["s5"] = 7,
            ["s9"] = 33,
            ["s10"] = 13,
            ["s11"] = 14,
            ["s12"] = 28,
            ["s13"] = 17,
            ["s14"] = 17,
            ["s15"] = 14,
        };
        // Ground a Prospector may search, per scenario: barren hills and
        // mountains, then the swamp/desert/tundra behind Oil Drilling. This is
        // the number the original's toolbar counts down.
        //
        // s1, s3, s13 and s14 agree exactly because they share the historical
        // world map. The ten sum to 4,449 open tiles, which is the 2,860 barren
        // hills plus 1,589 mountains already counted in
        // docs/formulas/development.md — arrived at from the other direction.
        var expectedProspectable = new Dictionary<string, (int Open, int Gated)>(StringComparer.Ordinal)
        {
            ["s1"] = (598, 414),
            ["s3"] = (598, 414),
            ["s13"] = (598, 414),
            ["s14"] = (598, 414),
            ["s9"] = (371, 428),
            ["s12"] = (371, 428),
            ["s10"] = (382, 425),
            ["s11"] = (364, 448),
            ["s15"] = (368, 452),
            ["s5"] = (201, 517),
        };
        // Technologies actually granted, after repeats are dropped. s1 gives all
        // seven powers the same 21; s3 is the discriminating one, giving them 9,
        // 13 and 14 apiece — which is what makes it the strongest evidence that
        // the manual's table order is the right reading of the ids, since a
        // wrong ordering would fire at once on the power holding only nine.
        //
        // s3 also ships 98 records for 92 grants: six of them repeat a pair it
        // has already granted, exactly as s1 develops three cells twice. Legal
        // authoring, warned about and dropped.
        var expectedTechnologies = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["s1"] = 147,
            ["s3"] = 92,
            ["s5"] = 42,
            ["s9"] = 63,
            ["s12"] = 63,
            ["s13"] = 42,
            ["s14"] = 42,
            ["s10"] = 0,
            ["s11"] = 0,
            ["s15"] = 0,
        };
        // tran records per scenario. Four carry none at all and run on the
        // engine's default — the guess in docs/formulas/transport.md — and s12
        // gives a network to exactly one of its seven powers, which is as clear
        // a demonstration as the corpus offers that these are authored
        // situations rather than a design to be mined.
        var expectedTransport = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["s1"] = 7,
            ["s3"] = 7,
            ["s5"] = 7,
            ["s13"] = 7,
            ["s14"] = 7,
            ["s12"] = 1,
            ["s9"] = 0,
            ["s10"] = 0,
            ["s11"] = 0,
            ["s15"] = 0,
        };
        var converted = 0;
        var totalCivilians = 0;
        var totalOpenProspectable = 0;

        foreach (var mapPath in Directory.GetFiles(directory, "*.map").OrderBy(static path => path))
        {
            var key = Path.GetFileNameWithoutExtension(mapPath);
            var scenarioPath = Path.Combine(directory, $"{key}.scn");

            // s0 is the working scenario, edited and relaunched, so it is never
            // reference data.
            if (key == "s0" || !File.Exists(scenarioPath))
            {
                continue;
            }

            var result = LegacyWorldConverter.Convert(
                LegacyMapCodec.Decode(File.ReadAllBytes(mapPath), MapFormatProfile.Imperialism1),
                LegacyScenarioCodec.Decode(File.ReadAllBytes(scenarioPath)),
                null,
                $"corpus-{key}");

            Assert.True(
                result.Success,
                $"{key} failed to convert: {string.Join("; ", result.Report.Diagnostics)}");

            // A port record that reads as landlocked means our adjacency is
            // wrong, not that the shipped map is.
            Assert.DoesNotContain(
                result.Report.Diagnostics,
                item => item.Code == "scenario.landlocked-port");

            if (expectedPorts.TryGetValue(key, out var ports))
            {
                Assert.Equal(ports, result.Document!.Scenarios[0].Ports.Length);
            }

            // Every scenario carries all seven workforces, and s1's spread is
            // what settles the grade order: country 2 is 60 untrained with no
            // experts, which only reads sensibly one way round.
            var workers = result.Document!.Scenarios[0].Workers;
            Assert.Equal(7, workers.Length);
            if (key == "s1")
            {
                var first = workers.Single(item => item.Country.EndsWith("000", StringComparison.Ordinal));
                Assert.Equal((15, 15, 30), (first.Untrained, first.Trained, first.Expert));
                var third = workers.Single(item => item.Country.EndsWith("002", StringComparison.Ordinal));
                Assert.Equal((60, 5, 0), (third.Untrained, third.Trained, third.Expert));
            }

            if (expectedDepots.TryGetValue(key, out var depots))
            {
                Assert.Equal(depots, result.Document!.Scenarios[0].Depots.Length);

                // Every depot in an original stands on track. s5 is our own
                // generated output and is not part of the corpus.
                Assert.DoesNotContain(
                    result.Report.Diagnostics,
                    item => item.Code == "scenario.depot-without-rail");
            }

            // civi carries no owner, so every one of these is a province
            // lookup that could have come back empty. None does.
            var civilians = result.Document!.Scenarios[0].Civilians;
            Assert.DoesNotContain(
                result.Report.Diagnostics,
                item => item.Code.StartsWith("scenario.civi", StringComparison.Ordinal) ||
                    item.Code == "scenario.invalid-civi");
            Assert.DoesNotContain("scenario.tag.civi", result.Report.DeferredCounts.Keys);
            if (expectedCivilians.TryGetValue(key, out var expected))
            {
                Assert.Equal(expected, civilians.Length);
            }

            // The manual's whole table, and whatever this scenario grants on top
            // of the two every power starts with.
            Assert.Equal(28, result.Document.Technologies.Length);
            Assert.DoesNotContain("scenario.tag.tech", result.Report.DeferredCounts.Keys);
            Assert.DoesNotContain(
                result.Report.Diagnostics,
                item => item.Code.StartsWith("scenario.invalid-tech", StringComparison.Ordinal) ||
                    item.Code == "scenario.unknown-tech-id");
            if (expectedTechnologies.TryGetValue(key, out var granted))
            {
                Assert.Equal(granted, result.Document.Scenarios[0].CountryTechnologies.Length);
            }

            var open = TerrainKeysWhere(result.Document, static item => item is { RequiredTechnology: null });
            var gated = TerrainKeysWhere(result.Document, static item => item is { RequiredTechnology: not null });
            var openCells = result.Document.Map.Cells.Count(cell => open.Contains(cell.Terrain));
            if (expectedProspectable.TryGetValue(key, out var prospectable))
            {
                Assert.Equal(
                    prospectable,
                    (openCells, result.Document.Map.Cells.Count(cell => gated.Contains(cell.Terrain))));
            }

            // tran is converted now too. A scenario carrying none leaves its
            // powers on the engine's guessed default.
            Assert.DoesNotContain("scenario.tag.tran", result.Report.DeferredCounts.Keys);
            Assert.DoesNotContain(
                result.Report.Diagnostics,
                item => item.Code.StartsWith("scenario.invalid-tran", StringComparison.Ordinal));
            if (expectedTransport.TryGetValue(key, out var networks))
            {
                Assert.Equal(networks, result.Document.Scenarios[0].TransportCapacity.Length);
            }

            totalOpenProspectable += openCells;
            totalCivilians += civilians.Length;
            converted++;
        }

        Assert.True(converted >= 9, $"Expected the full corpus, converted only {converted}.");
        Assert.Equal(210, totalCivilians);

        // 2,860 barren hills and 1,589 mountains, counted independently of the
        // terrain census in docs/formulas/development.md and agreeing with it.
        Assert.Equal(4449, totalOpenProspectable);
    }

    private static HashSet<string> TerrainKeysWhere(
        WorldContentDocument document,
        Func<ProspectingContent?, bool> predicate) => document.Terrains
        .Where(item => predicate(item.Prospecting))
        .Select(static item => item.Key)
        .ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// **The check the technology ladder rests on.** A <c>tech</c> record is a
    /// bare number and nothing names it; reading it as a position in the
    /// manual's Benefits of Technology Table is an inference, and this is what
    /// tests it. Every level a scenario authors is compared against what the
    /// owning power's technologies would permit a civilian to build.
    /// </summary>
    /// <remarks>
    /// A wrong ordering would misfire everywhere. It does not: across the four
    /// originals carrying both records, 379 authored levels are permitted and 4
    /// are not — all four the same deposit, timber at Level III, in one country
    /// of <c>s1</c>. <c>s3</c> is the decisive case because its powers hold
    /// **unequal** sets of 9, 13 and 14, so a shifted table would fire at once
    /// on the power holding only nine. It fires not at all.
    /// <para>
    /// The four exceptions are not failures. <b>The gate governs a civilian
    /// raising a level and never a scenario authoring one</b>, exactly as the
    /// capacity ladder governs building and not storing, so authoring past the
    /// ladder is legal input. They are counted rather than tolerated silently,
    /// because the count moving is how a mistaken transcription would announce
    /// itself. See <c>docs/formulas/technology.md</c>.
    /// </para>
    /// <para>
    /// <c>s5</c> is excluded: it is a generated world holding six technologies
    /// with Level III tiles all over it, and it authors 74 levels no power in it
    /// could build. That is a demonstration of the rule rather than a breach of
    /// it, and averaging it in would drown the signal.
    /// </para>
    /// </remarks>
    [Fact]
    public void EveryAuthoredLevelInTheCorpusIsOneItsOwnerCouldHaveBuilt()
    {
        var directory = Environment.GetEnvironmentVariable("IMPERIALISM_SCENARIO_DIR");
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        var permitted = 0;
        var beyond = new List<string>();
        var checkedScenarios = 0;

        foreach (var mapPath in Directory.GetFiles(directory, "*.map").OrderBy(static path => path))
        {
            var key = Path.GetFileNameWithoutExtension(mapPath);
            var scenarioPath = Path.Combine(directory, $"{key}.scn");

            // s0 is the working scenario and s5 is generated, not shipped.
            if (key is "s0" or "s5" || !File.Exists(scenarioPath))
            {
                continue;
            }

            var result = LegacyWorldConverter.Convert(
                LegacyMapCodec.Decode(File.ReadAllBytes(mapPath), MapFormatProfile.Imperialism1),
                LegacyScenarioCodec.Decode(File.ReadAllBytes(scenarioPath)),
                null,
                $"ladder-{key}");
            Assert.True(result.Success);

            var document = result.Document!;
            if (document.Scenarios[0].CellDevelopment.Length == 0)
            {
                continue;
            }

            checkedScenarios++;
            var owners = document.Scenarios[0].ProvinceOwners
                .ToDictionary(static item => item.Province, static item => item.Country, StringComparer.Ordinal);
            var held = document.Scenarios[0].CountryTechnologies
                .GroupBy(static item => item.Country, StringComparer.Ordinal)
                .ToDictionary(
                    static group => group.Key,
                    static group => group.Select(static item => item.Technology)
                        .ToHashSet(StringComparer.Ordinal),
                    StringComparer.Ordinal);
            var ladders = document.Resources.ToDictionary(
                static item => item.Key,
                static item => item.TechnologyByDevelopmentLevel,
                StringComparer.Ordinal);
            var starting = document.StartingDefaults!.Technologies;

            foreach (var developed in document.Scenarios[0].CellDevelopment)
            {
                var cell = document.Map.Cells[developed.Cell];
                if (cell.Region.Province is not { } province ||
                    !owners.TryGetValue(province, out var owner) ||
                    owner is null)
                {
                    continue;
                }

                var known = held.TryGetValue(owner, out var set) ? set : [];
                foreach (var resource in cell.Resources)
                {
                    if (ladders.GetValueOrDefault(resource) is not { } ladder ||
                        developed.Level >= ladder.Length)
                    {
                        continue;
                    }

                    var gate = ladder[developed.Level];
                    if (gate is null || known.Contains(gate) || starting.Contains(gate))
                    {
                        permitted++;
                        continue;
                    }

                    beyond.Add($"{key} cell {developed.Cell} {resource} L{developed.Level} needs {gate}");
                }
            }
        }

        Assert.True(checkedScenarios >= 4, $"Only {checkedScenarios} scenarios carried both records.");
        Assert.Equal(380, permitted);

        // Four, all timber at Level III in one country of s1. If this number
        // moves, the transcription moved with it.
        Assert.Equal(4, beyond.Count);
        Assert.All(beyond, entry => Assert.Contains("resource.forest L3", entry, StringComparison.Ordinal));
    }

    /// <summary>
    /// Runs a real turn on a real scenario, end to end: import, compile,
    /// resolve. Unit fixtures are four cells and four workers; this is 6,480
    /// cells and seven powers, and it is the only check that the phases compose
    /// on data nobody tailored for them.
    /// </summary>
    [Fact]
    public void AnImportedScenarioResolvesATurn()
    {
        var directory = Environment.GetEnvironmentVariable("IMPERIALISM_SCENARIO_DIR");
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        // Setting the variable is a declaration that the corpus is there, so a
        // missing `s1` is a broken setup rather than a reason to test nothing.
        // Returning quietly on it made this gate indistinguishable from a pass.
        var mapPath = Path.Combine(directory, "s1.map");
        Assert.True(File.Exists(mapPath), $"IMPERIALISM_SCENARIO_DIR is set but {mapPath} is missing.");

        var result = LegacyWorldConverter.Convert(
            LegacyMapCodec.Decode(File.ReadAllBytes(mapPath), MapFormatProfile.Imperialism1),
            LegacyScenarioCodec.Decode(File.ReadAllBytes(Path.Combine(directory, "s1.scn"))),
            null,
            "corpus-turn");
        Assert.True(result.Success);

        var compiled = WorldContentCompiler.Compile(result.Document!);
        var world = compiled.World;
        var state = new WorldState(world);
        var britain = new CountryId(0);
        var headcountBefore = state.GetTotalWorkers(britain);

        // 15 untrained + 15 trained + 30 expert = 60 workers, 165 labour.
        Assert.Equal(60, headcountBefore);
        Assert.Equal(165, state.GetAvailableLabour(britain));

        // Britain's clothing factory, ordered far beyond what it can do. 165
        // labour would buy 82 cycles at two labour each, so the factory's own
        // capacity is what stops it at two — which is the point. On a shipped
        // scenario labour is real on turn one without being the constraint that
        // bites, exactly as a starting position should read.
        var clothing = world.ProductionRecipes.Single(recipe =>
            compiled.Catalog.GetKey(recipe.Id) == "recipe.clothing-from-fabric");
        Assert.Equal(2, clothing.LabourCost);

        var orders = new TurnOrders(world.Countries
            .Select(country => country.Id == britain
                ? new CountryTurnOrders(country.Id, [new ProductionOrder(clothing.Id, 1000)])
                : new CountryTurnOrders(country.Id, []))
            .ToArray());

        var resolution = TurnResolver.Resolve(state, orders, 0);

        var produced = resolution.Events.OfType<ProductionCompletedEvent>()
            .Single(item => item.Country == britain);
        Assert.Equal(2, produced.CompletedCycles);
        Assert.Equal(4, produced.LabourUsed);
        Assert.Equal(2 * produced.CompletedCycles, produced.LabourUsed);

        var fed = resolution.Events.OfType<WorkersFedEvent>().Single(item => item.Country == britain);
        Assert.Equal(headcountBefore, fed.WellFed + fed.Sick + fed.Starved);
        Assert.Equal(headcountBefore - fed.Starved, state.GetTotalWorkers(britain));

        // A shipped scenario feeds its whole workforce properly on turn one:
        // its starting warehouse and its first harvest cover the demand with
        // nobody reduced to the wrong food. If this ever goes red, the feeding
        // rules or the food supply have drifted, not the scenario.
        Assert.Equal(headcountBefore, fed.WellFed);
        Assert.Equal(0, fed.Sick);
        Assert.Equal(0, fed.Starved);

        // Nobody ill means the pool is intact for turn two as well. If this
        // goes red, a shipped scenario has started poisoning its own workforce.
        foreach (var grade in WorkerGrades.All)
        {
            Assert.Equal(0, state.GetSickWorkers(britain, grade));
        }

        Assert.Equal(165, state.GetAvailableLabour(britain));

        // Every power has deposits and a capital, so extraction must report.
        Assert.NotEmpty(resolution.Events.OfType<ResourceExtractedEvent>());
    }

    [Fact]
    public void DependencyDirectionKeepsFormatsIndependentFromModernLayers()
    {
        var importerReferences = typeof(LegacyWorldConverter).Assembly.GetReferencedAssemblies();
        var formatReferences = typeof(MapDocument).Assembly.GetReferencedAssemblies();

        Assert.Contains(importerReferences, static item => item.Name == "Imperialism.Content");
        Assert.Contains(importerReferences, static item => item.Name == "Imperialism.Formats");
        Assert.DoesNotContain(formatReferences, static item =>
            item.Name is "Imperialism.Core" or "Imperialism.Content" or "Imperialism.LegacyImport");
    }

    /// <summary>
    /// The manual's Terrain Tiles Table, both columns. It gives every terrain a
    /// civilian worker and gives three of them "None"; it also names the
    /// Prospector on exactly five, and gates three of those on Oil Drilling.
    /// Those cases are the whole reason terrain gained attributes, so they are
    /// pinned here one by one rather than left to the corpus test, which can
    /// only show that nothing contradicts them.
    /// </summary>
    /// <remarks>
    /// <paramref name="prospecting"/> is a tri-state written as a string so the
    /// three cases read at a glance: <c>null</c> for ground that hides nothing,
    /// <c>""</c> for ground anyone may search, and a technology key for ground
    /// that must be paid for in knowledge first.
    /// </remarks>
    [Theory]
    [InlineData(0, "terrain.ocean", false, null)]
    [InlineData(1, "terrain.clear", false, null)]
    [InlineData(2, "terrain.cotton", true, null)]
    [InlineData(3, "terrain.cattle-ranch", true, null)]
    [InlineData(4, "terrain.horse-ranch", false, null)]
    [InlineData(5, "terrain.grain-farm", true, null)]
    [InlineData(6, "terrain.orchard", true, null)]
    [InlineData(7, "terrain.wool-hill", true, null)]
    [InlineData(8, "terrain.hill", true, "")]
    [InlineData(9, "terrain.mountain", true, "")]
    [InlineData(10, "terrain.swamp", true, "technology.oil-drilling")]
    [InlineData(11, "terrain.desert", true, "technology.oil-drilling")]
    [InlineData(12, "terrain.tundra", true, "technology.oil-drilling")]
    [InlineData(13, "terrain.forest", true, null)]
    [InlineData(14, "terrain.town", false, null)]
    [InlineData(15, "terrain.scrub-forest", false, null)]
    [InlineData(16, "terrain.capital", false, null)]
    public void EveryLegacyTerrainCarriesTheManualsImprovability(
        byte code,
        string key,
        bool isImprovable,
        string? prospecting)
    {
        var cells = new[]
        {
            code == 0
                ? OceanCell()
                : new HexCell { Terrain = code, Province = 0, NationZoneA = 0, NationZoneB = 0 },
            LandCell(0, 0),
        };
        var scenario = new ScenarioDocument(
        [
            Record("year", 1815),
            NameRecord("cnam", 0, "Country"),
            NameRecord("pnam", 0, "Province"),
            NameRecord("zone", 0, "Sea"),
        ]);

        var result = LegacyWorldConverter.Convert(
            CreateMap(2, 1, cells), scenario, null, "terrain-map");

        var terrain = Assert.Single(result.Document!.Terrains, item => item.Key == key);
        Assert.Equal(isImprovable, terrain.IsImprovable);
        if (prospecting is null)
        {
            Assert.Null(terrain.Prospecting);
        }
        else
        {
            Assert.NotNull(terrain.Prospecting);
            Assert.Equal(
                prospecting.Length == 0 ? null : prospecting,
                terrain.Prospecting!.RequiredTechnology);
        }
    }

    /// <summary>
    /// "Coal, iron, gold, gems, and oil must be found by a Prospector before
    /// they can be exploited by your other civilians." Everything else on the
    /// map announces itself by its terrain, so only those five hide.
    /// </summary>
    [Fact]
    public void OnlyTheFiveDepositsTheManualHidesRequireDiscovery()
    {
        var expected = new (byte Code, string Resource, bool Hidden)[]
        {
            (0, "resource.cotton", false),
            (1, "resource.wool", false),
            (2, "resource.forest", false),
            (3, "resource.coal", true),
            (4, "resource.iron", true),
            (5, "resource.horses", false),
            (6, "resource.oil", true),
            (17, "resource.grain", false),
            (18, "resource.fruit", false),
            (19, "resource.fish", false),
            (20, "resource.cattle", false),
            (21, "resource.gems", true),
            (22, "resource.gold", true),
        };
        var cells = expected.Select(item => new HexCell
        {
            Terrain = 1,
            Province = 0,
            NationZoneA = 0,
            NationZoneB = 0,
            ResourceA = item.Code,
        }).ToArray();
        var scenario = new ScenarioDocument(
        [
            Record("year", 1815),
            NameRecord("cnam", 0, "Country"),
            NameRecord("pnam", 0, "Province"),
        ]);

        var result = LegacyWorldConverter.Convert(
            CreateMap(cells.Length, 1, cells), scenario, null, "discovery-map");

        Assert.True(result.Success);
        var hidden = result.Document!.Resources
            .ToDictionary(static item => item.Key, static item => item.RequiresDiscovery);
        foreach (var item in expected)
        {
            Assert.Equal(item.Hidden, hidden[item.Resource]);
        }
    }

    /// <summary>
    /// A Prospector searches and nothing else does. The work kind is what lets
    /// one order type mean two things, so it is pinned per civilian rather than
    /// assumed from the name.
    /// </summary>
    [Fact]
    public void OnlyTheProspectorProspects()
    {
        var scenario = new ScenarioDocument(
        [
            Record("year", 1815),
            NameRecord("cnam", 0, "Country"),
            NameRecord("pnam", 0, "Province"),
        ]);

        var result = LegacyWorldConverter.Convert(
            CreateMap(2, 1, LandCell(0, 0), OceanCell()), scenario, null, "work-kind-map");

        Assert.True(result.Success);
        var byKey = result.Document!.CivilianTypes
            .ToDictionary(static item => item.Key, static item => item.Work);
        Assert.Equal(CivilianWorkKind.Prospect, byKey["civilian.prospector"]);
        Assert.All(
            byKey.Where(static item => item.Key != "civilian.prospector"),
            item => Assert.Equal(CivilianWorkKind.Improve, item.Value));
    }

    /// <summary>
    /// An unknown code gets a placeholder key and no permission to be worked:
    /// nothing is known about the ground, so letting a civilian improve it
    /// would invent a rule about a tile we cannot even name.
    /// </summary>
    [Fact]
    public void AnUnknownTerrainCodeIsNeverImprovable()
    {
        var cells = new[]
        {
            new HexCell { Terrain = 200, Province = 0, NationZoneA = 0, NationZoneB = 0 },
            LandCell(0, 0),
        };
        var scenario = new ScenarioDocument(
        [
            Record("year", 1815),
            NameRecord("cnam", 0, "Country"),
            NameRecord("pnam", 0, "Province"),
        ]);

        var result = LegacyWorldConverter.Convert(
            CreateMap(2, 1, cells), scenario, null, "unknown-terrain");

        var terrain = Assert.Single(
            result.Document!.Terrains, item => item.Key == "terrain.legacy-unknown-200");
        Assert.False(terrain.IsImprovable);
        Assert.Contains(
            result.Report.Diagnostics,
            item => item.Code == "map.unknown-terrain-code");
    }

    /// <summary>
    /// The Resource Development Table read the other way round. Fish has no
    /// improver in the table and horses are absent from it altogether, which is
    /// why both come out null rather than defaulting to something.
    /// </summary>
    [Fact]
    public void EveryDepositNamesTheCivilianTheManualGivesIt()
    {
        var expected = new (byte Code, string Resource, string? Improver)[]
        {
            (0, "resource.cotton", "civilian.farmer"),
            (1, "resource.wool", "civilian.rancher"),
            (2, "resource.forest", "civilian.forester"),
            (3, "resource.coal", "civilian.miner"),
            (4, "resource.iron", "civilian.miner"),
            (5, "resource.horses", null),
            (6, "resource.oil", "civilian.driller"),
            (17, "resource.grain", "civilian.farmer"),
            (18, "resource.fruit", "civilian.farmer"),
            (19, "resource.fish", null),
            (20, "resource.cattle", "civilian.rancher"),
            (21, "resource.gems", "civilian.miner"),
            (22, "resource.gold", "civilian.miner"),
        };
        var cells = expected.Select(item => new HexCell
        {
            Terrain = 1,
            Province = 0,
            NationZoneA = 0,
            NationZoneB = 0,
            ResourceA = item.Code,
        }).ToArray();
        var scenario = new ScenarioDocument(
        [
            Record("year", 1815),
            NameRecord("cnam", 0, "Country"),
            NameRecord("pnam", 0, "Province"),
        ]);

        var result = LegacyWorldConverter.Convert(
            CreateMap(cells.Length, 1, cells), scenario, null, "improver-map");

        Assert.True(result.Success);
        var improvers = result.Document!.Resources
            .ToDictionary(static item => item.Key, static item => item.ImprovedBy);
        foreach (var item in expected)
        {
            Assert.Equal(item.Improver, improvers[item.Resource]);
        }

        // Every improver named above must exist in the catalog, or the deposit
        // would refer to a civilian type nothing declares.
        var declared = result.Document.CivilianTypes.Select(static item => item.Key).ToHashSet();
        Assert.All(
            expected.Select(static item => item.Improver).Where(static item => item is not null),
            improver => Assert.Contains(improver!, declared));
    }

    /// <summary>
    /// <c>civi</c> is <c>[type, cell]</c> and names no owner; the province the
    /// cell sits in supplies it.
    /// </summary>
    [Fact]
    public void CiviRecordsBecomeCiviliansOwnedByTheProvinceTheyStandIn()
    {
        var map = CreateMap(
            3,
            1,
            LandCell(10, 2),
            LandCell(11, 3),
            OceanCell());
        var scenario = new ScenarioDocument(
        [
            Record("year", 1815),
            NameRecord("cnam", 2, "Blue"),
            NameRecord("cnam", 3, "Green"),
            NameRecord("pnam", 10, "West"),
            NameRecord("pnam", 11, "East"),
            Record("civi", 2, 0),
            Record("civi", 4, 1),
        ]);

        var result = LegacyWorldConverter.Convert(map, scenario, null, "civi-map");

        Assert.True(result.Success, result.Report.ToHumanReadable());
        var civilians = result.Document!.Scenarios[0].Civilians;
        Assert.Equal(2, civilians.Length);
        Assert.Equal("civilian.farmer", civilians[0].Type);
        Assert.Equal(0, civilians[0].Cell);
        Assert.Equal("civilian.engineer", civilians[1].Type);
        Assert.Equal(1, civilians[1].Cell);
        Assert.NotEqual(civilians[0].Country, civilians[1].Country);

        // The tag stops being reported as unconverted, which is the point of
        // adding it to ConvertedScenarioTags.
        Assert.DoesNotContain("scenario.tag.civi", result.Report.DeferredCounts.Keys);
    }

    [Fact]
    public void CiviRecordsOffTheMapOnOceanOrOnUnownedLandAreRejected()
    {
        var map = CreateMap(2, 1, LandCell(10, 2), OceanCell());
        var unowned = CreateMap(2, 1, LandCell(10, 255), OceanCell());
        var names = new[]
        {
            Record("year", 1815),
            NameRecord("cnam", 2, "Blue"),
            NameRecord("pnam", 10, "West"),
        };

        Assert.Contains(
            LegacyWorldConverter
                .Convert(map, new ScenarioDocument([.. names, Record("civi", 0, 99)]), null, "off-map")
                .Report.Diagnostics,
            item => item.Code == "scenario.invalid-civi-cell");
        Assert.Contains(
            LegacyWorldConverter
                .Convert(map, new ScenarioDocument([.. names, Record("civi", 0, 1)]), null, "ocean")
                .Report.Diagnostics,
            item => item.Code == "scenario.civi-on-ocean");
        Assert.Contains(
            LegacyWorldConverter
                .Convert(unowned, new ScenarioDocument([.. names, Record("civi", 0, 0)]), null, "unowned")
                .Report.Diagnostics,
            item => item.Code == "scenario.civi-on-unowned-land");
        Assert.Contains(
            LegacyWorldConverter
                .Convert(map, new ScenarioDocument([.. names, Record("civi", 99, 0)]), null, "type")
                .Report.Diagnostics,
            item => item.Code == "scenario.invalid-civi-type");
        Assert.Contains(
            LegacyWorldConverter
                .Convert(map, new ScenarioDocument([.. names, Record("civi", 0)]), null, "fields")
                .Report.Diagnostics,
            item => item.Code == "scenario.invalid-civi");
    }

    private static MapDocument CreateMap(int width, int height, params HexCell[] cells)
    {
        var profile = new MapFormatProfile(width, height, trailerRecordCount: 2, trailerRecordSize: 3);
        return new MapDocument(profile, cells, new byte[profile.TrailerSize]);
    }

    private static HexCell LandCell(ushort province, byte owner) => new()
    {
        Terrain = 1,
        Province = province,
        NationZoneA = owner,
        NationZoneB = owner,
    };

    private static HexCell OceanCell() => new()
    {
        Terrain = 0,
        Province = ushort.MaxValue,
        NationZoneA = 11,
        NationZoneB = 11,
    };

    private static ScenarioDocument Scenario(uint year) => new([Record("year", year)]);

    private static ScenarioRecord Record(string tag, params uint[] fields) => new(tag, fields);

    private static ScenarioRecord NameRecord(string tag, uint id, string name) => new(tag, [id], name);
}
