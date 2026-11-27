using Godot;

namespace Imperialism.Client;

/// <summary>Seven-pixel Antiqua digits, rasterised once at 1x and enlarged as pixels.</summary>
public sealed partial class LedgerFigure : LedgerRasterText
{
    private const int FontSize = 10;
    private readonly FontFile _font;
    private string _text = "";

    public string Text
    {
        get => _text;
        set { if (_text == value) return; _text = value; Rasterise(); }
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

    private readonly Dictionary<char, LedgerGlyph> _ink = new();

    private LedgerGlyph Ink(char character)
    {
        if (_ink.TryGetValue(character, out var cached)) return cached;
        _font.RenderRange(0, new Vector2I(FontSize, 0), character, character);
        var rid = _font.GetRids()[0];
        var glyph = (int)TextServerManager.GetPrimaryInterface().FontGetGlyphIndex(rid, FontSize, character, 0);
        var ink = LedgerGlyph.Read(rid, FontSize, glyph);
        _ink.Add(character, ink);
        return ink;
    }

    private void Rasterise()
    {
        // Preserve D14's spacing and right alignment, now in a native pixel image.
        var characters = _text.Where(c => !char.IsWhiteSpace(c)).ToArray();
        var width = 0;
        for (var i = 0; i < characters.Length; i++)
        {
            if (i > 0) width += characters[i] == '/' || characters[i - 1] == '/' ? 6 : 2;
            width += Ink(characters[i]).Width;
        }
        var image = Image.CreateEmpty((int)Size.X, (int)Size.Y, false, Image.Format.Rgba8);
        var pen = (int)Size.X - width;
        for (var i = 0; i < characters.Length; i++)
        {
            if (i > 0) pen += characters[i] == '/' || characters[i - 1] == '/' ? 6 : 2;
            var ink = Ink(characters[i]);
            foreach (var pixel in ink.Pixels)
                PutInk(image, new Vector2I(pen + pixel.X - ink.Left, 7 + pixel.Y), Colors.Black);
            pen += ink.Width;
        }
        SetRaster(image);
    }
}
