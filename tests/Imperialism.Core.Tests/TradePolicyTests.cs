using Imperialism.Core;
using Xunit;

namespace Imperialism.Core.Tests;

/// <summary>
/// Trade subsidies, voluntary boycotts and the bidder ranking. See the trade-policy section
/// of <c>docs/formulas/diplomacy.md</c>.
/// </summary>
/// <remarks>
/// The same four countries as <see cref="DiplomacyTests"/>: 0 and 1 are Great Powers holding
/// an embassy in each other, 2 and 3 Minor Nations. Coal's world price is held fixed by a
/// market that never moves it, so every total below is exact arithmetic on a known price.
/// </remarks>
public sealed class TradePolicyTests
{
    private const int Coal = 0;
    private const int Fish = 1;
    private const long ConsulateCost = 500;
    private const long StartingCash = 10_000;

    private static readonly CountryId Power = new(0);
    private static readonly CountryId Rival = new(1);
    private static readonly CountryId Minor = new(2);
    private static readonly CountryId OtherMinor = new(3);

    [Fact]
    public void ASubsidyIsFirstFeltByNextTurnsMarket()
    {
        var state = CreateState();
        OpenConsulate(state, Power, Minor);

        // Set this turn: this turn's market still trades at the world price.
        var sameTurn = Resolve(
            state,
            Offer(Minor, 2),
            new CountryTurnOrders(Power, tradeBids: [Bid(2)], tradePolicies: [Subsidy(Minor, 10)]));
        Assert.Single(sameTurn.Events.OfType<TradePolicySetEvent>());
        Assert.Equal(200, Assert.Single(sameTurn.Events.OfType<CommodityTradedEvent>()).Total);
        Assert.Equal(TradePolicy.Subsidy(10), state.GetTradePolicy(Power, Minor));

        // Next turn the Minor Nation sells to its grantor at 10% over.
        var (powerCash, minorCash) = (state.GetCash(Power), state.GetCash(Minor));
        var next = Resolve(state, Offer(Minor, 2), new CountryTurnOrders(Power, tradeBids: [Bid(2)]));

        var deal = Assert.Single(next.Events.OfType<CommodityTradedEvent>());
        Assert.Equal((100L, 220L), (deal.UnitPrice, deal.Total));
        Assert.Equal(powerCash - 220, state.GetCash(Power));
        Assert.Equal(minorCash + 220, state.GetCash(Minor));
    }

    [Fact]
    public void ASubsidisedMinorNationBuysUnderAndSellsOverTheWorldPrice()
    {
        var state = CreateState();
        OpenConsulate(state, Power, Minor);
        _ = Resolve(state, Policies(Power, Subsidy(Minor, 5)));

        // Buying from the grantor: 5% under.
        var (powerCash, minorCash) = (state.GetCash(Power), state.GetCash(Minor));
        var bought = Resolve(state, Offer(Power, 2), new CountryTurnOrders(Minor, tradeBids: [Bid(2)]));
        var purchase = Assert.Single(bought.Events.OfType<CommodityTradedEvent>());
        Assert.Equal((Power, Minor, 190L), (purchase.Seller, purchase.Buyer, purchase.Total));
        Assert.Equal(powerCash + 190, state.GetCash(Power));
        Assert.Equal(minorCash - 190, state.GetCash(Minor));

        // Selling to the grantor: 5% over.
        (powerCash, minorCash) = (state.GetCash(Power), state.GetCash(Minor));
        var sold = Resolve(state, Offer(Minor, 2), new CountryTurnOrders(Power, tradeBids: [Bid(2)]));
        var sale = Assert.Single(sold.Events.OfType<CommodityTradedEvent>());
        Assert.Equal((Minor, Power, 210L), (sale.Seller, sale.Buyer, sale.Total));
        Assert.Equal(powerCash - 210, state.GetCash(Power));
        Assert.Equal(minorCash + 210, state.GetCash(Minor));
    }

    [Fact]
    public void ASubsidyFavoursOnlyItsRecipient()
    {
        // The same deal between the grantor and a country it does not subsidise is
        // at the world price.
        var state = CreateState();
        OpenConsulate(state, Power, Minor);
        _ = Resolve(state, Policies(Power, Subsidy(Minor, 10)));

        var result = Resolve(state, Offer(OtherMinor, 2), new CountryTurnOrders(Power, tradeBids: [Bid(2)]));

        Assert.Equal(200, Assert.Single(result.Events.OfType<CommodityTradedEvent>()).Total);
    }

    [Fact]
    public void ADiscountIsAffordedByDivisionAtTheSubsidisedPrice()
    {
        // $190 buys two units at 5% under 100, not one at the world price.
        var state = CreateState(minorCash: 190);
        OpenConsulate(state, Power, Minor);
        _ = Resolve(state, Policies(Power, Subsidy(Minor, 5)));

        var result = Resolve(state, Offer(Power, 2), new CountryTurnOrders(Minor, tradeBids: [Bid(2)]));

        Assert.Equal(2, Assert.Single(result.Events.OfType<CommodityTradedEvent>()).Quantity);
        Assert.Equal(0, state.GetCash(Minor));
    }

    [Fact]
    public void TheBidderWhoSubsidisesTheSellerGetsFirstRefusal()
    {
        var state = CreateState();
        OpenConsulate(state, Rival, Minor);

        // No subsidy anywhere: country id order, so the Power takes the whole offer.
        var unranked = Deals(Resolve(state, Offer(Minor, 2), Bids(Power, 2), Bids(Rival, 2)));
        Assert.Equal([(Minor, Power, 2L)], unranked);

        _ = Resolve(state, Policies(Rival, Subsidy(Minor, 10)));
        var ranked = Deals(Resolve(state, Offer(Minor, 2), Bids(Power, 2), Bids(Rival, 2)));
        Assert.Equal([(Minor, Rival, 2L)], ranked);

        // Cancelled, the id order returns.
        _ = Resolve(state, Policies(Rival, new TradePolicyOrder(Minor, TradePolicy.None)));
        var cancelled = Deals(Resolve(state, Offer(Minor, 2), Bids(Power, 2), Bids(Rival, 2)));
        Assert.Equal([(Minor, Power, 2L)], cancelled);
    }

    [Fact]
    public void TheLargerSubsidyRanksFirstAndWhatIsLeftPassesDown()
    {
        var state = CreateState();
        OpenConsulate(state, Power, Minor);
        OpenConsulate(state, Rival, Minor);
        _ = Resolve(state, Policies(Power, Subsidy(Minor, 5)), Policies(Rival, Subsidy(Minor, 10)));

        var deals = Deals(Resolve(state, Offer(Minor, 3), Bids(Power, 2), Bids(Rival, 2)));

        Assert.Equal([(Minor, Rival, 2L), (Minor, Power, 1L)], deals);
    }

    [Fact]
    public void ABoycottBlocksDealsBothWaysAndTheOfferPassesOn()
    {
        var state = CreateState();
        OpenConsulate(state, Power, Minor);
        _ = Resolve(state, Policies(Power, Boycott(Minor)));

        // The Minor Nation's coal passes the boycotting Power by and goes to the Rival.
        var selling = Deals(Resolve(state, Offer(Minor, 2), Bids(Power, 2), Bids(Rival, 2)));
        Assert.Equal([(Minor, Rival, 2L)], selling);

        // And the Power's coal does not reach the Minor Nation either.
        var buying = Resolve(state, Offer(Power, 2), new CountryTurnOrders(Minor, tradeBids: [Bid(2)]));
        Assert.Empty(buying.Events.OfType<CommodityTradedEvent>());
    }

    [Fact]
    public void ABoycottSetByEitherSideOfThePairBlocksIt()
    {
        // The Rival boycotts the Power. Deals with the Power as seller and as buyer both stop.
        var state = CreateState();
        _ = Resolve(state, Policies(Rival, Boycott(Power)));
        Assert.True(state.IsTradeBoycotted(Power, Rival));
        Assert.True(state.IsTradeBoycotted(Rival, Power));

        var powerSells = Resolve(state, Offer(Power, 2), Bids(Rival, 2));
        Assert.Empty(powerSells.Events.OfType<CommodityTradedEvent>());

        var rivalSells = Resolve(state, Offer(Rival, 2), Bids(Power, 2));
        Assert.Empty(rivalSells.Events.OfType<CommodityTradedEvent>());
    }

    [Fact]
    public void RefusedPoliciesCostNothingAndDoNotStopLaterOnes()
    {
        var state = CreateState();

        var result = Resolve(
            state,
            new CountryTurnOrders(
                Power,
                tradePolicies:
                [
                    Subsidy(Minor, 10),           // no consulate in the Minor Nation
                    Subsidy(Rival, 7),            // the world lists 5 and 10 only
                    Boycott(Power),               // itself
                    new TradePolicyOrder(new CountryId(99), TradePolicy.Boycott),
                    Subsidy(Rival, 5),            // set
                    Boycott(Rival),               // a second order toward the Rival
                ]),
            Policies(Minor, Boycott(Power)));    // a Minor Nation sets nothing

        Assert.Equal(
            [
                (Power, Minor, TradePolicyRefusal.NoPost),
                (Power, Rival, TradePolicyRefusal.PercentNotOffered),
                (Power, Power, TradePolicyRefusal.Itself),
                (Power, new CountryId(99), TradePolicyRefusal.NoSuchCountry),
                (Power, Rival, TradePolicyRefusal.AlreadyOrderedThisTurn),
                (Minor, Power, TradePolicyRefusal.NotAGreatPower),
            ],
            result.Events.OfType<TradePolicyRefusedEvent>().Select(static e => (e.Country, e.Target, e.Reason)));
        var set = Assert.Single(result.Events.OfType<TradePolicySetEvent>());
        Assert.Equal((Power, Rival, TradePolicy.Subsidy(5)), (set.Country, set.Target, set.Policy));

        Assert.Equal(TradePolicy.Subsidy(5), state.GetTradePolicy(Power, Rival));
        Assert.Equal(TradePolicy.None, state.GetTradePolicy(Power, Minor));
        Assert.Equal(TradePolicy.None, state.GetTradePolicy(Minor, Power));
        Assert.Equal(StartingCash, state.GetCash(Power));
        Assert.Equal(StartingCash, state.GetCash(Minor));
    }

    [Fact]
    public void AConsulateBoughtThisTurnCarriesNoPolicyUntilNextTurn()
    {
        var state = CreateState();

        var result = Resolve(
            state,
            new CountryTurnOrders(
                Power,
                overtures: [new DiplomaticOverture(DiplomaticOvertureKind.Consulate, Minor)],
                tradePolicies: [Subsidy(Minor, 5)]));

        Assert.Equal(
            TradePolicyRefusal.NoPost,
            Assert.Single(result.Events.OfType<TradePolicyRefusedEvent>()).Reason);

        var next = Resolve(state, Policies(Power, Subsidy(Minor, 5)));
        Assert.Single(next.Events.OfType<TradePolicySetEvent>());
    }

    [Fact]
    public void AWorldThatListsNoPercentagesGrantsNoSubsidyButStillBoycotts()
    {
        // What a package migrated from version 30 compiles to.
        var state = CreateState(subsidyPercents: []);

        var result = Resolve(state, Policies(Power, Subsidy(Rival, 5), Boycott(Rival)));

        Assert.Equal(
            TradePolicyRefusal.PercentNotOffered,
            Assert.Single(result.Events.OfType<TradePolicyRefusedEvent>()).Reason);
        Assert.Equal(TradePolicy.Boycott, state.GetTradePolicy(Power, Rival));
    }

    [Fact]
    public void AWarBetweenGreatPowersClearsThePoliciesBothWays()
    {
        var state = CreateState();
        _ = Resolve(state, Policies(Power, Subsidy(Rival, 10)), Policies(Rival, Boycott(Power)));

        var result = Resolve(state, new CountryTurnOrders(Power, declareWar: [Rival]));

        Assert.Equal(
            [(Power, Rival, TradePolicy.Subsidy(10)), (Rival, Power, TradePolicy.Boycott)],
            result.Events.OfType<TradePolicyClearedEvent>().Select(static e => (e.Country, e.Target, e.Previous)));
        Assert.Equal(TradePolicy.None, state.GetTradePolicy(Power, Rival));
        Assert.Equal(TradePolicy.None, state.GetTradePolicy(Rival, Power));
    }

    [Fact]
    public void AWarOnAMinorNationClosesNoPostAndClearsNoPolicy()
    {
        var state = CreateState();
        OpenConsulate(state, Power, Minor);
        _ = Resolve(state, Policies(Power, Subsidy(Minor, 5)));

        var result = Resolve(state, new CountryTurnOrders(Power, declareWar: [Minor]));

        Assert.Empty(result.Events.OfType<TradePolicyClearedEvent>());
        Assert.Equal(TradePolicy.Subsidy(5), state.GetTradePolicy(Power, Minor));
    }

    [Fact]
    public void BetweenGreatPowersASubsidyLowersOnlyTheGrantorsSalePrice()
    {
        // The release-notes rule (docs/game-systems.md:204-206): the Power grants the Rival
        // 10%, which lowers what the Rival pays the Power and does not change what the Power
        // pays the Rival.
        var state = CreateState();
        _ = Resolve(state, Policies(Power, Subsidy(Rival, 10)));

        // The Rival buys from its grantor at 10% under: 2 × 100 × 90% = 180.
        var (powerCash, rivalCash) = (state.GetCash(Power), state.GetCash(Rival));
        var bought = Resolve(state, Offer(Power, 2), Bids(Rival, 2));
        var purchase = Assert.Single(bought.Events.OfType<CommodityTradedEvent>());
        Assert.Equal((Power, Rival, 180L), (purchase.Seller, purchase.Buyer, purchase.Total));
        Assert.Equal(powerCash + 180, state.GetCash(Power));
        Assert.Equal(rivalCash - 180, state.GetCash(Rival));

        // The Rival sells to its grantor at the world price exactly: 2 × 100 = 200.
        (powerCash, rivalCash) = (state.GetCash(Power), state.GetCash(Rival));
        var sold = Resolve(state, Offer(Rival, 2), Bids(Power, 2));
        var sale = Assert.Single(sold.Events.OfType<CommodityTradedEvent>());
        Assert.Equal((Rival, Power, 200L), (sale.Seller, sale.Buyer, sale.Total));
        Assert.Equal(powerCash - 200, state.GetCash(Power));
        Assert.Equal(rivalCash + 200, state.GetCash(Rival));
    }

    [Fact]
    public void ASubsidyToAGreatPowerDoesNotRankItsGrantorAsABidder()
    {
        // Country 3 is a third Great Power here, so the grantor's id is above another
        // bidder's and a ranking by its subsidy would show. It grants the Power 10%; when
        // the Power sells, that subsidy moves no price, so it ranks nobody: id order, and
        // the Rival takes the whole offer.
        var third = OtherMinor;
        var state = CreateState(otherMinorIsGreatPower: true);
        _ = Resolve(state, Policies(third, Subsidy(Power, 10)));
        Assert.Equal(TradePolicy.Subsidy(10), state.GetTradePolicy(third, Power));

        var deals = Deals(Resolve(state, Offer(Power, 2), Bids(Rival, 2), Bids(third, 2)));
        Assert.Equal([(Power, Rival, 2L)], deals);

        // And when the Power's offer does reach its grantor, at the world price exactly.
        var result = Resolve(state, Offer(Power, 2), Bids(third, 2));
        var deal = Assert.Single(result.Events.OfType<CommodityTradedEvent>());
        Assert.Equal((Power, third, 200L), (deal.Seller, deal.Buyer, deal.Total));
    }

    [Fact]
    public void EachDealRoundsOnceTowardItsRecipient()
    {
        // At 101 a unit and three units, exact totals carry a fraction.
        var state = CreateState(worldPrice: 101);
        OpenConsulate(state, Power, Minor);
        _ = Resolve(state, Policies(Power, Subsidy(Minor, 5)));

        // The Minor Nation sells: 3 × 101 × 105% = 318.15, rounded up for it to 319. Per
        // unit it would be 3 × 107 = 321 rounded up, or 3 × 106 = 318 rounded down.
        var (powerCash, minorCash) = (state.GetCash(Power), state.GetCash(Minor));
        var sold = Resolve(state, Offer(Minor, 3), Bids(Power, 3));
        Assert.Equal(319, Assert.Single(sold.Events.OfType<CommodityTradedEvent>()).Total);
        Assert.Equal(powerCash - 319, state.GetCash(Power));
        Assert.Equal(minorCash + 319, state.GetCash(Minor));

        // The Minor Nation buys: 3 × 101 × 95% = 287.85, rounded down for it to 287.
        (powerCash, minorCash) = (state.GetCash(Power), state.GetCash(Minor));
        var bought = Resolve(state, Offer(Power, 3), new CountryTurnOrders(Minor, tradeBids: [Bid(3)]));
        Assert.Equal(287, Assert.Single(bought.Events.OfType<CommodityTradedEvent>()).Total);
        Assert.Equal(powerCash + 287, state.GetCash(Power));
        Assert.Equal(minorCash - 287, state.GetCash(Minor));
    }

    [Fact]
    public void ASubsidyDoesNotMoveTheWorldPrice()
    {
        // Two worlds, one with a subsidy in effect, trade the same volumes under the
        // moving market; the price each carries into the next turn is the same.
        long NextPrice(bool subsidised)
        {
            var state = CreateState(market: new ProportionalTradeMarket());
            OpenConsulate(state, Power, Minor);
            if (subsidised)
            {
                _ = Resolve(state, Policies(Power, Subsidy(Minor, 10)));
            }
            else
            {
                _ = Resolve(state, new CountryTurnOrders(Power));
            }

            var before = state.GetWorldPrice(new CommodityId(Coal));
            var result = Resolve(state, Offer(Minor, 1), Bids(Power, 6));
            Assert.Equal(subsidised ? 110 : 100, Assert.Single(result.Events.OfType<CommodityTradedEvent>()).Total);
            Assert.Equal(100, before);
            return state.GetWorldPrice(new CommodityId(Coal));
        }

        var plain = NextPrice(subsidised: false);
        Assert.NotEqual(100, plain);
        Assert.Equal(plain, NextPrice(subsidised: true));
    }

    [Fact]
    public void ASubsidyNoWorldCouldListIsMalformed()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TradePolicy.Subsidy(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => TradePolicy.Subsidy(100));
    }

    private static void OpenConsulate(WorldState state, CountryId owner, CountryId host)
    {
        var result = Resolve(
            state,
            new CountryTurnOrders(owner, overtures: [new DiplomaticOverture(DiplomaticOvertureKind.Consulate, host)]));
        Assert.Single(result.Events.OfType<DiplomaticOvertureAcceptedEvent>());
    }

    private static TradePolicyOrder Subsidy(CountryId target, int percent) =>
        new(target, TradePolicy.Subsidy(percent));

    private static TradePolicyOrder Boycott(CountryId target) => new(target, TradePolicy.Boycott);

    private static CountryTurnOrders Policies(CountryId country, params TradePolicyOrder[] orders) =>
        new(country, tradePolicies: orders);

    private static TradeOrder Bid(long quantity) => new(new CommodityId(Coal), quantity);

    private static CountryTurnOrders Offer(CountryId country, long quantity) =>
        new(country, tradeOffers: [Bid(quantity)]);

    private static CountryTurnOrders Bids(CountryId country, long quantity) =>
        new(country, tradeBids: [Bid(quantity)]);

    private static (CountryId Seller, CountryId Buyer, long Quantity)[] Deals(TurnResolution resolution) =>
        resolution.Events.OfType<CommodityTradedEvent>()
            .Select(static e => (e.Seller, e.Buyer, e.Quantity))
            .ToArray();

    private static TurnResolution Resolve(
        WorldState state, CountryTurnOrders first, params CountryTurnOrders[] rest)
    {
        CountryTurnOrders[] orders = [first, .. rest];
        var byCountry = Enumerable.Range(0, state.Definition.Countries.Count)
            .Select(index => orders.FirstOrDefault(item => item.Country.Value == index)
                ?? new CountryTurnOrders(new CountryId(index)))
            .ToArray();
        return TurnResolver.Resolve(state, new TurnOrders(byCountry), 0);
    }

    /// <summary>A market whose prices never move, so every expected total is exact.</summary>
    private sealed class FixedMarket : ITradeMarket
    {
        public long NextPrice(CommodityDefinition commodity, long currentPrice, long offered, long bid) =>
            currentPrice;
    }

    private static WorldState CreateState(
        long worldPrice = 100,
        long minorCash = StartingCash,
        int[]? subsidyPercents = null,
        ITradeMarket? market = null,
        bool otherMinorIsGreatPower = false)
    {
        const int countries = 4;
        var dimensions = new MapDimensions(countries, 1);
        var cells = Enumerable.Range(0, countries)
            .Select(index => new CellDefinition(
                new CellIndex(index),
                dimensions.GetCoordinate(new CellIndex(index)),
                new TerrainId(0),
                CellRegion.ForProvince(new ProvinceId(index)),
                null,
                SettlementSiteKind.Urban))
            .ToArray();

        var map = new MapDefinition(
            dimensions,
            cells,
            Enumerable.Range(0, countries)
                .Select(static index => new ProvinceDefinition(new ProvinceId(index), $"P{index}")),
            [],
            [],
            [new TerrainDefinition(new TerrainId(0), "Plains")]);

        var scenario = new ScenarioDefinition(
            "Trade policy",
            1840,
            Enumerable.Range(0, countries).Select(static index => (CountryId?)new CountryId(index)),
            initialCountryCapitals: Enumerable.Range(0, countries)
                .Select(static index => new CountryCapital(new CountryId(index), new CellIndex(index))),
            initialInventory: Enumerable.Range(0, countries)
                .SelectMany(static index => new[]
                {
                    new InitialCommodityStock(new CountryId(index), new CommodityId(Coal), 10),
                    new InitialCommodityStock(new CountryId(index), new CommodityId(Fish), 500),
                }),
            initialWorkforce: Enumerable.Range(0, countries)
                .Select(static index => new InitialWorkforce(new CountryId(index), 4, 0, 0)),
            initialCash: Enumerable.Range(0, countries)
                .Select(index => new InitialCash(
                    new CountryId(index), new CountryId(index) == Minor ? minorCash : StartingCash)),
            initialShips: Enumerable.Range(0, countries)
                .Select(static index => new InitialShip(new CountryId(index), new ShipTypeId(0), 0, 3)));

        return new WorldState(new WorldDefinition(
            map,
            [
                new CountryDefinition(Power, "Power", isGreatPower: true),
                new CountryDefinition(Rival, "Rival", isGreatPower: true),
                new CountryDefinition(Minor, "Minor"),
                new CountryDefinition(OtherMinor, "Other Minor", isGreatPower: otherMinorIsGreatPower),
            ],
            scenario,
            [
                new CommodityDefinition(
                    new CommodityId(Coal), "Coal", CommodityCategory.Raw, worldPrice: worldPrice, tradeOrder: 1),
                new CommodityDefinition(new CommodityId(Fish), "Fish", CommodityCategory.Raw),
            ],
            null,
            null,
            feeding: new FeedingSettings(
                [new FoodPreference([new CommodityId(Fish)])],
                [1, 2, 4]),
            shipTypes: [new ShipTypeDefinition(new ShipTypeId(0), "Trader", cargo: 2)],
            trade: market ?? new FixedMarket(),
            diplomacy: new DiplomacySettings(ConsulateCost, 5_000, subsidyPercents ?? [5, 10])));
    }
}
