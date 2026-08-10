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
