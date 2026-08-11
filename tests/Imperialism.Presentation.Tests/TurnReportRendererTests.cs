using Imperialism.Content;
using Imperialism.Core;
using Imperialism.Presentation;
using Xunit;

namespace Imperialism.Presentation.Tests;

public sealed class TurnReportRendererTests
{
    private static readonly CountryId Blue = new(0);
    private static readonly CountryId Red = new(1);
    private static readonly CellIndex Land = new(0);
    private static readonly CellIndex Ocean = new(2);

    [Fact]
    public void EveryConcreteTurnEventTypeIsRendered()
    {
        // The build-break test. A new event record in Core that nobody taught
        // this renderer about fails here, by name, rather than throwing in front
        // of a player halfway through their turn report.
        var declared = typeof(TurnEvent).Assembly.GetExportedTypes()
            .Where(type => !type.IsAbstract && type.IsSubclassOf(typeof(TurnEvent)))
            .Select(type => type.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        var covered = EveryEvent()
            .Select(turnEvent => turnEvent.GetType().Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(declared, covered);

        var renderer = CreateRenderer();
        foreach (var turnEvent in EveryEvent())
        {
            var lines = renderer.Render(turnEvent);
            if (turnEvent is TurnPhaseCompletedEvent)
            {
                Assert.Empty(lines);
                continue;
            }

            Assert.NotEmpty(lines);
            Assert.All(lines, line => Assert.False(
                string.IsNullOrWhiteSpace(line.Text),
                $"{turnEvent.GetType().Name} produced an empty line."));
            Assert.All(lines, line => Assert.Equal(turnEvent.Phase, line.Phase));
        }
    }

    [Fact]
    public void NoRenderedLineLeaksAnIdentifier()
    {
        var renderer = CreateRenderer();

        foreach (var line in EveryEvent().SelectMany(renderer.Render))
        {
            foreach (var leak in new[] { "Id(", "resource.", "commodity.", "technology.", "country." })
            {
                Assert.False(
                    line.Text.Contains(leak, StringComparison.Ordinal),
                    $"'{line.Text}' leaks '{leak}'.");
            }
        }
    }

    [Fact]
    public void LinesNameCountriesAndCommoditiesRatherThanKeys()
    {
        var renderer = CreateRenderer();

        var line = Assert.Single(renderer.Render(new WorldPriceChangedEvent(1, new CommodityId(1), 300, 320, 4, 9)));

        Assert.Contains("Lumber", line.Text, StringComparison.Ordinal);
        Assert.Contains("$300", line.Text, StringComparison.Ordinal);
        Assert.Null(line.Country);
    }

    [Fact]
    public void AProspectedCellNamesTheCommodityItsDepositYields()
    {
        // A deposit has no name of its own, so it is named by what it pays.
        // 'resource.grain' would be a developer string in a player's sentence.
        var renderer = CreateRenderer();

        var line = Assert.Single(renderer.Render(
            new CellProspectedEvent(1, Blue, new CivilianUnitId(1), Land, [new ResourceId(0)])));

        Assert.Contains("Grain", line.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("resource.grain", line.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void AProspectorThatFoundNothingStillSaysSo()
    {
        var renderer = CreateRenderer();

        var line = Assert.Single(renderer.Render(
            new CellProspectedEvent(1, Blue, new CivilianUnitId(1), Land, [])));

        Assert.Contains("found nothing", line.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void AnOrderFromACivilianThatNoLongerExistsStillRenders()
    {
        // GetCivilian returns null for a unit that has gone, which is precisely
        // the NoSuchCivilian refusal. The number survives so a report of it
        // still identifies the unit.
        var renderer = CreateRenderer();

        var line = Assert.Single(renderer.Render(new CivilianOrderRefusedEvent(
            1, Blue, new CivilianUnitId(9999), Land, CivilianOrderRefusal.NoSuchCivilian)));

        Assert.Contains("9999", line.Text, StringComparison.Ordinal);
        Assert.Equal(TurnReportKind.Refusal, line.Kind);
    }

    [Fact]
    public void ACellInASeaZoneRendersWithoutThrowing()
    {
        // CellRegion.Province throws for anything that is not a province, so a
        // naive namer dies on the first coastal tile in an event.
        var renderer = CreateRenderer();

        var line = Assert.Single(renderer.Render(
            new CivilianDeployedEvent(1, Blue, new CivilianUnitId(1), Land, Ocean)));

        Assert.Contains("Ocean", line.Text, StringComparison.Ordinal);
        Assert.Equal(Ocean, line.Cell);
    }

    [Fact]
    public void AHarvestReportsWhatItStrandedSeparatelyFromWhatItGathered()
    {
        var renderer = CreateRenderer();

        var lines = renderer.Render(new ResourceExtractedEvent(
            1, Blue, 9, 2, 1, 0, [new CommodityQuantity(new CommodityId(0), 12)],
            [new CommodityQuantity(new CommodityId(1), 3)]));

        Assert.Equal(2, lines.Count);
        Assert.Equal(TurnReportKind.Outcome, lines[0].Kind);
        Assert.Contains("gathered 12 Grain", lines[0].Text, StringComparison.Ordinal);
        Assert.Equal(TurnReportKind.Loss, lines[1].Kind);
        Assert.Contains("stranded 3 Lumber", lines[1].Text, StringComparison.Ordinal);
    }

    [Fact]
    public void AHarvestThatStrandedNothingSaysNothingAboutStranding()
    {
        var renderer = CreateRenderer();

        var lines = renderer.Render(new ResourceExtractedEvent(
            1, Blue, 9, 0, 0, 0, [new CommodityQuantity(new CommodityId(0), 12)], []));

        Assert.Single(lines);
    }

    [Fact]
    public void ProductionThatFellShortIsMarkedAsAShortfall()
    {
        var renderer = CreateRenderer();

        var full = Assert.Single(renderer.Render(new ProductionCompletedEvent(
            1, Blue, new ProductionRecipeId(0), 4, 4, 4, 8, [], [])));
        var partial = Assert.Single(renderer.Render(new ProductionCompletedEvent(
            1, Blue, new ProductionRecipeId(0), 4, 1, 1, 2, [], [])));
        var none = Assert.Single(renderer.Render(new ProductionCompletedEvent(
            1, Blue, new ProductionRecipeId(0), 4, 0, 0, 0, [], [])));

        Assert.Equal(TurnReportKind.Outcome, full.Kind);
        Assert.Equal(TurnReportKind.Shortfall, partial.Kind);
        Assert.Equal(TurnReportKind.Shortfall, none.Kind);
        Assert.Contains("1 of 4 cycles", partial.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void ALineCarriesTheCountryItIsAboutSoTheClientNeedNotReadIt()
    {
        var renderer = CreateRenderer();

        var line = Assert.Single(renderer.Render(
            new TechnologyPurchasedEvent(1, Red, new TechnologyId(0), 1200)));

        Assert.Equal(Red, line.Country);
        Assert.Equal("Red Empire", line.CountryName);
        Assert.Contains("$1,200", line.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void AStateFromAnotherScenarioIsRejected()
    {
        var package = WorldContentCompiler.CompilePackage(TurnReportFixture.CreateDocument());
        var alternate = new WorldState(package.GetWorld("scenario.alternate"));

        Assert.Throws<ArgumentException>(
            () => TurnReportRenderer.Create(package, "scenario.demo", alternate));
    }

    private static TurnReportRenderer CreateRenderer()
    {
        var package = WorldContentCompiler.CompilePackage(TurnReportFixture.CreateDocument());
        return TurnReportRenderer.Create(
            package, "scenario.demo", new WorldState(package.GetWorld("scenario.demo")));
    }

    /// <summary>
    /// One instance of every concrete event Core declares.
    /// </summary>
    /// <remarks>
    /// Hand-built rather than constructed by reflection. Several of these
    /// constructors validate their arguments and several take
    /// <c>IEnumerable&lt;CommodityQuantity&gt;</c>, which makes reflective
    /// construction fragile; and written out like this the list doubles as a
    /// readable statement of everything the renderer has to handle.
    /// </remarks>
    private static IReadOnlyList<TurnEvent> EveryEvent()
    {
        var unit = new CivilianUnitId(1);
        var grain = new CommodityId(0);
        var lumber = new CommodityId(1);
        CommodityQuantity[] some = [new(grain, 6)];
        return
        [
            new TurnPhaseCompletedEvent(1, TurnPhase.Connectivity),
            new CommodityTradedEvent(1, Blue, Red, grain, 5, 100, Red),
            new TradeUnfilledEvent(1, Blue, lumber, 8, 3, TradeRefusal.NoMerchantCapacity),
            new WorldPriceChangedEvent(1, lumber, 300, 320, 4, 9),
            new ProductionCompletedEvent(1, Blue, new ProductionRecipeId(0), 2, 2, 2, 4, some, some),
            new FacilityExpandedEvent(1, Blue, new ProductionFacilityId(0), 2, 3, some),
            new TransportCapacityBuiltEvent(1, Blue, 20, 24, 8, some),
            new CellDevelopedEvent(1, Blue, unit, Land, 0, 1),
            new CellProspectedEvent(1, Blue, unit, Land, [new ResourceId(0)]),
            new CivilianWorkBegunEvent(1, Blue, unit, Land, 3, 100),
            new ConstructionBegunEvent(1, Blue, unit, Land, EngineerConstruction.Depot, Land, 3, 200),
            new ConstructionCompletedEvent(1, Blue, unit, Land, EngineerConstruction.Port, Land),
            new CivilianDeployedEvent(1, Blue, unit, Land, new CellIndex(1)),
            new CivilianOrderRefusedEvent(1, Blue, unit, Land, CivilianOrderRefusal.NotEnoughCash),
            new WorkersRecruitedEvent(1, Blue, 3, 2, 4, some),
            new ResourceExtractedEvent(1, Blue, 9, 2, 1, 0, some, some),
            new CommoditiesTransportedEvent(1, Blue, 14, 20, some, some, some, 400),
            new WorkersFedEvent(1, Blue, 6, 1, 1, some),
            new CommodityDeliveredEvent(1, new PendingDelivery(
                new DeliveryId(1), Blue, grain, 12, PendingDeliverySource.Extraction)),
            new TechnologyPurchasedEvent(1, Blue, new TechnologyId(0), 1200),
            new TechnologyPurchaseRefusedEvent(
                1, Blue, new TechnologyId(1), TechnologyPurchaseRefusal.NotEnoughCash),
        ];
    }
}
