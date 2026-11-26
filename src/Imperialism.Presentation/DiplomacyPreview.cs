using Imperialism.Content;
using Imperialism.Core;

namespace Imperialism.Presentation;

/// <summary>
/// What a drafted declaration of war would cut, from Core's <c>CutTiesForWar</c>.
/// </summary>
/// <param name="BreaksPact">A non-aggression pact, either way, is broken at once.</param>
/// <param name="ClosesPosts">
/// Both are Great Powers and a consulate or embassy stands either way: every one closes.
/// </param>
/// <param name="ClearsOurPolicy">Our subsidy or boycott toward them is cleared with the posts.</param>
/// <param name="ClearsTheirPolicy">Theirs toward us is cleared with the posts.</param>
/// <param name="BoycottsTrade">
/// The war, once in effect next turn, boycotts all trade between the pair (manual
/// 3715–3716). True whenever the declaration is accepted.
/// </param>
public sealed record WarEffectsView(
    bool BreaksPact,
    bool ClosesPosts,
    bool ClearsOurPolicy,
    bool ClearsTheirPolicy,
    bool BoycottsTrade);

/// <summary>
/// One scroll on one country: what it would charge and why Core would refuse it.
/// An overture not in the draft is judged as if it were added last.
/// </summary>
/// <param name="Price">
/// What the scroll costs in this world; null when the world prices no posts. Pact and
/// peace are free.
/// </param>
/// <param name="Warning">Core's refusal, or null when it would be carried out.</param>
public sealed record OvertureOptionView(
    DiplomaticOvertureKind Kind,
    long? Price,
    bool IsDrafted,
    DiplomaticOvertureRefusal? Warning)
{
    public bool IsAccepted => Warning is null;
}

/// <summary>A declaration of war on one country, drafted or not, and what it would cut.</summary>
/// <param name="Effects">Null when Core would refuse the declaration.</param>
public sealed record WarOptionView(
    bool IsDrafted,
    WarDeclarationRefusal? Warning,
    WarEffectsView? Effects)
{
    public bool IsAccepted => Warning is null;
}

/// <summary>
/// One other country as the Diplomacy screen shows it: standing, relation, posts, pact
/// and trade policy as the turn opens, and what each order on it would do.
/// </summary>
/// <param name="RelationMode">The mode recorded, which may not be in effect yet.</param>
/// <param name="IsAtWar">A war is in effect this turn.</param>
/// <param name="IsWarPending">War is declared and begins next turn.</param>
/// <param name="IsPeacePending">Peace is made and the war ends next turn.</param>
/// <param name="OurConsulate">We hold a consulate in them.</param>
/// <param name="TheirConsulate">They hold a consulate in us.</param>
/// <param name="OurPolicy">Our standing policy toward them as the turn opens.</param>
/// <param name="TheirPolicy">Their standing policy toward us.</param>
/// <param name="DraftedPolicy">The policy change drafted toward them, or null.</param>
/// <param name="DraftedPolicyWarning">Core's refusal of <paramref name="DraftedPolicy"/>.</param>
/// <param name="PolicyUnavailable">
/// Why no policy can be set toward them this turn, or null when one can.
/// A subsidy may still be refused for its percentage.
/// </param>
/// <param name="IsTradeBoycotted">No trade this turn: a war in effect, or a boycott either way.</param>
/// <param name="PeaceLiftsBoycott">
/// When peace with them is accepted, trade resumes as the war ends — unless a voluntary
/// boycott stands either way, counting the drafted policy. Posts closed by the war are
/// not restored.
/// </param>
public sealed record DiplomaticCountryView(
    string CountryKey,
    string CountryName,
    bool IsGreatPower,
    bool IsEliminated,
    CountryRelationMode RelationMode,
    bool IsAtWar,
    bool IsWarPending,
    bool IsPeacePending,
    bool OurConsulate,
    bool OurEmbassy,
    bool TheirConsulate,
    bool TheirEmbassy,
    bool OurPact,
    bool TheirPact,
    TradePolicy OurPolicy,
    TradePolicy TheirPolicy,
    TradePolicy? DraftedPolicy,
    TradePolicyRefusal? DraftedPolicyWarning,
    TradePolicyRefusal? PolicyUnavailable,
    bool IsTradeBoycotted,
    IReadOnlyList<OvertureOptionView> Overtures,
    WarOptionView War,
    bool PeaceLiftsBoycott);

/// <summary>A drafted overture in the order the treasury pays for it.</summary>
public sealed record DraftedOvertureView(
    string TargetKey,
    DiplomaticOvertureKind Kind,
    long CashCost,
    DiplomaticOvertureRefusal? Warning);

/// <summary>A drafted declaration of war in the order Core reads it.</summary>
public sealed record DraftedWarView(
    string TargetKey,
    WarDeclarationRefusal? Warning,
    WarEffectsView? Effects);

/// <summary>A drafted trade policy change.</summary>
public sealed record DraftedPolicyView(
    string TargetKey,
    TradePolicy Policy,
    TradePolicyRefusal? Warning);

/// <summary>
/// The Diplomacy and trade policies screens' numbers. <b>A preview</b>: a restatement of
/// Core's internal <c>DiplomacyPlanner</c> and <c>NavalPlanner.ResolveDiplomacy</c>,
/// never a refusal — every drafted order goes to Core as given.
/// </summary>
/// <remarks>
/// <para>
/// Overtures are paid in <c>Diplomacy</c>, the first phase, from the treasury less what
/// the Industry screen's training, University and Armoury claim — Core plans them against
/// <c>production.CashClaimed</c>. That earlier claim is
/// <see cref="ClaimedBeforeDiplomacy"/>; no drafted order makes one yet, so it is 0 here.
/// Bids, civilians and research each see <see cref="ClaimedByOvertures"/> gone.
/// </para>
/// <para>
/// Rivals submit nothing, so no Great Power offers peace back and every peace offered
/// to one is <see cref="DiplomaticOvertureRefusal.Declined"/>; a Minor Nation accepts.
/// No rival declares war this turn either.
/// </para>
/// </remarks>
/// <param name="CashAtOpen">The treasury as the turn opens.</param>
/// <param name="ClaimedBeforeDiplomacy">
/// Cash the Industry screen's drafted orders claim before overtures are paid.
/// </param>
/// <param name="ClaimedByOvertures">Cash the accepted overtures take.</param>
/// <param name="CashAfterOvertures">
/// What the treasury holds once both claims are paid: the figure the Diplomacy screen
/// shows, so no client script subtracts. Later phases (trade, civilians, research,
/// income) still move the treasury after it.
/// </param>
/// <param name="IsOffered">The world prices consulates and embassies.</param>
/// <param name="OrdersSetAside">
/// The drafting country is eliminated: Core sets every order aside before anything
/// resolves, so nothing here happens.
/// </param>
public sealed record DiplomacyPreview(
    long CashAtOpen,
    long ClaimedBeforeDiplomacy,
    long ClaimedByOvertures,
    long CashAfterOvertures,
    bool IsGreatPower,
    bool OrdersSetAside,
    bool IsOffered,
    IReadOnlyList<int> SubsidyPercents,
    IReadOnlyList<DiplomaticCountryView> Countries,
    IReadOnlyList<DraftedOvertureView> Overtures,
    IReadOnlyList<DraftedWarView> Declarations,
    IReadOnlyList<DraftedPolicyView> Policies);

/// <summary>
/// A turn-local restatement of the <c>Diplomacy</c> phase for one country: overtures,
/// then policies, then declarations, each in list order, on an overlay that never
/// touches the <see cref="WorldState"/>.
/// </summary>
internal sealed class DiplomacySimulation
{
    private readonly WorldState _state;
    private readonly CountryId _country;
    private readonly HashSet<(CountryId Host, DiplomaticTie Tie)> _granted = [];
    private readonly HashSet<(CountryId Owner, CountryId Host, DiplomaticTie Tie)> _removed = [];
    private readonly Dictionary<(CountryId Grantor, CountryId Target), TradePolicy> _policies = [];
    private readonly HashSet<CountryId> _peace = [];
    private readonly HashSet<CountryId> _declared = [];

    private DiplomacySimulation(WorldState state, CountryId country)
    {
        _state = state;
        _country = country;
    }

    public List<(DiplomaticOverture Order, long Cost, DiplomaticOvertureRefusal? Refusal)> Overtures { get; } = [];

    public List<(TradePolicyOrder Order, TradePolicyRefusal? Refusal)> Policies { get; } = [];

    public List<(CountryId Target, WarDeclarationRefusal? Refusal, WarEffectsView? Effects)> Declarations { get; } = [];

    /// <summary>Cash the accepted overtures take.</summary>
    public long CashClaimed { get; private set; }

    public static DiplomacySimulation Run(
        WorldState state,
        CountryId country,
        IEnumerable<DiplomaticOverture> overtures,
        IReadOnlyList<CountryId> declarations,
        IEnumerable<TradePolicyOrder> policies,
        long claimedBefore)
    {
        var simulation = new DiplomacySimulation(state, country);
        simulation.RunOvertures(overtures, declarations, claimedBefore);
        simulation.RunPolicies(policies);
        simulation.RunDeclarations(declarations);
        return simulation;
    }

    /// <summary>The policy we hold toward a country once this turn's phase has run.</summary>
    public TradePolicy PolicyAfter(CountryId grantor, CountryId target) =>
        _policies.TryGetValue((grantor, target), out var policy) ? policy : _state.GetTradePolicy(grantor, target);

    private void RunOvertures(
        IEnumerable<DiplomaticOverture> overtures, IReadOnlyList<CountryId> declarations, long claimedBefore)
    {
        var settings = _state.Definition.Diplomacy;

        // Core's DiplomacyPlanner: the treasury less what production claimed first.
        var available = Math.Max(0, _state.GetCash(_country) - claimedBefore);
        var ordered = new HashSet<(DiplomaticOvertureKind, CountryId)>();
        foreach (var overture in overtures)
        {
            var cost = 0L;
            var refusal = Refuse(overture, ordered, declarations);
            if (refusal is null)
            {
                cost = Cost(settings, overture.Kind);
                if (cost > available - CashClaimed)
                {
                    refusal = DiplomaticOvertureRefusal.NotEnoughCash;
                    cost = 0;
                }
            }

            _ = ordered.Add((overture.Kind, overture.Target));
            if (refusal is null)
            {
                CashClaimed += cost;
                switch (overture.Kind)
                {
                    case DiplomaticOvertureKind.Consulate:
                        _ = _granted.Add((overture.Target, DiplomaticTie.Consulate));
                        break;
                    case DiplomaticOvertureKind.Embassy:
                        _ = _granted.Add((overture.Target, DiplomaticTie.Embassy));
                        break;
                    case DiplomaticOvertureKind.Pact:
                        _ = _granted.Add((overture.Target, DiplomaticTie.Pact));
                        break;
                    default:
                        _ = _peace.Add(overture.Target);
                        break;
                }
            }

            Overtures.Add((overture, cost, refusal));
        }
    }

    private DiplomaticOvertureRefusal? Refuse(
        DiplomaticOverture overture,
        HashSet<(DiplomaticOvertureKind, CountryId)> ordered,
        IReadOnlyList<CountryId> declarations)
    {
        var world = _state.Definition;
        var target = overture.Target;
        if ((uint)target.Value >= (uint)world.Countries.Count)
        {
            return DiplomaticOvertureRefusal.NoSuchCountry;
        }

        if (target == _country)
        {
            return DiplomaticOvertureRefusal.Itself;
        }

        if (_state.IsEliminated(target))
        {
            return DiplomaticOvertureRefusal.CountryEliminated;
        }

        if (!world.Countries[_country.Value].IsGreatPower)
        {
            return DiplomaticOvertureRefusal.NotAGreatPower;
        }

        if (ordered.Contains((overture.Kind, target)))
        {
            return DiplomaticOvertureRefusal.AlreadyOrderedThisTurn;
        }

        var targetIsGreatPower = world.Countries[target.Value].IsGreatPower;
        if (overture.Kind == DiplomaticOvertureKind.Peace)
        {
            if (_peace.Contains(target))
            {
                return DiplomaticOvertureRefusal.AlreadyMadeThisTurn;
            }

            if (!_state.HasEffectiveHostility(_country, target))
            {
                return DiplomaticOvertureRefusal.NotAtWar;
            }

            // A Great Power accepts only peace it offers back the same turn, and a rival
            // drafts nothing; a Minor Nation never refuses.
            return targetIsGreatPower ? DiplomaticOvertureRefusal.Declined : null;
        }

        if (_state.GetRelationMode(_country, target) == CountryRelationMode.Hostile ||
            declarations.Contains(target))
        {
            return DiplomaticOvertureRefusal.AtWar;
        }

        // Ties are read as the turn opened: an embassy needs a consulate already open,
        // not one bought earlier in the same list.
        switch (overture.Kind)
        {
            case DiplomaticOvertureKind.Consulate:
                if (world.Diplomacy is null)
                {
                    return DiplomaticOvertureRefusal.NotOffered;
                }

                return _state.HoldsDiplomaticTie(_country, target, DiplomaticTie.Consulate)
                    ? DiplomaticOvertureRefusal.AlreadyEstablished
                    : null;
            case DiplomaticOvertureKind.Embassy:
                if (world.Diplomacy is null)
                {
                    return DiplomaticOvertureRefusal.NotOffered;
                }

                if (_state.HoldsDiplomaticTie(_country, target, DiplomaticTie.Embassy))
                {
                    return DiplomaticOvertureRefusal.AlreadyEstablished;
                }

                return _state.HoldsDiplomaticTie(_country, target, DiplomaticTie.Consulate)
                    ? null
                    : DiplomaticOvertureRefusal.NoConsulate;
            case DiplomaticOvertureKind.Pact:
                if (targetIsGreatPower)
                {
                    return DiplomaticOvertureRefusal.TargetIsAGreatPower;
                }

                if (_state.HoldsDiplomaticTie(_country, target, DiplomaticTie.Pact))
                {
                    return DiplomaticOvertureRefusal.AlreadyEstablished;
                }

                return _state.HoldsDiplomaticTie(_country, target, DiplomaticTie.Embassy)
                    ? null
                    : DiplomaticOvertureRefusal.NoEmbassy;
            default:
                throw new ArgumentOutOfRangeException(nameof(overture));
        }
    }

    internal static long Cost(DiplomacySettings? settings, DiplomaticOvertureKind kind) => kind switch
    {
        DiplomaticOvertureKind.Consulate => settings?.ConsulateCost ?? 0,
        DiplomaticOvertureKind.Embassy => settings?.EmbassyCost ?? 0,
        _ => 0,
    };

    private void RunPolicies(IEnumerable<TradePolicyOrder> policies)
    {
        var ordered = new HashSet<CountryId>();
        foreach (var order in policies)
        {
            var refusal = RefusePolicy(_state, _country, order, ordered);
            if (refusal is null)
            {
                _ = ordered.Add(order.Target);
                _policies[(_country, order.Target)] = order.Policy;
            }

            Policies.Add((order, refusal));
        }
    }

    /// <summary>Core's <c>RefusePolicy</c>, planned against the posts open as the turn began.</summary>
    internal static TradePolicyRefusal? RefusePolicy(
        WorldState state, CountryId country, TradePolicyOrder order, IReadOnlySet<CountryId> ordered)
    {
        var world = state.Definition;
        var target = order.Target;
        if ((uint)target.Value >= (uint)world.Countries.Count)
        {
            return TradePolicyRefusal.NoSuchCountry;
        }

        if (target == country)
        {
            return TradePolicyRefusal.Itself;
        }

        if (state.IsEliminated(target))
        {
            return TradePolicyRefusal.CountryEliminated;
        }

        if (!world.Countries[country.Value].IsGreatPower)
        {
            return TradePolicyRefusal.NotAGreatPower;
        }

        if (ordered.Contains(target))
        {
            return TradePolicyRefusal.AlreadyOrderedThisTurn;
        }

        if (!state.HoldsDiplomaticTie(country, target, DiplomaticTie.Consulate) &&
            !state.HoldsDiplomaticTie(country, target, DiplomaticTie.Embassy))
        {
            return TradePolicyRefusal.NoPost;
        }

        if (order.Policy.Kind == TradePolicyKind.Subsidy &&
            !(world.Diplomacy?.SubsidyPercents.Contains(order.Policy.Percent) ?? false))
        {
            return TradePolicyRefusal.PercentNotOffered;
        }

        return null;
    }

    private void RunDeclarations(IReadOnlyList<CountryId> declarations)
    {
        var count = _state.Definition.Countries.Count;
        foreach (var target in declarations)
        {
            WarDeclarationRefusal? refusal =
                target.Value >= count ? WarDeclarationRefusal.NoSuchCountry
                : target == _country ? WarDeclarationRefusal.Itself
                : _state.IsEliminated(target) ? WarDeclarationRefusal.CountryEliminated
                : _declared.Contains(target) ? WarDeclarationRefusal.AlreadyDeclaredThisTurn
                : ModeNow(target) == CountryRelationMode.Hostile ? WarDeclarationRefusal.AlreadyAtWar
                : null;
            if (refusal is not null)
            {
                Declarations.Add((target, refusal, null));
                continue;
            }

            _ = _declared.Add(target);
            Declarations.Add((target, null, CutTies(target)));
        }
    }

    /// <summary>
    /// The recorded mode as the phase reaches the declarations: peace accepted earlier
    /// in it has set the pair to Standard, so a war declared after it is accepted.
    /// </summary>
    private CountryRelationMode ModeNow(CountryId target) =>
        _peace.Contains(target) ? CountryRelationMode.Standard : _state.GetRelationMode(_country, target);

    private bool Holds(CountryId owner, CountryId host, DiplomaticTie tie) =>
        !_removed.Contains((owner, host, tie)) &&
        (_state.HoldsDiplomaticTie(owner, host, tie) || owner == _country && _granted.Contains((host, tie)));

    private WarEffectsView CutTies(CountryId target)
    {
        var pact = Remove(_country, target, DiplomaticTie.Pact) | Remove(target, _country, DiplomaticTie.Pact);
        var posts = false;
        var countries = _state.Definition.Countries;
        if (countries[_country.Value].IsGreatPower && countries[target.Value].IsGreatPower)
        {
            foreach (var tie in new[] { DiplomaticTie.Consulate, DiplomaticTie.Embassy })
            {
                posts |= Remove(_country, target, tie);
                posts |= Remove(target, _country, tie);
            }
        }

        var ours = false;
        var theirs = false;
        if (posts)
        {
            ours = Clear(_country, target);
            theirs = Clear(target, _country);
        }

        return new WarEffectsView(pact, posts, ours, theirs, BoycottsTrade: true);
    }

    private bool Remove(CountryId owner, CountryId host, DiplomaticTie tie)
    {
        if (!Holds(owner, host, tie))
        {
            return false;
        }

        _ = _removed.Add((owner, host, tie));
        return true;
    }

    private bool Clear(CountryId owner, CountryId host)
    {
        if (PolicyAfter(owner, host).Kind == TradePolicyKind.None)
        {
            return false;
        }

        _policies[(owner, host)] = TradePolicy.None;
        return true;
    }
}

public sealed partial class OrdersPreview
{
    /// <summary>The Diplomacy and trade policies screens' preview.</summary>
    public DiplomacyPreview Diplomacy { get; private init; } = null!;

    private static DiplomacySimulation SimulateDiplomacy(WorldState state, OrdersDraft draft) =>
        DiplomacySimulation.Run(
            state,
            draft.Country,
            draft.Overtures,
            draft.DeclareWar,
            draft.TradePolicies,
            ClaimedBeforeDiplomacy(state, draft));

    /// <summary>
    /// Cash the drafted Industry-screen orders claim before <c>Diplomacy</c> pays any
    /// overture — Core's <c>production.CashClaimed</c>. No drafted order claims cash
    /// there yet, so it is 0; the Armoury, training and University add to it.
    /// </summary>
    private static long ClaimedBeforeDiplomacy(WorldState state, OrdersDraft draft) => 0;

    /// <summary>Cash the drafted overtures take in <c>Diplomacy</c>, ahead of everything else.</summary>
    private static long ClaimedByOvertures(WorldState state, OrdersDraft draft) =>
        SimulateDiplomacy(state, draft).CashClaimed;

    private static DiplomacyPreview PreviewDiplomacy(WorldState state, OrdersDraft draft, Names names)
    {
        var world = state.Definition;
        var country = draft.Country;
        var settings = world.Diplomacy;
        var claimedBefore = ClaimedBeforeDiplomacy(state, draft);
        var drafted = SimulateDiplomacy(state, draft);

        var countries = new List<DiplomaticCountryView>();
        foreach (var other in world.Countries)
        {
            var target = other.Id;
            if (target == country)
            {
                continue;
            }

            var overtures = new List<OvertureOptionView>();
            foreach (var kind in Enum.GetValues<DiplomaticOvertureKind>())
            {
                var overture = new DiplomaticOverture(kind, target);
                var index = drafted.Overtures.FindIndex(row => row.Order == overture);
                DiplomaticOvertureRefusal? warning;
                if (index >= 0)
                {
                    warning = drafted.Overtures[index].Refusal;
                }
                else
                {
                    var appended = DiplomacySimulation.Run(
                        state, country, draft.Overtures.Append(overture), draft.DeclareWar, [], claimedBefore);
                    warning = appended.Overtures[^1].Refusal;
                }

                var price = kind is DiplomaticOvertureKind.Consulate or DiplomaticOvertureKind.Embassy && settings is null
                    ? (long?)null
                    : DiplomacySimulation.Cost(settings, kind);
                overtures.Add(new OvertureOptionView(kind, price, index >= 0, warning));
            }

            var warIndex = drafted.Declarations.FindIndex(row => row.Target == target);
            var war = warIndex >= 0
                ? drafted.Declarations[warIndex]
                : DiplomacySimulation.Run(
                    state, country, draft.Overtures, [.. draft.DeclareWar, target], draft.TradePolicies, claimedBefore)
                    .Declarations[^1];

            var draftedPolicy = draft.GetTradePolicy(target);
            var policyIndex = drafted.Policies.FindIndex(row => row.Order.Target == target);
            var unavailable = DiplomacySimulation.RefusePolicy(
                state, country, new TradePolicyOrder(target, TradePolicy.Boycott), new HashSet<CountryId>());

            var ours = drafted.PolicyAfter(country, target);
            var theirs = state.GetTradePolicy(target, country);
            var recorded = state.GetRelationMode(country, target);
            var effective = state.HasEffectiveHostility(country, target);
            countries.Add(new DiplomaticCountryView(
                names.Key(target),
                other.Name,
                other.IsGreatPower,
                state.IsEliminated(target),
                recorded,
                effective,
                recorded == CountryRelationMode.Hostile && !effective,
                recorded != CountryRelationMode.Hostile && effective,
                state.HoldsDiplomaticTie(country, target, DiplomaticTie.Consulate),
                state.HoldsDiplomaticTie(country, target, DiplomaticTie.Embassy),
                state.HoldsDiplomaticTie(target, country, DiplomaticTie.Consulate),
                state.HoldsDiplomaticTie(target, country, DiplomaticTie.Embassy),
                state.HoldsDiplomaticTie(country, target, DiplomaticTie.Pact),
                state.HoldsDiplomaticTie(target, country, DiplomaticTie.Pact),
                state.GetTradePolicy(country, target),
                theirs,
                draftedPolicy,
                policyIndex >= 0 ? drafted.Policies[policyIndex].Refusal : null,
                unavailable,
                state.IsTradeBoycotted(country, target),
                Array.AsReadOnly(overtures.ToArray()),
                new WarOptionView(warIndex >= 0, war.Refusal, war.Effects),
                ours.Kind != TradePolicyKind.Boycott && theirs.Kind != TradePolicyKind.Boycott));
        }

        var cashAtOpen = state.GetCash(country);
        return new DiplomacyPreview(
            cashAtOpen,
            claimedBefore,
            drafted.CashClaimed,
            cashAtOpen - claimedBefore - drafted.CashClaimed,
            world.Countries[country.Value].IsGreatPower,
            state.IsEliminated(country),
            settings is not null,
            settings?.SubsidyPercents ?? [],
            Array.AsReadOnly(countries.ToArray()),
            Array.AsReadOnly(drafted.Overtures
                .Select(row => new DraftedOvertureView(
                    names.Key(row.Order.Target), row.Order.Kind, row.Cost, row.Refusal))
                .ToArray()),
            Array.AsReadOnly(drafted.Declarations
                .Select(row => new DraftedWarView(names.Key(row.Target), row.Refusal, row.Effects))
                .ToArray()),
            Array.AsReadOnly(drafted.Policies
                .Select(row => new DraftedPolicyView(names.Key(row.Order.Target), row.Order.Policy, row.Refusal))
                .ToArray()));
    }
}
