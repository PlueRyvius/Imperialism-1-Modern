using System.Globalization;
using System.Text;

namespace Imperialism.Formats;

public static class ScenarioTextCodec
{
    private static readonly Encoding StrictAscii = Encoding.GetEncoding(
        "us-ascii",
        EncoderFallback.ExceptionFallback,
        DecoderFallback.ExceptionFallback);

    public static ScenarioDocument Decode(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        EnsureAscii(text, "text");
        var normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        var records = new List<ScenarioRecord>();
        var lines = normalized.Split('\n');
        for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            var line = lines[lineIndex].Trim();
            if (line.Length == 0)
            {
                continue;
            }

            var position = 0;
            var tag = ReadToken(line, ref position);
            if (!ScenarioFormat.FieldCounts.TryGetValue(tag, out var fieldCount))
            {
                throw Error(lineIndex, $"unknown tag '{tag}'");
            }

            var fields = new uint[fieldCount];
            for (var fieldIndex = 0; fieldIndex < fieldCount; fieldIndex++)
            {
                var token = ReadToken(line, ref position);
                if (token.Length == 0)
                {
                    throw Error(
                        lineIndex,
                        $"tag '{tag}' expects {fieldCount} integer fields" +
                        (ScenarioFormat.NameTags.Contains(tag) ? " followed by a name" : string.Empty));
                }

                if (!uint.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out fields[fieldIndex]))
                {
                    throw Error(
                        lineIndex,
                        $"field {fieldIndex + 1} for tag '{tag}' is not a uint32 decimal integer: '{token}'");
                }
            }

            SkipWhitespace(line, ref position);
            string? name = null;
            if (ScenarioFormat.NameTags.Contains(tag))
            {
                name = line[position..].TrimEnd();
                if (name.Length == 0)
                {
                    throw Error(lineIndex, $"tag '{tag}' requires a name");
                }
            }
            else if (position < line.Length)
            {
                var actual = fieldCount + CountTokens(line[position..]);
                throw Error(
                    lineIndex,
                    $"tag '{tag}' expects {fieldCount} integer fields, got {actual}");
            }

            records.Add(new ScenarioRecord(tag, fields, name));
        }

        return new ScenarioDocument(records);
    }

    public static string Encode(ScenarioDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var builder = new StringBuilder();
        foreach (var record in document.Records)
        {
            LegacyScenarioCodec.ValidateRecord(record);
            EnsureAscii(record.Tag, "tag");
            builder.Append(record.Tag);
            foreach (var field in record.Fields)
            {
                builder.Append(' ').Append(field.ToString(CultureInfo.InvariantCulture));
            }

            if (ScenarioFormat.NameTags.Contains(record.Tag))
            {
                var name = record.Name!.Trim();
                EnsureAscii(name, "name");
                if (name.Contains('\r', StringComparison.Ordinal) ||
                    name.Contains('\n', StringComparison.Ordinal))
                {
                    throw new InvalidDataException($"Name for tag '{record.Tag}' cannot contain a newline.");
                }

                builder.Append(' ').Append(name);
            }

            builder.Append('\r');
        }

        return builder.ToString();
    }

    public static ScenarioDocument Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        // Decode strictly: silently replacing a non-ASCII byte with '?' would
        // defeat Decode's own EnsureAscii guard. A non-ASCII byte is evidence
        // the format needs further research, not something to mangle.
        return Decode(StrictAscii.GetString(File.ReadAllBytes(path)));
    }

    public static void Save(string path, ScenarioDocument document)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        File.WriteAllBytes(path, Encoding.ASCII.GetBytes(Encode(document)));
    }

    private static string ReadToken(string line, ref int position)
    {
        SkipWhitespace(line, ref position);
        var start = position;
        while (position < line.Length && !char.IsWhiteSpace(line[position]))
        {
            position++;
        }

        return line[start..position];
    }

    private static void SkipWhitespace(string line, ref int position)
    {
        while (position < line.Length && char.IsWhiteSpace(line[position]))
        {
            position++;
        }
    }

    private static int CountTokens(string text) =>
        text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;

    private static InvalidDataException Error(int zeroBasedLine, string message) =>
        new($"Line {zeroBasedLine + 1}: {message}.");

    private static void EnsureAscii(string value, string description)
    {
        if (value.Any(static character => character > 0x7f))
        {
            throw new InvalidDataException($"Scenario {description} must contain only ASCII characters.");
        }
    }
}
