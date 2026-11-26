using Godot;
using Imperialism.Core;
using Imperialism.Presentation;

namespace Imperialism.Client;

/// <summary>
/// The Armoury and the Shipyard: regiment purchases by category, the garrison, and hulls.
/// Every figure and outcome comes from the preview; input goes only into the draft.
/// </summary>
public sealed partial class ArmouryScreen : Control, IShellScreen
{
    private GameSession? _session;
    private VBoxContainer? _content;

    public string Title => "Armoury";

    public override void _Ready()
    {
        var panel = new PanelContainer();
        panel.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(panel);
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        panel.AddChild(scroll);
        _content = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _content.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(_content);
        if (_session is not null)
        {
            Refresh();
        }
    }

    public void Enter(GameSession session)
    {
        _session = session;
        Refresh();
    }

    public void Exit() { }

    private void Refresh()
    {
        if (_session is null || _content is null)
        {
            return;
        }

        foreach (var child in _content.GetChildren())
        {
            _content.RemoveChild(child);
            child.QueueFree();
        }

        var preview = _session.Preview();
        var armoury = preview.Armoury;
        var draft = _session.Draft;
        Heading("Armoury — preview");
        Text(preview.Fleet.OrdersSetAside
            ? "Country eliminated. All drafted orders are set aside."
            : "Each category builds its best type known as the turn opens. A regiment appears in the " +
              "capital's province next turn. Warnings do not block drafting.");
        Text($"Cash claimed {armoury.CashClaimed} · Labour withheld {armoury.LabourWithheld}");

        Heading("Regiments");
        foreach (var row in armoury.Categories)
        {
            Text(ArmourySmoke.Describe(row));

            // An unavailable category keeps its arrows: the preview warns, it never refuses.
            var category = row.Category;
            Arrows(
                () => { draft.SetRegimentPurchase(category, row.Drafted + 1); Refresh(); },
                row.Drafted > 0 ? () => { draft.SetRegimentPurchase(category, row.Drafted - 1); Refresh(); } : null,
                row.Drafted > 0 ? () => { draft.SetRegimentPurchase(category, 0); Refresh(); } : null);
        }

        Button("Clear all regiment purchases", () => { draft.ClearRegimentPurchases(); Refresh(); });

        Heading("Garrison");
        if (armoury.Regiments.Count == 0)
        {
            Text("No regiments.");
        }

        var provinces = _session.World.Definition.Map.Provinces;
        var catalog = _session.Package.Catalog;
        foreach (var regiment in armoury.Regiments)
        {
            var province = provinces[catalog.GetProvinceId(regiment.ProvinceKey).Value].Name;
            var move = regiment.DraftedDestinationKey is { } key
                ? $" · Drafted to {provinces[catalog.GetProvinceId(key).Value].Name}" : string.Empty;
            Text($"Regiment {regiment.Id.Value} · {regiment.TypeName} · {province} · " +
                $"Health {regiment.Health}/{regiment.MaxHealth} · Medals {regiment.Medals}{move}");
        }

        Heading("Shipyard");
        Text("Hulls cost materials, never cash or labour, and are paid after production.");
        foreach (var hull in preview.Shipyard.Hulls)
        {
            Text(ArmourySmoke.Describe(hull));
            var type = new ShipTypeId(hull.TypeIndex);
            Arrows(
                () => { draft.SetShipBuild(type, hull.Drafted + 1); Refresh(); },
                hull.Drafted > 0 ? () => { draft.SetShipBuild(type, hull.Drafted - 1); Refresh(); } : null,
                hull.Drafted > 0 ? () => { draft.SetShipBuild(type, 0); Refresh(); } : null);
        }

        Button("Clear all Shipyard orders", () => { draft.ClearShipBuilds(); Refresh(); });
    }

    private void Arrows(Action more, Action? fewer, Action? clear)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 4);
        AddButton(row, "+1", more);
        if (fewer is not null) { AddButton(row, "−1", fewer); }
        if (clear is not null) { AddButton(row, "Clear", clear); }
        _content!.AddChild(row);
    }

    private static void AddButton(Container parent, string text, Action action)
    {
        var button = new Button { Text = text };
        button.Pressed += action;
        parent.AddChild(button);
    }

    private void Heading(string text) => _content!.AddChild(new Label
    {
        Text = text,
        ThemeTypeVariation = "ImperialismScreenTitle",
        AutowrapMode = TextServer.AutowrapMode.WordSmart,
    });

    private void Text(string text) => _content!.AddChild(new Label
    {
        Text = text,
        AutowrapMode = TextServer.AutowrapMode.WordSmart,
    });

    private void Button(string text, Action action) => AddButton(_content!, text, action);
}
