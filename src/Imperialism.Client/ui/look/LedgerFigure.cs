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
        _font.Hinting = TextServer.Hinting.Normal;
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

    public override void _Draw()
    {
        // At size 10 the hinted digits occupy baseline-7 through baseline-1.
        // Floor the spare vertical pixel; the row's eight-pixel box stays intact.
        var width = _font.GetStringSize(_text, fontSize: FontSize).X;
        var baseline = new Vector2(Mathf.Round(Size.X - width), 7 + Mathf.Floor((Size.Y - 7) / 2));
        DrawString(_font, baseline, _text, fontSize: FontSize, modulate: Colors.Black);
    }
}
