using Imperialism.Core;
using Xunit;

namespace Imperialism.Core.Tests;

/// <summary>
/// The treasury, and the one income that fills it: gold and gems converting as
/// the network carries them.
/// </summary>
/// <remarks>
/// The fixture is a 4x1 strip owned by country 0 — a capital, a grain tile, a
/// gold tile and a gems tile — so a turn gathers 1 grain, 1 gold and 1 gems
/// against whatever capacity the test gives it. Every deposit yields 1 at level
/// 0 here so the arithmetic stays about the conversion rather than about the
/// yield curve.
/// <para>
/// The rates are the manual's, stated outright: "each unit of gold transported
/// increases your cash by $200"; "transported gems convert to cash at $500 per
/// unit." See <c>docs/formulas/money.md</c>.
/// </para>
/// </remarks>
public sealed class TreasuryTests
{
    private const int Grain = 0;
    private const int Gold = 1;
    private const int Gems = 2;

    private const long GoldRate = 200;
    private const long GemsRate = 500;

    private static readonly CountryId Country = new(0);
    private static readonly CommodityId GrainId = new(Grain);
    private static readonly CommodityId GoldId = new(Gold);
    private static readonly CommodityId GemsId = new(Gems);

    [Fact]
    public void ACountryStartsWithWhateverTheScenarioAuthored()
    {
        var state = CreateState(capacity: 0, cash: 1500);

        Assert.Equal(1500, state.GetCash(Country));
    }

    /// <summary>
    /// The same rule every other starting value follows: the default applies to
    /// the countries a scenario names, and an explicit record still wins.
    /// </summary>
    [Fact]
    public void TheFairStartFillsATreasuryTheScenarioLeftSilent()
    {
        var defaulted = CreateState(capacity: 0, cash: null, defaultCash: 4000);
        Assert.Equal(4000, defaulted.GetCash(Country));

        var authored = CreateState(capacity: 0, cash: 1500, defaultCash: 4000);
        Assert.Equal(1500, authored.GetCash(Country));
    }

    [Fact]
    public void ACountryOutsideTheFairStartGetsNoDefaultTreasury()
    {
        var state = CreateState(capacity: 0, cash: null, defaultCash: 4000, fairStart: false);

        Assert.Equal(0, state.GetCash(Country));
    }

    /// <summary>
    /// **The headline rule.** Gold and gems "never reach the industry warehouse";
    /// what the network carries of them converts on arrival.
    /// </summary>
    [Fact]
    public void CarriedGoldAndGemsPayCashInsteadOfFillingTheWarehouse()
    {
        var state = CreateState(capacity: 10);

        var result = Resolve(state, Move((Gold, 9), (Gems, 9)));

        var carried = Assert.Single(result.Events.OfType<CommoditiesTransportedEvent>());
        Assert.Equal(
            [new CommodityQuantity(GoldId, 1), new CommodityQuantity(GemsId, 1)],
            carried.Converted);
        Assert.Equal(GoldRate + GemsRate, carried.CashEarned);

        // Nothing was carried to the warehouse, and nothing ever will be.
        Assert.Empty(carried.Moved);
        Assert.Equal(GoldRate + GemsRate, state.GetCash(Country));
        Assert.Equal(0, state.GetAvailableQuantity(Country, GoldId));
        Assert.Equal(0, state.GetAvailableQuantity(Country, GemsId));
    }

    /// <summary>
    /// The income is per unit, so a richer mine is worth proportionately more —
    /// and gems are worth two and a half times gold.
    /// </summary>
    [Fact]
    public void EachCommodityPaysItsOwnRate()
    {
        var gold = CreateState(capacity: 10);
        _ = Resolve(gold, Move((Gold, 9)));
        Assert.Equal(GoldRate, gold.GetCash(Country));

        var gems = CreateState(capacity: 10);
        _ = Resolve(gems, Move((Gems, 9)));
        Assert.Equal(GemsRate, gems.GetCash(Country));
    }

    /// <summary>
    /// **Gold still costs capacity to move**, which is what makes carrying it a
    /// real choice against food and materials rather than free money.
    /// </summary>
    [Fact]
    public void ConvertingStillSpendsTheNetwork()
    {
        var state = CreateState(capacity: 1);

        // Gold first, so it takes the only point on the network and the grain
        // the workers would have eaten is left on the ground.
        var result = Resolve(state, Move((Gold, 9), (Grain, 9)));

        var carried = Assert.Single(result.Events.OfType<CommoditiesTransportedEvent>());
        Assert.Equal(1, carried.CapacityUsed);
        Assert.Equal([new CommodityQuantity(GoldId, 1)], carried.Converted);
        Assert.Empty(carried.Moved);
        Assert.Contains(new CommodityQuantity(GrainId, 1), carried.Wasted);
    }

    /// <summary>
    /// Unmoved gold does not keep, exactly as unmoved grain does not. A deposit
    /// gathered and left behind pays nothing.
    /// </summary>
    [Fact]
    public void GoldLeftOnTheGroundPaysNothing()
    {
        var state = CreateState(capacity: 10);

        var result = Resolve(state, Move((Grain, 9)));

        var carried = Assert.Single(result.Events.OfType<CommoditiesTransportedEvent>());
        Assert.Empty(carried.Converted);
        Assert.Equal(0, carried.CashEarned);
        Assert.Equal(0, state.GetCash(Country));
        Assert.Equal(
            [new CommodityQuantity(GoldId, 1), new CommodityQuantity(GemsId, 1)],
            carried.Wasted);
    }

    /// <summary>
    /// Income accumulates: nothing resets a treasury between turns, which is what
    /// makes a gold mine a way to pay for a network rather than a one-off.
    /// </summary>
    [Fact]
    public void CashAccumulatesAcrossTurns()
    {
        var state = CreateState(capacity: 10, cash: 100);

        _ = Resolve(state, Move((Gold, 9)));
        _ = Resolve(state, Move((Gold, 9)));

        Assert.Equal(100 + (2 * GoldRate), state.GetCash(Country));
    }

    /// <summary>
    /// Spending is all or nothing. A structure half paid for is not a structure,
    /// which is the same shape <see cref="WorldState.TryConsumeAvailable"/> uses
    /// for the warehouse.
    /// </summary>
    [Fact]
    public void SpendingIsAllOrNothing()
    {
        var state = CreateState(capacity: 0, cash: 1000);

        Assert.False(state.TrySpendCash(Country, 1001));
        Assert.Equal(1000, state.GetCash(Country));

        Assert.True(state.TrySpendCash(Country, 1000));
        Assert.Equal(0, state.GetCash(Country));
    }

    [Fact]
    public void ATreasuryCannotBeSetNegative() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateState(capacity: 0).SetCash(Country, -1));

    /// <summary>
    /// A commodity priced at zero is one that reaches the warehouse, so it is
    /// spelled by leaving the price off rather than by writing a zero.
    /// </summary>
    [Fact]
    public void ACommodityWorthNothingIsNotPricedAtAll() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CommodityDefinition(GoldId, "Gold", CommodityCategory.Raw, 0));

    private static TurnOrders Move(params (int Commodity, long Quantity)[] allocations) => new(
    [
        new CountryTurnOrders(
            Country,
            transport: allocations.Select(static item =>
                new TransportAllocationOrder(new CommodityId(item.Commodity), item.Quantity))),
    ]);

    private static TurnResolution Resolve(WorldState state, TurnOrders orders) =>
        TurnResolver.Resolve(state, orders, 0);

    /// <summary>
    /// A capital, a grain tile, a gold tile and a gems tile, all one country's
    /// and all inside the catchment. One unit of each a turn.
    /// </summary>
    private static WorldState CreateState(
        long capacity,
        long? cash = null,
        long? defaultCash = null,
        bool fairStart = true)
    {
        const int width = 4;
        var dimensions = new MapDimensions(width, 1);
        var deposits = new int?[] { null, Grain, Gold, Gems };
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
                new ResourceDefinition(new ResourceId(Gold), GoldId, [1]),
                new ResourceDefinition(new ResourceId(Gems), GemsId, [1]),
            ],
            [new TerrainDefinition(new TerrainId(0), "Farmland")]);

        var scenario = new ScenarioDefinition(
            "Treasury",
            1815,
            Enumerable.Repeat<CountryId?>(Country, width),
            initialCountryCapitals: [new CountryCapital(Country, new CellIndex(0))],
            defaultStartCountries: fairStart ? [Country] : null,
            initialTransportCapacity: [new InitialTransportCapacity(Country, capacity)],
            initialCash: cash is { } authored ? [new InitialCash(Country, authored)] : null);

        return new WorldState(new WorldDefinition(
            map,
            [new CountryDefinition(Country, "Country 0")],
            scenario,
            [
                new CommodityDefinition(GrainId, "Grain", CommodityCategory.Raw),
                new CommodityDefinition(GoldId, "Gold", CommodityCategory.Raw, GoldRate),
                new CommodityDefinition(GemsId, "Gems", CommodityCategory.Raw, GemsRate),
            ],
            extraction: new ExtractionSettings(width),
            startingDefaults: defaultCash is null
                ? null
                : new StartingDefaults([], cash: defaultCash),
            transport: new TransportSettings([new CommodityQuantity(GrainId, 1)])));
    }
}
