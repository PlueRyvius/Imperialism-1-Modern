namespace Imperialism.Core;

public enum ProductionCapacityMode : byte
{
    Limited,
    Unlimited,
}

public sealed record ProductionFacilityDefinition
{
    public ProductionFacilityDefinition(
        ProductionFacilityId id,
        string name,
        ProductionCapacityMode capacityMode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (!Enum.IsDefined(capacityMode))
        {
            throw new ArgumentOutOfRangeException(nameof(capacityMode));
        }

        Id = id;
        Name = name;
        CapacityMode = capacityMode;
    }

    public ProductionFacilityId Id { get; }

    public string Name { get; }

    public ProductionCapacityMode CapacityMode { get; }
}

public readonly record struct CommodityQuantity
{
    public CommodityQuantity(CommodityId commodity, long quantity)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Commodity quantity must be positive.");
        }

        Commodity = commodity;
        Quantity = quantity;
    }

    public CommodityId Commodity { get; }

    public long Quantity { get; }
}

public sealed class ProductionRecipeDefinition
{
    private readonly IReadOnlyList<CommodityQuantity> _inputs;
    private readonly IReadOnlyList<CommodityQuantity> _outputs;

    public ProductionRecipeDefinition(
        ProductionRecipeId id,
        string name,
        ProductionFacilityId facility,
        long capacityCost,
        IEnumerable<CommodityQuantity> inputs,
        IEnumerable<CommodityQuantity> outputs)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(outputs);
        if (capacityCost <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacityCost), "Capacity cost must be positive.");
        }

        var inputArray = inputs.ToArray();
        var outputArray = outputs.ToArray();
        if (inputArray.Length == 0 || outputArray.Length == 0)
        {
            throw new ArgumentException("A production recipe requires at least one input and one output.");
        }

        if (inputArray.Any(static item => item.Quantity <= 0))
        {
            throw new ArgumentException("Recipe input quantities must be positive.", nameof(inputs));
        }

        if (outputArray.Any(static item => item.Quantity <= 0))
        {
            throw new ArgumentException("Recipe output quantities must be positive.", nameof(outputs));
        }

        if (inputArray.Select(static item => item.Commodity).Distinct().Count() != inputArray.Length)
        {
            throw new ArgumentException("Recipe inputs cannot repeat a commodity.", nameof(inputs));
        }

        if (outputArray.Select(static item => item.Commodity).Distinct().Count() != outputArray.Length)
        {
            throw new ArgumentException("Recipe outputs cannot repeat a commodity.", nameof(outputs));
        }

        Id = id;
        Name = name;
        Facility = facility;
        CapacityCost = capacityCost;
        _inputs = Array.AsReadOnly(inputArray);
        _outputs = Array.AsReadOnly(outputArray);
    }

    public ProductionRecipeId Id { get; }

    public string Name { get; }

    public ProductionFacilityId Facility { get; }

    public long CapacityCost { get; }

    public IReadOnlyList<CommodityQuantity> Inputs => _inputs;

    public IReadOnlyList<CommodityQuantity> Outputs => _outputs;
}

public readonly record struct InitialProductionCapacity
{
    public InitialProductionCapacity(CountryId country, ProductionFacilityId facility, long quantity)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Initial production capacity must be positive.");
        }

        Country = country;
        Facility = facility;
        Quantity = quantity;
    }

    public CountryId Country { get; }

    public ProductionFacilityId Facility { get; }

    public long Quantity { get; }
}
