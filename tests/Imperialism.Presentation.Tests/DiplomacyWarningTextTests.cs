using System.Text.RegularExpressions;
using Imperialism.Core;
using Imperialism.Presentation;
using Xunit;

namespace Imperialism.Presentation.Tests;

/// <summary>
/// Holds every warning the Diplomacy screen can print to a plain sentence.
/// </summary>
/// <remarks>
/// The screen used to print the enum name split at its capitals — "Target Is AGreat
/// Power" — so each sentence is checked against both the name and that split form, and
/// against naming the country the order is aimed at.
/// </remarks>
public sealed class DiplomacyWarningTextTests
{
    private const string Target = "Portugal";

    [Fact]
    public void EveryOvertureWarningIsAPlainSentenceNamingTheTarget() =>
        AssertPlainAndNamed(Enum.GetValues<DiplomaticOvertureRefusal>(), DiplomacyWarningText.Describe,
            DiplomaticOvertureRefusal.NoSuchCountry);

    [Fact]
    public void EveryWarWarningIsAPlainSentenceNamingTheTarget() =>
        AssertPlainAndNamed(Enum.GetValues<WarDeclarationRefusal>(), DiplomacyWarningText.Describe,
            WarDeclarationRefusal.NoSuchCountry);

    [Fact]
    public void EveryTradePolicyWarningIsAPlainSentenceNamingTheTarget() =>
        AssertPlainAndNamed(Enum.GetValues<TradePolicyRefusal>(), DiplomacyWarningText.Describe,
            TradePolicyRefusal.NoSuchCountry);

    [Fact]
    public void APactOfferedToAGreatPowerSaysSoInTheTurnReportsWords()
    {
        var sentence = DiplomacyWarningText.Describe(DiplomaticOvertureRefusal.TargetIsAGreatPower, "France");
        Assert.Equal("Pacts are offered only to Minor Nations; France is a Great Power.", sentence);
    }

    [Fact]
    public void NoWarningMeansNoRefusalPredicted()
    {
        Assert.Equal(DiplomacyWarningText.NoRefusal, DiplomacyWarningText.Describe((DiplomaticOvertureRefusal?)null, Target));
        Assert.Equal(DiplomacyWarningText.NoRefusal, DiplomacyWarningText.Describe((WarDeclarationRefusal?)null, Target));
        Assert.Equal(DiplomacyWarningText.NoRefusal, DiplomacyWarningText.Describe((TradePolicyRefusal?)null, Target));
    }

    [Fact]
    public void AnUnnamedTargetIsThatCountry()
    {
        var sentence = DiplomacyWarningText.Describe(WarDeclarationRefusal.CountryEliminated, null);
        Assert.StartsWith("That country", sentence, StringComparison.Ordinal);
    }

    private static void AssertPlainAndNamed<T>(T[] values, Func<T?, string?, string> describe, T unnameable)
        where T : struct, Enum
    {
        var spoken = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in values)
        {
            var name = value.ToString();
            var split = Regex.Replace(name, "([a-z])([A-Z])", "$1 $2");
            foreach (var target in new[] { Target, null })
            {
                var sentence = describe(value, target);
                Assert.False(string.IsNullOrWhiteSpace(sentence), name);
                Assert.NotEqual(DiplomacyWarningText.NoRefusal, sentence);
                Assert.DoesNotContain(name, sentence, StringComparison.Ordinal);
                Assert.DoesNotContain(split, sentence, StringComparison.Ordinal);
                Assert.True(char.IsUpper(sentence[0]) && sentence.EndsWith('.'), $"{name}: {sentence}");
            }

            var named = describe(value, Target);
            if (!EqualityComparer<T>.Default.Equals(value, unnameable))
            {
                Assert.True(named.Contains(Target, StringComparison.Ordinal), $"{name} does not name the target: {named}");
            }

            Assert.True(spoken.Add(named), $"{name} shares its words with another warning: {named}");
        }
    }
}
