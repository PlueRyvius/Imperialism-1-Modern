using Imperialism.Core;
using Xunit;

namespace Imperialism.Core.Tests;

public sealed class TaskForceMovementTests
{
    [Fact]
    public void SailingPlansAndResolvesOneLegAtTheSlowestHullsAllowance()
    {
        var state = CreateState(
            new MapDimensions(5, 1),
            [0, 1, 2, 3, 4],
            [new InitialShip(new CountryId(0), new ShipTypeId(0), 0, 1),
             new InitialShip(new CountryId(0), new ShipTypeId(1), 0, 1)]);
        var force = state.AssembleTaskForce(new CountryId(0), [new FleetId(1), new FleetId(2)]);
        state.PatrolTaskForce(new CountryId(0), force.Id);
        Assert.Equal(TaskForceActivity.Patrolling, force.Activity);

        var plan = state.PlanTaskForceMove(new CountryId(0), force.Id, new SeaZoneId(4));

        Assert.Equal(new SeaZoneId(0), plan.From);
        Assert.Equal(new SeaZoneId(4), plan.RequestedDestination);
        Assert.Equal(new SeaZoneId(2), plan.ResolvedDestination);
        Assert.Equal(2, plan.MaximumSeaZones);
        Assert.Equal(new SeaZoneId(0), force.SeaZone);
        Assert.Equal(new SeaZoneId(2), force.PlannedSeaZone);
        Assert.Equal(TaskForceActivity.Idle, force.Activity);

        Assert.Equal(
            [new TaskForceMoveResolution(force.Id, new SeaZoneId(0), new SeaZoneId(2))],
            state.ResolveTaskForceMoves());
        Assert.Equal(new SeaZoneId(2), force.SeaZone);
        Assert.Null(force.PlannedSeaZone);
        Assert.All(force.Fleets, id => Assert.Equal(new SeaZoneId(2), state.GetFleet(id).SeaZone));
    }

    [Fact]
    public void OnlyTheOwnerCanPutAnIdleTaskForceOnPatrol()
    {
        var state = CreateState(
            new MapDimensions(2, 1),
            [0, 1],
            [new InitialShip(new CountryId(0), new ShipTypeId(0), 0, 1)]);
        var force = state.AssembleTaskForce(new CountryId(0), [new FleetId(1)]);

        state.PatrolTaskForce(new CountryId(0), force.Id);

        Assert.Equal(TaskForceActivity.Patrolling, force.Activity);
        Assert.Throws<InvalidOperationException>(() =>
            state.PatrolTaskForce(new CountryId(1), force.Id));
    }

    [Fact]
    public void SailingUsesTheRecoveredTopologyEncounterOrderForShortestPathTies()
    {
        var state = CreateState(
            new MapDimensions(2, 2),
            [0, 2, 1, 3],
            [new InitialShip(new CountryId(0), new ShipTypeId(2), 0, 1)]);
        var force = state.AssembleTaskForce(new CountryId(0), [new FleetId(1)]);

        var plan = state.PlanTaskForceMove(new CountryId(0), force.Id, new SeaZoneId(3));

        Assert.Equal(new SeaZoneId(2), plan.ResolvedDestination);
    }

    [Fact]
    public void SailingToAnUnreachableZoneResolvesAnExplicitZeroLengthLeg()
    {
        var state = CreateState(
            new MapDimensions(3, 1),
            [0, -1, 1],
            [new InitialShip(new CountryId(0), new ShipTypeId(0), 0, 1)]);
        var force = state.AssembleTaskForce(new CountryId(0), [new FleetId(1)]);

        var plan = state.PlanTaskForceMove(new CountryId(0), force.Id, new SeaZoneId(1));

        Assert.Equal(new SeaZoneId(0), plan.ResolvedDestination);
        Assert.Equal(new SeaZoneId(0), force.PlannedSeaZone);
        Assert.Equal(
            [new TaskForceMoveResolution(force.Id, new SeaZoneId(0), new SeaZoneId(0))],
            state.ResolveTaskForceMoves());
    }

    private static WorldState CreateState(
        MapDimensions dimensions,
        IReadOnlyList<int> zones,
        IReadOnlyList<InitialShip> ships)
    {
        var maxZone = zones.Max();
        var map = new MapDefinition(
            dimensions,
            zones.Select((zone, index) => new CellDefinition(
                new CellIndex(index),
                dimensions.GetCoordinate(new CellIndex(index)),
                new TerrainId(0),
                zone >= 0 ? CellRegion.ForSeaZone(new SeaZoneId(zone)) : CellRegion.Unassigned)),
            seaZones: Enumerable.Range(0, maxZone + 1)
                .Select(id => new SeaZoneDefinition(new SeaZoneId(id), $"Zone {id}")));
        var scenario = new ScenarioDefinition("Movement", 1815, [], initialShips: ships);
        return new WorldState(new WorldDefinition(
            map,
            [new CountryDefinition(new CountryId(0), "Power 0"),
             new CountryDefinition(new CountryId(1), "Power 1")],
            scenario,
            shipTypes:
            [
                new ShipTypeDefinition(new ShipTypeId(0), "Fast", seaZones: 3),
                new ShipTypeDefinition(new ShipTypeId(1), "Slow", seaZones: 2),
                new ShipTypeDefinition(new ShipTypeId(2), "One-zone", seaZones: 1),
            ]));
    }
}
