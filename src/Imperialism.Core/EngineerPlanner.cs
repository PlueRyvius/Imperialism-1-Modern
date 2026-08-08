namespace Imperialism.Core;

internal sealed record PlannedConstruction(
    CountryId Country,
    CivilianUnitId Unit,
    CellIndex Cell,
    EngineerConstruction Structure,
    CellIndex Target) : PlannedCivilianOutcome(Country, Unit);

/// <summary>
/// The Engineer's half of <see cref="TurnPhase.Development"/>: what it may
/// build, what that costs, and what finishing one does to the map.
/// </summary>
/// <remarks>
/// This is a helper for <see cref="DevelopmentPlanner"/> rather than a phase of
/// its own. An Engineer is a civilian taking a turn to do a job, so it shares
/// the timer, the busy-set and the refusal reporting with every other civilian
/// and differs only in what starting and finishing mean.
/// <para>
/// <b>Cash is spent when the work is ordered, not when it finishes.</b> The
/// manual frames it that way — a player might tell a civilian to do nothing
/// "when you lack the cash to pay for the civilian's improvements" — and it is
/// also the only ordering that makes a refusal useful: being told on the turn
/// you ordered it beats discovering it a turn later. Nothing is refunded if the
/// tile changes hands before the work completes, which is the same bargain
/// <see cref="DevelopmentPlanner"/> already makes with a civilian's time.
/// </para>
/// <para>
/// There is no pooling against the other phases. Construction is paid in cash
/// and every other cost in this engine is paid in commodities, so there is
/// nothing to book against; two Engineers of one country spending the same
/// treasury are resolved by reading the orders in turn, and the second is
/// refused if the first emptied it.
/// </para>
/// </remarks>
internal static class EngineerPlanner
{
    /// <summary>
    /// Whether this Engineer may start this job, and what it would cost. Every
    /// refusal here is a reason the original would not have shown the cursor at
    /// all, bar the last two, which it shows and then declines.
    /// </summary>
    public static CivilianOrderRefusal? Legality(
        WorldState state,
        CountryId country,
        CivilianUnit engineer,
        EngineerOrder order,
        out long cost)
    {
        cost = 0;
        var definition = state.Definition;
        if (definition.Construction is not { } settings)
        {
            return CivilianOrderRefusal.NothingCanBeBuiltInThisWorld;
        }

        if (definition.CivilianTypes[engineer.Type.Value].Work != CivilianWorkKind.Construct)
        {
            return CivilianOrderRefusal.NotAnEngineer;
        }

        // The target must be somewhere this country could work at all, which is
        // the same entry rule every civilian obeys. Rail is checked at both
        // ends, because a line has two.
        if (LegalityOfEntry(state, country, engineer.Cell) is { } here)
        {
            return here;
        }

        if (LegalityOfEntry(state, country, order.Cell) is { } there)
        {
            return there;
        }

        // Which cursor was used is decided by which tile was clicked, so an
        // order whose structure disagrees with its target is not an order the
        // original could have produced.
        var adjacent = IsAdjacent(definition.Map, engineer.Cell, order.Cell);
        if (order.Structure == EngineerConstruction.Rail)
        {
            if (order.Cell == engineer.Cell || !adjacent)
            {
                return CivilianOrderRefusal.RailNeedsAnAdjacentTile;
            }
        }
        else if (order.Cell != engineer.Cell)
        {
            return CivilianOrderRefusal.StructureNeedsTheEngineersOwnTile;
        }

        var refusal = order.Structure switch
        {
            EngineerConstruction.Rail => LegalityOfRail(state, country, engineer.Cell, order.Cell),
            EngineerConstruction.Depot => LegalityOfDepot(state, country, order.Cell),
            EngineerConstruction.Port => LegalityOfPort(state, country, order.Cell),
            _ => throw new ArgumentOutOfRangeException(nameof(order)),
        };

        if (refusal is not null)
        {
            return refusal;
        }

        cost = order.Structure == EngineerConstruction.Rail
            ? PriceOfRail(state, engineer.Cell, order.Cell)
            : settings.GetCashCost(order.Structure);
        return cost > 0 && state.GetCash(country) < cost
            ? CivilianOrderRefusal.NotEnoughCash
            : null;
    }

    /// <summary>
    /// What one link costs: <b>the dearer of its two ends.</b>
    /// </summary>
    /// <remarks>
    /// The price list gives one figure per ground for a link and a link crosses
    /// two, so something has to choose, and <b>this is a chosen rule.</b> Two
    /// alternatives were rejected for reasons worth keeping. Summing the ends
    /// would double every attested figure — a plains-to-plains link would cost
    /// 200 where the list says 100 — which contradicts the source outright.
    /// Charging the *target* end reads the manual's "build rail into certain
    /// terrain" literally and is asymmetric: a player would lay every swamp line
    /// from the swamp side to pay the plains price, which is a rule that rewards
    /// nothing but knowing about it.
    /// <para>
    /// The dearer end is direction-independent and agrees exactly with the list
    /// wherever both ends are the same ground, which is the case the list can
    /// actually be describing. The gate has already been checked, so both
    /// terrains are known to carry rail; a world that names no price builds free.
    /// </para>
    /// </remarks>
    private static long PriceOfRail(WorldState state, CellIndex from, CellIndex to)
    {
        var map = state.Definition.Map;
        var here = map.GetTerrain(map[from].Terrain)?.Rail?.CashCost ?? 0;
        var there = map.GetTerrain(map[to].Terrain)?.Rail?.CashCost ?? 0;
        return Math.Max(here, there);
    }

    /// <summary>
    /// Puts the finished structure on the map. Rail invalidates the cached
    /// connectivity index on its own, so a depot built at the end of a new line
    /// starts gathering with nothing here having to say so.
    /// </summary>
    public static void Complete(WorldState state, CellIndex cell, EngineerJob job)
    {
        switch (job.Kind)
        {
            case EngineerConstruction.Rail:
                _ = state.BuildRail(new CellLink(cell, job.Target));
                break;
            case EngineerConstruction.Depot:
                _ = state.BuildDepot(job.Target);
                break;
            case EngineerConstruction.Port:
                _ = state.BuildPort(job.Target);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(job));
        }
    }

    /// <summary>
    /// Whether a job that was legal when it was ordered is still legal now. Both
    /// ends of a rail line, or the structure's tile, may have changed hands
    /// while the Engineer worked.
    /// </summary>
    public static CivilianOrderRefusal? LegalityOfFinishing(
        WorldState state,
        CountryId country,
        CellIndex cell,
        EngineerJob job) => job.Kind switch
        {
            EngineerConstruction.Rail => LegalityOfEntry(state, country, cell) ??
                LegalityOfEntry(state, country, job.Target) ??
                LegalityOfRail(state, country, cell, job.Target),
            EngineerConstruction.Depot => LegalityOfEntry(state, country, job.Target) ??
                LegalityOfDepot(state, country, job.Target),
            EngineerConstruction.Port => LegalityOfEntry(state, country, job.Target) ??
                LegalityOfPort(state, country, job.Target),
            _ => throw new ArgumentOutOfRangeException(nameof(job)),
        };

    /// <summary>
    /// A line needs railable ground at both ends, because it crosses both. The
    /// gate is per terrain and per technology: "you do not always have the
    /// technology necessary to build rail into certain terrain."
    /// </summary>
    private static CivilianOrderRefusal? LegalityOfRail(
        WorldState state,
        CountryId country,
        CellIndex from,
        CellIndex to)
    {
        if (state.HasRail(new CellLink(from, to)))
        {
            return CivilianOrderRefusal.RailAlreadyBuilt;
        }

        return RailGate(state, country, from) ?? RailGate(state, country, to);
    }

    /// <summary>
    /// Depots reuse the rail gate. <b>An inference</b>: "more advanced
    /// construction technology increases the number of types terrain where rails
    /// may be laid and depots may be built", and no separate depot terrain table
    /// is given anywhere.
    /// </summary>
    private static CivilianOrderRefusal? LegalityOfDepot(
        WorldState state,
        CountryId country,
        CellIndex cell) => state.HasDepot(cell)
            ? CivilianOrderRefusal.DepotAlreadyBuilt
            : RailGate(state, country, cell);

    /// <summary>
    /// "Ports may be built only on coasts and tiles containing a river." The
    /// terrain gate does not apply: a port is not a railhead, and the manual
    /// gates it on water instead.
    /// </summary>
    private static CivilianOrderRefusal? LegalityOfPort(
        WorldState state,
        CountryId country,
        CellIndex cell)
    {
        if (state.HasPort(cell))
        {
            return CivilianOrderRefusal.PortAlreadyBuilt;
        }

        var map = state.Definition.Map;
        if (map[cell].River is not null)
        {
            return null;
        }

        var coordinate = map.Dimensions.GetCoordinate(cell);
        foreach (var direction in HexDirections.All)
        {
            if (!coordinate.TryGetNeighbor(direction, map.Dimensions, out var neighbor))
            {
                continue;
            }

            if (map[map.Dimensions.GetIndex(neighbor)].Region.Kind == CellRegionKind.SeaZone)
            {
                return null;
            }
        }

        return CivilianOrderRefusal.PortNeedsWater;
    }

    private static CivilianOrderRefusal? RailGate(
        WorldState state,
        CountryId country,
        CellIndex cell)
    {
        var map = state.Definition.Map;
        if (map.GetTerrain(map[cell].Terrain)?.Rail is not { } rule)
        {
            return CivilianOrderRefusal.TerrainCannotCarryRail;
        }

        return rule.RequiredTechnology is { } required && !state.HasTechnology(country, required)
            ? CivilianOrderRefusal.ConstructionTechnologyNotKnown
            : null;
    }

    private static bool IsAdjacent(MapDefinition map, CellIndex from, CellIndex to)
    {
        var coordinate = map.Dimensions.GetCoordinate(from);
        foreach (var direction in HexDirections.All)
        {
            if (coordinate.TryGetNeighbor(direction, map.Dimensions, out var neighbor) &&
                map.Dimensions.GetIndex(neighbor) == to)
            {
                return true;
            }
        }

        return false;
    }

    private static CivilianOrderRefusal? LegalityOfEntry(
        WorldState state,
        CountryId country,
        CellIndex cell)
    {
        var map = state.Definition.Map;
        if (!map.Dimensions.Contains(cell))
        {
            return CivilianOrderRefusal.TargetOffMap;
        }

        var region = map[cell].Region;
        if (region.Kind != CellRegionKind.Province)
        {
            return CivilianOrderRefusal.TargetNotLand;
        }

        return state.GetProvinceOwner(region.Province) == country
            ? null
            : CivilianOrderRefusal.TargetNotYourTerritory;
    }
}
