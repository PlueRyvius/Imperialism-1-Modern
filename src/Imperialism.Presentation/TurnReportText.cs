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

    public static string Describe(PendingDeliverySource source) => source switch
    {
        PendingDeliverySource.Transport => "carried",
        PendingDeliverySource.Trade => "bought",
        PendingDeliverySource.Extraction => "gathered",
        _ => throw new ArgumentOutOfRangeException(nameof(source)),
    };
}
