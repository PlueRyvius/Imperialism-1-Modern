namespace Imperialism.Core;

public enum CommodityCategory : byte
{
    Raw,
    Material,
    Goods,
}

public sealed record CommodityDefinition
{
    public CommodityDefinition(CommodityId id, string name, CommodityCategory category)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (!Enum.IsDefined(category))
        {
            throw new ArgumentOutOfRangeException(nameof(category));
        }

        Id = id;
        Name = name;
        Category = category;
    }

    public CommodityId Id { get; }

    public string Name { get; }

    public CommodityCategory Category { get; }
}

/// <summary>A technology a country either knows or does not.</summary>
/// <remarks>
/// There is no research system yet: nothing costs anything and nothing is
/// discovered. This exists so a scenario can state what its countries begin
/// knowing and so rules that gate on knowledge have something real to ask.
/// </remarks>
public sealed record TechnologyDefinition
{
    public TechnologyDefinition(TechnologyId id, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Id = id;
        Name = name;
    }

    public TechnologyId Id { get; }

    public string Name { get; }
}

/// <summary>
/// A map deposit type, the inventory commodity it yields, and how much one
/// collected cell contributes each turn at each development level.
/// </summary>
public sealed record ResourceDefinition
{
    private readonly IReadOnlyList<long> _yieldByDevelopmentLevel;
    private readonly IReadOnlyList<TechnologyId?> _technologyByDevelopmentLevel;

    public ResourceDefinition(
        ResourceId id,
        CommodityId commodity,
        IEnumerable<long> yieldByDevelopmentLevel,
        TechnologyId? requiredTechnology = null,
        CivilianTypeId? improvedBy = null,
        bool requiresDiscovery = false,
        IEnumerable<TechnologyId?>? technologyByDevelopmentLevel = null)
    {
        ArgumentNullException.ThrowIfNull(yieldByDevelopmentLevel);
        var yields = yieldByDevelopmentLevel.ToArray();
        if (yields.Length == 0)
        {
            throw new ArgumentException(
                "A deposit needs a yield for at least the undeveloped level.",
                nameof(yieldByDevelopmentLevel));
        }

        if (yields.Any(static value => value < 0))
        {
            throw new ArgumentOutOfRangeException(
                nameof(yieldByDevelopmentLevel),
                "Yields cannot be negative.");
        }

        if (yields.All(static value => value == 0))
        {
            throw new ArgumentException(
                "A deposit that yields nothing at every level would never be worth collecting.",
                nameof(yieldByDevelopmentLevel));
        }

        var gates = technologyByDevelopmentLevel?.ToArray() ?? [];
        if (gates.Length > yields.Length)
        {
            throw new ArgumentException(
                "A deposit cannot gate a development level its yield curve does not reach.",
                nameof(technologyByDevelopmentLevel));
        }

        Id = id;
        Commodity = commodity;
        RequiredTechnology = requiredTechnology;
        ImprovedBy = improvedBy;
        RequiresDiscovery = requiresDiscovery;
        _yieldByDevelopmentLevel = Array.AsReadOnly(yields);
        _technologyByDevelopmentLevel = Array.AsReadOnly(gates);
    }

    public ResourceId Id { get; }

    public CommodityId Commodity { get; }

    /// <summary>
    /// Output per turn from one collected cell, indexed by that cell's
    /// development level. Index 0 is undeveloped, and zero there is meaningful:
    /// a mine gives nothing until a worker has built on it, while a field
    /// already yields. The original's <c>deve</c> records carry levels 1 to 3,
    /// which is what sets the usable length of this curve.
    /// </summary>
    public IReadOnlyList<long> YieldByDevelopmentLevel => _yieldByDevelopmentLevel;

    /// <summary>The highest level this deposit has a distinct yield for.</summary>
    public int MaxDevelopmentLevel => _yieldByDevelopmentLevel.Count - 1;

    /// <summary>
    /// Knowledge required before this deposit yields anything at all. Null means
    /// ungated. No deposit in imported 1997 content declares one: which
    /// technologies gate which deposits has not been measured yet, and guessing
    /// it would quietly make part of the map worthless.
    /// </summary>
    public TechnologyId? RequiredTechnology { get; }

    /// <summary>
    /// The kind of civilian that raises this deposit's level, from the manual's
    /// Resource Development Table: Farmer for grain, fruit and cotton; Rancher
    /// for livestock and wool; Forester for timber; Miner for coal, iron, gold
    /// and gems; Driller for oil. Null means no civilian improves it, which is
    /// the table's answer for fish and its silence about horses.
    /// </summary>
    /// <remarks>
    /// The improver is a property of the deposit and improvability is a property
    /// of the ground, and both must agree before a cell can be worked. The two
    /// come from different tables in the manual and neither subsumes the other:
    /// grain names a Farmer wherever it sits, and dry plains admit no civilian
    /// however good the grain.
    /// </remarks>
    public CivilianTypeId? ImprovedBy { get; }

    /// <summary>
    /// Whether a Prospector must find this deposit before anyone may work it.
    /// True for coal, iron, gold, gems and oil; false for everything the terrain
    /// announces by itself — "you know that cotton is present at every cotton
    /// plantation terrain tile".
    /// </summary>
    /// <remarks>
    /// <b>This gates improvement only, and deliberately not extraction.</b> The
    /// manual's Resource Development Table already gives all five a yield of
    /// <em>zero</em> at level 0 — "until a mine is built the tile does not
    /// produce minerals" — so an undiscovered deposit pays nothing whether or
    /// not anything checks. Extraction is left alone rather than gated twice.
    /// <para>
    /// The catch worth naming: a content package that gave one of these a
    /// non-zero level-0 yield would collect it without any search, because the
    /// only gate is on the work order. Nothing shipped does, and the alternative
    /// — teaching <see cref="ExtractionPlanner"/> about discovery too — buys
    /// nothing for the content that exists.
    /// </para>
    /// </remarks>
    public bool RequiresDiscovery { get; }

    /// <summary>
    /// Knowledge a country needs before a civilian may raise this deposit to
    /// each level, indexed the same way as <see cref="YieldByDevelopmentLevel"/>:
    /// entry <c>n</c> is what it takes to reach level <c>n</c>. Null means that
    /// rung is ungated, and a short or empty list leaves every level above it
    /// ungated too.
    /// </summary>
    /// <remarks>
    /// The manual's Benefits of Technology Table gates nearly every rung —
    /// Seed Drill for grain to Level I, Steel and Iron Plows for Level II,
    /// Mechanical Reaper for Level III, and so on per deposit. The one exception
    /// is a mine opening at Level I, which needs nothing, consistent with the
    /// Miner being buildable from the start and prospecting being the only thing
    /// in its way.
    /// <para>
    /// <b>This gates a civilian raising a level and never a scenario authoring
    /// one</b>, exactly as the capacity ladder gates building and never storing.
    /// `s1` authors four timber tiles at Level III for a power that does not hold
    /// Dynamite, and the importer must take them. See
    /// <c>docs/formulas/technology.md</c>.
    /// </para>
    /// </remarks>
    public IReadOnlyList<TechnologyId?> TechnologyByDevelopmentLevel => _technologyByDevelopmentLevel;

    /// <summary>
    /// What it takes to reach <paramref name="developmentLevel"/>, or null when
    /// that rung is ungated. Levels past the end of the table are ungated rather
    /// than forbidden, so a package that declares no gates behaves as it did
    /// before technology existed.
    /// </summary>
    public TechnologyId? GetRequiredTechnology(int developmentLevel)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(developmentLevel);
        return developmentLevel < _technologyByDevelopmentLevel.Count
            ? _technologyByDevelopmentLevel[developmentLevel]
            : null;
    }

    /// <summary>
    /// Yield at <paramref name="developmentLevel"/>, holding at the top of the
    /// curve rather than throwing, so a scenario carrying a level this deposit
    /// has no separate entry for still behaves sensibly.
    /// </summary>
    public long GetYield(int developmentLevel)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(developmentLevel);
        return _yieldByDevelopmentLevel[Math.Min(developmentLevel, MaxDevelopmentLevel)];
    }
}

/// <summary>One cell that starts already improved, and how far.</summary>
/// <remarks>
/// Sparse on purpose: the shipped scenarios develop a few hundred cells out of
/// 6,480, and the original's <c>deve</c> records are themselves a sparse list.
/// </remarks>
public readonly record struct InitialCellDevelopment
{
    public InitialCellDevelopment(CellIndex cell, int level)
    {
        if (level <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(level),
                "Undeveloped is the absence of a record, not a level-zero one.");
        }

        Cell = cell;
        Level = level;
    }

    public CellIndex Cell { get; }

    public int Level { get; }
}

/// <summary>A technology one country begins the scenario already knowing.</summary>
public readonly record struct InitialCountryTechnology(CountryId Country, TechnologyId Technology);

public readonly record struct InitialCommodityStock
{
    public InitialCommodityStock(CountryId country, CommodityId commodity, long quantity)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Initial stock must be positive.");
        }

        Country = country;
        Commodity = commodity;
        Quantity = quantity;
    }

    public CountryId Country { get; }

    public CommodityId Commodity { get; }

    public long Quantity { get; }
}
