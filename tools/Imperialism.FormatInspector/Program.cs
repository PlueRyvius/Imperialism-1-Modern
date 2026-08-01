using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Imperialism.Formats;

var options = Options.Parse(args);
var summaries = options.Paths.Select(path => Summarize(path, options)).ToArray();
Console.WriteLine(JsonSerializer.Serialize(
    summaries,
    new JsonSerializerOptions { WriteIndented = options.Pretty }));

static Dictionary<string, object?> Summarize(string path, Options options)
{
    var extension = Path.GetExtension(path).ToLowerInvariant();
    return extension switch
    {
        ".map" => SummarizeMap(path, options),
        ".scn" => SummarizeScenario(path, false),
        ".inf" => SummarizeInfo(path),
        "" => SummarizeScenario(path, true),
        _ => throw new InvalidDataException($"Unsupported file extension '{extension}'."),
    };
}

static Dictionary<string, object?> SummarizeMap(string path, Options options)
{
    var profile = new MapFormatProfile(
        options.Width,
        options.Height,
        options.TrailerRecordCount,
        options.TrailerRecordSize);
    var document = LegacyMapCodec.Load(path, profile);
    var encodedCells = document.Cells.Select(static cell => cell.Encode()).ToArray();
    var fieldHashes = new SortedDictionary<string, string>(StringComparer.Ordinal);
    for (var fieldOffset = 0; fieldOffset < HexCell.Size; fieldOffset++)
    {
        var values = new byte[document.Cells.Count];
        for (var cellIndex = 0; cellIndex < document.Cells.Count; cellIndex++)
        {
            values[cellIndex] = encodedCells[cellIndex][fieldOffset];
        }

        fieldHashes[$"byte_{fieldOffset:D2}"] = Hash(values);
    }

    return new Dictionary<string, object?>
    {
        ["type"] = "map",
        ["path"] = Path.GetFullPath(path),
        ["width"] = document.Width,
        ["height"] = document.Height,
        ["cell_count"] = document.Cells.Count,
        ["field_hashes"] = fieldHashes,
        ["preserved_hashes"] = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["encoded_bytes"] = Hash(LegacyMapCodec.Encode(document)),
            ["source_bytes"] = Hash(File.ReadAllBytes(path)),
            ["trailer_bytes"] = Hash(document.TrailerBytes.Span),
        },
    };
}

static Dictionary<string, object?> SummarizeScenario(string path, bool plaintext)
{
    var document = plaintext ? ScenarioTextCodec.Load(path) : LegacyScenarioCodec.Load(path);
    var tagCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
    foreach (var group in document.Records.GroupBy(static record => record.Tag, StringComparer.Ordinal))
    {
        tagCounts[group.Key] = group.Count();
    }

    var preserved = new SortedDictionary<string, object?>(StringComparer.Ordinal)
    {
        ["trailing_bytes"] = Hash(document.TrailingBytes),
        ["raw_name_fields"] = document.Records
            .Where(static record => record.RawNameField.Length > 0)
            .Select(static record => Hash(record.RawNameField.Span))
            .ToArray(),
    };
    if (!plaintext)
    {
        preserved["encoded_bytes"] = Hash(LegacyScenarioCodec.Encode(document));
        preserved["source_bytes"] = Hash(File.ReadAllBytes(path));
    }

    return new Dictionary<string, object?>
    {
        ["type"] = plaintext ? "scenario_text" : "scenario",
        ["path"] = Path.GetFullPath(path),
        ["record_count"] = document.Records.Count,
        ["tag_counts"] = tagCounts,
        ["record_hashes"] = document.Records.Select(HashRecord).ToArray(),
        ["preserved_hashes"] = preserved,
    };
}

static Dictionary<string, object?> SummarizeInfo(string path)
{
    var document = ScenarioInfoCodec.Load(path);
    var sections = new SortedDictionary<string, string>(StringComparer.Ordinal)
    {
        ["overview"] = HashText(document.Overview),
        ["title"] = HashText(document.Title),
    };
    for (var index = 0; index < document.CountrySections.Count; index++)
    {
        sections[$"country_{index}"] = HashText(document.CountrySections[index]);
    }

    return new Dictionary<string, object?>
    {
        ["type"] = "scenario_info",
        ["path"] = Path.GetFullPath(path),
        ["section_hashes"] = sections,
        ["metadata"] = document.Metadata,
        ["preserved_hashes"] = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["encoded_bytes"] = Hash(ScenarioInfoCodec.Encode(document)),
            ["raw_bytes"] = Hash(document.RawBytes.Span),
        },
    };
}

static string HashRecord(ScenarioRecord record)
{
    using var stream = new MemoryStream();
    stream.Write(Encoding.ASCII.GetBytes(record.Tag));
    Span<byte> fieldBytes = stackalloc byte[4];
    foreach (var value in record.Fields)
    {
        BinaryPrimitives.WriteUInt32BigEndian(fieldBytes, value);
        stream.Write(fieldBytes);
    }

    var nameBytes = record.Name is null ? [] : Encoding.UTF8.GetBytes(record.Name);
    BinaryPrimitives.WriteUInt32BigEndian(fieldBytes, checked((uint)nameBytes.Length));
    stream.Write(fieldBytes);
    stream.Write(nameBytes);
    return Hash(stream.ToArray());
}

static string HashText(string value) => Hash(Encoding.UTF8.GetBytes(value));

static string Hash(ReadOnlySpan<byte> bytes) =>
    Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

internal sealed record Options(
    int Width,
    int Height,
    int TrailerRecordCount,
    int TrailerRecordSize,
    bool Pretty,
    string[] Paths)
{
    public static Options Parse(string[] args)
    {
        var legacyProfile = MapFormatProfile.Imperialism1;
        var width = legacyProfile.Width;
        var height = legacyProfile.Height;
        var trailerCount = legacyProfile.TrailerRecordCount;
        var trailerSize = legacyProfile.TrailerRecordSize;
        var pretty = false;
        var paths = new List<string>();
        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--width":
                    width = ParseInteger(args, ref index);
                    break;
                case "--height":
                    height = ParseInteger(args, ref index);
                    break;
                case "--trailer-count":
                    trailerCount = ParseInteger(args, ref index);
                    break;
                case "--trailer-size":
                    trailerSize = ParseInteger(args, ref index);
                    break;
                case "--pretty":
                    pretty = true;
                    break;
                default:
                    paths.Add(args[index]);
                    break;
            }
        }

        if (paths.Count == 0)
        {
            throw new ArgumentException("At least one source path is required.");
        }

        return new Options(width, height, trailerCount, trailerSize, pretty, paths.ToArray());
    }

    private static int ParseInteger(string[] args, ref int index)
    {
        if (++index >= args.Length || !int.TryParse(args[index], out var value))
        {
            throw new ArgumentException($"Option '{args[index - 1]}' requires an integer.");
        }

        return value;
    }
}
