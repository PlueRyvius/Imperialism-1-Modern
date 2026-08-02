using Imperialism.Core;
using Xunit;

namespace Imperialism.Core.Tests;

public sealed class TurnResolverTests
{
    private static readonly TurnPhase[] ExpectedPhases =
    [
        TurnPhase.Diplomacy,
        TurnPhase.Trade,
        TurnPhase.Production,
        TurnPhase.Conflict,
        TurnPhase.TradeCancellation,
        TurnPhase.Extraction,
        TurnPhase.Feeding,
        TurnPhase.Delivery,
        TurnPhase.Connectivity,
    ];

    [Fact]
    public void ResolverUsesFixedPhaseOrderAndAdvancesOneQuarter()
    {
        var state = CreateState();

        var result = TurnResolver.Resolve(state, TurnOrders.Empty(2), 0x1234UL);

        Assert.Equal(1, result.TurnNumber);
        Assert.Equal(new TurnDate(1815, 1), result.StartedAt);
        Assert.Equal(new TurnDate(1815, 2), result.EndedAt);
        Assert.Equal(0x1234UL, result.Seed);
        Assert.Equal(ExpectedPhases, result.Events.Select(static item => item.Phase));
        Assert.All(result.Events, static item =>
        {
            Assert.IsType<TurnPhaseCompletedEvent>(item);
            Assert.Equal(1, item.TurnNumber);
        });
        Assert.Equal(1, state.CompletedTurnCount);
        Assert.Equal(new TurnDate(1815, 2), state.CurrentDate);
        Assert.Equal(1815, state.CurrentYear);
    }

    [Fact]
    public void FourTurnsAdvanceOneYearWithoutHistoricalDateLimits()
    {
        var state = CreateState(startingYear: 5000);
        var orders = TurnOrders.Empty(2);

        for (var turn = 1; turn <= 4; turn++)
        {
            var result = TurnResolver.Resolve(state, orders, (ulong)turn);
            Assert.Equal(turn, result.TurnNumber);
        }

        Assert.Equal(4, state.CompletedTurnCount);
        Assert.Equal(new TurnDate(5001, 1), state.CurrentDate);
        Assert.Equal(5001, state.CurrentYear);
    }

    [Fact]
    public void EqualInputsProduceEqualEventLogs()
    {
        var first = CreateState();
        var second = CreateState();
        var firstResult = TurnResolver.Resolve(first, TurnOrders.Empty(2), 987654321UL);
        var secondResult = TurnResolver.Resolve(second, TurnOrders.Empty(2), 987654321UL);

        Assert.Equal(firstResult.TurnNumber, secondResult.TurnNumber);
        Assert.Equal(firstResult.StartedAt, secondResult.StartedAt);
        Assert.Equal(firstResult.EndedAt, secondResult.EndedAt);
        Assert.Equal(firstResult.Seed, secondResult.Seed);
        Assert.Equal(
            firstResult.Events.Select(static item => (item.GetType(), item.TurnNumber, item.Phase)),
            secondResult.Events.Select(static item => (item.GetType(), item.TurnNumber, item.Phase)));
    }

    [Fact]
    public void EventLogAndResolvedConnectivityAreDetachedSnapshots()
    {
        var state = CreateState();
        var result = TurnResolver.Resolve(state, TurnOrders.Empty(2), 1);
        var events = Assert.IsAssignableFrom<IList<TurnEvent>>(result.Events);
        var connectivity = state.GetRailConnectivity(new CountryId(0));

        Assert.Throws<NotSupportedException>(() =>
            events.Add(new TurnPhaseCompletedEvent(1, TurnPhase.Diplomacy)));
        Assert.Same(connectivity, state.GetRailConnectivity(new CountryId(0)));

        state.SetProvinceOwner(new ProvinceId(1), new CountryId(1));

        Assert.NotSame(connectivity, state.GetRailConnectivity(new CountryId(0)));
        Assert.Equal(ExpectedPhases, result.Events.Select(static item => item.Phase));
    }

    [Fact]
    public void ResolverRejectsMissingCountrySubmissionBeforeAdvancingState()
    {
        var state = CreateState();

        Assert.Throws<ArgumentException>(() =>
            TurnResolver.Resolve(state, TurnOrders.Empty(1), 0));

        Assert.Equal(0, state.CompletedTurnCount);
        Assert.Equal(new TurnDate(1815, 1), state.CurrentDate);
    }

    [Fact]
    public void TurnOrdersRequireDenseCountryIdOrderAndStayInert()
    {
        Assert.Throws<ArgumentException>(() => new TurnOrders(
            [new CountryTurnOrders(new CountryId(1)), new CountryTurnOrders(new CountryId(0))]));
        Assert.Throws<ArgumentException>(() => new TurnOrders(
            [new CountryTurnOrders(new CountryId(0)), null!]));

        var orders = TurnOrders.Empty(2);

        Assert.Equal(new CountryId(0), orders[new CountryId(0)].Country);
        Assert.Equal(new CountryId(1), orders[new CountryId(1)].Country);
        Assert.Throws<ArgumentOutOfRangeException>(() => orders[new CountryId(2)]);
        Assert.DoesNotContain(
            typeof(CountryTurnOrders).GetMethods(),
            static method => method.Name == "Execute");
    }

    [Fact]
    public void TurnDateValidatesQuarterAndCheckedRollover()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TurnDate(1815, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TurnDate(1815, 5));
        Assert.Equal("1815 Q4", new TurnDate(1815, 4).ToString());
        Assert.Throws<OverflowException>(() => new TurnDate(int.MaxValue, 4).Next());
    }

    private static WorldState CreateState(int startingYear = 1815)
    {
        var dimensions = new MapDimensions(2, 1);
        var map = new MapDefinition(
            dimensions,
            [
                new CellDefinition(
                    new CellIndex(0),
                    new HexCoord(0, 0),
                    new TerrainId(0),
                    CellRegion.ForProvince(new ProvinceId(0))),
                new CellDefinition(
                    new CellIndex(1),
                    new HexCoord(1, 0),
                    new TerrainId(0),
                    CellRegion.ForProvince(new ProvinceId(1))),
            ],
            [
                new ProvinceDefinition(new ProvinceId(0), "West"),
                new ProvinceDefinition(new ProvinceId(1), "East"),
            ]);
        var scenario = new ScenarioDefinition(
            "Turn Test",
            startingYear,
            [new CountryId(0), new CountryId(0)],
            [new CellLink(new CellIndex(0), new CellIndex(1))]);
        var definition = new WorldDefinition(
            map,
            [
                new CountryDefinition(new CountryId(0), "A"),
                new CountryDefinition(new CountryId(1), "B"),
            ],
            scenario);
        return new WorldState(definition);
    }
}
