namespace Imperialism.Core;

public enum TurnPhase : byte
{
    Diplomacy,
    Trade,
    Production,
    Conflict,
    TradeCancellation,
    Extraction,
    Feeding,
    Delivery,
    Connectivity,
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
        IEnumerable<CommodityQuantity> consumed,
        IEnumerable<CommodityQuantity> produced)
        : base(turnNumber, TurnPhase.Production)
    {
        if (requestedCycles <= 0 || completedCycles < 0 || completedCycles > requestedCycles || capacityUsed < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(completedCycles));
        }

        Country = country;
        Recipe = recipe;
        RequestedCycles = requestedCycles;
        CompletedCycles = completedCycles;
        CapacityUsed = capacityUsed;
        _consumed = Array.AsReadOnly(consumed.ToArray());
        _produced = Array.AsReadOnly(produced.ToArray());
    }

    public CountryId Country { get; }

    public ProductionRecipeId Recipe { get; }

    public long RequestedCycles { get; }

    public long CompletedCycles { get; }

    public long CapacityUsed { get; }

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

    /// <summary>Workers fed something they did not want; they do no labour this turn.</summary>
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
