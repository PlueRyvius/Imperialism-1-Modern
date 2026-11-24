using Godot;

namespace Imperialism.Client;

/// <summary>
/// The screens the manual says a player governs with: "a central Terrain Map
/// screen and four Orders screens accessed from the Terrain Map", plus the
/// Technology Investment screen the microscope reaches.
/// </summary>
public enum ShellScreen
{
    TerrainMap,
    Transport,
    Industry,
    BidAndOffers,
    Diplomacy,
    Technology,
}

/// <summary>What the shell requires of anything it can navigate to.</summary>
public interface IShellScreen
{
    /// <summary>The name shown in the screen's own title and its tab.</summary>
    string Title { get; }

    /// <summary>Called every time the screen is shown, with the session to read.</summary>
    void Enter(GameSession session);

    /// <summary>Called when navigating away. Screens keep their state; this only stops work.</summary>
    void Exit();
}

public static class ShellScreens
{
    public static readonly ShellScreen[] All =
    [
        ShellScreen.TerrainMap,
        ShellScreen.Transport,
        ShellScreen.Industry,
        ShellScreen.BidAndOffers,
        ShellScreen.Diplomacy,
        ShellScreen.Technology,
    ];

    /// <summary>The action name each screen answers to, bound to F1 through F6.</summary>
    public static StringName ActionName(ShellScreen screen) => screen switch
    {
        ShellScreen.TerrainMap => "shell_terrain_map",
        ShellScreen.Transport => "shell_transport",
        ShellScreen.Industry => "shell_industry",
        ShellScreen.BidAndOffers => "shell_bid_and_offers",
        ShellScreen.Diplomacy => "shell_diplomacy",
        _ => "shell_technology",
    };

    /// <summary>
    /// The tab art each screen wears. The original cut ten 60x56 tabs and
    /// assembled them down both columns of its screen frame; these six are the
    /// ones whose subject matches a screen we have.
    /// </summary>
    public static string TabTexturePath(ShellScreen screen) => screen switch
    {
        ShellScreen.TerrainMap => "res://art/tab/tab_01.png",
        ShellScreen.Transport => "res://art/tab/tab_10.png",
        ShellScreen.Industry => "res://art/tab/tab_02.png",
        ShellScreen.BidAndOffers => "res://art/tab/tab_07.png",
        ShellScreen.Diplomacy => "res://art/tab/tab_03.png",
        _ => "res://art/tab/tab_06.png",
    };
}
