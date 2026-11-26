using Godot;
using Imperialism.Core;
using Imperialism.Presentation;

namespace Imperialism.Client;

/// <summary>Exercises the screen's draft adapter and the real session. Windows Godot gate.</summary>
internal static class UnitOrdersSmoke
{
    public static bool Run(GameSession session)
    {
        var civilian = session.Preview().Civilians.Civilians.FirstOrDefault(unit => !unit.IsBusy);
        if (civilian is null)
        {
            GD.Print("UNITS_SMOKE_FAIL reason=no-idle-civilian (use a scenario with starting civilians)");
            return false;
        }

        var view = OrdersPreview.CreateUnitOrders(session.Package, session.ScenarioKey,
            session.World, session.Draft, civilian.Unit);
        var target = view.Targets.FirstOrDefault(row => row.Warning is null);
        if (target is null)
        {
            GD.Print($"UNITS_SMOKE_FAIL unit={civilian.Unit.Value} reason=no-clean-target");
            return false;
        }

        CivilianOrderInput.DraftCivilianOrder(session.Draft, civilian.Unit, target);
        var preview = session.Preview().Civilians.Civilians.Single(unit => unit.Unit == civilian.Unit);
        GD.Print($"UNITS_SMOKE_DRAFT unit={civilian.Unit.Value} order={session.Draft.GetUnitOrder(civilian.Unit)}");
        GD.Print($"UNITS_SMOKE_PREVIEW unit={civilian.Unit.Value} {CivilianOrderInput.DescribeTarget(preview.Order!)}");
        session.EndTurn();
        var renderer = TurnReportRenderer.Create(session.Package, session.ScenarioKey, session.World);
        var events = session.LastResolution!.Events.Where(item => item switch
        {
            CivilianDeployedEvent deployed => deployed.Unit == civilian.Unit,
            CivilianWorkBegunEvent begun => begun.Unit == civilian.Unit,
            ConstructionBegunEvent begun => begun.Unit == civilian.Unit,
            CivilianOrderRefusedEvent refused => refused.Unit == civilian.Unit,
            _ => false,
        }).ToArray();
        foreach (var item in events)
        {
            GD.Print($"UNITS_SMOKE_EVENT unit={civilian.Unit.Value} {item}");
            foreach (var line in renderer.Render(item))
            {
                GD.Print($"UNITS_SMOKE_RESOLVED {line.Text}");
            }
        }

        var accepted = events.Any(item => item switch
        {
            CivilianDeployedEvent moved => target.Verb == UnitOrderVerb.Deploy && moved.To == target.Cell,
            CivilianWorkBegunEvent begun => target.Verb is UnitOrderVerb.Improve or UnitOrderVerb.Prospect &&
                begun.Cell == target.Cell && begun.Paid == target.CashCost,
            ConstructionBegunEvent begun => begun.Target == target.Cell && begun.Paid == target.CashCost &&
                (target.Verb, begun.Structure) is
                    (UnitOrderVerb.Rail, EngineerConstruction.Rail) or
                    (UnitOrderVerb.Depot, EngineerConstruction.Depot) or
                    (UnitOrderVerb.Port, EngineerConstruction.Port) or
                    (UnitOrderVerb.Fort, EngineerConstruction.Fort),
            _ => false,
        });
        var ok = preview.Order is { Warning: null, IsDrafted: true } &&
            preview.Order.Verb == target.Verb && preview.Order.Cell == target.Cell &&
            session.Draft.GetUnitOrder(civilian.Unit) is null &&
            accepted &&
            events.All(item => item is not CivilianOrderRefusedEvent);
        GD.Print($"UNITS_SMOKE_{(ok ? "OK" : "FAIL")} unit={civilian.Unit.Value} verb={target.Verb} cell={target.Cell.Value} events={events.Length}");
        return ok;
    }
}
