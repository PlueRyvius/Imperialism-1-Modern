using Godot;
using Imperialism.Core;
using Imperialism.Presentation;

namespace Imperialism.Client;

public sealed partial class WorldMapView : Node2D
{
    private static readonly Color CellOutline = new(0.06f, 0.07f, 0.08f, 0.45f);
    private static readonly Color RegionBorder = new(0.92f, 0.88f, 0.72f, 0.82f);
    private static readonly Color RiverColor = new(0.20f, 0.67f, 0.94f, 0.95f);
    private static readonly Color RailColor = new(0.13f, 0.12f, 0.10f, 0.95f);
    private readonly MultiMeshInstance2D _terrainLayer = new() { Name = "Terrain" };
    private readonly MultiMeshInstance2D _ownershipLayer = new() { Name = "Ownership" };
    private readonly MapStateOverlay _stateLayer = new() { Name = "WorldState" };
    private readonly MapInteractionOverlay _interactionLayer = new() { Name = "Interaction" };
    private MapViewDefinition? _map;
    private HexMapProjection? _projection;
    private CellIndex? _hovered;
    private CellIndex? _selected;
    private bool _debugMode;

    public event Action<CellIndex?>? HoveredChanged;

    public event Action<CellIndex?>? SelectedChanged;

    public HexMapProjection Projection => _projection ??
        throw new InvalidOperationException("The map view has not been configured.");

    public override void _Ready()
    {
        AddChild(_terrainLayer);
        AddChild(_ownershipLayer);
        AddChild(_stateLayer);
        AddChild(_interactionLayer);
        ZIndex = -10;
    }

    public void Configure(MapViewDefinition map, double radius = 32)
    {
        ArgumentNullException.ThrowIfNull(map);
        _map = map;
        _projection = new HexMapProjection(map.Dimensions, radius);
        _hovered = null;
        _selected = null;
        _interactionLayer.Configure(_projection);
        _stateLayer.Configure(map, _projection);

        var mesh = CreateHexMesh(_projection);
        _terrainLayer.Multimesh = CreateInstances(mesh, map, terrain: true);
        _ownershipLayer.Multimesh = CreateInstances(mesh, map, terrain: false);
        ApplyOwnershipOpacity();
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

        var ownership = _ownershipLayer.Multimesh ??
            throw new InvalidOperationException("The map view has not been configured.");
        foreach (var cell in state.Cells)
        {
            ownership.SetInstanceColor(cell.Index.Value, TerrainPalette.Country(cell.OwnerKey, 1));
        }

        _stateLayer.ApplyState(state);
    }

    public void SetDebugMode(bool enabled)
    {
        _debugMode = enabled;
        ApplyOwnershipOpacity();
        QueueRedraw();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_projection is null)
        {
            return;
        }

        if (@event is InputEventMouseMotion)
        {
            SetHovered(_projection.Pick(ToMapPoint(GetLocalMousePosition())));
        }
        else if (@event is InputEventMouseButton mouseButton &&
            mouseButton.Pressed && mouseButton.ButtonIndex == MouseButton.Left)
        {
            _selected = _projection.Pick(ToMapPoint(GetLocalMousePosition()));
            _interactionLayer.SetSelected(_selected);
            SelectedChanged?.Invoke(_selected);
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _Draw()
    {
        if (_map is null || _projection is null)
        {
            return;
        }

        DrawRegionBorders(_map, _projection);
        DrawRivers(_map, _projection);
        DrawSitesAndResources(_map, _projection);
    }

    private static ArrayMesh CreateHexMesh(HexMapProjection projection)
    {
        var vertices = projection.GetVertices(new HexCoord(0, 0));
        var center = projection.GetCenter(new HexCoord(0, 0));
        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        for (var index = 0; index < vertices.Count; index++)
        {
            var next = vertices[(index + 1) % vertices.Count];
            surface.SetColor(Colors.White);
            surface.AddVertex(Vector3.Zero);
            surface.SetColor(Colors.White);
            surface.AddVertex(ToLocalVector(vertices[index], center));
            surface.SetColor(Colors.White);
            surface.AddVertex(ToLocalVector(next, center));
        }

        return (ArrayMesh)surface.Commit();
    }

    private MultiMesh CreateInstances(
        ArrayMesh mesh,
        MapViewDefinition map,
        bool terrain)
    {
        var multiMesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform2D,
            UseColors = true,
            Mesh = mesh,
            InstanceCount = map.Cells.Count,
            VisibleInstanceCount = map.Cells.Count,
        };

        foreach (var cell in map.Cells)
        {
            var center = Projection.GetCenter(cell.Index);
            multiMesh.SetInstanceTransform2D(
                cell.Index.Value,
                new Transform2D(0, new Vector2((float)center.X, (float)center.Y)));
            multiMesh.SetInstanceColor(
                cell.Index.Value,
                terrain
                    ? TerrainPalette.Terrain(cell.TerrainKey)
                    : new Color(0, 0, 0, 0));
        }

        return multiMesh;
    }

    private void ApplyOwnershipOpacity()
    {
        _ownershipLayer.Modulate = new Color(1, 1, 1, _debugMode ? 0.34f : 0.13f);
    }

    private void DrawRegionBorders(MapViewDefinition map, HexMapProjection projection)
    {
        foreach (var cell in map.Cells)
        {
            foreach (var direction in HexDirections.All)
            {
                var edge = GetEdge(direction);
                var hasNeighbor = cell.Coordinate.TryGetNeighbor(
                    direction,
                    map.Dimensions,
                    out var neighborCoordinate);
                if (hasNeighbor)
                {
                    var neighbor = map[map.Dimensions.GetIndex(neighborCoordinate)];
                    if (neighbor.Index.Value < cell.Index.Value)
                    {
                        continue;
                    }

                    if (_debugMode)
                    {
                        DrawLine(
                            ToVector(projection.GetVertex(cell.Coordinate, edge.First)),
                            ToVector(projection.GetVertex(cell.Coordinate, edge.Second)),
                            CellOutline,
                            1);
                    }

                    if (cell.RegionKind == neighbor.RegionKind &&
                        string.Equals(cell.RegionKey, neighbor.RegionKey, StringComparison.Ordinal))
                    {
                        continue;
                    }
                }

                DrawLine(
                    ToVector(projection.GetVertex(cell.Coordinate, edge.First)),
                    ToVector(projection.GetVertex(cell.Coordinate, edge.Second)),
                    RegionBorder,
                    hasNeighbor ? 2.6f : 3.2f,
                    true);
            }
        }
    }

    private void DrawRivers(MapViewDefinition map, HexMapProjection projection)
    {
        foreach (var cell in map.Cells)
        {
            if (cell.River is not { } river)
            {
                continue;
            }

            var first = projection.GetRiverEndpoint(cell.Index, river.First);
            var second = projection.GetRiverEndpoint(cell.Index, river.Second);
            DrawLine(ToVector(first), ToVector(second), RiverColor, 4.4f, true);
            DrawRiverTerminal(first, river.First, projection.Radius);
            DrawRiverTerminal(second, river.Second, projection.Radius);
        }
    }

    private void DrawRiverTerminal(MapPoint point, RiverEndpoint endpoint, double radius)
    {
        if (endpoint == RiverEndpoint.Source)
        {
            DrawCircle(ToVector(point), (float)(radius * 0.11), RiverColor);
        }
        else if (endpoint == RiverEndpoint.Mouth)
        {
            DrawArc(ToVector(point), (float)(radius * 0.16), 0, Mathf.Tau, 18, RiverColor, 2.4f, true);
        }
    }

    private void DrawSitesAndResources(MapViewDefinition map, HexMapProjection projection)
    {
        foreach (var cell in map.Cells)
        {
            var center = ToVector(projection.GetCenter(cell.Index));
            if (cell.SettlementSite == SettlementSiteKind.Urban)
            {
                DrawCircle(center, (float)(projection.Radius * 0.17), new Color(0.93f, 0.89f, 0.78f));
                DrawArc(center, (float)(projection.Radius * 0.17), 0, Mathf.Tau, 20, RailColor, 2, true);
            }

            if (_debugMode)
            {
                for (var index = 0; index < cell.ResourceKeys.Count; index++)
                {
                    var angle = (Mathf.Tau * index / Math.Max(1, cell.ResourceKeys.Count)) - (Mathf.Pi / 2);
                    var offset = Vector2.FromAngle(angle) * (float)(projection.Radius * 0.42);
                    DrawCircle(center + offset, 3.4f, TerrainPalette.Resource(cell.ResourceKeys[index]));
                }
            }
        }
    }

    private void SetHovered(CellIndex? value)
    {
        if (_hovered == value)
        {
            return;
        }

        _hovered = value;
        _interactionLayer.SetHovered(value);
        HoveredChanged?.Invoke(value);
    }

    private static (int First, int Second) GetEdge(HexDirection direction) => direction switch
    {
        HexDirection.NorthEast => (0, 1),
        HexDirection.East => (1, 2),
        HexDirection.SouthEast => (2, 3),
        HexDirection.SouthWest => (3, 4),
        HexDirection.West => (4, 5),
        HexDirection.NorthWest => (5, 0),
        _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, "Unknown direction."),
    };

    private static MapPoint ToMapPoint(Vector2 point) => new(point.X, point.Y);

    private static Vector2 ToVector(MapPoint point) => new((float)point.X, (float)point.Y);

    private static Vector3 ToLocalVector(MapPoint point, MapPoint center) =>
        new((float)(point.X - center.X), (float)(point.Y - center.Y), 0);
}
