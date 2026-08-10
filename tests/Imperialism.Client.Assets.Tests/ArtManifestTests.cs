using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Xunit;

namespace Imperialism.Client.Assets.Tests;

/// <summary>
/// Holds the committed art to its manifest.
/// </summary>
/// <remarks>
/// Committing extracted art is a narrowing of this project's "we ship tools,
/// never content" rule, and a narrowing nobody checks is a rule that decays.
/// The load-bearing assertion is the reverse direction — every file under
/// <c>art/</c> has to be named by the manifest — because that is what stops art
/// arriving in the tree without a recorded source.
/// </remarks>
public sealed class ArtManifestTests
{
    private static readonly string ManifestPath =
        Path.Combine(RepositoryRoot(), "assets", "manifest", "imperialism-art.json");

    private static readonly string ArtRoot =
        Path.Combine(RepositoryRoot(), "src", "Imperialism.Client", "art");

    [Fact]
    public void EveryManifestEntryNamesADistinctSourceAndTarget()
    {
        var manifest = Load();
        var sources = new HashSet<string>(StringComparer.Ordinal);
        var targets = new HashSet<string>(StringComparer.Ordinal);

        foreach (var entry in manifest.Entries)
        {
            Assert.True(
                sources.Add($"{entry.Archive}/{entry.Resource}#{entry.Crop}"),
                $"{entry.Archive}/{entry.Resource} is extracted twice with the same crop.");
            Assert.True(targets.Add(entry.Path), $"'{entry.Path}' is written twice.");
        }

        foreach (var font in manifest.Fonts)
        {
            Assert.True(targets.Add(font.Path), $"'{font.Path}' is written twice.");
        }
    }

    [Fact]
    public void EveryManifestEntryHasBeenExtracted()
    {
        var manifest = Load();

        foreach (var relative in manifest.Entries.Select(entry => entry.Path)
                     .Concat(manifest.Fonts.Select(font => font.Path)))
        {
            Assert.True(
                File.Exists(Path.Combine(ArtRoot, relative.Replace('/', Path.DirectorySeparatorChar))),
                $"The manifest names '{relative}' but no such file is committed.");
        }
    }

    [Fact]
    public void EveryCommittedFileIsNamedByTheManifest()
    {
        var manifest = Load();
        var named = manifest.Entries.Select(entry => entry.Path)
            .Concat(manifest.Fonts.Select(font => font.Path))
            .ToHashSet(StringComparer.Ordinal);

        foreach (var file in Directory.EnumerateFiles(ArtRoot, "*", SearchOption.AllDirectories))
        {
            // Godot writes an .import sidecar beside every asset. Those are
            // committed on purpose so a fresh clone does not regenerate them.
            if (Path.GetExtension(file) is ".import")
            {
                continue;
            }

            var relative = Path.GetRelativePath(ArtRoot, file).Replace(Path.DirectorySeparatorChar, '/');
            Assert.True(named.Contains(relative), $"'{relative}' is committed but the manifest does not name it.");
        }
    }

    [Fact]
    public void EveryNinePatchMarginFitsInsideItsImage()
    {
        var manifest = Load();
        var checkedAny = false;

        foreach (var entry in manifest.Entries.Where(entry => entry.NinePatch is not null))
        {
            var margins = entry.NinePatch!.Value;
            var file = Path.Combine(ArtRoot, entry.Path.Replace('/', Path.DirectorySeparatorChar));
            var (width, height) = ReadPngSize(file);

            Assert.True(
                margins.Left + margins.Right < width,
                $"'{entry.Path}' leaves no stretchable centre horizontally.");
            Assert.True(
                margins.Top + margins.Bottom < height,
                $"'{entry.Path}' leaves no stretchable centre vertically.");
            checkedAny = true;
        }

        Assert.True(checkedAny, "No nine-patch entries were checked, so this assertion proved nothing.");
    }

    [Fact]
    public void EveryEntryRecordsItsEvidenceAndConfidence()
    {
        var manifest = Load();

        foreach (var entry in manifest.Entries)
        {
            Assert.False(
                string.IsNullOrWhiteSpace(entry.Evidence),
                $"'{entry.Path}' does not say why it is what the manifest claims it is.");
            Assert.Contains(entry.Confidence, new[] { "confirmed", "inferred" }, StringComparer.Ordinal);
        }
    }

    private static Manifest Load()
    {
        Assert.True(File.Exists(ManifestPath), $"No manifest at {ManifestPath}.");
        using var document = JsonDocument.Parse(
            File.ReadAllText(ManifestPath),
            new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });

        var entries = document.RootElement.GetProperty("entries").EnumerateArray().Select(element =>
            new Entry(
                element.GetProperty("archive").GetString()!,
                element.GetProperty("resource").GetString()!,
                element.GetProperty("path").GetString()!,
                element.GetProperty("evidence").GetString()!,
                element.GetProperty("confidence").GetString()!,
                element.TryGetProperty("crop", out var crop) ? crop.GetRawText() : "full",
                element.TryGetProperty("ninePatch", out var patch)
                    ? new Margins(
                        patch.GetProperty("left").GetInt32(),
                        patch.GetProperty("top").GetInt32(),
                        patch.GetProperty("right").GetInt32(),
                        patch.GetProperty("bottom").GetInt32())
                    : null)).ToArray();

        var fonts = document.RootElement.GetProperty("fonts").EnumerateArray()
            .Select(element => new Font(element.GetProperty("path").GetString()!))
            .ToArray();

        return new Manifest(entries, fonts);
    }

    private static (int Width, int Height) ReadPngSize(string path)
    {
        var header = new byte[24];
        using (var stream = File.OpenRead(path))
        {
            Assert.Equal(header.Length, stream.Read(header));
        }

        return (
            (int)BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(16)),
            (int)BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(20)));
    }

    private static string RepositoryRoot([CallerFilePath] string callerPath = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(callerPath)!, "..", ".."));

    private sealed record Manifest(IReadOnlyList<Entry> Entries, IReadOnlyList<Font> Fonts);

    private sealed record Entry(
        string Archive,
        string Resource,
        string Path,
        string Evidence,
        string Confidence,
        string Crop,
        Margins? NinePatch);

    private readonly record struct Margins(int Left, int Top, int Right, int Bottom);

    private sealed record Font(string Path);
}
