namespace Imperialism.Core;

/// <summary>
/// World-level rules governing which cells hand their deposits to their owner
/// each turn.
/// </summary>
/// <remarks>
/// The original gathers a tile's output only when it lies on, or within one
/// tile of, a depot or port that still has an unbroken route to the capital.
/// Depots, ports and sea routes are not modelled yet, so every cell of the
/// capital's own rail component stands in for a depot and the radius below is
/// measured from those cells. That substitution is deliberately conservative —
/// it can only under-collect relative to the original, never over-collect —
/// and is recorded in <c>docs/formulas/extraction.md</c>.
/// </remarks>
public sealed record ExtractionSettings
{
    /// <summary>The manual's "on or within one tile of" catchment.</summary>
    public static readonly ExtractionSettings Default = new(1);

    public ExtractionSettings(int catchmentRadius)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(catchmentRadius);
        CatchmentRadius = catchmentRadius;
    }

    /// <summary>
    /// How many hex steps a cell may sit from a connected collection point and
    /// still be gathered. Zero collects only the connection points themselves.
    /// </summary>
    public int CatchmentRadius { get; }
}
