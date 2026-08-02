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

/// <summary>A map deposit type and the inventory commodity it yields.</summary>
public sealed record ResourceDefinition(ResourceId Id, CommodityId Commodity);

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
