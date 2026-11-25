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
    public void ASubsidisedDealPrintsItsTotalRatherThanAUnitPriceThatDoesNotMultiplyOut()
    {
        var renderer = CreateRenderer();
        var grain = new CommodityId(0);

        var plain = Assert.Single(renderer.Render(new CommodityTradedEvent(1, Blue, Red, grain, 5, 100, Red)));
        var subsidised = Assert.Single(renderer.Render(
            new CommodityTradedEvent(1, Blue, Red, grain, 5, 100, Red, total: 475)));

        Assert.Contains("a unit", plain.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("subsidised", plain.Text, StringComparison.Ordinal);
        Assert.Contains("subsidised", subsidised.Text, StringComparison.Ordinal);
        Assert.Contains("475", subsidised.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void ARefusedExpansionNamesTheFacilityAndWhy()
    {
        var renderer = CreateRenderer();

        var line = Assert.Single(renderer.Render(new FacilityExpansionRefusedEvent(
            1, Blue, new ProductionFacilityId(0), FacilityExpansionRefusal.TechnologyNotKnown)));

        Assert.Equal(TurnReportKind.Refusal, line.Kind);
        Assert.Contains("could not expand Lumber Mill", line.Text, StringComparison.Ordinal);
        Assert.Contains(
            TurnReportText.Describe(FacilityExpansionRefusal.TechnologyNotKnown),
            line.Text,
            StringComparison.Ordinal);
    }

    [Fact]
    public void WarAndFleetOrdersNameTheCountriesTheTaskForceAndTheSea()
    {
        var renderer = CreateRenderer();

        var declared = Assert.Single(renderer.Render(new WarDeclaredEvent(1, Blue, Red)));
        Assert.Equal(TurnReportKind.Outcome, declared.Kind);
        Assert.Contains("declared war on", declared.Text, StringComparison.Ordinal);
        Assert.Contains("begins next turn", declared.Text, StringComparison.Ordinal);

        var refused = Assert.Single(renderer.Render(new WarDeclarationRefusedEvent(
            1, Blue, new CountryId(40), WarDeclarationRefusal.NoSuchCountry)));
        Assert.Equal(TurnReportKind.Refusal, refused.Kind);
        Assert.Contains("Country 40", refused.Text, StringComparison.Ordinal);
        Assert.Contains(
            TurnReportText.Describe(WarDeclarationRefusal.NoSuchCountry), refused.Text, StringComparison.Ordinal);

        var patrol = Assert.Single(renderer.Render(
            new TaskForcePatrolOrderedEvent(1, Blue, new TaskForceId(2), new SeaZoneId(0))));
        Assert.Contains("task force 2 is patrolling Northern Sea", patrol.Text, StringComparison.Ordinal);

        // A leg cut short by the slowest hull is a shortfall, and an unknown zone
        // still renders rather than throwing.
        var shortLeg = Assert.Single(renderer.Render(new TaskForceSailedEvent(
            1, Blue, new TaskForceId(2), new SeaZoneId(0), new SeaZoneId(7), new SeaZoneId(9))));
        Assert.Equal(TurnReportKind.Shortfall, shortLeg.Kind);
        Assert.Contains(
            "sailed from Northern Sea as far as Sea zone 7, bound for Sea zone 9",
            shortLeg.Text,
            StringComparison.Ordinal);

        var assemble = Assert.Single(renderer.Render(new NavalOrderRefusedEvent(
            1, Blue, NavalOrderKind.Assemble, null, NavalOrderRefusal.FleetNotPlaced)));
        Assert.Equal(TurnReportKind.Refusal, assemble.Kind);
        Assert.Contains("could not assemble a task force:", assemble.Text, StringComparison.Ordinal);

        var assembled = Assert.Single(renderer.Render(new TaskForceAssembledEvent(
            1, Blue, new TaskForceId(1), new SeaZoneId(0), [new FleetId(30)])));
        Assert.Contains("from fleet 30 in Northern Sea", assembled.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void ShipsBuiltAndRefusedNameTheHullAndTheBill()
    {
        var renderer = CreateRenderer();
        CommodityQuantity[] bill = [new(new CommodityId(1), 8)];

        var built = Assert.Single(renderer.Render(
            new ShipsBuiltEvent(1, Blue, new ShipTypeId(0), 2, new FleetId(1), bill)));
        Assert.Equal(TurnReportKind.Outcome, built.Kind);
        Assert.Contains("built 2 Trader hulls, paying 8 Lumber", built.Text, StringComparison.Ordinal);

        var refused = Assert.Single(renderer.Render(new ShipBuildRefusedEvent(
            1, Blue, new ShipTypeId(0), 1, ShipBuildRefusal.TechnologyNotKnown)));
        Assert.Equal(TurnReportKind.Refusal, refused.Kind);
        Assert.Contains("could not build 1 Trader hull:", refused.Text, StringComparison.Ordinal);
        Assert.Contains(
            TurnReportText.Describe(ShipBuildRefusal.TechnologyNotKnown),
            refused.Text,
            StringComparison.Ordinal);

        // A hull the world does not define still renders rather than throwing.
        var unknown = Assert.Single(renderer.Render(new ShipBuildRefusedEvent(
            1, Blue, new ShipTypeId(40), 1, ShipBuildRefusal.UnknownType)));
        Assert.Contains("Ship 40", unknown.Text, StringComparison.Ordinal);
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
    public void PowerNobodyUsedIsReportedAsALossOnItsOwnLine()
    {
        var renderer = CreateRenderer();

        var lines = renderer.Render(new PowerGeneratedEvent(1, Blue, new CommodityId(1), 10, 60, 40));
        var allUsed = renderer.Render(new PowerGeneratedEvent(1, Blue, new CommodityId(1), 10, 60, 60));

        Assert.Equal(2, lines.Count);
        Assert.Equal(TurnReportKind.Outcome, lines[0].Kind);
        Assert.Contains("60 power", lines[0].Text, StringComparison.Ordinal);
        Assert.Equal(TurnReportKind.Loss, lines[1].Kind);
        Assert.Contains("wasted 20 power", lines[1].Text, StringComparison.Ordinal);
        Assert.Single(allUsed);
    }

    [Fact]
    public void TrainingSaysWhatItCostAndWhenTheWorkersStart()
    {
        var renderer = CreateRenderer();

        var line = Assert.Single(renderer.Render(new WorkersTrainedEvent(
            1, Blue, WorkerGrade.Trained, WorkerGrade.Expert, 1, [new CommodityQuantity(new CommodityId(1), 2)], 1000)));

        Assert.Equal(TurnReportKind.Outcome, line.Kind);
        Assert.Contains("1 trained worker to expert", line.Text, StringComparison.Ordinal);
        Assert.Contains("2 Lumber and $1,000", line.Text, StringComparison.Ordinal);
        Assert.Contains("next turn", line.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void TheUniversitySaysWhatACivilianCostAndWhenItArrives()
    {
        // The fixture world declares no civilian types, so the type falls back to
        // its number rather than throwing — the same courtesy an unknown hull gets.
        var renderer = CreateRenderer();
        var type = new CivilianTypeId(0);

        var bought = Assert.Single(renderer.Render(new CiviliansPurchasedEvent(
            1, Blue, type, 2, 2, [new CommodityQuantity(new CommodityId(1), 4)], 2000)));
        Assert.Equal(TurnReportKind.Outcome, bought.Kind);
        Assert.Contains("bought 2 civilian type 0s at the University", bought.Text, StringComparison.Ordinal);
        Assert.Contains("2 expert workers and 4 Lumber and $2,000", bought.Text, StringComparison.Ordinal);

        var refused = Assert.Single(renderer.Render(new CivilianPurchaseRefusedEvent(
            1, Blue, type, 1, CivilianPurchaseRefusal.TechnologyNotKnown)));
        Assert.Equal(TurnReportKind.Refusal, refused.Kind);
        Assert.Contains(
            TurnReportText.Describe(CivilianPurchaseRefusal.TechnologyNotKnown),
            refused.Text,
            StringComparison.Ordinal);

        var delivered = Assert.Single(renderer.Render(new CiviliansDeliveredEvent(
            1, Blue, type, Land, [new CivilianUnitId(7)])));
        Assert.Equal(Land, delivered.Cell);
        Assert.Contains("next turn", delivered.Text, StringComparison.Ordinal);

        var lost = Assert.Single(renderer.Render(new CivilianDeliveryLostEvent(1, Blue, type, 1)));
        Assert.Equal(TurnReportKind.Loss, lost.Kind);

        var disbanded = Assert.Single(renderer.Render(new CivilianDisbandedEvent(
            1, Blue, new CivilianUnitId(7), type, 1)));
        Assert.Contains("returning 1 expert worker who works from next turn", disbanded.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void TheArmourySaysWhatARegimentCostAndHowItTravelled()
    {
        // The fixture world declares no army types, so the type falls back to its
        // number rather than throwing — the same courtesy an unknown civilian gets.
        var renderer = CreateRenderer();
        var type = new ArmyTypeId(2);

        var bought = Assert.Single(renderer.Render(new RegimentsPurchasedEvent(
            1, Blue, ArmyCategory.RegularInfantry, type, 2, WorkerGrade.Untrained, 2,
            [new CommodityQuantity(new CommodityId(1), 2)], 1000)));
        Assert.Equal(TurnReportKind.Outcome, bought.Kind);
        Assert.Contains("raised 2 regiments of army type 2 at the Armoury", bought.Text, StringComparison.Ordinal);
        Assert.Contains("2 untrained workers and 2 Lumber and $1,000", bought.Text, StringComparison.Ordinal);

        var refused = Assert.Single(renderer.Render(new RegimentPurchaseRefusedEvent(
            1, Blue, ArmyCategory.Militia, 1, RegimentPurchaseRefusal.NotPurchasable)));
        Assert.Equal(TurnReportKind.Refusal, refused.Kind);
        Assert.Contains("1 militia regiment", refused.Text, StringComparison.Ordinal);

        var lost = Assert.Single(renderer.Render(new RegimentDeliveryLostEvent(1, Blue, type, 1)));
        Assert.Equal(TurnReportKind.Loss, lost.Kind);

        var rail = Assert.Single(renderer.Render(new RegimentsMovedEvent(
            1, Blue, [new RegimentId(1), new RegimentId(2)], new ProvinceId(0), new ProvinceId(0),
            RegimentMoveMode.Rail, 10)));
        Assert.Contains("2 regiments", rail.Text, StringComparison.Ordinal);
        Assert.Contains("by rail, using 10 transport capacity", rail.Text, StringComparison.Ordinal);

        var march = Assert.Single(renderer.Render(new RegimentsMovedEvent(
            1, Blue, [new RegimentId(1)], new ProvinceId(0), new ProvinceId(0), RegimentMoveMode.March, 0)));
        Assert.Contains("on the march", march.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void ABattleSaysWhoWonWhatWasTakenAndWhatWasLost()
    {
        var renderer = CreateRenderer();
        var province = new ProvinceId(0);

        var battle = Assert.Single(renderer.Render(new BattleFoughtEvent(
            1, province, Blue, Red,
            [
                new BattleParticipant(new RegimentId(1), new ArmyTypeId(2), BattleSide.Attacker, 500, 300, 35),
                new BattleParticipant(new RegimentId(2), new ArmyTypeId(2), BattleSide.Defender, 500, 0, 0),
            ],
            [new RegimentId(2)], Blue, 3)));
        Assert.Equal(TurnReportKind.Outcome, battle.Kind);
        Assert.Equal(Blue, battle.Country);
        Assert.Contains("after 3 rounds and 1 regiment was destroyed", battle.Text, StringComparison.Ordinal);

        var captured = Assert.Single(renderer.Render(new ProvinceCapturedEvent(
            1, province, Red, Blue, uncontested: true, [new RegimentId(1)], [new CivilianUnitId(4)], capitalLost: true)));
        Assert.Contains(", undefended; 1 civilian there was killed;", captured.Text, StringComparison.Ordinal);
        Assert.Contains("Red Empire's capital, which is lost", captured.Text, StringComparison.Ordinal);

        var anarchy = Assert.Single(renderer.Render(new ProvinceCapturedEvent(
            1, province, null, Blue, uncontested: true, [new RegimentId(1)], [], capitalLost: false)));
        Assert.DoesNotContain(" from ", anarchy.Text, StringComparison.Ordinal);

        var surrendered = Assert.Single(renderer.Render(new RegimentsSurrenderedEvent(1, Red, [new RegimentId(2)], province)));
        Assert.Equal(TurnReportKind.Loss, surrendered.Kind);

        var healed = Assert.Single(renderer.Render(new RegimentsHealedEvent(1, Blue, [new RegimentId(1), new RegimentId(3)], 150)));
        Assert.Contains("2 resting regiments recovered 150 health", healed.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void ANavalBattleAndAnInterceptionSayWhoLostWhatAndThatTheMoneyStands()
    {
        var renderer = CreateRenderer();

        var blockade = Assert.Single(renderer.Render(
            new TaskForceBlockadeOrderedEvent(1, Blue, new TaskForceId(2), new SeaZoneId(0), Red)));
        Assert.Contains("task force 2 is blockading Red Empire's capital from Northern Sea", blockade.Text, StringComparison.Ordinal);

        var battle = Assert.Single(renderer.Render(new NavalBattleFoughtEvent(
            1, NavalBattleKind.Escort, new SeaZoneId(0), Blue, Red,
            [
                new NavalBattleParticipant(new FleetId(1), new ShipTypeId(0), BattleSide.Attacker, 2, 0),
                new NavalBattleParticipant(new FleetId(2), new ShipTypeId(0), BattleSide.Defender, 1, 1),
            ],
            Blue, 4)));
        Assert.Equal(TurnReportKind.Outcome, battle.Kind);
        Assert.Contains("merchant escort", battle.Text, StringComparison.Ordinal);
        Assert.Contains("after 4 rounds", battle.Text, StringComparison.Ordinal);

        var quiet = Assert.Single(renderer.Render(new NavalBattleFoughtEvent(
            1, NavalBattleKind.Patrol, new SeaZoneId(0), Blue, Red,
            [new NavalBattleParticipant(new FleetId(1), new ShipTypeId(0), BattleSide.Attacker, 1, 0)], Red, 20)));
        Assert.Contains("no ship was sunk", quiet.Text, StringComparison.Ordinal);

        var sunk = Assert.Single(renderer.Render(new MerchantInterceptedEvent(
            1, Blue, new TaskForceId(2), Red, Blue, Red, new CommodityId(0), 2,
            InterceptionOutcome.Sunk, TaskForceActivity.Blockading, new ShipTypeId(0))));
        Assert.Equal((TurnReportKind.Loss, Red), (sunk.Kind, sunk.Country));
        Assert.Contains("blockade (task force 2)", sunk.Text, StringComparison.Ordinal);
        Assert.Contains("sunk with its cargo", sunk.Text, StringComparison.Ordinal);
        Assert.Contains("The money already paid stands.", sunk.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void ALandingSaysWhereItCameFromWhatItCarriedAndWhoDrowned()
    {
        var renderer = CreateRenderer();
        var province = new ProvinceId(0);

        var site = Assert.Single(renderer.Render(
            new TaskForceLandingOrderedEvent(1, Blue, new TaskForceId(2), new SeaZoneId(0), province)));
        Assert.Equal(TurnReportKind.Outcome, site.Kind);
        Assert.Contains("task force 2 holds a landing site on", site.Text, StringComparison.Ordinal);
        Assert.Contains("from Northern Sea; troops may cross from next turn.", site.Text, StringComparison.Ordinal);

        var intercepted = Assert.Single(renderer.Render(new NavalBattleFoughtEvent(
            1, NavalBattleKind.Landing, new SeaZoneId(0), Red, Blue,
            [new NavalBattleParticipant(new FleetId(1), new ShipTypeId(0), BattleSide.Defender, 2, 1)], Red, 3)));
        Assert.Contains("Red Empire's ships fell on", intercepted.Text, StringComparison.Ordinal);
        Assert.Contains("landing fleet in Northern Sea", intercepted.Text, StringComparison.Ordinal);

        var landed = Assert.Single(renderer.Render(new RegimentsLandedEvent(
            1, Blue, new TaskForceId(2), [new RegimentId(1), new RegimentId(3)], province, province, 4, 2)));
        Assert.Equal((TurnReportKind.Outcome, Blue), (landed.Kind, landed.Country));
        Assert.Contains("landed 2 regiments from", landed.Text, StringComparison.Ordinal);
        Assert.Contains("through task force 2, carrying 4 armaments with room for 2 more this turn.", landed.Text, StringComparison.Ordinal);

        var drowned = Assert.Single(renderer.Render(new RegimentsDrownedEvent(
            1, Blue, new TaskForceId(2), [new RegimentId(5)], province)));
        Assert.Equal((TurnReportKind.Loss, Blue), (drowned.Kind, drowned.Country));
        Assert.Contains("lost 1 regiment at sea", drowned.Text, StringComparison.Ordinal);
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
            new FacilityExpansionRefusedEvent(
                1, Blue, new ProductionFacilityId(0), FacilityExpansionRefusal.TechnologyNotKnown),
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
            new WorkersTrainedEvent(1, Blue, WorkerGrade.Untrained, WorkerGrade.Trained, 2, some, 200),
            new WorkerTrainingRefusedEvent(
                1, Blue, WorkerGrade.Trained, 1, WorkerTrainingRefusal.NotEnoughCash),
            new PowerGeneratedEvent(1, Blue, lumber, 10, 60, 40),
            new PowerRefusedEvent(1, Blue, 101, PowerRefusal.OverTurnLimit),
            new ShipsBuiltEvent(1, Blue, new ShipTypeId(0), 2, new FleetId(1), some),
            new ShipBuildRefusedEvent(1, Blue, new ShipTypeId(0), 3, ShipBuildRefusal.NotEnoughCommodities),
            new WarDeclaredEvent(1, Blue, Red),
            new WarDeclarationRefusedEvent(1, Blue, Blue, WarDeclarationRefusal.Itself),
            new TaskForceAssembledEvent(1, Blue, new TaskForceId(1), new SeaZoneId(0), [new FleetId(1)]),
            new TaskForcePatrolOrderedEvent(1, Blue, new TaskForceId(1), new SeaZoneId(0)),
            new TaskForceSailedEvent(
                1, Blue, new TaskForceId(1), new SeaZoneId(0), new SeaZoneId(0), new SeaZoneId(0)),
            new NavalOrderRefusedEvent(
                1, Blue, NavalOrderKind.Command, new TaskForceId(1), NavalOrderRefusal.AlreadyOrderedThisTurn),
            new TaskForceBlockadeOrderedEvent(1, Blue, new TaskForceId(1), new SeaZoneId(0), Red),
            new TaskForceLandingOrderedEvent(1, Blue, new TaskForceId(1), new SeaZoneId(0), new ProvinceId(0)),
            new NavalBattleFoughtEvent(
                1, NavalBattleKind.Patrol, new SeaZoneId(0), Blue, Red,
                [new NavalBattleParticipant(new FleetId(1), new ShipTypeId(0), BattleSide.Attacker, 1, 0)], Blue, 2),
            new MerchantInterceptedEvent(
                1, Blue, new TaskForceId(1), Red, Blue, Red, grain, 2,
                InterceptionOutcome.Captured, TaskForceActivity.Patrolling, null),
            new CiviliansPurchasedEvent(1, Blue, new CivilianTypeId(0), 1, 1, some, 1000),
            new CivilianPurchaseRefusedEvent(
                1, Blue, new CivilianTypeId(0), 1, CivilianPurchaseRefusal.NotEnoughExperts),
            new CiviliansDeliveredEvent(1, Blue, new CivilianTypeId(0), Land, [unit]),
            new CivilianDeliveryLostEvent(1, Blue, new CivilianTypeId(0), 1),
            new CivilianDisbandedEvent(1, Blue, unit, new CivilianTypeId(0), 1),
            new CivilianDisbandRefusedEvent(1, Blue, unit, CivilianDisbandRefusal.NotYours),
            new DiplomaticOvertureAcceptedEvent(1, Blue, Red, DiplomaticOvertureKind.Consulate, 500),
            new DiplomaticOvertureRefusedEvent(
                1, Blue, Red, DiplomaticOvertureKind.Embassy, DiplomaticOvertureRefusal.NoConsulate),
            new DiplomaticTiesCutEvent(1, Blue, Red, pactBroken: true, postsClosed: true),
            new TradePolicySetEvent(1, Blue, Red, TradePolicy.Subsidy(5)),
            new TradePolicyRefusedEvent(1, Blue, Red, TradePolicy.Boycott, TradePolicyRefusal.NoPost),
            new TradePolicyClearedEvent(1, Blue, Red, TradePolicy.Subsidy(10)),
            new RegimentsPurchasedEvent(
                1, Blue, ArmyCategory.RegularInfantry, new ArmyTypeId(2), 1, WorkerGrade.Untrained, 1, some, 500),
            new RegimentPurchaseRefusedEvent(
                1, Blue, ArmyCategory.Militia, 1, RegimentPurchaseRefusal.NotPurchasable),
            new RegimentsDeliveredEvent(1, Blue, new ArmyTypeId(2), new ProvinceId(0), [new RegimentId(1)]),
            new RegimentDeliveryLostEvent(1, Blue, new ArmyTypeId(2), 1),
            new RegimentsMovedEvent(
                1, Blue, [new RegimentId(1)], new ProvinceId(0), new ProvinceId(0), RegimentMoveMode.Rail, 5),
            new RegimentMoveRefusedEvent(
                1, Blue, [new RegimentId(1)], new ProvinceId(0), RegimentMoveRefusal.NotConnectedByRail),
            new BattleFoughtEvent(
                1, new ProvinceId(0), Blue, Red,
                [new BattleParticipant(new RegimentId(1), new ArmyTypeId(2), BattleSide.Attacker, 500, 300, 35)],
                [], Blue, 4),
            new ProvinceCapturedEvent(1, new ProvinceId(0), Red, Blue, false, [new RegimentId(1)], [unit], true),
            new RegimentsRetreatedEvent(1, Red, [new RegimentId(2)], new ProvinceId(0), new ProvinceId(0)),
            new RegimentsSurrenderedEvent(1, Red, [new RegimentId(2)], new ProvinceId(0)),
            new RegimentsLandedEvent(
                1, Blue, new TaskForceId(1), [new RegimentId(1)], new ProvinceId(0), new ProvinceId(0), 1, 0),
            new RegimentsDrownedEvent(1, Blue, new TaskForceId(1), [new RegimentId(1)], new ProvinceId(0)),
            new RegimentsHealedEvent(1, Blue, [new RegimentId(1)], 100),
        ];
    }
}
