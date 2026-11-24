using Imperialism.Content;
using Imperialism.Core;
using Imperialism.Presentation;
using Xunit;

namespace Imperialism.Presentation.Tests;

public sealed class CountryStatusViewTests
{
    private static readonly CountryId Blue = new(0);
    private static readonly CountryId Red = new(1);

    [Fact]
    public void StatusReportsTheTreasuryLabourNetworkAndDateOfOneCountry()
    {
        var package = WorldContentCompiler.CompilePackage(CreateDocument());
        var state = new WorldState(package.GetWorld("scenario.demo"));

        var status = CountryStatusView.Create(package, "scenario.demo", state, Blue);

        Assert.Equal("country.blue", status.CountryKey);
        Assert.Equal("Blue Republic", status.CountryName);
        Assert.True(status.IsGreatPower);
        Assert.Equal("Opening", status.ScenarioName);
        Assert.Equal(new TurnDate(1815, 1), status.CurrentDate);
        Assert.Equal(0, status.CompletedTurnCount);
        Assert.Equal(5000, status.Cash);
        Assert.Equal(40, status.TransportCapacity);
        Assert.Equal(7, status.TotalWorkers);

        // Labour is not a headcount. Core derives it from the feeding rules, and
        // this fixture declares none, so seven healthy workers still supply
        // nothing. The border reports what Core says rather than counting heads,
        // which is the whole reason this snapshot exists.
        Assert.Equal(0, status.AvailableLabour);
    }

    [Fact]
    public void StatusIsDetachedFromTheStateItWasReadFrom()
    {
        // The border keeps a snapshot between refreshes, so a snapshot that
        // tracked live state would show numbers from halfway through a turn.
        var package = WorldContentCompiler.CompilePackage(CreateDocument());
        var state = new WorldState(package.GetWorld("scenario.demo"));
        var initial = CountryStatusView.Create(package, "scenario.demo", state, Blue);

        state.SetCash(Blue, 17);
        state.SetTransportCapacity(Blue, 99);
        state.SetWorkers(Blue, WorkerGrade.Expert, 12);
        _ = TurnResolver.Resolve(state, TurnOrders.Empty(2), 0);
        var changed = CountryStatusView.Create(package, "scenario.demo", state, Blue);

        Assert.Equal(5000, initial.Cash);
        Assert.Equal(40, initial.TransportCapacity);
        Assert.Equal(new TurnDate(1815, 1), initial.CurrentDate);
        Assert.Equal(0, initial.CompletedTurnCount);
        Assert.Equal(1, initial.Workforce[2].Total);

        Assert.Equal(17, changed.Cash);
        Assert.Equal(99, changed.TransportCapacity);
        Assert.Equal(new TurnDate(1815, 2), changed.CurrentDate);
        Assert.Equal(1, changed.CompletedTurnCount);
        Assert.Equal(12, changed.Workforce[2].Total);
    }

    [Fact]
    public void TheWarehouseListsEveryCommodityInCatalogOrderEvenWhenEmpty()
    {
        var package = WorldContentCompiler.CompilePackage(CreateDocument());
        var state = new WorldState(package.GetWorld("scenario.demo"));

        var warehouse = CountryStatusView.Create(package, "scenario.demo", state, Blue).Warehouse;

        Assert.Equal(
            ["commodity.grain", "commodity.lumber"],
            warehouse.Select(stock => stock.CommodityKey));
        Assert.Equal(12, warehouse[0].Available);
        Assert.Equal(0, warehouse[1].Available);
        Assert.Equal(CommodityCategory.Raw, warehouse[0].Category);
        Assert.Equal(CommodityCategory.Material, warehouse[1].Category);
    }

    [Fact]
    public void AnUnpricedCommodityIsReportedAsUntradable()
    {
        // Absence of a world price is what makes a commodity untradable, so the
        // border has to distinguish "worth nothing" from "never sold".
        var package = WorldContentCompiler.CompilePackage(CreateDocument());
        var state = new WorldState(package.GetWorld("scenario.demo"));

        var warehouse = CountryStatusView.Create(package, "scenario.demo", state, Blue).Warehouse;

        Assert.False(warehouse[0].IsTradable);
        Assert.True(warehouse[1].IsTradable);
        Assert.Equal(300, warehouse[1].WorldPrice);
    }

    [Fact]
    public void TheWorkforceIsSplitByGradeWithTheSickCountedSeparately()
    {
        // Sickness is Core's to cause, so the fixture starts everyone well and
        // this pins the shape rather than the illness: three grades, lowest
        // first, each carrying its own headcount and its own sick count.
        var package = WorldContentCompiler.CompilePackage(CreateDocument());
        var state = new WorldState(package.GetWorld("scenario.demo"));

        var status = CountryStatusView.Create(package, "scenario.demo", state, Blue);

        Assert.Equal(
            [WorkerGrade.Untrained, WorkerGrade.Trained, WorkerGrade.Expert],
            status.Workforce.Select(grade => grade.Grade));
        Assert.Equal([4, 2, 1], status.Workforce.Select(grade => grade.Total));
        Assert.All(status.Workforce, grade => Assert.Equal(0, grade.Sick));
        Assert.Equal([4, 2, 1], status.Workforce.Select(grade => grade.Healthy));
        Assert.Equal(7, status.TotalWorkers);
    }

    [Fact]
    public void OnlyTheTechnologiesACountryKnowsAreReported()
    {
        var package = WorldContentCompiler.CompilePackage(CreateDocument());
        var state = new WorldState(package.GetWorld("scenario.demo"));

        Assert.Equal(
            ["technology.seed-drill"],
            CountryStatusView.Create(package, "scenario.demo", state, Blue).TechnologyKeys);
        Assert.Empty(CountryStatusView.Create(package, "scenario.demo", state, Red).TechnologyKeys);
    }

    [Fact]
    public void RuntimeStateMustBelongToSelectedScenario()
    {
        var package = WorldContentCompiler.CompilePackage(CreateDocument());
        var alternate = new WorldState(package.GetWorld("scenario.alternate"));

        Assert.Throws<ArgumentException>(() =>
            CountryStatusView.Create(package, "scenario.demo", alternate, Blue));
    }

    [Fact]
    public void ACountryOutsideTheWorldIsRejected()
    {
        var package = WorldContentCompiler.CompilePackage(CreateDocument());
        var state = new WorldState(package.GetWorld("scenario.demo"));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CountryStatusView.Create(package, "scenario.demo", state, new CountryId(2)));
    }

    [Fact]
    public void TheStatusIsIdenticalOnASmallAndALargeMap()
    {
        // The border refreshes constantly, so its snapshot must not walk the
        // map. A timing assertion would be flaky; producing byte-identical
        // output for the same country on a three-cell and a four-thousand-cell
        // world says the same thing and says it deterministically.
        var small = WorldContentCompiler.CompilePackage(CreateDocument());
        var large = WorldContentCompiler.CompilePackage(CreateDocument(width: 80, height: 50));

        var fromSmall = CountryStatusView.Create(
            small, "scenario.demo", new WorldState(small.GetWorld("scenario.demo")), Blue);
        var fromLarge = CountryStatusView.Create(
            large, "scenario.demo", new WorldState(large.GetWorld("scenario.demo")), Blue);

        Assert.Equal(4000, large.GetWorld("scenario.demo").Map.Cells.Count);
        Assert.Equal(fromSmall.Cash, fromLarge.Cash);
        Assert.Equal(fromSmall.AvailableLabour, fromLarge.AvailableLabour);
        Assert.Equal(fromSmall.TransportCapacity, fromLarge.TransportCapacity);
        Assert.Equal(fromSmall.TotalWorkers, fromLarge.TotalWorkers);
        Assert.Equal(fromSmall.Workforce, fromLarge.Workforce);
        Assert.Equal(fromSmall.Warehouse, fromLarge.Warehouse);
        Assert.Equal(fromSmall.TechnologyKeys, fromLarge.TechnologyKeys);
    }

    private static WorldContentDocument CreateDocument(int width = 3, int height = 1)
    {
        var cells = new List<CellContentDocument>
        {
            new()
            {
                Terrain = "terrain.plains",
                Region = new CellRegionContent { Province = "province.home" },
                Resources = ["resource.grain"],
                HasSettlementSite = true,
            },
            new()
            {
                Terrain = "terrain.plains",
                Region = new CellRegionContent { Province = "province.home" },
            },
            new()
            {
                Terrain = "terrain.ocean",
                Region = new CellRegionContent { SeaZone = "sea.north" },
            },
        };
        while (cells.Count < width * height)
        {
            cells.Add(new CellContentDocument
            {
                Terrain = "terrain.ocean",
                Region = new CellRegionContent { SeaZone = "sea.north" },
            });
        }

        return new WorldContentDocument
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
                    YieldByDevelopmentLevel = [1, 2, 4, 8],
                },
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
                Width = width,
                Height = height,
                Provinces = [new NamedContentDefinition { Key = "province.home", Name = "Home Province" }],
                SeaZones = [new NamedContentDefinition { Key = "sea.north", Name = "Northern Sea" }],
                Cells = cells.ToArray(),
            },
            Countries =
            [
                new CountryContentDefinition
                {
                    Key = "country.blue",
                    Name = "Blue Republic",
                    IsGreatPower = true,
                },
                new CountryContentDefinition { Key = "country.red", Name = "Red Empire" },
            ],
            Scenarios = [Scenario("scenario.demo", "Opening"), Scenario("scenario.alternate", "Alternate")],
        };
    }

    private static ScenarioContentDocument Scenario(string key, string name) => new()
    {
        Key = key,
        Name = name,
        StartingYear = 1815,
        ProvinceOwners = [new ProvinceOwnerContent { Province = "province.home", Country = "country.blue" }],
        Capitals = [new CountryCapitalContent { Country = "country.blue", Cell = 0 }],
        Cash = [new CountryCashContent { Country = "country.blue", Amount = 5000 }],
        TransportCapacity =
        [
            new TransportCapacityContent { Country = "country.blue", Capacity = 40 },
        ],
        Workers =
        [
            new WorkforceContent
            {
                Country = "country.blue",
                Untrained = 4,
                Trained = 2,
                Expert = 1,
            },
        ],
        InitialInventory =
        [
            new InitialInventoryContent
            {
                Country = "country.blue",
                Commodity = "commodity.grain",
                Quantity = 12,
            },
        ],
        CountryTechnologies =
        [
            new CountryTechnologyContent
            {
                Country = "country.blue",
                Technology = "technology.seed-drill",
            },
        ],
    };
}
