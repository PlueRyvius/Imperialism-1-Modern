using Imperialism.Content;
using Imperialism.Core;
using Xunit;

namespace Imperialism.Presentation.Tests;

/// <summary>
/// The play-test wording faults: enum names on the screens, wrong plurals, the trade
/// header's two totals, and a technology refusal that blamed building for a price the
/// treasury never covered.
/// </summary>
public sealed class PlayTestWordingTests
{
    private static readonly CountryId Blue = new(0);

    [Fact]
    public void CountsTakeTheRightNumber()
    {
        Assert.Equal("1 hull", Plural.Count(1, "hull"));
        Assert.Equal("2 hulls", Plural.Count(2, "hull"));
        Assert.Equal("0 hulls", Plural.Count(0, "hull"));
        Assert.Equal("1,200 tiles", Plural.Count(1200, "tile"));
        Assert.Equal("1 unit of Horses", Plural.Quantity(1, "Horses"));
        Assert.Equal("2 Horses", Plural.Quantity(2, "Horses"));
        Assert.Equal("1 Grain", Plural.Quantity(1, "Grain"));
    }

    [Fact]
    public void ArmyCategoriesAndNavalNamesAreWords()
    {
        Assert.Equal("Light Infantry", MilitaryText.CategoryName(ArmyCategory.LightInfantry));
        Assert.Equal("Combat Engineers", MilitaryText.CategoryName(ArmyCategory.CombatEngineers));
        foreach (var category in Enum.GetValues<ArmyCategory>())
        {
            AssertNoEnumName(MilitaryText.CategoryName(category), category.ToString());
        }

        foreach (var activity in Enum.GetValues<TaskForceActivity>())
        {
            AssertNoEnumName(MilitaryText.ActivityName(activity), activity.ToString());
        }

        foreach (var mission in Enum.GetValues<NavalMissionKind>())
        {
            AssertNoEnumName(MilitaryText.MissionName(mission), mission.ToString());
        }

        Assert.Equal("Holding a landing site", MilitaryText.ActivityName(TaskForceActivity.Landing));
    }

    [Fact]
    public void RefusalsOnTheMilitaryScreensAreSentencesNotEnumNames()
    {
        Assert.Equal(
            "Generals are not bought here.",
            MilitaryText.Unavailable(ArmyCategory.General, RegimentPurchaseRefusal.NotPurchasable));

        foreach (var refusal in Enum.GetValues<RegimentPurchaseRefusal>())
        {
            var text = MilitaryText.Result(new OrderWarning<RegimentPurchaseRefusal>(refusal, false), ArmyCategory.LightInfantry);
            Assert.StartsWith("Warning: ", text, StringComparison.Ordinal);
            Assert.EndsWith(".", text, StringComparison.Ordinal);
            AssertNoEnumName(text, refusal.ToString());
        }

        foreach (var refusal in Enum.GetValues<ShipBuildRefusal>())
        {
            AssertNoEnumName(MilitaryText.Result(new OrderWarning<ShipBuildRefusal>(refusal, false)), refusal.ToString());
        }

        foreach (var refusal in Enum.GetValues<NavalOrderRefusal>())
        {
            AssertNoEnumName(MilitaryText.Result(new OrderWarning<NavalOrderRefusal>(refusal, false)), refusal.ToString());
        }

        Assert.Equal(MilitaryText.AcceptedByPreview, MilitaryText.Result((OrderWarning<ShipBuildRefusal>?)null));
        Assert.Equal(MilitaryText.SetAside, MilitaryText.Result(new OrderWarning<NavalOrderRefusal>(null, true)));
    }

    [Fact]
    public void AHarvestFromOneTileAndOnePortIsSingular()
    {
        var renderer = CreateRenderer();

        var line = renderer.Render(new ResourceExtractedEvent(
            1, Blue, 1, 0, 1, 0, [new CommodityQuantity(new CommodityId(0), 1)], []))[0].Text;

        Assert.Contains("1 tile and 1 fishing port", line, StringComparison.Ordinal);
        Assert.DoesNotContain("1 tiles", line, StringComparison.Ordinal);
        Assert.DoesNotContain("1 fishing ports", line, StringComparison.Ordinal);
    }

    [Fact]
    public void ANewRegimentIsCountedBeforeTheWordNew()
    {
        var renderer = CreateRenderer();

        var one = renderer.Render(new RegimentsDeliveredEvent(
            1, Blue, new ArmyTypeId(0), new ProvinceId(0), [new RegimentId(1)]))[0].Text;
        var two = renderer.Render(new RegimentsDeliveredEvent(
            1, Blue, new ArmyTypeId(0), new ProvinceId(0), [new RegimentId(1), new RegimentId(2)]))[0].Text;

        Assert.Contains("mustered 1 new regiment of", one, StringComparison.Ordinal);
        Assert.Contains("; it may move from next turn.", one, StringComparison.Ordinal);
        Assert.DoesNotContain("new 1", one, StringComparison.Ordinal);
        Assert.Contains("mustered 2 new regiments of", two, StringComparison.Ordinal);
        Assert.Contains("; they may move from next turn.", two, StringComparison.Ordinal);
    }

    [Fact]
    public void ATechnologyRefusalBlamesBuildingOnlyWhenTheOpeningTreasuryCoveredThePrice()
    {
        var renderer = CreateRenderer();
        var technology = new TechnologyId(0);

        var covered = renderer.Render(new TechnologyPurchaseRefusedEvent(
            1, Blue, technology, TechnologyPurchaseRefusal.NotEnoughCash) { Cost = 1000, CashAtTurnOpen = 5000 })[0].Text;
        var never = renderer.Render(new TechnologyPurchaseRefusedEvent(
            1, Blue, technology, TechnologyPurchaseRefusal.NotEnoughCash) { Cost = 40000, CashAtTurnOpen = 10000 })[0].Text;
        var unknown = renderer.Render(new TechnologyPurchaseRefusedEvent(
            1, Blue, technology, TechnologyPurchaseRefusal.NotEnoughCash))[0].Text;

        Assert.Contains("first call", covered, StringComparison.Ordinal);
        Assert.Contains("$1,000", covered, StringComparison.Ordinal);
        Assert.Contains("$5,000", covered, StringComparison.Ordinal);
        Assert.DoesNotContain("first call", never, StringComparison.Ordinal);
        Assert.Contains("costs $40,000, more than the $10,000 the treasury held", never, StringComparison.Ordinal);
        Assert.DoesNotContain("first call", unknown, StringComparison.Ordinal);
        Assert.Contains("the treasury will not cover it", unknown, StringComparison.Ordinal);
    }

    [Fact]
    public void TheResolverRecordsThePriceAndTheOpeningTreasuryOnACashRefusal()
    {
        var package = OrdersFixture.Compile(document => document.Technologies =
        [
            .. document.Technologies,
            new TechnologyContentDefinition { Key = "technology.e", Name = "Epsilon", Cost = 9000 },
        ]);
        var state = OrdersFixture.NewState(package);
        var draft = new OrdersDraft(state.Definition, Blue);
        var alpha = OrdersFixture.Technology(package, "technology.a");
        var delta = OrdersFixture.Technology(package, "technology.d");
        var epsilon = OrdersFixture.Technology(package, "technology.e");

        // Blue opens with 5,000: Delta (4,500) leaves too little for Alpha (1,000),
        // which the opening treasury covered; Epsilon (9,000) it never covered.
        draft.SetTechnologyPurchase(delta, true);
        draft.SetTechnologyPurchase(alpha, true);
        draft.SetTechnologyPurchase(epsilon, true);
        var resolution = TurnResolver.Resolve(state, draft.BuildTurnOrders(), 1);

        var refused = resolution.Events.OfType<TechnologyPurchaseRefusedEvent>()
            .ToDictionary(static e => e.Technology);
        Assert.Equal((1000L, 5000L), (refused[alpha].Cost, refused[alpha].CashAtTurnOpen));
        Assert.Equal((9000L, 5000L), (refused[epsilon].Cost, refused[epsilon].CashAtTurnOpen));

        var renderer = TurnReportRenderer.Create(package, OrdersFixture.Scenario, state);
        Assert.Contains("first call", renderer.Render(refused[alpha])[0].Text, StringComparison.Ordinal);
        Assert.DoesNotContain("first call", renderer.Render(refused[epsilon])[0].Text, StringComparison.Ordinal);
    }

    [Fact]
    public void TheTradeHeaderShowsBothTotalsAtWorldPrices()
    {
        var package = OrdersFixture.Compile();
        var state = OrdersFixture.NewState(package);
        var draft = new OrdersDraft(state.Definition, Blue);
        var lumber = OrdersFixture.Commodity(package, "commodity.lumber");
        var steel = OrdersFixture.Commodity(package, "commodity.steel");

        // 8 of 10 lumber is sellable after the mill's claim; only those 8 count.
        draft.SetExpansion(OrdersFixture.Facility(package, "facility.lumber-mill"), true);
        draft.SetOffer(lumber, 20);
        draft.SetBid(steel, 1);
        var trade = OrdersPreview.Create(package, OrdersFixture.Scenario, state, draft).Trade;

        Assert.Equal(8 * 300, trade.TotalOfferValue);
        var header = TradeText.Header(trade);
        Assert.Contains("bids: $300 · offers: $2,400 at world prices", header, StringComparison.Ordinal);
    }

    private static void AssertNoEnumName(string text, string enumName)
    {
        // A one-word enum name is also an English word ("Patrol", "Militia"); only
        // multi-word PascalCase names are a sign the raw name leaked.
        if (enumName.Skip(1).Any(char.IsUpper))
        {
            Assert.DoesNotContain(enumName, text, StringComparison.Ordinal);
        }
    }

    private static TurnReportRenderer CreateRenderer()
    {
        var package = WorldContentCompiler.CompilePackage(TurnReportFixture.CreateDocument());
        return TurnReportRenderer.Create(
            package, "scenario.demo", new WorldState(package.GetWorld("scenario.demo")));
    }
}
