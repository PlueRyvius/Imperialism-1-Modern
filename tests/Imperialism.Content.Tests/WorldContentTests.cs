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
        Assert.Equal(new CountryId(1), world.Scenario.InitialProvinceOwners[0]);
        Assert.Equal("empire.a", compiled.Catalog.GetKey(new CountryId(1)));
        Assert.Equal(new CountryId(0), compiled.Catalog.GetCountryId("empire.b"));
        Assert.Equal(new ProvinceId(1), compiled.Catalog.GetProvinceId("province.east"));
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

        Assert.Equal(new TerrainId(0), compiled.World.Map[new CellIndex(0)].Terrain);
        Assert.Equal("République 世界", compiled.World.Countries[0].Name);
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
    [InlineData(2)]
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
    public void DecoderRejectsUnknownPropertiesAndMalformedJson()
    {
        var valid = Encoding.UTF8.GetString(WorldContentCodec.Encode(CreateValidDocument()));
        var unknown = valid.Replace(
            "\"formatVersion\": 1,",
            "\"formatVersion\": 1,\n  \"mystery\": true,",
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

    private static void AssertPath(string expected, WorldContentDocument document)
    {
        var exception = Assert.Throws<ContentValidationException>(() =>
            WorldContentCompiler.Compile(document));
        Assert.Equal(expected, exception.Path);
    }

    private static WorldContentDocument CreateValidDocument() => new()
    {
        TerrainKeys = ["terrain.plains", "terrain.ocean"],
        ResourceKeys = ["resource.coal", "resource.grain", "resource.oil"],
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
