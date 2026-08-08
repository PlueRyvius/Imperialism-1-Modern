namespace Imperialism.Core;

/// <summary>
/// A class of ship: what it can carry, what it costs to build, what must be known
/// first, and — recorded but read by nothing — what it can do in a fight.
/// </summary>
/// <remarks>
/// <b>Only <see cref="Cargo"/> is modelled.</b> A country's merchant marine is the sum
/// of the cargo of the ships it owns, and that pool is what limits trade. Everything
/// else here is transcribed so the numbers exist when there is something to read them:
/// the build bill wants a shipyard, and the combat stats want the battle engine.
/// <para>
/// That is the same treatment the technology table got — all twenty-eight entries
/// transcribed "so the numbering is right and modelled nowhere" — and for the same
/// reason. The scarce thing is the data, not the code that reads it, and a table
/// transcribed twice is a table transcribed wrong once.
/// </para>
/// <para>
/// <b>The build bill is in commodities and never in cash.</b> The owner is explicit:
/// ships are built at the shipyard "at no monetary cost but with varying amounts of
/// resources and/or materials (and for warships arms)". There is also no upkeep, unlike
/// army units.
/// </para>
/// </remarks>
public sealed record ShipTypeDefinition
{
    private readonly IReadOnlyList<CommodityQuantity> _buildCost;

    public ShipTypeDefinition(
        ShipTypeId id,
        string name,
        long cargo = 0,
        IEnumerable<CommodityQuantity>? buildCost = null,
        TechnologyId? requiredTechnology = null,
        ShipCombatStats? combat = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfNegative(cargo);
        var costs = buildCost?.ToArray() ?? [];
        if (costs.Any(static item => item.Quantity <= 0))
        {
            throw new ArgumentException(
                "A build cost of nothing is an input to leave off the bill.", nameof(buildCost));
        }

        if (costs.Select(static item => item.Commodity).Distinct().Count() != costs.Length)
        {
            throw new ArgumentException(
                "A build bill cannot name the same commodity twice.", nameof(buildCost));
        }

        Id = id;
        Name = name;
        Cargo = cargo;
        RequiredTechnology = requiredTechnology;
        Combat = combat;
        _buildCost = Array.AsReadOnly(costs);
    }

    public ShipTypeId Id { get; }

    public string Name { get; }

    /// <summary>
    /// Cargo holds. "Each cargo hold can carry one unit of any trading commodity."
    /// Zero for a warship, which is what makes a navy and a merchant marine two
    /// different numbers built out of the same shipyard.
    /// </summary>
    public long Cargo { get; }

    /// <summary>
    /// What the shipyard consumes to build one. Materials and resources only — never
    /// cash — and empty where nothing has been transcribed.
    /// </summary>
    public IReadOnlyList<CommodityQuantity> BuildCost => _buildCost;

    /// <summary>
    /// Knowledge a country needs before its shipyard will lay one down, or null for the
    /// models available from the start. Streamlined Hulls opens the Clipper and
    /// Paddlewheels opens the Paddlewheeler and the Raider.
    /// </summary>
    /// <remarks>
    /// <b>Read by nothing yet</b>, because building ships is not modelled — but it is
    /// what the corpus check measures against, and it is the reason two entries in the
    /// technology table that gate nothing else are not dead after all.
    /// </remarks>
    public TechnologyId? RequiredTechnology { get; }

    /// <summary>
    /// What this hull does in a fight, or null where nothing was transcribed. Recorded
    /// for the battle engine and read by nothing.
    /// </summary>
    public ShipCombatStats? Combat { get; }
}

/// <summary>
/// A hull's fighting numbers. <b>Transcribed and modelled nowhere</b> — every one of
/// these wants the tactical battle engine.
/// </summary>
/// <remarks>
/// <see cref="Armour"/> and <see cref="Speed"/> are not purely military even so: the
/// owner notes that "not every merchant ship that sails or steams through a blockaded
/// sea zone gets caught", and those two are what decide it. So a merchant with armour
/// is a trade fact waiting for blockade to exist.
/// <para>
/// <see cref="Arms"/> is not here — it is part of the build bill, in
/// <see cref="ShipTypeDefinition.BuildCost"/>. It has a second meaning the owner
/// flags: the arms that went into a warship set "the force size that can be landed at
/// a beachhead on hostile soil in one turn", so it is a build input that becomes a
/// combat capability. Worth remembering when the beachhead rule lands.
/// </para>
/// </remarks>
public sealed record ShipCombatStats
{
    public ShipCombatStats(long firepower, long range, long armour, long hull, long speed)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(firepower);
        ArgumentOutOfRangeException.ThrowIfNegative(range);
        ArgumentOutOfRangeException.ThrowIfNegative(armour);
        ArgumentOutOfRangeException.ThrowIfNegative(hull);
        ArgumentOutOfRangeException.ThrowIfNegative(speed);
        Firepower = firepower;
        Range = range;
        Armour = armour;
        Hull = hull;
        Speed = speed;
    }

    public long Firepower { get; }

    public long Range { get; }

    public long Armour { get; }

    public long Hull { get; }

    public long Speed { get; }
}

/// <summary>
/// Ships a scenario starts a country with. The record is
/// <c>[country, type, zone, count]</c>.
/// </summary>
/// <remarks>
/// <b><paramref name="SeaZone"/> is carried and never interpreted.</b>
/// <c>docs/scenario-semantics.md</c> establishes that a ship's zone is <em>not</em> the
/// map's ocean zone byte — the two numberings are unrelated, no constant offset fits,
/// and 23 of the zone ids appear on no ocean cell at all. So a fleet can be named but
/// not located. Nothing here places a ship on the map, and nothing should until that
/// correspondence is recovered.
/// <para>
/// It is kept rather than dropped because losing it would make the information
/// unrecoverable from a round trip, and because merchant marine capacity does not care
/// where the ships are.
/// </para>
/// </remarks>
public readonly record struct InitialShip(
    CountryId Country,
    ShipTypeId Type,
    int SeaZone,
    long Count);
