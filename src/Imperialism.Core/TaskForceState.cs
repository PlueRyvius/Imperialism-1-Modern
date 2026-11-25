namespace Imperialism.Core;

/// <summary>
/// Task-force activities with an executable-backed modern behaviour.
/// </summary>
public enum TaskForceActivity
{
    Idle,
    Patrolling,

    /// <summary>
    /// Blockading a country's capital: the executable's task-force state 6. It does not
    /// qualify for the port predicate, so it never cuts a port; it only raises the odds
    /// of intercepting merchants entering or leaving that capital. See
    /// <c>docs/formulas/naval.md</c>, chosen rules 11 and 12.
    /// </summary>
    Blockading,

    /// <summary>
    /// Holding a landing site on a province: the executable's task-force state 5. Troops
    /// may cross to the province from the turn after it is established, for as long as
    /// the site holds. It never cuts a port and never intercepts merchants. See
    /// <c>docs/formulas/naval.md</c>, rules 29 onwards.
    /// </summary>
    Landing,
}

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
    private readonly List<FleetId> _fleets;
    private readonly IReadOnlyList<FleetId> _fleetsView;
    private TaskForceActivity _activity;

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
        _fleets = [.. members];
        _fleetsView = _fleets.AsReadOnly();
    }

    public TaskForceId Id { get; }

    public CountryId Country { get; }

    public SeaZoneId SeaZone { get; internal set; }

    /// <summary>
    /// The end of the currently planned sailing leg, if one awaits resolution.
    /// The original overwrites its requested destination with this resolved leg,
    /// so the long-range request is intentionally not retained here.
    /// </summary>
    public SeaZoneId? PlannedSeaZone { get; internal set; }

    /// <summary>
    /// The original port predicate qualifies the equivalent of this activity
    /// (task-force state 3). Other original state values are not represented
    /// until their target and resolution behaviour are recovered.
    /// </summary>
    public TaskForceActivity Activity
    {
        get => _activity;
        internal set
        {
            _activity = value;
            if (value != TaskForceActivity.Blockading)
            {
                BlockadeTarget = null;
            }

            if (value != TaskForceActivity.Landing)
            {
                LandingTarget = null;
                LandingSince = null;
            }
        }
    }

    /// <summary>
    /// The country whose capital this task force blockades; set only while
    /// <see cref="Activity"/> is <see cref="TaskForceActivity.Blockading"/>.
    /// </summary>
    public CountryId? BlockadeTarget { get; private set; }

    /// <summary>
    /// The province this task force holds a landing site on; set only while
    /// <see cref="Activity"/> is <see cref="TaskForceActivity.Landing"/>.
    /// </summary>
    public ProvinceId? LandingTarget { get; private set; }

    /// <summary>
    /// The turn the landing site was established. Troops may land only on a later turn:
    /// "You cannot move land forces on the turn the landing site is established."
    /// </summary>
    public int? LandingSince { get; private set; }

    /// <summary>Fleet records in deterministic ascending ID order.</summary>
    public IReadOnlyList<FleetId> Fleets => _fleetsView;

    internal void Blockade(CountryId target)
    {
        _activity = TaskForceActivity.Blockading;
        BlockadeTarget = target;
        LandingTarget = null;
        LandingSince = null;
    }

    internal void EstablishLanding(ProvinceId target, int turnNumber)
    {
        _activity = TaskForceActivity.Landing;
        BlockadeTarget = null;
        LandingTarget = target;
        LandingSince = turnNumber;
    }

    /// <summary>Drops a member whose last ship was sunk. Order is kept.</summary>
    internal void RemoveFleet(FleetId fleet) => _ = _fleets.Remove(fleet);
}
