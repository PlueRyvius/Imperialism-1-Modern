using System.Text.Json;
using System.Text.Json.Serialization;
using Imperialism.Formats.Resources;

namespace Imperialism.AssetExtractor;

/// <summary>
/// The committed record of which original bitmaps become which files, how they
/// are cropped, and how their transparency is resolved.
/// </summary>
/// <remarks>
/// The crop and the nine-patch margins live here rather than in a Godot
/// inspector on purpose: it makes the theme's style boxes generated from data,
/// so recutting a border after looking at it on a wide screen is a one-line
/// diff rather than a session of dragging handles.
/// </remarks>
internal sealed record ArtManifest(
    [property: JsonPropertyName("source")] IReadOnlyList<ArtSource> Source,
    [property: JsonPropertyName("entries")] IReadOnlyList<ArtEntry> Entries,
    [property: JsonPropertyName("fonts")] IReadOnlyList<ArtFont> Fonts)
{
    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static ArtManifest Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var manifest = JsonSerializer.Deserialize<ArtManifest>(File.ReadAllText(path), ReadOptions)
            ?? throw new InvalidDataException($"'{path}' is not an art manifest.");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var target in manifest.Entries.Select(entry => entry.Path)
                     .Concat(manifest.Fonts.Select(font => font.Path)))
        {
            if (!seen.Add(target))
            {
                throw new InvalidDataException($"The manifest writes '{target}' twice.");
            }
        }

        return manifest;
    }

    /// <summary>Crops one bitmap and applies its transparency rule, yielding RGBA.</summary>
    public static byte[] Render(DeviceIndependentBitmap bitmap, ArtEntry entry)
    {
        var width = entry.Width(bitmap);
        var height = entry.Height(bitmap);
        var left = entry.Crop?.X ?? 0;
        var top = entry.Crop?.Y ?? 0;
        if (left < 0 || top < 0 || left + width > bitmap.Width || top + height > bitmap.Height)
        {
            throw new InvalidDataException(
                $"The crop for '{entry.Path}' falls outside a {bitmap.Width}x{bitmap.Height} image.");
        }

        var rule = TransparencyRule.Parse(entry.Transparency);
        var pixels = new byte[width * height * 4];
        for (var row = 0; row < height; row++)
        {
            for (var column = 0; column < width; column++)
            {
                var source = (((top + row) * bitmap.Width) + left + column) * 4;
                var target = ((row * width) + column) * 4;
                bitmap.Pixels.AsSpan(source, 4).CopyTo(pixels.AsSpan(target, 4));
                if (rule.IsTransparent(bitmap, ((top + row) * bitmap.Width) + left + column))
                {
                    // Zero the colour as well as the alpha. A keyed pixel that
                    // keeps its colour bleeds that colour into the edges when the
                    // texture is filtered or mipmapped.
                    pixels.AsSpan(target, 4).Clear();
                }
            }
        }

        return pixels;
    }
}

/// <summary>
/// A typeface the original ships as an ordinary TrueType file beside its
/// archives. These are copied rather than decoded, and they carry a weaker
/// licensing position than the artwork: they are third-party faces the original
/// bundled, not art its authors drew. See <c>docs/asset-pipeline.md</c>.
/// </summary>
internal sealed record ArtFont(
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("role")] string Role);

internal sealed record ArtSource(
    [property: JsonPropertyName("archive")] string Archive,
    [property: JsonPropertyName("sha256")] string Sha256,
    [property: JsonPropertyName("resourceCount")] int ResourceCount);

internal sealed record ArtCrop(
    [property: JsonPropertyName("x")] int X,
    [property: JsonPropertyName("y")] int Y,
    [property: JsonPropertyName("width")] int Width,
    [property: JsonPropertyName("height")] int Height);

internal sealed record ArtNinePatch(
    [property: JsonPropertyName("left")] int Left,
    [property: JsonPropertyName("top")] int Top,
    [property: JsonPropertyName("right")] int Right,
    [property: JsonPropertyName("bottom")] int Bottom);

internal sealed record ArtEntry(
    [property: JsonPropertyName("archive")] string Archive,
    [property: JsonPropertyName("resource")] string Resource,
    [property: JsonPropertyName("group")] string Group,
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("transparency")] string Transparency,
    [property: JsonPropertyName("evidence")] string Evidence,
    [property: JsonPropertyName("confidence")] string Confidence,
    [property: JsonPropertyName("crop")] ArtCrop? Crop = null,
    [property: JsonPropertyName("ninePatch")] ArtNinePatch? NinePatch = null,
    [property: JsonPropertyName("axisStretch")] string? AxisStretch = null)
{
    public int Width(DeviceIndependentBitmap bitmap) => Crop?.Width ?? bitmap.Width;

    public int Height(DeviceIndependentBitmap bitmap) => Crop?.Height ?? bitmap.Height;
}

/// <summary>
/// How a bitmap declares which pixels are absent. The archives are eight-bit
/// with a palette per image, so an index key and a colour key are genuinely
/// different things: the same index is a different colour in a different image,
/// which makes a mis-applied index rule punch holes in unrelated art silently.
/// </summary>
internal readonly record struct TransparencyRule(int? Index, uint? Color)
{
    public static TransparencyRule Parse(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (string.Equals(value, "none", StringComparison.Ordinal))
        {
            return new TransparencyRule(null, null);
        }

        if (value.StartsWith("index:", StringComparison.Ordinal))
        {
            return new TransparencyRule(int.Parse(value[6..], System.Globalization.CultureInfo.InvariantCulture), null);
        }

        if (value.StartsWith("color:", StringComparison.Ordinal))
        {
            return new TransparencyRule(
                null,
                Convert.ToUInt32(value[6..], 16));
        }

        throw new InvalidDataException(
            $"'{value}' is not a transparency rule; expected none, index:N, or color:RRGGBB.");
    }

    public bool IsTransparent(DeviceIndependentBitmap bitmap, int pixel)
    {
        if (Index is { } index)
        {
            return bitmap.IsPalettized && bitmap.PaletteIndices[pixel] == index;
        }

        if (Color is { } color)
        {
            var packed = ((uint)bitmap.Pixels[pixel * 4] << 16) |
                ((uint)bitmap.Pixels[(pixel * 4) + 1] << 8) |
                bitmap.Pixels[(pixel * 4) + 2];
            return packed == color;
        }

        return false;
    }
}
