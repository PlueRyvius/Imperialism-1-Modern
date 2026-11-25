using Godot;
using Imperialism.Presentation;

namespace Imperialism.Client;

/// <summary>Shared layout and input adapters; all economic figures come from views.</summary>
internal static class OrdersControls
{
    // Godot ranges use doubles. Keep integer input within their exact range.
    public const double ExactIntegerLimit = 9007199254740991d;

    public static Label Text(Container parent, string text, string style = "")
    {
        var label = new Label
        {
            Text = text,
            ThemeTypeVariation = style,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        parent.AddChild(label);
        return label;
    }

    public static VBoxContainer Card(Container parent, string title)
    {
        var panel = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        parent.AddChild(panel);
        var rows = new VBoxContainer();
        panel.AddChild(rows);
        Text(rows, title, "ImperialismSectionHeading");
        return rows;
    }

    public static SpinBox Count(Container parent, Action<long> changed)
    {
        var input = new SpinBox { MinValue = 0, MaxValue = ExactIntegerLimit, Step = 1 };
        parent.AddChild(input);
        input.ValueChanged += value => changed((long)value);
        return input;
    }

    public static string Bill(IReadOnlyList<CommodityAmountView> amounts) =>
        string.Join(", ", amounts.Select(item => $"{item.Quantity} {item.CommodityName}"));

    public static void Clear(Container parent)
    {
        foreach (var child in parent.GetChildren())
        {
            parent.RemoveChild(child);
            child.QueueFree();
        }
    }
}
