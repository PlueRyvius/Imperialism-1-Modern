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
