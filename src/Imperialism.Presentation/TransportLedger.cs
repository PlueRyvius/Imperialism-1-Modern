using Imperialism.Core;

namespace Imperialism.Presentation;

/// <summary>Which page of the Transport ledger a row sits on.</summary>
public enum TransportLedgerColumn : byte
{
    Left,
    Right,
}

/// <summary>The demand line under a Transport ledger row (layout note D12).</summary>
public enum TransportDemandStatus : byte
{
    /// <summary>No worker or mill wants the row: no line, as under the original's horses.</summary>
    None,

    /// <summary>What is carried covers the demand: a green line.</summary>
    Met,

    /// <summary>The workers or mills want more than is carried: a red line.</summary>
    Short,
}

/// <summary>
/// One commodity on a Transport ledger row. A row holds one, or two for the original's
/// shared strips.
/// </summary>
/// <param name="Requested">What the draft orders now.</param>
/// <param name="Committed">What the shared bar carries of that, in the draft's order.</param>
/// <param name="Available">
/// <see cref="TransportDemandInputs.Available"/>: what Extraction is expected to gather
/// this turn, restating its catchment and port fishing (layout note D12).
/// </param>
/// <param name="Demand">
/// <see cref="TransportDemandLine.Demand"/>: what the workers and mills want carried,
/// net of stock and <b>not</b> capped at <paramref name="Available"/> (D12), so a mill
/// that wants more than exists shows short.
/// </param>
public sealed record TransportRowMember(
    string CommodityKey,
    CommodityId Commodity,
    string Name,
    long Requested,
    long Committed,
    long Available,
    long Demand);

/// <summary>
/// One row of the rebuilt Transport ledger (layout note decisions D6–D10): its place on
/// the original's page, its strip, and its members' figures summed.
/// </summary>
/// <remarks>
/// The figure is <see cref="Requested"/> / <see cref="Available"/> (D10), and
/// <see cref="Available"/> is Extraction's expected gather. The underline is
/// <see cref="DemandStatus"/> (D12): none, green or red. A row with nothing available is
/// drawn greyed and takes no clicks (D9): <see cref="IsActive"/>.
/// </remarks>
/// <param name="RowKey">The member keys joined by <c>+</c>, in strip order; stable.</param>
/// <param name="Name">A single member's name, or "A &amp; B" for a shared strip.</param>
/// <param name="Slot">
/// k on its page: y = 118 + 31k. A gatherable commodity with no strip takes the right
/// page's empty k = 8 and has no <see cref="Strip"/>.
/// </param>
/// <param name="Strip">The original's strip picture, 4001–4018, or null at k = 8.</param>
/// <param name="CanRaise">What <see cref="OrdersDraft.RaiseTransportRow"/> would do now.</param>
/// <param name="CanLower">What <see cref="OrdersDraft.LowerTransportRow"/> would do now.</param>
public sealed record TransportRowView(
    string RowKey,
    string Name,
    IReadOnlyList<TransportRowMember> Members,
    TransportLedgerColumn Column,
    int Slot,
    int? Strip,
    long Requested,
    long Committed,
    long Available,
    long Demand,
    bool CanRaise,
    bool CanLower)
{
    /// <summary>
    /// The demand line under the slider (manual 2283–2300, D12): none when nothing wants
    /// the row, green when what is carried covers the demand, red when it falls short.
    /// </summary>
    public TransportDemandStatus DemandStatus => Demand <= 0
        ? TransportDemandStatus.None
        : Committed >= Demand ? TransportDemandStatus.Met : TransportDemandStatus.Short;

    /// <summary>Something is expected to be gathered, so the strip is pasted and editable.</summary>
    public bool IsActive => Available > 0;
}

/// <summary>
/// The original Transport ledger's strips and which commodities each shows, from the
/// layout note's strip identification (<c>docs/look/transport-layout.md</c>). Art
/// identity only: no quantity or rule lives here.
/// </summary>
public static class TransportLedgerLayout
{
    /// <summary>The right page's empty slot, taken by one gatherable commodity with no strip.</summary>
    public const int UnstrippedSlot = 8;

    /// <summary>Strips in page order: 4001–4010 down the left page, 4011–4018 down the right.</summary>
    public static IReadOnlyList<(int Strip, IReadOnlyList<string> CommodityKeys)> Strips { get; } = Array.AsReadOnly(
        new (int, IReadOnlyList<string>)[]
        {
            (4001, ["commodity.fish", "commodity.livestock"]),
            (4002, ["commodity.fruit"]),
            (4003, ["commodity.grain"]),
            (4004, ["commodity.timber"]),
            (4005, ["commodity.lumber"]),
            (4006, ["commodity.furniture"]),
            (4007, ["commodity.coal"]),
            (4008, ["commodity.iron"]),
            (4009, ["commodity.steel"]),
            (4010, ["commodity.hardware"]),
            (4011, ["commodity.cotton", "commodity.wool"]),
            (4012, ["commodity.fabric"]),
            (4013, ["commodity.clothing"]),
            (4014, ["commodity.oil"]),
            (4015, ["commodity.fuel"]),
            (4016, ["commodity.horses"]),
            (4017, ["commodity.gold"]),
            (4018, ["commodity.gems"]),
        });

    /// <summary>The page and k for a strip number.</summary>
    public static (TransportLedgerColumn Column, int Slot) Place(int strip) => strip switch
    {
        >= 4001 and <= 4010 => (TransportLedgerColumn.Left, strip - 4001),
        >= 4011 and <= 4018 => (TransportLedgerColumn.Right, strip - 4011),
        _ => throw new ArgumentOutOfRangeException(nameof(strip)),
    };

    /// <summary>
    /// Whether Extraction can ever gather this commodity in this world: some deposit
    /// yields it, or ports fish it. Read from content, never from a list of names.
    /// </summary>
    public static bool IsGatherable(WorldDefinition world, CommodityId commodity)
    {
        ArgumentNullException.ThrowIfNull(world);
        return world.Map.Resources.Any(resource => resource.Commodity == commodity) ||
            world.Extraction.PortFishing?.Commodity == commodity;
    }

    /// <summary>
    /// The raise rule (D7): the member with the most expected-but-unordered, ties to the
    /// lower commodity id; null when no member has room.
    /// </summary>
    internal static TransportRowMember? RaiseTarget(
        IEnumerable<TransportRowMember> members, Func<CommodityId, long> ordered)
    {
        TransportRowMember? best = null;
        var bestRoom = 0L;
        foreach (var member in members)
        {
            var room = member.Available - ordered(member.Commodity);
            if (room > 0 && (best is null || room > bestRoom ||
                (room == bestRoom && member.Commodity.Value < best.Commodity.Value)))
            {
                best = member;
                bestRoom = room;
            }
        }

        return best;
    }

    /// <summary>
    /// The lower rule (D7): the member with the most ordered, ties to the higher
    /// commodity id; null when the row orders nothing.
    /// </summary>
    internal static TransportRowMember? LowerTarget(
        IEnumerable<TransportRowMember> members, Func<CommodityId, long> ordered)
    {
        TransportRowMember? best = null;
        var bestOrdered = 0L;
        foreach (var member in members)
        {
            var quantity = ordered(member.Commodity);
            if (quantity > 0 && (best is null || quantity > bestOrdered ||
                (quantity == bestOrdered && member.Commodity.Value > best.Commodity.Value)))
            {
                best = member;
                bestOrdered = quantity;
            }
        }

        return best;
    }

    /// <summary>D8: a raise needs the draft's ordered total under capacity, unless the network is unlimited.</summary>
    internal static bool HasCapacityLeft(TransportPreview transport, long orderedTotal) =>
        transport.IsUnlimited || orderedTotal < (transport.Capacity ?? 0);
}
