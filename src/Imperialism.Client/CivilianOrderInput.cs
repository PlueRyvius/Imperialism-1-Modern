using Imperialism.Core;
using Imperialism.Presentation;

namespace Imperialism.Client;

/// <summary>Maps preview verbs to draft input; never decides legality or price.</summary>
internal static class CivilianOrderInput
{
    internal static void DraftCivilianOrder(OrdersDraft draft, CivilianUnitId unit, UnitTargetView target)
    {
        switch (target.Verb)
        {
            case UnitOrderVerb.Deploy:
                draft.SetDeploy(unit, target.Cell);
                break;
            case UnitOrderVerb.Improve:
            case UnitOrderVerb.Prospect:
                draft.SetWork(unit, target.Cell);
                break;
            default:
                draft.SetEngineer(unit, target.Cell, target.Verb switch
                {
                    UnitOrderVerb.Rail => EngineerConstruction.Rail,
                    UnitOrderVerb.Depot => EngineerConstruction.Depot,
                    UnitOrderVerb.Port => EngineerConstruction.Port,
                    UnitOrderVerb.Fort => EngineerConstruction.Fort,
                    _ => throw new ArgumentOutOfRangeException(nameof(target)),
                });
                break;
        }
    }

    internal static string DescribeTarget(UnitTargetView target) =>
        $"{target.Verb} · ${target.CashCost:N0}\n" +
        (target.Warning is { } warning ? TurnReportText.Describe(warning) : "No warning.");
}
