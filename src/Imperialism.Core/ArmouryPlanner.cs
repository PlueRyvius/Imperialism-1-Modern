namespace Imperialism.Core;

internal sealed record PlannedRegimentPurchase(
    CountryId Country,
    ArmyCategory Category,
    ArmyTypeId Type,
    long Count,
    WorkerGrade WorkerGrade,
    long WorkersTaken,
    IReadOnlyList<CommodityQuantity> Paid,
    long CashPaid);

internal sealed record PlannedRegimentPurchaseRefusal(
    CountryId Country,
    ArmyCategory Category,
    long Requested,
    RegimentPurchaseRefusal Reason);

/// <summary>
/// The Armoury. Planned inside <see cref="ProductionPlanner"/>, after the University and
/// before any recipe and before the market — a chosen rule.
/// </summary>
/// <remarks>
/// It runs there for the University's reason: the workers "can never return to
/// industry" and leave the moment the order is given, so this turn's recipes must see
/// the smaller pool; and the armaments, horses, fuel and cash leave before recipes and
/// the market can claim them. See <c>docs/formulas/army.md</c>.
/// </remarks>
internal static class ArmouryPlanner
{
    /// <summary>
    /// The type a category builds: the highest era whose technology the country holds
    /// as the turn opens, among the types the Armoury sells. Null where the category
    /// sells nothing — militia, Generals, or a world without a catalogue.
    /// </summary>
    public static ArmyTypeDefinition? ResolveCategory(WorldState state, CountryId country, ArmyCategory category)
    {
        ArmyTypeDefinition? best = null;
        foreach (var type in state.Definition.ArmyTypes)
        {
            if (type.Category != category || type.Purchase is null)
            {
                continue;
            }

            if (type.RequiredTechnology is { } required && !state.HasTechnology(country, required))
            {
                continue;
            }

            if (best is null || type.Era > best.Era)
            {
                best = type;
            }
        }

        return best;
    }

    /// <summary>
    /// Plans one country's purchases in list order. <paramref name="healthy"/> is the
    /// healthy workforce per grade the turn opened with, less whatever the Trade School
    /// and the University have already taken, and is spent here.
    /// </summary>
    public static void PlanPurchases(
        WorldState state,
        CountryId country,
        IReadOnlyList<RegimentPurchaseOrder> orders,
        long[] available,
        long[] deltas,
        ref long cash,
        long[] healthy,
        List<PlannedRegimentPurchase> planned,
        List<PlannedRegimentPurchaseRefusal> refusals)
    {
        foreach (var order in orders)
        {
            var refusal = Plan(state, country, order, available, deltas, ref cash, healthy, planned);
            if (refusal is { } reason)
            {
                refusals.Add(new PlannedRegimentPurchaseRefusal(country, order.Category, order.Count, reason));
            }
        }
    }

    private static RegimentPurchaseRefusal? Plan(
        WorldState state,
        CountryId country,
        RegimentPurchaseOrder order,
        long[] available,
        long[] deltas,
        ref long cash,
        long[] healthy,
        List<PlannedRegimentPurchase> planned)
    {
        if (order.Count <= 0)
        {
            return RegimentPurchaseRefusal.NonPositiveCount;
        }

        if (ResolveCategory(state, country, order.Category) is not { Purchase: { } price } type)
        {
            return RegimentPurchaseRefusal.NotPurchasable;
        }

        if (state.GetCountryCapital(country) is null)
        {
            return RegimentPurchaseRefusal.NoCapital;
        }

        // Every check divides rather than multiplies, so a count whose bill would
        // overflow a long is simply unaffordable and refused like any other.
        var grade = (int)price.WorkerGrade;
        if (!CanAfford(1, order.Count, healthy[grade]))
        {
            return RegimentPurchaseRefusal.NotEnoughWorkers;
        }

        var commodityCount = state.Definition.Commodities.Count;
        if (price.Bill.Any(item => !CanAfford(
                item.Quantity,
                order.Count,
                available[(country.Value * commodityCount) + item.Commodity.Value])))
        {
            return RegimentPurchaseRefusal.NotEnoughCommodities;
        }

        if (!CanAfford(price.CashCost, order.Count, cash))
        {
            return RegimentPurchaseRefusal.NotEnoughCash;
        }

        var paid = price.Bill
            .Select(item => new CommodityQuantity(item.Commodity, item.Quantity * order.Count))
            .ToArray();
        foreach (var item in paid)
        {
            var offset = (country.Value * commodityCount) + item.Commodity.Value;
            available[offset] -= item.Quantity;
            deltas[offset] = checked(deltas[offset] - item.Quantity);
        }

        var cashCost = price.CashCost * order.Count;
        cash -= cashCost;
        healthy[grade] -= order.Count;
        planned.Add(new PlannedRegimentPurchase(
            country,
            order.Category,
            type.Id,
            order.Count,
            price.WorkerGrade,
            order.Count,
            Array.AsReadOnly(paid),
            cashCost));
        return null;
    }

    private static bool CanAfford(long perUnit, long count, long held) =>
        held >= 0 && (perUnit <= 0 || count <= held / perUnit);
}

/// <summary>
/// Deployment: marching to an adjacent province and moving further by rail. Resolved in
/// <c>Conflict</c> after the naval orders, countries in id order and orders in list
/// order — a chosen rule.
/// </summary>
/// <remarks>
/// Every move goes to the country's own province as the turn opened; entering anyone
/// else's is an attack, which is the land-battle slice. See <c>docs/formulas/army.md</c>.
/// </remarks>
internal static class RegimentMovePlanner
{
    /// <summary>"Five points of transport capacity are needed to move each armaments point."</summary>
    public const long CapacityPerArmament = 5;

    /// <summary>
    /// Pass 1 of <c>Conflict</c>: every move. An order whose destination was not the
    /// country's own as the turn opened is an <b>attack</b>, set aside in
    /// <paramref name="attacks"/> for <see cref="LandBattlePlanner"/> without being
    /// checked here — the destination decides the verb.
    /// </summary>
    /// <param name="ownersAtTurnStart">Province owners as the turn opened.</param>
    /// <param name="railBudget">
    /// Each country's total transport capacity as the turn opened — the regiment rail
    /// budget, separate from commodity transport and not carried over.
    /// </param>
    /// <param name="acted">Regiments named in an accepted order; moves are added here.</param>
    /// <param name="attacks">Receives every attack, countries in id order, orders in list order.</param>
    public static IReadOnlyList<TurnEvent> Resolve(
        WorldState state,
        TurnOrders orders,
        int turnNumber,
        IReadOnlyList<CountryId?> ownersAtTurnStart,
        long[] railBudget,
        HashSet<RegimentId> acted,
        List<(CountryId Country, RegimentMoveOrder Order)> attacks)
    {
        var events = new List<TurnEvent>();
        var remaining = railBudget.ToArray();
        var provinceCount = state.Definition.Map.Provinces.Count;
        for (var countryValue = 0; countryValue < orders.Count; countryValue++)
        {
            var country = new CountryId(countryValue);
            foreach (var order in orders[country].RegimentMoves)
            {
                if ((uint)order.Destination.Value < (uint)provinceCount &&
                    ownersAtTurnStart[order.Destination.Value] != country)
                {
                    attacks.Add((country, order));
                    continue;
                }

                if (Check(state, country, order, acted, remaining, out var from, out var mode, out var cost)
                    is { } refusal)
                {
                    events.Add(new RegimentMoveRefusedEvent(
                        turnNumber, country, order.Regiments, order.Destination, refusal));
                    continue;
                }

                remaining[countryValue] -= cost;
                foreach (var regiment in order.Regiments)
                {
                    state.MoveRegiment(regiment, order.Destination);
                    acted.Add(regiment);
                }

                events.Add(new RegimentsMovedEvent(
                    turnNumber, country, order.Regiments, from, order.Destination, mode, cost));
            }
        }

        return events;
    }

    /// <summary>
    /// The checks a move and an attack share, against live state: who is named, that they
    /// stand together and may leave, and that the destination is another province.
    /// </summary>
    internal static RegimentMoveRefusal? CheckCommon(
        WorldState state,
        CountryId country,
        RegimentMoveOrder order,
        HashSet<RegimentId> acted,
        out ProvinceId from,
        out IReadOnlyList<(Regiment Regiment, ArmyTypeDefinition Type)> named)
    {
        from = default;
        named = [];
        var definition = state.Definition;
        if (order.Regiments.Count == 0)
        {
            return RegimentMoveRefusal.NoRegiments;
        }

        if (order.Regiments.Distinct().Count() != order.Regiments.Count)
        {
            return RegimentMoveRefusal.DuplicateRegiment;
        }

        var regiments = new List<(Regiment Regiment, ArmyTypeDefinition Type)>(order.Regiments.Count);
        foreach (var id in order.Regiments)
        {
            if (state.GetRegiment(id) is not { } regiment)
            {
                return RegimentMoveRefusal.NoSuchRegiment;
            }

            if (regiment.Owner != country)
            {
                return RegimentMoveRefusal.NotYours;
            }

            if (acted.Contains(id))
            {
                return RegimentMoveRefusal.AlreadyMoved;
            }

            regiments.Add((regiment, definition.ArmyTypes[regiment.Type.Value]));
        }

        named = regiments;
        from = regiments[0].Regiment.Province;
        var source = from;
        if (regiments.Any(item => item.Regiment.Province != source))
        {
            return RegimentMoveRefusal.NotTogether;
        }

        if (regiments.Any(static item => !item.Type.LeavesProvince))
        {
            return RegimentMoveRefusal.CannotLeaveProvince;
        }

        var destination = order.Destination;
        if ((uint)destination.Value >= (uint)definition.Map.Provinces.Count)
        {
            return RegimentMoveRefusal.UnknownProvince;
        }

        if (destination == from)
        {
            return RegimentMoveRefusal.SameProvince;
        }

        return null;
    }

    private static RegimentMoveRefusal? Check(
        WorldState state,
        CountryId country,
        RegimentMoveOrder order,
        HashSet<RegimentId> acted,
        long[] remaining,
        out ProvinceId from,
        out RegimentMoveMode mode,
        out long cost)
    {
        mode = RegimentMoveMode.March;
        cost = 0;
        if (CheckCommon(state, country, order, acted, out from, out var regiments) is { } refusal)
        {
            return refusal;
        }

        var definition = state.Definition;
        var destination = order.Destination;
        if (definition.ProvinceAdjacency.AreAdjacent(from, destination))
        {
            return null;
        }

        mode = RegimentMoveMode.Rail;
        if (!ShareRailComponent(state, country, from, destination))
        {
            return RegimentMoveRefusal.NotConnectedByRail;
        }

        if (regiments.Any(static item => !item.Type.MovesByRail))
        {
            return RegimentMoveRefusal.CannotMoveByRail;
        }

        // Divides rather than multiplies, so no order of regiments can overflow it.
        var budget = remaining[country.Value];
        var arms = 0L;
        foreach (var (_, type) in regiments)
        {
            arms += type.Arms ?? 0;
            if (budget < 0 || arms > budget / CapacityPerArmament)
            {
                return RegimentMoveRefusal.NotEnoughRailCapacity;
            }
        }

        cost = arms * CapacityPerArmament;
        return null;
    }

    /// <summary>
    /// Whether some cell of each province sits in one component of the country's own
    /// rail — "moved by train through friendly territory" read as an unbroken line of
    /// own rail, a chosen rule.
    /// </summary>
    private static bool ShareRailComponent(WorldState state, CountryId country, ProvinceId from, ProvinceId to)
    {
        var rail = state.GetRailConnectivity(country);
        var map = state.Definition.Map;
        var components = new HashSet<int>();
        foreach (var cell in map.GetCells(from))
        {
            if (rail.GetComponentId(cell) is { } component)
            {
                components.Add(component);
            }
        }

        if (components.Count == 0)
        {
            return false;
        }

        foreach (var cell in map.GetCells(to))
        {
            if (rail.GetComponentId(cell) is { } component && components.Contains(component))
            {
                return true;
            }
        }

        return false;
    }
}
