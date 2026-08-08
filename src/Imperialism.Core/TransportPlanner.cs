namespace Imperialism.Core;

/// <summary>One country's starting transport capacity: the 1997 <c>tran</c> record.</summary>
public readonly record struct InitialTransportCapacity
{
    public InitialTransportCapacity(CountryId country, long capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(capacity);
        Country = country;
        Capacity = capacity;
    }

    public CountryId Country { get; }

    public long Capacity { get; }
}

/// <summary>One country's starting treasury: the 1997 <c>cash</c> record.</summary>
/// <remarks>
/// The record is <c>[country, amount]</c>, the same shape as <c>tran</c>, and
/// what a mission authors is authored design: <c>s3</c> gives one power ten
/// times another's. See <c>docs/formulas/money.md</c>.
/// </remarks>
public readonly record struct InitialCash
{
    public InitialCash(CountryId country, long amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        Country = country;
        Amount = amount;
    }

    public CountryId Country { get; }

    public long Amount { get; }
}

internal sealed record PlannedTransport(
    CountryId Country,
    long CapacityUsed,
    long CapacityAvailable,
    IReadOnlyList<CommodityQuantity> Moved,
    IReadOnlyList<CommodityQuantity> Converted,
    long CashEarned,
    IReadOnlyList<CommodityQuantity> Wasted);

internal sealed record PlannedRailyard(
    CountryId Country,
    long FromCapacity,
    long ToCapacity,
    long LabourUsed,
    IReadOnlyList<CommodityQuantity> Paid);

internal sealed class RailyardPlan
{
    public RailyardPlan(long[] inventoryDeltas, IEnumerable<PlannedRailyard> entries)
    {
        InventoryDeltas = inventoryDeltas;
        Entries = Array.AsReadOnly(entries.ToArray());
    }

    public long[] InventoryDeltas { get; }

    public IReadOnlyList<PlannedRailyard> Entries { get; }
}

/// <summary>
/// Moves what <see cref="TurnPhase.Extraction"/> gathered onto the network, up to
/// what the country can carry, and leaves the rest on the ground.
/// </summary>
/// <remarks>
/// The manual's Transport screen is a slider per commodity against one shared
/// capacity bar: "transport capacity is the total number of commodities that your
/// network can move each turn". One point moves one unit, whatever it is.
/// <para>
/// <b>What is not moved is lost, and that is a chosen rule rather than a
/// finding.</b> The pool refills from the tiles next turn and the manual never
/// says whether yesterday's unmoved grain waits at the depot. Losing it is the
/// reading that makes capacity matter, and it is reported in the event so a
/// player can see the cost. See <c>docs/formulas/transport.md</c>.
/// </para>
/// </remarks>
internal static class TransportPlanner
{
    public static IReadOnlyList<PlannedTransport> Create(
        WorldState state,
        TurnOrders orders,
        IReadOnlyList<PlannedExtraction> gathered,
        long[] capacityAtTurnStart)
    {
        var definition = state.Definition;
        var commodityCount = definition.Commodities.Count;
        var results = new List<PlannedTransport>();

        foreach (var extraction in gathered)
        {
            var country = extraction.Country;

            // A world with no transport settings has no limit, which is how
            // every world behaved before capacity existed: everything gathered
            // went straight through.
            var unlimited = definition.Transport is null;
            var capacity = unlimited ? long.MaxValue : capacityAtTurnStart[country.Value];

            var pool = new long[commodityCount];
            foreach (var quantity in extraction.Collected)
            {
                pool[quantity.Commodity.Value] = checked(
                    pool[quantity.Commodity.Value] + quantity.Quantity);
            }

            var moved = new long[commodityCount];
            var remaining = capacity;

            // Sliders in the order the player set them, which is the same rule
            // production and capacity already use for contention.
            foreach (var order in orders[country].Transport)
            {
                if ((uint)order.Commodity.Value >= (uint)commodityCount)
                {
                    throw new ArgumentException(
                        $"Country {country.Value} allocates transport to missing commodity " +
                        $"{order.Commodity.Value}.",
                        nameof(orders));
                }

                var take = Math.Min(order.Quantity, pool[order.Commodity.Value] - moved[order.Commodity.Value]);
                take = Math.Min(take, remaining);
                if (take <= 0)
                {
                    continue;
                }

                moved[order.Commodity.Value] += take;
                if (remaining != long.MaxValue)
                {
                    remaining -= take;
                }
            }

            // An unlimited world has no sliders to set, so everything gathered
            // moves. Ordering nothing in a limited world moves nothing, which is
            // what every slider at zero means.
            if (unlimited)
            {
                Array.Copy(pool, moved, commodityCount);
            }

            var used = 0L;
            var earned = 0L;
            var movedQuantities = new List<CommodityQuantity>();
            var convertedQuantities = new List<CommodityQuantity>();
            var wastedQuantities = new List<CommodityQuantity>();
            for (var commodity = 0; commodity < commodityCount; commodity++)
            {
                if (moved[commodity] > 0)
                {
                    used = checked(used + moved[commodity]);
                    var quantity = new CommodityQuantity(new CommodityId(commodity), moved[commodity]);

                    // Gold and gems "never reach the industry warehouse"; what
                    // the network carries of them converts on arrival. They cost
                    // capacity like anything else, which is the whole point —
                    // carrying gold is carrying less food.
                    if (definition.Commodities[commodity].CashPerUnit is { } rate)
                    {
                        earned = checked(earned + (moved[commodity] * rate));
                        convertedQuantities.Add(quantity);
                    }
                    else
                    {
                        movedQuantities.Add(quantity);
                    }
                }

                var left = pool[commodity] - moved[commodity];
                if (left > 0)
                {
                    wastedQuantities.Add(new CommodityQuantity(new CommodityId(commodity), left));
                }
            }

            results.Add(new PlannedTransport(
                country,
                used,
                unlimited ? used : capacity,
                Array.AsReadOnly(movedQuantities.ToArray()),
                Array.AsReadOnly(convertedQuantities.ToArray()),
                earned,
                Array.AsReadOnly(wastedQuantities.ToArray())));
        }

        return Array.AsReadOnly(results.ToArray());
    }

    /// <summary>
    /// Buys transport capacity at the railyard, priced per point and paid from
    /// what earlier phases have not already committed.
    /// </summary>
    /// <remarks>
    /// Unlike a mill there is no ladder: "you can build as much transport
    /// capacity as you want, provided you have steel, lumber, and available
    /// labour." Also unlike a mill, it costs labour — expanding a facility does
    /// not — so an order is trimmed by what production left in the pool as well
    /// as by the warehouse.
    /// </remarks>
    public static RailyardPlan CreateRailyard(
        WorldState state,
        TurnOrders orders,
        long[] alreadySpent,
        long[] labourAlreadySpent)
    {
        var definition = state.Definition;
        var commodityCount = definition.Commodities.Count;
        var available = state.CopyAvailableInventory();
        for (var index = 0; index < available.Length; index++)
        {
            available[index] += alreadySpent[index];
        }

        var deltas = new long[available.Length];
        var entries = new List<PlannedRailyard>();
        if (definition.Transport is not { } transport || transport.CostPerCapacityPoint.Count == 0)
        {
            return new RailyardPlan(deltas, entries);
        }

        for (var countryValue = 0; countryValue < orders.Count; countryValue++)
        {
            var country = new CountryId(countryValue);
            var wanted = orders[country].BuildTransportCapacity;
            if (wanted <= 0)
            {
                continue;
            }

            // How many points the warehouse can cover, taking the tightest
            // commodity. Partial builds are the rule everywhere else here.
            var points = wanted;
            foreach (var item in transport.CostPerCapacityPoint)
            {
                var held = available[checked((countryValue * commodityCount) + item.Commodity.Value)];
                points = Math.Min(points, held / item.Quantity);
            }

            if (transport.LabourPerCapacityPoint > 0 && definition.Feeding is not null)
            {
                var labourLeft = state.GetAvailableLabour(country) - labourAlreadySpent[countryValue];
                points = Math.Min(points, Math.Max(0, labourLeft) / transport.LabourPerCapacityPoint);
            }

            if (points <= 0)
            {
                continue;
            }

            var paid = transport.CostPerCapacityPoint
                .Select(item => new CommodityQuantity(item.Commodity, checked(item.Quantity * points)))
                .ToArray();
            foreach (var item in paid)
            {
                var offset = checked((countryValue * commodityCount) + item.Commodity.Value);
                available[offset] -= item.Quantity;
                deltas[offset] = checked(deltas[offset] - item.Quantity);
            }

            var from = state.GetTransportCapacity(country);
            entries.Add(new PlannedRailyard(
                country,
                from,
                checked(from + points),
                checked(points * transport.LabourPerCapacityPoint),
                Array.AsReadOnly(paid)));
        }

        return new RailyardPlan(deltas, entries);
    }
}
