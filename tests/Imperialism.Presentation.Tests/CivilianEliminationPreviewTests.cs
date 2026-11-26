using System.Reflection;
using Imperialism.Core;
using Imperialism.Presentation;
using Xunit;
using static Imperialism.Presentation.Tests.UnitOrdersFixture;

namespace Imperialism.Presentation.Tests;

public sealed class CivilianEliminationPreviewTests
{
    [Theory]
    [InlineData(false, 0)]
    [InlineData(false, 1)]
    [InlineData(false, 2)]
    [InlineData(true, 0)]
    [InlineData(true, 1)]
    [InlineData(true, 2)]
    public void PreviewClaimsOnlyTheCivilianCashThatCoreActuallySpends(bool eliminated, int kind)
    {
        var package = Compile();
        var state = NewState(package);
        // Arrange the persistent turn-open elimination flag. Elimination keeps
        // civilians and province ownership; no other eliminated-state field is
        // relevant to these civilian orders. Resolve below uses the real planner.
        if (eliminated)
        {
            var mark = typeof(WorldState).GetMethod("MarkEliminated", BindingFlags.Instance | BindingFlags.NonPublic)!;
            Assert.True((bool)mark.Invoke(state, [Blue, 0])!);
            state.SetCountryCapital(Blue, null);
        }

        var draft = NewDraft(state);
        var unit = kind == 0 ? Farmer : EngineerOne;
        if (kind == 0) draft.SetWork(unit, Cell(1));
        else draft.SetEngineer(unit, Cell(1), kind == 1 ? EngineerConstruction.Depot : EngineerConstruction.Fort);
        var beforeCash = state.GetCash(Blue);
        var preview = OrdersPreview.Create(package, Scenario, state, draft).Civilians;
        var order = OrdersPreview.CreateUnitOrders(package, Scenario, state, draft, unit).Targets.Single(t => t.IsDrafted);
        Assert.Equal(kind == 0 ? 100L : kind == 1 ? 500L : 2000L, order.CashCost);

        var events = TurnResolver.Resolve(state, draft.BuildTurnOrders(), 1).Events;
        if (eliminated)
        {
            Assert.Equal(Blue, Assert.Single(events.OfType<EliminatedCountryOrdersIgnoredEvent>()).Country);
            Assert.False(state.GetCivilian(unit)!.IsBusy);
            Assert.Empty(events.OfType<CivilianWorkBegunEvent>());
            Assert.Empty(events.OfType<ConstructionBegunEvent>());
            Assert.Equal(beforeCash, state.GetCash(Blue));
        }
        else
        {
            Assert.Empty(events.OfType<EliminatedCountryOrdersIgnoredEvent>());
            Assert.True(state.GetCivilian(unit)!.IsBusy);
            Assert.Equal(order.CashCost, beforeCash - state.GetCash(Blue));
        }

        // Expected value comes from the resolved treasury, not the preview.
        Assert.Equal(beforeCash - state.GetCash(Blue), preview.CommittedCost);
    }
}
