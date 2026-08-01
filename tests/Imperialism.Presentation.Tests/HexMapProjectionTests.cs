using Imperialism.Core;
using Imperialism.Presentation;
using Xunit;

namespace Imperialism.Presentation.Tests;

public sealed class HexMapProjectionTests
{
    [Fact]
    public void PresentationAssemblyDoesNotReferenceGodotOrLegacyCodecs()
    {
        var references = typeof(HexMapProjection).Assembly.GetReferencedAssemblies();

        Assert.DoesNotContain(references, static reference =>
            reference.Name?.StartsWith("Godot", StringComparison.Ordinal) == true ||
            reference.Name is "Imperialism.Formats" or "Imperialism.LegacyImport");
    }

    [Fact]
    public void EveryCenterPicksItsCellOnArbitraryDimensions()
    {
        var dimensions = new MapDimensions(137, 83);
        var projection = new HexMapProjection(dimensions, 19.5);

        for (var value = 0; value < dimensions.CellCount; value++)
        {
            var index = new CellIndex(value);
            Assert.Equal(index, projection.Pick(projection.GetCenter(index)));
        }
    }

    [Fact]
    public void OddRowsAreShiftedHalfAHexWidthToTheRight()
    {
        var projection = new HexMapProjection(new MapDimensions(3, 3), 20);
        var even = projection.GetCenter(new HexCoord(0, 0));
        var odd = projection.GetCenter(new HexCoord(0, 1));

        Assert.Equal(projection.HexWidth / 2, odd.X - even.X, 10);
        Assert.Equal(projection.RowSpacing, odd.Y - even.Y, 10);
    }

    [Fact]
    public void PickRejectsPointsOutsideTheActualHexes()
    {
        var projection = new HexMapProjection(new MapDimensions(1, 1), 20);

        Assert.Null(projection.Pick(new MapPoint(-0.01, 20)));
        Assert.Null(projection.Pick(new MapPoint(0.01, 0.01)));
        Assert.Null(projection.Pick(new MapPoint(double.NaN, 0)));
    }

    [Fact]
    public void SharedVerticesResolveDeterministically()
    {
        var dimensions = new MapDimensions(2, 2);
        var projection = new HexMapProjection(dimensions, 20);
        var vertex = projection.GetVertices(new HexCoord(0, 0))[2];

        var first = projection.Pick(vertex);
        var second = projection.Pick(vertex);

        Assert.NotNull(first);
        Assert.Equal(first, second);
    }

    [Fact]
    public void RiverEndpointsStayOnTheirOwningHexOrItsSharedBoundary()
    {
        var dimensions = new MapDimensions(2, 2);
        var projection = new HexMapProjection(dimensions, 20);
        var cell = dimensions.GetIndex(new HexCoord(1, 1));

        foreach (var endpoint in Enum.GetValues<RiverEndpoint>())
        {
            var picked = projection.Pick(projection.GetRiverEndpoint(cell, endpoint));
            Assert.NotNull(picked);
            Assert.InRange(
                dimensions.GetCoordinate(cell).DistanceTo(dimensions.GetCoordinate(picked.Value)),
                0,
                1);
        }
    }

    [Fact]
    public void BoundsExpandForShiftedOddRows()
    {
        var projection = new HexMapProjection(new MapDimensions(4, 2), 12);

        Assert.Equal(4.5 * projection.HexWidth, projection.Bounds.Width, 10);
        Assert.Equal(3.5 * projection.Radius, projection.Bounds.Height, 10);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void RadiusMustBeFiniteAndPositive(double radius)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new HexMapProjection(new MapDimensions(1, 1), radius));
    }
}
