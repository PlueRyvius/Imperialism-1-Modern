namespace Imperialism.Core;

/// <summary>
/// Records a task force put on blockade of a country's capital. It stays there until it
/// is ordered to move or given another mission, and it never cuts a port.
/// </summary>
public sealed record TaskForceBlockadeOrderedEvent : TurnEvent
{
    public TaskForceBlockadeOrderedEvent(
        int turnNumber, CountryId country, TaskForceId taskForce, SeaZoneId seaZone, CountryId target)
        : base(turnNumber, TurnPhase.Conflict)
    {
        Country = country;
        TaskForce = taskForce;
        SeaZone = seaZone;
        Target = target;
    }

    public CountryId Country { get; }

    public TaskForceId TaskForce { get; }

    public SeaZoneId SeaZone { get; }

    /// <summary>The country whose capital is blockaded.</summary>
    public CountryId Target { get; }
}

/// <summary>One fleet's part in a naval battle.</summary>
/// <param name="ShipsBefore">Ships of this fleet that fought; a class with no combat stats stands aside and is not listed.</param>
/// <param name="ShipsSunk">Ships of this fleet sunk in the battle. Survivors come out undamaged.</param>
public sealed record NavalBattleParticipant(
    FleetId Fleet,
    ShipTypeId Type,
    BattleSide Side,
    long ShipsBefore,
    long ShipsSunk);

/// <summary>Where a naval battle was fought.</summary>
public enum NavalBattleKind : byte
{
    /// <summary>A patrol found a hostile task force in <c>Conflict</c>.</summary>
    Patrol,

    /// <summary>
    /// An interceptor met the carrier's escorts in <c>TradeCancellation</c>: warships
    /// anchored beside the carrier's capital.
    /// </summary>
    Escort,
}

/// <summary>
/// Records one strategic naval battle, resolved headlessly. The resolver behind it is a
/// labelled placeholder until the executable's <c>0x005578A0</c> is traced; see
/// <c>docs/formulas/naval.md</c>.
/// </summary>
public sealed record NavalBattleFoughtEvent : TurnEvent
{
    private readonly IReadOnlyList<NavalBattleParticipant> _participants;

    public NavalBattleFoughtEvent(
        int turnNumber,
        NavalBattleKind kind,
        SeaZoneId seaZone,
        CountryId attacker,
        CountryId defender,
        IEnumerable<NavalBattleParticipant> participants,
        CountryId winner,
        int rounds)
        : base(turnNumber, kind == NavalBattleKind.Escort ? TurnPhase.TradeCancellation : TurnPhase.Conflict)
    {
        ArgumentNullException.ThrowIfNull(participants);
        ArgumentOutOfRangeException.ThrowIfNegative(rounds);
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        if (attacker == defender)
        {
            throw new ArgumentException("A battle needs two countries.", nameof(defender));
        }

        if (winner != attacker && winner != defender)
        {
            throw new ArgumentException("The winner is one of the two sides.", nameof(winner));
        }

        Kind = kind;
        SeaZone = seaZone;
        Attacker = attacker;
        Defender = defender;
        _participants = Array.AsReadOnly(participants.ToArray());
        Winner = winner;
        Rounds = rounds;
    }

    public NavalBattleKind Kind { get; }

    /// <summary>The zone the attacking task force lay in.</summary>
    public SeaZoneId SeaZone { get; }

    public CountryId Attacker { get; }

    public CountryId Defender { get; }

    /// <summary>Attacking fleets then defending fleets, each in fleet-id order.</summary>
    public IReadOnlyList<NavalBattleParticipant> Participants => _participants;

    public CountryId Winner { get; }

    /// <summary>Rounds fought; at most twenty.</summary>
    public int Rounds { get; }
}

/// <summary>What became of an intercepted merchant and its cargo.</summary>
public enum InterceptionOutcome : byte
{
    /// <summary>The merchant turned back: the goods return to the seller next turn.</summary>
    TurnedBack,

    /// <summary>The cargo was captured: the interceptor receives the goods next turn.</summary>
    Captured,

    /// <summary>The merchant was sunk: the goods are lost and the carrier loses one hull.</summary>
    Sunk,
}

/// <summary>
/// Records a deal's cargo intercepted at sea. The money settled in <c>Trade</c> stands,
/// so the buyer has paid for goods it will not receive — a chosen rule.
/// </summary>
public sealed record MerchantInterceptedEvent : TurnEvent
{
    public MerchantInterceptedEvent(
        int turnNumber,
        CountryId interceptor,
        TaskForceId taskForce,
        CountryId carrier,
        CountryId seller,
        CountryId buyer,
        CommodityId commodity,
        long quantity,
        InterceptionOutcome outcome,
        TaskForceActivity mission,
        ShipTypeId? hullSunk)
        : base(turnNumber, TurnPhase.TradeCancellation)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
        if (!Enum.IsDefined(outcome))
        {
            throw new ArgumentOutOfRangeException(nameof(outcome));
        }

        if (mission is not (TaskForceActivity.Patrolling or TaskForceActivity.Blockading))
        {
            throw new ArgumentOutOfRangeException(nameof(mission), "Only a patrol or a blockade intercepts.");
        }

        if (hullSunk is not null && outcome != InterceptionOutcome.Sunk)
        {
            throw new ArgumentException("Only a sinking costs a hull.", nameof(hullSunk));
        }

        Interceptor = interceptor;
        TaskForce = taskForce;
        Carrier = carrier;
        Seller = seller;
        Buyer = buyer;
        Commodity = commodity;
        Quantity = quantity;
        Outcome = outcome;
        Mission = mission;
        HullSunk = hullSunk;
    }

    public CountryId Interceptor { get; }

    public TaskForceId TaskForce { get; }

    /// <summary>The country whose merchants carried the deal: its <c>HoldsPaidBy</c>.</summary>
    public CountryId Carrier { get; }

    public CountryId Seller { get; }

    public CountryId Buyer { get; }

    public CommodityId Commodity { get; }

    public long Quantity { get; }

    public InterceptionOutcome Outcome { get; }

    /// <summary>Whether a patrol or a blockade made the interception.</summary>
    public TaskForceActivity Mission { get; }

    /// <summary>The class of the merchant hull lost on a sinking, if the carrier had one to lose.</summary>
    public ShipTypeId? HullSunk { get; }
}
