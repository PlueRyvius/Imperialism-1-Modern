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

/// <summary>
/// What a consulate and an embassy cost, both the manual's figures, and which subsidy
/// percentages the trade policies screen offers.
/// </summary>
public sealed class DiplomacySettings
{
    public DiplomacySettings(long consulateCost, long embassyCost, IEnumerable<int>? subsidyPercents = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(consulateCost);
        ArgumentOutOfRangeException.ThrowIfNegative(embassyCost);
        var percents = subsidyPercents?.ToArray() ?? [];
        for (var index = 0; index < percents.Length; index++)
        {
            if (percents[index] < TradePolicy.MinimumSubsidyPercent ||
                percents[index] > TradePolicy.MaximumSubsidyPercent)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(subsidyPercents), "A subsidy is between 1 and 99 percent.");
            }

            if (index > 0 && percents[index] <= percents[index - 1])
            {
                throw new ArgumentException(
                    "Subsidy percentages are distinct and in ascending order.", nameof(subsidyPercents));
            }
        }

        ConsulateCost = consulateCost;
        EmbassyCost = embassyCost;
        SubsidyPercents = Array.AsReadOnly(percents);
    }

    /// <summary>"The overture costs you $500" (manual line 3517).</summary>
    public long ConsulateCost { get; }

    /// <summary>"The Embassy overture costs $5000" (manual lines 3530-3531).</summary>
    public long EmbassyCost { get; }

    /// <summary>
    /// The subsidy buttons on the trade policies screen, ascending. The manual promises "a
    /// new trade subsidy of varying percentages" (line 3681) and its tutorial presses
    /// buttons labelled 5% and 10% (lines 5121 and 5126); nothing in the executable names
    /// a third. Empty means no subsidy can be granted, which is how a package migrated
    /// from before version 31 behaves. A boycott needs no number and is unaffected.
    /// </summary>
    public IReadOnlyList<int> SubsidyPercents { get; }
}

/// <summary>Which button on the trade policies screen a policy was set with.</summary>
public enum TradePolicyKind : byte
{
    /// <summary>No policy: trade at the world price, ranked by nothing but country id.</summary>
    None,

    /// <summary>A trade subsidy granted to the target (manual lines 3692-3699).</summary>
    Subsidy,

    /// <summary>"No trade of any kind occurs between the two countries" (lines 3436-3437).</summary>
    Boycott,
}

/// <summary>
/// One Great Power's standing trade policy toward one other country: nothing, a subsidy of
/// some percentage, or a boycott. The three are exclusive; setting one replaces the last.
/// </summary>
public readonly record struct TradePolicy
{
    /// <summary>The smallest subsidy a world may list.</summary>
    public const int MinimumSubsidyPercent = 1;

    /// <summary>The largest subsidy a world may list; 100 would make goods free.</summary>
    public const int MaximumSubsidyPercent = 99;

    private TradePolicy(TradePolicyKind kind, int percent)
    {
        Kind = kind;
        Percent = percent;
    }

    public static TradePolicy None => default;

    public static TradePolicy Boycott => new(TradePolicyKind.Boycott, 0);

    public TradePolicyKind Kind { get; }

    /// <summary>The subsidy's percentage; zero for anything but a subsidy.</summary>
    public int Percent { get; }

    /// <summary>
    /// A subsidy of <paramref name="percent"/>. Whether the world offers that percentage
    /// is decided when the turn resolves, and refused with an event if not; only a figure
    /// no world could list is malformed.
    /// </summary>
    public static TradePolicy Subsidy(int percent)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(percent, MinimumSubsidyPercent);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(percent, MaximumSubsidyPercent);
        return new TradePolicy(TradePolicyKind.Subsidy, percent);
    }

    /// <summary>The subsidy this policy grants, or zero.</summary>
    internal int SubsidyPercent => Kind == TradePolicyKind.Subsidy ? Percent : 0;

    internal byte Encode() => Kind switch
    {
        TradePolicyKind.None => 0,
        TradePolicyKind.Subsidy => (byte)Percent,
        TradePolicyKind.Boycott => byte.MaxValue,
        _ => throw new ArgumentOutOfRangeException(nameof(Kind)),
    };

    internal static TradePolicy Decode(byte value) => value switch
    {
        0 => None,
        byte.MaxValue => Boycott,
        _ => new TradePolicy(TradePolicyKind.Subsidy, value),
    };
}

/// <summary>
/// One button on the trade policies screen pressed on one country: a subsidy, a boycott,
/// or <see cref="TradePolicy.None"/> to cancel what stands.
/// </summary>
public readonly record struct TradePolicyOrder(CountryId Target, TradePolicy Policy);

/// <summary>Why a trade policy order changed nothing.</summary>
public enum TradePolicyRefusal : byte
{
    /// <summary>No country carries that id in this world.</summary>
    NoSuchCountry,

    /// <summary>A country sets no policy toward itself.</summary>
    Itself,

    /// <summary>"Both boycotts and subsidies are initiated by Great Powers" (line 3433).</summary>
    NotAGreatPower,

    /// <summary>A second policy toward the same country in one turn.</summary>
    AlreadyOrderedThisTurn,

    /// <summary>
    /// Policies are set "with any country in the game where you have a trade consulate or
    /// Embassy" (lines 3663-3667), and none was open as the turn began.
    /// </summary>
    NoPost,

    /// <summary>The world's trade policies screen has no button for that percentage.</summary>
    PercentNotOffered,
}

/// <summary>Records a trade policy set. It takes effect next turn.</summary>
public sealed record TradePolicySetEvent : TurnEvent
{
    public TradePolicySetEvent(int turnNumber, CountryId country, CountryId target, TradePolicy policy)
        : base(turnNumber, TurnPhase.Diplomacy)
    {
        Country = country;
        Target = target;
        Policy = policy;
    }

    public CountryId Country { get; }

    public CountryId Target { get; }

    public TradePolicy Policy { get; }
}

/// <summary>Records a trade policy order that changed nothing. It cost nothing either.</summary>
public sealed record TradePolicyRefusedEvent : TurnEvent
{
    public TradePolicyRefusedEvent(
        int turnNumber, CountryId country, CountryId target, TradePolicy policy, TradePolicyRefusal reason)
        : base(turnNumber, TurnPhase.Diplomacy)
    {
        Country = country;
        Target = target;
        Policy = policy;
        Reason = reason;
    }

    public CountryId Country { get; }

    public CountryId Target { get; }

    public TradePolicy Policy { get; }

    public TradePolicyRefusal Reason { get; }
}

/// <summary>
/// Records a standing policy cleared because the posts it needed were closed by a war
/// between Great Powers. A chosen rule; see <c>docs/formulas/diplomacy.md</c>.
/// </summary>
public sealed record TradePolicyClearedEvent : TurnEvent
{
    public TradePolicyClearedEvent(int turnNumber, CountryId country, CountryId target, TradePolicy previous)
        : base(turnNumber, TurnPhase.Diplomacy)
    {
        Country = country;
        Target = target;
        Previous = previous;
    }

    /// <summary>The Great Power whose policy it was.</summary>
    public CountryId Country { get; }

    public CountryId Target { get; }

    /// <summary>The policy that stood before the war cleared it.</summary>
    public TradePolicy Previous { get; }
}

internal sealed record PlannedTradePolicy(
    CountryId Country,
    CountryId Target,
    TradePolicy Policy,
    TradePolicyRefusal? Refusal);

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
    public DiplomacyPlan(
        IReadOnlyList<PlannedOverture> overtures,
        long[] cashClaimed,
        IReadOnlyList<PlannedTradePolicy> policies)
    {
        Overtures = overtures;
        CashClaimed = cashClaimed;
        Policies = policies;
    }

    /// <summary>Every trade policy order in resolution order, set or refused.</summary>
    public IReadOnlyList<PlannedTradePolicy> Policies { get; }

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

        return new DiplomacyPlan(planned, spent, PlanPolicies(state, orders));
    }

    /// <summary>
    /// Plans the trade policy orders against the posts open as the turn began, so a
    /// consulate bought this turn cannot carry a policy until next turn — the same way it
    /// cannot carry an embassy.
    /// </summary>
    private static IReadOnlyList<PlannedTradePolicy> PlanPolicies(WorldState state, TurnOrders orders)
    {
        var definition = state.Definition;
        var planned = new List<PlannedTradePolicy>();
        for (var index = 0; index < definition.Countries.Count; index++)
        {
            var country = new CountryId(index);
            var ordered = new HashSet<CountryId>();
            foreach (var order in orders[country].TradePolicies)
            {
                // Only a policy that stands counts against a second one: a refused order
                // changed nothing, so it must not stop a later order toward the same country.
                var refusal = RefusePolicy(state, country, order, ordered);
                if (refusal is null)
                {
                    _ = ordered.Add(order.Target);
                }

                planned.Add(new PlannedTradePolicy(country, order.Target, order.Policy, refusal));
            }
        }

        return planned;
    }

    private static TradePolicyRefusal? RefusePolicy(
        WorldState state, CountryId country, TradePolicyOrder order, HashSet<CountryId> ordered)
    {
        var definition = state.Definition;
        var target = order.Target;
        if ((uint)target.Value >= (uint)definition.Countries.Count)
        {
            return TradePolicyRefusal.NoSuchCountry;
        }

        if (target == country)
        {
            return TradePolicyRefusal.Itself;
        }

        if (!definition.Countries[country.Value].IsGreatPower)
        {
            return TradePolicyRefusal.NotAGreatPower;
        }

        if (ordered.Contains(target))
        {
            return TradePolicyRefusal.AlreadyOrderedThisTurn;
        }

        // A consulate or an embassy, open as the turn began. An embassy is only ever
        // bought on top of a consulate, but a scenario could author one alone.
        if (!state.HoldsDiplomaticTie(country, target, DiplomaticTie.Consulate) &&
            !state.HoldsDiplomaticTie(country, target, DiplomaticTie.Embassy))
        {
            return TradePolicyRefusal.NoPost;
        }

        if (order.Policy.Kind == TradePolicyKind.Subsidy &&
            !(definition.Diplomacy?.SubsidyPercents.Contains(order.Policy.Percent) ?? false))
        {
            return TradePolicyRefusal.PercentNotOffered;
        }

        return null;
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

        // After overtures, before declarations. A policy set now stands at once, but the
        // market was planned before any phase ran, so it is first felt next turn — "any
        // changes in diplomatic status take effect next turn" (lines 626-629).
        foreach (var policy in plan.Policies)
        {
            if (policy.Refusal is { } reason)
            {
                events.Add(new TradePolicyRefusedEvent(
                    turnNumber, policy.Country, policy.Target, policy.Policy, reason));
                continue;
            }

            state.SetTradePolicy(policy.Country, policy.Target, policy.Policy);
            events.Add(new TradePolicySetEvent(turnNumber, policy.Country, policy.Target, policy.Policy));
        }

        return events;
    }

    /// <summary>
    /// Cuts what a declaration of war breaks: a pact either way at once, and between two
    /// Great Powers their consulates and embassies either way. Returns the event, or null
    /// when there was nothing to cut.
    /// </summary>
    public static IReadOnlyList<TurnEvent> CutTiesForWar(
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

        var events = new List<TurnEvent>();
        if (pactBroken || postsClosed)
        {
            events.Add(new DiplomaticTiesCutEvent(turnNumber, country, target, pactBroken, postsClosed));
        }

        // A policy needs an open post (lines 3663-3667), so closing the posts clears it,
        // both ways. A chosen rule: the manual says the posts must be re-established after
        // a war and says nothing of the policies that stood on them. A war on a Minor
        // Nation closes no post and clears nothing; the war's own boycott covers it.
        if (postsClosed)
        {
            foreach (var (owner, host) in new[] { (country, target), (target, country) })
            {
                var previous = state.GetTradePolicy(owner, host);
                if (previous.Kind != TradePolicyKind.None)
                {
                    state.SetTradePolicy(owner, host, TradePolicy.None);
                    events.Add(new TradePolicyClearedEvent(turnNumber, owner, host, previous));
                }
            }
        }

        return events;
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
