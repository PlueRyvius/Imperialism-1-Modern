namespace Imperialism.Core;

/// <summary>
/// The static movement graph between base sea zones.
/// </summary>
/// <remarks>
/// The original builds this graph by walking the map's six hex neighbours rather
/// than reading a scenario table. Port-zone nodes have a separate lifecycle and
/// are deliberately not represented here until that lifecycle is recovered.
/// </remarks>
public sealed class SeaZoneTopology
{
    private readonly IReadOnlyList<SeaZoneId>[] _neighbors;

    private SeaZoneTopology(IReadOnlyList<SeaZoneId>[] neighbors) => _neighbors = neighbors;

    /// <summary>Gets the deterministically ordered zones directly reachable from a zone.</summary>
    public IReadOnlyList<SeaZoneId> GetNeighbors(SeaZoneId seaZone) =>
        (uint)seaZone.Value < (uint)_neighbors.Length
            ? _neighbors[seaZone.Value]
            : throw new ArgumentOutOfRangeException(nameof(seaZone));

    internal static SeaZoneTopology FromMap(
        MapDimensions dimensions,
        IReadOnlyList<CellDefinition> cells,
        int seaZoneCount,
        bool wrapsHorizontally)
    {
        // UOcean setup in the original scans cells in record order and probes
        // the six directions in HexDirections.All order.  Its adjacency array
        // appends a reciprocal pair at the first encounter; preserving that
        // order matters where two shortest paths are otherwise tied.
        var links = Enumerable.Range(0, seaZoneCount)
            .Select(static _ => new List<SeaZoneId>())
            .ToArray();

        foreach (var cell in cells)
        {
            if (cell.Region.Kind != CellRegionKind.SeaZone)
            {
                continue;
            }

            var from = cell.Region.SeaZone;
            foreach (var direction in HexDirections.All)
            {
                if (!TryGetNeighbor(cell.Coordinate, direction, dimensions, wrapsHorizontally, out var neighbor))
                {
                    continue;
                }

                var adjacent = cells[dimensions.GetIndex(neighbor).Value];
                if (adjacent.Region.Kind != CellRegionKind.SeaZone)
                {
                    continue;
                }

                var to = adjacent.Region.SeaZone;
                if (from == to)
                {
                    continue;
                }

                if (links[from.Value].Contains(to))
                {
                    continue;
                }

                links[from.Value].Add(to);
                links[to.Value].Add(from);
            }
        }

        return new SeaZoneTopology(links
            .Select(static link => (IReadOnlyList<SeaZoneId>)link.ToArray())
            .ToArray());
    }

    private static bool TryGetNeighbor(
        HexCoord coordinate,
        HexDirection direction,
        MapDimensions dimensions,
        bool wrapsHorizontally,
        out HexCoord neighbor)
    {
        neighbor = coordinate.Neighbor(direction);
        if ((uint)neighbor.Row >= (uint)dimensions.Height)
        {
            return false;
        }

        if ((uint)neighbor.Column < (uint)dimensions.Width)
        {
            return true;
        }

        if (!wrapsHorizontally)
        {
            return false;
        }

        neighbor = new HexCoord(
            neighbor.Column < 0 ? dimensions.Width - 1 : 0,
            neighbor.Row);
        return true;
    }
}
