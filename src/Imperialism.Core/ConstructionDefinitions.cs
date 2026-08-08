namespace Imperialism.Core;

/// <summary>
/// What an Engineer's structures cost, and how long they take.
/// </summary>
/// <remarks>
/// A world that declares no settings has no construction at all: every Engineer
/// order is refused. That is how every world behaved before version 17, and it
/// is why the prices are not optional — a free network would be a different game
/// rather than an unpriced one.
/// <para>
/// <b>Rail is not priced here.</b> Version 19 moved it to
/// <see cref="RailRule.CashCost"/>, because the price list charges by the ground
/// crossed and the gate already lived per terrain. Asking this object for rail's
/// price throws rather than returning a number, so the two cannot drift apart.
/// </para>
/// <para>
/// <b>The two prices left are the weakest numbers in this slice and are
/// deliberately kept in content.</b> The manual states no figure for either. It
/// says only that ports "cost more than depots", which is a single ordering
/// constraint across two prices. See <c>docs/formulas/engineer.md</c>.
/// </para>
/// <para>
/// Duration is not here: it comes from the Engineer's own
/// <see cref="CivilianTypeDefinition.WorkTurns"/>, the same per-type guess every
/// other civilian's work uses, because the manual's sentence is the same one —
/// the order "spends the turn" building.
/// </para>
/// </remarks>
public sealed record ConstructionSettings
{
    private readonly long _depotCashCost;
    private readonly long _portCashCost;

    public ConstructionSettings(long depotCashCost, long portCashCost)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(depotCashCost);
        ArgumentOutOfRangeException.ThrowIfNegative(portCashCost);
        _depotCashCost = depotCashCost;
        _portCashCost = portCashCost;
    }

    /// <summary>
    /// What one of these costs the treasury. Zero is free, not forbidden.
    /// <see cref="EngineerConstruction.Rail"/> is priced by the terrain it crosses
    /// and is not an answer this object has.
    /// </summary>
    public long GetCashCost(EngineerConstruction structure) => structure switch
    {
        EngineerConstruction.Depot => _depotCashCost,
        EngineerConstruction.Port => _portCashCost,
        EngineerConstruction.Rail => throw new ArgumentOutOfRangeException(
            nameof(structure),
            "Rail is priced per terrain, on RailRule.CashCost."),
        _ => throw new ArgumentOutOfRangeException(nameof(structure)),
    };
}

/// <summary>The construction kinds, for iterating without reflecting over the enum.</summary>
public static class EngineerConstructions
{
    public static readonly IReadOnlyList<EngineerConstruction> All =
        Array.AsReadOnly(Enum.GetValues<EngineerConstruction>());

    public static int Count => All.Count;
}

/// <summary>
/// What raising a tile's development level costs its owner in cash.
/// </summary>
/// <remarks>
/// The manual never prints a figure and implies the cost exists: a player might
/// tell a unit to do nothing "when you lack the cash to pay for the civilian's
/// improvements." <b>The numbers are the owner's recollection from play</b> —
/// 100 to reach Level I, 1,000 for Level II, 3,000 for Level III — which the
/// scoreboard rates good for shape and poor for exact values.
/// <para>
/// Indexed by the level being <em>reached</em>, so index 0 is unused and entry
/// <c>n</c> prices rung <c>n</c>. That runs parallel to
/// <see cref="ResourceDefinition.YieldByDevelopmentLevel"/> and
/// <see cref="ResourceDefinition.TechnologyByDevelopmentLevel"/>, and all three
/// then answer "what does rung <c>n</c> take?" in the same shape.
/// </para>
/// <para>
/// <b>Flat across deposits, and per cell rather than per deposit.</b> A hex
/// carrying two resources costs the same as one, which is exactly how
/// <see cref="WorldState.GetCellDevelopment"/> already models it. A world that
/// declares nothing improves for free, which is how every world behaved before
/// this existed. See <c>docs/formulas/development.md</c>.
/// </para>
/// </remarks>
public sealed record ImprovementSettings
{
    private readonly IReadOnlyList<long> _cashCostByDevelopmentLevel;

    public ImprovementSettings(IEnumerable<long> cashCostByDevelopmentLevel)
    {
        ArgumentNullException.ThrowIfNull(cashCostByDevelopmentLevel);
        var costs = cashCostByDevelopmentLevel.ToArray();
        if (costs.Any(static cost => cost < 0))
        {
            throw new ArgumentException(
                "An improvement cannot cost a negative amount.",
                nameof(cashCostByDevelopmentLevel));
        }

        _cashCostByDevelopmentLevel = Array.AsReadOnly(costs);
    }

    /// <summary>
    /// What it costs to raise a cell to <paramref name="level"/>. A rung past
    /// the end of the list is free, the same way a short technology ladder
    /// leaves the rungs above it ungated.
    /// </summary>
    public long GetCashCost(int level)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(level);
        return level < _cashCostByDevelopmentLevel.Count
            ? _cashCostByDevelopmentLevel[level]
            : 0;
    }

    public IReadOnlyList<long> CashCostByDevelopmentLevel => _cashCostByDevelopmentLevel;
}
