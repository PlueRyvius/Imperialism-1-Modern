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
        var reached = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < session.Preview().Industry.Facilities.Count + 2; i++)
        {
            var current = canvas.GetNode<IndustryDialog>("production");
            reached.Add(current.GetNode<Label>("content/title").Text);
            var next = current.GetNode<PlateButton>("facility_next");
            next._GuiInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true });
        }
        var accessible = session.Preview().Industry.Facilities.All(row => reached.Contains(row.FacilityName));
        screen.CloseTopDialog();
        var order = before.SequenceEqual(session.Draft.Production.Select(row => row.Recipe));
        var tiles = IndustryScreen.Buildings.All(row =>
        {
            var view = session.Preview().Industry.Facilities.FirstOrDefault(f => f.FacilityKey == row.Role);
            return canvas.GetNode<Control>($"bldg_{row.Tile}").Visible ==
                (!row.Role.StartsWith("facility.", StringComparison.Ordinal) || view is { Capacity: null or > 0 });
        });
        var crisp = canvas.GetNode<Label>("date").GetThemeFont("font") is FontVariation variation &&
            variation.BaseFont is FontFile font && font.Oversampling == k;
        var prohibited = Descendants(screen).Any(node => node is Button or SpinBox or Slider or ProgressBar or ScrollContainer);
        var ok = geometry && idle && retained && changed && balance && expanded && liveBalance && top && closed && special && order && tiles && crisp && accessible && allRecipes && !prohibited;
        GD.Print($"INDUSTRY_SMOKE_{(ok ? "OK" : "FAIL")} geometry={geometry} idle={idle} warehouseRetained={retained} production={changed} balance={balance}/{liveBalance} expansion={expanded} topClose={top}/{closed} special={special} priority={order} tiles={tiles} crisp={crisp} accessible={accessible} allRecipes={allRecipes} stockControls={prohibited} openingTimber={opening}");
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
