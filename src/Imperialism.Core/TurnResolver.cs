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
        TurnPhase.Transport,
        TurnPhase.Feeding,
        TurnPhase.Delivery,
        TurnPhase.Investment,
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

        // The railyard is priced against the same running total, and is the one
        // build that also draws on the labour pool, so it needs to know what
        // production already spent there.
        var labourSpent = new long[orders.Count];
        foreach (var entry in production.Entries)
        {
            labourSpent[entry.Country.Value] = checked(
                labourSpent[entry.Country.Value] + entry.LabourUsed);
        }

        var railyard = TransportPlanner.CreateRailyard(state, orders, spentSoFar, labourSpent);
        for (var index = 0; index < spentSoFar.Length; index++)
        {
            spentSoFar[index] += railyard.InventoryDeltas[index];
        }

        // Migration is priced last, against what production and building have
        // already committed, so one turn cannot spend the same clothing twice.
        var migration = MigrationPlanner.Create(state, orders, spentSoFar);
        var combined = new long[spentSoFar.Length];
        for (var index = 0; index < combined.Length; index++)
        {
            combined[index] = spentSoFar[index] + migration.InventoryDeltas[index];
        }

        // What a country may sell is what it holds less what is already spoken for:
        // "you cannot sell items you do not own or that you have ordered industry to use
        // this turn." So this has to be a pure *consumption* figure and deliberately not
        // a net one — netting would let a country sell output that does not reach the
        // warehouse until Production commits, which is after Trade.
        //
        // Every plan's delta is `outputs - consumption`, and only production has
        // outputs, so consumption is `outputs - delta` commodity by commodity. Doing it
        // that way rather than clamping the net is what keeps a commodity both consumed
        // and produced honest: 2 lumber in and 3 out nets +1, and 2 is still claimed.
        var produced = new long[combined.Length];
        var commodityCount = state.Definition.Commodities.Count;
        foreach (var entry in production.Entries)
        {
            foreach (var quantity in entry.Produced)
            {
                produced[(entry.Country.Value * commodityCount) + quantity.Commodity.Value] +=
                    quantity.Quantity;
            }
        }

        var claimed = new long[combined.Length];
        for (var index = 0; index < claimed.Length; index++)
        {
            claimed[index] = Math.Max(0, produced[index] - combined[index]);
        }

        var trade = TradePlanner.Create(state, orders, claimed);
        for (var index = 0; index < combined.Length; index++)
        {
            combined[index] += trade.InventoryDeltas[index];
        }

        state.PreflightInventoryChanges(combined);

        // Capacity bought this turn carries next turn, "as with other industrial
        // expansion". Transport runs long after Construction in the pipeline, so
        // it reads the figure the turn opened with rather than the live one.
        var capacityAtTurnStart = state.CopyTransportCapacity();
        IReadOnlyList<PlannedExtraction> gathered = [];
        var events = new List<TurnEvent>(Pipeline.Length + production.Entries.Count);
        foreach (var phase in Pipeline)
        {
            if (phase == TurnPhase.Trade)
            {
                // Before Production, which is the order the manual's screens imply:
                // industry claims its inputs first and the market sells what is left.
                // The money moves now, so it is available to Development later in the
                // same turn — which is what makes trade able to pay for an improvement.
                state.CommitProduction(trade.InventoryDeltas);
                foreach (var entry in trade.Trades)
                {
                    _ = state.TrySpendCash(entry.Buyer, entry.Quantity * entry.UnitPrice);
                    state.AddCash(entry.Seller, entry.Quantity * entry.UnitPrice);

                    // The buyer's goods arrive next turn: "the commodities you buy appear
                    // for your use in the Industry screen next turn." That is the same
                    // machinery extraction already uses, and the reason this needed no
                    // new state.
                    _ = state.QueuePendingDelivery(
                        entry.Buyer, entry.Commodity, entry.Quantity, PendingDeliverySource.Trade);
                    events.Add(new CommodityTradedEvent(
                        turnNumber,
                        entry.Seller,
                        entry.Buyer,
                        entry.Commodity,
                        entry.Quantity,
                        entry.UnitPrice,
                        entry.HoldsPaidBy));
                }

                foreach (var entry in trade.Shortfalls)
                {
                    events.Add(new TradeUnfilledEvent(
                        turnNumber,
                        entry.Country,
                        entry.Commodity,
                        entry.Requested,
                        entry.Settled,
                        entry.Reason));
                }

                foreach (var move in trade.PriceMoves)
                {
                    state.SetWorldPrice(move.Commodity, move.ToPrice);
                    events.Add(new WorldPriceChangedEvent(
                        turnNumber,
                        move.Commodity,
                        move.FromPrice,
                        move.ToPrice,
                        move.Offered,
                        move.Bid));
                }
            }
            else if (phase == TurnPhase.Production)
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
                state.CommitProduction(railyard.InventoryDeltas);
                foreach (var entry in railyard.Entries)
                {
                    state.SetTransportCapacity(entry.Country, entry.ToCapacity);
                    events.Add(new TransportCapacityBuiltEvent(
                        turnNumber,
                        entry.Country,
                        entry.FromCapacity,
                        entry.ToCapacity,
                        entry.LabourUsed,
                        entry.Paid));
                }

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
                        PlannedCellProspected entry => new CellProspectedEvent(
                            turnNumber, entry.Country, entry.Unit, entry.Cell, entry.Revealed),
                        PlannedCivilianWorkStart entry => new CivilianWorkBegunEvent(
                            turnNumber,
                            entry.Country,
                            entry.Unit,
                            entry.Cell,
                            entry.TurnsRequired,
                            entry.Paid),
                        PlannedConstructionStart entry => new ConstructionBegunEvent(
                            turnNumber,
                            entry.Country,
                            entry.Unit,
                            entry.Cell,
                            entry.Structure,
                            entry.Target,
                            entry.TurnsRequired,
                            entry.Paid),
                        PlannedConstruction entry => new ConstructionCompletedEvent(
                            turnNumber,
                            entry.Country,
                            entry.Unit,
                            entry.Cell,
                            entry.Structure,
                            entry.Target),
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
                // stops paying its owner this turn. It no longer queues
                // anything: what it gathers is a pool the network may carry
                // from, and Transport decides how much of it actually moves.
                gathered = ExtractionPlanner.Create(state);
                foreach (var entry in gathered)
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
            else if (phase == TurnPhase.Transport)
            {
                // Between Extraction and Feeding: you can only carry what you
                // gathered, and workers eat what was carried before they touch
                // the warehouse. What the network leaves behind does not keep.
                foreach (var entry in TransportPlanner.Create(
                    state, orders, gathered, capacityAtTurnStart))
                {
                    foreach (var quantity in entry.Moved)
                    {
                        _ = state.QueuePendingDelivery(
                            entry.Country,
                            quantity.Commodity,
                            quantity.Quantity,
                            PendingDeliverySource.Extraction);
                    }

                    // Gold and gems pay on arrival rather than being queued:
                    // "all gems and gold transported convert immediately into
                    // cash", and nothing about them ever enters the warehouse.
                    if (entry.CashEarned > 0)
                    {
                        state.AddCash(entry.Country, entry.CashEarned);
                    }

                    if (entry.Moved.Count == 0 && entry.Converted.Count == 0 &&
                        entry.Wasted.Count == 0)
                    {
                        continue;
                    }

                    events.Add(new CommoditiesTransportedEvent(
                        turnNumber,
                        entry.Country,
                        entry.CapacityUsed,
                        entry.CapacityAvailable,
                        entry.Moved,
                        entry.Wasted,
                        entry.Converted,
                        entry.CashEarned));
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
            else if (phase == TurnPhase.Investment)
            {
                // Last, so every gate that reads knowledge during a turn has
                // already run against what the turn opened with. That is the
                // whole of "bought this turn, known next turn" — the same trick
                // Construction uses to complete next turn.
                foreach (var outcome in TechnologyPlanner.Resolve(state, orders))
                {
                    events.Add(outcome switch
                    {
                        PlannedTechnologyPurchase entry => new TechnologyPurchasedEvent(
                            turnNumber, entry.Country, entry.Technology, entry.Paid),
                        PlannedTechnologyRefusal entry => new TechnologyPurchaseRefusedEvent(
                            turnNumber, entry.Country, entry.Technology, entry.Reason),
                        _ => throw new InvalidOperationException(
                            $"Unhandled investment outcome {outcome.GetType().Name}."),
                    });
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
