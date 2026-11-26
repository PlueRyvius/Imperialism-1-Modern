using Godot;
using Imperialism.Presentation;

namespace Imperialism.Client;

/// <summary>Warehouse, workforce and industrial orders, drawn from detached previews.</summary>
public sealed partial class IndustryScreen : Control, IShellScreen
{
    private GameSession? _session;
    private bool _applying;
    private VBoxContainer? _workforce;
    private VBoxContainer? _orders;
    private VBoxContainer? _warehouse;
    private readonly List<Action<IndustryPreview>> _updates = [];

    public IndustryScreen()
    {
        Name = "IndustryScreen";
        SetAnchorsPreset(LayoutPreset.FullRect);
    }

    public string Title => "Industry";

    public void Enter(GameSession session)
    {
        _session = session;
        // Add only missing zero rows: never disturb an existing standing order or priority.
        foreach (var recipe in session.World.Definition.ProductionRecipes)
        {
            if (!session.Draft.Production.Any(row => row.Recipe == recipe.Id))
            {
                session.Draft.SetProduction(recipe.Id, 0);
            }
        }

        if (_orders is null)
        {
            var layout = GD.Load<PackedScene>("res://scenes/IndustryLayout.tscn").Instantiate<Control>();
            AddChild(layout);
            _workforce = layout.GetNode<VBoxContainer>("WorkforcePanel/Workforce");
            _orders = layout.GetNode<VBoxContainer>("OrdersScroll/Orders");
            _warehouse = layout.GetNode<VBoxContainer>("WarehousePanel/WarehouseScroll/Warehouse");
        }

        BuildRows();
    }

    public void Exit() { }

    private void BuildRows()
    {
        var session = _session!;
        var preview = session.Preview().Industry;
        _updates.Clear();
        OrdersControls.Clear(_orders!);
        OrdersControls.Clear(_workforce!);
        OrdersControls.Clear(_warehouse!);
        OrdersControls.Text(_workforce!, "Workforce", "ImperialismSectionHeading");
        foreach (var grade in session.Status.Workforce)
        {
            OrdersControls.Text(_workforce!, $"{grade.Grade}\n{grade.Healthy} healthy\n{grade.Sick} sick");
        }

        var labour = OrdersControls.Text(_workforce!, "");
        _updates.Add(view => labour.Text = view.IsLabourLimited
            ? $"Labour\n{view.LabourAvailable} available\n{view.LabourCommitted} committed"
            : "Labour\nUnlimited");
        OrdersControls.Text(_warehouse!, "Warehouse", "ImperialismSectionHeading");
        foreach (var stock in session.Status.Warehouse)
        {
            OrdersControls.Text(_warehouse!, $"{stock.CommodityName}: {stock.Available}");
        }

        OrdersControls.Text(_orders!, "Industry", "ImperialismScreenTitle");
        OrdersControls.Text(_orders!, "Orders commit at End Turn. Output arrives next turn. Escape returns to the map.", "ImperialismQuiet");
        foreach (var facility in preview.Facilities)
        {
            var card = OrdersControls.Card(_orders!, facility.FacilityName);
            var capacity = OrdersControls.Text(card, "");
            var expansion = new CheckButton { Text = "+1 expansion rung" };
            card.AddChild(expansion);
            var cost = OrdersControls.Text(card, "", "ImperialismQuiet");
            var warning = OrdersControls.Text(card, "", "ImperialismQuiet");
            var facilityId = session.Package.Catalog.GetProductionFacilityId(facility.FacilityKey);
            expansion.Toggled += enabled => Change(() => session.Draft.SetExpansion(facilityId, enabled));
            _updates.Add(view =>
            {
                var row = view.Facilities.First(item => item.FacilityKey == facility.FacilityKey);
                capacity.Text = $"Capacity: {row.Capacity?.ToString() ?? "unlimited"} · committed: {row.CapacityCommitted}\nNext rung: {row.NextRung?.ToString() ?? "none"}";
                expansion.SetPressedNoSignal(row.ExpansionOrdered);
                expansion.Disabled = row.NextRung is null && !row.ExpansionOrdered;
                cost.Text = $"Expansion cost: {OrdersControls.Bill(row.ExpansionCost)}";
                warning.Text = row.IsGatedByTechnology ? $"Requires {row.RequiredTechnologyName} before expansion." : row.Expansion switch
                {
                    ExpansionOutcome.NotEnoughMaterials => "Warning: not enough materials after production.",
                    ExpansionOutcome.CannotExpand => "Warning: this facility cannot expand.",
                    ExpansionOutcome.Expands => "Expansion ordered for next turn.",
                    _ => "",
                };
            });
        }

        OrdersControls.Text(_orders!, "Production priority", "ImperialismSectionHeading");
        foreach (var recipe in preview.Recipes)
        {
            var card = OrdersControls.Card(_orders!, recipe.RecipeName);
            OrdersControls.Text(card, $"{OrdersControls.Bill(recipe.Inputs)} → {OrdersControls.Bill(recipe.Outputs)}\nLabour per cycle: {recipe.LabourPerCycle}", "ImperialismQuiet");
            var controls = new HBoxContainer();
            card.AddChild(controls);
            var id = session.Package.Catalog.GetProductionRecipeId(recipe.RecipeKey);
            var input = OrdersControls.Count(controls, value => Change(() => session.Draft.SetProduction(id, value)));
            input.TooltipText = "Production cycles; maximum comes from the current preview.";
            AddPriority(controls, () => session.Draft.MoveProductionUp(id), () => session.Draft.MoveProductionDown(id));
            var expected = OrdersControls.Text(card, "");
            var warning = OrdersControls.Text(card, "", "ImperialismQuiet");
            _updates.Add(view =>
            {
                var row = view.Recipes.First(item => item.RecipeKey == recipe.RecipeKey);
                input.MaxValue = row.MaximumCycles is { } maximum ? maximum : OrdersControls.ExactIntegerLimit;
                input.SetValueNoSignal(row.RequestedCycles);
                expected.Text = $"Requested: {row.RequestedCycles} · expected: {row.ExpectedCycles} · maximum: {row.MaximumCycles?.ToString() ?? "unlimited"}";
                warning.Text = (row.IsOverAsked ? $"Warning: limited by {row.LimitedBy}. " : "") +
                    (row.IsNotSaved ? "Cleared after End Turn." : "Saved for the next turn, including shortfalls.");
            });
        }

        if (preview.Railyard is { } railyard)
        {
            var card = OrdersControls.Card(_orders!, "Railyard");
            OrdersControls.Text(card, $"Per capacity point: {OrdersControls.Bill(railyard.CostPerPoint)} · labour: {railyard.LabourPerPoint}", "ImperialismQuiet");
            var input = OrdersControls.Count(card, value => Change(() => session.Draft.BuildTransportCapacity = value));
            var result = OrdersControls.Text(card, "");
            _updates.Add(view =>
            {
                var row = view.Railyard!;
                input.SetValueNoSignal(row.RequestedPoints);
                result.Text = $"Expected: {row.ExpectedPoints}. Cleared after End Turn." +
                    (row.IsShort ? " Warning: materials or labour are short." : "");
            });
        }

        if (preview.Recruitment is { } recruitment)
        {
            var card = OrdersControls.Card(_orders!, "Capitol recruitment");
            OrdersControls.Text(card, $"Cap: {recruitment.Cap} · per worker: {OrdersControls.Bill(recruitment.PricePerWorker)}", "ImperialismQuiet");
            var input = OrdersControls.Count(card, value => Change(() => session.Draft.RecruitWorkers = value));
            var result = OrdersControls.Text(card, "");
            _updates.Add(view =>
            {
                var row = view.Recruitment!;
                input.SetValueNoSignal(row.Requested);
                result.Text = $"Expected recruits: {row.ExpectedRecruits}. Cleared after End Turn." +
                    (row.IsShort ? " Warning: province cap or supplies limit recruitment." : "");
            });
        }

        Apply(preview);
    }

    private void AddPriority(Container parent, Func<bool> up, Func<bool> down)
    {
        foreach (var (text, move) in new[] { ("↑", up), ("↓", down) })
        {
            var button = new Button { Text = text, TooltipText = text == "↑" ? "Move earlier in production priority" : "Move later in production priority" };
            parent.AddChild(button);
            button.Pressed += () => { if (move()) BuildRows(); };
        }
    }

    private void Change(Action edit)
    {
        if (_applying) return;
        edit();
        Apply(_session!.Preview().Industry);
    }

    private void Apply(IndustryPreview preview)
    {
        _applying = true;
        try { foreach (var update in _updates) update(preview); }
        finally { _applying = false; }
    }
}
