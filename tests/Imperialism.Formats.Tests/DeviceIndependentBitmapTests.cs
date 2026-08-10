using System.Buffers.Binary;
using Imperialism.Formats.Resources;
using Xunit;

namespace Imperialism.Formats.Tests;

public sealed class DeviceIndependentBitmapTests
{
    [Fact]
    public void RowsArePaddedToFourBytesAndStoredBottomUp()
    {
        // Three eight-bit pixels occupy four bytes per row. Reading three would
        // shear the image by one pixel per row, which looks like art rather than
        // a bug.
        var payload = Palettized(
            width: 3,
            height: 2,
            bottomUp: true,
            rows: [[1, 2, 3, 0xFF], [4, 5, 6, 0xFF]]);

        var bitmap = DeviceIndependentBitmap.Decode(payload);

        Assert.Equal(3, bitmap.Width);
        Assert.Equal(2, bitmap.Height);
        Assert.Equal<byte[]>([4, 5, 6, 1, 2, 3], bitmap.PaletteIndices);
    }

    [Fact]
    public void ANegativeHeightStoresRowsTopDown()
    {
        var payload = Palettized(3, 2, bottomUp: false, rows: [[1, 2, 3, 0xFF], [4, 5, 6, 0xFF]]);

        var bitmap = DeviceIndependentBitmap.Decode(payload);

        Assert.Equal(2, bitmap.Height);
        Assert.Equal<byte[]>([1, 2, 3, 4, 5, 6], bitmap.PaletteIndices);
    }

    [Fact]
    public void ASinglePixelStillOccupiesAWholePaddedRow()
    {
        var payload = Palettized(1, 1, bottomUp: true, rows: [[9, 0xFF, 0xFF, 0xFF]]);

        var bitmap = DeviceIndependentBitmap.Decode(payload);

        Assert.Equal(1, bitmap.Width);
        Assert.Equal<byte[]>([9], bitmap.PaletteIndices);
    }

    [Fact]
    public void PaletteEntriesAreBlueGreenRedAndTheFourthByteIsNotAlpha()
    {
        var payload = Palettized(1, 1, bottomUp: true, rows: [[1, 0, 0, 0]]);

        var bitmap = DeviceIndependentBitmap.Decode(payload);

        // Entry one is written below as blue=0x30, green=0x20, red=0x10 with a
        // zero reserved byte. Reading that byte as alpha makes every image
        // invisible.
        Assert.Equal<byte[]>([0x10, 0x20, 0x30, 0xFF], bitmap.Pixels);
    }

    [Fact]
    public void AnIndexOutsideAShortPaletteIsRejected()
    {
        // A header may declare fewer than the full 1 << bpp entries. A pixel
        // beyond that is corrupt data, not a colour to guess at.
        var payload = new byte[40 + (16 * 4) + 4];
        WriteHeader(payload, 1, 1, compression: 0);
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(32), 16);
        payload[40 + (16 * 4)] = 200;

        var exception = Assert.Throws<InvalidDataException>(() => DeviceIndependentBitmap.Decode(payload));

        Assert.Contains("palette of 16", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RunLengthEncodedImagesDecodeIncludingSkippedRegions()
    {
        // Thirty-four images in the shipped archives are run-length encoded. The
        // encoder uses the end-of-line escape to leave the rest of a row alone,
        // so untouched pixels have to stay at index zero rather than repeat.
        var pixels = new byte[]
        {
            3, 1,       // three pixels of index 1
            0, 0,       // end of line, leaving the fourth pixel alone
            0, 3, 2, 2, 2, 0,   // absolute run of three index-2 pixels, padded to a word
            0, 1,       // end of bitmap
        };
        var payload = Compressed(width: 4, height: 2, pixels);

        var bitmap = DeviceIndependentBitmap.Decode(payload);

        // Stored bottom-up, so the first encoded row is the last image row.
        Assert.Equal<byte[]>([2, 2, 2, 0, 1, 1, 1, 0], bitmap.PaletteIndices);
    }

    [Fact]
    public void AnUnsupportedCompressionIsRejectedRatherThanGuessed()
    {
        var payload = Palettized(1, 1, bottomUp: true, rows: [[0, 0, 0, 0]]);
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(16), 3);

        var exception = Assert.Throws<InvalidDataException>(() => DeviceIndependentBitmap.Decode(payload));

        Assert.Contains("compression 3", exception.Message, StringComparison.Ordinal);
    }

    private static byte[] Palettized(int width, int height, bool bottomUp, byte[][] rows)
    {
        var stride = ((width * 8) + 31) / 32 * 4;
        var payload = new byte[40 + (256 * 4) + (stride * height)];
        WriteHeader(payload, width, bottomUp ? height : -height, compression: 0);
        WritePalette(payload);
        for (var row = 0; row < rows.Length; row++)
        {
            rows[row].AsSpan(0, stride).CopyTo(payload.AsSpan(40 + (256 * 4) + (row * stride)));
        }

        return payload;
    }

    private static byte[] Compressed(int width, int height, byte[] pixels)
    {
        var payload = new byte[40 + (256 * 4) + pixels.Length];
        WriteHeader(payload, width, height, compression: 1);
        WritePalette(payload);
        pixels.CopyTo(payload.AsSpan(40 + (256 * 4)));
        return payload;
    }

    private static void WriteHeader(byte[] payload, int width, int height, uint compression)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(payload, 40);
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(4), width);
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(8), height);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(12), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(14), 8);
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(16), compression);
    }

    private static void WritePalette(byte[] payload)
    {
        for (var index = 0; index < 256; index++)
        {
            payload[40 + (index * 4) + 0] = (byte)(index * 0x30);
            payload[40 + (index * 4) + 1] = (byte)(index * 0x20);
            payload[40 + (index * 4) + 2] = (byte)(index * 0x10);
            payload[40 + (index * 4) + 3] = 0;
        }
    }
}
