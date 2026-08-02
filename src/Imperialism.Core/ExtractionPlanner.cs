namespace Imperialism.Core;

internal sealed record PlannedExtraction(
    CountryId Country,
    int CollectedCellCount,
    int StrandedCellCount,
    int FishingPortCount,
    int StrandedPortCount,
    IReadOnlyList<CommodityQuantity> Collected,
    IReadOnlyList<CommodityQuantity> Stranded);

/// <summary>
/// Works out what each country's deposits hand over this turn. Yield is a
/// property of the deposit; whether it is reachable is a property of the
/// country's rail topology, so this reads post-Conflict ownership rather than
/// the state the turn opened with.
/// </summary>
internal static class ExtractionPlanner
{
    public static IReadOnlyList<PlannedExtraction> Create(WorldState state)
    {
        var definition = state.Definition;
        var map = definition.Map;
        var dimensions = map.Dimensions;
        var cellCount = dimensions.CellCount;
        var commodityCount = definition.Commodities.Count;
        var radius = definition.Extraction.CatchmentRadius;

        // One stamp array reused across countries: a cell belongs to the country
        // currently being planned when its stamp matches that country's pass.
        // Catchments may overlap between countries but never within one, which
        // is what stops a cell in range of two collection points paying twice.
        var stamp = new int[cellCount];

        // Collection points only, before the catchment widens. A port has to sit
        // on the network itself: its catch leaves by rail, so being merely
        // within reach of a railhead is not enough.
        var connectionStamp = new int[cellCount];
        var frontier = new List<int>();
        var next = new List<int>();
        var collected = new long[commodityCount];
        var stranded = new long[commodityCount];
        var results = new PlannedExtraction[definition.Countries.Count];

        for (var countryValue = 0; countryValue < results.Length; countryValue++)
        {
            var country = new CountryId(countryValue);
            var pass = countryValue + 1;
            Array.Clear(collected);
            Array.Clear(stranded);
            frontier.Clear();

            SeedCollectionPoints(state, country, pass, stamp, frontier);
            foreach (var seed in frontier)
            {
                connectionStamp[seed] = pass;
            }

            ExpandCatchment(dimensions, radius, pass, stamp, ref frontier, ref next);

            var collectedCells = 0;
            var strandedCells = 0;
            for (var cell = 0; cell < cellCount; cell++)
            {
                var cellDefinition = map.Cells[cell];
                if (cellDefinition.Resources.Count == 0 ||
                    cellDefinition.Region.Kind != CellRegionKind.Province ||
                    state.GetProvinceOwner(cellDefinition.Region.Province) != country)
                {
                    continue;
                }

                var reachable = stamp[cell] == pass;
                var target = reachable ? collected : stranded;
                if (reachable)
                {
                    collectedCells++;
                }
                else
                {
                    strandedCells++;
                }

                var level = state.GetCellDevelopment(new CellIndex(cell));
                foreach (var resource in cellDefinition.Resources)
                {
                    var deposit = map.Resources[resource.Value];

                    // A deposit nobody knows how to work yields nothing, however
                    // well connected or improved its cell is.
                    if (deposit.RequiredTechnology is { } required &&
                        !state.HasTechnology(country, required))
                    {
                        continue;
                    }

                    var yield = deposit.GetYield(level);
                    if (yield == 0)
                    {
                        continue;
                    }

                    var offset = deposit.Commodity.Value;
                    target[offset] = checked(target[offset] + yield);
                }
            }

            var fishingPorts = 0;
            var strandedPorts = 0;
            if (definition.Extraction.PortFishing is { } fishing)
            {
                foreach (var port in state.GetPorts())
                {
                    var portCell = map[port];
                    if (portCell.Region.Kind != CellRegionKind.Province ||
                        state.GetProvinceOwner(portCell.Region.Province) != country)
                    {
                        continue;
                    }

                    var water = CountAdjacentWater(map, portCell);
                    if (water == 0)
                    {
                        continue;
                    }

                    var connected = connectionStamp[port.Value] == pass;
                    var target = connected ? collected : stranded;
                    if (connected)
                    {
                        fishingPorts++;
                    }
                    else
                    {
                        strandedPorts++;
                    }

                    var offset = fishing.Commodity.Value;
                    target[offset] = checked(
                        target[offset] + checked(water * fishing.YieldPerAdjacentWaterTile));
                }
            }

            results[countryValue] = new PlannedExtraction(
                country,
                collectedCells,
                strandedCells,
                fishingPorts,
                strandedPorts,
                ToQuantities(collected),
                ToQuantities(stranded));
        }

        return Array.AsReadOnly(results);
    }

    /// <summary>
    /// Neighbouring tiles a port can fish: open sea, or land carrying a river.
    /// Each neighbour counts once even if it somehow qualifies both ways.
    /// </summary>
    private static int CountAdjacentWater(MapDefinition map, CellDefinition port)
    {
        var water = 0;
        foreach (var direction in HexDirections.All)
        {
            if (!port.Coordinate.TryGetNeighbor(direction, map.Dimensions, out var neighbor))
            {
                continue;
            }

            var cell = map[neighbor];
            if (cell.Region.Kind == CellRegionKind.SeaZone || cell.River is not null)
            {
                water++;
            }
        }

        return water;
    }

    /// <summary>
    /// Marks the cells a country can gather from directly: its capital, plus
    /// every cell sharing the capital's rail component. A rail network that no
    /// longer reaches the capital collects nothing, which is the connectivity
    /// rule doing its job rather than an omission.
    /// </summary>
    private static void SeedCollectionPoints(
        WorldState state,
        CountryId country,
        int pass,
        int[] stamp,
        List<int> frontier)
    {
        var capital = state.GetCountryCapital(country);
        if (!capital.HasValue)
        {
            return;
        }

        stamp[capital.Value.Value] = pass;
        frontier.Add(capital.Value.Value);

        var rail = state.GetRailConnectivity(country);
        var capitalComponent = rail.GetComponentId(capital.Value);
        if (!capitalComponent.HasValue)
        {
            return;
        }

        for (var cell = 0; cell < stamp.Length; cell++)
        {
            if (stamp[cell] != pass && rail.GetComponentId(new CellIndex(cell)) == capitalComponent)
            {
                stamp[cell] = pass;
                frontier.Add(cell);
            }
        }
    }

    private static void ExpandCatchment(
        MapDimensions dimensions,
        int radius,
        int pass,
        int[] stamp,
        ref List<int> frontier,
        ref List<int> next)
    {
        for (var step = 0; step < radius && frontier.Count > 0; step++)
        {
            next.Clear();
            foreach (var cell in frontier)
            {
                var coordinate = dimensions.GetCoordinate(new CellIndex(cell));
                foreach (var direction in HexDirections.All)
                {
                    if (!coordinate.TryGetNeighbor(direction, dimensions, out var neighbor))
                    {
                        continue;
                    }

                    var neighborCell = dimensions.GetIndex(neighbor).Value;
                    if (stamp[neighborCell] == pass)
                    {
                        continue;
                    }

                    stamp[neighborCell] = pass;
                    next.Add(neighborCell);
                }
            }

            (frontier, next) = (next, frontier);
        }
    }

    private static IReadOnlyList<CommodityQuantity> ToQuantities(long[] totals)
    {
        var quantities = new List<CommodityQuantity>();
        for (var commodity = 0; commodity < totals.Length; commodity++)
        {
            if (totals[commodity] > 0)
            {
                quantities.Add(new CommodityQuantity(new CommodityId(commodity), totals[commodity]));
            }
        }

        return Array.AsReadOnly(quantities.ToArray());
    }
}
