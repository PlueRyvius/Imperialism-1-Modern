using Godot;
using Imperialism.Presentation;

namespace Imperialism.Client;

/// <summary>Purchasable research and the draft's ordered buy list.</summary>
public sealed partial class TechnologyScreen : Control, IShellScreen
{
    private GameSession? _session;
    private VBoxContainer? _rows;
    private VBoxContainer? _purchases;
    private Label? _budget;
    private Label? _warning;
    private readonly List<Action<TechnologyPreview>> _updates = [];
    private bool _applying;

    public TechnologyScreen()
    {
        Name = "TechnologyScreen";
        SetAnchorsPreset(LayoutPreset.FullRect);
    }

    public string Title => "Investment";

    public void Enter(GameSession session)
    {
        _session = session;
        if (_rows is null)
        {
            var layout = GD.Load<PackedScene>("res://scenes/TechnologyLayout.tscn").Instantiate<Control>();
            AddChild(layout);
            _budget = layout.GetNode<Label>("Column/Budget");
            _warning = layout.GetNode<Label>("Column/Warning");
            _rows = layout.GetNode<VBoxContainer>("Column/RowsScroll/Rows/Offers");
            _purchases = layout.GetNode<VBoxContainer>("Column/RowsScroll/Rows/Purchases");
        }

        BuildRows();
    }

    public void Exit() { }

    private void BuildRows()
    {
        var session = _session!;
        var preview = session.Preview().Technology;
        _updates.Clear();
        OrdersControls.Clear(_rows!);
        if (preview.Offered.Count == 0)
            OrdersControls.Text(_rows!, "No technologies are available to purchase this turn.", "ImperialismQuiet");
        foreach (var offer in preview.Offered)
        {
            var card = OrdersControls.Card(_rows!, offer.TechnologyName);
            var buy = new CheckButton { Text = $"Buy · ${offer.Cost}", TooltipText = "Toggle this purchase; known next turn." };
            card.AddChild(buy);
            var id = session.Package.Catalog.GetTechnologyId(offer.TechnologyKey);
            buy.Toggled += selected =>
            {
                if (_applying) return;
                session.Draft.SetTechnologyPurchase(id, selected);
                Apply(session.Preview().Technology);
            };
            _updates.Add(view =>
            {
                var row = view.Offered.First(item => item.TechnologyKey == offer.TechnologyKey);
                buy.SetPressedNoSignal(row.IsSelected);
                buy.Text = row.IsSelected ? $"Purchasing · ${row.Cost}" : $"Buy · ${row.Cost}";
            });
        }

        Apply(preview);
    }

    private void Apply(TechnologyPreview preview)
    {
        _applying = true;
        try
        {
            _budget!.Text = $"Buy list: ${preview.CommittedCost} / cash: ${preview.Cash}";
            _warning!.Text = preview.IsOverCash ? "Warning: the buy list exceeds current cash." : "";
            foreach (var update in _updates) update(preview);
            OrdersControls.Clear(_purchases!);
            OrdersControls.Text(_purchases!, "Buy list · purchase order", "ImperialismSectionHeading");
            if (preview.Purchases.Count == 0)
                OrdersControls.Text(_purchases!, "No purchases selected.", "ImperialismQuiet");
            foreach (var purchase in preview.Purchases)
            {
                var card = OrdersControls.Card(_purchases!, purchase.TechnologyName);
                OrdersControls.Text(card,
                    (!purchase.IsOffered ? "Warning: this technology is no longer offered. " : "") +
                    (!purchase.IsAffordable ? "Warning: not affordable in this purchase order against current cash." : "Affordable against current cash."),
                    "ImperialismQuiet");
                var cancel = new Button { Text = "Cancel purchase" };
                card.AddChild(cancel);
                cancel.Pressed += () =>
                {
                    _session!.Draft.SetTechnologyPurchase(_session.Package.Catalog.GetTechnologyId(purchase.TechnologyKey), false);
                    Apply(_session.Preview().Technology);
                };
            }
        }
        finally { _applying = false; }
    }
}
