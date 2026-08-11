using Godot;
using Imperialism.Core;

namespace Imperialism.Client;

/// <summary>
/// The map and its inspector. The manual calls this the central screen: "Each
/// turn begins and ends on the Terrain Map screen."
/// </summary>
/// <remarks>
/// The map layers are built in code rather than authored as a scene, because
/// their children are decided by cell count rather than by layout. The chrome
/// around them is a scene for the opposite reason.
/// </remarks>
public sealed partial class TerrainMapScreen : Control, IShellScreen
{
    private GameSession? _session;
    private WorldMapView? _mapView;
    private MapCameraController? _camera;
    private SubViewport? _viewport;
    private RichTextLabel? _cellInfo;
    private CheckButton? _debugToggle;
    private Button? _endTurn;
    private CellIndex? _hovered;
    private CellIndex? _selected;

    public TerrainMapScreen()
    {
        Name = "TerrainMapScreen";
        SetAnchorsPreset(LayoutPreset.FullRect);
    }

    /// <summary>
    /// Raised when the player commits the turn. The screen does not resolve it
    /// and does not know a report exists; modality is the shell's business.
    /// </summary>
    public event Action? TurnEndRequested;

    public string Title => "Terrain Map";

    /// <summary>The map projection, for the smoke gate's picking check.</summary>
    public WorldMapView? MapView => _mapView;

    public void Enter(GameSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        var isFirstEntry = _session is null;
        _session = session;
        if (isFirstEntry)
        {
            Build(session);
            session.Refreshed += ApplyState;
        }

        ApplyState();
    }

    public void Exit()
    {
    }

    /// <summary>
    /// Cycles a province's owner and one rail link. A development harness rather
    /// than a gameplay command: it proves mutable Core state reaches the
    /// ownership layer and the status border without any orders existing yet.
    /// </summary>
    public bool ApplyDebugStateProbe()
    {
        if (_session is null)
        {
            return false;
        }

        var state = _session.World;
        var world = state.Definition;
        if (world.Map.Provinces.Count > 0 && world.Countries.Count > 0)
        {
            var province = FindProbeProvince(state);
            var owner = state.GetProvinceOwner(province);
            CountryId? nextOwner = owner.HasValue && owner.Value.Value + 1 < world.Countries.Count
                ? new CountryId(owner.Value.Value + 1)
                : owner.HasValue
                    ? null
                    : new CountryId(0);
            state.SetProvinceOwner(province, nextOwner);
        }

        if (world.Scenario.InitialRailLinks.Count > 0)
        {
            var rail = world.Scenario.InitialRailLinks[0];
            if (!state.RemoveRail(rail))
            {
                state.BuildRail(rail);
            }
        }

        _session.Refresh();
        return true;
    }

    private static ProvinceId FindProbeProvince(WorldState state)
    {
        var capitalProvinces = new HashSet<ProvinceId>();
        foreach (var country in state.Definition.Countries)
        {
            var capital = state.GetCountryCapital(country.Id);
            if (capital.HasValue)
            {
                capitalProvinces.Add(state.Definition.Map[capital.Value].Region.Province);
            }
        }

        return state.Definition.Map.Provinces
            .Select(static province => province.Id)
            .FirstOrDefault(province => !capitalProvinces.Contains(province));
    }

    private void Build(GameSession session)
    {
        var split = new HBoxContainer();
        split.SetAnchorsPreset(LayoutPreset.FullRect);
        split.AddThemeConstantOverride("separation", 8);
        AddChild(split);

        // The map lives in its own viewport so it clips to its frame and so its
        // input is scoped: without this it consumes clicks meant for the border
        // and draws underneath the chrome.
        var container = new SubViewportContainer
        {
            Stretch = true,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        split.AddChild(container);

        _viewport = new SubViewport
        {
            HandleInputLocally = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
            TransparentBg = false,
        };
        container.AddChild(_viewport);

        _mapView = new WorldMapView { Name = "WorldMap" };
        _camera = new MapCameraController { Name = "MapCamera" };
        _viewport.AddChild(_mapView);
        _viewport.AddChild(_camera);
        _camera.MakeCurrent();
        _mapView.Configure(session.Map);
        _camera.Configure(_mapView.Projection.Bounds);
        _mapView.HoveredChanged += cell =>
        {
            _hovered = cell;
            UpdateInspector();
        };
        _mapView.SelectedChanged += cell =>
        {
            _selected = cell;
            UpdateInspector();
        };

        split.AddChild(BuildSidePanel());
    }

    private Control BuildSidePanel()
    {
        var column = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(200, 0),
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        column.AddThemeConstantOverride("separation", 6);

        var inspector = new PanelContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        _cellInfo = new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = false,
            Text = "[b]Cell inspector[/b]\nHover or click a hex.",
        };
        inspector.AddChild(_cellInfo);
        column.AddChild(inspector);

        _debugToggle = new CheckButton { Text = "Debug overlays" };
        _debugToggle.Toggled += enabled => _mapView?.SetDebugMode(enabled);
        column.AddChild(_debugToggle);

        var probe = new Button { Text = "Probe state" };
        probe.Pressed += () => ApplyDebugStateProbe();
        column.AddChild(probe);

        column.AddChild(new Label
        {
            ThemeTypeVariation = "ImperialismReadoutLabel",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            Text = "Pan: middle/right drag or arrows · Zoom: wheel · Fit: Home · Select: left click",
        });

        // The manual puts this control in one place and only one: "The End Turn
        // button appears only on the Terrain Map screen at the bottom of the
        // toolbar", and elsewhere, "in the lower right".
        _endTurn = new Button
        {
            Text = "End Turn",
            SizeFlagsVertical = SizeFlags.ShrinkEnd,
            TooltipText = "When you click here, you are committed.",
        };
        _endTurn.Pressed += () =>
        {
            _endTurn.Disabled = true;
            TurnEndRequested?.Invoke();
        };
        column.AddChild(_endTurn);
        return column;
    }

    /// <summary>Lets the shell hand the turn back to the player once the report is read.</summary>
    public void AllowAnotherTurn()
    {
        if (_endTurn is not null)
        {
            _endTurn.Disabled = false;
        }
    }

    private void ApplyState()
    {
        if (_session is null || _mapView is null)
        {
            return;
        }

        _mapView.ApplyState(_session.WorldView);
        UpdateInspector();
    }

    private void UpdateInspector()
    {
        if (_cellInfo is null || _session is null)
        {
            return;
        }

        var index = _selected ?? _hovered;
        if (!index.HasValue)
        {
            _cellInfo.Text = "[b]Cell inspector[/b]\nHover or click a hex. A click pins the selection.";
            return;
        }

        var cell = _session.Map[index.Value];
        var state = _session.WorldView[index.Value];
        var selection = _selected.HasValue ? "Selected" : "Hovered";
        var resources = cell.ResourceKeys.Count == 0 ? "—" : string.Join(", ", cell.ResourceKeys);
        var river = cell.River is { } path ? $"{path.First} ↔ {path.Second}" : "—";
        _cellInfo.Text = string.Join(
            '\n',
            $"[b]{selection} cell {cell.Index.Value}[/b]",
            $"Coordinate: {cell.Coordinate.Column}, {cell.Coordinate.Row}",
            $"Terrain: {cell.TerrainKey}",
            $"Region: {cell.RegionName ?? "Unassigned"}",
            $"Owner: {state.OwnerName ?? "—"}",
            $"Resources: {resources}",
            $"Settlement: {cell.SettlementSite}",
            $"Capital: {(state.CapitalCountry.HasValue ? "yes" : "no")}",
            $"River: {river}");
    }
}
