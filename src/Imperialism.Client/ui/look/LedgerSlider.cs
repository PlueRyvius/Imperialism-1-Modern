using Godot;
using Imperialism.Presentation;

namespace Imperialism.Client;

/// <summary>
/// One original 224x30 row. Arithmetic here only maps values to pixels and pointer
/// positions to a requested total; Presentation owns all bounds and edit decisions.
/// </summary>
public sealed partial class LedgerSlider : Control
{
    private TransportRowView? _row;
    private Texture2D? _strip;
    private Font? _font;
    private Label? _name;
    private LedgerFigure? _figure;
    private float _edge;
    private int _heldDirection;
    private double _repeatIn;
    private bool _dragging;

    public event Action<int>? StepRequested;
    public event Action<long>? TotalRequested;
    public event Action<string>? HotTextChanged;

    private Rect2 LeftArrow => new(_edge - 12, 10, 13, 10);
    private Rect2 RightArrow => new(_edge + 115, 10, 11, 10);
    private Rect2 Track => new(_edge, 8, 115, 12);

    public void Build(TransportRowView row, Font font)
    {
        Name = "row_" + row.RowKey.Replace("commodity.", "").Replace('+', '_');
        SetMeta("layout_name", "row." + row.RowKey);
        Size = new Vector2(224, 30);
        _font = font;
        FocusMode = FocusModeEnum.All;
        MouseFilter = MouseFilterEnum.Stop;
        _edge = row.Column == TransportLedgerColumn.Left ? 96 : 92;
        if (row.Strip is { } strip) _strip = GD.Load<Texture2D>($"res://art/transport/{strip}.png");
        // The two mockup additions are separate nodes: the name here, the handle below.
        _name = LedgerText.Add(this, "row.name", new Rect2(0, 22, 180, 8), row.Name,
            font, 8, new Color("5e4a30"));
        _figure = LedgerFigure.Add(this, "row.figure", new Rect2(184, 22, 38, 9));
        var handle = new LedgerHandle { Name = "SliderHandle", Position = new Vector2(_edge + 1, 13) };
        handle.SetMeta("layout_name", "slider.handle");
        AddChild(handle);
        MouseExited += () => HotTextChanged?.Invoke("");
        FocusEntered += () => HotTextChanged?.Invoke(_row?.Name ?? "");
        FocusExited += CancelHold;
        Apply(row);
    }

    public void Apply(TransportRowView row)
    {
        _row = row;
        FocusMode = row.IsActive || row.CanLower ? FocusModeEnum.All : FocusModeEnum.None;
        _name!.Visible = _figure!.Visible = row.IsActive;
        _figure.Text = $"{row.Requested} / {row.Available}";
        var handle = GetNode<LedgerHandle>("SliderHandle");
        handle.Visible = row.IsActive;
        handle.Position = new Vector2(_edge + 1 + FillWidth(row), 13);
        QueueRedraw();
    }

    private static float FillWidth(TransportRowView row) => row.Available > 0
        ? 113 * (float)Math.Clamp((double)row.Committed / row.Available, 0, 1) : 0;

    public override void _Draw()
    {
        if (_row is not { IsActive: true } row) return;
        if (_strip is not null) DrawTexture(_strip, Vector2.Zero);
        else
        {
            // D9: the first custom gatherable good uses the empty slot without a strip.
            DrawRect(new Rect2(_edge, 12, 115, 1), new Color("800000"));
            DrawString(_font!, new Vector2(_edge - 9, 18), "‹", fontSize: 10, modulate: Colors.Black);
            DrawString(_font!, new Vector2(_edge + 116, 18), "›", fontSize: 10, modulate: Colors.Black);
        }
        DrawRect(new Rect2(_edge, 13, 1, 4), Colors.Black);
        DrawRect(new Rect2(_edge + 1, 13, 113, 4), new Color("cabc6c"));
        DrawRect(new Rect2(_edge + 1, 13, FillWidth(row), 4), new Color("0058a2"));
        DrawRect(new Rect2(_edge, 17, 115, 1), Colors.White);
        // The original's white line is tinted three pixels in from its left end
        // (measured on O30, every slider row, both columns).
        DrawRect(new Rect2(_edge + 1, 17, 1, 1), new Color("fffbf0"));
        DrawRect(new Rect2(_edge + 3, 17, 2, 1), new Color("f5f5f5"));
        // D12: no demand line where nothing wants the row, as under the original's horses.
        if (row.DemandStatus != TransportDemandStatus.None)
            DrawRect(new Rect2(_edge, 18, 115, 2), new Color(row.DemandStatus == TransportDemandStatus.Met ? "5fba4b" : "fa0500"));
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (_row is not { } row) return;
        if (@event is InputEventMouseMotion motion)
        {
            HotTextChanged?.Invoke(motion.Position.Y is >= 18 and < 20
                ? $"{row.Name}\nDemand: {row.Demand}" : $"{row.Name}\nAvailable: {row.Available}");
            if (_dragging) RequestAt(motion.Position.X);
        }
        if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left } mouse)
        {
            if (!mouse.Pressed) { CancelHold(); return; }
            if (!row.IsActive && !row.CanLower) return;
            GrabFocus();
            if (LeftArrow.HasPoint(mouse.Position) && row.CanLower) StartHold(-1);
            else if (RightArrow.HasPoint(mouse.Position) && row.IsActive && row.CanRaise) StartHold(1);
            else if (Track.HasPoint(mouse.Position) && row.IsActive)
            {
                _dragging = true;
                RequestAt(mouse.Position.X);
            }
            AcceptEvent();
        }
        if (@event is InputEventKey { Pressed: true } key)
        {
            if (key.Keycode == Key.Left && row.CanLower) StepRequested?.Invoke(-1);
            else if (key.Keycode == Key.Right && row.CanRaise) StepRequested?.Invoke(1);
            else if (key.Keycode == Key.Home && row.CanLower) TotalRequested?.Invoke(0);
            else if (key.Keycode == Key.End && row.CanRaise) TotalRequested?.Invoke(row.Available);
            else return;
            AcceptEvent();
        }
    }

    private void RequestAt(float x)
    {
        if (_row is not { } row) return;
        var fraction = Math.Clamp((x - _edge - 1) / 113d, 0, 1);
        // Input conversion only. SetTransportRow clamps and performs the member edits.
        TotalRequested?.Invoke((long)Math.Round(fraction * row.Available));
    }

    private void StartHold(int direction)
    {
        _heldDirection = direction;
        _repeatIn = 0.4;
        StepRequested?.Invoke(direction);
    }

    public override void _Process(double delta)
    {
        if (!IsVisibleInTree() || !Input.IsMouseButtonPressed(MouseButton.Left))
        {
            CancelHold();
            return;
        }
        if (_heldDirection == 0) return;
        _repeatIn -= delta;
        if (_repeatIn > 0) return;
        _repeatIn += 0.06;
        if ((_heldDirection < 0 ? LeftArrow : RightArrow).HasPoint(GetLocalMousePosition()))
            StepRequested?.Invoke(_heldDirection);
    }

    public void CancelHold()
    {
        _heldDirection = 0;
        _dragging = false;
    }
}

/// <summary>The approved mockup's handle, separate from the original row art.</summary>
public sealed partial class LedgerHandle : Control
{
    public LedgerHandle() => MouseFilter = MouseFilterEnum.Ignore;
    public override void _Draw()
    {
        DrawCircle(Vector2.Zero, 4.5f, new Color("ecd696"));
        DrawArc(Vector2.Zero, 4.5f, 0, Mathf.Tau, 32, new Color("6e4614"), 0.5f, true);
    }
}
