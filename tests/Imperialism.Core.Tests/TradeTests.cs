using Imperialism.Core;
using Xunit;

namespace Imperialism.Core.Tests;

/// <summary>
/// The world market: the manual's central loop, and the first income in this engine
/// worth the name.
/// </summary>
/// <remarks>
/// The fixture is four countries on a 4x1 strip, one province each. Countries 0 and 1
/// are Great Powers; 2 and 3 are minor nations, which own no merchant marine and are
/// what makes the hold-payment rule observable.
/// <para>
/// Three commodities, all with the price list's real figures: coal at 100 (raw, trade
/// order 13), steel at 300 (material, order 9), clothing at 900 (goods, order 1). Grain
/// is the untradable one, priced at nothing. The trade orders are the real ones too, so
/// clothing is always considered before steel and steel before coal — which is what the
/// cargo-hold tests turn on.
/// </para>
/// </remarks>
public sealed class TradeTests
{
    private const int Coal = 0;
    private const int Steel = 1;
    private const int Clothing = 2;
    private const int Grain = 3;

    // The workforce's food, kept separate from grain so that the untradable-commodity
    // test can assert its stock is untouched without workers eating it.
    private const int Fish = 4;

    private const long CoalPrice = 100;
    private const long SteelPrice = 300;
    private const long ClothingPrice = 900;

    private const int Trader = 0;
    private const int Frigate = 1;

    private static readonly CountryId Seller = new(0);
    private static readonly CountryId Buyer = new(1);
    private static readonly CountryId MinorSeller = new(2);
    private static readonly CountryId MinorBuyer = new(3);

    [Fact]
    public void AnOfferMeetsABidAndBothSidesSettleAtTheWorldPrice()
    {
        var state = CreateState(stock: [(Seller, Coal, 10)]);

        var result = Resolve(state, Offer(Seller, Coal, 4), Bid(Buyer, Coal, 4));

        var deal = Assert.Single(result.Events.OfType<CommodityTradedEvent>());
        Assert.Equal(
            (Seller, Buyer, new CommodityId(Coal), 4L, CoalPrice, 400L),
            (deal.Seller, deal.Buyer, deal.Commodity, deal.Quantity, deal.UnitPrice, deal.Total));

        // The seller's stock leaves now and its cash arrives now.
        Assert.Equal(6, state.GetAvailableQuantity(Seller, new CommodityId(Coal)));
        Assert.Equal(10_000 + 400, state.GetCash(Seller));
        Assert.Equal(10_000 - 400, state.GetCash(Buyer));
    }

    /// <summary>
    /// "The commodities you buy appear for your use in the Industry screen next turn."
    /// A purchase is a pending delivery, which is the machinery extraction already used —
    /// the reason buying needed no new state at all.
    /// </summary>
    /// <remarks>
    /// The sharp test of "not this turn" is not where the stock sits at the end of the
    /// turn — <see cref="TurnPhase.Delivery"/> runs long after
    /// <see cref="TurnPhase.Trade"/>, so it is in the warehouse by then, exactly as a
    /// harvest is. It is whether anything during the turn could <em>use</em> it, and
    /// nothing can: both the market and industry read stock before deliveries land.
    /// </remarks>
    [Fact]
    public void WhatIsBoughtCannotBeUsedUntilTheNextTurn()
    {
        var state = CreateState(stock: [(Seller, Coal, 10)]);

        // The buyer bids for four coal and tries to re-offer four in the same turn. The
        // purchase goes through and the re-offer finds an empty warehouse.
        var result = Resolve(
            state,
            Offer(Seller, Coal, 4),
            new CountryTurnOrders(
                Buyer,
                tradeOffers: [new TradeOrder(new CommodityId(Coal), 4)],
                tradeBids: [new TradeOrder(new CommodityId(Coal), 4)]));

        Assert.Equal(4, Assert.Single(result.Events.OfType<CommodityTradedEvent>()).Quantity);
        Assert.Contains(
            result.Events.OfType<TradeUnfilledEvent>(),
            item => item.Country == Buyer && item.Reason == TradeRefusal.NothingToSell);

        var delivered = Assert.Single(result.Events.OfType<CommodityDeliveredEvent>());
        Assert.Equal(PendingDeliverySource.Trade, delivered.Delivery.Source);
        Assert.Equal(4, state.GetAvailableQuantity(Buyer, new CommodityId(Coal)));

        // And the turn after, it is the buyer's to sell.
        var next = Resolve(state, Offer(Buyer, Coal, 4), Bid(Seller, Coal, 4));
        Assert.Equal(4, Assert.Single(next.Events.OfType<CommodityTradedEvent>()).Quantity);
    }

    [Fact]
    public void AnUntradableCommodityIsNeverOffered()
    {
        var state = CreateState(stock: [(Seller, Grain, 10)]);

        var result = Resolve(state, Offer(Seller, Grain, 4), Bid(Buyer, Grain, 4));

        // Not a refusal: an untradable commodity has no row on the screen at all, so
        // there is nothing to report against. Nothing moves and nothing is said.
        Assert.Empty(result.Events.OfType<CommodityTradedEvent>());
        Assert.Empty(result.Events.OfType<TradeUnfilledEvent>());
        Assert.Equal(10, state.GetAvailableQuantity(Seller, new CommodityId(Grain)));
    }

    [Fact]
    public void NothingCanBeSoldThatTheWarehouseDoesNotHold()
    {
        var state = CreateState(stock: [(Seller, Coal, 3)]);

        var result = Resolve(state, Offer(Seller, Coal, 10), Bid(Buyer, Coal, 10));

        // Three sold, and the shortfall reported against the offer rather than silently
        // trimmed.
        Assert.Equal(3, Assert.Single(result.Events.OfType<CommodityTradedEvent>()).Quantity);
        var unfilled = Assert.Single(
            result.Events.OfType<TradeUnfilledEvent>(),
            item => item.Reason == TradeRefusal.NothingToSell);
        Assert.Equal((10L, 3L), (unfilled.Requested, unfilled.Settled));
        Assert.Equal(0, state.GetAvailableQuantity(Seller, new CommodityId(Coal)));
    }

    /// <summary>
    /// "You cannot sell items you do not own <b>or that you have ordered industry to use
    /// this turn</b>" — the screen shows stock "after deduction of the commodities you
    /// have ordered for production". Industry gets first claim.
    /// </summary>
    [Fact]
    public void IndustryClaimsItsInputsBeforeTheMarketSeesThem()
    {
        var state = CreateState(stock: [(Seller, Coal, 4)]);

        // One cycle of the steel recipe eats 2 coal, leaving 2 sellable of the 4 held.
        var result = Resolve(
            state,
            new CountryTurnOrders(
                Seller,
                production: [new ProductionOrder(new ProductionRecipeId(0), 1)],
                tradeOffers: [new TradeOrder(new CommodityId(Coal), 4)]),
            Bid(Buyer, Coal, 4));

        Assert.Equal(2, Assert.Single(result.Events.OfType<CommodityTradedEvent>()).Quantity);
        Assert.Equal(
            TradeRefusal.NothingToSell,
            Assert.Single(result.Events.OfType<TradeUnfilledEvent>()).Reason);

        // 4 held, 2 eaten by industry, 2 sold: nothing left and nothing overdrawn.
        Assert.Equal(0, state.GetAvailableQuantity(Seller, new CommodityId(Coal)));
    }

    /// <summary>
    /// Production's <em>output</em> is not on the market this turn. It reaches the
    /// warehouse when Production commits, which is after Trade — so netting inputs
    /// against outputs would let a country sell steel it has not got.
    /// </summary>
    [Fact]
    public void ThisTurnsOutputCannotBeSoldThisTurn()
    {
        var state = CreateState(stock: [(Seller, Coal, 2)]);

        var result = Resolve(
            state,
            new CountryTurnOrders(
                Seller,
                production: [new ProductionOrder(new ProductionRecipeId(0), 1)],
                tradeOffers: [new TradeOrder(new CommodityId(Steel), 1)]),
            Bid(Buyer, Steel, 1));

        Assert.Empty(result.Events.OfType<CommodityTradedEvent>());
        Assert.Equal(
            TradeRefusal.NothingToSell,
            Assert.Single(result.Events.OfType<TradeUnfilledEvent>()).Reason);

        // The steel exists by the end of the turn -- it just was not sellable during it.
        Assert.Equal(1, state.GetAvailableQuantity(Seller, new CommodityId(Steel)));
    }

    /// <summary>
    /// "You can accept any number up to the amount offered… any commodities remaining
    /// are passed on to other countries that bid on those commodities."
    /// </summary>
    [Fact]
    public void WhatTheFirstBidderLeavesPassesToTheNext()
    {
        var state = CreateState(stock: [(Seller, Coal, 10)]);

        var result = Resolve(
            state,
            Offer(Seller, Coal, 9),
            Bid(Buyer, Coal, 2),
            Bid(MinorSeller, Coal, 3),
            Bid(MinorBuyer, Coal, 99));

        var deals = result.Events.OfType<CommodityTradedEvent>().ToArray();
        Assert.Equal(3, deals.Length);
        Assert.Equal([Buyer, MinorSeller, MinorBuyer], deals.Select(static d => d.Buyer));

        // **2, 3, 3 rather than 2, 3, 4, and the reason is the hold rule biting.** The
        // seller has six holds. The first bidder is a Great Power, so it carries its own
        // two. The next two are minor nations, so the seller carries for both: three, and
        // then only three left of the six. The last unit of the offer strands for want of
        // a hull, not for want of a buyer.
        Assert.Equal([2L, 3L, 3L], deals.Select(static d => d.Quantity));
        Assert.Equal([Buyer, Seller, Seller], deals.Select(static d => d.HoldsPaidBy));

        // One unit of the nine went nowhere.
        var unfilled = Assert.Single(result.Events.OfType<TradeUnfilledEvent>());
        Assert.Equal((Seller, 9L, 8L), (unfilled.Country, unfilled.Requested, unfilled.Settled));
    }

    [Fact]
    public void AnOfferNobodyBidsForGoesUnsold()
    {
        var state = CreateState(stock: [(Seller, Coal, 10)]);

        var result = Resolve(state, Offer(Seller, Coal, 4));

        Assert.Empty(result.Events.OfType<CommodityTradedEvent>());
        var unfilled = Assert.Single(result.Events.OfType<TradeUnfilledEvent>());
        Assert.Equal(
            (Seller, TradeRefusal.NoBuyer, 4L, 0L),
            (unfilled.Country, unfilled.Reason, unfilled.Requested, unfilled.Settled));

        // Unsold stock stays in the warehouse. Unlike transport, nothing is lost.
        Assert.Equal(10, state.GetAvailableQuantity(Seller, new CommodityId(Coal)));
    }

    [Fact]
    public void ABuyerThatCannotPayTakesWhatItCanAfford()
    {
        var state = CreateState(stock: [(Seller, Coal, 10)], cash: 250);

        var result = Resolve(state, Offer(Seller, Coal, 10), Bid(Buyer, Coal, 10));

        // 250 buys two units at 100 and no more.
        Assert.Equal(2, Assert.Single(result.Events.OfType<CommodityTradedEvent>()).Quantity);
        Assert.Equal(50, state.GetCash(Buyer));
    }

    [Fact]
    public void ABuyerWithNoCashIsReportedRatherThanSilentlySkipped()
    {
        var state = CreateState(stock: [(Seller, Coal, 10)], cash: 0);

        var result = Resolve(state, Offer(Seller, Coal, 4), Bid(Buyer, Coal, 4));

        Assert.Empty(result.Events.OfType<CommodityTradedEvent>());
        Assert.Contains(
            result.Events.OfType<TradeUnfilledEvent>(),
            item => item.Country == Buyer && item.Reason == TradeRefusal.NotEnoughCash);
    }

    /// <summary>
    /// "You can buy nothing if you have no merchant marine to move the cargo", and a
    /// bidder without a hold free "is not permitted to accept the deal, and the items are
    /// offered to the next bidder on the list."
    /// </summary>
    [Fact]
    public void ABidderWithNoHoldsIsSkippedAndTheOfferPassesOn()
    {
        // Country 1 owns a Frigate, which carries nothing, so it has a navy and no
        // merchant marine at all. Country 3 is a minor nation, so the Great Power
        // selling to it carries the cargo itself.
        var state = CreateState(
            stock: [(Seller, Coal, 10)],
            ships: [(Seller, Trader, 3), (Buyer, Frigate, 2)]);

        Assert.Equal(0, state.GetMerchantMarine(Buyer));

        var result = Resolve(state, Offer(Seller, Coal, 4), Bid(Buyer, Coal, 4), Bid(MinorBuyer, Coal, 4));

        var deal = Assert.Single(result.Events.OfType<CommodityTradedEvent>());
        Assert.Equal(MinorBuyer, deal.Buyer);
        Assert.Contains(
            result.Events.OfType<TradeUnfilledEvent>(),
            item => item.Country == Buyer && item.Reason == TradeRefusal.NoMerchantCapacity);
    }

    /// <summary>
    /// "The rule for trades between Great Powers is that the buyer always picks up the
    /// commodities." Against a minor nation the Great Power carries either way, because
    /// "no Minor Nation owns merchant marine".
    /// </summary>
    [Fact]
    public void TheBuyerCarriesBetweenGreatPowersAndTheGreatPowerCarriesAgainstAMinor()
    {
        var betweenPowers = CreateState(
            stock: [(Seller, Coal, 10)], ships: [(Seller, Trader, 3), (Buyer, Trader, 3)]);
        var deal = Assert.Single(
            Resolve(betweenPowers, Offer(Seller, Coal, 2), Bid(Buyer, Coal, 2))
                .Events.OfType<CommodityTradedEvent>());
        Assert.Equal(Buyer, deal.HoldsPaidBy);

        // Selling to a minor nation: the seller's own holds move it.
        var toMinor = CreateState(stock: [(Seller, Coal, 10)], ships: [(Seller, Trader, 3)]);
        var toMinorDeal = Assert.Single(
            Resolve(toMinor, Offer(Seller, Coal, 2), Bid(MinorBuyer, Coal, 2))
                .Events.OfType<CommodityTradedEvent>());
        Assert.Equal(Seller, toMinorDeal.HoldsPaidBy);
    }

    /// <summary>
    /// **A hold is spent once a turn**, and the commodity order decides which deals get
    /// them: "clothing deals are always considered prior to all other deals because
    /// clothing is the first item in commodity order. Reserving some cargo holds for
    /// later deals becomes an important skill."
    /// </summary>
    [Fact]
    public void HoldsAreSpentInCommodityOrderAndRunOut()
    {
        // Three holds, and bids for two clothing and two coal. Clothing is first in
        // commodity order, so it takes two of the three and coal gets the one left.
        var state = CreateState(
            stock: [(Seller, Clothing, 10), (Seller, Coal, 10)],
            ships: [(Seller, Trader, 4), (Buyer, Trader, 1), (Buyer, Frigate, 9)],
            cash: 100_000);

        Assert.Equal(2, state.GetMerchantMarine(Buyer));

        var result = Resolve(
            state,
            new CountryTurnOrders(
                Seller,
                tradeOffers:
                [
                    new TradeOrder(new CommodityId(Clothing), 2),
                    new TradeOrder(new CommodityId(Coal), 2),
                ]),
            new CountryTurnOrders(
                Buyer,
                tradeBids:
                [
                    new TradeOrder(new CommodityId(Coal), 2),
                    new TradeOrder(new CommodityId(Clothing), 2),
                ]));

        // Both holds went to clothing, and coal never got one -- even though coal was
        // listed first in this country's own bid list. The world's order wins, not the
        // order the player typed.
        var deal = Assert.Single(result.Events.OfType<CommodityTradedEvent>());
        Assert.Equal((new CommodityId(Clothing), 2L), (deal.Commodity, deal.Quantity));
        Assert.Contains(
            result.Events.OfType<TradeUnfilledEvent>(),
            item => item.Commodity == new CommodityId(Coal) &&
                item.Reason == TradeRefusal.NoMerchantCapacity);
    }

    [Fact]
    public void MerchantMarineIsTheSumOfCargoAndRefillsEachTurn()
    {
        var state = CreateState(
            stock: [(Seller, Coal, 100)],
            ships: [(Seller, Trader, 3), (Seller, Frigate, 5), (Buyer, Trader, 1)],
            cash: 100_000);

        // Three Traders at 2 cargo each; five Frigates carry nothing.
        Assert.Equal(6, state.GetMerchantMarine(Seller));
        Assert.Equal(2, state.GetMerchantMarine(Buyer));

        // Buyer has two holds, so it takes two a turn, twice.
        var first = Resolve(state, Offer(Seller, Coal, 10), Bid(Buyer, Coal, 10));
        Assert.Equal(2, Assert.Single(first.Events.OfType<CommodityTradedEvent>()).Quantity);

        var second = Resolve(state, Offer(Seller, Coal, 10), Bid(Buyer, Coal, 10));
        Assert.Equal(2, Assert.Single(second.Events.OfType<CommodityTradedEvent>()).Quantity);
    }

    /// <summary>
    /// The manual's direction, which is a finding; the step is the guess
    /// <see cref="ITradeMarket"/> exists to isolate.
    /// </summary>
    [Fact]
    public void ThePriceRisesOnDemandFallsOnSupplyAndHoldsWhenMatched()
    {
        var dear = CreateState(stock: [(Seller, Coal, 100)]);
        var rise = Assert.Single(
            Resolve(dear, Offer(Seller, Coal, 1), Bid(Buyer, Coal, 50))
                .Events.OfType<WorldPriceChangedEvent>());
        Assert.True(rise.ToPrice > rise.FromPrice, $"{rise.FromPrice} -> {rise.ToPrice}");

        var cheap = CreateState(stock: [(Seller, Coal, 100)]);
        var fall = Assert.Single(
            Resolve(cheap, Offer(Seller, Coal, 50), Bid(Buyer, Coal, 1))
                .Events.OfType<WorldPriceChangedEvent>());
        Assert.True(fall.ToPrice < fall.FromPrice, $"{fall.FromPrice} -> {fall.ToPrice}");

        // "If supply and demand are closely matched, the price this turn remains much
        // the same as last turn's price."
        var matched = CreateState(stock: [(Seller, Coal, 100)]);
        Assert.Empty(
            Resolve(matched, Offer(Seller, Coal, 10), Bid(Buyer, Coal, 10))
                .Events.OfType<WorldPriceChangedEvent>());
    }

    /// <summary>
    /// A market nobody came to keeps its price. That is not the same as being closely
    /// matched: silence carries no information, and drifting on it would invent a trend.
    /// </summary>
    [Fact]
    public void AnEmptyMarketDoesNotMoveThePrice()
    {
        var state = CreateState(stock: [(Seller, Coal, 10)]);

        Assert.Empty(Resolve(state).Events.OfType<WorldPriceChangedEvent>());
        Assert.Equal(CoalPrice, state.GetWorldPrice(new CommodityId(Coal)));
    }

    /// <summary>
    /// The price carries across turns — this turn opens at what last turn closed on,
    /// because the screen shows "the world market prices … during the previous turn".
    /// </summary>
    [Fact]
    public void ThePriceCarriesIntoTheNextTurn()
    {
        var state = CreateState(stock: [(Seller, Coal, 100)], cash: 1_000_000);

        var first = Assert.Single(
            Resolve(state, Offer(Seller, Coal, 1), Bid(Buyer, Coal, 50))
                .Events.OfType<WorldPriceChangedEvent>());
        Assert.Equal(first.ToPrice, state.GetWorldPrice(new CommodityId(Coal)));

        var second = Assert.Single(
            Resolve(state, Offer(Seller, Coal, 1), Bid(Buyer, Coal, 50))
                .Events.OfType<WorldPriceChangedEvent>());
        Assert.Equal(first.ToPrice, second.FromPrice);
        Assert.True(second.ToPrice > first.ToPrice);
    }

    /// <summary>
    /// A world with no <c>trade</c> market never moves a price, which is how every world
    /// behaved before version 20 — and it still trades, at the opening price forever.
    /// The prices are transcribed and the curve is the guess, so they are separable.
    /// </summary>
    [Fact]
    public void AWorldWithNoMarketStillTradesAtAFixedPrice()
    {
        var state = CreateState(stock: [(Seller, Coal, 10)], withMarket: false);

        var result = Resolve(state, Offer(Seller, Coal, 4), Bid(Buyer, Coal, 40));

        Assert.Equal(CoalPrice, Assert.Single(result.Events.OfType<CommodityTradedEvent>()).UnitPrice);
        Assert.Empty(result.Events.OfType<WorldPriceChangedEvent>());
        Assert.Equal(CoalPrice, state.GetWorldPrice(new CommodityId(Coal)));
    }

    [Fact]
    public void AWorldThatPricesNothingTradesNothing()
    {
        var state = CreateState(stock: [(Seller, Coal, 10)], untradableWorld: true);

        var result = Resolve(state, Offer(Seller, Coal, 4), Bid(Buyer, Coal, 4));

        Assert.Empty(result.Events.OfType<CommodityTradedEvent>());
        Assert.Equal(10, state.GetAvailableQuantity(Seller, new CommodityId(Coal)));
    }

    [Fact]
    public void ACommodityCannotBeOfferedOrBidTwice()
    {
        Assert.Throws<ArgumentException>(() => new CountryTurnOrders(
            Seller,
            tradeOffers:
            [
                new TradeOrder(new CommodityId(Coal), 1),
                new TradeOrder(new CommodityId(Coal), 2),
            ]));

        Assert.Throws<ArgumentException>(() => new CountryTurnOrders(
            Seller,
            tradeBids:
            [
                new TradeOrder(new CommodityId(Coal), 1),
                new TradeOrder(new CommodityId(Coal), 2),
            ]));
    }

    [Fact]
    public void ATradedCommodityCannotAlsoConvertToCashOnCarriage()
    {
        // Gold converts as the network carries it and "cannot be traded". The two are
        // alternatives, so a commodity claiming both would be sold twice.
        Assert.Throws<ArgumentException>(() => new CommodityDefinition(
            new CommodityId(0), "Gold", CommodityCategory.Raw, cashPerUnit: 200, worldPrice: 100,
            tradeOrder: 0));
    }

    [Fact]
    public void ATradedCommodityNeedsAPlaceInTheCommodityOrder()
    {
        Assert.Throws<ArgumentException>(() => new CommodityDefinition(
            new CommodityId(0), "Coal", CommodityCategory.Raw, worldPrice: 100));

        Assert.Throws<ArgumentException>(() => new CommodityDefinition(
            new CommodityId(0), "Grain", CommodityCategory.Raw, tradeOrder: 3));
    }

    [Fact]
    public void TwoCommoditiesCannotShareATradeOrder()
    {
        var exception = Assert.Throws<ArgumentException>(() => CreateState(sharedTradeOrder: true));
        Assert.Contains("trade order", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static CountryTurnOrders Offer(CountryId country, int commodity, long quantity) =>
        new(country, tradeOffers: [new TradeOrder(new CommodityId(commodity), quantity)]);

    private static CountryTurnOrders Bid(CountryId country, int commodity, long quantity) =>
        new(country, tradeBids: [new TradeOrder(new CommodityId(commodity), quantity)]);

    private static TurnResolution Resolve(WorldState state, params CountryTurnOrders[] orders)
    {
        var byCountry = Enumerable.Range(0, state.Definition.Countries.Count)
            .Select(index => orders.FirstOrDefault(item => item.Country.Value == index)
                ?? new CountryTurnOrders(new CountryId(index)))
            .ToArray();
        return TurnResolver.Resolve(state, new TurnOrders(byCountry), 0);
    }

    private static WorldState CreateState(
        (CountryId Country, int Commodity, long Quantity)[]? stock = null,
        (CountryId Country, int Type, long Count)[]? ships = null,
        long cash = 10_000,
        bool withMarket = true,
        bool untradableWorld = false,
        bool sharedTradeOrder = false)
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

        // The price list's own figures, and its own commodity order: clothing first,
        // then steel, then coal. Grain is never traded and carries no price.
        long? Price(long amount) => untradableWorld ? null : amount;
        int? Order(int order) => untradableWorld ? null : order;

        var scenario = new ScenarioDefinition(
            "Trade",
            1840,
            Enumerable.Range(0, countries).Select(static index => (CountryId?)new CountryId(index)),
            initialCountryCapitals: Enumerable.Range(0, countries)
                .Select(static index => new CountryCapital(new CountryId(index), new CellIndex(index))),
            // Every country is given fish so its workers never go hungry: the workforce
            // exists only to supply the labour production needs, and starvation would be
            // a second variable in a trade test.
            initialInventory: (stock ?? [])
                .Select(static item => new InitialCommodityStock(
                    item.Country, new CommodityId(item.Commodity), item.Quantity))
                .Concat(Enumerable.Range(0, countries)
                    .Select(static index => new InitialCommodityStock(
                        new CountryId(index), new CommodityId(Fish), 500))),
            initialWorkforce: Enumerable.Range(0, countries)
                .Select(static index => new InitialWorkforce(new CountryId(index), 4, 0, 0)),
            initialCash: Enumerable.Range(0, countries)
                .Select(index => new InitialCash(new CountryId(index), cash)),
            // Three Traders each unless a test says otherwise, which is what all three
            // shipped skirmishes give every power — six cargo holds. A fixture with no
            // ships would trade nothing at all, since "you can buy nothing if you have
            // no merchant marine".
            initialShips: (ships ?? Enumerable.Range(0, countries)
                    .Select(static index => (Country: new CountryId(index), Type: Trader, Count: 3L))
                    .ToArray())
                .Select(static item => new InitialShip(
                    item.Country, new ShipTypeId(item.Type), 0, item.Count)));

        return new WorldState(new WorldDefinition(
            map,
            [
                new CountryDefinition(new CountryId(0), "Power 0", isGreatPower: true),
                new CountryDefinition(new CountryId(1), "Power 1", isGreatPower: true),
                new CountryDefinition(new CountryId(2), "Minor 2"),
                new CountryDefinition(new CountryId(3), "Minor 3"),
            ],
            scenario,
            [
                new CommodityDefinition(
                    new CommodityId(Coal), "Coal", CommodityCategory.Raw,
                    worldPrice: Price(CoalPrice), tradeOrder: Order(13)),
                new CommodityDefinition(
                    new CommodityId(Steel), "Steel", CommodityCategory.Material,
                    worldPrice: Price(SteelPrice), tradeOrder: Order(sharedTradeOrder ? 13 : 9)),
                new CommodityDefinition(
                    new CommodityId(Clothing), "Clothing", CommodityCategory.Goods,
                    worldPrice: Price(ClothingPrice), tradeOrder: Order(1)),

                // Raw food, which "cannot be traded on the world market".
                new CommodityDefinition(new CommodityId(Grain), "Grain", CommodityCategory.Raw),
                new CommodityDefinition(new CommodityId(Fish), "Fish", CommodityCategory.Raw),
            ],
            [
                new ProductionFacilityDefinition(
                    new ProductionFacilityId(0), "Steel Mill", ProductionCapacityMode.Unlimited),
            ],
            [
                // Two inputs, two labour — the project's rate, from the manual's one
                // priced recipe.
                new ProductionRecipeDefinition(
                    new ProductionRecipeId(0),
                    "Steel",
                    new ProductionFacilityId(0),
                    capacityCost: 1,
                    labourCost: 2,
                    [new CommodityQuantity(new CommodityId(Coal), 2)],
                    [new CommodityQuantity(new CommodityId(Steel), 1)]),
            ],
            feeding: new FeedingSettings(
                [new FoodPreference([new CommodityId(Fish)])],
                [1, 2, 4]),
            shipTypes:
            [
                new ShipTypeDefinition(new ShipTypeId(Trader), "Trader", cargo: 2),
                new ShipTypeDefinition(new ShipTypeId(Frigate), "Frigate"),
            ],
            trade: withMarket ? new ProportionalTradeMarket() : null));
    }
}
