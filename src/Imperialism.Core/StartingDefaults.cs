namespace Imperialism.Core;

/// <summary>
/// What a power begins with when the scenario does not say otherwise: the fair
/// start every skirmish uses.
/// </summary>
/// <remarks>
/// <para>
/// The original works this way and the corpus shows it plainly. A mission
/// scenario spells out each power's industry and workforce; a skirmish
/// scenario carries no such records at all and every power still starts
/// equipped, because the engine supplies the values. `s10`, `s11` and `s15` do
/// exactly that, and agree with each other exactly: mills at 2, factories at 1,
/// no refinery, and four untrained, two trained and one expert.
/// </para>
/// <para>
/// That is not an invented number. It is the manual's construction floor — a
/// mill is always built at capacity 2 and a factory begins at 1 — so **the fair
/// start and the bottom rung of the build ladder are the same thing.** See
/// <c>docs/formulas/production.md</c>.
/// </para>
/// <para>
/// Defaults are applied only to the countries a scenario names in
/// <see cref="ScenarioDefinition.DefaultStartCountries"/>. They are deliberately
/// not applied to every country: the original gives industry to its seven Great
/// Powers and not to the minor nations, and Core has no notion of which is
/// which, so guessing would hand a workforce to every statelet on the map.
/// </para>
/// </remarks>
public sealed class StartingDefaults
{
    private readonly IReadOnlyList<FacilityCapacityDefault> _productionCapacities;
    private readonly IReadOnlyList<TechnologyId> _technologies;

    public StartingDefaults(
        IEnumerable<FacilityCapacityDefault> productionCapacities,
        WorkforceDefault? workforce = null,
        IEnumerable<TechnologyId>? technologies = null)
    {
        ArgumentNullException.ThrowIfNull(productionCapacities);
        var capacities = productionCapacities.ToArray();
        if (capacities.Select(static item => item.Facility).Distinct().Count() != capacities.Length)
        {
            throw new ArgumentException(
                "Starting defaults cannot name a facility twice.",
                nameof(productionCapacities));
        }

        var known = technologies?.ToArray() ?? [];
        if (known.Distinct().Count() != known.Length)
        {
            throw new ArgumentException(
                "Starting defaults cannot name a technology twice.",
                nameof(technologies));
        }

        _productionCapacities = Array.AsReadOnly(capacities);
        _technologies = Array.AsReadOnly(known);
        Workforce = workforce;
    }

    /// <summary>
    /// Capacity a listed country starts each facility at. A facility absent from
    /// this list starts unbuilt, which is how the refinery behaves — the corpus
    /// skirmishes carry no capacity record for it because it is gated behind Oil
    /// Drilling.
    /// </summary>
    public IReadOnlyList<FacilityCapacityDefault> ProductionCapacities => _productionCapacities;

    /// <summary>The workforce a listed country starts with, if the world says.</summary>
    public WorkforceDefault? Workforce { get; }

    /// <summary>
    /// Knowledge a listed country begins holding. The manual states this one
    /// outright: "every player always starts with the first two technologies
    /// listed below: High Pressure Steam Engine and Seed Drill".
    /// </summary>
    /// <remarks>
    /// This is the only one of the seven engine defaults in
    /// <c>docs/formulas/_index.md</c> recovered so far, and it came from the
    /// manual rather than the binary. A skirmish carries no <c>tech</c> record
    /// and every power still starts able to farm, which is why the corpus alone
    /// could never have supplied it.
    /// <para>
    /// It is also what keeps the technology gates a gate rather than a wall: a
    /// fresh 1815 start can improve grain and orchards to Level I and open mines
    /// at Level I on its first turn.
    /// </para>
    /// </remarks>
    public IReadOnlyList<TechnologyId> Technologies => _technologies;
}

public readonly record struct FacilityCapacityDefault
{
    public FacilityCapacityDefault(ProductionFacilityId facility, long quantity)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(quantity), "A default production capacity must be positive.");
        }

        Facility = facility;
        Quantity = quantity;
    }

    public ProductionFacilityId Facility { get; }

    public long Quantity { get; }
}

public readonly record struct WorkforceDefault
{
    public WorkforceDefault(long untrained, long trained, long expert)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(untrained);
        ArgumentOutOfRangeException.ThrowIfNegative(trained);
        ArgumentOutOfRangeException.ThrowIfNegative(expert);
        Untrained = untrained;
        Trained = trained;
        Expert = expert;
    }

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
