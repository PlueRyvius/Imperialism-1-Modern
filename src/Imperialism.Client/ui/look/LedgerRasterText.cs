using Godot;

namespace Imperialism.Client;

/// <summary>Native font pixels uploaded at 1x; canvas scaling never rasterises the font again.</summary>
public partial class LedgerRasterText : Control
{
    private Image? _raster;
    private ImageTexture? _texture;

    public LedgerRasterText()
    {
        TextureFilter = TextureFilterEnum.Nearest;
        MouseFilter = MouseFilterEnum.Ignore;
        ClipContents = true;
    }

    protected void SetRaster(Image image)
    {
        _texture?.Dispose();
        _raster?.Dispose();
        _raster = image;
        _texture = ImageTexture.CreateFromImage(image);
        QueueRedraw();
    }

    internal int NativeInkCount
    {
        get
        {
            var count = 0;
            if (_raster is null) return count;
            for (var y = 0; y < _raster.GetHeight(); y++)
                for (var x = 0; x < _raster.GetWidth(); x++)
                    if (HasNativeInk(x, y)) count++;
            return count;
        }
    }

    internal bool HasNativeInk(int x, int y) => _raster?.GetPixel(x, y).A > 0.5f;

    internal bool UsesWholePixels(int scale)
    {
        var transform = GetGlobalTransform();
        return TextureFilter == TextureFilterEnum.Nearest && _texture is not null &&
            _texture.GetSize() == Size &&
            transform.X == new Vector2(scale, 0) && transform.Y == new Vector2(0, scale) &&
            transform.Origin == transform.Origin.Round();
    }

    public override void _Draw()
    {
        if (_texture is not null) DrawTexture(_texture, Vector2.Zero);
    }

    public static LedgerRasterText AddTitle(Control parent, string name, Rect2 box,
        string text, Font font, int fontSize, Color colour)
    {
        var label = new LedgerRasterText
        {
            Name = name.Replace('.', '_'), Position = box.Position, Size = box.Size,
        };
        label.SetMeta("layout_name", name);
        using var shaped = new ShapedLine(font, fontSize, text);
        var image = Image.CreateEmpty((int)box.Size.X, (int)box.Size.Y, false, Image.Format.Rgba8);
        // Match the existing left-aligned, vertically centred Label's baseline.
        var baseline = Mathf.Floor((box.Size.Y - font.GetHeight(fontSize)) / 2) + font.GetAscent(fontSize);
        var pen = 0f;
        foreach (var glyph in shaped.Glyphs)
        {
            var rid = glyph["font_rid"].AsRid();
            var index = glyph["index"].AsInt32();
            var offset = glyph["offset"].AsVector2();
            var repeat = glyph["repeat"].AsInt32();
            var advance = (float)glyph["advance"].AsDouble();
            for (var i = 0; i < repeat; i++)
            {
                var ink = LedgerGlyph.Read(rid, fontSize, index);
                foreach (var pixel in ink.Pixels)
                    PutInk(image, new Vector2I(Mathf.RoundToInt(pen + offset.X + pixel.X),
                        Mathf.RoundToInt(baseline + offset.Y + pixel.Y)), colour);
                pen += advance;
            }
        }
        label.SetRaster(image);
        parent.AddChild(label);
        return label;
    }

    internal static void PutInk(Image image, Vector2I point, Color colour)
    {
        if (point.X >= 0 && point.Y >= 0 && point.X < image.GetWidth() && point.Y < image.GetHeight())
            image.SetPixelv(point, colour);
    }

    private sealed class ShapedLine : IDisposable
    {
        private readonly TextServer _server = TextServerManager.GetPrimaryInterface();
        private readonly Rid _rid;
        public Godot.Collections.Array<Godot.Collections.Dictionary> Glyphs { get; }

        public ShapedLine(Font font, int size, string text)
        {
            _rid = _server.CreateShapedText();
            _server.ShapedTextAddString(_rid, text, font.GetRids(), size);
            _server.ShapedTextShape(_rid);
            Glyphs = _server.ShapedTextGetGlyphs(_rid);
        }

        public void Dispose() => _server.FreeRid(_rid);
    }
}

/// <summary>Binary ink copied from the existing font's native atlas, never invented glyphs.</summary>
internal sealed record LedgerGlyph(Vector2I[] Pixels, int Left, int Width)
{
    public static LedgerGlyph Read(Rid font, int size, int glyph)
    {
        var server = TextServerManager.GetPrimaryInterface();
        var key = new Vector2I(size, 0);
        server.FontRenderGlyph(font, key, glyph);
        var texture = server.FontGetGlyphTextureIdx(font, key, glyph);
        if (texture < 0) return new LedgerGlyph([], 0, 0);
        var uv = server.FontGetGlyphUVRect(font, key, glyph);
        var offset = server.FontGetGlyphOffset(font, key, glyph);
        using var image = server.FontGetTextureImage(font, key, texture);
        var pixels = new List<Vector2I>();
        var left = int.MaxValue;
        var right = int.MinValue;
        for (var y = (int)uv.Position.Y; y < uv.End.Y; y++)
            for (var x = (int)uv.Position.X; x < uv.End.X; x++)
                if (image.GetPixel(x, y).A > 0.5f)
                {
                    var point = new Vector2I(Mathf.RoundToInt(x - uv.Position.X + offset.X),
                        Mathf.RoundToInt(y - uv.Position.Y + offset.Y));
                    pixels.Add(point);
                    left = Math.Min(left, point.X);
                    right = Math.Max(right, point.X);
                }
        return new LedgerGlyph(pixels.ToArray(), right >= left ? left : 0, right >= left ? right - left + 1 : 0);
    }
}
