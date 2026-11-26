using System.Runtime.CompilerServices;
using Xunit;

namespace Imperialism.Client.Assets.Tests;

public sealed class ThemeIntegrityTests
{
    [Fact]
    public void DropdownArrowUsesReadableFontColoursInEveryState()
    {
        var settings = File.ReadLines(ThemePath())
            .Where(line => line.StartsWith("OptionButton/", StringComparison.Ordinal))
            .Select(line => line.Split('=', 2, StringSplitOptions.TrimEntries))
            .ToDictionary(parts => parts[0], parts => parts[1]);

        // Godot's OptionButton tints the arrow with the current font colour
        // only when this constant is enabled; arrow_color is unsupported.
        Assert.Equal("1", settings["OptionButton/constants/modulate_arrow"]);
        Assert.False(settings.ContainsKey("OptionButton/colors/arrow_color"));

        foreach (var state in new[] { "font_color", "font_hover_color", "font_pressed_color",
                     "font_hover_pressed_color", "font_focus_color" })
        {
            Assert.Equal("Color(0.16, 0.12, 0.07, 1)", settings[$"OptionButton/colors/{state}"]);
        }

        Assert.Equal("Color(0.45, 0.41, 0.35, 1)",
            settings["OptionButton/colors/font_disabled_color"]);
    }

    private static string ThemePath([CallerFilePath] string callerPath = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(callerPath)!,
            "..", "..", "src", "Imperialism.Client", "ui", "imperialism.theme.tres"));
}
