namespace Imperialism.Presentation;

/// <summary>
/// The newspaper's own words: its masthead, its headlines, and the one fixed item
/// a quiet quarter prints.
/// </summary>
/// <remarks>
/// The wording is ours, in a plain period voice. None of it is copied from the
/// original, whose story strings sit in <c>STR#ENU.GOB</c> and are still unread
/// (<c>docs/disasm/wanted-values.md</c> §10). The one exception is "New Invention",
/// which the manual quotes as the headline's title (lines 4096–4099).
/// A story's body is the turn report's own sentence for the same event, so the
/// names and numbers are formatted in exactly one place.
/// </remarks>
public static class NewspaperText
{
    /// <summary>The manual's own title for the story (lines 4096–4099).</summary>
    public const string NewInvention = "New Invention";

    public const string QuietQuarterHeadline = "A Quiet Quarter";

    public const string QuietQuarterBody =
        "Our correspondents report nothing of consequence this quarter.";

    /// <summary>A placeholder name: the original's masthead is not recovered.</summary>
    public static string Masthead(string readerName) => $"The {readerName} Gazette";

    public static string InventionBody(string technology) =>
        $"Word arrives of a new invention: {technology}. Every power may invest in it from this quarter.";

    public static string Eliminated(string country) => $"{country} Falls";

    public static string WarDeclared(string country, string target) =>
        $"{country} Declares War on {target}";

    public static string PeaceMade(string country, string target) =>
        $"Peace Between {country} and {target}";

    public static string ProvinceCaptured(string province, string newOwner) =>
        $"{province} Taken by {newOwner}";

    public static string NavalBattle(string seaZone) => $"Battle at Sea in {seaZone}";

    public static string Landing(string country, string province) =>
        $"{country} Lands Troops in {province}";

    public static string LandBattle(string province) => $"Battle for {province}";

    public static string MerchantIntercepted(string carrier) => $"{carrier} Merchant Intercepted";

    public static string Hunger(string country) => $"Hunger and Sickness in {country}";

    public static string CiviliansLost() => "Civilians Lost for Want of a Capital";

    public static string RegimentsLost() => "Regiments Lost for Want of a Capital";

    public static string ShipsLost() => "Ships Lost for Want of a Capital";

    public static string TiesCut(string country, string target) =>
        $"Ties Cut Between {country} and {target}";

    public static string MarketReport(string commodity) => $"Market Report: {commodity}";

    public static string WorkersRecruited() => "New Workers at the Capitol";

    public static string ShipsLaunched(string shipType) => $"{shipType} Launched";

    public static string CiviliansArrived() => "New Civilians Report for Duty";

    public static string RegimentsMustered() => "New Regiments Muster";

    public static string Investment(string country, string technology) =>
        $"{country} Invests in {technology}";

    public static string ConstructionCompleted() => "Engineering Works Completed";
}
