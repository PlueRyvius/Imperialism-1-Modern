namespace Imperialism.Core;

/// <summary>A pointy-topped odd-row offset coordinate; odd rows are shifted right.</summary>
public readonly record struct HexCoord(int Column, int Row)
{
    public HexCoord Neighbor(HexDirection direction)
    {
        var oddRow = (Row & 1) != 0;
        return direction switch
        {
            HexDirection.NorthEast => new HexCoord(Column + (oddRow ? 1 : 0), Row - 1),
            HexDirection.East => new HexCoord(Column + 1, Row),
            HexDirection.SouthEast => new HexCoord(Column + (oddRow ? 1 : 0), Row + 1),
            HexDirection.SouthWest => new HexCoord(Column - (oddRow ? 0 : 1), Row + 1),
            HexDirection.West => new HexCoord(Column - 1, Row),
            HexDirection.NorthWest => new HexCoord(Column - (oddRow ? 0 : 1), Row - 1),
            _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, "Unknown direction."),
        };
    }

    public bool TryGetNeighbor(
        HexDirection direction,
        MapDimensions dimensions,
        out HexCoord neighbor)
    {
        neighbor = Neighbor(direction);
        return dimensions.Contains(neighbor);
    }

    public AxialCoord ToAxial() => new(Column - ((Row - (Row & 1)) / 2), Row);

    public int DistanceTo(HexCoord other) => ToAxial().DistanceTo(other.ToAxial());

    public override string ToString() => $"({Column}, {Row})";
}

public readonly record struct AxialCoord(int Q, int R)
{
    public HexCoord ToOddRowOffset() => new(Q + ((R - (R & 1)) / 2), R);

    public int DistanceTo(AxialCoord other)
    {
        var deltaQ = (long)Q - other.Q;
        var deltaR = (long)R - other.R;
        var deltaS = -(long)Q - R - (-(long)other.Q - other.R);
        var distance = Math.Max(Math.Abs(deltaQ), Math.Max(Math.Abs(deltaR), Math.Abs(deltaS)));
        return checked((int)distance);
    }
}
