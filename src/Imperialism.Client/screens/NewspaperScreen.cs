using Godot;
using Imperialism.Presentation;

namespace Imperialism.Client;

/// <summary>The local country's paper, shown before the complete turn report.</summary>
/// <remarks>All selection, ordering, names and game numbers come from Presentation.</remarks>
public sealed partial class NewspaperScreen : Control
{
    private VBoxContainer? _body;
    private Label? _masthead;
    private Label? _date;
    private ScrollContainer? _scroll;
    private Button? _continue;

    public NewspaperScreen()
    {
        Name = "NewspaperScreen";
        Visible = false;
        MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsPreset(LayoutPreset.FullRect);
    }

    public event Action? Dismissed;

    /// <summary>Story text rows and the optional overflow row, excluding masthead and date.</summary>
    public int LineCount => _body?.GetChildCount() ?? 0;

    public override void _Ready() => Build();

    public void Present(NewspaperView paper)
    {
        ArgumentNullException.ThrowIfNull(paper);
        Build();
        _masthead!.Text = paper.Masthead;
        _date!.Text = paper.Date.ToString();
        foreach (var child in _body!.GetChildren())
        {
            child.QueueFree();
            _body.RemoveChild(child);
        }

        foreach (var story in paper.Stories)
        {
            _body.AddChild(new Label
            {
                ThemeTypeVariation = story.IsHeadline ? "ImperialismScreenTitle" : "Label",
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                Text = story.Headline,
            });
            _body.AddChild(new Label
            {
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                Text = story.Body,
            });
        }

        if (paper.StoriesOmitted > 0)
        {
            _body.AddChild(new Label
            {
                ThemeTypeVariation = "ImperialismQuiet",
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                Text = NewspaperText.MoreStories(paper.StoriesOmitted),
            });
        }

        _scroll!.ScrollVertical = 0;
        Visible = true;
        _continue!.GrabFocus();
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

        _masthead = new Label
        {
            ThemeTypeVariation = "ImperialismScreenTitle",
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        rows.AddChild(_masthead);
        _date = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        rows.AddChild(_date);
        rows.AddChild(new HSeparator());

        _scroll = new ScrollContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        rows.AddChild(_scroll);
        _body = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _body.AddThemeConstantOverride("separation", 6);
        _scroll.AddChild(_body);

        _continue = new Button
        {
            Text = "Continue",
            TooltipText = "Read the full turn report",
            SizeFlagsHorizontal = SizeFlags.ShrinkEnd,
        };
        _continue.Pressed += Dismiss;
        rows.AddChild(_continue);
    }
}
