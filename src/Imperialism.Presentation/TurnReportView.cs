using Imperialism.Content;
using Imperialism.Core;

namespace Imperialism.Presentation;

/// <summary>One phase of a resolved turn, and everything it had to say.</summary>
public sealed record TurnReportPhaseView(
    TurnPhase Phase,
    string Heading,
    string? Note,
    IReadOnlyList<TurnReportLine> Lines);

/// <summary>
/// What one resolved turn did, grouped under the phases that did it, detached
/// from the world that has since moved on.
/// </summary>
/// <remarks>
/// <c>docs/architecture.md</c> has said since Phase 0 that the event log is the
/// presentation contract and that the client animates the log rather than
/// diffing state. This is the first thing to honour that, so the shape here is
/// the shape every later report inherits.
///
/// The view holds strings, dates and value-type identifiers and nothing else —
/// no <c>WorldState</c>, no <c>WorldDefinition</c>, no package. Detachment is
/// therefore a property of its construction rather than a discipline anyone has
/// to keep, which matters because a report stays on screen while the player
/// reads it and the world behind it is free to move.
/// </remarks>
public sealed class TurnReportView
{
    private readonly IReadOnlyList<TurnReportPhaseView> _phases;

    private TurnReportView(
        int turnNumber,
        TurnDate startedAt,
        TurnDate endedAt,
        ulong seed,
        string scenarioKey,
        int eventCount,
        IEnumerable<TurnReportPhaseView> phases)
    {
        TurnNumber = turnNumber;
        StartedAt = startedAt;
        EndedAt = endedAt;
        Seed = seed;
        ScenarioKey = scenarioKey;
        EventCount = eventCount;
        _phases = Array.AsReadOnly(phases.ToArray());
        LineCount = _phases.Sum(phase => phase.Lines.Count);
    }

    public int TurnNumber { get; }

    public TurnDate StartedAt { get; }

    public TurnDate EndedAt { get; }

    /// <summary>
    /// Recorded for replay. No phase consumes it yet, so two turns resolved from
    /// the same state and the same orders differ in nothing but this number.
    /// </summary>
    public ulong Seed { get; }

    public string ScenarioKey { get; }

    /// <summary>Source events, excluding the phase markers.</summary>
    public int EventCount { get; }

    /// <summary>Rendered lines. Larger than <see cref="EventCount"/> where an event had more than one thing to say.</summary>
    public int LineCount { get; }

    /// <summary>Always fourteen, in pipeline order, including the phases that had nothing to report.</summary>
    public IReadOnlyList<TurnReportPhaseView> Phases => _phases;

    public static TurnReportView Create(
        CompiledWorldPackage package,
        string scenarioKey,
        WorldState state,
        TurnResolution resolution)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentException.ThrowIfNullOrWhiteSpace(scenarioKey);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(resolution);
        var world = package.GetWorld(scenarioKey);
        if (!ReferenceEquals(world, state.Definition))
        {
            throw new ArgumentException(
                "The runtime state must belong to the selected package scenario.",
                nameof(state));
        }

        // Resolving completes the turn as its last act, so a state that agrees
        // with the resolution is one the events were produced from. A state that
        // has run on since would name civilians and provinces that were not
        // there at the time.
        if (resolution.TurnNumber != state.CompletedTurnCount)
        {
            throw new ArgumentException(
                $"Turn {resolution.TurnNumber} cannot be reported against a world that has " +
                $"completed {state.CompletedTurnCount} turns.",
                nameof(resolution));
        }

        var renderer = TurnReportRenderer.Create(package, scenarioKey, state);
        var byPhase = new Dictionary<TurnPhase, List<TurnReportLine>>();
        var eventCount = 0;
        foreach (var turnEvent in resolution.Events)
        {
            if (turnEvent is TurnPhaseCompletedEvent)
            {
                continue;
            }

            eventCount++;
            if (!byPhase.TryGetValue(turnEvent.Phase, out var lines))
            {
                lines = [];
                byPhase[turnEvent.Phase] = lines;
            }

            lines.AddRange(renderer.Render(turnEvent));
        }

        var phases = new List<TurnReportPhaseView>();
        foreach (var phase in Enum.GetValues<TurnPhase>())
        {
            var (heading, note) = Describe(phase);
            phases.Add(new TurnReportPhaseView(
                phase,
                heading,
                note,
                Array.AsReadOnly(byPhase.TryGetValue(phase, out var lines)
                    ? lines.ToArray()
                    : [])));
        }

        return new TurnReportView(
            resolution.TurnNumber,
            resolution.StartedAt,
            resolution.EndedAt,
            resolution.Seed,
            scenarioKey,
            eventCount,
            phases);
    }

    /// <summary>
    /// What each phase is called, and where a phase is empty by design rather
    /// than by circumstance.
    /// </summary>
    /// <remarks>
    /// The first six headings are the manual's own list, which is also where the
    /// pipeline's order came from: "Diplomatic offers are exchanged, and either
    /// accepted or rejected. Trade deals are offered, and accepted, or rejected.
    /// Industrial production takes place. Military conflicts are resolved.
    /// Intercepted or blockaded trades are cancelled. All commodities
    /// transported internally, or successfully delivered by traders, are placed
    /// in the industrial warehouse for use on the next turn."
    ///
    /// <b>The notes are hand-maintained and nothing will fail when they go
    /// stale.</b> When Diplomacy, Conflict or TradeCancellation gain rules, this
    /// table has to be edited by hand or the report will keep calling a working
    /// phase unmodelled. It is the one fact in this file with no test behind it;
    /// a test asserting those phases stay empty would assert a temporary truth
    /// and fail on the day the feature arrived.
    /// </remarks>
    private static (string Heading, string? Note) Describe(TurnPhase phase) => phase switch
    {
        TurnPhase.Diplomacy => ("Diplomatic offers", "Not modelled yet."),
        TurnPhase.Trade => ("Trade deals", null),
        TurnPhase.Production => ("Industrial production", null),
        TurnPhase.Construction => ("Industrial expansion", null),
        TurnPhase.Development => ("Civilian work", null),
        TurnPhase.Migration => ("The Capitol", null),
        TurnPhase.Conflict => ("Military conflicts", "Not modelled yet."),
        TurnPhase.TradeCancellation => ("Intercepted and blockaded trades", "Not modelled yet."),
        TurnPhase.Extraction => ("The harvest", null),
        TurnPhase.Transport => ("The network", null),
        TurnPhase.Feeding => ("Feeding the workforce", null),
        TurnPhase.Delivery => ("Into the warehouse", null),
        TurnPhase.Investment => ("Investment in technology", null),
        TurnPhase.Connectivity => ("The rail network settles", null),
        _ => throw new ArgumentOutOfRangeException(nameof(phase)),
    };
}
