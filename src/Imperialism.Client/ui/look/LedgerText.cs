using Godot;

namespace Imperialism.Client;

/// <summary>Original faces, confined to the measured boxes inside a scaled ledger.</summary>
internal static class LedgerText
{
    public static Label Add(Control parent, string layoutName, Rect2 box, string text,
        Font font, int fontSize, Color colour, HorizontalAlignment alignment = HorizontalAlignment.Left)
    {
        var label = new Label
        {
            Name = layoutName.Replace('.', '_'), Position = box.Position, Size = box.Size,
            Text = text, HorizontalAlignment = alignment, VerticalAlignment = VerticalAlignment.Center,
            ClipText = true, MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        label.SetMeta("layout_name", layoutName);
        label.AddThemeFontOverride("font", font);
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", colour);
        label.AddThemeColorOverride("font_shadow_color", Colors.Transparent);
        label.AddThemeConstantOverride("outline_size", 0);
        parent.AddChild(label);
        return label;
    }
}
