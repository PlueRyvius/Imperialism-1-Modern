using Godot;

namespace Imperialism.Client;

internal static class TerrainPalette
{
    private static readonly IReadOnlyDictionary<string, Color> TerrainColors =
        new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase)
        {
            ["terrain.ocean"] = Color.Color8(43, 91, 130),
            ["terrain.sea"] = Color.Color8(43, 91, 130),
            ["terrain.plains"] = Color.Color8(139, 157, 92),
            ["terrain.grassland"] = Color.Color8(139, 157, 92),
            ["terrain.forest"] = Color.Color8(60, 111, 70),
            ["terrain.hills"] = Color.Color8(142, 124, 88),
            ["terrain.mountains"] = Color.Color8(119, 111, 103),
            ["terrain.desert"] = Color.Color8(194, 166, 103),
            ["terrain.swamp"] = Color.Color8(89, 119, 91),
            ["terrain.tundra"] = Color.Color8(164, 174, 166),
        };

    public static Color Terrain(string key) => TerrainColors.TryGetValue(key, out var color)
        ? color
        : FromKey(key, saturation: 0.32f, value: 0.63f);

    public static Color Country(string? key, float alpha)
    {
        if (key is null)
        {
            return new Color(0, 0, 0, 0);
        }

        var color = FromKey(key, saturation: 0.65f, value: 0.94f);
        return new Color(color.R, color.G, color.B, alpha);
    }

    public static Color Resource(string key) =>
        FromKey(key, saturation: 0.72f, value: 0.95f);

    private static Color FromKey(string key, float saturation, float value)
    {
        var hash = 2166136261u;
        foreach (var character in key)
        {
            hash ^= character;
            hash *= 16777619u;
        }

        return Color.FromHsv((hash % 360) / 360f, saturation, value);
    }
}
