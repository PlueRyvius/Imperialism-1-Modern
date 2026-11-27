using Godot;

namespace Imperialism.Client;

/// <summary>Antialiased faces, confined to the measured boxes inside a scaled ledger.</summary>
internal static class LedgerText
{
    public static Label Add(Control parent, string layoutName, Rect2 box, string text,
        Font font, int fontSize, Color colour, HorizontalAlignment alignment = HorizontalAlignment.Left, bool shadow = false)
    {
        var label = new Label
        {
            Name = layoutName.Replace('.', '_'), Position = box.Position, Size = box.Size,
            Text = text, HorizontalAlignment = alignment, VerticalAlignment = VerticalAlignment.Center,
            ClipText = true, TextureFilter = CanvasItem.TextureFilterEnum.Linear, MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        label.SetMeta("layout_name", layoutName);
        label.AddThemeFontOverride("font", font);
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", colour);
        label.AddThemeColorOverride("font_shadow_color", shadow ? Colors.Black : Colors.Transparent);
        label.AddThemeConstantOverride("shadow_offset_x", 1);
        label.AddThemeConstantOverride("shadow_offset_y", 1);
        label.AddThemeConstantOverride("outline_size", 0);
        parent.AddChild(label);
        return label;
    }
    // Align actual glyph ink, rather than font ascent boxes, to the compose script.
    public static void PlaceInk(Label label, int scale, Vector2 anchor, bool right = false)
    {
        if (string.IsNullOrEmpty(label.Text)) return;
        var (minimum, maximum) = InkBounds(label, scale);
        label.Position += anchor - new Vector2(right ? maximum.X : minimum.X, minimum.Y);
    }

    public static (Vector2 Minimum, Vector2 Maximum) InkBounds(Label label, int scale, float threshold = 0.5f)
    {
        // Shape at final resolution with oversampling 1, as in the independent
        // atlas probe. Read live Label text, variation axes, position and metrics.
        var liveFont = label.GetThemeFont("font");
        Font font;
        if (liveFont is FontVariation variation)
        {
            var copy = (FontVariation)variation.Duplicate();
            var face = (FontFile)copy.BaseFont.Duplicate();
            face.Oversampling = 1;
            copy.BaseFont = face;
            font = copy;
        }
        else
        {
            var face = (FontFile)liveFont.Duplicate();
            face.Oversampling = 1;
            font = face;
        }
        var size = label.GetThemeFontSize("font_size");
        var ts = TextServerManager.GetPrimaryInterface();
        var shaped = ts.CreateShapedText();
        try
        {
            ts.ShapedTextAddString(shaped, label.Text, font.GetRids(), size * scale);
            ts.ShapedTextShape(shaped);
            var baseline = label.Position + new Vector2(0,
                Mathf.Floor((label.Size.Y - liveFont.GetHeight(size)) / 2) + liveFont.GetAscent(size));
            var advance = liveFont.GetStringSize(label.Text, fontSize: size).X;
            baseline.X += label.HorizontalAlignment switch
            {
                HorizontalAlignment.Right => label.Size.X - advance,
                HorizontalAlignment.Center => (label.Size.X - advance) / 2,
                _ => 0,
            };
            var minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            var maximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            var pen = 0f;
            var cacheSize = new Vector2I(size * scale, 0);
            foreach (var glyph in ts.ShapedTextGetGlyphs(shaped))
            {
                var rid = glyph["font_rid"].AsRid();
                var index = glyph["index"].AsInt32();
                ts.FontRenderGlyph(rid, cacheSize, index);
                var uv = ts.FontGetGlyphUVRect(rid, cacheSize, index);
                var offset = ts.FontGetGlyphOffset(rid, cacheSize, index);
                var texture = ts.FontGetGlyphTextureIdx(rid, cacheSize, index);
                if (texture >= 0)
                {
                    var atlas = ts.FontGetTextureImage(rid, cacheSize, texture);
                    var logical = ts.FontGetGlyphSize(rid, cacheSize, index);
                    for (var y = 0; y < (int)uv.Size.Y; y++)
                    for (var x = 0; x < (int)uv.Size.X; x++)
                    {
                        if (atlas.GetPixel((int)uv.Position.X + x, (int)uv.Position.Y + y).A < threshold) continue;
                        var point = baseline + new Vector2(pen, 0) / scale +
                            (glyph["offset"].AsVector2() +
                             offset + new Vector2(x, y) * (logical / uv.Size)) / scale;
                        minimum = minimum.Min(point);
                        maximum = maximum.Max(point);
                    }
                }
                pen += glyph["advance"].AsSingle() * glyph["repeat"].AsInt32();
            }
            return (minimum, maximum);
        }
        finally
        {
            ts.FreeRid(shaped);
        }
    }
}
