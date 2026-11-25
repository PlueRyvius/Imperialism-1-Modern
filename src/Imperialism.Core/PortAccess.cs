namespace Imperialism.Core;

/// <summary>Original strategic access check for a port's water route.</summary>
internal static class PortAccess
{
    public static bool HasAccess(WorldState state, CellIndex port, CountryId owner)
    {
        var map = state.Definition.Map;
        var seaZones = AdjacentSeaZones(map, port).Distinct().ToArray();
        if (seaZones.Length == 0)
        {
            return RiverPortConnectivity.HasSeaAccess(state, port, owner);
        }

        // UMap tests every adjacent ocean. A port retains a route when any one
        // of those oceans is not under undisputed effective-hostile control.
        foreach (var seaZone in seaZones)
        {
            var friendlyPresent = false;
            var hostilePresent = false;
            foreach (var force in state.TaskForces)
            {
                if (force.SeaZone != seaZone || force.Activity != TaskForceActivity.Patrolling)
                {
                    continue;
                }

                if (force.Country == owner)
                {
                    friendlyPresent = true;
                }
                else if (state.HasEffectiveHostility(force.Country, owner))
                {
                    hostilePresent = true;
                }
            }

            if (friendlyPresent || !hostilePresent)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The sea zones touching a cell, one per adjacent water cell (so possibly
    /// repeated). Shared with the naval war code: a capital's route is exposed only in
    /// these zones, and a blockade must lie in one of them.
    /// </summary>
    internal static IEnumerable<SeaZoneId> AdjacentSeaZones(MapDefinition map, CellIndex port)
    {
        foreach (var direction in HexDirections.All.ToArray())
        {
            if (!map.TryGetNeighbor(port, direction, out var neighbor))
            {
                continue;
            }

            var cell = map[neighbor];
            if (cell.Region.Kind == CellRegionKind.SeaZone)
            {
                yield return cell.Region.SeaZone;
            }
        }
    }
}
