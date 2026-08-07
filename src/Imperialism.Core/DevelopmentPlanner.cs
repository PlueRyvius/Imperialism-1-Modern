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

internal sealed record PlannedConstructionStart(
    CountryId Country,
    CivilianUnitId Unit,
    CellIndex Cell,
    EngineerConstruction Structure,
    CellIndex Target,
    int TurnsRequired,
    long Paid) : PlannedCivilianOutcome(Country, Unit);

internal sealed record PlannedCellDevelopment(
    CountryId Country,
    CivilianUnitId Unit,
    CellIndex Cell,
    int FromLevel,
    int ToLevel) : PlannedCivilianOutcome(Country, Unit);

internal sealed record PlannedCellProspected(
    CountryId Country,
    CivilianUnitId Unit,
    CellIndex Cell,
    IReadOnlyList<ResourceId> Revealed) : PlannedCivilianOutcome(Country, Unit);

internal sealed record PlannedCivilianRefusal(
    CountryId Country,
    CivilianUnitId Unit,
    CellIndex Cell,
    CivilianOrderRefusal Reason) : PlannedCivilianOutcome(Country, Unit);

/// <summary>
/// Moves civilians, raises the tiles they have finished working, and records the
/// ground their Prospectors have searched. This is the only thing in the engine
/// that creates development; before it, a cell's level was whatever the scenario
/// authored and nothing could change it.
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
/// <para>
/// Prospecting shares all of that — the timer, the movement, the refusal
/// reporting — and differs only in what finishing does. Which of the two a
/// civilian performs is a property of its type, so a work order needs no
/// discriminator of its own.
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

            foreach (var order in countryOrders.EngineerWork)
            {
                if (RefuseConstruction(state, country, order, busyAtStart, out var cost) is { } refusal)
                {
                    outcomes.Add(new PlannedCivilianRefusal(country, order.Unit, order.Cell, refusal));
                    continue;
                }

                var engineer = state.GetCivilian(order.Unit)!;
                var turns = state.Definition.CivilianTypes[engineer.Type.Value].WorkTurns;
                if (cost > 0 && !state.TrySpendCash(country, cost))
                {
                    // Unreachable while Legality has already checked the
                    // treasury, and kept because the check and the spend are two
                    // statements and a half-built structure is not a structure.
                    outcomes.Add(new PlannedCivilianRefusal(
                        country, order.Unit, order.Cell, CivilianOrderRefusal.NotEnoughCash));
                    continue;
                }

                // No move: the original builds rail *from* where the Engineer
                // stands, so moving it first would silently change which tile
                // the line starts at.
                state.SetCivilianWork(
                    order.Unit,
                    new CivilianWorkInProgress(
                        engineer.Cell,
                        turns,
                        new EngineerJob(order.Structure, order.Cell)));
                outcomes.Add(new PlannedConstructionStart(
                    country, order.Unit, engineer.Cell, order.Structure, order.Cell, turns, cost));
            }
        }

        return outcomes;
    }

    private static CivilianOrderRefusal? RefuseConstruction(
        WorldState state,
        CountryId country,
        EngineerOrder order,
        HashSet<CivilianUnitId> busyAtStart,
        out long cost)
    {
        cost = 0;
        if (state.GetCivilian(order.Unit) is not { } engineer)
        {
            return CivilianOrderRefusal.NoSuchCivilian;
        }

        if (engineer.Country != country)
        {
            return CivilianOrderRefusal.NotYours;
        }

        if (busyAtStart.Contains(order.Unit))
        {
            return CivilianOrderRefusal.AlreadyWorking;
        }

        return EngineerPlanner.Legality(state, country, engineer, order, out cost);
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
                    new CivilianWorkInProgress(job.Cell, job.TurnsRemaining - 1, job.Construction));
                continue;
            }

            state.SetCivilianWork(civilian.Id, null);

            if (job.Construction is { } construction)
            {
                // Either end of a line, or the structure's own tile, may have
                // changed hands while the Engineer worked. The cash was spent
                // when the order was given and is not refunded, which is the
                // same bargain a civilian's time already makes.
                if (EngineerPlanner.LegalityOfFinishing(
                        state, civilian.Country, job.Cell, construction) is { } blocked)
                {
                    outcomes.Add(new PlannedCivilianRefusal(
                        civilian.Country, civilian.Id, construction.Target, blocked));
                    continue;
                }

                EngineerPlanner.Complete(state, job.Cell, construction);
                outcomes.Add(new PlannedConstruction(
                    civilian.Country,
                    civilian.Id,
                    job.Cell,
                    construction.Kind,
                    construction.Target));
                continue;
            }

            // The tile may have changed hands, or been improved by somebody
            // else, since the work began. Finishing a job that is no longer
            // legal frees the worker and raises nothing.
            if (LegalityOfWork(state, civilian.Country, civilian.Type, job.Cell) is { } refusal)
            {
                outcomes.Add(new PlannedCivilianRefusal(
                    civilian.Country, civilian.Id, job.Cell, refusal));
                continue;
            }

            if (WorkOf(state, civilian.Type) == CivilianWorkKind.Prospect)
            {
                state.SetProspected(civilian.Country, job.Cell);
                outcomes.Add(new PlannedCellProspected(
                    civilian.Country, civilian.Id, job.Cell, HiddenDeposits(state, job.Cell)));
                continue;
            }

            var from = state.GetCellDevelopment(job.Cell);
            state.SetCellDevelopment(job.Cell, from + 1);
            outcomes.Add(new PlannedCellDevelopment(
                civilian.Country, civilian.Id, job.Cell, from, from + 1));
        }
    }

    private static CivilianWorkKind WorkOf(WorldState state, CivilianTypeId type) =>
        state.Definition.CivilianTypes[type.Value].Work;

    /// <summary>
    /// Deposits on a cell that a country cannot see until it has searched. The
    /// list is what the search reveals, and it is empty on most of the ground a
    /// Prospector is sent to.
    /// </summary>
    private static IReadOnlyList<ResourceId> HiddenDeposits(WorldState state, CellIndex cell)
    {
        var map = state.Definition.Map;
        List<ResourceId>? revealed = null;
        foreach (var resourceId in map[cell].Resources)
        {
            if (map.Resources[resourceId.Value].RequiresDiscovery)
            {
                (revealed ??= []).Add(resourceId);
            }
        }

        return revealed is null ? [] : Array.AsReadOnly(revealed.ToArray());
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
    /// Whether this civilian can do its work here. Which work that is comes from
    /// the civilian's type, so this is the fork between improving a tile and
    /// searching one.
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

        return WorkOf(state, type) switch
        {
            // An Engineer builds and never improves, so a plain work order to
            // one is the wrong cursor rather than the wrong tile. Reported as
            // such, because "send an EngineerOrder instead" is the fix and
            // "nothing here is your work" would not say so.
            CivilianWorkKind.Construct => CivilianOrderRefusal.NotAnEngineer,
            CivilianWorkKind.Prospect => LegalityOfProspecting(state, country, cell),
            _ => LegalityOfImprovement(state, country, type, cell),
        };
    }

    /// <summary>
    /// Whether this kind of civilian can raise this tile. Five separate things
    /// must all hold, and they come from five different places: the ground must
    /// admit improvement at all (the manual's Terrain Tiles Table), something on
    /// it must be this civilian's work (the Resource Development Table), that
    /// deposit must already be visible if it is one of the five that hide, its
    /// yield curve must have a rung left, and the country must hold whatever
    /// knowledge that rung takes (the Benefits of Technology Table).
    /// </summary>
    private static CivilianOrderRefusal? LegalityOfImprovement(
        WorldState state,
        CountryId country,
        CivilianTypeId type,
        CellIndex cell)
    {
        var map = state.Definition.Map;
        if (map.GetTerrain(map[cell].Terrain) is not { IsImprovable: true })
        {
            return CivilianOrderRefusal.TerrainCannotBeImproved;
        }

        var level = state.GetCellDevelopment(cell);
        var worked = false;
        var undiscovered = false;
        var unknown = false;
        foreach (var resourceId in map[cell].Resources)
        {
            var resource = map.Resources[resourceId.Value];
            if (resource.ImprovedBy != type)
            {
                continue;
            }

            // "Miners cannot be used until a Prospector locates some gold, gems,
            // coal, or iron to mine." A hidden deposit is not merely unworkable
            // here — as far as its owner is concerned it is not there at all, so
            // it cannot even count towards the tile being fully developed.
            if (resource.RequiresDiscovery && !state.CanSeeDeposits(country, cell))
            {
                undiscovered = true;
                continue;
            }

            worked = true;

            // A cell holding two deposits has one level, so one deposit still
            // short of the top of its curve is reason enough to keep working.
            if (level >= resource.MaxDevelopmentLevel)
            {
                continue;
            }

            // The rung being climbed is the next one up, so that is the gate to
            // ask about. A scenario may already have authored the tile past it;
            // that is its privilege and this is not the place to argue.
            if (resource.GetRequiredTechnology(level + 1) is { } required &&
                !state.HasTechnology(country, required))
            {
                unknown = true;
                continue;
            }

            return null;
        }

        if (worked)
        {
            // Ordered by what the player can do about it: learn something,
            // or nothing at all.
            return unknown
                ? CivilianOrderRefusal.ImprovementTechnologyNotKnown
                : CivilianOrderRefusal.AlreadyFullyDeveloped;
        }

        // Reported ahead of "nothing here is your work", because it is the more
        // useful of the two: it means come back with a Prospector rather than
        // this is the wrong civilian entirely.
        return undiscovered
            ? CivilianOrderRefusal.DepositNotYetDiscovered
            : CivilianOrderRefusal.NoDepositThisCivilianWorks;
    }

    /// <summary>
    /// Whether a Prospector may search this tile. The ground must be the kind
    /// that hides anything, the country must know whatever that ground takes,
    /// and nobody of theirs may have searched it already.
    /// </summary>
    /// <remarks>
    /// Notice what is <em>not</em> checked: whether there is anything to find.
    /// A search of empty ground is legal, completes normally and reveals
    /// nothing, which is the common case and the whole reason the original
    /// counts down the tiles left to search.
    /// </remarks>
    private static CivilianOrderRefusal? LegalityOfProspecting(
        WorldState state,
        CountryId country,
        CellIndex cell)
    {
        var map = state.Definition.Map;
        if (map.GetTerrain(map[cell].Terrain)?.Prospecting is not { } rule)
        {
            return CivilianOrderRefusal.TerrainCannotBeProspected;
        }

        if (rule.RequiredTechnology is { } required && !state.HasTechnology(country, required))
        {
            return CivilianOrderRefusal.ProspectingTechnologyNotKnown;
        }

        return state.HasProspected(country, cell)
            ? CivilianOrderRefusal.AlreadyProspected
            : null;
    }
}
