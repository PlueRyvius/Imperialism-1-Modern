using Imperialism.Core;
using Imperialism.Presentation;

namespace Imperialism.Client;

/// <summary>Managed half of the Armoury smoke flag; uses the same draft and preview as the screen.</summary>
public static class ArmourySmoke
{
    /// <summary>
    /// Drafts one regiment of the first category the preview accepts, leaving every
    /// rejected try cleared. Null when no category is purchasable.
    /// </summary>
    public static (ArmouryCategoryView Row, ArmouryView Armoury)? DraftAcceptedPurchase(GameSession session)
    {
        foreach (var row in session.Preview().Armoury.Categories)
        {
            if (row.Unavailable is not null)
            {
                continue;
            }

            session.Draft.SetRegimentPurchase(row.Category, 1);
            var armoury = session.Preview().Armoury;
            var drafted = armoury.Categories.First(candidate => candidate.Category == row.Category);
            if (drafted.Warning is null && drafted.WorkersTaken > 0)
            {
                return (drafted, armoury);
            }

            session.Draft.SetRegimentPurchase(row.Category, 0);
            _ = session.Preview();
        }

        return null;
    }

    /// <summary>One category as the screen and the smoke print it.</summary>
    public static string Describe(ArmouryCategoryView row)
    {
        var type = row.TypeName is null ? "builds nothing" : row.TypeName;
        var price = row.TypeName is null
            ? string.Empty
            : $" · {Grade(row.WorkerGrade)} · ${row.CashCost} · {Bill(row.Bill)} · Arms {row.Arms}";
        var unavailable = row.Unavailable is { } reason ? $"\nNot available: {reason}" : string.Empty;
        var drafted = row.Drafted > 0
            ? $"\nDrafted {row.Drafted} · {Result(row.Warning)} · Workers taken {row.WorkersTaken}"
            : string.Empty;
        return $"{row.Category} · {type}{price}{unavailable}{drafted}";
    }

    /// <summary>One hull class as the screen prints it.</summary>
    public static string Describe(ShipyardRowView hull)
    {
        var kind = hull.IsWarship ? "Warship" : "Merchant";
        var gate = hull.TechnologyKey is { } key ? $" · Needs {key}{(hull.IsGated ? " (not known)" : " (known)")}" : string.Empty;
        var drafted = hull.Drafted > 0 ? $"\nDrafted {hull.Drafted} · {Result(hull.Warning)}" : string.Empty;
        return $"{hull.Name} · {kind} · Cargo {hull.Cargo} · Sea zones {hull.SeaZones} · {Bill(hull.Bill)}{gate}{drafted}";
    }

    public static string Result<TRefusal>(OrderWarning<TRefusal>? warning)
        where TRefusal : struct, Enum => warning switch
        {
            null => "Accepted by preview",
            { OrdersSetAside: true } => "Set aside: country eliminated",
            { Refusal: { } refusal } => $"Warning: {refusal}",
            _ => "Warning",
        };

    private static string Grade(WorkerGrade? grade) => grade is { } value ? $"1 {value} worker" : "no worker";

    private static string Bill(IReadOnlyList<CommodityAmountView> bill) => bill.Count == 0
        ? "No bill"
        : string.Join(", ", bill.Select(item => $"{item.Quantity} {item.CommodityName}"));
}
