using Imperialism.Core;
using Xunit;

namespace Imperialism.Core.Tests;

public sealed class CommodityInventoryTests
{
    [Fact]
    public void ResourcesMapToDistinctContentDefinedCommodities()
    {
        var world = CreateWorld();

        Assert.Equal(3, world.Commodities.Count);
        Assert.Equal(CommodityCategory.Raw, world.Commodities[0].Category);
        Assert.Equal(CommodityCategory.Material, world.Commodities[1].Category);
        Assert.Equal(CommodityCategory.Goods, world.Commodities[2].Category);
        Assert.Equal(new CommodityId(0), Assert.Single(world.Map.Resources).Commodity);
        Assert.Equal(new ResourceId(0), Assert.Single(world.Map.Cells[0].Resources));
    }

    [Fact]
    public void AvailableInventoryUsesCheckedLongQuantities()
    {
        var state = new WorldState(CreateWorld(initialQuantity: 10));
        var country = new CountryId(0);
        var commodity = new CommodityId(0);

        Assert.Equal(10, state.GetAvailableQuantity(country, commodity));
        Assert.True(state.TryConsumeAvailable(country, commodity, 4));
        Assert.Equal(6, state.GetAvailableQuantity(country, commodity));
        Assert.False(state.TryConsumeAvailable(country, commodity, 7));
        Assert.Equal(6, state.GetAvailableQuantity(country, commodity));
        state.AddAvailableQuantity(country, commodity, 9);
        Assert.Equal(15, state.GetAvailableQuantity(country, commodity));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            state.AddAvailableQuantity(country, commodity, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            state.TryConsumeAvailable(country, commodity, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            state.GetAvailableQuantity(new CountryId(2), commodity));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            state.GetAvailableQuantity(country, new CommodityId(3)));

        var full = new WorldState(CreateWorld(initialQuantity: long.MaxValue));
        Assert.Throws<OverflowException>(() =>
            full.AddAvailableQuantity(country, commodity, 1));
        Assert.Equal(long.MaxValue, full.GetAvailableQuantity(country, commodity));
    }

    [Fact]
    public void PendingDeliveriesRemainIdentifiableAndCancellable()
    {
        var state = new WorldState(CreateWorld());
        var country = new CountryId(0);
        var commodity = new CommodityId(0);
        var transport = state.QueuePendingDelivery(
            country,
            commodity,
            5,
            PendingDeliverySource.Transport);
        var trade = state.QueuePendingDelivery(
            country,
            commodity,
            7,
            PendingDeliverySource.Trade);
        var snapshot = state.GetPendingDeliveries();

        Assert.Equal(new DeliveryId(1), transport);
        Assert.Equal(new DeliveryId(2), trade);
        Assert.Equal(12, state.GetPendingQuantity(country, commodity));
        Assert.Equal(0, state.GetAvailableQuantity(country, commodity));
        Assert.True(state.CancelPendingDelivery(trade));
        Assert.False(state.CancelPendingDelivery(trade));
        Assert.Equal(5, state.GetPendingQuantity(country, commodity));

        Assert.Equal(2, snapshot.Count);
        Assert.Equal(PendingDeliverySource.Transport, snapshot[0].Source);
        Assert.Equal(PendingDeliverySource.Trade, snapshot[1].Source);
        var mutableView = Assert.IsAssignableFrom<IList<PendingDelivery>>(snapshot);
        Assert.Throws<NotSupportedException>(() => mutableView.Add(snapshot[0]));
        Assert.Throws<ArgumentOutOfRangeException>(() => new DeliveryId(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => state.QueuePendingDelivery(
            country,
            commodity,
            0,
            PendingDeliverySource.Transport));
        Assert.Throws<ArgumentOutOfRangeException>(() => state.QueuePendingDelivery(
            country,
            commodity,
            1,
            (PendingDeliverySource)200));
    }

    [Fact]
    public void DeliveryPhaseCommitsRemainingIntentsAndEmitsFacts()
    {
        var state = new WorldState(CreateWorld(initialQuantity: 3));
        var country = new CountryId(0);
        var commodity = new CommodityId(0);
        var cancelled = state.QueuePendingDelivery(
            country,
            commodity,
            100,
            PendingDeliverySource.Trade);
        var committed = state.QueuePendingDelivery(
            country,
            commodity,
            8,
            PendingDeliverySource.Transport);
        Assert.True(state.CancelPendingDelivery(cancelled));

        var result = TurnResolver.Resolve(state, TurnOrders.Empty(2), 44);

        Assert.Equal(11, state.GetAvailableQuantity(country, commodity));
        Assert.Empty(state.GetPendingDeliveries());
        var deliveryEvent = Assert.Single(result.Events.OfType<CommodityDeliveredEvent>());
        Assert.Equal(committed, deliveryEvent.Delivery.Id);
        Assert.Equal(8, deliveryEvent.Delivery.Quantity);
        Assert.Equal(TurnPhase.Delivery, deliveryEvent.Phase);
        Assert.Equal(
            TurnPhase.Delivery,
            result.Events[result.Events.IndexOf(deliveryEvent) + 1].Phase);
        Assert.IsType<TurnPhaseCompletedEvent>(
            result.Events[result.Events.IndexOf(deliveryEvent) + 1]);
    }

    [Fact]
    public void OverflowDuringDeliveryIsAtomic()
    {
        var state = new WorldState(CreateWorld(initialQuantity: long.MaxValue - 2));
        var country = new CountryId(0);
        var commodity = new CommodityId(0);
        _ = state.QueuePendingDelivery(country, commodity, 2, PendingDeliverySource.Transport);
        _ = state.QueuePendingDelivery(country, commodity, 1, PendingDeliverySource.Trade);
        var before = state.GetPendingDeliveries();

        Assert.Throws<OverflowException>(() =>
            TurnResolver.Resolve(state, TurnOrders.Empty(2), 1));

        Assert.Equal(long.MaxValue - 2, state.GetAvailableQuantity(country, commodity));
        Assert.Equal(before, state.GetPendingDeliveries());
        Assert.Equal(0, state.CompletedTurnCount);
        Assert.Equal(new TurnDate(1815, 1), state.CurrentDate);
    }

    [Fact]
    public void PendingAggregationUsesCheckedArithmeticWithoutMutatingEntries()
    {
        var state = new WorldState(CreateWorld());
        var country = new CountryId(0);
        var commodity = new CommodityId(0);
        _ = state.QueuePendingDelivery(country, commodity, long.MaxValue, PendingDeliverySource.Transport);
        _ = state.QueuePendingDelivery(country, commodity, 1, PendingDeliverySource.Trade);

        Assert.Throws<OverflowException>(() => state.GetPendingQuantity(country, commodity));
        Assert.Throws<OverflowException>(() =>
            TurnResolver.Resolve(state, TurnOrders.Empty(2), 1));
        Assert.Equal(2, state.GetPendingDeliveries().Count);
        Assert.Equal(0, state.GetAvailableQuantity(country, commodity));
    }

    [Fact]
    public void InitialInventoryRejectsDuplicateCountryCommodityPairs()
    {
        Assert.Throws<ArgumentException>(() => new ScenarioDefinition(
            "Duplicate",
            1815,
            [new CountryId(0)],
            initialInventory:
            [
                new InitialCommodityStock(new CountryId(0), new CommodityId(0), 1),
                new InitialCommodityStock(new CountryId(0), new CommodityId(0), 2),
            ]));
    }

    [Fact]
    public void WorldBoundaryValidatesResourceAndCommodityReferences()
    {
        var dimensions = new MapDimensions(1, 1);
        var cell = new CellDefinition(
            new CellIndex(0),
            new HexCoord(0, 0),
            new TerrainId(0),
            CellRegion.Unassigned,
            [new ResourceId(0)]);
        Assert.Throws<ArgumentException>(() => new MapDefinition(dimensions, [cell]));

        var map = new MapDefinition(
            dimensions,
            [cell],
            resources: [new ResourceDefinition(new ResourceId(0), new CommodityId(1), 1)]);
        Assert.Throws<ArgumentException>(() => new WorldDefinition(
            map,
            [],
            new ScenarioDefinition("Invalid", 1815, []),
            [new CommodityDefinition(new CommodityId(0), "Only", CommodityCategory.Raw)]));
        Assert.Throws<ArgumentException>(() => new WorldDefinition(
            new MapDefinition(
                dimensions,
                [new CellDefinition(
                    new CellIndex(0),
                    new HexCoord(0, 0),
                    new TerrainId(0),
                    CellRegion.Unassigned)]),
            [],
            new ScenarioDefinition("Invalid", 1815, []),
            [new CommodityDefinition(new CommodityId(1), "Sparse", CommodityCategory.Raw)]));
    }

    [Fact]
    public void InventoryHasNoHistoricalCommodityOrCountryCeiling()
    {
        const int countryCount = 30;
        const int commodityCount = 300;
        var map = new MapDefinition(
            new MapDimensions(1, 1),
            [new CellDefinition(
                new CellIndex(0),
                new HexCoord(0, 0),
                new TerrainId(0),
                CellRegion.Unassigned)]);
        var countries = Enumerable.Range(0, countryCount)
            .Select(static value => new CountryDefinition(new CountryId(value), $"Country {value}"));
        var commodities = Enumerable.Range(0, commodityCount)
            .Select(static value => new CommodityDefinition(
                new CommodityId(value),
                $"Commodity {value}",
                CommodityCategory.Raw));
        var state = new WorldState(new WorldDefinition(
            map,
            countries,
            new ScenarioDefinition("Large Catalog", 1815, []),
            commodities));

        state.AddAvailableQuantity(new CountryId(29), new CommodityId(299), long.MaxValue);

        Assert.Equal(long.MaxValue, state.GetAvailableQuantity(
            new CountryId(29),
            new CommodityId(299)));
    }

    private static WorldDefinition CreateWorld(long initialQuantity = 0)
    {
        var commodities = new[]
        {
            new CommodityDefinition(new CommodityId(0), "Grain", CommodityCategory.Raw),
            new CommodityDefinition(new CommodityId(1), "Steel", CommodityCategory.Material),
            new CommodityDefinition(new CommodityId(2), "Hardware", CommodityCategory.Goods),
        };
        var dimensions = new MapDimensions(1, 1);
        var map = new MapDefinition(
            dimensions,
            [new CellDefinition(
                new CellIndex(0),
                new HexCoord(0, 0),
                new TerrainId(0),
                CellRegion.ForProvince(new ProvinceId(0)),
                [new ResourceId(0)])],
            [new ProvinceDefinition(new ProvinceId(0), "Province")],
            resources: [new ResourceDefinition(new ResourceId(0), new CommodityId(0), 1)]);
        var inventory = initialQuantity > 0
            ? new[] { new InitialCommodityStock(new CountryId(0), new CommodityId(0), initialQuantity) }
            : [];
        var scenario = new ScenarioDefinition(
            "Economy",
            1815,
            [new CountryId(0)],
            initialInventory: inventory);
        return new WorldDefinition(
            map,
            [
                new CountryDefinition(new CountryId(0), "A"),
                new CountryDefinition(new CountryId(1), "B"),
            ],
            scenario,
            commodities);
    }
}

internal static class ReadOnlyListTestExtensions
{
    public static int IndexOf<T>(this IReadOnlyList<T> values, T value)
    {
        for (var index = 0; index < values.Count; index++)
        {
            if (EqualityComparer<T>.Default.Equals(values[index], value))
            {
                return index;
            }
        }

        return -1;
    }
}
