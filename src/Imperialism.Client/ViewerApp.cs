using System.Globalization;
using Godot;
using Imperialism.Content;
using Imperialism.Core;
using Imperialism.Presentation;

namespace Imperialism.Client;

public sealed partial class ViewerApp : Node
{
    private const string DefaultWorldPath = "res://demo/demo.iworld";
    private CompiledWorldPackage? _package;
    private MapViewDefinition? _mapDefinition;
    private WorldState? _worldState;
    private WorldViewState? _viewState;
    private WorldMapView? _mapView;
    private MapCameraController? _camera;
    private OptionButton? _scenarioPicker;
    private Label? _title;
    private Label? _status;
    private RichTextLabel? _cellInfo;
    private CellIndex? _hovered;
    private CellIndex? _selected;
    private bool _smokeTest;

    public override void _Ready()
    {
        var arguments = OS.GetCmdlineUserArgs();
        _smokeTest = arguments.Contains("--smoke-test", StringComparer.Ordinal);
        var worldPath = ReadArgument(arguments, "--world") ?? DefaultWorldPath;

        try
        {
            var filePath = worldPath.StartsWith("res://", StringComparison.Ordinal)
                ? ProjectSettings.GlobalizePath(worldPath)
                : Path.GetFullPath(worldPath);
            _package = WorldContentCodec.DecodeAndCompilePackage(File.ReadAllBytes(filePath));
            _mapDefinition = MapViewDefinition.Create(_package);
            BuildScene();
            LoadScenario(0);
            if (_smokeTest)
            {
                CallDeferred(nameof(FinishSmokeTest));
            }
        }
        catch (Exception exception)
        {
            GD.PushError($"Could not load world package '{worldPath}': {exception.Message}");
            if (_smokeTest)
            {
                GetTree().Quit(1);
                return;
            }

            BuildErrorUi(exception.Message);
        }
    }

    private void BuildScene()
    {
        _mapView = new WorldMapView { Name = "WorldMap" };
        _camera = new MapCameraController { Name = "MapCamera" };
        AddChild(_mapView);
        AddChild(_camera);
        _camera.MakeCurrent();
        _mapView.Configure(_mapDefinition ??
            throw new InvalidOperationException("Map presentation definition is unavailable."));
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

        BuildUi();
    }

    private void BuildUi()
    {
        var canvas = new CanvasLayer { Name = "Interface" };
        AddChild(canvas);

        var header = new PanelContainer();
        header.SetAnchorsPreset(Control.LayoutPreset.TopWide);
        header.OffsetLeft = 14;
        header.OffsetTop = 14;
        header.OffsetRight = -14;
        header.OffsetBottom = 68;
        canvas.AddChild(header);

        var row = new HBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center,
        };
        row.AddThemeConstantOverride("separation", 18);
        header.AddChild(row);

        _title = new Label
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            Text = "Imperialism Modern Viewer",
        };
        _title.AddThemeFontSizeOverride("font_size", 22);
        row.AddChild(_title);

        row.AddChild(new Label { Text = "Scenario" });
        _scenarioPicker = new OptionButton { CustomMinimumSize = new Vector2(210, 0) };
        _scenarioPicker.ItemSelected += OnScenarioSelected;
        row.AddChild(_scenarioPicker);

        var stateProbe = new Button { Text = "Probe state", Visible = false };
        stateProbe.Pressed += () =>
        {
            if (ApplyDebugStateProbe() && _status is not null)
            {
                _status.Text += "  •  state probe applied";
            }
        };
        var debugToggle = new CheckButton { Text = "Debug overlays" };
        debugToggle.Toggled += enabled =>
        {
            _mapView?.SetDebugMode(enabled);
            stateProbe.Visible = enabled;
        };
        row.AddChild(debugToggle);
        row.AddChild(stateProbe);

        _status = new Label { Text = "Loading..." };
        row.AddChild(_status);

        var inspector = new PanelContainer();
        inspector.SetAnchorsPreset(Control.LayoutPreset.TopRight);
        inspector.OffsetLeft = -360;
        inspector.OffsetTop = 82;
        inspector.OffsetRight = -14;
        inspector.OffsetBottom = 430;
        canvas.AddChild(inspector);

        _cellInfo = new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = false,
            Text = "[b]Cell inspector[/b]\nHover or click a hex.",
        };
        inspector.AddChild(_cellInfo);

        var help = new Label
        {
            Text = "Pan: middle/right drag or arrows  •  Zoom: wheel  •  Fit: Home  •  Select: left click",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        help.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
        help.OffsetLeft = 14;
        help.OffsetTop = -46;
        help.OffsetRight = -14;
        help.OffsetBottom = -14;
        canvas.AddChild(help);
    }

    private void LoadScenario(int selectedIndex)
    {
        if (_package is null || _mapView is null || _camera is null)
        {
            throw new InvalidOperationException("Viewer scene is not initialized.");
        }

        if ((uint)selectedIndex >= (uint)_package.ScenarioKeys.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(selectedIndex));
        }

        var scenarioKey = _package.ScenarioKeys[selectedIndex];
        _worldState = new WorldState(_package.GetWorld(scenarioKey));
        RefreshState(scenarioKey);

        if (_scenarioPicker is not null)
        {
            if (_scenarioPicker.ItemCount == 0)
            {
                foreach (var key in _package.ScenarioKeys)
                {
                    _scenarioPicker.AddItem(_package.GetWorld(key).Scenario.Name);
                }
            }

            _scenarioPicker.Select(selectedIndex);
        }

        if (_title is not null)
        {
            _title.Text = $"{_mapDefinition?.MapName} — {_viewState?.ScenarioName}";
        }

        UpdateInspector();
    }

    private void OnScenarioSelected(long index) => LoadScenario(checked((int)index));

    private void UpdateInspector()
    {
        if (_cellInfo is null || _mapDefinition is null || _viewState is null)
        {
            return;
        }

        var index = _selected ?? _hovered;
        if (!index.HasValue)
        {
            _cellInfo.Text = "[b]Cell inspector[/b]\nHover or click a hex. A click pins the selection.";
            return;
        }

        var cell = _mapDefinition[index.Value];
        var state = _viewState[index.Value];
        var selection = _selected.HasValue ? "Selected" : "Hovered";
        var resources = cell.ResourceKeys.Count == 0 ? "—" : string.Join(", ", cell.ResourceKeys);
        var river = cell.River is { } path ? $"{path.First} ↔ {path.Second}" : "—";
        var capital = state.CapitalCountry.HasValue ? "yes" : "no";
        _cellInfo.Text = string.Join(
            '\n',
            $"[b]{selection} cell {cell.Index.Value}[/b]",
            $"Coordinate: {cell.Coordinate.Column}, {cell.Coordinate.Row}",
            $"Terrain: {cell.TerrainKey}",
            $"Region: {cell.RegionName ?? "Unassigned"}",
            $"Region key: {cell.RegionKey ?? "—"}",
            $"Owner: {state.OwnerName ?? "—"}",
            $"Owner key: {state.OwnerKey ?? "—"}",
            $"Resources: {resources}",
            $"Settlement: {cell.SettlementSite}",
            $"Capital: {capital}",
            $"River: {river}");
    }

    private async void FinishSmokeTest()
    {
        // Let the real controls and both static/interactive draw surfaces run once.
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        if (_package is null || _mapDefinition is null || _viewState is null || _mapView is null)
        {
            GetTree().Quit(1);
            return;
        }

        var pickedCenters = _mapDefinition.Cells.Count(cell =>
            _mapView.Projection.Pick(_mapView.Projection.GetCenter(cell.Index)) == cell.Index);
        var stateProbePassed = ApplyDebugStateProbe();
        GD.Print(string.Create(
            CultureInfo.InvariantCulture,
            $"VIEWER_SMOKE_OK map={_mapDefinition.MapKey} scenarios={_package.ScenarioKeys.Count} " +
            $"dimensions={_mapDefinition.Dimensions.Width}x{_mapDefinition.Dimensions.Height} " +
            $"cells={_mapDefinition.Cells.Count} pickedCenters={pickedCenters} " +
            $"stateProbe={(stateProbePassed ? "pass" : "fail")}"));
        GetTree().Quit(pickedCenters == _mapDefinition.Cells.Count && stateProbePassed ? 0 : 1);
    }

    private void RefreshState(string? scenarioKey = null)
    {
        if (_package is null || _mapDefinition is null || _worldState is null || _mapView is null)
        {
            throw new InvalidOperationException("Viewer state is not initialized.");
        }

        scenarioKey ??= _viewState?.ScenarioKey ??
            throw new InvalidOperationException("No scenario is selected.");
        _viewState = WorldViewState.Create(_package, scenarioKey, _worldState);
        _mapView.ApplyState(_viewState);
        if (_status is not null)
        {
            _status.Text =
                $"{_mapDefinition.Dimensions.Width}×{_mapDefinition.Dimensions.Height}  •  {_viewState.CurrentDate}";
        }

        UpdateInspector();
    }

    private bool ApplyDebugStateProbe()
    {
        if (_worldState is null || _viewState is null)
        {
            return false;
        }

        var world = _worldState.Definition;
        if (world.Map.Provinces.Count > 0 && world.Countries.Count > 0)
        {
            var province = FindProbeProvince(_worldState);
            var owner = _worldState.GetProvinceOwner(province);
            CountryId? nextOwner = owner.HasValue && owner.Value.Value + 1 < world.Countries.Count
                ? new CountryId(owner.Value.Value + 1)
                : owner.HasValue
                    ? null
                    : new CountryId(0);
            _worldState.SetProvinceOwner(province, nextOwner);
        }

        if (world.Scenario.InitialRailLinks.Count > 0)
        {
            var rail = world.Scenario.InitialRailLinks[0];
            if (!_worldState.RemoveRail(rail))
            {
                _worldState.BuildRail(rail);
            }
        }

        RefreshState();
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

    private void BuildErrorUi(string message)
    {
        var canvas = new CanvasLayer();
        var label = new Label
        {
            Position = new Vector2(24, 24),
            Text = $"Unable to load the world package.\n\n{message}",
        };
        canvas.AddChild(label);
        AddChild(canvas);
    }

    private static string? ReadArgument(string[] arguments, string name)
    {
        for (var index = 0; index < arguments.Length; index++)
        {
            if (arguments[index] == name)
            {
                if (index + 1 >= arguments.Length)
                {
                    throw new ArgumentException($"{name} requires a path.");
                }

                return arguments[index + 1];
            }
        }

        return null;
    }
}
