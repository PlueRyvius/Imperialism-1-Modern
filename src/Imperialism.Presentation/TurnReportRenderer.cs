using System.Globalization;
using Imperialism.Content;
using Imperialism.Core;

namespace Imperialism.Presentation;

/// <summary>How a line reads, so the client can style it without interpreting it.</summary>
public enum TurnReportKind : byte
{
    /// <summary>It happened.</summary>
    Outcome,

    /// <summary>Asked for something and did not get all of it.</summary>
    Shortfall,

    /// <summary>Gone for good: wasted, stranded, starved, fallen ill.</summary>
    Loss,

    /// <summary>The order was rejected outright.</summary>
    Refusal,
}

/// <summary>One finished sentence of a turn report.</summary>
/// <remarks>
/// A line is a record and not a string because the client needs three things it
/// must not work out for itself: which country the line is about, so it can put
/// the name in its own column; how to style it, which is a lookup on
/// <see cref="Kind"/> and never a reading of the words; and where on the map it
/// happened, so that centring the map on a line is later a change to a screen
/// rather than a change to this renderer.
/// <see cref="Text"/> is complete. The client concatenates nothing.
/// </remarks>
public sealed record TurnReportLine(
    TurnPhase Phase,
    TurnReportKind Kind,
    CountryId? Country,
    string? CountryName,
    CellIndex? Cell,
    string Text);

/// <summary>
/// Turns one <see cref="TurnEvent"/> into the sentences a player reads.
/// </summary>
/// <remarks>
/// <b>This is a separate public type on purpose; do not fold it back into
/// <see cref="TurnReportView"/>.</b> <c>TurnResolution</c>'s constructor is
/// internal, so no test can fabricate a resolution carrying a chosen set of
/// events. Rendering one event at a time is the only seam through which a test
/// can prove that every concrete event type produces words — and that test is
/// what turns "somebody added an event and forgot the renderer" from an
/// exception in front of a player into a red build.
///
/// It lives in Presentation rather than the client for two reasons that are both
/// checked: <c>HexMapProjectionTests</c> forbids Godot in this assembly, and
/// <c>docs/architecture.md</c> forbids a client script computing a game number.
/// Note that deciding <see cref="TurnReportKind.Shortfall"/> means comparing two
/// numbers on an event. That looks like the rule being broken until you notice
/// which assembly it happens in; here is exactly where it belongs.
///
/// The verbs are not free choices. <em>gathered, carried, wasted, stranded,
/// delivered, eaten, produced N cycles, built, recruited, improved, searched,
/// revealed, offered and unsold, short of a cargo hold</em> are the words the
/// soak harness and the formula documents already use for these same facts.
/// </remarks>
public sealed class TurnReportRenderer
{
    private readonly WorldDefinition _world;
    private readonly WorldState _state;
    private readonly WorldContentCatalog _catalog;

    private TurnReportRenderer(WorldDefinition world, WorldState state, WorldContentCatalog catalog)
    {
        _world = world;
        _state = state;
        _catalog = catalog;
    }

    public static TurnReportRenderer Create(
        CompiledWorldPackage package,
        string scenarioKey,
        WorldState state)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentException.ThrowIfNullOrWhiteSpace(scenarioKey);
        ArgumentNullException.ThrowIfNull(state);
        var world = package.GetWorld(scenarioKey);
        if (!ReferenceEquals(world, state.Definition))
        {
            throw new ArgumentException(
                "The runtime state must belong to the selected package scenario.",
                nameof(state));
        }

        return new TurnReportRenderer(world, state, package.Catalog);
    }

    /// <summary>
    /// The sentences one event produces: none for a phase marker, usually one,
    /// and several where an event carries stories that do not belong together —
    /// what a harvest gathered is a different fact from what it stranded.
    /// </summary>
    public IReadOnlyList<TurnReportLine> Render(TurnEvent turnEvent)
    {
        ArgumentNullException.ThrowIfNull(turnEvent);
        return turnEvent switch
        {
            TurnPhaseCompletedEvent => [],
            WarDeclaredEvent declared => [WarDeclared(declared)],
            WarDeclarationRefusedEvent refused => [WarRefused(refused)],
            DiplomaticOvertureAcceptedEvent accepted => [OvertureAccepted(accepted)],
            DiplomaticOvertureRefusedEvent refused => [OvertureRefused(refused)],
            DiplomaticTiesCutEvent cut => [TiesCut(cut)],
            TradePolicySetEvent set => [PolicySet(set)],
            TradePolicyRefusedEvent refused => [PolicyRefused(refused)],
            TradePolicyClearedEvent cleared => [PolicyCleared(cleared)],
            TaskForceAssembledEvent assembled => [Assembled(assembled)],
            TaskForcePatrolOrderedEvent patrol => [Patrolling(patrol)],
            TaskForceBlockadeOrderedEvent blockade => [Blockading(blockade)],
            NavalBattleFoughtEvent battle => [NavalBattle(battle)],
            MerchantInterceptedEvent intercepted => [Intercepted(intercepted)],
            TaskForceSailedEvent sailed => [Sailed(sailed)],
            NavalOrderRefusedEvent refused => [NavalRefused(refused)],
            CommodityTradedEvent traded => [Traded(traded)],
            TradeUnfilledEvent unfilled => [Unfilled(unfilled)],
            WorldPriceChangedEvent price => [PriceMoved(price)],
            ProductionCompletedEvent produced => [Produced(produced)],
            WorkersTrainedEvent trained => [Trained(trained)],
            WorkerTrainingRefusedEvent refused => [TrainingRefused(refused)],
            CivilianDisbandedEvent disbanded => [Disbanded(disbanded)],
            CivilianDisbandRefusedEvent refused => [DisbandRefused(refused)],
            CiviliansPurchasedEvent bought => [CiviliansBought(bought)],
            CivilianPurchaseRefusedEvent refused => [CivilianPurchaseRefused(refused)],
            CiviliansDeliveredEvent delivered => [CiviliansDelivered(delivered)],
            CivilianDeliveryLostEvent lost => [CivilianDeliveryLost(lost)],
            RegimentsPurchasedEvent bought => [RegimentsBought(bought)],
            RegimentPurchaseRefusedEvent refused => [RegimentPurchaseRefused(refused)],
            RegimentsDeliveredEvent delivered => [RegimentsDelivered(delivered)],
            RegimentDeliveryLostEvent lost => [RegimentDeliveryLost(lost)],
            RegimentsMovedEvent moved => [RegimentsMoved(moved)],
            RegimentMoveRefusedEvent refused => [RegimentMoveRefused(refused)],
            BattleFoughtEvent battle => [BattleFought(battle)],
            ProvinceCapturedEvent captured => [ProvinceCaptured(captured)],
            RegimentsRetreatedEvent retreated => [RegimentsRetreated(retreated)],
            RegimentsSurrenderedEvent surrendered => [RegimentsSurrendered(surrendered)],
            RegimentsHealedEvent healed => [RegimentsHealed(healed)],
            PowerGeneratedEvent generated => Powered(generated),
            PowerRefusedEvent refused => [PowerRefused(refused)],
            FacilityExpandedEvent expanded => [Expanded(expanded)],
            FacilityExpansionRefusedEvent refused => [ExpansionRefused(refused)],
            ShipsBuiltEvent built => [ShipsBuilt(built)],
            ShipBuildRefusedEvent refused => [ShipBuildRefused(refused)],
            TransportCapacityBuiltEvent built => [NetworkBuilt(built)],
            CellDevelopedEvent developed => [Developed(developed)],
            CellProspectedEvent prospected => [Prospected(prospected)],
            CivilianWorkBegunEvent begun => [WorkBegun(begun)],
            ConstructionBegunEvent begun => [ConstructionBegun(begun)],
            ConstructionCompletedEvent done => [ConstructionDone(done)],
            CivilianDeployedEvent deployed => [Deployed(deployed)],
            CivilianOrderRefusedEvent refused => [OrderRefused(refused)],
            WorkersRecruitedEvent recruited => [Recruited(recruited)],
            ResourceExtractedEvent extracted => Extracted(extracted),
            CommoditiesTransportedEvent transported => Transported(transported),
            WorkersFedEvent fed => Fed(fed),
            CommodityDeliveredEvent delivered => [Delivered(delivered)],
            TechnologyPurchasedEvent bought => [TechnologyBought(bought)],
            TechnologyPurchaseRefusedEvent refused => [TechnologyRefused(refused)],
            _ => throw new InvalidOperationException(
                $"No words exist for a {turnEvent.GetType().Name}."),
        };
    }

    // ---- Diplomacy and fleets -------------------------------------------

    private TurnReportLine WarDeclared(WarDeclaredEvent declared) => Line(
        declared,
        TurnReportKind.Outcome,
        declared.Country,
        null,
        $"{Country(declared.Country)} declared war on {Country(declared.Target)}; " +
        "the war begins next turn.");

    private TurnReportLine WarRefused(WarDeclarationRefusedEvent refused) => Line(
        refused,
        TurnReportKind.Refusal,
        refused.Country,
        null,
        $"{Country(refused.Country)} could not declare war on {Country(refused.Target)}: " +
        $"{TurnReportText.Describe(refused.Reason)}.");

    private TurnReportLine OvertureAccepted(DiplomaticOvertureAcceptedEvent accepted)
    {
        var who = Country(accepted.Country);
        var whom = Country(accepted.Target);
        var cost = accepted.CashCost == 0 ? string.Empty : $" for {Money(accepted.CashCost)}";
        var text = accepted.Kind switch
        {
            DiplomaticOvertureKind.Consulate => $"{who} established a trade consulate in {whom}{cost}; it opens next turn.",
            DiplomaticOvertureKind.Embassy => $"{who} established an embassy in {whom}{cost}; it opens next turn.",
            DiplomaticOvertureKind.Pact => $"{whom} accepted a non-aggression pact with {who}; it takes effect next turn.",
            _ => $"{who} made peace with {whom}; the war ends next turn.",
        };
        return Line(accepted, TurnReportKind.Outcome, accepted.Country, null, text);
    }

    private TurnReportLine OvertureRefused(DiplomaticOvertureRefusedEvent refused) => Line(
        refused,
        TurnReportKind.Refusal,
        refused.Country,
        null,
        $"{Country(refused.Country)} could not {TurnReportText.Describe(refused.Kind)} " +
        $"{Country(refused.Target)}: {TurnReportText.Describe(refused.Reason)}.");

    private TurnReportLine TiesCut(DiplomaticTiesCutEvent cut)
    {
        var lost = (cut.PactBroken, cut.PostsClosed) switch
        {
            (true, true) => "broke its non-aggression pact and closed both countries' embassies and consulates",
            (true, false) => "broke the non-aggression pact between them",
            _ => "closed both countries' embassies and consulates; they must be re-established after peace",
        };
        return Line(
            cut,
            TurnReportKind.Outcome,
            cut.Country,
            null,
            $"{Country(cut.Country)}'s war on {Country(cut.Target)} {lost}.");
    }

    private TurnReportLine PolicySet(TradePolicySetEvent set)
    {
        var who = Country(set.Country);
        var whom = Country(set.Target);
        var text = set.Policy.Kind switch
        {
            TradePolicyKind.Subsidy => $"{who} granted {whom} a {set.Policy.Percent}% trade subsidy; it takes effect next turn.",
            TradePolicyKind.Boycott => $"{who} declared a trade boycott of {whom}; it takes effect next turn.",
            _ => $"{who} cancelled its trade policy toward {whom}; ordinary trade resumes next turn.",
        };
        return Line(set, TurnReportKind.Outcome, set.Country, null, text);
    }

    private TurnReportLine PolicyRefused(TradePolicyRefusedEvent refused) => Line(
        refused,
        TurnReportKind.Refusal,
        refused.Country,
        null,
        $"{Country(refused.Country)} could not {TurnReportText.Describe(refused.Policy)} " +
        $"{Country(refused.Target)}: {TurnReportText.Describe(refused.Reason)}.");

    private TurnReportLine PolicyCleared(TradePolicyClearedEvent cleared) => Line(
        cleared,
        TurnReportKind.Outcome,
        cleared.Country,
        null,
        $"With its posts in {Country(cleared.Target)} closed by war, {Country(cleared.Country)}'s " +
        $"{TurnReportText.Name(cleared.Previous)} toward them has lapsed.");

    private TurnReportLine Assembled(TaskForceAssembledEvent assembled) => Line(
        assembled,
        TurnReportKind.Outcome,
        assembled.Country,
        null,
        $"{Country(assembled.Country)} assembled task force {assembled.TaskForce.Value} " +
        $"from {Join(assembled.Fleets.Select(Fleet))} in {SeaZone(assembled.SeaZone)}.");

    private TurnReportLine Patrolling(TaskForcePatrolOrderedEvent patrol) => Line(
        patrol,
        TurnReportKind.Outcome,
        patrol.Country,
        null,
        $"{Country(patrol.Country)}'s task force {patrol.TaskForce.Value} is patrolling " +
        $"{SeaZone(patrol.SeaZone)}.");

    private TurnReportLine Blockading(TaskForceBlockadeOrderedEvent blockade) => Line(
        blockade,
        TurnReportKind.Outcome,
        blockade.Country,
        null,
        $"{Country(blockade.Country)}'s task force {blockade.TaskForce.Value} is blockading " +
        $"{Country(blockade.Target)}'s capital from {SeaZone(blockade.SeaZone)}.");

    private TurnReportLine NavalBattle(NavalBattleFoughtEvent battle)
    {
        var loser = battle.Winner == battle.Attacker ? battle.Defender : battle.Attacker;
        var sunk = battle.Participants.Where(static ship => ship.ShipsSunk > 0).ToArray();
        var losses = sunk.Length == 0
            ? "no ship was sunk"
            : $"{Join(sunk.Select(ship => $"{Country(Owner(battle, ship.Side))} lost {Hulls(ship.ShipsSunk, ship.Type)}"))}";
        var opening = battle.Kind == NavalBattleKind.Escort
            ? $"{Country(battle.Attacker)}'s raiders met {Country(battle.Defender)}'s merchant escort"
            : $"{Country(battle.Attacker)}'s patrol engaged {Country(battle.Defender)}'s ships in {SeaZone(battle.SeaZone)}";
        return Line(
            battle,
            TurnReportKind.Outcome,
            battle.Attacker,
            null,
            $"{opening}; {Country(battle.Winner)} beat {Country(loser)} after {Count(battle.Rounds)} " +
            $"{(battle.Rounds == 1 ? "round" : "rounds")} and {losses}.");
    }

    private static CountryId Owner(NavalBattleFoughtEvent battle, BattleSide side) =>
        side == BattleSide.Attacker ? battle.Attacker : battle.Defender;

    private TurnReportLine Intercepted(MerchantInterceptedEvent intercepted)
    {
        var by = intercepted.Mission == TaskForceActivity.Blockading ? "blockade" : "patrol";
        var cargo = $"{Count(intercepted.Quantity)} {Commodity(intercepted.Commodity)}";
        var what = intercepted.Outcome switch
        {
            InterceptionOutcome.TurnedBack =>
                $"turned back; the cargo returns to {Country(intercepted.Seller)}",
            InterceptionOutcome.Captured =>
                $"seized, and {Country(intercepted.Interceptor)} takes the cargo",
            InterceptionOutcome.Sunk => intercepted.HullSunk is { } hull
                ? $"sunk with its cargo, at the cost of a {Ship(hull)}"
                : "sunk with its cargo",
            _ => throw new ArgumentOutOfRangeException(nameof(intercepted)),
        };
        return Line(
            intercepted,
            TurnReportKind.Loss,
            intercepted.Carrier,
            null,
            $"{Country(intercepted.Interceptor)}'s {by} (task force {intercepted.TaskForce.Value}) intercepted " +
            $"{Country(intercepted.Carrier)}'s merchant carrying {cargo} from {Country(intercepted.Seller)} " +
            $"to {Country(intercepted.Buyer)}: it was {what}. The money already paid stands.");
    }

    private TurnReportLine Sailed(TaskForceSailedEvent sailed) => Line(
        sailed,
        sailed.To == sailed.RequestedDestination ? TurnReportKind.Outcome : TurnReportKind.Shortfall,
        sailed.Country,
        null,
        sailed.From == sailed.To
            ? $"{Country(sailed.Country)}'s task force {sailed.TaskForce.Value} could not make " +
              $"way towards {SeaZone(sailed.RequestedDestination)} and stayed in {SeaZone(sailed.From)}."
            : sailed.To == sailed.RequestedDestination
                ? $"{Country(sailed.Country)}'s task force {sailed.TaskForce.Value} sailed from " +
                  $"{SeaZone(sailed.From)} to {SeaZone(sailed.To)}."
                : $"{Country(sailed.Country)}'s task force {sailed.TaskForce.Value} sailed from " +
                  $"{SeaZone(sailed.From)} as far as {SeaZone(sailed.To)}, " +
                  $"bound for {SeaZone(sailed.RequestedDestination)}.");

    private TurnReportLine NavalRefused(NavalOrderRefusedEvent refused) => Line(
        refused,
        TurnReportKind.Refusal,
        refused.Country,
        null,
        refused.TaskForce is { } taskForce
            ? $"{Country(refused.Country)} could not command task force {taskForce.Value}: " +
              $"{TurnReportText.Describe(refused.Reason)}."
            : $"{Country(refused.Country)} could not assemble a task force: " +
              $"{TurnReportText.Describe(refused.Reason)}.");

    // ---- Trade -----------------------------------------------------------

    private TurnReportLine Traded(CommodityTradedEvent traded)
    {
        // A subsidy moves only the deal's total, so say so rather than print a unit price
        // that does not multiply out.
        var price = traded.Total == traded.Quantity * traded.UnitPrice
            ? $"at {Money(traded.UnitPrice)} a unit, {Money(traded.Total)} in all"
            : $"for {Money(traded.Total)} in all at a subsidised price (world price {Money(traded.UnitPrice)} a unit)";
        return Line(
            traded,
            TurnReportKind.Outcome,
            traded.Seller,
            null,
            $"{Country(traded.Seller)} sold {Count(traded.Quantity)} {Commodity(traded.Commodity)} " +
            $"to {Country(traded.Buyer)} {price}; {Country(traded.HoldsPaidBy)} paid the cargo holds.");
    }

    private TurnReportLine Unfilled(TradeUnfilledEvent unfilled) => Line(
        unfilled,
        TurnReportKind.Shortfall,
        unfilled.Country,
        null,
        $"{Country(unfilled.Country)} settled {Count(unfilled.Settled)} of " +
        $"{Count(unfilled.Requested)} {Commodity(unfilled.Commodity)}: " +
        $"{TurnReportText.Describe(unfilled.Reason)}.");

    private TurnReportLine PriceMoved(WorldPriceChangedEvent price) => Line(
        price,
        TurnReportKind.Outcome,
        null,
        null,
        $"{Commodity(price.Commodity)} moved from {Money(price.FromPrice)} to {Money(price.ToPrice)} " +
        $"on {Count(price.Offered)} offered against {Count(price.Bid)} bid.");

    // ---- Production and construction --------------------------------------

    private TurnReportLine Produced(ProductionCompletedEvent produced)
    {
        var recipe = Recipe(produced.Recipe);
        if (produced.CompletedCycles == 0)
        {
            return Line(
                produced,
                TurnReportKind.Shortfall,
                produced.Country,
                null,
                $"{Country(produced.Country)} produced no {recipe}; " +
                $"{Count(produced.RequestedCycles)} cycles were asked for.");
        }

        var shortfall = produced.CompletedCycles < produced.RequestedCycles;
        var cycles = shortfall
            ? $"{Count(produced.CompletedCycles)} of {Count(produced.RequestedCycles)} cycles"
            : $"{Count(produced.CompletedCycles)} cycles";
        return Line(
            produced,
            shortfall ? TurnReportKind.Shortfall : TurnReportKind.Outcome,
            produced.Country,
            null,
            $"{Country(produced.Country)} produced {cycles} of {recipe}, " +
            $"making {Quantities(produced.Produced)} from {Quantities(produced.Consumed)} " +
            $"and {Count(produced.LabourUsed)} labour.");
    }

    private TurnReportLine Trained(WorkersTrainedEvent trained)
    {
        var cost = new List<string>();
        if (trained.Paid.Count > 0)
        {
            cost.Add(Quantities(trained.Paid));
        }

        if (trained.CashPaid > 0)
        {
            cost.Add(Money(trained.CashPaid));
        }

        var paying = cost.Count == 0 ? string.Empty : $", paying {string.Join(" and ", cost)}";
        return Line(
            trained,
            TurnReportKind.Outcome,
            trained.Country,
            null,
            $"{Country(trained.Country)} trained {Workers(trained.Trained, trained.From)} " +
            $"to {TurnReportText.Describe(trained.To)}{paying}; they work at their new grade from next turn.");
    }

    private TurnReportLine TrainingRefused(WorkerTrainingRefusedEvent refused) => Line(
        refused,
        TurnReportKind.Refusal,
        refused.Country,
        null,
        $"{Country(refused.Country)} could not train {Workers(refused.Requested, refused.From)}: " +
        $"{TurnReportText.Describe(refused.Reason)}.");

    // ---- The University ---------------------------------------------------

    private TurnReportLine CiviliansBought(CiviliansPurchasedEvent bought)
    {
        var cost = new List<string> { Workers(bought.ExpertsTaken, WorkerGrade.Expert) };
        if (bought.Paid.Count > 0)
        {
            cost.Add(Quantities(bought.Paid));
        }

        if (bought.CashPaid > 0)
        {
            cost.Add(Money(bought.CashPaid));
        }

        return Line(
            bought,
            TurnReportKind.Outcome,
            bought.Country,
            null,
            $"{Country(bought.Country)} bought {Civilians(bought.Count, bought.Type)} at the University " +
            $"for {string.Join(" and ", cost)}; the experts leave the workforce for good.");
    }

    private TurnReportLine CivilianPurchaseRefused(CivilianPurchaseRefusedEvent refused) => Line(
        refused,
        TurnReportKind.Refusal,
        refused.Country,
        null,
        $"{Country(refused.Country)} could not buy {Civilians(refused.Requested, refused.Type)}: " +
        $"{TurnReportText.Describe(refused.Reason)}.");

    private TurnReportLine CiviliansDelivered(CiviliansDeliveredEvent delivered) => Line(
        delivered,
        TurnReportKind.Outcome,
        delivered.Country,
        delivered.Cell,
        $"{Country(delivered.Country)}'s new {Civilians(delivered.Units.Count, delivered.Type)} " +
        $"arrived at {Cell(delivered.Cell)} and {(delivered.Units.Count == 1 ? "takes" : "take")} orders from next turn.");

    private TurnReportLine CivilianDeliveryLost(CivilianDeliveryLostEvent lost) => Line(
        lost,
        TurnReportKind.Loss,
        lost.Country,
        null,
        $"{Country(lost.Country)} lost the {Civilians(lost.Count, lost.Type)} it paid for, " +
        "because it holds no capital to receive them.");

    private TurnReportLine Disbanded(CivilianDisbandedEvent disbanded) => Line(
        disbanded,
        TurnReportKind.Outcome,
        disbanded.Country,
        null,
        $"{Country(disbanded.Country)} disbanded its {CivilianType(disbanded.Type)} " +
        $"(civilian {disbanded.Unit.Value}), returning {Workers(disbanded.ExpertsReturned, WorkerGrade.Expert)} " +
        $"who {(disbanded.ExpertsReturned == 1 ? "works" : "work")} from next turn.");

    private TurnReportLine DisbandRefused(CivilianDisbandRefusedEvent refused) => Line(
        refused,
        TurnReportKind.Refusal,
        refused.Country,
        null,
        $"{Country(refused.Country)} could not disband civilian {refused.Unit.Value}: " +
        $"{TurnReportText.Describe(refused.Reason)}.");

    // ---- The Armoury and deployment ----------------------------------------

    private TurnReportLine RegimentsBought(RegimentsPurchasedEvent bought)
    {
        var cost = new List<string> { Workers(bought.WorkersTaken, bought.WorkerGrade) };
        if (bought.Paid.Count > 0)
        {
            cost.Add(Quantities(bought.Paid));
        }

        if (bought.CashPaid > 0)
        {
            cost.Add(Money(bought.CashPaid));
        }

        return Line(
            bought,
            TurnReportKind.Outcome,
            bought.Country,
            null,
            $"{Country(bought.Country)} raised {Regiments(bought.Count, bought.Type)} at the Armoury " +
            $"for {string.Join(" and ", cost)}; the workers leave industry for good.");
    }

    private TurnReportLine RegimentPurchaseRefused(RegimentPurchaseRefusedEvent refused) => Line(
        refused,
        TurnReportKind.Refusal,
        refused.Country,
        null,
        $"{Country(refused.Country)} could not raise {Count(refused.Requested)} " +
        $"{TurnReportText.Describe(refused.Category)} {(refused.Requested == 1 ? "regiment" : "regiments")}: " +
        $"{TurnReportText.Describe(refused.Reason)}.");

    private TurnReportLine RegimentsDelivered(RegimentsDeliveredEvent delivered) => Line(
        delivered,
        TurnReportKind.Outcome,
        delivered.Country,
        null,
        $"{Country(delivered.Country)}'s new {Regiments(delivered.Regiments.Count, delivered.Type)} " +
        $"mustered in {Province(delivered.Province)} and may move from next turn.");

    private TurnReportLine RegimentDeliveryLost(RegimentDeliveryLostEvent lost) => Line(
        lost,
        TurnReportKind.Loss,
        lost.Country,
        null,
        $"{Country(lost.Country)} lost the {Regiments(lost.Count, lost.Type)} it paid for, " +
        "because it holds no capital to muster them in.");

    private TurnReportLine RegimentsMoved(RegimentsMovedEvent moved)
    {
        var how = moved.Mode == RegimentMoveMode.Rail
            ? $"by rail, using {Count(moved.CapacityUsed)} transport capacity"
            : "on the march";
        var count = moved.Regiments.Count;
        return Line(
            moved,
            TurnReportKind.Outcome,
            moved.Country,
            null,
            $"{Country(moved.Country)} moved {Count(count)} {(count == 1 ? "regiment" : "regiments")} " +
            $"from {Province(moved.From)} to {Province(moved.To)} {how}.");
    }

    private TurnReportLine RegimentMoveRefused(RegimentMoveRefusedEvent refused)
    {
        var count = refused.Regiments.Count;
        return Line(
            refused,
            TurnReportKind.Refusal,
            refused.Country,
            null,
            $"{Country(refused.Country)} could not move {Count(count)} {(count == 1 ? "regiment" : "regiments")} " +
            $"to {Province(refused.Destination)}: {TurnReportText.Describe(refused.Reason)}.");
    }

    private TurnReportLine BattleFought(BattleFoughtEvent battle)
    {
        var loser = battle.Winner == battle.Attacker ? battle.Defender : battle.Attacker;
        var killed = battle.Killed.Count;
        var losses = killed == 0
            ? "no regiment was destroyed"
            : $"{Count(killed)} {(killed == 1 ? "regiment was" : "regiments were")} destroyed";
        return Line(
            battle,
            TurnReportKind.Outcome,
            battle.Attacker,
            null,
            $"{Country(battle.Attacker)} attacked {Country(battle.Defender)} in {Province(battle.Province)}; " +
            $"{Country(battle.Winner)} beat {Country(loser)} after {Count(battle.Rounds)} " +
            $"{(battle.Rounds == 1 ? "round" : "rounds")} and {losses}.");
    }

    private TurnReportLine ProvinceCaptured(ProvinceCapturedEvent captured)
    {
        var from = captured.PreviousOwner is { } previous ? $" from {Country(previous)}" : string.Empty;
        var how = captured.Uncontested ? ", undefended" : string.Empty;
        var civilians = captured.CiviliansKilled.Count;
        var killed = civilians == 0
            ? string.Empty
            : $"; {Count(civilians)} {(civilians == 1 ? "civilian" : "civilians")} there {(civilians == 1 ? "was" : "were")} killed";
        var capital = captured.CapitalLost
            ? $"; it held {Country(captured.PreviousOwner!.Value)}'s capital, which is lost"
            : string.Empty;
        return Line(
            captured,
            TurnReportKind.Outcome,
            captured.NewOwner,
            null,
            $"{Country(captured.NewOwner)} took {Province(captured.Province)}{from}{how}{killed}{capital}.");
    }

    private TurnReportLine RegimentsRetreated(RegimentsRetreatedEvent retreated)
    {
        var count = retreated.Regiments.Count;
        return Line(
            retreated,
            TurnReportKind.Loss,
            retreated.Country,
            null,
            $"{Country(retreated.Country)} fell back from {Province(retreated.From)} to {Province(retreated.To)} " +
            $"with {Count(count)} {(count == 1 ? "regiment" : "regiments")}.");
    }

    private TurnReportLine RegimentsSurrendered(RegimentsSurrenderedEvent surrendered)
    {
        var count = surrendered.Regiments.Count;
        return Line(
            surrendered,
            TurnReportKind.Loss,
            surrendered.Country,
            null,
            $"{Country(surrendered.Country)} surrendered {Count(count)} {(count == 1 ? "regiment" : "regiments")} " +
            $"in {Province(surrendered.Province)}, with no province of its own to fall back to.");
    }

    private TurnReportLine RegimentsHealed(RegimentsHealedEvent healed)
    {
        var count = healed.Regiments.Count;
        return Line(
            healed,
            TurnReportKind.Outcome,
            healed.Country,
            null,
            $"{Country(healed.Country)}'s {Count(count)} resting {(count == 1 ? "regiment" : "regiments")} " +
            $"recovered {Count(healed.HealthRestored)} health.");
    }

    private string Regiments(long count, ArmyTypeId type) =>
        $"{Count(count)} {(count == 1 ? "regiment" : "regiments")} of {ArmyType(type)}";

    private string ArmyType(ArmyTypeId type) =>
        (uint)type.Value < (uint)_world.ArmyTypes.Count
            ? _world.ArmyTypes[type.Value].Name
            : $"army type {type.Value}";

    private string Province(ProvinceId province) =>
        (uint)province.Value < (uint)_world.Map.Provinces.Count
            ? _world.Map.Provinces[province.Value].Name
            : $"province {province.Value}";

    private string Civilians(long count, CivilianTypeId type) =>
        $"{Count(count)} {CivilianType(type)}{(count == 1 ? string.Empty : "s")}";

    private string CivilianType(CivilianTypeId type) =>
        (uint)type.Value < (uint)_world.CivilianTypes.Count
            ? _world.CivilianTypes[type.Value].Name
            : $"civilian type {type.Value}";

    private IReadOnlyList<TurnReportLine> Powered(PowerGeneratedEvent generated)
    {
        var lines = new List<TurnReportLine>
        {
            Line(
                generated,
                TurnReportKind.Outcome,
                generated.Country,
                null,
                $"{Country(generated.Country)} burned {Count(generated.FuelBurned)} " +
                $"{Commodity(generated.Fuel)} for {Count(generated.PowerMade)} power, " +
                $"and production used {Count(generated.PowerSpent)} of it ahead of its workers."),
        };

        if (generated.PowerWasted > 0)
        {
            lines.Add(Line(
                generated,
                TurnReportKind.Loss,
                generated.Country,
                null,
                $"{Country(generated.Country)} wasted {Count(generated.PowerWasted)} power, " +
                "which cannot be stored."));
        }

        return lines;
    }

    private TurnReportLine PowerRefused(PowerRefusedEvent refused) => Line(
        refused,
        TurnReportKind.Refusal,
        refused.Country,
        null,
        $"{Country(refused.Country)} could not burn {Count(refused.FuelRequested)} fuel: " +
        $"{TurnReportText.Describe(refused.Reason)}.");

    private static string Workers(long count, WorkerGrade grade) =>
        $"{Count(count)} {TurnReportText.Describe(grade)} {(count == 1 ? "worker" : "workers")}";

    private TurnReportLine Expanded(FacilityExpandedEvent expanded) => Line(
        expanded,
        TurnReportKind.Outcome,
        expanded.Country,
        null,
        $"{Country(expanded.Country)} expanded {Facility(expanded.Facility)} from " +
        $"{Count(expanded.FromCapacity)} to {Count(expanded.ToCapacity)}, " +
        $"paying {Quantities(expanded.Paid)}.");

    private TurnReportLine ExpansionRefused(FacilityExpansionRefusedEvent refused) => Line(
        refused,
        TurnReportKind.Refusal,
        refused.Country,
        null,
        $"{Country(refused.Country)} could not expand {Facility(refused.Facility)}: " +
        $"{TurnReportText.Describe(refused.Reason)}.");

    private TurnReportLine ShipsBuilt(ShipsBuiltEvent built) => Line(
        built,
        TurnReportKind.Outcome,
        built.Country,
        null,
        $"{Country(built.Country)} built {Hulls(built.Count, built.Type)}, " +
        $"paying {Quantities(built.Paid)}.");

    private TurnReportLine ShipBuildRefused(ShipBuildRefusedEvent refused) => Line(
        refused,
        TurnReportKind.Refusal,
        refused.Country,
        null,
        $"{Country(refused.Country)} could not build {Hulls(refused.Requested, refused.Type)}: " +
        $"{TurnReportText.Describe(refused.Reason)}.");

    private TurnReportLine NetworkBuilt(TransportCapacityBuiltEvent built) => Line(
        built,
        TurnReportKind.Outcome,
        built.Country,
        null,
        $"{Country(built.Country)} built the network from {Count(built.FromCapacity)} to " +
        $"{Count(built.ToCapacity)} points, spending {Count(built.LabourUsed)} labour " +
        $"and {Quantities(built.Paid)}.");

    // ---- Civilians --------------------------------------------------------

    private TurnReportLine Developed(CellDevelopedEvent developed) => Line(
        developed,
        TurnReportKind.Outcome,
        developed.Country,
        developed.Cell,
        $"{Country(developed.Country)}'s {Civilian(developed.Unit)} improved " +
        $"{Cell(developed.Cell)} from level {developed.FromLevel} to level {developed.ToLevel}.");

    private TurnReportLine Prospected(CellProspectedEvent prospected)
    {
        var found = prospected.Revealed.Count == 0
            ? "and found nothing"
            : $"and revealed {Join(prospected.Revealed.Select(Resource).Distinct(StringComparer.Ordinal))}";
        return Line(
            prospected,
            TurnReportKind.Outcome,
            prospected.Country,
            prospected.Cell,
            $"{Country(prospected.Country)}'s {Civilian(prospected.Unit)} searched " +
            $"{Cell(prospected.Cell)} {found}.");
    }

    private TurnReportLine WorkBegun(CivilianWorkBegunEvent begun)
    {
        var cost = begun.Paid == 0 ? string.Empty : $" for {Money(begun.Paid)}";
        return Line(
            begun,
            TurnReportKind.Outcome,
            begun.Country,
            begun.Cell,
            $"{Country(begun.Country)}'s {Civilian(begun.Unit)} began work on {Cell(begun.Cell)}" +
            $"{cost}, and will finish in {begun.TurnsRequired} turns.");
    }

    private TurnReportLine ConstructionBegun(ConstructionBegunEvent begun) => Line(
        begun,
        TurnReportKind.Outcome,
        begun.Country,
        begun.Target,
        $"{Country(begun.Country)}'s {Civilian(begun.Unit)} began {Structure(begun.Structure)} at " +
        $"{Cell(begun.Target)} for {Money(begun.Paid)}, and will finish in {begun.TurnsRequired} turns.");

    private TurnReportLine ConstructionDone(ConstructionCompletedEvent done) => Line(
        done,
        TurnReportKind.Outcome,
        done.Country,
        done.Target,
        $"{Country(done.Country)}'s {Civilian(done.Unit)} finished {Structure(done.Structure)} at " +
        $"{Cell(done.Target)}.");

    private TurnReportLine Deployed(CivilianDeployedEvent deployed) => Line(
        deployed,
        TurnReportKind.Outcome,
        deployed.Country,
        deployed.To,
        $"{Country(deployed.Country)}'s {Civilian(deployed.Unit)} moved from {Cell(deployed.From)} " +
        $"to {Cell(deployed.To)}.");

    private TurnReportLine OrderRefused(CivilianOrderRefusedEvent refused) => Line(
        refused,
        TurnReportKind.Refusal,
        refused.Country,
        refused.Cell,
        $"{Country(refused.Country)} could not send its {Civilian(refused.Unit)} to " +
        $"{Cell(refused.Cell)}: {TurnReportText.Describe(refused.Reason)}.");

    // ---- The workforce ----------------------------------------------------

    private TurnReportLine Recruited(WorkersRecruitedEvent recruited)
    {
        if (recruited.Recruited == 0 && recruited.Requested == 0)
        {
            return Line(
                recruited,
                TurnReportKind.Outcome,
                recruited.Country,
                null,
                $"{Country(recruited.Country)} asked the Capitol for nobody.");
        }

        if (recruited.Recruited == 0)
        {
            return Line(
                recruited,
                TurnReportKind.Shortfall,
                recruited.Country,
                null,
                $"{Country(recruited.Country)} recruited nobody of the " +
                $"{Count(recruited.Requested)} asked for; the Capitol allows " +
                $"{Count(recruited.SizeLimit)} a turn.");
        }

        var shortfall = recruited.Recruited < recruited.Requested;
        return Line(
            recruited,
            shortfall ? TurnReportKind.Shortfall : TurnReportKind.Outcome,
            recruited.Country,
            null,
            shortfall
                ? $"{Country(recruited.Country)} recruited {Count(recruited.Recruited)} of the " +
                  $"{Count(recruited.Requested)} asked for, paying {Quantities(recruited.Paid)}; " +
                  $"the Capitol allows {Count(recruited.SizeLimit)} a turn."
                : $"{Country(recruited.Country)} recruited {Count(recruited.Recruited)} workers, " +
                  $"paying {Quantities(recruited.Paid)}.");
    }

    private IReadOnlyList<TurnReportLine> Fed(WorkersFedEvent fed)
    {
        var lines = new List<TurnReportLine>
        {
            Line(
                fed,
                TurnReportKind.Outcome,
                fed.Country,
                null,
                $"{Country(fed.Country)} fed {Count(fed.WellFed)} workers on {Quantities(fed.Eaten)}."),
        };

        if (fed.Sick > 0 || fed.Starved > 0)
        {
            lines.Add(Line(
                fed,
                TurnReportKind.Loss,
                fed.Country,
                null,
                $"{Country(fed.Country)} had {Count(fed.Sick)} fall ill and " +
                $"{Count(fed.Starved)} starve."));
        }

        return lines;
    }

    // ---- The harvest, the network, the warehouse --------------------------

    private IReadOnlyList<TurnReportLine> Extracted(ResourceExtractedEvent extracted)
    {
        var from = $"from {Count(extracted.CollectedCellCount)} tiles and " +
            $"{Count(extracted.FishingPortCount)} fishing ports";
        var lines = new List<TurnReportLine>
        {
            Line(
                extracted,
                TurnReportKind.Outcome,
                extracted.Country,
                null,
                extracted.Collected.Count == 0
                    ? $"{Country(extracted.Country)} gathered nothing {from}."
                    : $"{Country(extracted.Country)} gathered {Quantities(extracted.Collected)} {from}."),
        };

        if (extracted.Stranded.Count > 0)
        {
            lines.Add(Line(
                extracted,
                TurnReportKind.Loss,
                extracted.Country,
                null,
                $"{Country(extracted.Country)} stranded {Quantities(extracted.Stranded)} on " +
                $"{Count(extracted.StrandedCellCount)} tiles and " +
                $"{Count(extracted.StrandedPortCount)} ports no route reached."));
        }

        return lines;
    }

    private IReadOnlyList<TurnReportLine> Transported(CommoditiesTransportedEvent transported)
    {
        var carried = transported.Moved.Count == 0
            ? $"{Country(transported.Country)} carried nothing, of " +
              $"{Count(transported.CapacityAvailable)} capacity."
            : $"{Country(transported.Country)} carried {Quantities(transported.Moved)}, using " +
              $"{Count(transported.CapacityUsed)} of {Count(transported.CapacityAvailable)} capacity.";
        var lines = new List<TurnReportLine>
        {
            Line(transported, TurnReportKind.Outcome, transported.Country, null, carried),
        };

        if (transported.Converted.Count > 0)
        {
            lines.Add(Line(
                transported,
                TurnReportKind.Outcome,
                transported.Country,
                null,
                $"{Country(transported.Country)} turned {Quantities(transported.Converted)} " +
                $"into {Money(transported.CashEarned)}."));
        }

        if (transported.Wasted.Count > 0)
        {
            lines.Add(Line(
                transported,
                TurnReportKind.Loss,
                transported.Country,
                null,
                $"{Country(transported.Country)} wasted {Quantities(transported.Wasted)} " +
                $"for want of capacity."));
        }

        return lines;
    }

    private TurnReportLine Delivered(CommodityDeliveredEvent delivered)
    {
        var pending = delivered.Delivery;
        return Line(
            delivered,
            TurnReportKind.Outcome,
            pending.Recipient,
            null,
            $"{Country(pending.Recipient)} took delivery of {Count(pending.Quantity)} " +
            $"{Commodity(pending.Commodity)}, {TurnReportText.Describe(pending.Source)} last turn.");
    }

    // ---- Investment -------------------------------------------------------

    private TurnReportLine TechnologyBought(TechnologyPurchasedEvent bought) => Line(
        bought,
        TurnReportKind.Outcome,
        bought.Country,
        null,
        $"{Country(bought.Country)} bought {Technology(bought.Technology)} for {Money(bought.Paid)}.");

    private TurnReportLine TechnologyRefused(TechnologyPurchaseRefusedEvent refused) => Line(
        refused,
        TurnReportKind.Refusal,
        refused.Country,
        null,
        $"{Country(refused.Country)} could not buy {Technology(refused.Technology)}: " +
        $"{TurnReportText.Describe(refused.Reason)}.");

    // ---- Names and numbers ------------------------------------------------

    private TurnReportLine Line(
        TurnEvent source,
        TurnReportKind kind,
        CountryId? country,
        CellIndex? cell,
        string text) => new(
            source.Phase,
            kind,
            country,
            country.HasValue ? Country(country.Value) : null,
            cell,
            text);

    private string Country(CountryId country) => (uint)country.Value < (uint)_world.Countries.Count
        ? _world.Countries[country.Value].Name
        : $"Country {country.Value}";

    private string Commodity(CommodityId commodity) =>
        (uint)commodity.Value < (uint)_world.Commodities.Count
            ? _world.Commodities[commodity.Value].Name
            : $"Commodity {commodity.Value}";

    private string Recipe(ProductionRecipeId recipe) =>
        (uint)recipe.Value < (uint)_world.ProductionRecipes.Count
            ? _world.ProductionRecipes[recipe.Value].Name
            : $"Recipe {recipe.Value}";

    private string Facility(ProductionFacilityId facility) =>
        (uint)facility.Value < (uint)_world.ProductionFacilities.Count
            ? _world.ProductionFacilities[facility.Value].Name
            : $"Facility {facility.Value}";

    private string Hulls(long count, ShipTypeId type) =>
        $"{Count(count)} {Ship(type)} {(count == 1 ? "hull" : "hulls")}";

    private string Ship(ShipTypeId type) =>
        (uint)type.Value < (uint)_world.ShipTypes.Count
            ? _world.ShipTypes[type.Value].Name
            : $"Ship {type.Value}";

    private string SeaZone(SeaZoneId seaZone) =>
        (uint)seaZone.Value < (uint)_world.Map.SeaZones.Count
            ? _world.Map.SeaZones[seaZone.Value].Name
            : $"Sea zone {seaZone.Value}";

    private string Fleet(FleetId fleet) =>
        _state.TryGetFleet(fleet, out var found)
            ? $"fleet {fleet.Value} ({Hulls(found.Count, found.Type)})"
            : $"fleet {fleet.Value}";

    private string Technology(TechnologyId technology) =>
        (uint)technology.Value < (uint)_world.Technologies.Count
            ? _world.Technologies[technology.Value].Name
            : $"Technology {technology.Value}";

    private static string Structure(EngineerConstruction structure) =>
        TurnReportText.Describe(structure);

    /// <summary>
    /// A deposit named by what it yields, because <c>ResourceDefinition</c> has
    /// no name of its own. The stable key would do — <c>resource.coal</c> — but
    /// this is the first player-facing prose in the project and a dotted
    /// developer string is the wrong precedent to set in it. What a player wants
    /// from a Prospector's report is what the tile will now pay, which is
    /// precisely the commodity.
    /// </summary>
    /// <remarks>
    /// Lossy where two deposits yield the same commodity, so callers rendering a
    /// list of them de-duplicate first. The real fix is a name on the deposit,
    /// which is a content format change and belongs in its own slice.
    /// </remarks>
    private string Resource(ResourceId resource)
    {
        if ((uint)resource.Value >= (uint)_world.Map.Resources.Count)
        {
            return _catalog.ResourceKeys.Count > resource.Value
                ? _catalog.GetKey(resource)
                : $"Deposit {resource.Value}";
        }

        return Commodity(_world.Map.Resources[resource.Value].Commodity);
    }

    /// <summary>
    /// A civilian named by its type, or by its number when it no longer exists.
    /// </summary>
    /// <remarks>
    /// <c>GetCivilian</c> returns null for a unit that has gone, which is exactly
    /// the <c>NoSuchCivilian</c> refusal and is reachable today. The number is
    /// kept in the fallback rather than writing a vague "a civilian", so a report
    /// of something going wrong still identifies which one.
    /// </remarks>
    private string Civilian(CivilianUnitId unit)
    {
        var civilian = _state.GetCivilian(unit);
        if (civilian is null)
        {
            return $"civilian {unit.Value}";
        }

        var type = civilian.Type;
        return (uint)type.Value < (uint)_world.CivilianTypes.Count
            ? _world.CivilianTypes[type.Value].Name
            : $"civilian {unit.Value}";
    }

    /// <summary>
    /// A tile named by its ground and where that ground is.
    /// </summary>
    /// <remarks>
    /// <c>CellRegion.Province</c> throws for a sea zone or an unassigned cell, so
    /// the kind is checked first. Any coastal tile in an event would otherwise
    /// take the whole report down.
    /// </remarks>
    private string Cell(CellIndex cell)
    {
        if ((uint)cell.Value >= (uint)_world.Map.Cells.Count)
        {
            return $"cell {cell.Value}";
        }

        var definition = _world.Map[cell];
        var terrain = (uint)definition.Terrain.Value < (uint)_world.Map.Terrains.Count
            ? _world.Map.Terrains[definition.Terrain.Value].Name
            : $"cell {cell.Value}";
        if (definition.Region.Kind != CellRegionKind.Province)
        {
            return $"{terrain} (cell {cell.Value})";
        }

        var province = definition.Region.Province;
        return (uint)province.Value < (uint)_world.Map.Provinces.Count
            ? $"{terrain} in {_world.Map.Provinces[province.Value].Name}"
            : $"{terrain} (cell {cell.Value})";
    }

    private string Quantities(IReadOnlyList<CommodityQuantity> quantities) => quantities.Count == 0
        ? "nothing"
        : Join(quantities.Select(entry => $"{Count(entry.Quantity)} {Commodity(entry.Commodity)}"));

    private static string Join(IEnumerable<string> parts) =>
        string.Join(", ", parts);

    private static string Count(long amount) => amount.ToString("N0", CultureInfo.InvariantCulture);

    private static string Money(long amount) =>
        "$" + amount.ToString("N0", CultureInfo.InvariantCulture);
}
