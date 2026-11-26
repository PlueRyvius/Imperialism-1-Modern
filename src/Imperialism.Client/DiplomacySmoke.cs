using System.Globalization;
using Godot;
using Imperialism.Core;

namespace Imperialism.Client;

/// <summary>A deterministic draft/preview/resolve probe, separate from the empty-turn gate.</summary>
internal static class DiplomacySmoke
{
    public static bool Run(GameSession session)
    {
        var target = session.Preview().Diplomacy.Countries.FirstOrDefault(country =>
            country.IsGreatPower && !country.OurConsulate);
        if (target is null)
        {
            GD.PushError("DIPLOMACY_SMOKE_FAIL: no other Great Power without our consulate exists.");
            return false;
        }

        session.Draft.SetOverture(DiplomaticOvertureKind.Consulate,
            session.Package.Catalog.GetCountryId(target.CountryKey), true);
        var preview = session.Preview().Diplomacy;
        var option = preview.Countries.First(country => country.CountryKey == target.CountryKey)
            .Overtures.First(overture => overture.Kind == DiplomaticOvertureKind.Consulate);
        GD.Print(string.Create(CultureInfo.InvariantCulture,
            $"DIPLOMACY_SMOKE_PREVIEW target={target.CountryKey} overture=Consulate price={option.Price} " +
            $"warning={option.Warning?.ToString() ?? "none"} CashAtOpen={preview.CashAtOpen} " +
            $"ClaimedBeforeDiplomacy={preview.ClaimedBeforeDiplomacy} ClaimedByOvertures={preview.ClaimedByOvertures} " +
            $"CashAfterOvertures={preview.CashAfterOvertures}"));
        var report = session.EndTurn();
        foreach (var phase in report.Phases)
        {
            foreach (var line in phase.Lines)
            {
                GD.Print($"DIPLOMACY_SMOKE_EVENT phase={phase.Phase} country={line.CountryName} {line.Text}");
            }
        }

        if (report.EventCount == 0)
        {
            GD.PushError("DIPLOMACY_SMOKE_FAIL: the resolved turn emitted no events.");
            return false;
        }

        GD.Print($"DIPLOMACY_SMOKE_OK turn={report.TurnNumber} events={report.EventCount}");
        return true;
    }
}
