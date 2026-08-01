using System.Buffers.Binary;
using System.Text;

namespace Imperialism.Formats;

public static class LegacyScenarioCodec
{
    public static ScenarioDocument Decode(ReadOnlySpan<byte> bytes)
    {
        var records = new List<ScenarioRecord>();
        var offset = 0;
        while (offset < bytes.Length)
        {
            if (bytes.Length - offset < 4)
            {
                throw new InvalidDataException($"Truncated scenario tag at offset {offset}.");
            }

            var tagBytes = bytes.Slice(offset, 4);
            if (tagBytes.ContainsAnyExceptInRange((byte)0x20, (byte)0x7e))
            {
                throw new InvalidDataException($"Invalid scenario tag at offset {offset}.");
            }

            var tag = Encoding.ASCII.GetString(tagBytes);
            offset += 4;
            if (tag == "TERM")
            {
                return new ScenarioDocument(records, bytes[offset..]);
            }

            if (!ScenarioFormat.FieldCounts.TryGetValue(tag, out var fieldCount))
            {
                throw new InvalidDataException($"Unknown scenario tag '{tag}' at offset {offset - 4}.");
            }

            var fieldByteCount = checked(fieldCount * sizeof(uint));
            if (bytes.Length - offset < fieldByteCount)
            {
                throw new InvalidDataException($"Truncated fields for tag '{tag}' at offset {offset}.");
            }

            var fields = new uint[fieldCount];
            for (var index = 0; index < fields.Length; index++)
            {
                fields[index] = BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(offset, 4));
                offset += 4;
            }

            string? name = null;
            byte[]? rawNameField = null;
            if (ScenarioFormat.NameTags.Contains(tag))
            {
                if (bytes.Length - offset < ScenarioFormat.NameFieldSize)
                {
                    throw new InvalidDataException($"Truncated name for tag '{tag}' at offset {offset}.");
                }

                rawNameField = bytes.Slice(offset, ScenarioFormat.NameFieldSize).ToArray();
                offset += ScenarioFormat.NameFieldSize;
                var terminator = Array.IndexOf(rawNameField, (byte)0);
                var nameLength = terminator < 0 ? rawNameField.Length : terminator;
                name = Encoding.ASCII.GetString(rawNameField, 0, nameLength);
            }

            records.Add(new ScenarioRecord(tag, fields, name, rawNameField));
        }

        throw new InvalidDataException("Scenario is missing its terminating TERM tag.");
    }

    public static byte[] Encode(ScenarioDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        using var stream = new MemoryStream();
        Span<byte> fieldBytes = stackalloc byte[4];
        foreach (var record in document.Records)
        {
            ValidateRecord(record);
            stream.Write(Encoding.ASCII.GetBytes(record.Tag));
            foreach (var value in record.Fields)
            {
                BinaryPrimitives.WriteUInt32BigEndian(fieldBytes, value);
                stream.Write(fieldBytes);
            }

            if (ScenarioFormat.NameTags.Contains(record.Tag))
            {
                stream.Write(record.EncodeNameField());
            }
        }

        stream.Write("TERM"u8);
        stream.Write(document.TrailingBytes);
        return stream.ToArray();
    }

    public static ScenarioDocument Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return Decode(File.ReadAllBytes(path));
    }

    public static void Save(string path, ScenarioDocument document)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        File.WriteAllBytes(path, Encode(document));
    }

    internal static void ValidateRecord(ScenarioRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (!ScenarioFormat.FieldCounts.TryGetValue(record.Tag, out var expected))
        {
            throw new InvalidDataException($"Unknown scenario tag '{record.Tag}'.");
        }

        if (record.Fields.Count != expected)
        {
            throw new InvalidDataException(
                $"Tag '{record.Tag}' expects {expected} fields, got {record.Fields.Count}.");
        }

        if (ScenarioFormat.NameTags.Contains(record.Tag) && string.IsNullOrWhiteSpace(record.Name))
        {
            throw new InvalidDataException($"Tag '{record.Tag}' requires a name.");
        }
    }
}
