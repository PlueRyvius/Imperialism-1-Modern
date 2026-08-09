using Imperialism.Core;
using Xunit;

namespace Imperialism.Core.Tests;

public sealed class FleetStateTests
{
    [Fact]
    public void ScenarioFleetsPreserveRecordOrderAndMapKnownSeaZones()
    {
        var state = CreateState(
        [
            new InitialShip(new CountryId(0), new ShipTypeId(0), 1, 3),
            new InitialShip(new CountryId(0), new ShipTypeId(0), 7, 2),
        ]);

        Assert.Collection(
            state.Fleets,
            fleet =>
            {
                Assert.Equal(new FleetId(1), fleet.Id);
                Assert.Equal(3, fleet.Count);
                Assert.Equal(new SeaZoneId(1), fleet.SeaZone);
            },
            fleet =>
            {
                Assert.Equal(new FleetId(2), fleet.Id);
                Assert.Equal(2, fleet.Count);
                Assert.Null(fleet.SeaZone);
            });
        Assert.Equal(5, state.GetShipCount(new CountryId(0), new ShipTypeId(0)));
        Assert.Equal(state.Fleets[0], state.GetFleet(new FleetId(1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => state.GetFleet(new FleetId(3)));
    }

    [Fact]
    public void DefaultMerchantShipsDoNotInventAPositionedFleet()
    {
        var state = CreateState([]);

        Assert.Empty(state.Fleets);
    }

    private static WorldState CreateState(IEnumerable<InitialShip> ships)
    {
        var dimensions = new MapDimensions(2, 1);
        var map = new MapDefinition(
            dimensions,
            [
                new CellDefinition(new CellIndex(0), new HexCoord(0, 0), new TerrainId(0),
                    CellRegion.ForSeaZone(new SeaZoneId(0))),
                new CellDefinition(new CellIndex(1), new HexCoord(1, 0), new TerrainId(0),
                    CellRegion.ForSeaZone(new SeaZoneId(1))),
            ],
            seaZones:
            [
                new SeaZoneDefinition(new SeaZoneId(0), "West"),
                new SeaZoneDefinition(new SeaZoneId(1), "East"),
            ]);
        var scenario = new ScenarioDefinition(
            "Fleets",
            1815,
            [],
            initialShips: ships,
            defaultStartCountries: [new CountryId(0)]);
        var defaults = new StartingDefaults(
            [],
            null,
            [],
            0,
            [],
            0,
            [new ShipDefault(new ShipTypeId(0), 3)]);
        return new WorldState(new WorldDefinition(
            map,
            [new CountryDefinition(new CountryId(0), "Power")],
            scenario,
            startingDefaults: defaults,
            shipTypes: [new ShipTypeDefinition(new ShipTypeId(0), "Trader", cargo: 2)]));
    }
}
