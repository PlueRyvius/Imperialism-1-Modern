namespace Imperialism.Core;

internal sealed record PlannedFeeding(
    CountryId Country,
    long WellFed,
    long Sick,
    long Starved,
    IReadOnlyList<CommodityQuantity> Eaten);

/// <summary>
/// Feeds each country's workforce from what it can reach this turn.
/// </summary>
/// <remarks>
/// Unlike the other planners this one mutates as it goes, because the food it
/// takes has to come out of two places at once — the deliveries arriving this
/// turn and the warehouse behind them — and the order matters. Workers eat
/// newly transported food first, which is one of the two documented
/// same-resolution exceptions to everything else being deferred a turn.
/// </remarks>
internal static class FeedingPlanner
{
    public static IReadOnlyList<PlannedFeeding> Resolve(WorldState state)
    {
        var definition = state.Definition;
        if (definition.Feeding is not { } feeding)
        {
            return Array.Empty<PlannedFeeding>();
        }

        var results = new List<PlannedFeeding>(definition.Countries.Count);
        var eaten = new long[definition.Commodities.Count];
        for (var countryValue = 0; countryValue < definition.Countries.Count; countryValue++)
        {
            var country = new CountryId(countryValue);
            var headcount = state.GetTotalWorkers(country);
            if (headcount == 0)
            {
                continue;
            }

            Array.Clear(eaten);
            long wellFed = 0, sick = 0, starved = 0;
            for (var worker = 0L; worker < headcount; worker++)
            {
                var preference = feeding.GetPreference(worker);
                if (TryEatAny(state, country, preference.Accepted, eaten))
                {
                    wellFed++;
                    continue;
                }

                // Canned food is the polite substitute: it satisfies a worker
                // whose own preference is unavailable without making it ill.
                if (feeding.CannedFood is { } canned &&
                    TryEat(state, country, canned, eaten))
                {
                    wellFed++;
                    continue;
                }

                if (TryEatAnythingElse(state, country, feeding, eaten))
                {
                    sick++;
                    continue;
                }

                starved++;
            }

            // Starvation first, so illness is assigned among the survivors and
            // the same worker is never both. Both take the cheapest grades.
            Starve(state, country, starved);
            state.SetSickWorkers(country, sick);
            results.Add(new PlannedFeeding(country, wellFed, sick, starved, ToQuantities(eaten)));
        }

        return Array.AsReadOnly(results.ToArray());
    }

    /// <summary>
    /// Anything edible at all, for a worker whose preference and the canned
    /// substitute have both run out. Eating it means reporting sick.
    /// </summary>
    private static bool TryEatAnythingElse(
        WorldState state,
        CountryId country,
        FeedingSettings feeding,
        long[] eaten)
    {
        foreach (var preference in feeding.PreferenceCycle)
        {
            if (TryEatAny(state, country, preference.Accepted, eaten))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryEatAny(
        WorldState state,
        CountryId country,
        IReadOnlyList<CommodityId> accepted,
        long[] eaten)
    {
        foreach (var commodity in accepted)
        {
            if (TryEat(state, country, commodity, eaten))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryEat(WorldState state, CountryId country, CommodityId commodity, long[] eaten)
    {
        if (state.ConsumePending(country, commodity, 1) == 0 &&
            !state.TryConsumeAvailable(country, commodity, 1))
        {
            return false;
        }

        eaten[commodity.Value]++;
        return true;
    }

    /// <summary>
    /// Removes the workers who found nothing at all, lowest grade first.
    /// </summary>
    /// <remarks>
    /// **This order is a choice, not a finding.** The manual says a starving
    /// worker is permanently removed but never which one. Taking the untrained
    /// first mirrors the way the pool grows — new arrivals are untrained — and
    /// costs the player least. <see cref="WorldState.SetSickWorkers"/> applies
    /// the same convention to illness, for the same reason. See
    /// <c>docs/formulas/feeding.md</c>.
    /// </remarks>
    private static void Starve(WorldState state, CountryId country, long starved)
    {
        foreach (var grade in WorkerGrades.All)
        {
            if (starved == 0)
            {
                return;
            }

            var present = state.GetWorkers(country, grade);
            var lost = Math.Min(present, starved);
            state.SetWorkers(country, grade, present - lost);
            starved -= lost;
        }
    }

    private static IReadOnlyList<CommodityQuantity> ToQuantities(long[] totals)
    {
        var quantities = new List<CommodityQuantity>();
        for (var commodity = 0; commodity < totals.Length; commodity++)
        {
            if (totals[commodity] > 0)
            {
                quantities.Add(new CommodityQuantity(new CommodityId(commodity), totals[commodity]));
            }
        }

        return Array.AsReadOnly(quantities.ToArray());
    }
}
