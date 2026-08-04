namespace Imperialism.Core;

internal sealed record PlannedMigration(
    CountryId Country,
    long Requested,
    long Recruited,
    long SizeLimit,
    IReadOnlyList<CommodityQuantity> Paid);

internal sealed class MigrationPlan
{
    public MigrationPlan(long[] inventoryDeltas, IEnumerable<PlannedMigration> entries)
    {
        InventoryDeltas = inventoryDeltas;
        Entries = Array.AsReadOnly(entries.ToArray());
    }

    public long[] InventoryDeltas { get; }

    public IReadOnlyList<PlannedMigration> Entries { get; }
}

/// <summary>
/// Draws rural workers into industry, priced in the comforts of a developing
/// economy and capped by the size of the country.
/// </summary>
/// <remarks>
/// Unlike an expansion, a migration order is **not** all-or-nothing. The manual
/// describes a slider a player drags until something runs out, so asking for
/// more than the country can afford or is allowed brings as many as it can
/// rather than none — the cap trims the order, it does not reject it.
/// </remarks>
internal static class MigrationPlanner
{
    public static MigrationPlan Create(WorldState state, TurnOrders orders, long[] alreadySpent)
    {
        var definition = state.Definition;
        var commodityCount = definition.Commodities.Count;
        var available = state.CopyAvailableInventory();
        for (var index = 0; index < available.Length; index++)
        {
            available[index] += alreadySpent[index];
        }

        var deltas = new long[available.Length];
        var entries = new List<PlannedMigration>();

        if (definition.Migration is not { } migration)
        {
            return new MigrationPlan(deltas, entries);
        }

        for (var countryValue = 0; countryValue < orders.Count; countryValue++)
        {
            var country = new CountryId(countryValue);
            var requested = orders[country].RecruitWorkers;
            if (requested <= 0)
            {
                continue;
            }

            // "One-fourth of the number of provinces you own, rounded down", so
            // a country of three provinces recruits nobody however rich it is.
            var owned = 0;
            for (var province = 0; province < definition.Map.Provinces.Count; province++)
            {
                if (state.GetProvinceOwner(new ProvinceId(province)) == country)
                {
                    owned++;
                }
            }

            var sizeLimit = owned / migration.ProvincesPerRecruit;
            var recruited = Math.Min(requested, sizeLimit);

            foreach (var item in migration.CostPerWorker)
            {
                var offset = checked((countryValue * commodityCount) + item.Commodity.Value);
                recruited = Math.Min(recruited, available[offset] / item.Quantity);
            }

            if (recruited <= 0)
            {
                entries.Add(new PlannedMigration(country, requested, 0, sizeLimit, []));
                continue;
            }

            var paid = migration.CostPerWorker
                .Select(item => new CommodityQuantity(
                    item.Commodity, checked(item.Quantity * recruited)))
                .ToArray();

            foreach (var item in paid)
            {
                var offset = checked((countryValue * commodityCount) + item.Commodity.Value);
                available[offset] -= item.Quantity;
                deltas[offset] = checked(deltas[offset] - item.Quantity);
            }

            entries.Add(new PlannedMigration(
                country, requested, recruited, sizeLimit, Array.AsReadOnly(paid)));
        }

        return new MigrationPlan(deltas, entries);
    }
}
