using System.Globalization;

namespace Imperialism.Presentation;

/// <summary>
/// A count with its noun in the right number: "1 hull", "2 hulls".
/// </summary>
/// <remarks>
/// Every player-facing count goes through here, so the screens and the turn report
/// never print "1 hulls". Counts are formatted with thousands separators in the
/// invariant culture, which is how the turn report already prints them.
/// </remarks>
public static class Plural
{
    /// <summary>The count and its noun: <paramref name="plural"/> defaults to the singular plus "s".</summary>
    public static string Count(long count, string singular, string? plural = null) =>
        $"{Number(count)} {Noun(count, singular, plural)}";

    /// <summary>The noun alone, in the number <paramref name="count"/> needs.</summary>
    public static string Noun(long count, string singular, string? plural = null) =>
        count == 1 ? singular : plural ?? singular + "s";

    /// <summary>
    /// A quantity of a commodity. Commodity names are mass nouns or plurals
    /// ("Grain", "Horses", "Armaments"), so a single unit of a name ending in "s" is
    /// written "1 unit of Horses" rather than "1 Horses". A chosen rule: the content
    /// carries no singular form.
    /// </summary>
    public static string Quantity(long quantity, string commodityName) =>
        quantity == 1 && commodityName.EndsWith('s')
            ? $"1 unit of {commodityName}"
            : $"{Number(quantity)} {commodityName}";

    public static string Number(long count) => count.ToString("N0", CultureInfo.InvariantCulture);
}
