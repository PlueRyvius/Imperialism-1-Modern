using Godot;

namespace Imperialism.Client;

/// <summary>Checks actual Industry controls and the navigation invariants, not game rules.</summary>
internal static class IndustryLookSmoke
{
    public static bool Run(GameSession session, IndustryScreen screen, Vector2 windowSize)
    {
        var canvas = screen.GetNode<Control>("Canvas");
        var k = Math.Max(1, (int)Math.Floor(Math.Min(windowSize.X / 640, windowSize.Y / 480)));
        var geometry = canvas.Size == new Vector2(640, 480) && canvas.Scale == new Vector2(k, k) &&
            canvas.Position == (windowSize - new Vector2(640, 480) * k) / 2;
        var before = session.Draft.Production.Select(row => row.Recipe).ToArray();
        var hot = canvas.GetNode<Label>("hottext");
        var idle = hot.Text == "Give Industry Orders";
        var warehouseBuilding = IndustryScreen.Buildings.Single(row => row.Role == "warehouse");
        screen.OpenBuilding(warehouseBuilding);
        var warehouse = canvas.GetNode<IndustryDialog>("warehouse");
        var opening = warehouse.GetNode<Label>("stock_timber").Text;
        var facility = session.Preview().Industry.Recipes.First();
        var building = IndustryScreen.Buildings.Single(row => row.Role == facility.FacilityKey);
        screen.OpenBuilding(building);
        var panel = canvas.GetNode<IndustryDialog>("production");
        var allRecipes = session.Preview().Industry.Recipes.Where(row => row.FacilityKey == facility.FacilityKey)
            .All(row => panel.HasNode("content/recipe_" + row.RecipeKey.Replace('.', '_') + "_right"));
        var retained = GodotObject.IsInstanceValid(warehouse) && warehouse.IsInsideTree();
        var recipeKey = facility.RecipeKey.Replace('.', '_');
        var arrow = panel.GetNode<PlateButton>("content/recipe_" + recipeKey + "_right");
        arrow._GuiInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true });
        arrow._GuiInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false });
        var changed = session.Preview().Industry.Recipes.Single(row => row.RecipeKey == facility.RecipeKey).RequestedCycles == facility.RequestedCycles + 1;
        var balanceArrow = warehouse.GetNode<PlateButton>("balance");
        balanceArrow._GuiInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true });
        balanceArrow._GuiInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false });
        var expected = session.Preview().Industry.WarehouseBalance.Single(row => row.CommodityKey == "commodity.timber").Quantity.ToString();
        var balance = warehouse.GetNode<Label>("stock_timber").Text == expected;
        var facilityRow = session.Preview().Industry.Facilities.Single(row => row.FacilityKey == facility.FacilityKey);
        var expansion = panel.GetNode<PlateButton>("content/expansion");
        expansion._GuiInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true });
        expansion._GuiInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false });
        var expanded = session.Preview().Industry.Facilities.Single(row => row.FacilityKey == facility.FacilityKey).ExpansionOrdered != facilityRow.ExpansionOrdered;
        var liveBalance = warehouse.GetNode<Label>("stock_timber").Text == session.Preview().Industry.WarehouseBalance.Single(row => row.CommodityKey == "commodity.timber").Quantity.ToString();
        var top = screen.CloseTopDialog() && !canvas.HasNode("production") && canvas.HasNode("warehouse");
        var closed = screen.CloseTopDialog() && !canvas.HasNode("warehouse") && !screen.CloseTopDialog();
        var special = true;
        foreach (var role in new[] { "railyard", "capitol" })
        {
            screen.OpenBuilding(IndustryScreen.Buildings.Single(row => row.Role == role));
            var specialPanel = canvas.GetNode<IndustryDialog>("production");
            var control = role == "capitol" ? "recruitment" : "railyard";
            var previous = role == "capitol" ? session.Draft.RecruitWorkers : session.Draft.BuildTransportCapacity;
            var button = specialPanel.GetNode<PlateButton>("content/" + control + "_right");
            button._GuiInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true });
            button._GuiInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false });
            special &= (role == "capitol" ? session.Draft.RecruitWorkers : session.Draft.BuildTransportCapacity) == previous + 1;
            screen.CloseTopDialog();
        }
        screen.OpenBuilding(building);
        foreach (var mappedFacility in session.Preview().Industry.Facilities.Where(row => row.FacilityKey != "facility.power-plant" && IndustryScreen.Buildings.Any(item => item.Role == row.FacilityKey)))
        {
            screen.OpenBuilding(IndustryScreen.Buildings.Single(row => row.Role == mappedFacility.FacilityKey));
            var recipePanel = canvas.GetNode<IndustryDialog>("production");
            allRecipes &= session.Preview().Industry.Recipes.Where(row => row.FacilityKey == mappedFacility.FacilityKey)
                .All(row => recipePanel.HasNode("content/recipe_" + row.RecipeKey.Replace('.', '_') + "_right"));
        }
        var reached = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < session.Preview().Industry.Facilities.Count + 2; i++)
        {
            var current = canvas.GetNode<IndustryDialog>("production");
            reached.Add(current.GetNode<Label>("content/title").Text);
            var next = current.GetNode<PlateButton>("facility_next");
            next._GuiInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true });
        }
        var accessible = session.Preview().Industry.Facilities.Where(row => IndustryScreen.Buildings.Any(buildingRow => buildingRow.Role == row.FacilityKey)).All(row => reached.Contains(row.FacilityName));
        screen.CloseTopDialog();
        var order = before.SequenceEqual(session.Draft.Production.Select(row => row.Recipe));
        var preview = session.Preview().Industry;
        var tiles = IndustryScreen.Buildings.All(row =>
        {
            var capacity = preview.Facilities.FirstOrDefault(item => item.FacilityKey == row.Role);
            var held = capacity is { Capacity: null or > 0 };
            var refineryHeld = preview.Facilities.Any(item => item.FacilityKey == "facility.oil-refinery" && item.Capacity is null or > 0);
            var expectedVisible = row.Slot is 7 or 8 or 9 or 10 or 13 or 14 or 15 || (held && (row.Slot != 11 || !refineryHeld));
            return canvas.GetNode<Control>($"slot_{row.Slot}").Visible == expectedVisible;
        });
        var expectedOrder = new[] { 12,13,7,10,14,15,9,6,11,2,3,8,0,1,4,5 };
        var slotTable = IndustryScreen.Buildings.Length == 16 &&
            IndustryScreen.Buildings.Select(row => row.Slot).SequenceEqual(Enumerable.Range(0,16)) &&
            IndustryScreen.Buildings.Select(row => row.Role).SequenceEqual(new[] {
                "facility.textile-mill","facility.clothing-factory","facility.steel-mill","facility.metal-works",
                "facility.lumber-mill","facility.furniture-factory","facility.oil-refinery","shipyard","armory",
                "trade-school","university","facility.power-plant","facility.food-processing","warehouse","railyard","capitol" }) &&
            IndustryScreen.Buildings.Select(row => row.Panel).SequenceEqual(new[] {9200,9201,9202,9203,9204,9205,9206,9207,9208,9209,9210,9211,9212,9215,9214,9216}) &&
            IndustryScreen.Buildings.Select(row => new Vector2(row.X,row.Y)).SequenceEqual(new Vector2[] {
                new(200,235),new(340,300),new(281,184),new(340,266),new(87,286),new(230,310),new(340,139),new(240,35),
                new(50,220),new(50,107),new(50,35),new(340,139),new(82,35),new(300,35),new(340,44),new(150,95) });
        var drawOrder = IndustryScreen.DrawOrder.SequenceEqual(expectedOrder) &&
            canvas.GetChildren().OfType<Control>().Where(row => row.Name.ToString().StartsWith("slot_", StringComparison.Ordinal))
                .Select(row => int.Parse(row.Name.ToString()[5..])).SequenceEqual(expectedOrder);
        var hidden = !IndustryScreen.ShowsBuilding(IndustryScreen.Buildings[0], preview with { Facilities = [] });
        var hitOrder = true;
        foreach (var candidate in IndustryScreen.Buildings)
        {
            var slot = canvas.GetNode<Control>($"slot_{candidate.Slot}");
            var hit = slot.GetNode<Control>("hit");
            var point = slot.Position + hit.Position + hit.Size / 2;
            var expectedHit = expectedOrder.Reverse().Select(id => canvas.GetNode<Control>($"slot_{id}"))
                .FirstOrDefault(node => node.Visible && new Rect2(node.Position + node.GetNode<Control>("hit").Position, node.GetNode<Control>("hit").Size).HasPoint(point));
            hitOrder &= screen.PickBuilding(point)?.Slot == (expectedHit is null ? null : int.Parse(expectedHit.Name.ToString()[5..]));
        }
        screen.OpenBuilding(IndustryScreen.Buildings[11]);
        var fuelBefore = session.Draft.BurnFuel;
        var fuelButton = canvas.GetNode<PlateButton>("production/content/fuel_right");
        fuelButton._GuiInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true });
        fuelButton._GuiInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false });
        var power = session.Draft.BurnFuel == fuelBefore + 1;
        session.Draft.BurnFuel = fuelBefore;
        screen.CloseTopDialog();
        // A separate screen observes the actual target input wiring without switching the live shell.
        var navScreen = new IndustryScreen();
        screen.GetParent().AddChild(navScreen);
        navScreen.Enter(session);
        var navigation = new List<ShellScreen>();
        navScreen.NavigationRequested += navigation.Add;
        foreach (var slot in new[] { 7, 8 })
            navScreen.GetNode<Control>($"Canvas/slot_{slot}/hit").EmitSignal(Control.SignalName.GuiInput,
                new InputEventKey { Pressed = true, Keycode = Key.Enter });
        navScreen.Free();
        var armoury = navigation.SequenceEqual(new[] { ShellScreen.Armoury, ShellScreen.Armoury });
        var crisp = canvas.GetNode<Label>("date").GetThemeFont("font") is FontVariation variation &&
            variation.BaseFont is FontFile font && font.Oversampling == k;
        var prohibited = Descendants(screen).Any(node => node is Button or SpinBox or Slider or ProgressBar or ScrollContainer);
        var ok = geometry && idle && retained && changed && balance && expanded && liveBalance && top && closed && special && order && tiles && crisp && accessible && allRecipes && slotTable && drawOrder && hidden && hitOrder && armoury && power && !prohibited;
        GD.Print($"INDUSTRY_SMOKE_{(ok ? "OK" : "FAIL")} geometry={geometry} idle={idle} warehouseRetained={retained} production={changed} balance={balance}/{liveBalance} expansion={expanded} topClose={top}/{closed} special={special} priority={order} tiles={tiles} crisp={crisp} accessible={accessible} allRecipes={allRecipes} slots={slotTable} drawOrder={drawOrder} absent={hidden} hits={hitOrder} armoury={armoury} power={power} stockControls={prohibited} openingTimber={opening}");
        return ok;
    }
    private static IEnumerable<Node> Descendants(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
}
