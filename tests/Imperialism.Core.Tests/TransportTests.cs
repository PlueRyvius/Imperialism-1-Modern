using Imperialism.Core;
using Xunit;

namespace Imperialism.Core.Tests;

/// <summary>
/// The Transport phase: how much of what the land yielded actually reaches the
/// warehouse, and what it costs to raise that ceiling.
/// </summary>
/// <remarks>
/// The fixture is a 6x1 strip owned by country 0: a capital, three grain tiles
/// and two timber, so the catchment gathers 3 grain and 2 timber a turn against
/// whatever capacity the test gives it.
/// <para>
/// <b>What is not carried is lost.</b> That is a chosen rule rather than a
/// finding; see <c>docs/formulas/transport.md</c>.
/// </para>
/// </remarks>
public sealed class TransportTests
{
    private const int Grain = 0;
    private const int Timber = 1;
    private const int Lumber = 2;
    private const int Steel = 3;

    private static readonly CommodityId GrainId = new(Grain);
    private static readonly CommodityId TimberId = new(Timber);

    [Fact]
    public void AnAllocationIsTrimmedToWhatTheLandActuallyYielded()
    {
        var state = CreateState(capacity: 100);

        // Ask for far more grain than three tiles can grow.
        var result = Resolve(state, Move((Grain, 99)));

        var moved = Assert.Single(result.Events.OfType<CommoditiesTransportedEvent>());
        Assert.Equal([new CommodityQuantity(GrainId, 3)], moved.Moved);
        Assert.Equal(3, moved.CapacityUsed);

        // The timber nobody asked for is gathered, unmoved and gone.
        Assert.Equal([new CommodityQuantity(TimberId, 2)], moved.Wasted);
    }

    [Fact]
    public void AnAllocationIsTrimmedAgainToWhatTheNetworkCanCarry()
    {
        var state = CreateState(capacity: 2);

        var result = Resolve(state, Move((Grain, 99)));

        var moved = Assert.Single(result.Events.OfType<CommoditiesTransportedEvent>());
        Assert.Equal([new CommodityQuantity(GrainId, 2)], moved.Moved);
        Assert.Equal((2L, 2L), (moved.CapacityUsed, moved.CapacityAvailable));

        // One grain and both timber left on the ground.
        Assert.Equal(
            [new CommodityQuantity(GrainId, 1), new CommodityQuantity(TimberId, 2)],
            moved.Wasted);
    }

    /// <summary>
    /// Sliders are served in the order the player set them, which is the same
    /// rule production and facility capacity already use for contention.
    /// </summary>
    [Fact]
    public void AllocationsAreHonouredInTheOrderGiven()
    {
        var first = CreateState(capacity: 3);
        var timberFirst = Assert.Single(
            Resolve(first, Move((Timber, 99), (Grain, 99)))
                .Events.OfType<CommoditiesTransportedEvent>());
        Assert.Equal(
            [new CommodityQuantity(GrainId, 1), new CommodityQuantity(TimberId, 2)],
            timberFirst.Moved);

        var second = CreateState(capacity: 3);
        var grainFirst = Assert.Single(
            Resolve(second, Move((Grain, 99), (Timber, 99)))
                .Events.OfType<CommoditiesTransportedEvent>());
        Assert.Equal(
            [new CommodityQuantity(GrainId, 3)],
            grainFirst.Moved);
    }

    /// <summary>
    /// A slider at zero is a commodity left off the orders entirely, and moves
    /// nothing. Ordering nothing at all moves nothing at all — the capacity does
    /// not allocate itself.
    /// </summary>
    [Fact]
    public void OrderingNothingMovesNothingAndWastesEverything()
    {
        var state = CreateState(capacity: 100);

        var result = Resolve(state, TurnOrders.Empty(1));

        var moved = Assert.Single(result.Events.OfType<CommoditiesTransportedEvent>());
        Assert.Empty(moved.Moved);
        Assert.Equal(0, moved.CapacityUsed);
        Assert.Equal(
            [new CommodityQuantity(GrainId, 3), new CommodityQuantity(TimberId, 2)],
            moved.Wasted);
        Assert.Equal(0, state.GetAvailableQuantity(new CountryId(0), GrainId));
    }

    /// <summary>
    /// What the network leaves behind does not wait for it. Next turn's pool is
    /// what next turn's tiles grow, and no more.
    /// </summary>
    [Fact]
    public void WhatIsLeftBehindDoesNotKeep()
    {
        var state = CreateState(capacity: 1);

        _ = Resolve(state, Move((Grain, 99)));
        var second = Resolve(state, Move((Grain, 99)));

        // Still only one, though two were wasted last turn.
        var moved = Assert.Single(second.Events.OfType<CommoditiesTransportedEvent>());
        Assert.Equal([new CommodityQuantity(GrainId, 1)], moved.Moved);
    }

    /// <summary>
    /// Carried goods reach the warehouse the following turn, through Delivery,
    /// exactly as they did before capacity existed.
    /// </summary>
    [Fact]
    public void WhatIsCarriedReachesTheWarehouseNextTurn()
    {
        var state = CreateState(capacity: 100);
        var country = new CountryId(0);

        _ = Resolve(state, Move((Grain, 99)));

        Assert.Equal(3, state.GetAvailableQuantity(country, GrainId));
    }

    /// <summary>
    /// "As with other industrial expansion" puts the railyard under the same
    /// rule as a mill: ordered now, working next turn.
    /// </summary>
    [Fact]
    public void CapacityBoughtThisTurnCarriesNextTurn()
    {
        var state = CreateState(capacity: 1, stock: 10);
        var country = new CountryId(0);

        var first = Resolve(state, Move([(Grain, 99L)], buildCapacity: 4));

        var built = Assert.Single(first.Events.OfType<TransportCapacityBuiltEvent>());
        Assert.Equal((1L, 5L), (built.FromCapacity, built.ToCapacity));
        Assert.Equal(
            [new CommodityQuantity(new CommodityId(Lumber), 4),
             new CommodityQuantity(new CommodityId(Steel), 4)],
            built.Paid);

        // Still only one this turn, because the yard was not finished when the
        // trains ran.
        Assert.Equal(
            1,
            Assert.Single(first.Events.OfType<CommoditiesTransportedEvent>()).CapacityUsed);
        Assert.Equal(5, state.GetTransportCapacity(country));

        // Next turn it carries everything.
        var second = Resolve(state, Move((Grain, 99), (Timber, 99)));
        Assert.Equal(5, Assert.Single(
            second.Events.OfType<CommoditiesTransportedEvent>()).CapacityUsed);
    }

    /// <summary>
    /// The railyard is trimmed by the warehouse like any other build, and a
    /// partial build is the ordinary outcome rather than a refusal.
    /// </summary>
    [Fact]
    public void TheRailyardBuildsWhatItCanAfford()
    {
        var state = CreateState(capacity: 1, stock: 3);

        var built = Assert.Single(
            Resolve(state, Move([(Grain, 99L)], buildCapacity: 10))
                .Events.OfType<TransportCapacityBuiltEvent>());

        Assert.Equal(4, built.ToCapacity);
    }

    /// <summary>
    /// A world that declares no transport settings has no limit at all, which is
    /// how every world behaved before capacity existed and what a package older
    /// than version 16 still means.
    /// </summary>
    [Fact]
    public void AWorldWithNoTransportSettingsCarriesEverything()
    {
        var state = CreateState(capacity: 0, withTransport: false);

        var result = Resolve(state, TurnOrders.Empty(1));

        var moved = Assert.Single(result.Events.OfType<CommoditiesTransportedEvent>());
        Assert.Equal(
            [new CommodityQuantity(GrainId, 3), new CommodityQuantity(TimberId, 2)],
            moved.Moved);
        Assert.Empty(moved.Wasted);
    }

    [Fact]
    public void ACommodityHasOneSliderAndCannotBeAllocatedTwice()
    {
        Assert.Throws<ArgumentException>(() => new CountryTurnOrders(
            new CountryId(0),
            transport:
            [
                new TransportAllocationOrder(GrainId, 1),
                new TransportAllocationOrder(GrainId, 2),
            ]));
    }

    private static TurnOrders Move(params (int Commodity, long Quantity)[] allocations) =>
        Move(allocations, buildCapacity: 0);

    private static TurnOrders Move((int Commodity, long Quantity)[] allocations, long buildCapacity) => new(
    [
        new CountryTurnOrders(
            new CountryId(0),
            transport: allocations.Select(static item =>
                new TransportAllocationOrder(new CommodityId(item.Commodity), item.Quantity)),
            buildTransportCapacity: buildCapacity),
    ]);

    private static TurnResolution Resolve(WorldState state, TurnOrders orders) =>
        TurnResolver.Resolve(state, orders, 0);

    /// <summary>
    /// A capital, three grain tiles and two timber — all one country's, all
    /// inside the catchment, so the only thing that ever limits delivery is the
    /// network. Three grain and two timber a turn, five units in all.
    /// </summary>
    private static WorldState CreateState(
        long capacity,
        long stock = 0,
        bool withTransport = true)
    {
        const int width = 6;
        var dimensions = new MapDimensions(width, 1);
        var deposits = new int?[] { null, Grain, Grain, Grain, Timber, Timber };
        var cells = new CellDefinition[width];
        for (var index = 0; index < width; index++)
        {
            cells[index] = new CellDefinition(
                new CellIndex(index),
                dimensions.GetCoordinate(new CellIndex(index)),
                new TerrainId(0),
                CellRegion.ForProvince(new ProvinceId(index)),
                deposits[index] is { } deposit ? [new ResourceId(deposit)] : null,
                index == 0 ? SettlementSiteKind.Urban : SettlementSiteKind.None);
        }

        var map = new MapDefinition(
            dimensions,
            cells,
            Enumerable.Range(0, width)
                .Select(static index => new ProvinceDefinition(new ProvinceId(index), $"P{index}")),
            [],
            [
                new ResourceDefinition(new ResourceId(Grain), GrainId, [1]),
                new ResourceDefinition(new ResourceId(Timber), TimberId, [1]),
            ],
            [new TerrainDefinition(new TerrainId(0), "Farmland")]);

        var scenario = new ScenarioDefinition(
            "Transport",
            1815,
            Enumerable.Repeat<CountryId?>(new CountryId(0), width),
            initialCountryCapitals: [new CountryCapital(new CountryId(0), new CellIndex(0))],
            initialInventory: stock == 0
                ? null
                :
                [
                    new InitialCommodityStock(new CountryId(0), new CommodityId(Lumber), stock),
                    new InitialCommodityStock(new CountryId(0), new CommodityId(Steel), stock),
                ],
            initialTransportCapacity: [new InitialTransportCapacity(new CountryId(0), capacity)]);

        return new WorldState(new WorldDefinition(
            map,
            [new CountryDefinition(new CountryId(0), "Country 0")],
            scenario,
            [
                new CommodityDefinition(GrainId, "Grain", CommodityCategory.Raw),
                new CommodityDefinition(TimberId, "Timber", CommodityCategory.Raw),
                new CommodityDefinition(new CommodityId(Lumber), "Lumber", CommodityCategory.Material),
                new CommodityDefinition(new CommodityId(Steel), "Steel", CommodityCategory.Material),
            ],
            extraction: new ExtractionSettings(width),
            transport: withTransport
                ? new TransportSettings(
                    [
                        new CommodityQuantity(new CommodityId(Lumber), 1),
                        new CommodityQuantity(new CommodityId(Steel), 1),
                    ])
                : null));
    }
}
