namespace Imperialism.Core;

public enum CommodityCategory : byte
{
    Raw,
    Material,
    Goods,
}

public sealed record CommodityDefinition
{
    public CommodityDefinition(CommodityId id, string name, CommodityCategory category)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (!Enum.IsDefined(category))
        {
            throw new ArgumentOutOfRangeException(nameof(category));
        }

        Id = id;
        Name = name;
        Category = category;
    }

    public CommodityId Id { get; }

    public string Name { get; }

    public CommodityCategory Category { get; }
}

/// <summary>A map deposit type, the inventory commodity it yields, and how much of it one collected cell contributes each turn.</summary>
public sealed record ResourceDefinition
{
    public ResourceDefinition(ResourceId id, CommodityId commodity, long yieldPerTurn)
    {
        if (yieldPerTurn <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(yieldPerTurn),
                "Resource yield per turn must be positive.");
        }

        Id = id;
        Commodity = commodity;
        YieldPerTurn = yieldPerTurn;
    }

    public ResourceId Id { get; }

    public CommodityId Commodity { get; }

    /// <summary>
    /// Output from one collected cell carrying this deposit, per turn. Flat for
    /// now: the original scales this by the cell's development level, which is
    /// not yet modelled. See <c>docs/formulas/extraction.md</c>.
    /// </summary>
    public long YieldPerTurn { get; }
}

public readonly record struct InitialCommodityStock
{
    public InitialCommodityStock(CountryId country, CommodityId commodity, long quantity)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Initial stock must be positive.");
        }

        Country = country;
        Commodity = commodity;
        Quantity = quantity;
    }

    public CountryId Country { get; }

    public CommodityId Commodity { get; }

    public long Quantity { get; }
}
