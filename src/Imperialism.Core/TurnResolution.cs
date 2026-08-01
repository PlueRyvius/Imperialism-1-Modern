namespace Imperialism.Core;

public enum TurnPhase : byte
{
    Diplomacy,
    Trade,
    Production,
    Conflict,
    TradeCancellation,
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
