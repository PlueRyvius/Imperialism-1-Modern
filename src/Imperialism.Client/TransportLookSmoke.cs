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
        var shadow = date.GetThemeColor("font_shadow_color") == new Color("140a04") &&
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
        // The mockup supersedes D18: roughly 32 physical pixels of ink at 2x,
        // centered on the former title boxes, rather than the old 14 px ledger height.
        var titlesFit = TitleInkFits(leftTitle, new Rect2(160, 85, 92, 17), k) &
            TitleInkFits(rightTitle, new Rect2(417, 85, 59, 17), k);
        var crisp = new[] { leftTitle, rightTitle, date, hot, detail }.All(Crisp);
        var preview = session.Preview().Transport;
        var sliders = canvas.GetChildren().OfType<LedgerSlider>().ToArray();
        var placed = preview.Rows.All(row => sliders.Any(slider =>
            slider.GetMeta("layout_name").AsString() == "row." + row.RowKey &&
            slider.Position == new Vector2(row.Column == Presentation.TransportLedgerColumn.Left ? 70 : 325,
                118 + 31 * row.Slot)));
        var capacityRow = canvas.GetNode<Control>("CapacityRow");
        bool Unclipped(LedgerSlider slider, Vector2 minimum, Vector2 maximum)
        {
            var first = slider.GetGlobalTransform() * minimum;
            var last = slider.GetGlobalTransform() * maximum;
            for (Node? node = slider; node is not null; node = node.GetParent())
                if (node is Control { ClipContents: true } clip &&
                    (!clip.GetGlobalRect().HasPoint(first) || !clip.GetGlobalRect().HasPoint(last))) return false;
            return true;
        }
        var tightestGap = float.PositiveInfinity;
        var smallestSize = LedgerSlider.PreferredTextSize;
        var checkedRows = 0;
        bool RowFits(LedgerSlider slider)
        {
            checkedRows++;
            var name = slider.GetNode<Label>("row_name");
            var figure = slider.GetNode<Label>("row_figure");
            var left = slider.Position.X == 70;
            var size = name.GetThemeFontSize("font_size");
            var font = name.GetThemeFont("font");
            var figureFont = figure.GetThemeFont("font");
            var bold = figureFont is FontVariation weight &&
                weight.VariationOpentype["wght"].AsInt32() == 700;
            var expectedSize = Enumerable.Range(1, 10).Where(candidateSize =>
                font.GetStringSize(name.Text, fontSize: candidateSize).X +
                figureFont.GetStringSize(figure.Text, fontSize: candidateSize).X + 3 <= (left ? 113 : 112))
                .DefaultIfEmpty(1).Max();
            var (nameMin, nameMax) = LedgerText.InkBounds(name, k, 1f / 255);
            var (figureMin, figureMax) = LedgerText.InkBounds(figure, k, 1f / 255);
            var gap = figureMin.X - nameMax.X - 1f / k;
            tightestGap = Math.Min(tightestGap, gap);
            smallestSize = Math.Min(smallestSize, size);
            var values = figure.Text.Split(" / ");
            var fits = bold && values.Length == 2 && values.All(value => long.TryParse(value, out _)) &&
                name.GetThemeColor("font_color") == new Color("5a3e24") &&
                figure.GetThemeColor("font_color") == new Color("341e0e") &&
                name.GetThemeColor("font_shadow_color").A == 0 && figure.GetThemeColor("font_shadow_color").A == 0 && Crisp(name) && Crisp(figure) && Fits(name) && Fits(figure) &&
                size == expectedSize && figure.GetThemeFontSize("font_size") == size &&
                nameMin.IsFinite() && figureMin.IsFinite() &&
                nameMin.X >= name.Position.X && nameMax.X < name.Position.X + name.Size.X &&
                figureMin.X >= figure.Position.X && figureMax.X < figure.Position.X + figure.Size.X &&
                // The mockup's captions use the blank band beyond the 31 px control.
                nameMin.Y >= 20 && figureMin.Y >= 20 &&
                nameMax.Y < 39 && figureMax.Y < 39 && gap >= LedgerSlider.TextGap &&
                Unclipped(slider, nameMin, nameMax) && Unclipped(slider, figureMin, figureMax) &&
                slider.Position.Y + Math.Max(nameMax.Y, figureMax.Y) < (left ? 440 : capacityRow.Position.Y + 8) &&
                name.Position.X == (left ? 96 : 92) && name.Position.Y == 21.5f && name.Position.Y == figure.Position.Y &&
                name.Position.X + name.Size.X <= figure.Position.X &&
                Math.Abs(slider.Position.X + figure.Position.X + figure.Size.X - (left ? 279 : 529)) < 0.01f;
            if (!fits) GD.Print($"TRANSPORT_ROW_FAIL name={name.Text} figure={figure.Text} size={size}/{expectedSize} " +
                $"nameBox={name.Position}/{name.Size} ink={nameMin}..{nameMax} figureBox={figure.Position}/{figure.Size} " +
                $"ink={figureMin}..{figureMax} gap={gap} crisp={Crisp(name)}/{Crisp(figure)} advanceFit={Fits(name)}/{Fits(figure)}");
            return fits;
        }
        if (sliders.Length > 0)
        {
            var first = sliders[0];
            var (nameTop, nameBottom) = LedgerText.InkBounds(first.GetNode<Label>("row_name"), k, 1f / 255);
            var (figureTop, figureBottom) = LedgerText.InkBounds(first.GetNode<Label>("row_figure"), k, 1f / 255);
            GD.Print($"TRANSPORT_FIRST_ROW_INK nameY={nameTop.Y}..{nameBottom.Y} figureY={figureTop.Y}..{figureBottom.Y} local=true");
        }
        var rowText = LedgerSlider.PreferredTextSize == 10 && LedgerSlider.TextGap == 3 && sliders.All(RowFits);
        // Exercise long paired names with the widest requested sample in both
        // columns, independently of the tiny smoke world's active commodities.
        var stripsSafe = CaptionsAboveStrips(sliders.Select(slider =>
            (Slider: slider, PaintsStrip: preview.Rows.Any(row => row.IsActive && row.Strip is not null &&
                slider.GetMeta("layout_name").AsString() == "row." + row.RowKey))).ToArray(), k);
        var rowFont = sliders[0].GetNode<Label>("row_name").GetThemeFont("font");
        while (rowFont is FontVariation rowVariation) rowFont = rowVariation.BaseFont;
        // Force two adjacent active strips: the demo world may leave the next row inactive.
        var upper = new LedgerSlider { Position = new Vector2(70, 118) };
        var lowerStrip = new LedgerSlider { Position = new Vector2(70, 149) };
        canvas.AddChild(upper);
        upper.Build(preview.Rows[0] with { Name = "Fish & Livestock", Strip = 4001, Requested = 15, Available = 19 }, rowFont);
        canvas.AddChild(lowerStrip);
        lowerStrip.Build(preview.Rows[0] with { Strip = 4002, Requested = 15, Available = 19 }, rowFont);
        var adjacent = CaptionsAboveStrips([(upper, true), (lowerStrip, true)], k);
        GD.Print($"TRANSPORT_DRAW_ORDER adjacentActiveStrips={adjacent} liveRows={stripsSafe}");
        rowText &= adjacent && stripsSafe;
        upper.Free();
        lowerStrip.Free();
        foreach (var column in Enum.GetValues<Presentation.TransportLedgerColumn>())
        foreach (var (requested, available) in new[] { (15L, 19L), (119L, 120L) })
        foreach (var name in preview.Rows.Select(row => row.Name).Concat(new[] { "Fish & Livestock", "Cotton & Wool" }).Distinct())
        {
            var row = preview.Rows[0] with { Column = column, Name = name, Requested = requested, Available = available };
            var sample = new LedgerSlider { Position = new Vector2(column == Presentation.TransportLedgerColumn.Left ? 70 : 325,
                118 + 31 * (column == Presentation.TransportLedgerColumn.Left ? 9 : 8)) };
            // Inherit the live screen's theme before testing Label minimum sizes.
            canvas.AddChild(sample);
            sample.Build(row, rowFont);
            rowText &= RowFits(sample);
            sample.Free();
        }
        var measurement = new LedgerSlider { Position = new Vector2(70, 0) };
        canvas.AddChild(measurement);
        measurement.Build(preview.Rows[0] with { Name = "Fish & Livestock", Column = Presentation.TransportLedgerColumn.Left,
            Requested = 15, Available = 19 }, rowFont);
        var (sampleNameMin, sampleNameMax) = LedgerText.InkBounds(measurement.GetNode<Label>("row_name"), k, 1f / 255);
        var (sampleFigureMin, sampleFigureMax) = LedgerText.InkBounds(measurement.GetNode<Label>("row_figure"), k, 1f / 255);
        rowText &= RowFits(measurement);
        GD.Print($"TRANSPORT_ROW_INK scale={k} preferredSize={LedgerSlider.PreferredTextSize} smallestSize={smallestSize} fitCases={checkedRows} " +
            $"nameHeight={(sampleNameMax.Y - sampleNameMin.Y) * k + 1} figureHeight={(sampleFigureMax.Y - sampleFigureMin.Y) * k + 1} " +
            $"figureWidth={(sampleFigureMax.X - sampleFigureMin.X) * k + 1} tightestGap={tightestGap} name={sampleNameMin}..{sampleNameMax} figure={sampleFigureMin}..{sampleFigureMax}");
        measurement.Free();
        var capacity = canvas.GetNode<Control>("CapacityRow").GetNode<Label>("capacity_figure");
        var capacityName = capacityRow.GetNode<Label>("capacity_name");
        var capacityFont = capacity.GetThemeFont("font");
        bool CapacityFits(string expected)
        {
            var size = capacityName.GetThemeFontSize("font_size");
            var (nameMin, nameMax) = LedgerText.InkBounds(capacityName, k, 1f / 255);
            var (figureMin, figureMax) = LedgerText.InkBounds(capacity, k, 1f / 255);
            return capacityName.Text == "Capacity used" && capacity.Text == expected &&
                capacityFont is FontVariation weight && weight.VariationOpentype["wght"].AsInt32() == 700 &&
                size == LedgerSlider.FitTextSize(rowFont, capacityFont, "Capacity used", expected, 112) &&
                capacity.GetThemeFontSize("font_size") == size &&
                capacityName.Position == new Vector2(92, 21.5f) && capacity.Position.Y == 21.5f &&
                Math.Abs(capacity.Position.X + capacity.Size.X - 204) < 0.01f &&
                nameMin.Y >= 20 && figureMin.Y >= 20 && nameMax.Y < 39 && figureMax.Y < 39 &&
                capacityRow.Position.Y + Math.Max(nameMax.Y, figureMax.Y) < 440 &&
                capacityName.Position.X + capacityName.Size.X + 3 <= capacity.Position.X + 0.01f &&
                capacityName.GetThemeColor("font_color") == new Color("5a3e24") &&
                capacity.GetThemeColor("font_color") == new Color("341e0e") &&
                capacityName.GetThemeColor("font_shadow_color").A == 0 && capacity.GetThemeColor("font_shadow_color").A == 0 &&
                Crisp(capacityName) && Crisp(capacity) && Fits(capacityName) && Fits(capacity);
        }
        var capacityText = CapacityFits($"{preview.Used} / {preview.Capacity?.ToString() ?? "∞"}");
        var capacityControl = (CapacityBar)capacityRow;
        capacityControl.Apply(preview with { Used = 119, Capacity = 120 });
        capacityText &= CapacityFits("119 / 120");
        capacityControl.Apply(preview with { Used = 119, Capacity = null });
        capacityText &= CapacityFits("119 / ∞");
        capacityControl.Apply(preview);
        var treasury = canvas.GetNode<Label>("treasury");
        bool RailInk(Label label, Vector2 expected, bool right = false)
        {
            var (min, max) = LedgerText.InkBounds(label, k);
            return (new Vector2(right ? max.X : min.X, min.Y) - expected).Length() < 0.01f;
        }
        bool RailStyle(Label label, int size, bool display) => label.GetThemeFontSize("font_size") == size &&
            label.GetThemeColor("font_color") == new Color("faeec8") &&
            label.GetThemeColor("font_shadow_color") == new Color("140a04") &&
            label.GetThemeConstant("shadow_offset_x") == 1 && label.GetThemeConstant("shadow_offset_y") == 1 &&
            (display ? label.GetThemeFont("font") is FontVariation weight &&
                weight.VariationOpentype["wght"].AsInt32() == 700 : label.GetThemeFont("font") is FontFile);
        var rail = RailStyle(date, 12, true) && RailStyle(treasury, 12, true) &&
            RailStyle(hot, 11, true) && RailStyle(detail, 9, false) &&
            RailInk(date, new Vector2(46, 15)) && RailInk(treasury, new Vector2(158, 15)) &&
            RailInk(hot, new Vector2(619, 7), true) && RailInk(detail, new Vector2(619, 20.5f), true) &&
            hot.HorizontalAlignment == HorizontalAlignment.Right && detail.HorizontalAlignment == HorizontalAlignment.Right;
        titlesFit &= new[] { leftTitle, rightTitle }.All(title => title.GetThemeColor("font_color") == new Color("341e0e"));
        GD.Print($"TRANSPORT_MOCKUP_TEXT rail={rail} capacity={capacityText}");
        crisp &= rail && capacityText && Crisp(treasury) && Fits(treasury) && Crisp(capacity) && Fits(capacity) && rowText && titlesFit &&
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

    private static int EffectiveZ(CanvasItem item) => item.ZIndex +
        (item.ZAsRelative && item.GetParent() is CanvasItem parent ? EffectiveZ(parent) : 0);

    private static bool CaptionsAboveStrips((LedgerSlider Slider, bool PaintsStrip)[] rows, int scale)
    {
        // Headless uses the dummy renderer; inspect actual opaque strip bounds and
        // CanvasItem ordering instead of claiming a viewport pixel readback.
        foreach (var (slider, _) in rows)
        foreach (var label in new[] { slider.GetNode<Label>("row_name"), slider.GetNode<Label>("row_figure") })
        {
            if (!label.Visible) continue;
            var (minimum, maximum) = LedgerText.InkBounds(label, scale, 1f / 255);
            var ink = new Rect2(slider.Position + minimum, maximum - minimum + Vector2.One / scale);
            foreach (var (cover, paints) in rows)
            {
                if (!paints || cover == slider) continue;
                var later = EffectiveZ(cover) > EffectiveZ(label) ||
                    (EffectiveZ(cover) == EffectiveZ(label) && cover.GetIndex() > slider.GetIndex());
                if (later && ink.Intersects(new Rect2(cover.Position, new Vector2(224, 30)))) return false;
            }
        }
        return true;
    }

    private static bool TitleInkFits(Label label, Rect2 target, int scale)
    {
        var (minimum, maximum) = LedgerText.InkBounds(label, scale);
        var height = (maximum.Y - minimum.Y) * scale + 1;
        var fits = height >= 15 * scale && height <= 16.5f * scale && minimum.IsFinite() && maximum.IsFinite() && minimum.X >= target.Position.X && minimum.Y >= target.Position.Y &&
            maximum.X < target.End.X && maximum.Y < target.End.Y;
        GD.Print($"TRANSPORT_TITLE_INK {label.Text} {minimum}..{maximum} MOCKUP_FITS={fits} inkHeight={height}");
        return fits;
    }

}
