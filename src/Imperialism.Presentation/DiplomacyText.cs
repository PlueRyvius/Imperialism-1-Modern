using System.Globalization;
using Imperialism.Core;

namespace Imperialism.Presentation;

/// <summary>
/// The Diplomacy screen's labels and each country's summary, so the screen never turns
/// a Core enum name into text itself.
/// </summary>
public static class DiplomacyText
{
    /// <summary>What the "Drafted:" line says when nothing is drafted toward a country.</summary>
    public const string NoChange = "no change";

    /// <summary>The scroll an overture buys, as a title.</summary>
    public static string OvertureName(DiplomaticOvertureKind kind) => kind switch
    {
        DiplomaticOvertureKind.Consulate => "Trade Consulate",
        DiplomaticOvertureKind.Embassy => "Embassy",
        DiplomaticOvertureKind.Pact => "Non-Aggression Pact",
        DiplomaticOvertureKind.Peace => "Peace",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    public static string RelationName(CountryRelationMode mode) => mode switch
    {
        CountryRelationMode.Standard => "At peace",
        CountryRelationMode.Hostile => "Hostile",
        _ => throw new ArgumentOutOfRangeException(nameof(mode)),
    };

    /// <summary>A standing or drafted trade policy, as a short label.</summary>
    public static string PolicyName(TradePolicy policy) => policy.Kind switch
    {
        TradePolicyKind.None => "None",
        TradePolicyKind.Subsidy => string.Create(CultureInfo.InvariantCulture, $"Subsidy {policy.Percent}%"),
        TradePolicyKind.Boycott => "Boycott",
        _ => throw new ArgumentOutOfRangeException(nameof(policy)),
    };

    /// <summary>
    /// Everything drafted toward this country: each overture, a declaration of war and
    /// a trade policy change, in that order, or <see cref="NoChange"/>.
    /// </summary>
    public static string Drafted(DiplomaticCountryView country)
    {
        var parts = country.Overtures
            .Where(static option => option.IsDrafted)
            .Select(static option => OvertureName(option.Kind))
            .ToList();
        if (country.War.IsDrafted)
        {
            parts.Add("Declare War");
        }

        if (country.DraftedPolicy is { } policy)
        {
            parts.Add("Trade policy: " + PolicyName(policy));
        }

        return parts.Count == 0 ? NoChange : string.Join(", ", parts);
    }

    /// <summary>The block under a country's name in the list and in its detail.</summary>
    public static string Summary(DiplomaticCountryView country) =>
        $"{(country.IsGreatPower ? "Great Power" : "Minor nation")} · {RelationName(country.RelationMode)} · Provinces {country.ProvincesOwned}\n" +
        $"At war: {Yes(country.IsAtWar)} · War pending: {Yes(country.IsWarPending)} · Peace pending: {Yes(country.IsPeacePending)}\n" +
        $"Our posts: {Posts(country.OurConsulate, country.OurEmbassy)} · Their posts: {Posts(country.TheirConsulate, country.TheirEmbassy)}\n" +
        $"Pact ours / theirs: {Yes(country.OurPact)} / {Yes(country.TheirPact)}\n" +
        $"Our policy: {PolicyName(country.OurPolicy)} · Theirs: {PolicyName(country.TheirPolicy)}\n" +
        $"Drafted: {Drafted(country)}";

    private static string Posts(bool consulate, bool embassy) =>
        $"consulate {Yes(consulate)}, embassy {Yes(embassy)}";

    private static string Yes(bool value) => value ? "yes" : "no";
}
