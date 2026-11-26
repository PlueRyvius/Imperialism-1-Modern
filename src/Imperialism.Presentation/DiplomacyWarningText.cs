using Imperialism.Core;

namespace Imperialism.Presentation;

/// <summary>
/// The sentence the Diplomacy screen prints under an order the preview expects Core to
/// refuse.
/// </summary>
/// <remarks>
/// The client never composes game text, so the screen passes the warning and the target's
/// name here and prints what comes back. Each sentence is built from the same clause
/// <see cref="TurnReportText"/> uses for that refusal, so the warning before End Turn and
/// the report after it say the same thing. Where a refusal is about the target, the
/// sentence names it. <c>NoSuchCountry</c> is the one value that cannot: there is no
/// country to name.
///
/// <paramref name="targetName"/> may be null, and then the sentence says "that country".
/// A null warning means the preview expects the order to be carried out.
/// </remarks>
public static class DiplomacyWarningText
{
    public const string NoRefusal = "No refusal predicted.";

    public static string Describe(DiplomaticOvertureRefusal? warning, string? targetName)
    {
        if (warning is not { } reason)
        {
            return NoRefusal;
        }

        var who = targetName ?? "that country";
        var clause = TurnReportText.Describe(reason);
        return Sentence(reason switch
        {
            DiplomaticOvertureRefusal.NoSuchCountry => clause,
            DiplomaticOvertureRefusal.Itself => $"{who} is our own country, and {clause}",
            DiplomaticOvertureRefusal.NotAGreatPower => $"{clause}, so none can go to {who}",
            DiplomaticOvertureRefusal.AlreadyOrderedThisTurn => $"the same overture to {who} was already made this turn",
            DiplomaticOvertureRefusal.NotOffered => $"this world sets no price for an overture to {who}",
            DiplomaticOvertureRefusal.AlreadyEstablished => $"we already have one with {who}",
            DiplomaticOvertureRefusal.AtWar => $"we are at war, or going to war, with {who}",
            DiplomaticOvertureRefusal.NoConsulate => $"{clause}, and we have none in {who}",
            DiplomaticOvertureRefusal.NoEmbassy => $"{clause}, and we have none in {who}",
            DiplomaticOvertureRefusal.TargetIsAGreatPower => $"{clause}; {who} is a Great Power",
            DiplomaticOvertureRefusal.NotEnoughCash => $"the treasury will not cover the overture to {who}",
            DiplomaticOvertureRefusal.NotAtWar => $"we are not at war with {who}",
            DiplomaticOvertureRefusal.Declined => $"{who} did not offer peace in return",
            DiplomaticOvertureRefusal.AlreadyMadeThisTurn => $"peace with {who} was already made this turn on their offer",
            DiplomaticOvertureRefusal.CountryEliminated => $"{who} has been eliminated, and there is no government to receive it",
            _ => throw new ArgumentOutOfRangeException(nameof(warning)),
        });
    }

    public static string Describe(WarDeclarationRefusal? warning, string? targetName)
    {
        if (warning is not { } reason)
        {
            return NoRefusal;
        }

        var who = targetName ?? "that country";
        var clause = TurnReportText.Describe(reason);
        return Sentence(reason switch
        {
            WarDeclarationRefusal.NoSuchCountry => clause,
            WarDeclarationRefusal.Itself => $"{who} is our own country, and {clause}",
            WarDeclarationRefusal.AlreadyAtWar => $"we are already at war with {who}, or they declared first this turn",
            WarDeclarationRefusal.AlreadyDeclaredThisTurn => $"war on {who} was already declared this turn",
            WarDeclarationRefusal.CountryEliminated => $"{who} has been eliminated and its lands are in anarchy",
            _ => throw new ArgumentOutOfRangeException(nameof(warning)),
        });
    }

    public static string Describe(TradePolicyRefusal? warning, string? targetName)
    {
        if (warning is not { } reason)
        {
            return NoRefusal;
        }

        var who = targetName ?? "that country";
        var clause = TurnReportText.Describe(reason);
        return Sentence(reason switch
        {
            TradePolicyRefusal.NoSuchCountry => clause,
            TradePolicyRefusal.Itself => $"{who} is our own country, and {clause}",
            TradePolicyRefusal.NotAGreatPower => $"{clause}, so none can be set toward {who}",
            TradePolicyRefusal.AlreadyOrderedThisTurn => $"a trade policy toward {who} was already set this turn",
            TradePolicyRefusal.NoPost => $"it needs a trade consulate or an embassy in {who}, open as the turn began",
            TradePolicyRefusal.PercentNotOffered => $"this world offers no subsidy of that size to {who}",
            TradePolicyRefusal.CountryEliminated => $"{who} has been eliminated and trades with nobody",
            _ => throw new ArgumentOutOfRangeException(nameof(warning)),
        });
    }

    private static string Sentence(string text) => char.ToUpperInvariant(text[0]) + text[1..] + ".";
}
