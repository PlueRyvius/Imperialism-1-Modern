using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Imperialism.Content;

public static class WorldContentCodec
{
    public const string FormatName = "imperialism-world";
    public const int CurrentVersion = 20;
    public const string FileExtension = ".iworld";

    /// <summary>
    /// The manual's "on or within one tile of" gathering catchment. Shared by
    /// the version 3 migration and the legacy importer so a converted world and
    /// an upgraded one agree. See <c>docs/formulas/extraction.md</c>.
    /// </summary>
    public const int DefaultCatchmentRadius = 1;

    /// <summary>
    /// One unit of fish per turn from each coast or river tile beside a port.
    /// </summary>
    public const long DefaultPortFishYieldPerWaterTile = 1;

    // The manual's Resource Development Table, indexed by development level.
    // These are transcribed, not modelled: the progression is linear and its
    // slope differs per deposit, which is why there is no single formula here.
    // See docs/reference/manual-mechanics.md.

    /// <summary>Grain, fruit, livestock, cotton, wool and timber.</summary>
    public static readonly long[] CultivatedYieldByDevelopmentLevel = [1, 2, 3, 4];

    /// <summary>Coal, iron and oil: nothing until dug, then two per level.</summary>
    public static readonly long[] HeavyMineralYieldByDevelopmentLevel = [0, 2, 4, 6];

    /// <summary>Gold and gems: nothing until dug, then one per level.</summary>
    public static readonly long[] PreciousMineralYieldByDevelopmentLevel = [0, 1, 2, 3];

    /// <summary>
    /// Fish and horses. No civilian unit improves either, so the curve stops at
    /// the level every tile starts on.
    /// </summary>
    public static readonly long[] UnimprovableYieldByDevelopmentLevel = [1];

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
