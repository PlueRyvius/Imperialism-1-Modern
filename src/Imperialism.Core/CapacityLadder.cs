namespace Imperialism.Core;

/// <summary>
/// The sizes a facility may be built to, and what it grows by once it is off
/// the end of the list.
/// </summary>
/// <remarks>
/// <para>
/// From the manual, which is unusually exact here: mills start at 2 and improve
/// to 4, 8, 16, 24 and then by eight at a time; factories start at 1 and improve
/// to 2, 4, 8, 12 and then by four. Each point of capacity costs one lumber and
/// one steel, and expansion needs no labour.
/// </para>
/// <para>
/// **This constrains building, not storing.** Fifty-three `capa` records in the
/// shipped corpus sit off these rungs — metal works at 3, steel mills at 5 and
/// 7, lumber mills at 6 — so validating stored capacity against the ladder would
/// reject real scenarios. A player may only build to the next rung; a scenario
/// may author anything. See <c>docs/formulas/production.md</c>.
/// </para>
/// <para>
/// A facility already above every listed rung grows by <see cref="Increment"/>,
/// which is also how a facility sitting between rungs advances: the next size is
/// the smallest reachable one strictly greater than where it is now.
/// </para>
/// </remarks>
public sealed class CapacityLadder
{
    private readonly IReadOnlyList<long> _rungs;

    public CapacityLadder(IEnumerable<long> rungs, long increment)
    {
        ArgumentNullException.ThrowIfNull(rungs);
        var ordered = rungs.ToArray();
        if (ordered.Length == 0)
        {
            throw new ArgumentException("A capacity ladder needs at least one rung.", nameof(rungs));
        }

        if (ordered.Any(static rung => rung <= 0))
        {
            throw new ArgumentException("Capacity ladder rungs must be positive.", nameof(rungs));
        }

        for (var index = 1; index < ordered.Length; index++)
        {
            if (ordered[index] <= ordered[index - 1])
            {
                throw new ArgumentException(
                    "Capacity ladder rungs must ascend.", nameof(rungs));
            }
        }

        if (increment <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(increment), "The ladder's increment must be positive.");
        }

        _rungs = Array.AsReadOnly(ordered);
        Increment = increment;
    }

    /// <summary>The sizes a facility may be built to, ascending.</summary>
    public IReadOnlyList<long> Rungs => _rungs;

    /// <summary>What the facility grows by once past the last rung.</summary>
    public long Increment { get; }

    /// <summary>Where a facility at <paramref name="current"/> may build to next.</summary>
    public long NextAbove(long current)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(current);
        foreach (var rung in _rungs)
        {
            if (rung > current)
            {
                return rung;
            }
        }

        // Past the table. Step from the last rung so the sequence stays on the
        // documented grid even when a scenario started the facility off it.
        var last = _rungs[^1];
        var steps = ((current - last) / Increment) + 1;
        return checked(last + (steps * Increment));
    }
}
