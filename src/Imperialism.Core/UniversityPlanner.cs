namespace Imperialism.Core;

internal sealed record PlannedCivilianPurchase(
    CountryId Country,
    CivilianTypeId Type,
    long Count,
    long ExpertsTaken,
    IReadOnlyList<CommodityQuantity> Paid,
    long CashPaid);

internal sealed record PlannedCivilianPurchaseRefusal(
    CountryId Country,
    CivilianTypeId Type,
    long Requested,
    CivilianPurchaseRefusal Reason);

internal sealed record PlannedCivilianDisband(
    CountryId Country,
    CivilianUnitId Unit,
    CivilianTypeId Type);

internal sealed record PlannedCivilianDisbandRefusal(
    CountryId Country,
    CivilianUnitId Unit,
    CivilianDisbandRefusal Reason);

/// <summary>
/// The University: disbanding civilians, then buying them. Planned inside
/// <see cref="ProductionPlanner"/>, after the Trade School and before any recipe.
/// </summary>
/// <remarks>
/// It runs there because what it spends is what the Industry screen spends. The
/// expert "is deducted from your work force permanently" the moment the civilian is
/// ordered, so this turn's recipes must already see the smaller pool; and the paper
/// and cash leave before recipes and the market can claim them. See
/// <c>docs/formulas/university.md</c>.
/// <para>
/// <b>Every count is read against the workforce the turn opened with.</b> A civilian
/// disbanded this turn returns its expert after production, so the same expert
/// cannot be spent again at the University in the same turn — that follows from the
/// expert working only from next turn, a chosen rule.
/// </para>
/// </remarks>
internal static class UniversityPlanner
{
    /// <summary>One expert per civilian disbanded, whatever the type cost.</summary>
    public const long ExpertsReturnedPerDisband = 1;

    public static void PlanDisbands(
        WorldState state,
        CountryId country,
        IReadOnlyList<CivilianUnitId> disbands,
        List<PlannedCivilianDisband> planned,
        List<PlannedCivilianDisbandRefusal> refusals)
    {
        foreach (var unit in disbands)
        {
            if (state.GetCivilian(unit) is not { } civilian)
            {
                refusals.Add(new PlannedCivilianDisbandRefusal(
                    country, unit, CivilianDisbandRefusal.NoSuchCivilian));
                continue;
            }

            if (civilian.Country != country)
            {
                refusals.Add(new PlannedCivilianDisbandRefusal(
                    country, unit, CivilianDisbandRefusal.NotYours));
                continue;
            }

            planned.Add(new PlannedCivilianDisband(country, unit, civilian.Type));
        }
    }

    /// <summary>
    /// Plans one country's purchases in list order, against the healthy experts the
    /// turn opened with and the cash and paper the Trade School left.
    /// </summary>
    public static void PlanPurchases(
        WorldState state,
        CountryId country,
        IReadOnlyList<CivilianPurchaseOrder> orders,
        long[] available,
        long[] deltas,
        ref long cash,
        List<PlannedCivilianPurchase> planned,
        List<PlannedCivilianPurchaseRefusal> refusals)
    {
        if (orders.Count == 0)
        {
            return;
        }

        // Only the healthy can be spared, the same rule the Trade School keeps: a sick
        // expert supplies nothing this turn, so there is nothing to take.
        var healthyExperts = state.GetWorkers(country, WorkerGrade.Expert) -
            state.GetSickWorkers(country, WorkerGrade.Expert);
        foreach (var order in orders)
        {
            var refusal = Plan(
                state, country, order, available, deltas, ref cash, ref healthyExperts, planned);
            if (refusal is { } reason)
            {
                refusals.Add(new PlannedCivilianPurchaseRefusal(country, order.Type, order.Count, reason));
            }
        }
    }

    private static CivilianPurchaseRefusal? Plan(
        WorldState state,
        CountryId country,
        CivilianPurchaseOrder order,
        long[] available,
        long[] deltas,
        ref long cash,
        ref long healthyExperts,
        List<PlannedCivilianPurchase> planned)
    {
        var definition = state.Definition;
        if ((uint)order.Type.Value >= (uint)definition.CivilianTypes.Count)
        {
            return CivilianPurchaseRefusal.UnknownType;
        }

        if (order.Count <= 0)
        {
            return CivilianPurchaseRefusal.NonPositiveCount;
        }

        if (definition.CivilianTypes[order.Type.Value].Purchase is not { } price)
        {
            return CivilianPurchaseRefusal.NotPurchasable;
        }

        // Before anything is priced, against knowledge as the turn opened: research
        // bought this turn lands in Investment, which runs last.
        if (price.RequiredTechnology is { } required && !state.HasTechnology(country, required))
        {
            return CivilianPurchaseRefusal.TechnologyNotKnown;
        }

        if (state.GetCountryCapital(country) is null)
        {
            return CivilianPurchaseRefusal.NoCapital;
        }

        // Every check divides rather than multiplies, so a count whose bill would
        // overflow a long is simply unaffordable and refused like any other.
        if (!CanAfford(price.ExpertWorkers, order.Count, healthyExperts))
        {
            return CivilianPurchaseRefusal.NotEnoughExperts;
        }

        var commodityCount = definition.Commodities.Count;
        if (price.Cost.Any(item => !CanAfford(
                item.Quantity,
                order.Count,
                available[(country.Value * commodityCount) + item.Commodity.Value])))
        {
            return CivilianPurchaseRefusal.NotEnoughCommodities;
        }

        if (!CanAfford(price.CashCost, order.Count, cash))
        {
            return CivilianPurchaseRefusal.NotEnoughCash;
        }

        // Every total is now bounded by what the country holds, so none overflows.
        var experts = price.ExpertWorkers * order.Count;
        var paid = price.Cost
            .Select(item => new CommodityQuantity(item.Commodity, item.Quantity * order.Count))
            .ToArray();
        var cashCost = price.CashCost * order.Count;
        foreach (var item in paid)
        {
            var offset = (country.Value * commodityCount) + item.Commodity.Value;
            available[offset] -= item.Quantity;
            deltas[offset] = checked(deltas[offset] - item.Quantity);
        }

        cash -= cashCost;
        healthyExperts -= experts;
        planned.Add(new PlannedCivilianPurchase(
            country, order.Type, order.Count, experts, Array.AsReadOnly(paid), cashCost));
        return null;
    }

    private static bool CanAfford(long perUnit, long count, long held) =>
        held >= 0 && (perUnit <= 0 || count <= held / perUnit);
}
