namespace Imperialism.Core;

/// <summary>
/// Records one Armoury order paid for. The workers have left the workforce for good;
/// the regiments stand in the capital's province from <c>Delivery</c>.
/// </summary>
public sealed record RegimentsPurchasedEvent : TurnEvent
{
    private readonly IReadOnlyList<CommodityQuantity> _paid;

    public RegimentsPurchasedEvent(
        int turnNumber,
        CountryId country,
        ArmyCategory category,
        ArmyTypeId type,
        long count,
        WorkerGrade workerGrade,
        long workersTaken,
        IEnumerable<CommodityQuantity> paid,
        long cashPaid)
        : base(turnNumber, TurnPhase.Production)
    {
        ArgumentNullException.ThrowIfNull(paid);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        ArgumentOutOfRangeException.ThrowIfNegative(workersTaken);
        ArgumentOutOfRangeException.ThrowIfNegative(cashPaid);
        Country = country;
        Category = category;
        Type = type;
        Count = count;
        WorkerGrade = workerGrade;
        WorkersTaken = workersTaken;
        _paid = Array.AsReadOnly(paid.ToArray());
        CashPaid = cashPaid;
    }

    public CountryId Country { get; }

    public ArmyCategory Category { get; }

    /// <summary>The category's best type known as the turn opened.</summary>
    public ArmyTypeId Type { get; }

    public long Count { get; }

    public WorkerGrade WorkerGrade { get; }

    /// <summary>Workers of <see cref="WorkerGrade"/> taken for good, for all of them.</summary>
    public long WorkersTaken { get; }

    /// <summary>Commodities charged, for all of them together.</summary>
    public IReadOnlyList<CommodityQuantity> Paid => _paid;

    /// <summary>Cash charged, for all of them together.</summary>
    public long CashPaid { get; }
}

/// <summary>Why the Armoury did not build an order. Every refusal charges nothing.</summary>
public enum RegimentPurchaseRefusal : byte
{
    /// <summary>The order asks for no regiments, or fewer than none.</summary>
    NonPositiveCount,

    /// <summary>
    /// The category has no type the Armoury builds: militia, which "is not built in the
    /// Armory", the Generals, or any category in a package older than version 32.
    /// </summary>
    NotPurchasable,

    /// <summary>
    /// The country holds no capital, so there is nowhere for a new regiment to appear:
    /// new units "start their first turn … in the province of the capital".
    /// </summary>
    NoCapital,

    /// <summary>Too few healthy workers of the type's grade for every regiment in the order.</summary>
    NotEnoughWorkers,

    /// <summary>The warehouse does not hold the whole bill.</summary>
    NotEnoughCommodities,

    /// <summary>The treasury does not hold the whole bill.</summary>
    NotEnoughCash,
}

/// <summary>Records one Armoury order being refused. Nothing was charged.</summary>
public sealed record RegimentPurchaseRefusedEvent : TurnEvent
{
    public RegimentPurchaseRefusedEvent(
        int turnNumber,
        CountryId country,
        ArmyCategory category,
        long requested,
        RegimentPurchaseRefusal reason)
        : base(turnNumber, TurnPhase.Production)
    {
        Country = country;
        Category = category;
        Requested = requested;
        Reason = reason;
    }

    public CountryId Country { get; }

    public ArmyCategory Category { get; }

    public long Requested { get; }

    public RegimentPurchaseRefusal Reason { get; }
}

/// <summary>
/// Records regiments bought this turn arriving in the capital's province. They may be
/// ordered to move from next turn.
/// </summary>
public sealed record RegimentsDeliveredEvent : TurnEvent
{
    private readonly IReadOnlyList<RegimentId> _regiments;

    public RegimentsDeliveredEvent(
        int turnNumber,
        CountryId country,
        ArmyTypeId type,
        ProvinceId province,
        IEnumerable<RegimentId> regiments)
        : base(turnNumber, TurnPhase.Delivery)
    {
        ArgumentNullException.ThrowIfNull(regiments);
        Country = country;
        Type = type;
        Province = province;
        _regiments = Array.AsReadOnly(regiments.ToArray());
        if (_regiments.Count == 0)
        {
            throw new ArgumentException("A delivery delivers somebody.", nameof(regiments));
        }
    }

    public CountryId Country { get; }

    public ArmyTypeId Type { get; }

    /// <summary>The capital's province, where every new regiment stands.</summary>
    public ProvinceId Province { get; }

    public IReadOnlyList<RegimentId> Regiments => _regiments;
}

/// <summary>
/// Records regiments bought this turn that never arrived, because the country held no
/// capital by the time they were due. Nothing is refunded — a chosen rule.
/// </summary>
public sealed record RegimentDeliveryLostEvent : TurnEvent
{
    public RegimentDeliveryLostEvent(int turnNumber, CountryId country, ArmyTypeId type, long count)
        : base(turnNumber, TurnPhase.Delivery)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        Country = country;
        Type = type;
        Count = count;
    }

    public CountryId Country { get; }

    public ArmyTypeId Type { get; }

    public long Count { get; }
}

/// <summary>How a deployment travelled.</summary>
public enum RegimentMoveMode : byte
{
    /// <summary>To an adjacent province, on foot or horse. Free.</summary>
    March,

    /// <summary>Further, along an unbroken line of the country's own rail.</summary>
    Rail,
}

/// <summary>Records one deployment carried out.</summary>
public sealed record RegimentsMovedEvent : TurnEvent
{
    private readonly IReadOnlyList<RegimentId> _regiments;

    public RegimentsMovedEvent(
        int turnNumber,
        CountryId country,
        IEnumerable<RegimentId> regiments,
        ProvinceId from,
        ProvinceId to,
        RegimentMoveMode mode,
        long capacityUsed)
        : base(turnNumber, TurnPhase.Conflict)
    {
        ArgumentNullException.ThrowIfNull(regiments);
        ArgumentOutOfRangeException.ThrowIfNegative(capacityUsed);
        if (mode == RegimentMoveMode.March && capacityUsed != 0)
        {
            throw new ArgumentException("A march uses no transport capacity.", nameof(capacityUsed));
        }

        Country = country;
        _regiments = Array.AsReadOnly(regiments.ToArray());
        if (_regiments.Count == 0)
        {
            throw new ArgumentException("A move moves somebody.", nameof(regiments));
        }

        From = from;
        To = to;
        Mode = mode;
        CapacityUsed = capacityUsed;
    }

    public CountryId Country { get; }

    public IReadOnlyList<RegimentId> Regiments => _regiments;

    public ProvinceId From { get; }

    public ProvinceId To { get; }

    public RegimentMoveMode Mode { get; }

    /// <summary>
    /// Transport capacity spent: five per armaments point by rail, nothing on a march.
    /// Drawn from the regiment rail budget, never from commodity transport.
    /// </summary>
    public long CapacityUsed { get; }
}

/// <summary>Why a deployment was refused. A refused order moves nobody.</summary>
public enum RegimentMoveRefusal : byte
{
    /// <summary>The order names no regiments.</summary>
    NoRegiments,

    /// <summary>The order names a regiment twice.</summary>
    DuplicateRegiment,

    /// <summary>The order names a regiment that does not exist.</summary>
    NoSuchRegiment,

    /// <summary>The order names another country's regiment.</summary>
    NotYours,

    /// <summary>A regiment named has already moved this turn: one move per regiment per turn.</summary>
    AlreadyMoved,

    /// <summary>The regiments named do not all stand in one province.</summary>
    NotTogether,

    /// <summary>
    /// A regiment named is militia, which "cannot be ordered to leave their home
    /// province" — refused on a march as well as by rail.
    /// </summary>
    CannotLeaveProvince,

    /// <summary>The destination is not a province on this map.</summary>
    UnknownProvince,

    /// <summary>The destination is where the regiments already stand.</summary>
    SameProvince,

    /// <summary>
    /// Retired at row 6b and never issued now: a destination that was not this country's
    /// as the turn opened is an attack, not a refused move. Kept so no later value moves.
    /// </summary>
    NotOwnProvince,

    /// <summary>
    /// Not adjacent, and the two provinces share no component of the country's own rail.
    /// </summary>
    NotConnectedByRail,

    /// <summary>A regiment named cannot travel by rail.</summary>
    CannotMoveByRail,

    /// <summary>Five capacity points per armaments point is more than the rail budget has left.</summary>
    NotEnoughRailCapacity,

    /// <summary>
    /// An attack on a province that does not border the regiments' own: attacks "may be
    /// made only from an adjacent province" (manual 1765–1770). There is no attack by rail.
    /// </summary>
    NotAdjacent,

    /// <summary>
    /// An attack on a province that is already this country's when the attack comes to
    /// resolve — taken earlier the same turn, for instance.
    /// </summary>
    AlreadyOwn,

    /// <summary>
    /// An attack on a country with no war in effect as the turn opened, which includes a
    /// war declared this turn. A province that changed hands earlier this turn is judged
    /// by its new owner, so "the invasion does not take place" (manual 3798–3817).
    /// </summary>
    NotAtWar,
}

/// <summary>Records one deployment being refused. Nobody moved.</summary>
public sealed record RegimentMoveRefusedEvent : TurnEvent
{
    private readonly IReadOnlyList<RegimentId> _regiments;

    public RegimentMoveRefusedEvent(
        int turnNumber,
        CountryId country,
        IEnumerable<RegimentId> regiments,
        ProvinceId destination,
        RegimentMoveRefusal reason)
        : base(turnNumber, TurnPhase.Conflict)
    {
        ArgumentNullException.ThrowIfNull(regiments);
        Country = country;
        _regiments = Array.AsReadOnly(regiments.ToArray());
        Destination = destination;
        Reason = reason;
    }

    public CountryId Country { get; }

    public IReadOnlyList<RegimentId> Regiments => _regiments;

    public ProvinceId Destination { get; }

    public RegimentMoveRefusal Reason { get; }
}
