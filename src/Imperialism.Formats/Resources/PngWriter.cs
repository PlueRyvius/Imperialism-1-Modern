using System.Buffers.Binary;
using System.IO.Compression;

namespace Imperialism.Formats.Resources;

/// <summary>
/// Writes eight-bit RGBA images as PNG.
/// </summary>
/// <remarks>
/// Determinism is a requirement rather than a nicety here: the extracted art is
/// committed, so re-running the extractor has to leave the working tree clean or
/// every run churns the repository. That is why the filter is fixed at None on
/// every scanline and the compression level is pinned rather than chosen.
/// </remarks>
public static class PngWriter
{
    private static readonly byte[] Signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly uint[] CrcTable = BuildCrcTable();

    public static byte[] Encode(int width, int height, ReadOnlySpan<byte> rgba)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        if (rgba.Length != width * height * 4)
        {
            throw new ArgumentException(
                $"An {width}x{height} image needs {width * height * 4} bytes, not {rgba.Length}.",
                nameof(rgba));
        }

        var header = new byte[13];
        BinaryPrimitives.WriteUInt32BigEndian(header, (uint)width);
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(4), (uint)height);
        header[8] = 8;      // bit depth
        header[9] = 6;      // colour type: truecolour with alpha
        header[10] = 0;     // compression: deflate
        header[11] = 0;     // filter: adaptive
        header[12] = 0;     // interlace: none

        var raw = new byte[height * ((width * 4) + 1)];
        for (var row = 0; row < height; row++)
        {
            var target = row * ((width * 4) + 1);
            raw[target] = 0;
            rgba.Slice(row * width * 4, width * 4).CopyTo(raw.AsSpan(target + 1));
        }

        using var compressed = new MemoryStream();
        using (var deflate = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
        {
            deflate.Write(raw);
        }

        using var output = new MemoryStream();
        output.Write(Signature);
        WriteChunk(output, "IHDR"u8, header);
        WriteChunk(output, "IDAT"u8, compressed.GetBuffer().AsSpan(0, (int)compressed.Length));
        WriteChunk(output, "IEND"u8, ReadOnlySpan<byte>.Empty);
        return output.ToArray();
    }

    private static void WriteChunk(Stream output, ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(length, (uint)data.Length);
        output.Write(length);
        output.Write(type);
        output.Write(data);

        var crc = Crc(type, data);
        Span<byte> checksum = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(checksum, crc);
        output.Write(checksum);
    }

    private static uint Crc(ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        var crc = 0xFFFF_FFFFu;
        foreach (var value in type)
        {
            crc = CrcTable[(crc ^ value) & 0xFF] ^ (crc >> 8);
        }

        foreach (var value in data)
        {
            crc = CrcTable[(crc ^ value) & 0xFF] ^ (crc >> 8);
        }

        return crc ^ 0xFFFF_FFFFu;
    }

    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (var index = 0u; index < table.Length; index++)
        {
            var value = index;
            for (var bit = 0; bit < 8; bit++)
            {
                value = (value & 1) != 0 ? 0xEDB8_8320u ^ (value >> 1) : value >> 1;
            }

            table[index] = value;
        }

        return table;
    }
}
