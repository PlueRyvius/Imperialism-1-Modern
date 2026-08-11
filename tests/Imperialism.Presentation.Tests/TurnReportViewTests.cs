using Imperialism.Content;
using Imperialism.Core;
using Imperialism.Presentation;
using Xunit;

namespace Imperialism.Presentation.Tests;

public sealed class TurnReportViewTests
{
    [Fact]
    public void EveryPhaseAppearsOnceInPipelineOrder()
    {
        // A phase added to Core without a heading here would otherwise show up
        // as a silently missing section of the report.
        var report = ResolveFirstTurn(out _, out _);

        Assert.Equal(Enum.GetValues<TurnPhase>(), report.Phases.Select(phase => phase.Phase));
        Assert.All(report.Phases, phase => Assert.False(string.IsNullOrWhiteSpace(phase.Heading)));
    }

    [Fact]
    public void ThePhasesCoreLeavesEmptySayWhyRatherThanNothing()
    {
        var report = ResolveFirstTurn(out _, out _);

        foreach (var phase in new[] { TurnPhase.Diplomacy, TurnPhase.Conflict, TurnPhase.TradeCancellation })
        {
            var view = report.Phases.Single(candidate => candidate.Phase == phase);
            Assert.Equal("Not modelled yet.", view.Note);
            Assert.Empty(view.Lines);
        }
    }

    [Fact]
    public void ATurnWithNoOrdersStillHasSomethingToReport()
    {
        // Extraction, Transport, Feeding, Delivery and Connectivity run whether
        // or not anybody asked for anything, which is what makes an End Turn
        // button worth pressing before a single orders screen exists.
        var report = ResolveFirstTurn(out _, out _);

        Assert.Equal(1, report.TurnNumber);
        Assert.Equal(new TurnDate(1815, 1), report.StartedAt);
        Assert.Equal(new TurnDate(1815, 2), report.EndedAt);
        Assert.True(report.EventCount > 0, "A resolved turn produced no events at all.");
        Assert.True(report.LineCount >= report.EventCount, "Events outnumbered the lines rendered from them.");
        Assert.NotEmpty(report.Phases.Single(phase => phase.Phase == TurnPhase.Extraction).Lines);
    }

    [Fact]
    public void TheReportCoversEveryCountryAndNotOnlyOne()
    {
        // With no AI, a report filtered to the player would be nearly empty and
        // would hide the fact that the rest of the world is inert.
        var report = ResolveFirstTurn(out _, out _);

        var named = report.Phases
            .SelectMany(phase => phase.Lines)
            .Select(line => line.CountryName)
            .Where(name => name is not null)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        Assert.Contains("Blue Republic", named, StringComparer.Ordinal);
        Assert.Contains("Red Empire", named, StringComparer.Ordinal);
    }

    [Fact]
    public void TheReportDoesNotChangeWhenTheWorldMovesOn()
    {
        // A report stays on screen while it is read, and the world behind it is
        // free to move. Mirrors WorldViewReflectsCurrentStateWithoutChangingPriorSnapshots.
        var report = ResolveFirstTurn(out var package, out var state);
        var before = report.Phases
            .SelectMany(phase => phase.Lines)
            .Select(line => line.Text)
            .ToArray();
        var lineCount = report.LineCount;

        state.SetProvinceOwner(new ProvinceId(0), new CountryId(1));
        state.SetCash(new CountryId(0), 1);
        _ = TurnResolver.Resolve(state, TurnOrders.Empty(2), 2);

        Assert.Equal(1, report.TurnNumber);
        Assert.Equal(new TurnDate(1815, 2), report.EndedAt);
        Assert.Equal(lineCount, report.LineCount);
        Assert.Equal(before, report.Phases.SelectMany(phase => phase.Lines).Select(line => line.Text));
        Assert.Equal(2, state.CompletedTurnCount);
        Assert.NotNull(package);
    }

    [Fact]
    public void AReportRejectsAStateFromAnotherScenario()
    {
        var package = WorldContentCompiler.CompilePackage(TurnReportFixture.CreateDocument());
        var state = new WorldState(package.GetWorld("scenario.demo"));
        var resolution = TurnResolver.Resolve(state, TurnOrders.Empty(2), 1);
        var alternate = new WorldState(package.GetWorld("scenario.alternate"));

        Assert.Throws<ArgumentException>(
            () => TurnReportView.Create(package, "scenario.demo", alternate, resolution));
    }

    [Fact]
    public void AReportRejectsAStateThatHasMovedOn()
    {
        var package = WorldContentCompiler.CompilePackage(TurnReportFixture.CreateDocument());
        var state = new WorldState(package.GetWorld("scenario.demo"));
        var first = TurnResolver.Resolve(state, TurnOrders.Empty(2), 1);
        _ = TurnResolver.Resolve(state, TurnOrders.Empty(2), 2);

        var exception = Assert.Throws<ArgumentException>(
            () => TurnReportView.Create(package, "scenario.demo", state, first));

        Assert.Contains("completed 2 turns", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ThePhaseMarkersAreDroppedBecauseTheHeadingsAreTheMarkers()
    {
        var package = WorldContentCompiler.CompilePackage(TurnReportFixture.CreateDocument());
        var state = new WorldState(package.GetWorld("scenario.demo"));
        var resolution = TurnResolver.Resolve(state, TurnOrders.Empty(2), 1);

        var report = TurnReportView.Create(package, "scenario.demo", state, resolution);

        var markers = resolution.Events.Count(turnEvent => turnEvent is TurnPhaseCompletedEvent);
        Assert.Equal(14, markers);
        Assert.Equal(resolution.Events.Count - markers, report.EventCount);
    }

    [Fact]
    public void TheSeedIsCarriedThroughForReplay()
    {
        var package = WorldContentCompiler.CompilePackage(TurnReportFixture.CreateDocument());
        var state = new WorldState(package.GetWorld("scenario.demo"));
        var resolution = TurnResolver.Resolve(state, TurnOrders.Empty(2), 41);

        Assert.Equal(41ul, TurnReportView.Create(package, "scenario.demo", state, resolution).Seed);
    }

    private static TurnReportView ResolveFirstTurn(
        out CompiledWorldPackage package,
        out WorldState state)
    {
        package = WorldContentCompiler.CompilePackage(TurnReportFixture.CreateDocument());
        state = new WorldState(package.GetWorld("scenario.demo"));
        var resolution = TurnResolver.Resolve(state, TurnOrders.Empty(2), 1);
        return TurnReportView.Create(package, "scenario.demo", state, resolution);
    }
}
