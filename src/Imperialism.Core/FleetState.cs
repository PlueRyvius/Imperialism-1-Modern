namespace Imperialism.Core;

/// <summary>
/// One homogeneous, positioned scenario fleet.
/// </summary>
/// <remarks>
/// A legacy <c>ship</c> record creates ships at a sea-zone reference but does
/// not attach them to a task force. This object preserves that record-level
/// placement. Task-force membership, missions, and movement are separate work.
/// </remarks>
public sealed class FleetState
{
    internal FleetState(
        FleetId id,
        CountryId country,
        ShipTypeId type,
        long count,
        SeaZoneId? seaZone)
    {
        if (count <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        Id = id;
        Country = country;
        Type = type;
        Count = count;
        SeaZone = seaZone;
    }

    public FleetId Id { get; }

    public CountryId Country { get; }

    public ShipTypeId Type { get; }

    public long Count { get; }

    /// <summary>
    /// Current base sea zone, or null where legacy content names a zone that
    /// this map does not define. Such a record remains preserved for cargo but
    /// cannot receive a positional naval command.
    /// </summary>
    public SeaZoneId? SeaZone { get; internal set; }

    /// <summary>The task force this fleet record belongs to, if assembled.</summary>
    public TaskForceId? TaskForce { get; internal set; }
}
