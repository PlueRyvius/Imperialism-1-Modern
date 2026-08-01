using Imperialism.Core;

namespace Imperialism.Presentation;

public readonly record struct MapPoint(double X, double Y)
{
    public double DistanceSquaredTo(MapPoint other)
    {
        var deltaX = X - other.X;
        var deltaY = Y - other.Y;
        return (deltaX * deltaX) + (deltaY * deltaY);
    }
}

public readonly record struct MapBounds(double Left, double Top, double Right, double Bottom)
{
    public double Width => Right - Left;

    public double Height => Bottom - Top;

    public MapPoint Center => new((Left + Right) / 2, (Top + Bottom) / 2);
}

/// <summary>
/// Projects the core's pointy-topped odd-row coordinates into map space.
/// The radius is the distance from a hex center to its north or south vertex.
/// </summary>
public sealed class HexMapProjection
{
    private static readonly double SqrtThree = Math.Sqrt(3);
    private readonly MapPoint[] _localVertices;

    public HexMapProjection(MapDimensions dimensions, double radius = 32)
    {
        if (!double.IsFinite(radius) || radius <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(radius), "Hex radius must be finite and positive.");
        }

        Dimensions = dimensions;
        Radius = radius;
        HexWidth = SqrtThree * radius;
        RowSpacing = 1.5 * radius;
        _localVertices =
        [
            new MapPoint(0, -radius),
            new MapPoint(HexWidth / 2, -radius / 2),
            new MapPoint(HexWidth / 2, radius / 2),
            new MapPoint(0, radius),
            new MapPoint(-HexWidth / 2, radius / 2),
            new MapPoint(-HexWidth / 2, -radius / 2),
        ];

        var oddRowOffset = dimensions.Height > 1 ? HexWidth / 2 : 0;
        Bounds = new MapBounds(
            0,
            0,
            checked((dimensions.Width * HexWidth) + oddRowOffset),
            checked(((dimensions.Height - 1) * RowSpacing) + (2 * radius)));
    }

    public MapDimensions Dimensions { get; }

    public double Radius { get; }

    public double HexWidth { get; }

    public double RowSpacing { get; }

    public MapBounds Bounds { get; }

    public MapPoint GetCenter(HexCoord coordinate)
    {
        if (!Dimensions.Contains(coordinate))
        {
            throw new ArgumentOutOfRangeException(nameof(coordinate));
        }

        var rowOffset = (coordinate.Row & 1) == 0 ? 0 : HexWidth / 2;
        return new MapPoint(
            (HexWidth / 2) + (coordinate.Column * HexWidth) + rowOffset,
            Radius + (coordinate.Row * RowSpacing));
    }

    public MapPoint GetCenter(CellIndex index) => GetCenter(Dimensions.GetCoordinate(index));

    public IReadOnlyList<MapPoint> GetVertices(HexCoord coordinate)
    {
        return Array.AsReadOnly(Enumerable.Range(0, _localVertices.Length)
            .Select(index => GetVertex(coordinate, index))
            .ToArray());
    }

    public MapPoint GetVertex(HexCoord coordinate, int vertexIndex)
    {
        if ((uint)vertexIndex >= (uint)_localVertices.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(vertexIndex));
        }

        var center = GetCenter(coordinate);
        var vertex = _localVertices[vertexIndex];
        return new MapPoint(center.X + vertex.X, center.Y + vertex.Y);
    }

    public CellIndex? Pick(MapPoint point)
    {
        if (!double.IsFinite(point.X) || !double.IsFinite(point.Y) ||
            point.X < Bounds.Left || point.Y < Bounds.Top ||
            point.X > Bounds.Right || point.Y > Bounds.Bottom)
        {
            return null;
        }

        var approximateRow = (int)Math.Round((point.Y - Radius) / RowSpacing);
        CellIndex? best = null;
        var bestDistance = double.PositiveInfinity;
        for (var row = approximateRow - 1; row <= approximateRow + 1; row++)
        {
            if ((uint)row >= (uint)Dimensions.Height)
            {
                continue;
            }

            var rowOffset = (row & 1) == 0 ? 0 : HexWidth / 2;
            var approximateColumn = (int)Math.Round((point.X - (HexWidth / 2) - rowOffset) / HexWidth);
            for (var column = approximateColumn - 1; column <= approximateColumn + 1; column++)
            {
                var coordinate = new HexCoord(column, row);
                if (!Dimensions.Contains(coordinate))
                {
                    continue;
                }

                var center = GetCenter(coordinate);
                if (ContainsRelative(point.X - center.X, point.Y - center.Y))
                {
                    var index = Dimensions.GetIndex(coordinate);
                    var distance = point.DistanceSquaredTo(center);
                    if (distance < bestDistance ||
                        (distance == bestDistance && (!best.HasValue || index.Value < best.Value.Value)))
                    {
                        best = index;
                        bestDistance = distance;
                    }
                }
            }
        }

        return best;
    }

    public MapPoint GetRiverEndpoint(CellIndex cell, RiverEndpoint endpoint)
    {
        var center = GetCenter(cell);
        var local = endpoint switch
        {
            RiverEndpoint.NorthEast => new MapPoint(HexWidth / 4, -3 * Radius / 4),
            RiverEndpoint.EastUpper => new MapPoint(HexWidth / 2, -Radius / 4),
            RiverEndpoint.EastLower => new MapPoint(HexWidth / 2, Radius / 4),
            RiverEndpoint.SouthEast => new MapPoint(HexWidth / 4, 3 * Radius / 4),
            RiverEndpoint.SouthWest => new MapPoint(-HexWidth / 4, 3 * Radius / 4),
            RiverEndpoint.WestUpper => new MapPoint(-HexWidth / 2, -Radius / 4),
            RiverEndpoint.WestLower => new MapPoint(-HexWidth / 2, Radius / 4),
            RiverEndpoint.NorthWest => new MapPoint(-HexWidth / 4, -3 * Radius / 4),
            RiverEndpoint.Source or RiverEndpoint.Mouth => default,
            _ => throw new ArgumentOutOfRangeException(nameof(endpoint), endpoint, "Unknown river endpoint."),
        };
        return new MapPoint(center.X + local.X, center.Y + local.Y);
    }

    private bool ContainsRelative(double x, double y)
    {
        // Pointy hex inequality for vertices (0, +/-R) and (+/-sqrt(3)R/2, +/-R/2).
        const double tolerance = 1e-9;
        var absoluteX = Math.Abs(x);
        var absoluteY = Math.Abs(y);
        return absoluteY <= Radius + tolerance &&
            absoluteX + (SqrtThree * absoluteY) <= (SqrtThree * Radius) + tolerance;
    }
}
