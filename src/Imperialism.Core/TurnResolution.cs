namespace Imperialism.Core;

public enum TurnPhase : byte
{
    Diplomacy,
    Trade,
    Production,
    Construction,
    Development,
    Migration,
    Conflict,
    TradeCancellation,
    Extraction,

    /// <summary>
    /// Moves what Extraction gathered onto the network, up to what the country
    /// can carry. Sits before <see cref="Feeding"/> because workers eat
    /// transported food ahead of warehouse stock, which is the whole point of
    /// the original's grain demand line.
    /// </summary>
    Transport,
    Feeding,
    Delivery,
    Connectivity,
}

/// <summary>Records one country's turn at the Capitol.</summary>
/// <remarks>
/// Reported even when nobody came, because "you asked for four and your country
/// is too small for any" is a fact a player needs, and a silence would leave
/// them dragging a slider that does nothing.
/// </remarks>
public sealed record WorkersRecruitedEvent : TurnEvent
{
    private readonly IReadOnlyList<CommodityQuantity> _paid;

    public WorkersRecruitedEvent(
        int turnNumber,
        CountryId country,
        long requested,
        long recruited,
        long sizeLimit,
        IEnumerable<CommodityQuantity> paid)
        : base(turnNumber, TurnPhase.Migration)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(recruited);
        ArgumentOutOfRangeException.ThrowIfNegative(sizeLimit);
        if (recruited > requested)
        {
            throw new ArgumentOutOfRangeException(nameof(recruited));
        }

        Country = country;
        Requested = requested;
        Recruited = recruited;
        SizeLimit = sizeLimit;
        _paid = Array.AsReadOnly(paid.ToArray());
    }

    public CountryId Country { get; }

    public long Requested { get; }

    /// <summary>Untrained workers who actually arrived.</summary>
    public long Recruited { get; }

    /// <summary>What the country's size allowed this turn, before cost.</summary>
    public long SizeLimit { get; }

    public IReadOnlyList<CommodityQuantity> Paid => _paid;
}

/// <summary>Records one facility built a rung larger.</summary>
public sealed record FacilityExpandedEvent : TurnEvent
{
    private readonly IReadOnlyList<CommodityQuantity> _paid;

    public FacilityExpandedEvent(
        int turnNumber,
        CountryId country,
        ProductionFacilityId facility,
        long fromCapacity,
        long toCapacity,
        IEnumerable<CommodityQuantity> paid)
        : base(turnNumber, TurnPhase.Construction)
    {
        if (toCapacity <= fromCapacity)
        {
            throw new ArgumentOutOfRangeException(nameof(toCapacity));
        }

        Country = country;
        Facility = facility;
        FromCapacity = fromCapacity;
        ToCapacity = toCapacity;
        _paid = Array.AsReadOnly(paid.ToArray());
    }

    public CountryId Country { get; }

    public ProductionFacilityId Facility { get; }

    public long FromCapacity { get; }

    public long ToCapacity { get; }

    /// <summary>What the build cost, at one lumber and one steel per point.</summary>
    public IReadOnlyList<CommodityQuantity> Paid => _paid;
}

/// <summary>Why a civilian could not be given the order it was given.</summary>
/// <remarks>
/// Refusals are reported rather than thrown. A civilian can die between the
/// orders being written and the turn resolving, and a tile can change hands, so
/// an impossible order is an ordinary outcome of simultaneous turns rather than
/// a malformed submission.
/// </remarks>
public enum CivilianOrderRefusal : byte
{
    /// <summary>No civilian carries that id — most likely it has died.</summary>
    NoSuchCivilian,

    /// <summary>The civilian belongs to another country.</summary>
    NotYours,

    /// <summary>The civilian is part way through a job and cannot be redirected.</summary>
    AlreadyWorking,

    TargetOffMap,

    TargetNotLand,

    /// <summary>
    /// The tile belongs to somebody else. The manual bars civilians from
    /// another Great Power's land outright, and from a Minor Nation's without an
    /// embassy; with no diplomacy modelled, only a country's own land is
    /// allowed.
    /// </summary>
    TargetNotYourTerritory,

    /// <summary>Dry plains, horse ranch, scrub forest, water, or a settlement.</summary>
    TerrainCannotBeImproved,

    /// <summary>Nothing on the tile is improved by this kind of civilian.</summary>
    NoDepositThisCivilianWorks,

    /// <summary>The tile is already at the top of its deposit's yield curve.</summary>
    AlreadyFullyDeveloped,

    /// <summary>
    /// A Prospector was sent to ground that hides nothing. The manual's eye
    /// cursor appears over barren hills and mountains, and over swamp, desert
    /// and tundra once Oil Drilling is known; everywhere else announces its own
    /// resources by its terrain.
    /// </summary>
    TerrainCannotBeProspected,

    /// <summary>
    /// The ground is searchable but this country has not invested in what it
    /// takes. Oil Drilling is the manual's only instance.
    /// </summary>
    ProspectingTechnologyNotKnown,

    /// <summary>
    /// This country has already searched this tile. Whatever was there is known,
    /// and a second search would find the same thing or the same nothing.
    /// </summary>
    AlreadyProspected,

    /// <summary>
    /// A Miner or Driller was sent to a deposit nobody has found yet. The
    /// deposit is on the map and the country cannot see it.
    /// </summary>
    DepositNotYetDiscovered,

    /// <summary>
    /// The tile has a rung left and the country does not know how to climb it.
    /// The manual's Benefits of Technology Table gates nearly every level —
    /// Steel and Iron Plows for grain to Level II, Dynamite for a Level III
    /// mine, and so on. Distinct from <see cref="AlreadyFullyDeveloped"/>
    /// because this one is a matter of investing rather than of the tile being
    /// finished.
    /// </summary>
    ImprovementTechnologyNotKnown,
}

/// <summary>Records one civilian moving without being set to work.</summary>
public sealed record CivilianDeployedEvent : TurnEvent
{
    public CivilianDeployedEvent(
        int turnNumber,
        CountryId country,
        CivilianUnitId unit,
        CellIndex from,
        CellIndex to)
        : base(turnNumber, TurnPhase.Development)
    {
        Country = country;
        Unit = unit;
        From = from;
        To = to;
    }

    public CountryId Country { get; }

    public CivilianUnitId Unit { get; }

    public CellIndex From { get; }

    public CellIndex To { get; }
}

/// <summary>Records one civilian starting work on a tile.</summary>
public sealed record CivilianWorkBegunEvent : TurnEvent
{
    public CivilianWorkBegunEvent(
        int turnNumber,
        CountryId country,
        CivilianUnitId unit,
        CellIndex cell,
        int turnsRequired)
        : base(turnNumber, TurnPhase.Development)
    {
        if (turnsRequired <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(turnsRequired));
        }

        Country = country;
        Unit = unit;
        Cell = cell;
        TurnsRequired = turnsRequired;
    }

    public CountryId Country { get; }

    public CivilianUnitId Unit { get; }

    public CellIndex Cell { get; }

    /// <summary>How many turns this civilian's type takes. The one guess here.</summary>
    public int TurnsRequired { get; }
}

/// <summary>
/// Records one tile finishing a level of improvement. Emitted in the same
/// Development phase that raises the level, which the turn's later Extraction
/// then gathers at the new rate.
/// </summary>
public sealed record CellDevelopedEvent : TurnEvent
{
    public CellDevelopedEvent(
        int turnNumber,
        CountryId country,
        CivilianUnitId unit,
        CellIndex cell,
        int fromLevel,
        int toLevel)
        : base(turnNumber, TurnPhase.Development)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(fromLevel);
        if (toLevel <= fromLevel)
        {
            throw new ArgumentOutOfRangeException(nameof(toLevel));
        }

        Country = country;
        Unit = unit;
        Cell = cell;
        FromLevel = fromLevel;
        ToLevel = toLevel;
    }

    public CountryId Country { get; }

    /// <summary>The civilian whose work finished. It is idle again from now.</summary>
    public CivilianUnitId Unit { get; }

    public CellIndex Cell { get; }

    public int FromLevel { get; }

    public int ToLevel { get; }
}

/// <summary>
/// Records a Prospector finishing a search, and what it turned up.
/// </summary>
/// <remarks>
/// <see cref="Revealed"/> is empty far more often than not — only 449 of the
/// corpus's 2,860 barren hills and 346 of its 1,589 mountains carry a deposit at
/// all — and the empty case is reported rather than swallowed. A player needs to
/// know the tile has been looked at, which is exactly what the original's
/// pickaxe-and-red-X marker tells them.
/// </remarks>
public sealed record CellProspectedEvent : TurnEvent
{
    private readonly IReadOnlyList<ResourceId> _revealed;

    public CellProspectedEvent(
        int turnNumber,
        CountryId country,
        CivilianUnitId unit,
        CellIndex cell,
        IEnumerable<ResourceId> revealed)
        : base(turnNumber, TurnPhase.Development)
    {
        ArgumentNullException.ThrowIfNull(revealed);
        Country = country;
        Unit = unit;
        Cell = cell;
        _revealed = Array.AsReadOnly(revealed.ToArray());
    }

    public CountryId Country { get; }

    /// <summary>The Prospector whose search finished. It is idle again from now.</summary>
    public CivilianUnitId Unit { get; }

    public CellIndex Cell { get; }

    /// <summary>
    /// Hidden deposits this search brought to light, in map order. Empty when the
    /// ground held nothing, which is the ordinary outcome.
    /// </summary>
    public IReadOnlyList<ResourceId> Revealed => _revealed;
}

/// <summary>Records an order a civilian could not carry out, and why.</summary>
public sealed record CivilianOrderRefusedEvent : TurnEvent
{
    public CivilianOrderRefusedEvent(
        int turnNumber,
        CountryId country,
        CivilianUnitId unit,
        CellIndex cell,
        CivilianOrderRefusal reason)
        : base(turnNumber, TurnPhase.Development)
    {
        if (!Enum.IsDefined(reason))
        {
            throw new ArgumentOutOfRangeException(nameof(reason));
        }

        Country = country;
        Unit = unit;
        Cell = cell;
        Reason = reason;
    }

    /// <summary>The country that gave the order, not necessarily the owner.</summary>
    public CountryId Country { get; }

    public CivilianUnitId Unit { get; }

    public CellIndex Cell { get; }

    public CivilianOrderRefusal Reason { get; }
}

/// <summary>A presentation-facing fact emitted while resolving a turn.</summary>
public abstract record TurnEvent
{
    protected TurnEvent(int turnNumber, TurnPhase phase)
    {
        if (turnNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(turnNumber));
        }

        if (!Enum.IsDefined(phase))
        {
            throw new ArgumentOutOfRangeException(nameof(phase));
        }

        TurnNumber = turnNumber;
        Phase = phase;
    }

    public int TurnNumber { get; }

    public TurnPhase Phase { get; }
}

/// <summary>Marks completion of one fixed pipeline phase.</summary>
public sealed record TurnPhaseCompletedEvent : TurnEvent
{
    public TurnPhaseCompletedEvent(int turnNumber, TurnPhase phase)
        : base(turnNumber, phase)
    {
    }
}

/// <summary>Records one pending commodity intent entering available inventory.</summary>
public sealed record CommodityDeliveredEvent : TurnEvent
{
    public CommodityDeliveredEvent(int turnNumber, PendingDelivery delivery)
        : base(turnNumber, TurnPhase.Delivery)
    {
        Delivery = delivery;
    }

    public PendingDelivery Delivery { get; }
}

/// <summary>Records the deterministic result of one production request.</summary>
public sealed record ProductionCompletedEvent : TurnEvent
{
    private readonly IReadOnlyList<CommodityQuantity> _consumed;
    private readonly IReadOnlyList<CommodityQuantity> _produced;

    public ProductionCompletedEvent(
        int turnNumber,
        CountryId country,
        ProductionRecipeId recipe,
        long requestedCycles,
        long completedCycles,
        long capacityUsed,
        long labourUsed,
        IEnumerable<CommodityQuantity> consumed,
        IEnumerable<CommodityQuantity> produced)
        : base(turnNumber, TurnPhase.Production)
    {
        if (requestedCycles <= 0 || completedCycles < 0 || completedCycles > requestedCycles ||
            capacityUsed < 0 || labourUsed < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(completedCycles));
        }

        Country = country;
        Recipe = recipe;
        RequestedCycles = requestedCycles;
        CompletedCycles = completedCycles;
        CapacityUsed = capacityUsed;
        LabourUsed = labourUsed;
        _consumed = Array.AsReadOnly(consumed.ToArray());
        _produced = Array.AsReadOnly(produced.ToArray());
    }

    public CountryId Country { get; }

    public ProductionRecipeId Recipe { get; }

    public long RequestedCycles { get; }

    public long CompletedCycles { get; }

    public long CapacityUsed { get; }

    /// <summary>Labour spent out of the country's pool for the cycles that ran.</summary>
    public long LabourUsed { get; }

    public IReadOnlyList<CommodityQuantity> Consumed => _consumed;

    public IReadOnlyList<CommodityQuantity> Produced => _produced;
}

/// <summary>
/// Records what one country's deposits handed over this turn, and what its own
/// territory produced but could not move. Stranded output is reported rather
/// than dropped silently: it is the visible cost of a severed rail network, and
/// the number a player needs in order to see why a warehouse stopped filling.
/// </summary>
public sealed record ResourceExtractedEvent : TurnEvent
{
    private readonly IReadOnlyList<CommodityQuantity> _collected;
    private readonly IReadOnlyList<CommodityQuantity> _stranded;

    public ResourceExtractedEvent(
        int turnNumber,
        CountryId country,
        int collectedCellCount,
        int strandedCellCount,
        int fishingPortCount,
        int strandedPortCount,
        IEnumerable<CommodityQuantity> collected,
        IEnumerable<CommodityQuantity> stranded)
        : base(turnNumber, TurnPhase.Extraction)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(collectedCellCount);
        ArgumentOutOfRangeException.ThrowIfNegative(strandedCellCount);
        ArgumentOutOfRangeException.ThrowIfNegative(fishingPortCount);
        ArgumentOutOfRangeException.ThrowIfNegative(strandedPortCount);
        Country = country;
        CollectedCellCount = collectedCellCount;
        StrandedCellCount = strandedCellCount;
        FishingPortCount = fishingPortCount;
        StrandedPortCount = strandedPortCount;
        _collected = Array.AsReadOnly(collected.ToArray());
        _stranded = Array.AsReadOnly(stranded.ToArray());
    }

    public CountryId Country { get; }

    /// <summary>Owned cells carrying a deposit that were inside the catchment.</summary>
    public int CollectedCellCount { get; }

    /// <summary>Owned cells carrying a deposit that no connected route reached.</summary>
    public int StrandedCellCount { get; }

    /// <summary>Owned ports on the network with water to fish.</summary>
    public int FishingPortCount { get; }

    /// <summary>Owned ports with water to fish that no connected route reached.</summary>
    public int StrandedPortCount { get; }

    public IReadOnlyList<CommodityQuantity> Collected => _collected;

    public IReadOnlyList<CommodityQuantity> Stranded => _stranded;
}

/// <summary>
/// Records what one country's network carried this turn, and what it had to
/// leave on the ground.
/// </summary>
/// <remarks>
/// <see cref="Wasted"/> is reported rather than silently dropped because it is
/// the number a player acts on: it is the difference between a network that is
/// big enough and one that is not, and the original nags about it through a
/// Minister warning on "wasting transport capacity".
/// <para>
/// Distinct from <see cref="ResourceExtractedEvent.Stranded"/>, which is output
/// no route reached at all. A cell can now fail to reach the warehouse two
/// different ways, and they want different fixes — build a depot, or build a
/// railyard.
/// </para>
/// </remarks>
public sealed record CommoditiesTransportedEvent : TurnEvent
{
    private readonly IReadOnlyList<CommodityQuantity> _moved;
    private readonly IReadOnlyList<CommodityQuantity> _wasted;

    public CommoditiesTransportedEvent(
        int turnNumber,
        CountryId country,
        long capacityUsed,
        long capacityAvailable,
        IEnumerable<CommodityQuantity> moved,
        IEnumerable<CommodityQuantity> wasted)
        : base(turnNumber, TurnPhase.Transport)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(capacityUsed);
        ArgumentOutOfRangeException.ThrowIfNegative(capacityAvailable);
        if (capacityUsed > capacityAvailable)
        {
            throw new ArgumentOutOfRangeException(nameof(capacityUsed));
        }

        Country = country;
        CapacityUsed = capacityUsed;
        CapacityAvailable = capacityAvailable;
        _moved = Array.AsReadOnly(moved.ToArray());
        _wasted = Array.AsReadOnly(wasted.ToArray());
    }

    public CountryId Country { get; }

    public long CapacityUsed { get; }

    /// <summary>The capacity the turn began with; anything built this turn carries next turn.</summary>
    public long CapacityAvailable { get; }

    /// <summary>What reached the network, and so the warehouse next turn.</summary>
    public IReadOnlyList<CommodityQuantity> Moved => _moved;

    /// <summary>Gathered, reachable, and left behind. It does not keep.</summary>
    public IReadOnlyList<CommodityQuantity> Wasted => _wasted;
}

/// <summary>Records one country buying transport capacity at the railyard.</summary>
public sealed record TransportCapacityBuiltEvent : TurnEvent
{
    private readonly IReadOnlyList<CommodityQuantity> _paid;

    public TransportCapacityBuiltEvent(
        int turnNumber,
        CountryId country,
        long fromCapacity,
        long toCapacity,
        long labourUsed,
        IEnumerable<CommodityQuantity> paid)
        : base(turnNumber, TurnPhase.Construction)
    {
        if (toCapacity <= fromCapacity)
        {
            throw new ArgumentOutOfRangeException(nameof(toCapacity));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(labourUsed);
        Country = country;
        FromCapacity = fromCapacity;
        ToCapacity = toCapacity;
        LabourUsed = labourUsed;
        _paid = Array.AsReadOnly(paid.ToArray());
    }

    public CountryId Country { get; }

    public long FromCapacity { get; }

    public long ToCapacity { get; }

    /// <summary>
    /// The railyard is the one build that costs labour; expanding a mill does
    /// not. See <see cref="TransportSettings.LabourPerCapacityPoint"/>.
    /// </summary>
    public long LabourUsed { get; }

    public IReadOnlyList<CommodityQuantity> Paid => _paid;
}

/// <summary>
/// Records how one country's workforce ate. Sickness and starvation are
/// reported rather than inferred from a headcount drop, because a player needs
/// to see the near miss as well as the loss.
/// </summary>
public sealed record WorkersFedEvent : TurnEvent
{
    private readonly IReadOnlyList<CommodityQuantity> _eaten;

    public WorkersFedEvent(
        int turnNumber,
        CountryId country,
        long wellFed,
        long sick,
        long starved,
        IEnumerable<CommodityQuantity> eaten)
        : base(turnNumber, TurnPhase.Feeding)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(wellFed);
        ArgumentOutOfRangeException.ThrowIfNegative(sick);
        ArgumentOutOfRangeException.ThrowIfNegative(starved);
        Country = country;
        WellFed = wellFed;
        Sick = sick;
        Starved = starved;
        _eaten = Array.AsReadOnly(eaten.ToArray());
    }

    public CountryId Country { get; }

    /// <summary>Workers that got their preference, or canned food instead.</summary>
    public long WellFed { get; }

    /// <summary>
    /// Workers fed something they did not want. They supply no labour to the
    /// next turn's production, which is the first one whose orders could have
    /// been given knowing they were ill.
    /// </summary>
    public long Sick { get; }

    /// <summary>Workers that found nothing and were permanently removed.</summary>
    public long Starved { get; }

    public IReadOnlyList<CommodityQuantity> Eaten => _eaten;
}

public sealed class TurnResolution
{
    private readonly IReadOnlyList<TurnEvent> _events;

    internal TurnResolution(
        int turnNumber,
        TurnDate startedAt,
        TurnDate endedAt,
        ulong seed,
        IEnumerable<TurnEvent> events)
    {
        TurnNumber = turnNumber;
        StartedAt = startedAt;
        EndedAt = endedAt;
        Seed = seed;
        _events = Array.AsReadOnly(events.ToArray());
    }

    public int TurnNumber { get; }

    public TurnDate StartedAt { get; }

    public TurnDate EndedAt { get; }

    public ulong Seed { get; }

    public IReadOnlyList<TurnEvent> Events => _events;
}
