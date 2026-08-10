using Imperialism.Formats.Resources;

namespace Imperialism.AssetExtractor;

/// <summary>
/// Montages of every bitmap in an archive with its resource name burned in.
/// </summary>
/// <remarks>
/// The archives name their images with bare numbers, so nothing in the tree says
/// what <c>10000.BMP</c> depicts. Building that catalogue is human work, and
/// this is the tool for it: a sheet on one screen and the manual's figures on
/// the other. Sheets are working output and are never committed.
/// </remarks>
internal static class ContactSheet
{
    private const int Columns = 10;
    private const int Rows = 10;
    private const int CellSize = 128;
    private const int LabelHeight = 8;
    private const int Padding = 4;

    internal sealed record Tile(string Name, DeviceIndependentBitmap Bitmap);

    public static IEnumerable<byte[]> Build(IReadOnlyList<Tile> tiles)
    {
        ArgumentNullException.ThrowIfNull(tiles);
        var perSheet = Columns * Rows;
        for (var start = 0; start < tiles.Count; start += perSheet)
        {
            yield return BuildSheet(tiles.Skip(start).Take(perSheet).ToArray());
        }
    }

    private static byte[] BuildSheet(IReadOnlyList<Tile> tiles)
    {
        var cell = CellSize + LabelHeight + Padding;
        var width = Columns * cell;
        var height = Rows * cell;
        var pixels = new byte[width * height * 4];
        for (var index = 0; index < pixels.Length; index += 4)
        {
            pixels[index + 0] = 24;
            pixels[index + 1] = 24;
            pixels[index + 2] = 28;
            pixels[index + 3] = byte.MaxValue;
        }

        for (var index = 0; index < tiles.Count; index++)
        {
            var originX = (index % Columns) * cell;
            var originY = (index / Columns) * cell;
            DrawScaled(pixels, width, tiles[index].Bitmap, originX, originY + LabelHeight);
            DrawLabel(pixels, width, tiles[index].Name, originX, originY);
        }

        return PngWriter.Encode(width, height, pixels);
    }

    private static void DrawScaled(
        byte[] pixels,
        int surfaceWidth,
        DeviceIndependentBitmap bitmap,
        int originX,
        int originY)
    {
        // Nearest neighbour, aspect preserved. These are browsing thumbnails, and
        // a smoothed one hides exactly the dithering we are trying to look at.
        var scale = Math.Max(1, Math.Max(
            (bitmap.Width + CellSize - 1) / CellSize,
            (bitmap.Height + CellSize - 1) / CellSize));
        var drawWidth = Math.Min(CellSize, bitmap.Width / scale);
        var drawHeight = Math.Min(CellSize, bitmap.Height / scale);
        for (var row = 0; row < drawHeight; row++)
        {
            for (var column = 0; column < drawWidth; column++)
            {
                var source = (((row * scale) * bitmap.Width) + (column * scale)) * 4;
                var target = (((originY + row) * surfaceWidth) + originX + column) * 4;
                bitmap.Pixels.AsSpan(source, 4).CopyTo(pixels.AsSpan(target, 4));
            }
        }
    }

    private static void DrawLabel(byte[] pixels, int surfaceWidth, string text, int originX, int originY)
    {
        var column = 0;
        foreach (var character in text)
        {
            var glyph = Glyph(character);
            for (var row = 0; row < 5; row++)
            {
                for (var bit = 0; bit < 3; bit++)
                {
                    if ((glyph[row] & (1 << (2 - bit))) == 0)
                    {
                        continue;
                    }

                    var x = originX + (column * 4) + bit;
                    var y = originY + 1 + row;
                    if (x >= surfaceWidth)
                    {
                        continue;
                    }

                    var target = ((y * surfaceWidth) + x) * 4;
                    pixels[target + 0] = 255;
                    pixels[target + 1] = 220;
                    pixels[target + 2] = 140;
                }
            }

            column++;
        }
    }

    private static byte[] Glyph(char character) => character switch
    {
        '0' => [0b111, 0b101, 0b101, 0b101, 0b111],
        '1' => [0b010, 0b110, 0b010, 0b010, 0b111],
        '2' => [0b111, 0b001, 0b111, 0b100, 0b111],
        '3' => [0b111, 0b001, 0b111, 0b001, 0b111],
        '4' => [0b101, 0b101, 0b111, 0b001, 0b001],
        '5' => [0b111, 0b100, 0b111, 0b001, 0b111],
        '6' => [0b111, 0b100, 0b111, 0b101, 0b111],
        '7' => [0b111, 0b001, 0b010, 0b010, 0b010],
        '8' => [0b111, 0b101, 0b111, 0b101, 0b111],
        '9' => [0b111, 0b101, 0b111, 0b001, 0b111],
        '.' => [0b000, 0b000, 0b000, 0b000, 0b010],
        'B' => [0b110, 0b101, 0b110, 0b101, 0b110],
        'M' => [0b101, 0b111, 0b111, 0b101, 0b101],
        'P' => [0b111, 0b101, 0b111, 0b100, 0b100],
        _ => [0b000, 0b000, 0b000, 0b000, 0b000],
    };
}
