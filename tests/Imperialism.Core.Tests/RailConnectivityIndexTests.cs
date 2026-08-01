using Imperialism.Core;
using Xunit;

namespace Imperialism.Core.Tests;

public sealed class RailConnectivityIndexTests
{
    [Fact]
    public void ConnectivityUsesCurrentOwnershipAndSnapshotsRemainImmutable()
    {
        var dimensions = new MapDimensions(4, 1);
        var state = CreateWorldState(
            dimensions,
            provincePerCell: true,
            owners: [0, 0, 0, 0],
            rails: [Link(0, 1), Link(1, 2), Link(2, 3)],
            countryCount: 2);

        var original = state.GetRailConnectivity(new CountryId(0));

        Assert.Same(original, state.GetRailConnectivity(new CountryId(0)));
        Assert.Equal(3, original.RailLinkCount);
        Assert.Equal(4, original.RailCellCount);
        Assert.Equal(1, original.ComponentCount);
        Assert.Equal(4, original.GetComponentSize(0));
        Assert.True(original.AreConnected(new CellIndex(0), new CellIndex(3)));

        state.SetProvinceOwner(new ProvinceId(1), new CountryId(1));
        var afterConquest = state.GetRailConnectivity(new CountryId(0));

        Assert.NotSame(original, afterConquest);
        Assert.False(afterConquest.AreConnected(new CellIndex(0), new CellIndex(3)));
        Assert.Null(afterConquest.GetComponentId(new CellIndex(0)));
        Assert.Equal(1, afterConquest.RailLinkCount);
        Assert.True(afterConquest.AreConnected(new CellIndex(2), new CellIndex(3)));
        Assert.True(original.AreConnected(new CellIndex(0), new CellIndex(3)));
        Assert.Equal(0, state.GetRailConnectivity(new CountryId(1)).RailLinkCount);
    }

    [Fact]
    public void RailChangesInvalidateLazilyOnlyWhenStateChanges()
    {
        var dimensions = new MapDimensions(3, 1);
        var firstLink = Link(0, 1);
        var secondLink = Link(1, 2);
        var state = CreateWorldState(
            dimensions,
            provincePerCell: false,
            owners: [0],
            rails: [firstLink]);
        var initial = state.GetRailConnectivity(new CountryId(0));

        Assert.False(initial.AreConnected(new CellIndex(0), new CellIndex(2)));
        Assert.True(state.BuildRail(secondLink));

        var extended = state.GetRailConnectivity(new CountryId(0));
        Assert.NotSame(initial, extended);
        Assert.True(extended.AreConnected(new CellIndex(0), new CellIndex(2)));
        Assert.False(state.BuildRail(secondLink));
        Assert.Same(extended, state.GetRailConnectivity(new CountryId(0)));

        Assert.True(state.RemoveRail(firstLink));
        var reduced = state.GetRailConnectivity(new CountryId(0));
        Assert.NotSame(extended, reduced);
        Assert.Null(reduced.GetComponentId(new CellIndex(0)));
        Assert.False(state.RemoveRail(firstLink));
        Assert.Same(reduced, state.GetRailConnectivity(new CountryId(0)));
    }

    [Fact]
    public void ComponentIdsAreDeterministicByLowestCellIndex()
    {
        var dimensions = new MapDimensions(5, 1);
        var state = CreateWorldState(
            dimensions,
            provincePerCell: false,
            owners: [0],
            rails: [Link(3, 4), Link(0, 1)]);

        var index = state.GetRailConnectivity(new CountryId(0));

        Assert.Equal(0, index.GetComponentId(new CellIndex(0)));
        Assert.Equal(0, index.GetComponentId(new CellIndex(1)));
        Assert.Null(index.GetComponentId(new CellIndex(2)));
        Assert.Equal(1, index.GetComponentId(new CellIndex(3)));
        Assert.Equal(1, index.GetComponentId(new CellIndex(4)));
        Assert.Equal(2, index.GetComponentSize(0));
        Assert.Equal(2, index.GetComponentSize(1));
    }

    [Fact]
    public void AddingRailNeverDisconnectsPreviouslyConnectedCells()
    {
        const int width = 32;
        var dimensions = new MapDimensions(width, 1);
        var state = CreateWorldState(
            dimensions,
            provincePerCell: false,
            owners: [0],
            rails: []);
        var previouslyConnected = new bool[width, width];

        foreach (var offset in Enumerable.Range(0, width - 1)
                     .OrderBy(static value => value % 2)
                     .ThenBy(static value => value))
        {
            Assert.True(state.BuildRail(Link(offset, offset + 1)));
            var index = state.GetRailConnectivity(new CountryId(0));
            for (var first = 0; first < width; first++)
            {
                for (var second = 0; second < width; second++)
                {
                    var connected = index.AreConnected(new CellIndex(first), new CellIndex(second));
                    Assert.False(previouslyConnected[first, second] && !connected);
                    previouslyConnected[first, second] = connected;
                }
            }
        }

        Assert.True(previouslyConnected[0, width - 1]);
    }

    [Fact]
    public void LosingProvincesNeverCreatesNewRailConnectivity()
    {
        const int width = 24;
        var dimensions = new MapDimensions(width, 1);
        var state = CreateWorldState(
            dimensions,
            provincePerCell: true,
            owners: Enumerable.Repeat(0, width).ToArray(),
            rails: Enumerable.Range(0, width - 1).Select(static cell => Link(cell, cell + 1)),
            countryCount: 2);
        var before = state.GetRailConnectivity(new CountryId(0));

        for (var province = 2; province < width; province += 3)
        {
            state.SetProvinceOwner(new ProvinceId(province), new CountryId(1));
        }

        var after = state.GetRailConnectivity(new CountryId(0));
        for (var first = 0; first < width; first++)
        {
            for (var second = 0; second < width; second++)
            {
                if (after.AreConnected(new CellIndex(first), new CellIndex(second)))
                {
                    Assert.True(before.AreConnected(new CellIndex(first), new CellIndex(second)));
                }
            }
        }
    }

    [Fact]
    public void TenTimesOriginalCellCountUsesNoLegacyDimensionAssumption()
    {
        // 360 * 180 = 64,800 cells: exactly ten times the original 108 * 60 area.
        var dimensions = new MapDimensions(360, 180);
        var rails = new List<CellLink>(dimensions.Height * (dimensions.Width - 1));
        for (var row = 0; row < dimensions.Height; row++)
        {
            for (var column = 0; column < dimensions.Width - 1; column++)
            {
                rails.Add(new CellLink(
                    dimensions.GetIndex(new HexCoord(column, row)),
                    dimensions.GetIndex(new HexCoord(column + 1, row))));
            }
        }

        var state = CreateWorldState(
            dimensions,
            provincePerCell: false,
            owners: [0],
            rails);
        var index = state.GetRailConnectivity(new CountryId(0));

        Assert.Equal(64_800, index.RailCellCount);
        Assert.Equal(64_620, index.RailLinkCount);
        Assert.Equal(180, index.ComponentCount);
        Assert.True(index.AreConnected(
            dimensions.GetIndex(new HexCoord(0, 90)),
            dimensions.GetIndex(new HexCoord(359, 90))));
        Assert.False(index.AreConnected(
            dimensions.GetIndex(new HexCoord(0, 90)),
            dimensions.GetIndex(new HexCoord(0, 91))));
    }

    [Fact]
    public void QueriesRejectUnknownCountriesCellsAndComponents()
    {
        var dimensions = new MapDimensions(2, 1);
        var state = CreateWorldState(
            dimensions,
            provincePerCell: false,
            owners: [0],
            rails: [Link(0, 1)]);
        var index = state.GetRailConnectivity(new CountryId(0));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            state.GetRailConnectivity(new CountryId(1)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            index.GetComponentId(new CellIndex(2)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            index.GetComponentSize(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            index.GetComponentSize(1));
    }

    private static WorldState CreateWorldState(
        MapDimensions dimensions,
        bool provincePerCell,
        IReadOnlyList<int> owners,
        IEnumerable<CellLink> rails,
        int? countryCount = null)
    {
        var provinceCount = provincePerCell ? dimensions.CellCount : 1;
        Assert.Equal(provinceCount, owners.Count);
        var cells = Enumerable.Range(0, dimensions.CellCount)
            .Select(index => new CellDefinition(
                new CellIndex(index),
                dimensions.GetCoordinate(new CellIndex(index)),
                new TerrainId(0),
                CellRegion.ForProvince(new ProvinceId(provincePerCell ? index : 0))))
            .ToArray();
        var provinces = Enumerable.Range(0, provinceCount)
            .Select(static index => new ProvinceDefinition(new ProvinceId(index), $"Province {index}"))
            .ToArray();
        var countries = Enumerable.Range(0, countryCount ?? (owners.Max() + 1))
            .Select(static index => new CountryDefinition(new CountryId(index), $"Country {index}"))
            .ToArray();
        var scenario = new ScenarioDefinition(
            "Connectivity",
            1815,
            owners.Select(static owner => (CountryId?)new CountryId(owner)),
            rails);

        return new WorldState(new WorldDefinition(
            new MapDefinition(dimensions, cells, provinces),
            countries,
            scenario));
    }

    private static CellLink Link(int first, int second) =>
        new(new CellIndex(first), new CellIndex(second));
}
