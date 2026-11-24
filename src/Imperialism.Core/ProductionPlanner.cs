namespace Imperialism.Core;

internal sealed record PlannedProduction(
    CountryId Country,
    ProductionOrder Order,
    long CompletedCycles,
    long CapacityUsed,
    long LabourUsed,
    IReadOnlyList<CommodityQuantity> Consumed,
    IReadOnlyList<CommodityQuantity> Produced);

internal sealed record PlannedTraining(
    CountryId Country,
    WorkerTrainingRule Rule,
    long Count,
    IReadOnlyList<CommodityQuantity> Paid,
    long CashPaid);

internal sealed record PlannedTrainingRefusal(
    CountryId Country,
    WorkerGrade From,
    long Requested,
    WorkerTrainingRefusal Reason);

internal sealed record PlannedPower(
    CountryId Country,
    CommodityId Fuel,
    long FuelBurned,
    long PowerMade,
    long PowerSpent);

internal sealed record PlannedPowerRefusal(
    CountryId Country,
    long Requested,
    PowerRefusal Reason);

internal sealed class ProductionPlan
{
    public ProductionPlan(
        long[] inventoryDeltas,
        IEnumerable<PlannedProduction> entries,
        IEnumerable<PlannedTraining> training,
        IEnumerable<PlannedTrainingRefusal> trainingRefusals,
        IEnumerable<PlannedPower> power,
        IEnumerable<PlannedPowerRefusal> powerRefusals,
        long[] cashClaimed,
        long[] humanLabourClaimed)
    {
        InventoryDeltas = inventoryDeltas;
        Entries = Array.AsReadOnly(entries.ToArray());
        Training = Array.AsReadOnly(training.ToArray());
        TrainingRefusals = Array.AsReadOnly(trainingRefusals.ToArray());
        Power = Array.AsReadOnly(power.ToArray());
        PowerRefusals = Array.AsReadOnly(powerRefusals.ToArray());
        CashClaimed = cashClaimed;
        HumanLabourClaimed = humanLabourClaimed;
    }

    /// <summary>
    /// Every recipe's inputs and outputs, and also the paper training costs and the
    /// fuel the plant burns — all three leave the warehouse in the same phase.
    /// </summary>
    public long[] InventoryDeltas { get; }

    public IReadOnlyList<PlannedProduction> Entries { get; }

    public IReadOnlyList<PlannedTraining> Training { get; }

    public IReadOnlyList<PlannedTrainingRefusal> TrainingRefusals { get; }

    public IReadOnlyList<PlannedPower> Power { get; }

    public IReadOnlyList<PlannedPowerRefusal> PowerRefusals { get; }

    /// <summary>Cash per country the Trade School will charge, so trade cannot spend it first.</summary>
    public long[] CashClaimed { get; }

    /// <summary>
    /// Human labour per country already spoken for: what the trainees would have
    /// supplied and what recipes drew after power ran out. The railyard prices against
    /// what is left. Power is not in it, because power is production's alone.
    /// </summary>
    public long[] HumanLabourClaimed { get; }
}

/// <summary>
/// The Industry screen's turn: the Trade School, then the Power Plant, then every
/// recipe in the order it was asked for.
/// </summary>
/// <remarks>
/// The first two run first because they change what the third can spend. Training
/// "takes him out of the labour pool for the turn", so trainees must leave the pool
/// before any recipe draws on it; and power "is used in advance of any of your human
/// labour", so it must be in the pool before any recipe does. See
/// <c>docs/formulas/production.md</c>.
/// </remarks>
internal static class ProductionPlanner
{
    public static ProductionPlan Create(WorldState state, TurnOrders orders)
    {
        var definition = state.Definition;
        var commodityCount = definition.Commodities.Count;
        var available = state.CopyAvailableInventory();
        var deltas = new long[available.Length];
        var entries = new List<PlannedProduction>();
        var training = new List<PlannedTraining>();
        var trainingRefusals = new List<PlannedTrainingRefusal>();
        var power = new List<PlannedPower>();
        var powerRefusals = new List<PlannedPowerRefusal>();
        var cashClaimed = new long[orders.Count];
        var humanLabourClaimed = new long[orders.Count];

        for (var countryValue = 0; countryValue < orders.Count; countryValue++)
        {
            var country = new CountryId(countryValue);
            var countryOrders = orders[country];
            var remainingCapacity = definition.ProductionFacilities
                .Select(facility => facility.CapacityMode == ProductionCapacityMode.Unlimited
                    ? long.MaxValue
                    : state.GetProductionCapacity(country, facility.Id)!.Value)
                .ToArray();

            // One pool for the whole country, spent in the order the requests
            // arrive, exactly like facility capacity but shared across every
            // facility. A world that defines no feeding has no workforce to
            // draw on and no way to invent one, so labour does not bind there
            // at all — that is what it did before labour was priced.
            var remainingLabour = definition.Feeding is null
                ? long.MaxValue
                : state.GetAvailableLabour(country);

            // (a) The Trade School. Each step is all or nothing and is counted
            // against the workforce the turn opened with.
            var cash = state.GetCash(country);
            foreach (var grade in WorkerGrades.All)
            {
                var requested = countryOrders.Training[grade];
                if (requested == 0)
                {
                    continue;
                }

                var refusal = PlanTraining(
                    state, country, grade, requested, available, deltas, ref cash, training);
                if (refusal is { } reason)
                {
                    trainingRefusals.Add(new PlannedTrainingRefusal(country, grade, requested, reason));
                    continue;
                }

                cashClaimed[countryValue] = checked(cashClaimed[countryValue] + training[^1].CashPaid);
                if (definition.Feeding is { } feeding)
                {
                    var withheld = checked(requested * feeding.GetLabour(grade));
                    remainingLabour -= withheld;
                    humanLabourClaimed[countryValue] = checked(humanLabourClaimed[countryValue] + withheld);
                }
            }

            // (b) The Power Plant. What it makes is this turn's alone.
            var remainingPower = 0L;
            PlannedPower? burned = null;
            if (countryOrders.BurnFuel > 0)
            {
                var refusal = PlanPower(state, country, countryOrders.BurnFuel, available, deltas);
                if (refusal is { } reason)
                {
                    powerRefusals.Add(new PlannedPowerRefusal(country, countryOrders.BurnFuel, reason));
                }
                else
                {
                    var settings = definition.Power!;
                    remainingPower = checked(countryOrders.BurnFuel * settings.PowerPerFuel);
                    burned = new PlannedPower(country, settings.Fuel, countryOrders.BurnFuel, remainingPower, 0);
                }
            }

            // (c) Recipes, drawing power before people.
            foreach (var order in countryOrders.Production)
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

                if (remainingLabour != long.MaxValue)
                {
                    completed = Math.Min(
                        completed, checked(remainingPower + remainingLabour) / recipe.LabourCost);
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

                var labourUsed = checked(completed * recipe.LabourCost);
                var powerUsed = Math.Min(remainingPower, labourUsed);
                remainingPower -= powerUsed;
                if (remainingLabour != long.MaxValue)
                {
                    var humanUsed = labourUsed - powerUsed;
                    remainingLabour -= humanUsed;
                    humanLabourClaimed[countryValue] = checked(humanLabourClaimed[countryValue] + humanUsed);
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
                    labourUsed,
                    consumed,
                    produced));
            }

            // (d) Whatever power is left is simply not recorded anywhere: there is
            // no store for it to go into, which is the whole of "cannot be stored".
            if (burned is not null)
            {
                power.Add(burned with { PowerSpent = burned.PowerMade - remainingPower });
            }
        }

        return new ProductionPlan(
            deltas,
            entries,
            training,
            trainingRefusals,
            power,
            powerRefusals,
            cashClaimed,
            humanLabourClaimed);
    }

    private static WorkerTrainingRefusal? PlanTraining(
        WorldState state,
        CountryId country,
        WorkerGrade grade,
        long requested,
        long[] available,
        long[] deltas,
        ref long cash,
        List<PlannedTraining> planned)
    {
        if (state.Definition.WorkerTraining?.GetRule(grade) is not { } rule)
        {
            return WorkerTrainingRefusal.NotOffered;
        }

        // Only the healthy can be spared: a sick worker supplies no labour this
        // turn, so there is nothing to take out of the pool.
        var healthy = state.GetWorkers(country, grade) - state.GetSickWorkers(country, grade);
        if (requested > healthy)
        {
            return WorkerTrainingRefusal.NotEnoughWorkers;
        }

        var commodityCount = state.Definition.Commodities.Count;
        var paid = rule.Cost
            .Select(item => new CommodityQuantity(item.Commodity, checked(item.Quantity * requested)))
            .ToArray();
        if (paid.Any(item =>
                available[(country.Value * commodityCount) + item.Commodity.Value] < item.Quantity))
        {
            return WorkerTrainingRefusal.NotEnoughCommodities;
        }

        var cashCost = checked(rule.CashCost * requested);
        if (cashCost > cash)
        {
            return WorkerTrainingRefusal.NotEnoughCash;
        }

        foreach (var item in paid)
        {
            var offset = (country.Value * commodityCount) + item.Commodity.Value;
            available[offset] -= item.Quantity;
            deltas[offset] = checked(deltas[offset] - item.Quantity);
        }

        cash -= cashCost;
        planned.Add(new PlannedTraining(country, rule, requested, Array.AsReadOnly(paid), cashCost));
        return null;
    }

    private static PowerRefusal? PlanPower(
        WorldState state,
        CountryId country,
        long fuel,
        long[] available,
        long[] deltas)
    {
        if (state.Definition.Power is not { } settings)
        {
            return PowerRefusal.NotModelled;
        }

        if (state.GetProductionCapacity(country, settings.Plant) is not > 0)
        {
            return PowerRefusal.NoPowerPlant;
        }

        // Refused rather than trimmed to the limit: a chosen rule, the same
        // all-or-nothing shape a technology purchase has. See production.md.
        if (fuel > settings.MaxFuelPerTurn)
        {
            return PowerRefusal.OverTurnLimit;
        }

        var offset = (country.Value * state.Definition.Commodities.Count) + settings.Fuel.Value;
        if (available[offset] < fuel)
        {
            return PowerRefusal.NotEnoughFuel;
        }

        available[offset] -= fuel;
        deltas[offset] = checked(deltas[offset] - fuel);
        return null;
    }

    private static IReadOnlyList<CommodityQuantity> Scale(
        IReadOnlyList<CommodityQuantity> quantities,
        long cycles) =>
        cycles == 0
            ? Array.Empty<CommodityQuantity>()
            : Array.AsReadOnly(quantities.Select(item =>
                new CommodityQuantity(item.Commodity, checked(item.Quantity * cycles))).ToArray());
}
