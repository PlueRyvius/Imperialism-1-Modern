namespace Imperialism.Core;

/// <summary>
/// A standing diplomatic tie one country holds in another. Ties are directional: a
/// consulate is <em>owned</em> by the Great Power that bought it and stands in the host.
/// </summary>
public enum DiplomaticTie : byte
{
    /// <summary>A trade consulate. Required before an embassy.</summary>
    Consulate,

    /// <summary>An embassy. Required before a non-aggression pact.</summary>
    Embassy,

    /// <summary>A non-aggression pact with a Minor Nation.</summary>
    Pact,
}

/// <summary>Which scroll on the Diplomacy screen an overture was made with.</summary>
public enum DiplomaticOvertureKind : byte
{
    /// <summary>Open a trade consulate. $500 at the manual's price (lines 3516-3518).</summary>
    Consulate,

    /// <summary>Open an embassy. $5,000 at the manual's price (lines 3528-3531).</summary>
    Embassy,

    /// <summary>Offer a non-aggression pact. Free (lines 3541-3545).</summary>
    Pact,

    /// <summary>Offer peace to a country this one is at war with (lines 3466-3468).</summary>
    Peace,
}

/// <summary>
/// One overture from the Diplomacy screen: a scroll clicked on a country.
/// </summary>
/// <remarks>
/// Whether it can be carried out is decided when the turn resolves, never here, so an
/// overture to a country that does not exist is refused with an event rather than
/// thrown — the same standing as a declaration of war.
/// </remarks>
public readonly record struct DiplomaticOverture(DiplomaticOvertureKind Kind, CountryId Target);

/// <summary>What a consulate and an embassy cost. Both figures are the manual's.</summary>
public sealed class DiplomacySettings
{
    public DiplomacySettings(long consulateCost, long embassyCost)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(consulateCost);
        ArgumentOutOfRangeException.ThrowIfNegative(embassyCost);
        ConsulateCost = consulateCost;
        EmbassyCost = embassyCost;
    }

    /// <summary>"The overture costs you $500" (manual line 3517).</summary>
    public long ConsulateCost { get; }

    /// <summary>"The Embassy overture costs $5000" (manual lines 3530-3531).</summary>
    public long EmbassyCost { get; }
}

/// <summary>Why an overture changed nothing and cost nothing.</summary>
public enum DiplomaticOvertureRefusal : byte
{
    /// <summary>No country carries that id in this world.</summary>
    NoSuchCountry,

    /// <summary>A country cannot make an overture to itself.</summary>
    Itself,

    /// <summary>
    /// Only a Great Power makes overtures. Minor Nations are played by nobody and the
    /// manual describes overtures only from a Great Power's side.
    /// </summary>
    NotAGreatPower,

    /// <summary>The same kind of overture to the same country appears twice this turn.</summary>
    AlreadyOrderedThisTurn,

    /// <summary>
    /// The world prices no consulates or embassies — a package migrated from before
    /// version 30.
    /// </summary>
    NotOffered,

    /// <summary>The tie is already held as the turn opens.</summary>
    AlreadyEstablished,

    /// <summary>
    /// The pair is at war, declared or in effect, as the turn opens, or either side
    /// declares war on the other this turn. Only peace is offered across a war.
    /// </summary>
    AtWar,

    /// <summary>An embassy needs an open consulate (manual line 3527).</summary>
    NoConsulate,

    /// <summary>A pact needs an open embassy (manual lines 3541-3542).</summary>
    NoEmbassy,

    /// <summary>
    /// A non-aggression pact is offered to a Minor Nation; between Great Powers the
    /// manual offers alliances instead, which are not modelled.
    /// </summary>
    TargetIsAGreatPower,

    /// <summary>The treasury, less what the Industry screen already spent, cannot pay.</summary>
    NotEnoughCash,

    /// <summary>Peace needs a war in effect as the turn opens (manual lines 3466-3468).</summary>
    NotAtWar,

    /// <summary>
    /// A Great Power accepts peace "only when it is to their advantage" (lines
    /// 3498-3499). With nothing to play it, the test is a chosen rule: it must offer peace
    /// back the same turn. See <c>docs/formulas/diplomacy.md</c>.
    /// </summary>
    Declined,

    /// <summary>
    /// Peace between this pair was already made earlier this turn — the other side's
    /// offer, taken first because its country id is lower. Counts once, like a mutual
    /// declaration of war.
    /// </summary>
    AlreadyMadeThisTurn,
}

/// <summary>Records an overture carried out. What it grants opens next turn.</summary>
public sealed record DiplomaticOvertureAcceptedEvent : TurnEvent
{
    public DiplomaticOvertureAcceptedEvent(
        int turnNumber, CountryId country, CountryId target, DiplomaticOvertureKind kind, long cashCost)
        : base(turnNumber, TurnPhase.Diplomacy)
    {
        Country = country;
        Target = target;
        Kind = kind;
        CashCost = cashCost;
    }

    public CountryId Country { get; }

    public CountryId Target { get; }

    public DiplomaticOvertureKind Kind { get; }

    /// <summary>Cash taken from the treasury when the order was given; never refunded.</summary>
    public long CashCost { get; }
}

/// <summary>Records an overture that changed nothing and cost nothing.</summary>
public sealed record DiplomaticOvertureRefusedEvent : TurnEvent
{
    public DiplomaticOvertureRefusedEvent(
        int turnNumber,
        CountryId country,
        CountryId target,
        DiplomaticOvertureKind kind,
        DiplomaticOvertureRefusal reason)
        : base(turnNumber, TurnPhase.Diplomacy)
    {
        Country = country;
        Target = target;
        Kind = kind;
        Reason = reason;
    }

    public CountryId Country { get; }

    public CountryId Target { get; }

    public DiplomaticOvertureKind Kind { get; }

    public DiplomaticOvertureRefusal Reason { get; }
}

/// <summary>
/// Records the ties a declaration of war cut between a pair: any pact, either way, and
/// between two Great Powers their consulates and embassies, either way.
/// </summary>
public sealed record DiplomaticTiesCutEvent : TurnEvent
{
    public DiplomaticTiesCutEvent(
        int turnNumber, CountryId country, CountryId target, bool pactBroken, bool postsClosed)
        : base(turnNumber, TurnPhase.Diplomacy)
    {
        Country = country;
        Target = target;
        PactBroken = pactBroken;
        PostsClosed = postsClosed;
    }

    /// <summary>The country that declared war.</summary>
    public CountryId Country { get; }

    public CountryId Target { get; }

    /// <summary>A non-aggression pact between the pair, in either direction, was broken.</summary>
    public bool PactBroken { get; }

    /// <summary>Consulates or embassies between the two Great Powers were closed.</summary>
    public bool PostsClosed { get; }
}

internal sealed record PlannedOverture(
    CountryId Country,
    CountryId Target,
    DiplomaticOvertureKind Kind,
    long CashCost,
    DiplomaticOvertureRefusal? Refusal);

/// <summary>
/// The turn's overtures, decided before anything is committed so their cash is spoken
/// for before the market clears.
/// </summary>
internal sealed class DiplomacyPlan
{
    public DiplomacyPlan(IReadOnlyList<PlannedOverture> overtures, long[] cashClaimed)
    {
        Overtures = overtures;
        CashClaimed = cashClaimed;
    }

    /// <summary>Every overture in resolution order, accepted or refused.</summary>
    public IReadOnlyList<PlannedOverture> Overtures { get; }

    /// <summary>Cash each country's accepted overtures cost, by country id.</summary>
    public long[] CashClaimed { get; }
}

/// <summary>
/// Consulates, embassies, pacts and peace: the <c>Diplomacy</c> phase's overtures.
/// </summary>
/// <remarks>
/// <b>Planned against the state the turn opened with</b>, countries in id order and
/// overtures in list order, and applied in the <c>Diplomacy</c> phase before any
/// declaration of war. Planning first is what lets their cash be reserved before trade
/// bids with it, the way the Trade School's already is. Every rule the manual does not
/// state is labelled in <c>docs/formulas/diplomacy.md</c>.
/// </remarks>
internal static class DiplomacyPlanner
{
    public static DiplomacyPlan Create(WorldState state, TurnOrders orders, long[]? cashClaimed = null)
    {
        var definition = state.Definition;
        var countryCount = definition.Countries.Count;
        var planned = new List<PlannedOverture>();
        var spent = new long[countryCount];

        // Which country offered peace to which, for the mutual test between Great Powers.
        var peaceOffers = new HashSet<(int, int)>();
        for (var index = 0; index < countryCount; index++)
        {
            foreach (var overture in orders[new CountryId(index)].Overtures)
            {
                if (overture.Kind == DiplomaticOvertureKind.Peace)
                {
                    _ = peaceOffers.Add((index, overture.Target.Value));
                }
            }
        }

        var peaceMade = new HashSet<(int, int)>();
        for (var index = 0; index < countryCount; index++)
        {
            var country = new CountryId(index);
            var ordered = new HashSet<(DiplomaticOvertureKind, CountryId)>();
            var available = Math.Max(0, state.GetCash(country) - (cashClaimed?[index] ?? 0));
            foreach (var overture in orders[country].Overtures)
            {
                var target = overture.Target;
                var cost = 0L;
                var refusal = Refuse(state, orders, country, overture, ordered, peaceOffers, peaceMade);
                if (refusal is null)
                {
                    cost = Cost(definition.Diplomacy!, overture.Kind);
                    if (cost > available - spent[index])
                    {
                        refusal = DiplomaticOvertureRefusal.NotEnoughCash;
                        cost = 0;
                    }
                }

                _ = ordered.Add((overture.Kind, target));
                if (refusal is null)
                {
                    spent[index] = checked(spent[index] + cost);
                    if (overture.Kind == DiplomaticOvertureKind.Peace)
                    {
                        _ = peaceMade.Add(Pair(index, target.Value));
                    }
                }

                planned.Add(new PlannedOverture(country, target, overture.Kind, cost, refusal));
            }
        }

        return new DiplomacyPlan(planned, spent);
    }

    /// <summary>
    /// Carries out the accepted overtures. Runs first in the <c>Diplomacy</c> phase, so a
    /// declaration later in the phase still cuts what was granted here.
    /// </summary>
    public static IReadOnlyList<TurnEvent> Apply(WorldState state, DiplomacyPlan plan, int turnNumber)
    {
        var events = new List<TurnEvent>(plan.Overtures.Count);
        foreach (var overture in plan.Overtures)
        {
            if (overture.Refusal is { } reason)
            {
                events.Add(new DiplomaticOvertureRefusedEvent(
                    turnNumber, overture.Country, overture.Target, overture.Kind, reason));
                continue;
            }

            if (overture.CashCost > 0 && !state.TrySpendCash(overture.Country, overture.CashCost))
            {
                // The plan reserved it against the turn's opening treasury, and nothing
                // runs before Diplomacy, so this cannot happen without a bug.
                throw new InvalidOperationException("A planned overture could not be paid for.");
            }

            switch (overture.Kind)
            {
                case DiplomaticOvertureKind.Consulate:
                    state.GrantDiplomaticTie(overture.Country, overture.Target, DiplomaticTie.Consulate);
                    break;
                case DiplomaticOvertureKind.Embassy:
                    state.GrantDiplomaticTie(overture.Country, overture.Target, DiplomaticTie.Embassy);
                    break;
                case DiplomaticOvertureKind.Pact:
                    state.GrantDiplomaticTie(overture.Country, overture.Target, DiplomaticTie.Pact);
                    break;
                default:
                    // The existing next-turn token: the war stays in effect this turn and
                    // ends as the next one opens.
                    state.SetRelationMode(overture.Country, overture.Target, CountryRelationMode.Standard);
                    break;
            }

            events.Add(new DiplomaticOvertureAcceptedEvent(
                turnNumber, overture.Country, overture.Target, overture.Kind, overture.CashCost));
        }

        return events;
    }

    /// <summary>
    /// Cuts what a declaration of war breaks: a pact either way at once, and between two
    /// Great Powers their consulates and embassies either way. Returns the event, or null
    /// when there was nothing to cut.
    /// </summary>
    public static DiplomaticTiesCutEvent? CutTiesForWar(
        WorldState state, CountryId country, CountryId target, int turnNumber)
    {
        var pactBroken = state.RemoveDiplomaticTie(country, target, DiplomaticTie.Pact);
        pactBroken |= state.RemoveDiplomaticTie(target, country, DiplomaticTie.Pact);

        var postsClosed = false;
        var countries = state.Definition.Countries;
        if (countries[country.Value].IsGreatPower && countries[target.Value].IsGreatPower)
        {
            foreach (var tie in new[] { DiplomaticTie.Consulate, DiplomaticTie.Embassy })
            {
                postsClosed |= state.RemoveDiplomaticTie(country, target, tie);
                postsClosed |= state.RemoveDiplomaticTie(target, country, tie);
            }
        }

        return pactBroken || postsClosed
            ? new DiplomaticTiesCutEvent(turnNumber, country, target, pactBroken, postsClosed)
            : null;
    }

    private static DiplomaticOvertureRefusal? Refuse(
        WorldState state,
        TurnOrders orders,
        CountryId country,
        DiplomaticOverture overture,
        HashSet<(DiplomaticOvertureKind, CountryId)> ordered,
        HashSet<(int, int)> peaceOffers,
        HashSet<(int, int)> peaceMade)
    {
        var definition = state.Definition;
        var target = overture.Target;
        if ((uint)target.Value >= (uint)definition.Countries.Count)
        {
            return DiplomaticOvertureRefusal.NoSuchCountry;
        }

        if (target == country)
        {
            return DiplomaticOvertureRefusal.Itself;
        }

        if (!definition.Countries[country.Value].IsGreatPower)
        {
            return DiplomaticOvertureRefusal.NotAGreatPower;
        }

        if (ordered.Contains((overture.Kind, target)))
        {
            return DiplomaticOvertureRefusal.AlreadyOrderedThisTurn;
        }

        var targetIsGreatPower = definition.Countries[target.Value].IsGreatPower;
        if (overture.Kind == DiplomaticOvertureKind.Peace)
        {
            if (peaceMade.Contains(Pair(country.Value, target.Value)))
            {
                return DiplomaticOvertureRefusal.AlreadyMadeThisTurn;
            }

            if (!state.HasEffectiveHostility(country, target))
            {
                return DiplomaticOvertureRefusal.NotAtWar;
            }

            // "Minor Nations never refuse an offer of peace" (lines 3496-3497). A Great
            // Power accepts only an offer it makes back the same turn — a chosen rule.
            return !targetIsGreatPower || peaceOffers.Contains((target.Value, country.Value))
                ? null
                : DiplomaticOvertureRefusal.Declined;
        }

        // A war declared but not yet begun counts, and so does one either side declares
        // this turn: nobody opens an embassy in a country it is going to war with.
        if (state.GetRelationMode(country, target) == CountryRelationMode.Hostile ||
            orders[country].DeclareWar.Contains(target) ||
            orders[target].DeclareWar.Contains(country))
        {
            return DiplomaticOvertureRefusal.AtWar;
        }

        switch (overture.Kind)
        {
            case DiplomaticOvertureKind.Consulate:
                if (definition.Diplomacy is null)
                {
                    return DiplomaticOvertureRefusal.NotOffered;
                }

                return state.HoldsDiplomaticTie(country, target, DiplomaticTie.Consulate)
                    ? DiplomaticOvertureRefusal.AlreadyEstablished
                    : null;
            case DiplomaticOvertureKind.Embassy:
                if (definition.Diplomacy is null)
                {
                    return DiplomaticOvertureRefusal.NotOffered;
                }

                if (state.HoldsDiplomaticTie(country, target, DiplomaticTie.Embassy))
                {
                    return DiplomaticOvertureRefusal.AlreadyEstablished;
                }

                return state.HoldsDiplomaticTie(country, target, DiplomaticTie.Consulate)
                    ? null
                    : DiplomaticOvertureRefusal.NoConsulate;
            case DiplomaticOvertureKind.Pact:
                if (targetIsGreatPower)
                {
                    return DiplomaticOvertureRefusal.TargetIsAGreatPower;
                }

                if (state.HoldsDiplomaticTie(country, target, DiplomaticTie.Pact))
                {
                    return DiplomaticOvertureRefusal.AlreadyEstablished;
                }

                return state.HoldsDiplomaticTie(country, target, DiplomaticTie.Embassy)
                    ? null
                    : DiplomaticOvertureRefusal.NoEmbassy;
            default:
                throw new ArgumentOutOfRangeException(nameof(overture));
        }
    }

    private static long Cost(DiplomacySettings settings, DiplomaticOvertureKind kind) => kind switch
    {
        DiplomaticOvertureKind.Consulate => settings.ConsulateCost,
        DiplomaticOvertureKind.Embassy => settings.EmbassyCost,
        _ => 0,
    };

    private static (int, int) Pair(int first, int second) =>
        first < second ? (first, second) : (second, first);
}
