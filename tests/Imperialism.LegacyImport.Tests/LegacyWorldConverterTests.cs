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
        Assert.Equal(1, result.Report.DeferredCounts["scenario.tag.rail"]);
        Assert.Equal(1, result.Report.DeferredCounts["scenario.tag.tech"]);
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
    public void DependencyDirectionKeepsFormatsIndependentFromModernLayers()
    {
        var importerReferences = typeof(LegacyWorldConverter).Assembly.GetReferencedAssemblies();
        var formatReferences = typeof(MapDocument).Assembly.GetReferencedAssemblies();

        Assert.Contains(importerReferences, static item => item.Name == "Imperialism.Content");
        Assert.Contains(importerReferences, static item => item.Name == "Imperialism.Formats");
        Assert.DoesNotContain(formatReferences, static item =>
            item.Name is "Imperialism.Core" or "Imperialism.Content" or "Imperialism.LegacyImport");
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

    private static ScenarioDocument Scenario(uint year) => new([Record("year", year)]);

    private static ScenarioRecord Record(string tag, params uint[] fields) => new(tag, fields);

    private static ScenarioRecord NameRecord(string tag, uint id, string name) => new(tag, [id], name);
}
