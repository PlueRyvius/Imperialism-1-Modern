namespace Imperialism.Core;

/// <summary>What a civilian does when it is set to work on a tile.</summary>
/// <remarks>
/// The discriminator sits on the type rather than on the order because that is
/// where the original puts it: the cursor a player sees is decided by the unit
/// they have selected, a Prospector never improves anything, and no other
/// civilian ever searches. <see cref="CivilianWorkOrder"/> therefore stays a
/// bare (unit, cell) pair and what it means follows from who was ordered.
/// <para>
/// <b>The Engineer is the exception the manual names outright</b> — "the only
/// civilian with multiple functions" — so <see cref="Construct"/> selects a
/// civilian that takes <see cref="EngineerOrder"/> instead, and the order
/// carries which of the two cursors was used. The type still decides which
/// <em>family</em> of work is possible; only inside construction does the order
/// have anything left to say.
/// </para>
/// <para>
/// Keeping the choice in content is also what keeps the word "Prospector" out
/// of Core, in the same way <see cref="PortFishing"/> keeps "fish" out.
/// </para>
/// </remarks>
public enum CivilianWorkKind : byte
{
    /// <summary>Raise the tile's development level by one. Every civilian but the Prospector.</summary>
    Improve,

    /// <summary>
    /// Search the tile for deposits its owner cannot otherwise see. Reveals
    /// whatever is there, which is usually nothing: only 449 of the corpus's
    /// 2,860 barren hills and 346 of its 1,589 mountains carry a marker at all.
    /// </summary>
    Prospect,

    /// <summary>
    /// Build the transport network: rail into an adjacent tile, or a depot or
    /// port on this one. The Engineer's, and nobody else's.
    /// </summary>
    Construct,
}

/// <summary>What an Engineer is building.</summary>
/// <remarks>
/// The original selects between them by where the cursor is: over an adjacent
/// tile it shows a piece of track, over the Engineer's own tile a hammer and a
/// dialog. So <see cref="Rail"/> is the one that names a tile the Engineer does
/// not stand on, and the other two are choices from that dialog.
/// <para>
/// Fortifications are the dialog's third choice and are out of scope: they are
/// military, and the manual builds them "throughout the province, not just the
/// current tile", which is a different shape entirely.
/// </para>
/// </remarks>
public enum EngineerConstruction : byte
{
    /// <summary>A railroad line between the Engineer's tile and an adjacent one.</summary>
    Rail,

    /// <summary>A rail depot on the Engineer's own tile.</summary>
    Depot,

    /// <summary>A port on the Engineer's own tile. Needs water, and costs more than a depot.</summary>
    Port,
}

/// <summary>
/// A kind of civilian worker, what its work does, and how long one of them takes
/// to do it.
/// </summary>
/// <remarks>
/// The manual names nine — Miner, Prospector, Farmer, Forester, Engineer,
/// Rancher, Fisherman, Developer and Oil Driller — of which the shipped corpus
/// uses the first six. Improvement and prospecting are modelled here;
/// construction and buying land are each their own slice.
/// <para>
/// <b>The duration is the one guess in this system.</b> Nothing in the manual,
/// the corpus or the binary says how many turns a civilian's work takes, so it
/// lives here, per type, where changing it is an edit to content rather than to
/// code. A search reuses that same number rather than inventing a second one.
/// See <c>docs/formulas/development.md</c>.
/// </para>
/// </remarks>
public sealed record CivilianTypeDefinition
{
    public CivilianTypeDefinition(
        CivilianTypeId id,
        string name,
        int workTurns,
        CivilianWorkKind work = CivilianWorkKind.Improve)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (workTurns <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(workTurns),
                "Work that takes no turns would let one civilian improve a tile every turn for free.");
        }

        if (!Enum.IsDefined(work))
        {
            throw new ArgumentOutOfRangeException(nameof(work));
        }

        Id = id;
        Name = name;
        WorkTurns = workTurns;
        Work = work;
    }

    public CivilianTypeId Id { get; }

    public string Name { get; }

    /// <summary>
    /// Turns between ordering the work and the level rising, or the ground being
    /// searched. One means the tile is improved during the next turn's
    /// Development phase, in time for that turn's Extraction.
    /// </summary>
    public int WorkTurns { get; }

    /// <summary>
    /// What setting this civilian to work actually does. Defaults to improving,
    /// which is what every civilian did before prospecting existed and what an
    /// older content package still means.
    /// </summary>
    public CivilianWorkKind Work { get; }
}

/// <summary>Work a civilian has begun and not yet finished.</summary>
public readonly record struct CivilianWorkInProgress
{
    public CivilianWorkInProgress(
        CellIndex cell,
        int turnsRemaining,
        EngineerJob? construction = null)
    {
        if (turnsRemaining <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(turnsRemaining),
                "Finished work is the absence of a job, not a job with nothing left.");
        }

        if (construction is { } job)
        {
            if (!Enum.IsDefined(job.Kind))
            {
                throw new ArgumentOutOfRangeException(nameof(construction));
            }

            if (job.Kind == EngineerConstruction.Rail == (job.Target == cell))
            {
                throw new ArgumentException(
                    "Rail joins the Engineer's tile to another; a depot or port stands on its own.",
                    nameof(construction));
            }
        }

        Cell = cell;
        TurnsRemaining = turnsRemaining;
        Construction = construction;
    }

    /// <summary>
    /// The tile being improved, which is also where the civilian stands. The
    /// manual's hammer cursor moves the worker and sets it to work in one click.
    /// </summary>
    public CellIndex Cell { get; }

    public int TurnsRemaining { get; }

    /// <summary>
    /// What an Engineer is building here, or null for the ordinary work of
    /// improving or searching the tile it stands on.
    /// </summary>
    public EngineerJob? Construction { get; }
}

/// <summary>
/// One piece of transport network an Engineer is building, and where.
/// </summary>
/// <remarks>
/// <see cref="Target"/> is the tile the player clicked, which is the whole of
/// how the original distinguishes the two cursors: an adjacent tile lays rail
/// towards it, the Engineer's own tile opens the construction dialog.
/// </remarks>
public readonly record struct EngineerJob(EngineerConstruction Kind, CellIndex Target);

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
