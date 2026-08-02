namespace Imperialism.Formats;

internal static class ScenarioFormat
{
    public const int NameFieldSize = 64;

    public static IReadOnlyDictionary<string, int> FieldCounts { get; } =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["cnam"] = 1,
            ["pnam"] = 1,
            ["zone"] = 1,
            ["tech"] = 2,
            ["year"] = 1,
            ["tyer"] = 2,
            ["cash"] = 2,
            ["tran"] = 2,
            ["capa"] = 3,
            ["army"] = 3,
            ["ware"] = 3,
            ["emba"] = 3,
            ["rela"] = 3,
            ["trea"] = 3,
            ["port"] = 1,
            ["rail"] = 1,
            ["deve"] = 2,
            ["civi"] = 2,
            ["labo"] = 4,
            ["tclr"] = 1,
            ["tbar"] = 3,
            ["ship"] = 4,
            ["coun"] = 2,
            ["flag"] = 1,
        };

    public static ISet<string> NameTags { get; } = new HashSet<string>(
        ["cnam", "pnam", "zone"], StringComparer.Ordinal);
}
