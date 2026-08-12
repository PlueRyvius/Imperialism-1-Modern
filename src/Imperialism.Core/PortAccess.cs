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

    private static IEnumerable<SeaZoneId> AdjacentSeaZones(MapDefinition map, CellIndex port)
    {
        var coordinate = map[port].Coordinate;
        foreach (var direction in HexDirections.All.ToArray())
        {
            var neighbor = coordinate.Neighbor(direction);
            if ((uint)neighbor.Row >= (uint)map.Dimensions.Height)
            {
                continue;
            }

            var column = neighbor.Column;
            if (map.WrapsHorizontally)
            {
                column %= map.Dimensions.Width;
                if (column < 0)
                {
                    column += map.Dimensions.Width;
                }
            }
            else if ((uint)column >= (uint)map.Dimensions.Width)
            {
                continue;
            }

            var cell = map[new HexCoord(column, neighbor.Row)];
            if (cell.Region.Kind == CellRegionKind.SeaZone)
            {
                yield return cell.Region.SeaZone;
            }
        }
    }
}
