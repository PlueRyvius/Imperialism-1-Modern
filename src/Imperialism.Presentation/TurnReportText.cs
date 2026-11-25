using Imperialism.Core;

namespace Imperialism.Presentation;

/// <summary>
/// Plain words for the enumerations a turn report has to say out loud.
/// </summary>
/// <remarks>
/// Every clause here is compressed from that member's own XML documentation in
/// <c>TurnResolution.cs</c>, which was already written as an explanation to a
/// player rather than to a compiler. Nothing is invented, and where the project
/// has settled on a phrase — "short of a cargo hold" — it is used verbatim.
///
/// These are <em>clauses</em>, not sentences. Each is spliced after a colon into
/// a sentence naming the country and what it tried to do, so none begins with a
/// capital or ends with a stop.
///
/// Public rather than internal because the Bid and Offers and Investment screens
/// will need exactly these sentences when they land, and because the test that
/// holds every enumeration to having distinct text has to reach them. There is
/// no <c>InternalsVisibleTo</c> anywhere in this repository and this is not the
/// place to introduce one.
/// </remarks>
public static class TurnReportText
{
    public static string Describe(CivilianOrderRefusal reason) => reason switch
    {
        CivilianOrderRefusal.NoSuchCivilian => "no civilian carries that id, so it has most likely died",
        CivilianOrderRefusal.NotYours => "the civilian belongs to another country",
        CivilianOrderRefusal.AlreadyWorking => "it is part way through a job and cannot be redirected",
        CivilianOrderRefusal.TargetOffMap => "the tile is off the map",
        CivilianOrderRefusal.TargetNotLand => "the tile is water",
        CivilianOrderRefusal.TargetNotYourTerritory => "the tile belongs to somebody else",
        CivilianOrderRefusal.TerrainCannotBeImproved => "that ground admits no civilian at all",
        CivilianOrderRefusal.NoDepositThisCivilianWorks => "nothing on the tile is improved by this kind of civilian",
        CivilianOrderRefusal.AlreadyFullyDeveloped => "the tile is already at the top rung of its yield curve",
        CivilianOrderRefusal.TerrainCannotBeProspected => "that ground hides nothing to find",
        CivilianOrderRefusal.ProspectingTechnologyNotKnown => "the ground is searchable and we have not invested in what it takes",
        CivilianOrderRefusal.AlreadyProspected => "we have searched this tile before, and a second look finds the same nothing",
        CivilianOrderRefusal.DepositNotYetDiscovered => "the deposit is there and nobody has found it yet",
        CivilianOrderRefusal.ImprovementTechnologyNotKnown => "the tile has a rung left and we do not know how to climb it",
        CivilianOrderRefusal.NotAnEngineer => "that work belongs to another kind of civilian",
        CivilianOrderRefusal.NothingCanBeBuiltInThisWorld => "this world prices no construction at all",
        CivilianOrderRefusal.RailNeedsAnAdjacentTile => "rail is laid only to a tile next to the Engineer",
        CivilianOrderRefusal.StructureNeedsTheEngineersOwnTile => "that is built where the Engineer stands and nowhere else",
        CivilianOrderRefusal.TerrainCannotCarryRail => "no line crosses that ground, whatever we invest",
        CivilianOrderRefusal.ConstructionTechnologyNotKnown => "the ground can carry a line and we have not invested in what it takes",
        CivilianOrderRefusal.RailAlreadyBuilt => "the line is already there",
        CivilianOrderRefusal.DepotAlreadyBuilt => "the tile already has a depot",
        CivilianOrderRefusal.PortAlreadyBuilt => "the tile already has a port",
        CivilianOrderRefusal.PortNeedsWater => "a port wants a coast or a river, and the tile is neither",
        CivilianOrderRefusal.NotEnoughCash => "the treasury will not cover it",
        _ => throw new ArgumentOutOfRangeException(nameof(reason)),
    };

    public static string Describe(TechnologyPurchaseRefusal reason) => reason switch
    {
        TechnologyPurchaseRefusal.NoSuchTechnology => "no technology carries that id in this world",
        TechnologyPurchaseRefusal.AlreadyKnown => "we hold it already",
        TechnologyPurchaseRefusal.NotYetAvailable => "its year has not come",
        TechnologyPurchaseRefusal.PrerequisiteNotKnown => "something it builds on is not known yet",
        TechnologyPurchaseRefusal.NotEnoughCash => "the treasury will not cover it, because building had first call",
        TechnologyPurchaseRefusal.NotForSale => "it has no price, so it was never on the screen",
        _ => throw new ArgumentOutOfRangeException(nameof(reason)),
    };

    public static string Describe(TradeRefusal reason) => reason switch
    {
        TradeRefusal.NothingToSell => "nothing to sell, because industry had already claimed it",
        TradeRefusal.NoBuyer => "offered and unsold",
        TradeRefusal.NotEnoughCash => "the treasury would not cover the world price",
        TradeRefusal.NoMerchantCapacity => "short of a cargo hold",
        _ => throw new ArgumentOutOfRangeException(nameof(reason)),
    };

    public static string Describe(EngineerConstruction structure) => structure switch
    {
        EngineerConstruction.Rail => "rail",
        EngineerConstruction.Depot => "a depot",
        EngineerConstruction.Port => "a port",
        _ => throw new ArgumentOutOfRangeException(nameof(structure)),
    };

    public static string Describe(PowerRefusal reason) => reason switch
    {
        PowerRefusal.NotModelled => "this world has no power",
        PowerRefusal.NoPowerPlant => "it has no Power Plant",
        PowerRefusal.OverTurnLimit => "that is more than a plant may burn in one turn",
        PowerRefusal.NotEnoughFuel => "the warehouse does not hold that much fuel",
        _ => throw new ArgumentOutOfRangeException(nameof(reason)),
    };

    public static string Describe(FacilityExpansionRefusal reason) => reason switch
    {
        FacilityExpansionRefusal.TechnologyNotKnown => "we have not invested in the technology it needs",
        _ => throw new ArgumentOutOfRangeException(nameof(reason)),
    };

    public static string Describe(ShipBuildRefusal reason) => reason switch
    {
        ShipBuildRefusal.TechnologyNotKnown => "we have not invested in the technology the hull needs",
        ShipBuildRefusal.NotEnoughCommodities => "the warehouse does not hold the whole bill",
        ShipBuildRefusal.UnknownType => "no such class of ship exists",
        ShipBuildRefusal.NonPositiveCount => "an order must ask for at least one ship",
        _ => throw new ArgumentOutOfRangeException(nameof(reason)),
    };

    public static string Describe(WarDeclarationRefusal reason) => reason switch
    {
        WarDeclarationRefusal.NoSuchCountry => "no country carries that id in this world",
        WarDeclarationRefusal.Itself => "a country cannot make war on itself",
        WarDeclarationRefusal.AlreadyAtWar => "we are already at war, or they declared first this turn",
        WarDeclarationRefusal.AlreadyDeclaredThisTurn => "the same declaration was already made this turn",
        _ => throw new ArgumentOutOfRangeException(nameof(reason)),
    };

    public static string Describe(NavalOrderRefusal reason) => reason switch
    {
        NavalOrderRefusal.NoFleetsSelected => "no fleet was selected",
        NavalOrderRefusal.FleetSelectedTwice => "a fleet was selected twice",
        NavalOrderRefusal.NoSuchFleet => "no fleet carries that id",
        NavalOrderRefusal.FleetNotYours => "a fleet belongs to another country",
        NavalOrderRefusal.FleetNotPlaced => "a fleet has not been placed at sea yet",
        NavalOrderRefusal.FleetAlreadyInTaskForce => "a fleet already sails in a task force",
        NavalOrderRefusal.FleetsInDifferentSeaZones => "the fleets lie in different sea zones",
        NavalOrderRefusal.NoSuchTaskForce => "no task force carries that id",
        NavalOrderRefusal.TaskForceNotYours => "the task force belongs to another country",
        NavalOrderRefusal.AlreadyOrderedThisTurn => "it already has its mission or move this turn",
        NavalOrderRefusal.NoSuchSeaZone => "no such sea zone exists",
        NavalOrderRefusal.AlreadyInThatSeaZone => "it is already in that sea zone",
        NavalOrderRefusal.OutOfRange => "that sea zone is out of range this turn",
        NavalOrderRefusal.NoSuchCountry => "no such country exists to blockade",
        NavalOrderRefusal.BlockadeOwnCountry => "a country cannot blockade its own capital",
        NavalOrderRefusal.TargetHasNoCapital => "that country holds no capital to blockade",
        NavalOrderRefusal.CapitalNotOnThisSeaZone => "that capital does not lie on the task force's sea zone",
        NavalOrderRefusal.UnknownProvince => "no such province exists to land on",
        NavalOrderRefusal.LandingOnOwnProvince => "a country cannot land on its own province",
        NavalOrderRefusal.ProvinceNotOnThisSeaZone => "that province does not touch the task force's sea zone",
        _ => throw new ArgumentOutOfRangeException(nameof(reason)),
    };

    public static string Describe(WorkerTrainingRefusal reason) => reason switch
    {
        WorkerTrainingRefusal.NotOffered => "the Trade School offers no training from that grade",
        WorkerTrainingRefusal.NotEnoughWorkers => "it has fewer healthy workers of that grade",
        WorkerTrainingRefusal.NotEnoughCommodities => "the warehouse does not hold the paper it costs",
        WorkerTrainingRefusal.NotEnoughCash => "the treasury will not cover it",
        _ => throw new ArgumentOutOfRangeException(nameof(reason)),
    };

    public static string Describe(CivilianPurchaseRefusal reason) => reason switch
    {
        CivilianPurchaseRefusal.UnknownType => "no such kind of civilian exists",
        CivilianPurchaseRefusal.NonPositiveCount => "an order must ask for at least one civilian",
        CivilianPurchaseRefusal.NotPurchasable => "the University does not train that kind of civilian",
        CivilianPurchaseRefusal.TechnologyNotKnown => "we have not invested in the technology it needs",
        CivilianPurchaseRefusal.NoCapital => "there is no capital to receive them",
        CivilianPurchaseRefusal.NotEnoughExperts => "it has fewer healthy expert workers than they cost",
        CivilianPurchaseRefusal.NotEnoughCommodities => "the warehouse does not hold the paper it costs",
        CivilianPurchaseRefusal.NotEnoughCash => "the treasury will not cover it",
        _ => throw new ArgumentOutOfRangeException(nameof(reason)),
    };

    /// <summary>The verb phrase for an overture, ready to be followed by its target.</summary>
    public static string Describe(DiplomaticOvertureKind kind) => kind switch
    {
        DiplomaticOvertureKind.Consulate => "open a trade consulate in",
        DiplomaticOvertureKind.Embassy => "open an embassy in",
        DiplomaticOvertureKind.Pact => "offer a non-aggression pact to",
        DiplomaticOvertureKind.Peace => "make peace with",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    public static string Describe(DiplomaticOvertureRefusal reason) => reason switch
    {
        DiplomaticOvertureRefusal.NoSuchCountry => "no country carries that id in this world",
        DiplomaticOvertureRefusal.Itself => "a country cannot make overtures to itself",
        DiplomaticOvertureRefusal.NotAGreatPower => "only a Great Power makes overtures",
        DiplomaticOvertureRefusal.AlreadyOrderedThisTurn => "the same overture was already made this turn",
        DiplomaticOvertureRefusal.NotOffered => "this world sets no price for it",
        DiplomaticOvertureRefusal.AlreadyEstablished => "we already have one there",
        DiplomaticOvertureRefusal.AtWar => "we are at war, or going to war, with them",
        DiplomaticOvertureRefusal.NoConsulate => "an embassy needs an open trade consulate first",
        DiplomaticOvertureRefusal.NoEmbassy => "a pact needs an open embassy first",
        DiplomaticOvertureRefusal.TargetIsAGreatPower => "pacts are offered only to Minor Nations",
        DiplomaticOvertureRefusal.NotEnoughCash => "the treasury will not cover it",
        DiplomaticOvertureRefusal.NotAtWar => "we are not at war with them",
        DiplomaticOvertureRefusal.Declined => "they did not offer peace in return",
        DiplomaticOvertureRefusal.AlreadyMadeThisTurn => "peace was already made this turn on their offer",
        _ => throw new ArgumentOutOfRangeException(nameof(reason)),
    };

    /// <summary>The verb phrase for a trade policy order, ready to be followed by its target.</summary>
    public static string Describe(TradePolicy policy) => policy.Kind switch
    {
        TradePolicyKind.None => "cancel its trade policy toward",
        TradePolicyKind.Subsidy => $"grant a {policy.Percent}% trade subsidy to",
        TradePolicyKind.Boycott => "declare a trade boycott of",
        _ => throw new ArgumentOutOfRangeException(nameof(policy)),
    };

    /// <summary>A standing trade policy as a noun phrase.</summary>
    public static string Name(TradePolicy policy) => policy.Kind switch
    {
        TradePolicyKind.None => "no trade policy",
        TradePolicyKind.Subsidy => $"{policy.Percent}% trade subsidy",
        TradePolicyKind.Boycott => "trade boycott",
        _ => throw new ArgumentOutOfRangeException(nameof(policy)),
    };

    public static string Describe(TradePolicyRefusal reason) => reason switch
    {
        TradePolicyRefusal.NoSuchCountry => "no country carries that id in this world",
        TradePolicyRefusal.Itself => "a country sets no trade policy toward itself",
        TradePolicyRefusal.NotAGreatPower => "only a Great Power sets trade policies",
        TradePolicyRefusal.AlreadyOrderedThisTurn => "a trade policy toward them was already set this turn",
        TradePolicyRefusal.NoPost => "it needs a trade consulate or an embassy there, open as the turn began",
        TradePolicyRefusal.PercentNotOffered => "this world offers no subsidy of that size",
        _ => throw new ArgumentOutOfRangeException(nameof(reason)),
    };

    public static string Describe(CivilianDisbandRefusal reason) => reason switch
    {
        CivilianDisbandRefusal.NoSuchCivilian => "no civilian carries that id, so it has most likely died",
        CivilianDisbandRefusal.NotYours => "it is not ours to disband",
        _ => throw new ArgumentOutOfRangeException(nameof(reason)),
    };

    public static string Describe(RegimentPurchaseRefusal reason) => reason switch
    {
        RegimentPurchaseRefusal.NonPositiveCount => "an order must ask for at least one regiment",
        RegimentPurchaseRefusal.NotPurchasable => "the Armoury builds nothing of that kind",
        RegimentPurchaseRefusal.NoCapital => "there is no capital to muster them in",
        RegimentPurchaseRefusal.NotEnoughWorkers => "it has fewer healthy workers of the grade than they cost",
        RegimentPurchaseRefusal.NotEnoughCommodities => "the warehouse does not hold the whole bill",
        RegimentPurchaseRefusal.NotEnoughCash => "the treasury will not cover it",
        _ => throw new ArgumentOutOfRangeException(nameof(reason)),
    };

    public static string Describe(RegimentMoveRefusal reason) => reason switch
    {
        RegimentMoveRefusal.NoRegiments => "the order names no regiments",
        RegimentMoveRefusal.DuplicateRegiment => "the order names a regiment twice",
        RegimentMoveRefusal.NoSuchRegiment => "no regiment carries that id",
        RegimentMoveRefusal.NotYours => "a regiment named belongs to another country",
        RegimentMoveRefusal.AlreadyMoved => "a regiment named has already moved this turn",
        RegimentMoveRefusal.NotTogether => "the regiments named do not stand in one province",
        RegimentMoveRefusal.CannotLeaveProvince => "militia cannot be ordered out of its home province",
        RegimentMoveRefusal.UnknownProvince => "no such province exists",
        RegimentMoveRefusal.SameProvince => "they are already there",
        RegimentMoveRefusal.NotOwnProvince => "the province was not ours as the turn began",
        RegimentMoveRefusal.NotConnectedByRail => "it is not next door and no line of our own rail joins the two",
        RegimentMoveRefusal.CannotMoveByRail => "a regiment named cannot travel by rail",
        RegimentMoveRefusal.NotEnoughRailCapacity => "the railways cannot carry that many armaments this turn",
        RegimentMoveRefusal.NotAdjacent => "an attack may only be made from a neighbouring province",
        RegimentMoveRefusal.AlreadyOwn => "the province is already ours",
        RegimentMoveRefusal.NotAtWar => "we were not at war with its owner as the turn began",
        RegimentMoveRefusal.NotCoastal => "the regiments' province touches no sea to embark from",
        RegimentMoveRefusal.NoSeaPath => "no sea path joins their coast to the landing site",
        RegimentMoveRefusal.NotEnoughLandingCapacity => "the landing site cannot carry that many armaments this turn",
        _ => throw new ArgumentOutOfRangeException(nameof(reason)),
    };

    public static string Describe(ArmyCategory category) => category switch
    {
        ArmyCategory.Militia => "militia",
        ArmyCategory.LightInfantry => "light infantry",
        ArmyCategory.RegularInfantry => "regular infantry",
        ArmyCategory.HeavyInfantry => "heavy infantry",
        ArmyCategory.LightCavalry => "light cavalry",
        ArmyCategory.HeavyCavalry => "heavy cavalry",
        ArmyCategory.LightArtillery => "light artillery",
        ArmyCategory.HeavyArtillery => "heavy artillery",
        ArmyCategory.CombatEngineers => "combat engineers",
        ArmyCategory.General => "generals",
        _ => throw new ArgumentOutOfRangeException(nameof(category)),
    };

    public static string Describe(WorkerGrade grade) => grade switch
    {
        WorkerGrade.Untrained => "untrained",
        WorkerGrade.Trained => "trained",
        WorkerGrade.Expert => "expert",
        _ => throw new ArgumentOutOfRangeException(nameof(grade)),
    };

    public static string Describe(PendingDeliverySource source) => source switch
    {
        PendingDeliverySource.Transport => "carried",
        PendingDeliverySource.Trade => "bought",
        PendingDeliverySource.Extraction => "gathered",
        _ => throw new ArgumentOutOfRangeException(nameof(source)),
    };
}
