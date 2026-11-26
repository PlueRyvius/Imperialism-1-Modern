using System.Globalization;
using Imperialism.Core;

namespace Imperialism.Presentation;

/// <summary>
/// The words the Armoury, Shipyard and Fleet screens print: category and mission
/// names, warnings and each row's line.
/// </summary>
/// <remarks>
/// The client never composes game text, and before this a screen printed Core's enum
/// names as they stood ("LightInfantry", "NotEnoughCommodities"). Every warning here
/// reuses the clause <see cref="TurnReportText"/> gives the same refusal, so the
/// warning before End Turn and the report after it say the same thing.
/// </remarks>
public static class MilitaryText
{
    public const string AcceptedByPreview = "Accepted by preview";

    public const string SetAside = "Set aside: country eliminated";

    /// <summary>An Armoury category as a title: "Light Infantry".</summary>
    public static string CategoryName(ArmyCategory category) => category switch
    {
        ArmyCategory.Militia => "Militia",
        ArmyCategory.LightInfantry => "Light Infantry",
        ArmyCategory.RegularInfantry => "Regular Infantry",
        ArmyCategory.HeavyInfantry => "Heavy Infantry",
        ArmyCategory.LightCavalry => "Light Cavalry",
        ArmyCategory.HeavyCavalry => "Heavy Cavalry",
        ArmyCategory.LightArtillery => "Light Artillery",
        ArmyCategory.HeavyArtillery => "Heavy Artillery",
        ArmyCategory.CombatEngineers => "Combat Engineers",
        ArmyCategory.General => "Generals",
        _ => throw new ArgumentOutOfRangeException(nameof(category)),
    };

    /// <summary>What a task force is doing as the turn opens.</summary>
    public static string ActivityName(TaskForceActivity activity) => activity switch
    {
        TaskForceActivity.Idle => "Idle",
        TaskForceActivity.Patrolling => "Patrolling",
        TaskForceActivity.Blockading => "Blockading",
        TaskForceActivity.Landing => "Holding a landing site",
        _ => throw new ArgumentOutOfRangeException(nameof(activity)),
    };

    /// <summary>A drafted naval mission.</summary>
    public static string MissionName(NavalMissionKind mission) => mission switch
    {
        NavalMissionKind.None => "No mission",
        NavalMissionKind.Patrol => "Patrol",
        NavalMissionKind.Blockade => "Blockade",
        NavalMissionKind.Landing => "Landing site",
        NavalMissionKind.Move => "Move",
        _ => throw new ArgumentOutOfRangeException(nameof(mission)),
    };

    /// <summary>
    /// Why a category cannot be bought at all, as a sentence. Generals are the one
    /// category the Armoury never sells, so their sentence says that rather than the
    /// generic clause.
    /// </summary>
    public static string Unavailable(ArmyCategory category, RegimentPurchaseRefusal reason) =>
        category == ArmyCategory.General && reason == RegimentPurchaseRefusal.NotPurchasable
            ? "Generals are not bought here."
            : Sentence(TurnReportText.Describe(reason));

    public static string Result(OrderWarning<RegimentPurchaseRefusal>? warning, ArmyCategory category) =>
        warning is { Refusal: { } refusal, OrdersSetAside: false }
            ? "Warning: " + Unavailable(category, refusal)
            : Result(warning, static _ => string.Empty);

    public static string Result(OrderWarning<ShipBuildRefusal>? warning) =>
        Result(warning, TurnReportText.Describe);

    public static string Result(OrderWarning<NavalOrderRefusal>? warning) =>
        Result(warning, TurnReportText.Describe);

    /// <summary>One Armoury category as the screen prints it.</summary>
    public static string Describe(ArmouryCategoryView row)
    {
        var name = CategoryName(row.Category);
        var type = row.TypeName is null
            ? (row.Category == ArmyCategory.General ? string.Empty : " · builds nothing yet")
            : $" · {row.TypeName} · {Worker(row.WorkerGrade)} · {Money(row.CashCost)} · {Bill(row.Bill)} · " +
              $"Arms {Plural.Number(row.Arms)}";
        var unavailable = row.Unavailable is { } reason
            ? $"\nNot available: {Unavailable(row.Category, reason)}"
            : string.Empty;
        var drafted = row.Drafted > 0
            ? $"\nDrafted: {Plural.Count(row.Drafted, "regiment")} · {Result(row.Warning, row.Category)} · " +
              $"takes {Workers(row.WorkersTaken, row.WorkerGrade)}"
            : string.Empty;
        return $"{name}{type}{unavailable}{drafted}";
    }

    /// <summary>One hull class as the Shipyard prints it.</summary>
    public static string Describe(ShipyardRowView hull)
    {
        var kind = hull.IsWarship ? "Warship" : "Merchant";
        var gate = hull.TechnologyKey is { } key
            ? $" · Needs {hull.TechnologyName ?? key}{(hull.IsGated ? " (not known)" : " (known)")}"
            : string.Empty;
        var drafted = hull.Drafted > 0
            ? $"\nDrafted: {Plural.Count(hull.Drafted, "hull")} · {Result(hull.Warning)}"
            : string.Empty;
        return $"{hull.Name} · {kind} · Cargo {Plural.Count(hull.Cargo, "hold")} · " +
            $"{Plural.Count(hull.SeaZones, "sea zone")} a turn · {Bill(hull.Bill)}{gate}{drafted}";
    }

    /// <summary>A fleet's second line on the Fleet screen.</summary>
    public static string Describe(FleetRowView fleet, string seaZoneName) =>
        $"{Plural.Count(fleet.Count, "hull")} · {seaZoneName} · " +
        $"{(fleet.TaskForce is { } force ? $"Task force {force.Value}" : "Anchored, no mission")} · " +
        $"{Plural.Count(fleet.Holds, "hold")} · Reach {Plural.Count(fleet.Reach, "sea zone")}";

    /// <summary>A task force on the Fleet screen; <paramref name="target"/> names its blockade or landing.</summary>
    public static string Describe(TaskForceRowView force, string seaZoneName, string? target) =>
        $"Task force {force.Id.Value} · {seaZoneName} · {ActivityName(force.Activity)}" +
        (target is null ? string.Empty : $" · {target}") + "\n" +
        $"{Plural.Count(force.Ships, "hull")} · {Plural.Count(force.Holds, "hold")} · " +
        $"Reach {Plural.Count(force.Reach, "sea zone")}";

    /// <summary>A drafted naval order on the Fleet screen.</summary>
    public static string Describe(DraftedNavalOrderView order)
    {
        var subject = order.Kind == DraftNavalOrderKind.Assemble
            ? $"Assemble {Plural.Noun(order.Fleets.Count, "fleet")} {string.Join(", ", order.Fleets.Select(id => id.Value))}"
            : $"Task force {order.TaskForce!.Value.Value}";
        var result = order.Warning is null && !order.HasEffect
            ? "Accepted: no effect or event"
            : Result(order.Warning);
        var reach = order.Reach is { } zones ? $" · Reach {Plural.Count(zones, "sea zone")}" : string.Empty;
        var distance = order.Mission == NavalMissionKind.Move
            ? $" · Distance {(order.Distance is { } d ? Plural.Count(d, "sea zone") : "no sea path")}"
            : string.Empty;
        var landing = order.LandingUsableFromTurn is { } turn ? $" · Landing usable from turn {turn}" : string.Empty;
        return $"{subject} · {MissionName(order.Mission)}{reach}{distance}\n{result}{landing}" +
            (order.RestampsLandingSite ? " · Resets the held landing site" : string.Empty);
    }

    private static string Result<TRefusal>(OrderWarning<TRefusal>? warning, Func<TRefusal, string> clause)
        where TRefusal : struct, Enum => warning switch
        {
            null => AcceptedByPreview,
            { OrdersSetAside: true } => SetAside,
            { Refusal: { } refusal } => "Warning: " + Sentence(clause(refusal)),
            _ => "Warning",
        };

    private static string Worker(WorkerGrade? grade) =>
        grade is { } value ? $"1 {TurnReportText.Describe(value)} worker" : "no worker";

    private static string Workers(long count, WorkerGrade? grade) =>
        grade is { } value && count > 0
            ? $"{Plural.Number(count)} {TurnReportText.Describe(value)} {Plural.Noun(count, "worker")}"
            : "no workers";

    private static string Bill(IReadOnlyList<CommodityAmountView> bill) => bill.Count == 0
        ? "No bill"
        : string.Join(", ", bill.Select(item => Plural.Quantity(item.Quantity, item.CommodityName)));

    private static string Money(long amount) => "$" + amount.ToString("N0", CultureInfo.InvariantCulture);

    private static string Sentence(string clause) => char.ToUpperInvariant(clause[0]) + clause[1..] + ".";
}
