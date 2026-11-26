using Imperialism.Core;
using Imperialism.Presentation;
using Xunit;
using static Imperialism.Presentation.Tests.UnitOrdersFixture;

namespace Imperialism.Presentation.Tests;

/// <summary>
/// Row 4d-1: civilian orders in the draft, and a preview that names Core's
/// refusal before the turn resolves. Every preview claim here is held against the
/// events of a real resolved turn, never against the preview's own arithmetic.
/// </summary>
public sealed class UnitOrdersTests
{
    [Fact]
    public void BuildMapsEachUnitOrderOneToOneInUnitIdOrder()
    {
        var state = NewState(Compile());
        var draft = NewDraft(state);
        draft.SetEngineer(EngineerTwo, Cell(6), EngineerConstruction.Port);
        draft.SetWork(Miner, Cell(2));
        draft.SetDeploy(Prospector, Cell(5));
        draft.SetWork(Farmer, Cell(1));
        draft.SetEngineer(EngineerOne, Cell(0), EngineerConstruction.Rail);

        var orders = draft.Build();

        Assert.Equal([new CivilianDeployOrder(Prospector, Cell(5))], orders.Deployments);
        Assert.Equal(
            [new CivilianWorkOrder(Farmer, Cell(1)), new CivilianWorkOrder(Miner, Cell(2))],
            orders.CivilianWork);
        Assert.Equal(
            [
                new EngineerOrder(EngineerOne, Cell(0), EngineerConstruction.Rail),
                new EngineerOrder(EngineerTwo, Cell(6), EngineerConstruction.Port),
            ],
            orders.EngineerWork);
        Assert.False(draft.IsEmpty);
    }

    [Fact]
    public void ANewOrderReplacesTheOldAndClearingLeavesTheDraftEmpty()
    {
        var state = NewState(Compile());
        var draft = NewDraft(state);
        draft.SetWork(Farmer, Cell(1));
        draft.SetDeploy(Farmer, Cell(5));

        var orders = draft.Build();
        Assert.Empty(orders.CivilianWork);
        Assert.Equal([new CivilianDeployOrder(Farmer, Cell(5))], orders.Deployments);
        Assert.Equal(new DraftUnitOrder(Farmer, UnitOrderKind.Deploy, Cell(5), null), draft.GetUnitOrder(Farmer));

        draft.SetEngineer(EngineerOne, Cell(1), EngineerConstruction.Depot);
        draft.SetEngineer(EngineerOne, Cell(1), EngineerConstruction.Fort);
        Assert.Equal(
            [new EngineerOrder(EngineerOne, Cell(1), EngineerConstruction.Fort)], draft.Build().EngineerWork);

        Assert.True(draft.ClearUnitOrder(Farmer));
        Assert.True(draft.ClearUnitOrder(EngineerOne));
        Assert.False(draft.ClearUnitOrder(EngineerOne));
        Assert.True(draft.IsEmpty);
        Assert.Throws<ArgumentOutOfRangeException>(() => draft.SetDeploy(Farmer, Cell(12)));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => draft.SetEngineer(EngineerOne, Cell(1), (EngineerConstruction)99));
    }

    [Fact]
    public void UnitOrdersAreClearedAtEndTurnAndAJobUnderWayCarriesOnWithoutThem()
    {
        // Manual 1206–1214 and 1065–1069: a unit is ordered afresh each turn, and one
        // still working is not in the cycle at all.
        var state = NewState(Compile());
        var draft = NewDraft(state);
        draft.SetWork(Farmer, Cell(1));
        draft.SetDeploy(Miner, Cell(5));
        TurnResolver.Resolve(state, draft.BuildTurnOrders(), 1);

        draft.AdvanceTurn();

        Assert.Empty(draft.UnitOrders);
        Assert.True(draft.IsEmpty);
        Assert.True(state.GetCivilian(Farmer)!.IsBusy);
    }

    [Fact]
    public void AnUntouchedDraftResolvesExactlyLikeEmptyOrders()
    {
        var package = Compile();
        var before = NewState(package);
        var after = NewState(package);
        var expected = TurnReportView.Create(
            package, Scenario, before, TurnResolver.Resolve(before, TurnOrders.Empty(2), 1));
        var actual = TurnReportView.Create(
            package, Scenario, after, TurnResolver.Resolve(after, NewDraft(after).BuildTurnOrders(), 1));

        Assert.Equal(Lines(expected), Lines(actual));
        Assert.Equal(before.GetCash(Blue), after.GetCash(Blue));
    }

    [Fact]
    public void TheCivilianListShowsTypeCellAndBusy()
    {
        var package = Compile();
        var state = NewState(package);
        var draft = NewDraft(state);
        draft.SetWork(Farmer, Cell(1));
        TurnResolver.Resolve(state, draft.BuildTurnOrders(), 1);
        draft.AdvanceTurn();

        var civilians = OrdersPreview.Create(package, Scenario, state, draft).Civilians.Civilians;

        Assert.Equal([EngineerOne, EngineerTwo, Farmer, Miner, Prospector], civilians.Select(c => c.Unit));
        var farmer = civilians.Single(c => c.Unit == Farmer);
        Assert.Equal(("Farmer", CivilianWorkKind.Improve, Cell(1), true, (int?)3), (farmer.TypeName, farmer.Work, farmer.Cell, farmer.IsBusy, farmer.TurnsRemaining));
        Assert.Equal(("Engineer", Cell(6), false), (civilians[1].TypeName, civilians[1].Cell, civilians[1].IsBusy));
        Assert.Null(civilians[1].Order);
    }

    // ---- Each verb, accepted and refused, matched to the resolved turn ----

    [Fact]
    public void DeployAcceptedAndRefused()
    {
        AssertAgrees(draft => draft.SetDeploy(Farmer, Cell(5)), Farmer, UnitOrderVerb.Deploy, 0, null);
        AssertAgrees(draft => draft.SetDeploy(Farmer, Cell(8)), Farmer, UnitOrderVerb.Deploy, 0,
            CivilianOrderRefusal.TargetNotYourTerritory);
    }

    [Fact]
    public void ImproveAcceptedAndRefused()
    {
        AssertAgrees(draft => draft.SetWork(Farmer, Cell(1)), Farmer, UnitOrderVerb.Improve, 100, null);
        AssertAgrees(draft => draft.SetWork(Farmer, Cell(0)), Farmer, UnitOrderVerb.Improve, 1000,
            CivilianOrderRefusal.ImprovementTechnologyNotKnown);
        AssertAgrees(draft => draft.SetWork(Miner, Cell(2)), Miner, UnitOrderVerb.Improve, 100,
            CivilianOrderRefusal.DepositNotYetDiscovered);
    }

    [Fact]
    public void ProspectAcceptedAndRefused()
    {
        AssertAgrees(draft => draft.SetWork(Prospector, Cell(2)), Prospector, UnitOrderVerb.Prospect, 0, null);
        AssertAgrees(draft => draft.SetWork(Prospector, Cell(4)), Prospector, UnitOrderVerb.Prospect, 0,
            CivilianOrderRefusal.ProspectingTechnologyNotKnown);
        AssertAgrees(draft => draft.SetWork(Prospector, Cell(5)), Prospector, UnitOrderVerb.Prospect, 0,
            CivilianOrderRefusal.TerrainCannotBeProspected);
    }

    [Fact]
    public void RailAcceptedAndRefused()
    {
        AssertAgrees(draft => draft.SetEngineer(EngineerOne, Cell(0), EngineerConstruction.Rail),
            EngineerOne, UnitOrderVerb.Rail, 100, null);
        AssertAgrees(draft => draft.SetEngineer(EngineerOne, Cell(2), EngineerConstruction.Rail),
            EngineerOne, UnitOrderVerb.Rail, 200, CivilianOrderRefusal.ConstructionTechnologyNotKnown);
        AssertAgrees(draft => draft.SetEngineer(EngineerOne, Cell(10), EngineerConstruction.Rail),
            EngineerOne, UnitOrderVerb.Rail, 100, CivilianOrderRefusal.TargetNotYourTerritory);
    }

    [Fact]
    public void DepotAcceptedAndRefused()
    {
        AssertAgrees(draft => draft.SetEngineer(EngineerOne, Cell(1), EngineerConstruction.Depot),
            EngineerOne, UnitOrderVerb.Depot, 500, null);
        AssertAgrees(draft => draft.SetEngineer(EngineerTwo, Cell(6), EngineerConstruction.Depot),
            EngineerTwo, UnitOrderVerb.Depot, 500, CivilianOrderRefusal.ConstructionTechnologyNotKnown);
    }

    [Fact]
    public void PortAcceptedAndRefused()
    {
        AssertAgrees(draft => draft.SetEngineer(EngineerTwo, Cell(6), EngineerConstruction.Port),
            EngineerTwo, UnitOrderVerb.Port, 1000, null);
        AssertAgrees(draft => draft.SetEngineer(EngineerOne, Cell(1), EngineerConstruction.Port),
            EngineerOne, UnitOrderVerb.Port, 1000, CivilianOrderRefusal.PortNeedsWater);
    }

    [Fact]
    public void FortAcceptedAndRefused()
    {
        AssertAgrees(draft => draft.SetEngineer(EngineerOne, Cell(1), EngineerConstruction.Fort),
            EngineerOne, UnitOrderVerb.Fort, 2000, null);
        AssertAgrees(draft => draft.SetEngineer(EngineerOne, Cell(0), EngineerConstruction.Fort),
            EngineerOne, UnitOrderVerb.Fort, 2000, CivilianOrderRefusal.StructureNeedsTheEngineersOwnTile);
    }

    [Fact]
    public void AFortCountsAgainstTheProvinceCapForTheNextEngineer()
    {
        var package = Compile(blueCash: 10_000);
        var state = NewState(package);
        state.SetFortLevel(state.Definition.Map[Cell(1)].Region.Province, 2);
        var draft = NewDraft(state);
        draft.SetEngineer(EngineerOne, Cell(1), EngineerConstruction.Fort);
        draft.SetEngineer(EngineerTwo, Cell(6), EngineerConstruction.Fort);

        var outcomes = AssertPreviewMatchesTurn(package, state, draft);

        Assert.Null(outcomes[EngineerOne].Warning);
        Assert.Equal(CivilianOrderRefusal.FortAtMaximum, outcomes[EngineerTwo].Warning);
    }

    // ---- Cash, in Core's spending order ----

    [Fact]
    public void WorkOrdersAreChargedBeforeEngineersWhateverTheUnitIds()
    {
        // 3,000: the Farmer's 100 (unit 3) is charged before Engineer 1's fort
        // (2,000), and Engineer 2's port (1,000) is then short by 100. Charged in
        // plain unit-id order the port would be paid and the Farmer refused.
        var package = Compile(blueCash: 3000);
        var state = NewState(package);
        var draft = NewDraft(state);
        draft.SetWork(Farmer, Cell(1));
        draft.SetEngineer(EngineerOne, Cell(1), EngineerConstruction.Fort);
        draft.SetEngineer(EngineerTwo, Cell(6), EngineerConstruction.Port);

        var outcomes = AssertPreviewMatchesTurn(package, state, draft);

        Assert.Null(outcomes[Farmer].Warning);
        Assert.Null(outcomes[EngineerOne].Warning);
        Assert.Equal(CivilianOrderRefusal.NotEnoughCash, outcomes[EngineerTwo].Warning);
        var civilians = OrdersPreview.Create(package, Scenario, NewState(package), draft).Civilians;
        Assert.Equal(2100, civilians.CommittedCost);

        var view = OrdersPreview.CreateUnitOrders(package, Scenario, NewState(package), draft, EngineerTwo);
        Assert.Equal(900, view.CashBeforeOrder);
        Assert.Equal(CivilianOrderRefusal.NotEnoughCash,
            view.Targets.Single(t => t.Verb == UnitOrderVerb.Port).Warning);
        Assert.True(view.Targets.Single(t => t.Verb == UnitOrderVerb.Port).IsDrafted);
    }

    [Fact]
    public void BidsClaimCashBeforeCiviliansAndCiviliansBeforeResearch()
    {
        var package = Compile(blueCash: 1000);
        var state = NewState(package);
        var draft = NewDraft(state);
        var lumber = state.Definition.Commodities.Single(c => package.Catalog.GetKey(c.Id) == "commodity.lumber").Id;
        draft.SetBid(lumber, 3);
        draft.SetEngineer(EngineerOne, Cell(1), EngineerConstruction.Depot);

        var preview = OrdersPreview.Create(package, Scenario, state, draft);

        Assert.Equal(900, preview.Civilians.ClaimedByBids);
        Assert.Equal(100, preview.Civilians.CashBeforeCivilians);
        Assert.Equal(CivilianOrderRefusal.NotEnoughCash,
            preview.Civilians.Civilians.Single(c => c.Unit == EngineerOne).Order!.Warning);

        draft.SetBid(lumber, 0);
        var funded = OrdersPreview.Create(package, Scenario, state, draft);
        Assert.Equal(500, funded.Civilians.CommittedCost);
        Assert.Equal(500, funded.Technology.ClaimedByCivilians);
    }

    [Fact]
    public void AJobFinishingThisTurnIsSeenBeforeTheNewOrders()
    {
        // The Prospector searches the hills in one turn, so on turn 2 the coal under
        // them is visible to the Miner before the Miner's order is read.
        var package = Compile();
        var state = NewState(package);
        var draft = NewDraft(state);
        draft.SetWork(Prospector, Cell(2));
        TurnResolver.Resolve(state, draft.BuildTurnOrders(), 1);
        draft.AdvanceTurn();
        Assert.True(state.GetCivilian(Prospector)!.IsBusy);

        var view = OrdersPreview.CreateUnitOrders(package, Scenario, state, draft, Miner);
        Assert.Null(view.Evaluate(Cell(2), UnitOrderVerb.Improve).Warning);

        draft.SetWork(Miner, Cell(2));
        draft.SetWork(Prospector, Cell(1));
        var outcomes = AssertPreviewMatchesTurn(package, state, draft, turn: 2);
        Assert.Null(outcomes[Miner].Warning);
        Assert.Equal(CivilianOrderRefusal.AlreadyWorking, outcomes[Prospector].Warning);
    }

    [Fact]
    public void TheTargetListCoversEveryOwnLandCellAndTheEngineersSites()
    {
        var package = Compile();
        var state = NewState(package);
        var draft = NewDraft(state);

        var farmer = OrdersPreview.CreateUnitOrders(package, Scenario, state, draft, Farmer);
        Assert.Equal([0, 1, 2, 4, 5, 6],
            farmer.Targets.Where(t => t.Verb == UnitOrderVerb.Deploy).Select(t => t.Cell.Value));
        Assert.Equal([0, 1, 2, 4, 5, 6],
            farmer.Targets.Where(t => t.Verb == UnitOrderVerb.Improve).Select(t => t.Cell.Value));
        Assert.Equal([1], farmer.Targets.Where(t => t.IsAccepted && t.Verb == UnitOrderVerb.Improve).Select(t => t.Cell.Value));

        var engineer = OrdersPreview.CreateUnitOrders(package, Scenario, state, draft, EngineerOne);
        Assert.Equal([0, 2, 4, 5],
            engineer.Targets.Where(t => t.Verb == UnitOrderVerb.Rail).Select(t => t.Cell.Value).Order());
        Assert.Equal([UnitOrderVerb.Depot, UnitOrderVerb.Port, UnitOrderVerb.Fort],
            engineer.Targets.Where(t => t.Cell == Cell(1) && t.Verb != UnitOrderVerb.Deploy).Select(t => t.Verb));
        Assert.DoesNotContain(engineer.Targets, t => t.Verb is UnitOrderVerb.Improve or UnitOrderVerb.Prospect);

        Assert.Throws<ArgumentException>(() => OrdersPreview.CreateUnitOrders(package, Scenario, state, draft, RedFarmer));
    }

    [Fact]
    public void EveryListedTargetAgreesWithTheTurnItWouldResolve()
    {
        // Exhaustive: for each Blue civilian, each listed target is drafted alone
        // and resolved, and the warning must be the turn's verdict.
        var package = Compile();
        foreach (var unit in new[] { EngineerOne, EngineerTwo, Farmer, Miner, Prospector })
        {
            var listed = OrdersPreview.CreateUnitOrders(
                package, Scenario, NewState(package), NewDraft(NewState(package)), unit).Targets;
            foreach (var target in listed)
            {
                var state = NewState(package);
                var draft = NewDraft(state);
                Draft(draft, unit, target.Verb, target.Cell);
                var outcomes = AssertPreviewMatchesTurn(package, state, draft);
                Assert.Equal(target.Warning, outcomes[unit].Warning);
            }
        }
    }

    private static void Draft(OrdersDraft draft, CivilianUnitId unit, UnitOrderVerb verb, CellIndex cell)
    {
        switch (verb)
        {
            case UnitOrderVerb.Deploy: draft.SetDeploy(unit, cell); break;
            case UnitOrderVerb.Improve or UnitOrderVerb.Prospect: draft.SetWork(unit, cell); break;
            case UnitOrderVerb.Rail: draft.SetEngineer(unit, cell, EngineerConstruction.Rail); break;
            case UnitOrderVerb.Depot: draft.SetEngineer(unit, cell, EngineerConstruction.Depot); break;
            case UnitOrderVerb.Port: draft.SetEngineer(unit, cell, EngineerConstruction.Port); break;
            default: draft.SetEngineer(unit, cell, EngineerConstruction.Fort); break;
        }
    }

    private static void AssertAgrees(
        Action<OrdersDraft> order,
        CivilianUnitId unit,
        UnitOrderVerb verb,
        long cost,
        CivilianOrderRefusal? warning)
    {
        var package = Compile();
        var state = NewState(package);
        var draft = NewDraft(state);
        order(draft);

        var listed = OrdersPreview.CreateUnitOrders(package, Scenario, state, draft, unit).Targets
            .SingleOrDefault(t => t.IsDrafted);
        var outcomes = AssertPreviewMatchesTurn(package, state, draft);

        var outcome = outcomes[unit];
        Assert.Equal((verb, cost, warning, true), (outcome.Verb, outcome.CashCost, outcome.Warning, outcome.IsDrafted));
        if (listed is not null)
        {
            Assert.Equal(outcome, listed);
        }
    }

    /// <summary>
    /// Previews the draft, resolves the turn on <paramref name="state"/>, and holds
    /// every drafted order's preview to that unit's event.
    /// </summary>
    private static Dictionary<CivilianUnitId, UnitTargetView> AssertPreviewMatchesTurn(
        Imperialism.Content.CompiledWorldPackage package, WorldState state, OrdersDraft draft, int turn = 1)
    {
        var preview = OrdersPreview.Create(package, Scenario, state, draft).Civilians;
        var outcomes = preview.Civilians
            .Where(c => c.Order is not null)
            .ToDictionary(c => c.Unit, c => c.Order!);
        foreach (var (unit, order) in preview.OtherOrders)
        {
            outcomes[unit] = order;
        }

        Assert.Equal(draft.UnitOrders.Count, outcomes.Count);
        var cash = state.GetCash(Blue);
        var events = TurnResolver.Resolve(state, draft.BuildTurnOrders(), (ulong)turn).Events
            .Where(e => e.Phase == TurnPhase.Development)
            .ToArray();
        foreach (var (unit, outcome) in outcomes)
        {
            if (outcome.Warning is { } reason)
            {
                var refused = Assert.Single(events.OfType<CivilianOrderRefusedEvent>(), e => e.Unit == unit);
                Assert.Equal(reason, refused.Reason);
                continue;
            }

            Assert.DoesNotContain(events.OfType<CivilianOrderRefusedEvent>(), e => e.Unit == unit);
            switch (outcome.Verb)
            {
                case UnitOrderVerb.Deploy:
                    Assert.Equal(outcome.Cell, Assert.Single(events.OfType<CivilianDeployedEvent>(), e => e.Unit == unit).To);
                    break;
                case UnitOrderVerb.Improve or UnitOrderVerb.Prospect:
                    var begun = Assert.Single(events.OfType<CivilianWorkBegunEvent>(), e => e.Unit == unit);
                    Assert.Equal((outcome.Cell, outcome.CashCost), (begun.Cell, begun.Paid));
                    break;
                default:
                    var built = Assert.Single(events.OfType<ConstructionBegunEvent>(), e => e.Unit == unit);
                    Assert.Equal((outcome.Cell, outcome.CashCost), (built.Target, built.Paid));
                    break;
            }
        }

        // Nothing else in this fixture moves Blue's cash, so the whole turn's spend is
        // the civilians'.
        Assert.Equal(preview.CommittedCost, cash - state.GetCash(Blue));
        return outcomes;
    }

    private static string[] Lines(TurnReportView report) =>
        report.Phases.SelectMany(static phase => phase.Lines.Select(line => $"{phase.Phase}:{line}")).ToArray();
}
