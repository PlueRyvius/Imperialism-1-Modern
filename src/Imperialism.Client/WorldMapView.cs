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
    private static readonly Color CapitalColor = new(1f, 0.84f, 0.24f, 1f);
    private readonly MultiMeshInstance2D _terrainLayer = new() { Name = "Terrain" };
    private readonly MultiMeshInstance2D _ownershipLayer = new() { Name = "Ownership" };
    private readonly MapInteractionOverlay _interactionLayer = new() { Name = "Interaction" };
    private MapViewSnapshot? _snapshot;
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
        AddChild(_interactionLayer);
        ZIndex = -10;
    }

    public void Configure(MapViewSnapshot snapshot, double radius = 32)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _snapshot = snapshot;
        _projection = new HexMapProjection(snapshot.Dimensions, radius);
        _hovered = null;
        _selected = null;
        _interactionLayer.Configure(_projection);

        var mesh = CreateHexMesh(_projection);
        _terrainLayer.Multimesh = CreateInstances(mesh, snapshot, ownership: false);
        _ownershipLayer.Multimesh = CreateInstances(mesh, snapshot, ownership: true);
        ApplyOwnershipOpacity();
        QueueRedraw();
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
        if (_snapshot is null || _projection is null)
        {
            return;
        }

        DrawRegionBorders(_snapshot, _projection);
        DrawRivers(_snapshot, _projection);
        DrawRails(_snapshot, _projection);
        DrawSitesAndResources(_snapshot, _projection);
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
        MapViewSnapshot snapshot,
        bool ownership)
    {
        var multiMesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform2D,
            UseColors = true,
            Mesh = mesh,
            InstanceCount = snapshot.Cells.Count,
            VisibleInstanceCount = snapshot.Cells.Count,
        };

        foreach (var cell in snapshot.Cells)
        {
            var center = Projection.GetCenter(cell.Index);
            multiMesh.SetInstanceTransform2D(
                cell.Index.Value,
                new Transform2D(0, new Vector2((float)center.X, (float)center.Y)));
            multiMesh.SetInstanceColor(
                cell.Index.Value,
                ownership
                    ? TerrainPalette.Country(cell.OwnerKey, 1)
                    : TerrainPalette.Terrain(cell.TerrainKey));
        }

        return multiMesh;
    }

    private void ApplyOwnershipOpacity()
    {
        _ownershipLayer.Modulate = new Color(1, 1, 1, _debugMode ? 0.34f : 0.13f);
    }

    private void DrawRegionBorders(MapViewSnapshot snapshot, HexMapProjection projection)
    {
        foreach (var cell in snapshot.Cells)
        {
            foreach (var direction in HexDirections.All)
            {
                var edge = GetEdge(direction);
                var hasNeighbor = cell.Coordinate.TryGetNeighbor(
                    direction,
                    snapshot.Dimensions,
                    out var neighborCoordinate);
                if (hasNeighbor)
                {
                    var neighbor = snapshot[snapshot.Dimensions.GetIndex(neighborCoordinate)];
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

    private void DrawRivers(MapViewSnapshot snapshot, HexMapProjection projection)
    {
        foreach (var cell in snapshot.Cells)
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

    private void DrawRails(MapViewSnapshot snapshot, HexMapProjection projection)
    {
        foreach (var rail in snapshot.Rails)
        {
            var first = ToVector(projection.GetCenter(rail.First));
            var second = ToVector(projection.GetCenter(rail.Second));
            DrawLine(first, second, new Color(0.88f, 0.81f, 0.64f, 0.96f), 6.2f, true);
            DrawLine(first, second, RailColor, 2.3f, true);
        }
    }

    private void DrawSitesAndResources(MapViewSnapshot snapshot, HexMapProjection projection)
    {
        foreach (var cell in snapshot.Cells)
        {
            var center = ToVector(projection.GetCenter(cell.Index));
            if (cell.SettlementSite == SettlementSiteKind.Urban)
            {
                DrawCircle(center, (float)(projection.Radius * 0.17), new Color(0.93f, 0.89f, 0.78f));
                DrawArc(center, (float)(projection.Radius * 0.17), 0, Mathf.Tau, 20, RailColor, 2, true);
            }

            if (cell.CapitalCountry.HasValue)
            {
                DrawArc(center, (float)(projection.Radius * 0.27), 0, Mathf.Tau, 24, CapitalColor, 3, true);
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
