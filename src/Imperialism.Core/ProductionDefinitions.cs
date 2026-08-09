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
        ProductionCapacityMode capacityMode,
        CapacityLadder? capacityLadder = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (!Enum.IsDefined(capacityMode))
        {
            throw new ArgumentOutOfRangeException(nameof(capacityMode));
        }

        if (capacityLadder is not null && capacityMode == ProductionCapacityMode.Unlimited)
        {
            throw new ArgumentException(
                "An uncapped facility cannot be expanded, so it cannot carry a capacity ladder.",
                nameof(capacityLadder));
        }

        Id = id;
        Name = name;
        CapacityMode = capacityMode;
        CapacityLadder = capacityLadder;
    }

    public ProductionFacilityId Id { get; }

    public string Name { get; }

    public ProductionCapacityMode CapacityMode { get; }

    /// <summary>
    /// The sizes this facility may be built to, or null if it cannot be
    /// expanded. Uncapped facilities — food processing, the railyard — never
    /// have one.
    /// </summary>
    public CapacityLadder? CapacityLadder { get; }
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
        long labourCost,
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

        if (labourCost <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(labourCost), "Labour cost must be positive.");
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
        LabourCost = labourCost;
        _inputs = Array.AsReadOnly(inputArray);
        _outputs = Array.AsReadOnly(outputArray);
    }

    public ProductionRecipeId Id { get; }

    public string Name { get; }

    public ProductionFacilityId Facility { get; }

    public long CapacityCost { get; }

    /// <summary>
    /// Labour spent per cycle, drawn from the country's single pool rather than
    /// from the facility. <b>The original charges two, flat, for every one of its
    /// recipes</b> — its own help resources say so, including for the
    /// food-processing cycle that takes four input units and makes two. The manual's
    /// tutorial priced only clothing and so admitted "one per input unit" and "two
    /// per output unit" as well; both are retracted. See
    /// <c>docs/formulas/production.md</c>.
    /// </summary>
    public long LabourCost { get; }

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
