using System.Globalization;
using Godot;
using Imperialism.Content;
using Imperialism.Core;
using Imperialism.Presentation;

namespace Imperialism.Client;

/// <summary>
/// The root of the interface: a tab column, a screen stack, a persistent status
/// border, and the hot text the manual puts in the upper right of every screen.
/// </summary>
/// <remarks>
/// This is the project's first Control-derived class, and that base type is the
/// whole reason the theme reaches anything: a plain Node inherits no theme.
/// </remarks>
public sealed partial class GameShell : Control
{
    private const string DefaultWorldPath = "res://demo/demo.iworld";

    private readonly Dictionary<ShellScreen, Control> _screens = [];
    private CompiledWorldPackage? _package;
    private MapViewDefinition? _mapDefinition;
    private GameSession? _session;
    private Control? _screenStack;
    private StatusBorder? _statusBorder;
    private Label? _hotText;
    private Label? _screenTitle;
    private VBoxContainer? _leftTabs;
    private VBoxContainer? _rightTabs;
    private TurnReportScreen? _turnReport;
    private NewspaperScreen? _newspaper;
    private ShellScreen _current = ShellScreen.TerrainMap;
    private bool _smokeTest;
    private bool _smokeScreens;
    private bool _smokeTurn;
    private bool _smokeDiplomacy;
    private bool _smokeOrders;
    private bool _smokeUnits;
    private bool _smokeFleet;
    private string? _screenshotDirectory;

    public override void _Ready()
    {
        SetAnchorsPreset(LayoutPreset.FullRect);
        var arguments = OS.GetCmdlineUserArgs();
        _smokeTest = arguments.Contains("--smoke-test", StringComparer.Ordinal);
        _smokeScreens = arguments.Contains("--smoke-screens", StringComparer.Ordinal);
        _smokeTurn = arguments.Contains("--smoke-turn", StringComparer.Ordinal);
        _smokeDiplomacy = arguments.Contains("--smoke-diplomacy", StringComparer.Ordinal);
        _smokeOrders = arguments.Contains("--smoke-orders", StringComparer.Ordinal);
        _smokeUnits = arguments.Contains("--smoke-units", StringComparer.Ordinal);
        _smokeFleet = arguments.Contains("--smoke-fleet", StringComparer.Ordinal);
        _screenshotDirectory = ReadArgument(arguments, "--screenshot");
        var worldPath = ReadArgument(arguments, "--world") ??
            (_smokeOrders ? "res://demo/orders.iworld" : DefaultWorldPath);

        try
        {
            var filePath = worldPath.StartsWith("res://", StringComparison.Ordinal)
                ? ProjectSettings.GlobalizePath(worldPath)
                : Path.GetFullPath(worldPath);
            _package = WorldContentCodec.DecodeAndCompilePackage(File.ReadAllBytes(filePath));
            _mapDefinition = MapViewDefinition.Create(_package);
            BuildInterface();
            StartSession(
                ReadArgument(arguments, "--scenario") ?? _package.ScenarioKeys[0],
                ReadArgument(arguments, "--country"));
            if (_smokeTest || _smokeScreens || _smokeTurn || _smokeUnits || _smokeOrders || _smokeDiplomacy || _smokeFleet || _screenshotDirectory is not null)
            {
                CallDeferred(nameof(FinishSmoke));
            }
        }
        catch (Exception exception)
        {
            GD.PushError($"Could not load world package '{worldPath}': {exception.Message}");
            if (_smokeTest || _smokeScreens || _smokeTurn || _smokeUnits || _smokeOrders || _smokeDiplomacy || _smokeFleet)
            {
                GetTree().Quit(1);
                return;
            }

            BuildErrorUi(exception.Message);
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_newspaper is { Visible: true })
        {
            if (@event.IsActionPressed("shell_back"))
            {
                _newspaper.Dismiss();
                AcceptEvent();
            }

            return;
        }

        // While the report is up, the only key that does anything is the one
        // that closes it. Without this the function keys navigate the stack
        // behind the modal, and the player dismisses into a screen they never
        // chose.
        if (_turnReport is { Visible: true })
        {
            if (@event.IsActionPressed("shell_back"))
            {
                _turnReport.Dismiss();
                AcceptEvent();
            }

            return;
        }

        foreach (var screen in ShellScreens.All)
        {
            if (InputMap.HasAction(ShellScreens.ActionName(screen)) && @event.IsActionPressed(ShellScreens.ActionName(screen)))
            {
                Navigate(screen);
                AcceptEvent();
                return;
            }
        }

        if (@event.IsActionPressed("shell_back") && _current == ShellScreen.TerrainMap &&
            ((TerrainMapScreen)_screens[ShellScreen.TerrainMap]).DeselectCivilian())
        {
            AcceptEvent();
            return;
        }

        if (@event.IsActionPressed("shell_back") && _current != ShellScreen.TerrainMap)
        {
            Navigate(ShellScreen.TerrainMap);
            AcceptEvent();
        }
    }

    private void BuildInterface()
    {
        var frame = new PanelContainer { ThemeTypeVariation = "ImperialismScreenFrame" };
        frame.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(frame);

        var rows = new VBoxContainer();
        rows.AddThemeConstantOverride("separation", 4);
        frame.AddChild(rows);

        // A top rail, as the original has: the screen's name on the left and the
        // hot text on the right, where the manual puts the narration of whatever
        // the cursor is over.
        var rail = new HBoxContainer();
        rail.AddThemeConstantOverride("separation", 12);
        rows.AddChild(rail);

        _screenTitle = new Label
        {
            ThemeTypeVariation = "ImperialismReadout",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            Text = string.Empty,
        };
        rail.AddChild(_screenTitle);

        _hotText = new Label
        {
            Name = "HotText",
            ThemeTypeVariation = "ImperialismHotText",
            HorizontalAlignment = HorizontalAlignment.Right,
            Text = string.Empty,
        };
        rail.AddChild(_hotText);

        var body = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 4);
        rows.AddChild(body);

        // Tabs run down both edges, as the original's screen frame does. That is
        // not only faithful: six 60x56 tabs stacked in one column need 418 of the
        // 450 pixels the base viewport has, which leaves no room for a screen.
        _leftTabs = BuildTabColumn(body);

        _screenStack = new Control
        {
            Name = "ScreenStack",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            ClipContents = true,
        };
        body.AddChild(_screenStack);

        _rightTabs = BuildTabColumn(body);

        _statusBorder = new StatusBorder();
        rows.AddChild(_statusBorder);

        BuildScreens();
        BuildTabs();

        // Added after the frame so it covers the status border too. While a
        // report is up, nothing behind it is worth interacting with.
        _turnReport = new TurnReportScreen();
        _turnReport.Dismissed += () =>
        {
            ((TerrainMapScreen)_screens[ShellScreen.TerrainMap]).AllowAnotherTurn();
            Navigate(ShellScreen.TerrainMap);
        };
        AddChild(_turnReport);

        _newspaper = new NewspaperScreen();
        _newspaper.Dismissed += () =>
        {
            if (_session?.LastReport is { } report)
            {
                _turnReport.Present(report);
            }
        };
        AddChild(_newspaper);
    }

    private void BuildScreens()
    {
        // The manual specifies each of these; the stubs quote where, so the next
        // person to implement one starts from the rules rather than from a guess.
        var terrainMap = new TerrainMapScreen();
        terrainMap.TurnEndRequested += EndTurn;
        _screens[ShellScreen.TerrainMap] = terrainMap;
        _screens[ShellScreen.Transport] = new TransportScreen();
        _screens[ShellScreen.Industry] = new IndustryScreen();
        _screens[ShellScreen.BidAndOffers] = new TradeScreen();
        _screens[ShellScreen.Diplomacy] = new DiplomacyScreen();
        _screens[ShellScreen.Technology] = new TechnologyScreen();

        _screens[ShellScreen.Armoury] = StubScreen.Create(
            "Armoury", "Regiment purchases, the garrison and Shipyard orders. Manual: Armoury screen.");
        _screens[ShellScreen.Fleet] = new FleetScreen();

        foreach (var screen in ShellScreens.All)
        {
            var control = _screens[screen];
            control.SetAnchorsPreset(LayoutPreset.FullRect);
            control.Visible = false;
            _screenStack!.AddChild(control);
        }
    }

    private static VBoxContainer BuildTabColumn(Container parent)
    {
        var column = new PanelContainer { ThemeTypeVariation = "ImperialismSideColumn" };
        parent.AddChild(column);
        var tabs = new VBoxContainer { SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
        tabs.AddThemeConstantOverride("separation", 2);
        column.AddChild(tabs);
        return tabs;
    }

    private void BuildTabs()
    {
        var artScreens = ShellScreens.All.Where(screen => screen is not (ShellScreen.Armoury or ShellScreen.Fleet)).ToArray();
        var half = artScreens.Length / 2;
        for (var index = 0; index < artScreens.Length; index++)
        {
            var screen = artScreens[index];
            var target = screen;
            var button = new Button
            {
                CustomMinimumSize = new Vector2(60, 56),
                TooltipText = ((IShellScreen)_screens[screen]).Title,
                IconAlignment = HorizontalAlignment.Center,
                ExpandIcon = false,
            };

            // A tab whose art is missing still navigates: the theme degrades to
            // Godot's default rather than the shell failing to build.
            if (ResourceLoader.Exists(ShellScreens.TabTexturePath(screen)))
            {
                button.Icon = GD.Load<Texture2D>(ShellScreens.TabTexturePath(screen));
            }
            else
            {
                button.Text = ((IShellScreen)_screens[screen]).Title[..1];
            }

            button.Pressed += () => Navigate(target);
            button.MouseEntered += () => SetHotText(((IShellScreen)_screens[target]).Title);
            button.MouseExited += () => SetHotText(string.Empty);
            (index < half ? _leftTabs! : _rightTabs!).AddChild(button);
        }

        foreach (var screen in new[] { ShellScreen.Armoury, ShellScreen.Fleet })
        {
            var target = screen;
            var button = new Button { Text = ((IShellScreen)_screens[screen]).Title };
            button.Pressed += () => Navigate(target);
            button.MouseEntered += () => SetHotText(((IShellScreen)_screens[target]).Title);
            button.MouseExited += () => SetHotText(string.Empty);
            (screen == ShellScreen.Armoury ? _leftTabs! : _rightTabs!).AddChild(button);
        }
    }

    private void StartSession(string scenarioKey, string? countryKey)
    {
        if (_package is null || _mapDefinition is null)
        {
            throw new InvalidOperationException("The shell is not initialized.");
        }

        var world = _package.GetWorld(scenarioKey);
        var playable = GameSession.PlayableCountries(world);
        var country = countryKey is null
            ? playable[0].Id
            : _package.Catalog.GetCountryId(countryKey);

        _session = GameSession.Start(_package, scenarioKey, country, _mapDefinition);
        _session.Refreshed += () => _statusBorder?.Show(_session!.Status);
        _statusBorder?.Show(_session.Status);
        Navigate(ShellScreen.TerrainMap);
    }

    private void Navigate(ShellScreen screen)
    {
        if (_session is null)
        {
            return;
        }

        foreach (var candidate in ShellScreens.All)
        {
            var control = _screens[candidate];
            var isTarget = candidate == screen;
            if (control.Visible && !isTarget)
            {
                ((IShellScreen)control).Exit();
            }

            control.Visible = isTarget;
        }

        _current = screen;
        ((IShellScreen)_screens[screen]).Enter(_session);
        if (_screenTitle is not null)
        {
            _screenTitle.Text = ((IShellScreen)_screens[screen]).Title;
        }

        SetHotText(string.Empty);
    }

    private void EndTurn()
    {
        if (_session is null || _turnReport is null || _newspaper is null ||
            _newspaper.Visible || _turnReport.Visible)
        {
            return;
        }

        _session.EndTurn();
        _newspaper.Present(_session.LastNewspaper!);
    }

    private void SetHotText(string text)
    {
        if (_hotText is not null)
        {
            _hotText.Text = text;
        }
    }

    private async void FinishSmoke()
    {
        // Let the real controls and every draw surface run once.
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        if (_package is null || _mapDefinition is null || _session is null)
        {
            GetTree().Quit(1);
            return;
        }

        if (_smokeDiplomacy)
        {
            try
            {
                GetTree().Quit(DiplomacySmoke.Run(_session) ? 0 : 1);
            }
            catch (Exception exception)
            {
                GD.PushError($"DIPLOMACY_SMOKE_FAIL: {exception.Message}");
                GetTree().Quit(1);
            }

            return;
        }

        if (_screenshotDirectory is not null)
        {
            await CaptureEveryScreen(_screenshotDirectory);
        }

        if (_smokeOrders)
        {
            // Exercise the same draft entry points as all four orders screens.
            Navigate(ShellScreen.Industry);
            Navigate(ShellScreen.Transport);
            var recipe = _session.Preview().Industry.Recipes.FirstOrDefault();
            var commodity = _session.Preview().Transport.Sliders.FirstOrDefault();
            if (recipe is null || commodity is null)
            {
                GD.PushError("ORDERS_SMOKE_FAILED requires a recipe and commodity in the world package.");
                GetTree().Quit(1);
                return;
            }

            _session.Draft.SetProduction(_package.Catalog.GetProductionRecipeId(recipe.RecipeKey), 1);
            _session.Draft.SetTransport(_package.Catalog.GetCommodityId(commodity.CommodityKey), 1);
            Navigate(ShellScreen.Technology);
            var technology = _session.Preview().Technology.Offered.FirstOrDefault();
            if (technology is not null)
                _session.Draft.SetTechnologyPurchase(_package.Catalog.GetTechnologyId(technology.TechnologyKey), true);
            Navigate(ShellScreen.BidAndOffers);
            var trade = _session.Preview().Trade.Rows.FirstOrDefault(row => row.Sellable > 0);
            if (trade is null)
            {
                GD.PushError("ORDERS_SMOKE_FAILED requires a tradable commodity with sellable stock.");
                GetTree().Quit(1);
                return;
            }

            _session.Draft.SetOffer(_package.Catalog.GetCommodityId(trade.CommodityKey), 1);
            var submitted = _session.Draft.Build();
            var ordersOk = submitted.Production.Count == 1 && submitted.Transport.Count == 1 &&
                submitted.TradeOffers.Count == 1 &&
                submitted.BuyTechnology.Count == (technology is null ? 0 : 1);
            var turnOk = await ReportTurnSmoke(null);
            foreach (var phase in _session.LastReport!.Phases)
            {
                foreach (var line in phase.Lines) GD.Print($"ORDERS_EVENT {phase.Phase}: {line.Text}");
            }

            GD.Print($"ORDERS_SMOKE_{(ordersOk && turnOk ? "OK" : "FAILED")} production={submitted.Production.Count} transport={submitted.Transport.Count} technology={submitted.BuyTechnology.Count} offers={submitted.TradeOffers.Count} events={_session.LastReport.EventCount}");
            GetTree().Quit(ordersOk && turnOk ? 0 : 1);
            return;
        }

        if (_smokeUnits)
        {
            GetTree().Quit(UnitOrdersSmoke.Run(_session) ? 0 : 1);
            return;
        }

        if (_smokeFleet)
        {
            try
            {
                var order = FleetSmoke.DraftAcceptedOrder(_session);
                if (order is null)
                {
                    GD.PushError("FLEET_SMOKE_FAIL: no accepted naval order is possible; choose a world/country with placed fleets or task forces.");
                    GetTree().Quit(1);
                    return;
                }

                GD.Print($"FLEET_PREVIEW {FleetSmoke.Describe(order)}");
                var turnOk = await ReportTurnSmoke(null);
                foreach (var phase in _session.LastReport!.Phases)
                {
                    foreach (var line in phase.Lines) { GD.Print($"FLEET_EVENT {phase.Phase}: {line.Text}"); }
                }

                GD.Print(turnOk ? "FLEET_SMOKE_OK" : "FLEET_SMOKE_FAIL: turn report or overlay checks failed.");
                GetTree().Quit(turnOk ? 0 : 1);
            }
            catch (Exception exception)
            {
                GD.PushError($"FLEET_SMOKE_FAIL: {exception.Message}");
                GetTree().Quit(1);
            }

            return;
        }

        if (_smokeTurn || _screenshotDirectory is not null)
        {
            // Resolving moves the world, so it happens after the six screen
            // shots or they would disagree with each other.
            var turnOk = await ReportTurnSmoke(_screenshotDirectory);
            if (_smokeTurn)
            {
                GetTree().Quit(turnOk ? 0 : 1);
                return;
            }
        }

        var ok = _smokeScreens ? ReportScreenSmoke() : ReportViewerSmoke();
        GetTree().Quit(ok ? 0 : 1);
    }

    /// <summary>
    /// Ends a turn the way the button does and checks that something happened.
    /// </summary>
    /// <remarks>
    /// <c>events=0</c> is the assertion that matters. A turn nobody gave an
    /// order in still gathers, carries, feeds and delivers, so a pipeline that
    /// resolves and reports nothing is broken rather than quiet.
    ///
    /// This is its own mode with its own prefix. Folding it into
    /// <c>--smoke-test</c> would move that line's <c>statusCash</c> and
    /// <c>statusWorkers</c>, and those are published.
    /// </remarks>
    private async Task<bool> ReportTurnSmoke(string? screenshotDirectory)
    {
        Navigate(ShellScreen.TerrainMap);
        EndTurn();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        var paper = _session!.LastNewspaper;
        var paperShown = _newspaper is { Visible: true } && _newspaper.Size.X > 0 &&
            _turnReport is { Visible: false };
        var paperLines = _newspaper?.LineCount ?? 0;
        if (screenshotDirectory is not null && paperShown)
        {
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            GetViewport().GetTexture().GetImage().SavePng($"{screenshotDirectory}/newspaper.png");
        }

        _newspaper?.Dismiss();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var paperDismissed = _newspaper is { Visible: false };
        var report = _session.LastReport;
        var shown = _turnReport is { Visible: true } && _turnReport.Size.X > 0;
        if (screenshotDirectory is not null && shown)
        {
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            GetViewport().GetTexture().GetImage().SavePng($"{screenshotDirectory}/turnreport.png");
        }

        _turnReport?.Dismiss();
        var dismissed = _turnReport is { Visible: false } && _current == ShellScreen.TerrainMap;

        GD.Print(string.Create(
            CultureInfo.InvariantCulture,
            $"TURN_SMOKE_OK turn={report?.TurnNumber} from={report?.StartedAt} to={report?.EndedAt} " +
            $"events={report?.EventCount} phases={report?.Phases.Count} lines={report?.LineCount} " +
            $"reportVisible={(shown ? "yes" : "no")} dismissed={(dismissed ? "yes" : "no")} " +
            $"country={_session.Status.CountryKey} seed={report?.Seed} " +
            $"paperLines={paperLines} paperVisible={(paperShown ? "yes" : "no")} " +
            $"paperDismissed={(paperDismissed ? "yes" : "no")}"));

        return report is { EventCount: > 0, LineCount: > 0 } &&
            report.Phases.Count == 14 &&
            paper is not null && paperLines > 0 && paperShown && paperDismissed &&
            shown &&
            dismissed;
    }

    /// <summary>
    /// Saves one image per screen. No gate can tell whether wood grain smeared
    /// or a border landed on top of a readout, so the checklist for those things
    /// is a human looking at pictures, and this is what produces them.
    /// </summary>
    private async Task CaptureEveryScreen(string directory)
    {
        DirAccess.MakeDirRecursiveAbsolute(directory);
        foreach (var screen in ShellScreens.All)
        {
            Navigate(screen);
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            var image = GetViewport().GetTexture().GetImage();
            image.SavePng($"{directory}/{screen.ToString().ToLowerInvariant()}.png");
        }

        Navigate(ShellScreen.TerrainMap);
    }

    private bool ReportScreenSmoke()
    {
        var visited = 0;
        foreach (var screen in ShellScreens.All)
        {
            Navigate(screen);
            if (_screens[screen].Visible && _screens[screen].Size.X > 0)
            {
                visited++;
            }
        }

        Navigate(ShellScreen.TerrainMap);
        visited = BorderIsOnScreen() ? visited : 0;
        GD.Print(string.Create(
            CultureInfo.InvariantCulture,
            $"SHELL_SMOKE_OK screens={ShellScreens.All.Length} visited={visited} " +
            $"country={_session!.Status.CountryKey} theme={(ThemeReached() ? "pass" : "fail")} " +
            $"borderVisible={(BorderIsOnScreen() ? "yes" : "no")}"));
        return visited == ShellScreens.All.Length;
    }

    private bool ReportViewerSmoke()
    {
        var map = (TerrainMapScreen)_screens[ShellScreen.TerrainMap];
        var view = map.MapView;
        var pickedCenters = view is null
            ? 0
            : _mapDefinition!.Cells.Count(cell =>
                view.Projection.Pick(view.Projection.GetCenter(cell.Index)) == cell.Index);
        var stateProbePassed = map.ApplyDebugStateProbe();
        var status = _session!.Status;

        // The prefix and the first six fields are unchanged from the Phase 1
        // viewer so anything parsing this line keeps working. New fields are
        // appended, never reordered.
        GD.Print(string.Create(
            CultureInfo.InvariantCulture,
            $"VIEWER_SMOKE_OK map={_mapDefinition!.MapKey} scenarios={_package!.ScenarioKeys.Count} " +
            $"dimensions={_mapDefinition.Dimensions.Width}x{_mapDefinition.Dimensions.Height} " +
            $"cells={_mapDefinition.Cells.Count} pickedCenters={pickedCenters} " +
            $"stateProbe={(stateProbePassed ? "pass" : "fail")} " +
            $"country={status.CountryKey} screens={ShellScreens.All.Length} " +
            $"statusCash={status.Cash} statusWorkers={status.TotalWorkers} " +
            $"theme={(ThemeReached() ? "pass" : "fail")}"));
        return pickedCenters == _mapDefinition.Cells.Count && stateProbePassed;
    }

    /// <summary>
    /// Whether the theme resolved to real artwork. This is the only automated
    /// proof that extraction, import, and the theme all reached a widget.
    /// </summary>
    /// <remarks>
    /// Deliberately not fatal. A machine that has not run the extractor should
    /// still be able to run the simulation and the map, exactly as the asset
    /// policy in <c>docs/map-viewer.md</c> requires.
    /// </remarks>
    private bool ThemeReached() =>
        _statusBorder?.GetThemeStylebox("panel", "ImperialismStatusBorder") is StyleBoxTexture;

    /// <summary>
    /// Whether the status border actually fits on the screen. Six tabs in one
    /// column once pushed it twenty-nine pixels past the bottom edge, and every
    /// other gate passed while it did — the border existed, was the right size,
    /// and was simply not where anyone could see it.
    /// </summary>
    private bool BorderIsOnScreen() =>
        _statusBorder is not null &&
        _statusBorder.GlobalPosition.Y >= 0 &&
        _statusBorder.GlobalPosition.Y + _statusBorder.Size.Y <= Size.Y + 0.5f;

    private void BuildErrorUi(string message)
    {
        var label = new Label
        {
            Position = new Vector2(24, 24),
            Text = $"Unable to load the world package.\n\n{message}",
        };
        AddChild(label);
    }

    private static string? ReadArgument(string[] arguments, string name)
    {
        for (var index = 0; index < arguments.Length; index++)
        {
            if (arguments[index] == name)
            {
                if (index + 1 >= arguments.Length)
                {
                    throw new ArgumentException($"{name} requires a value.");
                }

                return arguments[index + 1];
            }
        }

        return null;
    }
}
