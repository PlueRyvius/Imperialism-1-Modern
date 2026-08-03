namespace Imperialism.Core;

internal sealed record PlannedExpansion(
    CountryId Country,
    ProductionFacilityId Facility,
    long FromCapacity,
    long ToCapacity,
    IReadOnlyList<CommodityQuantity> Paid);

internal sealed class ExpansionPlan
{
    public ExpansionPlan(long[] inventoryDeltas, IEnumerable<PlannedExpansion> entries)
    {
        InventoryDeltas = inventoryDeltas;
        Entries = Array.AsReadOnly(entries.ToArray());
    }

    public long[] InventoryDeltas { get; }

    public IReadOnlyList<PlannedExpansion> Entries { get; }
}

/// <summary>
/// Builds facilities one rung larger. The manual is unusually exact here: each
/// point of capacity costs one lumber and one steel, expansion requires no
/// labour, and the work completes the following turn.
/// </summary>
/// <remarks>
/// "Completes next turn" needs no pending state of its own, because
/// <see cref="TurnPhase.Construction"/> runs after
/// <see cref="TurnPhase.Production"/>: this turn's output was already decided
/// against the old size, so a facility built now first produces at its new size
/// next turn. What is *not* modelled is the scaffolding the original shows on
/// the dialog while the work is under way — a presentation state with no
/// mechanical effect.
/// </remarks>
internal static class ExpansionPlanner
{
    public static ExpansionPlan Create(WorldState state, TurnOrders orders, long[] alreadySpent)
    {
        var definition = state.Definition;
        var commodityCount = definition.Commodities.Count;
        var available = state.CopyAvailableInventory();
        for (var index = 0; index < available.Length; index++)
        {
            available[index] += alreadySpent[index];
        }

        var deltas = new long[available.Length];
        var entries = new List<PlannedExpansion>();

        for (var countryValue = 0; countryValue < orders.Count; countryValue++)
        {
            var country = new CountryId(countryValue);
            foreach (var order in orders[country].Expansions)
            {
                if ((uint)order.Facility.Value >= (uint)definition.ProductionFacilities.Count)
                {
                    throw new ArgumentException(
                        $"Country {countryValue} refers to missing facility {order.Facility.Value}.",
                        nameof(orders));
                }

                var facility = definition.ProductionFacilities[order.Facility.Value];
                if (facility.CapacityLadder is not { } ladder ||
                    definition.ExpansionCostPerCapacityPoint.Count == 0)
                {
                    continue;
                }

                var from = state.GetProductionCapacity(country, facility.Id) ?? 0;
                var to = ladder.NextAbove(from);
                var points = to - from;

                var cost = definition.ExpansionCostPerCapacityPoint
                    .Select(item => new CommodityQuantity(
                        item.Commodity, checked(item.Quantity * points)))
                    .ToArray();

                var affordable = cost.All(item =>
                    available[(countryValue * commodityCount) + item.Commodity.Value] >= item.Quantity);
                if (!affordable)
                {
                    continue;
                }

                foreach (var item in cost)
                {
                    var offset = checked((countryValue * commodityCount) + item.Commodity.Value);
                    available[offset] -= item.Quantity;
                    deltas[offset] = checked(deltas[offset] - item.Quantity);
                }

                entries.Add(new PlannedExpansion(
                    country, facility.Id, from, to, Array.AsReadOnly(cost)));
            }
        }

        return new ExpansionPlan(deltas, entries);
    }
}
