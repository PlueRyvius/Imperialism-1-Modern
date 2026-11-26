using System.Globalization;
using Godot;
using Imperialism.Core;
using Imperialism.Presentation;

namespace Imperialism.Client;

/// <summary>Draws the diplomacy preview; all input changes only the session draft.</summary>
public sealed partial class DiplomacyScreen : Control, IShellScreen
{
    private GameSession? _session;
    private VBoxContainer? _countries;
    private VBoxContainer? _detail;
    private Label? _treasury;
    private Label? _notice;
    private string? _selected;

    public string Title => "Diplomacy";

    public void Enter(GameSession session)
    {
        _session = session;
        Build();
        RefreshPreview();
    }

    public void Exit() => _session = null;

    private void Build()
    {
        if (_countries is not null)
        {
            return;
        }

        var panel = new PanelContainer();
        panel.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(panel);
        var body = new VBoxContainer();
        body.AddThemeConstantOverride("separation", 6);
        panel.AddChild(body);
        body.AddChild(Text("Diplomacy — preview", "ImperialismScreenTitle"));
        _treasury = Text(string.Empty);
        body.AddChild(_treasury);
        _notice = Text(string.Empty, "ImperialismQuiet");
        body.AddChild(_notice);
        var columns = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        columns.AddThemeConstantOverride("separation", 8);
        body.AddChild(columns);
        _countries = ScrollColumn(columns, 205, false);
        _detail = ScrollColumn(columns, 0, true);
    }

    private static VBoxContainer ScrollColumn(Container parent, float width, bool expand)
    {
        var scroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(width, 0),
            SizeFlagsHorizontal = expand ? SizeFlags.ExpandFill : SizeFlags.Fill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        parent.AddChild(scroll);
        var column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", 5);
        scroll.AddChild(column);
        return column;
    }

    private void RefreshPreview()
    {
        var preview = _session!.Preview().Diplomacy;
        _treasury!.Text = string.Create(CultureInfo.InvariantCulture,
            $"Treasury at open: ${preview.CashAtOpen:N0} · Earlier claims: ${preview.ClaimedBeforeDiplomacy:N0}\n" +
            $"Overtures claim: ${preview.ClaimedByOvertures:N0} · After overtures: ${preview.CashAfterOvertures:N0}");
        _notice!.Text = preview.OrdersSetAside
            ? "Your country is eliminated. Core sets these orders aside."
            : "Preview warns; every order can still be drafted. Rivals submit no orders. Later phases may move the treasury.";
        Clear(_countries!);
        Clear(_detail!);
        var ordered = preview.Countries; // Presentation sorts: Great Powers first, then by name.
        if (!ordered.Any(country => country.CountryKey == _selected))
        {
            _selected = ordered.FirstOrDefault()?.CountryKey;
        }

        foreach (var country in ordered)
        {
            var row = new VBoxContainer();
            if (country.IsEliminated)
            {
                row.Modulate = new Color(0.6f, 0.6f, 0.6f);
            }

            _countries!.AddChild(row);
            var select = ActionButton(country.CountryName + (country.IsEliminated ? " (eliminated)" : string.Empty), () =>
            {
                _selected = country.CountryKey;
                RefreshPreview();
            });
            select.ToggleMode = true;
            select.ButtonPressed = country.CountryKey == _selected;
            row.AddChild(select);
            row.AddChild(Text(CountrySummary(country), "ImperialismQuiet"));
            row.AddChild(new HSeparator());
        }

        var selected = ordered.FirstOrDefault(country => country.CountryKey == _selected);
        if (selected is not null)
        {
            DrawCountry(selected, preview);
        }
        else
        {
            _detail!.AddChild(Text("No other countries in this world."));
        }

        DrawDraft(preview);
    }

    private void DrawCountry(DiplomaticCountryView country, DiplomacyPreview preview)
    {
        var target = _session!.Package.Catalog.GetCountryId(country.CountryKey);
        _detail!.AddChild(Text(country.CountryName, "ImperialismSectionHeading"));
        _detail.AddChild(Text(CountrySummary(country)));
        _detail.AddChild(Text($"Trade boycotted: {Yes(country.IsTradeBoycotted)} · Peace lifts boycott: {Yes(country.PeaceLiftsBoycott)}", "ImperialismQuiet"));
        foreach (var option in country.Overtures)
        {
            var price = option.Price is { } cash ? string.Create(CultureInfo.InvariantCulture, $"${cash:N0}") : "unpriced";
            _detail.AddChild(ActionButton($"Offer {DiplomacyText.OvertureName(option.Kind)} · {price}" + (option.IsDrafted ? " · drafted" : string.Empty),
                () => Change(() => _session!.Draft.SetOverture(option.Kind, target, true))));
            _detail.AddChild(Text(Warning(DiplomacyWarningText.Describe(option.Warning, country.CountryName)), "ImperialismQuiet"));
        }

        _detail.AddChild(ActionButton("Declare War" + (country.War.IsDrafted ? " · drafted" : string.Empty),
            () => Change(() => _session!.Draft.SetDeclareWar(target, true))));
        _detail.AddChild(Text(Warning(DiplomacyWarningText.Describe(country.War.Warning, country.CountryName)) + "\n" + WarEffects(country.War.Effects), "ImperialismQuiet"));
        _detail.AddChild(Text("Change trade policy", "ImperialismSectionHeading"));
        var policies = new List<TradePolicy> { TradePolicy.None };
        policies.AddRange(preview.SubsidyPercents.Select(TradePolicy.Subsidy));
        policies.Add(TradePolicy.Boycott);
        var control = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        foreach (var policy in policies)
        {
            control.AddItem(Policy(policy));
        }

        control.Select(policies.IndexOf(country.DraftedPolicy ?? country.OurPolicy));
        control.ItemSelected += index => Change(() => _session!.Draft.SetTradePolicy(target, policies[(int)index]));
        _detail.AddChild(control);
        // An explicit apply also permits drafting the policy already selected in the menu.
        _detail.AddChild(ActionButton("Draft selected policy", () => Change(() =>
            _session!.Draft.SetTradePolicy(target, policies[control.Selected]))));
        _detail.AddChild(Text("Policy warning: " + Warning(DiplomacyWarningText.Describe(country.DraftedPolicyWarning ?? country.PolicyUnavailable, country.CountryName)), "ImperialismQuiet"));
    }

    private void DrawDraft(DiplomacyPreview preview)
    {
        _detail!.AddChild(new HSeparator());
        _detail.AddChild(Text("Drafted orders · all countries", "ImperialismSectionHeading"));
        string Name(string key) => preview.Countries.First(country => country.CountryKey == key).CountryName;
        foreach (var order in preview.Overtures)
        {
            var target = _session!.Package.Catalog.GetCountryId(order.TargetKey);
            DraftRow(string.Create(CultureInfo.InvariantCulture,
                $"{Name(order.TargetKey)} · {DiplomacyText.OvertureName(order.Kind)} · claim ${order.CashCost:N0}\n{Warning(DiplomacyWarningText.Describe(order.Warning, Name(order.TargetKey)))}"),
                () => _session!.Draft.SetOverture(order.Kind, target, false));
        }

        foreach (var order in preview.Declarations)
        {
            var target = _session!.Package.Catalog.GetCountryId(order.TargetKey);
            DraftRow($"{Name(order.TargetKey)} · Declare War\n{Warning(DiplomacyWarningText.Describe(order.Warning, Name(order.TargetKey)))}\n{WarEffects(order.Effects)}",
                () => _session!.Draft.SetDeclareWar(target, false));
        }

        foreach (var order in preview.Policies)
        {
            var target = _session!.Package.Catalog.GetCountryId(order.TargetKey);
            DraftRow($"{Name(order.TargetKey)} · {Policy(order.Policy)}\n{Warning(DiplomacyWarningText.Describe(order.Warning, Name(order.TargetKey)))}",
                () => _session!.Draft.ClearTradePolicy(target));
        }

        if (preview.Overtures.Count == 0 && preview.Declarations.Count == 0 && preview.Policies.Count == 0)
        {
            _detail.AddChild(Text("No diplomacy orders drafted.", "ImperialismQuiet"));
        }
    }

    private void DraftRow(string description, Action clear)
    {
        _detail!.AddChild(Text(description));
        _detail.AddChild(ActionButton("Clear this order", () => Change(clear)));
    }

    private void Change(Action change)
    {
        change();
        RefreshPreview();
    }

    private static string CountrySummary(DiplomaticCountryView country) => DiplomacyText.Summary(country);

    private static string Yes(bool value) => value ? "yes" : "no";
    private static string Policy(TradePolicy policy) => DiplomacyText.PolicyName(policy);
    // The sentence is Presentation's; the screen only labels it.
    private static string Warning(string sentence) =>
        sentence == DiplomacyWarningText.NoRefusal ? sentence : "Warning: " + sentence;
    private static string WarEffects(WarEffectsView? effects) => effects is null ? "No war effects predicted." :
        $"Breaks pact: {Yes(effects.BreaksPact)} · Closes posts: {Yes(effects.ClosesPosts)}\n" +
        $"Clears our policy: {Yes(effects.ClearsOurPolicy)} · Their policy: {Yes(effects.ClearsTheirPolicy)}\n" +
        $"Boycotts trade: {Yes(effects.BoycottsTrade)}";

    private static Label Text(string text, string style = "Label") => new()
    {
        Text = text,
        ThemeTypeVariation = style,
        AutowrapMode = TextServer.AutowrapMode.WordSmart,
        SizeFlagsHorizontal = SizeFlags.ExpandFill,
    };

    private static Button ActionButton(string text, Action action)
    {
        var button = new Button { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        button.Pressed += action;
        return button;
    }

    private static void Clear(Node parent)
    {
        foreach (var child in parent.GetChildren())
        {
            parent.RemoveChild(child);
            child.QueueFree();
        }
    }
}
