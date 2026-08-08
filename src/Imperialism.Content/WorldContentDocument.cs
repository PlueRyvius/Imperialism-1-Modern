using Imperialism.Core;
using System.Text.Json.Serialization;

namespace Imperialism.Content;

public sealed class WorldContentDocument
{
    public string Format { get; set; } = WorldContentCodec.FormatName;

    public int FormatVersion { get; set; } = WorldContentCodec.CurrentVersion;

    /// <summary>
    /// Version 12's bare key list. Superseded by <see cref="Terrains"/>, which
    /// gives terrain the attributes improvability depends on; never written at
    /// version 13.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string[]? TerrainKeys { get; set; }

    public TerrainContentDefinition[] Terrains { get; set; } = [];

    /// <summary>
    /// The kinds of civilian this world has. Empty means it has none and
    /// nothing on its map can be improved.
    /// </summary>
    public CivilianTypeContentDefinition[] CivilianTypes { get; set; } = [];

    public CommodityContentDefinition[] Commodities { get; set; } = [];

    public ResourceContentDefinition[] Resources { get; set; } = [];

    public ProductionFacilityContentDefinition[] ProductionFacilities { get; set; } = [];

    public ProductionRecipeContentDefinition[] ProductionRecipes { get; set; } = [];

    /// <summary>Null only in packages written before version 4; the migrator fills it in.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ExtractionContentSettings? Extraction { get; set; }

    /// <summary>Absent in worlds whose workers never eat.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public FeedingContentSettings? Feeding { get; set; }

    /// <summary>The fair start a skirmish runs on. See <c>StartingDefaultsContent</c>.</summary>
    public StartingDefaultsContent? StartingDefaults { get; set; }

    /// <summary>
    /// What one point of production capacity costs to build — one lumber and one
    /// steel in the original. Empty means facilities cannot be expanded.
    /// </summary>
    public CommodityQuantityContent[] ExpansionCostPerCapacityPoint { get; set; } = [];

    /// <summary>How a country draws new workers into industry. Absent means it cannot.</summary>
    public MigrationContent? Migration { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string[]? ResourceKeys { get; set; }

    public MapContentDocument Map { get; set; } = new();

    /// <summary>
    /// What carrying commodities costs, or absent where the network has no
    /// limit — which is how every world behaved before version 16.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public TransportContentSettings? Transport { get; set; }

    /// <summary>
    /// What an Engineer's constructions cost, or absent where the world has no
    /// construction — which is how every world behaved before version 17.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ConstructionContentSettings? Construction { get; set; }

    /// <summary>
    /// What raising a cell's development level costs, or absent where a civilian
    /// improves for free — which is how every world behaved before version 18.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ImprovementContentSettings? Improvement { get; set; }

    /// <summary>
    /// The technology catalog. **Order is load-bearing for the legacy importer**,
    /// whose <c>tech</c> records are bare 1-based indices into it.
    /// </summary>
    public TechnologyContentDefinition[] Technologies { get; set; } = [];

    /// <summary>
    /// The classes of ship this world has. Empty means no navy and no merchant marine, so
    /// nothing can be carried to market — which is how every world behaved before v20.
    /// </summary>
    public ShipTypeContentDefinition[] ShipTypes { get; set; } = [];

    /// <summary>
    /// How prices answer to supply and demand, or absent where they never move. Absent
    /// does not stop trading: a world with prices and no market trades at the opening
    /// price forever, which keeps the transcribed prices separable from the guessed curve.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public TradeContentSettings? Trade { get; set; }

    public CountryContentDefinition[] Countries { get; set; } = [];

    public ScenarioContentDocument[] Scenarios { get; set; } = [];
}

public sealed class MapContentDocument
{
    public string Key { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public int Width { get; set; }

    public int Height { get; set; }

    public NamedContentDefinition[] Provinces { get; set; } = [];

    public NamedContentDefinition[] SeaZones { get; set; } = [];

    public CellContentDocument[] Cells { get; set; } = [];

}

public sealed class ScenarioContentDocument
{
    public string Key { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public int StartingYear { get; set; }

    public ProvinceOwnerContent[] ProvinceOwners { get; set; } = [];

    public CellLinkContent[] Rails { get; set; } = [];

    public CountryCapitalContent[] Capitals { get; set; } = [];

    public InitialInventoryContent[] InitialInventory { get; set; } = [];

    public InitialProductionCapacityContent[] ProductionCapacities { get; set; } = [];

    public CellDevelopmentContent[] CellDevelopment { get; set; } = [];

    /// <summary>Cell indices carrying a port.</summary>
    public int[] Ports { get; set; } = [];

    /// <summary>Cell indices carrying a rail depot.</summary>
    public int[] Depots { get; set; } = [];

    public WorkforceContent[] Workers { get; set; } = [];

    /// <summary>What each country's network can carry at the start: the 1997 `tran` record.</summary>
    public TransportCapacityContent[] TransportCapacity { get; set; } = [];

    /// <summary>What each country's treasury holds at the start: the 1997 `cash` record.</summary>
    public CountryCashContent[] Cash { get; set; } = [];

    /// <summary>Civilians on the map at the start, in the order they take ids.</summary>
    public CivilianContent[] Civilians { get; set; } = [];

    /// <summary>
    /// Fleets each country starts with: the 1997 <c>ship</c> record. A country listed here
    /// ignores <c>startingDefaults.ships</c> entirely, rather than adding to it.
    /// </summary>
    public ShipContent[] Ships { get; set; } = [];

    public CountryTechnologyContent[] CountryTechnologies { get; set; } = [];

    /// <summary>
    /// Country keys that begin from the world's <c>startingDefaults</c>. Named
    /// rather than inferred: the original equips its Great Powers and not the
    /// minor nations, and nothing here says which a country is.
    /// </summary>
    public string[] DefaultStartCountries { get; set; } = [];
}

/// <summary>
/// The Capitol's terms. The manual names the commodities and the size limit and
/// never says how much of each per worker — see <c>docs/formulas/migration.md</c>.
/// </summary>
public sealed class MigrationContent
{
    public CommodityQuantityContent[] CostPerWorker { get; set; } = [];

    /// <summary>Owned provinces per recruit per turn. Four in the original.</summary>
    public int ProvincesPerRecruit { get; set; }
}

/// <summary>What a listed power starts with when the scenario is silent.</summary>
public sealed class StartingDefaultsContent
{
    public FacilityCapacityDefaultContent[] ProductionCapacities { get; set; } = [];

    public WorkforceDefaultContent? Workforce { get; set; }

    /// <summary>
    /// Technology keys a listed country begins holding. The 1997 fair start is
    /// High Pressure Steam Engine and Seed Drill, which the manual states
    /// outright and no scenario record carries.
    /// </summary>
    public string[] Technologies { get; set; } = [];

    /// <summary>
    /// What a listed country's network can carry before it builds anything.
    /// **A guess** — a skirmish carries no <c>tran</c> record and the corpus
    /// says only that the engine supplies one.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? TransportCapacity { get; set; }

    /// <summary>
    /// What a listed country finds in its warehouse on turn one. The manual says
    /// a power starts with stockpiles of lumber and steel; how much is a guess.
    /// </summary>
    public CommodityQuantityContent[] Inventory { get; set; } = [];

    /// <summary>
    /// What a listed country's treasury holds on turn one. **A guess** — the
    /// manual attests the treasury and never its size, and the five scenarios
    /// carrying a <c>cash</c> record author 1,500 to 15,000 apiece.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? Cash { get; set; }

    /// <summary>
    /// The fleet a listed country starts with, and therefore its opening merchant marine.
    /// </summary>
    /// <remarks>
    /// **Not a guess, unlike the transport pool above.** All three skirmish scenarios give
    /// every power three ships of the same class, independently — the same agreement that
    /// settled the mills and the workforce. Which class is the inference; see
    /// <c>docs/formulas/trade.md</c>.
    /// </remarks>
    public ShipDefaultContent[] Ships { get; set; } = [];
}

public sealed class ShipDefaultContent
{
    public string Type { get; set; } = string.Empty;

    public long Count { get; set; }
}

public sealed class FacilityCapacityDefaultContent
{
    public string Facility { get; set; } = string.Empty;

    public long Quantity { get; set; }
}

public sealed class WorkforceDefaultContent
{
    public long Untrained { get; set; }

    public long Trained { get; set; }

    public long Expert { get; set; }
}

/// <summary>
/// What the railyard charges for a point of transport capacity. One point moves
/// one commodity unit a turn.
/// </summary>
public sealed class TransportContentSettings
{
    public CommodityQuantityContent[] CostPerCapacityPoint { get; set; } = [];

    /// <summary>
    /// Labour a point costs. The railyard is the one build that wants labour;
    /// expanding a mill does not.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public long LabourPerCapacityPoint { get; set; }
}

public sealed class CellDevelopmentContent
{
    public int Cell { get; set; }

    public int Level { get; set; }
}

public sealed class CountryTechnologyContent
{
    public string Country { get; set; } = string.Empty;

    public string Technology { get; set; } = string.Empty;
}

public sealed class TransportCapacityContent
{
    public string Country { get; set; } = string.Empty;

    public long Capacity { get; set; }
}

public sealed class CountryCashContent
{
    public string Country { get; set; } = string.Empty;

    public long Amount { get; set; }
}

/// <summary>One fleet: a country, a class of ship, a sea zone and a count.</summary>
/// <remarks>
/// <b><see cref="SeaZone"/> is carried and never interpreted.</b> A ship's zone is not the
/// map's ocean zone byte — the numberings are unrelated, no offset fits, and 23 of the zone
/// ids appear on no ocean cell at all. So a fleet can be named but not located, and nothing
/// here places one. See <c>docs/scenario-semantics.md</c>.
/// </remarks>
public sealed class ShipContent
{
    public string Country { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public int SeaZone { get; set; }

    public long Count { get; set; }
}

/// <summary>
/// A terrain type and what a civilian may do to it. Three of the original's —
/// dry plains, horse ranch and scrub forest — yield a commodity and admit no
/// worker at all, which is why improvability cannot be read off the deposit.
/// </summary>
public sealed class TerrainContentDefinition
{
    public string Key { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public bool IsImprovable { get; set; }

    /// <summary>
    /// On what terms a Prospector may search this ground. Absent means it can
    /// never be searched, which is every terrain but barren hills, mountains,
    /// swamp, desert and tundra.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ProspectingContent? Prospecting { get; set; }

    /// <summary>
    /// On what terms an Engineer may lay rail here and build a depot. Absent
    /// means it never can, which is ocean's answer and every terrain's answer in
    /// a world with no construction.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RailContent? Rail { get; set; }
}

/// <summary>
/// Terms on which rail can cross a terrain. Present-but-empty is meaningful and
/// is how a world writes ground that anyone may build on from turn one.
/// </summary>
public sealed class RailContent
{
    /// <summary>
    /// Technology key a country needs before an Engineer may build here, or null
    /// for none. The manual gates four groups: High Pressure Steam Engine for
    /// farms, plains, deserts, forests and tundra; Iron Railroad Bridge for
    /// swamp; Compound Steam Engine for hills; Dynamite for mountains.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RequiredTechnology { get; set; }

    /// <summary>
    /// What one tile of track across this ground costs the treasury. Zero, the
    /// default, is free — which is what a pre-v19 package gets. The price list
    /// charges 100 for plains, farm and desert, 150 for tundra and either forest,
    /// 200 for hills, 300 for swamp; mountains it does not price at all.
    /// </summary>
    public long CashCost { get; set; }
}

/// <summary>
/// What an Engineer's two structures cost the treasury. Absent means the world
/// has no construction at all.
/// </summary>
/// <remarks>
/// **Both are weak numbers.** The manual prices neither and says only that ports
/// "cost more than depots". Rail moved out at version 19: it is priced per terrain
/// on <see cref="RailContent.CashCost"/>, because the price list charges by the
/// ground crossed. See <c>docs/formulas/engineer.md</c>.
/// </remarks>
public sealed class ConstructionContentSettings
{
    /// <summary>
    /// Version 17's flat price for a tile of track, superseded by
    /// <see cref="RailContent.CashCost"/>; never written at version 19.
    /// </summary>
    /// <remarks>
    /// **It is dropped rather than carried across, and that is deliberate.** The
    /// number was an invention — "a guess. Nothing supports it at all" — and the
    /// owner's call is that it was simply wrong. Every migration before this one
    /// preserved the old package's behaviour exactly; this one does not, because
    /// preserving a retracted number is worse than losing it. Re-importing legacy
    /// content is what supplies the real per-terrain prices.
    /// </remarks>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? RailCashCost { get; set; }

    public long DepotCashCost { get; set; }

    public long PortCashCost { get; set; }
}

/// <summary>
/// What a civilian's improvement costs, indexed by the level being reached —
/// index 0 unused, so the original's is <c>[0, 100, 1000, 3000]</c>.
/// </summary>
/// <remarks>
/// **Observed play, and tentative.** The manual implies the cost exists and
/// prints no figure. Flat across deposits and per cell rather than per deposit;
/// a rung past the end of the list is free. See
/// <c>docs/formulas/development.md</c>.
/// </remarks>
public sealed class ImprovementContentSettings
{
    public long[] CashCostByDevelopmentLevel { get; set; } = [];
}

/// <summary>
/// Terms on which a terrain can be searched. Present-but-empty is meaningful and
/// is how barren hills and mountains are written: searchable, requiring nothing.
/// </summary>
public sealed class ProspectingContent
{
    /// <summary>
    /// Technology key a country needs before it may search this ground, or null
    /// for none. The manual's one instance is Oil Drilling, over swamp, desert
    /// and tundra.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RequiredTechnology { get; set; }
}

/// <summary>
/// A kind of civilian worker. <c>workTurns</c> is the one number here that
/// nothing supports — see <c>docs/formulas/development.md</c>.
/// </summary>
public sealed class CivilianTypeContentDefinition
{
    public string Key { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public int WorkTurns { get; set; }

    /// <summary>
    /// What setting this civilian to work does: <c>improve</c> or
    /// <c>prospect</c>. Absent means improve, which is what every civilian did
    /// before version 14 and what an older package still means.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public Imperialism.Core.CivilianWorkKind Work { get; set; }
}

/// <summary>One civilian a scenario starts with.</summary>
/// <remarks>
/// The 1997 <c>civi</c> record names only a type and a cell; the owner comes
/// from the province the cell sits in. That is resolved at import so this
/// record can state it outright.
/// </remarks>
public sealed class CivilianContent
{
    public string Country { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public int Cell { get; set; }
}

public sealed class CommodityContentDefinition
{
    public string Key { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public CommodityCategory Category { get; set; }

    /// <summary>
    /// What a unit is worth in cash when the network carries it, instead of
    /// reaching the warehouse. Absent for everything but gold and gems, which
    /// the manual prices at $200 and $500 a unit.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? CashPerUnit { get; set; }

    /// <summary>
    /// What a unit fetches on the world market at the start of the game, or absent where
    /// this commodity is never traded. **Absence is what makes it untradable.**
    /// </summary>
    /// <remarks>
    /// Transcribed from the original's Bid and Offers screen: 100 for raw, 300 for
    /// materials, 900 for goods, with canned food at 100 and horses at 300 breaking the
    /// tiers. The eight commodities with no price are exactly the ones the manual says
    /// cannot be traded. See <c>docs/formulas/trade.md</c>.
    /// </remarks>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? WorldPrice { get; set; }

    /// <summary>
    /// Where this commodity sits in the order the merchant marine spends cargo holds,
    /// or absent where it is not traded. Clothing is first.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? TradeOrder { get; set; }
}

/// <summary>
/// A class of ship: its cargo holds, what it costs to build, what must be known first,
/// and its combat numbers.
/// </summary>
/// <remarks>
/// **Only <c>cargo</c> is modelled.** A country's merchant marine is the sum of the cargo
/// of the ships it owns, and that is what limits trade. The rest is transcribed for the
/// slices that will read it — a shipyard for the bill, the battle engine for the stats.
/// </remarks>
public sealed class ShipTypeContentDefinition
{
    public string Key { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>Cargo holds; zero for a warship, which is every ship that fights.</summary>
    public long Cargo { get; set; }

    /// <summary>
    /// What the shipyard consumes to build one — materials only, never cash. Empty where
    /// nothing reliable has been transcribed.
    /// </summary>
    public CommodityQuantityContent[] BuildCost { get; set; } = [];

    /// <summary>
    /// Technology key the shipyard needs first, or absent for the classes available from
    /// the start.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RequiredTechnology { get; set; }

    /// <summary>Fighting numbers, recorded and read by nothing.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ShipCombatContent? Combat { get; set; }
}

/// <summary>A hull's fighting numbers. Transcribed for the battle engine.</summary>
public sealed class ShipCombatContent
{
    public long Firepower { get; set; }

    public long Range { get; set; }

    public long Armour { get; set; }

    public long Hull { get; set; }

    /// <summary>
    /// Sailing speed, which is not purely military: it decides whether a merchant runs a
    /// blockade. Distinct from battle movement, which is not modelled at all.
    /// </summary>
    public long Speed { get; set; }
}

public sealed class ResourceContentDefinition
{
    public string Key { get; set; } = string.Empty;

    public string Commodity { get; set; } = string.Empty;

    /// <summary>Version 4's flat rate. Superseded by the curve; never written at version 5.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public long YieldPerTurn { get; set; }

    /// <summary>Yield per turn indexed by development level; index 0 is undeveloped.</summary>
    public long[] YieldByDevelopmentLevel { get; set; } = [];

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RequiredTechnology { get; set; }

    /// <summary>
    /// The civilian type that raises this deposit's level, from the manual's
    /// Resource Development Table. Null means none does — its answer for fish,
    /// and its silence about horses.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ImprovedBy { get; set; }

    /// <summary>
    /// Whether a Prospector must find this deposit before anyone may work it.
    /// True for coal, iron, gold, gems and oil; false for everything a terrain
    /// announces by itself.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool RequiresDiscovery { get; set; }

    /// <summary>
    /// Technology keys needed to raise this deposit to each level, indexed like
    /// <see cref="YieldByDevelopmentLevel"/>: entry <c>n</c> gates level
    /// <c>n</c>. Null entries are ungated rungs — index 0 always is, and a mine
    /// opening at Level I is the manual's one other case. Absent means the
    /// deposit is ungated at every level.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string?[]? TechnologyByDevelopmentLevel { get; set; }
}

public sealed class ExtractionContentSettings
{
    public int CatchmentRadius { get; set; }

    /// <summary>Absent in worlds that have no fishing at all.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PortFishingContent? PortFishing { get; set; }
}

public sealed class FeedingContentSettings
{
    /// <summary>
    /// Repeating preference cycle, walked one worker at a time. The original's
    /// is grain, fruit, grain, then livestock-or-fish.
    /// </summary>
    public FoodPreferenceContent[] PreferenceCycle { get; set; } = [];

    /// <summary>Labour per worker per turn: untrained, trained, expert.</summary>
    public long[] LabourByGrade { get; set; } = [];

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CannedFood { get; set; }
}

public sealed class FoodPreferenceContent
{
    /// <summary>Commodities this position in the cycle will eat happily.</summary>
    public string[] Accepted { get; set; } = [];
}

public sealed class WorkforceContent
{
    public string Country { get; set; } = string.Empty;

    public long Untrained { get; set; }

    public long Trained { get; set; }

    public long Expert { get; set; }
}

public sealed class PortFishingContent
{
    public string Commodity { get; set; } = string.Empty;

    public long YieldPerAdjacentWaterTile { get; set; }
}

public sealed class ProductionFacilityContentDefinition
{
    public string Key { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public ProductionCapacityMode CapacityMode { get; set; }

    /// <summary>
    /// The sizes this facility may be built to. Absent means it cannot be
    /// expanded; an uncapped facility must not carry one.
    /// </summary>
    public CapacityLadderContent? CapacityLadder { get; set; }
}

/// <summary>
/// From the manual: mills improve 2, 4, 8, 16, 24 and then by eight; factories
/// 1, 2, 4, 8, 12 and then by four.
/// </summary>
public sealed class CapacityLadderContent
{
    public long[] Rungs { get; set; } = [];

    public long Increment { get; set; }
}

public sealed class ProductionRecipeContentDefinition
{
    public string Key { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Facility { get; set; } = string.Empty;

    public long CapacityCost { get; set; }

    public long LabourCost { get; set; }

    public CommodityQuantityContent[] Inputs { get; set; } = [];

    public CommodityQuantityContent[] Outputs { get; set; } = [];
}

public sealed class CommodityQuantityContent
{
    public string Commodity { get; set; } = string.Empty;

    public long Quantity { get; set; }
}

public sealed class NamedContentDefinition
{
    public string Key { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// A country, and whether it is one of the world's Great Powers.
/// </summary>
/// <remarks>
/// Countries used to be plain <see cref="NamedContentDefinition"/> entries. Great Power
/// status decides who pays the cargo holds on a trade: "no Minor Nation owns merchant
/// marine", and between two Great Powers "the buyer always picks up the commodities".
/// The importer reads it from <c>labo</c>, the one record naming the Great Powers and only
/// them.
/// </remarks>
public sealed class CountryContentDefinition
{
    public string Key { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public bool IsGreatPower { get; set; }
}

/// <summary>
/// How a world's prices answer to supply and demand.
/// </summary>
/// <remarks>
/// **The direction is the manual's and every number here is a guess.** "If demand for a
/// commodity is stronger than the supply, the price rises. If the reverse is true, the
/// price falls. If supply and demand are closely matched, the price this turn remains much
/// the same." It states no magnitude, and the clearing price is the project's most-wanted
/// unknown. These live in content so recalibration is an edit.
/// </remarks>
public sealed class TradeContentSettings
{
    /// <summary>How far a price moves in a turn when supply and demand disagree.</summary>
    public long StepPercent { get; set; }

    /// <summary>How close counts as "closely matched", against the larger of the two.</summary>
    public long TolerancePercent { get; set; }

    /// <summary>
    /// Floor and ceiling as a percentage of the opening price. **A modelling safeguard
    /// rather than a rule about 1897**: at zero a commodity would trade for nothing.
    /// </summary>
    public long FloorPercent { get; set; }

    public long CeilingPercent { get; set; }
}

/// <summary>
/// One technology, and the terms on which a country may invest in it.
/// </summary>
/// <remarks>
/// Technologies used to be plain <see cref="NamedContentDefinition"/> entries,
/// because knowledge could only be granted and never bought.
/// <para>
/// **A technology with no <see cref="Cost"/> is not for sale**, which is how a
/// pre-v19 package migrates and how the two every power starts holding are
/// written. See <c>docs/formulas/technology.md</c>.
/// </para>
/// </remarks>
public sealed class TechnologyContentDefinition
{
    public string Key { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Technology keys a country must already know before buying this. Checked
    /// when buying and never when a scenario grants knowledge outright.
    /// </summary>
    public string[] Prerequisites { get; set; } = [];

    /// <summary>
    /// The first year anybody may buy this, or absent for no date. World-wide:
    /// advances "cannot be kept secret".
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? AvailableFrom { get; set; }

    /// <summary>
    /// What investing costs the treasury, or absent where it is not for sale.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? Cost { get; set; }
}

public sealed class CellContentDocument
{
    public string Terrain { get; set; } = string.Empty;

    public CellRegionContent Region { get; set; } = new();

    public string[] Resources { get; set; } = [];

    public bool HasSettlementSite { get; set; }

    public RiverPathContent? River { get; set; }
}

public sealed class RiverPathContent
{
    public RiverEndpoint First { get; set; }

    public RiverEndpoint Second { get; set; }
}

public sealed class CellRegionContent
{
    public string? Province { get; set; }

    public string? SeaZone { get; set; }
}

public sealed class CellLinkContent
{
    public int First { get; set; }

    public int Second { get; set; }
}

public sealed class ProvinceOwnerContent
{
    public string Province { get; set; } = string.Empty;

    public string? Country { get; set; }
}

public sealed class CountryCapitalContent
{
    public string Country { get; set; } = string.Empty;

    public int Cell { get; set; }
}

public sealed class InitialInventoryContent
{
    public string Country { get; set; } = string.Empty;

    public string Commodity { get; set; } = string.Empty;

    public long Quantity { get; set; }
}

public sealed class InitialProductionCapacityContent
{
    public string Country { get; set; } = string.Empty;

    public string Facility { get; set; } = string.Empty;

    public long Quantity { get; set; }
}
