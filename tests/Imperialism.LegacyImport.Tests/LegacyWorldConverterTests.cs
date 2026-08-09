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

        // **Labour is two per cycle, flat**, and the original's own recipe help strings
        // say so for all nine of them. The manual's single priced example — two fabric and
        // two labour for a unit of clothing — admitted three readings; food processing is
        // the recipe that separates them, taking four inputs and making two for the same
        // two labour. The input-total reading this used to assert is retracted. See
        // docs/formulas/production.md.
        Assert.All(result.Document.ProductionRecipes, static recipe =>
            Assert.Equal(2, recipe.LabourCost));

        var cannedFood = result.Document.ProductionRecipes
            .Single(static recipe => recipe.Key == "recipe.canned-food-from-fish");
        Assert.Equal(
            (4L, 2L, 2L),
            (cannedFood.Inputs.Sum(static item => item.Quantity),
                cannedFood.Outputs.Sum(static item => item.Quantity),
                cannedFood.LabourCost));
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
    /// **The whole technology table, pinned**: order, cost, arrival year and
    /// prerequisites. The order is load-bearing because a <c>tech</c> record is a
    /// bare 1-based index into it, and the other three columns are what the
    /// Investment screen reads.
    /// </summary>
    /// <remarks>
    /// **Order and cost are the executable's**, from the name blocks in `STR#ENU.GOB`
    /// and the 28-entry cash table the technology store reads at the same position.
    /// The recovered order turns out to be the manual's printed one, so the wiki
    /// ordering this used to pin — differing at positions 4–7 and 13–14 — is
    /// retracted. Twelve of the twenty-six prices moved with it, because the wiki's
    /// price column was **off by one from Streamlined Hulls onwards**: each entry
    /// carried the next one's price.
    /// <para>
    /// **The years are derived and the prerequisites are not recovered.** The
    /// executable stores an inclusive pseudo-random turn-offset window per
    /// technology, not a year; the year here is <c>1815 + window minimum</c>, which
    /// puts 25 of the wiki's 26 observed years inside their window and 19 exactly on
    /// it. Prerequisites are still the wiki's and are now the weakest column. See
    /// <c>docs/formulas/technology.md</c>.
    /// </para>
    /// <para>
    /// Names stay the manual's where the sources disagree: "Steel and Iron Plows"
    /// over "Steel Plows", "Fertiliser" over "Fertilizer", "Armour" over "Armor".
    /// </para>
    /// </remarks>
    [Fact]
    public void TheWholeTechnologyTableIsPinned()
    {
        (string Name, long? Cost, int Year, string[] Requires)[] expected =
        [
            ("High Pressure Steam Engine", null, 1815, []),
            ("Seed Drill", null, 1815, []),
            ("Cotton Gin", 1_000, 1816, []),
            ("Streamlined Hulls", 1_000, 1821, []),
            ("Square-Set Timbering", 1_500, 1821, []),
            ("Iron Railroad Bridge", 1_500, 1821, []),
            ("Feed Grasses", 1_500, 1821, []),
            ("Spinning Jenny", 1_500, 1826, ["Cotton Gin", "Feed Grasses"]),
            ("Paddlewheels", 3_000, 1826, []),
            ("Steel and Iron Plows", 3_000, 1831, ["Seed Drill"]),
            ("Bessemer Converter", 3_000, 1836, []),
            ("Compound Steam Engine", 6_000, 1836, ["Iron Railroad Bridge"]),
            ("Rifled Artillery", 7_000, 1841, []),
            ("Breech-Loading Rifles", 10_000, 1841, ["Bessemer Converter"]),
            ("Advanced Iron Working", 12_000, 1846, []),
            ("Power Loom", 12_000, 1846, ["Spinning Jenny"]),
            ("Mechanical Reaper", 12_000, 1851, ["Steel and Iron Plows"]),
            ("Commercial Fertiliser", 12_000, 1856, ["Steel and Iron Plows"]),
            ("Oil Drilling", 12_000, 1856, []),
            ("Barbed Wire", 25_000, 1861, ["Feed Grasses"]),
            ("Steel Armour Plate", 20_000, 1866, ["Advanced Iron Working"]),
            ("Large Artillery", 40_000, 1871, ["Rifled Artillery"]),
            ("Dynamite", 40_000, 1871, ["Compound Steam Engine", "Square-Set Timbering"]),
            ("Marine Engineering", 40_000, 1871, ["Steel Armour Plate"]),
            ("Machine Guns", 40_000, 1876, ["Breech-Loading Rifles"]),
            ("Chemistry", 100_000, 1876, ["Oil Drilling", "Barbed Wire"]),
            ("Improved Range-Finding", 120_000, 1881, ["Marine Engineering"]),
            ("Internal Combustion", 150_000, 1881, ["Chemistry"]),
        ];

        var result = LegacyWorldConverter.Convert(
            CreateMap(2, 1, LandCell(0, 0), OceanCell()),
            new ScenarioDocument(
            [
                Record("year", 0),
                NameRecord("cnam", 0, "Country"),
                NameRecord("pnam", 0, "Province"),
            ]),
            null,
            "technology-table-map");

        Assert.True(result.Success, result.Report.ToHumanReadable());
        var table = result.Document!.Technologies;
        Assert.Equal(28, table.Length);

        static string Key(string name) =>
            $"technology.{name.ToLowerInvariant().Replace(' ', '-')}";

        for (var index = 0; index < expected.Length; index++)
        {
            var (name, cost, year, requires) = expected[index];
            var actual = table[index];
            Assert.Equal(
                (Key(name), name, cost, (int?)year),
                (actual.Key, actual.Name, actual.Cost, actual.AvailableFrom));

            // Separately, because a tuple would compare the arrays by reference.
            Assert.Equal(requires.Select(Key), actual.Prerequisites);
        }

        // The first two are **not for sale** rather than free: the price list gives
        // them no price, and nobody can buy what every power already holds.
        Assert.Null(table[0].Cost);
        Assert.Null(table[1].Cost);
        Assert.All(table.Skip(2), item => Assert.NotNull(item.Cost));
    }

    /// <summary>
    /// **The whole trade roster, pinned**: which commodities the market sees, in what
    /// order, at what price — and, as informatively, the eight it never sees.
    /// </summary>
    /// <remarks>
    /// The order is a rule rather than a listing: it decides which deals get cargo holds,
    /// and "clothing deals are always considered prior to all other deals".
    /// <para>
    /// **The eight untradable commodities are the striking part.** They are exactly the
    /// ones the manual says cannot be traded, and the roster comes from a screenshot rather
    /// than from the prose — so two independent sources agree, three times over: raw food
    /// ("food resources cannot be traded on the world market"), gold and gems ("they never
    /// reach the industry warehouse and they cannot be traded"), and canned food being the
    /// exception that *is* tradable.
    /// </para>
    /// </remarks>
    [Fact]
    public void TheWholeTradeRosterIsPinned()
    {
        (string Key, long Price)[] expected =
        [
            ("commodity.clothing", 900),
            ("commodity.furniture", 900),
            ("commodity.hardware", 900),
            ("commodity.armaments", 900),
            ("commodity.canned-food", 100),
            ("commodity.fabric", 300),
            ("commodity.lumber", 300),
            ("commodity.paper", 300),
            ("commodity.steel", 300),
            ("commodity.cotton", 100),
            ("commodity.wool", 100),
            ("commodity.timber", 100),
            ("commodity.coal", 100),
            ("commodity.iron", 100),
            ("commodity.horses", 300),
        ];

        var result = LegacyWorldConverter.Convert(
            CreateMap(2, 1, LandCell(0, 0), OceanCell()),
            new ScenarioDocument(
            [
                Record("year", 0),
                NameRecord("cnam", 0, "Country"),
                NameRecord("pnam", 0, "Province"),
            ]),
            null,
            "roster-map");

        Assert.True(result.Success, result.Report.ToHumanReadable());
        var traded = result.Document!.Commodities
            .Where(static item => item.WorldPrice is not null)
            .OrderBy(static item => item.TradeOrder!.Value)
            .ToArray();

        Assert.Equal(
            expected.Select(static item => item.Key),
            traded.Select(static item => item.Key));
        Assert.Equal(
            expected.Select(static item => (long?)item.Price),
            traded.Select(static item => item.WorldPrice));

        // The order is dense from zero, so nothing shares a slot and nothing is skipped.
        Assert.Equal(Enumerable.Range(0, expected.Length), traded.Select(static i => i.TradeOrder!.Value));

        // And the eight the market never sees.
        Assert.Equal(
            [
                "commodity.grain", "commodity.livestock", "commodity.fruit", "commodity.fish",
                "commodity.oil", "commodity.gold", "commodity.gems", "commodity.fuel",
            ],
            result.Document.Commodities
                .Where(static item => item.WorldPrice is null)
                .Select(static item => item.Key));

        // Gold and gems convert on carriage instead, and the two are alternatives.
        Assert.All(
            result.Document.Commodities.Where(static item => item.CashPerUnit is not null),
            item => Assert.Null(item.WorldPrice));
    }

    /// <summary>
    /// The thirteen classes of ship, **in the executable's own array order** — which is
    /// what a legacy <c>ship</c> record's 1-based type indexes into, and what used to be
    /// the blocking unknown.
    /// </summary>
    /// <remarks>
    /// The whole table is pinned now rather than cargo alone: order, cargo, sea zones, the
    /// six-commodity build bill and the combat numbers all come from
    /// <c>docs/disasm/definitive-original-data.md</c>. If any of it moves, the
    /// transcription moved.
    /// </remarks>
    [Fact]
    public void TheShipTableIsPinned()
    {
        var result = LegacyWorldConverter.Convert(
            CreateMap(2, 1, LandCell(0, 0), OceanCell()),
            new ScenarioDocument(
            [
                Record("year", 0),
                NameRecord("cnam", 0, "Country"),
                NameRecord("pnam", 0, "Province"),
            ]),
            null,
            "ship-map");

        Assert.True(result.Success, result.Report.ToHumanReadable());
        var ships = result.Document!.ShipTypes;
        Assert.Equal(13, ships.Length);

        // **The order is the executable's**, and it is what a `ship` record indexes into.
        Assert.Equal(
            [
                "ship.trader", "ship.indiaman", "ship.frigate", "ship.ship-of-the-line",
                "ship.paddlewheeler", "ship.clipper", "ship.raider", "ship.ironclad",
                "ship.advanced-ironclad", "ship.freighter", "ship.armoured-cruiser",
                "ship.dreadnought", "ship.battle-cruiser",
            ],
            ships.Select(static item => item.Key));

        // Cargo: the five merchants, and nothing else. **The Freighter's 16 was the last
        // unknown cargo figure** and is four Traders' worth.
        Assert.Equal(
            [
                ("ship.trader", 2L), ("ship.indiaman", 4L), ("ship.paddlewheeler", 8L),
                ("ship.clipper", 4L), ("ship.freighter", 16L),
            ],
            ships.Where(static item => item.Cargo > 0).Select(static item => (item.Key, item.Cargo)));

        // Sea zones is the column the manual prints as Speed: one for every merchant, two
        // to six for a warship.
        Assert.Equal(
            [1L, 1L, 3L, 2L, 1L, 1L, 5L, 3L, 4L, 1L, 6L, 5L, 6L],
            ships.Select(static item => item.SeaZones));

        // **Every hull has combat numbers, merchants included** — the manual's table
        // printed warships only, and the Freighter is the toughest thing afloat that
        // cannot shoot back.
        Assert.All(ships, item => Assert.NotNull(item.Combat));
        var freighter = ships.Single(static item => item.Key == "ship.freighter");
        Assert.Equal(
            (0L, 0L, 25L, 1200L, 0L, (long?)null),
            (freighter.Combat!.Firepower, freighter.Combat.Range, freighter.Combat.Armour,
                freighter.Combat.HullScale, freighter.Combat.BattleSpeed, freighter.Combat.Hull));

        var dreadnought = ships.Single(static item => item.Key == "ship.dreadnought");
        Assert.Equal(
            (20L, 13L, 70L, 2800L, 7L, (long?)115L),
            (dreadnought.Combat!.Firepower, dreadnought.Combat.Range, dreadnought.Combat.Armour,
                dreadnought.Combat.HullScale, dreadnought.Combat.BattleSpeed,
                dreadnought.Combat.Hull));

        // Two technology entries that gate nothing else in the engine gate a hull here.
        Assert.Equal(
            "technology.streamlined-hulls",
            ships.Single(static item => item.Key == "ship.clipper").RequiredTechnology);
        Assert.Equal(
            "technology.paddlewheels",
            ships.Single(static item => item.Key == "ship.paddlewheeler").RequiredTechnology);

        // **Every hull is priced, and none of them in cash.** The Frigate's arms figure
        // settles the 2-versus-3 discrepancy the old cost table left open.
        Assert.All(ships, item => Assert.NotEmpty(item.BuildCost));
        Assert.Equal(
            [("commodity.lumber", 5L), ("commodity.fabric", 2L), ("commodity.armaments", 2L)],
            ships.Single(static item => item.Key == "ship.frigate").BuildCost
                .Select(static item => (item.Commodity, item.Quantity)));
        Assert.Equal(
            [("commodity.armaments", 24L), ("commodity.steel", 30L), ("commodity.fuel", 20L)],
            dreadnought.BuildCost.Select(static item => (item.Commodity, item.Quantity)));

        // Every commodity a bill names must exist in the roster.
        var commodities = result.Document.Commodities.Select(static item => item.Key).ToHashSet();
        Assert.All(
            ships.SelectMany(static item => item.BuildCost),
            item => Assert.Contains(item.Commodity, commodities));

        // Every gate named must exist in the catalog.
        var declared = result.Document.Technologies.Select(static item => item.Key).ToHashSet();
        Assert.All(
            ships.Where(static item => item.RequiredTechnology is not null),
            item => Assert.Contains(item.RequiredTechnology!, declared));
    }

    /// <summary>
    /// Every power starts with three Traders — six cargo holds — which all three skirmish
    /// scenarios agree on independently. **Not a guess**, unlike the transport pool beside
    /// it; <c>ship</c> is not one of the seven records a skirmish omits.
    /// </summary>
    [Fact]
    public void EveryPowerStartsWithThreeTraders()
    {
        var result = LegacyWorldConverter.Convert(
            CreateMap(2, 1, LandCell(0, 0), OceanCell()),
            new ScenarioDocument(
            [
                Record("year", 0),
                NameRecord("cnam", 0, "Country"),
                NameRecord("pnam", 0, "Province"),
                Record("labo", 0, 4, 2, 1),
            ]),
            null,
            "fleet-map");

        Assert.True(result.Success, result.Report.ToHumanReadable());
        var fleet = Assert.Single(result.Document!.StartingDefaults!.Ships);
        Assert.Equal(("ship.trader", 3L), (fleet.Type, fleet.Count));

        // `labo` is also what makes this power a Great Power, which is what decides who
        // carries a cargo.
        Assert.True(result.Document.Countries[0].IsGreatPower);
    }

    /// <summary>
    /// <c>ship</c> records convert. The record is <c>[country, type, zone, count]</c> and
    /// the type is a 1-based index into the executable's naval table.
    /// </summary>
    /// <remarks>
    /// **They were deferred for want of that order and are not any more.** The zone is
    /// carried and never interpreted — it is not the map's ocean zone byte — and a
    /// repeated class is a second record rather than an error, which is why `s1` can give
    /// one power `8x2` and `8x1` separately. See <c>docs/formulas/trade.md</c>.
    /// </remarks>
    [Fact]
    public void ShipRecordsBecomeFleets()
    {
        var result = LegacyWorldConverter.Convert(
            CreateMap(2, 1, LandCell(0, 0), OceanCell()),
            new ScenarioDocument(
            [
                Record("year", 0),
                NameRecord("cnam", 0, "Country"),
                NameRecord("pnam", 0, "Province"),
                Record("labo", 0, 4, 2, 1),
                Record("ship", 0, 1, 14, 3),

                // The same class again, in another zone: a bag, not a table.
                Record("ship", 0, 1, 9, 2),

                // Type 5 is the Paddlewheeler, which is where a guessed order would have
                // put something else.
                Record("ship", 0, 5, 9, 1),

                // Dropped: no such hull, and no ships at all.
                Record("ship", 0, 14, 9, 1),
                Record("ship", 0, 2, 9, 0),
            ]),
            null,
            "ship-record-map");

        Assert.True(result.Success, result.Report.ToHumanReadable());
        Assert.Equal(
            [
                ("country.legacy.000", "ship.trader", 14, 3L),
                ("country.legacy.000", "ship.trader", 9, 2L),
                ("country.legacy.000", "ship.paddlewheeler", 9, 1L),
            ],
            result.Document!.Scenarios[0].Ships
                .Select(static item => (item.Country, item.Type, item.SeaZone, item.Count)));

        // A hull the table cannot name is reported and dropped, never clamped — that is
        // the one mistake the whole deferral existed to avoid.
        Assert.Contains(
            result.Report.Diagnostics,
            static item => item.Code == "scenario.unknown-ship-type");
        Assert.DoesNotContain("scenario.tag.ship", result.Report.DeferredCounts.Keys);

        // An equipped country takes its authored fleet and not the default three Traders,
        // even though `labo` makes it a default-start country.
        var compiled = WorldContentCompiler.Compile(result.Document);
        var state = new WorldState(compiled.World);
        var country = new CountryId(0);
        var trader = compiled.World.ShipTypes.Single(static item => item.Name == "Trader").Id;
        Assert.Equal(5, state.GetShipCount(country, trader));

        // 5 Traders at 2 holds, plus a Paddlewheeler at 8.
        Assert.Equal(18, state.GetMerchantMarine(country));
    }

    /// <summary>
    /// Every prerequisite points strictly earlier, so any contiguous prefix of the
    /// table is prerequisite-closed — which is the shape a <c>tech</c> record has,
    /// being a bare 1-based index into it.
    /// </summary>
    /// <remarks>
    /// **This proves nothing about which ordering is right, and saying so is the
    /// point.** It holds under the manual's printed order as well as the wiki's, so
    /// it cannot discriminate between them. What it does catch is a future edit that
    /// moves an entry above something it depends on.
    /// </remarks>
    [Fact]
    public void EveryPrerequisiteSitsEarlierInTheTable()
    {
        var result = LegacyWorldConverter.Convert(
            CreateMap(2, 1, LandCell(0, 0), OceanCell()),
            new ScenarioDocument(
            [
                Record("year", 0),
                NameRecord("cnam", 0, "Country"),
                NameRecord("pnam", 0, "Province"),
            ]),
            null,
            "closure-map");

        Assert.True(result.Success, result.Report.ToHumanReadable());
        var table = result.Document!.Technologies;
        var positionOf = table
            .Select(static (item, index) => (item.Key, index))
            .ToDictionary(static item => item.Key, static item => item.index, StringComparer.Ordinal);

        var edges = 0;
        var withPrerequisites = 0;
        for (var index = 0; index < table.Length; index++)
        {
            if (table[index].Prerequisites.Length > 0)
            {
                withPrerequisites++;
            }

            foreach (var required in table[index].Prerequisites)
            {
                Assert.True(
                    positionOf.TryGetValue(required, out var at),
                    $"{table[index].Key} requires {required}, which the table does not declare.");
                Assert.True(
                    at < index,
                    $"{table[index].Key} at {index} requires {required} at {at}.");
                edges++;
            }
        }

        // Not vacuous in the weak sense — there is plenty to check — while still
        // being vacuous as evidence about the ordering. Sixteen entries name a
        // prerequisite and three of them name two, so there are nineteen edges.
        Assert.Equal(16, withPrerequisites);
        Assert.Equal(19, edges);
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
    /// <c>cash</c> is <c>[country, amount]</c> — the same two-field shape as
    /// <c>tran</c>, measured across the corpus before this was built.
    /// </summary>
    [Fact]
    public void CashRecordsBecomeStartingTreasuries()
    {
        var scenario = new ScenarioDocument(
        [
            Record("year", 1815),
            NameRecord("cnam", 0, "Country"),
            NameRecord("pnam", 0, "Province"),
            Record("cash", 0, 2500),
        ]);

        var result = LegacyWorldConverter.Convert(
            CreateMap(2, 1, LandCell(0, 0), OceanCell()), scenario, null, "cash-map");

        Assert.True(result.Success, result.Report.ToHumanReadable());
        var treasury = Assert.Single(result.Document!.Scenarios[0].Cash);
        Assert.Equal(2500, treasury.Amount);
        Assert.DoesNotContain("scenario.tag.cash", result.Report.DeferredCounts.Keys);
    }

    /// <summary>
    /// A scenario carrying no <c>cash</c> leaves its powers on the engine's
    /// default, which is a guess — see <c>docs/formulas/money.md</c>. Pinned so
    /// that changing it is a deliberate act.
    /// </summary>
    [Fact]
    public void AScenarioWithNoCashLeavesEveryPowerOnTheDefault()
    {
        var scenario = new ScenarioDocument(
        [
            Record("year", 1815),
            NameRecord("cnam", 0, "Country"),
            NameRecord("pnam", 0, "Province"),
            Record("labo", 0, 4, 2, 1),
        ]);

        var result = LegacyWorldConverter.Convert(
            CreateMap(2, 1, LandCell(0, 0), OceanCell()), scenario, null, "no-cash-map");

        Assert.True(result.Success, result.Report.ToHumanReadable());
        Assert.Empty(result.Document!.Scenarios[0].Cash);
        Assert.Equal(5000, result.Document.StartingDefaults!.Cash);
    }

    /// <summary>
    /// **The manual prices both outright** — gold at $200 a unit and gems at
    /// $500 — and prices nothing else, because nothing else bypasses the
    /// warehouse.
    /// </summary>
    [Fact]
    public void OnlyGoldAndGemsArePricedInCash()
    {
        var result = LegacyWorldConverter.Convert(
            CreateMap(2, 1, LandCell(0, 0), OceanCell()),
            new ScenarioDocument(
            [
                Record("year", 1815),
                NameRecord("cnam", 0, "Country"),
                NameRecord("pnam", 0, "Province"),
            ]),
            null,
            "cash-value-map");

        Assert.True(result.Success, result.Report.ToHumanReadable());
        Assert.Equal(
            [("commodity.gold", 200L), ("commodity.gems", 500L)],
            result.Document!.Commodities
                .Where(static item => item.CashPerUnit is not null)
                .Select(static item => (item.Key, item.CashPerUnit!.Value)));
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
    /// technology table. See <c>docs/formulas/technology.md</c> for the corpus
    /// check behind that reading.
    /// </summary>
    /// <remarks>
    /// **Id 5 is one of the six positions the orderings disagree about**, which is
    /// what makes it the id worth pinning: Feed Grasses under the wiki's order,
    /// Square-Set Timbering under the executable's. It reads Square-Set Timbering
    /// now, and the wiki order it used to hold is retracted. Id 23 is Dynamite under
    /// both.
    /// </remarks>
    [Fact]
    public void TechRecordsBecomeStartingKnowledge()
    {
        var scenario = new ScenarioDocument(
        [
            Record("year", 0),
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
        // cash records per scenario, measured before anything was built on them.
        // Five carry none at all; the five that do are authored situations, and
        // s3 makes the point on its own by giving its seven powers 1,500 to
        // 15,000 apiece. There is no constant in there to mine.
        var expectedCash = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["s1"] = 7,
            ["s3"] = 7,
            ["s5"] = 7,
            ["s13"] = 7,
            ["s14"] = 7,
            ["s9"] = 0,
            ["s10"] = 0,
            ["s11"] = 0,
            ["s12"] = 0,
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

            // cash is converted now too, and no longer deferred.
            Assert.DoesNotContain("scenario.tag.cash", result.Report.DeferredCounts.Keys);
            Assert.DoesNotContain(
                result.Report.Diagnostics,
                item => item.Code.StartsWith("scenario.invalid-cash", StringComparison.Ordinal) ||
                    item.Code == "scenario.repeated-cash");
            if (expectedCash.TryGetValue(key, out var treasuries))
            {
                Assert.Equal(treasuries, result.Document.Scenarios[0].Cash.Length);
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
    /// **The port-and-depot rule on shipped data.** Six of the ten scenarios
    /// author at least one hex carrying both, and every one of them is a gateway
    /// that lets a rail line reach the capital by sea.
    /// </summary>
    /// <remarks>
    /// This engine used to connect a depot only when its rail component reached
    /// the capital, so every line hanging off a coastal port was stranded. The
    /// correction is worth its own measurement because it is large — <c>s9</c>
    /// and <c>s12</c> each gain thirty collecting cells, a fifth more than they
    /// had — and because a rule this load-bearing should not be trusted to a
    /// synthetic four-cell fixture alone.
    ///
    /// <code>
    ///        port+depot  cells before  after
    ///  s1            12           463    471
    ///  s3             3           235    239
    ///  s9             4           126    156
    ///  s12            4           124    154
    ///  s13, s14       1           105    109
    /// </code>
    ///
    /// <c>s5</c>, <c>s10</c>, <c>s11</c> and <c>s15</c> author no such hex and
    /// are unchanged, which is the control this measurement needs.
    /// </remarks>
    [Fact]
    public void TheCorpusAuthorsPortAndDepotHexesAndTheyConnectTheLinesBehindThem()
    {
        var directory = Environment.GetEnvironmentVariable("IMPERIALISM_SCENARIO_DIR");
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        var expected = new Dictionary<string, (int Gateways, int CollectedCells)>(StringComparer.Ordinal)
        {
            ["s1"] = (12, 471),
            ["s3"] = (3, 239),
            ["s9"] = (4, 156),
            ["s12"] = (4, 154),
            ["s13"] = (1, 109),
            ["s14"] = (1, 109),
            ["s5"] = (0, 34),
            ["s10"] = (0, 38),
            ["s11"] = (0, 41),
            ["s15"] = (0, 42),
        };
        var checkedScenarios = 0;

        foreach (var mapPath in Directory.GetFiles(directory, "*.map").OrderBy(static path => path))
        {
            var key = Path.GetFileNameWithoutExtension(mapPath);
            var scenarioPath = Path.Combine(directory, $"{key}.scn");
            if (key == "s0" || !File.Exists(scenarioPath) || !expected.TryGetValue(key, out var want))
            {
                continue;
            }

            var document = LegacyWorldConverter.Convert(
                LegacyMapCodec.Decode(File.ReadAllBytes(mapPath), MapFormatProfile.Imperialism1),
                LegacyScenarioCodec.Decode(File.ReadAllBytes(scenarioPath)),
                null,
                $"port-depot-{key}").Document!;
            var scenario = document.Scenarios[0];
            var gateways = scenario.Ports.Intersect(scenario.Depots).Count();

            var world = WorldContentCompiler.Compile(document).World;
            var collectedCells = TurnResolver
                .Resolve(new WorldState(world), TurnOrders.Empty(world.Countries.Count), 0)
                .Events.OfType<ResourceExtractedEvent>()
                .Sum(static item => item.CollectedCellCount);

            Assert.Equal(want, (gateways, collectedCells));
            checkedScenarios++;
        }

        Assert.Equal(10, checkedScenarios);
    }

    /// <summary>
    /// **The check the rail terrain gates rest on**, and the strongest
    /// corroboration in the project so far. Every end of every railed link the
    /// corpus authors is compared against what the owning power's technologies
    /// would have let an Engineer cross.
    /// </summary>
    /// <remarks>
    /// **1,140 ends permitted, none not.** A wrong mapping would misfire, and
    /// the check is not vacuous — the gated terrains are exercised, and the
    /// pattern across scenarios is exactly what the gates predict:
    /// <list type="bullet">
    /// <item><c>s1</c>'s powers hold ids 1–21, which include Iron Railroad
    /// Bridge and Compound Steam Engine, and it is the one scenario that rails
    /// swamp (3 ends), barren hills (29) and fertile hills (13).</item>
    /// <item><c>s9</c> and <c>s12</c>'s powers hold 1–9, which include Iron
    /// Railroad Bridge and <em>not</em> Compound Steam Engine. Between them they
    /// author 137 links and <b>not one hill end</b>, while <c>s9</c> does rail a
    /// swamp.</item>
    /// <item><c>s3</c>'s powers hold unequal sets of 9, 13 and 14; its two hill
    /// ends both fall to powers holding at least thirteen.</item>
    /// <item><b>Nobody in the corpus holds Dynamite</b> (position 23, against a
    /// maximum of 21), and <b>no scenario rails a single mountain.</b></item>
    /// </list>
    /// That last pair is the striking one: the only terrain needing a technology
    /// no shipped power has is the only terrain no shipped scenario builds on.
    /// <para>
    /// Like every other gate here, <b>this governs building and never
    /// authoring</b> — a scenario may lay track wherever it likes and the
    /// importer must take it. Nothing validates against this; the count is
    /// measured so that a mistaken transcription would announce itself. Two of
    /// the readings are inferences the corpus cannot separate from the
    /// alternative — fertile hills (only <c>s1</c> rails them, and it holds the
    /// gate anyway) and towns — and both are flagged in
    /// <c>docs/formulas/engineer.md</c>.
    /// </para>
    /// </remarks>
    [Fact]
    public void EveryRailedCellInTheCorpusIsOneItsOwnerCouldHaveBuilt()
    {
        var directory = Environment.GetEnvironmentVariable("IMPERIALISM_SCENARIO_DIR");
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        var permitted = 0;
        var beyond = new List<string>();
        var byTerrain = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var mapPath in Directory.GetFiles(directory, "*.map").OrderBy(static path => path))
        {
            var key = Path.GetFileNameWithoutExtension(mapPath);
            var scenarioPath = Path.Combine(directory, $"{key}.scn");
            if (key == "s0" || !File.Exists(scenarioPath))
            {
                continue;
            }

            var document = LegacyWorldConverter.Convert(
                LegacyMapCodec.Decode(File.ReadAllBytes(mapPath), MapFormatProfile.Imperialism1),
                LegacyScenarioCodec.Decode(File.ReadAllBytes(scenarioPath)),
                null,
                $"rail-gate-{key}").Document!;
            var scenario = document.Scenarios[0];
            var gates = document.Terrains.ToDictionary(
                static item => item.Key,
                static item => item.Rail?.RequiredTechnology,
                StringComparer.Ordinal);
            var owners = scenario.ProvinceOwners.ToDictionary(
                static item => item.Province, static item => item.Country, StringComparer.Ordinal);
            var known = scenario.CountryTechnologies
                .GroupBy(static item => item.Country, StringComparer.Ordinal)
                .ToDictionary(
                    static group => group.Key,
                    static group => group.Select(static item => item.Technology)
                        .ToHashSet(StringComparer.Ordinal),
                    StringComparer.Ordinal);
            var starting = document.StartingDefaults!.Technologies.ToHashSet(StringComparer.Ordinal);
            var fairStart = scenario.DefaultStartCountries.ToHashSet(StringComparer.Ordinal);

            foreach (var link in scenario.Rails)
            {
                foreach (var index in new[] { link.First, link.Second })
                {
                    var cell = document.Map.Cells[index];
                    byTerrain[cell.Terrain] = byTerrain.GetValueOrDefault(cell.Terrain) + 1;
                    if (cell.Region.Province is not { } province ||
                        owners.GetValueOrDefault(province) is not { } owner)
                    {
                        continue;
                    }

                    if (gates.GetValueOrDefault(cell.Terrain) is not { } gate)
                    {
                        beyond.Add($"{key} cell {index} on {cell.Terrain} carries no rail at all");
                        continue;
                    }

                    if ((fairStart.Contains(owner) && starting.Contains(gate)) ||
                        (known.TryGetValue(owner, out var held) && held.Contains(gate)))
                    {
                        permitted++;
                        continue;
                    }

                    beyond.Add($"{key} cell {index} on {cell.Terrain} needs {gate}, {owner} lacks it");
                }
            }
        }

        // Setting the variable is a declaration that the corpus is there, so
        // finding nothing to check is a broken setup rather than a pass.
        Assert.Equal(1140, permitted);
        Assert.Empty(beyond);

        // Not vacuous: the gated ground is genuinely built on, and the one
        // terrain nobody could build on is the one nobody did.
        Assert.Equal(31, byTerrain["terrain.hill"]);
        Assert.Equal(13, byTerrain["terrain.wool-hill"]);
        Assert.Equal(4, byTerrain["terrain.swamp"]);
        Assert.False(byTerrain.ContainsKey("terrain.mountain"));
    }

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
    /// **Every ship in the corpus is a hull its owner could have built.** 142 records,
    /// 307 ships, zero contradictions — the same falsification method that validated the
    /// <c>tech</c> ids, and the check `wanted-values.md` promised before the ship array
    /// order was known.
    /// </summary>
    /// <remarks>
    /// **This is the check that pinned 1-based indexing**, back when the order was not
    /// known: read as 0-based it puts a Clipper — which needs Streamlined Hulls — in an
    /// 1816 skirmish whose powers hold nothing, plus five more in <c>s13</c> and
    /// <c>s14</c>. Nine contradictions against zero.
    /// <para>
    /// It is worth keeping now that the order is recovered, because the two agree without
    /// either having been fitted to the other: the check requires types 1–4 to be ungated,
    /// and the executable's first four are Trader, Indiaman, Frigate and Ship-of-the-Line.
    /// <b>If this count moves, either the array or the technology table moved.</b>
    /// </para>
    /// <para>
    /// Unlike the development ladder there are **no exceptions at all**, which is a
    /// stronger result than 380/4: a scenario may author a development level past what its
    /// owner could build, and no scenario authors a hull past it.
    /// </para>
    /// <para>
    /// <b>Provenance of the numbers, because they are not all the same kind.</b> The
    /// per-scenario row for <c>s1</c> was measured against the file; the 142/307 totals are
    /// the corpus figures <c>docs/formulas/trade.md</c> recorded when the 1-based reading
    /// was established, and they are transcribed here rather than re-measured — this
    /// environment holds only <c>s1</c>. If a full corpus disagrees with them, believe the
    /// corpus.
    /// </para>
    /// </remarks>
    [Fact]
    public void EveryShipInTheCorpusIsAHullItsOwnerCouldHaveBuilt()
    {
        var directory = Environment.GetEnvironmentVariable("IMPERIALISM_SCENARIO_DIR");
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        // Measured. s1 is the only scenario fielding a gated hull at all — the
        // Paddlewheeler and the Raider both need Paddlewheels — which is what stops this
        // check being vacuous, and it is 1882 holding 21 technologies, so it passes.
        var expected = new Dictionary<string, (int Records, long Hulls)>(StringComparer.Ordinal)
        {
            ["s1"] = (29, 59),
        };

        var records = 0;
        var hulls = 0L;
        var beyond = new List<string>();
        var byType = new SortedDictionary<string, int>(StringComparer.Ordinal);

        foreach (var mapPath in Directory.GetFiles(directory, "*.map").OrderBy(static path => path))
        {
            var key = Path.GetFileNameWithoutExtension(mapPath);
            var scenarioPath = Path.Combine(directory, $"{key}.scn");
            if (key == "s0" || !File.Exists(scenarioPath))
            {
                continue;
            }

            var result = LegacyWorldConverter.Convert(
                LegacyMapCodec.Decode(File.ReadAllBytes(mapPath), MapFormatProfile.Imperialism1),
                LegacyScenarioCodec.Decode(File.ReadAllBytes(scenarioPath)),
                null,
                $"fleet-{key}");
            Assert.True(result.Success);

            var document = result.Document!;
            var gates = document.ShipTypes.ToDictionary(
                static item => item.Key,
                static item => item.RequiredTechnology,
                StringComparer.Ordinal);
            var held = document.Scenarios[0].CountryTechnologies
                .GroupBy(static item => item.Country, StringComparer.Ordinal)
                .ToDictionary(
                    static group => group.Key,
                    static group => group.Select(static item => item.Technology)
                        .ToHashSet(StringComparer.Ordinal),
                    StringComparer.Ordinal);
            var starting = document.StartingDefaults!.Technologies;

            foreach (var ship in document.Scenarios[0].Ships)
            {
                records++;
                hulls += ship.Count;
                byType[ship.Type] = byType.GetValueOrDefault(ship.Type) + 1;

                var gate = gates[ship.Type];
                if (gate is null ||
                    starting.Contains(gate) ||
                    (held.TryGetValue(ship.Country, out var known) && known.Contains(gate)))
                {
                    continue;
                }

                beyond.Add($"{key} {ship.Country} holds {ship.Type}, which needs {gate}");
            }

            if (expected.TryGetValue(key, out var counts))
            {
                Assert.Equal(
                    counts,
                    (document.Scenarios[0].Ships.Length,
                        document.Scenarios[0].Ships.Sum(static item => item.Count)));
            }
        }

        // The finding itself first, so a partial corpus still reports it rather than
        // failing on the completeness guard below and saying nothing.
        Assert.Empty(beyond);

        // Not vacuous: gated hulls are genuinely present, so the check has something to
        // catch. The Paddlewheeler and the Raider are the two that need Paddlewheels, and
        // only s1 — in 1882, holding 21 technologies — fields either.
        Assert.True(
            byType.GetValueOrDefault("ship.paddlewheeler") + byType.GetValueOrDefault("ship.raider") > 0,
            $"No gated hull in the corpus, so this check proves nothing: {string.Join(", ", byType)}");

        // Setting the variable is a declaration that the whole corpus is there, so a
        // partial one is a broken setup rather than a pass — the same guard the other
        // corpus checks in this file use, and it fires for the same reason they do.
        Assert.Equal(142, records);
        Assert.Equal(307, hulls);
    }

    /// <summary>
    /// **A <c>year</c> record is an offset from 1815, not an absolute year**, and
    /// this pins the epoch against the corpus. The importer used to pass the field
    /// through verbatim, which nothing noticed because nothing read the year until
    /// technology gained an arrival date.
    /// </summary>
    /// <remarks>
    /// The epoch comes from the scenarios' own briefing text: <c>s1.inf</c> is
    /// "Naval Competition 1882" against a field of 67, and <c>s3.inf</c> is
    /// "Unification Movements 1848-1890" against 33. Both are 1815 + field exactly,
    /// which is also the manual's campaign start.
    /// </remarks>
    [Fact]
    public void AScenarioYearIsAnOffsetFrom1815()
    {
        var directory = Environment.GetEnvironmentVariable("IMPERIALISM_SCENARIO_DIR");
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        var years = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var mapPath in Directory.GetFiles(directory, "*.map").OrderBy(static path => path))
        {
            var key = Path.GetFileNameWithoutExtension(mapPath);
            var scenarioPath = Path.Combine(directory, $"{key}.scn");
            if (key == "s0" || !File.Exists(scenarioPath))
            {
                continue;
            }

            var document = LegacyWorldConverter.Convert(
                LegacyMapCodec.Decode(File.ReadAllBytes(mapPath), MapFormatProfile.Imperialism1),
                LegacyScenarioCodec.Decode(File.ReadAllBytes(scenarioPath)),
                null,
                $"year-{key}").Document!;
            years[key] = document.Scenarios[0].StartingYear;
        }

        Assert.Equal(10, years.Count);

        // The two the briefings name outright.
        Assert.Equal(1882, years["s1"]);
        Assert.Equal(1848, years["s3"]);
        Assert.Equal(1820, years["s5"]);

        // And the rest, so a change to the epoch shows up everywhere at once. The
        // three skirmishes carry field 1, so a skirmish starts in 1816 rather than
        // 1815 — what the data says, not rounded to the manual's campaign year.
        Assert.Equal(1826, years["s9"]);
        Assert.Equal(1825, years["s12"]);
        Assert.Equal(1820, years["s13"]);
        Assert.Equal(1820, years["s14"]);
        Assert.Equal(1816, years["s10"]);
        Assert.Equal(1816, years["s11"]);
        Assert.Equal(1816, years["s15"]);
    }

    /// <summary>
    /// The arrival years against what each scenario grants. **Measured and not
    /// enforced**: the manual calls its dates "approximate" and the standing rule
    /// is that a scenario may author anything, so this is expected to fire.
    /// </summary>
    /// <remarks>
    /// It fires much less than expected, and that is the finding. Three of the four
    /// dated missions grant **nothing** that has not yet arrived — <c>s1</c> in 1882
    /// holds 21 of the 27 available, <c>s3</c> in 1848 holds 14 of 16, and <c>s9</c>
    /// in 1826 holds 9 of exactly 9. That last one sits on the boundary: Spinning
    /// Jenny and Paddlewheels both arrive in 1826, and <c>s9</c> holds both and
    /// nothing later.
    /// <para>
    /// The two that do overshoot are each **one year** short: <c>s12</c> in 1825
    /// holds two technologies arriving in 1826, and <c>s13</c>/<c>s14</c>/<c>s5</c>
    /// in 1820 hold three arriving in 1821. For dates the manual calls approximate,
    /// against an epoch derived from two briefing paragraphs, that is a much tighter
    /// agreement than a scenario's authoring liberty would predict — so it
    /// corroborates the arrival years and the epoch together.
    /// </para>
    /// <para>
    /// <b>It cannot separate the two candidate orderings.</b> Positions 4–7 all
    /// arrive in 1821, so permuting them among themselves changes no scenario's
    /// count. The third check, like the other two, is silent on the question.
    /// </para>
    /// </remarks>
    [Fact]
    public void HowMuchTheCorpusGrantsAheadOfItsArrivalDates()
    {
        var directory = Environment.GetEnvironmentVariable("IMPERIALISM_SCENARIO_DIR");
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        var early = new Dictionary<string, int>(StringComparer.Ordinal);
        var granted = 0;
        foreach (var mapPath in Directory.GetFiles(directory, "*.map").OrderBy(static path => path))
        {
            var key = Path.GetFileNameWithoutExtension(mapPath);
            var scenarioPath = Path.Combine(directory, $"{key}.scn");
            if (key == "s0" || !File.Exists(scenarioPath))
            {
                continue;
            }

            var document = LegacyWorldConverter.Convert(
                LegacyMapCodec.Decode(File.ReadAllBytes(mapPath), MapFormatProfile.Imperialism1),
                LegacyScenarioCodec.Decode(File.ReadAllBytes(scenarioPath)),
                null,
                $"arrival-{key}").Document!;
            var scenario = document.Scenarios[0];
            var arrival = document.Technologies.ToDictionary(
                static item => item.Key, static item => item.AvailableFrom, StringComparer.Ordinal);

            granted += scenario.CountryTechnologies.Length;
            early[key] = scenario.CountryTechnologies
                .Count(item => arrival[item.Technology] > scenario.StartingYear);
        }

        // Every tech record in the corpus, s5 included: it is a generated world
        // rather than a shipped mission, but it is dated like one.
        Assert.Equal(491, granted);

        // Nothing early at all in the three latest missions.
        Assert.Equal(0, early["s1"]);
        Assert.Equal(0, early["s3"]);
        Assert.Equal(0, early["s9"]);

        // And one year early in the two earliest, across every power they equip.
        Assert.Equal(14, early["s12"]);
        Assert.Equal(21, early["s13"]);
        Assert.Equal(21, early["s14"]);
        Assert.Equal(21, early["s5"]);

        // The skirmishes grant nothing, so they can be neither early nor late.
        foreach (var key in new[] { "s10", "s11", "s15" })
        {
            Assert.Equal(0, early[key]);
        }
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
    /// A Prospector searches, an Engineer builds, and the other five improve.
    /// The work kind is what lets one order type mean several things, so it is
    /// pinned per civilian rather than assumed from the name.
    /// </summary>
    [Fact]
    public void OnlyTheProspectorProspectsAndOnlyTheEngineerBuilds()
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
        Assert.Equal(CivilianWorkKind.Construct, byKey["civilian.engineer"]);
        Assert.All(
            byKey.Where(static item =>
                item.Key is not ("civilian.prospector" or "civilian.engineer")),
            item => Assert.Equal(CivilianWorkKind.Improve, item.Value));
    }

    /// <summary>
    /// **Three turns, from observed play.** This used to be 1 and the one number
    /// in the Development phase with nothing behind it; moving it moved every
    /// table the soak publishes, so it is pinned here to make a change to it a
    /// deliberate act.
    /// </summary>
    /// <remarks>
    /// The observation is of an iron mine. Giving the Prospector and the
    /// Engineer the same duration is extrapolation, and this test is where that
    /// would be relaxed per type. See <c>docs/formulas/development.md</c>.
    /// </remarks>
    [Fact]
    public void ACiviliansWorkTakesThreeTurns()
    {
        var result = LegacyWorldConverter.Convert(
            CreateMap(2, 1, LandCell(0, 0), OceanCell()),
            new ScenarioDocument(
            [
                Record("year", 1815),
                NameRecord("cnam", 0, "Country"),
                NameRecord("pnam", 0, "Province"),
            ]),
            null,
            "work-turns-map");

        Assert.True(result.Success, result.Report.ToHumanReadable());
        Assert.All(
            result.Document!.CivilianTypes,
            static item => Assert.Equal(3, item.WorkTurns));
    }

    /// <summary>
    /// What an improvement costs, indexed by the level being reached. Index 0 is
    /// never used — nothing is improved *to* level zero — and the climb is steep
    /// enough that a Level III tile costs thirty times what opening it did.
    /// </summary>
    /// <remarks>
    /// The owner's recollection from play; the manual implies the cost exists
    /// and prints no figure. Pinned so that changing it is a deliberate act.
    /// </remarks>
    [Fact]
    public void ImprovementIsPricedPerRung()
    {
        var result = LegacyWorldConverter.Convert(
            CreateMap(2, 1, LandCell(0, 0), OceanCell()),
            new ScenarioDocument(
            [
                Record("year", 1815),
                NameRecord("cnam", 0, "Country"),
                NameRecord("pnam", 0, "Province"),
            ]),
            null,
            "improvement-price-map");

        Assert.True(result.Success, result.Report.ToHumanReadable());
        Assert.Equal(
            [0, 100, 1000, 3000],
            result.Document!.Improvement!.CashCostByDevelopmentLevel);
    }

    /// <summary>
    /// The rail gate per terrain, from the manual's Benefits of Technology
    /// Table. Ocean carries none ever; the rest divide four ways.
    /// </summary>
    /// <remarks>
    /// Two of these are inferences rather than transcriptions and are flagged in
    /// <c>docs/formulas/engineer.md</c>: Fertile Hills takes the hills gate, and
    /// towns and capitals take the plains one.
    /// </remarks>
    [Fact]
    public void EachTerrainCarriesRailOnTheManualsTerms()
    {
        var scenario = new ScenarioDocument(
        [
            Record("year", 1815),
            NameRecord("cnam", 0, "Country"),
            NameRecord("pnam", 0, "Province"),
        ]);

        // Every terrain code on the map, because the importer emits definitions
        // only for the ground a world actually contains.
        var cells = Enumerable.Range(0, 17)
            .Select(static code => code == 0
                ? OceanCell()
                : LandCell(0, 0) with { Terrain = (byte)code })
            .ToArray();

        var result = LegacyWorldConverter.Convert(
            CreateMap(17, 1, cells), scenario, null, "rail-gate-map");

        Assert.True(result.Success, result.Report.ToHumanReadable());
        var byKey = result.Document!.Terrains
            .ToDictionary(static item => item.Key, static item => item.Rail?.RequiredTechnology);

        Assert.Null(byKey["terrain.ocean"]);
        foreach (var key in new[]
        {
            "terrain.clear", "terrain.cotton", "terrain.cattle-ranch", "terrain.horse-ranch",
            "terrain.grain-farm", "terrain.orchard", "terrain.desert", "terrain.tundra",
            "terrain.forest", "terrain.scrub-forest", "terrain.town", "terrain.capital",
        })
        {
            Assert.Equal("technology.high-pressure-steam-engine", byKey[key]);
        }

        Assert.Equal("technology.iron-railroad-bridge", byKey["terrain.swamp"]);
        Assert.Equal("technology.compound-steam-engine", byKey["terrain.wool-hill"]);
        Assert.Equal("technology.compound-steam-engine", byKey["terrain.hill"]);
        Assert.Equal("technology.dynamite", byKey["terrain.mountain"]);
    }

    /// <summary>
    /// The two structure prices. Pinned so that changing them is a deliberate
    /// act, because both are recollection. **Rail is not among them any more**:
    /// the price list charges by the ground, so rail is priced on the terrain and
    /// this block must not carry version 17's flat figure at all.
    /// </summary>
    [Fact]
    public void ConstructionIsPricedInCashAndAPortCostsMoreThanADepot()
    {
        var result = LegacyWorldConverter.Convert(
            CreateMap(2, 1, LandCell(0, 0), OceanCell()),
            new ScenarioDocument(
            [
                Record("year", 0),
                NameRecord("cnam", 0, "Country"),
                NameRecord("pnam", 0, "Province"),
            ]),
            null,
            "construction-price-map");

        Assert.True(result.Success, result.Report.ToHumanReadable());
        var construction = result.Document!.Construction!;
        Assert.Equal((1500L, 2000L), (construction.DepotCashCost, construction.PortCashCost));
        Assert.Null(construction.RailCashCost);

        // The manual's one statement about either.
        Assert.True(construction.PortCashCost > construction.DepotCashCost);
    }

    /// <summary>
    /// Rail's price, per terrain, from the price list: 100 for plains, farm and
    /// desert and the grounds that plainly go with them, 150 for tundra and either
    /// forest, 200 for hills, 300 for swamp.
    /// </summary>
    /// <remarks>
    /// **This is a guess becoming an observation.** Version 17 priced a tile of
    /// track at a flat 500 and labelled it "a guess. Nothing supports it at all".
    /// <para>
    /// Mountains are the one exception and the one guess left: the list does not
    /// price them, so they take swamp's price rather than a fifth invented number.
    /// </para>
    /// </remarks>
    [Fact]
    public void RailIsPricedByTheGroundItCrosses()
    {
        // Every terrain code on the map, because the importer emits definitions
        // only for the ground a world actually contains.
        var cells = Enumerable.Range(0, 17)
            .Select(static code => code == 0
                ? OceanCell()
                : LandCell(0, 0) with { Terrain = (byte)code })
            .ToArray();

        var result = LegacyWorldConverter.Convert(
            CreateMap(17, 1, cells),
            new ScenarioDocument(
            [
                Record("year", 0),
                NameRecord("cnam", 0, "Country"),
                NameRecord("pnam", 0, "Province"),
            ]),
            null,
            "rail-price-map");

        Assert.True(result.Success, result.Report.ToHumanReadable());
        var byKey = result.Document!.Terrains.ToDictionary(
            static item => item.Key, static item => item.Rail?.CashCost, StringComparer.Ordinal);

        foreach (var key in new[]
        {
            "terrain.clear", "terrain.cotton", "terrain.cattle-ranch", "terrain.horse-ranch",
            "terrain.grain-farm", "terrain.orchard", "terrain.desert", "terrain.town",
            "terrain.capital",
        })
        {
            Assert.Equal(100L, byKey[key]);
        }

        Assert.Equal(150L, byKey["terrain.tundra"]);
        Assert.Equal(150L, byKey["terrain.forest"]);
        Assert.Equal(150L, byKey["terrain.scrub-forest"]);
        Assert.Equal(200L, byKey["terrain.wool-hill"]);
        Assert.Equal(200L, byKey["terrain.hill"]);
        Assert.Equal(300L, byKey["terrain.swamp"]);

        // Unpriced by the list; swamp's figure rather than an invented one.
        Assert.Equal(300L, byKey["terrain.mountain"]);

        // Ocean carries no line, so it needs no price.
        Assert.Null(byKey["terrain.ocean"]);
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
