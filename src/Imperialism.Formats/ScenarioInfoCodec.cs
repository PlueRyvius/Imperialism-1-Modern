using System.Globalization;
using System.Text;

namespace Imperialism.Formats;

public static class ScenarioInfoCodec
{
    private static readonly Encoding Windows1252 = CreateEncoding();

    public static ScenarioInfoDocument Decode(ReadOnlySpan<byte> bytes)
    {
        var rawBytes = bytes.ToArray();
        var text = Windows1252.GetString(rawBytes);
        var normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        var blocks = new List<List<string>> { new() };
        int[]? metadata = null;
        var lines = normalized.Split('\n');
        for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            var line = lines[lineIndex];
            if (line.StartsWith('#'))
            {
                var suffix = line[1..].Trim();
                if (suffix.Length == 0)
                {
                    if (metadata is not null)
                    {
                        throw Error(lineIndex, "section follows metadata");
                    }

                    blocks.Add([]);
                    continue;
                }

                if (metadata is not null)
                {
                    throw Error(lineIndex, "duplicate metadata record");
                }

                var values = suffix.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                metadata = new int[values.Length];
                for (var valueIndex = 0; valueIndex < values.Length; valueIndex++)
                {
                    if (!int.TryParse(
                            values[valueIndex],
                            NumberStyles.AllowLeadingSign,
                            CultureInfo.InvariantCulture,
                            out metadata[valueIndex]))
                    {
                        throw Error(lineIndex, "metadata must contain decimal integers");
                    }
                }

                continue;
            }

            if (metadata is not null)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    throw Error(lineIndex, "text follows metadata");
                }

                continue;
            }

            blocks[^1].Add(line);
        }

        if (blocks.Count != 2 + ScenarioInfoDocument.CountrySectionCount)
        {
            throw new InvalidDataException(
                "Scenario info must contain a title, overview, and exactly seven country sections.");
        }

        if (metadata is null || metadata.Length != ScenarioInfoDocument.MetadataValueCount)
        {
            throw new InvalidDataException(
                $"Scenario info metadata must contain exactly eight integers, got {metadata?.Length ?? 0}.");
        }

        var sections = blocks.Select(NormalizeSection).ToArray();
        if (sections[0].Length == 0)
        {
            throw new InvalidDataException("Scenario info contains no title.");
        }

        if (sections[0].Contains('\n', StringComparison.Ordinal))
        {
            throw new InvalidDataException("Scenario info title must be one line.");
        }

        return new ScenarioInfoDocument(
            sections[0], sections[1], sections[2..], metadata, rawBytes);
    }

    public static byte[] Encode(ScenarioInfoDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        Validate(document);
        if (document.IsUnchanged)
        {
            return document.RawBytes.ToArray();
        }

        return Windows1252.GetBytes(ToCanonicalText(document));
    }

    public static ScenarioInfoDocument Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return Decode(File.ReadAllBytes(path));
    }

    public static void Save(string path, ScenarioInfoDocument document)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        File.WriteAllBytes(path, Encode(document));
    }

    public static string ToCanonicalText(ScenarioInfoDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        Validate(document);
        var parts = new List<string>
        {
            document.Title.Trim(),
            "#",
            CanonicalSection(document.Overview),
        };
        foreach (var section in document.CountrySections)
        {
            parts.Add("#");
            parts.Add(CanonicalSection(section));
        }

        parts.Add("# " + string.Join(
            ' ', document.Metadata.Select(static value => value.ToString(CultureInfo.InvariantCulture))));
        return string.Join('\r', parts) + '\r';
    }

    private static void Validate(ScenarioInfoDocument document)
    {
        if (string.IsNullOrWhiteSpace(document.Title) ||
            document.Title.Contains('\r', StringComparison.Ordinal) ||
            document.Title.Contains('\n', StringComparison.Ordinal))
        {
            throw new InvalidDataException("Scenario info title must be one non-empty line.");
        }

        if (document.CountrySections.Count != ScenarioInfoDocument.CountrySectionCount)
        {
            throw new InvalidDataException("Scenario info requires exactly seven country sections.");
        }

        if (document.Metadata.Count != ScenarioInfoDocument.MetadataValueCount)
        {
            throw new InvalidDataException("Scenario info requires exactly eight metadata integers.");
        }

        foreach (var section in new[] { document.Overview }.Concat(document.CountrySections))
        {
            if (section is null)
            {
                throw new InvalidDataException("Scenario info sections cannot be null.");
            }

            var normalized = section.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
            if (normalized.Split('\n').Any(static line => line.StartsWith('#')))
            {
                throw new InvalidDataException("Scenario info section lines cannot start with '#'.");
            }
        }
    }

    private static string NormalizeSection(IEnumerable<string> lines) =>
        string.Join('\n', lines).Trim();

    private static string CanonicalSection(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Trim()
            .Replace('\n', '\r');

    private static InvalidDataException Error(int zeroBasedLine, string message) =>
        new($"Line {zeroBasedLine + 1}: {message}.");

    private static Encoding CreateEncoding()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(
            1252,
            EncoderFallback.ExceptionFallback,
            DecoderFallback.ExceptionFallback);
    }
}
