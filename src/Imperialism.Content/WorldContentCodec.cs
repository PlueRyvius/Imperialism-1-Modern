using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Imperialism.Content;

public static class WorldContentCodec
{
    public const string FormatName = "imperialism-world";
    public const int CurrentVersion = 1;
    public const string FileExtension = ".iworld";

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
        var document = Deserialize(bytes);
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
        WorldContentCompiler.Compile(Deserialize(bytes));

    public static CompiledWorldContent DecodeAndCompile(
        ReadOnlySpan<byte> bytes,
        string scenarioKey) =>
        WorldContentCompiler.Compile(Deserialize(bytes), scenarioKey);

    public static CompiledWorldPackage DecodeAndCompilePackage(ReadOnlySpan<byte> bytes) =>
        WorldContentCompiler.CompilePackage(Deserialize(bytes));

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
