using Imperialism.Core;
using Xunit;

namespace Imperialism.Core.Tests;

/// <summary>
/// Warship delivery reads the map's own adjacency: across a wrapping map's seam
/// the capital touches the water on the far edge (codex-3, gate of PR #59).
/// </summary>
public sealed class ShipPlacementSeamTests
{
    [Fact]
    public void UniversityAndShipyardBothDeliverInTheSameTurn()
    {
        var owner = new CountryId(0);
        var dimensions = new MapDimensions(2, 1);
        var map = new MapDefinition(dimensions,
            [new CellDefinition(new CellIndex(0), dimensions.GetCoordinate(new CellIndex(0)), new TerrainId(0),
                CellRegion.ForProvince(new ProvinceId(0)), null, SettlementSiteKind.Urban),
             new CellDefinition(new CellIndex(1), dimensions.GetCoordinate(new CellIndex(1)), new TerrainId(0),
                CellRegion.ForSeaZone(new SeaZoneId(0)))],
            [new ProvinceDefinition(new ProvinceId(0), "Home")],
            [new SeaZoneDefinition(new SeaZoneId(0), "Home Waters")]);
        var state = new WorldState(new WorldDefinition(map,
            [new CountryDefinition(owner, "Owner")],
            new ScenarioDefinition("Combined delivery", 1815, [owner],
                initialCountryCapitals: [new CountryCapital(owner, new CellIndex(0))],
                initialWorkforce: [new InitialWorkforce(owner, 0, 0, 1)]),
            civilianTypes: [new CivilianTypeDefinition(new CivilianTypeId(0), "Miner", 3,
                purchase: new CivilianPurchase(1, [], 0))],
            shipTypes: [new ShipTypeDefinition(new ShipTypeId(0), "Frigate", seaZones: 1,
                combat: new ShipCombatStats(3, 5, 10, 900, 4))]));
        var result = TurnResolver.Resolve(state, new TurnOrders([
            new CountryTurnOrders(owner,
                civilianPurchases: [new CivilianPurchaseOrder(new CivilianTypeId(0), 1)],
                shipBuilds: [new ShipBuildOrder(new ShipTypeId(0), 1)])]), 0);
        var events = result.Events.ToList();
        var civilian = Assert.Single(events.OfType<CiviliansDeliveredEvent>());
        var ship = Assert.Single(events.OfType<ShipsBuiltEvent>());
        Assert.Equal(TurnPhase.Delivery, civilian.Phase);
        Assert.Equal(TurnPhase.Delivery, ship.Phase);
        Assert.True(events.IndexOf(civilian) < events.IndexOf(ship));
        Assert.Single(state.GetCivilians(owner));
        Assert.Equal(new SeaZoneId(0), state.GetFleet(ship.Fleet).SeaZone);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void DeliveryUsesTheMapsOwnAdjacency(bool wraps, bool boundedWater)
    {
        var owner = new CountryId(0);
        var capital = new CellIndex(0);
        var acrossSeam = new CellIndex(3);
        var dimensions = new MapDimensions(4, 1);
        var map = new MapDefinition(
            dimensions,
            Enumerable.Range(0, 4).Select(i => new CellDefinition(
                new CellIndex(i), dimensions.GetCoordinate(new CellIndex(i)), new TerrainId(0),
                i == 3 ? CellRegion.ForSeaZone(new SeaZoneId(0))
                    : i == 1 && boundedWater ? CellRegion.ForSeaZone(new SeaZoneId(1))
                    : CellRegion.ForProvince(new ProvinceId(0)),
                null, i == 0 ? SettlementSiteKind.Urban : SettlementSiteKind.None)),
            [new ProvinceDefinition(new ProvinceId(0), "Home")],
            boundedWater
                ? [new SeaZoneDefinition(new SeaZoneId(0), "West"), new SeaZoneDefinition(new SeaZoneId(1), "East")]
                : [new SeaZoneDefinition(new SeaZoneId(0), "West")],
            wrapsHorizontally: wraps);
        Assert.Equal(wraps, map.AreAdjacent(capital, acrossSeam));
        var state = new WorldState(new WorldDefinition(
            map, [new CountryDefinition(owner, "Owner")],
            new ScenarioDefinition("Seam", 1815, [owner],
                initialCountryCapitals: [new CountryCapital(owner, capital)]),
            shipTypes: [new ShipTypeDefinition(new ShipTypeId(0), "Frigate", seaZones: 1,
                combat: new ShipCombatStats(3, 5, 10, 900, 4))]));
        var result = TurnResolver.Resolve(state, new TurnOrders([
            new CountryTurnOrders(owner, shipBuilds: [new ShipBuildOrder(new ShipTypeId(0), 1)])]), 0);
        var built = Assert.Single(result.Events.OfType<ShipsBuiltEvent>());
        SeaZoneId? expected = wraps ? new SeaZoneId(0) : boundedWater ? new SeaZoneId(1) : null;
        Assert.Equal(expected, built.SeaZone);
        Assert.Equal(expected, state.GetFleet(built.Fleet).SeaZone);
    }
}
