namespace Imperialism.Core;

/// <summary>
/// What a task force is told to do this turn: nothing, patrol where it is, blockade a
/// capital it lies beside, establish a landing site on a province it lies beside, or sail.
/// </summary>
/// <remarks>
/// "Mission orders are carried out in the sea zone where the fleet is already located.
/// Movement sends the fleet to a new location. Fleets cannot do both a mission and a
/// move in the same turn." So a task force takes one of these a turn, never two. A
/// blockade and a landing site never cut a port: the executable's port predicate counts
/// only the patrol state. See <c>docs/formulas/naval.md</c>.
/// </remarks>
public abstract class NavalMission
{
    private NavalMission()
    {
    }

    /// <summary>No mission and no move. The task force keeps whatever it was doing.</summary>
    public static NavalMission None { get; } = new NoMission();

    /// <summary>Patrol the task force's present sea zone, the Telescope cursor.</summary>
    public static NavalMission Patrol { get; } = new PatrolMission();

    /// <summary>
    /// Blockade a country's capital from the task force's present sea zone, the red "X"
    /// cursor. A blockade names a country and acts on its capital (chosen rule 11).
    /// </summary>
    public static NavalMission Blockade(CountryId target) => new BlockadeMission(target);

    /// <summary>
    /// Establish a landing site on a province from the task force's present sea zone, the
    /// Cannon cursor: "Establish a site for a landing on coast of this sea zone" (manual
    /// 1114–1116). Troops may cross to it from the next turn (naval rule 29).
    /// </summary>
    public static NavalMission Landing(ProvinceId target) => new LandingMission(target);

    /// <summary>Sail towards another sea zone, the Wheel cursor.</summary>
    public static NavalMission MoveTo(SeaZoneId destination) => new MoveMission(destination);

    internal sealed class NoMission : NavalMission
    {
        public override string ToString() => "None";
    }

    internal sealed class PatrolMission : NavalMission
    {
        public override string ToString() => "Patrol";
    }

    /// <summary>A blockade order. The target is checked when the turn resolves.</summary>
    public sealed class BlockadeMission : NavalMission
    {
        internal BlockadeMission(CountryId target) => Target = target;

        public CountryId Target { get; }

        public override string ToString() => $"Blockade({Target})";
    }

    /// <summary>A landing-site order. The target is checked when the turn resolves.</summary>
    public sealed class LandingMission : NavalMission
    {
        internal LandingMission(ProvinceId target) => Target = target;

        public ProvinceId Target { get; }

        public override string ToString() => $"Landing({Target})";
    }

    /// <summary>A move order. The destination is checked when the turn resolves.</summary>
    public sealed class MoveMission : NavalMission
    {
        internal MoveMission(SeaZoneId destination) => Destination = destination;

        public SeaZoneId Destination { get; }

        public override string ToString() => $"MoveTo({Destination})";
    }
}

/// <summary>
/// One naval order: group fleets into a task force, or command one that exists.
/// </summary>
/// <remarks>
/// Nothing here is validated against the world. A fleet that was sunk, a task force
/// that belongs to somebody else and a sea zone the map does not have are all refused
/// with a <see cref="NavalOrderRefusedEvent"/> when the turn resolves, never thrown:
/// simultaneous turns make an impossible order an ordinary outcome rather than a
/// malformed submission.
/// </remarks>
public abstract class NavalOrder
{
    private NavalOrder(NavalMission mission)
    {
        ArgumentNullException.ThrowIfNull(mission);
        Mission = mission;
    }

    /// <summary>The mission or move to give the task force once the order is applied.</summary>
    public NavalMission Mission { get; }

    /// <summary>
    /// Group whole fleet records into a new task force and give it a mission in the
    /// same order, the way the original's select-then-click does without the player
    /// ever seeing a task-force number.
    /// </summary>
    public static NavalOrder Assemble(IEnumerable<FleetId> fleets, NavalMission mission) =>
        new AssembleOrder(fleets, mission);

    /// <summary>Give an existing task force its mission or move for this turn.</summary>
    public static NavalOrder Command(TaskForceId taskForce, NavalMission mission) =>
        new CommandOrder(taskForce, mission);

    /// <summary>Assemble a task force, then apply <see cref="NavalOrder.Mission"/>.</summary>
    public sealed class AssembleOrder : NavalOrder
    {
        private readonly IReadOnlyList<FleetId> _fleets;

        internal AssembleOrder(IEnumerable<FleetId> fleets, NavalMission mission)
            : base(mission)
        {
            ArgumentNullException.ThrowIfNull(fleets);
            _fleets = Array.AsReadOnly(fleets.ToArray());
        }

        /// <summary>The fleets as given, including any repeat; repeats are refused.</summary>
        public IReadOnlyList<FleetId> Fleets => _fleets;
    }

    /// <summary>Apply <see cref="NavalOrder.Mission"/> to a task force already assembled.</summary>
    public sealed class CommandOrder : NavalOrder
    {
        internal CommandOrder(TaskForceId taskForce, NavalMission mission)
            : base(mission) => TaskForce = taskForce;

        public TaskForceId TaskForce { get; }
    }
}
