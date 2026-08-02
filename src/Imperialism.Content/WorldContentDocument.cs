using Imperialism.Core;
using System.Text.Json.Serialization;

namespace Imperialism.Content;

public sealed class WorldContentDocument
{
    public string Format { get; set; } = WorldContentCodec.FormatName;

    public int FormatVersion { get; set; } = WorldContentCodec.CurrentVersion;

    public string[] TerrainKeys { get; set; } = [];

    public CommodityContentDefinition[] Commodities { get; set; } = [];

    public ResourceContentDefinition[] Resources { get; set; } = [];

    public ProductionFacilityContentDefinition[] ProductionFacilities { get; set; } = [];

    public ProductionRecipeContentDefinition[] ProductionRecipes { get; set; } = [];

    /// <summary>Null only in packages written before version 4; the migrator fills it in.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ExtractionContentSettings? Extraction { get; set; }

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

    public CountryTechnologyContent[] CountryTechnologies { get; set; } = [];
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
}

public sealed class ExtractionContentSettings
{
    public int CatchmentRadius { get; set; }
}

public sealed class ProductionFacilityContentDefinition
{
    public string Key { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public ProductionCapacityMode CapacityMode { get; set; }
}

public sealed class ProductionRecipeContentDefinition
{
    public string Key { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Facility { get; set; } = string.Empty;

    public long CapacityCost { get; set; }

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
