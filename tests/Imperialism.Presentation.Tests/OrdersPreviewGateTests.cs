using Imperialism.Core;
using Imperialism.Presentation;
using Xunit;
using static Imperialism.Presentation.Tests.OrdersFixture;

namespace Imperialism.Presentation.Tests;

// Gate regressions for PR #72. A preview must match the resolved turn even
// when orders contend for stock or this turn's output funds later phases.
public sealed class OrdersPreviewGateTests
{
    [Fact]
    public void ExpansionPreviewHonoursTheDraftOrderWhenMaterialsCoverOnlyOneBuilding()
    {
        var package = Compile();
        var state = NewState(package);
        state.GrantTechnology(Blue, Technology(package, "technology.dynamite"));
        Assert.True(state.TryConsumeAvailable(Blue, Commodity(package, "commodity.lumber"), 8));
        Assert.True(state.TryConsumeAvailable(Blue, Commodity(package, "commodity.steel"), 8));
        var refinery = Facility(package, "facility.refinery");
        var mill = Facility(package, "facility.lumber-mill");
        var draft = new OrdersDraft(state.Definition, Blue);
        draft.SetExpansion(refinery, true);
        draft.SetExpansion(mill, true);

        var preview = OrdersPreview.Create(package, Scenario, state, draft).Industry;
        var expanded = TurnResolver.Resolve(state, draft.BuildTurnOrders(), 1).Events
            .OfType<FacilityExpandedEvent>().Where(e => e.Country == Blue).ToArray();

        Assert.Equal(refinery, Assert.Single(expanded).Facility);
        Assert.Equal(ExpansionOutcome.Expands,
            preview.Facilities.Single(row => row.FacilityKey == "facility.refinery").Expansion);
        Assert.Equal(ExpansionOutcome.NotEnoughMaterials,
            preview.Facilities.Single(row => row.FacilityKey == "facility.lumber-mill").Expansion);
    }

    [Fact]
    public void ExpansionPreviewIncludesOutputProducedBeforeConstruction()
    {
        var package = Compile();
        var state = NewState(package);
        Assert.True(state.TryConsumeAvailable(Blue, Commodity(package, "commodity.lumber"), 10));
        var mill = Facility(package, "facility.lumber-mill");
        var draft = new OrdersDraft(state.Definition, Blue);
        draft.SetProduction(Recipe(package, "recipe.lumber"), 2);
        draft.SetExpansion(mill, true);

        var preview = OrdersPreview.Create(package, Scenario, state, draft).Industry;
        var expanded = TurnResolver.Resolve(state, draft.BuildTurnOrders(), 1).Events
            .OfType<FacilityExpandedEvent>().Where(e => e.Country == Blue).ToArray();

        Assert.Equal(mill, Assert.Single(expanded).Facility);
        Assert.Equal(ExpansionOutcome.Expands,
            preview.Facilities.Single(row => row.FacilityKey == "facility.lumber-mill").Expansion);
    }

    [Fact]
    public void RailyardPreviewIncludesOutputProducedBeforeConstruction()
    {
        var package = Compile();
        var state = NewState(package);
        Assert.True(state.TryConsumeAvailable(Blue, Commodity(package, "commodity.lumber"), 10));
        var draft = new OrdersDraft(state.Definition, Blue);
        draft.SetProduction(Recipe(package, "recipe.lumber"), 2);
        draft.BuildTransportCapacity = 2;

        var preview = OrdersPreview.Create(package, Scenario, state, draft).Industry;
        var built = TurnResolver.Resolve(state, draft.BuildTurnOrders(), 1).Events
            .OfType<TransportCapacityBuiltEvent>().Single(e => e.Country == Blue);

        Assert.Equal(2, built.ToCapacity - built.FromCapacity);
        Assert.Equal(built.ToCapacity - built.FromCapacity, preview.Railyard!.ExpectedPoints);
    }

    [Fact]
    public void RecruitmentPreviewIncludesOutputProducedBeforeMigration()
    {
        var package = Compile();
        var state = NewState(package);
        Assert.True(state.TryConsumeAvailable(Blue, Commodity(package, "commodity.canned-food"), 5));
        var draft = new OrdersDraft(state.Definition, Blue);
        draft.SetProduction(Recipe(package, "recipe.canned-food"), 1);
        draft.RecruitWorkers = 1;

        var preview = OrdersPreview.Create(package, Scenario, state, draft).Industry;
        var recruited = TurnResolver.Resolve(state, draft.BuildTurnOrders(), 1).Events
            .OfType<WorkersRecruitedEvent>().Single(e => e.Country == Blue);

        Assert.Equal(1, recruited.Recruited);
        Assert.Equal(recruited.Recruited, preview.Recruitment!.ExpectedRecruits);
    }
}
