using Imperialism.Core;
using Xunit;

namespace Imperialism.Core.Tests;

/// <summary>
/// Labour is the workforce's output and production's third constraint, after
/// the warehouse and facility capacity. The manual prices exactly one recipe —
/// a unit of clothing costs two fabric and two labour — and every recipe the
/// original ships spends two input units per unit of output, so that single
/// quote fixes the rate for all of them. See <c>docs/formulas/production.md</c>.
/// </summary>
public sealed class LabourTests
{
    private const int Fabric = 0;
    private const int Clothing = 1;
    private const int Grain = 2;

    /// <summary>
    /// What the workforce eats. Deliberately not one of the recipe inputs, so
    /// that a stock assertion measures production alone — feeding runs in the
    /// same resolution and would otherwise be indistinguishable from it.
    /// </summary>
    private const int Fish = 3;

    [Fact]
    public void OneUnitOfClothingCostsTwoFabricAndTwoLabour()
    {
        // The manual's tutorial, verbatim in behaviour: ordering one clothing
        // takes two fabric out of the warehouse and two off the arm icon.
        var state = CreateState(untrained: 2, fabric: 10);

        var completed = Assert.Single(Resolve(state, Order(ClothingRecipe, 1)));

        Assert.Equal(1, completed.CompletedCycles);
        Assert.Equal(2, completed.LabourUsed);
        Assert.Equal(8, state.GetAvailableQuantity(Home, new CommodityId(Fabric)));
    }

    [Fact]
    public void LabourCapsCyclesTheWarehouseCouldOtherwiseAfford()
    {
        // Ten fabric would make five clothing and the factory is uncapped, but
        // three untrained workers supply three labour, which buys one cycle.
        var state = CreateState(untrained: 3, fabric: 10);

        var completed = Assert.Single(Resolve(state, Order(ClothingRecipe, 5)));

        Assert.Equal(1, completed.CompletedCycles);
        Assert.Equal(2, completed.LabourUsed);
        Assert.Equal(8, state.GetAvailableQuantity(Home, new CommodityId(Fabric)));
    }

    [Fact]
    public void TrainingMultipliesWhatOneWorkerBuys()
    {
        // One of each grade is 1 + 2 + 4 = 7 labour: three cycles and a point
        // left over that no recipe is cheap enough to spend.
        var state = CreateState(untrained: 1, trained: 1, expert: 1, fabric: 20);

        var completed = Assert.Single(Resolve(state, Order(ClothingRecipe, 10)));

        Assert.Equal(3, completed.CompletedCycles);
        Assert.Equal(6, completed.LabourUsed);
    }

    [Fact]
    public void OnePoolIsSharedAcrossFacilitiesInTheOrderTheRequestsArrive()
    {
        // Capacity is per facility; labour is not. Two workers cover one
        // clothing, and the food centre — a different building entirely, and an
        // uncapped one — is left with nothing to work with.
        var state = CreateState(untrained: 2, fabric: 10, grain: 10);

        var completed = Resolve(state, Order(ClothingRecipe, 1), Order(CannedFoodRecipe, 1));

        Assert.Equal(2, completed.Count);
        Assert.Equal(1, completed[0].CompletedCycles);
        Assert.Equal(2, completed[0].LabourUsed);
        Assert.Equal(0, completed[1].CompletedCycles);
        Assert.Equal(0, completed[1].LabourUsed);
        Assert.Equal(10, state.GetAvailableQuantity(Home, new CommodityId(Grain)));
    }

    [Fact]
    public void ReversingThePriorityReversesWhoGetsTheLabour()
    {
        var state = CreateState(untrained: 2, fabric: 10, grain: 10);

        var completed = Resolve(state, Order(CannedFoodRecipe, 1), Order(ClothingRecipe, 1));

        Assert.Equal(1, completed[0].CompletedCycles);
        Assert.Equal(2, completed[0].LabourUsed);
        Assert.Equal(0, completed[1].CompletedCycles);
        Assert.Equal(10, state.GetAvailableQuantity(Home, new CommodityId(Fabric)));
    }

    [Fact]
    public void LabourIsOnlySpentOnCyclesThatActuallyRan()
    {
        // Plenty of labour, not enough fabric: the shortfall must not be billed.
        var state = CreateState(untrained: 20, fabric: 5);

        var completed = Assert.Single(Resolve(state, Order(ClothingRecipe, 4)));

        Assert.Equal(2, completed.CompletedCycles);
        Assert.Equal(4, completed.LabourUsed);
        Assert.Equal(20, state.GetAvailableLabour(Home));
    }

    [Fact]
    public void AWorkforceOfNoneProducesNothing()
    {
        var state = CreateState(fabric: 10);

        var completed = Assert.Single(Resolve(state, Order(ClothingRecipe, 1)));

        Assert.Equal(0, completed.CompletedCycles);
        Assert.Equal(0, completed.LabourUsed);
        Assert.Equal(10, state.GetAvailableQuantity(Home, new CommodityId(Fabric)));
    }

    [Fact]
    public void AWorldThatDefinesNoFeedingIgnoresLabourAltogether()
    {
        // A package with no workforce has no labour to invent, so pricing it
        // must not quietly stop its factories. This is the migration promise
        // for every version 8 world: unchanged unless it feeds workers.
        var state = CreateState(fabric: 10, withFeeding: false);

        var completed = Assert.Single(Resolve(state, Order(ClothingRecipe, 4)));

        Assert.Equal(4, completed.CompletedCycles);
        Assert.Equal(8, completed.LabourUsed);
        Assert.Equal(2, state.GetAvailableQuantity(Home, new CommodityId(Fabric)));
        Assert.Equal(0, state.GetAvailableLabour(Home));
    }

    [Fact]
    public void StarvingThisTurnCostsLabourNextTurn()
    {
        // Feeding runs after Production, so a workforce that starves still
        // works the turn it dies and is smaller when the next turn's orders
        // resolve. Two workers, no food at all: both are gone by turn two.
        var state = CreateState(untrained: 2, fabric: 10, fish: 0);

        var first = Assert.Single(Resolve(state, Order(ClothingRecipe, 4)));
        Assert.Equal(1, first.CompletedCycles);
        Assert.Equal(0, state.GetTotalWorkers(Home));

        var second = Assert.Single(Resolve(state, Order(ClothingRecipe, 4)));
        Assert.Equal(0, second.CompletedCycles);
    }

    private static readonly CountryId Home = new(0);
    private static readonly ProductionRecipeId ClothingRecipe = new(0);
    private static readonly ProductionRecipeId CannedFoodRecipe = new(1);

    private static ProductionOrder Order(ProductionRecipeId recipe, long cycles) =>
        new(recipe, cycles);

    private static IReadOnlyList<ProductionCompletedEvent> Resolve(
        WorldState state,
        params ProductionOrder[] production) =>
        TurnResolver.Resolve(state, new TurnOrders([new CountryTurnOrders(Home, production)]), 0)
            .Events.OfType<ProductionCompletedEvent>().ToArray();

    private static WorldState CreateState(
        long untrained = 0,
        long trained = 0,
        long expert = 0,
        long fabric = 0,
        long grain = 0,
        long fish = 1000,
        bool withFeeding = true)
    {
        var map = new MapDefinition(
            new MapDimensions(1, 1),
            [new CellDefinition(
                new CellIndex(0),
                new HexCoord(0, 0),
                new TerrainId(0),
                CellRegion.Unassigned)]);

        var stock = new List<InitialCommodityStock>();
        if (fabric > 0)
        {
            stock.Add(new InitialCommodityStock(Home, new CommodityId(Fabric), fabric));
        }

        if (grain > 0)
        {
            stock.Add(new InitialCommodityStock(Home, new CommodityId(Grain), grain));
        }

        if (fish > 0)
        {
            stock.Add(new InitialCommodityStock(Home, new CommodityId(Fish), fish));
        }

        var workforce = untrained + trained + expert > 0
            ? new[] { new InitialWorkforce(Home, untrained, trained, expert) }
            : [];

        var scenario = new ScenarioDefinition(
            "Labour",
            1815,
            [],
            initialInventory: stock,
            initialWorkforce: workforce);

        // Both facilities are uncapped so every cap in these tests is labour or
        // the warehouse, never capacity.
        var facilities = new[]
        {
            new ProductionFacilityDefinition(
                new ProductionFacilityId(0), "Clothing Factory", ProductionCapacityMode.Unlimited),
            new ProductionFacilityDefinition(
                new ProductionFacilityId(1), "Food Processing", ProductionCapacityMode.Unlimited),
        };

        var recipes = new[]
        {
            new ProductionRecipeDefinition(
                ClothingRecipe,
                "Clothing",
                new ProductionFacilityId(0),
                1,
                2,
                [new CommodityQuantity(new CommodityId(Fabric), 2)],
                [new CommodityQuantity(new CommodityId(Clothing), 1)]),
            new ProductionRecipeDefinition(
                CannedFoodRecipe,
                "Canned Food",
                new ProductionFacilityId(1),
                1,
                2,
                [new CommodityQuantity(new CommodityId(Grain), 2)],
                [new CommodityQuantity(new CommodityId(Clothing), 1)]),
        };

        return new WorldState(new WorldDefinition(
            map,
            [new CountryDefinition(Home, "Home")],
            scenario,
            [
                new CommodityDefinition(new CommodityId(Fabric), "Fabric", CommodityCategory.Material),
                new CommodityDefinition(new CommodityId(Clothing), "Clothing", CommodityCategory.Material),
                new CommodityDefinition(new CommodityId(Grain), "Grain", CommodityCategory.Raw),
                new CommodityDefinition(new CommodityId(Fish), "Fish", CommodityCategory.Raw),
            ],
            facilities,
            recipes,
            null,
            null,
            withFeeding
                ? new FeedingSettings([new FoodPreference([new CommodityId(Fish)])], [1, 2, 4])
                : null));
    }
}
