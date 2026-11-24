using Godot;
using Imperialism.Presentation;

namespace Imperialism.Client;

/// <summary>
/// What the turn just did, under the fourteen headings that did it.
/// </summary>
/// <remarks>
/// <b>A modal rather than a seventh screen</b>, for three reasons. Both headless
/// gates print <c>screens=6</c> from the navigable set, and adding a seventh
/// would silently rewrite two published contracts. The original's screen frame
/// has ten tabs and none of them is a turn report. And a tab would let a player
/// open a <em>stale</em> report whenever they liked — this is the consequence of
/// an action, not a place you can go.
///
/// It deliberately does not implement <see cref="IShellScreen"/>: giving it an
/// <c>Enter</c> would imply it is somewhere the shell can navigate to.
///
/// It reads <see cref="TurnReportView"/> and formats nothing. Which country a
/// line belongs to and how it should look are fields on the line; this class
/// looks them up and never interprets the words.
/// </remarks>
public sealed partial class TurnReportScreen : Control
{
    private VBoxContainer? _body;
    private Label? _title;
    private Button? _close;

    public TurnReportScreen()
    {
        Name = "TurnReportScreen";
        Visible = false;
        MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsPreset(LayoutPreset.FullRect);
    }

    /// <summary>Raised when the player is done reading.</summary>
    public event Action? Dismissed;

    public override void _Ready() => Build();

    public void Present(TurnReportView report)
    {
        ArgumentNullException.ThrowIfNull(report);
        Build();

        _title!.Text = $"Turn {report.TurnNumber} — {report.StartedAt} to {report.EndedAt}";
        foreach (var child in _body!.GetChildren())
        {
            child.QueueFree();
            _body.RemoveChild(child);
        }

        foreach (var phase in report.Phases)
        {
            _body.AddChild(new Label
            {
                ThemeTypeVariation = "ImperialismSectionHeading",
                Text = phase.Heading,
            });

            if (phase.Lines.Count == 0)
            {
                _body.AddChild(new Label
                {
                    ThemeTypeVariation = "ImperialismQuiet",
                    Text = phase.Note ?? "Nothing this turn.",
                });
                continue;
            }

            foreach (var line in phase.Lines)
            {
                _body.AddChild(BuildLine(line));
            }
        }

        Visible = true;
        _close?.GrabFocus();
    }

    public void Dismiss()
    {
        if (!Visible)
        {
            return;
        }

        Visible = false;
        Dismissed?.Invoke();
    }

    private static Control BuildLine(TurnReportLine line)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        row.AddChild(new Label
        {
            ThemeTypeVariation = "ImperialismQuiet",
            CustomMinimumSize = new Vector2(96, 0),
            Text = (line.CountryName ?? "The world").ToUpperInvariant(),
        });

        row.AddChild(new Label
        {
            // A lookup, not a reading. The renderer decided what kind of thing
            // this line is; nothing here inspects the words to find out.
            ThemeTypeVariation = line.Kind == TurnReportKind.Outcome
                ? "Label"
                : "ImperialismQuiet",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            Text = line.Text,
        });
        return row;
    }

    private void Build()
    {
        if (_body is not null)
        {
            return;
        }

        var frame = new PanelContainer { ThemeTypeVariation = "ImperialismScreenFrame" };
        frame.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(frame);

        var margin = new MarginContainer();
        foreach (var side in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" })
        {
            margin.AddThemeConstantOverride(side, 10);
        }

        frame.AddChild(margin);

        var panel = new PanelContainer();
        margin.AddChild(panel);

        var rows = new VBoxContainer();
        rows.AddThemeConstantOverride("separation", 6);
        panel.AddChild(rows);

        _title = new Label
        {
            ThemeTypeVariation = "ImperialismScreenTitle",
            HorizontalAlignment = HorizontalAlignment.Center,
            Text = "Turn",
        };
        rows.AddChild(_title);

        rows.AddChild(new Label
        {
            ThemeTypeVariation = "ImperialismQuiet",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            Text = "No country is played by a computer. Every country including yours submitted " +
                "empty orders, because no orders screen exists yet. A rival that gathered its " +
                "harvest and did nothing else is the engine as it stands, not a fault.",
        });

        // Mandatory rather than tidy: one line is emitted per delivery, and a
        // busy world delivers once per country per commodity every turn.
        var scroll = new ScrollContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        rows.AddChild(scroll);

        _body = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _body.AddThemeConstantOverride("separation", 3);
        scroll.AddChild(_body);

        _close = new Button { Text = "Close", SizeFlagsHorizontal = SizeFlags.ShrinkEnd };
        if (ResourceLoader.Exists("res://art/button/back.png"))
        {
            _close.Icon = GD.Load<Texture2D>("res://art/button/back.png");
        }

        _close.Pressed += Dismiss;
        rows.AddChild(_close);
    }
}
