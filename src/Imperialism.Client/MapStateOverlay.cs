using Godot;
using Imperialism.Presentation;

namespace Imperialism.Client;

internal sealed partial class MapStateOverlay : Node2D
{
    private static readonly Color RailColor = new(0.13f, 0.12f, 0.10f, 0.95f);
    private static readonly Color RailBedColor = new(0.88f, 0.81f, 0.64f, 0.96f);
    private static readonly Color CapitalColor = new(1f, 0.84f, 0.24f, 1f);
    private MapViewDefinition? _map;
    private WorldViewState? _state;
    private HexMapProjection? _projection;

    public void Configure(MapViewDefinition map, HexMapProjection projection)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(projection);
        _map = map;
        _projection = projection;
        _state = null;
        QueueRedraw();
    }

    public void ApplyState(WorldViewState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (_map is null ||
            !string.Equals(state.MapKey, _map.MapKey, StringComparison.Ordinal) ||
            state.Cells.Count != _map.Cells.Count)
        {
            throw new ArgumentException("World view state does not match the configured map.", nameof(state));
        }

        _state = state;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_state is null || _projection is null)
        {
            return;
        }

        foreach (var rail in _state.Rails)
        {
            var first = ToVector(_projection.GetCenter(rail.First));
            var second = ToVector(_projection.GetCenter(rail.Second));
            DrawLine(first, second, RailBedColor, 6.2f, true);
            DrawLine(first, second, RailColor, 2.3f, true);
        }

        foreach (var cell in _state.Cells)
        {
            if (!cell.CapitalCountry.HasValue)
            {
                continue;
            }

            var center = ToVector(_projection.GetCenter(cell.Index));
            DrawArc(
                center,
                (float)(_projection.Radius * 0.27),
                0,
                Mathf.Tau,
                24,
                CapitalColor,
                3,
                true);
        }
    }

    private static Vector2 ToVector(MapPoint point) => new((float)point.X, (float)point.Y);
}
