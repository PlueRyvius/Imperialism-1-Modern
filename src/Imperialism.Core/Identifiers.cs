using System.Globalization;

namespace Imperialism.Core;

public readonly record struct CellIndex
{
    public CellIndex(int value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        Value = value;
    }

    public int Value { get; }

    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}

public readonly record struct ProvinceId
{
    public ProvinceId(int value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        Value = value;
    }

    public int Value { get; }

    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}

public readonly record struct CountryId
{
    public CountryId(int value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        Value = value;
    }

    public int Value { get; }

    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}

public readonly record struct SeaZoneId
{
    public SeaZoneId(int value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        Value = value;
    }

    public int Value { get; }

    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}

public readonly record struct TerrainId
{
    public TerrainId(int value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        Value = value;
    }

    public int Value { get; }

    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}

public readonly record struct ResourceId
{
    public ResourceId(int value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        Value = value;
    }

    public int Value { get; }

    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}

public readonly record struct CommodityId
{
    public CommodityId(int value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        Value = value;
    }

    public int Value { get; }

    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}

public readonly record struct ProductionFacilityId
{
    public ProductionFacilityId(int value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        Value = value;
    }

    public int Value { get; }

    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}

public readonly record struct ProductionRecipeId
{
    public ProductionRecipeId(int value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        Value = value;
    }

    public int Value { get; }

    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}

public readonly record struct TechnologyId
{
    public TechnologyId(int value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        Value = value;
    }

    public int Value { get; }

    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}

public readonly record struct CivilianTypeId
{
    public CivilianTypeId(int value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        Value = value;
    }

    public int Value { get; }

    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}

/// <summary>
/// Identifies a class of ship. A legacy <c>ship</c> record's type field is a
/// <b>1-based</b> index into the world's ship table, verified against the corpus: read
/// as 0-based it puts a Clipper in an 1816 skirmish whose powers hold no technology at
/// all, and five more in <c>s13</c> and <c>s14</c>. See <c>docs/formulas/trade.md</c>.
/// </summary>
public readonly record struct ShipTypeId
{
    public ShipTypeId(int value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        Value = value;
    }

    public int Value { get; }

    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}

/// <summary>
/// Identifies one of the original executable's 30 army-unit rows. A legacy
/// <c>army</c> record uses this as a <b>zero-based</b> index; unlike ships, no
/// one-based conversion is involved. See <c>docs/scenario-semantics.md</c>.
/// </summary>
public readonly record struct ArmyTypeId
{
    /// <summary>The number of army rows in the original executable table.</summary>
    public const int OriginalTypeCount = 30;

    public ArmyTypeId(int value)
    {
        if ((uint)value >= OriginalTypeCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                $"Army type must be between 0 and {OriginalTypeCount - 1}.");
        }

        Value = value;
    }

    public int Value { get; }

    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}

/// <summary>
/// Identifies one positioned scenario fleet. IDs are issued in scenario-record
/// order and are never reused during a world state.
/// </summary>
public readonly record struct FleetId
{
    public FleetId(long value)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Fleet IDs must be positive.");
        }

        Value = value;
    }

    public long Value { get; }

    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}

/// <summary>
/// Identifies one assembled task force. IDs are issued in assembly order and
/// are never reused during a world state.
/// </summary>
public readonly record struct TaskForceId
{
    public TaskForceId(long value)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Task-force IDs must be positive.");
        }

        Value = value;
    }

    public long Value { get; }

    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}

/// <summary>
/// Identifies one civilian on the map. Unlike every other id here this is not
/// dense: civilians are created and destroyed during play, so an id is issued
/// once and never reused.
/// </summary>
public readonly record struct CivilianUnitId
{
    public CivilianUnitId(long value)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Civilian unit IDs must be positive.");
        }

        Value = value;
    }

    public long Value { get; }

    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}

public readonly record struct DeliveryId
{
    public DeliveryId(long value)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Delivery IDs must be positive.");
        }

        Value = value;
    }

    public long Value { get; }

    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}
