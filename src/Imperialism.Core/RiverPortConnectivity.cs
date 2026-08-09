namespace Imperialism.Core;

/// <summary>
/// The original map's river-to-sea connectivity predicate, recovered from
/// <c>UMap::0x00513CA0</c> and its river walker at <c>0x00563B70</c>.
/// </summary>
/// <remarks>
/// The legacy river artwork is also its topology. The eight drawable exit
/// points map to the six hex neighbours, with upper/lower exits retained on
/// the east and west edges. This pairing reproduces every non-terminal river
/// join in the shipped corpus. The original walker has a 100-cell guard; an
/// incomplete or ambiguous modern map therefore has no invented sea route.
/// </remarks>
internal static class RiverPortConnectivity
{
    private const int MaximumTraceCells = 100;

    /// <summary>
    /// Whether a port can currently reach the sea. Coastal ports remain
    /// available in this slice; their strategic-naval control branch awaits
    /// the recovered persisted-fleet loader. An inland port must have a clear
    /// river trace to a mouth.
    /// </summary>
    public static bool HasSeaAccess(WorldState state, CellIndex port, CountryId owner)
    {
        var map = state.Definition.Map;
        if (HasAdjacentSea(map, port))
        {
            return true;
        }

        // Imported corpus ports always have sea or a river. Preserve the legacy
        // static behaviour for deliberately incomplete synthetic maps rather
        // than silently classifying their unmodelled water feature as blocked.
        return map[port].River is null || HasClearRiverRoute(state, map, port, owner);
    }

    private static bool HasAdjacentSea(MapDefinition map, CellIndex port)
    {
        foreach (var direction in HexDirections.All)
        {
            if (TryGetWrappedNeighbor(map, port, direction, out var neighbor) &&
                map[neighbor].Region.Kind == CellRegionKind.SeaZone)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasClearRiverRoute(
        WorldState state,
        MapDefinition map,
        CellIndex port,
        CountryId owner)
    {
        if (map[port].River is null)
        {
            return false;
        }

        var previous = new int[map.Dimensions.CellCount];
        Array.Fill(previous, -2);
        previous[port.Value] = -1;

        var frontier = new Queue<CellIndex>();
        frontier.Enqueue(port);
        CellIndex? mouth = null;
        var visited = 0;

        while (frontier.Count > 0)
        {
            var current = frontier.Dequeue();
            if (++visited > MaximumTraceCells)
            {
                return false;
            }

            if (current != port && HasEndpoint(map[current].River, RiverEndpoint.Mouth))
            {
                if (mouth.HasValue)
                {
                    // The original tables select one deterministic continuation.
                    // A modern map with two mouths cannot be made equivalent by
                    // choosing one arbitrarily.
                    return false;
                }

                mouth = current;
                continue;
            }

            foreach (var neighbor in GetRiverNeighbors(map, current))
            {
                if (previous[neighbor.Value] != -2)
                {
                    continue;
                }

                previous[neighbor.Value] = current.Value;
                frontier.Enqueue(neighbor);
            }
        }

        if (!mouth.HasValue)
        {
            return false;
        }

        // The mouth is an ocean tile in the original data. Only land cells on
        // the reconstructed downstream path participate in the ownership test.
        for (var cell = previous[mouth.Value.Value]; cell >= 0; cell = previous[cell])
        {
            var definition = map[new CellIndex(cell)];
            if (definition.Region.Kind != CellRegionKind.Province ||
                state.GetProvinceOwner(definition.Region.Province) != owner)
            {
                return false;
            }
        }

        return true;
    }

    private static IEnumerable<CellIndex> GetRiverNeighbors(MapDefinition map, CellIndex cell)
    {
        var path = map[cell].River;
        if (path is null)
        {
            yield break;
        }

        foreach (var endpoint in Endpoints(path.Value))
        {
            if (!TryGetDirection(endpoint, out var direction) ||
                !TryGetWrappedNeighbor(map, cell, direction, out var neighbor))
            {
                continue;
            }

            var neighborPath = map[neighbor].River;
            if (neighborPath is not null &&
                HasEndpoint(neighborPath, Opposite(endpoint)))
            {
                yield return neighbor;
            }
        }
    }

    private static bool TryGetWrappedNeighbor(
        MapDefinition map,
        CellIndex cell,
        HexDirection direction,
        out CellIndex neighbor)
    {
        var coordinate = map[cell].Coordinate.Neighbor(direction);
        var dimensions = map.Dimensions;
        if ((uint)coordinate.Row >= (uint)dimensions.Height)
        {
            neighbor = default;
            return false;
        }

        var column = coordinate.Column % dimensions.Width;
        if (column < 0)
        {
            column += dimensions.Width;
        }

        neighbor = dimensions.GetIndex(new HexCoord(column, coordinate.Row));
        return true;
    }

    private static IReadOnlyList<RiverEndpoint> Endpoints(RiverPath path) =>
        [path.First, path.Second];

    private static bool HasEndpoint(RiverPath? path, RiverEndpoint endpoint) =>
        path is { } value && (value.First == endpoint || value.Second == endpoint);

    private static bool TryGetDirection(RiverEndpoint endpoint, out HexDirection direction)
    {
        direction = endpoint switch
        {
            RiverEndpoint.NorthEast => HexDirection.NorthEast,
            RiverEndpoint.EastUpper or RiverEndpoint.EastLower => HexDirection.East,
            RiverEndpoint.SouthEast => HexDirection.SouthEast,
            RiverEndpoint.SouthWest => HexDirection.SouthWest,
            RiverEndpoint.WestUpper or RiverEndpoint.WestLower => HexDirection.West,
            RiverEndpoint.NorthWest => HexDirection.NorthWest,
            _ => default,
        };
        return endpoint is not RiverEndpoint.Source and not RiverEndpoint.Mouth;
    }

    private static RiverEndpoint Opposite(RiverEndpoint endpoint) => endpoint switch
    {
        RiverEndpoint.NorthEast => RiverEndpoint.SouthWest,
        RiverEndpoint.EastUpper => RiverEndpoint.WestUpper,
        RiverEndpoint.EastLower => RiverEndpoint.WestLower,
        RiverEndpoint.SouthEast => RiverEndpoint.NorthWest,
        RiverEndpoint.SouthWest => RiverEndpoint.NorthEast,
        RiverEndpoint.WestUpper => RiverEndpoint.EastUpper,
        RiverEndpoint.WestLower => RiverEndpoint.EastLower,
        RiverEndpoint.NorthWest => RiverEndpoint.SouthEast,
        _ => throw new ArgumentOutOfRangeException(nameof(endpoint), endpoint, "River terminus has no opposite edge."),
    };
}
