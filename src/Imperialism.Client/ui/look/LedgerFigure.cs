using Godot;

namespace Imperialism.Client;

/// <summary>Seven-pixel Antiqua digits, rasterised once at 1x and enlarged as pixels.</summary>
public sealed partial class LedgerFigure : Control
{
    private const int FontSize = 10;
    private readonly FontFile _font;
    private string _text = "";

    public string Text
    {
        get => _text;
        set { _text = value; QueueRedraw(); }
    }

    public LedgerFigure()
    {
        // A private copy keeps the screen's scale-dependent oversampling and the
        // other labels' smoothing out of these figures. No new font asset is used.
        _font = (FontFile)GD.Load<FontFile>("res://art/fonts/antiqua.ttf").Duplicate();
        _font.Antialiasing = TextServer.FontAntialiasing.None;
        _font.Hinting = TextServer.Hinting.None;
        _font.SubpixelPositioning = TextServer.SubpixelPositioning.Disabled;
        _font.Oversampling = 1;
        TextureFilter = TextureFilterEnum.Nearest;
        MouseFilter = MouseFilterEnum.Ignore;
        ClipContents = true;
    }

    public static LedgerFigure Add(Control parent, string layoutName, Rect2 box)
    {
        var figure = new LedgerFigure
        {
            Name = layoutName.Replace('.', '_'), Position = box.Position, Size = box.Size,
        };
        figure.SetMeta("layout_name", layoutName);
        parent.AddChild(figure);
        return figure;
    }

    private readonly Dictionary<char, (int Left, int Width)> _ink = new();

    private (int Left, int Width) Ink(char character)
    {
        if (_ink.TryGetValue(character, out var cached)) return cached;
        _font.RenderRange(0, new Vector2I(FontSize, 0), character, character);
        var glyph = (int)TextServerManager.GetPrimaryInterface().FontGetGlyphIndex(
            _font.GetRids()[0], FontSize, character, 0);
        var size = new Vector2I(FontSize, 0);
        var uv = _font.GetGlyphUVRect(0, size, glyph);
        var offset = _font.GetGlyphOffset(0, size, glyph);
        var texture = _font.GetGlyphTextureIdx(0, size, glyph);
        var left = int.MaxValue;
        var right = int.MinValue;
        if (texture >= 0)
        {
            using var image = _font.GetTextureImage(0, size, texture);
            for (var y = (int)uv.Position.Y; y < uv.End.Y; y++)
                for (var x = (int)uv.Position.X; x < uv.End.X; x++)
                    if (image.GetPixel(x, y).A > 0.5f)
                    {
                        left = Math.Min(left, x);
                        right = Math.Max(right, x);
                    }
        }
        var result = right >= left
            ? ((int)(left - uv.Position.X + offset.X), right - left + 1) : (0, 0);
        _ink.Add(character, result);
        return result;
    }

    public override void _Draw()
    {
        // Space ink boxes, rather than font advances: two page pixels between
        // digits and six on each side of the slash (D14). Values remain untouched.
        var characters = _text.Where(c => !char.IsWhiteSpace(c)).ToArray();
        var width = 0;
        for (var i = 0; i < characters.Length; i++)
        {
            if (i > 0) width += characters[i] == '/' || characters[i - 1] == '/' ? 6 : 2;
            width += Ink(characters[i]).Width;
        }
        var pen = Size.X - width;
        for (var i = 0; i < characters.Length; i++)
        {
            if (i > 0) pen += characters[i] == '/' || characters[i - 1] == '/' ? 6 : 2;
            var ink = Ink(characters[i]);
            DrawString(_font, new Vector2(pen - ink.Left, 7), characters[i].ToString(),
                fontSize: FontSize, modulate: Colors.Black);
            pen += ink.Width;
        }
    }
}
