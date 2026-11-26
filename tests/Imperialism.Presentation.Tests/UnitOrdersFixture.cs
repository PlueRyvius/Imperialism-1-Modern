using Imperialism.Content;
using Imperialism.Core;

namespace Imperialism.Presentation.Tests;

/// <summary>
/// A 4×3 odd-row map with one civilian of each kind, so every verb an order can
/// take has a cell where Core accepts it and a cell where Core refuses it.
/// </summary>
/// <remarks>
/// <code>
/// row 0   0 plains+grain(L1, Blue capital)  1 plains+grain   2 hills+hidden coal  3 ocean
/// row 1     4 swamp                          5 plains         6 hills              7 ocean
/// row 2   8 plains (Red capital)            9 plains        10 plains            11 ocean
/// </code>
/// Rows 0 and 1 are Blue's home province; row 2 is Red's. Blue knows Seed Drill
/// only: grain to Level I, but not II; hills rail and depots need Compound Steam;
/// swamp needs Oil Drilling to search and a Bridge to rail.
/// Civilians, in id order: 1 Engineer on 1, 2 Engineer on 6, 3 Farmer on 0,
/// 4 Miner on 0, 5 Prospector on 0 (one-turn work), 6 Red Farmer on 8.
/// </remarks>
internal static class UnitOrdersFixture
{
    public const string Scenario = "scenario.demo";

    public static readonly CountryId Blue = new(0);
    public static readonly CountryId Red = new(1);

    public static readonly CivilianUnitId EngineerOne = new(1);
    public static readonly CivilianUnitId EngineerTwo = new(2);
    public static readonly CivilianUnitId Farmer = new(3);
    public static readonly CivilianUnitId Miner = new(4);
    public static readonly CivilianUnitId Prospector = new(5);
    public static readonly CivilianUnitId RedFarmer = new(6);

    public static CellIndex Cell(int index) => new(index);

    public static CompiledWorldPackage Compile(long blueCash = 5000)
    {
        var document = TurnReportFixture.CreateDocument();
        document.Terrains =
        [
            new TerrainContentDefinition
            {
                Key = "terrain.plains", Name = "Plains", IsImprovable = true,
                Rail = new RailContent { CashCost = 100 },
            },
            new TerrainContentDefinition
            {
                Key = "terrain.hills", Name = "Hills", IsImprovable = true,
                Prospecting = new ProspectingContent(),
                Rail = new RailContent { RequiredTechnology = "technology.compound", CashCost = 200 },
            },
            new TerrainContentDefinition
            {
                Key = "terrain.swamp", Name = "Swamp", IsImprovable = true,
                Prospecting = new ProspectingContent { RequiredTechnology = "technology.oil" },
                Rail = new RailContent { RequiredTechnology = "technology.bridge", CashCost = 300 },
            },
            new TerrainContentDefinition { Key = "terrain.ocean", Name = "Ocean" },
        ];
        document.Commodities =
        [
            .. document.Commodities,
            new CommodityContentDefinition { Key = "commodity.coal", Name = "Coal", Category = CommodityCategory.Raw },
        ];
        document.CivilianTypes =
        [
            new CivilianTypeContentDefinition
            {
                Key = "civilian.engineer", Name = "Engineer", WorkTurns = 3, Work = CivilianWorkKind.Construct,
            },
            new CivilianTypeContentDefinition
            {
                Key = "civilian.farmer", Name = "Farmer", WorkTurns = 3, Work = CivilianWorkKind.Improve,
            },
            new CivilianTypeContentDefinition
            {
                Key = "civilian.miner", Name = "Miner", WorkTurns = 3, Work = CivilianWorkKind.Improve,
            },
            new CivilianTypeContentDefinition
            {
                Key = "civilian.prospector", Name = "Prospector", WorkTurns = 1, Work = CivilianWorkKind.Prospect,
            },
        ];
        document.Resources =
        [
            new ResourceContentDefinition
            {
                Key = "resource.grain",
                Commodity = "commodity.grain",
                YieldByDevelopmentLevel = [2, 4, 6, 8],
                ImprovedBy = "civilian.farmer",
                TechnologyByDevelopmentLevel = [null, "technology.seed-drill", "technology.plows", "technology.plows"],
            },
            new ResourceContentDefinition
            {
                Key = "resource.coal",
                Commodity = "commodity.coal",
                YieldByDevelopmentLevel = [0, 1, 2, 3],
                ImprovedBy = "civilian.miner",
                RequiresDiscovery = true,
            },
        ];
        document.Technologies =
        [
            .. document.Technologies,
            new TechnologyContentDefinition { Key = "technology.plows", Name = "Plows" },
            new TechnologyContentDefinition { Key = "technology.compound", Name = "Compound Steam Engine" },
            new TechnologyContentDefinition { Key = "technology.oil", Name = "Oil Drilling" },
            new TechnologyContentDefinition { Key = "technology.bridge", Name = "Iron Railroad Bridge" },
        ];
        document.Improvement = new ImprovementContentSettings { CashCostByDevelopmentLevel = [0, 100, 1000, 3000] };
        document.Construction = new ConstructionContentSettings
        {
            DepotCashCost = 500, PortCashCost = 1000, FortCashCost = 2000,
        };

        static CellContentDocument Land(string terrain, string province, params string[] resources) => new()
        {
            Terrain = terrain,
            Region = new CellRegionContent { Province = province },
            Resources = resources,
        };

        static CellContentDocument Sea() => new()
        {
            Terrain = "terrain.ocean",
            Region = new CellRegionContent { SeaZone = "sea.north" },
        };

        document.Map.Height = 3;
        document.Map.Cells =
        [
            Land("terrain.plains", "province.home", "resource.grain"),
            Land("terrain.plains", "province.home", "resource.grain"),
            Land("terrain.hills", "province.home", "resource.coal"),
            Sea(),
            Land("terrain.swamp", "province.home"),
            Land("terrain.plains", "province.home"),
            Land("terrain.hills", "province.home"),
            Sea(),
            Land("terrain.plains", "province.far"),
            Land("terrain.plains", "province.far"),
            Land("terrain.plains", "province.far"),
            Sea(),
        ];
        document.Map.Cells[0].HasSettlementSite = true;
        document.Map.Cells[8].HasSettlementSite = true;

        foreach (var scenario in document.Scenarios)
        {
            scenario.Capitals =
            [
                new CountryCapitalContent { Country = "country.blue", Cell = 0 },
                new CountryCapitalContent { Country = "country.red", Cell = 8 },
            ];
            scenario.Cash =
            [
                new CountryCashContent { Country = "country.blue", Amount = blueCash },
                new CountryCashContent { Country = "country.red", Amount = 5000 },
            ];
            scenario.CellDevelopment = [new CellDevelopmentContent { Cell = 0, Level = 1 }];
            scenario.Civilians =
            [
                new CivilianContent { Country = "country.blue", Type = "civilian.engineer", Cell = 1 },
                new CivilianContent { Country = "country.blue", Type = "civilian.engineer", Cell = 6 },
                new CivilianContent { Country = "country.blue", Type = "civilian.farmer", Cell = 0 },
                new CivilianContent { Country = "country.blue", Type = "civilian.miner", Cell = 0 },
                new CivilianContent { Country = "country.blue", Type = "civilian.prospector", Cell = 0 },
                new CivilianContent { Country = "country.red", Type = "civilian.farmer", Cell = 8 },
            ];
        }

        return WorldContentCompiler.CompilePackage(document);
    }

    public static WorldState NewState(CompiledWorldPackage package) => new(package.GetWorld(Scenario));

    public static OrdersDraft NewDraft(WorldState state) => new(state.Definition, Blue);

    public static TechnologyId Technology(CompiledWorldPackage package, string key) =>
        package.GetWorld(Scenario).Technologies.Single(item => package.Catalog.GetKey(item.Id) == key).Id;
}
