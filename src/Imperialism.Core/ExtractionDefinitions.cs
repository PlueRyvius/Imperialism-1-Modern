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
/// What it costs to move commodities off the land, and to be able to move more.
/// </summary>
/// <remarks>
/// "Transport capacity is the total number of commodities that your network can
/// move each turn." One point moves one unit, whatever it is — the manual's
/// Transport screen shows a single shared bar with a slider per commodity, and
/// no commodity weighs more than another.
/// <para>
/// A world that declares no settings has no limit at all, which is how every
/// world behaved before this existed: <see cref="TurnPhase.Extraction"/> handed
/// everything it gathered straight to <see cref="TurnPhase.Delivery"/>.
/// </para>
/// </remarks>
public sealed record TransportSettings
{
    private readonly IReadOnlyList<CommodityQuantity> _costPerCapacityPoint;

    public TransportSettings(
        IEnumerable<CommodityQuantity> costPerCapacityPoint,
        long labourPerCapacityPoint = 0)
    {
        ArgumentNullException.ThrowIfNull(costPerCapacityPoint);
        ArgumentOutOfRangeException.ThrowIfNegative(labourPerCapacityPoint);
        var cost = costPerCapacityPoint.ToArray();
        if (cost.Any(static item => item.Quantity <= 0))
        {
            throw new ArgumentException(
                "A capacity point cannot cost nothing of a commodity it names.",
                nameof(costPerCapacityPoint));
        }

        if (cost.Select(static item => item.Commodity).Distinct().Count() != cost.Length)
        {
            throw new ArgumentException(
                "A capacity point cannot name a commodity twice.",
                nameof(costPerCapacityPoint));
        }

        _costPerCapacityPoint = Array.AsReadOnly(cost);
        LabourPerCapacityPoint = labourPerCapacityPoint;
    }

    /// <summary>
    /// What one point of transport capacity costs at the railyard. The manual
    /// puts it "as with other industrial expansion", which it prices at one
    /// lumber and one steel per point.
    /// </summary>
    public IReadOnlyList<CommodityQuantity> CostPerCapacityPoint => _costPerCapacityPoint;

    /// <summary>
    /// Labour one point costs. **This is where the railyard differs from
    /// expanding a mill**: the manual prices facility capacity at "one lumber
    /// and one steel" and mentions no labour, while the railyard needs "steel,
    /// lumber, and available labour". It never says how much, so the rate
    /// follows the same total-input-units rule every recipe's
    /// <see cref="ProductionRecipeDefinition.LabourCost"/> uses, and is carried
    /// explicitly here rather than derived, for the same reason.
    /// </summary>
    public long LabourPerCapacityPoint { get; }
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
