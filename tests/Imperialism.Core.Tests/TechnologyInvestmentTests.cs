using Imperialism.Core;
using Xunit;

namespace Imperialism.Core.Tests;

/// <summary>
/// The Investment screen: buying technology with cash. **The first thing in this
/// engine that acquires knowledge at all** — before it, every gate in the project
/// could only ever be tested shut.
/// </summary>
/// <remarks>
/// The fixture is a 3x1 strip owned by country 0, with a grain farm at cell 1 and
/// a Farmer standing on the capital. Its catalog is five technologies whose ids run
/// in prerequisite order, which is what <see cref="WorldDefinition"/> requires:
/// <list type="number">
/// <item><b>Seed Drill</b> — no price, so never for sale. Stands in for the two
/// every power starts with.</item>
/// <item><b>Cotton Gin</b> — 1,000 in 1816, nothing before it.</item>
/// <item><b>Steel and Iron Plows</b> — 3,000 in 1831, wants Seed Drill.</item>
/// <item><b>Bessemer Converter</b> — 6,000 in 1836, nothing before it.</item>
/// <item><b>Breech-Loading Rifles</b> — 12,000 in 1841, wants Bessemer.</item>
/// </list>
/// The numbers are the real ones from the price list, so a test that reads oddly
/// is a test disagreeing with the source rather than with a fixture.
/// </remarks>
public sealed class TechnologyInvestmentTests
{
    private const int Farm = 0;
    private const int Grain = 0;
    private const int Farmer = 0;

    private const int SeedDrill = 0;
    private const int CottonGin = 1;
    private const int Plows = 2;
    private const int Bessemer = 3;
    private const int Rifles = 4;

    private const long CottonGinCost = 1_000;
    private const long PlowsCost = 3_000;
    private const long BessemerCost = 6_000;
    private const long RiflesCost = 12_000;

    private static readonly CountryId Country = new(0);
    private static readonly CellIndex GrainFarm = new(1);

    /// <summary>
    /// "Technology, once available, does not vanish. If you cannot afford the
    /// cotton gin in 1818, invest in 1830." The date is the only thing that has to
    /// pass; nothing about it is per country.
    /// </summary>
    [Fact]
    public void APurchaseIsRefusedBeforeItsYearAndAcceptedAfter()
    {
        // Bessemer Converter, because it has no prerequisite: the only thing
        // standing between this country and it is the calendar.
        var early = CreateState(year: 1835);

        Assert.Equal(
            TechnologyPurchaseRefusal.NotYetAvailable,
            Assert.Single(Buy(early, Bessemer).Events.OfType<TechnologyPurchaseRefusedEvent>()).Reason);
        Assert.False(early.HasTechnology(Country, new TechnologyId(Bessemer)));
        Assert.Equal(50_000, early.GetCash(Country));

        var late = CreateState(year: 1836);
        var bought = Assert.Single(Buy(late, Bessemer).Events.OfType<TechnologyPurchasedEvent>());

        Assert.Equal((new TechnologyId(Bessemer), BessemerCost), (bought.Technology, bought.Paid));
        Assert.True(late.HasTechnology(Country, new TechnologyId(Bessemer)));
        Assert.Equal(50_000 - BessemerCost, late.GetCash(Country));
    }

    /// <summary>
    /// The arrival year is the *first* year, not the only one: a country that let
    /// the date go by can still buy decades later.
    /// </summary>
    [Fact]
    public void AnArrivedTechnologyStaysOnTheScreen()
    {
        var state = CreateState(year: 1880);

        Assert.Single(Buy(state, CottonGin).Events.OfType<TechnologyPurchasedEvent>());
        Assert.True(state.HasTechnology(Country, new TechnologyId(CottonGin)));
    }

    [Fact]
    public void APurchaseIsRefusedWithoutItsPrerequisiteAndAcceptedWithIt()
    {
        var state = CreateState(year: 1841);

        Assert.Equal(
            TechnologyPurchaseRefusal.PrerequisiteNotKnown,
            Assert.Single(Buy(state, Rifles).Events.OfType<TechnologyPurchaseRefusedEvent>()).Reason);
        Assert.Equal(50_000, state.GetCash(Country));

        state.GrantTechnology(Country, new TechnologyId(Bessemer));
        var bought = Assert.Single(Buy(state, Rifles).Events.OfType<TechnologyPurchasedEvent>());

        Assert.Equal(RiflesCost, bought.Paid);
        Assert.True(state.HasTechnology(Country, new TechnologyId(Rifles)));
    }

    /// <summary>
    /// **A chain cannot be bought in one turn, whichever order it is listed in.**
    /// Buying a technology spends the money and the research "finishes after the
    /// turn ends before the next starts", so the dependent entry is not something
    /// the original would even let a player click.
    /// </summary>
    [Fact]
    public void APrerequisiteBoughtThisTurnDoesNotUnlockItsDependentThisTurn()
    {
        var state = CreateState(year: 1841);

        var result = Buy(state, Bessemer, Rifles);

        var bought = Assert.Single(result.Events.OfType<TechnologyPurchasedEvent>());
        Assert.Equal(new TechnologyId(Bessemer), bought.Technology);

        var refused = Assert.Single(result.Events.OfType<TechnologyPurchaseRefusedEvent>());
        Assert.Equal(
            (new TechnologyId(Rifles), TechnologyPurchaseRefusal.PrerequisiteNotKnown),
            (refused.Technology, refused.Reason));

        // Only the prerequisite was paid for, and the next turn is the first that
        // can buy what it unlocked.
        Assert.Equal(50_000 - BessemerCost, state.GetCash(Country));
        Assert.Single(Buy(state, Rifles).Events.OfType<TechnologyPurchasedEvent>());
        Assert.True(state.HasTechnology(Country, new TechnologyId(Rifles)));
    }

    [Fact]
    public void APurchaseIsRefusedForWantOfCash()
    {
        var state = CreateState(year: 1836, cash: BessemerCost - 1);

        Assert.Equal(
            TechnologyPurchaseRefusal.NotEnoughCash,
            Assert.Single(Buy(state, Bessemer).Events.OfType<TechnologyPurchaseRefusedEvent>()).Reason);

        // Refused outright rather than part funded, and nothing was taken.
        Assert.Equal(BessemerCost - 1, state.GetCash(Country));
        Assert.False(state.HasTechnology(Country, new TechnologyId(Bessemer)));
    }

    /// <summary>
    /// One treasury, two purchases: the first takes what it needs and the second
    /// is refused. There is no pooling and no preflight, exactly as with two
    /// Engineers of one country.
    /// </summary>
    [Fact]
    public void ATreasuryThatCoversOnePurchaseRefusesTheSecond()
    {
        var state = CreateState(year: 1836, cash: BessemerCost);

        var result = Buy(state, Bessemer, CottonGin);

        Assert.Equal(new TechnologyId(Bessemer),
            Assert.Single(result.Events.OfType<TechnologyPurchasedEvent>()).Technology);
        Assert.Equal(
            (new TechnologyId(CottonGin), TechnologyPurchaseRefusal.NotEnoughCash),
            Assert.Single(result.Events.OfType<TechnologyPurchaseRefusedEvent>()) is var refused
                ? (refused.Technology, refused.Reason)
                : default);
        Assert.Equal(0, state.GetCash(Country));
    }

    /// <summary>
    /// "You may invest in several technologies before ending the turn." Three
    /// independent ones — no chain among them — all go through.
    /// </summary>
    [Fact]
    public void SeveralPurchasesCanBeMadeInOneTurn()
    {
        var state = CreateState(year: 1836, known: [SeedDrill]);

        var result = Buy(state, CottonGin, Plows, Bessemer);

        Assert.Equal(3, result.Events.OfType<TechnologyPurchasedEvent>().Count());
        Assert.Empty(result.Events.OfType<TechnologyPurchaseRefusedEvent>());
        Assert.Equal(50_000 - CottonGinCost - PlowsCost - BessemerCost, state.GetCash(Country));
        foreach (var technology in new[] { CottonGin, Plows, Bessemer })
        {
            Assert.True(state.HasTechnology(Country, new TechnologyId(technology)));
        }
    }

    [Fact]
    public void WhatIsAlreadyKnownCannotBeBoughtAgain()
    {
        var state = CreateState(year: 1836, known: [CottonGin]);

        Assert.Equal(
            TechnologyPurchaseRefusal.AlreadyKnown,
            Assert.Single(Buy(state, CottonGin).Events.OfType<TechnologyPurchaseRefusedEvent>()).Reason);
        Assert.Equal(50_000, state.GetCash(Country));
    }

    /// <summary>
    /// The two every power starts with are **not for sale**, which is a different
    /// fact from being free: a price of zero would put them on the screen at no
    /// charge, and nobody can buy what they already have.
    /// </summary>
    [Fact]
    public void ATechnologyWithNoPriceIsNotForSale()
    {
        // Not held, so the refusal is about the price rather than about knowing it.
        var state = CreateState(year: 1836);

        Assert.Equal(
            TechnologyPurchaseRefusal.NotForSale,
            Assert.Single(Buy(state, SeedDrill).Events.OfType<TechnologyPurchaseRefusedEvent>()).Reason);
        Assert.Equal(50_000, state.GetCash(Country));
    }

    /// <summary>
    /// A world older than version 19 prices nothing, so nothing in it is for sale
    /// and it behaves exactly as it did.
    /// </summary>
    [Fact]
    public void AWorldThatPricesNothingSellsNothing()
    {
        var state = CreateState(year: 1900, unpriced: true);

        var result = Buy(state, CottonGin, Plows, Bessemer);

        Assert.Empty(result.Events.OfType<TechnologyPurchasedEvent>());
        Assert.All(
            result.Events.OfType<TechnologyPurchaseRefusedEvent>(),
            item => Assert.Equal(TechnologyPurchaseRefusal.NotForSale, item.Reason));
        Assert.Equal(50_000, state.GetCash(Country));
    }

    [Fact]
    public void AnIdOutsideTheCatalogIsRefusedRatherThanThrowing()
    {
        var state = CreateState(year: 1836);

        Assert.Equal(
            TechnologyPurchaseRefusal.NoSuchTechnology,
            Assert.Single(Buy(state, 99).Events.OfType<TechnologyPurchaseRefusedEvent>()).Reason);
    }

    [Fact]
    public void ATechnologyCannotBeBoughtTwiceInOneTurn()
    {
        Assert.Throws<ArgumentException>(() => new CountryTurnOrders(
            Country, buyTechnology: [new TechnologyId(CottonGin), new TechnologyId(CottonGin)]));
    }

    /// <summary>
    /// **The payoff, and the reason Investment runs last.** A Farmer ordered onto
    /// a gated rung on the same turn its gate is bought is refused, and succeeds
    /// the turn after. Every gate in the project reads knowledge before the
    /// Investment phase, so this falls out of the pipeline order rather than
    /// needing a rule.
    /// </summary>
    [Fact]
    public void KnowledgeBoughtThisTurnCannotBeUsedUntilNext()
    {
        var state = CreateState(year: 1831, known: [SeedDrill]);
        var farmer = new CivilianUnitId(1);

        // Level I is Seed Drill, which it holds; get the farm there first.
        _ = Resolve(state, Work(farmer, GrainFarm));
        _ = Resolve(state, TurnOrders.Empty(1));
        Assert.Equal(1, state.GetCellDevelopment(GrainFarm));

        // Level II is Steel and Iron Plows. Buy it and order the work in the same
        // turn: the purchase goes through and the Farmer is turned away.
        var together = Resolve(state, new TurnOrders(
        [
            new CountryTurnOrders(
                Country,
                civilianWork: [new CivilianWorkOrder(farmer, GrainFarm)],
                buyTechnology: [new TechnologyId(Plows)]),
        ]));

        Assert.Single(together.Events.OfType<TechnologyPurchasedEvent>());
        Assert.Equal(
            CivilianOrderRefusal.ImprovementTechnologyNotKnown,
            Assert.Single(together.Events.OfType<CivilianOrderRefusedEvent>()).Reason);
        Assert.Equal(1, state.GetCellDevelopment(GrainFarm));

        // The next turn is the first whose orders could have been written knowing.
        _ = Resolve(state, Work(farmer, GrainFarm));
        _ = Resolve(state, TurnOrders.Empty(1));
        Assert.Equal(2, state.GetCellDevelopment(GrainFarm));
    }

    /// <summary>
    /// Availability is world-wide: one country buying does not consume the entry,
    /// and no country is ahead of another on the date.
    /// </summary>
    [Fact]
    public void AvailabilityIsWorldWideRatherThanPerCountry()
    {
        var state = CreateState(year: 1836, countries: 2);

        var result = Resolve(state, new TurnOrders(
        [
            new CountryTurnOrders(Country, buyTechnology: [new TechnologyId(Bessemer)]),
            new CountryTurnOrders(new CountryId(1), buyTechnology: [new TechnologyId(Bessemer)]),
        ]));

        Assert.Equal(2, result.Events.OfType<TechnologyPurchasedEvent>().Count());
        Assert.True(state.HasTechnology(Country, new TechnologyId(Bessemer)));
        Assert.True(state.HasTechnology(new CountryId(1), new TechnologyId(Bessemer)));
    }

    /// <summary>
    /// A prerequisite has to sit earlier in the catalog. **A chosen constraint**:
    /// it forbids cycles without a graph walk, and it is what makes any prefix of
    /// the catalog prerequisite-closed — the shape a legacy `tech` record needs.
    /// </summary>
    [Fact]
    public void APrerequisiteMustSitEarlierInTheCatalog()
    {
        var exception = Assert.Throws<ArgumentException>(() => new TechnologyDefinition(
            new TechnologyId(1), "Backwards", [new TechnologyId(1)], 1815, 100));

        Assert.Contains("prerequisite", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ATechnologyCannotCostNothing()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new TechnologyDefinition(new TechnologyId(0), "Free", cost: 0));
    }

    private static TurnResolution Buy(WorldState state, params int[] technologies) =>
        Resolve(state, new TurnOrders(Enumerable
            .Range(0, state.Definition.Countries.Count)
            .Select(index => index == 0
                ? new CountryTurnOrders(
                    Country,
                    buyTechnology: technologies.Select(static item => new TechnologyId(item)))
                : new CountryTurnOrders(new CountryId(index)))));

    private static TurnOrders Work(CivilianUnitId unit, CellIndex cell) => new(
    [
        new CountryTurnOrders(Country, civilianWork: [new CivilianWorkOrder(unit, cell)]),
    ]);

    private static TurnResolution Resolve(WorldState state, TurnOrders orders) =>
        TurnResolver.Resolve(state, orders, 0);

    private static WorldState CreateState(
        int year,
        long cash = 50_000,
        IEnumerable<int>? known = null,
        bool unpriced = false,
        int countries = 1)
    {
        const int width = 3;
        var dimensions = new MapDimensions(width, 1);
        var cells = new CellDefinition[width];
        for (var index = 0; index < width; index++)
        {
            cells[index] = new CellDefinition(
                new CellIndex(index),
                dimensions.GetCoordinate(new CellIndex(index)),
                new TerrainId(Farm),
                CellRegion.ForProvince(new ProvinceId(index)),
                index == 1 ? [new ResourceId(Grain)] : null,
                index == 0 ? SettlementSiteKind.Urban : SettlementSiteKind.None);
        }

        var map = new MapDefinition(
            dimensions,
            cells,
            Enumerable.Range(0, width)
                .Select(static index => new ProvinceDefinition(new ProvinceId(index), $"P{index}")),
            [],
            [
                new ResourceDefinition(
                    new ResourceId(Grain),
                    new CommodityId(Grain),
                    [1, 2, 3, 4],
                    improvedBy: new CivilianTypeId(Farmer),
                    technologyByDevelopmentLevel:
                    [
                        null,
                        new TechnologyId(SeedDrill),
                        new TechnologyId(Plows),
                        null,
                    ]),
            ],
            [new TerrainDefinition(new TerrainId(Farm), "Farm", isImprovable: true)]);

        var scenario = new ScenarioDefinition(
            "Investment",
            year,
            Enumerable.Repeat<CountryId?>(Country, width),
            initialCountryCapitals: [new CountryCapital(Country, new CellIndex(0))],
            initialCountryTechnologies: known
                ?.Select(static item => new InitialCountryTechnology(Country, new TechnologyId(item))),
            initialCivilians: [new InitialCivilian(Country, new CivilianTypeId(Farmer), new CellIndex(0))],
            initialCash: Enumerable.Range(0, countries)
                .Select(index => new InitialCash(new CountryId(index), cash)));

        // Prices, years and prerequisites are the price list's own. Setting them
        // all absent is how a pre-v19 world reads.
        long? Price(long amount) => unpriced ? null : amount;
        int? Arrives(int arrival) => unpriced ? null : arrival;

        return new WorldState(new WorldDefinition(
            map,
            Enumerable.Range(0, countries)
                .Select(static index => new CountryDefinition(new CountryId(index), $"Country {index}")),
            scenario,
            [new CommodityDefinition(new CommodityId(Grain), "Grain", CommodityCategory.Raw)],
            technologies:
            [
                new TechnologyDefinition(new TechnologyId(SeedDrill), "Seed Drill", null, 1815),
                new TechnologyDefinition(
                    new TechnologyId(CottonGin), "Cotton Gin", null, Arrives(1816), Price(CottonGinCost)),
                new TechnologyDefinition(
                    new TechnologyId(Plows),
                    "Steel and Iron Plows",
                    [new TechnologyId(SeedDrill)],
                    Arrives(1831),
                    Price(PlowsCost)),
                new TechnologyDefinition(
                    new TechnologyId(Bessemer),
                    "Bessemer Converter",
                    null,
                    Arrives(1836),
                    Price(BessemerCost)),
                new TechnologyDefinition(
                    new TechnologyId(Rifles),
                    "Breech-Loading Rifles",
                    [new TechnologyId(Bessemer)],
                    Arrives(1841),
                    Price(RiflesCost)),
            ],
            civilianTypes: [new CivilianTypeDefinition(new CivilianTypeId(Farmer), "Farmer", 1)]));
    }
}
