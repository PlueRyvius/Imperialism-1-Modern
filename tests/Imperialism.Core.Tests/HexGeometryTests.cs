using Imperialism.Core;
using Xunit;

namespace Imperialism.Core.Tests;

public sealed class HexGeometryTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    [InlineData(-1, 1)]
    [InlineData(1, -1)]
    public void DimensionsRejectNonPositiveValues(int width, int height)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MapDimensions(width, height));
    }

    [Fact]
    public void DimensionsUseCheckedCellCountArithmetic()
    {
        Assert.Throws<OverflowException>(() => new MapDimensions(int.MaxValue, 2));
    }

    [Fact]
    public void IndexAndCoordinateRoundTripOnLargeNonLegacyMap()
    {
        var dimensions = new MapDimensions(257, 129);

        for (var value = 0; value < dimensions.CellCount; value++)
        {
            var index = new CellIndex(value);
            Assert.Equal(index, dimensions.GetIndex(dimensions.GetCoordinate(index)));
        }
    }

    [Fact]
    public void CellIndexIsRowMajor()
    {
        var dimensions = new MapDimensions(108, 60);

        Assert.Equal(new CellIndex(1_333), dimensions.GetIndex(new HexCoord(37, 12)));
        Assert.Equal(new CellIndex(6_258), dimensions.GetIndex(new HexCoord(102, 57)));
        Assert.Equal(new CellIndex(5_933), dimensions.GetIndex(new HexCoord(101, 54)));
    }

    [Theory]
    [MemberData(nameof(VerifiedNeighborCases))]
    public void NeighborsUseVerifiedOddRowOffsetConvention(
        HexCoord origin,
        HexDirection direction,
        HexCoord expected)
    {
        Assert.Equal(expected, origin.Neighbor(direction));
    }

    [Fact]
    public void EveryDirectionAndOppositeReturnsToOrigin()
    {
        for (var row = -10; row <= 10; row++)
        {
            for (var column = -10; column <= 10; column++)
            {
                var origin = new HexCoord(column, row);
                foreach (var direction in HexDirections.All)
                {
                    Assert.Equal(origin, origin.Neighbor(direction).Neighbor(direction.Opposite()));
                }
            }
        }
    }

    [Fact]
    public void BoundedNeighborsNeverWrapAcrossMapEdges()
    {
        var dimensions = new MapDimensions(3, 3);

        Assert.Equal(2, CountNeighbors(new HexCoord(0, 0), dimensions));
        Assert.Equal(3, CountNeighbors(new HexCoord(2, 0), dimensions));
        Assert.Equal(5, CountNeighbors(new HexCoord(0, 1), dimensions));
        Assert.Equal(3, CountNeighbors(new HexCoord(2, 1), dimensions));
        Assert.Equal(0, CountNeighbors(new HexCoord(0, 0), new MapDimensions(1, 1)));
    }

    [Fact]
    public void AxialConversionRoundTripsIncludingNegativeCoordinates()
    {
        for (var row = -20; row <= 20; row++)
        {
            for (var column = -20; column <= 20; column++)
            {
                var offset = new HexCoord(column, row);
                Assert.Equal(offset, offset.ToAxial().ToOddRowOffset());
            }
        }
    }

    [Fact]
    public void DistanceIsSymmetricAndCountsNeighborSteps()
    {
        var origin = new HexCoord(0, 0);
        foreach (var direction in HexDirections.All)
        {
            Assert.Equal(1, origin.DistanceTo(origin.Neighbor(direction)));
        }

        var destination = new HexCoord(9, 7);
        Assert.Equal(origin.DistanceTo(destination), destination.DistanceTo(origin));
        Assert.Equal(13, origin.DistanceTo(destination));
    }

    [Fact]
    public void DistanceMatchesShortestPathsAcrossBoundedMap()
    {
        var dimensions = new MapDimensions(9, 8);
        for (var startValue = 0; startValue < dimensions.CellCount; startValue++)
        {
            var start = dimensions.GetCoordinate(new CellIndex(startValue));
            var pathLengths = FindPathLengths(start, dimensions);
            for (var destinationValue = 0; destinationValue < dimensions.CellCount; destinationValue++)
            {
                var destination = dimensions.GetCoordinate(new CellIndex(destinationValue));
                Assert.Equal(pathLengths[destinationValue], start.DistanceTo(destination));
            }
        }
    }

    [Fact]
    public void DirectionMasksRejectNonHexBitsAndSupportEditing()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new HexDirectionMask(64));
        var mask = new HexDirectionMask(0)
            .With(HexDirection.NorthEast)
            .With(HexDirection.West);

        Assert.True(mask.Contains(HexDirection.NorthEast));
        Assert.True(mask.Contains(HexDirection.West));
        Assert.False(mask.Without(HexDirection.NorthEast).Contains(HexDirection.NorthEast));
        Assert.Throws<ArgumentOutOfRangeException>(() => mask.Without((HexDirection)64));
    }

    public static TheoryData<HexCoord, HexDirection, HexCoord> VerifiedNeighborCases => new()
    {
        { new HexCoord(10, 4), HexDirection.NorthEast, new HexCoord(10, 3) },
        { new HexCoord(10, 4), HexDirection.East, new HexCoord(11, 4) },
        { new HexCoord(10, 4), HexDirection.SouthEast, new HexCoord(10, 5) },
        { new HexCoord(10, 4), HexDirection.SouthWest, new HexCoord(9, 5) },
        { new HexCoord(10, 4), HexDirection.West, new HexCoord(9, 4) },
        { new HexCoord(10, 4), HexDirection.NorthWest, new HexCoord(9, 3) },
        { new HexCoord(10, 5), HexDirection.NorthEast, new HexCoord(11, 4) },
        { new HexCoord(10, 5), HexDirection.East, new HexCoord(11, 5) },
        { new HexCoord(10, 5), HexDirection.SouthEast, new HexCoord(11, 6) },
        { new HexCoord(10, 5), HexDirection.SouthWest, new HexCoord(10, 6) },
        { new HexCoord(10, 5), HexDirection.West, new HexCoord(9, 5) },
        { new HexCoord(10, 5), HexDirection.NorthWest, new HexCoord(10, 4) },
    };

    private static int CountNeighbors(HexCoord coordinate, MapDimensions dimensions)
    {
        var count = 0;
        foreach (var direction in HexDirections.All)
        {
            if (coordinate.TryGetNeighbor(direction, dimensions, out _))
            {
                count++;
            }
        }

        return count;
    }

    private static int[] FindPathLengths(HexCoord start, MapDimensions dimensions)
    {
        var distances = Enumerable.Repeat(-1, dimensions.CellCount).ToArray();
        var queue = new Queue<HexCoord>();
        distances[dimensions.GetIndex(start).Value] = 0;
        queue.Enqueue(start);

        while (queue.TryDequeue(out var coordinate))
        {
            var distance = distances[dimensions.GetIndex(coordinate).Value];
            foreach (var direction in HexDirections.All)
            {
                if (!coordinate.TryGetNeighbor(direction, dimensions, out var neighbor))
                {
                    continue;
                }

                var neighborIndex = dimensions.GetIndex(neighbor).Value;
                if (distances[neighborIndex] >= 0)
                {
                    continue;
                }

                distances[neighborIndex] = distance + 1;
                queue.Enqueue(neighbor);
            }
        }

        return distances;
    }
}
