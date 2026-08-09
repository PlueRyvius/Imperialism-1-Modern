namespace Imperialism.Core;

/// <summary>
/// A group of co-located fleet records awaiting a strategic naval command.
/// </summary>
/// <remarks>
/// This is the modern equivalent of the original `TTaskForce` attachment: it
/// groups selected ships but does not infer a patrol, blockade, invasion, or
/// sailing order. Those original state paths remain distinct.
/// </remarks>
public sealed class TaskForceState
{
    private readonly IReadOnlyList<FleetId> _fleets;

    internal TaskForceState(
        TaskForceId id,
        CountryId country,
        SeaZoneId seaZone,
        IEnumerable<FleetId> fleets)
    {
        var members = fleets.ToArray();
        if (members.Length == 0)
        {
            throw new ArgumentException("A task force needs at least one fleet.", nameof(fleets));
        }

        if (members.Distinct().Count() != members.Length)
        {
            throw new ArgumentException("A task force cannot contain the same fleet twice.", nameof(fleets));
        }

        Id = id;
        Country = country;
        SeaZone = seaZone;
        _fleets = Array.AsReadOnly(members);
    }

    public TaskForceId Id { get; }

    public CountryId Country { get; }

    public SeaZoneId SeaZone { get; internal set; }

    /// <summary>Fleet records in deterministic ascending ID order.</summary>
    public IReadOnlyList<FleetId> Fleets => _fleets;
}
