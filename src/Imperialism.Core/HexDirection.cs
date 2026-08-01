namespace Imperialism.Core;

public enum HexDirection : byte
{
    NorthEast = 1,
    East = 2,
    SouthEast = 4,
    SouthWest = 8,
    West = 16,
    NorthWest = 32,
}

public static class HexDirections
{
    private static readonly HexDirection[] Values =
    [
        HexDirection.NorthEast,
        HexDirection.East,
        HexDirection.SouthEast,
        HexDirection.SouthWest,
        HexDirection.West,
        HexDirection.NorthWest,
    ];

    public static ReadOnlySpan<HexDirection> All => Values;

    public static HexDirection Opposite(this HexDirection direction) => direction switch
    {
        HexDirection.NorthEast => HexDirection.SouthWest,
        HexDirection.East => HexDirection.West,
        HexDirection.SouthEast => HexDirection.NorthWest,
        HexDirection.SouthWest => HexDirection.NorthEast,
        HexDirection.West => HexDirection.East,
        HexDirection.NorthWest => HexDirection.SouthEast,
        _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, "Unknown direction."),
    };
}

public readonly record struct HexDirectionMask
{
    private const byte ValidBits = 0b0011_1111;

    public HexDirectionMask(byte bits)
    {
        if ((bits & ~ValidBits) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bits), "Only the six hex-edge bits are valid.");
        }

        Bits = bits;
    }

    public byte Bits { get; }

    public bool Contains(HexDirection direction)
    {
        _ = direction.Opposite();
        return (Bits & (byte)direction) != 0;
    }

    public HexDirectionMask With(HexDirection direction)
    {
        Validate(direction);
        return new HexDirectionMask((byte)(Bits | (byte)direction));
    }

    public HexDirectionMask Without(HexDirection direction)
    {
        Validate(direction);
        return new HexDirectionMask((byte)(Bits & ~(byte)direction));
    }

    private static void Validate(HexDirection direction) => _ = direction.Opposite();
}
