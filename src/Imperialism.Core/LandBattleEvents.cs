namespace Imperialism.Core;

/// <summary>Which side of a land battle a regiment fought on.</summary>
public enum BattleSide : byte
{
    Attacker,
    Defender,
}

/// <summary>One regiment's part in a land battle.</summary>
/// <param name="ExperienceGained">In hundredths: 35 for the winning side, 20 for the losing one, less whatever the ceiling of 400 took.</param>
public sealed record BattleParticipant(
    RegimentId Regiment,
    ArmyTypeId Type,
    BattleSide Side,
    long HealthBefore,
    long HealthAfter,
    long ExperienceGained);

/// <summary>
/// Records one strategic land battle, resolved headlessly. The loss rule behind it is a
/// labelled placeholder until the executable's own is traced; see
/// <c>docs/formulas/army.md</c>.
/// </summary>
public sealed record BattleFoughtEvent : TurnEvent
{
    private readonly IReadOnlyList<BattleParticipant> _participants;
    private readonly IReadOnlyList<RegimentId> _killed;

    public BattleFoughtEvent(
        int turnNumber,
        ProvinceId province,
        CountryId attacker,
        CountryId defender,
        IEnumerable<BattleParticipant> participants,
        IEnumerable<RegimentId> killed,
        CountryId winner,
        int rounds)
        : base(turnNumber, TurnPhase.Conflict)
    {
        ArgumentNullException.ThrowIfNull(participants);
        ArgumentNullException.ThrowIfNull(killed);
        ArgumentOutOfRangeException.ThrowIfNegative(rounds);
        if (attacker == defender)
        {
            throw new ArgumentException("A battle needs two countries.", nameof(defender));
        }

        if (winner != attacker && winner != defender)
        {
            throw new ArgumentException("The winner is one of the two sides.", nameof(winner));
        }

        Province = province;
        Attacker = attacker;
        Defender = defender;
        _participants = Array.AsReadOnly(participants.ToArray());
        _killed = Array.AsReadOnly(killed.ToArray());
        Winner = winner;
        Rounds = rounds;
    }

    public ProvinceId Province { get; }

    public CountryId Attacker { get; }

    public CountryId Defender { get; }

    /// <summary>Attackers then defenders, each in regiment-id order.</summary>
    public IReadOnlyList<BattleParticipant> Participants => _participants;

    /// <summary>Regiments that ended the battle at health 0 and were removed.</summary>
    public IReadOnlyList<RegimentId> Killed => _killed;

    public CountryId Winner { get; }

    /// <summary>Rounds fought; at most twenty.</summary>
    public int Rounds { get; }
}

/// <summary>
/// Records a province changing hands in a land attack: won in battle, or taken
/// undefended.
/// </summary>
public sealed record ProvinceCapturedEvent : TurnEvent
{
    private readonly IReadOnlyList<RegimentId> _regiments;
    private readonly IReadOnlyList<CivilianUnitId> _civiliansKilled;

    public ProvinceCapturedEvent(
        int turnNumber,
        ProvinceId province,
        CountryId? previousOwner,
        CountryId newOwner,
        bool uncontested,
        IEnumerable<RegimentId> regiments,
        IEnumerable<CivilianUnitId> civiliansKilled,
        bool capitalLost)
        : base(turnNumber, TurnPhase.Conflict)
    {
        ArgumentNullException.ThrowIfNull(regiments);
        ArgumentNullException.ThrowIfNull(civiliansKilled);
        if (previousOwner == newOwner)
        {
            throw new ArgumentException("A capture changes the owner.", nameof(newOwner));
        }

        if (capitalLost && previousOwner is null)
        {
            throw new ArgumentException("Only an owner has a capital to lose.", nameof(capitalLost));
        }

        Province = province;
        PreviousOwner = previousOwner;
        NewOwner = newOwner;
        Uncontested = uncontested;
        _regiments = Array.AsReadOnly(regiments.ToArray());
        _civiliansKilled = Array.AsReadOnly(civiliansKilled.ToArray());
        CapitalLost = capitalLost;
    }

    public ProvinceId Province { get; }

    /// <summary>Null when the province had no owner.</summary>
    public CountryId? PreviousOwner { get; }

    public CountryId NewOwner { get; }

    /// <summary>True when nobody defended it and no battle was fought.</summary>
    public bool Uncontested { get; }

    /// <summary>The attacking regiments that moved in.</summary>
    public IReadOnlyList<RegimentId> Regiments => _regiments;

    /// <summary>The previous owner's civilians on the province, killed (manual 1162–1164).</summary>
    public IReadOnlyList<CivilianUnitId> CiviliansKilled => _civiliansKilled;

    /// <summary>
    /// The province held the previous owner's capital, which is now cleared. Elimination
    /// and anarchy are not modelled: the country keeps its other provinces.
    /// </summary>
    public bool CapitalLost { get; }
}

/// <summary>Records a lost province's surviving defenders falling back to a neighbour their owner still holds.</summary>
public sealed record RegimentsRetreatedEvent : TurnEvent
{
    private readonly IReadOnlyList<RegimentId> _regiments;

    public RegimentsRetreatedEvent(
        int turnNumber,
        CountryId country,
        IEnumerable<RegimentId> regiments,
        ProvinceId from,
        ProvinceId to)
        : base(turnNumber, TurnPhase.Conflict)
    {
        ArgumentNullException.ThrowIfNull(regiments);
        Country = country;
        _regiments = Array.AsReadOnly(regiments.ToArray());
        if (_regiments.Count == 0)
        {
            throw new ArgumentException("A retreat moves somebody.", nameof(regiments));
        }

        From = from;
        To = to;
    }

    public CountryId Country { get; }

    public IReadOnlyList<RegimentId> Regiments => _regiments;

    public ProvinceId From { get; }

    public ProvinceId To { get; }
}

/// <summary>
/// Records a lost province's surviving defenders surrendering, because their owner holds
/// no neighbouring province to fall back to. They are removed.
/// </summary>
public sealed record RegimentsSurrenderedEvent : TurnEvent
{
    private readonly IReadOnlyList<RegimentId> _regiments;

    public RegimentsSurrenderedEvent(
        int turnNumber,
        CountryId country,
        IEnumerable<RegimentId> regiments,
        ProvinceId province)
        : base(turnNumber, TurnPhase.Conflict)
    {
        ArgumentNullException.ThrowIfNull(regiments);
        Country = country;
        _regiments = Array.AsReadOnly(regiments.ToArray());
        if (_regiments.Count == 0)
        {
            throw new ArgumentException("A surrender gives up somebody.", nameof(regiments));
        }

        Province = province;
    }

    public CountryId Country { get; }

    public IReadOnlyList<RegimentId> Regiments => _regiments;

    public ProvinceId Province { get; }
}

/// <summary>
/// Records one country's regiments recovering at the end of <c>Conflict</c>: +100 each
/// to a ceiling of 500, for every regiment named in no accepted move or attack.
/// </summary>
public sealed record RegimentsHealedEvent : TurnEvent
{
    private readonly IReadOnlyList<RegimentId> _regiments;

    public RegimentsHealedEvent(
        int turnNumber,
        CountryId country,
        IEnumerable<RegimentId> regiments,
        long healthRestored)
        : base(turnNumber, TurnPhase.Conflict)
    {
        ArgumentNullException.ThrowIfNull(regiments);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(healthRestored);
        Country = country;
        _regiments = Array.AsReadOnly(regiments.ToArray());
        if (_regiments.Count == 0)
        {
            throw new ArgumentException("Healing heals somebody.", nameof(regiments));
        }

        HealthRestored = healthRestored;
    }

    public CountryId Country { get; }

    public IReadOnlyList<RegimentId> Regiments => _regiments;

    /// <summary>Health restored across all of them.</summary>
    public long HealthRestored { get; }
}

/// <summary>
/// Records regiments carried across water from a landing site, before the land battle (if
/// any) for the province. <see cref="ArmsUsed"/> is what they took of the site's capacity
/// and <see cref="CapacityLeft"/> what remains for later landings through it this turn.
/// </summary>
public sealed record RegimentsLandedEvent : TurnEvent
{
    private readonly IReadOnlyList<RegimentId> _regiments;

    public RegimentsLandedEvent(
        int turnNumber,
        CountryId country,
        TaskForceId taskForce,
        IEnumerable<RegimentId> regiments,
        ProvinceId from,
        ProvinceId destination,
        long armsUsed,
        long capacityLeft)
        : base(turnNumber, TurnPhase.Conflict)
    {
        ArgumentNullException.ThrowIfNull(regiments);
        ArgumentOutOfRangeException.ThrowIfNegative(armsUsed);
        ArgumentOutOfRangeException.ThrowIfNegative(capacityLeft);
        _regiments = Array.AsReadOnly(regiments.ToArray());
        if (_regiments.Count == 0)
        {
            throw new ArgumentException("A landing lands somebody.", nameof(regiments));
        }

        Country = country;
        TaskForce = taskForce;
        From = from;
        Destination = destination;
        ArmsUsed = armsUsed;
        CapacityLeft = capacityLeft;
    }

    public CountryId Country { get; }

    /// <summary>The task force holding the landing site.</summary>
    public TaskForceId TaskForce { get; }

    public IReadOnlyList<RegimentId> Regiments => _regiments;

    public ProvinceId From { get; }

    public ProvinceId Destination { get; }

    public long ArmsUsed { get; }

    public long CapacityLeft { get; }
}

/// <summary>
/// Records regiments drowned on the way to a landing, because the interception battle
/// sank the ships carrying them or broke the landing site. They are removed. A
/// <b>placeholder</b> until the executable's landing is traced; see
/// <c>docs/formulas/army.md</c>, rule 34.
/// </summary>
public sealed record RegimentsDrownedEvent : TurnEvent
{
    private readonly IReadOnlyList<RegimentId> _regiments;

    public RegimentsDrownedEvent(
        int turnNumber,
        CountryId country,
        TaskForceId taskForce,
        IEnumerable<RegimentId> regiments,
        ProvinceId destination)
        : base(turnNumber, TurnPhase.Conflict)
    {
        ArgumentNullException.ThrowIfNull(regiments);
        _regiments = Array.AsReadOnly(regiments.ToArray());
        if (_regiments.Count == 0)
        {
            throw new ArgumentException("A drowning drowns somebody.", nameof(regiments));
        }

        Country = country;
        TaskForce = taskForce;
        Destination = destination;
    }

    public CountryId Country { get; }

    public TaskForceId TaskForce { get; }

    public IReadOnlyList<RegimentId> Regiments => _regiments;

    public ProvinceId Destination { get; }
}
