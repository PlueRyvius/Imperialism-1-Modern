namespace Imperialism.Core;

/// <summary>
/// What an Engineer's constructions cost, and how long they take.
/// </summary>
/// <remarks>
/// A world that declares no settings has no construction at all: every Engineer
/// order is refused. That is how every world behaved before version 17, and it
/// is why the prices are not optional — a free network would be a different game
/// rather than an unpriced one.
/// <para>
/// <b>The prices are the weakest numbers in this slice and are deliberately kept
/// in content.</b> The manual states no figure for any of the three. It says only
/// that ports "cost more than depots", which is a single ordering constraint
/// across three prices. Rail's is not attested at all. See
/// <c>docs/formulas/engineer.md</c>.
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
    private readonly long[] _cashCost;

    public ConstructionSettings(long railCashCost, long depotCashCost, long portCashCost)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(railCashCost);
        ArgumentOutOfRangeException.ThrowIfNegative(depotCashCost);
        ArgumentOutOfRangeException.ThrowIfNegative(portCashCost);
        _cashCost = new long[EngineerConstructions.Count];
        _cashCost[(int)EngineerConstruction.Rail] = railCashCost;
        _cashCost[(int)EngineerConstruction.Depot] = depotCashCost;
        _cashCost[(int)EngineerConstruction.Port] = portCashCost;
    }

    /// <summary>What one of these costs the treasury. Zero is free, not forbidden.</summary>
    public long GetCashCost(EngineerConstruction structure) =>
        Enum.IsDefined(structure)
            ? _cashCost[(int)structure]
            : throw new ArgumentOutOfRangeException(nameof(structure));
}

/// <summary>The construction kinds, for iterating without reflecting over the enum.</summary>
public static class EngineerConstructions
{
    public static readonly IReadOnlyList<EngineerConstruction> All =
        Array.AsReadOnly(Enum.GetValues<EngineerConstruction>());

    public static int Count => All.Count;
}
