namespace Imperialism.Core;

/// <summary>
/// How much labour one worker supplies per turn. The original's three grades
/// give 1, 2 and 4, but the values are content data rather than constants here.
/// </summary>
public enum WorkerGrade : byte
{
    Untrained,
    Trained,
    Expert,
}

public static class WorkerGrades
{
    private static readonly WorkerGrade[] Values =
    [
        WorkerGrade.Untrained,
        WorkerGrade.Trained,
        WorkerGrade.Expert,
    ];

    /// <summary>
    /// Lowest grade first. Hunger removes workers in this order, and the pool
    /// grows from this end too, since new arrivals are untrained.
    /// </summary>
    public static ReadOnlySpan<WorkerGrade> All => Values;

    public const int Count = 3;
}

/// <summary>One country's starting workforce.</summary>
public readonly record struct InitialWorkforce
{
    public InitialWorkforce(CountryId country, long untrained, long trained, long expert)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(untrained);
        ArgumentOutOfRangeException.ThrowIfNegative(trained);
        ArgumentOutOfRangeException.ThrowIfNegative(expert);
        Country = country;
        Untrained = untrained;
        Trained = trained;
        Expert = expert;
    }

    public CountryId Country { get; }

    public long Untrained { get; }

    public long Trained { get; }

    public long Expert { get; }

    public long this[WorkerGrade grade] => grade switch
    {
        WorkerGrade.Untrained => Untrained,
        WorkerGrade.Trained => Trained,
        WorkerGrade.Expert => Expert,
        _ => throw new ArgumentOutOfRangeException(nameof(grade)),
    };
}

/// <summary>
/// One entry in the repeating preference cycle: the commodities a worker at this
/// position in the cycle will eat happily.
/// </summary>
/// <remarks>
/// A group rather than a single commodity because the original's fourth
/// preference accepts livestock <em>or</em> fish, either being equally welcome.
/// </remarks>
public sealed record FoodPreference
{
    private readonly IReadOnlyList<CommodityId> _accepted;

    public FoodPreference(IEnumerable<CommodityId> accepted)
    {
        ArgumentNullException.ThrowIfNull(accepted);
        var array = accepted.ToArray();
        if (array.Length == 0)
        {
            throw new ArgumentException(
                "A preference no commodity satisfies would starve its workers by construction.",
                nameof(accepted));
        }

        if (array.Distinct().Count() != array.Length)
        {
            throw new ArgumentException("A preference cannot repeat a commodity.", nameof(accepted));
        }

        _accepted = Array.AsReadOnly(array);
    }

    public IReadOnlyList<CommodityId> Accepted => _accepted;
}

/// <summary>
/// What the workforce eats, and what each grade contributes.
/// </summary>
/// <remarks>
/// The preference cycle is walked one worker at a time, so a workforce of any
/// size splits in the documented proportions without a rounding rule. The
/// original's cycle is grain, fruit, grain, meat-or-fish: half the workers want
/// grain, a quarter fruit, and the rest meat or fish.
/// </remarks>
public sealed record FeedingSettings
{
    private readonly IReadOnlyList<FoodPreference> _preferenceCycle;
    private readonly IReadOnlyList<long> _labourByGrade;

    public FeedingSettings(
        IEnumerable<FoodPreference> preferenceCycle,
        IEnumerable<long> labourByGrade,
        CommodityId? cannedFood = null)
    {
        ArgumentNullException.ThrowIfNull(preferenceCycle);
        ArgumentNullException.ThrowIfNull(labourByGrade);
        var cycle = preferenceCycle.ToArray();
        if (cycle.Length == 0)
        {
            throw new ArgumentException("The preference cycle cannot be empty.", nameof(preferenceCycle));
        }

        if (cycle.Any(static entry => entry is null))
        {
            throw new ArgumentException("The preference cycle cannot contain null entries.", nameof(preferenceCycle));
        }

        var labour = labourByGrade.ToArray();
        if (labour.Length != WorkerGrades.Count)
        {
            throw new ArgumentException(
                $"Expected {WorkerGrades.Count} labour values, one per worker grade.",
                nameof(labourByGrade));
        }

        if (labour.Any(static value => value < 0))
        {
            throw new ArgumentOutOfRangeException(nameof(labourByGrade), "Labour cannot be negative.");
        }

        _preferenceCycle = Array.AsReadOnly(cycle);
        _labourByGrade = Array.AsReadOnly(labour);
        CannedFood = cannedFood;
    }

    public IReadOnlyList<FoodPreference> PreferenceCycle => _preferenceCycle;

    /// <summary>Labour per worker per turn, indexed by <see cref="WorkerGrade"/>.</summary>
    public IReadOnlyList<long> LabourByGrade => _labourByGrade;

    /// <summary>
    /// The fallback a worker eats when its preference is unavailable, without
    /// falling ill. Null where the world has no such commodity.
    /// </summary>
    public CommodityId? CannedFood { get; }

    public long GetLabour(WorkerGrade grade) => _labourByGrade[(int)grade];

    /// <summary>What the worker at <paramref name="index"/> in the workforce wants.</summary>
    public FoodPreference GetPreference(long index) =>
        _preferenceCycle[(int)(index % _preferenceCycle.Count)];
}
