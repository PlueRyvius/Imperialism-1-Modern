namespace Imperialism.Formats;

/// <summary>Lossless codec for the headerless Imperialism 1 map format.</summary>
public static class LegacyMapCodec
{
    public static MapDocument Decode(
        ReadOnlySpan<byte> bytes,
        MapFormatProfile? profile = null)
    {
        profile ??= MapFormatProfile.Imperialism1;
        if (bytes.Length != profile.FileSize)
        {
            throw new InvalidDataException(
                $"Expected {profile.FileSize} bytes for a {profile.Width}x{profile.Height} " +
                $"map, got {bytes.Length}.");
        }

        var cells = new HexCell[profile.CellCount];
        var offset = 0;
        for (var index = 0; index < cells.Length; index++)
        {
            cells[index] = HexCell.Decode(bytes.Slice(offset, HexCell.Size));
            offset += HexCell.Size;
        }

        return new MapDocument(profile, cells, bytes[offset..]);
    }

    public static byte[] Encode(MapDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var bytes = new byte[document.Profile.FileSize];
        var offset = 0;
        foreach (var cell in document.Cells)
        {
            cell.WriteTo(bytes.AsSpan(offset, HexCell.Size));
            offset += HexCell.Size;
        }

        document.TrailerBytes.Span.CopyTo(bytes.AsSpan(offset));
        return bytes;
    }

    public static MapDocument Load(string path, MapFormatProfile? profile = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return Decode(File.ReadAllBytes(path), profile);
    }

    public static void Save(string path, MapDocument document)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        File.WriteAllBytes(path, Encode(document));
    }
}
