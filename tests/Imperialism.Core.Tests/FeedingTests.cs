using Imperialism.Core;
using Xunit;

namespace Imperialism.Core.Tests;

/// <summary>
/// The workforce eats on a repeating cycle of four: grain, fruit, grain, then
/// livestock or fish. Half want grain, a quarter fruit, a quarter meat.
/// </summary>
public sealed class FeedingTests
{
    private const int Grain = 0;
    private const int Fruit = 1;
    private const int Livestock = 2;
    private const int Fish = 3;
    private const int Canned = 4;

    [Fact]
    public void AWorkforceOfFourSplitsInTheDocumentedProportions()
    {
        var state = CreateState(untrained: 4, stock: [(Grain, 10), (Fruit, 10), (Livestock, 10)]);

        var fed = Resolve(state);

        Assert.Equal(4, fed.WellFed);
        Assert.Equal(0, fed.Sick);
        Assert.Equal(0, fed.Starved);
        Assert.Equal(
            [
                new CommodityQuantity(new CommodityId(Grain), 2),
                new CommodityQuantity(new CommodityId(Fruit), 1),
                new CommodityQuantity(new CommodityId(Livestock), 1),
            ],
            fed.Eaten);
    }

    [Fact]
    public void AHeadcountThatDoesNotDivideByFourStillFollowsTheCycle()
    {
        // Six workers walk the cycle as grain, fruit, grain, meat, grain, fruit.
        var state = CreateState(untrained: 6, stock: [(Grain, 10), (Fruit, 10), (Livestock, 10)]);

        var fed = Resolve(state);

        Assert.Equal(6, fed.WellFed);
        Assert.Equal(
            [
                new CommodityQuantity(new CommodityId(Grain), 3),
                new CommodityQuantity(new CommodityId(Fruit), 2),
                new CommodityQuantity(new CommodityId(Livestock), 1),
            ],
            fed.Eaten);
    }

    [Fact]
    public void EitherLivestockOrFishSatisfiesTheFourthPreference()
    {
        var state = CreateState(untrained: 4, stock: [(Grain, 10), (Fruit, 10), (Fish, 10)]);

        var fed = Resolve(state);

        Assert.Equal(4, fed.WellFed);
        Assert.Equal(0, fed.Sick);
        Assert.Contains(new CommodityQuantity(new CommodityId(Fish), 1), fed.Eaten);
    }

    [Fact]
    public void CannedFoodSubstitutesWithoutMakingAnyoneSick()
    {
        // No fruit at all, so the fruit-eater falls back to canned food.
        var state = CreateState(
            untrained: 4,
            stock: [(Grain, 10), (Livestock, 10), (Canned, 10)]);

        var fed = Resolve(state);

        Assert.Equal(4, fed.WellFed);
        Assert.Equal(0, fed.Sick);
        Assert.Contains(new CommodityQuantity(new CommodityId(Canned), 1), fed.Eaten);
    }

    [Fact]
    public void TheWrongFoodIsEatenOnlyAsALastResortAndMakesTheWorkerSick()
    {
        // Grain only: the two grain-eaters are content, the fruit and meat
        // eaters make do with grain and report sick.
        var state = CreateState(untrained: 4, stock: [(Grain, 10)]);

        var fed = Resolve(state);

        Assert.Equal(2, fed.WellFed);
        Assert.Equal(2, fed.Sick);
        Assert.Equal(0, fed.Starved);
        Assert.Equal(
            new CommodityQuantity(new CommodityId(Grain), 4),
            Assert.Single(fed.Eaten));
    }

    [Fact]
    public void WorkersWithNothingAtAllStarveAndAreGone()
    {
        var state = CreateState(untrained: 4, stock: []);

        var fed = Resolve(state);

        Assert.Equal(0, fed.WellFed);
        Assert.Equal(0, fed.Sick);
        Assert.Equal(4, fed.Starved);
        Assert.Empty(fed.Eaten);
        Assert.Equal(0, state.GetTotalWorkers(new CountryId(0)));
    }

    [Fact]
    public void HungerTakesTheUntrainedBeforeTheSkilled()
    {
        // One unit of grain feeds one worker; the other three starve, and the
        // untrained go first. This ordering is a documented choice, not a
        // finding - see docs/formulas/feeding.md.
        var state = CreateState(
            untrained: 2,
            trained: 1,
            expert: 1,
            stock: [(Grain, 1)]);

        var fed = Resolve(state);

        Assert.Equal(3, fed.Starved);
        Assert.Equal(0, state.GetWorkers(new CountryId(0), WorkerGrade.Untrained));
        Assert.Equal(0, state.GetWorkers(new CountryId(0), WorkerGrade.Trained));
        Assert.Equal(1, state.GetWorkers(new CountryId(0), WorkerGrade.Expert));
    }

    [Fact]
    public void FoodArrivingThisTurnIsEatenBeforeWarehouseStock()
    {
        // One worker, first in the cycle, so it wants grain. There is grain in
        // both places.
        var state = CreateState(untrained: 1, stock: [(Grain, 5)]);
        state.QueuePendingDelivery(
            new CountryId(0),
            new CommodityId(Grain),
            1,
            PendingDeliverySource.Extraction);

        var fed = Resolve(state);

        // It eats off the cart, so the warehouse is untouched.
        Assert.Equal(1, fed.WellFed);
        Assert.Equal(5, state.GetAvailableQuantity(new CountryId(0), new CommodityId(Grain)));
        Assert.Empty(state.GetPendingDeliveries());
    }

    [Fact]
    public void APartlyEatenDeliveryKeepsItsRemainder()
    {
        var state = CreateState(untrained: 2, stock: []);
        state.QueuePendingDelivery(
            new CountryId(0),
            new CommodityId(Grain),
            5,
            PendingDeliverySource.Extraction);

        _ = Resolve(state);

        // Two grain eaten off a five-unit delivery; the rest still lands.
        Assert.Equal(3, state.GetAvailableQuantity(new CountryId(0), new CommodityId(Grain)));
    }

    [Fact]
    public void LabourIsTheSumOfEachGradesContribution()
    {
        var state = CreateState(untrained: 3, trained: 2, expert: 1, stock: []);

        // 3x1 + 2x2 + 1x4 = 11.
        Assert.Equal(11, state.GetAvailableLabour(new CountryId(0)));
        Assert.Equal(6, state.GetTotalWorkers(new CountryId(0)));
    }

    [Fact]
    public void AWorldWithNoFeedingRulesNeverFeedsAnyone()
    {
        var state = CreateState(untrained: 4, stock: [(Grain, 10)], withFeeding: false);

        Assert.Empty(TurnResolver.Resolve(state, TurnOrders.Empty(1), 0)
            .Events.OfType<WorkersFedEvent>());
        Assert.Equal(4, state.GetTotalWorkers(new CountryId(0)));
        Assert.Equal(0, state.GetAvailableLabour(new CountryId(0)));
    }

    [Fact]
    public void APreferenceCycleNeedsSomethingInIt()
    {
        Assert.Throws<ArgumentException>(() => new FoodPreference([]));
        Assert.Throws<ArgumentException>(() =>
            new FoodPreference([new CommodityId(0), new CommodityId(0)]));
        Assert.Throws<ArgumentException>(() => new FeedingSettings([], [1, 2, 4]));
        Assert.Throws<ArgumentException>(() =>
            new FeedingSettings([new FoodPreference([new CommodityId(0)])], [1, 2]));
    }

    private static WorkersFedEvent Resolve(WorldState state) =>
        Assert.Single(TurnResolver.Resolve(state, TurnOrders.Empty(1), 0)
            .Events.OfType<WorkersFedEvent>());

    private static WorldState CreateState(
        long untrained = 0,
        long trained = 0,
        long expert = 0,
        (int Commodity, long Quantity)[]? stock = null,
        bool withFeeding = true)
    {
        var dimensions = new MapDimensions(1, 1);
        var map = new MapDefinition(
            dimensions,
            [
                new CellDefinition(
                    new CellIndex(0),
                    new HexCoord(0, 0),
                    new TerrainId(0),
                    CellRegion.ForProvince(new ProvinceId(0))),
            ],
            [new ProvinceDefinition(new ProvinceId(0), "Home")]);

        var scenario = new ScenarioDefinition(
            "Feeding",
            1815,
            [new CountryId(0)],
            null,
            null,
            (stock ?? []).Select(item =>
                new InitialCommodityStock(new CountryId(0), new CommodityId(item.Commodity), item.Quantity)),
            null,
            null,
            null,
            null,
            null,
            [new InitialWorkforce(new CountryId(0), untrained, trained, expert)]);

        var definition = new WorldDefinition(
            map,
            [new CountryDefinition(new CountryId(0), "Home")],
            scenario,
            [
                new CommodityDefinition(new CommodityId(Grain), "Grain", CommodityCategory.Raw),
                new CommodityDefinition(new CommodityId(Fruit), "Fruit", CommodityCategory.Raw),
                new CommodityDefinition(new CommodityId(Livestock), "Livestock", CommodityCategory.Raw),
                new CommodityDefinition(new CommodityId(Fish), "Fish", CommodityCategory.Raw),
                new CommodityDefinition(new CommodityId(Canned), "Canned Food", CommodityCategory.Material),
            ],
            null,
            null,
            null,
            null,
            withFeeding
                ? new FeedingSettings(
                    [
                        new FoodPreference([new CommodityId(Grain)]),
                        new FoodPreference([new CommodityId(Fruit)]),
                        new FoodPreference([new CommodityId(Grain)]),
                        new FoodPreference([new CommodityId(Livestock), new CommodityId(Fish)]),
                    ],
                    [1, 2, 4],
                    new CommodityId(Canned))
                : null);
        return new WorldState(definition);
    }
}
