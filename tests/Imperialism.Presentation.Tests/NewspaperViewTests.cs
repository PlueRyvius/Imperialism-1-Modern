using System.Reflection;
using Imperialism.Content;
using Imperialism.Core;
using Imperialism.Presentation;
using Xunit;
using Xunit.Abstractions;

namespace Imperialism.Presentation.Tests;

/// <summary>
/// The newspaper a country reads at the start of a turn. Every selection rule
/// tested here is chosen (N1–N4 in <c>docs/formulas/newspaper.md</c>); the tests
/// hold the code to the rules as written, not to the original.
/// </summary>
/// <remarks>
/// The fixture is <see cref="TurnReportFixture"/>'s strip widened to six cells:
/// Home (Blue) | Far (Red) | ocean | Far (Red) | West (Green) | East (Yellow).
/// Blue and Red are Great Powers, Green and Yellow minors, and Purple a Great Power
/// holding no land. So East borders nothing of Blue's or Red's, while West borders
/// Red's Far and not Blue's Home. Dynamite is priced and arrives in 1816, so the
/// fourth turn from 1815 Q1 announces it.
/// </remarks>
public sealed class NewspaperViewTests(ITestOutputHelper output)
{
    private const string ScenarioKey = "scenario.demo";

    private static readonly CountryId Blue = new(0);
    private static readonly CountryId Red = new(1);
    private static readonly CountryId Green = new(2);
    private static readonly CountryId Yellow = new(3);
    private static readonly CountryId Purple = new(4);

    private static readonly ProvinceId Home = new(0);
    private static readonly ProvinceId Far = new(1);
    private static readonly ProvinceId West = new(2);
    private static readonly ProvinceId East = new(3);

    private static readonly CommodityId Lumber = new(1);

    [Fact]
    public void NewInventionIsTheFirstStoryAndItsHeadlineReadsNewInvention()
    {
        var (package, state, resolution) = ResolveTurns(4);

        var paper = NewspaperView.Create(package, ScenarioKey, state, resolution, Blue);
        output.WriteLine(Print(paper));

        var first = paper.Stories[0];
        Assert.Equal(NewspaperStoryKind.NewInvention, first.Kind);
        Assert.True(first.IsHeadline);
        Assert.Equal("New Invention", first.Headline);
        Assert.Contains("Dynamite", first.Body, StringComparison.Ordinal);
        Assert.Equal(new TurnDate(1816, 1), paper.Date);
        Assert.Equal("The Blue Republic Gazette", paper.Masthead);
    }

    [Fact]
    public void TheTurnBeforeTheCrossingHasNoInvention()
    {
        var (package, state, resolution) = ResolveTurns(3);

        var paper = NewspaperView.Create(package, ScenarioKey, state, resolution, Blue);

        Assert.DoesNotContain(paper.Stories, story => story.Kind == NewspaperStoryKind.NewInvention);
    }

    [Fact]
    public void AnEliminationOutranksACapture()
    {
        var (package, state) = Fresh();

        // The capture is logged first; priority, not the log, decides.
        var paper = NewspaperView.Compose(package, ScenarioKey, state, new TurnDate(1815, 2),
        [
            new ProvinceCapturedEvent(1, East, Yellow, Green, false, [new RegimentId(1)], [], false),
            new CountryEliminatedEvent(1, Yellow, Green, 0, 0, 0, 0, 0),
        ], Blue);

        Assert.Equal(
            [NewspaperStoryKind.CountryEliminated, NewspaperStoryKind.ProvinceCaptured],
            paper.Stories.Select(static story => story.Kind));
        Assert.Equal("Yellow Duchy Falls", paper.Stories[0].Headline);
    }

    [Fact]
    public void ABattleBetweenTwoMinorsFarFromTheReaderIsAbsentWhileOneBetweenGreatPowersIsPresent()
    {
        var (package, state) = Fresh();
        TurnEvent[] events =
        [
            Battle(East, Green, Yellow),
            Battle(East, Red, Purple),
        ];

        var paper = NewspaperView.Compose(package, ScenarioKey, state, new TurnDate(1815, 2), events, Blue);

        var story = Assert.Single(paper.Stories);
        Assert.Equal(NewspaperStoryKind.LandBattle, story.Kind);
        Assert.Contains("Red Empire attacked Purple Kingdom", story.Body, StringComparison.Ordinal);
    }

    [Fact]
    public void ABattleBetweenMinorsNextToTheReadersLandIsPresent()
    {
        // West borders Red's Far and not Blue's Home.
        var (package, state) = Fresh();
        TurnEvent[] events = [Battle(West, Green, Yellow)];

        var forRed = NewspaperView.Compose(package, ScenarioKey, state, new TurnDate(1815, 2), events, Red);
        var forBlue = NewspaperView.Compose(package, ScenarioKey, state, new TurnDate(1815, 2), events, Blue);

        Assert.Equal(NewspaperStoryKind.LandBattle, Assert.Single(forRed.Stories).Kind);
        Assert.Equal(NewspaperStoryKind.QuietQuarter, Assert.Single(forBlue.Stories).Kind);
    }

    [Fact]
    public void RefusalsNeverAppear()
    {
        var (package, state) = Fresh();
        TurnEvent[] refusals =
        [
            new WarDeclarationRefusedEvent(1, Blue, Blue, WarDeclarationRefusal.Itself),
            new TechnologyPurchaseRefusedEvent(1, Blue, new TechnologyId(1), TechnologyPurchaseRefusal.NotEnoughCash),
            new ShipBuildRefusedEvent(1, Blue, new ShipTypeId(0), 3, ShipBuildRefusal.NotEnoughCommodities),
            new CivilianOrderRefusedEvent(1, Blue, new CivilianUnitId(1), new CellIndex(0), CivilianOrderRefusal.NotEnoughCash),
            new NavalOrderRefusedEvent(1, Blue, NavalOrderKind.Command, new TaskForceId(1), NavalOrderRefusal.AlreadyOrderedThisTurn),
            new DiplomaticOvertureRefusedEvent(1, Blue, Red, DiplomaticOvertureKind.Embassy, DiplomaticOvertureRefusal.NoConsulate),
            new TradePolicyRefusedEvent(1, Blue, Red, TradePolicy.Boycott, TradePolicyRefusal.NoPost),
            new RegimentPurchaseRefusedEvent(1, Blue, ArmyCategory.Militia, 1, RegimentPurchaseRefusal.NotPurchasable),
            new RegimentMoveRefusedEvent(1, Blue, [new RegimentId(1)], Far, RegimentMoveRefusal.NotConnectedByRail),
            new FacilityExpansionRefusedEvent(1, Blue, new ProductionFacilityId(0), FacilityExpansionRefusal.TechnologyNotKnown),
            new TradeUnfilledEvent(1, Blue, Lumber, 8, 3, TradeRefusal.NoMerchantCapacity),
            new EliminatedCountryOrdersIgnoredEvent(1, Blue),
        ];

        var paper = NewspaperView.Compose(package, ScenarioKey, state, new TurnDate(1815, 2), refusals, Blue);

        Assert.Equal(NewspaperStoryKind.QuietQuarter, Assert.Single(paper.Stories).Kind);
        Assert.Equal(0, paper.StoriesOmitted);
    }

    [Fact]
    public void FifteenStoriesAreCutToTwelveAndNoHeadlineGivesWayToAnItem()
    {
        var (package, state) = Fresh();

        // Ten items logged before five headlines: a cap applied in log order would
        // keep every item and drop three headlines.
        var events = new List<TurnEvent>();
        for (var index = 0; index < 10; index++)
        {
            events.Add(new WorldPriceChangedEvent(1, Lumber, 300 + index, 301 + index, 4, 9));
        }

        events.Add(new WarDeclaredEvent(1, Green, Yellow));
        events.Add(new WarDeclaredEvent(1, Yellow, Green));
        events.Add(new WarDeclaredEvent(1, Red, Purple));
        events.Add(new WarDeclaredEvent(1, Purple, Green));
        events.Add(new WarDeclaredEvent(1, Red, Yellow));

        var paper = NewspaperView.Compose(package, ScenarioKey, state, new TurnDate(1815, 2), events, Blue);
        output.WriteLine(Print(paper));

        Assert.Equal(12, paper.Stories.Count);
        Assert.Equal(3, paper.StoriesOmitted);
        Assert.Equal(
            Enumerable.Repeat(true, 5).Concat(Enumerable.Repeat(false, 7)),
            paper.Stories.Select(static story => story.IsHeadline));

        // The items kept are the first seven the log gave.
        Assert.Contains("$300", paper.Stories[5].Body, StringComparison.Ordinal);
        Assert.Contains("$306", paper.Stories[11].Body, StringComparison.Ordinal);
    }

    [Fact]
    public void AQuietTurnHasExactlyTheOneFixedItem()
    {
        var (package, state) = Fresh();
        TurnEvent[] bookkeeping =
        [
            new TurnPhaseCompletedEvent(1, TurnPhase.Diplomacy),
            new ResourceExtractedEvent(1, Blue, 9, 2, 1, 0, [new CommodityQuantity(Lumber, 3)], []),
            new CommoditiesTransportedEvent(1, Blue, 14, 20, [new CommodityQuantity(Lumber, 3)], [], [], 0),
            new CommodityDeliveredEvent(1, new PendingDelivery(
                new DeliveryId(1), Blue, Lumber, 3, PendingDeliverySource.Extraction)),
            new WorkersFedEvent(1, Blue, 7, 0, 0, []),
            new TurnPhaseCompletedEvent(1, TurnPhase.Connectivity),
        ];

        var paper = NewspaperView.Compose(package, ScenarioKey, state, new TurnDate(1815, 2), bookkeeping, Blue);

        var story = Assert.Single(paper.Stories);
        Assert.Equal(
            new NewspaperStory(
                NewspaperStoryKind.QuietQuarter, false, "A Quiet Quarter", NewspaperText.QuietQuarterBody),
            story);
        Assert.Equal(0, paper.StoriesOmitted);
    }

    [Fact]
    public void TwoReadersOfOneTurnShareTheWorldsHeadlinesAndNotEachOthersHomeNews()
    {
        var (package, state) = Fresh();
        TurnEvent[] events =
        [
            new WarDeclaredEvent(1, Green, Yellow),
            new WorkersFedEvent(1, Blue, 5, 1, 0, []),
            new WorkersFedEvent(1, Red, 4, 0, 2, []),
            new ShipsBuiltEvent(1, Blue, new ShipTypeId(0), 1, new FleetId(1), []),
        ];

        var blue = NewspaperView.Compose(package, ScenarioKey, state, new TurnDate(1815, 2), events, Blue);
        var red = NewspaperView.Compose(package, ScenarioKey, state, new TurnDate(1815, 2), events, Red);

        Assert.Equal(blue.Stories[0], red.Stories[0]);
        Assert.Equal("Green Principality Declares War on Yellow Duchy", blue.Stories[0].Headline);
        Assert.Equal(
            [NewspaperStoryKind.WarDeclared, NewspaperStoryKind.Hunger, NewspaperStoryKind.ShipsLaunched],
            blue.Stories.Select(static story => story.Kind));
        Assert.Equal(
            [NewspaperStoryKind.WarDeclared, NewspaperStoryKind.Hunger],
            red.Stories.Select(static story => story.Kind));
        Assert.Contains("Blue Republic had 1 fall ill", blue.Stories[1].Body, StringComparison.Ordinal);
        Assert.Contains("Red Empire had 0 fall ill and 2 starve", red.Stories[1].Body, StringComparison.Ordinal);
    }

    [Fact]
    public void ThePaperKeepsNoReferenceToTheWorldAndDoesNotChangeWhenItMovesOn()
    {
        var (package, state, resolution) = ResolveTurns(4);
        var paper = NewspaperView.Create(package, ScenarioKey, state, resolution, Blue);
        var before = paper.Stories.ToArray();

        state.SetProvinceOwner(Home, Red);
        state.SetCash(Blue, 1);
        _ = TurnResolver.Resolve(state, TurnOrders.Empty(state.Definition.Countries.Count), 9);

        Assert.Equal(before, paper.Stories);
        Assert.Equal(new TurnDate(1816, 1), paper.Date);

        // And by construction: no field of the view or a story can hold the world.
        Type[] forbidden =
        [
            typeof(WorldState), typeof(WorldDefinition), typeof(CompiledWorldPackage), typeof(TurnReportRenderer),
            typeof(TurnResolution),
        ];
        foreach (var type in new[] { typeof(NewspaperView), typeof(NewspaperStory) })
        {
            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                Assert.DoesNotContain(forbidden, item => item.IsAssignableFrom(field.FieldType));
            }
        }
    }

    [Fact]
    public void TheSameResolutionRendersTheSamePaper()
    {
        var (package, state, resolution) = ResolveTurns(4);

        var first = NewspaperView.Create(package, ScenarioKey, state, resolution, Blue);
        var second = NewspaperView.Create(package, ScenarioKey, state, resolution, Blue);

        Assert.Equal(first.Masthead, second.Masthead);
        Assert.Equal(first.Date, second.Date);
        Assert.Equal(first.Stories, second.Stories);
        Assert.Equal(first.StoriesOmitted, second.StoriesOmitted);
    }

    [Fact]
    public void APaperRejectsAStateThatHasMovedOn()
    {
        var (package, state, resolution) = ResolveTurns(1);
        _ = TurnResolver.Resolve(state, TurnOrders.Empty(state.Definition.Countries.Count), 2);

        Assert.Throws<ArgumentException>(
            () => NewspaperView.Create(package, ScenarioKey, state, resolution, Blue));
    }

    private static BattleFoughtEvent Battle(ProvinceId province, CountryId attacker, CountryId defender) => new(
        1,
        province,
        attacker,
        defender,
        [new BattleParticipant(new RegimentId(1), new ArmyTypeId(0), BattleSide.Attacker, 500, 300, 35)],
        [],
        attacker,
        4);

    private static string Print(NewspaperView paper)
    {
        var lines = new List<string> { paper.Masthead, paper.Date.ToString() };
        foreach (var story in paper.Stories)
        {
            lines.Add(story.IsHeadline ? $"** {story.Headline} **" : $"-- {story.Headline}");
            lines.Add($"   {story.Body}");
        }

        if (paper.StoriesOmitted > 0)
        {
            lines.Add($"... and {paper.StoriesOmitted} more stories");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static (CompiledWorldPackage Package, WorldState State, TurnResolution Resolution) ResolveTurns(int turns)
    {
        var (package, state) = Fresh();
        TurnResolution? resolution = null;
        for (var turn = 0; turn < turns; turn++)
        {
            resolution = TurnResolver.Resolve(state, TurnOrders.Empty(state.Definition.Countries.Count), (ulong)turn);
        }

        return (package, state, resolution!);
    }

    private static (CompiledWorldPackage Package, WorldState State) Fresh()
    {
        var package = WorldContentCompiler.CompilePackage(CreateDocument());
        return (package, new WorldState(package.GetWorld(ScenarioKey)));
    }

    private static WorldContentDocument CreateDocument()
    {
        var document = TurnReportFixture.CreateDocument();
        var map = document.Map!;
        map.Width = 6;
        map.Provinces =
        [
            .. map.Provinces,
            new NamedContentDefinition { Key = "province.west", Name = "West Province" },
            new NamedContentDefinition { Key = "province.east", Name = "East Province" },
        ];
        map.Cells =
        [
            .. map.Cells,
            new CellContentDocument
            {
                Terrain = "terrain.plains",
                Region = new CellRegionContent { Province = "province.west" },
            },
            new CellContentDocument
            {
                Terrain = "terrain.plains",
                Region = new CellRegionContent { Province = "province.east" },
            },
        ];
        document.Countries =
        [
            .. document.Countries,
            new CountryContentDefinition { Key = "country.green", Name = "Green Principality" },
            new CountryContentDefinition { Key = "country.yellow", Name = "Yellow Duchy" },
            new CountryContentDefinition { Key = "country.purple", Name = "Purple Kingdom", IsGreatPower = true },
        ];
        foreach (var scenario in document.Scenarios)
        {
            scenario.ProvinceOwners =
            [
                .. scenario.ProvinceOwners,
                new ProvinceOwnerContent { Province = "province.west", Country = "country.green" },
                new ProvinceOwnerContent { Province = "province.east", Country = "country.yellow" },
            ];
        }

        var dynamite = document.Technologies.Single(static item => item.Key == "technology.dynamite");
        dynamite.AvailableFrom = 1816;
        dynamite.Cost = 4_000;
        return document;
    }
}
