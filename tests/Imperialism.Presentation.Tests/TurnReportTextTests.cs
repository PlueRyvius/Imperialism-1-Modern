using Imperialism.Core;
using Imperialism.Presentation;
using Xunit;

namespace Imperialism.Presentation.Tests;

/// <summary>
/// Holds every enumeration a report can print to having its own words.
/// </summary>
/// <remarks>
/// <b>This is the only thing enforcing that.</b> The compiler will not help: a
/// switch expression over an enum needs a discard arm whatever it handles,
/// because any integer is a possible value, so the discard is always present and
/// CS8509 never fires. Add a refusal reason to Core without adding a clause here
/// and the first player to provoke it gets an exception in the middle of their
/// turn report — unless this test caught it first.
/// </remarks>
public sealed class TurnReportTextTests
{
    [Fact]
    public void EveryCivilianRefusalHasItsOwnWords() =>
        AssertDistinctAndSpoken(Enum.GetValues<CivilianOrderRefusal>(), TurnReportText.Describe);

    [Fact]
    public void EveryTechnologyRefusalHasItsOwnWords() =>
        AssertDistinctAndSpoken(Enum.GetValues<TechnologyPurchaseRefusal>(), TurnReportText.Describe);

    [Fact]
    public void EveryTradeRefusalHasItsOwnWords() =>
        AssertDistinctAndSpoken(Enum.GetValues<TradeRefusal>(), TurnReportText.Describe);

    [Fact]
    public void EveryStructureAnEngineerBuildsHasItsOwnWords() =>
        AssertDistinctAndSpoken(Enum.GetValues<EngineerConstruction>(), TurnReportText.Describe);

    [Fact]
    public void EveryDeliverySourceHasItsOwnWords() =>
        AssertDistinctAndSpoken(Enum.GetValues<PendingDeliverySource>(), TurnReportText.Describe);

    [Fact]
    public void TheCivilianRefusalsAreTheTwentyFiveCoreDeclares()
    {
        // A count is a blunt instrument, but it is the one assertion that fails
        // loudly when the enumeration grows rather than when someone provokes it.
        Assert.Equal(25, Enum.GetValues<CivilianOrderRefusal>().Length);
    }

    [Fact]
    public void AValueCoreNeverDeclaresIsRejectedRatherThanPrinted()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => TurnReportText.Describe((TradeRefusal)200));
    }

    private static void AssertDistinctAndSpoken<T>(T[] values, Func<T, string> describe)
        where T : struct, Enum
    {
        var spoken = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in values)
        {
            var text = describe(value);
            Assert.False(string.IsNullOrWhiteSpace(text), $"{value} has no words.");

            // Clauses are spliced after a colon, so a leading capital or a
            // trailing stop would show up mid-sentence.
            Assert.False(char.IsUpper(text[0]), $"{value} starts with a capital: '{text}'.");
            Assert.DoesNotContain(text, ".", StringComparison.Ordinal);
            Assert.True(spoken.Add(text), $"{value} repeats another member's words: '{text}'.");
        }
    }
}
