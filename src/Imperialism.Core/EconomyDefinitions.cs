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

    public ResourceDefinition(
        ResourceId id,
        CommodityId commodity,
        IEnumerable<long> yieldByDevelopmentLevel,
        TechnologyId? requiredTechnology = null,
        CivilianTypeId? improvedBy = null)
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

        Id = id;
        Commodity = commodity;
        RequiredTechnology = requiredTechnology;
        ImprovedBy = improvedBy;
        _yieldByDevelopmentLevel = Array.AsReadOnly(yields);
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
