using Imperialism.Core;
using Xunit;

namespace Imperialism.Core.Tests;

/// <summary>
/// The newspaper's "New Invention": a technology announced on the turn whose date
/// advance crosses into its arrival year. Chosen rule N1 in
/// <c>docs/formulas/newspaper.md</c>: a crossing, not a level.
/// </summary>
/// <remarks>
/// The catalog starts in 1816 Q1 and holds, in id order:
/// <list type="number">
/// <item><b>Seed Drill</b>, unpriced, dated 1817. Never on the Investment screen, so
/// never announced, whatever its date.</item>
/// <item><b>Cotton Gin</b>, 1,000 in 1816. Already available as the game opens.</item>
/// <item><b>Mechanical Reaper</b>, 2,000 in 1817.</item>
/// <item><b>Bessemer Converter</b>, 3,000 in 1817, arriving the same year.</item>
/// <item><b>Dynamite</b>, 4,000 in 1818.</item>
/// </list>
/// </remarks>
public sealed class TechnologyAvailableEventTests
{
    private const int SeedDrill = 0;
    private const int CottonGin = 1;
    private const int Reaper = 2;
    private const int Bessemer = 3;
    private const int Dynamite = 4;

    [Fact]
    public void AnInventionIsAnnouncedExactlyOnceOnTheTurnWhoseEndCrossesIntoItsYear()
    {
        var state = CreateState(technologies: Catalog());
        var announced = ResolveTurns(state, 12);

        // Turn 4 runs 1816 Q4 into 1817 Q1; turn 8 runs 1817 Q4 into 1818 Q1.
        Assert.Equal(
            [(4, Reaper), (4, Bessemer), (8, Dynamite)],
            announced);
        Assert.Equal(new TurnDate(1819, 1), state.CurrentDate);
    }

    [Fact]
    public void OneAlreadyAvailableAtTheStartIsNeverAnnounced()
    {
        var state = CreateState(technologies: Catalog());

        Assert.DoesNotContain(ResolveTurns(state, 12), entry => entry.Technology == CottonGin);
    }

    [Fact]
    public void AnUnpricedTechnologyIsNeverAnnounced()
    {
        var state = CreateState(technologies: Catalog());

        Assert.DoesNotContain(ResolveTurns(state, 12), entry => entry.Technology == SeedDrill);
    }

    [Fact]
    public void TwoArrivingTheSameYearAreAnnouncedInIdOrderInTheInvestmentPhase()
    {
        var state = CreateState(technologies: Catalog());
        TurnResolution? crossing = null;
        for (var turn = 0; turn < 4; turn++)
        {
            crossing = TurnResolver.Resolve(state, Empty(state), 0);
        }

        var events = crossing!.Events.OfType<TechnologyAvailableEvent>().ToArray();
        Assert.Equal(
            [new TechnologyId(Reaper), new TechnologyId(Bessemer)],
            events.Select(static item => item.Technology));
        Assert.All(events, item => Assert.Equal(TurnPhase.Investment, item.Phase));

        // After the purchases and before the phase marker, so the report reads them
        // under Investment, last.
        var list = crossing.Events.ToList();
        var marker = list.FindIndex(static item =>
            item is TurnPhaseCompletedEvent { Phase: TurnPhase.Investment });
        Assert.True(list.IndexOf(events[^1]) < marker);
    }

    [Fact]
    public void AWorldWithNoTechnologiesAnnouncesNothing()
    {
        var state = CreateState(technologies: []);

        Assert.Empty(ResolveTurns(state, 8));
    }

    [Fact]
    public void AGameOpeningInsideTheArrivalYearNeverAnnouncesThatYear()
    {
        // 1817 Q1: Reaper and Bessemer are available as the game opens. Only
        // Dynamite is still to come, on the turn ending in 1818 Q1.
        var state = CreateState(technologies: Catalog(), year: 1817);

        Assert.Equal([(4, Dynamite)], ResolveTurns(state, 8));
    }

    private static List<(int Turn, int Technology)> ResolveTurns(WorldState state, int turns)
    {
        var announced = new List<(int Turn, int Technology)>();
        for (var turn = 0; turn < turns; turn++)
        {
            var resolution = TurnResolver.Resolve(state, Empty(state), 0);
            announced.AddRange(resolution.Events
                .OfType<TechnologyAvailableEvent>()
                .Select(item => (item.TurnNumber, item.Technology.Value)));
        }

        return announced;
    }

    private static TurnOrders Empty(WorldState state) => new(Enumerable
        .Range(0, state.Definition.Countries.Count)
        .Select(static index => new CountryTurnOrders(new CountryId(index))));

    private static TechnologyDefinition[] Catalog() =>
    [
        new(new TechnologyId(SeedDrill), "Seed Drill", null, 1817),
        new(new TechnologyId(CottonGin), "Cotton Gin", null, 1816, 1_000),
        new(new TechnologyId(Reaper), "Mechanical Reaper", null, 1817, 2_000),
        new(new TechnologyId(Bessemer), "Bessemer Converter", null, 1817, 3_000),
        new(new TechnologyId(Dynamite), "Dynamite", null, 1818, 4_000),
    ];

    private static WorldState CreateState(
        IEnumerable<TechnologyDefinition> technologies, int year = 1816)
    {
        var country = new CountryId(0);
        var dimensions = new MapDimensions(1, 1);
        var map = new MapDefinition(
            dimensions,
            [
                new CellDefinition(
                    new CellIndex(0),
                    dimensions.GetCoordinate(new CellIndex(0)),
                    new TerrainId(0),
                    CellRegion.ForProvince(new ProvinceId(0)),
                    null,
                    SettlementSiteKind.Urban),
            ],
            [new ProvinceDefinition(new ProvinceId(0), "Home")],
            [],
            [],
            [new TerrainDefinition(new TerrainId(0), "Plains")]);
        var scenario = new ScenarioDefinition(
            "Inventions",
            year,
            [country],
            initialCountryCapitals: [new CountryCapital(country, new CellIndex(0))]);
        return new WorldState(new WorldDefinition(
            map,
            [new CountryDefinition(country, "Country 0")],
            scenario,
            [new CommodityDefinition(new CommodityId(0), "Grain", CommodityCategory.Raw)],
            technologies: technologies));
    }
}
