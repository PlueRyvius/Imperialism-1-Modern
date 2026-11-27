using System.Globalization;
using Godot;
using Imperialism.Presentation;

namespace Imperialism.Client;

/// <summary>The original 640x480 Transport ledger; only Presentation supplies its numbers.</summary>
public sealed partial class TransportScreen : Control, IShellScreen
{
    private GameSession? _session;
    private Control? _canvas;
    private Label? _hotText;
    private Label? _hotDetail;
    private Label? _date;
    private Label? _treasury;
    private CapacityBar? _capacity;
    private TransportPreview? _preview;
    private FontFile? _bodyFont;
    private FontFile? _headingFont;
    private readonly Dictionary<string, LedgerSlider> _sliders = new(StringComparer.Ordinal);

    public TransportScreen()
    {
        Name = "TransportScreen";
        SetAnchorsPreset(LayoutPreset.FullRect);
        Resized += FitCanvas;
    }

    public string Title => "Transport";

    public void Enter(GameSession session)
    {
        _session = session;
        if (_canvas is null) Build();
        _date!.Text = TransportText.FormatDate(session.Status.CurrentDate);
        _treasury!.Text = "$" + session.Status.Cash.ToString("N0", CultureInfo.InvariantCulture);
        SetHotText("");
        FitCanvas();
    }

    public void Exit()
    {
        foreach (var slider in _sliders.Values) slider.CancelHold();
        if (_hotText is not null) SetHotText("");
    }

    private void Build()
    {
        var surround = new ColorRect { Name = "Surround", Color = new Color("0e0905") };
        surround.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(surround);
        _canvas = GD.Load<PackedScene>("res://scenes/TransportLayout.tscn").Instantiate<Control>();
        AddChild(_canvas);
        // Per-screen copies: oversampling must not mutate the other screens' cached fonts.
        _bodyFont = (FontFile)GD.Load<FontFile>("res://art/fonts/transport-lora.ttf").Duplicate();
        _headingFont = (FontFile)GD.Load<FontFile>("res://art/fonts/transport-fraunces.ttf").Duplicate();
        foreach (var font in new[] { _bodyFont, _headingFont })
        {
            font.Antialiasing = TextServer.FontAntialiasing.Gray;
            font.Hinting = TextServer.Hinting.Normal;
            font.SubpixelPositioning = TextServer.SubpixelPositioning.Disabled;
        }
        _headingFont.Oversampling = 1;
        _date = Header("date", new Rect2(47, 9, 77, 16), "", 10);
        _treasury = Header("treasury", new Rect2(159, 10, 48, 16), "", 10);
        _hotText = Header("hottext", new Rect2(484, 4, 135, 15), "", 10, HorizontalAlignment.Right);
        _hotDetail = Header("hottext.detail", new Rect2(524, 19, 95, 15), "", 10, HorizontalAlignment.Right);
        var display = new FontVariation
        {
            BaseFont = _headingFont,
            VariationOpentype = new Godot.Collections.Dictionary { ["wght"] = 700, ["opsz"] = 34, ["SOFT"] = 50 },
        };
        LedgerText.Add(_canvas, "title.left", new Rect2(162.5f, 80.5f, 90, 23), "Transport",
            display, 15, new Color("5e4a30"));
        LedgerText.Add(_canvas, "title.right", new Rect2(416.5f, 80.5f, 62, 23), "Ledger",
            display, 15, new Color("5e4a30"));
        WatchHover(_date, "Date");
        WatchHover(_treasury, "Treasury");
        foreach (var (node, text) in new[] { ("TabTransport", "Transport"), ("TabIndustry", "Industry"),
                     ("TabTrade", "Trade"), ("TabDiplomacy", "Diplomacy") })
            WatchHover(_canvas.GetNode<Control>(node), text);
        _capacity = new CapacityBar();
        _capacity.Build(_bodyFont);
        WatchHover(_capacity, "Transport capacity");
        _canvas.AddChild(_capacity);
    }

    private Label Header(string name, Rect2 box, string text, int fontSize,
        HorizontalAlignment alignment = HorizontalAlignment.Left, int spaceSpacing = 0, int glyphSpacing = 0)
    {
        var label = LedgerText.Add(_canvas!, name, box, text, new FontVariation { BaseFont = _bodyFont, SpacingGlyph = glyphSpacing, SpacingSpace = spaceSpacing }, fontSize,
            new Color("e3e0b8"), alignment, shadow: true);
        return label;
    }

    private void WatchHover(Control control, string text)
    {
        control.MouseFilter = MouseFilterEnum.Stop;
        control.MouseEntered += () => SetHotText(text);
        control.MouseExited += () => SetHotText("");
    }

    private void SetHotText(string text)
    {
        _hotText!.Text = string.IsNullOrEmpty(text) ? "Give Transport Orders" : text;
        _hotDetail!.Text = string.IsNullOrEmpty(text) ? "(current screen)" : "";
    }

    private void Apply()
    {
        _preview = _session!.Preview().Transport;
        // Rows keep their controls while held/dragged; only their detached views change.
        var keys = _preview.Rows.Select(row => row.RowKey).ToHashSet(StringComparer.Ordinal);
        foreach (var (key, slider) in _sliders) slider.Visible = keys.Contains(key);
        foreach (var row in _preview.Rows)
        {
            if (!_sliders.TryGetValue(row.RowKey, out var slider))
            {
                slider = new LedgerSlider();
                slider.Build(row, _bodyFont!);
                var rowKey = row.RowKey;
                slider.StepRequested += direction =>
                {
                    if (direction < 0) _session.Draft.LowerTransportRow(_preview!, rowKey);
                    else _session.Draft.RaiseTransportRow(_preview!, rowKey);
                    Apply();
                };
                slider.TotalRequested += total =>
                {
                    _session.Draft.SetTransportRow(_preview!, rowKey, total);
                    Apply();
                };
                slider.HotTextChanged += SetHotText;
                _canvas!.AddChild(slider);
                _sliders.Add(rowKey, slider);
            }
            slider.Position = new Vector2(row.Column == TransportLedgerColumn.Left ? 70 : 325, 118 + 31 * row.Slot);
            slider.Visible = true;
            slider.Apply(row);
        }
        _capacity!.Apply(_preview);
    }

    private void FitCanvas()
    {
        if (_canvas is null) return;
        var k = Math.Max(1, (int)Math.Floor(Math.Min(Size.X / 640, Size.Y / 480)));
        _canvas.Scale = new Vector2(k, k);
        _canvas.Position = (Size - new Vector2(640, 480) * k) / 2;
        _bodyFont!.Oversampling = k;
        // Both faces rasterize at the final canvas resolution, including after resize.
        _headingFont!.Oversampling = k;
        // Font metrics may change with hinting at the new raster size.
        if (_session is not null && _capacity is not null) Apply();
    }
}
