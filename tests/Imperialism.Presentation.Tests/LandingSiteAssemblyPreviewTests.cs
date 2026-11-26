using System.Reflection;
using Imperialism.Content;
using Imperialism.Core;
using Xunit;

namespace Imperialism.Presentation.Tests;

/// <summary>
/// The ready-site rule after the draft's naval orders, for task forces an assembly creates
/// this turn and for a site that stands beside one the draft changes. Every expectation is
/// Core's own resolution of the same draft.
/// </summary>
public sealed class LandingSiteAssemblyPreviewTests
{
    private const string Scenario = "scenario.demo";
    private static readonly CountryId Blue = new(0), Red = new(1);
    private static readonly ProvinceId Far = new(1);
    private static readonly SeaZoneId SeaC = new(2), SeaD = new(3);

    // codex-3's fixture: Home on Sea A; Far on Seas C and D; one Blue frigate on each of C and D.
    private static CompiledWorldPackage Compile() => (CompiledWorldPackage)typeof(NavalLandingDraftInteractionTests)
        .GetMethod("Compile", BindingFlags.NonPublic | BindingFlags.Static)!
        .Invoke(null, null)!;

    /// <param name="sitesBefore">Sea zones whose frigate holds a site on Far from turn one.</param>
    /// <param name="assembleThisTurn">Sea zones whose frigate the draft assembles onto Far.</param>
    /// <param name="restampLowest">The draft re-orders the lowest-id existing site (Sea C's) to Landing(Far).</param>
    /// <param name="expected">
    /// Derived from the fixture: Sea C's site carries the landing; Sea D's is the next ready
    /// one but Sea D has no sea path from Home, so using it gives NoSeaPath; no ready site
    /// gives NotAdjacent.
    /// </param>
    [Theory]
    [InlineData(new int[0], new[] { 3 }, false, RegimentMoveRefusal.NotAdjacent)]
    [InlineData(new int[0], new[] { 2, 3 }, false, RegimentMoveRefusal.NotAdjacent)]
    [InlineData(new[] { 2 }, new[] { 3 }, false, null)]
    [InlineData(new[] { 2, 3 }, new int[0], false, null)]
    [InlineData(new[] { 2, 3 }, new int[0], true, RegimentMoveRefusal.NoSeaPath)]
    [InlineData(new[] { 2 }, new[] { 3 }, true, RegimentMoveRefusal.NotAdjacent)]
    public void ANewTaskForceIsNeverReadyAndAnUntouchedSiteStillIs(
        int[] sitesBefore, int[] assembleThisTurn, bool restampLowest, RegimentMoveRefusal? expected)
    {
        var package = Compile();
        var state = new WorldState(package.GetWorld(Scenario));
        var regiment = state.CreateRegiment(Blue, new ProvinceId(0), new ArmyTypeId(0));
        FleetId FrigateOn(int zone) => state.Fleets.Single(x => x.Country == Blue && x.SeaZone == new SeaZoneId(zone)).Id;
        var setup = TurnResolver.Resolve(state, new TurnOrders([
            new CountryTurnOrders(Blue, declareWar: [Red],
                naval: [.. sitesBefore.Select(zone => NavalOrder.Assemble([FrigateOn(zone)], NavalMission.Landing(Far)))]),
            new CountryTurnOrders(Red)]), 1);
        Assert.Equal(sitesBefore.Length, setup.Events.OfType<TaskForceLandingOrderedEvent>().Count());

        var draft = new OrdersDraft(state.Definition, Blue);
        if (restampLowest)
        {
            draft.SetNavalCommand(new TaskForceId(state.TaskForces.Min(x => x.Id.Value)), NavalMission.Landing(Far));
        }

        foreach (var zone in assembleThisTurn)
        {
            draft.SetNavalAssembly([FrigateOn(zone)], NavalMission.Landing(Far));
        }

        draft.SetRegimentMove([regiment], Far);
        var preview = OrdersPreview.Create(package, Scenario, state, draft);
        Assert.All(preview.Fleet.Orders, x => Assert.Null(x.Warning));
        var listed = OrdersPreview.CreateArmyMoves(package, Scenario, state, draft, [regiment])
            .Destinations.Single(x => x.Province == Far);
        var drafted = Assert.Single(preview.ArmyMoves.Moves);

        var events = TurnResolver.Resolve(state, draft.BuildTurnOrders(), 2).Events;
        var refused = events.OfType<RegimentMoveRefusedEvent>().SingleOrDefault(x => x.Country == Blue)?.Reason;
        Assert.Equal(expected, refused);
        Assert.Equal(expected, drafted.Warning?.Refusal);
        Assert.Equal(expected, listed.Warning?.Refusal);
        Assert.Equal(expected == RegimentMoveRefusal.NotAdjacent ? ArmyMoveVerb.Attack : ArmyMoveVerb.Landing, drafted.Verb);
    }
}
