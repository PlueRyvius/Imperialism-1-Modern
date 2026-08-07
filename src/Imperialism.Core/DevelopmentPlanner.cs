namespace Imperialism.Core;

/// <summary>One thing that happened to a civilian during the Development phase.</summary>
internal abstract record PlannedCivilianOutcome(CountryId Country, CivilianUnitId Unit);

internal sealed record PlannedCivilianDeployment(
    CountryId Country,
    CivilianUnitId Unit,
    CellIndex From,
    CellIndex To) : PlannedCivilianOutcome(Country, Unit);

internal sealed record PlannedCivilianWorkStart(
    CountryId Country,
    CivilianUnitId Unit,
    CellIndex Cell,
    int TurnsRequired) : PlannedCivilianOutcome(Country, Unit);

internal sealed record PlannedCellDevelopment(
    CountryId Country,
    CivilianUnitId Unit,
    CellIndex Cell,
    int FromLevel,
    int ToLevel) : PlannedCivilianOutcome(Country, Unit);

internal sealed record PlannedCivilianRefusal(
    CountryId Country,
    CivilianUnitId Unit,
    CellIndex Cell,
    CivilianOrderRefusal Reason) : PlannedCivilianOutcome(Country, Unit);

/// <summary>
/// Moves civilians and raises the tiles they have finished working. This is the
/// only thing in the engine that creates development; before it, a cell's level
/// was whatever the scenario authored and nothing could change it.
/// </summary>
/// <remarks>
/// Like <see cref="FeedingPlanner"/> and unlike <see cref="ExpansionPlanner"/>
/// this resolves against the state as it goes, because improvement costs
/// nothing: the civilian was paid for when it was built, and the manual prices
/// no materials for the work itself. With nothing to draw from the warehouse
/// there is no shared pool to book against the other phases first.
/// <para>
/// Work already under way is advanced <em>before</em> new orders are read, and
/// the set of busy civilians is taken before that, so a civilian finishing this
/// turn cannot also accept an order written while it was still busy.
/// </para>
/// </remarks>
internal static class DevelopmentPlanner
{
    public static IReadOnlyList<PlannedCivilianOutcome> Resolve(WorldState state, TurnOrders orders)
    {
        var outcomes = new List<PlannedCivilianOutcome>();
        var busyAtStart = state.GetCivilians()
            .Where(static civilian => civilian.IsBusy)
            .Select(static civilian => civilian.Id)
            .ToHashSet();

        AdvanceWorkInProgress(state, outcomes);

        for (var countryValue = 0; countryValue < orders.Count; countryValue++)
        {
            var country = new CountryId(countryValue);
            var countryOrders = orders[country];

            foreach (var order in countryOrders.Deployments)
            {
                if (Refuse(state, country, order.Unit, order.Cell, busyAtStart, work: false) is { } refusal)
                {
                    outcomes.Add(new PlannedCivilianRefusal(country, order.Unit, order.Cell, refusal));
                    continue;
                }

                var from = state.GetCivilian(order.Unit)!.Cell;
                state.MoveCivilian(order.Unit, order.Cell);
                outcomes.Add(new PlannedCivilianDeployment(country, order.Unit, from, order.Cell));
            }

            foreach (var order in countryOrders.CivilianWork)
            {
                if (Refuse(state, country, order.Unit, order.Cell, busyAtStart, work: true) is { } refusal)
                {
                    outcomes.Add(new PlannedCivilianRefusal(country, order.Unit, order.Cell, refusal));
                    continue;
                }

                var civilian = state.GetCivilian(order.Unit)!;
                var turns = state.Definition.CivilianTypes[civilian.Type.Value].WorkTurns;

                // The move and the job are one command: the original's hammer
                // cursor sends the worker and sets it going in a single click.
                state.MoveCivilian(order.Unit, order.Cell);
                state.SetCivilianWork(order.Unit, new CivilianWorkInProgress(order.Cell, turns));
                outcomes.Add(new PlannedCivilianWorkStart(country, order.Unit, order.Cell, turns));
            }
        }

        return outcomes;
    }

    private static void AdvanceWorkInProgress(WorldState state, List<PlannedCivilianOutcome> outcomes)
    {
        foreach (var civilian in state.GetCivilians())
        {
            if (civilian.Work is not { } job)
            {
                continue;
            }

            if (job.TurnsRemaining > 1)
            {
                state.SetCivilianWork(
                    civilian.Id,
                    new CivilianWorkInProgress(job.Cell, job.TurnsRemaining - 1));
                continue;
            }

            state.SetCivilianWork(civilian.Id, null);

            // The tile may have changed hands, or been improved by somebody
            // else, since the work began. Finishing a job that is no longer
            // legal frees the worker and raises nothing.
            if (LegalityOfWork(state, civilian.Country, civilian.Type, job.Cell) is { } refusal)
            {
                outcomes.Add(new PlannedCivilianRefusal(
                    civilian.Country, civilian.Id, job.Cell, refusal));
                continue;
            }

            var from = state.GetCellDevelopment(job.Cell);
            state.SetCellDevelopment(job.Cell, from + 1);
            outcomes.Add(new PlannedCellDevelopment(
                civilian.Country, civilian.Id, job.Cell, from, from + 1));
        }
    }

    private static CivilianOrderRefusal? Refuse(
        WorldState state,
        CountryId country,
        CivilianUnitId unit,
        CellIndex cell,
        HashSet<CivilianUnitId> busyAtStart,
        bool work)
    {
        if (state.GetCivilian(unit) is not { } civilian)
        {
            return CivilianOrderRefusal.NoSuchCivilian;
        }

        if (civilian.Country != country)
        {
            return CivilianOrderRefusal.NotYours;
        }

        if (busyAtStart.Contains(unit))
        {
            return CivilianOrderRefusal.AlreadyWorking;
        }

        return work
            ? LegalityOfWork(state, country, civilian.Type, cell)
            : LegalityOfEntry(state, country, cell);
    }

    /// <summary>
    /// Where a civilian may stand. The manual bars them from another Great
    /// Power's territory, and from a Minor Nation's without an embassy. Nothing
    /// here models diplomacy or knows a minor nation from a great one, so the
    /// rule is narrowed to a country's own land — which is always allowed, and
    /// can only under-permit.
    /// </summary>
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

    /// <summary>
    /// Whether this kind of civilian can raise this tile. Three separate things
    /// must all hold, and they come from three different places: the ground must
    /// admit improvement at all (the manual's Terrain Tiles Table), something on
    /// it must be this civilian's work (the Resource Development Table), and
    /// that deposit's yield curve must have a rung left.
    /// </summary>
    private static CivilianOrderRefusal? LegalityOfWork(
        WorldState state,
        CountryId country,
        CivilianTypeId type,
        CellIndex cell)
    {
        if (LegalityOfEntry(state, country, cell) is { } entry)
        {
            return entry;
        }

        var map = state.Definition.Map;
        if (map.GetTerrain(map[cell].Terrain) is not { IsImprovable: true })
        {
            return CivilianOrderRefusal.TerrainCannotBeImproved;
        }

        var level = state.GetCellDevelopment(cell);
        var worked = false;
        foreach (var resourceId in map[cell].Resources)
        {
            var resource = map.Resources[resourceId.Value];
            if (resource.ImprovedBy != type)
            {
                continue;
            }

            worked = true;

            // A cell holding two deposits has one level, so one deposit still
            // short of the top of its curve is reason enough to keep working.
            if (level < resource.MaxDevelopmentLevel)
            {
                return null;
            }
        }

        return worked
            ? CivilianOrderRefusal.AlreadyFullyDeveloped
            : CivilianOrderRefusal.NoDepositThisCivilianWorks;
    }
}
