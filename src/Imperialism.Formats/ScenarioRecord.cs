using System.Text;

namespace Imperialism.Formats;

public sealed class ScenarioRecord
{
    private static readonly Encoding StrictAscii = Encoding.GetEncoding(
        "us-ascii",
        EncoderFallback.ExceptionFallback,
        DecoderFallback.ExceptionFallback);

    private readonly byte[]? _rawNameField;
    private readonly string? _originalName;

    public ScenarioRecord(string tag, IEnumerable<uint> fields, string? name = null)
        : this(tag, fields, name, null)
    {
    }

    internal ScenarioRecord(
        string tag,
        IEnumerable<uint> fields,
        string? name,
        byte[]? rawNameField)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);
        ArgumentNullException.ThrowIfNull(fields);
        Tag = tag;
        Fields = fields.ToList();
        Name = name;
        _originalName = name;
        _rawNameField = rawNameField?.ToArray();
    }

    public string Tag { get; }

    public IList<uint> Fields { get; }

    public string? Name { get; set; }

    public ReadOnlyMemory<byte> RawNameField => _rawNameField ?? ReadOnlyMemory<byte>.Empty;

    internal byte[] EncodeNameField()
    {
        if (_rawNameField is not null && Name == _originalName)
        {
            return _rawNameField.ToArray();
        }

        var name = Name ?? string.Empty;
        byte[] nameBytes;
        try
        {
            nameBytes = StrictAscii.GetBytes(name);
        }
        catch (EncoderFallbackException exception)
        {
            throw new InvalidDataException($"Name '{name}' must be ASCII.", exception);
        }

        var encoded = new byte[ScenarioFormat.NameFieldSize];
        nameBytes.AsSpan(0, Math.Min(nameBytes.Length, encoded.Length)).CopyTo(encoded);
        return encoded;
    }
}
