namespace Imperialism.Core;

/// <summary>
/// What it takes to draw rural workers into industry, and how many will come.
/// </summary>
/// <remarks>
/// <para>
/// The manual is explicit about the shape and silent about one number. Recruits
/// arrive **untrained**; the price is "the comforts of a developing economy:
/// canned foods, clothing, and furniture"; and "the size of your country limits
/// the number of workers that migrate during one turn to one-fourth of the
/// number of provinces you own, rounded down".
/// </para>
/// <para>
/// **How much of each commodity per worker is not documented anywhere.** The
/// catalogue's one-of-each is a guess and is recorded as one in
/// <c>docs/formulas/migration.md</c> — it is a real economic constant nobody has
/// measured, not a symmetric default, and nothing downstream should cite it as
/// evidence.
/// </para>
/// <para>
/// The manual's later Capitol upgrade, which relaxes the limit to one-third, is
/// deliberately absent: there is no upgrade mechanic, and inventing a trigger
/// for one would be inventing a rule rather than filling in a value.
/// </para>
/// </remarks>
public sealed class MigrationSettings
{
    private readonly IReadOnlyList<CommodityQuantity> _costPerWorker;

    public MigrationSettings(
        IEnumerable<CommodityQuantity> costPerWorker,
        int provincesPerRecruit)
    {
        ArgumentNullException.ThrowIfNull(costPerWorker);
        if (provincesPerRecruit <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(provincesPerRecruit),
                "A country needs some positive number of provinces per recruit.");
        }

        var costs = costPerWorker.ToArray();
        if (costs.Length == 0)
        {
            throw new ArgumentException(
                "Migration must cost something; a free workforce is not a rule the original has.",
                nameof(costPerWorker));
        }

        if (costs.Select(static item => item.Commodity).Distinct().Count() != costs.Length)
        {
            throw new ArgumentException(
                "Migration cost cannot name a commodity twice.", nameof(costPerWorker));
        }

        _costPerWorker = Array.AsReadOnly(costs);
        ProvincesPerRecruit = provincesPerRecruit;
    }

    /// <summary>What one recruit costs. Charged per worker, not per order.</summary>
    public IReadOnlyList<CommodityQuantity> CostPerWorker => _costPerWorker;

    /// <summary>
    /// How many owned provinces buy one recruit per turn. Four in the original,
    /// rounded down, so a country of three provinces can recruit nobody at all.
    /// </summary>
    public int ProvincesPerRecruit { get; }
}
