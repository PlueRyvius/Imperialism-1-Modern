using Godot;

namespace Imperialism.Client;

/// <summary>
/// A screen the shell can reach that does not yet take orders. It carries the
/// chrome every decision screen shares — a title, the manual reference for the
/// rules it will implement, and the Left Arrow that closes it — so that the
/// navigation and the frame are proved before any one screen's detail is.
/// </summary>
public sealed partial class StubScreen : Control, IShellScreen
{
    private Label? _pending;

    public StubScreen()
    {
        Title = string.Empty;
        Specification = string.Empty;
    }

    public string Title { get; private set; }

    /// <summary>Where the manual specifies this screen, quoted in the placeholder.</summary>
    public string Specification { get; private set; }

    public static StubScreen Create(string title, string specification)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(specification);
        var screen = new StubScreen { Name = title.Replace(" ", string.Empty, StringComparison.Ordinal) };
        screen.Title = title;
        screen.Specification = specification;
        screen.SetAnchorsPreset(LayoutPreset.FullRect);
        screen.Build();
        return screen;
    }

    public void Enter(GameSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (_pending is not null)
        {
            _pending.Text = $"{Specification}\n\nNot yet implemented. " +
                $"Playing {session.Status.CountryName}, {session.Status.CurrentDate}.";
        }
    }

    public void Exit()
    {
    }

    private void Build()
    {
        var margin = new MarginContainer();
        margin.SetAnchorsPreset(LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 12);
        margin.AddThemeConstantOverride("margin_top", 12);
        margin.AddThemeConstantOverride("margin_right", 12);
        margin.AddThemeConstantOverride("margin_bottom", 12);
        AddChild(margin);

        var panel = new PanelContainer { ThemeTypeVariation = "PanelContainer" };
        margin.AddChild(panel);

        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 10);
        panel.AddChild(column);

        column.AddChild(new Label
        {
            Text = Title,
            ThemeTypeVariation = "ImperialismScreenTitle",
            HorizontalAlignment = HorizontalAlignment.Center,
        });

        _pending = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            Text = Specification,
        };
        column.AddChild(_pending);
    }
}
