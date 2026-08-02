using Imperialism.Core;
using Xunit;

namespace Imperialism.Core.Tests;

public sealed class ProductionTests
{
    [Fact]
    public void OrderedRequestsShareFacilityCapacityAndPartiallyComplete()
    {
        var state = CreateState(raw: 20, capacity: 5);
        var orders = Orders(
            new ProductionOrder(new ProductionRecipeId(0), 4),
            new ProductionOrder(new ProductionRecipeId(1), 4));

        var resolution = TurnResolver.Resolve(state, orders, 1);

        Assert.Equal(10, state.GetAvailableQuantity(new CountryId(0), new CommodityId(0)));
        Assert.Equal(4, state.GetAvailableQuantity(new CountryId(0), new CommodityId(1)));
        Assert.Equal(1, state.GetAvailableQuantity(new CountryId(0), new CommodityId(2)));
        var events = resolution.Events.OfType<ProductionCompletedEvent>().ToArray();
        Assert.Equal([4L, 1L], events.Select(static item => item.CompletedCycles));
        Assert.Equal([4L, 1L], events.Select(static item => item.CapacityUsed));
    }

    [Fact]
    public void ProducedGoodsCannotFeedAnotherRecipeInTheSameTurn()
    {
        var state = CreateState(raw: 4, capacity: 4);
        var orders = Orders(
            new ProductionOrder(new ProductionRecipeId(0), 2),
            new ProductionOrder(new ProductionRecipeId(2), 2));

        var first = TurnResolver.Resolve(state, orders, 1);

        Assert.Equal([2L, 0L], first.Events.OfType<ProductionCompletedEvent>()
            .Select(static item => item.CompletedCycles));
        Assert.Equal(2, state.GetAvailableQuantity(new CountryId(0), new CommodityId(1)));

        var second = TurnResolver.Resolve(
            state,
            Orders(new ProductionOrder(new ProductionRecipeId(2), 2)),
            2);

        Assert.Equal(2, Assert.Single(second.Events.OfType<ProductionCompletedEvent>()).CompletedCycles);
        Assert.Equal(2, state.GetAvailableQuantity(new CountryId(0), new CommodityId(3)));
    }

    [Fact]
    public void UnlimitedFacilityIgnoresStoredCapacityButStillNeedsInputs()
    {
        var state = CreateState(raw: 5, capacity: 0);

        var result = TurnResolver.Resolve(
            state,
            Orders(new ProductionOrder(new ProductionRecipeId(3), 100)),
            1);

        var production = Assert.Single(result.Events.OfType<ProductionCompletedEvent>());
        Assert.Equal(5, production.CompletedCycles);
        Assert.Null(state.GetProductionCapacity(new CountryId(0), new ProductionFacilityId(1)));
        Assert.Throws<InvalidOperationException>(() => state.SetProductionCapacity(
            new CountryId(0),
            new ProductionFacilityId(1),
            1));
    }

    [Fact]
    public void ProductionAndPendingDeliveryOverflowAreAtomicTogether()
    {
        var state = CreateState(raw: 2, capacity: 1, output: long.MaxValue - 1);
        _ = state.QueuePendingDelivery(
            new CountryId(0),
            new CommodityId(1),
            1,
            PendingDeliverySource.Trade);
        var pending = state.GetPendingDeliveries();

        Assert.Throws<OverflowException>(() => TurnResolver.Resolve(
            state,
            Orders(new ProductionOrder(new ProductionRecipeId(0), 1)),
            1));

        Assert.Equal(2, state.GetAvailableQuantity(new CountryId(0), new CommodityId(0)));
        Assert.Equal(long.MaxValue - 1, state.GetAvailableQuantity(new CountryId(0), new CommodityId(1)));
        Assert.Equal(pending, state.GetPendingDeliveries());
        Assert.Equal(0, state.CompletedTurnCount);
    }

    [Fact]
    public void ProductionDefinitionsRejectInvalidAndAliasedCollections()
    {
        var inputs = new[] { new CommodityQuantity(new CommodityId(0), 2) };
        var recipe = new ProductionRecipeDefinition(
            new ProductionRecipeId(0),
            "Output",
            new ProductionFacilityId(0),
            1,
            inputs,
            [new CommodityQuantity(new CommodityId(1), 1)]);

        inputs[0] = new CommodityQuantity(new CommodityId(2), 9);

        Assert.Equal(new CommodityId(0), recipe.Inputs[0].Commodity);
        Assert.Throws<ArgumentException>(() => new ProductionRecipeDefinition(
            new ProductionRecipeId(0),
            "Bad",
            new ProductionFacilityId(0),
            1,
            [],
            [new CommodityQuantity(new CommodityId(1), 1)]));
        Assert.Throws<ArgumentException>(() => new CountryTurnOrders(
            new CountryId(0),
            [new ProductionOrder(new ProductionRecipeId(0), 1), new ProductionOrder(new ProductionRecipeId(0), 2)]));
        Assert.Throws<ArgumentException>(() => new CountryTurnOrders(
            new CountryId(0),
            [default(ProductionOrder)]));
        Assert.Throws<ArgumentException>(() => new ProductionRecipeDefinition(
            new ProductionRecipeId(0),
            "Bad default",
            new ProductionFacilityId(0),
            1,
            [default(CommodityQuantity)],
            [new CommodityQuantity(new CommodityId(1), 1)]));
    }

    private static TurnOrders Orders(params ProductionOrder[] production) => new(
        [new CountryTurnOrders(new CountryId(0), production)]);

    private static WorldState CreateState(long raw, long capacity, long output = 0)
    {
        var map = new MapDefinition(
            new MapDimensions(1, 1),
            [new CellDefinition(
                new CellIndex(0),
                new HexCoord(0, 0),
                new TerrainId(0),
                CellRegion.Unassigned)]);
        var inventory = new List<InitialCommodityStock>
        {
            new(new CountryId(0), new CommodityId(0), raw),
        };
        if (output > 0)
        {
            inventory.Add(new InitialCommodityStock(new CountryId(0), new CommodityId(1), output));
        }

        var capacities = capacity > 0
            ? new[] { new InitialProductionCapacity(new CountryId(0), new ProductionFacilityId(0), capacity) }
            : [];
        var scenario = new ScenarioDefinition(
            "Production",
            1815,
            [],
            initialInventory: inventory,
            initialProductionCapacities: capacities);
        var commodities = Enumerable.Range(0, 4)
            .Select(static id => new CommodityDefinition(new CommodityId(id), $"Commodity {id}", CommodityCategory.Raw));
        var facilities = new[]
        {
            new ProductionFacilityDefinition(new ProductionFacilityId(0), "Mill", ProductionCapacityMode.Limited),
            new ProductionFacilityDefinition(new ProductionFacilityId(1), "Food", ProductionCapacityMode.Unlimited),
        };
        var recipes = new[]
        {
            Recipe(0, 0, 0, 1, inputQuantity: 2),
            Recipe(1, 0, 0, 2, inputQuantity: 2),
            Recipe(2, 0, 1, 3),
            Recipe(3, 1, 0, 2),
        };
        return new WorldState(new WorldDefinition(
            map,
            [new CountryDefinition(new CountryId(0), "Country")],
            scenario,
            commodities,
            facilities,
            recipes));
    }

    private static ProductionRecipeDefinition Recipe(
        int id,
        int facility,
        int input,
        int output,
        long inputQuantity = 1) => new(
            new ProductionRecipeId(id),
            $"Recipe {id}",
            new ProductionFacilityId(facility),
            1,
            [new CommodityQuantity(new CommodityId(input), inputQuantity)],
            [new CommodityQuantity(new CommodityId(output), 1)]);
}
