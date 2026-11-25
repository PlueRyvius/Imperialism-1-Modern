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
                foreach (var port in Harbours(state, country))
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
    /// Marks the structures a country gathers from: its capital, its connected
    /// depots, and its ports.
    /// </summary>
    /// <remarks>
    /// The capital is always both a connected depot and a connected port, so it
    /// seeds unconditionally. A port needs no railroad at all — its goods leave
    /// by water — but an inland river port must retain the recovered clear trace
    /// to a sea mouth. The undisputed-naval-control branch remains deferred until
    /// persisted fleet state and its turn ordering are recovered.
    ///
    /// <para>
    /// <b>A depot has two ways to be connected and the manual gives both.</b>
    /// The obvious one is rail to the capital. The other is rail "to a tile with
    /// a port that also contains a depot", from which "the commodities must pass
    /// through the second depot to reach the port and then travel to the capital
    /// by water" — so any rail component holding a port-and-depot hex is a
    /// gateway, whether or not that component also reaches the capital.
    /// </para>
    /// <para>
    /// Both structures are needed at the gateway and they do different jobs: the
    /// port is the sea end and the depot is the rail end, the thing that can
    /// accept goods arriving down a line. A port without one is connected for
    /// itself and a dead end for everything behind it, which is the trap the
    /// manual spells out — "the port itself is connected, but the future depots
    /// constructed along your new railroad have no way to move their commodities
    /// to the port."
    /// </para>
    ///
    /// Rail cells that are not depots gather nothing. Track alone moves goods
    /// past a tile; a structure is what lifts them off it.
    /// </remarks>
    private static void SeedCollectionPoints(
        WorldState state,
        CountryId country,
        int pass,
        int[] stamp,
        List<int> frontier)
    {
        // Everything ends at the capital, by rail or by water, so a country
        // without one has nothing for anything to connect to — ports included.
        var capital = state.GetCountryCapital(country);
        if (!capital.HasValue)
        {
            return;
        }

        var map = state.Definition.Map;
        stamp[capital.Value.Value] = pass;
        frontier.Add(capital.Value.Value);

        foreach (var port in state.GetPorts())
        {
            if (Owns(state, map, port, country) &&
                PortAccess.HasAccess(state, port, country) &&
                stamp[port.Value] != pass)
            {
                stamp[port.Value] = pass;
                frontier.Add(port.Value);
            }
        }

        var rail = state.GetRailConnectivity(country);
        var gateways = new HashSet<int>();
        if (rail.GetComponentId(capital.Value) is { } capitalComponent)
        {
            gateways.Add(capitalComponent);
        }

        // A port that also carries a depot hands the whole line it sits on a
        // route to the capital by sea.
        foreach (var port in state.GetPorts())
        {
            if (state.HasDepot(port) &&
                Owns(state, map, port, country) &&
                PortAccess.HasAccess(state, port, country) &&
                rail.GetComponentId(port) is { } seaward)
            {
                gateways.Add(seaward);
            }
        }

        if (gateways.Count == 0)
        {
            return;
        }

        foreach (var depot in state.GetDepots())
        {
            if (stamp[depot.Value] == pass ||
                !Owns(state, map, depot, country) ||
                rail.GetComponentId(depot) is not { } component ||
                !gateways.Contains(component))
            {
                continue;
            }

            stamp[depot.Value] = pass;
            frontier.Add(depot.Value);
        }
    }

    /// <summary>
    /// Every cell that fishes for a country: its ports, plus its capital, which
    /// the manual makes a connected port whether or not a record names it.
    /// </summary>
    private static IEnumerable<CellIndex> Harbours(WorldState state, CountryId country)
    {
        var ports = state.GetPorts();
        var capital = state.GetCountryCapital(country);
        return capital is { } cell && !state.HasPort(cell)
            ? ports.Append(cell)
            : ports;
    }

    private static bool Owns(WorldState state, MapDefinition map, CellIndex cell, CountryId country)
    {
        var region = map[cell].Region;
        return region.Kind == CellRegionKind.Province &&
            state.GetProvinceOwner(region.Province) == country;
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
