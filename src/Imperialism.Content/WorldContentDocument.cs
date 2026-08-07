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

    public NamedContentDefinition[] Technologies { get; set; } = [];

    public NamedContentDefinition[] Countries { get; set; } = [];

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

    /// <summary>Civilians on the map at the start, in the order they take ids.</summary>
    public CivilianContent[] Civilians { get; set; } = [];

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
