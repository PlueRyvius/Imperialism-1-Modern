using System.Text;
using Imperialism.Formats;
using Xunit;

namespace Imperialism.Formats.Tests;

public sealed class ScenarioInfoCodecTests
{
    [Theory]
    [InlineData("\r")]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void DecodeAcceptsNewlineVariants(string newline)
    {
        var document = ScenarioInfoCodec.Decode(Encoding.ASCII.GetBytes(SampleText(newline, false)));

        Assert.Equal("A New World", document.Title);
        Assert.Equal("Description", document.Overview);
        Assert.Equal(7, document.CountrySections.Count);
        Assert.Equal([2, -1, 4, 3, 2, 4, 1, 0], document.Metadata);
    }

    [Fact]
    public void UnchangedDocumentReproducesRawBytesExactly()
    {
        var raw = Encoding.ASCII.GetBytes(SampleText("\r\n", false));
        var document = ScenarioInfoCodec.Decode(raw);

        Assert.Equal(raw, ScenarioInfoCodec.Encode(document));
        Assert.Equal(raw, document.RawBytes.ToArray());
    }

    [Fact]
    public void EditedDocumentUsesCanonicalCp1252AndCr()
    {
        var document = ScenarioInfoCodec.Decode(Encoding.ASCII.GetBytes(SampleText("\n", false)));
        document.Title = "L'été nouveau";

        var encoded = ScenarioInfoCodec.Encode(document);

        Assert.Contains((byte)0xe9, encoded);
        Assert.DoesNotContain((byte)'\n', encoded);
        Assert.Equal((byte)'\r', encoded[^1]);
        Assert.Equal("L'été nouveau", ScenarioInfoCodec.Decode(encoded).Title);
    }

    [Theory]
    [InlineData(6, 8, "seven country")]
    [InlineData(7, 7, "eight metadata")]
    public void EncodeEnforcesLegacyCardinality(int countries, int metadata, string message)
    {
        var document = new ScenarioInfoDocument(
            "Title", "Overview", Enumerable.Repeat("Country", countries), Enumerable.Repeat(0, metadata));

        var exception = Assert.Throws<InvalidDataException>(() => ScenarioInfoCodec.Encode(document));

        Assert.Contains(message, exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void OriginalInfoFilesRoundTripWhenCorpusIsConfigured()
    {
        var directory = Environment.GetEnvironmentVariable("IMPERIALISM_SCENARIO_DIR");
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        var paths = Directory.GetFiles(directory, "*.inf")
            .Where(IsNumberedScenarioFile)
            .Order(StringComparer.Ordinal)
            .ToArray();
        // At least the ten originals; a used Scenario folder also holds worlds
        // this project generated into it, which must round-trip too.
        Assert.True(paths.Length >= 10, $"Expected the corpus, found {paths.Length}.");
        foreach (var path in paths)
        {
            var original = File.ReadAllBytes(path);
            Assert.Equal(original, ScenarioInfoCodec.Encode(ScenarioInfoCodec.Decode(original)));
        }
    }

    private static string SampleText(string newline, bool finalNewline) =>
        string.Join(
            newline,
            "A New World", "#", "Description",
            "#", "Country one", "#", "Country two", "#", "Country three",
            "#", "Country four", "#", "Country five", "#", "Country six",
            "#", "Country seven", "# 2 -1 4 3 2 4 1 0") +
        (finalNewline ? newline : string.Empty);

    private static bool IsNumberedScenarioFile(string path)
    {
        var stem = Path.GetFileNameWithoutExtension(path);
        return stem.Length > 1 && stem[0] == 's' && int.TryParse(stem.AsSpan(1), out _);
    }
}
