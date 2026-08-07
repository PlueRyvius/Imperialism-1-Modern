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
    private readonly IReadOnlyList<CommodityQuantity> _inventory;

    public StartingDefaults(
        IEnumerable<FacilityCapacityDefault> productionCapacities,
        WorkforceDefault? workforce = null,
        IEnumerable<TechnologyId>? technologies = null,
        long? transportCapacity = null,
        IEnumerable<CommodityQuantity>? inventory = null,
        long? cash = null)
    {
        if (transportCapacity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(transportCapacity));
        }

        if (cash < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(cash));
        }

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

        var stock = inventory?.ToArray() ?? [];
        if (stock.Select(static item => item.Commodity).Distinct().Count() != stock.Length)
        {
            throw new ArgumentException(
                "Starting defaults cannot name a commodity twice.",
                nameof(inventory));
        }

        if (stock.Any(static item => item.Quantity <= 0))
        {
            throw new ArgumentException(
                "A starting stock of nothing is the absence of an entry.",
                nameof(inventory));
        }

        _productionCapacities = Array.AsReadOnly(capacities);
        _technologies = Array.AsReadOnly(known);
        _inventory = Array.AsReadOnly(stock);
        Workforce = workforce;
        TransportCapacity = transportCapacity;
        Cash = cash;
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

    /// <summary>
    /// What a listed country's network can carry before it builds anything.
    /// </summary>
    /// <remarks>
    /// <b>This is a guess, and the only number in the transport system without
    /// evidence behind it.</b> A skirmish carries no <c>tran</c> record, so the
    /// corpus says nothing except that the engine supplies one — and the mission
    /// scenarios that do carry `tran` are six authored special cases which this
    /// project has a standing rule against mining for constants.
    /// <para>
    /// Zero was the alternative and it is worse: it would make every imported
    /// skirmish unplayable, since nothing could ever leave the land. So a number
    /// is invented, and it lives in content where changing it is an edit rather
    /// than a code change — the same treatment as
    /// <see cref="CivilianTypeDefinition.WorkTurns"/>. Do not cite it as
    /// evidence for anything. See <c>docs/formulas/transport.md</c>.
    /// </para>
    /// </remarks>
    public long? TransportCapacity { get; }

    /// <summary>
    /// What a listed country finds in its warehouse on turn one — the `ware`
    /// record a skirmish never carries.
    /// </summary>
    /// <remarks>
    /// <b>That a stockpile exists is the manual's</b>, and it names the
    /// commodities: "you must construct a lumber and steel mill with your
    /// <em>initial stockpiles of lumber and steel</em>, or you may be forced to
    /// beg for lumber and steel from other Great Powers." A power that began
    /// with an empty warehouse could not do that, and begging would be its only
    /// option from the first turn.
    /// <para>
    /// <b>How much is not.</b> The quantity is a guess, and it lives in content
    /// so changing it is an edit. It matters more than most: with nothing in the
    /// warehouse a country cannot build the railyard that would let it carry the
    /// materials to fill the warehouse, which is a trap the manual plainly does
    /// not intend. See <c>docs/formulas/transport.md</c>.
    /// </para>
    /// </remarks>
    public IReadOnlyList<CommodityQuantity> Inventory => _inventory;

    /// <summary>
    /// What a listed country's treasury holds on turn one — the <c>cash</c>
    /// record a skirmish never carries.
    /// </summary>
    /// <remarks>
    /// <b>That there is a treasury at all is the manual's</b>: "each Great Power
    /// begins the game with a limited amount of cash which is totally inadequate
    /// to meet its needs." <b>How much is a guess</b>, and it lives in content so
    /// changing it is an edit.
    /// <para>
    /// The corpus cannot settle it. Five of the ten scenarios carry no <c>cash</c>
    /// record at all, and the five that do are authored situations: <c>s3</c>
    /// gives its powers 1,500 to 15,000 apiece. Reading a constant out of that is
    /// the mistake this project has a standing rule against. See
    /// <c>docs/formulas/money.md</c>.
    /// </para>
    /// </remarks>
    public long? Cash { get; }
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
