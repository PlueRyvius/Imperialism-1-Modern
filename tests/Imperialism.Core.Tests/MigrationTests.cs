using Imperialism.Core;
using Xunit;

namespace Imperialism.Core.Tests;

/// <summary>
/// Drawing rural workers into industry through the Capitol.
/// </summary>
/// <remarks>
/// The manual gives the shape outright — recruits arrive untrained, the price is
/// "canned foods, clothing, and furniture", and the country's size limits
/// arrivals to "one-fourth of the number of provinces you own, rounded down".
/// It never says how much of each commodity a worker costs, so the one-of-each
/// used here is a guess; see <c>docs/formulas/migration.md</c>.
/// </remarks>
public sealed class MigrationTests
{
    private const int CannedFood = 0;
    private const int Clothing = 1;
    private const int Furniture = 2;

    private static readonly CountryId Home = new(0);

    [Fact]
    public void RecruitsArriveUntrainedAndCostOneOfEach()
    {
        var state = CreateState(provinces: 8, stock: 10);

        var recruited = Assert.Single(Resolve(state, recruit: 2));

        Assert.Equal(2, recruited.Recruited);
        Assert.Equal(2, state.GetWorkers(Home, WorkerGrade.Untrained));
        Assert.Equal(0, state.GetWorkers(Home, WorkerGrade.Trained));
        Assert.Equal(0, state.GetWorkers(Home, WorkerGrade.Expert));

        Assert.Equal(8, state.GetAvailableQuantity(Home, new CommodityId(Clothing)));
        Assert.Equal(8, state.GetAvailableQuantity(Home, new CommodityId(Furniture)));

        // Canned food goes down twice over: two paid to bring them, and two
        // eaten because they arrive before Feeding and are hungry on the day.
        // That is the whole reason the manual warns about growing too fast.
        Assert.Equal(6, state.GetAvailableQuantity(Home, new CommodityId(CannedFood)));
    }

    [Theory]
    // "One-fourth of the number of provinces you own, rounded down."
    [InlineData(8, 2)]
    [InlineData(4, 1)]
    [InlineData(7, 1)]
    [InlineData(20, 5)]
    // A country of three provinces recruits nobody, however rich it is.
    [InlineData(3, 0)]
    [InlineData(1, 0)]
    public void TheCountrysSizeCapsArrivals(int provinces, long expected)
    {
        var state = CreateState(provinces, stock: 100);

        var recruited = Assert.Single(Resolve(state, recruit: 99));

        Assert.Equal(expected, recruited.Recruited);
        Assert.Equal(expected, recruited.SizeLimit);
        Assert.Equal(99, recruited.Requested);
        Assert.Equal(expected, state.GetWorkers(Home, WorkerGrade.Untrained));
    }

    [Fact]
    public void ARequestBeyondWhatTheWarehouseCanPayBringsAsManyAsItCan()
    {
        // Unlike an expansion, migration is not all-or-nothing: the manual
        // describes a slider dragged until something runs out.
        var state = CreateState(provinces: 40, stock: 3);

        var recruited = Assert.Single(Resolve(state, recruit: 10));

        Assert.Equal(3, recruited.Recruited);
        Assert.Equal(10, recruited.SizeLimit);
        Assert.Equal(0, state.GetAvailableQuantity(Home, new CommodityId(Clothing)));
    }

    [Fact]
    public void TheScarcestComfortDecidesHowManyCome()
    {
        var state = CreateState(provinces: 40, stock: 9, furniture: 2);

        var recruited = Assert.Single(Resolve(state, recruit: 9));

        // Nine canned food and nine clothing, but only two furniture, so two
        // come. Canned food is then 9 - 2 paid - 2 eaten.
        Assert.Equal(2, recruited.Recruited);
        Assert.Equal(5, state.GetAvailableQuantity(Home, new CommodityId(CannedFood)));
        Assert.Equal(0, state.GetAvailableQuantity(Home, new CommodityId(Furniture)));
    }

    [Fact]
    public void AskingWithNothingToPayWithRecruitsNobodyAndStillReports()
    {
        // Reported even at zero: "your country is too small" and "you cannot
        // afford it" are both facts a player dragging a slider needs.
        var state = CreateState(provinces: 8, stock: 0);

        var recruited = Assert.Single(Resolve(state, recruit: 2));

        Assert.Equal(0, recruited.Recruited);
        Assert.Equal(2, recruited.SizeLimit);
        Assert.Empty(recruited.Paid);
        Assert.Equal(0, state.GetTotalWorkers(Home));
    }

    [Fact]
    public void ARecruitEatsOnArrivalAndWorksOnlyFromTheNextTurn()
    {
        // Migration sits after Production and before Feeding, so a recruit is
        // fed the turn it arrives and supplies no labour until the turn after.
        var state = CreateState(provinces: 8, stock: 10);

        var first = TurnResolver.Resolve(
            state, Orders(recruit: 1), 0);

        var fed = Assert.Single(first.Events.OfType<WorkersFedEvent>());
        Assert.Equal(1, fed.WellFed + fed.Sick + fed.Starved);
        Assert.Equal(1, state.GetWorkers(Home, WorkerGrade.Untrained));

        // One untrained worker is one labour, and it is available now — which is
        // to say, to the turn that comes next.
        Assert.Equal(1, state.GetAvailableLabour(Home));
    }

    [Fact]
    public void AWorldWithNoCapitolTermsCannotRecruit()
    {
        var state = CreateState(provinces: 40, stock: 100, withMigration: false);

        Assert.Empty(Resolve(state, recruit: 5));
        Assert.Equal(0, state.GetTotalWorkers(Home));
    }

    [Fact]
    public void SettingsRejectFreeWorkersAndAZeroDivisor()
    {
        var cost = new[] { new CommodityQuantity(new CommodityId(CannedFood), 1) };
        Assert.Throws<ArgumentException>(() => new MigrationSettings([], 4));
        Assert.Throws<ArgumentOutOfRangeException>(() => new MigrationSettings(cost, 0));
        Assert.Throws<ArgumentException>(() => new MigrationSettings(
            [cost[0], new CommodityQuantity(new CommodityId(CannedFood), 2)], 4));
    }

    private static IReadOnlyList<WorkersRecruitedEvent> Resolve(WorldState state, long recruit) =>
        TurnResolver.Resolve(state, Orders(recruit), 0)
            .Events.OfType<WorkersRecruitedEvent>().ToArray();

    private static TurnOrders Orders(long recruit) =>
        new([new CountryTurnOrders(Home, null, null, recruit)]);

    private static WorldState CreateState(
        int provinces,
        long stock,
        long? furniture = null,
        bool withMigration = true)
    {
        var dimensions = new MapDimensions(provinces, 1);
        var cells = Enumerable.Range(0, provinces)
            .Select(index => new CellDefinition(
                new CellIndex(index),
                dimensions.GetCoordinate(new CellIndex(index)),
                new TerrainId(0),
                CellRegion.ForProvince(new ProvinceId(index))))
            .ToArray();

        var inventory = new List<InitialCommodityStock>();
        foreach (var (commodity, quantity) in new[]
                 {
                     (CannedFood, stock), (Clothing, stock), (Furniture, furniture ?? stock),
                 })
        {
            if (quantity > 0)
            {
                inventory.Add(new InitialCommodityStock(Home, new CommodityId(commodity), quantity));
            }
        }

        var scenario = new ScenarioDefinition(
            "Migration",
            1815,
            Enumerable.Repeat<CountryId?>(Home, provinces),
            initialInventory: inventory);

        return new WorldState(new WorldDefinition(
            new MapDefinition(
                dimensions,
                cells,
                Enumerable.Range(0, provinces)
                    .Select(index => new ProvinceDefinition(new ProvinceId(index), $"P{index}"))),
            [new CountryDefinition(Home, "Home")],
            scenario,
            [
                new CommodityDefinition(new CommodityId(CannedFood), "Canned Food", CommodityCategory.Material),
                new CommodityDefinition(new CommodityId(Clothing), "Clothing", CommodityCategory.Material),
                new CommodityDefinition(new CommodityId(Furniture), "Furniture", CommodityCategory.Material),
            ],
            null,
            null,
            null,
            null,
            // Canned food substitutes for any preference, so a recruit that
            // arrives can always eat if there is any left.
            new FeedingSettings(
                [new FoodPreference([new CommodityId(CannedFood)])],
                [1, 2, 4],
                new CommodityId(CannedFood)),
            null,
            null,
            withMigration
                ? new MigrationSettings(
                    [
                        new CommodityQuantity(new CommodityId(CannedFood), 1),
                        new CommodityQuantity(new CommodityId(Clothing), 1),
                        new CommodityQuantity(new CommodityId(Furniture), 1),
                    ],
                    provincesPerRecruit: 4)
                : null));
    }
}
