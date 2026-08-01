using Imperialism.Core;
using Xunit;

namespace Imperialism.Core.Tests;

public sealed class WorldDefinitionTests
{
    [Fact]
    public void MapBuildsProvinceAndSeaZoneMembershipFromCells()
    {
        var dimensions = new MapDimensions(3, 2);
        var cells = CreateCells(
            dimensions,
            CellRegion.ForProvince(new ProvinceId(0)),
            CellRegion.ForProvince(new ProvinceId(0)),
            CellRegion.ForSeaZone(new SeaZoneId(0)),
            CellRegion.ForProvince(new ProvinceId(1)),
            CellRegion.ForProvince(new ProvinceId(1)),
            CellRegion.Unassigned);
        var map = new MapDefinition(
            dimensions,
            cells,
            [new ProvinceDefinition(new ProvinceId(0), "North"), new ProvinceDefinition(new ProvinceId(1), "South")],
            [new SeaZoneDefinition(new SeaZoneId(0), "Western Sea")]);

        Assert.Equal([new CellIndex(0), new CellIndex(1)], map.GetCells(new ProvinceId(0)));
        Assert.Equal([new CellIndex(3), new CellIndex(4)], map.GetCells(new ProvinceId(1)));
        Assert.Equal([new CellIndex(2)], map.GetCells(new SeaZoneId(0)));
        Assert.Equal(map.Cells[4], map[new HexCoord(1, 1)]);
    }

    [Fact]
    public void CellsSupportMoreThanTwoResourcesWithoutLegacySlots()
    {
        var resources = new[]
        {
            new ResourceId(0),
            new ResourceId(1),
            new ResourceId(2),
            new ResourceId(3),
        };
        var cell = new CellDefinition(
            new CellIndex(0),
            new HexCoord(0, 0),
            new TerrainId(0),
            CellRegion.Unassigned,
            resources);

        Assert.Equal(resources, cell.Resources);
    }

    [Fact]
    public void CellRejectsDuplicateResources()
    {
        Assert.Throws<ArgumentException>(() => new CellDefinition(
            new CellIndex(0),
            new HexCoord(0, 0),
            new TerrainId(0),
            CellRegion.Unassigned,
            [new ResourceId(4), new ResourceId(4)]));
    }

    [Fact]
    public void CellLinksAreUndirectedAndRejectSelfLinks()
    {
        var forward = new CellLink(new CellIndex(2), new CellIndex(9));
        var reverse = new CellLink(new CellIndex(9), new CellIndex(2));

        Assert.Equal(forward, reverse);
        Assert.Equal(new CellIndex(2), reverse.First);
        Assert.Equal(new CellIndex(9), reverse.Second);
        Assert.Throws<ArgumentException>(() =>
            new CellLink(new CellIndex(4), new CellIndex(4)));
    }

    [Fact]
    public void RiverPathsAreUndirectedPerCellShapes()
    {
        var forward = new RiverPath(RiverEndpoint.NorthEast, RiverEndpoint.WestLower);
        var reverse = new RiverPath(RiverEndpoint.WestLower, RiverEndpoint.NorthEast);

        Assert.Equal(forward, reverse);
        Assert.Equal(RiverEndpoint.NorthEast, reverse.First);
        Assert.Equal(RiverEndpoint.WestLower, reverse.Second);
        Assert.Throws<ArgumentException>(() =>
            new RiverPath(RiverEndpoint.Source, RiverEndpoint.Source));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RiverPath((RiverEndpoint)200, RiverEndpoint.Mouth));
    }

    [Fact]
    public void CellRejectsDefaultRiverPathValue()
    {
        Assert.Throws<ArgumentException>(() => new CellDefinition(
            new CellIndex(0),
            new HexCoord(0, 0),
            new TerrainId(0),
            CellRegion.Unassigned,
            river: default(RiverPath)));
    }

    [Fact]
    public void DefinitionsAllowUtf8NamesFarBeyondLegacyPadding()
    {
        var name = "République industrielle de très longue durée 世界";

        Assert.Equal(name, new CountryDefinition(new CountryId(0), name).Name);
        Assert.Equal(name, new ProvinceDefinition(new ProvinceId(0), name).Name);
        Assert.Equal(name, new SeaZoneDefinition(new SeaZoneId(0), name).Name);
    }

    [Fact]
    public void WorldStateOwnsMutableProvinceOwnership()
    {
        var map = CreateOneProvinceMap();
        var scenario = new ScenarioDefinition("Start", 1815, [new CountryId(0)]);
        var world = new WorldDefinition(
            map,
            [new CountryDefinition(new CountryId(0), "A"), new CountryDefinition(new CountryId(1), "B")],
            scenario);
        var state = new WorldState(world);

        state.SetProvinceOwner(new ProvinceId(0), new CountryId(1));

        Assert.Equal(new CountryId(1), state.GetProvinceOwner(new ProvinceId(0)));
        Assert.Equal(1815, state.CurrentYear);
        Assert.Equal(new CountryId(0), scenario.InitialProvinceOwners[0]);
        Assert.Same(map, world.Map);
    }

    [Fact]
    public void RiverPathsAreCellGeographyWhileRailsAndCapitalsAreMutableState()
    {
        var dimensions = new MapDimensions(2, 1);
        var link = new CellLink(new CellIndex(0), new CellIndex(1));
        var map = new MapDefinition(
            dimensions,
            [
                new CellDefinition(
                    new CellIndex(0),
                    new HexCoord(0, 0),
                    new TerrainId(0),
                    CellRegion.ForProvince(new ProvinceId(0)),
                    settlementSite: SettlementSiteKind.Urban,
                    river: new RiverPath(RiverEndpoint.EastUpper, RiverEndpoint.WestUpper)),
                new CellDefinition(
                    new CellIndex(1),
                    new HexCoord(1, 0),
                    new TerrainId(0),
                    CellRegion.ForProvince(new ProvinceId(1)),
                    settlementSite: SettlementSiteKind.Urban),
            ],
            [
                new ProvinceDefinition(new ProvinceId(0), "West"),
                new ProvinceDefinition(new ProvinceId(1), "East"),
            ]);
        var scenario = new ScenarioDefinition(
            "Start",
            1815,
            [new CountryId(0), new CountryId(1)],
            [link],
            [
                new CountryCapital(new CountryId(0), new CellIndex(0)),
                new CountryCapital(new CountryId(1), new CellIndex(1)),
            ]);
        var world = new WorldDefinition(
            map,
            [
                new CountryDefinition(new CountryId(0), "A"),
                new CountryDefinition(new CountryId(1), "B"),
            ],
            scenario);
        var state = new WorldState(world);

        Assert.Equal(
            new RiverPath(RiverEndpoint.EastUpper, RiverEndpoint.WestUpper),
            map.Cells[0].River);
        Assert.Null(map.Cells[1].River);
        Assert.True(state.HasRail(link));
        var railSnapshot = state.GetRailLinks();
        Assert.Equal([link], railSnapshot);
        Assert.Equal(new CellIndex(0), state.GetCountryCapital(new CountryId(0)));

        Assert.True(state.RemoveRail(link));
        state.SetCountryCapital(new CountryId(0), null);

        Assert.False(state.HasRail(link));
        Assert.Empty(state.GetRailLinks());
        Assert.Equal([link], railSnapshot);
        Assert.Null(state.GetCountryCapital(new CountryId(0)));
        Assert.Contains(link, scenario.InitialRailLinks);
        Assert.Contains(
            new CountryCapital(new CountryId(0), new CellIndex(0)),
            scenario.InitialCountryCapitals);
    }

    [Fact]
    public void ModernLinksAndCapitalsAreValidatedAtWorldBoundary()
    {
        var dimensions = new MapDimensions(3, 1);
        var cells = CreateCells(
            dimensions,
            CellRegion.ForProvince(new ProvinceId(0)),
            CellRegion.ForProvince(new ProvinceId(0)),
            CellRegion.ForProvince(new ProvinceId(0)));
        var provinces = new[] { new ProvinceDefinition(new ProvinceId(0), "Province") };

        var map = new MapDefinition(dimensions, cells, provinces);
        var countries = new[] { new CountryDefinition(new CountryId(0), "Country") };
        Assert.Throws<ArgumentException>(() => new WorldDefinition(
            map,
            countries,
            new ScenarioDefinition(
                "Invalid rail",
                1815,
                [new CountryId(0)],
                [new CellLink(new CellIndex(0), new CellIndex(2))])));
        Assert.Throws<ArgumentException>(() => new WorldDefinition(
            map,
            countries,
            new ScenarioDefinition(
                "Invalid capital",
                1815,
                [new CountryId(0)],
                initialCountryCapitals:
                [new CountryCapital(new CountryId(0), new CellIndex(0))])));
    }

    [Fact]
    public void OneMapCanBackMultipleScenarioStarts()
    {
        var map = CreateOneProvinceMap();
        var countries = new[]
        {
            new CountryDefinition(new CountryId(0), "A"),
            new CountryDefinition(new CountryId(1), "B"),
        };

        var first = new WorldDefinition(map, countries, new ScenarioDefinition("First", 1815, [new CountryId(0)]));
        var second = new WorldDefinition(map, countries, new ScenarioDefinition("Second", 1882, [new CountryId(1)]));

        Assert.Same(first.Map, second.Map);
        Assert.NotEqual(
            new WorldState(first).GetProvinceOwner(new ProvinceId(0)),
            new WorldState(second).GetProvinceOwner(new ProvinceId(0)));
    }

    [Fact]
    public void ModernModelHasNoLegacyCountryOrProvinceCap()
    {
        const int provinceCount = 400;
        const int countryCount = 30;
        var dimensions = new MapDimensions(provinceCount, 1);
        var provinces = Enumerable.Range(0, provinceCount)
            .Select(static value => new ProvinceDefinition(new ProvinceId(value), $"Province {value}"))
            .ToArray();
        var countries = Enumerable.Range(0, countryCount)
            .Select(static value => new CountryDefinition(new CountryId(value), $"Country {value}"))
            .ToArray();
        var cells = Enumerable.Range(0, provinceCount)
            .Select(value => new CellDefinition(
                new CellIndex(value),
                new HexCoord(value, 0),
                new TerrainId(0),
                CellRegion.ForProvince(new ProvinceId(value))))
            .ToArray();
        var owners = Enumerable.Range(0, provinceCount)
            .Select(value => (CountryId?)new CountryId(value % countryCount))
            .ToArray();

        var world = new WorldDefinition(
            new MapDefinition(dimensions, cells, provinces),
            countries,
            new ScenarioDefinition("Large", 2000, owners));

        Assert.Equal(provinceCount, world.Map.Provinces.Count);
        Assert.Equal(countryCount, world.Countries.Count);
        Assert.Equal(new CountryId(9), new WorldState(world).GetProvinceOwner(new ProvinceId(399)));
    }

    [Fact]
    public void MapRejectsMismatchedIndexCoordinateOrRegionReference()
    {
        var dimensions = new MapDimensions(1, 1);
        Assert.Throws<ArgumentException>(() => new MapDefinition(
            dimensions,
            [new CellDefinition(
                new CellIndex(0),
                new HexCoord(1, 0),
                new TerrainId(0),
                CellRegion.Unassigned)]));
        Assert.Throws<ArgumentException>(() => new MapDefinition(
            dimensions,
            [new CellDefinition(
                new CellIndex(0),
                new HexCoord(0, 0),
                new TerrainId(0),
                CellRegion.ForProvince(new ProvinceId(0)))]));
    }

    [Fact]
    public void WorldRejectsSparseIdsAndInvalidInitialOwnership()
    {
        var map = CreateOneProvinceMap();
        Assert.Throws<ArgumentException>(() => new WorldDefinition(
            map,
            [new CountryDefinition(new CountryId(1), "Sparse")],
            new ScenarioDefinition("Invalid", 1815, [new CountryId(1)])));
        Assert.Throws<ArgumentException>(() => new WorldDefinition(
            map,
            [new CountryDefinition(new CountryId(0), "Only")],
            new ScenarioDefinition("Invalid", 1815, [new CountryId(1)])));
    }

    private static MapDefinition CreateOneProvinceMap()
    {
        var dimensions = new MapDimensions(1, 1);
        return new MapDefinition(
            dimensions,
            CreateCells(dimensions, CellRegion.ForProvince(new ProvinceId(0))),
            [new ProvinceDefinition(new ProvinceId(0), "Province")]);
    }

    private static CellDefinition[] CreateCells(
        MapDimensions dimensions,
        params CellRegion[] regions)
    {
        Assert.Equal(dimensions.CellCount, regions.Length);
        return regions.Select((region, value) => new CellDefinition(
            new CellIndex(value),
            dimensions.GetCoordinate(new CellIndex(value)),
            new TerrainId(0),
            region)).ToArray();
    }
}
