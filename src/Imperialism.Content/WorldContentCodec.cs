using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Imperialism.Content;

public static class WorldContentCodec
{
    public const string FormatName = "imperialism-world";
    public const int CurrentVersion = 5;
    public const string FileExtension = ".iworld";

    /// <summary>
    /// The manual's "on or within one tile of" gathering catchment. Shared by
    /// the version 3 migration and the legacy importer so a converted world and
    /// an upgraded one agree. See <c>docs/formulas/extraction.md</c>.
    /// </summary>
    public const int DefaultCatchmentRadius = 1;

    /// <summary>
    /// A surface deposit — field, orchard, forest, pasture — yields one per turn
    /// with no improvement at all, and doubles with each level a worker builds.
    /// </summary>
    public static readonly long[] SurfaceYieldByDevelopmentLevel = [1, 2, 4, 8];

    /// <summary>
    /// A subsurface deposit — coal, iron, oil, gold, gems — gives nothing until
    /// it has been dug, then starts at two and doubles like the rest.
    /// </summary>
    public static readonly long[] SubsurfaceYieldByDevelopmentLevel = [0, 2, 4, 8];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        WriteIndented = true,
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false),
        },
    };

    public static WorldContentDocument Decode(ReadOnlySpan<byte> bytes)
    {
        var document = DeserializeCurrent(bytes);
        _ = WorldContentCompiler.CompilePackage(document);
        return document;
    }

    public static byte[] Encode(WorldContentDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        _ = WorldContentCompiler.CompilePackage(document);
        var json = JsonSerializer.SerializeToUtf8Bytes(document, JsonOptions);
        var carriageReturns = json.Count(static value => value == (byte)'\r');
        var canonical = new byte[json.Length - carriageReturns + 1];
        var destination = 0;
        foreach (var value in json)
        {
            if (value != (byte)'\r')
            {
                canonical[destination++] = value;
            }
        }

        canonical[destination] = (byte)'\n';
        return canonical;
    }

    public static WorldContentDocument Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return Decode(File.ReadAllBytes(path));
    }

    public static void Save(string path, WorldContentDocument document)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        File.WriteAllBytes(path, Encode(document));
    }

    public static CompiledWorldContent DecodeAndCompile(ReadOnlySpan<byte> bytes) =>
        WorldContentCompiler.Compile(DeserializeCurrent(bytes));

    public static CompiledWorldContent DecodeAndCompile(
        ReadOnlySpan<byte> bytes,
        string scenarioKey) =>
        WorldContentCompiler.Compile(DeserializeCurrent(bytes), scenarioKey);

    public static CompiledWorldPackage DecodeAndCompilePackage(ReadOnlySpan<byte> bytes) =>
        WorldContentCompiler.CompilePackage(DeserializeCurrent(bytes));

    private static WorldContentDocument DeserializeCurrent(ReadOnlySpan<byte> bytes) =>
        WorldContentMigrator.ToCurrent(Deserialize(bytes));

    private static WorldContentDocument Deserialize(ReadOnlySpan<byte> bytes)
    {
        WorldContentDocument document;
        try
        {
            document = JsonSerializer.Deserialize<WorldContentDocument>(bytes, JsonOptions)
                ?? throw new ContentValidationException("$", "Document cannot be null.");
        }
        catch (JsonException exception)
        {
            throw new ContentValidationException(
                exception.Path ?? "$",
                exception.Message,
                exception);
        }

        return document;
    }
}
