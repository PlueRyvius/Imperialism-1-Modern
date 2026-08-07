namespace Imperialism.Core;

/// <summary>Resolves every country's inert orders through the fixed simultaneous-turn pipeline.</summary>
public static class TurnResolver
{
    private static readonly TurnPhase[] Pipeline =
    [
        TurnPhase.Diplomacy,
        TurnPhase.Trade,
        TurnPhase.Production,
        TurnPhase.Construction,
        TurnPhase.Development,
        TurnPhase.Migration,
        TurnPhase.Conflict,
        TurnPhase.TradeCancellation,
        TurnPhase.Extraction,
        TurnPhase.Feeding,
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
        var production = ProductionPlanner.Create(state, orders);

        // Expansion is planned against what production has already committed to
        // spending, so a turn cannot pay for the same lumber twice.
        var expansion = ExpansionPlanner.Create(state, orders, production.InventoryDeltas);
        var spentSoFar = new long[production.InventoryDeltas.Length];
        for (var index = 0; index < spentSoFar.Length; index++)
        {
            spentSoFar[index] = production.InventoryDeltas[index] + expansion.InventoryDeltas[index];
        }

        // Migration is priced last, against what production and building have
        // already committed, so one turn cannot spend the same clothing twice.
        var migration = MigrationPlanner.Create(state, orders, spentSoFar);
        var combined = new long[spentSoFar.Length];
        for (var index = 0; index < combined.Length; index++)
        {
            combined[index] = spentSoFar[index] + migration.InventoryDeltas[index];
        }

        state.PreflightInventoryChanges(combined);
        var events = new List<TurnEvent>(Pipeline.Length + production.Entries.Count);
        foreach (var phase in Pipeline)
        {
            if (phase == TurnPhase.Production)
            {
                state.CommitProduction(production.InventoryDeltas);
                foreach (var entry in production.Entries)
                {
                    events.Add(new ProductionCompletedEvent(
                        turnNumber,
                        entry.Country,
                        entry.Order.Recipe,
                        entry.Order.RequestedCycles,
                        entry.CompletedCycles,
                        entry.CapacityUsed,
                        entry.LabourUsed,
                        entry.Consumed,
                        entry.Produced));
                }
            }
            else if (phase == TurnPhase.Construction)
            {
                // After Production, which is what makes "completes next turn"
                // fall out for free: this turn's output was already decided
                // against the old size.
                state.CommitProduction(expansion.InventoryDeltas);
                foreach (var entry in expansion.Entries)
                {
                    state.SetProductionCapacity(entry.Country, entry.Facility, entry.ToCapacity);
                    events.Add(new FacilityExpandedEvent(
                        turnNumber,
                        entry.Country,
                        entry.Facility,
                        entry.FromCapacity,
                        entry.ToCapacity,
                        entry.Paid));
                }
            }
            else if (phase == TurnPhase.Development)
            {
                // Beside Construction, which it resembles: both take an order
                // now and pay it off later. It sits before Extraction so a tile
                // finished this turn is gathered at its new rate this turn, and
                // that harvest reaches the warehouse for next turn's production
                // like every other harvest.
                foreach (var outcome in DevelopmentPlanner.Resolve(state, orders))
                {
                    events.Add(outcome switch
                    {
                        PlannedCellDevelopment entry => new CellDevelopedEvent(
                            turnNumber,
                            entry.Country,
                            entry.Unit,
                            entry.Cell,
                            entry.FromLevel,
                            entry.ToLevel),
                        PlannedCivilianWorkStart entry => new CivilianWorkBegunEvent(
                            turnNumber, entry.Country, entry.Unit, entry.Cell, entry.TurnsRequired),
                        PlannedCivilianDeployment entry => new CivilianDeployedEvent(
                            turnNumber, entry.Country, entry.Unit, entry.From, entry.To),
                        PlannedCivilianRefusal entry => new CivilianOrderRefusedEvent(
                            turnNumber, entry.Country, entry.Unit, entry.Cell, entry.Reason),
                        _ => throw new InvalidOperationException(
                            $"Unhandled development outcome {outcome.GetType().Name}."),
                    });
                }
            }
            else if (phase == TurnPhase.Migration)
            {
                // Before Feeding, so a recruit eats on the turn it arrives —
                // which is what gives the manual's warning about growing too
                // fast any teeth. After Production, so it supplies no labour
                // until the following turn.
                state.CommitProduction(migration.InventoryDeltas);
                foreach (var entry in migration.Entries)
                {
                    if (entry.Recruited > 0)
                    {
                        state.SetWorkers(
                            entry.Country,
                            WorkerGrade.Untrained,
                            checked(state.GetWorkers(entry.Country, WorkerGrade.Untrained) +
                                entry.Recruited));
                    }

                    events.Add(new WorkersRecruitedEvent(
                        turnNumber,
                        entry.Country,
                        entry.Requested,
                        entry.Recruited,
                        entry.SizeLimit,
                        entry.Paid));
                }
            }
            else if (phase == TurnPhase.Extraction)
            {
                // Extraction runs after Conflict so a province lost this turn
                // stops paying its owner this turn, and queues rather than
                // credits: gathered output reaches the warehouse through
                // Delivery, making it available to next turn's production.
                foreach (var entry in ExtractionPlanner.Create(state))
                {
                    // A country with no deposits and no ports is not an event.
                    // Anything else is, even when every quantity is zero: a
                    // reachable but undeveloped mine yields nothing, and that
                    // is a fact worth showing rather than a silence.
                    if (entry.CollectedCellCount == 0 && entry.StrandedCellCount == 0 &&
                        entry.FishingPortCount == 0 && entry.StrandedPortCount == 0)
                    {
                        continue;
                    }

                    foreach (var quantity in entry.Collected)
                    {
                        _ = state.QueuePendingDelivery(
                            entry.Country,
                            quantity.Commodity,
                            quantity.Quantity,
                            PendingDeliverySource.Extraction);
                    }

                    events.Add(new ResourceExtractedEvent(
                        turnNumber,
                        entry.Country,
                        entry.CollectedCellCount,
                        entry.StrandedCellCount,
                        entry.FishingPortCount,
                        entry.StrandedPortCount,
                        entry.Collected,
                        entry.Stranded));
                }
            }
            else if (phase == TurnPhase.Feeding)
            {
                // After Extraction so this turn's harvest can feed this turn's
                // workers, and before Delivery so it is eaten off the back of
                // the cart rather than out of the warehouse.
                foreach (var entry in FeedingPlanner.Resolve(state))
                {
                    events.Add(new WorkersFedEvent(
                        turnNumber,
                        entry.Country,
                        entry.WellFed,
                        entry.Sick,
                        entry.Starved,
                        entry.Eaten));
                }
            }
            else if (phase == TurnPhase.Delivery)
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
