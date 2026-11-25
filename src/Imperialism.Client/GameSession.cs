using Imperialism.Content;
using Imperialism.Core;
using Imperialism.Presentation;

namespace Imperialism.Client;

/// <summary>
/// One game in progress: the package, the scenario, the mutable world, the
/// country the human is playing, and the current snapshots of both.
/// </summary>
/// <remarks>
/// <b>Which country the human plays lives here and nowhere lower.</b> Core has
/// no notion of a player and should not grow one — that is a rule about the
/// interface, not about the world, and Core's public surface is held to a
/// architecture test. Putting it in content would bake the playable power into
/// the <c>.iworld</c>, when the same package must be playable as any of them.
/// Putting it in Presentation would confuse issuing a view with choosing which
/// view to issue.
///
/// This type is deliberately not a Godot node and deliberately not an autoload.
/// Screens receive it through <c>Enter</c>, so a screen can only read what the
/// shell handed it rather than reaching for whatever is ambient.
///
/// It computes nothing. <see cref="Refresh"/> calls the two Presentation
/// factories and raises an event; every number the interface shows was worked
/// out by Core.
/// </remarks>
public sealed class GameSession
{
    private bool _isEndingTurn;

    private GameSession(
        CompiledWorldPackage package,
        string scenarioKey,
        WorldState world,
        CountryId localCountry,
        MapViewDefinition map)
    {
        Package = package;
        ScenarioKey = scenarioKey;
        World = world;
        LocalCountry = localCountry;
        Map = map;
        WorldView = WorldViewState.Create(package, scenarioKey, world);
        Status = CountryStatusView.Create(package, scenarioKey, world, localCountry);
        Draft = new OrdersDraft(world.Definition, localCountry);
    }

    /// <summary>Raised after both snapshots have been re-issued.</summary>
    public event Action? Refreshed;

    public CompiledWorldPackage Package { get; }

    public string ScenarioKey { get; }

    public WorldState World { get; }

    public CountryId LocalCountry { get; }

    public MapViewDefinition Map { get; }

    public WorldViewState WorldView { get; private set; }

    public CountryStatusView Status { get; private set; }

    /// <summary>
    /// The local country's orders as the screens have set them so far. One draft
    /// for the whole game: End Turn submits it and then clears only what the
    /// manual says is not saved (<see cref="OrdersDraft.AdvanceTurn"/>).
    /// </summary>
    public OrdersDraft Draft { get; }

    /// <summary>What the orders screens draw for <see cref="Draft"/> right now.</summary>
    public OrdersPreview Preview() => OrdersPreview.Create(Package, ScenarioKey, World, Draft);

    /// <summary>The report of the last turn resolved, or null before the first.</summary>
    public TurnReportView? LastReport { get; private set; }

    /// <summary>Raw events for client smoke diagnostics, from the same resolved turn.</summary>
    public TurnResolution? LastResolution { get; private set; }

    /// <summary>The local country's paper, or null before the first resolved turn.</summary>
    public NewspaperView? LastNewspaper { get; private set; }

    public static GameSession Start(
        CompiledWorldPackage package,
        string scenarioKey,
        CountryId localCountry,
        MapViewDefinition map)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentException.ThrowIfNullOrWhiteSpace(scenarioKey);
        ArgumentNullException.ThrowIfNull(map);
        return new GameSession(
            package,
            scenarioKey,
            new WorldState(package.GetWorld(scenarioKey)),
            localCountry,
            map);
    }

    /// <summary>
    /// The one place snapshots are re-issued. Everything that changes the world
    /// calls this and nothing else rebuilds a view.
    /// </summary>
    public void Refresh()
    {
        WorldView = WorldViewState.Create(Package, ScenarioKey, World);
        Status = CountryStatusView.Create(Package, ScenarioKey, World, LocalCountry);
        Refreshed?.Invoke();
    }

    /// <summary>
    /// Resolves one turn for every country and reports what happened.
    /// </summary>
    /// <remarks>
    /// <b>The local country submits its <see cref="Draft"/>, and every other
    /// country submits empty orders</b>, because nothing plays the other powers.
    /// That is not a placeholder for an AI so much as an honest statement of what
    /// the engine currently is; the report says so on its own face. An untouched
    /// draft builds empty orders, so a turn ended before any screen is used
    /// resolves exactly as it did before the draft existed.
    ///
    /// Presentation factories and a guard, deliberately. Nothing in this
    /// assembly is under the test suite, so every judgement about what a turn <em>means</em>
    /// belongs in <see cref="TurnReportView"/> where it is covered.
    /// </remarks>
    public TurnReportView EndTurn()
    {
        // Resolving is destructive and Core has no re-entrancy guard of its own,
        // while a Godot button press can arrive twice. The disabled button is a
        // courtesy; this is the guard.
        if (_isEndingTurn)
        {
            throw new InvalidOperationException("A turn is already being resolved.");
        }

        _isEndingTurn = true;
        try
        {
            var resolution = TurnResolver.Resolve(
                World,
                Draft.BuildTurnOrders(),
                // The turn about to resolve. No phase consumes the seed yet, so
                // what matters today is only that it is deterministic and
                // distinct per turn: a clock would make the headless gate's
                // output move for no gain. When a phase does start reading it,
                // this becomes a proper stream and the change is one line.
                (ulong)(World.CompletedTurnCount + 1));

            // Built before the refresh, so nothing handling Refreshed can
            // observe a session whose report or paper has not been made yet.
            LastResolution = resolution;
            LastReport = TurnReportView.Create(Package, ScenarioKey, World, resolution);
            LastNewspaper = NewspaperView.Create(Package, ScenarioKey, World, resolution, LocalCountry);
            Draft.AdvanceTurn();
            Refresh();
            return LastReport;
        }
        finally
        {
            _isEndingTurn = false;
        }
    }

    /// <summary>
    /// The countries a player may choose, most specific rule first: the
    /// scenario's own list of powers that take the fair-start defaults, then the
    /// Great Powers, then everyone. Core has no playable flag, so this is the
    /// closest thing the data offers and the order matters.
    /// </summary>
    public static IReadOnlyList<CountryDefinition> PlayableCountries(WorldDefinition world)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (world.Scenario.DefaultStartCountries.Count > 0)
        {
            return Array.AsReadOnly(world.Scenario.DefaultStartCountries
                .Select(country => world.Countries[country.Value])
                .ToArray());
        }

        var greatPowers = world.Countries.Where(static country => country.IsGreatPower).ToArray();
        return Array.AsReadOnly(greatPowers.Length > 0 ? greatPowers : world.Countries.ToArray());
    }
}
