namespace Imperialism.Core;

/// <summary>Resolves every country's inert orders through the fixed simultaneous-turn pipeline.</summary>
public static class TurnResolver
{
    private static readonly TurnPhase[] Pipeline =
    [
        TurnPhase.Diplomacy,
        TurnPhase.Trade,
        TurnPhase.Production,
        TurnPhase.Conflict,
        TurnPhase.TradeCancellation,
        TurnPhase.Delivery,
        TurnPhase.Connectivity,
    ];

    /// <summary>
    /// Runs one simultaneous turn. The <paramref name="seed"/> is recorded on the
    /// returned <see cref="TurnResolution"/> for replay and is reserved for the
    /// explicit seeded tiebreaks future contention rules will use; no phase
    /// consumes it yet because the current pipeline makes no random decisions.
    /// </summary>
    public static TurnResolution Resolve(WorldState state, TurnOrders orders, ulong seed)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(orders);
        if (orders.Count != state.Definition.Countries.Count)
        {
            throw new ArgumentException(
                $"Expected orders for {state.Definition.Countries.Count} countries, " +
                $"got {orders.Count}.",
                nameof(orders));
        }

        var turnNumber = checked(state.CompletedTurnCount + 1);
        var startedAt = state.CurrentDate;
        var events = new List<TurnEvent>(Pipeline.Length);
        foreach (var phase in Pipeline)
        {
            if (phase == TurnPhase.Delivery)
            {
                foreach (var delivery in state.CommitPendingDeliveries())
                {
                    events.Add(new CommodityDeliveredEvent(turnNumber, delivery));
                }
            }
            else if (phase == TurnPhase.Connectivity)
            {
                FinalizeConnectivity(state);
            }

            events.Add(new TurnPhaseCompletedEvent(turnNumber, phase));
        }

        state.CompleteTurn();
        return new TurnResolution(
            turnNumber,
            startedAt,
            state.CurrentDate,
            seed,
            events);
    }

    private static void FinalizeConnectivity(WorldState state)
    {
        for (var country = 0; country < state.Definition.Countries.Count; country++)
        {
            _ = state.GetRailConnectivity(new CountryId(country));
        }
    }
}
