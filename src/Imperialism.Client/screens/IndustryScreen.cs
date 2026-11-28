using System.Globalization;
using Godot;
using Imperialism.Presentation;

namespace Imperialism.Client;

/// <summary>The original city, with detached Presentation figures and existing draft setters.</summary>
public sealed partial class IndustryScreen : Control, IShellScreen
{
    // ONE role table. Every role is pending trace: panel location does not prove tile identity.
    internal sealed record Building(int Tile, int X, int Y, Rect2 Hit, string Role, int Panel, Vector2 Popup);
    internal static readonly Building[] Buildings =
    [
        new(7026, 50, 35, new(17,52,191,100), "railyard", 9214, new(98,166)), // pending trace
        new(7039, 240,35, new(8,12,216,141), "warehouse", 9215, new(71,61)), // pending trace
        new(7030, 340,44, new(111,25,138,140), "facility.oil-refinery", 9211, new(300,110)), // pending trace
        new(7031, 150,95, new(54,28,148,102), "capitol", 9216, new(63,236)), // pending trace
        new(7050, 281,184, new(25,24,205,131), "facility.textile-mill", 9200, new(98,166)), // pending trace
        new(7024, 50,220, new(0,38,107,75), "facility.food-processing", 9214, new(63,236)), // pending trace
        new(7048, 200,235, new(8,24,210,135), "facility.furniture-factory", 9205, new(245,271)), // pending trace
        new(7051, 340,266, new(122,4,128,139), "facility.steel-mill", 9202, new(300,271)), // pending trace
        new(7052, 87,286, new(20,8,209,147), "facility.lumber-mill", 9206, new(98,310)), // pending trace
        new(7049, 340,300, new(38,43,170,118), "facility.metal-works", 9211, new(300,330)), // pending trace
        new(7053, 230,310, new(42,68,177,97), "facility.clothing-factory", 9201, new(245,330)), // pending trace
    ];
    private GameSession? _session;
    private Control? _canvas;
    private FontFile? _body;
    private FontFile? _heading;
    private FontVariation? _display;
    private Label? _hot;
    private Label? _detail;
    private Label? _date;
    private Label? _cash;
    private Label? _labour;
    private Label? _food;
    private IndustryPreview? _preview;
    private IndustryDialog? _warehouse;
    private IndustryDialog? _panel;
    private Building? _selected;
    private string? _facilityKey;
    private readonly Dictionary<int, Control> _tiles = [];
    private readonly List<Action> _panelUpdates = [];
    private readonly List<Action> _warehouseUpdates = [];
    private ShaderMaterial? _keyMaterial;
    private bool _showBalance;
    public event Action<ShellScreen>? NavigationRequested;
    public string Title => "Industry";

    public IndustryScreen()
    {
        Name = "IndustryScreen";
        SetAnchorsPreset(LayoutPreset.FullRect);
        Resized += FitCanvas;
    }

    public void Enter(GameSession session)
    {
        _session = session;
        // Keep existing standing rows and their priority; add only missing editable rows.
        foreach (var recipe in session.World.Definition.ProductionRecipes)
            if (!session.Draft.Production.Any(row => row.Recipe == recipe.Id)) session.Draft.SetProduction(recipe.Id, 0);
        if (_canvas is null) Build();
        _date!.Text = TransportText.FormatDate(session.Status.CurrentDate);
        _cash!.Text = "$" + session.Status.Cash.ToString("N0", CultureInfo.InvariantCulture);
        Apply();
        FitCanvas();
        SetHot("");
    }

    public void Exit()
    {
        CancelHolds(this);
        ClosePanel();
        CloseWarehouse();
    }

    private static void CancelHolds(Node node)
    {
        if (node is PlateButton button) button.CancelHold();
        foreach (var child in node.GetChildren()) CancelHolds(child);
    }

    private Texture2D Art(int id) => GD.Load<Texture2D>($"res://art/industry/{id}.png");
    private TextureRect Picture(Control parent, string name, Texture2D art, Vector2 position, bool keyed = false)
    {
        var piece = new TextureRect
        {
            Name = name.Replace('.', '_'),
            Position = position,
            Texture = art,
            MouseFilter = MouseFilterEnum.Ignore,
            Material = keyed ? _keyMaterial : null
        };
        piece.SetMeta("layout_name", name);
        parent.AddChild(piece);
        return piece;
    }

    private Label Text(Control parent, string name, Rect2 box, string text, bool light = false,
        bool heading = false, HorizontalAlignment align = HorizontalAlignment.Left, int size = 10)
    {
        var label = LedgerText.Add(parent, name, box, text, heading ? _display! : _body!, size,
            new Color(light ? "faeec8" : "241609"), align, light);
        if (light) label.AddThemeColorOverride("font_shadow_color", new Color("140a04"));
        return label;
    }

    private PlateButton Button(Control parent, string name, Texture2D art, Vector2 position, Action action,
        string hot, bool repeat = false, Texture2D? down = null)
    {
        var button = new PlateButton
        {
            Name = name.Replace('.', '_'),
            Position = position,
            Texture = art,
            Resting = art,
            Down = down,
            Repeat = repeat
        };
        button.SetMeta("layout_name", name);
        parent.AddChild(button);
        button.Pressed += action;
        button.MouseEntered += () => SetHot(hot);
        button.FocusEntered += () => SetHot(hot);
        button.MouseExited += () => SetHot("");
        return button;
    }

    private void Build()
    {
        var surround = new ColorRect { Name = "Surround", Color = new Color("0e0905") };
        surround.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(surround);
        _canvas = GD.Load<PackedScene>("res://scenes/IndustryLayout.tscn").Instantiate<Control>();
        AddChild(_canvas);
        _body = (FontFile)GD.Load<FontFile>("res://art/fonts/lora.ttf").Duplicate();
        _heading = (FontFile)GD.Load<FontFile>("res://art/fonts/fraunces.ttf").Duplicate();
        _display = new FontVariation
        {
            BaseFont = _heading,
            VariationOpentype = new Godot.Collections.Dictionary { ["wght"] = 700 }
        };
        _keyMaterial = new ShaderMaterial { Shader = new Shader { Code = "shader_type canvas_item; void fragment(){ vec4 c = texture(TEXTURE, UV); if(c.r > 0.99 && c.g < 0.01 && c.b > 0.99) discard; COLOR = c; }" } };
        Picture(_canvas, "city", Art(6000), Vector2.Zero);
        foreach (var building in Buildings)
        {
            var tile = new Control
            {
                Name = $"bldg_{building.Tile}",
                Position = new(building.X, building.Y),
                Size = new(250, 165),
                MouseFilter = MouseFilterEnum.Ignore
            };
            tile.SetMeta("layout_name", $"bldg.{building.Tile}");
            _canvas.AddChild(tile);
            Picture(tile, "art", Art(building.Tile), Vector2.Zero, true);
            var hit = new Control
            {
                Name = "hit",
                Position = building.Hit.Position,
                Size = building.Hit.Size,
                MouseFilter = MouseFilterEnum.Stop,
                FocusMode = FocusModeEnum.All
            };
            tile.AddChild(hit);
            hit.MouseEntered += () => SetHot(BuildingHot(building));
            hit.FocusEntered += () => SetHot(BuildingHot(building));
            hit.MouseExited += () => SetHot("");
            hit.GuiInput += input =>
            {
                if (input is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } ||
                    input is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Enter or Key.Space })
                { OpenBuilding(building); hit.AcceptEvent(); }
            };
            _tiles.Add(building.Tile, tile);
        }
        foreach (var (id, x, y) in new[] { (6013,6,110),(6001,6,149),(6002,6,189),(6003,6,229),
                     (6010,595,110),(6011,595,149),(6012,595,189) })
        {
            var texture = id == 6012 ? Art(id) : GD.Load<Texture2D>($"res://art/icon/commodity_{id - 6000:00}.png");
            Picture(_canvas, $"side.{id}", texture, new(x, y));
        }
        _labour = Text(_canvas, "side.6013.figure", new(10, 132, 25, 14), "", true, align: HorizontalAlignment.Right);
        _food = Text(_canvas, "side.6010.figure", new(599, 132, 25, 14), "", true, align: HorizontalAlignment.Right);
        foreach (var (id, x, target) in new[] { (9455,270,ShellScreen.Transport),(9454,298,ShellScreen.Industry),
                     (9451,326,ShellScreen.BidAndOffers),(9449,355,ShellScreen.Diplomacy) })
            Button(_canvas, "tab." + target, id is 9455 or 9454 ? Art(id) : GD.Load<Texture2D>($"res://art/transport/{id}.png"),
                new(x, 8), () => NavigationRequested?.Invoke(target), target.ToString());
        _date = Text(_canvas, "date", new(50, 7, 105, 22), "", true, true, size: 11);
        _cash = Text(_canvas, "treasury", new(161, 7, 100, 22), "", true, true, size: 11);
        _hot = Text(_canvas, "hottext", new(430, 2, 157, 18), "", true, true, HorizontalAlignment.Right, 10);
        _detail = Text(_canvas, "hottext.detail", new(420, 18, 167, 16), "", true, align: HorizontalAlignment.Right, size: 9);
    }

    private string BuildingHot(Building building)
    {
        if (!building.Role.StartsWith("facility.", StringComparison.Ordinal))
            return building.Role == "capitol" ? "Capitol" : building.Role == "railyard" ? "Railyard" : "Warehouse";
        var facility = _preview!.Facilities.FirstOrDefault(row => row.FacilityKey == building.Role);
        var recipe = _preview.Recipes.FirstOrDefault(row => row.FacilityKey == building.Role);
        return facility is null ? "" : facility.FacilityName + (recipe is null ? "" :
            $"\nProduction: {recipe.RequestedCycles} of {facility.Capacity?.ToString() ?? "unlimited"}");
    }

    private void SetHot(string value)
    {
        var lines = value.Split('\n', 2);
        _hot!.Text = value.Length == 0 ? "Give Industry Orders" : lines[0];
        _detail!.Text = value.Length == 0 ? "(current screen)" : lines.Length > 1 ? lines[1] : "";
    }

    private void Apply()
    {
        _preview = _session!.Preview().Industry;
        foreach (var building in Buildings)
        {
            var facility = _preview.Facilities.FirstOrDefault(row => row.FacilityKey == building.Role);
            // Chosen: draw only facilities this country has; unlimited facilities exist without a capacity.
            _tiles[building.Tile].Visible = !building.Role.StartsWith("facility.", StringComparison.Ordinal) ||
                facility is { Capacity: null or > 0 };
        }
        _labour!.Text = _preview.IsLabourLimited ? _preview.LabourAvailable.ToString() : "∞";
        _food!.Text = _session.Status.Warehouse.FirstOrDefault(row => row.CommodityKey == "commodity.canned-food")?.Available.ToString() ?? "";
        foreach (var update in _panelUpdates) update();
        foreach (var update in _warehouseUpdates) update();
    }

    private IndustryDialog Dialog(string name, Vector2 panelPosition, int art)
    {
        var texture = Art(art);
        var dialog = new IndustryDialog
        {
            Name = name,
            Position = panelPosition - new Vector2(3, 26),
            Size = texture.GetSize() + new Vector2(6, 29),
        };
        dialog.SetMeta("layout_name", name);
        _canvas!.AddChild(dialog);
        Picture(dialog, "panel", texture, new(3, 26));
        return dialog;
    }

    internal void OpenBuilding(Building building)
    {
        if (building.Role == "warehouse") { OpenWarehouse(); return; }
        OpenPanel(building, building.Role);
    }

    private void OpenPanel(Building building, string role)
    {
        ClosePanel();
        _selected = building;
        _facilityKey = role;
        var mapped = Buildings.FirstOrDefault(row => row.Role == role);
        var art = role == "capitol" ? 9216 : mapped?.Panel ?? 9214;
        _panel = Dialog("production", building.Popup, art);
        _panel.CloseRequested += ClosePanel;
        var content = new Control { Name = "content", Position = new(3, 26), Size = new(270, 120), MouseFilter = MouseFilterEnum.Ignore };
        _panel.AddChild(content);
        var facility = _preview!.Facilities.FirstOrDefault(row => row.FacilityKey == role);
        var title = facility?.FacilityName ?? (role == "capitol" ? "Capitol" : "Railyard");
        Text(content, "title", new(19, 8, 174, 17), title, heading: true);
        var capacity = Text(content, "capacity", new(19, 24, 170, 16), "");
        if (facility is not null)
        {
            var expansion = Button(content, "expansion", Art(facility.ExpansionOrdered ? 9245 : 9244), new(196, 9), () =>
            {
                var row = _preview!.Facilities.Single(item => item.FacilityKey == role);
                _session!.Draft.SetExpansion(_session.Package.Catalog.GetProductionFacilityId(role), !row.ExpansionOrdered);
                Apply();
            }, "");
            void ExpansionHot()
            {
                var row = _preview!.Facilities.Single(item => item.FacilityKey == role);
                SetHot("Expansion: " + OrdersControls.Bill(row.ExpansionCost) + "\n" +
                    (row.IsGatedByTechnology ? "Requires " + row.RequiredTechnologyName : row.Expansion switch
                    { ExpansionOutcome.NotEnoughMaterials => "Not enough materials after production", ExpansionOutcome.CannotExpand => "Cannot expand", _ => row.ExpansionOrdered ? "Expansion ordered" : "Expand facility" }));
            }
            expansion.MouseEntered += ExpansionHot;
            expansion.FocusEntered += ExpansionHot;
            _panelUpdates.Add(() =>
            {
                var row = _preview!.Facilities.Single(item => item.FacilityKey == role);
                capacity.Text = "Capacity: " + (row.Capacity?.ToString() ?? "unlimited");
                expansion.Texture = expansion.Resting = Art(row.ExpansionOrdered ? 9245 : 9244);
            });
            var recipes = _preview.Recipes.Where(row => row.FacilityKey == role).ToArray();
            for (var i = 0; i < recipes.Length; i++)
            {
                var key = recipes[i].RecipeKey;
                var y = 91 - (recipes.Length - 1 - i) * 40;
                if (recipes.Length > 1) Text(content, "recipe." + key + ".name", new(19, y - 13, 174, 14), recipes[i].RecipeName, size: 9);
                Stepper(content, "recipe." + key, new(68, y), new(231, y), new(79, y + 12, 149, 16),
                    () => _preview!.Recipes.Single(row => row.RecipeKey == key).RequestedCycles,
                    value => _session!.Draft.SetProduction(_session.Package.Catalog.GetProductionRecipeId(key), value), recipes[i].RecipeName);
            }
        }
        else if (role == "capitol")
        {
            _panelUpdates.Add(() => capacity.Text = "Capacity: " + (_preview!.Recruitment?.Cap.ToString() ?? "unavailable"));
            Stepper(content, "recruitment", new(74, 89), new(237, 89), new(85, 102, 149, 16),
                () => _preview!.Recruitment?.Requested ?? 0, value => _session!.Draft.RecruitWorkers = value, "Recruit workers");
        }
        else
        {
            _panelUpdates.Add(() => capacity.Text = "Railyard construction");
            Stepper(content, "railyard", new(68, 91), new(231, 91), new(79, 103, 149, 16),
                () => _preview!.Railyard?.RequestedPoints ?? 0, value => _session!.Draft.BuildTransportCapacity = value, "Build transport capacity");
        }
        // Chosen access fallback: hidden/unmapped facilities must still be buildable.
        // Reuse the original arrows in the empty frame bar; no new game order or priority edit.
        Button(_panel, "facility.previous", Art(9240), new(8, 7), () => CycleFacility(-1), "Previous facility (Page Up)");
        Button(_panel, "facility.next", Art(9242), new(255, 7), () => CycleFacility(1), "Next facility (Page Down)");
        Apply();
        SetHot(title);
    }

    private void Stepper(Control parent, string name, Vector2 left, Vector2 right, Rect2 box, Func<long> read, Action<long> write, string hot)
    {
        var figure = Text(parent, name + ".figure", box, "", align: HorizontalAlignment.Left);
        void Step(int direction)
        {
            var value = read();
            write(direction < 0 ? Math.Max(0, value - 1) : value < OrdersControls.ExactIntegerLimit ? value + 1 : value);
            Apply();
        }
        Button(parent, name + ".left", Art(9240), left, () => Step(-1), hot, true, Art(9241));
        Button(parent, name + ".right", Art(9242), right, () => Step(1), hot, true, Art(9243));
        _panelUpdates.Add(() => figure.Text = read().ToString());
    }

    private void CycleFacility(int delta)
    {
        var keys = _preview!.Facilities.Select(row => row.FacilityKey).Concat(new[] { "railyard", "capitol" }).ToArray();
        var index = Array.IndexOf(keys, _facilityKey);
        OpenPanel(_selected ?? Buildings[0], keys[(index + delta + keys.Length) % keys.Length]);
    }

    private void OpenWarehouse()
    {
        if (_warehouse is not null) { _canvas!.MoveChild(_warehouse, -1); return; }
        _showBalance = false;
        _warehouse = Dialog("warehouse", new(71, 61), 9215);
        _warehouse.CloseRequested += CloseWarehouse;
        Text(_warehouse, "title", new(55, 33, 120, 18), "Warehouse", heading: true);
        // Positions measured in the frame. Raw food identities are pending trace.
        (string Key, int X, int Y)[] counts =
        [ ("cotton",107,84),("fabric",170,103),("clothing",224,103),("wool",106,117),("paper",172,154),
          ("timber",106,170),("furniture",221,170),("lumber",169,183),("iron",107,221),("hardware",224,224),
          ("steel",170,241),("coal",106,247),("armaments",227,248),("oil",110,280),("fuel",175,282),
          ("grain",109,317),("fruit",158,317),("livestock",158,344),("canned-food",215,336),("horses",137,382) ];
        foreach (var (key, x, y) in counts)
        {
            var opening = Text(_warehouse, "stock." + key, new(x - 68, y - 35 - 3, 24, 14), "", size: 9);
            _warehouseUpdates.Add(() => opening.Text = _showBalance
                ? _preview!.WarehouseBalance.FirstOrDefault(row => row.CommodityKey == "commodity." + key)?.Quantity.ToString() ?? ""
                : _session!.Status.Warehouse.FirstOrDefault(row => row.CommodityKey == "commodity." + key)?.Available.ToString() ?? "");
            opening.MouseFilter = MouseFilterEnum.Stop;
            opening.MouseEntered += () =>
            {
                var row = _preview!.WarehouseBalance.FirstOrDefault(item => item.CommodityKey == "commodity." + key);
                SetHot(row is null ? "" : row.CommodityName + "\nAfter industry orders: " + row.Quantity);
            };
            opening.MouseExited += () => SetHot("");
        }
        var labour = Text(_warehouse, "labour", new(204 - 68, 382 - 35 - 3, 30, 14), "", size: 9);
        _warehouseUpdates.Add(() => labour.Text = _preview!.LabourAvailable.ToString());
        // The frame arrow toggles opening stock / draft balance in the measured boxes.
        Button(_warehouse, "balance", Art(9242), new(156, 7), () =>
        {
            _showBalance = !_showBalance;
            foreach (var (key, _, _) in counts)
            {
                var label = _warehouse.GetNode<Label>("stock_" + key);
                label.Text = _showBalance ? _preview!.WarehouseBalance.FirstOrDefault(row => row.CommodityKey == "commodity." + key)?.Quantity.ToString() ?? "" :
                    _session!.Status.Warehouse.FirstOrDefault(row => row.CommodityKey == "commodity." + key)?.Available.ToString() ?? "";
            }
        }, "Opening stock / balance after industry orders");
        Apply();
    }

    private void ClosePanel()
    {
        if (_panel is not null) { _panel.GetParent().RemoveChild(_panel); _panel.QueueFree(); _panel = null; }
        _panelUpdates.Clear();
    }
    private void CloseWarehouse()
    {
        if (_warehouse is not null) { _warehouse.GetParent().RemoveChild(_warehouse); _warehouse.QueueFree(); _warehouse = null; }
        _warehouseUpdates.Clear();
    }
    internal bool CloseTopDialog()
    {
        var top = _canvas?.GetChildren().OfType<IndustryDialog>().LastOrDefault();
        if (top is null) return false;
        if (top == _panel) ClosePanel(); else CloseWarehouse();
        return true;
    }
    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true } key && key.Keycode is Key.Pageup or Key.Pagedown)
        { CycleFacility(key.Keycode == Key.Pageup ? -1 : 1); AcceptEvent(); }
    }
    private void FitCanvas()
    {
        if (_canvas is null) return;
        var k = Math.Max(1, (int)Math.Floor(Math.Min(Size.X / 640, Size.Y / 480)));
        _canvas.Scale = new(k, k);
        _canvas.Position = (Size - new Vector2(640, 480) * k) / 2;
        _body!.Oversampling = _heading!.Oversampling = k;
    }
}
