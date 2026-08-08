using Imperialism.Content;
using Imperialism.Core;
using Imperialism.Presentation;
using Xunit;

namespace Imperialism.Presentation.Tests;

public sealed class MapViewStateTests
{
    [Fact]
    public void MapViewContainsOnlyImmutableGeographyAndStableNames()
    {
        var package = WorldContentCompiler.CompilePackage(CreateDocument());

        var map = MapViewDefinition.Create(package);

        Assert.Equal("Demo Map", map.MapName);
        Assert.Equal(new MapDimensions(3, 1), map.Dimensions);
        Assert.Equal("terrain.plains", map[new CellIndex(0)].TerrainKey);
        Assert.Equal("province.home", map[new CellIndex(0)].RegionKey);
        Assert.Equal("Home Province", map[new CellIndex(0)].RegionName);
        Assert.Equal(["resource.grain"], map[new CellIndex(0)].ResourceKeys);
        Assert.Equal(new RiverPath(RiverEndpoint.Source, RiverEndpoint.EastLower),
            map[new CellIndex(0)].River);
        Assert.Equal("sea.north", map[new CellIndex(2)].RegionKey);
        Assert.Equal("Northern Sea", map[new CellIndex(2)].RegionName);
    }

    [Fact]
    public void WorldViewReflectsCurrentStateWithoutChangingPriorSnapshots()
    {
        var package = WorldContentCompiler.CompilePackage(CreateDocument());
        var world = package.GetWorld("scenario.demo");
        var state = new WorldState(world);
        var initial = WorldViewState.Create(package, "scenario.demo", state);
        Assert.Equal("map.demo", initial.MapKey);
        var rail = Assert.Single(initial.Rails);

        state.SetProvinceOwner(new ProvinceId(0), new CountryId(1));
        Assert.True(state.RemoveRail(rail));
        state.SetCountryCapital(new CountryId(0), null);
        _ = TurnResolver.Resolve(state, TurnOrders.Empty(2), 0);
        var changed = WorldViewState.Create(package, "scenario.demo", state);

        Assert.Equal("Opening", changed.ScenarioName);
        Assert.Equal(1815, changed.CurrentYear);
        Assert.Equal(new TurnDate(1815, 2), changed.CurrentDate);
        Assert.Equal("country.red", changed[new CellIndex(0)].OwnerKey);
        Assert.Equal("Red Empire", changed[new CellIndex(0)].OwnerName);
        Assert.Null(changed[new CellIndex(0)].CapitalCountry);
        Assert.Empty(changed.Rails);

        Assert.Equal("country.blue", initial[new CellIndex(0)].OwnerKey);
        Assert.Equal(new TurnDate(1815, 1), initial.CurrentDate);
        Assert.Equal(new CountryId(0), initial[new CellIndex(0)].CapitalCountry);
        Assert.Single(initial.Rails);
    }

    [Fact]
    public void SeaCellsNeverAcquireProvinceOwnership()
    {
        var package = WorldContentCompiler.CompilePackage(CreateDocument());
        var state = new WorldState(package.GetWorld("scenario.demo"));

        var sea = WorldViewState.Create(package, "scenario.demo", state)[new CellIndex(2)];

        Assert.Null(sea.OwnerKey);
        Assert.Null(sea.OwnerName);
        Assert.Null(sea.CapitalCountry);
    }

    [Fact]
    public void RuntimeStateMustBelongToSelectedScenario()
    {
        var package = WorldContentCompiler.CompilePackage(CreateDocument());
        var alternateState = new WorldState(package.GetWorld("scenario.alternate"));

        Assert.Throws<ArgumentException>(() =>
            WorldViewState.Create(package, "scenario.demo", alternateState));
    }

    private static WorldContentDocument CreateDocument() => new()
    {
        Terrains =
        [
            new TerrainContentDefinition { Key = "terrain.plains", Name = "Plains" },
            new TerrainContentDefinition { Key = "terrain.ocean", Name = "Ocean" },
        ],
        Commodities =
        [
            new CommodityContentDefinition
            {
                Key = "commodity.grain",
                Name = "Grain",
                Category = CommodityCategory.Raw,
            },
        ],
        Resources =
        [
            new ResourceContentDefinition
            {
                Key = "resource.grain",
                Commodity = "commodity.grain",
                YieldByDevelopmentLevel = [1, 2, 4, 8],
            },
        ],
        Extraction = new ExtractionContentSettings { CatchmentRadius = 1 },
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
            new CountryContentDefinition { Key = "country.blue", Name = "Blue Republic" },
            new CountryContentDefinition { Key = "country.red", Name = "Red Empire" },
        ],
        Scenarios =
        [
            Scenario("scenario.demo", "Opening", "country.blue"),
            Scenario("scenario.alternate", "Alternate", "country.red"),
        ],
    };

    private static ScenarioContentDocument Scenario(string key, string name, string owner) => new()
    {
        Key = key,
        Name = name,
        StartingYear = 1815,
        ProvinceOwners =
        [
            new ProvinceOwnerContent { Province = "province.home", Country = owner },
        ],
        Rails = [new CellLinkContent { First = 0, Second = 1 }],
        Capitals =
        [
            new CountryCapitalContent
            {
                Country = owner,
                Cell = 0,
            },
        ],
    };
}
