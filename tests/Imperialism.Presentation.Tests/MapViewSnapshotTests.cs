using Imperialism.Content;
using Imperialism.Core;
using Imperialism.Presentation;
using Xunit;

namespace Imperialism.Presentation.Tests;

public sealed class MapViewSnapshotTests
{
    [Fact]
    public void SnapshotResolvesStableKeysNamesAndScenarioFeatures()
    {
        var package = WorldContentCompiler.CompilePackage(CreateDocument());

        var snapshot = MapViewSnapshot.Create(package, "scenario.demo");

        Assert.Equal("Demo Map", snapshot.MapName);
        Assert.Equal("Opening", snapshot.ScenarioName);
        Assert.Equal(1815, snapshot.StartingYear);
        Assert.Equal("terrain.plains", snapshot[new CellIndex(0)].TerrainKey);
        Assert.Equal("province.home", snapshot[new CellIndex(0)].RegionKey);
        Assert.Equal("Home Province", snapshot[new CellIndex(0)].RegionName);
        Assert.Equal("country.blue", snapshot[new CellIndex(0)].OwnerKey);
        Assert.Equal("Blue Republic", snapshot[new CellIndex(0)].OwnerName);
        Assert.Equal(new CountryId(0), snapshot[new CellIndex(0)].CapitalCountry);
        Assert.Null(snapshot[new CellIndex(1)].CapitalCountry);
        Assert.Equal(["resource.grain"], snapshot[new CellIndex(0)].ResourceKeys);
        Assert.Equal(new RiverPath(RiverEndpoint.Source, RiverEndpoint.EastLower),
            snapshot[new CellIndex(0)].River);
        Assert.Single(snapshot.Rails);
    }

    [Fact]
    public void SeaZonesHaveNamesButNoOwners()
    {
        var package = WorldContentCompiler.CompilePackage(CreateDocument());

        var sea = MapViewSnapshot.Create(package, "scenario.demo")[new CellIndex(2)];

        Assert.Equal(CellRegionKind.SeaZone, sea.RegionKind);
        Assert.Equal("sea.north", sea.RegionKey);
        Assert.Equal("Northern Sea", sea.RegionName);
        Assert.Null(sea.OwnerKey);
        Assert.Null(sea.OwnerName);
    }

    private static WorldContentDocument CreateDocument() => new()
    {
        TerrainKeys = ["terrain.plains", "terrain.ocean"],
        ResourceKeys = ["resource.grain"],
        Map = new MapContentDocument
        {
            Key = "map.demo",
            Name = "Demo Map",
            Width = 3,
            Height = 1,
            Provinces =
            [
                new NamedContentDefinition { Key = "province.home", Name = "Home Province" },
            ],
            SeaZones =
            [
                new NamedContentDefinition { Key = "sea.north", Name = "Northern Sea" },
            ],
            Cells =
            [
                new CellContentDocument
                {
                    Terrain = "terrain.plains",
                    Region = new CellRegionContent { Province = "province.home" },
                    Resources = ["resource.grain"],
                    HasSettlementSite = true,
                    River = new RiverPathContent
                    {
                        First = RiverEndpoint.Source,
                        Second = RiverEndpoint.EastLower,
                    },
                },
                new CellContentDocument
                {
                    Terrain = "terrain.plains",
                    Region = new CellRegionContent { Province = "province.home" },
                },
                new CellContentDocument
                {
                    Terrain = "terrain.ocean",
                    Region = new CellRegionContent { SeaZone = "sea.north" },
                },
            ],
        },
        Countries =
        [
            new NamedContentDefinition { Key = "country.blue", Name = "Blue Republic" },
        ],
        Scenarios =
        [
            new ScenarioContentDocument
            {
                Key = "scenario.demo",
                Name = "Opening",
                StartingYear = 1815,
                ProvinceOwners =
                [
                    new ProvinceOwnerContent
                    {
                        Province = "province.home",
                        Country = "country.blue",
                    },
                ],
                Rails = [new CellLinkContent { First = 0, Second = 1 }],
                Capitals = [new CountryCapitalContent { Country = "country.blue", Cell = 0 }],
            },
        ],
    };
}
