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

    public ExtractionSettings(int catchmentRadius, PortFishing? portFishing = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(catchmentRadius);
        CatchmentRadius = catchmentRadius;
        PortFishing = portFishing;
    }

    /// <summary>
    /// What a port pulls out of the water beside it, or null where the world has
    /// no fishing at all. Content names the commodity so nothing in Core has to
    /// know that "fish" exists.
    /// </summary>
    public PortFishing? PortFishing { get; }

    /// <summary>
    /// How many hex steps a cell may sit from a connected collection point and
    /// still be gathered. Zero collects only the connection points themselves.
    /// </summary>
    public int CatchmentRadius { get; }
}

/// <summary>
/// A port's catch: one commodity, earned per neighbouring water tile.
/// </summary>
/// <remarks>
/// The manual counts rivers as well as coast — "rivers, like coasts, produce one
/// unit of fish per turn for adjacent ports" — so an inland river port fishes
/// exactly as a sea port does. 45 of the corpus's 124 ports have no adjacent
/// sea at all and every one of them sits on a river, which would be a strange
/// thing to author if river ports could not fish.
/// </remarks>
public sealed record PortFishing
{
    public PortFishing(CommodityId commodity, long yieldPerAdjacentWaterTile)
    {
        if (yieldPerAdjacentWaterTile <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(yieldPerAdjacentWaterTile),
                "A fishing yield that is zero or less would make every port pointless.");
        }

        Commodity = commodity;
        YieldPerAdjacentWaterTile = yieldPerAdjacentWaterTile;
    }

    public CommodityId Commodity { get; }

    public long YieldPerAdjacentWaterTile { get; }
}
