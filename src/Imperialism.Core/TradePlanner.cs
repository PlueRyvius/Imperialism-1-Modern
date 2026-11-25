namespace Imperialism.Core;

/// <summary>One deal that cleared: a seller, a buyer, a commodity and a price.</summary>
/// <remarks>
/// <see cref="UnitPrice"/> is the world price; <see cref="Total"/> is what actually changed
/// hands, which differs from <c>Quantity × UnitPrice</c> only under a subsidy.
/// </remarks>
internal sealed record PlannedTrade(
    CountryId Seller,
    CountryId Buyer,
    CommodityId Commodity,
    long Quantity,
    long UnitPrice,
    CountryId HoldsPaidBy,
    long Total);

/// <summary>One country's row that did not get what it asked for, and why.</summary>
internal sealed record PlannedTradeShortfall(
    CountryId Country,
    CommodityId Commodity,
    long Requested,
    long Settled,
    TradeRefusal Reason);

/// <summary>A commodity's price moving, and what moved it.</summary>
internal sealed record PlannedPriceMove(
    CommodityId Commodity,
    long FromPrice,
    long ToPrice,
    long Offered,
    long Bid);

internal sealed class TradePlan
{
    public TradePlan(
        long[] inventoryDeltas,
        IEnumerable<PlannedTrade> trades,
        IEnumerable<PlannedTradeShortfall> shortfalls,
        IEnumerable<PlannedPriceMove> priceMoves)
    {
        InventoryDeltas = inventoryDeltas;
        Trades = Array.AsReadOnly(trades.ToArray());
        Shortfalls = Array.AsReadOnly(shortfalls.ToArray());
        PriceMoves = Array.AsReadOnly(priceMoves.ToArray());
    }

    /// <summary>
    /// What sales take out of warehouses, country-major like every other plan's.
    /// <b>Purchases are not here</b>: they arrive next turn as pending deliveries, so
    /// they are not a change to this turn's stock.
    /// </summary>
    public long[] InventoryDeltas { get; }

    public IReadOnlyList<PlannedTrade> Trades { get; }

    public IReadOnlyList<PlannedTradeShortfall> Shortfalls { get; }

    public IReadOnlyList<PlannedPriceMove> PriceMoves { get; }
}

/// <summary>
/// The world market: matches what every country offered against what every country bid,
/// moves the money and the goods, and sets next turn's price.
/// </summary>
/// <remarks>
/// This is the manual's central loop and the first income in this engine worth the name.
/// The shape it models:
/// <list type="number">
/// <item>Every country submits offers to sell and bids to buy, at no price of its own —
/// "it is impossible to predict the final price for this turn".</item>
/// <item>Each seller's offer is presented to the bidders <em>in turn</em>, and each may
/// take any part of it — "you can accept any number up to the amount offered". What is
/// left "passes to the next coal-bidding country".</item>
/// <item>Carrying it costs cargo holds, which are finite, spent in a fixed commodity
/// order, and refill next turn.</item>
/// <item>Goods sold leave the warehouse now; goods bought arrive next turn.</item>
/// <item>The price for next turn answers to what was offered and bid.</item>
/// </list>
/// <para>
/// <b>The bidder ranking is half the manual's rule.</b> An offer goes first to the
/// seller's "most favoured trading partner" (lines 2976-2983), and "subsidised prices and
/// improved diplomatic relations both affect the order" (lines 3031-3033). Subsidies are
/// modelled; relation scores are not. So each seller ranks its bidders by the subsidy each
/// grants it, highest first, with ties — every unsubsidised bidder among them — in country
/// id order. That is a chosen rule pending relations; see <c>docs/formulas/diplomacy.md</c>.
/// </para>
/// <para>
/// <b>A subsidy moves the price of a deal, never the world price.</b> The world price
/// answers to the volumes offered and bid, as it always did.
/// </para>
/// <para>
/// <b>This planner does not touch the world.</b> It works against a local copy of every
/// treasury and returns the deals; <see cref="TurnResolver"/> applies them during
/// <see cref="TurnPhase.Trade"/>. That is what lets its inventory deltas join the
/// combined preflight before anything is committed, the same way production's,
/// expansion's, the railyard's and migration's already do.
/// </para>
/// </remarks>
internal static class TradePlanner
{
    /// <summary>
    /// Plans the turn's trading. <paramref name="claimed"/> is country-major and
    /// non-negative: how much of each commodity is already spoken for before the market
    /// opens.
    /// </summary>
    /// <remarks>
    /// It exists because the manual's screen shows the warehouse "after deduction of the
    /// commodities you have ordered for production on the Industry screen" — industry
    /// gets first claim and "you cannot sell items you do not own or that you have
    /// ordered industry to use this turn".
    /// <para>
    /// It counts what production and building will <em>consume</em> and deliberately not
    /// what production will <em>make</em>. Netting the two would let a country sell
    /// output it has not received: production's own deltas carry its outputs as a credit,
    /// and those do not reach the warehouse until <see cref="TurnPhase.Production"/>
    /// commits, which is after this.
    /// </para>
    /// </remarks>
    public static TradePlan Create(
        WorldState state,
        TurnOrders orders,
        long[] claimed,
        long[]? cashClaimed = null)
    {
        var definition = state.Definition;
        var commodityCount = definition.Commodities.Count;
        var countryCount = orders.Count;
        var deltas = new long[claimed.Length];
        var trades = new List<PlannedTrade>();
        var shortfalls = new List<PlannedTradeShortfall>();
        var priceMoves = new List<PlannedPriceMove>();

        // A local treasury per country, so a deal is checked and paid for in one place
        // and nothing is committed until the phase runs.
        var cash = new long[countryCount];

        // Cargo holds are per country and per turn: "each cargo hold can be used only
        // once per turn". A country with no ships has none, and the manual is blunt
        // about what that means — "you can buy nothing if you have no merchant marine".
        var holds = new long[countryCount];
        for (var index = 0; index < countryCount; index++)
        {
            var country = new CountryId(index);
            cash[index] = state.GetCash(country) - (cashClaimed?[index] ?? 0);
            holds[index] = state.GetMerchantMarine(country);
        }

        // Worked in the original's fixed commodity order rather than in id order,
        // because that order decides which deals get the holds: "clothing deals are
        // always considered prior to all other deals". Reserving holds for later deals
        // is a real skill in the original, and it only exists because this is stable.
        var tradable = definition.Commodities
            .Where(static commodity => commodity.TradeOrder is not null)
            .OrderBy(static commodity => commodity.TradeOrder!.Value)
            .ToArray();

        foreach (var commodity in tradable)
        {
            var price = state.GetWorldPrice(commodity.Id);
            var offers = new long[countryCount];
            var bids = new long[countryCount];
            var offered = 0L;
            var bid = 0L;

            for (var index = 0; index < countryCount; index++)
            {
                var country = new CountryId(index);
                var offset = (index * commodityCount) + commodity.Id.Value;

                if (Find(orders[country].TradeOffers, commodity.Id) is { } offer)
                {
                    // Trimmed to what the warehouse can actually part with, the same way
                    // a transport slider is trimmed to what was gathered.
                    var sellable = Math.Clamp(
                        state.GetAvailableQuantity(country, commodity.Id) - claimed[offset],
                        0,
                        offer);
                    offers[index] = sellable;
                    offered += sellable;
                    if (sellable < offer)
                    {
                        shortfalls.Add(new PlannedTradeShortfall(
                            country, commodity.Id, offer, sellable, TradeRefusal.NothingToSell));
                    }
                }

                if (Find(orders[country].TradeBids, commodity.Id) is { } want)
                {
                    bids[index] = want;
                    bid += want;
                }
            }

            // Every seller in turn, and for each, every bidder in turn — which is the
            // offer sheet passing down the list.
            for (var sellerIndex = 0; sellerIndex < countryCount; sellerIndex++)
            {
                var remaining = offers[sellerIndex];
                if (remaining == 0)
                {
                    continue;
                }

                var seller = new CountryId(sellerIndex);
                foreach (var buyerIndex in RankBidders(state, seller, countryCount))
                {
                    if (remaining <= 0)
                    {
                        break;
                    }

                    if (bids[buyerIndex] == 0)
                    {
                        continue;
                    }

                    var buyer = new CountryId(buyerIndex);

                    // One gate, two causes. "A trade boycott goes into effect
                    // automatically between countries at war" (manual lines 3714-3715) —
                    // only a war in effect: one declared this turn begins next turn, and
                    // peace made this turn leaves it in effect until then. And a chosen
                    // boycott either way means "no trade of any kind" (lines 3436-3437).
                    // The offer passes to the next bidder, as it does past one with no
                    // hold free.
                    if (state.IsTradeBoycotted(seller, buyer))
                    {
                        continue;
                    }

                    var payer = HoldPayer(definition, seller, buyer);
                    var factor = PriceFactor(state, seller, buyer);
                    var affordable = price > 0 ? Affordable(cash[buyerIndex], price, factor) : bids[buyerIndex];
                    var taken = Math.Min(
                        Math.Min(remaining, bids[buyerIndex]),
                        Math.Min(affordable, holds[payer.Value]));

                    if (taken <= 0)
                    {
                        // Reported so a player can tell "nobody sold to me" from "I could
                        // not pay" and from "I had no hold free". The manual makes the
                        // last one skip the bidder outright: "the bidder is not permitted
                        // to accept the deal, and the items are offered to the next
                        // bidder on the list."
                        //
                        // **A hold shortage is reported against whoever ran out of hulls**,
                        // which is not always the bidder: selling to a minor nation spends
                        // the seller's holds, so it is the seller who cannot move the cargo
                        // and the seller who needs to hear about it.
                        var outOfHolds = holds[payer.Value] <= 0;
                        shortfalls.Add(new PlannedTradeShortfall(
                            outOfHolds ? payer : buyer,
                            commodity.Id,
                            outOfHolds ? remaining : bids[buyerIndex],
                            0,
                            outOfHolds
                                ? TradeRefusal.NoMerchantCapacity
                                : TradeRefusal.NotEnoughCash));
                        continue;
                    }

                    var total = DealTotal(taken, price, factor);
                    cash[buyerIndex] -= total;
                    cash[sellerIndex] = checked(cash[sellerIndex] + total);
                    deltas[(sellerIndex * commodityCount) + commodity.Id.Value] -= taken;
                    remaining -= taken;
                    bids[buyerIndex] -= taken;
                    holds[payer.Value] -= taken;
                    trades.Add(new PlannedTrade(seller, buyer, commodity.Id, taken, price, payer, total));
                }

                if (remaining > 0)
                {
                    shortfalls.Add(new PlannedTradeShortfall(
                        seller,
                        commodity.Id,
                        offers[sellerIndex],
                        offers[sellerIndex] - remaining,
                        TradeRefusal.NoBuyer));
                }
            }

            // The price answers to what was offered and bid rather than to what settled:
            // a bid nobody could fill is still demand, which is what makes a shortage
            // dear. Settled volume would make an unaffordable market look balanced.
            if (definition.Trade is { } market)
            {
                var next = market.NextPrice(commodity, price, offered, bid);
                if (next != price)
                {
                    priceMoves.Add(new PlannedPriceMove(commodity.Id, price, next, offered, bid));
                }
            }
        }

        return new TradePlan(deltas, trades, shortfalls, priceMoves);
    }

    /// <summary>
    /// Whose cargo holds a deal spends. "The rule for trades between Great Powers is that
    /// the buyer always picks up the commodities", and against a minor nation the Great
    /// Power carries either way because "no Minor Nation owns merchant marine".
    /// </summary>
    /// <remarks>
    /// So the buyer pays unless the buyer is a minor nation, in which case the seller
    /// does. Two minor nations trading with each other would leave nobody to carry it;
    /// nothing here forbids that, and it costs no holds, because inventing a rule for a
    /// case the manual does not describe would be worse than letting it be free.
    /// </remarks>
    private static CountryId HoldPayer(WorldDefinition definition, CountryId seller, CountryId buyer) =>
        definition.Countries[buyer.Value].IsGreatPower ? buyer : seller;

    /// <summary>
    /// The order the seller's offer passes down its bidders: by the subsidy each bidder
    /// grants the seller, highest first, ties in country id order. The seller is left out.
    /// </summary>
    /// <remarks>
    /// "The favourite trading partner receives the first chance to sell to and buy from the
    /// Minor Nation" (lines 3393-3394). Relation scores are the other half of that ranking
    /// and are not modelled, so with no subsidy anywhere this is id order exactly, as it was.
    /// </remarks>
    private static IEnumerable<int> RankBidders(WorldState state, CountryId seller, int countryCount) =>
        Enumerable.Range(0, countryCount)
            .Where(index => index != seller.Value)
            .OrderByDescending(index => state.GetTradePolicy(new CountryId(index), seller).SubsidyPercent)
            .ThenBy(static index => index);

    /// <summary>
    /// What the buyer pays per hundred of world price on this deal.
    /// </summary>
    /// <remarks>
    /// A subsidy favours its recipient "on both ends of trade deals" (lines 3018-3021): the
    /// recipient buys from the grantor at the world price "reduced … by the percentage of
    /// the subsidy" and sells to it at the world price "increased … by the percentage"
    /// (lines 3694-3699). The seller's subsidy to the buyer lowers the price; the buyer's
    /// to the seller raises it. <b>Where both subsidise each other the two net</b> — a
    /// chosen rule; the manual never puts two subsidies on one deal. Percentages are at
    /// most 99, so this is always between 2 and 198.
    /// </remarks>
    private static int PriceFactor(WorldState state, CountryId seller, CountryId buyer) =>
        100
        - state.GetTradePolicy(seller, buyer).SubsidyPercent
        + state.GetTradePolicy(buyer, seller).SubsidyPercent;

    /// <summary>
    /// What <paramref name="quantity"/> units cost at <paramref name="factor"/> per hundred,
    /// <b>rounded once per deal and toward whoever the subsidy favours</b> — a chosen rule.
    /// A buyer favoured pays the floor; a seller favoured receives the ceiling.
    /// </summary>
    private static long DealTotal(long quantity, long price, int factor)
    {
        var exact = (Int128)quantity * price * factor;
        var total = factor > 100 ? (exact + 99) / 100 : exact / 100;
        return checked((long)total);
    }

    /// <summary>
    /// The most units <paramref name="cash"/> pays for, by division so an overflowing
    /// product is never formed. The inverse of <see cref="DealTotal"/>, rounding included:
    /// a ceiling stays within cash when the exact cost does, a floor while the exact cost
    /// is below the next whole dollar.
    /// </summary>
    private static long Affordable(long cash, long price, int factor)
    {
        if (cash < 0)
        {
            return 0;
        }

        var budget = ((Int128)cash * 100) + (factor < 100 ? 99 : 0);
        var units = budget / ((Int128)price * factor);
        return units > long.MaxValue ? long.MaxValue : (long)units;
    }

    private static long? Find(IReadOnlyList<TradeOrder> orders, CommodityId commodity)
    {
        foreach (var order in orders)
        {
            if (order.Commodity == commodity)
            {
                return order.Quantity;
            }
        }

        return null;
    }
}
