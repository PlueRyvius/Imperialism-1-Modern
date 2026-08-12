using System.Buffers.Binary;
using System.Text;
using Imperialism.Formats;
using Xunit;

namespace Imperialism.Formats.Tests;

public sealed class ScenarioCodecTests
{
    [Fact]
    public void BinaryCodecUsesBigEndianFieldsAndTerminator()
    {
        var document = new ScenarioDocument(
            [new ScenarioRecord("year", [0x01020304])]);

        var encoded = LegacyScenarioCodec.Encode(document);

        Assert.Equal("year", Encoding.ASCII.GetString(encoded, 0, 4));
        Assert.Equal(0x01020304u, BinaryPrimitives.ReadUInt32BigEndian(encoded.AsSpan(4, 4)));
        Assert.Equal("TERM", Encoding.ASCII.GetString(encoded, 8, 4));
    }

    [Fact]
    public void RawNamePaddingAndTrailingBytesArePreserved()
    {
        var nameField = new byte[64];
        Encoding.ASCII.GetBytes("Testland").CopyTo(nameField, 0);
        nameField[8] = 0;
        for (var index = 9; index < nameField.Length; index++)
        {
            nameField[index] = (byte)index;
        }

        var raw = "cnam"u8.ToArray()
            .Concat(new byte[4])
            .Concat(nameField)
            .Concat("TERM"u8.ToArray())
            .Concat(new byte[] { 9, 8, 7 })
            .ToArray();

        var decoded = LegacyScenarioCodec.Decode(raw);

        Assert.Equal("Testland", decoded.Records[0].Name);
        Assert.Equal(nameField, decoded.Records[0].RawNameField.ToArray());
        Assert.Equal(new byte[] { 9, 8, 7 }, decoded.TrailingBytes);
        Assert.Equal(raw, LegacyScenarioCodec.Encode(decoded));
    }

    [Fact]
    public void EditedNameIsCanonicallyNullPadded()
    {
        var raw = "cnam"u8.ToArray()
            .Concat(new byte[4])
            .Concat(Encoding.ASCII.GetBytes("Old\0").Concat(Enumerable.Repeat((byte)'!', 60)))
            .Concat("TERM"u8.ToArray())
            .ToArray();
        var document = LegacyScenarioCodec.Decode(raw);
        document.Records[0].Name = "New";

        var encoded = LegacyScenarioCodec.Encode(document);

        Assert.Equal(Encoding.ASCII.GetBytes("New"), encoded[8..11]);
        Assert.All(encoded[11..72], static value => Assert.Equal(0, value));
    }

    [Theory]
    [InlineData("year\0\0\0\x01", "TERM")]
    [InlineData("nope", "Unknown")]
    [InlineData("ye", "Truncated")]
    public void BinaryCodecRejectsMalformedInput(string source, string message)
    {
        var raw = Encoding.Latin1.GetBytes(source);

        var exception = Assert.Throws<InvalidDataException>(() => LegacyScenarioCodec.Decode(raw));

        Assert.Contains(message, exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BinaryCodecRejectsTruncatedName()
    {
        var raw = "cnam"u8.ToArray().Concat(new byte[4]).Concat(new byte[63]).ToArray();

        Assert.Throws<InvalidDataException>(() => LegacyScenarioCodec.Decode(raw));
    }

    [Fact]
    public void BinaryCodecRoundTripsBlankNameFieldLosslessly()
    {
        var raw = "cnam"u8.ToArray()
            .Concat(new byte[4])
            .Concat(new byte[64])
            .Concat("TERM"u8.ToArray())
            .ToArray();

        var decoded = LegacyScenarioCodec.Decode(raw);

        Assert.Equal(string.Empty, decoded.Records[0].Name);
        Assert.Equal(raw, LegacyScenarioCodec.Encode(decoded));
    }

    [Fact]
    public void EditedNonAsciiNameThrowsInsteadOfBeingReplaced()
    {
        var document = new ScenarioDocument([new ScenarioRecord("cnam", [0u], "France")]);
        document.Records[0].Name = "Café";

        Assert.Throws<InvalidDataException>(() => LegacyScenarioCodec.Encode(document));
    }

    [Fact]
    public void TextLoadRejectsNonAsciiBytesInsteadOfReplacingThem()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(path, Encoding.Latin1.GetBytes("year 5\rzone 4 Café\r"));

            Assert.Throws<DecoderFallbackException>(() => ScenarioTextCodec.Load(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData("\r")]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void TextCodecAcceptsAllNewlineStylesAndNamesWithSpaces(string newline)
    {
        var text = string.Join(newline, "year 5", "zone 40 Port Said", "cash 0 2500") + newline;

        var document = ScenarioTextCodec.Decode(text);

        Assert.Equal(["year", "zone", "cash"], document.Records.Select(static record => record.Tag));
        Assert.Equal("Port Said", document.Records[1].Name);
        Assert.Equal("year 5\rzone 40 Port Said\rcash 0 2500\r", ScenarioTextCodec.Encode(document));
    }

    [Fact]
    public void TextCodecHandlesEveryKnownTagAndArity()
    {
        (string Tag, int Fields, bool HasName)[] formats =
        [
            ("cnam", 1, true),
            ("pnam", 1, true),
            ("zone", 1, true),
            ("tech", 2, false),
            ("year", 1, false),
            ("tyer", 2, false),
            ("cash", 2, false),
            ("tran", 2, false),
            ("capa", 3, false),
            ("army", 3, false),
            ("ware", 3, false),
            ("emba", 3, false),
            ("rela", 3, false),
            ("trea", 3, false),
            ("port", 1, false),
            ("rail", 1, false),
            ("deve", 2, false),
            ("civi", 2, false),
            ("labo", 4, false),
            ("tclr", 1, false),
            ("tbar", 3, false),
            ("ship", 4, false),
            ("coun", 2, false),
            ("flag", 1, false),
        ];
        var lines = formats.Select(format =>
            format.Tag +
            string.Concat(Enumerable.Range(0, format.Fields).Select(static value => $" {value}")) +
            (format.HasName ? " Example Name" : string.Empty));

        var document = ScenarioTextCodec.Decode(string.Join('\r', lines));

        Assert.Equal(formats.Select(static format => format.Tag), document.Records.Select(static record => record.Tag));
        Assert.Equal(
            LegacyScenarioCodec.Encode(document),
            LegacyScenarioCodec.Encode(LegacyScenarioCodec.Decode(LegacyScenarioCodec.Encode(document))));
    }

    [Theory]
    [InlineData("nope 1", "Line 1: unknown tag")]
    [InlineData("cash 0", "Line 1: tag 'cash' expects 2")]
    [InlineData("cash -1 2", "Line 1: field 1")]
    [InlineData("cash 0 4294967296", "Line 1: field 2")]
    [InlineData("year 1 2", "Line 1: tag 'year' expects 1")]
    [InlineData("zone 4", "Line 1: tag 'zone' requires a name")]
    public void TextCodecReportsLineNumberedErrors(string text, string message)
    {
        var exception = Assert.Throws<InvalidDataException>(() => ScenarioTextCodec.Decode(text));

        Assert.Contains(message, exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [CorpusFact]
    public void OriginalBinaryAndTextScenariosRoundTripWhenCorpusIsConfigured()
    {
        var directory = CorpusFactAttribute.RequireScenarioDirectory();

        var binaries = Directory.GetFiles(directory, "*.scn")
            .Where(IsNumberedScenarioFile)
            .Order(StringComparer.Ordinal)
            .ToArray();
        // At least the ten originals; a used Scenario folder also holds worlds
        // this project generated into it, which must round-trip too.
        Assert.True(binaries.Length >= 10, $"Expected the corpus, found {binaries.Length}.");
        foreach (var path in binaries)
        {
            var original = File.ReadAllBytes(path);
            Assert.Equal(original, LegacyScenarioCodec.Encode(LegacyScenarioCodec.Decode(original)));
        }

        var plaintext = Directory.GetFiles(directory)
            .Where(static path => string.IsNullOrEmpty(Path.GetExtension(path)))
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(7, plaintext.Length);
        foreach (var path in plaintext)
        {
            var decoded = ScenarioTextCodec.Load(path);
            var reparsed = ScenarioTextCodec.Decode(ScenarioTextCodec.Encode(decoded));
            Assert.Equal(
                decoded.Records.Select(Shape),
                reparsed.Records.Select(Shape));
        }
    }

    private static string Shape(ScenarioRecord record) =>
        $"{record.Tag}|{string.Join(',', record.Fields)}|{record.Name}";

    private static bool IsNumberedScenarioFile(string path)
    {
        var stem = Path.GetFileNameWithoutExtension(path);
        return stem.Length > 1 && stem[0] == 's' && int.TryParse(stem.AsSpan(1), out _);
    }
}
