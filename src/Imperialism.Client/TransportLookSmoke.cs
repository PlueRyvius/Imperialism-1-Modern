using Godot;

namespace Imperialism.Client;

/// <summary>Exercises the rebuilt control's actual input wiring, separately from game-rule tests.</summary>
internal static class TransportLookSmoke
{
    public static bool Run(GameSession session, TransportScreen screen, Vector2 windowSize)
    {
        var canvas = screen.GetNode<Control>("Canvas");
        var k = Math.Max(1, (int)Math.Floor(Math.Min(windowSize.X / 640, windowSize.Y / 480)));
        var geometry = canvas.Size == new Vector2(640, 480) && canvas.Scale == new Vector2(k, k) &&
            canvas.Position == (windowSize - new Vector2(640, 480) * k) / 2;
        var date = canvas.GetNode<Label>("date");
        var hot = canvas.GetNode<Label>("hottext");
        var detail = canvas.GetNode<Label>("hottext_detail");
        var idle = hot.Text == "Give Transport Orders" && detail.Text == "(current screen)";
        date.EmitSignal(Control.SignalName.MouseEntered);
        var hover = hot.Text == "Date" && detail.Text == "";
        date.EmitSignal(Control.SignalName.MouseExited);
        var restoredText = hot.Text == "Give Transport Orders" && detail.Text == "(current screen)";
        var shadow = date.GetThemeColor("font_shadow_color") == Colors.Black &&
            date.GetThemeConstant("shadow_offset_x") == 1 && date.GetThemeConstant("shadow_offset_y") == 1;
        var text = idle && hover && restoredText && shadow;
        var leftTitle = canvas.GetNode<LedgerRasterText>("title_left");
        var rightTitle = canvas.GetNode<LedgerRasterText>("title_right");
        var figure = new LedgerFigure { Size = new Vector2(38, 9), Text = "15 / 19" };
        canvas.AddChild(figure);
        // Diagnostic, not a passing claim: the unhinted native 1 has no foot.
        var foot = Enumerable.Range(0, 4).Any(x => figure.HasNativeInk(x, 6) &&
            figure.HasNativeInk(x + 1, 6) && figure.HasNativeInk(x + 2, 6));
        var raster = leftTitle.NativeInkCount == 242 && rightTitle.NativeInkCount == 162 &&
            figure.NativeInkCount == 55 && leftTitle.UsesWholePixels(k) &&
            rightTitle.UsesWholePixels(k) && figure.UsesWholePixels(k);
        GD.Print($"TRANSPORT_RASTER_{(raster ? "OK" : "FAIL")} titles={leftTitle.NativeInkCount}/{rightTitle.NativeInkCount} " +
            $"figure={figure.NativeInkCount} foot={foot} nativePixels={raster} scale={k}");
        canvas.RemoveChild(figure);
        figure.Free();
        var preview = session.Preview().Transport;
        var sliders = canvas.GetChildren().OfType<LedgerSlider>().ToArray();
        var placed = preview.Rows.All(row => sliders.Any(slider =>
            slider.GetMeta("layout_name").AsString() == "row." + row.RowKey &&
            slider.Position == new Vector2(row.Column == Presentation.TransportLedgerColumn.Left ? 70 : 325,
                118 + 31 * row.Slot)));
        var candidate = preview.Rows.FirstOrDefault(row => row.IsActive && (row.CanLower || row.CanRaise));
        if (candidate is null)
        {
            GD.PushError("TRANSPORT_LOOK_SMOKE_FAIL needs an active, editable row.");
            return false;
        }
        var input = sliders.Single(slider => slider.GetMeta("layout_name").AsString() == "row." + candidate.RowKey);
        var edge = candidate.Column == Presentation.TransportLedgerColumn.Left ? 96 : 92;
        void Click(float x)
        {
            input._GuiInput(new InputEventMouseButton
                { ButtonIndex = MouseButton.Left, Pressed = true, Position = new Vector2(x, 14) });
            input._GuiInput(new InputEventMouseButton
                { ButtonIndex = MouseButton.Left, Pressed = false, Position = new Vector2(x, 14) });
        }
        if (!candidate.CanLower)
        {
            Click(edge + 120);
            candidate = session.Preview().Transport.GetRow(candidate.RowKey)!;
        }
        // Expected amounts follow one-unit arrow edits and the track's zero/full endpoints.
        Click(edge - 6);
        var lower = session.Preview().Transport.GetRow(candidate.RowKey)!.Requested == candidate.Requested - 1;
        Click(edge + 120);
        var raise = session.Preview().Transport.GetRow(candidate.RowKey)!.Requested == candidate.Requested;
        Click(edge + 1);
        var zero = session.Preview().Transport.GetRow(candidate.RowKey)!.Requested == 0;
        input._GuiInput(new InputEventMouseButton
            { ButtonIndex = MouseButton.Left, Pressed = true, Position = new Vector2(edge + 1, 14) });
        input._GuiInput(new InputEventMouseMotion { Position = new Vector2(edge + 114, 14) });
        input._GuiInput(new InputEventMouseButton
            { ButtonIndex = MouseButton.Left, Pressed = false, Position = new Vector2(edge + 114, 14) });
        var full = session.Preview().Transport.GetRow(candidate.RowKey)!;
        var bounded = full.Requested > 0 && full.Requested <= full.Available && !full.CanRaise;
        var ok = raster && text && geometry && placed && lower && raise && zero && bounded;
        GD.Print($"TRANSPORT_LOOK_SMOKE_{(ok ? "OK" : "FAIL")} geometry={geometry} rows={sliders.Length} " +
            $"text={text} placed={placed} lower={lower} raise={raise} zero={zero} bounded={bounded} scale={k}");
        return ok;
    }
}
