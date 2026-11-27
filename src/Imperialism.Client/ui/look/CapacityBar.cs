using Godot;
using Imperialism.Presentation;

namespace Imperialism.Client;

/// <summary>The ledger's shared-capacity strip; values are supplied by Presentation.</summary>
public sealed partial class CapacityBar : Control
{
    private readonly Texture2D _strip = GD.Load<Texture2D>("res://art/transport/4019.png");
    private TransportPreview? _view;
    private Label? _figure;
    private Label? _name;
    private Font? _font;
    private FontVariation? _figureFont;

    public void Build(Font font)
    {
        Name = "CapacityRow";
        SetMeta("layout_name", "capacity.row");
        Position = new Vector2(325, 397);
        Size = new Vector2(224, 34);
        _font = font;
        _figureFont = new FontVariation
        {
            BaseFont = font,
            VariationOpentype = new Godot.Collections.Dictionary { ["wght"] = 700 },
        };
        _name = LedgerText.Add(this, "capacity.name", new Rect2(92, 21.5f, 70, 14), "Capacity used",
            font, LedgerSlider.PreferredTextSize, new Color("5a3e24"));
        _figure = LedgerText.Add(this, "capacity.figure", new Rect2(162, 21.5f, 42, 14), "",
            _figureFont, LedgerSlider.PreferredTextSize, new Color("341e0e"), HorizontalAlignment.Right);
    }

    public void Apply(TransportPreview view)
    {
        _view = view;
        _figure!.Text = $"{view.Used} / {view.Capacity?.ToString() ?? "∞"}";
        var size = LedgerSlider.FitTextSize(_font!, _figureFont!, _name!.Text, _figure.Text, 112);
        var width = _figureFont!.GetStringSize(_figure.Text, fontSize: size).X;
        var height = Math.Max(_font!.GetHeight(size), _figureFont.GetHeight(size));
        _name.AddThemeFontSizeOverride("font_size", size);
        _figure.AddThemeFontSizeOverride("font_size", size);
        _name.Size = new Vector2(112 - width - LedgerSlider.TextGap, height);
        _figure.Position = new Vector2(204 - width, 21.5f);
        _figure.Size = new Vector2(width, height);
        QueueRedraw();
    }

    public override void _Draw()
    {
        DrawTexture(_strip, Vector2.Zero);
        // Geometry only: the share's length, never a new game quantity.
        var share = _view?.Capacity is > 0
            ? Math.Clamp((double)_view.Used / _view.Capacity.Value, 0, 1) : 0;
        DrawRect(new Rect2(93, 13, (float)(113 * share), 4), new Color("fa0500"));
    }
}
