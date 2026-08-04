namespace Imperialism.Core;

public enum TurnPhase : byte
{
    Diplomacy,
    Trade,
    Production,
    Construction,
    Migration,
    Conflict,
    TradeCancellation,
    Extraction,
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
