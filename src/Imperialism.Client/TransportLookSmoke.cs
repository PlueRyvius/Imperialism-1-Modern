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
        var leftTitle = canvas.GetNode<Label>("title_left");
        var rightTitle = canvas.GetNode<Label>("title_right");
        bool Crisp(Label label)
        {
            var font = label.GetThemeFont("font");
            while (font is FontVariation variation) font = variation.BaseFont;
            return font is FontFile file && file.Antialiasing == TextServer.FontAntialiasing.Gray &&
                file.Oversampling == k && label.TextureFilter == CanvasItem.TextureFilterEnum.Linear;
        }
        bool Fits(Label label) => label.GetThemeFont("font").GetStringSize(label.Text,
            fontSize: label.GetThemeFontSize("font_size")).X <= label.Size.X + 0.01f;
        // D18 specifies inclusive ink-pixel boxes, not Label advance rectangles.
        var titlesFit = TitleInkFits(leftTitle, new Rect2(163, 86, 86, 15), k) &
            TitleInkFits(rightTitle, new Rect2(417, 86, 59, 15), k);
        var crisp = new[] { leftTitle, rightTitle, date, hot, detail }.All(Crisp);
        var preview = session.Preview().Transport;
        var sliders = canvas.GetChildren().OfType<LedgerSlider>().ToArray();
        var placed = preview.Rows.All(row => sliders.Any(slider =>
            slider.GetMeta("layout_name").AsString() == "row." + row.RowKey &&
            slider.Position == new Vector2(row.Column == Presentation.TransportLedgerColumn.Left ? 70 : 325,
                118 + 31 * row.Slot)));
        var rowText = sliders.All(slider =>
        {
            var name = slider.GetNode<Label>("row_name");
            var figure = slider.GetNode<Label>("row_figure");
            var left = slider.Position.X == 70;
            return Crisp(name) && Crisp(figure) && Fits(name) && Fits(figure) &&
                name.Position.X == (left ? 96 : 92) && name.Position.Y == figure.Position.Y &&
                name.Position.X + name.Size.X <= figure.Position.X &&
                name.Position.Y + name.Size.Y <= slider.Size.Y &&
                figure.Position.Y + figure.Size.Y <= slider.Size.Y &&
                Math.Abs(slider.Position.X + figure.Position.X + figure.Size.X - (left ? 279 : 529)) < 0.01f;
        });
        var capacity = canvas.GetNode<Control>("CapacityRow").GetNode<Label>("capacity_figure");
        crisp &= Crisp(capacity) && Fits(capacity) && rowText && titlesFit &&
            new[] { date, hot, detail }.All(Fits);
        GD.Print($"TRANSPORT_TEXT_{(crisp ? "OK" : "FAIL")} finalScale={k} rowLabels={sliders.Length * 2} " +
            $"namesFit={rowText} titlesFit={titlesFit} antialiasing=grayscale");
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
        var ok = crisp && text && geometry && placed && lower && raise && zero && bounded;
        GD.Print($"TRANSPORT_LOOK_SMOKE_{(ok ? "OK" : "FAIL")} geometry={geometry} rows={sliders.Length} " +
            $"text={text} placed={placed} lower={lower} raise={raise} zero={zero} bounded={bounded} scale={k}");
        return ok;
    }

    private static bool TitleInkFits(Label label, Rect2 target, int scale)
    {
        // Shape at final resolution with oversampling 1, as in the independent
        // atlas probe. Read live Label text, variation axes, position and metrics.
        var liveFont = label.GetThemeFont("font");
        var font = (FontVariation)liveFont.Duplicate();
        var face = (FontFile)font.BaseFont.Duplicate();
        face.Oversampling = 1;
        font.BaseFont = face;
        var size = label.GetThemeFontSize("font_size");
        var ts = TextServerManager.GetPrimaryInterface();
        var shaped = ts.CreateShapedText();
        try
        {
            ts.ShapedTextAddString(shaped, label.Text, font.GetRids(), size * scale);
            ts.ShapedTextShape(shaped);
            var baseline = label.Position + new Vector2(0,
                Mathf.Floor((label.Size.Y - liveFont.GetHeight(size)) / 2) + liveFont.GetAscent(size));
            var minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            var maximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            var pen = 0f;
            var cacheSize = new Vector2I(size * scale, 0);
            foreach (var glyph in ts.ShapedTextGetGlyphs(shaped))
            {
                var rid = glyph["font_rid"].AsRid();
                var index = glyph["index"].AsInt32();
                ts.FontRenderGlyph(rid, cacheSize, index);
                var uv = ts.FontGetGlyphUVRect(rid, cacheSize, index);
                var offset = ts.FontGetGlyphOffset(rid, cacheSize, index);
                var texture = ts.FontGetGlyphTextureIdx(rid, cacheSize, index);
                if (texture >= 0)
                {
                    var atlas = ts.FontGetTextureImage(rid, cacheSize, texture);
                    var logical = ts.FontGetGlyphSize(rid, cacheSize, index);
                    for (var y = 0; y < (int)uv.Size.Y; y++)
                    for (var x = 0; x < (int)uv.Size.X; x++)
                    {
                        if (atlas.GetPixel((int)uv.Position.X + x, (int)uv.Position.Y + y).A < 0.5f) continue;
                        var point = baseline + new Vector2(pen, 0) / scale +
                            (glyph["offset"].AsVector2() +
                             offset + new Vector2(x, y) * (logical / uv.Size)) / scale;
                        minimum = minimum.Min(point);
                        maximum = maximum.Max(point);
                    }
                }
                pen += glyph["advance"].AsSingle() * glyph["repeat"].AsInt32();
            }
            var fits = minimum.IsFinite() && maximum.IsFinite() && minimum.X >= target.Position.X && minimum.Y >= target.Position.Y &&
                maximum.X < target.End.X && maximum.Y < target.End.Y;
            GD.Print($"TRANSPORT_TITLE_INK {label.Text} {minimum}..{maximum} D18_FITS={fits}");
            return fits;
        }
        finally
        {
            ts.FreeRid(shaped);
        }
    }

}
