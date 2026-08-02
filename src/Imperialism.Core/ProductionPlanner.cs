namespace Imperialism.Core;

internal sealed record PlannedProduction(
    CountryId Country,
    ProductionOrder Order,
    long CompletedCycles,
    long CapacityUsed,
    IReadOnlyList<CommodityQuantity> Consumed,
    IReadOnlyList<CommodityQuantity> Produced);

internal sealed class ProductionPlan
{
    public ProductionPlan(long[] inventoryDeltas, IEnumerable<PlannedProduction> entries)
    {
        InventoryDeltas = inventoryDeltas;
        Entries = Array.AsReadOnly(entries.ToArray());
    }

    public long[] InventoryDeltas { get; }

    public IReadOnlyList<PlannedProduction> Entries { get; }
}

internal static class ProductionPlanner
{
    public static ProductionPlan Create(WorldState state, TurnOrders orders)
    {
        var definition = state.Definition;
        var commodityCount = definition.Commodities.Count;
        var available = state.CopyAvailableInventory();
        var deltas = new long[available.Length];
        var entries = new List<PlannedProduction>();

        for (var countryValue = 0; countryValue < orders.Count; countryValue++)
        {
            var country = new CountryId(countryValue);
            var remainingCapacity = definition.ProductionFacilities
                .Select(facility => facility.CapacityMode == ProductionCapacityMode.Unlimited
                    ? long.MaxValue
                    : state.GetProductionCapacity(country, facility.Id)!.Value)
                .ToArray();

            foreach (var order in orders[country].Production)
            {
                if ((uint)order.Recipe.Value >= (uint)definition.ProductionRecipes.Count)
                {
                    throw new ArgumentException(
                        $"Country {countryValue} refers to missing production recipe {order.Recipe.Value}.",
                        nameof(orders));
                }

                var recipe = definition.ProductionRecipes[order.Recipe.Value];
                var completed = order.RequestedCycles;
                var facility = definition.ProductionFacilities[recipe.Facility.Value];
                if (facility.CapacityMode == ProductionCapacityMode.Limited)
                {
                    completed = Math.Min(completed, remainingCapacity[recipe.Facility.Value] / recipe.CapacityCost);
                }

                foreach (var input in recipe.Inputs)
                {
                    var offset = checked((countryValue * commodityCount) + input.Commodity.Value);
                    completed = Math.Min(completed, available[offset] / input.Quantity);
                }

                var capacityUsed = checked(completed * recipe.CapacityCost);
                if (facility.CapacityMode == ProductionCapacityMode.Limited)
                {
                    remainingCapacity[recipe.Facility.Value] -= capacityUsed;
                }

                var consumed = Scale(recipe.Inputs, completed);
                var produced = Scale(recipe.Outputs, completed);
                foreach (var quantity in consumed)
                {
                    var offset = checked((countryValue * commodityCount) + quantity.Commodity.Value);
                    available[offset] -= quantity.Quantity;
                    deltas[offset] = checked(deltas[offset] - quantity.Quantity);
                }

                // Outputs are deliberately not added to `available`: production from this
                // turn cannot feed another recipe until the following turn.
                foreach (var quantity in produced)
                {
                    var offset = checked((countryValue * commodityCount) + quantity.Commodity.Value);
                    deltas[offset] = checked(deltas[offset] + quantity.Quantity);
                }

                entries.Add(new PlannedProduction(
                    country,
                    order,
                    completed,
                    capacityUsed,
                    consumed,
                    produced));
            }
        }

        return new ProductionPlan(deltas, entries);
    }

    private static IReadOnlyList<CommodityQuantity> Scale(
        IReadOnlyList<CommodityQuantity> quantities,
        long cycles) =>
        cycles == 0
            ? Array.Empty<CommodityQuantity>()
            : Array.AsReadOnly(quantities.Select(item =>
                new CommodityQuantity(item.Commodity, checked(item.Quantity * cycles))).ToArray());
}
