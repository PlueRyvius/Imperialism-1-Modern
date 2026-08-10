using System.Globalization;
using Godot;
using Imperialism.Presentation;

namespace Imperialism.Client;

/// <summary>
/// The persistent border. The manual keeps a country's standing on the edge of
/// every screen — the workforce down the Industry screen's left border with the
/// labour total under a muscular arm, the commodities the workers need along the
/// Bid and Offers border — so it lives outside the screen stack and survives
/// navigation.
/// </summary>
/// <remarks>
/// <b>This class formats and never computes.</b> Every number it shows is a
/// property of <see cref="CountryStatusView"/>, worked out by Core. If a
/// readout here ever needs a sum, a difference or a comparison, that arithmetic
/// belongs in the snapshot and not in a Godot script — see the "logic in the
/// client" trap in <c>docs/architecture.md</c>.
/// </remarks>
public sealed partial class StatusBorder : PanelContainer
{
    private readonly Dictionary<string, Label> _readouts = new(StringComparer.Ordinal);
    private Label? _country;
    private Label? _date;

    public StatusBorder()
    {
        Name = "StatusBorder";
        ThemeTypeVariation = "ImperialismStatusBorder";

        // Two stacked lines of text plus the wood's own content margins. Without
        // a floor the surrounding box gives the border whatever is left over and
        // clips the values off the bottom of the window.
        CustomMinimumSize = new Vector2(0, 34);
        SizeFlagsVertical = SizeFlags.ShrinkEnd;
    }

    public override void _Ready() => Build();

    public void Show(CountryStatusView status)
    {
        ArgumentNullException.ThrowIfNull(status);
        if (_country is null || _date is null)
        {
            Build();
        }

        _country!.Text = status.IsGreatPower
            ? $"{status.CountryName}  —  Great Power"
            : status.CountryName;
        _date!.Text = status.CurrentDate.ToString();

        Set("Treasury", Money(status.Cash));
        Set("Labour", Count(status.AvailableLabour));
        Set("Workers", Count(status.TotalWorkers));
        Set("Transport", Count(status.TransportCapacity));
        Set("Holds", Count(status.MerchantMarine));
        foreach (var grade in status.Workforce)
        {
            Set(grade.Grade.ToString(), $"{Count(grade.Healthy)} / {Count(grade.Sick)} ill");
        }
    }

    private void Set(string caption, string value)
    {
        if (_readouts.TryGetValue(caption, out var label))
        {
            label.Text = value;
        }
    }

    private static string Money(long amount) =>
        "$" + amount.ToString("N0", CultureInfo.InvariantCulture);

    private static string Count(long amount) => amount.ToString("N0", CultureInfo.InvariantCulture);

    private void Build()
    {
        if (_country is not null)
        {
            return;
        }

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 14);
        AddChild(row);

        _country = new Label
        {
            ThemeTypeVariation = "ImperialismReadout",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            Text = "—",
        };
        row.AddChild(_country);

        foreach (var caption in new[]
                 {
                     "Treasury", "Labour", "Workers", "Transport", "Holds",
                     "Untrained", "Trained", "Expert",
                 })
        {
            row.AddChild(BuildReadout(caption));
        }

        _date = new Label { ThemeTypeVariation = "ImperialismReadout", Text = "—" };
        row.AddChild(_date);
    }

    private Control BuildReadout(string caption)
    {
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 0);
        column.AddChild(new Label
        {
            ThemeTypeVariation = "ImperialismReadoutLabel",
            Text = caption.ToUpperInvariant(),
        });

        var value = new Label { ThemeTypeVariation = "ImperialismReadout", Text = "—" };
        column.AddChild(value);
        _readouts[caption] = value;
        return column;
    }
}
