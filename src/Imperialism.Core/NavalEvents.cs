namespace Imperialism.Core;

/// <summary>Why a declaration of war changed nothing.</summary>
/// <remarks>
/// "Unlike most overtures, a country cannot refuse a declaration of war", so none of
/// these is the target saying no. Each is an order that has no meaning to carry out.
/// </remarks>
public enum WarDeclarationRefusal : byte
{
    /// <summary>No country carries that id in this world.</summary>
    NoSuchCountry,

    /// <summary>A country cannot declare war on itself.</summary>
    Itself,

    /// <summary>
    /// The pair is already at war — including when the other side declared earlier in
    /// this same turn. Declaring again would restamp the war and put its start back a
    /// turn, which is why it is refused rather than repeated.
    /// </summary>
    AlreadyAtWar,

    /// <summary>The same target appears twice in this turn's list; the first one counted.</summary>
    AlreadyDeclaredThisTurn,
}

/// <summary>Records a declaration of war. The war begins next turn.</summary>
public sealed record WarDeclaredEvent : TurnEvent
{
    public WarDeclaredEvent(int turnNumber, CountryId country, CountryId target)
        : base(turnNumber, TurnPhase.Diplomacy)
    {
        Country = country;
        Target = target;
    }

    public CountryId Country { get; }

    public CountryId Target { get; }
}

/// <summary>Records a declaration of war that changed nothing.</summary>
public sealed record WarDeclarationRefusedEvent : TurnEvent
{
    public WarDeclarationRefusedEvent(
        int turnNumber, CountryId country, CountryId target, WarDeclarationRefusal reason)
        : base(turnNumber, TurnPhase.Diplomacy)
    {
        Country = country;
        Target = target;
        Reason = reason;
    }

    public CountryId Country { get; }

    public CountryId Target { get; }

    public WarDeclarationRefusal Reason { get; }
}

/// <summary>Which kind of naval order was refused.</summary>
public enum NavalOrderKind : byte
{
    /// <summary>A <see cref="NavalOrder.AssembleOrder"/>.</summary>
    Assemble,

    /// <summary>A <see cref="NavalOrder.CommandOrder"/>.</summary>
    Command,
}

/// <summary>Why a naval order was not carried out. Every refusal changes nothing.</summary>
/// <remarks>
/// An assembly is all or nothing: if its mission would be refused, the fleets are not
/// grouped either. That is a chosen rule; see <c>docs/formulas/naval.md</c>.
/// </remarks>
public enum NavalOrderRefusal : byte
{
    /// <summary>An assembly selected no fleets.</summary>
    NoFleetsSelected,

    /// <summary>An assembly named the same fleet twice.</summary>
    FleetSelectedTwice,

    /// <summary>No fleet carries that id.</summary>
    NoSuchFleet,

    /// <summary>The fleet belongs to another country.</summary>
    FleetNotYours,

    /// <summary>
    /// The fleet has no sea zone — a newly built ship, which waits unplaced, or a legacy
    /// record naming a zone this map does not have.
    /// </summary>
    FleetNotPlaced,

    /// <summary>The fleet already belongs to a task force; there is no disband order yet.</summary>
    FleetAlreadyInTaskForce,

    /// <summary>A task force is assembled only from fleets lying in one sea zone.</summary>
    FleetsInDifferentSeaZones,

    /// <summary>No task force carries that id.</summary>
    NoSuchTaskForce,

    /// <summary>The task force belongs to another country.</summary>
    TaskForceNotYours,

    /// <summary>
    /// The task force already has its mission or move for this turn: "Fleets cannot do
    /// both a mission and a move in the same turn".
    /// </summary>
    AlreadyOrderedThisTurn,

    /// <summary>The move names a sea zone this map does not have.</summary>
    NoSuchSeaZone,

    /// <summary>
    /// The move names the zone the task force is already in. The Wheel Cursor "does not
    /// appear in the fleet's current sea zone".
    /// </summary>
    AlreadyInThatSeaZone,

    /// <summary>
    /// The move names a zone beyond the slowest selected hull's reach this turn, or one
    /// no sea path reaches: "you cannot move the fleet that far in one turn".
    /// </summary>
    OutOfRange,

    /// <summary>A blockade names a country this world does not have.</summary>
    NoSuchCountry,

    /// <summary>A country cannot blockade its own capital.</summary>
    BlockadeOwnCountry,

    /// <summary>The blockaded country holds no capital to blockade.</summary>
    TargetHasNoCapital,

    /// <summary>The target's capital does not touch the task force's present sea zone.</summary>
    CapitalNotOnThisSeaZone,

    /// <summary>A landing site names a province this map does not have.</summary>
    UnknownProvince,

    /// <summary>A landing site names a province the ordering country already owns.</summary>
    LandingOnOwnProvince,

    /// <summary>No cell of the landing site's province touches the task force's present sea zone.</summary>
    ProvinceNotOnThisSeaZone,
}

/// <summary>Records fleets grouped into a new task force.</summary>
public sealed record TaskForceAssembledEvent : TurnEvent
{
    private readonly IReadOnlyList<FleetId> _fleets;

    public TaskForceAssembledEvent(
        int turnNumber,
        CountryId country,
        TaskForceId taskForce,
        SeaZoneId seaZone,
        IEnumerable<FleetId> fleets)
        : base(turnNumber, TurnPhase.Conflict)
    {
        ArgumentNullException.ThrowIfNull(fleets);
        Country = country;
        TaskForce = taskForce;
        SeaZone = seaZone;
        _fleets = Array.AsReadOnly(fleets.ToArray());
    }

    public CountryId Country { get; }

    public TaskForceId TaskForce { get; }

    public SeaZoneId SeaZone { get; }

    /// <summary>The member fleets, in ascending id order.</summary>
    public IReadOnlyList<FleetId> Fleets => _fleets;
}

/// <summary>
/// Records a task force put on patrol in its present sea zone. It stays on patrol until
/// it is ordered to move.
/// </summary>
public sealed record TaskForcePatrolOrderedEvent : TurnEvent
{
    public TaskForcePatrolOrderedEvent(
        int turnNumber, CountryId country, TaskForceId taskForce, SeaZoneId seaZone)
        : base(turnNumber, TurnPhase.Conflict)
    {
        Country = country;
        TaskForce = taskForce;
        SeaZone = seaZone;
    }

    public CountryId Country { get; }

    public TaskForceId TaskForce { get; }

    public SeaZoneId SeaZone { get; }
}

/// <summary>
/// Records one sailing leg applied. The task force arrives idle, never on patrol.
/// </summary>
/// <remarks>
/// <see cref="To"/> can fall short of <see cref="RequestedDestination"/>: the leg is no
/// longer than the slowest hull's sea zones, and a zone the task force cannot reach at
/// all gives a leg of length zero, <see cref="From"/> equal to <see cref="To"/>.
/// </remarks>
public sealed record TaskForceSailedEvent : TurnEvent
{
    public TaskForceSailedEvent(
        int turnNumber,
        CountryId country,
        TaskForceId taskForce,
        SeaZoneId from,
        SeaZoneId to,
        SeaZoneId requestedDestination)
        : base(turnNumber, TurnPhase.Conflict)
    {
        Country = country;
        TaskForce = taskForce;
        From = from;
        To = to;
        RequestedDestination = requestedDestination;
    }

    public CountryId Country { get; }

    public TaskForceId TaskForce { get; }

    public SeaZoneId From { get; }

    public SeaZoneId To { get; }

    public SeaZoneId RequestedDestination { get; }
}

/// <summary>Records a naval order that was refused. Nothing changed.</summary>
public sealed record NavalOrderRefusedEvent : TurnEvent
{
    public NavalOrderRefusedEvent(
        int turnNumber,
        CountryId country,
        NavalOrderKind kind,
        TaskForceId? taskForce,
        NavalOrderRefusal reason)
        : base(turnNumber, TurnPhase.Conflict)
    {
        Country = country;
        Kind = kind;
        TaskForce = taskForce;
        Reason = reason;
    }

    public CountryId Country { get; }

    public NavalOrderKind Kind { get; }

    /// <summary>The task force a command named; null for an assembly.</summary>
    public TaskForceId? TaskForce { get; }

    public NavalOrderRefusal Reason { get; }
}
