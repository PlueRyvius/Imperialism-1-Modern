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

    [Fact]
    public void TaskForceAssemblyRequiresOwnedCoLocatedUnattachedFleets()
    {
        var state = CreateState(
        [
            new InitialShip(new CountryId(0), new ShipTypeId(0), 1, 3),
            new InitialShip(new CountryId(0), new ShipTypeId(0), 1, 2),
            new InitialShip(new CountryId(0), new ShipTypeId(0), 0, 1),
            new InitialShip(new CountryId(0), new ShipTypeId(0), 7, 1),
            new InitialShip(new CountryId(1), new ShipTypeId(0), 1, 1),
        ]);

        var taskForce = state.AssembleTaskForce(new CountryId(0), [new FleetId(2), new FleetId(1)]);

        Assert.Equal(new TaskForceId(1), taskForce.Id);
        Assert.Equal(new SeaZoneId(1), taskForce.SeaZone);
        Assert.Equal([new FleetId(1), new FleetId(2)], taskForce.Fleets);
        Assert.Equal(taskForce.Id, state.GetFleet(new FleetId(1)).TaskForce);
        Assert.Equal(taskForce.Id, state.GetFleet(new FleetId(2)).TaskForce);
        Assert.Throws<InvalidOperationException>(() =>
            state.AssembleTaskForce(new CountryId(0), [new FleetId(1)]));
        Assert.Throws<InvalidOperationException>(() =>
            state.AssembleTaskForce(new CountryId(0), [new FleetId(2), new FleetId(3)]));
        Assert.Throws<InvalidOperationException>(() =>
            state.AssembleTaskForce(new CountryId(0), [new FleetId(4)]));
        Assert.Throws<ArgumentException>(() =>
            state.AssembleTaskForce(new CountryId(0), [new FleetId(3), new FleetId(3)]));
        Assert.Throws<InvalidOperationException>(() =>
            state.AssembleTaskForce(new CountryId(0), [new FleetId(5)]));
    }

    private static WorldState CreateState(IEnumerable<InitialShip> ships)
    {
        var initialShips = ships.ToArray();
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
            initialShips: initialShips,
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
            Enumerable.Range(0, Math.Max(1, initialShips.Select(static ship => ship.Country.Value).DefaultIfEmpty(-1).Max() + 1))
                .Select(static id => new CountryDefinition(new CountryId(id), $"Power {id}")),
            scenario,
            startingDefaults: defaults,
            shipTypes: [new ShipTypeDefinition(new ShipTypeId(0), "Trader", cargo: 2)]));
    }
}
