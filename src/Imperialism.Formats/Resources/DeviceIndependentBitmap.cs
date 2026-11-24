using System.Buffers.Binary;

namespace Imperialism.Formats.Resources;

/// <summary>
/// A decoded <c>RT_BITMAP</c> resource. The stored payload is a
/// <c>BITMAPINFOHEADER</c>, its palette, and its pixels, with the fourteen-byte
/// file header stripped — that is the resource convention, not a corruption.
/// Rather than synthesise the missing header to produce a <c>.bmp</c>, this
/// decodes straight to top-down RGBA and keeps the palette indices beside it,
/// because resolving the archives' transparency rule needs to reason about
/// indices and not colours.
/// </summary>
public sealed class DeviceIndependentBitmap
{
    private const int InfoHeaderSize = 40;
    private const uint UncompressedRgb = 0;
    private const uint RunLengthEncoded8 = 1;

    private DeviceIndependentBitmap(
        int width,
        int height,
        int bitsPerPixel,
        byte[] palette,
        byte[] paletteIndices,
        byte[] pixels)
    {
        Width = width;
        Height = height;
        BitsPerPixel = bitsPerPixel;
        Palette = palette;
        PaletteIndices = paletteIndices;
        Pixels = pixels;
    }

    public int Width { get; }

    public int Height { get; }

    public int BitsPerPixel { get; }

    /// <summary>The palette as RGBA quadruples, empty for direct-colour images.</summary>
    public byte[] Palette { get; }

    /// <summary>Palette entry count, zero for direct-colour images.</summary>
    public int PaletteCount => Palette.Length / 4;

    /// <summary>One index per pixel in top-down order, empty for direct-colour images.</summary>
    public byte[] PaletteIndices { get; }

    /// <summary>RGBA quadruples in top-down row order. Every pixel is fully opaque.</summary>
    public byte[] Pixels { get; }

    public bool IsPalettized => Palette.Length > 0;

    public static DeviceIndependentBitmap Decode(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < InfoHeaderSize)
        {
            throw new InvalidDataException("The bitmap payload is shorter than its header.");
        }

        var headerSize = BinaryPrimitives.ReadUInt32LittleEndian(payload);
        if (headerSize < InfoHeaderSize || headerSize > payload.Length)
        {
            throw new InvalidDataException($"Unsupported bitmap header size {headerSize}.");
        }

        var width = BinaryPrimitives.ReadInt32LittleEndian(payload[4..]);
        var storedHeight = BinaryPrimitives.ReadInt32LittleEndian(payload[8..]);
        int bitsPerPixel = BinaryPrimitives.ReadUInt16LittleEndian(payload[14..]);
        var compression = BinaryPrimitives.ReadUInt32LittleEndian(payload[16..]);
        var declaredPaletteCount = BinaryPrimitives.ReadUInt32LittleEndian(payload[32..]);

        // Most of the archives are uncompressed, but thirty-four images across
        // pictuniv and pictpaid are run-length encoded. That was found by
        // throwing here first and reading the failures, which is the only reason
        // the count is known rather than assumed.
        if (compression is not (UncompressedRgb or RunLengthEncoded8))
        {
            throw new InvalidDataException($"Unsupported bitmap compression {compression}.");
        }

        if (compression == RunLengthEncoded8 && bitsPerPixel != 8)
        {
            throw new InvalidDataException(
                $"Run-length encoding needs eight bits per pixel, not {bitsPerPixel}.");
        }

        if (width <= 0 || storedHeight == 0)
        {
            throw new InvalidDataException($"Unsupported bitmap dimensions {width}x{storedHeight}.");
        }

        var bottomUp = storedHeight > 0;
        var height = Math.Abs(storedHeight);
        var paletteCount = bitsPerPixel switch
        {
            1 or 4 or 8 => declaredPaletteCount == 0 ? 1 << bitsPerPixel : (int)declaredPaletteCount,
            24 or 32 => (int)declaredPaletteCount,
            _ => throw new InvalidDataException($"Unsupported bitmap depth {bitsPerPixel}."),
        };

        var paletteOffset = (int)headerSize;
        var pixelOffset = paletteOffset + (paletteCount * 4);
        if (pixelOffset > payload.Length)
        {
            throw new InvalidDataException("The bitmap palette runs past the end of the payload.");
        }

        var palette = new byte[paletteCount * 4];
        for (var index = 0; index < paletteCount; index++)
        {
            // Palette entries are stored blue, green, red, reserved. The fourth
            // byte is not alpha and is almost always zero; reading it as alpha
            // makes every image fully transparent.
            var source = paletteOffset + (index * 4);
            palette[(index * 4) + 0] = payload[source + 2];
            palette[(index * 4) + 1] = payload[source + 1];
            palette[(index * 4) + 2] = payload[source + 0];
            palette[(index * 4) + 3] = byte.MaxValue;
        }

        if (compression == RunLengthEncoded8)
        {
            var runIndices = DecodeRunLength(payload[pixelOffset..], width, height, bottomUp);
            return new DeviceIndependentBitmap(
                width,
                height,
                bitsPerPixel,
                palette,
                runIndices,
                Colorize(runIndices, palette, paletteCount));
        }

        // Every row is padded to a four-byte boundary. A one-pixel-wide 8bpp
        // image therefore still occupies four bytes per row, which is the single
        // most common way to decode this format almost correctly.
        var stride = (((width * bitsPerPixel) + 31) / 32) * 4;
        if (pixelOffset + ((long)stride * height) > payload.Length)
        {
            throw new InvalidDataException("The bitmap pixels run past the end of the payload.");
        }

        var isPalettized = bitsPerPixel <= 8;
        var indices = isPalettized ? new byte[width * height] : [];
        var pixels = new byte[width * height * 4];
        for (var row = 0; row < height; row++)
        {
            var sourceRow = bottomUp ? height - 1 - row : row;
            var source = payload.Slice(pixelOffset + (sourceRow * stride), stride);
            for (var column = 0; column < width; column++)
            {
                var target = ((row * width) + column) * 4;
                if (isPalettized)
                {
                    var index = ReadIndex(source, column, bitsPerPixel);
                    if (index >= paletteCount)
                    {
                        throw new InvalidDataException(
                            $"Pixel index {index} is outside a palette of {paletteCount}.");
                    }

                    indices[(row * width) + column] = (byte)index;
                    palette.AsSpan(index * 4, 4).CopyTo(pixels.AsSpan(target, 4));
                    continue;
                }

                var bytesPerPixel = bitsPerPixel / 8;
                var pixel = column * bytesPerPixel;
                pixels[target + 0] = source[pixel + 2];
                pixels[target + 1] = source[pixel + 1];
                pixels[target + 2] = source[pixel + 0];
                pixels[target + 3] = byte.MaxValue;
            }
        }

        return new DeviceIndependentBitmap(width, height, bitsPerPixel, palette, indices, pixels);
    }

    private static byte[] Colorize(byte[] indices, byte[] palette, int paletteCount)
    {
        var pixels = new byte[indices.Length * 4];
        for (var pixel = 0; pixel < indices.Length; pixel++)
        {
            var index = indices[pixel];
            if (index >= paletteCount)
            {
                throw new InvalidDataException(
                    $"Pixel index {index} is outside a palette of {paletteCount}.");
            }

            palette.AsSpan(index * 4, 4).CopyTo(pixels.AsSpan(pixel * 4, 4));
        }

        return pixels;
    }

    /// <summary>
    /// Decodes <c>BI_RLE8</c> into top-down palette indices. Pixels no run ever
    /// reaches keep index zero, which is what the delta and end-of-line escapes
    /// are for: the encoder uses them to skip whole regions rather than encode them.
    /// </summary>
    private static byte[] DecodeRunLength(
        ReadOnlySpan<byte> source,
        int width,
        int height,
        bool bottomUp)
    {
        var indices = new byte[width * height];
        var column = 0;
        var row = 0;
        var position = 0;
        while (position + 1 < source.Length)
        {
            var count = source[position];
            var value = source[position + 1];
            position += 2;

            if (count > 0)
            {
                for (var repeat = 0; repeat < count; repeat++)
                {
                    Write(indices, width, height, bottomUp, row, column++, value);
                }

                continue;
            }

            switch (value)
            {
                case 0:
                    column = 0;
                    row++;
                    break;
                case 1:
                    return indices;
                case 2:
                    if (position + 1 >= source.Length)
                    {
                        return indices;
                    }

                    column += source[position];
                    row += source[position + 1];
                    position += 2;
                    break;
                default:
                    if (position + value > source.Length)
                    {
                        throw new InvalidDataException("A run-length literal runs past the payload.");
                    }

                    for (var literal = 0; literal < value; literal++)
                    {
                        Write(indices, width, height, bottomUp, row, column++, source[position + literal]);
                    }

                    // Literal runs are padded to a two-byte boundary.
                    position += value + (value & 1);
                    break;
            }
        }

        return indices;
    }

    private static void Write(
        byte[] indices,
        int width,
        int height,
        bool bottomUp,
        int row,
        int column,
        byte value)
    {
        if (column < 0 || column >= width || row < 0 || row >= height)
        {
            return;
        }

        var target = bottomUp ? height - 1 - row : row;
        indices[(target * width) + column] = value;
    }

    private static int ReadIndex(ReadOnlySpan<byte> row, int column, int bitsPerPixel) => bitsPerPixel switch
    {
        8 => row[column],
        4 => (column & 1) == 0 ? row[column / 2] >> 4 : row[column / 2] & 0x0F,
        _ => (row[column / 8] >> (7 - (column & 7))) & 1,
    };
}
