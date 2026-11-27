using Godot;
using Imperialism.Presentation;

namespace Imperialism.Client;

/// <summary>The ledger's shared-capacity strip; values are supplied by Presentation.</summary>
public sealed partial class CapacityBar : Control
{
    private readonly Texture2D _strip = GD.Load<Texture2D>("res://art/transport/4019.png");
    private TransportPreview? _view;
    private LedgerFigure? _figure;

    public void Build(Font font)
    {
        Name = "CapacityRow";
        SetMeta("layout_name", "capacity.row");
        Position = new Vector2(325, 397);
        Size = new Vector2(224, 34);
        _figure = LedgerFigure.Add(this, "capacity.figure", new Rect2(174, 24, 48, 9));
    }

    public void Apply(TransportPreview view)
    {
        _view = view;
        _figure!.Text = $"{view.Used} / {view.Capacity?.ToString() ?? "∞"}";
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
