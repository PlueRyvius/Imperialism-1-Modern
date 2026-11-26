using Godot;
using Imperialism.Core;
using Imperialism.Presentation;

namespace Imperialism.Client;

/// <summary>Two cached draw batches, rebuilt on preview changes, never per frame.</summary>
internal sealed partial class MapCivilianOverlay : Node2D
{
    private readonly List<(CivilianPreview Unit, Vector2 Center)> _markers = [];
    private readonly List<(UnitTargetView Target, Rect2 Bounds)> _targets = [];
    private CivilianUnitId? _selected;
    private Font? _font;

    public void Apply(HexMapProjection projection, IReadOnlyList<CivilianPreview> civilians,
        UnitOrdersView? selected, Font font)
    {
        _font = font;
        _selected = selected?.Civilian.Unit;
        _markers.Clear();
        _targets.Clear();
        foreach (var group in civilians.GroupBy(unit => unit.Cell))
        {
            var center = ToVector(projection.GetCenter(group.Key));
            var units = group.ToArray();
            for (var i = 0; i < units.Length; i++)
            {
                // Marker layout only; no game figures are derived here.
                _markers.Add((units[i], center + new Vector2((i - (units.Length - 1) / 2f) * 14, -23)));
            }
        }

        if (selected is not null)
        {
            foreach (var group in selected.Targets.GroupBy(target => target.Cell))
            {
                var center = ToVector(projection.GetCenter(group.Key));
                var row = 0;
                foreach (var target in group)
                {
                    _targets.Add((target, new Rect2(center + new Vector2(-23, -13 + row * 10), new Vector2(46, 10))));
                    row++;
                }
            }
        }

        QueueRedraw();
    }

    public CivilianPreview? PickUnit(Vector2 position) =>
        _markers.LastOrDefault(marker => marker.Center.DistanceTo(position) <= 7).Unit;

    public UnitTargetView? PickTarget(Vector2 position) =>
        _targets.FirstOrDefault(row => row.Bounds.HasPoint(position)).Target;

    public override void _Draw()
    {
        if (_font is null)
        {
            return;
        }

        foreach (var (target, bounds) in _targets)
        {
            var color = target.Warning is null ? new Color(0.72f, 0.92f, 0.75f) : new Color(1f, 0.69f, 0.46f);
            DrawRect(bounds, new Color(0.08f, 0.10f, 0.08f, 0.91f));
            if (target.IsDrafted)
            {
                DrawRect(bounds, new Color(1f, 0.84f, 0.24f), false, 1.5f);
            }

            var text = $"{(target.Warning is null ? "" : "!")}{target.Verb}";
            DrawString(_font, bounds.Position + new Vector2(2, 8), text, fontSize: 9, modulate: color);
        }

        foreach (var (unit, center) in _markers)
        {
            DrawCircle(center, 7, new Color(0.08f, 0.10f, 0.08f));
            var color = unit.IsBusy ? new Color(0.67f, 0.65f, 0.61f) : new Color(0.90f, 0.87f, 0.73f);
            DrawCircle(center, 5, color);
            // Busy has a cross; a drafted order has a gold square. Both can coexist.
            if (unit.IsBusy)
            {
                DrawLine(center + new Vector2(-3, -3), center + new Vector2(3, 3), Colors.Black, 1.5f);
                DrawLine(center + new Vector2(-3, 3), center + new Vector2(3, -3), Colors.Black, 1.5f);
            }

            if (unit.Order is not null)
            {
                DrawRect(new Rect2(center - new Vector2(6, 6), new Vector2(12, 12)), new Color(1f, 0.84f, 0.24f), false, 2);
            }

            if (unit.Unit == _selected)
            {
                DrawArc(center, 8, 0, Mathf.Tau, 20, Colors.White, 2, true);
            }
        }
    }

    private static Vector2 ToVector(MapPoint point) => new((float)point.X, (float)point.Y);
}
