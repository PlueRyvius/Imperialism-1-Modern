using Imperialism.Content;
using Imperialism.Core;

namespace Imperialism.Presentation.Tests;

/// <summary>
/// A small world that actually resolves a turn: two countries, both holding land
/// with a deposit and a capital to gather at, so the Extraction and Delivery
/// phases have something to say even when nobody submits an order.
/// </summary>
internal static class TurnReportFixture
{
    public static WorldContentDocument CreateDocument() => new()
    {
        Terrains =
        [
            new TerrainContentDefinition { Key = "terrain.plains", Name = "Plains", IsImprovable = true },
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
            new CommodityContentDefinition
            {
                Key = "commodity.lumber",
                Name = "Lumber",
                Category = CommodityCategory.Material,
                WorldPrice = 300,
                TradeOrder = 1,
            },
        ],
        Resources =
        [
            new ResourceContentDefinition
            {
                Key = "resource.grain",
                Commodity = "commodity.grain",
                YieldByDevelopmentLevel = [2, 4, 6, 8],
            },
            new ResourceContentDefinition
            {
                Key = "resource.timber",
                Commodity = "commodity.lumber",
                YieldByDevelopmentLevel = [1, 2, 3, 4],
            },
        ],
        ProductionFacilities =
        [
            new ProductionFacilityContentDefinition { Key = "facility.lumber-mill", Name = "Lumber Mill" },
        ],
        Technologies =
        [
            new TechnologyContentDefinition { Key = "technology.seed-drill", Name = "Seed Drill" },
            new TechnologyContentDefinition { Key = "technology.dynamite", Name = "Dynamite" },
        ],
        Extraction = new ExtractionContentSettings { CatchmentRadius = 1 },
        Map = new MapContentDocument
        {
            Key = "map.demo",
            Name = "Demo Map",
            Width = 4,
            Height = 1,
            Provinces =
            [
                new NamedContentDefinition { Key = "province.home", Name = "Home Province" },
                new NamedContentDefinition { Key = "province.far", Name = "Far Province" },
            ],
            SeaZones = [new NamedContentDefinition { Key = "sea.north", Name = "Northern Sea" }],
            Cells =
            [
                new CellContentDocument
                {
                    Terrain = "terrain.plains",
                    Region = new CellRegionContent { Province = "province.home" },
                    Resources = ["resource.grain"],
                    HasSettlementSite = true,
                },
                new CellContentDocument
                {
                    Terrain = "terrain.plains",
                    Region = new CellRegionContent { Province = "province.far" },
                    Resources = ["resource.timber"],
                    HasSettlementSite = true,
                },
                new CellContentDocument
                {
                    Terrain = "terrain.ocean",
                    Region = new CellRegionContent { SeaZone = "sea.north" },
                },
                new CellContentDocument
                {
                    Terrain = "terrain.plains",
                    Region = new CellRegionContent { Province = "province.far" },
                },
            ],
        },
        Countries =
        [
            new CountryContentDefinition
            {
                Key = "country.blue",
                Name = "Blue Republic",
                IsGreatPower = true,
            },
            new CountryContentDefinition
            {
                Key = "country.red",
                Name = "Red Empire",
                IsGreatPower = true,
            },
        ],
        Scenarios = [Scenario("scenario.demo", "Opening"), Scenario("scenario.alternate", "Alternate")],
    };

    private static ScenarioContentDocument Scenario(string key, string name) => new()
    {
        Key = key,
        Name = name,
        StartingYear = 1815,
        ProvinceOwners =
        [
            new ProvinceOwnerContent { Province = "province.home", Country = "country.blue" },
            new ProvinceOwnerContent { Province = "province.far", Country = "country.red" },
        ],
        Capitals =
        [
            new CountryCapitalContent { Country = "country.blue", Cell = 0 },
            new CountryCapitalContent { Country = "country.red", Cell = 1 },
        ],
        Cash =
        [
            new CountryCashContent { Country = "country.blue", Amount = 5000 },
            new CountryCashContent { Country = "country.red", Amount = 5000 },
        ],
        TransportCapacity =
        [
            new TransportCapacityContent { Country = "country.blue", Capacity = 40 },
            new TransportCapacityContent { Country = "country.red", Capacity = 40 },
        ],
        Workers =
        [
            new WorkforceContent { Country = "country.blue", Untrained = 4, Trained = 2, Expert = 1 },
            new WorkforceContent { Country = "country.red", Untrained = 4, Trained = 2, Expert = 1 },
        ],
        CountryTechnologies =
        [
            new CountryTechnologyContent { Country = "country.blue", Technology = "technology.seed-drill" },
        ],
    };
}
