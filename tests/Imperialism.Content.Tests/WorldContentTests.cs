using System.Text;
using System.Text.RegularExpressions;
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
            Terrains = [Terrain("terrain.plains", "Plains")],
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
            Terrains = [Terrain("terrain.plains", "Plains")],
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
                .Select(static index => new CountryContentDefinition
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
    [InlineData(22)]
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
        Assert.Contains(CurrentVersionLabel, encoded, StringComparison.Ordinal);
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
        var json = Relabel(Encoding.UTF8.GetString(WorldContentCodec.Encode(document)), 2);

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
        Assert.Contains(CurrentVersionLabel, valid, StringComparison.Ordinal);
        var unknown = valid.Replace(
            $"{CurrentVersionLabel},",
            $"{CurrentVersionLabel},\n  \"mystery\": true,",
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
        document.Terrains[0].Key = key;

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
        var encoded = Encoding.UTF8.GetString(WorldContentCodec.Encode(CreateValidDocument()));
        var terrains = Regex.Match(encoded, "\"terrains\": \\[.*?\\n  \\],\\n", RegexOptions.Singleline);
        Assert.True(terrains.Success, "The encoder no longer writes a terrains block.");
        var json = encoded.Replace(terrains.Value, "\"terrains\": null,\n", StringComparison.Ordinal);

        var exception = Assert.Throws<ContentValidationException>(() =>
            WorldContentCodec.Decode(Encoding.UTF8.GetBytes(json)));

        Assert.Equal("terrains", exception.Path);
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
            WorldContentCodec.CultivatedYieldByDevelopmentLevel,
            migrated.Resources[0].YieldByDevelopmentLevel);
        Assert.Equal(
            WorldContentCodec.DefaultCatchmentRadius,
            migrated.Extraction!.CatchmentRadius);
        Assert.Equal(1, compiled.World.Map.Resources[0].GetYield(0));
        Assert.Equal(4, compiled.World.Map.Resources[0].GetYield(3));
        Assert.Equal(1, compiled.World.Extraction.CatchmentRadius);
    }

    [Fact]
    public void AnOlderVersionCannotCarryDataItsSchemaNeverHad()
    {
        // Each migration step refuses a document labelled with the older version
        // while holding the newer version's fields, so a hand-edited version
        // number cannot smuggle state past the step meant to create it.
        var current = Encoding.UTF8.GetString(WorldContentCodec.Encode(CreateValidDocument()));

        var labelledThree = Relabel(current, 3);
        Assert.Equal(
            "formatVersion",
            Assert.Throws<ContentValidationException>(() =>
                WorldContentCodec.Decode(Encoding.UTF8.GetBytes(labelledThree))).Path);

        // Labelled 4 and carrying a version 5 curve. Technologies are stripped
        // first so this trips the curve guard rather than the technology one.
        var withoutTechnologies = CreateValidDocument();
        withoutTechnologies.Technologies = [];
        withoutTechnologies.Resources[2].RequiredTechnology = null;
        var labelledFour = Relabel(
            Encoding.UTF8.GetString(WorldContentCodec.Encode(withoutTechnologies)), 4);
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
    public void PortsAndFishingCompileAndAreValidated()
    {
        var document = CreateValidDocument();
        document.Extraction!.PortFishing = new PortFishingContent
        {
            Commodity = "commodity.grain",
            YieldPerAdjacentWaterTile = 2,
        };
        document.Scenarios[0].Ports = [0];

        var world = WorldContentCompiler.Compile(document).World;
        var state = new WorldState(world);

        Assert.Equal(new CommodityId(1), world.Extraction.PortFishing!.Commodity);
        Assert.Equal(2, world.Extraction.PortFishing.YieldPerAdjacentWaterTile);
        Assert.True(state.HasPort(new CellIndex(0)));

        var unknownCommodity = CreateValidDocument();
        unknownCommodity.Extraction!.PortFishing = new PortFishingContent
        {
            Commodity = "commodity.missing",
            YieldPerAdjacentWaterTile = 1,
        };
        AssertPath("extraction.portFishing.commodity", unknownCommodity);

        var zeroYield = CreateValidDocument();
        zeroYield.Extraction!.PortFishing = new PortFishingContent
        {
            Commodity = "commodity.grain",
            YieldPerAdjacentWaterTile = 0,
        };
        AssertPath("extraction.portFishing.yieldPerAdjacentWaterTile", zeroYield);

        // A port in open water is not a port.
        var seaPort = CreateValidDocument();
        seaPort.Scenarios[0].Ports = [2];
        AssertPath("scenarios[0]", seaPort);
    }

    [Fact]
    public void VersionFiveMigrationRejectsVersionSixPortData()
    {
        var withPorts = CreateValidDocument();
        withPorts.Scenarios[0].Ports = [0];
        var json = Relabel(Encoding.UTF8.GetString(WorldContentCodec.Encode(withPorts)), 5);

        var exception = Assert.Throws<ContentValidationException>(() =>
            WorldContentCodec.Decode(Encoding.UTF8.GetBytes(json)));

        Assert.Equal("formatVersion", exception.Path);
    }

    [Fact]
    public void VersionEightMigrationPricesLabourAtTheRecipesInputTotal()
    {
        // The steel recipe takes one coal and one oil, so it costs two labour —
        // the rate the manual gives for the one recipe it prices outright.
        var json = Relabel(
            Encoding.UTF8.GetString(WorldContentCodec.Encode(CreateValidDocument())), 8);
        var withoutLabour = json.Replace(
            "\"labourCost\": 2,\n", string.Empty, StringComparison.Ordinal);
        Assert.DoesNotContain("labourCost", withoutLabour, StringComparison.Ordinal);

        var migrated = WorldContentCodec.Decode(Encoding.UTF8.GetBytes(withoutLabour));

        Assert.Equal(WorldContentCodec.CurrentVersion, migrated.FormatVersion);
        Assert.Equal(2, Assert.Single(migrated.ProductionRecipes).LabourCost);
    }

    [Fact]
    public void VersionEightMigrationRejectsAVersionNineLabourCost()
    {
        var json = Relabel(
            Encoding.UTF8.GetString(WorldContentCodec.Encode(CreateValidDocument())), 8);

        var exception = Assert.Throws<ContentValidationException>(() =>
            WorldContentCodec.Decode(Encoding.UTF8.GetBytes(json)));

        Assert.Equal("formatVersion", exception.Path);
    }

    [Fact]
    public void AFairStartCompilesAndAppliesToTheCountriesItNames()
    {
        var document = WithSkirmishDefaults(CreateValidDocument());

        var state = new WorldState(WorldContentCompiler.Compile(document).World);
        var country = new CountryId(0);

        Assert.Equal(2, state.GetProductionCapacity(country, new ProductionFacilityId(0)));
        Assert.Equal(7, state.GetTotalWorkers(country));
    }

    /// <summary>
    /// Version 13 renames <c>terrainKeys</c> to <c>terrains</c> and gives each
    /// entry attributes. A version 12 world had no way to improve anything, so
    /// every migrated terrain arrives unimprovable — a faithful reproduction of
    /// its old behaviour rather than a guess standing in for a missing value.
    /// </summary>
    [Fact]
    public void VersionTwelveMigratesToNamedUnimprovableTerrainAndNoCivilians()
    {
        var json = Relabel(Encoding.UTF8.GetString(WorldContentCodec.Encode(CreateValidDocument())), 12);
        Assert.Contains("terrainKeys", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"terrains\"", json, StringComparison.Ordinal);

        var migrated = WorldContentCodec.Decode(Encoding.UTF8.GetBytes(json));

        Assert.Equal(WorldContentCodec.CurrentVersion, migrated.FormatVersion);
        Assert.Null(migrated.TerrainKeys);
        Assert.Equal(
            ["terrain.plains", "terrain.ocean"],
            migrated.Terrains.Select(static item => item.Key));
        Assert.All(migrated.Terrains, static item => Assert.False(item.IsImprovable));
        Assert.All(migrated.Terrains, static item => Assert.False(string.IsNullOrWhiteSpace(item.Name)));
        Assert.Empty(migrated.CivilianTypes);
        Assert.All(migrated.Resources, static item => Assert.Null(item.ImprovedBy));
        Assert.All(migrated.Scenarios, static item => Assert.Empty(item.Civilians));
    }

    [Fact]
    public void VersionTwelveMigrationRejectsVersionThirteenTerrainAndCivilians()
    {
        var withTerrains = Relabel(
            Encoding.UTF8.GetString(WorldContentCodec.Encode(CreateValidDocument())), 12);

        // Relabel writes version 12's terrainKeys; putting the definitions back
        // makes a document that claims to be older than the field it carries.
        var contradictory = withTerrains.Replace(
            "\"terrainKeys\": [\n    \"terrain.plains\",\n    \"terrain.ocean\"\n  ],\n",
            "\"terrains\": [\n    {\n      \"key\": \"terrain.plains\",\n" +
            "      \"name\": \"Plains\",\n      \"isImprovable\": false\n    }\n  ],\n",
            StringComparison.Ordinal);
        Assert.Contains("\"terrains\"", contradictory, StringComparison.Ordinal);

        var exception = Assert.Throws<ContentValidationException>(() =>
            WorldContentCodec.Decode(Encoding.UTF8.GetBytes(contradictory)));

        Assert.Equal("formatVersion", exception.Path);
    }

    /// <summary>
    /// A world can declare terrain a civilian may improve, the civilians that
    /// improve it, and which deposit each of them works.
    /// </summary>
    [Fact]
    public void CivilianTypesAndImprovableTerrainSurviveARoundTrip()
    {
        var document = CreateValidDocument();
        document.Terrains[0].IsImprovable = true;
        document.CivilianTypes =
        [
            new CivilianTypeContentDefinition
            {
                Key = "civilian.farmer",
                Name = "Farmer",
                WorkTurns = 1,
            },
        ];
        document.Resources[0].ImprovedBy = "civilian.farmer";
        document.Scenarios[0].Civilians =
        [
            new CivilianContent
            {
                Country = document.Countries[0].Key,
                Type = "civilian.farmer",
                Cell = 0,
            },
        ];

        var first = WorldContentCodec.Encode(document);
        var decoded = WorldContentCodec.Decode(first);
        Assert.Equal(first, WorldContentCodec.Encode(decoded));

        var compiled = WorldContentCompiler.Compile(decoded);
        var type = Assert.Single(compiled.World.CivilianTypes);
        Assert.Equal(1, type.WorkTurns);
        Assert.True(compiled.World.Map.GetTerrain(new TerrainId(0))!.IsImprovable);
        Assert.Equal(type.Id, compiled.World.Map.Resources[0].ImprovedBy);

        var civilian = Assert.Single(new WorldState(compiled.World).GetCivilians());
        Assert.Equal(type.Id, civilian.Type);
        Assert.Equal(new CellIndex(0), civilian.Cell);
    }

    [Fact]
    public void ADepositCannotNameACivilianTypeTheWorldDoesNotDeclare()
    {
        var document = CreateValidDocument();
        document.Resources[0].ImprovedBy = "civilian.missing";

        AssertPath("resources[0].improvedBy", document);
    }

    /// <summary>
    /// Version 14 hides the five deposits a Prospector must find and says which
    /// ground is worth searching. Neither can be invented for an older package —
    /// which of an arbitrary world's terrains conceals something is a property
    /// of that world — so a migrated one hides nothing and searches nothing,
    /// exactly as it behaved before.
    /// </summary>
    [Fact]
    public void VersionThirteenMigratesToAWorldWhereNothingIsHiddenAndNothingIsSearchable()
    {
        var document = CreateValidDocument();
        document.CivilianTypes =
        [
            new CivilianTypeContentDefinition
            {
                Key = "civilian.farmer",
                Name = "Farmer",
                WorkTurns = 1,
            },
        ];
        var json = Relabel(Encoding.UTF8.GetString(WorldContentCodec.Encode(document)), 13);

        var migrated = WorldContentCodec.Decode(Encoding.UTF8.GetBytes(json));

        Assert.Equal(WorldContentCodec.CurrentVersion, migrated.FormatVersion);
        Assert.All(migrated.Terrains, static item => Assert.Null(item.Prospecting));
        Assert.All(migrated.Resources, static item => Assert.False(item.RequiresDiscovery));
        Assert.All(
            migrated.CivilianTypes,
            static item => Assert.Equal(CivilianWorkKind.Improve, item.Work));
    }

    [Fact]
    public void VersionThirteenMigrationRejectsVersionFourteenProspecting()
    {
        var document = CreateValidDocument();
        document.Terrains[0].Prospecting = new ProspectingContent();
        var contradictory = Relabel(
            Encoding.UTF8.GetString(WorldContentCodec.Encode(document)), 13);
        Assert.Contains("\"prospecting\"", contradictory, StringComparison.Ordinal);

        var exception = Assert.Throws<ContentValidationException>(() =>
            WorldContentCodec.Decode(Encoding.UTF8.GetBytes(contradictory)));

        Assert.Equal("formatVersion", exception.Path);
    }

    /// <summary>
    /// The whole of version 14 in one package: searchable ground, ground gated
    /// on knowledge, a hidden deposit, and the civilian whose work is to look.
    /// </summary>
    [Fact]
    public void ProspectingTermsAndHiddenDepositsSurviveARoundTrip()
    {
        var document = CreateValidDocument();
        document.Terrains[0].Prospecting = new ProspectingContent();
        document.Terrains[1].Prospecting = new ProspectingContent
        {
            RequiredTechnology = "technology.drilling",
        };
        document.Resources[0].RequiresDiscovery = true;
        document.CivilianTypes =
        [
            new CivilianTypeContentDefinition
            {
                Key = "civilian.prospector",
                Name = "Prospector",
                WorkTurns = 1,
                Work = CivilianWorkKind.Prospect,
            },
        ];

        var first = WorldContentCodec.Encode(document);
        var decoded = WorldContentCodec.Decode(first);
        Assert.Equal(first, WorldContentCodec.Encode(decoded));

        var compiled = WorldContentCompiler.Compile(decoded);
        Assert.Equal(
            CivilianWorkKind.Prospect,
            Assert.Single(compiled.World.CivilianTypes).Work);
        Assert.True(compiled.World.Map.Resources[0].RequiresDiscovery);

        var open = compiled.World.Map.GetTerrain(new TerrainId(0))!.Prospecting;
        Assert.NotNull(open);
        Assert.Null(open!.RequiredTechnology);

        var gated = compiled.World.Map.GetTerrain(new TerrainId(1))!.Prospecting;
        Assert.Equal(new TechnologyId(0), gated!.RequiredTechnology);
    }

    /// <summary>
    /// Version 15 gates improvement behind technology. A version 14 package
    /// names no gate and no starting knowledge, and neither can be invented for
    /// it — which technology opens which rung is a property of a world, and the
    /// 1997 answer is a fact about the original rather than a default. So it
    /// migrates to a world where every rung is open, exactly as it behaved.
    /// </summary>
    [Fact]
    public void VersionFourteenMigratesToAWorldWhereEveryRungIsUngated()
    {
        var json = Relabel(Encoding.UTF8.GetString(WorldContentCodec.Encode(CreateValidDocument())), 14);

        var migrated = WorldContentCodec.Decode(Encoding.UTF8.GetBytes(json));

        Assert.Equal(WorldContentCodec.CurrentVersion, migrated.FormatVersion);
        Assert.All(migrated.Resources, static item => Assert.Null(item.TechnologyByDevelopmentLevel));
        Assert.Empty(migrated.StartingDefaults?.Technologies ?? []);
    }

    [Fact]
    public void VersionFourteenMigrationRejectsVersionFifteenGates()
    {
        var document = CreateValidDocument();
        document.Resources[0].TechnologyByDevelopmentLevel = [null, "technology.drilling"];
        var contradictory = Relabel(
            Encoding.UTF8.GetString(WorldContentCodec.Encode(document)), 14);
        Assert.Contains("technologyByDevelopmentLevel", contradictory, StringComparison.Ordinal);

        var exception = Assert.Throws<ContentValidationException>(() =>
            WorldContentCodec.Decode(Encoding.UTF8.GetBytes(contradictory)));

        Assert.Equal("formatVersion", exception.Path);
    }

    /// <summary>
    /// A ladder and a fair start's knowledge, through encode, decode and
    /// compile. Index 0 is always ungated; a mine opening at Level I is the
    /// manual's one other ungated rung, which a null entry expresses.
    /// </summary>
    [Fact]
    public void TechnologyLaddersAndStartingKnowledgeSurviveARoundTrip()
    {
        var document = CreateValidDocument();
        document.Resources[0].TechnologyByDevelopmentLevel = [null, null, "technology.drilling"];
        document.StartingDefaults = new StartingDefaultsContent
        {
            Technologies = ["technology.drilling"],
        };
        document.Scenarios[0].DefaultStartCountries = [document.Countries[0].Key];

        var first = WorldContentCodec.Encode(document);
        var decoded = WorldContentCodec.Decode(first);
        Assert.Equal(first, WorldContentCodec.Encode(decoded));

        var compiled = WorldContentCompiler.Compile(decoded);
        Assert.Equal(
            [null, null, new TechnologyId(0)],
            compiled.World.Map.Resources[0].TechnologyByDevelopmentLevel);
        Assert.Null(compiled.World.Map.Resources[0].GetRequiredTechnology(1));
        Assert.Equal(new TechnologyId(0), compiled.World.Map.Resources[0].GetRequiredTechnology(2));

        // Past the end of the ladder is ungated rather than forbidden.
        Assert.Null(compiled.World.Map.Resources[0].GetRequiredTechnology(3));

        // And the default reaches the country the scenario names.
        var state = new WorldState(compiled.World);
        Assert.True(state.HasTechnology(new CountryId(0), new TechnologyId(0)));
    }

    /// <summary>
    /// Version 16 limits how much a network carries. A version 15 package has no
    /// limit and none can be invented — a sensible capacity depends entirely on
    /// how much a world's land yields — so it migrates to a network that carries
    /// everything, which is how it behaved.
    /// </summary>
    [Fact]
    public void VersionFifteenMigratesToANetworkWithNoLimit()
    {
        var json = Relabel(Encoding.UTF8.GetString(WorldContentCodec.Encode(CreateValidDocument())), 15);

        var migrated = WorldContentCodec.Decode(Encoding.UTF8.GetBytes(json));

        Assert.Equal(WorldContentCodec.CurrentVersion, migrated.FormatVersion);
        Assert.Null(migrated.Transport);
        Assert.Null(migrated.StartingDefaults?.TransportCapacity);
        Assert.All(migrated.Scenarios, static item => Assert.Empty(item.TransportCapacity));
        Assert.Null(WorldContentCompiler.Compile(migrated).World.Transport);
    }

    [Fact]
    public void VersionFifteenMigrationRejectsVersionSixteenTransport()
    {
        var document = CreateValidDocument();
        document.Transport = new TransportContentSettings
        {
            CostPerCapacityPoint = [new CommodityQuantityContent { Commodity = "commodity.grain", Quantity = 1 }],
        };
        var contradictory = Relabel(
            Encoding.UTF8.GetString(WorldContentCodec.Encode(document)), 15);
        Assert.Contains("\"transport\"", contradictory, StringComparison.Ordinal);

        var exception = Assert.Throws<ContentValidationException>(() =>
            WorldContentCodec.Decode(Encoding.UTF8.GetBytes(contradictory)));

        Assert.Equal("formatVersion", exception.Path);
    }

    [Fact]
    public void TransportSettingsAndCapacitySurviveARoundTrip()
    {
        var document = CreateValidDocument();
        document.Transport = new TransportContentSettings
        {
            CostPerCapacityPoint = [new CommodityQuantityContent { Commodity = "commodity.grain", Quantity = 2 }],
            LabourPerCapacityPoint = 2,
        };
        document.StartingDefaults = new StartingDefaultsContent { TransportCapacity = 12 };
        document.Scenarios[0].DefaultStartCountries = [document.Countries[0].Key];
        document.Scenarios[0].TransportCapacity =
        [
            new TransportCapacityContent { Country = document.Countries[0].Key, Capacity = 30 },
        ];

        var first = WorldContentCodec.Encode(document);
        var decoded = WorldContentCodec.Decode(first);
        Assert.Equal(first, WorldContentCodec.Encode(decoded));

        var compiled = WorldContentCompiler.Compile(decoded);
        Assert.Equal(2, compiled.World.Transport!.LabourPerCapacityPoint);

        // The explicit record beats the default, the same way workforce and
        // capacity already work.
        var state = new WorldState(compiled.World);
        Assert.Equal(30, state.GetTransportCapacity(new CountryId(0)));
    }

    /// <summary>
    /// Version 17 gives a country a treasury. A version 16 package has no money
    /// at all and none can be invented — what a commodity is worth in cash is a
    /// fact about the 1997 economy rather than about worlds in general — so it
    /// migrates to a world where nobody holds any and nothing converts.
    /// </summary>
    [Fact]
    public void VersionSixteenMigratesToAWorldWithNoMoney()
    {
        var json = Relabel(Encoding.UTF8.GetString(WorldContentCodec.Encode(CreateValidDocument())), 16);

        var migrated = WorldContentCodec.Decode(Encoding.UTF8.GetBytes(json));

        Assert.Equal(WorldContentCodec.CurrentVersion, migrated.FormatVersion);
        Assert.Null(migrated.StartingDefaults?.Cash);
        Assert.All(migrated.Commodities, static item => Assert.Null(item.CashPerUnit));
        Assert.All(migrated.Scenarios, static item => Assert.Empty(item.Cash));
        Assert.Equal(0, new WorldState(WorldContentCompiler.Compile(migrated).World)
            .GetCash(new CountryId(0)));
    }

    [Fact]
    public void VersionSixteenMigrationRejectsVersionSeventeenCash()
    {
        var document = CreateValidDocument();
        document.Commodities[0].CashPerUnit = 200;
        var contradictory = Relabel(
            Encoding.UTF8.GetString(WorldContentCodec.Encode(document)), 16);
        Assert.Contains("\"cashPerUnit\"", contradictory, StringComparison.Ordinal);

        var exception = Assert.Throws<ContentValidationException>(() =>
            WorldContentCodec.Decode(Encoding.UTF8.GetBytes(contradictory)));

        Assert.Equal("formatVersion", exception.Path);
    }

    /// <summary>
    /// The treasury follows the same rule every other starting value does: the
    /// default reaches the countries a scenario names, and an explicit record
    /// still wins.
    /// </summary>
    [Fact]
    public void CashAndItsConversionRateSurviveARoundTrip()
    {
        var document = CreateValidDocument();
        document.Commodities[0].CashPerUnit = 200;
        document.StartingDefaults = new StartingDefaultsContent { Cash = 5000 };
        document.Scenarios[0].DefaultStartCountries = [document.Countries[0].Key];
        document.Scenarios[0].Cash =
        [
            new CountryCashContent { Country = document.Countries[0].Key, Amount = 1500 },
        ];

        var first = WorldContentCodec.Encode(document);
        var decoded = WorldContentCodec.Decode(first);
        Assert.Equal(first, WorldContentCodec.Encode(decoded));

        var compiled = WorldContentCompiler.Compile(decoded);
        Assert.Equal(200, compiled.World.Commodities[0].CashPerUnit);
        Assert.Equal(1500, new WorldState(compiled.World).GetCash(new CountryId(0)));
    }

    [Fact]
    public void VersionSixteenMigrationRejectsVersionSeventeenConstruction()
    {
        var document = CreateValidDocument();
        document.Terrains[0].Rail = new RailContent();
        var contradictory = Relabel(
            Encoding.UTF8.GetString(WorldContentCodec.Encode(document)), 16);
        Assert.Contains("\"rail\"", contradictory, StringComparison.Ordinal);

        var exception = Assert.Throws<ContentValidationException>(() =>
            WorldContentCodec.Decode(Encoding.UTF8.GetBytes(contradictory)));

        Assert.Equal("formatVersion", exception.Path);
    }

    /// <summary>
    /// A terrain with no <c>rail</c> block can never carry a line, which is
    /// ocean's answer and every terrain's answer in a world with no
    /// construction. Present-but-empty means anyone may build on it, free.
    /// </summary>
    [Fact]
    public void ConstructionAndTheRailGateSurviveARoundTrip()
    {
        var document = CreateValidDocument();
        document.Construction = new ConstructionContentSettings
        {
            DepotCashCost = 1500,
            PortCashCost = 2000,
        };
        document.Terrains[0].Rail = new RailContent
        {
            RequiredTechnology = document.Technologies[0].Key,
            CashCost = 200,
        };

        var first = WorldContentCodec.Encode(document);
        var decoded = WorldContentCodec.Decode(first);
        Assert.Equal(first, WorldContentCodec.Encode(decoded));

        var world = WorldContentCompiler.Compile(decoded).World;
        Assert.Equal(1500, world.Construction!.GetCashCost(EngineerConstruction.Depot));

        // Rail is not one of this object's answers any more.
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            world.Construction!.GetCashCost(EngineerConstruction.Rail));

        var rail = world.Map.GetTerrain(new TerrainId(0))!.Rail!;
        Assert.Equal(new TechnologyId(0), rail.RequiredTechnology);
        Assert.Equal(200, rail.CashCost);
    }

    /// <summary>
    /// A version 19 package cannot carry version 17's flat rail price. The field
    /// exists on the document only so a v18 package still deserializes.
    /// </summary>
    [Fact]
    public void TheFlatRailPriceIsRejectedAtTheCurrentVersion()
    {
        var document = CreateValidDocument();
        document.Construction = new ConstructionContentSettings
        {
            RailCashCost = 500,
            DepotCashCost = 1500,
            PortCashCost = 2000,
        };

        var exception = Assert.Throws<ContentValidationException>(() =>
            WorldContentCompiler.Compile(document));

        Assert.Equal("construction.railCashCost", exception.Path);
    }

    [Fact]
    public void ARailGateCannotNameATechnologyTheWorldDoesNotDeclare()
    {
        var document = CreateValidDocument();
        document.Terrains[0].Rail = new RailContent { RequiredTechnology = "technology.missing" };

        AssertPath("terrains[0].rail.requiredTechnology", document);
    }

    /// <summary>
    /// A commodity worth nothing is one that reaches the warehouse, so it is
    /// spelled by leaving the price off rather than by writing a zero.
    /// </summary>
    [Fact]
    public void ACommodityPricedAtNothingIsRejected()
    {
        var document = CreateValidDocument();
        document.Commodities[0].CashPerUnit = 0;

        AssertPath("commodities[0].cashPerUnit", document);
    }

    /// <summary>
    /// Version 18 charges a civilian for its work. A version 17 package prices
    /// none, so it migrates to a world where improving is free — which is how it
    /// behaved, and a coherent world rather than a broken one.
    /// </summary>
    [Fact]
    public void VersionSeventeenMigratesToFreeImprovement()
    {
        var json = Relabel(Encoding.UTF8.GetString(WorldContentCodec.Encode(CreateValidDocument())), 17);

        var migrated = WorldContentCodec.Decode(Encoding.UTF8.GetBytes(json));

        Assert.Equal(WorldContentCodec.CurrentVersion, migrated.FormatVersion);
        Assert.Null(migrated.Improvement);
        Assert.Null(WorldContentCompiler.Compile(migrated).World.Improvement);
    }

    [Fact]
    public void VersionSeventeenMigrationRejectsVersionEighteenImprovement()
    {
        var document = CreateValidDocument();
        document.Improvement = new ImprovementContentSettings
        {
            CashCostByDevelopmentLevel = [0, 100],
        };
        var contradictory = Relabel(
            Encoding.UTF8.GetString(WorldContentCodec.Encode(document)), 17);
        Assert.Contains("\"improvement\"", contradictory, StringComparison.Ordinal);

        var exception = Assert.Throws<ContentValidationException>(() =>
            WorldContentCodec.Decode(Encoding.UTF8.GetBytes(contradictory)));

        Assert.Equal("formatVersion", exception.Path);
    }

    /// <summary>
    /// Version 19 lets a country buy technology. A version 18 package prices none,
    /// so it migrates to a world where nothing is for sale — which is how it
    /// behaved: knowledge came only from a scenario, from the fair-start default,
    /// or from a test granting it.
    /// </summary>
    [Fact]
    public void VersionEighteenMigratesToUnpurchasableTechnology()
    {
        var json = Relabel(Encoding.UTF8.GetString(WorldContentCodec.Encode(CreateValidDocument())), 18);

        var migrated = WorldContentCodec.Decode(Encoding.UTF8.GetBytes(json));

        Assert.Equal(WorldContentCodec.CurrentVersion, migrated.FormatVersion);
        Assert.All(migrated.Technologies, technology =>
        {
            Assert.Null(technology.Cost);
            Assert.Null(technology.AvailableFrom);
            Assert.Empty(technology.Prerequisites);
        });
        Assert.All(
            WorldContentCompiler.Compile(migrated).World.Technologies,
            technology => Assert.Null(technology.Cost));
    }

    /// <summary>
    /// **The one migration here that deliberately does not preserve behaviour.**
    /// Version 17's flat rail price is dropped rather than spread across the
    /// terrains, so a migrated package lays track for nothing. The figure was an
    /// invention this project had already labelled unsupported, and carrying a
    /// retracted number forward would give it a longer life than it earned.
    /// </summary>
    [Fact]
    public void VersionEighteenMigrationDropsTheFlatRailPrice()
    {
        var document = CreateValidDocument();
        document.Construction = new ConstructionContentSettings
        {
            DepotCashCost = 1500,
            PortCashCost = 2000,
        };
        document.Terrains[0].Rail = new RailContent();

        // The field cannot be encoded at the current version, so a version 18
        // package has to be written by hand. That is the point of the field
        // existing at all: only a file already on disk can carry it.
        var json = Relabel(Encoding.UTF8.GetString(WorldContentCodec.Encode(document)), 18)
            .Replace(
                "\"depotCashCost\": 1500",
                "\"railCashCost\": 500,\n    \"depotCashCost\": 1500",
                StringComparison.Ordinal);
        Assert.Contains("\"railCashCost\": 500", json, StringComparison.Ordinal);

        var migrated = WorldContentCodec.Decode(Encoding.UTF8.GetBytes(json));

        Assert.Null(migrated.Construction!.RailCashCost);
        Assert.Equal(1500, migrated.Construction.DepotCashCost);
        Assert.Equal(0, migrated.Terrains[0].Rail!.CashCost);

        // And it is gone from what is written back out, not merely ignored.
        Assert.DoesNotContain(
            "railCashCost",
            Encoding.UTF8.GetString(WorldContentCodec.Encode(migrated)),
            StringComparison.Ordinal);
    }

    [Fact]
    public void VersionEighteenMigrationRejectsAPurchasableTechnology()
    {
        var document = CreateValidDocument();
        document.Technologies[0].Cost = 1_000;
        document.Technologies[0].AvailableFrom = 1816;
        var contradictory = Relabel(
            Encoding.UTF8.GetString(WorldContentCodec.Encode(document)), 18);
        Assert.Contains("\"cost\": 1000", contradictory, StringComparison.Ordinal);

        var exception = Assert.Throws<ContentValidationException>(() =>
            WorldContentCodec.Decode(Encoding.UTF8.GetBytes(contradictory)));

        Assert.Equal("formatVersion", exception.Path);
    }

    [Fact]
    public void VersionEighteenMigrationRejectsPerTerrainRailPricing()
    {
        var document = CreateValidDocument();
        document.Terrains[0].Rail = new RailContent { CashCost = 200 };
        var contradictory = Relabel(
            Encoding.UTF8.GetString(WorldContentCodec.Encode(document)), 18);

        var exception = Assert.Throws<ContentValidationException>(() =>
            WorldContentCodec.Decode(Encoding.UTF8.GetBytes(contradictory)));

        Assert.Equal("formatVersion", exception.Path);
    }

    /// <summary>
    /// Prices, arrival years and prerequisites all survive a round trip, and a
    /// prerequisite resolves by key against the catalog.
    /// </summary>
    [Fact]
    public void TechnologyTermsSurviveARoundTrip()
    {
        var document = CreateValidDocument();
        document.Technologies =
        [
            new TechnologyContentDefinition { Key = "technology.drilling", Name = "Oil Drilling" },
            new TechnologyContentDefinition
            {
                Key = "technology.chemistry",
                Name = "Chemistry",
                Prerequisites = ["technology.drilling"],
                AvailableFrom = 1875,
                Cost = 120_000,
            },
        ];

        var first = WorldContentCodec.Encode(document);
        var decoded = WorldContentCodec.Decode(first);
        Assert.Equal(first, WorldContentCodec.Encode(decoded));

        var chemistry = WorldContentCompiler.Compile(decoded).World.Technologies[1];
        Assert.Equal((1875, 120_000L), (chemistry.AvailableFrom!.Value, chemistry.Cost!.Value));
        Assert.Equal(new TechnologyId(0), Assert.Single(chemistry.Prerequisites));

        // The one it builds on is not for sale, and writes nothing at all.
        var drilling = WorldContentCompiler.Compile(decoded).World.Technologies[0];
        Assert.Null(drilling.Cost);
    }

    [Fact]
    public void APrerequisiteCannotNameATechnologyTheWorldDoesNotDeclare()
    {
        var document = CreateValidDocument();
        document.Technologies =
        [
            new TechnologyContentDefinition { Key = "technology.drilling", Name = "Oil Drilling" },
            new TechnologyContentDefinition
            {
                Key = "technology.chemistry",
                Name = "Chemistry",
                Prerequisites = ["technology.missing"],
                Cost = 100,
            },
        ];

        AssertPath("technologies[1].prerequisites[0]", document);
    }

    /// <summary>
    /// A prerequisite must sit earlier in the catalog, so that any prefix of it is
    /// prerequisite-closed. **A chosen constraint**, and the 1997 table satisfies
    /// it for all sixteen of its prerequisites.
    /// </summary>
    [Fact]
    public void APrerequisiteCannotPointForwards()
    {
        var document = CreateValidDocument();
        document.Technologies =
        [
            new TechnologyContentDefinition
            {
                Key = "technology.chemistry",
                Name = "Chemistry",
                Prerequisites = ["technology.drilling"],
                Cost = 100,
            },
            new TechnologyContentDefinition { Key = "technology.drilling", Name = "Oil Drilling" },
        ];

        AssertPath("technologies[0]", document);
    }

    /// <summary>
    /// Version 20 opens a world market. A version 19 package trades nothing, so it migrates
    /// to a world where nothing is tradable, no hull exists and no price moves — which is
    /// exactly how it behaved.
    /// </summary>
    [Fact]
    public void VersionNineteenMigratesToAWorldThatTradesNothing()
    {
        var json = Relabel(Encoding.UTF8.GetString(WorldContentCodec.Encode(CreateValidDocument())), 19);

        var migrated = WorldContentCodec.Decode(Encoding.UTF8.GetBytes(json));

        Assert.Equal(WorldContentCodec.CurrentVersion, migrated.FormatVersion);
        Assert.All(migrated.Commodities, commodity =>
        {
            Assert.Null(commodity.WorldPrice);
            Assert.Null(commodity.TradeOrder);
        });
        Assert.Empty(migrated.ShipTypes);
        Assert.Null(migrated.Trade);

        var world = WorldContentCompiler.Compile(migrated).World;
        Assert.Empty(world.ShipTypes);
        Assert.Null(world.Trade);
        Assert.All(world.Commodities, commodity => Assert.False(commodity.IsTradable));

        // And a country is not a Great Power, which is the right answer for a world with
        // no trade in it: the flag exists only to decide who carries a cargo.
        Assert.All(world.Countries, country => Assert.False(country.IsGreatPower));
    }

    [Fact]
    public void VersionTwentyMigratesToANonWrappingMap()
    {
        var json = Relabel(Encoding.UTF8.GetString(WorldContentCodec.Encode(CreateValidDocument())), 20);

        var migrated = WorldContentCodec.Decode(Encoding.UTF8.GetBytes(json));

        Assert.Equal(WorldContentCodec.CurrentVersion, migrated.FormatVersion);
        Assert.False(migrated.Map.WrapsHorizontally);
        Assert.False(WorldContentCompiler.Compile(migrated).World.Map.WrapsHorizontally);
    }

    [Theory]
    [InlineData("\"worldPrice\": 100", "a priced commodity")]
    [InlineData("\"shipTypes\": [", "a ship")]
    public void VersionNineteenMigrationRejectsVersionTwentyTrade(string marker, string _)
    {
        var document = CreateValidDocument();
        document.Commodities[0].WorldPrice = 100;
        document.Commodities[0].TradeOrder = 0;
        document.ShipTypes = [new ShipTypeContentDefinition { Key = "ship.trader", Name = "Trader", Cargo = 2 }];
        var contradictory = Relabel(
            Encoding.UTF8.GetString(WorldContentCodec.Encode(document)), 19);
        Assert.Contains(marker, contradictory, StringComparison.Ordinal);

        var exception = Assert.Throws<ContentValidationException>(() =>
            WorldContentCodec.Decode(Encoding.UTF8.GetBytes(contradictory)));

        Assert.Equal("formatVersion", exception.Path);
    }

    /// <summary>
    /// The roster, the fleet, the hulls and the market all survive a round trip — and an
    /// untradable commodity writes nothing at all rather than a zero.
    /// </summary>
    [Fact]
    public void TheTradeRosterAndShipsSurviveARoundTrip()
    {
        var document = CreateValidDocument();
        document.Commodities[0].WorldPrice = 300;
        document.Commodities[0].TradeOrder = 4;
        document.ShipTypes =
        [
            new ShipTypeContentDefinition
            {
                Key = "ship.trader", Name = "Trader", Cargo = 2, SeaZones = 1,
            },
            new ShipTypeContentDefinition
            {
                Key = "ship.clipper",
                Name = "Clipper",
                Cargo = 4,
                SeaZones = 1,
                RequiredTechnology = document.Technologies[0].Key,
                Combat = new ShipCombatContent
                {
                    Firepower = 0, Range = 0, Armour = 0, HullScale = 600, BattleSpeed = 0,
                },
            },
        ];
        document.Trade = new TradeContentSettings
        {
            StepPercent = 10,
            TolerancePercent = 10,
            FloorPercent = 25,
            CeilingPercent = 400,
        };
        document.StartingDefaults ??= new StartingDefaultsContent();
        document.StartingDefaults.Ships =
            [new ShipDefaultContent { Type = "ship.trader", Count = 3 }];
        document.Countries[0].IsGreatPower = true;

        var first = WorldContentCodec.Encode(document);
        var decoded = WorldContentCodec.Decode(first);
        Assert.Equal(first, WorldContentCodec.Encode(decoded));

        var world = WorldContentCompiler.Compile(decoded).World;
        var traded = world.Commodities[0];
        Assert.Equal((300L, 4, true), (traded.WorldPrice!.Value, traded.TradeOrder!.Value, traded.IsTradable));

        var clipper = world.ShipTypes[1];
        Assert.Equal((4L, new TechnologyId(0)), (clipper.Cargo, clipper.RequiredTechnology!.Value));
        Assert.Equal((600L, 1L), (clipper.Combat!.HullScale, clipper.SeaZones));

        // A merchant has no printed hull rating, and absent is how that is written.
        Assert.Null(clipper.Combat.Hull);
        Assert.NotNull(world.Trade);
        Assert.True(world.Countries[0].IsGreatPower);

        // An untradable commodity writes neither field, so absence stays the signal.
        var json = Encoding.UTF8.GetString(first);
        Assert.DoesNotContain("\"worldPrice\": 0", json, StringComparison.Ordinal);
    }

    [Fact]
    public void ATradedCommodityMustNameItsPlaceInTheCommodityOrder()
    {
        var document = CreateValidDocument();
        document.Commodities[0].WorldPrice = 100;

        AssertPath("commodities[0]", document);
    }

    [Fact]
    public void TwoCommoditiesCannotShareATradeOrder()
    {
        var document = CreateValidDocument();
        Assert.True(document.Commodities.Length >= 2, "This test needs two commodities.");
        foreach (var commodity in document.Commodities.Take(2))
        {
            commodity.WorldPrice = 100;
            commodity.TradeOrder = 0;
        }

        var exception = Assert.Throws<ContentValidationException>(() =>
            WorldContentCompiler.Compile(document));
        Assert.Contains("trade order", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AShipCannotNameATechnologyTheWorldDoesNotDeclare()
    {
        var document = CreateValidDocument();
        document.ShipTypes =
        [
            new ShipTypeContentDefinition
            {
                Key = "ship.clipper",
                Name = "Clipper",
                RequiredTechnology = "technology.missing",
            },
        ];

        AssertPath("shipTypes[0].requiredTechnology", document);
    }

    [Fact]
    public void AStartingFleetCannotNameAHullTheWorldDoesNotDeclare()
    {
        var document = CreateValidDocument();
        document.StartingDefaults ??= new StartingDefaultsContent();
        document.StartingDefaults.Ships =
            [new ShipDefaultContent { Type = "ship.missing", Count = 3 }];

        AssertPath("startingDefaults.ships[0].type", document);
    }

    /// <summary>
    /// Indexed by the level being reached, so index 0 is unused and a rung past
    /// the end of the list is free.
    /// </summary>
    [Fact]
    public void TheImprovementLadderSurvivesARoundTrip()
    {
        var document = CreateValidDocument();
        document.Improvement = new ImprovementContentSettings
        {
            CashCostByDevelopmentLevel = [0, 100, 1000, 3000],
        };

        var first = WorldContentCodec.Encode(document);
        var decoded = WorldContentCodec.Decode(first);
        Assert.Equal(first, WorldContentCodec.Encode(decoded));

        var improvement = WorldContentCompiler.Compile(decoded).World.Improvement!;
        Assert.Equal((0L, 100L, 1000L, 3000L), (
            improvement.GetCashCost(0),
            improvement.GetCashCost(1),
            improvement.GetCashCost(2),
            improvement.GetCashCost(3)));
        Assert.Equal(0, improvement.GetCashCost(4));
    }

    [Fact]
    public void AnImprovementCannotCostANegativeAmount()
    {
        var document = CreateValidDocument();
        document.Improvement = new ImprovementContentSettings
        {
            CashCostByDevelopmentLevel = [0, -1],
        };

        AssertPath("improvement.cashCostByDevelopmentLevel", document);
    }

    [Fact]
    public void CapacityThatCostsNothingIsRejected()
    {
        var document = CreateValidDocument();
        document.Transport = new TransportContentSettings { CostPerCapacityPoint = [] };

        AssertPath("transport.costPerCapacityPoint", document);
    }

    [Fact]
    public void AGatedRungCannotNameATechnologyTheWorldDoesNotDeclare()
    {
        var document = CreateValidDocument();
        document.Resources[0].TechnologyByDevelopmentLevel = [null, "technology.missing"];

        AssertPath("resources[0].technologyByDevelopmentLevel[1]", document);
    }

    [Fact]
    public void StartingTechnologyCannotNameOneTheWorldDoesNotDeclare()
    {
        var document = CreateValidDocument();
        document.StartingDefaults = new StartingDefaultsContent
        {
            Technologies = ["technology.missing"],
        };

        AssertPath("startingDefaults.technologies[0]", document);
    }

    [Fact]
    public void ProspectableTerrainCannotNameATechnologyTheWorldDoesNotDeclare()
    {
        var document = CreateValidDocument();
        document.Terrains[0].Prospecting = new ProspectingContent
        {
            RequiredTechnology = "technology.missing",
        };

        AssertPath("terrains[0].prospecting.requiredTechnology", document);
    }

    [Fact]
    public void NamingACountryWithNoDefaultsToStartFromIsRejected()
    {
        // Claiming a fair start from a world that defines none is a content
        // error, not a silent zero: the scenario is asking for something the
        // package cannot give it.
        var document = CreateValidDocument();
        document.Scenarios[0].DefaultStartCountries = [document.Countries[0].Key];

        var exception = Assert.Throws<ContentValidationException>(() =>
            WorldContentCompiler.Compile(document));

        Assert.Equal("scenarios[0].defaultStartCountries", exception.Path);
    }

    [Fact]
    public void VersionNineMigrationRejectsVersionTenStartingDefaults()
    {
        var json = Relabel(
            Encoding.UTF8.GetString(WorldContentCodec.Encode(WithSkirmishDefaults(CreateValidDocument()))), 9);

        var exception = Assert.Throws<ContentValidationException>(() =>
            WorldContentCodec.Decode(Encoding.UTF8.GetBytes(json)));

        Assert.Equal("formatVersion", exception.Path);
    }

    /// <summary>
    /// Mills at 2 and factories at 1, four untrained, two trained, one expert:
    /// what `s10`, `s11` and `s15` give every power, and the manual's
    /// construction floor.
    /// </summary>
    private static WorldContentDocument WithSkirmishDefaults(WorldContentDocument document)
    {
        document.StartingDefaults = new StartingDefaultsContent
        {
            ProductionCapacities =
            [
                new FacilityCapacityDefaultContent
                {
                    Facility = document.ProductionFacilities[0].Key,
                    Quantity = 2,
                },
            ],
            Workforce = new WorkforceDefaultContent { Untrained = 4, Trained = 2, Expert = 1 },
        };
        document.Scenarios[0].DefaultStartCountries = [document.Countries[0].Key];
        return document;
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
        Assert.NotEmpty(document.Resources);
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

    /// <summary>The version label the encoder actually writes right now.</summary>
    private static string CurrentVersionLabel =>
        $"\"formatVersion\": {WorldContentCodec.CurrentVersion}";

    /// <summary>
    /// Rewrites an encoded package's version label to <paramref name="version"/>,
    /// asserting that the substitution happened. Spelling the current version
    /// literally is how several of these tests once went vacuously green: after a
    /// version bump the replacement silently matched nothing, and a document that
    /// was supposed to be labelled with an older version stayed current.
    /// </summary>
    private static string Relabel(string json, int version)
    {
        Assert.Contains(CurrentVersionLabel, json, StringComparison.Ordinal);
        var relabelled = json.Replace(
            CurrentVersionLabel,
            $"\"formatVersion\": {version}",
            StringComparison.Ordinal);
        return version >= 13 ? relabelled : WithTerrainKeysInsteadOfDefinitions(relabelled);
    }

    /// <summary>
    /// Turns the version 13 <c>terrains</c> block back into version 12's bare
    /// <c>terrainKeys</c> list.
    /// </summary>
    /// <remarks>
    /// Relabelling alone stopped being enough at version 13, which is the first
    /// bump to rename a field rather than add one. Without this every migration
    /// test below would trip the version 13 guard instead of reaching the rule
    /// it is aiming at — passing, but for the wrong reason.
    /// </remarks>
    private static string WithTerrainKeysInsteadOfDefinitions(string json)
    {
        var terrains = Regex.Match(json, "\"terrains\": \\[.*?\\n  \\],\\n", RegexOptions.Singleline);
        Assert.True(terrains.Success, "The encoder no longer writes a terrains block.");
        var keys = Regex.Matches(terrains.Value, "\"key\": \"(?<key>[^\"]+)\"")
            .Select(static match => $"\n    \"{match.Groups["key"].Value}\"");
        return json.Replace(
            terrains.Value,
            $"\"terrainKeys\": [{string.Join(",", keys)}\n  ],\n",
            StringComparison.Ordinal);
    }

    private static TerrainContentDefinition Terrain(
        string key,
        string name,
        bool isImprovable = false) =>
        new() { Key = key, Name = name, IsImprovable = isImprovable };

    private static WorldContentDocument CreateValidDocument() => new()
    {
        Terrains = [Terrain("terrain.plains", "Plains"), Terrain("terrain.ocean", "Ocean")],
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
            new TechnologyContentDefinition { Key = "technology.drilling", Name = "Oil Drilling" },
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
                LabourCost = 2,
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
            new CountryContentDefinition { Key = "empire.b", Name = "République 世界" },
            new CountryContentDefinition { Key = "empire.a", Name = "Empire A" },
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
