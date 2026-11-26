using Imperialism.Content;
using Imperialism.Core;

namespace Imperialism.Presentation.Tests;

/// <summary>
/// <see cref="TurnReportFixture"/>'s two-country world given an economy to order:
/// a mill on a capacity ladder, an unlimited food processing centre, a labour
/// pool, a railyard, the Capitol, priced research and a market with holds.
/// Blue (country 0) is the player; Red is a buyer with ships and cash.
/// </summary>
internal static class OrdersFixture
{
    public const string Scenario = "scenario.demo";

    public static readonly CountryId Blue = new(0);
    public static readonly CountryId Red = new(1);

    public static CompiledWorldPackage Compile()
    {
        var document = TurnReportFixture.CreateDocument();
        document.Commodities =
        [
            .. document.Commodities,
            new CommodityContentDefinition
            {
                Key = "commodity.timber", Name = "Timber", Category = CommodityCategory.Raw,
            },
            new CommodityContentDefinition
            {
                Key = "commodity.steel", Name = "Steel", Category = CommodityCategory.Material,
                WorldPrice = 300, TradeOrder = 2,
            },
            new CommodityContentDefinition
            {
                Key = "commodity.canned-food", Name = "Canned Food", Category = CommodityCategory.Goods,
                WorldPrice = 100, TradeOrder = 3,
            },
        ];
        document.ProductionFacilities =
        [
            new ProductionFacilityContentDefinition
            {
                Key = "facility.lumber-mill",
                Name = "Lumber Mill",
                CapacityLadder = new CapacityLadderContent { Rungs = [2, 4], Increment = 4 },
            },
            new ProductionFacilityContentDefinition
            {
                Key = "facility.food-processing",
                Name = "Food Processing",
                CapacityMode = ProductionCapacityMode.Unlimited,
            },
            new ProductionFacilityContentDefinition
            {
                Key = "facility.refinery",
                Name = "Refinery",
                CapacityLadder = new CapacityLadderContent { Rungs = [2, 4], Increment = 4 },
                RequiredTechnology = "technology.dynamite",
            },
        ];
        document.ProductionRecipes =
        [
            new ProductionRecipeContentDefinition
            {
                Key = "recipe.lumber", Name = "Lumber", Facility = "facility.lumber-mill",
                CapacityCost = 1, LabourCost = 2,
                Inputs = [new CommodityQuantityContent { Commodity = "commodity.timber", Quantity = 2 }],
                Outputs = [new CommodityQuantityContent { Commodity = "commodity.lumber", Quantity = 1 }],
            },
            new ProductionRecipeContentDefinition
            {
                Key = "recipe.canned-food", Name = "Canned Food", Facility = "facility.food-processing",
                CapacityCost = 1, LabourCost = 2,
                Inputs = [new CommodityQuantityContent { Commodity = "commodity.grain", Quantity = 2 }],
                Outputs = [new CommodityQuantityContent { Commodity = "commodity.canned-food", Quantity = 2 }],
            },
        ];
        document.ExpansionCostPerCapacityPoint =
        [
            new CommodityQuantityContent { Commodity = "commodity.lumber", Quantity = 1 },
            new CommodityQuantityContent { Commodity = "commodity.steel", Quantity = 1 },
        ];
        document.Transport = new TransportContentSettings
        {
            CostPerCapacityPoint =
            [
                new CommodityQuantityContent { Commodity = "commodity.lumber", Quantity = 1 },
                new CommodityQuantityContent { Commodity = "commodity.steel", Quantity = 1 },
            ],
            LabourPerCapacityPoint = 1,
        };
        document.Feeding = new FeedingContentSettings
        {
            PreferenceCycle = [new FoodPreferenceContent { Accepted = ["commodity.grain"] }],
            LabourByGrade = [1, 2, 4],
        };
        document.Migration = new MigrationContent
        {
            CostPerWorker = [new CommodityQuantityContent { Commodity = "commodity.canned-food", Quantity = 1 }],
            ProvincesPerRecruit = 1,
        };
        document.Technologies =
        [
            .. document.Technologies,
            new TechnologyContentDefinition { Key = "technology.a", Name = "Alpha", Cost = 1000, AvailableFrom = 1815 },
            new TechnologyContentDefinition
            {
                Key = "technology.b", Name = "Beta", Cost = 3000, Prerequisites = ["technology.a"],
            },
            new TechnologyContentDefinition { Key = "technology.c", Name = "Gamma", Cost = 500, AvailableFrom = 1900 },
            new TechnologyContentDefinition { Key = "technology.d", Name = "Delta", Cost = 4500 },
        ];

        foreach (var scenario in document.Scenarios)
        {
            scenario.InitialInventory =
            [
                Stock("commodity.timber", 10),
                Stock("commodity.lumber", 10),
                Stock("commodity.steel", 10),
                Stock("commodity.grain", 10),
                Stock("commodity.canned-food", 5),
            ];
            scenario.ProductionCapacities =
            [
                new InitialProductionCapacityContent
                {
                    Country = "country.blue", Facility = "facility.lumber-mill", Quantity = 2,
                },
            ];
            scenario.TransportCapacity =
            [
                new TransportCapacityContent { Country = "country.blue", Capacity = 1 },
                new TransportCapacityContent { Country = "country.red", Capacity = 40 },
            ];
            scenario.Ships =
            [
                new ShipContent { Country = "country.red", Type = "ship.trader", SeaZoneKey = "sea.north", Count = 5 },
            ];
        }

        return WorldContentCompiler.CompilePackage(document);
    }

    public static WorldState NewState(CompiledWorldPackage package) => new(package.GetWorld(Scenario));

    public static ProductionRecipeId Recipe(CompiledWorldPackage package, string key) =>
        Find(package.GetWorld(Scenario).ProductionRecipes, item => item.Id, package.Catalog.GetKey, key);

    public static ProductionFacilityId Facility(CompiledWorldPackage package, string key) =>
        Find(package.GetWorld(Scenario).ProductionFacilities, item => item.Id, package.Catalog.GetKey, key);

    public static CommodityId Commodity(CompiledWorldPackage package, string key) =>
        Find(package.GetWorld(Scenario).Commodities, item => item.Id, package.Catalog.GetKey, key);

    public static TechnologyId Technology(CompiledWorldPackage package, string key) =>
        Find(package.GetWorld(Scenario).Technologies, item => item.Id, package.Catalog.GetKey, key);

    private static TId Find<TItem, TId>(
        IEnumerable<TItem> items, Func<TItem, TId> id, Func<TId, string> keyOf, string key) =>
        id(items.Single(item => keyOf(id(item)) == key));

    private static InitialInventoryContent Stock(string commodity, long quantity) =>
        new() { Country = "country.blue", Commodity = commodity, Quantity = quantity };
}
