using System.Globalization;

namespace Imperialism.Core;

/// <summary>
/// The Armoury's generic categories, plus the General records. "The Armory is
/// organized by eight generic categories of regiment" (manual 2733–2735); militia and
/// combat engineers are the two the Armoury screen does not list as buildable in the
/// same way, and the executable's role-slot table at <c>0x00695528</c> puts the
/// engineers in slot 8 and the Generals in slot 9.
/// </summary>
public enum ArmyCategory : byte
{
    Militia,
    LightInfantry,
    RegularInfantry,
    HeavyInfantry,
    LightCavalry,
    HeavyCavalry,
    LightArtillery,
    HeavyArtillery,
    CombatEngineers,
    General,
}

/// <summary>
/// What the Armoury charges for one regiment: one worker of a grade, a bill in
/// commodities and cash. Every figure is the executable's own table at
/// <c>0x00695CD0</c>; see <c>docs/disasm/definitive-original-data.md</c>.
/// </summary>
public sealed record ArmyPurchase
{
    private readonly IReadOnlyList<CommodityQuantity> _bill;

    public ArmyPurchase(WorkerGrade workerGrade, IEnumerable<CommodityQuantity> bill, long cashCost)
    {
        ArgumentNullException.ThrowIfNull(bill);
        if (!Enum.IsDefined(workerGrade))
        {
            throw new ArgumentOutOfRangeException(nameof(workerGrade));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(cashCost);
        var items = bill.ToArray();
        if (items.Any(static item => item.Quantity <= 0))
        {
            throw new ArgumentException("A regiment's bill must name positive quantities.", nameof(bill));
        }

        if (items.Select(static item => item.Commodity).Distinct().Count() != items.Length)
        {
            throw new ArgumentException("A regiment's bill cannot name a commodity twice.", nameof(bill));
        }

        WorkerGrade = workerGrade;
        _bill = Array.AsReadOnly(items);
        CashCost = cashCost;
    }

    /// <summary>
    /// The grade of the one healthy worker each regiment takes, permanently: the
    /// workers "can never return to industry" (manual 2729–2731).
    /// </summary>
    public WorkerGrade WorkerGrade { get; }

    /// <summary>Armaments, horses and fuel charged per regiment.</summary>
    public IReadOnlyList<CommodityQuantity> Bill => _bill;

    /// <summary>Cash charged per regiment.</summary>
    public long CashCost { get; }
}

/// <summary>
/// A regiment type's fighting numbers from the manual's Regiment Abilities table
/// (4040–4072), with the executable winning where its verified fields disagree.
/// <b>Recorded and read by nothing</b> until the land battle slice.
/// </summary>
/// <param name="FirepowerVsInfantry">FPN. Fractional: Grenadiers are 12.5 in the executable.</param>
/// <param name="FirepowerVsMounted">FPM.</param>
/// <param name="Range">RNG.</param>
/// <param name="DefendingRange">The artillery's parenthesised range when defending, or null.</param>
/// <param name="Defence">DEF.</param>
/// <param name="EntrenchedDefence">The parenthesised entrenched DEF, or null for cavalry, which has none.</param>
/// <param name="Movement">MVR, movement inside a tactical battle.</param>
public sealed record ArmyCombatStats(
    decimal FirepowerVsInfantry,
    decimal FirepowerVsMounted,
    long Range,
    long? DefendingRange,
    long Defence,
    long? EntrenchedDefence,
    long Movement);

/// <summary>
/// One type in the executable's thirty-entry army table. A scenario <c>army</c>
/// record's 0-based type indexes this catalogue, so <b>order is load-bearing</b>.
/// </summary>
public sealed record ArmyTypeDefinition
{
    public ArmyTypeDefinition(
        ArmyTypeId id,
        string name,
        ArmyCategory category,
        int era,
        TechnologyId? requiredTechnology = null,
        ArmyPurchase? purchase = null,
        long? arms = null,
        bool movesByRail = true,
        bool leavesProvince = true,
        ArmyCombatStats? stats = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (!Enum.IsDefined(category))
        {
            throw new ArgumentOutOfRangeException(nameof(category));
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(era, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(era, 3);
        if (arms is { } value)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value, nameof(arms));
        }

        Id = id;
        Name = name;
        Category = category;
        Era = era;
        RequiredTechnology = requiredTechnology;
        Purchase = purchase;
        Arms = arms;
        MovesByRail = movesByRail;
        LeavesProvince = leavesProvince;
        Stats = stats;
    }

    public ArmyTypeId Id { get; }

    public string Name { get; }

    public ArmyCategory Category { get; }

    /// <summary>1 to 3: which column of the Regimental Upgrade Requirement Table.</summary>
    public int Era { get; }

    /// <summary>
    /// The technology that opens this era of its category, or null for era 1. Read as
    /// the turn opens.
    /// </summary>
    public TechnologyId? RequiredTechnology { get; }

    /// <summary>What the Armoury charges, or null where it never builds this type: militia and Generals.</summary>
    public ArmyPurchase? Purchase { get; }

    /// <summary>
    /// The ARMS column: the armaments in the bill, and the rail cost at five capacity
    /// points apiece (manual 4038–4041). Null for militia and Generals.
    /// </summary>
    public long? Arms { get; }

    /// <summary>False for militia: "cannot be deployed by rail".</summary>
    public bool MovesByRail { get; }

    /// <summary>False for militia: it "cannot be ordered to leave their home province".</summary>
    public bool LeavesProvince { get; }

    public ArmyCombatStats? Stats { get; }
}

/// <summary>
/// Identifies one regiment. Issued densely from 1 and never reused, so an event
/// naming a regiment that has since been lost still names only that one.
/// </summary>
public readonly record struct RegimentId
{
    public RegimentId(long value)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Regiment IDs must be positive.");
        }

        Value = value;
    }

    public long Value { get; }

    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}

/// <summary>
/// A regiment on the map. <b>The owner is stored</b>, not read from the province:
/// a land battle puts one country's regiments in another's province, and storing it
/// keeps that a data change rather than a model change.
/// </summary>
/// <param name="Experience">
/// In <b>hundredths of a medal</b>, 0 to <see cref="MaxExperience"/>, as the executable
/// stores it: a victory adds 35 and a defeat 20, so "three victories or five defeats
/// earns one medal" (manual 1847–1857). <see cref="Medals"/> is the whole part.
/// </param>
/// <param name="Health">
/// 0 to <see cref="MaxHealth"/>. Every regiment starts at the cap, because a scenario
/// <c>army</c> record carries no health and 500 is the executable's ceiling.
/// </param>
public sealed record Regiment(
    RegimentId Id,
    CountryId Owner,
    ProvinceId Province,
    ArmyTypeId Type,
    long Experience,
    long Health = Regiment.MaxHealth)
{
    /// <summary>The executable's health ceiling, and what a new regiment starts with.</summary>
    public const long MaxHealth = 500;

    /// <summary>What a regiment without a movement order recovers each turn, up to the ceiling.</summary>
    public const long HealingPerTurn = 100;

    /// <summary>The executable's experience ceiling: four medals, in hundredths.</summary>
    public const long MaxExperience = 400;

    /// <summary>One medal, in the hundredths <see cref="Experience"/> is kept in.</summary>
    public const long ExperiencePerMedal = 100;

    /// <summary>Whole medals earned.</summary>
    public long Medals => Experience / ExperiencePerMedal;
}

/// <summary>
/// Which provinces border which: two provinces are adjacent when a cell of one is a
/// hex neighbour of a cell of the other. Derived once from a map, at any size.
/// </summary>
/// <remarks>
/// It asks <see cref="MapDefinition.TryGetNeighbor"/>, so it honours the map's own
/// <see cref="MapDefinition.WrapsHorizontally"/>: a map without a seam has no
/// adjacency across it, and an imported 1997 map, which has one, does.
/// </remarks>
public sealed class ProvinceAdjacency
{
    private readonly HashSet<int>[] _neighbours;

    public ProvinceAdjacency(MapDefinition map)
    {
        ArgumentNullException.ThrowIfNull(map);
        _neighbours = new HashSet<int>[map.Provinces.Count];
        for (var index = 0; index < _neighbours.Length; index++)
        {
            _neighbours[index] = [];
        }

        foreach (var cell in map.Cells)
        {
            if (cell.Region.Kind != CellRegionKind.Province)
            {
                continue;
            }

            var province = cell.Region.Value;
            foreach (var direction in HexDirections.All)
            {
                if (!map.TryGetNeighbor(cell.Index, direction, out var neighbour))
                {
                    continue;
                }

                var other = map[neighbour].Region;
                if (other.Kind == CellRegionKind.Province && other.Value != province)
                {
                    _neighbours[province].Add(other.Value);
                    _neighbours[other.Value].Add(province);
                }
            }
        }
    }

    public bool AreAdjacent(ProvinceId first, ProvinceId second) =>
        (uint)first.Value < (uint)_neighbours.Length && _neighbours[first.Value].Contains(second.Value);

    /// <summary>The provinces bordering one, in id order.</summary>
    public IReadOnlyList<ProvinceId> GetNeighbours(ProvinceId province) =>
        (uint)province.Value < (uint)_neighbours.Length
            ? Array.AsReadOnly(_neighbours[province.Value].Order().Select(static value => new ProvinceId(value)).ToArray())
            : throw new ArgumentOutOfRangeException(nameof(province));
}
