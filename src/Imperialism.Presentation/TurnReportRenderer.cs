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
            CommodityTradedEvent traded => [Traded(traded)],
            TradeUnfilledEvent unfilled => [Unfilled(unfilled)],
            WorldPriceChangedEvent price => [PriceMoved(price)],
            ProductionCompletedEvent produced => [Produced(produced)],
            FacilityExpandedEvent expanded => [Expanded(expanded)],
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

    // ---- Trade -----------------------------------------------------------

    private TurnReportLine Traded(CommodityTradedEvent traded) => Line(
        traded,
        TurnReportKind.Outcome,
        traded.Seller,
        null,
        $"{Country(traded.Seller)} sold {Count(traded.Quantity)} {Commodity(traded.Commodity)} " +
        $"to {Country(traded.Buyer)} at {Money(traded.UnitPrice)} a unit, {Money(traded.Total)} in all; " +
        $"{Country(traded.HoldsPaidBy)} paid the cargo holds.");

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

    private TurnReportLine Expanded(FacilityExpandedEvent expanded) => Line(
        expanded,
        TurnReportKind.Outcome,
        expanded.Country,
        null,
        $"{Country(expanded.Country)} expanded {Facility(expanded.Facility)} from " +
        $"{Count(expanded.FromCapacity)} to {Count(expanded.ToCapacity)}, " +
        $"paying {Quantities(expanded.Paid)}.");

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
