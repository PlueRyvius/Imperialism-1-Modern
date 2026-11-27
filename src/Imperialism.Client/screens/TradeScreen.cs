using System.Globalization;
using Godot;
using Imperialism.Presentation;

namespace Imperialism.Client;

/// <summary>The market's fixed commodity order; inputs forward directly to the draft.</summary>
public sealed partial class TradeScreen : Control, IShellScreen
{
    private GameSession? _session;
    private VBoxContainer? _rows;
    private Label? _budget;
    private Label? _warning;
    private readonly List<Action<TradePreview>> _updates = [];
    private bool _applying;

    public TradeScreen()
    {
        Name = "TradeScreen";
        SetAnchorsPreset(LayoutPreset.FullRect);
    }

    public string Title => "Bid and Offers";

    public void Enter(GameSession session)
    {
        _session = session;
        if (_rows is null)
        {
            var layout = GD.Load<PackedScene>("res://scenes/TradeLayout.tscn").Instantiate<Control>();
            AddChild(layout);
            _budget = layout.GetNode<Label>("Column/Budget");
            _warning = layout.GetNode<Label>("Column/Warning");
            _rows = layout.GetNode<VBoxContainer>("Column/RowsScroll/Rows");
        }

        BuildRows();
    }

    public void Exit() { }

    private void BuildRows()
    {
        var session = _session!;
        var preview = session.Preview().Trade;
        _updates.Clear();
        OrdersControls.Clear(_rows!);
        if (preview.Rows.Count == 0)
            OrdersControls.Text(_rows!, "This world has no tradable commodities.", "ImperialismQuiet");
        foreach (var commodity in preview.Rows)
        {
            var card = OrdersControls.Card(_rows!, commodity.CommodityName);
            var stock = OrdersControls.Text(card, "");
            var controls = new HBoxContainer();
            card.AddChild(controls);
            var id = session.Package.Catalog.GetCommodityId(commodity.CommodityKey);
            OrdersControls.Text(controls, "Offer");
            var offer = OrdersControls.Count(controls, value => Change(() => session.Draft.SetOffer(id, value)));
            offer.TooltipText = "Units to sell; bounded by sellable stock in the preview.";
            OrdersControls.Text(controls, "Bid");
            var bid = OrdersControls.Count(controls, value => Change(() => session.Draft.SetBid(id, value)));
            bid.TooltipText = "Units to buy; cash and cargo holds may limit the filled trade.";
            var requested = OrdersControls.Text(card, "", "ImperialismQuiet");
            var warning = OrdersControls.Text(card, "", "ImperialismQuiet");
            _updates.Add(view =>
            {
                var row = view.Rows.First(item => item.CommodityKey == commodity.CommodityKey);
                stock.Text = string.Create(CultureInfo.InvariantCulture,
                    $"World price: ${row.WorldPrice:N0} · sellable: {row.Sellable}\n" +
                    $"Warehouse: {row.Stock} · industry claims: {row.ClaimedByIndustry}");
                // UI precision bound only; the requested amount stays visible below.
                offer.MaxValue = Math.Min(row.Sellable, OrdersControls.ExactIntegerLimit);
                offer.SetValueNoSignal(row.Offer);
                bid.SetValueNoSignal(row.Bid);
                requested.Text = string.Create(CultureInfo.InvariantCulture,
                    $"Requested offer: {row.Offer} · bid: {row.Bid} · bid cost at world price: ${row.BidCost:N0}");
                warning.Text = row.IsOverOffered ? "Warning: the offer exceeds sellable stock. Industry has first claim; the excess remains unfilled." : "";
            });
        }

        Apply(preview);
    }

    private void Change(Action edit)
    {
        if (_applying) return;
        edit();
        Apply(_session!.Preview().Trade);
    }

    private void Apply(TradePreview preview)
    {
        _applying = true;
        try
        {
            _budget!.Text = TradeText.Header(preview);
            _warning!.Text =
                (preview.IsOverBidLimit ? $"Warning: you may bid on only {OrdersPreview.MaximumBids} commodities each turn. " : "") +
                (preview.BidsExceedCash ? "Warning: bids exceed current cash." : "");
            foreach (var update in _updates) update(preview);
        }
        finally { _applying = false; }
    }
}
