using System.Globalization;
using System.Text.RegularExpressions;
using Imperialism.Content;
using Imperialism.Core;
using Imperialism.Formats;

namespace Imperialism.LegacyImport;

public sealed record LegacyImportOptions(string PackageKey);

public sealed record LegacyImportResult(
    WorldContentDocument? Document,
    LegacyImportReport Report)
{
    public bool Success => Document is not null && !Report.HasErrors;
}

/// <summary>
/// One legacy terrain code's key stem, display name, improvability, and whether
/// a Prospector may search it — and if so, at what price in knowledge.
/// </summary>
/// <remarks>
/// <paramref name="Prospecting"/> is a tri-state and the middle case is the easy
/// one to lose: <see cref="LegacyProspecting.No"/> means the ground hides
/// nothing, <see cref="LegacyProspecting.Open"/> means search it from turn one,
/// and <see cref="LegacyProspecting.NeedsOilDrilling"/> means the eye cursor
/// appears only once the country has invested.
/// </remarks>
internal readonly record struct LegacyTerrain(
    string Name,
    string DisplayName,
    bool IsImprovable,
    LegacyProspecting Prospecting = LegacyProspecting.No,
    string? Rail = null,
    long RailCost = 0);

internal enum LegacyProspecting : byte
{
    No,
    Open,
    NeedsOilDrilling,
}

/// <summary>One class of ship: its key, cargo, technology gate and combat numbers.</summary>
internal readonly record struct LegacyShipType(
    string Key,
    string Name,
    long Cargo = 0,
    LegacyShipCombat? Combat = null,
    string? RequiredTechnology = null);

/// <summary>A hull's fighting numbers, from the manual's Ship Type table.</summary>
/// <remarks>
/// Battle movement is deliberately absent. The manual's table carries it, and nothing here
/// models fleet movement — recording a number no reader can interpret would invite it being
/// mistaken for sailing speed, which is the error that produced a misaligned column in the
/// first place.
/// </remarks>
internal readonly record struct LegacyShipCombat(
    long Firepower,
    long Range,
    long Armour,
    long Hull,
    long Speed);

public static class LegacyWorldConverter
{
    /// <summary>
    /// The seventeen legacy terrain codes, their display names from the
    /// manual's Terrain Tiles Table, and whether a civilian can improve them.
    /// </summary>
    /// <remarks>
    /// The codes and the table line up one for one — fourteen land types plus
    /// town, capital and ocean — which is what lets "dry plains" be identified
    /// with code 1 despite our key for it being <c>clear</c>.
    /// <para>
    /// Improvability is the manual's: the table gives every terrain a civilian
    /// worker, and dry plains, horse ranch and scrub forest get "None". Towns
    /// and capitals admit only the Engineer, who builds rather than improves,
    /// and the manual says a capital already produces at the highest level it
    /// can. The corpus corroborates without exception — of 481 <c>deve</c>
    /// records across five scenarios, none lands on any of these.
    /// </para>
    /// <para>
    /// Prospectability is the same table read for its second column. Barren
    /// hills and mountains list "Miner, Prospector" and swamp, desert and tundra
    /// list "Driller, Prospector"; every other terrain names no Prospector at
    /// all, because it announces what it holds by being what it is. The oil
    /// three are gated: "when your country invests in Oil Drilling technology,
    /// the eye cursor appears over unprospected swamps, deserts, and tundra as
    /// well."
    /// </para>
    /// </remarks>
    private static readonly IReadOnlyDictionary<byte, LegacyTerrain> Terrains =
        new Dictionary<byte, LegacyTerrain>
        {
            [0] = new("ocean", "Ocean", false),
            [1] = new("clear", "Dry Plains", false, Rail: SteamEngine, RailCost: OpenGroundRail),
            [2] = new("cotton", "Plantation", true, Rail: SteamEngine, RailCost: OpenGroundRail),
            [3] = new("cattle-ranch", "Open Range", true, Rail: SteamEngine, RailCost: OpenGroundRail),
            [4] = new("horse-ranch", "Horse Ranch", false, Rail: SteamEngine, RailCost: OpenGroundRail),
            [5] = new("grain-farm", "Farm", true, Rail: SteamEngine, RailCost: OpenGroundRail),
            [6] = new("orchard", "Orchard", true, Rail: SteamEngine, RailCost: OpenGroundRail),
            [7] = new("wool-hill", "Fertile Hills", true, Rail: CompoundSteamEngine, RailCost: HillRail),
            [8] = new(
                "hill", "Barren Hills", true, LegacyProspecting.Open, CompoundSteamEngine, HillRail),
            [9] = new("mountain", "Mountains", true, LegacyProspecting.Open, Dynamite, MountainRail),
            [10] = new(
                "swamp", "Swamp", true, LegacyProspecting.NeedsOilDrilling, IronRailroadBridge, SwampRail),
            [11] = new(
                "desert", "Desert", true, LegacyProspecting.NeedsOilDrilling, SteamEngine, OpenGroundRail),
            [12] = new(
                "tundra", "Tundra", true, LegacyProspecting.NeedsOilDrilling, SteamEngine, RoughGroundRail),
            [13] = new("forest", "Hardwood Forest", true, Rail: SteamEngine, RailCost: RoughGroundRail),
            [14] = new("town", "Town", false, Rail: SteamEngine, RailCost: OpenGroundRail),
            [15] = new("scrub-forest", "Scrub Forest", false, Rail: SteamEngine, RailCost: RoughGroundRail),
            [16] = new("capital", "Capital", false, Rail: SteamEngine, RailCost: OpenGroundRail),
        };

    /// <summary>
    /// What a tile of rail costs, by the ground it crosses.
    /// </summary>
    /// <remarks>
    /// <b>This replaces the weakest number in the project with an observed one.</b>
    /// A flat $500 shipped with the Engineer slice and was labelled "a guess.
    /// Nothing supports it at all"; the price list gives rail per terrain, which
    /// is also why the price now lives on <see cref="RailContent"/> beside the
    /// technology gate rather than in <c>construction</c> beside the depot and the
    /// port. A terrain that cannot carry rail needs no price.
    /// <para>
    /// The list names five grounds and this engine has seventeen terrain codes, so
    /// each constant covers the codes that ground plainly means. "Plains, farm,
    /// desert" is <see cref="OpenGroundRail"/> and takes the plantation, the open
    /// range, the horse ranch, the orchard, the town and the capital with them —
    /// the same reading that already gives towns and capitals the plains *gate*.
    /// "Tundra and either forest" is <see cref="RoughGroundRail"/>, which is the
    /// one place the list is explicit that two terrains sharing a name share a
    /// price. Fertile hills take the hills price for the same reason they take the
    /// hills gate. See <c>docs/formulas/engineer.md</c>.
    /// </para>
    /// <para>
    /// <b>Mountains are the one ground the list does not price, and they are the
    /// one guess left in this table.</b> They take swamp's price — the most
    /// expensive ground that *is* attested — rather than a fifth invented number,
    /// because inventing one is what the flat $500 did. The corpus cannot check it
    /// either way: no shipped power holds Dynamite and no shipped scenario rails a
    /// single mountain.
    /// </para>
    /// </remarks>
    private const long OpenGroundRail = 100;
    private const long RoughGroundRail = 150;
    private const long HillRail = 200;
    private const long SwampRail = 300;
    private const long MountainRail = SwampRail;

    /// <summary>
    /// The four technologies the Benefits of Technology Table names for rail.
    /// </summary>
    /// <remarks>
    /// "Allows Engineers to build railroads through farms, plains, deserts,
    /// forests, and tundra" (High Pressure Steam Engine); "through swamps" (Iron
    /// Railroad Bridge); "through hills" (Compound Steam Engine); "through
    /// mountains" (Dynamite). Every power starts with the first, so an 1815 start
    /// can already build across most of its land.
    /// <para>
    /// <b>Held by name, not by table position.</b> They used to be positions, and
    /// that made <see cref="TechnologyTable"/>'s order load-bearing twice over: a
    /// reorder silently rewired every gate while looking like a rename. A name
    /// survives a reorder because <see cref="TechnologyKey(string)"/> is derived
    /// from it. The same applies to <see cref="ResourceTechnologyLadders"/> and to
    /// <see cref="OilDrilling"/>.
    /// </para>
    /// <para>
    /// <b>Two readings here are inferences and are flagged in
    /// <c>docs/formulas/engineer.md</c>.</b> Fertile Hills takes the hills gate,
    /// because the manual says "hills" without qualification and this project
    /// does not invent permission. Towns and capitals take the plains gate,
    /// because a capital must be railable or it could not be the hub every depot
    /// connects to; the manual lists neither.
    /// </para>
    /// </remarks>
    private const string SteamEngine = "High Pressure Steam Engine";
    private const string IronRailroadBridge = "Iron Railroad Bridge";
    private const string CompoundSteamEngine = "Compound Steam Engine";
    private const string Dynamite = "Dynamite";

    /// <summary>Oil Drilling, which gates prospecting swamp, desert and tundra.</summary>
    private const string OilDrilling = "Oil Drilling";

    /// <summary>
    /// One entry of the technology table: what it is called, what investing in it
    /// costs, the year it becomes available world-wide, and what a country must
    /// already know before it may be bought.
    /// </summary>
    /// <remarks>
    /// <paramref name="Cost"/> is null for the two every power starts holding.
    /// **They are unpurchasable rather than free**, which is a different fact: a
    /// price of zero would put them on the Investment screen at no charge, and
    /// nobody can ever buy what they already have.
    /// <para>
    /// <paramref name="Prerequisites"/> are names rather than positions, for the
    /// same reason the rail gates are.
    /// </para>
    /// </remarks>
    private readonly record struct LegacyTechnology(
        string Name,
        long? Cost,
        int AvailableFrom,
        params string[] Prerequisites);

    /// <summary>
    /// The technology table: names, order, costs, arrival years and
    /// prerequisites. **The order is load-bearing**: a <c>tech</c> record is
    /// <c>[country, id]</c> with a 1-based id and nothing naming it, and this
    /// list is what an id is resolved against.
    /// </summary>
    /// <remarks>
    /// <b>The order and the prices are the executable's now</b>, and the recovered
    /// order turns out to be the manual's printed one. `STR#ENU.GOB` blocks
    /// #1073–#1075 hold all 28 names in progression order and the technology store
    /// reads a 28-entry cash table at <c>0x0066AAE8</c> indexed by that same
    /// position; both are recorded in
    /// <c>docs/disasm/definitive-original-data.md</c>. The wiki ordering this table
    /// used to carry — differing at positions 4–7 and 13–14 — is **retracted, not
    /// superseded**. The corpus never could choose between the two, and in the end
    /// did not have to.
    /// <para>
    /// <b>The wiki's price column was off by one, and the binary shows exactly
    /// where.</b> From Streamlined Hulls onwards each wiki price is the price of the
    /// *next* technology in the recovered order — Bessemer Converter carried Compound
    /// Steam Engine's 6,000, Oil Drilling carried Barbed Wire's 25,000, Machine Guns
    /// carried Chemistry's 100,000 — which is why twelve of the twenty-six moved here
    /// and why the last entry did not have to. A slip that reproduces itself for
    /// twenty-four consecutive rows is not two sources disagreeing; it is one source
    /// wrong in a legible way.
    /// </para>
    /// <para>
    /// <b>The arrival years are derived, and the derivation is corroborated
    /// twenty-five times.</b> The executable stores no year: it generates each
    /// non-starting technology an inclusive pseudo-random <em>turn-offset</em> window
    /// from 26 two-word entries at <c>0x0066ABA4</c>. Read as one offset per year
    /// from the 1815 epoch the corpus already established, **25 of the wiki's 26
    /// years fall inside their window and 19 sit exactly on its minimum**. Chemistry
    /// is the single miss, by one year, and is also one of the two rows the wiki had
    /// out of order. So the year below is <c>1815 + window minimum</c> — the earliest
    /// anybody may buy, which is what the field means — and the wiki's scattered
    /// later dates read as single draws from a range rather than as a table.
    /// </para>
    /// <para>
    /// <b>The prerequisites are still the wiki's, and are now the weakest column
    /// here.</b> The executable's prerequisite graph has not been recovered, and the
    /// price column's slip is a reason to trust the rest of that source less rather
    /// than more. Every edge still points backwards under the recovered order, which
    /// is the only check available. See <c>docs/formulas/technology.md</c>.
    /// </para>
    /// <para>
    /// Names stay the manual's where the sources disagree: "Steel and Iron Plows"
    /// over "Steel Plows", "Fertiliser" over "Fertilizer", "Armour" over "Armor".
    /// The keys are name-derived and already shipped, so a rename would be a
    /// content break for no gain.
    /// </para>
    /// <para>
    /// Only the entries this engine can act on are given a gate below. Regiments,
    /// ships, the Refinery and rail-through-terrain are named here so the
    /// numbering is right and modelled nowhere.
    /// </para>
    /// </remarks>
    private static readonly IReadOnlyList<LegacyTechnology> TechnologyTable =
    [
        new(SteamEngine, null, 1815),
        new(SeedDrill, null, 1815),
        new(CottonGin, 1_000, 1816),
        new(StreamlinedHulls, 1_000, 1821),
        new(SquareSetTimbering, 1_500, 1821),
        new(IronRailroadBridge, 1_500, 1821),
        new(FeedGrasses, 1_500, 1821),
        new(SpinningJenny, 1_500, 1826, CottonGin, FeedGrasses),
        new(Paddlewheels, 3_000, 1826),
        new(SteelAndIronPlows, 3_000, 1831, SeedDrill),
        new(BessemerConverter, 3_000, 1836),
        new(CompoundSteamEngine, 6_000, 1836, IronRailroadBridge),
        new(RifledArtillery, 7_000, 1841),
        new(BreechLoadingRifles, 10_000, 1841, BessemerConverter),
        new(AdvancedIronWorking, 12_000, 1846),
        new(PowerLoom, 12_000, 1846, SpinningJenny),
        new(MechanicalReaper, 12_000, 1851, SteelAndIronPlows),
        new(CommercialFertiliser, 12_000, 1856, SteelAndIronPlows),
        new(OilDrilling, 12_000, 1856),
        new(BarbedWire, 25_000, 1861, FeedGrasses),
        new(SteelArmourPlate, 20_000, 1866, AdvancedIronWorking),
        new("Large Artillery", 40_000, 1871, RifledArtillery),
        new(Dynamite, 40_000, 1871, CompoundSteamEngine, SquareSetTimbering),
        new(MarineEngineering, 40_000, 1871, SteelArmourPlate),
        new("Machine Guns", 40_000, 1876, BreechLoadingRifles),
        new(Chemistry, 100_000, 1876, OilDrilling, BarbedWire),
        new("Improved Range-Finding", 120_000, 1881, MarineEngineering),
        new(InternalCombustion, 150_000, 1881, Chemistry),
    ];

    private const string SeedDrill = "Seed Drill";
    private const string CottonGin = "Cotton Gin";
    private const string FeedGrasses = "Feed Grasses";
    private const string SquareSetTimbering = "Square-Set Timbering";
    private const string SpinningJenny = "Spinning Jenny";
    private const string SteelAndIronPlows = "Steel and Iron Plows";
    private const string BessemerConverter = "Bessemer Converter";
    private const string BreechLoadingRifles = "Breech-Loading Rifles";
    private const string RifledArtillery = "Rifled Artillery";
    private const string AdvancedIronWorking = "Advanced Iron Working";
    private const string PowerLoom = "Power Loom";
    private const string MechanicalReaper = "Mechanical Reaper";
    private const string CommercialFertiliser = "Commercial Fertiliser";
    private const string BarbedWire = "Barbed Wire";
    private const string SteelArmourPlate = "Steel Armour Plate";
    private const string MarineEngineering = "Marine Engineering";
    private const string Chemistry = "Chemistry";
    private const string InternalCombustion = "Internal Combustion";

    /// <summary>
    /// The two technologies every power starts with, whatever the scenario says:
    /// "every player always starts with the first two technologies listed below:
    /// High Pressure Steam Engine and Seed Drill".
    /// </summary>
    /// <remarks>
    /// This is one of the seven engine defaults <c>docs/formulas/_index.md</c>
    /// calls unrecoverable from the corpus, and it is recovered — from the
    /// manual. A skirmish carries no <c>tech</c> record and its powers still
    /// start able to farm. They are also the two the price list gives no price,
    /// which is the same fact from the other side.
    /// </remarks>
    private static readonly string[] StartingTechnologies = [SteamEngine, SeedDrill];

    /// <summary>
    /// The Benefits of Technology Table read as a ladder: what it takes to raise
    /// each deposit to level 1, 2 and 3. Null means the rung is ungated, which is
    /// true only of a mine opening at Level I.
    /// </summary>
    /// <remarks>
    /// Cross-checked row by row against the seven gates already transcribed in
    /// <c>docs/reference/manual-mechanics.md</c>; every one agrees. Fish and
    /// horses are absent because no civilian improves them at all.
    /// </remarks>
    private static readonly IReadOnlyDictionary<byte, string?[]> ResourceTechnologyLadders =
        new Dictionary<byte, string?[]>
        {
            [17] = [SeedDrill, SteelAndIronPlows, MechanicalReaper],       // grain
            [18] = [SeedDrill, SteelAndIronPlows, CommercialFertiliser],   // fruit
            [0] = [CottonGin, SpinningJenny, PowerLoom],                   // cotton
            [1] = [FeedGrasses, SpinningJenny, PowerLoom],                 // wool
            [20] = [FeedGrasses, BarbedWire, Chemistry],                   // livestock
            [2] = [IronRailroadBridge, CompoundSteamEngine, Dynamite],     // timber
            [3] = [null, SquareSetTimbering, Dynamite],                    // coal
            [4] = [null, SquareSetTimbering, Dynamite],                    // iron
            [21] = [null, SquareSetTimbering, Dynamite],                   // gems
            [22] = [null, SquareSetTimbering, Dynamite],                   // gold
            [6] = [OilDrilling, Chemistry, InternalCombustion],            // oil
        };

    /// <summary>
    /// The stable external key for a technology, derived from its name — which is
    /// what makes every gate above survive a reordering of the table.
    /// </summary>
    private static string TechnologyKey(string name) =>
        $"technology.{name.ToLowerInvariant().Replace(' ', '-')}";

    /// <summary>Resolves a 1-based <c>tech</c> id against the table.</summary>
    private static string TechnologyKeyAt(int position) =>
        TechnologyKey(TechnologyTable[position - 1].Name);

    /// <summary>
    /// Deposits a Prospector must find first: "coal, iron, gold, gems, and oil
    /// must be found by a Prospector before they can be exploited by your other
    /// civilians". Everything else is announced by its terrain.
    /// </summary>
    private static readonly IReadOnlySet<byte> HiddenResources =
        new HashSet<byte>
        {
            3,  // coal
            4,  // iron
            6,  // oil
            21, // gems
            22, // gold
        };

    /// <summary>
    /// The civilian types this content declares, in the order the 1997
    /// <c>civi</c> record numbers them.
    /// </summary>
    /// <remarks>
    /// Codes 0 to 5 are the six the corpus ships, identified from where they
    /// stand: type 4 is the only one found in towns, where the manual says only
    /// the Engineer may work; type 5 is found on fertile hills and open range,
    /// which are the Rancher's two terrains; type 2 on plantations, farms and
    /// orchards, which are the Farmer's three; type 3 in hardwood forest. The
    /// skirmishes settle the last pair — <c>s11</c> and <c>s15</c> give each of
    /// the seven powers exactly one type 1 and one type 4, a Prospector and an
    /// Engineer.
    /// <para>
    /// The Driller is appended because the Resource Development Table names it
    /// as oil's improver and the deposits must be able to refer to it, even
    /// though no <c>civi</c> record in the corpus is one. The Developer and the
    /// Fisherman are left out: neither improves anything, so nothing would
    /// reference them.
    /// </para>
    /// </remarks>
    private static readonly IReadOnlyList<(string Name, string DisplayName, CivilianWorkKind Work)> CivilianTypes =
    [
        ("miner", "Miner", CivilianWorkKind.Improve),
        ("prospector", "Prospector", CivilianWorkKind.Prospect),
        ("farmer", "Farmer", CivilianWorkKind.Improve),
        ("forester", "Forester", CivilianWorkKind.Improve),
        ("engineer", "Engineer", CivilianWorkKind.Construct),
        ("rancher", "Rancher", CivilianWorkKind.Improve),
        ("driller", "Oil Driller", CivilianWorkKind.Improve),
    ];

    /// <summary>
    /// How many turns a civilian's work takes. <b>Three, from observed play</b>
    /// — this used to be the one number in this phase with nothing at all behind
    /// it, and it is the first thing here recovered from someone playing the
    /// original rather than from a document.
    /// </summary>
    /// <remarks>
    /// The observation is of an iron mine: three turns to open it at Level I,
    /// and three more for each later rung once the technology gating it arrives.
    /// <b>Applying it to the Prospector and the Engineer is extrapolation</b>
    /// from "three turns for everything" rather than something watched, and it
    /// is a one-line edit per type if either turns out to differ.
    /// <para>
    /// Moving this from 1 to 3 moved every published soak table. See
    /// <c>docs/formulas/development.md</c>.
    /// </para>
    /// </remarks>
    private const int CivilianWorkTurns = 3;

    /// <summary>
    /// The manual's Resource Development Table read the other way: which
    /// civilian raises each deposit. Fish has none, and horses are absent from
    /// the table entirely, which agrees with the horse ranch admitting no
    /// worker.
    /// </summary>
    private static readonly IReadOnlyDictionary<byte, string> ResourceImprovers =
        new Dictionary<byte, string>
        {
            [0] = "farmer",   // cotton
            [1] = "rancher",  // wool
            [2] = "forester", // timber
            [3] = "miner",    // coal
            [4] = "miner",    // iron
            [6] = "driller",  // oil
            [17] = "farmer",  // grain
            [18] = "farmer",  // fruit
            [20] = "rancher", // livestock
            [21] = "miner",   // gems
            [22] = "miner",   // gold
        };

    private static readonly IReadOnlyDictionary<byte, string> ResourceNames =
        new Dictionary<byte, string>
        {
            [0] = "cotton",
            [1] = "wool",
            [2] = "forest",
            [3] = "coal",
            [4] = "iron",
            [5] = "horses",
            [6] = "oil",
            [17] = "grain",
            [18] = "fruit",
            [19] = "fish",
            [20] = "cattle",
            [21] = "gems",
            [22] = "gold",
        };

    /// <summary>
    /// The manual's Resource Development Table, keyed by legacy deposit code.
    /// Transcribed rather than derived: the slope differs per deposit and two
    /// deposits have no improvement at all, so no single formula covers them.
    /// See <c>docs/reference/manual-mechanics.md</c>.
    /// </summary>
    private static readonly IReadOnlyDictionary<byte, long[]> ResourceYieldCurves =
        new Dictionary<byte, long[]>
        {
            [0] = WorldContentCodec.CultivatedYieldByDevelopmentLevel,      // cotton
            [1] = WorldContentCodec.CultivatedYieldByDevelopmentLevel,      // wool
            [2] = WorldContentCodec.CultivatedYieldByDevelopmentLevel,      // forest / timber
            [3] = WorldContentCodec.HeavyMineralYieldByDevelopmentLevel,    // coal
            [4] = WorldContentCodec.HeavyMineralYieldByDevelopmentLevel,    // iron
            [5] = WorldContentCodec.UnimprovableYieldByDevelopmentLevel,    // horses
            [6] = WorldContentCodec.HeavyMineralYieldByDevelopmentLevel,    // oil
            [17] = WorldContentCodec.CultivatedYieldByDevelopmentLevel,     // grain
            [18] = WorldContentCodec.CultivatedYieldByDevelopmentLevel,     // fruit
            [19] = WorldContentCodec.UnimprovableYieldByDevelopmentLevel,   // fish
            [20] = WorldContentCodec.CultivatedYieldByDevelopmentLevel,     // cattle / livestock
            [21] = WorldContentCodec.PreciousMineralYieldByDevelopmentLevel, // gems
            [22] = WorldContentCodec.PreciousMineralYieldByDevelopmentLevel, // gold
        };

    private static readonly IReadOnlyDictionary<byte, string> ResourceCommodityNames =
        new Dictionary<byte, string>
        {
            [0] = "cotton",
            [1] = "wool",
            [2] = "timber",
            [3] = "coal",
            [4] = "iron",
            [5] = "horses",
            [6] = "oil",
            [17] = "grain",
            [18] = "fruit",
            [19] = "fish",
            [20] = "livestock",
            [21] = "gems",
            [22] = "gold",
        };

    private static readonly IReadOnlyDictionary<uint, string> WarehouseCommodityNames =
        new Dictionary<uint, string>
        {
            [0] = "cotton",
            [1] = "wool",
            [2] = "timber",
            [3] = "coal",
            [4] = "iron",
            [5] = "horses",
            [6] = "oil",
            [7] = "canned-food",
            [8] = "fabric",
            [9] = "lumber",
            [10] = "paper",
            [11] = "steel",
            [12] = "fuel",
            [13] = "clothing",
            [14] = "furniture",
            [15] = "hardware",
            [16] = "armaments",
            [17] = "grain",
            [18] = "fruit",
            [19] = "fish",
            [20] = "livestock",
        };

    private static readonly IReadOnlyDictionary<uint, string> CapacityFacilityNames =
        new Dictionary<uint, string>
        {
            [0] = "textile-mill",
            [1] = "clothing-factory",
            [2] = "steel-mill",
            [3] = "metal-works",
            [4] = "lumber-mill",
            [5] = "furniture-factory",
            [6] = "oil-refinery",
        };

    private static readonly HashSet<string> ConvertedScenarioTags =
        new(
            [
                "cnam", "pnam", "zone", "year", "capa", "ware", "deve", "port", "rail", "labo",
                "civi", "tech", "tran", "cash",
            ],
            StringComparer.Ordinal);

    /// <summary>
    /// The original's food rules: half the workers want grain, a quarter fruit,
    /// and the rest livestock or fish. Expressed as a repeating cycle of four so
    /// that any headcount splits in those proportions without a rounding rule.
    /// </summary>
    private static FeedingContentSettings CreateStandardFeeding() => new()
    {
        PreferenceCycle =
        [
            new FoodPreferenceContent { Accepted = ["commodity.grain"] },
            new FoodPreferenceContent { Accepted = ["commodity.fruit"] },
            new FoodPreferenceContent { Accepted = ["commodity.grain"] },
            new FoodPreferenceContent { Accepted = ["commodity.livestock", "commodity.fish"] },
        ],
        LabourByGrade = [1, 2, 4],
        CannedFood = "commodity.canned-food",
    };

    public static LegacyImportResult Convert(
        MapDocument map,
        ScenarioDocument scenario,
        ScenarioInfoDocument? info,
        string packageKey) =>
        Convert(map, scenario, info, new LegacyImportOptions(packageKey));

    public static LegacyImportResult Convert(
        MapDocument map,
        ScenarioDocument scenario,
        ScenarioInfoDocument? info,
        LegacyImportOptions options)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(options);

        var report = new LegacyImportReport();
        if (!IsValidPackageKey(options.PackageKey))
        {
            report.Add(
                LegacyImportSeverity.Error,
                "package.invalid-key",
                "packageKey",
                "Package keys must use 1-96 lowercase ASCII letters, digits, hyphens, underscores, or dots, and begin and end with a letter or digit.");
            return new LegacyImportResult(null, report);
        }

        var countryNames = ReadNames(scenario, "cnam", "country", report);
        var provinceNames = ReadNames(scenario, "pnam", "province", report);
        var zoneNames = ReadNames(scenario, "zone", "zone", report);
        var year = ReadYear(scenario, report);

        foreach (var group in scenario.Records
                     .Where(record => !ConvertedScenarioTags.Contains(record.Tag))
                     .GroupBy(static record => record.Tag, StringComparer.Ordinal))
        {
            report.Defer($"scenario.tag.{group.Key}", group.Count());
        }

        if (scenario.TrailingBytes.Length > 0)
        {
            report.Defer("scenario.trailing-bytes", scenario.TrailingBytes.Length);
        }

        report.Defer("map.trailer-records", map.Profile.TrailerRecordCount);
        if (info is not null)
        {
            report.Defer("inf.overview-sections", 1);
            report.Defer("inf.country-briefings", info.CountrySections.Count);
            report.Defer("inf.metadata-values", info.Metadata.Count);
        }

        var mapProvinceIds = map.Cells
            .Where(static cell => !cell.IsOcean)
            .Select(static cell => (uint)cell.Province)
            .ToHashSet();
        var provinceIds = mapProvinceIds
            .Concat(provinceNames.Keys)
            .Distinct()
            .Order()
            .ToArray();
        var provinceOwners = ReadProvinceOwners(map, report);
        var capitalCells = ReadCapitalCells(map, report);
        var countryIds = countryNames.Keys
            .Concat(provinceOwners.Values.Where(static owner => owner.HasValue).Select(static owner => owner!.Value))
            .Concat(capitalCells.Keys)
            .Distinct()
            .Order()
            .ToArray();
        var countryNamespaceSize = countryIds.Length == 0 ? 0u : checked(countryIds[^1] + 1);
        var cellSeaZoneIds = new uint[map.Cells.Count];
        for (var index = 0; index < map.Cells.Count; index++)
        {
            var cell = map.Cells[index];
            if (!cell.IsOcean)
            {
                continue;
            }

            if (cell.NationZoneA < countryNamespaceSize)
            {
                report.Add(
                    LegacyImportSeverity.Error,
                    "map.invalid-sea-zone-reference",
                    $"map.cells[{index}]",
                    $"Ocean region value {cell.NationZoneA} is below the country namespace size {countryNamespaceSize}.");
                cellSeaZoneIds[index] = cell.NationZoneA;
            }
            else
            {
                cellSeaZoneIds[index] = cell.NationZoneA - countryNamespaceSize;
            }
        }

        var seaZoneIds = map.Cells
            .Select((cell, index) => (cell, index))
            .Where(static item => item.cell.IsOcean)
            .Select(item => cellSeaZoneIds[item.index])
            .Distinct()
            .Order()
            .ToArray();
        var seaZoneSet = seaZoneIds.ToHashSet();
        var unusedZoneRecords = scenario.Records.Count(record =>
            record.Tag == "zone" &&
            (record.Fields.Count == 0 || !seaZoneSet.Contains(record.Fields[0])));
        report.Defer("scenario.unused-zone-records", unusedZoneRecords);

        var provinceKeys = provinceIds.ToDictionary(static id => id, ProvinceKey);
        var seaZoneKeys = seaZoneIds.ToDictionary(static id => id, SeaZoneKey);
        var countryKeys = countryIds.ToDictionary(static id => id, CountryKey);
        var terrainCodes = map.Cells.Select(static cell => cell.Terrain).Distinct().Order().ToArray();
        var terrainKeys = terrainCodes.ToDictionary(static code => code, TerrainKey);
        var resourceCodes = map.Cells
            .SelectMany(static cell => new[] { cell.ResourceA, cell.ResourceB })
            .Where(static code => code != byte.MaxValue && ResourceNames.ContainsKey(code))
            .Distinct()
            .Order()
            .ToArray();
        var resourceKeys = resourceCodes.ToDictionary(static code => code, ResourceKey);

        WarnUnknownCodes(map, report);

        var cells = new CellContentDocument[map.Cells.Count];
        for (var index = 0; index < cells.Length; index++)
        {
            var source = map.Cells[index];
            if (source.NationZoneA != source.NationZoneB)
            {
                report.Add(
                    LegacyImportSeverity.Warning,
                    "map.nation-mirror-mismatch",
                    $"map.cells[{index}]",
                    $"Nation bytes differ ({source.NationZoneA} versus {source.NationZoneB}); the first value was used.");
            }

            var region = source.IsOcean
                ? new CellRegionContent { SeaZone = seaZoneKeys[cellSeaZoneIds[index]] }
                : new CellRegionContent { Province = provinceKeys[source.Province] };
            var resources = new List<string>(2);
            AddResource(source.ResourceA, resources, resourceKeys);
            AddResource(source.ResourceB, resources, resourceKeys);
            if (resources.Count == 2 && resources[0] == resources[1])
            {
                resources.RemoveAt(1);
                report.Add(
                    LegacyImportSeverity.Warning,
                    "map.duplicate-resource",
                    $"map.cells[{index}]",
                    "Both legacy resource slots contain the same resource; one modern deposit was emitted.");
            }

            cells[index] = new CellContentDocument
            {
                Terrain = terrainKeys[source.Terrain],
                Region = region,
                Resources = resources.ToArray(),
                HasSettlementSite = source.TownType is 34 or 35,
                River = DecodeRiver(source.River),
            };
        }

        // Countries are built after the workforce is read, because `labo` is what says
        // which of them are Great Powers. See below.
        var provinces = provinceIds.Select(id => new NamedContentDefinition
        {
            Key = provinceKeys[id],
            Name = FindName(provinceNames, id, "Province", report),
        }).ToArray();
        var seaZones = seaZoneIds.Select(id => new NamedContentDefinition
        {
            Key = seaZoneKeys[id],
            Name = FindName(zoneNames, id, "Sea Zone", report),
        }).ToArray();

        var ownerContent = provinceIds.Select(id => new ProvinceOwnerContent
        {
            Province = provinceKeys[id],
            Country = provinceOwners.GetValueOrDefault(id) is { } owner && countryKeys.TryGetValue(owner, out var key)
                ? key
                : null,
        }).ToArray();
        var capitals = capitalCells.OrderBy(static pair => pair.Key).Select(pair => new CountryCapitalContent
        {
            Country = countryKeys[pair.Key],
            Cell = pair.Value,
        }).ToArray();
        var rails = ReadReciprocalRails(map, report);
        var initialInventory = ReadInitialInventory(scenario, countryKeys, report);
        var productionCapacities = ReadProductionCapacities(scenario, countryKeys, report);
        var cellDevelopment = ReadCellDevelopment(scenario, map, report);
        var ports = ReadPorts(scenario, map, report);
        var depots = ReadDepots(scenario, map, report);
        var workers = ReadWorkforce(scenario, countryKeys, report);
        var countryTechnologies = ReadCountryTechnologies(scenario, countryKeys, report);
        var transportCapacity = ReadTransportCapacity(scenario, countryKeys, report);
        var countryCash = ReadCountryCash(scenario, countryKeys, report);
        var civilians = ReadCivilians(scenario, map, countryKeys, report);

        // `labo` names the Great Powers and only them — seven in every shipped scenario —
        // so it is how the importer tells them from the minor nations without guessing.
        // The same record already decides who gets the fair-start defaults; this puts the
        // fact on the country itself, because trade needs it to know who carries a cargo:
        // "no Minor Nation owns merchant marine."
        var greatPowers = workers
            .Select(static item => item.Country)
            .ToHashSet(StringComparer.Ordinal);
        var countries = countryIds.Select(id => new CountryContentDefinition
        {
            Key = countryKeys[id],
            Name = FindName(countryNames, id, "Country", report),
            IsGreatPower = greatPowers.Contains(countryKeys[id]),
        }).ToArray();

        var title = string.IsNullOrWhiteSpace(info?.Title)
            ? $"Legacy {options.PackageKey}"
            : info.Title;
        var document = new WorldContentDocument
        {
            Terrains = terrainCodes.Select(TerrainDefinitionFor).ToArray(),
            CivilianTypes = CivilianTypes.Select(static type => new CivilianTypeContentDefinition
            {
                Key = $"civilian.{type.Name}",
                Name = type.DisplayName,
                WorkTurns = CivilianWorkTurns,
                Work = type.Work,
            }).ToArray(),

            // The whole table in order, because a tech record is a bare 1-based
            // index into it — now carrying what each entry costs, when it becomes
            // available and what must be known first.
            Technologies = TechnologyTable.Select(static entry => new TechnologyContentDefinition
            {
                Key = TechnologyKey(entry.Name),
                Name = entry.Name,
                Cost = entry.Cost,
                AvailableFrom = entry.AvailableFrom,
                Prerequisites = [.. entry.Prerequisites.Select(TechnologyKey)],
            }).ToArray(),
            Commodities = CreateStandardCommodities(),
            ShipTypes = CreateStandardShipTypes(),
            Trade = CreateStandardTradeMarket(),
            ProductionFacilities = CreateStandardProductionFacilities(),
            ProductionRecipes = CreateStandardProductionRecipes(),
            ExpansionCostPerCapacityPoint = CreateStandardExpansionCost(),
            Migration = CreateStandardMigration(),
            Resources = resourceCodes.Select(code => new ResourceContentDefinition
            {
                Key = resourceKeys[code],
                Commodity = $"commodity.{ResourceCommodityNames[code]}",

                // The 1997 map records which deposit sits on a cell and never
                // its output, so the curve comes from the manual's Resource
                // Development Table rather than from the file. No deposit
                // declares a RequiredTechnology: the manual gates improvement
                // *levels* behind technology and never extraction from a
                // deposit that is already open, which is a different hook —
                // TechnologyByDevelopmentLevel below.
                YieldByDevelopmentLevel = [.. ResourceYieldCurves[code]],

                // Which civilian raises this deposit, from the manual's
                // Resource Development Table. Null for fish, which the table
                // gives no worker, and for horses, which it omits.
                ImprovedBy = ResourceImprovers.TryGetValue(code, out var improver)
                    ? $"civilian.{improver}"
                    : null,

                // Coal, iron, gold, gems and oil are on the map and invisible
                // to their owner until a Prospector has searched the tile.
                RequiresDiscovery = HiddenResources.Contains(code),

                // What each rung of this deposit's curve costs in knowledge.
                // Index 0 is the level a tile starts at and is always ungated.
                TechnologyByDevelopmentLevel = ResourceTechnologyLadders
                    .TryGetValue(code, out var ladder)
                    ? [null, .. ladder.Select(static step =>
                        step is null ? null : TechnologyKey(step))]
                    : null,
            }).ToArray(),
            Feeding = CreateStandardFeeding(),

            // The two the manual gives every power outright. Nothing else here
            // is defaulted: a shipped scenario authors its own industry and
            // workforce, so this block exists purely to carry the knowledge no
            // record ever states.
            StartingDefaults = new StartingDefaultsContent
            {
                Technologies = [.. StartingTechnologies.Select(TechnologyKey)],
                TransportCapacity = DefaultTransportCapacity,
                Inventory = CreateStandardStartingStock(),
                Cash = DefaultStartingCash,

                // Three Traders a power, which all three skirmishes agree on. Unlike the
                // transport pool above this is not invented — see StartingFleet.
                Ships =
                [
                    new ShipDefaultContent
                    {
                        Type = ShipTypeKey(StartingFleet.Type),
                        Count = StartingFleet.Count,
                    },
                ],
            },
            Transport = CreateStandardTransport(),
            Construction = CreateStandardConstruction(),
            Improvement = CreateStandardImprovement(),
            Extraction = new ExtractionContentSettings
            {
                CatchmentRadius = WorldContentCodec.DefaultCatchmentRadius,

                // Coast and river alike give a port one unit of fish per turn.
                // Fish is the one resource no civilian unit improves, so it has
                // no place in the development table and arrives this way instead.
                PortFishing = new PortFishingContent
                {
                    Commodity = "commodity.fish",
                    YieldPerAdjacentWaterTile = WorldContentCodec.DefaultPortFishYieldPerWaterTile,
                },
            },
            Map = new MapContentDocument
            {
                Key = $"map.legacy.{options.PackageKey}",
                Name = title,
                Width = map.Width,
                Height = map.Height,
                Provinces = provinces,
                SeaZones = seaZones,
                Cells = cells,
            },
            Countries = countries,
            Scenarios =
            [
                new ScenarioContentDocument
                {
                    Key = $"scenario.legacy.{options.PackageKey}",
                    Name = title,
                    StartingYear = year ?? 0,
                    ProvinceOwners = ownerContent,
                    Rails = rails,
                    Capitals = capitals,
                    InitialInventory = initialInventory,
                    ProductionCapacities = productionCapacities,
                    CellDevelopment = cellDevelopment,
                    Ports = ports,
                    Depots = depots,
                    Workers = workers,
                    Civilians = civilians,
                    CountryTechnologies = countryTechnologies,
                    TransportCapacity = transportCapacity,
                    Cash = countryCash,

                    // Every power the scenario gives a workforce to. `labo` is
                    // the one record that names the Great Powers and only them
                    // — seven in every shipped scenario — so it is how the
                    // importer tells them from the minor nations without
                    // guessing. They are the powers the manual's starting two
                    // technologies belong to.
                    DefaultStartCountries = [.. workers
                        .Select(static item => item.Country)
                        .Distinct(StringComparer.Ordinal)],
                },
            ],
        };

        if (!report.HasErrors)
        {
            try
            {
                _ = WorldContentCompiler.Compile(document);
            }
            catch (ContentValidationException exception)
            {
                report.Add(
                    LegacyImportSeverity.Error,
                    "content.validation-failed",
                    exception.Path,
                    exception.Message);
            }
        }

        return new LegacyImportResult(report.HasErrors ? null : document, report);
    }

    private static Dictionary<uint, string> ReadNames(
        ScenarioDocument scenario,
        string tag,
        string description,
        LegacyImportReport report)
    {
        var result = new Dictionary<uint, string>();
        foreach (var (record, index) in scenario.Records.Select(static (record, index) => (record, index)))
        {
            if (record.Tag != tag)
            {
                continue;
            }

            if (record.Fields.Count != 1 || string.IsNullOrWhiteSpace(record.Name))
            {
                report.Add(
                    LegacyImportSeverity.Error,
                    $"scenario.invalid-{tag}",
                    $"scenario.records[{index}]",
                    $"The {description} name record must have one ID and a nonblank name.");
                continue;
            }

            var id = record.Fields[0];
            if (result.TryGetValue(id, out var existing) && existing != record.Name)
            {
                report.Add(
                    LegacyImportSeverity.Error,
                    $"scenario.conflicting-{tag}",
                    $"scenario.{tag}.{id.ToString(CultureInfo.InvariantCulture)}",
                    $"Legacy {description} ID {id} has conflicting names.");
            }
            else
            {
                result[id] = record.Name;
            }
        }

        return result;
    }

    /// <summary>
    /// The year a <c>year</c> record of zero would mean. **A <c>year</c> field is
    /// an offset from 1815, not an absolute year**, and this importer used to pass
    /// it through verbatim.
    /// </summary>
    /// <remarks>
    /// The corpus's fields are 1, 5, 10, 11, 33 and 67, which are plainly not
    /// years, and nothing read the value until technology gained an arrival date —
    /// the same story as <c>tran</c> and <c>cash</c>, inert and then load-bearing.
    /// <para>
    /// <b>The epoch comes from the scenarios' own briefing text.</b> <c>s1.inf</c>
    /// is titled "Naval Competition 1882" and says "the year 1882 finds Germany
    /// with industrial and educational superiority"; its field is 67.
    /// <c>s3.inf</c> is "Unification Movements 1848-1890" and says "in 1848 France
    /// is still the leading power"; its field is 33. Both are 1815 + field exactly,
    /// which is also the manual's own campaign start.
    /// </para>
    /// <para>
    /// <b>A third check corroborates it and was not used to derive it.</b> Reading
    /// the corpus's <c>tech</c> grants against the price list's arrival years, the
    /// three latest scenarios grant nothing that has not yet arrived — <c>s1</c> in
    /// 1882 holds 21 of 27 available, <c>s3</c> in 1848 holds 14 of 16, and
    /// <c>s9</c> in 1826 holds 9 of exactly 9. That last one sits on the boundary:
    /// Spinning Jenny and Paddlewheels both arrive in 1826 and <c>s9</c> holds both
    /// and nothing later. An epoch off by even a few years would break it.
    /// </para>
    /// <para>
    /// The three skirmishes carry field 1, so a skirmish starts in **1816** rather
    /// than 1815. That is what the data says; it is not rounded to the manual's
    /// campaign year.
    /// </para>
    /// </remarks>
    private const int ScenarioYearEpoch = 1815;

    private static int? ReadYear(ScenarioDocument scenario, LegacyImportReport report)
    {
        var values = new List<uint>();
        foreach (var (record, index) in scenario.Records.Select(static (record, index) => (record, index)))
        {
            if (record.Tag != "year")
            {
                continue;
            }

            if (record.Fields.Count != 1)
            {
                report.Add(
                    LegacyImportSeverity.Error,
                    "scenario.invalid-year",
                    $"scenario.records[{index}]",
                    "A year record must contain exactly one value.");
                continue;
            }

            values.Add(record.Fields[0]);
        }

        if (values.Count == 0)
        {
            report.Add(LegacyImportSeverity.Error, "scenario.missing-year", "scenario.year", "A starting year is required.");
            return null;
        }

        if (values.Distinct().Count() != 1)
        {
            report.Add(LegacyImportSeverity.Error, "scenario.conflicting-year", "scenario.year", "Duplicate year records disagree.");
            return null;
        }

        if (values[0] > int.MaxValue - ScenarioYearEpoch)
        {
            report.Add(LegacyImportSeverity.Error, "scenario.year-out-of-range", "scenario.year", "The starting year exceeds the modern integer range.");
            return null;
        }

        return ScenarioYearEpoch + (int)values[0];
    }

    /// <summary>
    /// Converts <c>deve</c> records into starting development levels. The record
    /// is <c>[cell, level 1-3]</c>, verified across the corpus; a cell reference
    /// is a linear row-major index, not a coordinate pair. Levels outside 1-3
    /// are reported rather than clamped, since a value the original never writes
    /// means the reading is wrong, not that the file is unusual.
    /// </summary>
    /// <remarks>
    /// A cell may carry more than one record: <c>s1</c> does it three times, as
    /// <c>[2,1]</c>, <c>[1,1]</c> and <c>[2,1]</c>. That is shipped data, so it
    /// is legal by definition and treating it as corruption would be the wrong
    /// rule. The highest level wins, on the grounds that development is a level
    /// a cell has rather than a stack of separate works, so the largest record
    /// is the only one consistent with all of them. Last-record-wins is the
    /// alternative reading and just two cells in one file tell them apart, so
    /// the choice is recorded here rather than presented as settled.
    /// </remarks>
    private static CellDevelopmentContent[] ReadCellDevelopment(
        ScenarioDocument scenario,
        MapDocument map,
        LegacyImportReport report)
    {
        const int maximumLegacyLevel = 3;
        var byCell = new Dictionary<uint, int>();
        var order = new List<uint>();
        foreach (var (record, index) in scenario.Records.Select(static (record, index) => (record, index)))
        {
            if (record.Tag != "deve")
            {
                continue;
            }

            var path = $"scenario.records[{index}]";
            if (record.Fields.Count != 2)
            {
                report.Add(LegacyImportSeverity.Error, "scenario.invalid-deve", path, "A deve record must contain a cell and a level.");
                continue;
            }

            var cell = record.Fields[0];
            var level = record.Fields[1];
            if (cell >= (uint)map.Cells.Count)
            {
                report.Add(LegacyImportSeverity.Error, "scenario.invalid-deve-cell", path, $"Development refers to cell {cell} outside the map.");
                continue;
            }

            if (map.Cells[(int)cell].IsOcean)
            {
                report.Add(LegacyImportSeverity.Error, "scenario.deve-on-ocean", path, $"Development refers to ocean cell {cell}.");
                continue;
            }

            if (level == 0 || level > maximumLegacyLevel)
            {
                report.Add(LegacyImportSeverity.Warning, "scenario.unexpected-deve-level", path, $"Development level {level} is outside the corpus range 1-{maximumLegacyLevel}; no level was emitted.");
                continue;
            }

            if (byCell.TryGetValue(cell, out var existing))
            {
                var kept = Math.Max(existing, (int)level);
                report.Add(
                    LegacyImportSeverity.Warning,
                    "scenario.repeated-deve",
                    path,
                    $"Cell {cell} is developed more than once ({existing} and {level}); kept {kept}.");
                byCell[cell] = kept;
                continue;
            }

            byCell.Add(cell, (int)level);
            order.Add(cell);
        }

        return order
            .Select(cell => new CellDevelopmentContent { Cell = (int)cell, Level = byCell[cell] })
            .ToArray();
    }

    /// <summary>
    /// Converts <c>tran</c> records into starting transport capacity. The record
    /// is <c>[country, capacity]</c> — one number for the whole network, matching
    /// the manual's single shared capacity bar.
    /// </summary>
    /// <remarks>
    /// A scenario that carries none leaves every power on the engine's default,
    /// which is a guess; see <see cref="DefaultTransportCapacity"/>. The values
    /// a mission does author are authored design and must not be read as
    /// gameplay constants.
    /// </remarks>
    private static TransportCapacityContent[] ReadTransportCapacity(
        ScenarioDocument scenario,
        IReadOnlyDictionary<uint, string> countryKeys,
        LegacyImportReport report)
    {
        var result = new List<TransportCapacityContent>();
        var seen = new HashSet<uint>();
        foreach (var (record, index) in scenario.Records.Select(static (record, index) => (record, index)))
        {
            if (record.Tag != "tran")
            {
                continue;
            }

            var path = $"scenario.records[{index}]";
            if (record.Fields.Count != 2)
            {
                report.Add(
                    LegacyImportSeverity.Error,
                    "scenario.invalid-tran",
                    path,
                    "A tran record must contain a country and a capacity.");
                continue;
            }

            var country = record.Fields[0];
            if (!countryKeys.TryGetValue(country, out var countryKey))
            {
                report.Add(
                    LegacyImportSeverity.Error,
                    "scenario.invalid-tran-country",
                    path,
                    $"Transport capacity refers to unknown country {country}.");
                continue;
            }

            if (!seen.Add(country))
            {
                report.Add(
                    LegacyImportSeverity.Warning,
                    "scenario.repeated-tran",
                    path,
                    $"Country {country} has more than one transport capacity record.");
                continue;
            }

            result.Add(new TransportCapacityContent
            {
                Country = countryKey,
                Capacity = record.Fields[1],
            });
        }

        return result.ToArray();
    }

    /// <summary>
    /// Converts <c>cash</c> records into starting treasuries. The record is
    /// <c>[country, amount]</c> — the same two-field shape as <c>tran</c>.
    /// </summary>
    /// <remarks>
    /// A scenario that carries none leaves every power on the engine's default,
    /// which is a guess; see <see cref="DefaultStartingCash"/>. What a mission
    /// authors is authored design and must not be read as a gameplay constant:
    /// <c>s1</c>, <c>s13</c> and <c>s14</c> give their seven powers 1,500 to
    /// 10,000 apiece and <c>s3</c> spans 1,500 to 15,000.
    /// </remarks>
    private static CountryCashContent[] ReadCountryCash(
        ScenarioDocument scenario,
        IReadOnlyDictionary<uint, string> countryKeys,
        LegacyImportReport report)
    {
        var result = new List<CountryCashContent>();
        var seen = new HashSet<uint>();
        foreach (var (record, index) in scenario.Records.Select(static (record, index) => (record, index)))
        {
            if (record.Tag != "cash")
            {
                continue;
            }

            var path = $"scenario.records[{index}]";
            if (record.Fields.Count != 2)
            {
                report.Add(
                    LegacyImportSeverity.Error,
                    "scenario.invalid-cash",
                    path,
                    "A cash record must contain a country and an amount.");
                continue;
            }

            var country = record.Fields[0];
            if (!countryKeys.TryGetValue(country, out var countryKey))
            {
                report.Add(
                    LegacyImportSeverity.Error,
                    "scenario.invalid-cash-country",
                    path,
                    $"Cash refers to unknown country {country}.");
                continue;
            }

            if (!seen.Add(country))
            {
                report.Add(
                    LegacyImportSeverity.Warning,
                    "scenario.repeated-cash",
                    path,
                    $"Country {country} has more than one cash record.");
                continue;
            }

            result.Add(new CountryCashContent
            {
                Country = countryKey,
                Amount = record.Fields[1],
            });
        }

        return result.ToArray();
    }

    /// <summary>
    /// Converts <c>tech</c> records into starting knowledge. The record is
    /// <c>[country, id]</c>, where the id is a **1-based index into the manual's
    /// Benefits of Technology Table** — see <see cref="TechnologyTable"/> for the
    /// corpus evidence behind that reading.
    /// </summary>
    /// <remarks>
    /// A scenario grants technology on top of the two every power starts with,
    /// so a skirmish carrying no <c>tech</c> record at all is not a power that
    /// knows nothing. `s10`, `s11` and `s15` do exactly that.
    /// </remarks>
    private static CountryTechnologyContent[] ReadCountryTechnologies(
        ScenarioDocument scenario,
        IReadOnlyDictionary<uint, string> countryKeys,
        LegacyImportReport report)
    {
        var result = new List<CountryTechnologyContent>();
        var seen = new HashSet<(uint Country, uint Technology)>();
        foreach (var (record, index) in scenario.Records.Select(static (record, index) => (record, index)))
        {
            if (record.Tag != "tech")
            {
                continue;
            }

            var path = $"scenario.records[{index}]";
            if (record.Fields.Count != 2)
            {
                report.Add(
                    LegacyImportSeverity.Error,
                    "scenario.invalid-tech",
                    path,
                    "A tech record must contain a country and a technology.");
                continue;
            }

            var country = record.Fields[0];
            var technology = record.Fields[1];
            if (!countryKeys.TryGetValue(country, out var countryKey))
            {
                report.Add(
                    LegacyImportSeverity.Error,
                    "scenario.invalid-tech-country",
                    path,
                    $"Technology refers to unknown country {country}.");
                continue;
            }

            if (technology == 0 || technology > (uint)TechnologyTable.Count)
            {
                report.Add(
                    LegacyImportSeverity.Warning,
                    "scenario.unknown-tech-id",
                    path,
                    $"Technology {technology} is outside the table of " +
                    $"{TechnologyTable.Count}; no knowledge was granted.");
                continue;
            }

            if (!seen.Add((country, technology)))
            {
                report.Add(
                    LegacyImportSeverity.Warning,
                    "scenario.repeated-tech",
                    path,
                    $"Country {country} is granted technology {technology} more than once.");
                continue;
            }

            result.Add(new CountryTechnologyContent
            {
                Country = countryKey,
                Technology = TechnologyKeyAt((int)technology),
            });
        }

        return result.ToArray();
    }

    /// <summary>
    /// Converts <c>port</c> records into port sites. The record is a single
    /// linear cell index. Every one of the corpus's 124 ports names a land cell,
    /// and the 45 with no adjacent sea all carry a river, so the manual's "ports
    /// always require access to water" holds without exception and is enforced
    /// rather than merely reported.
    /// </summary>
    private static int[] ReadPorts(
        ScenarioDocument scenario,
        MapDocument map,
        LegacyImportReport report)
    {
        var result = new List<int>();
        var seen = new HashSet<uint>();
        foreach (var (record, index) in scenario.Records.Select(static (record, index) => (record, index)))
        {
            if (record.Tag != "port")
            {
                continue;
            }

            var path = $"scenario.records[{index}]";
            if (record.Fields.Count != 1)
            {
                report.Add(LegacyImportSeverity.Error, "scenario.invalid-port", path, "A port record must contain a single cell.");
                continue;
            }

            var cell = record.Fields[0];
            if (cell >= (uint)map.Cells.Count)
            {
                report.Add(LegacyImportSeverity.Error, "scenario.invalid-port-cell", path, $"Port refers to cell {cell} outside the map.");
                continue;
            }

            if (map.Cells[(int)cell].IsOcean)
            {
                report.Add(LegacyImportSeverity.Error, "scenario.port-on-ocean", path, $"Port refers to ocean cell {cell}.");
                continue;
            }

            // Repeats are collapsed rather than rejected: deve records taught
            // that the corpus repeats things, and a second port on one cell is
            // the same port either way.
            if (!seen.Add(cell))
            {
                report.Add(LegacyImportSeverity.Warning, "scenario.repeated-port", path, $"Cell {cell} carries more than one port record.");
                continue;
            }

            if (!TouchesWater(map, (int)cell))
            {
                report.Add(LegacyImportSeverity.Warning, "scenario.landlocked-port", path, $"Port cell {cell} touches neither sea nor a river.");
            }

            result.Add((int)cell);
        }

        return result.ToArray();
    }

    /// <summary>
    /// Converts <c>civi</c> records into starting civilians. The record is
    /// <c>[type, cell]</c> and names no owner.
    /// </summary>
    /// <remarks>
    /// The owner comes from the province the cell sits in, which the corpus
    /// supports without exception: all 210 records across the ten scenarios
    /// stand on owned land, and every one of those owners is a country holding
    /// a capital. Unowned land is therefore treated as an error rather than
    /// tolerated — nothing shipped does it, and a civilian nobody owns could
    /// never be given an order.
    /// <para>
    /// Stacking is allowed: <c>s1</c> gives one power two Miners, and nothing
    /// says a tile holds only one worker.
    /// </para>
    /// </remarks>
    private static CivilianContent[] ReadCivilians(
        ScenarioDocument scenario,
        MapDocument map,
        IReadOnlyDictionary<uint, string> countryKeys,
        LegacyImportReport report)
    {
        var result = new List<CivilianContent>();
        foreach (var (record, index) in scenario.Records.Select(static (record, index) => (record, index)))
        {
            if (record.Tag != "civi")
            {
                continue;
            }

            var path = $"scenario.records[{index}]";
            if (record.Fields.Count != 2)
            {
                report.Add(
                    LegacyImportSeverity.Error,
                    "scenario.invalid-civi",
                    path,
                    "A civi record must contain a type and a cell.");
                continue;
            }

            var type = record.Fields[0];
            if (type >= (uint)CivilianTypes.Count)
            {
                report.Add(
                    LegacyImportSeverity.Error,
                    "scenario.invalid-civi-type",
                    path,
                    $"Civilian refers to unknown type {type}.");
                continue;
            }

            var cell = record.Fields[1];
            if (cell >= (uint)map.Cells.Count)
            {
                report.Add(
                    LegacyImportSeverity.Error,
                    "scenario.invalid-civi-cell",
                    path,
                    $"Civilian refers to cell {cell} outside the map.");
                continue;
            }

            var source = map.Cells[(int)cell];
            if (source.IsOcean)
            {
                report.Add(
                    LegacyImportSeverity.Error,
                    "scenario.civi-on-ocean",
                    path,
                    $"Civilian refers to ocean cell {cell}.");
                continue;
            }

            if (source.NationZoneA == byte.MaxValue ||
                !countryKeys.TryGetValue(source.NationZoneA, out var countryKey))
            {
                report.Add(
                    LegacyImportSeverity.Error,
                    "scenario.civi-on-unowned-land",
                    path,
                    $"Civilian stands on cell {cell}, which no known country owns.");
                continue;
            }

            result.Add(new CivilianContent
            {
                Country = countryKey,
                Type = $"civilian.{CivilianTypes[(int)type].Name}",
                Cell = (int)cell,
            });
        }

        return result.ToArray();
    }

    /// <summary>
    /// Converts <c>labo</c> records into starting workforces. The record is
    /// <c>[country, untrained, trained, expert]</c>.
    /// </summary>
    /// <remarks>
    /// The grade order is settled by the data rather than assumed: <c>s1</c>
    /// gives country 2 <c>[60, 5, 0]</c>, which reads as a backward power with
    /// sixty untrained labourers and no experts. Reversed it would be a power
    /// with sixty experts and nobody to train, which no scenario would author.
    /// Every shipped scenario carries all seven records.
    /// </remarks>
    private static WorkforceContent[] ReadWorkforce(
        ScenarioDocument scenario,
        IReadOnlyDictionary<uint, string> countryKeys,
        LegacyImportReport report)
    {
        var result = new List<WorkforceContent>();
        var seen = new HashSet<uint>();
        foreach (var (record, index) in scenario.Records.Select(static (record, index) => (record, index)))
        {
            if (record.Tag != "labo")
            {
                continue;
            }

            var path = $"scenario.records[{index}]";
            if (record.Fields.Count != 4)
            {
                report.Add(LegacyImportSeverity.Error, "scenario.invalid-labo", path, "A labo record must contain a country and three worker counts.");
                continue;
            }

            var country = record.Fields[0];
            if (!countryKeys.TryGetValue(country, out var countryKey))
            {
                report.Add(LegacyImportSeverity.Error, "scenario.invalid-labo-country", path, $"Workforce refers to unknown country {country}.");
                continue;
            }

            if (!seen.Add(country))
            {
                report.Add(LegacyImportSeverity.Warning, "scenario.repeated-labo", path, $"Country {country} has more than one workforce record.");
                continue;
            }

            if (record.Fields.Skip(1).All(static value => value == 0))
            {
                continue;
            }

            result.Add(new WorkforceContent
            {
                Country = countryKey,
                Untrained = record.Fields[1],
                Trained = record.Fields[2],
                Expert = record.Fields[3],
            });
        }

        return result.ToArray();
    }

    /// <summary>
    /// Converts <c>rail</c> records into rail depots.
    /// </summary>
    /// <remarks>
    /// The tag is misleading: the map's own rail byte already carries the track,
    /// and these records are the depots built on it. The corpus says so twice
    /// over. They are a strict subset of railed cells — 76 of 310 in <c>s1</c>,
    /// 28 of 125 in <c>s3</c>, 25 of 81 in <c>s9</c> — and **no depot in any
    /// shipped scenario sits within two tiles of another**, which is exactly the
    /// spacing the manual recommends so that each tile is gathered once.
    /// </remarks>
    private static int[] ReadDepots(
        ScenarioDocument scenario,
        MapDocument map,
        LegacyImportReport report)
    {
        var result = new List<int>();
        var seen = new HashSet<uint>();
        foreach (var (record, index) in scenario.Records.Select(static (record, index) => (record, index)))
        {
            if (record.Tag != "rail")
            {
                continue;
            }

            var path = $"scenario.records[{index}]";
            if (record.Fields.Count != 1)
            {
                report.Add(LegacyImportSeverity.Error, "scenario.invalid-depot", path, "A rail depot record must contain a single cell.");
                continue;
            }

            var cell = record.Fields[0];
            if (cell >= (uint)map.Cells.Count)
            {
                report.Add(LegacyImportSeverity.Error, "scenario.invalid-depot-cell", path, $"Depot refers to cell {cell} outside the map.");
                continue;
            }

            if (map.Cells[(int)cell].IsOcean)
            {
                report.Add(LegacyImportSeverity.Error, "scenario.depot-on-ocean", path, $"Depot refers to ocean cell {cell}.");
                continue;
            }

            if (!seen.Add(cell))
            {
                report.Add(LegacyImportSeverity.Warning, "scenario.repeated-depot", path, $"Cell {cell} carries more than one depot record.");
                continue;
            }

            // Every corpus depot stands on track. Warn rather than reject: the
            // last two rules stated this confidently were both wrong.
            if (map.Cells[(int)cell].Rail == 0)
            {
                report.Add(LegacyImportSeverity.Warning, "scenario.depot-without-rail", path, $"Depot cell {cell} carries no rail.");
            }

            result.Add((int)cell);
        }

        return result.ToArray();
    }

    /// <summary>
    /// Whether a legacy cell has sea beside it or a river running through it.
    /// </summary>
    /// <remarks>
    /// Adjacency wraps east-west, because the 1997 grid does. That is not a
    /// detail: <c>s3</c> puts a port on the last column whose only water lies
    /// across the seam, and without the wrap it reads as landlocked. With the
    /// wrap, every one of the corpus's 124 ports touches water.
    /// </remarks>
    private static bool TouchesWater(MapDocument map, int cell)
    {
        if (map.Cells[cell].River != 0)
        {
            return true;
        }

        var width = map.Width;
        var height = map.Height;
        var x = cell % width;
        var y = cell / width;
        var odd = (y & 1) != 0;
        ReadOnlySpan<(int DeltaX, int DeltaY)> steps =
        [
            (odd ? 1 : 0, -1),
            (1, 0),
            (odd ? 1 : 0, 1),
            (odd ? 0 : -1, 1),
            (-1, 0),
            (odd ? 0 : -1, -1),
        ];

        foreach (var (deltaX, deltaY) in steps)
        {
            var neighborY = y + deltaY;
            if (neighborY < 0 || neighborY >= height)
            {
                continue;
            }

            var neighborX = ((x + deltaX) % width + width) % width;
            if (map.Cells[(neighborY * width) + neighborX].IsOcean)
            {
                return true;
            }
        }

        return false;
    }

    private static InitialInventoryContent[] ReadInitialInventory(
        ScenarioDocument scenario,
        IReadOnlyDictionary<uint, string> countryKeys,
        LegacyImportReport report)
    {
        var result = new List<InitialInventoryContent>();
        var seen = new HashSet<(uint Country, uint Commodity)>();
        foreach (var (record, index) in scenario.Records.Select(static (record, index) => (record, index)))
        {
            if (record.Tag != "ware")
            {
                continue;
            }

            var path = $"scenario.records[{index}]";
            if (record.Fields.Count != 3)
            {
                report.Add(LegacyImportSeverity.Error, "scenario.invalid-ware", path, "A ware record must contain country, commodity, and quantity values.");
                continue;
            }

            var country = record.Fields[0];
            var commodity = record.Fields[1];
            if (!countryKeys.TryGetValue(country, out var countryKey))
            {
                report.Add(LegacyImportSeverity.Error, "scenario.invalid-ware-country", path, $"Warehouse stock refers to unknown country {country}.");
                continue;
            }

            if (!WarehouseCommodityNames.TryGetValue(commodity, out var commodityName))
            {
                report.Add(LegacyImportSeverity.Warning, "scenario.unknown-ware-commodity", path, $"Warehouse stock uses unknown commodity code {commodity}; no stock was emitted.");
                continue;
            }

            if (!seen.Add((country, commodity)))
            {
                report.Add(LegacyImportSeverity.Error, "scenario.duplicate-ware", path, "Warehouse stock repeats a country and commodity pair.");
                continue;
            }

            var quantity = record.Fields[2];
            if (quantity == 0)
            {
                continue;
            }

            result.Add(new InitialInventoryContent
            {
                Country = countryKey,
                Commodity = $"commodity.{commodityName}",
                Quantity = quantity,
            });
        }

        return result.ToArray();
    }

    private static InitialProductionCapacityContent[] ReadProductionCapacities(
        ScenarioDocument scenario,
        IReadOnlyDictionary<uint, string> countryKeys,
        LegacyImportReport report)
    {
        var result = new List<InitialProductionCapacityContent>();
        var seen = new HashSet<(uint Country, uint Facility)>();
        foreach (var (record, index) in scenario.Records.Select(static (record, index) => (record, index)))
        {
            if (record.Tag != "capa")
            {
                continue;
            }

            var path = $"scenario.records[{index}]";
            if (record.Fields.Count != 3)
            {
                report.Add(LegacyImportSeverity.Error, "scenario.invalid-capa", path, "A capa record must contain country, industry, and capacity values.");
                continue;
            }

            var country = record.Fields[0];
            var facility = record.Fields[1];
            if (!countryKeys.TryGetValue(country, out var countryKey))
            {
                report.Add(LegacyImportSeverity.Error, "scenario.invalid-capa-country", path, $"Production capacity refers to unknown country {country}.");
                continue;
            }

            if (!CapacityFacilityNames.TryGetValue(facility, out var facilityName))
            {
                report.Add(LegacyImportSeverity.Warning, "scenario.unknown-capa-industry", path, $"Production capacity uses unknown industry code {facility}; no capacity was emitted.");
                continue;
            }

            if (!seen.Add((country, facility)))
            {
                report.Add(LegacyImportSeverity.Error, "scenario.duplicate-capa", path, "Production capacity repeats a country and facility pair.");
                continue;
            }

            var quantity = record.Fields[2];
            if (quantity == 0)
            {
                continue;
            }

            result.Add(new InitialProductionCapacityContent
            {
                Country = countryKey,
                Facility = $"facility.{facilityName}",
                Quantity = quantity,
            });
        }

        return result.ToArray();
    }

    private static Dictionary<uint, uint?> ReadProvinceOwners(MapDocument map, LegacyImportReport report)
    {
        var owners = new Dictionary<uint, uint?>();
        for (var index = 0; index < map.Cells.Count; index++)
        {
            var cell = map.Cells[index];
            if (cell.IsOcean)
            {
                continue;
            }

            var province = (uint)cell.Province;
            uint? owner = cell.NationZoneA == byte.MaxValue ? null : cell.NationZoneA;
            if (owners.TryGetValue(province, out var existing) && existing != owner)
            {
                report.Add(
                    LegacyImportSeverity.Error,
                    "map.conflicting-province-owner",
                    $"map.province.{province.ToString(CultureInfo.InvariantCulture)}",
                    $"Province {province} contains cells with conflicting owners.");
            }
            else
            {
                owners[province] = owner;
            }
        }

        return owners;
    }

    private static Dictionary<uint, int> ReadCapitalCells(MapDocument map, LegacyImportReport report)
    {
        var capitals = new Dictionary<uint, int>();
        for (var index = 0; index < map.Cells.Count; index++)
        {
            var cell = map.Cells[index];
            if (cell.TownType != 35)
            {
                continue;
            }

            if (cell.IsOcean || cell.NationZoneA == byte.MaxValue)
            {
                report.Add(
                    LegacyImportSeverity.Error,
                    "map.invalid-capital",
                    $"map.cells[{index}]",
                    "A capital must be a land cell with a country owner.");
                continue;
            }

            var country = (uint)cell.NationZoneA;
            if (!capitals.TryAdd(country, index))
            {
                report.Add(
                    LegacyImportSeverity.Error,
                    "map.duplicate-capital",
                    $"map.country.{country.ToString(CultureInfo.InvariantCulture)}",
                    $"Country {country} has more than one capital cell.");
            }
        }

        return capitals;
    }

    private static CellLinkContent[] ReadReciprocalRails(MapDocument map, LegacyImportReport report)
    {
        var links = new List<CellLinkContent>();
        for (var index = 0; index < map.Cells.Count; index++)
        {
            var source = map.Cells[index];
            if ((source.Rail & 0xc0) != 0)
            {
                report.Add(
                    LegacyImportSeverity.Warning,
                    "map.unknown-rail-bits",
                    $"map.cells[{index}].rail",
                    $"Rail value {source.Rail} has bits outside the six known directions; those bits were ignored.");
            }

            foreach (var direction in RailDirections)
            {
                if ((source.Rail & direction.Bit) == 0)
                {
                    continue;
                }

                var neighbour = GetNeighbour(index, map.Width, map.Height, direction.Bit);
                if (neighbour is null || (map.Cells[neighbour.Value].Rail & direction.OppositeBit) == 0)
                {
                    report.Add(
                        LegacyImportSeverity.Warning,
                        "map.asymmetric-rail-endpoint",
                        $"map.cells[{index}].rail.{direction.Name}",
                        "The legacy rail endpoint has no reciprocal neighbour and was dropped.");
                    continue;
                }

                if (index >= neighbour.Value)
                {
                    continue;
                }

                if (source.IsOcean || map.Cells[neighbour.Value].IsOcean)
                {
                    report.Add(
                        LegacyImportSeverity.Error,
                        "map.invalid-rail-reference",
                        $"map.cells[{index}].rail.{direction.Name}",
                        "A reciprocal rail link refers to an ocean cell.");
                    continue;
                }

                links.Add(new CellLinkContent { First = index, Second = neighbour.Value });
            }
        }

        return links.ToArray();
    }

    private static int? GetNeighbour(int index, int width, int height, byte bit)
    {
        var x = index % width;
        var y = index / width;
        var odd = (y & 1) != 0;
        var (dx, dy) = bit switch
        {
            1 => (odd ? 1 : 0, -1),
            2 => (1, 0),
            4 => (odd ? 1 : 0, 1),
            8 => (odd ? 0 : -1, 1),
            16 => (-1, 0),
            32 => (odd ? 0 : -1, -1),
            _ => throw new ArgumentOutOfRangeException(nameof(bit)),
        };
        var targetX = x + dx;
        var targetY = y + dy;
        return (uint)targetX < (uint)width && (uint)targetY < (uint)height
            ? checked((targetY * width) + targetX)
            : null;
    }

    private static void WarnUnknownCodes(MapDocument map, LegacyImportReport report)
    {
        foreach (var group in map.Cells.GroupBy(static cell => cell.Terrain).Where(group => !Terrains.ContainsKey(group.Key)))
        {
            report.Add(
                LegacyImportSeverity.Warning,
                "map.unknown-terrain-code",
                $"map.terrain-code.{group.Key}",
                $"Terrain code {group.Key} is unknown; a numeric placeholder terrain key was emitted.",
                group.Count());
        }

        foreach (var group in map.Cells
                     .SelectMany(static cell => new[] { cell.ResourceA, cell.ResourceB })
                     .Where(static code => code != byte.MaxValue)
                     .GroupBy(static code => code)
                     .Where(group => !ResourceNames.ContainsKey(group.Key)))
        {
            report.Add(
                LegacyImportSeverity.Warning,
                "map.unknown-resource-code",
                $"map.resource-code.{group.Key}",
                $"Resource code {group.Key} is unknown; no resource feature was inferred.",
                group.Count());
        }

        foreach (var group in map.Cells
                     .Select(static cell => cell.TownType)
                     .Where(static code => code is not 0 and not 34 and not 35)
                     .GroupBy(static code => code))
        {
            report.Add(
                LegacyImportSeverity.Warning,
                "map.unknown-town-code",
                $"map.town-code.{group.Key}",
                $"Town code {group.Key} is unknown; no settlement feature was inferred.",
                group.Count());
        }

        foreach (var group in map.Cells
                     .Select(static cell => cell.River)
                     .Where(static code => code != 0 && !LegacyRiverCodes.KnownPaths.ContainsKey(code))
                     .GroupBy(static code => code))
        {
            report.Add(
                LegacyImportSeverity.Warning,
                "map.unknown-river-code",
                $"map.river-code.{group.Key}",
                $"River code {group.Key} is unknown; no river path was inferred.",
                group.Count());
        }
    }

    private static RiverPathContent? DecodeRiver(byte code) =>
        LegacyRiverCodes.TryDecode(code, out var path)
            ? new RiverPathContent { First = path.First, Second = path.Second }
            : null;

    private static void AddResource(
        byte code,
        ICollection<string> resources,
        IReadOnlyDictionary<byte, string> resourceKeys)
    {
        if (resourceKeys.TryGetValue(code, out var key))
        {
            resources.Add(key);
        }
    }

    private static string FindName(
        IReadOnlyDictionary<uint, string> names,
        uint id,
        string description,
        LegacyImportReport report)
    {
        if (names.TryGetValue(id, out var name))
        {
            return name;
        }

        report.Add(
            LegacyImportSeverity.Warning,
            "scenario.missing-name",
            $"{description.ToLowerInvariant().Replace(' ', '-')}.{id.ToString(CultureInfo.InvariantCulture)}",
            $"{description} {id} has no legacy name; a deterministic fallback was used.");
        return $"Legacy {description} {id.ToString(CultureInfo.InvariantCulture)}";
    }

    private static bool IsValidPackageKey(string key) =>
        key.Length is >= 1 and <= 96 &&
        Regex.IsMatch(key, "^[a-z0-9](?:[a-z0-9._-]*[a-z0-9])?$", RegexOptions.CultureInvariant);

    private static string TerrainKey(byte code) => Terrains.TryGetValue(code, out var terrain)
        ? $"terrain.{terrain.Name}"
        : $"terrain.legacy-unknown-{code.ToString("D3", CultureInfo.InvariantCulture)}";

    /// <summary>
    /// An unknown terrain code is not improvable. Nothing is known about the
    /// ground, and guessing that a civilian may work it would silently invent a
    /// rule about a tile we cannot even name.
    /// </summary>
    private static TerrainContentDefinition TerrainDefinitionFor(byte code) =>
        Terrains.TryGetValue(code, out var terrain)
            ? new TerrainContentDefinition
            {
                Key = TerrainKey(code),
                Name = terrain.DisplayName,
                IsImprovable = terrain.IsImprovable,
                Prospecting = terrain.Prospecting switch
                {
                    LegacyProspecting.Open => new ProspectingContent(),
                    LegacyProspecting.NeedsOilDrilling => new ProspectingContent
                    {
                        RequiredTechnology = TechnologyKey(OilDrilling),
                    },
                    _ => null,
                },

                // Null is "rail may never be laid here", which is ocean's answer
                // and an unknown terrain's. Everything else names the technology
                // the Benefits of Technology Table gives it, and the price the
                // ground charges for a tile of track.
                Rail = terrain.Rail is null
                    ? null
                    : new RailContent
                    {
                        RequiredTechnology = TechnologyKey(terrain.Rail),
                        CashCost = terrain.RailCost,
                    },
            }
            : new TerrainContentDefinition
            {
                Key = TerrainKey(code),
                Name = $"Unknown terrain {code.ToString(CultureInfo.InvariantCulture)}",
                IsImprovable = false,
            };

    private static string ResourceKey(byte code) => $"resource.{ResourceNames[code]}";

    /// <summary>
    /// The fifteen commodities the world market trades, in the original's own commodity
    /// order, with the prices from its Bid and Offers screen.
    /// </summary>
    /// <remarks>
    /// <b>The order is a rule, not a presentation detail.</b> It decides which deals get
    /// cargo holds: "IMPERIALISM always uses an established order when expending the Great
    /// Powers' merchant marine for trade… Clothing deals, for example, are always
    /// considered prior to all other deals because clothing is the first item in commodity
    /// order. Reserving some cargo holds for later deals becomes an important skill."
    /// <para>
    /// The prices fall in three tiers — 100 raw, 300 material, 900 goods — and the 3x step
    /// is structural: every recipe takes two input units per unit of output, so 2x inputs
    /// plus 50% value added lands on the next tier. <b>Two entries break it and are
    /// transcribed rather than derived</b>: canned food at 100, because its input is grain
    /// and grain has no market price to mark up, and horses at 300 for no recoverable
    /// reason.
    /// </para>
    /// <para>
    /// <b>What is missing from this list is as informative as what is in it.</b> The eight
    /// commodities with no entry are exactly the ones the manual says cannot be traded —
    /// grain, fruit, livestock and fish ("food resources cannot be traded on the world
    /// market"), gold and gems ("they never reach the industry warehouse and they cannot be
    /// traded"), and oil and fuel. Three of those four groups are stated in prose, which
    /// makes the screenshot and the manual agree independently. See
    /// <c>docs/formulas/trade.md</c>.
    /// </para>
    /// </remarks>
    private static readonly IReadOnlyList<(string Key, long Price)> TradeRoster =
    [
        ("clothing", 900),
        ("furniture", 900),
        ("hardware", 900),
        ("armaments", 900),
        ("canned-food", 100),
        ("fabric", 300),
        ("lumber", 300),
        ("paper", 300),
        ("steel", 300),
        ("cotton", 100),
        ("wool", 100),
        ("timber", 100),
        ("coal", 100),
        ("iron", 100),
        ("horses", 300),
    ];

    private static CommodityContentDefinition[] CreateStandardCommodities() =>
    [
        Commodity("grain", "Grain", CommodityCategory.Raw),
        Commodity("livestock", "Livestock", CommodityCategory.Raw),
        Commodity("fruit", "Fruit", CommodityCategory.Raw),
        Commodity("fish", "Fish", CommodityCategory.Raw),
        Commodity("cotton", "Cotton", CommodityCategory.Raw),
        Commodity("wool", "Wool", CommodityCategory.Raw),
        Commodity("horses", "Horses", CommodityCategory.Raw),
        Commodity("timber", "Timber", CommodityCategory.Raw),
        Commodity("coal", "Coal", CommodityCategory.Raw),
        Commodity("iron", "Iron", CommodityCategory.Raw),
        Commodity("oil", "Oil", CommodityCategory.Raw),
        Commodity("gold", "Gold", CommodityCategory.Raw),
        Commodity("gems", "Gems", CommodityCategory.Raw),
        Commodity("canned-food", "Canned Food", CommodityCategory.Material),
        Commodity("fabric", "Fabric", CommodityCategory.Material),
        Commodity("paper", "Paper", CommodityCategory.Material),
        Commodity("lumber", "Lumber", CommodityCategory.Material),
        Commodity("steel", "Steel", CommodityCategory.Material),
        Commodity("fuel", "Fuel", CommodityCategory.Material),
        Commodity("clothing", "Clothing", CommodityCategory.Goods),
        Commodity("furniture", "Furniture", CommodityCategory.Goods),
        Commodity("hardware", "Hardware", CommodityCategory.Goods),
        Commodity("armaments", "Armaments", CommodityCategory.Goods),
    ];

    private static CommodityContentDefinition Commodity(
        string key,
        string name,
        CommodityCategory category)
    {
        var order = -1;
        for (var index = 0; index < TradeRoster.Count; index++)
        {
            if (string.Equals(TradeRoster[index].Key, key, StringComparison.Ordinal))
            {
                order = index;
                break;
            }
        }

        return new CommodityContentDefinition
        {
            Key = $"commodity.{key}",
            Name = name,
            Category = category,

            // Gold and gems are the manual's only two, and it prices both.
            // Everything else reaches the warehouse.
            CashPerUnit = CashPerUnit.TryGetValue(key, out var rate) ? rate : null,

            // Absent for the eight the market never sees, which is what makes them
            // untradable rather than free.
            WorldPrice = order < 0 ? null : TradeRoster[order].Price,
            TradeOrder = order < 0 ? null : order,
        };
    }

    /// <summary>
    /// How this world's prices answer to supply and demand.
    /// </summary>
    /// <remarks>
    /// <b>The direction is the manual's and every number is a guess.</b> It states the
    /// direction outright and no magnitude anywhere, and the clearing price is the oldest
    /// unknown on <c>docs/formulas/_index.md</c>. The defaults live in
    /// <see cref="ProportionalTradeMarket"/> so content and code cite one place.
    /// </remarks>
    private static TradeContentSettings CreateStandardTradeMarket() => new()
    {
        StepPercent = ProportionalTradeMarket.DefaultStepPercent,
        TolerancePercent = ProportionalTradeMarket.DefaultTolerancePercent,
        FloorPercent = ProportionalTradeMarket.DefaultFloorPercent,
        CeilingPercent = ProportionalTradeMarket.DefaultCeilingPercent,
    };

    /// <summary>
    /// The thirteen classes of ship: five merchants and eight warships.
    /// </summary>
    /// <remarks>
    /// <b>This is not the game's array order, and nothing here depends on it being.</b>
    /// Content refers to a hull by key, so the order below is only a listing. The order
    /// <em>does</em> matter for one thing — a legacy <c>ship</c> record's type is a 1-based
    /// index into the game's own array — and that is why <c>ship</c> records are still
    /// deferred: the array order needs the binary to settle. See
    /// <c>docs/formulas/trade.md</c>.
    /// <para>
    /// <b>Cargo is the only column this engine reads</b>, and only the merchants have any:
    /// a country's merchant marine is the sum of the cargo it owns. The four merchant cargo
    /// figures come from the owner; the Freighter's is unknown and is left at zero rather
    /// than invented, which makes it a hull nobody would build until the number arrives.
    /// </para>
    /// <para>
    /// <b>No build costs are transcribed, deliberately.</b> The owner's cost table proved to
    /// have a misaligned column — what it labelled Speed was battle movement, and it
    /// carried no sailing speed at all — so every value after Hull in it is suspect,
    /// including arms. Shipping possibly-shifted numbers is worse than shipping none, and
    /// nothing reads a build bill until a shipyard exists. The arms figure in particular
    /// later sets the force landable at a beachhead, so being wrong by one would be a
    /// gameplay bug rather than a cosmetic one.
    /// </para>
    /// <para>
    /// The warship combat stats are from the manual's own Ship Type table and are
    /// transcribed here and read by nothing, exactly as the technology table's
    /// unmodellable entries are.
    /// </para>
    /// </remarks>
    private static readonly IReadOnlyList<LegacyShipType> ShipTypes =
    [
        // The merchants. None appears in the manual's combat table, because they never
        // fight; speed still matters to them, for outrunning a blockade, and is unknown.
        new("trader", "Trader", Cargo: 2),
        new("indiaman", "Indiaman", Cargo: 4),
        new("steamship", "Steamship", Cargo: 8, RequiredTechnology: Paddlewheels),
        new("clipper", "Clipper", Cargo: 4, RequiredTechnology: StreamlinedHulls),
        new("freighter", "Freighter"),

        // The eight warships, in the manual's printed order: four fast ships and four
        // battleships, alternating by era.
        new("frigate", "Frigate", Combat: new(3, 5, 10, 35, 3)),
        new("ship-of-the-line", "Ship-of-the-Line", Combat: new(6, 6, 20, 65, 2)),
        new("raider", "Raider", Combat: new(3, 7, 20, 30, 5), RequiredTechnology: Paddlewheels),
        new("ironclad", "Ironclad", Combat: new(5, 8, 55, 50, 3)),
        new("armoured-cruiser", "Armoured Cruiser", Combat: new(6, 9, 50, 40, 6)),
        new("advanced-ironclad", "Advanced Ironclad", Combat: new(10, 10, 60, 70, 4)),
        new("battle-cruiser", "Battle Cruiser", Combat: new(18, 13, 55, 90, 6)),
        new("dreadnought", "Dreadnought", Combat: new(20, 13, 70, 115, 5)),
    ];

    private const string StreamlinedHulls = "Streamlined Hulls";
    private const string Paddlewheels = "Paddlewheels";

    /// <summary>
    /// The fleet every power starts with, and therefore its opening merchant marine.
    /// </summary>
    /// <remarks>
    /// <b>Not a guess.</b> All three skirmish scenarios — `s10`, `s11` and `s15` — give
    /// every one of their seven powers three ships of type 1, independently of each other.
    /// That is the same agreement that settled the fair start's mills and workforce, and
    /// <c>ship</c> is not one of the seven records a skirmish omits.
    /// <para>
    /// <b>The inference is which class type 1 is.</b> Both candidate orderings of the game's
    /// ship array put the Trader first, so three Traders — six cargo holds — is the
    /// reading. If the binary says the array begins elsewhere, this moves with it.
    /// </para>
    /// </remarks>
    private static readonly (string Type, long Count) StartingFleet = ("trader", 3);

    private static ShipTypeContentDefinition[] CreateStandardShipTypes() =>
        ShipTypes.Select(static type => new ShipTypeContentDefinition
        {
            Key = ShipTypeKey(type.Key),
            Name = type.Name,
            Cargo = type.Cargo,
            RequiredTechnology = type.RequiredTechnology is null
                ? null
                : TechnologyKey(type.RequiredTechnology),
            Combat = type.Combat is not { } combat
                ? null
                : new ShipCombatContent
                {
                    Firepower = combat.Firepower,
                    Range = combat.Range,
                    Armour = combat.Armour,
                    Hull = combat.Hull,
                    Speed = combat.Speed,
                },
        }).ToArray();

    private static string ShipTypeKey(string key) => $"ship.{key}";

    private static ProductionFacilityContentDefinition[] CreateStandardProductionFacilities() =>
    [
        Facility("textile-mill", "Textile Mill", ProductionCapacityMode.Limited, MillLadder),
        Facility("clothing-factory", "Clothing Factory", ProductionCapacityMode.Limited, FactoryLadder),
        Facility("steel-mill", "Steel Mill", ProductionCapacityMode.Limited, MillLadder),
        Facility("metal-works", "Metal Works", ProductionCapacityMode.Limited, FactoryLadder),
        Facility("lumber-mill", "Lumber Mill", ProductionCapacityMode.Limited, MillLadder),
        Facility("furniture-factory", "Furniture Factory", ProductionCapacityMode.Limited, FactoryLadder),
        Facility("oil-refinery", "Oil Refinery", ProductionCapacityMode.Limited, FactoryLadder),
        Facility("food-processing", "Food Processing", ProductionCapacityMode.Unlimited),
    ];

    /// <summary>
    /// "For mills, which start at capacity 2, the improvement levels are 4, 8,
    /// 16, 24 and then continue to increase by eight at a time."
    /// </summary>
    private static CapacityLadderContent MillLadder => new()
    {
        Rungs = [2, 4, 8, 16, 24],
        Increment = 8,
    };

    /// <summary>
    /// "For factories, which start at capacity 1, the improvement levels are 2,
    /// 4, 8, 12 and then continue to increase four at a time."
    /// </summary>
    private static CapacityLadderContent FactoryLadder => new()
    {
        Rungs = [1, 2, 4, 8, 12],
        Increment = 4,
    };

    /// <summary>
    /// The Capitol's terms: "the comforts of a developing economy: canned foods,
    /// clothing, and furniture", and a limit of "one-fourth of the number of
    /// provinces you own, rounded down".
    /// </summary>
    /// <remarks>
    /// **One of each per worker is a guess.** The manual names the three
    /// commodities and never says how much of any of them, so this is a real
    /// economic constant nobody has measured. See
    /// <c>docs/formulas/migration.md</c>; do not cite it as evidence.
    /// </remarks>
    private static MigrationContent CreateStandardMigration() => new()
    {
        CostPerWorker =
        [
            Quantity("canned-food", 1),
            Quantity("clothing", 1),
            Quantity("furniture", 1),
        ],
        ProvincesPerRecruit = 4,
    };

    /// <summary>
    /// "For each point of capacity built, you pay one lumber and one steel from
    /// your Warehouse." Expansion requires no labour.
    /// </summary>
    private static CommodityQuantityContent[] CreateStandardExpansionCost() =>
    [
        Quantity("lumber", 1),
        Quantity("steel", 1),
    ];

    /// <summary>
    /// The railyard: "as with other industrial expansion, increasing transport
    /// capacity requires both lumber and steel", so it takes the same rate the
    /// manual prices industrial capacity at.
    /// </summary>
    /// <remarks>
    /// The difference is labour. Expanding a mill needs none; the railyard needs
    /// "steel, lumber, and available labour". The manual never says how much, so
    /// the rate is the same total-input-units rule every recipe's labour cost
    /// follows — two inputs, two labour. See <c>docs/formulas/transport.md</c>.
    /// </remarks>
    /// <summary>
    /// What an Engineer's two structures cost. Rail is not here any more: it is
    /// priced per terrain, beside the gate, on <see cref="RailContent.CashCost"/>.
    /// </summary>
    /// <remarks>
    /// <b>These are now the two weakest numbers in this importer, and rail is no
    /// longer one of them.</b> The depot and the port come from <b>the owner's
    /// recollection of playing the original</b> — around 1,500 and around 2,000 —
    /// which the scoreboard rates "good for shape, poor for exact numbers". The
    /// manual prices neither and states only the ordering: ports "cost more than
    /// depots". These two satisfy it, and the price list does not price either, so
    /// they stand unchallenged. See <c>docs/formulas/engineer.md</c>.
    /// </remarks>
    private static ConstructionContentSettings CreateStandardConstruction() => new()
    {
        DepotCashCost = 1500,
        PortCashCost = 2000,
    };

    /// <summary>
    /// What raising a tile costs, indexed by the level being reached. Index 0 is
    /// never used — nothing is improved <em>to</em> level zero.
    /// </summary>
    /// <remarks>
    /// <b>The owner's recollection from play</b>, and the manual corroborates
    /// only that a cost exists at all: a player might tell a unit to do nothing
    /// "when you lack the cash to pay for the civilian's improvements."
    /// <para>
    /// The climb is steep — a Level III tile costs thirty times what opening it
    /// did — which is what makes a treasury a real constraint on development
    /// rather than a formality. Flat across deposits, and per cell rather than
    /// per deposit: a hex carrying two resources costs the same as one. See
    /// <c>docs/formulas/development.md</c>.
    /// </para>
    /// </remarks>
    private static ImprovementContentSettings CreateStandardImprovement() => new()
    {
        CashCostByDevelopmentLevel = [0, 100, 1000, 3000],
    };

    private static TransportContentSettings CreateStandardTransport() => new()
    {
        CostPerCapacityPoint =
        [
            Quantity("lumber", 1),
            Quantity("steel", 1),
        ],
        LabourPerCapacityPoint = 2,
    };

    /// <summary>
    /// What a power's network carries before it builds anything.
    /// </summary>
    /// <remarks>
    /// <b>A guess, and the only one in the transport system.</b> A skirmish
    /// carries no <c>tran</c> record at all, so the corpus attests only that the
    /// engine supplies a value; the missions that do carry one are authored
    /// special cases this project has a standing rule against mining. Zero was
    /// the alternative and would leave every imported skirmish unable to move
    /// anything off its own land. Do not cite this number as evidence.
    /// </remarks>
    private const int DefaultTransportCapacity = 20;

    /// <summary>
    /// What a power's treasury holds on turn one.
    /// </summary>
    /// <remarks>
    /// <b>That there is a treasury at all is the manual's</b>: "each Great Power
    /// begins the game with a limited amount of cash which is totally inadequate
    /// to meet its needs." <b>The amount is a guess.</b>
    /// <para>
    /// Five of the ten shipped scenarios carry no <c>cash</c> record and five
    /// author 1,500 to 15,000 apiece — <c>s3</c> alone spans that whole range
    /// across its seven powers — so there is no constant in the corpus to find,
    /// and this project has a standing rule against mining authored missions for
    /// one. The number below is invented to sit in that spread rather than
    /// derived from it: enough to build a couple of structures and not a network.
    /// Do not cite it as evidence. See <c>docs/formulas/money.md</c>.
    /// </para>
    /// </remarks>
    private const int DefaultStartingCash = 5000;

    /// <summary>
    /// What a unit of gold and a unit of gems are worth when the network carries
    /// them. <b>The manual prices both outright</b>: "each unit of gold
    /// transported increases your cash by $200"; "transported gems convert to
    /// cash at $500 per unit."
    /// </summary>
    /// <remarks>
    /// Keyed by commodity name rather than by deposit code because the manual
    /// attaches the conversion to the transporting rather than to the mining.
    /// </remarks>
    private static readonly IReadOnlyDictionary<string, long> CashPerUnit =
        new Dictionary<string, long>(StringComparer.Ordinal)
        {
            ["gold"] = 200,
            ["gems"] = 500,
        };

    /// <summary>
    /// What a power finds in its warehouse on turn one.
    /// </summary>
    /// <remarks>
    /// <b>That there is a stockpile at all is the manual's, and so are the two
    /// commodities</b>: "you must construct a lumber and steel mill with your
    /// <em>initial stockpiles of lumber and steel</em>, or you may be forced to
    /// beg for lumber and steel from other Great Powers." A power starting with
    /// an empty warehouse could do neither.
    /// <para>
    /// <b>The quantity is a guess.</b> It matters more than it looks: a country
    /// with an empty warehouse and a small network cannot buy the railyard that
    /// would let it carry the materials to fill the warehouse, and the soak
    /// shows it never escapes. See <c>docs/formulas/transport.md</c>.
    /// </para>
    /// </remarks>
    private static CommodityQuantityContent[] CreateStandardStartingStock() =>
    [
        Quantity("lumber", 20),
        Quantity("steel", 20),
    ];

    private static ProductionRecipeContentDefinition[] CreateStandardProductionRecipes() =>
    [
        Recipe("fabric-from-cotton", "Fabric from Cotton", "textile-mill", [("cotton", 2)], [("fabric", 1)]),
        Recipe("fabric-from-wool", "Fabric from Wool", "textile-mill", [("wool", 2)], [("fabric", 1)]),
        Recipe("clothing-from-fabric", "Clothing", "clothing-factory", [("fabric", 2)], [("clothing", 1)]),
        Recipe("steel-from-coal-and-iron", "Steel", "steel-mill", [("coal", 1), ("iron", 1)], [("steel", 1)]),
        Recipe("hardware-from-steel", "Hardware", "metal-works", [("steel", 2)], [("hardware", 1)]),
        Recipe("armaments-from-steel", "Armaments", "metal-works", [("steel", 2)], [("armaments", 1)]),
        Recipe("lumber-from-timber", "Lumber", "lumber-mill", [("timber", 2)], [("lumber", 1)]),
        Recipe("paper-from-timber", "Paper", "lumber-mill", [("timber", 2)], [("paper", 1)]),
        Recipe("furniture-from-lumber", "Furniture", "furniture-factory", [("lumber", 2)], [("furniture", 1)]),
        Recipe("fuel-from-oil", "Fuel", "oil-refinery", [("oil", 2)], [("fuel", 1)]),
        Recipe("canned-food-from-fish", "Canned Food from Fish", "food-processing", [("grain", 2), ("fruit", 1), ("fish", 1)], [("canned-food", 2)]),
        Recipe("canned-food-from-livestock", "Canned Food from Livestock", "food-processing", [("grain", 2), ("fruit", 1), ("livestock", 1)], [("canned-food", 2)]),
    ];

    private static ProductionFacilityContentDefinition Facility(
        string key,
        string name,
        ProductionCapacityMode capacityMode,
        CapacityLadderContent? capacityLadder = null) => new()
        {
            Key = $"facility.{key}",
            Name = name,
            CapacityMode = capacityMode,
            CapacityLadder = capacityLadder,
        };

    /// <summary>
    /// Labour is not passed in because no original recipe needs it to be: the
    /// manual prices clothing at two fabric and two labour, and every recipe the
    /// original ships spends exactly two input units per unit of output, so the
    /// input total reproduces that rate throughout. See
    /// <c>docs/formulas/production.md</c>.
    /// </summary>
    private static ProductionRecipeContentDefinition Recipe(
        string key,
        string name,
        string facility,
        IEnumerable<(string Commodity, long Quantity)> inputs,
        IEnumerable<(string Commodity, long Quantity)> outputs)
    {
        var inputArray = inputs.ToArray();
        return new ProductionRecipeContentDefinition
        {
            Key = $"recipe.{key}",
            Name = name,
            Facility = $"facility.{facility}",
            CapacityCost = 1,
            LabourCost = inputArray.Sum(static item => item.Quantity),
            Inputs = inputArray.Select(static item => Quantity(item.Commodity, item.Quantity)).ToArray(),
            Outputs = outputs.Select(static item => Quantity(item.Commodity, item.Quantity)).ToArray(),
        };
    }

    private static CommodityQuantityContent Quantity(string commodity, long quantity) => new()
    {
        Commodity = $"commodity.{commodity}",
        Quantity = quantity,
    };

    private static string CountryKey(uint id) => $"country.legacy.{id.ToString("D3", CultureInfo.InvariantCulture)}";

    private static string ProvinceKey(uint id) => $"province.legacy.{id.ToString("D5", CultureInfo.InvariantCulture)}";

    private static string SeaZoneKey(uint id) => $"sea-zone.legacy.{id.ToString("D3", CultureInfo.InvariantCulture)}";

    private static readonly (byte Bit, byte OppositeBit, string Name)[] RailDirections =
    [
        (1, 8, "north-east"),
        (2, 16, "east"),
        (4, 32, "south-east"),
        (8, 1, "south-west"),
        (16, 2, "west"),
        (32, 4, "north-west"),
    ];
}
