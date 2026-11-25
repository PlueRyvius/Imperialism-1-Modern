using Imperialism.Core;
using Xunit;

namespace Imperialism.Core.Tests;

/// <summary>
/// Consulates, embassies, pacts, peace and the war boycott. See
/// <c>docs/formulas/diplomacy.md</c>.
/// </summary>
/// <remarks>
/// Four countries on a 4x1 strip, one province each: 0 and 1 are Great Powers, 2 and 3
/// Minor Nations. Every country holds coal and fish, three Traders and $10,000, and coal
/// is priced at 100, so a sale is available to test the boycott against.
/// </remarks>
public sealed class DiplomacyTests
{
    private const int Coal = 0;
    private const int Fish = 1;
    private const long ConsulateCost = 500;
    private const long EmbassyCost = 5_000;
    private const long StartingCash = 10_000;

    private static readonly CountryId Power = new(0);
    private static readonly CountryId Rival = new(1);
    private static readonly CountryId Minor = new(2);
    private static readonly CountryId OtherMinor = new(3);

    [Fact]
    public void GreatPowersStartWithAnEmbassyAndAConsulateInEachOther()
    {
        var state = CreateState();

        foreach (var (owner, host) in new[] { (Power, Rival), (Rival, Power) })
        {
            Assert.True(state.HoldsDiplomaticTie(owner, host, DiplomaticTie.Consulate));
            Assert.True(state.HoldsDiplomaticTie(owner, host, DiplomaticTie.Embassy));
            Assert.False(state.HoldsDiplomaticTie(owner, host, DiplomaticTie.Pact));
        }

        // Nothing in a Minor Nation, and a Minor Nation holds nothing anywhere.
        foreach (var tie in Enum.GetValues<DiplomaticTie>())
        {
            Assert.False(state.HoldsDiplomaticTie(Power, Minor, tie));
            Assert.False(state.HoldsDiplomaticTie(Minor, Power, tie));
        }
    }

    [Fact]
    public void GreatPowersAuthoredAtWarStartWithNoPosts()
    {
        var state = CreateState(atWar: [(Power, Rival)]);

        Assert.False(state.HoldsDiplomaticTie(Power, Rival, DiplomaticTie.Consulate));
        Assert.False(state.HoldsDiplomaticTie(Rival, Power, DiplomaticTie.Embassy));
    }

    [Fact]
    public void AConsulateCostsFiveHundredAndOpensNextTurn()
    {
        var state = CreateState();

        var result = Resolve(state, Overtures(Power, (DiplomaticOvertureKind.Consulate, Minor)));

        var accepted = Assert.Single(result.Events.OfType<DiplomaticOvertureAcceptedEvent>());
        Assert.Equal(
            (Power, Minor, DiplomaticOvertureKind.Consulate, ConsulateCost),
            (accepted.Country, accepted.Target, accepted.Kind, accepted.CashCost));
        Assert.Equal(StartingCash - ConsulateCost, state.GetCash(Power));
        Assert.True(state.HoldsDiplomaticTie(Power, Minor, DiplomaticTie.Consulate));

        // Held, but not open the turn it was bought: an embassy ordered that turn is refused.
        var sameTurn = CreateState();
        var refused = Resolve(
            sameTurn,
            Overtures(
                Power,
                (DiplomaticOvertureKind.Consulate, Minor),
                (DiplomaticOvertureKind.Embassy, Minor)));
        Assert.Equal(
            DiplomaticOvertureRefusal.NoConsulate,
            Assert.Single(refused.Events.OfType<DiplomaticOvertureRefusedEvent>()).Reason);

        // Open once the turn it was bought in has ended.
        Assert.True(state.HoldsDiplomaticTie(Power, Minor, DiplomaticTie.Consulate));
    }

    [Fact]
    public void AnEmbassyNeedsAConsulateAndIsChargedNothingWithoutOne()
    {
        var state = CreateState();

        var result = Resolve(state, Overtures(Power, (DiplomaticOvertureKind.Embassy, Minor)));

        var refused = Assert.Single(result.Events.OfType<DiplomaticOvertureRefusedEvent>());
        Assert.Equal(DiplomaticOvertureRefusal.NoConsulate, refused.Reason);
        Assert.Equal(StartingCash, state.GetCash(Power));
        Assert.False(state.HoldsDiplomaticTie(Power, Minor, DiplomaticTie.Embassy));

        _ = Resolve(state, Overtures(Power, (DiplomaticOvertureKind.Consulate, Minor)));
        var opened = Resolve(state, Overtures(Power, (DiplomaticOvertureKind.Embassy, Minor)));

        Assert.Equal(
            EmbassyCost,
            Assert.Single(opened.Events.OfType<DiplomaticOvertureAcceptedEvent>()).CashCost);
        Assert.Equal(StartingCash - ConsulateCost - EmbassyCost, state.GetCash(Power));
    }

    [Fact]
    public void APactNeedsAnOpenEmbassyAndIsFree()
    {
        var state = CreateState();
        _ = Resolve(state, Overtures(Power, (DiplomaticOvertureKind.Consulate, Minor)));

        var early = Resolve(state, Overtures(
            Power,
            (DiplomaticOvertureKind.Embassy, Minor),
            (DiplomaticOvertureKind.Pact, Minor)));
        Assert.Equal(
            DiplomaticOvertureRefusal.NoEmbassy,
            Assert.Single(early.Events.OfType<DiplomaticOvertureRefusedEvent>()).Reason);

        var cash = state.GetCash(Power);
        var signed = Resolve(state, Overtures(Power, (DiplomaticOvertureKind.Pact, Minor)));

        Assert.Equal(0, Assert.Single(signed.Events.OfType<DiplomaticOvertureAcceptedEvent>()).CashCost);
        Assert.Equal(cash, state.GetCash(Power));
        Assert.True(state.HoldsDiplomaticTie(Power, Minor, DiplomaticTie.Pact));
    }

    [Fact]
    public void APactIsOfferedOnlyToAMinorNation()
    {
        var state = CreateState();

        var result = Resolve(state, Overtures(Power, (DiplomaticOvertureKind.Pact, Rival)));

        Assert.Equal(
            DiplomaticOvertureRefusal.TargetIsAGreatPower,
            Assert.Single(result.Events.OfType<DiplomaticOvertureRefusedEvent>()).Reason);
    }

    [Fact]
    public void OnlyAGreatPowerMakesOvertures()
    {
        var state = CreateState();

        var result = Resolve(state, Overtures(Minor, (DiplomaticOvertureKind.Consulate, OtherMinor)));

        Assert.Equal(
            DiplomaticOvertureRefusal.NotAGreatPower,
            Assert.Single(result.Events.OfType<DiplomaticOvertureRefusedEvent>()).Reason);
        Assert.Equal(StartingCash, state.GetCash(Minor));
    }

    [Fact]
    public void AnOvertureTheTreasuryCannotPayIsRefusedAndChargedNothing()
    {
        var state = CreateState(cash: ConsulateCost + 100);

        var result = Resolve(state, Overtures(
            Power,
            (DiplomaticOvertureKind.Consulate, Minor),
            (DiplomaticOvertureKind.Consulate, OtherMinor)));

        // The first takes $500 of $600, so the second cannot be paid.
        Assert.Equal(
            (Minor, OtherMinor),
            (Assert.Single(result.Events.OfType<DiplomaticOvertureAcceptedEvent>()).Target,
             Assert.Single(result.Events.OfType<DiplomaticOvertureRefusedEvent>(),
                 e => e.Reason == DiplomaticOvertureRefusal.NotEnoughCash).Target));
        Assert.Equal(100, state.GetCash(Power));
    }

    [Fact]
    public void ARefusedOvertureDoesNotStopLaterOnes()
    {
        var state = CreateState();

        var result = Resolve(state, Overtures(
            Power,
            (DiplomaticOvertureKind.Embassy, Minor),
            (DiplomaticOvertureKind.Consulate, new CountryId(99)),
            (DiplomaticOvertureKind.Consulate, Power),
            (DiplomaticOvertureKind.Consulate, Minor),
            (DiplomaticOvertureKind.Consulate, Minor)));

        Assert.Equal(
            [
                "R Embassy 2 NoConsulate",
                "R Consulate 99 NoSuchCountry",
                "R Consulate 0 Itself",
                "A Consulate 2 500",
                "R Consulate 2 AlreadyOrderedThisTurn",
            ],
            Describe(result));
        Assert.Equal(StartingCash - ConsulateCost, state.GetCash(Power));
    }

    [Fact]
    public void AnAlreadyHeldTieIsRefused()
    {
        var state = CreateState();

        var result = Resolve(state, Overtures(Power, (DiplomaticOvertureKind.Embassy, Rival)));

        Assert.Equal(
            DiplomaticOvertureRefusal.AlreadyEstablished,
            Assert.Single(result.Events.OfType<DiplomaticOvertureRefusedEvent>()).Reason);
        Assert.Equal(StartingCash, state.GetCash(Power));
    }

    [Fact]
    public void AWorldThatPricesNoPostsOffersNone()
    {
        var state = CreateState(withDiplomacy: false);

        var result = Resolve(state, Overtures(Power, (DiplomaticOvertureKind.Consulate, Minor)));

        Assert.Equal(
            DiplomaticOvertureRefusal.NotOffered,
            Assert.Single(result.Events.OfType<DiplomaticOvertureRefusedEvent>()).Reason);
        Assert.Equal(StartingCash, state.GetCash(Power));

        // The starting embassies follow from the Great Power flag, not the price block.
        Assert.True(state.HoldsDiplomaticTie(Power, Rival, DiplomaticTie.Embassy));
    }

    [Fact]
    public void NoOvertureButPeaceCrossesAWar()
    {
        var state = CreateState(atWar: [(Power, Minor)]);

        var result = Resolve(state, Overtures(Power, (DiplomaticOvertureKind.Consulate, Minor)));
        Assert.Equal(
            DiplomaticOvertureRefusal.AtWar,
            Assert.Single(result.Events.OfType<DiplomaticOvertureRefusedEvent>()).Reason);

        // Nor to a country either side declares war on this turn.
        var declaring = CreateState();
        var declared = Resolve(
            declaring,
            new CountryTurnOrders(
                Power,
                declareWar: [OtherMinor],
                overtures: [new DiplomaticOverture(DiplomaticOvertureKind.Consulate, OtherMinor)]));
        Assert.Equal(
            DiplomaticOvertureRefusal.AtWar,
            Assert.Single(declared.Events.OfType<DiplomaticOvertureRefusedEvent>()).Reason);
        Assert.Equal(StartingCash, declaring.GetCash(Power));
    }

    [Fact]
    public void AMinorNationAcceptsPeaceAndTheWarEndsNextTurn()
    {
        var state = CreateState(atWar: [(Power, Minor)]);
        Assert.True(state.HasEffectiveHostility(Power, Minor));

        // In effect for the rest of the turn it is ended in: see the boycott test, whose
        // market still refuses the pair on the turn peace is made.
        var result = Resolve(state, Overtures(Power, (DiplomaticOvertureKind.Peace, Minor)));

        Assert.Equal(
            (Power, Minor, DiplomaticOvertureKind.Peace, 0L),
            Assert.Single(result.Events.OfType<DiplomaticOvertureAcceptedEvent>()) is var a
                ? (a.Country, a.Target, a.Kind, a.CashCost)
                : default);
        Assert.Equal(CountryRelationMode.Standard, state.GetRelationMode(Power, Minor));
        Assert.False(state.HasEffectiveHostility(Power, Minor));
        Assert.False(state.HasEffectiveHostility(Minor, Power));
    }

    [Fact]
    public void ARelationChangeTakesEffectNextTurnWhicheverWayItGoes()
    {
        // War in effect, then peace stamped this turn: the war is still in effect.
        var peace = CreateState(atWar: [(Power, Minor)]);
        peace.SetRelationMode(Power, Minor, CountryRelationMode.Standard);
        Assert.True(peace.HasEffectiveHostility(Power, Minor));
        Assert.True(peace.HasEffectiveHostility(Minor, Power));

        // A second change in the same turn does not overwrite what was in effect.
        peace.SetRelationMode(Power, Minor, CountryRelationMode.Hostile);
        peace.SetRelationMode(Power, Minor, CountryRelationMode.Standard);
        Assert.True(peace.HasEffectiveHostility(Power, Minor));

        // Peace in effect, then war stamped this turn: not yet in effect.
        var war = CreateState();
        war.SetRelationMode(Power, Minor, CountryRelationMode.Hostile);
        Assert.False(war.HasEffectiveHostility(Power, Minor));
    }

    [Fact]
    public void PeaceIsRefusedWhereThereIsNoWarInEffect()
    {
        var state = CreateState();

        var result = Resolve(state, Overtures(Power, (DiplomaticOvertureKind.Peace, Minor)));

        Assert.Equal(
            DiplomaticOvertureRefusal.NotAtWar,
            Assert.Single(result.Events.OfType<DiplomaticOvertureRefusedEvent>()).Reason);
    }

    [Fact]
    public void PeaceBetweenGreatPowersNeedsBothToOfferIt()
    {
        var state = CreateState(atWar: [(Power, Rival)]);

        var oneSided = Resolve(state, Overtures(Power, (DiplomaticOvertureKind.Peace, Rival)));
        Assert.Equal(
            DiplomaticOvertureRefusal.Declined,
            Assert.Single(oneSided.Events.OfType<DiplomaticOvertureRefusedEvent>()).Reason);
        Assert.True(state.HasEffectiveHostility(Power, Rival));

        var mutual = Resolve(
            state,
            Overtures(Power, (DiplomaticOvertureKind.Peace, Rival)),
            Overtures(Rival, (DiplomaticOvertureKind.Peace, Power)));

        // Made once, for the lower id; the other side's offer finds it already made.
        Assert.Equal(["A Peace 1 0", "R Peace 0 AlreadyMadeThisTurn"], Describe(mutual));
        Assert.False(state.HasEffectiveHostility(Power, Rival));

        // The posts a war closed are not restored by peace; they are bought again.
        Assert.False(state.HoldsDiplomaticTie(Power, Rival, DiplomaticTie.Embassy));
        var rebuilt = Resolve(state, Overtures(Power, (DiplomaticOvertureKind.Consulate, Rival)));
        Assert.Single(rebuilt.Events.OfType<DiplomaticOvertureAcceptedEvent>());
    }

    [Fact]
    public void WarBetweenGreatPowersClosesTheirPostsBothWays()
    {
        var state = CreateState();

        var result = Resolve(state, new CountryTurnOrders(Power, declareWar: [Rival]));

        var cut = Assert.Single(result.Events.OfType<DiplomaticTiesCutEvent>());
        Assert.Equal((Power, Rival, false, true), (cut.Country, cut.Target, cut.PactBroken, cut.PostsClosed));
        foreach (var (owner, host) in new[] { (Power, Rival), (Rival, Power) })
        {
            Assert.False(state.HoldsDiplomaticTie(owner, host, DiplomaticTie.Consulate));
            Assert.False(state.HoldsDiplomaticTie(owner, host, DiplomaticTie.Embassy));
        }
    }

    [Fact]
    public void WarOnAMinorNationBreaksThePactButKeepsThePosts()
    {
        var state = CreateState();
        _ = Resolve(state, Overtures(Power, (DiplomaticOvertureKind.Consulate, Minor)));
        _ = Resolve(state, Overtures(Power, (DiplomaticOvertureKind.Embassy, Minor)));
        _ = Resolve(state, Overtures(Power, (DiplomaticOvertureKind.Pact, Minor)));
        Assert.True(state.HoldsDiplomaticTie(Power, Minor, DiplomaticTie.Pact));

        // The Minor Nation's own declaration breaks the Great Power's pact too.
        var result = Resolve(state, new CountryTurnOrders(Minor, declareWar: [Power]));

        var cut = Assert.Single(result.Events.OfType<DiplomaticTiesCutEvent>());
        Assert.Equal((Minor, Power, true, false), (cut.Country, cut.Target, cut.PactBroken, cut.PostsClosed));
        Assert.False(state.HoldsDiplomaticTie(Power, Minor, DiplomaticTie.Pact));
        Assert.True(state.HoldsDiplomaticTie(Power, Minor, DiplomaticTie.Embassy));
    }

    [Fact]
    public void AWarWithNothingToCutReportsNothingCut()
    {
        var state = CreateState();

        var result = Resolve(state, new CountryTurnOrders(Power, declareWar: [Minor]));

        Assert.Empty(result.Events.OfType<DiplomaticTiesCutEvent>());
    }

    [Fact]
    public void CountriesAtWarDoNotTradeAndTradeAgainAfterPeace()
    {
        var state = CreateState();

        // Declared this turn, in effect next: this turn still trades.
        var declared = Resolve(
            state,
            Offer(Minor, 2),
            new CountryTurnOrders(Power, declareWar: [Minor], tradeBids: [Bid(2)]));
        Assert.Single(declared.Events.OfType<CommodityTradedEvent>());

        var atWar = Resolve(state, Offer(Minor, 2), new CountryTurnOrders(Power, tradeBids: [Bid(2)]));
        Assert.Empty(atWar.Events.OfType<CommodityTradedEvent>());

        // Peace made this turn leaves the war in effect for this turn's market.
        var peace = Resolve(
            state,
            Offer(Minor, 2),
            new CountryTurnOrders(
                Power,
                tradeBids: [Bid(2)],
                overtures: [new DiplomaticOverture(DiplomaticOvertureKind.Peace, Minor)]));
        Assert.Empty(peace.Events.OfType<CommodityTradedEvent>());

        var after = Resolve(state, Offer(Minor, 2), new CountryTurnOrders(Power, tradeBids: [Bid(2)]));
        var deal = Assert.Single(after.Events.OfType<CommodityTradedEvent>());
        Assert.Equal((Minor, Power), (deal.Seller, deal.Buyer));
    }

    [Fact]
    public void AWarBoycottsOnlyThePairAtWar()
    {
        var state = CreateState(atWar: [(Power, Minor)]);

        var result = Resolve(
            state,
            Offer(Minor, 2),
            new CountryTurnOrders(Power, tradeBids: [Bid(2)]),
            new CountryTurnOrders(Rival, tradeBids: [Bid(2)]));

        var deal = Assert.Single(result.Events.OfType<CommodityTradedEvent>());
        Assert.Equal((Minor, Rival, 2L), (deal.Seller, deal.Buyer, deal.Quantity));
    }

    [Fact]
    public void OvertureCashIsSpokenForBeforeTheMarketClears()
    {
        // $600: a consulate leaves $100, which buys one unit of coal at 100, not two.
        var state = CreateState(cash: ConsulateCost + 100);

        var result = Resolve(
            state,
            Offer(Minor, 2),
            new CountryTurnOrders(
                Power,
                tradeBids: [Bid(2)],
                overtures: [new DiplomaticOverture(DiplomaticOvertureKind.Consulate, OtherMinor)]));

        Assert.Single(result.Events.OfType<DiplomaticOvertureAcceptedEvent>());
        Assert.Equal(1, result.Events.OfType<CommodityTradedEvent>().Sum(static e => e.Quantity));
        Assert.Equal(0, state.GetCash(Power));
    }

    [Fact]
    public void AnUndefinedOvertureKindIsRejectedWhenTheOrderIsBuilt()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CountryTurnOrders(
            Power,
            overtures: [new DiplomaticOverture((DiplomaticOvertureKind)99, Minor)]));
    }

    private static TradeOrder Bid(long quantity) => new(new CommodityId(Coal), quantity);

    private static CountryTurnOrders Offer(CountryId country, long quantity) =>
        new(country, tradeOffers: [Bid(quantity)]);

    private static CountryTurnOrders Overtures(
        CountryId country, params (DiplomaticOvertureKind Kind, CountryId Target)[] overtures) =>
        new(country, overtures: overtures.Select(static o => new DiplomaticOverture(o.Kind, o.Target)));

    private static IReadOnlyList<string> Describe(TurnResolution resolution) =>
        resolution.Events
            .Select(static e => e switch
            {
                DiplomaticOvertureAcceptedEvent a => $"A {a.Kind} {a.Target.Value} {a.CashCost}",
                DiplomaticOvertureRefusedEvent r => $"R {r.Kind} {r.Target.Value} {r.Reason}",
                _ => null,
            })
            .OfType<string>()
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

    private static WorldState CreateState(
        long cash = StartingCash,
        bool withDiplomacy = true,
        (CountryId First, CountryId Second)[]? atWar = null)
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
            "Diplomacy",
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
                .Select(index => new InitialCash(new CountryId(index), cash)),
            initialShips: Enumerable.Range(0, countries)
                .Select(static index => new InitialShip(new CountryId(index), new ShipTypeId(0), 0, 3)),
            // Authored at war with a token from before the first turn, so already in effect.
            initialRelationStates: (atWar ?? [])
                .Select(static pair => new InitialRelationState(
                    pair.First, pair.Second, (short)CountryRelationMode.Hostile, -1)));

        return new WorldState(new WorldDefinition(
            map,
            [
                new CountryDefinition(Power, "Power", isGreatPower: true),
                new CountryDefinition(Rival, "Rival", isGreatPower: true),
                new CountryDefinition(Minor, "Minor"),
                new CountryDefinition(OtherMinor, "Other Minor"),
            ],
            scenario,
            [
                new CommodityDefinition(
                    new CommodityId(Coal), "Coal", CommodityCategory.Raw, worldPrice: 100, tradeOrder: 1),
                new CommodityDefinition(new CommodityId(Fish), "Fish", CommodityCategory.Raw),
            ],
            null,
            null,
            feeding: new FeedingSettings(
                [new FoodPreference([new CommodityId(Fish)])],
                [1, 2, 4]),
            shipTypes: [new ShipTypeDefinition(new ShipTypeId(0), "Trader", cargo: 2)],
            trade: new ProportionalTradeMarket(),
            diplomacy: withDiplomacy ? new DiplomacySettings(ConsulateCost, EmbassyCost) : null));
    }
}
