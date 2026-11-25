namespace Imperialism.Core;

/// <summary>
/// One step on the Trade School's ladder: what it costs to raise one worker a
/// grade.
/// </summary>
/// <remarks>
/// The manual says only that "training costs paper and cash". The original's own
/// help resources price both steps — an untrained worker plus one paper and $100
/// becomes trained, a trained worker plus two paper and $1,000 becomes expert — and
/// those are what the legacy importer writes. See
/// <c>docs/disasm/definitive-original-data.md</c>.
/// </remarks>
public sealed record WorkerTrainingRule
{
    private readonly IReadOnlyList<CommodityQuantity> _cost;

    public WorkerTrainingRule(WorkerGrade from, IEnumerable<CommodityQuantity> cost, long cashCost)
    {
        ArgumentNullException.ThrowIfNull(cost);
        if (from is not (WorkerGrade.Untrained or WorkerGrade.Trained))
        {
            throw new ArgumentOutOfRangeException(
                nameof(from), "Only untrained and trained workers can be trained further.");
        }

        ArgumentOutOfRangeException.ThrowIfNegative(cashCost);
        var costs = cost.ToArray();
        if (costs.Select(static item => item.Commodity).Distinct().Count() != costs.Length)
        {
            throw new ArgumentException(
                "A training cost cannot name a commodity twice.", nameof(cost));
        }

        From = from;
        _cost = Array.AsReadOnly(costs);
        CashCost = cashCost;
    }

    /// <summary>The grade a trainee starts the turn at.</summary>
    public WorkerGrade From { get; }

    /// <summary>The grade a trainee ends the turn at: always the next one up.</summary>
    public WorkerGrade To => (WorkerGrade)((int)From + 1);

    /// <summary>Commodities charged per worker trained.</summary>
    public IReadOnlyList<CommodityQuantity> Cost => _cost;

    /// <summary>Cash charged per worker trained.</summary>
    public long CashCost { get; }
}

/// <summary>
/// The Trade School: which promotions a world offers and what each costs. Null on
/// <see cref="WorldDefinition.WorkerTraining"/> means the world trains nobody,
/// which is how every world behaved before version 27.
/// </summary>
public sealed class WorkerTrainingSettings
{
    private readonly WorkerTrainingRule?[] _byGrade = new WorkerTrainingRule?[WorkerGrades.Count];
    private readonly IReadOnlyList<WorkerTrainingRule> _rules;

    public WorkerTrainingSettings(IEnumerable<WorkerTrainingRule> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        var array = rules.ToArray();
        foreach (var rule in array)
        {
            if (rule is null)
            {
                throw new ArgumentException("Training rules cannot contain null entries.", nameof(rules));
            }

            if (_byGrade[(int)rule.From] is not null)
            {
                throw new ArgumentException(
                    $"Training from {rule.From} is priced twice.", nameof(rules));
            }

            _byGrade[(int)rule.From] = rule;
        }

        _rules = Array.AsReadOnly(array);
    }

    public IReadOnlyList<WorkerTrainingRule> Rules => _rules;

    /// <summary>The step that raises a worker out of this grade, or null if none is offered.</summary>
    public WorkerTrainingRule? GetRule(WorkerGrade from) =>
        Enum.IsDefined(from) ? _byGrade[(int)from] : null;
}

/// <summary>
/// The Power Plant's terms: which commodity it burns, how much labour a unit of it
/// becomes, and how much it may burn in a turn.
/// </summary>
/// <remarks>
/// <b>Both numbers are the original's own</b>: "1 fuel → 6 power, with up to 100 fuel
/// per turn", from its help resources. The manual supplies the shape — power "is not
/// a commodity", "cannot be sold or stored", needs no labour to make, "adds directly to
/// your labour total" on the turn it is made, and "is used in advance of any of your
/// human labour".
/// <para>
/// <b>The 100 is read as a flat cap per country per turn and does not scale with the
/// plant's capacity.</b> That is a reading, not a finding: the resource names one
/// figure and no capacity. A country needs a plant of any size to burn at all.
/// </para>
/// </remarks>
public sealed class PowerSettings
{
    public PowerSettings(
        CommodityId fuel,
        ProductionFacilityId plant,
        long powerPerFuel,
        long maxFuelPerTurn)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(powerPerFuel);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxFuelPerTurn);
        _ = checked(powerPerFuel * maxFuelPerTurn);
        Fuel = fuel;
        Plant = plant;
        PowerPerFuel = powerPerFuel;
        MaxFuelPerTurn = maxFuelPerTurn;
    }

    /// <summary>What the plant burns. Fuel, refined from oil, in the original.</summary>
    public CommodityId Fuel { get; }

    /// <summary>The facility a country must hold, at any size, to burn anything.</summary>
    public ProductionFacilityId Plant { get; }

    /// <summary>Labour one unit of fuel becomes. Six in the original.</summary>
    public long PowerPerFuel { get; }

    /// <summary>The most fuel one country may burn in one turn. One hundred in the original.</summary>
    public long MaxFuelPerTurn { get; }
}
