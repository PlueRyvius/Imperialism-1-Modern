namespace Imperialism.Core;

internal sealed record PlannedShipBuild(
    CountryId Country,
    ShipTypeId Type,
    long Count,
    IReadOnlyList<CommodityQuantity> Paid);

internal sealed record PlannedShipBuildRefusal(
    CountryId Country,
    ShipTypeId Type,
    long Requested,
    ShipBuildRefusal Reason);

internal sealed class ShipyardPlan
{
    public ShipyardPlan(
        long[] inventoryDeltas,
        IEnumerable<PlannedShipBuild> entries,
        IEnumerable<PlannedShipBuildRefusal> refusals)
    {
        InventoryDeltas = inventoryDeltas;
        Entries = Array.AsReadOnly(entries.ToArray());
        Refusals = Array.AsReadOnly(refusals.ToArray());
    }

    public long[] InventoryDeltas { get; }

    public IReadOnlyList<PlannedShipBuild> Entries { get; }

    public IReadOnlyList<PlannedShipBuildRefusal> Refusals { get; }
}

/// <summary>
/// Builds ships. The manual's Shipyard spends commodities and nothing else: "ships do
/// not require workers", and the executable's thirteen build bills carry no cash.
/// </summary>
/// <remarks>
/// Planned beside <see cref="ExpansionPlanner"/> and committed in
/// <see cref="TurnPhase.Construction"/>, which runs after
/// <see cref="TurnPhase.Trade"/>. That ordering is the whole of "new units … appear at
/// the start of the next turn": a ship built now is counted at the end of
/// Construction, after this turn's market has already spent its holds, so its cargo
/// first counts in next turn's trade.
/// <para>
/// <b>Each order is all or nothing — a chosen rule.</b> The Shipyard dialog shows a
/// shortage "in red text" rather than offering a smaller build, so an order the
/// warehouse cannot cover in full is refused whole rather than trimmed. Orders are paid
/// in the order given and a refused one does not stop the ones after it.
/// </para>
/// <para>
/// <b>Affordability is checked by division before anything is multiplied</b>, the
/// same way the Trade School's bill is, so a count too large to price is refused as
/// unaffordable rather than overflowing.
/// </para>
/// </remarks>
internal static class ShipyardPlanner
{
    public static ShipyardPlan Create(WorldState state, TurnOrders orders, long[] alreadySpent)
    {
        var definition = state.Definition;
        var commodityCount = definition.Commodities.Count;
        var available = state.CopyAvailableInventory();
        for (var index = 0; index < available.Length; index++)
        {
            available[index] += alreadySpent[index];
        }

        var deltas = new long[available.Length];
        var entries = new List<PlannedShipBuild>();
        var refusals = new List<PlannedShipBuildRefusal>();

        for (var countryValue = 0; countryValue < orders.Count; countryValue++)
        {
            var country = new CountryId(countryValue);
            foreach (var order in orders[country].ShipBuilds)
            {
                var refusal = Plan(state, country, order, available, deltas, out var paid);
                if (refusal is { } reason)
                {
                    refusals.Add(new PlannedShipBuildRefusal(country, order.Type, order.Count, reason));
                    continue;
                }

                entries.Add(new PlannedShipBuild(country, order.Type, order.Count, paid!));
            }
        }

        return new ShipyardPlan(deltas, entries, refusals);
    }

    private static ShipBuildRefusal? Plan(
        WorldState state,
        CountryId country,
        ShipBuildOrder order,
        long[] available,
        long[] deltas,
        out IReadOnlyList<CommodityQuantity>? paid)
    {
        paid = null;
        var definition = state.Definition;
        if ((uint)order.Type.Value >= (uint)definition.ShipTypes.Count)
        {
            return ShipBuildRefusal.UnknownType;
        }

        if (order.Count <= 0)
        {
            return ShipBuildRefusal.NonPositiveCount;
        }

        // Before anything is priced, so a refused order charges nothing. Knowledge is
        // read from the state the turn opened with: research bought this turn lands in
        // Investment, which runs last — the same snapshot the facility gate reads.
        var hull = definition.ShipTypes[order.Type.Value];
        if (hull.RequiredTechnology is { } required && !state.HasTechnology(country, required))
        {
            return ShipBuildRefusal.TechnologyNotKnown;
        }

        var commodityCount = definition.Commodities.Count;
        if (hull.BuildCost.Any(item => !CanAfford(
                item.Quantity,
                order.Count,
                available[(country.Value * commodityCount) + item.Commodity.Value])))
        {
            return ShipBuildRefusal.NotEnoughCommodities;
        }

        // Every total is now bounded by what the country holds, so none overflows.
        var bill = hull.BuildCost
            .Select(item => new CommodityQuantity(item.Commodity, item.Quantity * order.Count))
            .ToArray();
        foreach (var item in bill)
        {
            var offset = (country.Value * commodityCount) + item.Commodity.Value;
            available[offset] -= item.Quantity;
            deltas[offset] = checked(deltas[offset] - item.Quantity);
        }

        paid = Array.AsReadOnly(bill);
        return null;
    }

    private static bool CanAfford(long perUnit, long count, long held) =>
        held >= 0 && (perUnit <= 0 || count <= held / perUnit);
}
