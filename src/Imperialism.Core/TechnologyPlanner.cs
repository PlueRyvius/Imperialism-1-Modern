namespace Imperialism.Core;

/// <summary>One thing that happened on a country's Investment screen.</summary>
internal abstract record PlannedInvestment(CountryId Country, TechnologyId Technology);

internal sealed record PlannedTechnologyPurchase(
    CountryId Country,
    TechnologyId Technology,
    long Paid) : PlannedInvestment(Country, Technology);

internal sealed record PlannedTechnologyRefusal(
    CountryId Country,
    TechnologyId Technology,
    TechnologyPurchaseRefusal Reason) : PlannedInvestment(Country, Technology);

/// <summary>
/// The Investment screen: what a country may buy, what it costs, and what
/// knowing it a turn later means.
/// </summary>
/// <remarks>
/// This is the first thing in the engine that <em>acquires</em> knowledge. Before
/// it, every gate in the project — three improvement rungs per deposit, four rail
/// terrains, oil prospecting — could only ever be tested shut, because a `tech`
/// record was the sole source and imported worlds carry none the importer
/// converts into anything buyable.
/// <para>
/// <b>Knowledge is read from a snapshot taken before any of it is spent.</b> That
/// is what makes a chain of prerequisites take a turn per link, which is the
/// owner's reading of the original: buying a technology does not research it, it
/// "spends the money and the research finishes after the turn ends before the next
/// starts", and the dependent entry cannot even be clicked. Without the snapshot,
/// <see cref="TurnPhase.Investment"/> would be the one phase in the pipeline that
/// reads its own output.
/// </para>
/// <para>
/// <b>Cash leaves the treasury as each order is read, and there is no pooling.</b>
/// Construction and improvement were charged back in
/// <see cref="TurnPhase.Development"/> and had first call on the money; research
/// takes what is left, and a second purchase is refused outright if the first
/// emptied the treasury rather than being part-funded. Nothing is refunded.
/// </para>
/// <para>
/// Availability is by date and world-wide. There is nothing per country to check
/// and nothing to hide: "advances become available on a world-wide basis; they
/// cannot be kept secret", and "technology, once available, does not vanish. If
/// you cannot afford the cotton gin in 1818, invest in 1830."
/// </para>
/// </remarks>
internal static class TechnologyPlanner
{
    public static IReadOnlyList<PlannedInvestment> Resolve(WorldState state, TurnOrders orders)
    {
        var outcomes = new List<PlannedInvestment>();
        var definition = state.Definition;
        var year = state.CurrentYear;

        for (var countryValue = 0; countryValue < orders.Count; countryValue++)
        {
            var country = new CountryId(countryValue);
            var wanted = orders[country].BuyTechnology;
            if (wanted.Count == 0)
            {
                continue;
            }

            // Taken before anything is bought, so nothing bought this turn can
            // satisfy a prerequisite or make a second purchase redundant.
            var knownAtStart = new bool[definition.Technologies.Count];
            for (var index = 0; index < knownAtStart.Length; index++)
            {
                knownAtStart[index] = state.HasTechnology(country, new TechnologyId(index));
            }

            foreach (var technology in wanted)
            {
                var refusal = Legality(state, knownAtStart, year, technology, out var cost);
                if (refusal is null && !state.TrySpendCash(country, cost))
                {
                    refusal = TechnologyPurchaseRefusal.NotEnoughCash;
                }

                if (refusal is { } reason)
                {
                    outcomes.Add(new PlannedTechnologyRefusal(country, technology, reason));
                    continue;
                }

                state.GrantTechnology(country, technology);
                outcomes.Add(new PlannedTechnologyPurchase(country, technology, cost));
            }
        }

        return outcomes;
    }

    /// <summary>
    /// Whether this country may buy this, and what it would cost. Every refusal
    /// bar <see cref="TechnologyPurchaseRefusal.NotEnoughCash"/> is a reason the
    /// original would not have offered the entry at all.
    /// </summary>
    private static TechnologyPurchaseRefusal? Legality(
        WorldState state,
        bool[] knownAtStart,
        int year,
        TechnologyId technology,
        out long cost)
    {
        cost = 0;
        if ((uint)technology.Value >= (uint)knownAtStart.Length)
        {
            return TechnologyPurchaseRefusal.NoSuchTechnology;
        }

        if (knownAtStart[technology.Value])
        {
            return TechnologyPurchaseRefusal.AlreadyKnown;
        }

        var definition = state.Definition.Technologies[technology.Value];

        // No price means it was never on the screen. Checked before the date and
        // the prerequisites because it is the more basic fact: the two every power
        // starts with have neither a price nor anything to wait for.
        if (definition.Cost is not { } price)
        {
            return TechnologyPurchaseRefusal.NotForSale;
        }

        if (definition.AvailableFrom is { } arrival && year < arrival)
        {
            return TechnologyPurchaseRefusal.NotYetAvailable;
        }

        foreach (var required in definition.Prerequisites)
        {
            if (!knownAtStart[required.Value])
            {
                return TechnologyPurchaseRefusal.PrerequisiteNotKnown;
            }
        }

        // Affordability is not checked here: TrySpendCash is the one place that
        // both tests and takes, so asking twice would let a second order slip
        // through on a treasury the first had already emptied.
        cost = price;
        return null;
    }
}
