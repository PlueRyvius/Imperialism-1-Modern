using Imperialism.Core;
using Imperialism.Presentation;

namespace Imperialism.Client;

/// <summary>Managed half of the fleet smoke flag; uses the same draft and preview as the screen.</summary>
public static class FleetSmoke
{
    public static DraftedNavalOrderView? DraftAcceptedOrder(GameSession session)
    {
        var view = session.Preview().Fleet;
        foreach (var force in view.TaskForces)
        {
            session.Draft.SetNavalCommand(force.Id, NavalMission.Patrol);
            var order = session.Preview().Fleet.Orders.Last();
            if (order.Warning is null && order.HasEffect) { return order; }
            session.Draft.ClearNavalCommand(force.Id);
            _ = session.Preview();
        }

        foreach (var fleet in view.Fleets)
        {
            session.Draft.SetNavalAssembly([fleet.Id], NavalMission.Patrol);
            var order = session.Preview().Fleet.Orders.Last();
            if (order.Warning is null && order.HasEffect) { return order; }
            session.Draft.ClearNavalAssembly(fleet.Id);
            _ = session.Preview();
        }

        return null;
    }

    public static string Describe(DraftedNavalOrderView order)
    {
        var subject = order.Kind == DraftNavalOrderKind.Assemble
            ? $"Assemble fleets {string.Join(", ", order.Fleets.Select(id => id.Value))}"
            : $"Task force {order.TaskForce!.Value.Value}";
        var result = order.Warning is { } warning
            ? warning.OrdersSetAside ? "Set aside: country eliminated" : $"Warning: {warning.Refusal}"
            : order.HasEffect ? "Accepted by preview" : "Accepted: no effect or event";
        var reach = order.Reach is { } zones ? $" · Reach {zones}" : string.Empty;
        var distance = order.Mission == NavalMissionKind.Move
            ? $" · Distance {order.Distance?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "no sea path"}" : string.Empty;
        var landing = order.LandingUsableFromTurn is { } turn ? $" · Landing usable from turn {turn}" : string.Empty;
        return $"{subject} · {order.Mission}{reach}{distance}\n{result}{landing}" +
            (order.RestampsLandingSite ? " · Resets the held landing site" : string.Empty);
    }
}
