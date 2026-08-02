using Godot;
using Imperialism.Core;
using Imperialism.Presentation;

namespace Imperialism.Client;

internal sealed partial class MapInteractionOverlay : Node2D
{
    private static readonly Color SelectionColor = new(1f, 0.84f, 0.24f, 1f);
    private static readonly Color HoverColor = new(1f, 1f, 1f, 0.82f);
    private HexMapProjection? _projection;
    private CellIndex? _hovered;
    private CellIndex? _selected;

    public void Configure(HexMapProjection projection)
    {
        ArgumentNullException.ThrowIfNull(projection);
        _projection = projection;
        _hovered = null;
        _selected = null;
        QueueRedraw();
    }

    public void SetHovered(CellIndex? cell)
    {
        _hovered = cell;
        QueueRedraw();
    }

    public void SetSelected(CellIndex? cell)
    {
        _selected = cell;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_projection is null)
        {
            return;
        }

        DrawHighlight(_projection, _hovered, HoverColor, 2.2f);
        DrawHighlight(_projection, _selected, SelectionColor, 3.4f);
    }

    private void DrawHighlight(
        HexMapProjection projection,
        CellIndex? cell,
        Color color,
        float width)
    {
        if (!cell.HasValue)
        {
            return;
        }

        var coordinate = projection.Dimensions.GetCoordinate(cell.Value);
        for (var index = 0; index < 6; index++)
        {
            DrawLine(
                ToVector(projection.GetVertex(coordinate, index)),
                ToVector(projection.GetVertex(coordinate, (index + 1) % 6)),
                color,
                width,
                true);
        }
    }

    private static Vector2 ToVector(MapPoint point) => new((float)point.X, (float)point.Y);
}
