namespace Imperialism.Core;

/// <summary>
/// How a commodity's world price answers to what was offered and bid for it.
/// </summary>
/// <remarks>
/// <b>This interface exists to quarantine one number.</b> The manual states the
/// direction outright — "if demand for a commodity is stronger than the supply, the
/// price rises. If the reverse is true, the price falls. If supply and demand are
/// closely matched, the price this turn remains much the same as last turn's price" —
/// and states no magnitude anywhere. `docs/formulas/_index.md` has ranked the clearing
/// price the project's most-wanted unknown since the scoreboard was written, and calls
/// it emergent: this turn's price depends on last turn's, which shapes what countries
/// offer, which sets this turn's.
/// <para>
/// So the direction is a finding and the step is a guess, and putting the guess behind
/// an interface is what keeps it off the critical path. A decompiler reading the real
/// curve should be a new implementation and an edit to content, never a change to
/// <see cref="TradePlanner"/>.
/// </para>
/// </remarks>
public interface ITradeMarket
{
    /// <summary>
    /// The price a commodity carries into the next turn, given the price it carried
    /// into this one and what the world actually offered and bid.
    /// </summary>
    /// <remarks>
    /// Offered and bid are the totals <em>submitted</em>, not the amount that changed
    /// hands. The manual is explicit that the price answers to supply and demand rather
    /// than to settled volume — a bid nobody could fill is still demand, which is what
    /// makes a shortage dear.
    /// </remarks>
    long NextPrice(CommodityDefinition commodity, long currentPrice, long offered, long bid);
}

/// <summary>
/// The manual's direction with a deliberately plain step: a fixed percentage move when
/// supply and demand differ by more than a tolerance, and no move at all inside it.
/// </summary>
/// <remarks>
/// <b>Every number here is a guess and none of them is evidence.</b> They are chosen
/// to behave sanely over a century rather than to match the original, which nothing
/// available can do:
/// <list type="bullet">
/// <item>A move of <see cref="StepPercent"/> per turn, so a price takes several turns
/// to travel a long way rather than snapping. The original's prices visibly drift
/// across a game rather than oscillating.</item>
/// <item>A dead band of <see cref="TolerancePercent"/>, which is the manual's "closely
/// matched … remains much the same" given something has to define *closely*.</item>
/// <item>A floor and ceiling as a multiple of the opening price, so a commodity nobody
/// ever wants cannot reach zero and become free, and a permanent shortage cannot run
/// away. The manual describes neither bound; they exist so a hundred-turn soak stays
/// interpretable.</item>
/// </list>
/// <para>
/// The floor matters more than it looks: at zero a commodity would be bought and sold
/// for nothing and every downstream number would divide by it. That is a modelling
/// safeguard rather than a rule about 1897.
/// </para>
/// </remarks>
public sealed class ProportionalTradeMarket : ITradeMarket
{
    /// <summary>The defaults, kept here so content and tests cite one place.</summary>
    public const long DefaultStepPercent = 10;

    public const long DefaultTolerancePercent = 10;

    public const long DefaultFloorPercent = 25;

    public const long DefaultCeilingPercent = 400;

    public ProportionalTradeMarket(
        long stepPercent = DefaultStepPercent,
        long tolerancePercent = DefaultTolerancePercent,
        long floorPercent = DefaultFloorPercent,
        long ceilingPercent = DefaultCeilingPercent)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(stepPercent);
        ArgumentOutOfRangeException.ThrowIfNegative(tolerancePercent);
        ArgumentOutOfRangeException.ThrowIfNegative(floorPercent);
        if (ceilingPercent < floorPercent)
        {
            throw new ArgumentOutOfRangeException(
                nameof(ceilingPercent), "A price ceiling below its floor leaves nowhere to sit.");
        }

        StepPercent = stepPercent;
        TolerancePercent = tolerancePercent;
        FloorPercent = floorPercent;
        CeilingPercent = ceilingPercent;
    }

    /// <summary>How far a price moves in one turn when supply and demand disagree.</summary>
    public long StepPercent { get; }

    /// <summary>
    /// How close supply and demand must be to count as "closely matched", as a
    /// percentage of the larger of the two.
    /// </summary>
    public long TolerancePercent { get; }

    /// <summary>Bounds, as a percentage of the commodity's opening price.</summary>
    public long FloorPercent { get; }

    /// <summary>Bounds, as a percentage of the commodity's opening price.</summary>
    public long CeilingPercent { get; }

    public long NextPrice(CommodityDefinition commodity, long currentPrice, long offered, long bid)
    {
        ArgumentNullException.ThrowIfNull(commodity);
        ArgumentOutOfRangeException.ThrowIfNegative(currentPrice);
        ArgumentOutOfRangeException.ThrowIfNegative(offered);
        ArgumentOutOfRangeException.ThrowIfNegative(bid);
        if (commodity.WorldPrice is not { } opening)
        {
            throw new ArgumentException(
                "An untraded commodity has no price to move.", nameof(commodity));
        }

        // A market nobody came to keeps its price. That is not the same as being
        // closely matched: a commodity with no offers and no bids has no information
        // in it either way, and drifting it would invent a trend from silence.
        if (offered == 0 && bid == 0)
        {
            return currentPrice;
        }

        var larger = Math.Max(offered, bid);
        var gap = Math.Abs(offered - bid);
        var moved = gap * 100 <= larger * TolerancePercent
            ? currentPrice
            : bid > offered
                ? currentPrice + Math.Max(1, currentPrice * StepPercent / 100)
                : currentPrice - Math.Max(1, currentPrice * StepPercent / 100);

        // At least one unit of movement, so a cheap commodity under real pressure is
        // not pinned by integer division -- a price of 5 would otherwise never move
        // at a ten percent step.
        return Math.Clamp(
            moved,
            Math.Max(1, opening * FloorPercent / 100),
            opening * CeilingPercent / 100);
    }
}
