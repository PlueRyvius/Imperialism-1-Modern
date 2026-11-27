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
        _bodyFont = (FontFile)GD.Load<FontFile>("res://art/fonts/lora.ttf").Duplicate();
        _headingFont = (FontFile)GD.Load<FontFile>("res://art/fonts/fraunces.ttf").Duplicate();
        foreach (var font in new[] { _bodyFont, _headingFont })
        {
            font.Antialiasing = TextServer.FontAntialiasing.Gray;
            font.Hinting = TextServer.Hinting.Normal;
            font.SubpixelPositioning = TextServer.SubpixelPositioning.Disabled;
        }
        _headingFont.Oversampling = 1;
        var display = new FontVariation
        {
            BaseFont = _headingFont,
            VariationOpentype = new Godot.Collections.Dictionary { ["wght"] = 700, ["opsz"] = 34, ["SOFT"] = 50 },
        };
        _date = Header("date", new Rect2(46, 9, 105, 20), "", display, 12);
        _treasury = Header("treasury", new Rect2(158, 9, 100, 20), "", display, 12);
        _hotText = Header("hottext", new Rect2(460, 0, 160, 20), "", display, 11, HorizontalAlignment.Right);
        _hotDetail = Header("hottext.detail", new Rect2(500, 15, 120, 16), "", _bodyFont, 9, HorizontalAlignment.Right);
        LedgerText.Add(_canvas, "title.left", new Rect2(160.5f, 76.5f, 100, 30), "Transport",
            display, 17, new Color("341e0e"));
        LedgerText.Add(_canvas, "title.right", new Rect2(417.5f, 77.5f, 70, 30), "Ledger",
            display, 16, new Color("341e0e"));
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

    private Label Header(string name, Rect2 box, string text, Font font, int fontSize,
        HorizontalAlignment alignment = HorizontalAlignment.Left)
    {
        var label = LedgerText.Add(_canvas!, name, box, text, font, fontSize,
            new Color("faeec8"), alignment, shadow: true);
        label.AddThemeColorOverride("font_shadow_color", new Color("140a04"));
        return label;
    }

    private void PlaceRailText()
    {
        var k = (int)_canvas!.Scale.X;
        LedgerText.PlaceInk(_date!, k, new Vector2(46, 15));
        LedgerText.PlaceInk(_treasury!, k, new Vector2(158, 15));
        LedgerText.PlaceInk(_hotText!, k, new Vector2(619, 7), right: true);
        LedgerText.PlaceInk(_hotDetail!, k, new Vector2(619, 20.5f), right: true);
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
        PlaceRailText();
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
        PlaceRailText();
        // Font metrics may change with hinting at the new raster size.
        if (_session is not null && _capacity is not null) Apply();
    }
}
