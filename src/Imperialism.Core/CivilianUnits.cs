namespace Imperialism.Core;

/// <summary>
/// A kind of civilian worker, and how long one of them takes to raise a tile by
/// a level.
/// </summary>
/// <remarks>
/// The manual names nine — Miner, Prospector, Farmer, Forester, Engineer,
/// Rancher, Fisherman, Developer and Oil Driller — of which the shipped corpus
/// uses the first six. Only improvement is modelled here; prospecting,
/// construction and buying land are each their own slice.
/// <para>
/// <b>The duration is the one guess in this system.</b> Nothing in the manual,
/// the corpus or the binary says how many turns a civilian's work takes, so it
/// lives here, per type, where changing it is an edit to content rather than to
/// code. See <c>docs/formulas/development.md</c>.
/// </para>
/// </remarks>
public sealed record CivilianTypeDefinition
{
    public CivilianTypeDefinition(CivilianTypeId id, string name, int workTurns)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (workTurns <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(workTurns),
                "Work that takes no turns would let one civilian improve a tile every turn for free.");
        }

        Id = id;
        Name = name;
        WorkTurns = workTurns;
    }

    public CivilianTypeId Id { get; }

    public string Name { get; }

    /// <summary>
    /// Turns between ordering the work and the level rising. One means the tile
    /// is improved during the next turn's Development phase, in time for that
    /// turn's Extraction.
    /// </summary>
    public int WorkTurns { get; }
}

/// <summary>Work a civilian has begun and not yet finished.</summary>
public readonly record struct CivilianWorkInProgress
{
    public CivilianWorkInProgress(CellIndex cell, int turnsRemaining)
    {
        if (turnsRemaining <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(turnsRemaining),
                "Finished work is the absence of a job, not a job with nothing left.");
        }

        Cell = cell;
        TurnsRemaining = turnsRemaining;
    }

    /// <summary>
    /// The tile being improved, which is also where the civilian stands. The
    /// manual's hammer cursor moves the worker and sets it to work in one click.
    /// </summary>
    public CellIndex Cell { get; }

    public int TurnsRemaining { get; }
}

/// <summary>One civilian on the map: what it is, whose it is, and where.</summary>
/// <remarks>
/// Civilians move any distance in a turn — the manual says so outright, and
/// there is no movement-point model to build. What limits them is where they
/// may go, not how far.
/// </remarks>
public sealed record CivilianUnit
{
    public CivilianUnit(
        CivilianUnitId id,
        CountryId country,
        CivilianTypeId type,
        CellIndex cell,
        CivilianWorkInProgress? work = null)
    {
        if (work is { } job && job.Cell != cell)
        {
            throw new ArgumentException(
                "A civilian works the tile it stands on.",
                nameof(work));
        }

        Id = id;
        Country = country;
        Type = type;
        Cell = cell;
        Work = work;
    }

    public CivilianUnitId Id { get; }

    public CountryId Country { get; }

    public CivilianTypeId Type { get; }

    public CellIndex Cell { get; }

    /// <summary>Null when the civilian is idle and free to take a new order.</summary>
    public CivilianWorkInProgress? Work { get; }

    public bool IsBusy => Work is not null;
}

/// <summary>One civilian a scenario starts with.</summary>
/// <remarks>
/// The 1997 <c>civi</c> record is <c>[type, cell]</c> and names no owner: the
/// original reads it off the province the cell sits in. That holds across the
/// whole corpus — all 210 records stand on owned land, and every owner is a
/// country with a capital — so the importer derives it and Core is told
/// outright.
/// </remarks>
public readonly record struct InitialCivilian(CountryId Country, CivilianTypeId Type, CellIndex Cell);
