using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using Imperialism.Formats.Resources;
using Xunit;

namespace Imperialism.Formats.Tests;

public sealed class PngWriterTests
{
    [Fact]
    public void EncodingTheSameImageTwiceProducesIdenticalBytes()
    {
        // The extracted art is committed, so a writer that varies run to run
        // would leave the working tree dirty after every extraction.
        var pixels = Gradient(7, 5);

        Assert.Equal(PngWriter.Encode(7, 5, pixels), PngWriter.Encode(7, 5, pixels));
    }

    [Fact]
    public void TheHeaderDeclaresEightBitTruecolourWithAlpha()
    {
        var png = PngWriter.Encode(3, 2, Gradient(3, 2));

        Assert.Equal<byte[]>([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A], png[..8]);
        Assert.Equal("IHDR", Encoding.ASCII.GetString(png, 12, 4));
        Assert.Equal(3u, BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(16)));
        Assert.Equal(2u, BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(20)));
        Assert.Equal(8, png[24]);   // bit depth
        Assert.Equal(6, png[25]);   // colour type: truecolour with alpha
    }

    [Fact]
    public void ThePixelsSurviveTheRoundTripUnfiltered()
    {
        var pixels = Gradient(4, 3);
        var png = PngWriter.Encode(4, 3, pixels);

        var raw = Inflate(FindChunk(png, "IDAT"));

        for (var row = 0; row < 3; row++)
        {
            var start = row * ((4 * 4) + 1);
            Assert.Equal(0, raw[start]);   // filter type None on every scanline
            Assert.Equal(
                pixels.AsSpan(row * 4 * 4, 4 * 4).ToArray(),
                raw.AsSpan(start + 1, 4 * 4).ToArray());
        }
    }

    [Fact]
    public void AMismatchedPixelBufferIsRejected()
    {
        Assert.Throws<ArgumentException>(() => PngWriter.Encode(2, 2, new byte[15]));
    }

    private static byte[] Gradient(int width, int height)
    {
        var pixels = new byte[width * height * 4];
        for (var index = 0; index < pixels.Length; index++)
        {
            pixels[index] = (byte)(index * 7);
        }

        return pixels;
    }

    private static byte[] FindChunk(byte[] png, string type)
    {
        var offset = 8;
        while (offset < png.Length)
        {
            var length = (int)BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(offset));
            var name = Encoding.ASCII.GetString(png, offset + 4, 4);
            if (string.Equals(name, type, StringComparison.Ordinal))
            {
                return png.AsSpan(offset + 8, length).ToArray();
            }

            offset += length + 12;
        }

        throw new InvalidOperationException($"No {type} chunk.");
    }

    private static byte[] Inflate(byte[] compressed)
    {
        using var source = new MemoryStream(compressed);
        using var zlib = new ZLibStream(source, CompressionMode.Decompress);
        using var target = new MemoryStream();
        zlib.CopyTo(target);
        return target.ToArray();
    }
}
