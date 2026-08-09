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
/// army units. The bill is now the executable's own six commodity arrays rather than a
/// blank, and it corroborates that sentence exactly: thirteen hulls, not one of them
/// priced in cash.
/// </para>
/// </remarks>
public sealed record ShipTypeDefinition
{
    private readonly IReadOnlyList<CommodityQuantity> _buildCost;

    public ShipTypeDefinition(
        ShipTypeId id,
        string name,
        long cargo = 0,
        long seaZones = 0,
        IEnumerable<CommodityQuantity>? buildCost = null,
        TechnologyId? requiredTechnology = null,
        ShipCombatStats? combat = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfNegative(cargo);
        ArgumentOutOfRangeException.ThrowIfNegative(seaZones);
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
        SeaZones = seaZones;
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
    /// How many sea zones this hull crosses in a turn. Every merchant has exactly one and
    /// every warship between two and six, which is what makes a Raider able to hunt and a
    /// Ship-of-the-Line able only to sit on a coast.
    /// </summary>
    /// <remarks>
    /// <b>This is the column the manual prints as "Speed", and reading it as speed was
    /// wrong.</b> Field 7 of the executable's naval table carries exactly the manual's
    /// numbers, and the executable also carries a separate battle-movement field — see
    /// <see cref="ShipCombatStats.BattleSpeed"/> — so the two are different quantities
    /// and neither is a sailing rate. Nothing here decides whether a merchant outruns a
    /// blockade; that claim was inferred from the label and is retracted.
    /// </remarks>
    public long SeaZones { get; }

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
/// <b>Every one of the thirteen hulls has these, merchants included.</b> The executable's
/// naval table gives a Freighter 25 armour and a hull scale of 1,200 — it is the toughest
/// thing afloat that cannot shoot back — which is what a blockade needs and what the
/// manual's combat table, printing warships only, hid. A merchant's firepower, range and
/// battle movement are all zero, and that is the record saying it never fights rather
/// than the record being absent.
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
    public ShipCombatStats(
        long firepower,
        long range,
        long armour,
        long hullScale,
        long battleSpeed,
        long? hull = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(firepower);
        ArgumentOutOfRangeException.ThrowIfNegative(range);
        ArgumentOutOfRangeException.ThrowIfNegative(armour);
        ArgumentOutOfRangeException.ThrowIfNegative(hullScale);
        ArgumentOutOfRangeException.ThrowIfNegative(battleSpeed);
        if (hull is <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(hull), "A printed hull rating of nothing is one to leave absent.");
        }

        Firepower = firepower;
        Range = range;
        Armour = armour;
        HullScale = hullScale;
        BattleSpeed = battleSpeed;
        Hull = hull;
    }

    public long Firepower { get; }

    public long Range { get; }

    /// <summary>
    /// Armour, 0 to 70. <b>The executable stores its complement</b> — the accessor
    /// returns <c>100 - stored</c> — which is why the unused row 0 reads as 100 armour
    /// and why a Trader's 0 is a real zero rather than a missing value.
    /// </summary>
    public long Armour { get; }

    /// <summary>
    /// The executable's internal hull scale, 600 to 2,800. The battle report divides
    /// damage by it to normalise the bar it draws, so it is a divisor rather than a
    /// quantity of hull points.
    /// </summary>
    /// <remarks>
    /// <b>It is not the manual's H number and does not convert to one.</b> The ratio
    /// between the two runs from 23.3 to 26.2 across the eight warships the manual
    /// prints, so no single scale fits; both are kept rather than one being derived from
    /// the other. See <see cref="Hull"/>.
    /// </remarks>
    public long HullScale { get; }

    /// <summary>
    /// Movement inside a tactical battle, 0 to 9. Zero for every merchant, because they
    /// are never in one. Distinct from <see cref="ShipTypeDefinition.SeaZones"/>, which
    /// is movement on the world map.
    /// </summary>
    public long BattleSpeed { get; }

    /// <summary>
    /// The hull rating the manual prints, 30 to 115, or null for the five merchants its
    /// combat table leaves out. Kept beside <see cref="HullScale"/> because the two are
    /// separate observations of the same game and neither derives the other.
    /// </summary>
    public long? Hull { get; }
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
