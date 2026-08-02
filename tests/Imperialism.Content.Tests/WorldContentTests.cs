using System.Text;
using Imperialism.Content;
using Imperialism.Core;
using Xunit;

namespace Imperialism.Content.Tests;

public sealed class WorldContentTests
{
    [Fact]
    public void ContentAssemblyDependsOnCoreButNotLegacyFormatsOrGodot()
    {
        var references = typeof(WorldContentDocument).Assembly.GetReferencedAssemblies();

        Assert.Contains(references, static reference => reference.Name == "Imperialism.Core");
        Assert.DoesNotContain(references, static reference =>
            reference.Name == "Imperialism.Formats" ||
            reference.Name?.StartsWith("Godot", StringComparison.Ordinal) == true);
    }

    [Fact]
    public void CanonicalJsonRoundTripsSemanticallyAndByteExactly()
    {
        var document = CreateValidDocument();

        var first = WorldContentCodec.Encode(document);
        var decoded = WorldContentCodec.Decode(first);
        var second = WorldContentCodec.Encode(decoded);

        Assert.Equal(first, second);
        Assert.Equal((byte)'\n', first[^1]);
        Assert.DoesNotContain((byte)'\r', first);
        Assert.False(first.AsSpan().StartsWith(new byte[] { 0xef, 0xbb, 0xbf }));
        Assert.Contains("République 世界", Encoding.UTF8.GetString(first));
        Assert.Contains("\"first\": \"eastUpper\"", Encoding.UTF8.GetString(first));
    }

    [Fact]
    public void StableKeysCompileToDenseTypedIdsAndRemainBidirectional()
    {
        var compiled = WorldContentCompiler.Compile(CreateValidDocument());
        var world = compiled.World;

        Assert.Equal(new TerrainId(0), world.Map[new CellIndex(0)].Terrain);
        Assert.Equal(new ResourceId(2), world.Map[new CellIndex(0)].Resources[2]);
        Assert.Equal(new CommodityId(2), world.Map.Resources[2].Commodity);
        Assert.Equal(CommodityCategory.Material, world.Commodities[3].Category);
        Assert.Equal(new CountryId(1), world.Scenario.InitialProvinceOwners[0]);
        Assert.Equal("empire.a", compiled.Catalog.GetKey(new CountryId(1)));
        Assert.Equal(new CountryId(0), compiled.Catalog.GetCountryId("empire.b"));
        Assert.Equal(new ProvinceId(1), compiled.Catalog.GetProvinceId("province.east"));
        Assert.Equal(new CommodityId(1), compiled.Catalog.GetCommodityId("commodity.grain"));
        Assert.Equal("commodity.steel", compiled.Catalog.GetKey(new CommodityId(3)));
        Assert.Equal(
            new ProductionFacilityId(0),
            compiled.Catalog.GetProductionFacilityId("facility.steel-mill"));
        Assert.Equal(
            "recipe.steel",
            compiled.Catalog.GetKey(new ProductionRecipeId(0)));
        Assert.Equal(6, world.Scenario.InitialProductionCapacities[0].Quantity);
        Assert.Throws<KeyNotFoundException>(() => compiled.Catalog.GetTerrainId("terrain.missing"));
    }

    [Fact]
    public void OnePackageCompilesMultipleScenariosOverTheSameMap()
    {
        var document = CreateValidDocument();
        document.Scenarios =
        [
            document.Scenarios[0],
            new ScenarioContentDocument
            {
                Key = "scenario.alternate",
                Name = "Alternate Start",
                StartingYear = 1882,
                ProvinceOwners =
                [
                    new ProvinceOwnerContent { Province = "province.west", Country = "empire.b" },
                    new ProvinceOwnerContent { Province = "province.east", Country = "empire.a" },
                ],
            },
        ];

        var package = WorldContentCompiler.CompilePackage(document);
        var first = package.GetWorld("scenario.modern-start");
        var alternate = package.GetWorld("scenario.alternate");

        Assert.Equal("map.demo", package.MapKey);
        Assert.Equal(["scenario.modern-start", "scenario.alternate"], package.ScenarioKeys);
        Assert.Same(first.Map, alternate.Map);
        Assert.Equal(1815, first.Scenario.StartingYear);
        Assert.Equal(1882, alternate.Scenario.StartingYear);
        Assert.NotEqual(
            first.Scenario.InitialProvinceOwners[0],
            alternate.Scenario.InitialProvinceOwners[0]);
        Assert.Throws<ContentValidationException>(() => WorldContentCompiler.Compile(document));
        Assert.Throws<KeyNotFoundException>(() => package.GetWorld("scenario.missing"));

        var reloaded = WorldContentCodec.DecodeAndCompilePackage(WorldContentCodec.Encode(document));
        Assert.Equal(package.ScenarioKeys, reloaded.ScenarioKeys);
        Assert.Same(
            reloaded.GetWorld("scenario.modern-start").Map,
            reloaded.GetWorld("scenario.alternate").Map);
    }

    [Fact]
    public void CompiledWorldSeparatesGeographyFromScenarioState()
    {
        var compiled = WorldContentCompiler.Compile(CreateValidDocument());
        var rail = new CellLink(new CellIndex(0), new CellIndex(1));
        var state = new WorldState(compiled.World);

        Assert.Equal(
            new RiverPath(RiverEndpoint.EastUpper, RiverEndpoint.WestLower),
            compiled.World.Map.Cells[4].River);
        Assert.True(state.HasRail(rail));
        Assert.Equal(new CellIndex(0), state.GetCountryCapital(new CountryId(1)));
        Assert.Equal(12, state.GetAvailableQuantity(new CountryId(1), new CommodityId(1)));

        state.RemoveRail(rail);
        state.SetCountryCapital(new CountryId(1), null);

        Assert.Contains(rail, compiled.World.Scenario.InitialRailLinks);
        Assert.Contains(
            new CountryCapital(new CountryId(1), new CellIndex(0)),
            compiled.World.Scenario.InitialCountryCapitals);
    }

    [Fact]
    public void CompiledWorldDoesNotAliasEditableDocumentArrays()
    {
        var document = CreateValidDocument();
        var compiled = WorldContentCompiler.Compile(document);

        document.Map.Cells[0].Terrain = "terrain.ocean";
        document.Countries[0].Name = "Changed";
        document.Commodities[0].Name = "Changed";
        document.Resources[0].Commodity = "commodity.steel";

        Assert.Equal(new TerrainId(0), compiled.World.Map[new CellIndex(0)].Terrain);
        Assert.Equal("République 世界", compiled.World.Countries[0].Name);
        Assert.Equal("Coal", compiled.World.Commodities[0].Name);
        Assert.Equal(new CommodityId(0), compiled.World.Map.Resources[0].Commodity);
    }

    [Fact]
    public void LoadAndSaveUseModernExtensionWithoutLegacyEncodingLimits()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}{WorldContentCodec.FileExtension}");
        try
        {
            WorldContentCodec.Save(path, CreateValidDocument());
            var loaded = WorldContentCodec.Load(path);

            Assert.Equal("République 世界", loaded.Countries[0].Name);
            Assert.Equal(WorldContentCodec.CurrentVersion, loaded.FormatVersion);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void DimensionsLargerThanLegacyMapCompileWithoutSpecialProfile()
    {
        const int width = 129;
        const int height = 71;
        var document = new WorldContentDocument
        {
            TerrainKeys = ["terrain.plains"],
            Extraction = new ExtractionContentSettings { CatchmentRadius = 1 },
            Map = new MapContentDocument
            {
                Key = "map.large",
                Name = "Large Map",
                Width = width,
                Height = height,
                Cells = Enumerable.Range(0, width * height)
                    .Select(static _ => new CellContentDocument { Terrain = "terrain.plains" })
                    .ToArray(),
            },
            Scenarios =
            [
                new ScenarioContentDocument
                {
                    Key = "scenario.large",
                    Name = "Large",
                    StartingYear = 1815,
                },
            ],
        };

        var compiled = WorldContentCompiler.Compile(document);

        Assert.Equal(width, compiled.World.Map.Dimensions.Width);
        Assert.Equal(height, compiled.World.Map.Dimensions.Height);
        Assert.Equal(width * height, compiled.World.Map.Cells.Count);
    }

    [Fact]
    public void ModernPackageHasNoLegacyCountryOrProvinceCeiling()
    {
        const int provinceCount = 400;
        const int countryCount = 30;
        var document = new WorldContentDocument
        {
            TerrainKeys = ["terrain.plains"],
            Extraction = new ExtractionContentSettings { CatchmentRadius = 1 },
            Map = new MapContentDocument
            {
                Key = "map.expanded",
                Name = "Expanded Map",
                Width = provinceCount,
                Height = 1,
                Provinces = Enumerable.Range(0, provinceCount)
                    .Select(static index => new NamedContentDefinition
                    {
                        Key = $"province.{index}",
                        Name = $"Province {index}",
                    })
                    .ToArray(),
                Cells = Enumerable.Range(0, provinceCount)
                    .Select(static index => Cell("terrain.plains", province: $"province.{index}"))
                    .ToArray(),
            },
            Countries = Enumerable.Range(0, countryCount)
                .Select(static index => new NamedContentDefinition
                {
                    Key = $"country.{index}",
                    Name = $"Country {index}",
                })
                .ToArray(),
            Scenarios =
            [
                new ScenarioContentDocument
                {
                    Key = "scenario.expanded",
                    Name = "Expanded",
                    StartingYear = 2000,
                    ProvinceOwners = Enumerable.Range(0, provinceCount)
                        .Select(static index => new ProvinceOwnerContent
                        {
                            Province = $"province.{index}",
                            Country = $"country.{index % countryCount}",
                        })
                        .ToArray(),
                },
            ],
        };

        var compiled = WorldContentCompiler.Compile(document);

        Assert.Equal(provinceCount, compiled.World.Map.Provinces.Count);
        Assert.Equal(countryCount, compiled.World.Countries.Count);
    }

    [Fact]
    public void DecoderAcceptsCrLfAndTrailingWhitespaceButWriterCanonicalizesLf()
    {
        var canonical = Encoding.UTF8.GetString(WorldContentCodec.Encode(CreateValidDocument()));
        var platformText = canonical.Replace("\n", "\r\n", StringComparison.Ordinal) + " \r\n";

        var decoded = WorldContentCodec.Decode(Encoding.UTF8.GetBytes(platformText));
        var rewritten = WorldContentCodec.Encode(decoded);

        Assert.DoesNotContain((byte)'\r', rewritten);
        Assert.Equal((byte)'\n', rewritten[^1]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(999)]
    public void UnsupportedVersionsAreRejected(int version)
    {
        var document = CreateValidDocument();
        document.FormatVersion = version;

        var exception = Assert.Throws<ContentValidationException>(() =>
            WorldContentCompiler.Compile(document));

        Assert.Equal("formatVersion", exception.Path);
    }

    [Fact]
    public void CompilerRejectsVersionOneLabelOnVersionTwoShapedDocument()
    {
        var versionOneLabelOnVersionTwoContent = CreateValidDocument();
        versionOneLabelOnVersionTwoContent.FormatVersion = 1;

        var exception = Assert.Throws<ContentValidationException>(() =>
            WorldContentCompiler.Compile(versionOneLabelOnVersionTwoContent));

        Assert.Equal("formatVersion", exception.Path);
    }

    [Fact]
    public void DecoderMigratesVersionOneResourceKeysToExplicitRawCommodities()
    {
        var versionOne = """
            {
              "format": "imperialism-world",
              "formatVersion": 1,
              "terrainKeys": ["terrain.plains"],
              "resourceKeys": ["resource.grain", "deposit.crude-oil"],
              "map": {
                "key": "map.v1",
                "name": "Version One",
                "width": 1,
                "height": 1,
                "provinces": [],
                "seaZones": [],
                "cells": [
                  {
                    "terrain": "terrain.plains",
                    "region": {},
                    "resources": ["resource.grain", "deposit.crude-oil"],
                    "hasSettlementSite": false
                  }
                ]
              },
              "countries": [],
              "scenarios": [
                {
                  "key": "scenario.v1",
                  "name": "Version One",
                  "startingYear": 1815,
                  "provinceOwners": [],
                  "rails": [],
                  "capitals": []
                }
              ]
            }
            """;

        var migrated = WorldContentCodec.Decode(Encoding.UTF8.GetBytes(versionOne));
        var compiled = WorldContentCompiler.Compile(migrated);
        var encoded = Encoding.UTF8.GetString(WorldContentCodec.Encode(migrated));

        Assert.Equal(WorldContentCodec.CurrentVersion, migrated.FormatVersion);
        Assert.Null(migrated.ResourceKeys);
        Assert.Equal(
            ["commodity.grain", "commodity/from-resource/deposit.crude-oil"],
            migrated.Commodities.Select(static item => item.Key));
        Assert.All(migrated.Commodities, static item =>
            Assert.Equal(CommodityCategory.Raw, item.Category));
        Assert.Equal("commodity.grain", migrated.Resources[0].Commodity);
        Assert.Equal(
            new CommodityId(1),
            compiled.World.Map.Resources[1].Commodity);
        Assert.DoesNotContain("\"resourceKeys\"", encoded, StringComparison.Ordinal);
        Assert.Contains("\"formatVersion\": 5", encoded, StringComparison.Ordinal);
    }

    [Fact]
    public void VersionOneMigrationRejectsMixedSchemas()
    {
        var mixed = """
            {
              "format": "imperialism-world",
              "formatVersion": 1,
              "terrainKeys": [],
              "commodities": [{ "key": "commodity.grain", "name": "Grain", "category": "raw" }],
              "resources": [],
              "resourceKeys": [],
              "map": {},
              "countries": [],
              "scenarios": []
            }
            """;

        var exception = Assert.Throws<ContentValidationException>(() =>
            WorldContentCodec.Decode(Encoding.UTF8.GetBytes(mixed)));

        Assert.Equal("formatVersion", exception.Path);
    }

    [Fact]
    public void DecoderMigratesVersionTwoToEmptyVersionThreeProductionCollections()
    {
        var versionTwo = """
            {
              "format": "imperialism-world",
              "formatVersion": 2,
              "terrainKeys": ["terrain.plains"],
              "commodities": [],
              "resources": [],
              "map": {
                "key": "map.v2",
                "name": "Version Two",
                "width": 1,
                "height": 1,
                "provinces": [],
                "seaZones": [],
                "cells": [{ "terrain": "terrain.plains", "region": {}, "resources": [], "hasSettlementSite": false }]
              },
              "countries": [],
              "scenarios": [{
                "key": "scenario.v2",
                "name": "Version Two",
                "startingYear": 1815,
                "provinceOwners": [],
                "rails": [],
                "capitals": [],
                "initialInventory": []
              }]
            }
            """;

        var migrated = WorldContentCodec.Decode(Encoding.UTF8.GetBytes(versionTwo));

        Assert.Equal(WorldContentCodec.CurrentVersion, migrated.FormatVersion);
        Assert.Empty(migrated.ProductionFacilities);
        Assert.Empty(migrated.ProductionRecipes);
        Assert.Empty(migrated.Scenarios[0].ProductionCapacities);
    }

    [Fact]
    public void VersionTwoMigrationRejectsVersionThreeProductionData()
    {
        var document = CreateValidDocument();
        var json = Encoding.UTF8.GetString(WorldContentCodec.Encode(document))
            .Replace("\"formatVersion\": 5", "\"formatVersion\": 2", StringComparison.Ordinal);

        var exception = Assert.Throws<ContentValidationException>(() =>
            WorldContentCodec.Decode(Encoding.UTF8.GetBytes(json)));

        Assert.Equal("formatVersion", exception.Path);
    }

    [Fact]
    public void VersionOneMigrationReportsDuplicateResourceKeysAtTheirSourcePath()
    {
        var duplicateResources = """
            {
              "format": "imperialism-world",
              "formatVersion": 1,
              "terrainKeys": [],
              "resourceKeys": ["resource.grain", "resource.grain"],
              "map": {},
              "countries": [],
              "scenarios": []
            }
            """;

        var exception = Assert.Throws<ContentValidationException>(() =>
            WorldContentCodec.Decode(Encoding.UTF8.GetBytes(duplicateResources)));

        Assert.Equal("resourceKeys[1]", exception.Path);
    }

    [Fact]
    public void DecoderRejectsUnknownPropertiesAndMalformedJson()
    {
        var valid = Encoding.UTF8.GetString(WorldContentCodec.Encode(CreateValidDocument()));
        var unknown = valid.Replace(
            "\"formatVersion\": 5,",
            "\"formatVersion\": 5,\n  \"mystery\": true,",
            StringComparison.Ordinal);

        Assert.Throws<ContentValidationException>(() =>
            WorldContentCodec.Decode(Encoding.UTF8.GetBytes(unknown)));
        Assert.Throws<ContentValidationException>(() =>
            WorldContentCodec.Decode("{ not json"u8));
    }

    [Fact]
    public void DecoderRejectsUnknownOrNumericRiverEndpoints()
    {
        var valid = Encoding.UTF8.GetString(WorldContentCodec.Encode(CreateValidDocument()));
        var unknown = valid.Replace("\"eastUpper\"", "\"sideways\"", StringComparison.Ordinal);
        var numeric = valid.Replace("\"eastUpper\"", "1", StringComparison.Ordinal);

        Assert.Throws<ContentValidationException>(() =>
            WorldContentCodec.Decode(Encoding.UTF8.GetBytes(unknown)));
        Assert.Throws<ContentValidationException>(() =>
            WorldContentCodec.Decode(Encoding.UTF8.GetBytes(numeric)));
    }

    [Theory]
    [InlineData("Terrain.Plains")]
    [InlineData("-terrain")]
    [InlineData("terrain with spaces")]
    [InlineData("")]
    public void StableKeysUsePortableCanonicalSyntax(string key)
    {
        var document = CreateValidDocument();
        document.TerrainKeys[0] = key;

        Assert.Throws<ContentValidationException>(() => WorldContentCompiler.Compile(document));
    }

    [Fact]
    public void CompilerRejectsUnknownReferencesAndAmbiguousRegions()
    {
        var unknownTerrain = CreateValidDocument();
        unknownTerrain.Map.Cells[0].Terrain = "terrain.missing";
        AssertPath("map.cells[0].terrain", unknownTerrain);

        var ambiguousRegion = CreateValidDocument();
        ambiguousRegion.Map.Cells[0].Region.SeaZone = "sea.west";
        AssertPath("map.cells[0].region", ambiguousRegion);

        var unknownOwner = CreateValidDocument();
        unknownOwner.Scenarios[0].ProvinceOwners[0].Country = "empire.missing";
        AssertPath("scenarios[0].provinceOwners[0].country", unknownOwner);

        var unknownResourceCommodity = CreateValidDocument();
        unknownResourceCommodity.Resources[0].Commodity = "commodity.missing";
        AssertPath("resources[0].commodity", unknownResourceCommodity);
    }

    [Fact]
    public void CompilerValidatesCommodityDefinitionsAndInitialInventory()
    {
        var blankName = CreateValidDocument();
        blankName.Commodities[0].Name = " ";
        AssertPath("commodities[0].name", blankName);

        var invalidCategory = CreateValidDocument();
        invalidCategory.Commodities[0].Category = (CommodityCategory)200;
        AssertPath("commodities[0].category", invalidCategory);

        var duplicateCommodity = CreateValidDocument();
        duplicateCommodity.Commodities[1].Key = duplicateCommodity.Commodities[0].Key;
        AssertPath("commodities[1]", duplicateCommodity);

        var unknownInventoryCommodity = CreateValidDocument();
        unknownInventoryCommodity.Scenarios[0].InitialInventory[0].Commodity = "commodity.missing";
        AssertPath("scenarios[0].initialInventory[0].commodity", unknownInventoryCommodity);

        var zeroInventory = CreateValidDocument();
        zeroInventory.Scenarios[0].InitialInventory[0].Quantity = 0;
        AssertPath("scenarios[0].initialInventory[0].quantity", zeroInventory);

        var duplicateInventory = CreateValidDocument();
        duplicateInventory.Scenarios[0].InitialInventory =
        [
            duplicateInventory.Scenarios[0].InitialInventory[0],
            new InitialInventoryContent
            {
                Country = "empire.a",
                Commodity = "commodity.grain",
                Quantity = 1,
            },
        ];
        AssertPath("scenarios[0].initialInventory[1]", duplicateInventory);
    }

    [Fact]
    public void CompilerValidatesProductionDefinitionsAndCapacitiesWithPaths()
    {
        var missingFacility = CreateValidDocument();
        missingFacility.ProductionRecipes[0].Facility = "facility.missing";
        AssertPath("productionRecipes[0].facility", missingFacility);

        var zeroInput = CreateValidDocument();
        zeroInput.ProductionRecipes[0].Inputs[0].Quantity = 0;
        AssertPath("productionRecipes[0].inputs[0].quantity", zeroInput);

        var duplicateInput = CreateValidDocument();
        duplicateInput.ProductionRecipes[0].Inputs[1].Commodity = "commodity.coal";
        AssertPath("productionRecipes[0].inputs[1]", duplicateInput);

        var unlimitedCapacity = CreateValidDocument();
        unlimitedCapacity.ProductionFacilities[0].CapacityMode = ProductionCapacityMode.Unlimited;
        AssertPath("scenarios[0].productionCapacities[0].facility", unlimitedCapacity);
    }

    [Fact]
    public void CompilerRequiresExactlyOneOwnershipEntryPerProvince()
    {
        var missing = CreateValidDocument();
        missing.Scenarios[0].ProvinceOwners = [missing.Scenarios[0].ProvinceOwners[0]];
        AssertPath("scenarios[0].provinceOwners", missing);

        var duplicate = CreateValidDocument();
        duplicate.Scenarios[0].ProvinceOwners[1].Province = "province.west";
        AssertPath("scenarios[0].provinceOwners[1].province", duplicate);
    }

    [Fact]
    public void CompilerRejectsInvalidLinksAndCapitalSites()
    {
        var invalidRiver = CreateValidDocument();
        invalidRiver.Map.Cells[4].River = new RiverPathContent
        {
            First = RiverEndpoint.Source,
            Second = RiverEndpoint.Source,
        };
        AssertPath("map.cells[4].river", invalidRiver);

        var seaRail = CreateValidDocument();
        seaRail.Scenarios[0].Rails[0] = new CellLinkContent { First = 1, Second = 2 };
        AssertPath("scenarios[0]", seaRail);

        var nonUrbanCapital = CreateValidDocument();
        nonUrbanCapital.Scenarios[0].Capitals[0].Cell = 1;
        AssertPath("scenarios[0]", nonUrbanCapital);
    }

    [Fact]
    public void DecoderRejectsNullRequiredCollections()
    {
        var json = Encoding.UTF8.GetString(WorldContentCodec.Encode(CreateValidDocument()))
            .Replace(
                "\"terrainKeys\": [\n    \"terrain.plains\",\n    \"terrain.ocean\"\n  ]",
                "\"terrainKeys\": null",
                StringComparison.Ordinal);

        var exception = Assert.Throws<ContentValidationException>(() =>
            WorldContentCodec.Decode(Encoding.UTF8.GetBytes(json)));

        Assert.Equal("terrainKeys", exception.Path);
    }

    [Fact]
    public void DecoderMigratesVersionThreeToUndevelopedYieldsAndAOneTileCatchment()
    {
        var versionThree = """
            {
              "format": "imperialism-world",
              "formatVersion": 3,
              "terrainKeys": ["terrain.plains"],
              "commodities": [{ "key": "commodity.grain", "name": "Grain", "category": "raw" }],
              "resources": [{ "key": "resource.grain", "commodity": "commodity.grain" }],
              "productionFacilities": [],
              "productionRecipes": [],
              "map": {
                "key": "map.v3",
                "name": "Version Three",
                "width": 1,
                "height": 1,
                "provinces": [],
                "seaZones": [],
                "cells": [{ "terrain": "terrain.plains", "region": {}, "resources": [], "hasSettlementSite": false }]
              },
              "countries": [],
              "scenarios": [{
                "key": "scenario.v3",
                "name": "Version Three",
                "startingYear": 1815,
                "provinceOwners": [],
                "rails": [],
                "capitals": [],
                "initialInventory": [],
                "productionCapacities": []
              }]
            }
            """;

        var migrated = WorldContentCodec.Decode(Encoding.UTF8.GetBytes(versionThree));
        var compiled = WorldContentCompiler.Compile(migrated);

        // Version 3 knew no yields at all, so it lands on version 5's curve via
        // version 4's flat rate: one undeveloped, doubling per level.
        Assert.Equal(WorldContentCodec.CurrentVersion, migrated.FormatVersion);
        Assert.Equal(
            WorldContentCodec.SurfaceYieldByDevelopmentLevel,
            migrated.Resources[0].YieldByDevelopmentLevel);
        Assert.Equal(
            WorldContentCodec.DefaultCatchmentRadius,
            migrated.Extraction!.CatchmentRadius);
        Assert.Equal(1, compiled.World.Map.Resources[0].GetYield(0));
        Assert.Equal(8, compiled.World.Map.Resources[0].GetYield(3));
        Assert.Equal(1, compiled.World.Extraction.CatchmentRadius);
    }

    [Fact]
    public void AnOlderVersionCannotCarryDataItsSchemaNeverHad()
    {
        // Each migration step refuses a document labelled with the older version
        // while holding the newer version's fields, so a hand-edited version
        // number cannot smuggle state past the step meant to create it.
        var current = Encoding.UTF8.GetString(WorldContentCodec.Encode(CreateValidDocument()));

        var labelledThree = current.Replace(
            "\"formatVersion\": 5", "\"formatVersion\": 3", StringComparison.Ordinal);
        Assert.Equal(
            "formatVersion",
            Assert.Throws<ContentValidationException>(() =>
                WorldContentCodec.Decode(Encoding.UTF8.GetBytes(labelledThree))).Path);

        // Labelled 4 and carrying a version 5 curve. Technologies are stripped
        // first so this trips the curve guard rather than the technology one.
        var withoutTechnologies = CreateValidDocument();
        withoutTechnologies.Technologies = [];
        withoutTechnologies.Resources[2].RequiredTechnology = null;
        var labelledFour = Encoding.UTF8
            .GetString(WorldContentCodec.Encode(withoutTechnologies))
            .Replace("\"formatVersion\": 5", "\"formatVersion\": 4", StringComparison.Ordinal);
        Assert.Equal(
            "resources[0].yieldByDevelopmentLevel",
            Assert.Throws<ContentValidationException>(() =>
                WorldContentCodec.Decode(Encoding.UTF8.GetBytes(labelledFour))).Path);
    }

    [Fact]
    public void CompilerRequiresExtractionSettingsAndAUsableYieldCurve()
    {
        var missingSettings = CreateValidDocument();
        missingSettings.Extraction = null;
        AssertPath("extraction", missingSettings);

        var negativeRadius = CreateValidDocument();
        negativeRadius.Extraction = new ExtractionContentSettings { CatchmentRadius = -1 };
        AssertPath("extraction.catchmentRadius", negativeRadius);

        var emptyCurve = CreateValidDocument();
        emptyCurve.Resources[1].YieldByDevelopmentLevel = [];
        AssertPath("resources[1].yieldByDevelopmentLevel", emptyCurve);

        // Zero undeveloped is how a mine is expressed, so only an all-zero curve
        // is rejected.
        var barrenCurve = CreateValidDocument();
        barrenCurve.Resources[0].YieldByDevelopmentLevel = [0, 0, 0];
        AssertPath("resources[0].yieldByDevelopmentLevel", barrenCurve);

        var negativeYield = CreateValidDocument();
        negativeYield.Resources[0].YieldByDevelopmentLevel = [1, -3];
        AssertPath("resources[0].yieldByDevelopmentLevel", negativeYield);

        var legacyField = CreateValidDocument();
        legacyField.Resources[0].YieldPerTurn = 4;
        AssertPath("resources[0].yieldPerTurn", legacyField);

        var unknownTechnology = CreateValidDocument();
        unknownTechnology.Resources[0].RequiredTechnology = "technology.missing";
        AssertPath("resources[0].requiredTechnology", unknownTechnology);
    }

    [Fact]
    public void ScenariosCanSeedDevelopmentAndKnownTechnologies()
    {
        var document = CreateValidDocument();
        document.Scenarios[0].CellDevelopment = [new CellDevelopmentContent { Cell = 0, Level = 2 }];
        document.Scenarios[0].CountryTechnologies =
        [
            new CountryTechnologyContent
            {
                Country = document.Countries[0].Key,
                Technology = "technology.drilling",
            },
        ];

        var world = WorldContentCompiler.Compile(document).World;
        var state = new WorldState(world);

        Assert.Equal(2, state.GetCellDevelopment(new CellIndex(0)));
        Assert.True(state.HasTechnology(new CountryId(0), new TechnologyId(0)));

        var levelZero = CreateValidDocument();
        levelZero.Scenarios[0].CellDevelopment = [new CellDevelopmentContent { Cell = 0, Level = 0 }];
        AssertPath("scenarios[0].cellDevelopment[0].level", levelZero);
    }

    /// <summary>
    /// The Godot client ships exactly one world and loads it unconditionally, so
    /// a format bump that its migration path does not cover breaks the viewer
    /// with nothing else to catch it.
    /// </summary>
    [Fact]
    public void TheShippedDemoPackageStillCompilesAtTheCurrentVersion()
    {
        var repositoryRoot = FindRepositoryRoot();
        var demoPath = Path.Combine(
            repositoryRoot,
            "src",
            "Imperialism.Client",
            "demo",
            "demo.iworld");
        Assert.True(File.Exists(demoPath), $"Expected the client's demo world at {demoPath}.");

        var document = WorldContentCodec.Load(demoPath);
        var package = WorldContentCompiler.CompilePackage(document);

        Assert.Equal(WorldContentCodec.CurrentVersion, document.FormatVersion);
        Assert.NotEmpty(package.ScenarioKeys);
        Assert.All(
            package.ScenarioKeys,
            key => Assert.Equal(1, package.GetWorld(key).Extraction.CatchmentRadius));
        Assert.All(
            document.Resources,
            static resource => Assert.NotEmpty(resource.YieldByDevelopmentLevel));
        Assert.NotNull(document.Extraction);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null &&
            !File.Exists(Path.Combine(directory.FullName, "Imperialism.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory.FullName;
    }

    private static void AssertPath(string expected, WorldContentDocument document)
    {
        var exception = Assert.Throws<ContentValidationException>(() =>
            WorldContentCompiler.Compile(document));
        Assert.Equal(expected, exception.Path);
    }

    private static WorldContentDocument CreateValidDocument() => new()
    {
        TerrainKeys = ["terrain.plains", "terrain.ocean"],
        Commodities =
        [
            new CommodityContentDefinition
            {
                Key = "commodity.coal",
                Name = "Coal",
                Category = CommodityCategory.Raw,
            },
            new CommodityContentDefinition
            {
                Key = "commodity.grain",
                Name = "Grain",
                Category = CommodityCategory.Raw,
            },
            new CommodityContentDefinition
            {
                Key = "commodity.oil",
                Name = "Oil",
                Category = CommodityCategory.Raw,
            },
            new CommodityContentDefinition
            {
                Key = "commodity.steel",
                Name = "Steel",
                Category = CommodityCategory.Material,
            },
        ],
        Resources =
        [
            new ResourceContentDefinition
            {
                Key = "resource.coal",
                Commodity = "commodity.coal",
                YieldByDevelopmentLevel = [0, 2, 4, 8],
            },
            new ResourceContentDefinition
            {
                Key = "resource.grain",
                Commodity = "commodity.grain",
                YieldByDevelopmentLevel = [2, 4, 8, 16],
            },
            new ResourceContentDefinition
            {
                Key = "resource.oil",
                Commodity = "commodity.oil",
                YieldByDevelopmentLevel = [0, 2, 4, 8],
                RequiredTechnology = "technology.drilling",
            },
        ],
        Technologies =
        [
            new NamedContentDefinition { Key = "technology.drilling", Name = "Oil Drilling" },
        ],
        Extraction = new ExtractionContentSettings { CatchmentRadius = 1 },
        ProductionFacilities =
        [
            new ProductionFacilityContentDefinition
            {
                Key = "facility.steel-mill",
                Name = "Steel Mill",
                CapacityMode = ProductionCapacityMode.Limited,
            },
        ],
        ProductionRecipes =
        [
            new ProductionRecipeContentDefinition
            {
                Key = "recipe.steel",
                Name = "Steel",
                Facility = "facility.steel-mill",
                CapacityCost = 1,
                Inputs =
                [
                    new CommodityQuantityContent { Commodity = "commodity.coal", Quantity = 1 },
                    new CommodityQuantityContent { Commodity = "commodity.oil", Quantity = 1 },
                ],
                Outputs =
                [
                    new CommodityQuantityContent { Commodity = "commodity.steel", Quantity = 1 },
                ],
            },
        ],
        Map = new MapContentDocument
        {
            Key = "map.demo",
            Name = "Demonstration Map",
            Width = 3,
            Height = 2,
            Provinces =
            [
                new NamedContentDefinition { Key = "province.west", Name = "West" },
                new NamedContentDefinition { Key = "province.east", Name = "East" },
            ],
            SeaZones =
            [
                new NamedContentDefinition { Key = "sea.west", Name = "Western Sea" },
            ],
            Cells =
            [
                Cell("terrain.plains", province: "province.west", settlement: true,
                    resources: ["resource.coal", "resource.grain", "resource.oil"]),
                Cell("terrain.plains", province: "province.west"),
                Cell("terrain.ocean", seaZone: "sea.west"),
                Cell("terrain.plains", province: "province.east", settlement: true),
                Cell(
                    "terrain.plains",
                    province: "province.east",
                    river: new RiverPathContent
                    {
                        First = RiverEndpoint.EastUpper,
                        Second = RiverEndpoint.WestLower,
                    }),
                Cell("terrain.ocean", seaZone: "sea.west"),
            ],
        },
        Countries =
        [
            new NamedContentDefinition { Key = "empire.b", Name = "République 世界" },
            new NamedContentDefinition { Key = "empire.a", Name = "Empire A" },
        ],
        Scenarios =
        [
            new ScenarioContentDocument
            {
                Key = "scenario.modern-start",
                Name = "Modern Start",
                StartingYear = 1815,
                ProvinceOwners =
                [
                    new ProvinceOwnerContent { Province = "province.west", Country = "empire.a" },
                    new ProvinceOwnerContent { Province = "province.east", Country = "empire.b" },
                ],
                Rails = [new CellLinkContent { First = 0, Second = 1 }],
                Capitals =
                [
                    new CountryCapitalContent { Country = "empire.a", Cell = 0 },
                    new CountryCapitalContent { Country = "empire.b", Cell = 3 },
                ],
                InitialInventory =
                [
                    new InitialInventoryContent
                    {
                        Country = "empire.a",
                        Commodity = "commodity.grain",
                        Quantity = 12,
                    },
                ],
                ProductionCapacities =
                [
                    new InitialProductionCapacityContent
                    {
                        Country = "empire.a",
                        Facility = "facility.steel-mill",
                        Quantity = 6,
                    },
                ],
            },
        ],
    };

    private static CellContentDocument Cell(
        string terrain,
        string? province = null,
        string? seaZone = null,
        bool settlement = false,
        string[]? resources = null,
        RiverPathContent? river = null) => new()
        {
            Terrain = terrain,
            Region = new CellRegionContent { Province = province, SeaZone = seaZone },
            HasSettlementSite = settlement,
            Resources = resources ?? [],
            River = river,
        };
}
