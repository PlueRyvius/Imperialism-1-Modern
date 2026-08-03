using Imperialism.Core;
using Xunit;

namespace Imperialism.Core.Tests;

/// <summary>
/// Building a facility larger. The manual is unusually exact: mills improve
/// 2, 4, 8, 16, 24 and then by eight; factories 1, 2, 4, 8, 12 and then by four;
/// each point costs one lumber and one steel; expansion requires no labour and
/// completes the following turn.
/// </summary>
public sealed class CapacityConstructionTests
{
    private const int Mill = 0;
    private const int Lumber = 0;
    private const int Steel = 1;
    private const int Cloth = 2;

    private static readonly CountryId Home = new(0);
    private static readonly ProductionFacilityId MillId = new(Mill);

    [Theory]
    // Mills: 2 -> 4 -> 8 -> 16 -> 24, then by eight for ever.
    [InlineData(2, 4)]
    [InlineData(4, 8)]
    [InlineData(8, 16)]
    [InlineData(16, 24)]
    [InlineData(24, 32)]
    [InlineData(32, 40)]
    // Off-ladder sizes exist in six shipped scenarios and must still advance.
    // The next reachable size is the smallest rung strictly above.
    [InlineData(5, 8)]
    [InlineData(7, 8)]
    [InlineData(30, 32)]
    public void AMillAdvancesToTheNextRung(long from, long expected)
    {
        Assert.Equal(expected, MillLadder().NextAbove(from));
    }

    [Theory]
    [InlineData(1, 2)]
    [InlineData(2, 4)]
    [InlineData(8, 12)]
    [InlineData(12, 16)]
    [InlineData(3, 4)]
    public void AFactoryAdvancesOnItsOwnLadder(long from, long expected)
    {
        Assert.Equal(expected, new CapacityLadder([1, 2, 4, 8, 12], 4).NextAbove(from));
    }

    [Fact]
    public void BuildingOneRungCostsOneLumberAndOneSteelPerPoint()
    {
        // 2 -> 4 is two points, so two lumber and two steel.
        var state = CreateState(capacity: 2, lumber: 10, steel: 10);

        var expanded = Assert.Single(Resolve(state));

        Assert.Equal(2, expanded.FromCapacity);
        Assert.Equal(4, expanded.ToCapacity);
        Assert.Equal(8, state.GetAvailableQuantity(Home, new CommodityId(Lumber)));
        Assert.Equal(8, state.GetAvailableQuantity(Home, new CommodityId(Steel)));
        Assert.Equal(4, state.GetProductionCapacity(Home, MillId));
    }

    [Fact]
    public void ABiggerJumpCostsMore()
    {
        // 8 -> 16 is eight points, so eight of each.
        var state = CreateState(capacity: 8, lumber: 20, steel: 20);

        var expanded = Assert.Single(Resolve(state));

        Assert.Equal(16, expanded.ToCapacity);
        Assert.Equal(12, state.GetAvailableQuantity(Home, new CommodityId(Lumber)));
        Assert.Equal(12, state.GetAvailableQuantity(Home, new CommodityId(Steel)));
    }

    [Fact]
    public void AnUnaffordableExpansionDoesNothingAtAll()
    {
        // Two points needed, one steel available. Nothing is part-built and
        // nothing is spent.
        var state = CreateState(capacity: 2, lumber: 10, steel: 1);

        Assert.Empty(Resolve(state));

        Assert.Equal(2, state.GetProductionCapacity(Home, MillId));
        Assert.Equal(10, state.GetAvailableQuantity(Home, new CommodityId(Lumber)));
        Assert.Equal(1, state.GetAvailableQuantity(Home, new CommodityId(Steel)));
    }

    [Fact]
    public void TheNewSizeProducesOnlyFromTheFollowingTurn()
    {
        // Construction runs after Production, which is the whole of how
        // "completes next turn" is modelled. The mill starts at 2, so turn one
        // makes two units however large the order.
        var state = CreateState(capacity: 2, lumber: 10, steel: 10, cloth: 100);

        var first = Assert.Single(
            Resolve(state, produce: 8).Events.OfType<ProductionCompletedEvent>());
        Assert.Equal(2, first.CompletedCycles);
        Assert.Equal(4, state.GetProductionCapacity(Home, MillId));

        var second = Assert.Single(
            Resolve(state, produce: 8).Events.OfType<ProductionCompletedEvent>());
        Assert.Equal(4, second.CompletedCycles);
    }

    [Fact]
    public void ProductionAndConstructionCannotSpendTheSameLumber()
    {
        // Ten lumber. A recipe eating lumber is ordered first and the mill is
        // expanded in the same turn; the two are preflighted together, so the
        // warehouse can never go negative.
        var state = CreateState(capacity: 2, lumber: 10, steel: 10, cloth: 100, millEatsLumber: true);

        _ = Resolve(state, produce: 4);

        Assert.True(
            state.GetAvailableQuantity(Home, new CommodityId(Lumber)) >= 0,
            "Production and construction between them overdrew the warehouse.");
    }

    [Fact]
    public void AnUncappedFacilityCannotCarryALadder()
    {
        Assert.Throws<ArgumentException>(() => new ProductionFacilityDefinition(
            new ProductionFacilityId(0),
            "Food Processing",
            ProductionCapacityMode.Unlimited,
            MillLadder()));
    }

    [Fact]
    public void ALadderMustAscendAndBePositive()
    {
        Assert.Throws<ArgumentException>(() => new CapacityLadder([4, 2], 8));
        Assert.Throws<ArgumentException>(() => new CapacityLadder([], 8));
        Assert.Throws<ArgumentException>(() => new CapacityLadder([0], 8));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CapacityLadder([2], 0));
    }

    private static CapacityLadder MillLadder() => new([2, 4, 8, 16, 24], 8);

    private static IReadOnlyList<FacilityExpandedEvent> Resolve(WorldState state) =>
        Resolve(state, produce: 0).Events.OfType<FacilityExpandedEvent>().ToArray();

    private static TurnResolution Resolve(WorldState state, long produce)
    {
        var production = produce > 0
            ? new[] { new ProductionOrder(new ProductionRecipeId(0), produce) }
            : [];
        return TurnResolver.Resolve(
            state,
            new TurnOrders([new CountryTurnOrders(
                Home, production, [new ProductionExpansionOrder(MillId)])]),
            0);
    }

    private static WorldState CreateState(
        long capacity,
        long lumber = 0,
        long steel = 0,
        long cloth = 0,
        bool millEatsLumber = false)
    {
        var map = new MapDefinition(
            new MapDimensions(1, 1),
            [new CellDefinition(
                new CellIndex(0), new HexCoord(0, 0), new TerrainId(0), CellRegion.Unassigned)]);

        var stock = new List<InitialCommodityStock>
        {
            new(Home, new CommodityId(Lumber), lumber),
            new(Home, new CommodityId(Steel), steel),
        };
        if (cloth > 0)
        {
            stock.Add(new InitialCommodityStock(Home, new CommodityId(Cloth), cloth));
        }

        var scenario = new ScenarioDefinition(
            "Construction",
            1815,
            [],
            initialInventory: stock.Where(static item => item.Quantity > 0),
            initialProductionCapacities:
                [new InitialProductionCapacity(Home, MillId, capacity)]);

        var input = millEatsLumber ? Lumber : Cloth;

        return new WorldState(new WorldDefinition(
            map,
            [new CountryDefinition(Home, "Home")],
            scenario,
            [
                new CommodityDefinition(new CommodityId(Lumber), "Lumber", CommodityCategory.Material),
                new CommodityDefinition(new CommodityId(Steel), "Steel", CommodityCategory.Material),
                new CommodityDefinition(new CommodityId(Cloth), "Cloth", CommodityCategory.Material),
                new CommodityDefinition(new CommodityId(3), "Output", CommodityCategory.Material),
            ],
            [new ProductionFacilityDefinition(
                MillId, "Textile Mill", ProductionCapacityMode.Limited, MillLadder())],
            [new ProductionRecipeDefinition(
                new ProductionRecipeId(0), "Weave", MillId, 1, 2,
                [new CommodityQuantity(new CommodityId(input), 2)],
                [new CommodityQuantity(new CommodityId(3), 1)])],
            null,
            null,
            null,
            null,
            // One lumber and one steel per point of capacity.
            [
                new CommodityQuantity(new CommodityId(Lumber), 1),
                new CommodityQuantity(new CommodityId(Steel), 1),
            ]));
    }
}
