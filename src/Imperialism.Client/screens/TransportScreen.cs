using Godot;
using Imperialism.Presentation;

namespace Imperialism.Client;

/// <summary>A shared transport budget with commodity rows in draft priority order.</summary>
public sealed partial class TransportScreen : Control, IShellScreen
{
    private GameSession? _session;
    private VBoxContainer? _rows;
    private Label? _capacity;
    private Label? _warning;
    private ProgressBar? _bar;
    private readonly List<Action<TransportPreview>> _updates = [];
    private bool _applying;

    public TransportScreen()
    {
        Name = "TransportScreen";
        SetAnchorsPreset(LayoutPreset.FullRect);
    }

    public string Title => "Transport";

    public void Enter(GameSession session)
    {
        _session = session;
        foreach (var stock in session.Status.Warehouse)
        {
            var id = session.Package.Catalog.GetCommodityId(stock.CommodityKey);
            if (!session.Draft.Transport.Any(row => row.Commodity == id))
            {
                session.Draft.SetTransport(id, 0);
            }
        }

        if (_rows is null)
        {
            var layout = GD.Load<PackedScene>("res://scenes/TransportLayout.tscn").Instantiate<Control>();
            AddChild(layout);
            _capacity = layout.GetNode<Label>("Column/Capacity");
            _bar = layout.GetNode<ProgressBar>("Column/CapacityBar");
            _warning = layout.GetNode<Label>("Column/Warning");
            _rows = layout.GetNode<VBoxContainer>("Column/RowsScroll/Rows");
        }

        BuildRows();
    }

    public void Exit() { }

    private void BuildRows()
    {
        var session = _session!;
        var preview = session.Preview().Transport;
        _updates.Clear();
        OrdersControls.Clear(_rows!);
        foreach (var slider in preview.Sliders)
        {
            var card = OrdersControls.Card(_rows!, slider.CommodityName);
            var controls = new HBoxContainer();
            card.AddChild(controls);
            var input = new HSlider
            {
                MinValue = 0,
                Step = 1,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(100, 18),
                TooltipText = "Requested transport allocation",
            };
            controls.AddChild(input);
            var id = session.Package.Catalog.GetCommodityId(slider.CommodityKey);
            var numeric = OrdersControls.Count(controls, value =>
            {
                if (_applying) return;
                session.Draft.SetTransport(id, value);
                Apply(session.Preview().Transport);
            });
            numeric.TooltipText = "Requested transport units";
            input.ValueChanged += value =>
            {
                if (_applying) return;
                session.Draft.SetTransport(id, (long)value);
                Apply(session.Preview().Transport);
            };
            foreach (var (text, up) in new[] { ("↑", true), ("↓", false) })
            {
                var button = new Button { Text = text, TooltipText = up ? "Reserve capacity earlier" : "Reserve capacity later" };
                controls.AddChild(button);
                button.Pressed += () =>
                {
                    var moved = up ? session.Draft.MoveTransportUp(id) : session.Draft.MoveTransportDown(id);
                    if (moved) BuildRows();
                };
            }

            var result = OrdersControls.Text(card, "");
            _updates.Add(view =>
            {
                var row = view.Sliders.First(item => item.CommodityKey == slider.CommodityKey);
                input.MaxValue = view.Capacity is { } capacity ? capacity : OrdersControls.ExactIntegerLimit;
                numeric.MaxValue = input.MaxValue;
                numeric.Editable = !view.IsZeroCapacity;
                numeric.SetValueNoSignal(row.Requested);
                input.Editable = !view.IsZeroCapacity;
                input.SetValueNoSignal(row.Requested);
                result.Text = $"Requested: {row.Requested} · reserved: {row.Committed}" +
                    (row.IsShort ? " · Warning: shared capacity is short." : "");
            });
        }

        Apply(preview);
    }

    private void Apply(TransportPreview preview)
    {
        _applying = true;
        try
        {
            _capacity!.Text = $"Shared capacity: {preview.Used} reserved / {preview.Capacity?.ToString() ?? "unlimited"}";
            _bar!.MaxValue = preview.Capacity is { } capacity ? capacity : OrdersControls.ExactIntegerLimit;
            _bar.SetValueNoSignal(preview.Used);
            _warning!.Text = preview.IsOverAsked ? "Warning: requests exceed shared capacity; later rows are trimmed." : "";
            foreach (var update in _updates) update(preview);
        }
        finally { _applying = false; }
    }
}
