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

    /// <summary>One category as the screen and the smoke print it; the words are Presentation's.</summary>
    public static string Describe(ArmouryCategoryView row) => MilitaryText.Describe(row);

    /// <summary>One hull class as the screen prints it.</summary>
    public static string Describe(ShipyardRowView hull) => MilitaryText.Describe(hull);
}
