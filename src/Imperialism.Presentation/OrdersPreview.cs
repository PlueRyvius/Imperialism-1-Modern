using Imperialism.Content;
using Imperialism.Core;

namespace Imperialism.Presentation;

/// <summary>A commodity and an amount, named for a screen.</summary>
public sealed record CommodityAmountView(string CommodityKey, string CommodityName, long Quantity);

/// <summary>
/// What the orders screens show while a player drafts: every number a screen
/// draws, so a screen draws a view and computes nothing.
/// </summary>
/// <remarks>
/// <b>A preview, not a ruling.</b> Core's planners are internal, so these figures
/// repeat their rules in the order <see cref="TurnResolver"/> applies them —
/// recipes, then facility expansion in the draft's order, then the railyard, then the
/// Capitol, then the market; civilians' cash after the market's bids and before
/// research — and the tests resolve real turns to hold the two together. Core still
/// decides when the turn resolves. The preview never refuses: an over-ask shows as
/// a warning and goes to Core as given, which trims or refuses it and reports why.
/// <para>
/// What a preview cannot know is said, not guessed: transport is trimmed again to
/// what the tiles yield, which Extraction decides mid-turn, and research is paid
/// from what the market leaves.
/// </para>
/// </remarks>
public sealed partial class OrdersPreview
{
    /// <summary>"You may bid on only four commodities each turn" (manual 3107).</summary>
    public const int MaximumBids = 4;

    private OrdersPreview(
        IndustryPreview industry,
        TransportPreview transport,
        TechnologyPreview technology,
        TradePreview trade,
        CivilianOrdersPreview civilians)
    {
        Industry = industry;
        Transport = transport;
        Technology = technology;
        Trade = trade;
        Civilians = civilians;
    }

    public IndustryPreview Industry { get; }

    public TransportPreview Transport { get; }

    public TechnologyPreview Technology { get; }

    public TradePreview Trade { get; }

    /// <summary>The drafting country's civilians and their drafted orders.</summary>
    public CivilianOrdersPreview Civilians { get; }

    /// <summary>
    /// The drafting country is eliminated as the turn opens. Core sets every order it
    /// gives aside before anything is planned and reports one
    /// <see cref="EliminatedCountryOrdersIgnoredEvent"/>, so nothing drafted happens:
    /// military orders say so row by row with <see cref="OrderWarning{TRefusal}.SetAside"/>,
    /// the Diplomacy and civilian previews carry the same flag, and the Industry,
    /// Transport, Technology and Bid/Offers figures are what the draft would do were it
    /// read, which it is not.
    /// </summary>
    public bool OrdersSetAside { get; private init; }

    public static OrdersPreview Create(
        CompiledWorldPackage package,
        string scenarioKey,
        WorldState state,
        OrdersDraft draft)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentException.ThrowIfNullOrWhiteSpace(scenarioKey);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(draft);
        var world = package.GetWorld(scenarioKey);
        if (!ReferenceEquals(world, state.Definition))
        {
            throw new ArgumentException(
                "The runtime state must belong to the selected package scenario.",
                nameof(state));
        }

        var names = new Names(package, world);
        var country = draft.Country;
        var available = new long[world.Commodities.Count];
        for (var index = 0; index < available.Length; index++)
        {
            available[index] = state.GetAvailableQuantity(country, new CommodityId(index));
        }

        // Consumption only, never net of output: what industry produces this turn is
        // not in the warehouse until Production commits, after the market has cleared.
        var claimed = new long[available.Length];
        var armoury = SimulateArmoury(state, draft);
        var industry = PreviewIndustry(state, draft, names, available, claimed, armoury, out var shipyard);
        var transport = PreviewTransport(state, draft, names);
        var diplomacy = PreviewDiplomacy(state, draft, names);
        var civilians = PreviewCivilians(state, draft);
        var technology = PreviewTechnology(
            state, draft, names, diplomacy.ClaimedByOvertures, civilians.CommittedCost) with
        {
            ClaimedByArmoury = armoury.CashClaimed,
        };
        var trade = PreviewTrade(state, draft, names, claimed, diplomacy.ClaimedByOvertures) with
        {
            ClaimedByArmoury = armoury.CashClaimed,
        };
        return new OrdersPreview(industry, transport, technology, trade, civilians)
        {
            Diplomacy = diplomacy,
            Armoury = PreviewArmoury(package, state, draft, names, armoury),
            Shipyard = shipyard,
            ArmyMoves = PreviewArmyMoves(package, state, draft),
            Fleet = PreviewFleet(package, state, draft),
            OrdersSetAside = state.IsEliminated(country),
        };
    }

    private static IndustryPreview PreviewIndustry(
        WorldState state,
        OrdersDraft draft,
        Names names,
        long[] available,
        long[] claimed,
        ArmourySimulation armoury,
        out ShipyardView shipyard)
    {
        var world = state.Definition;
        var country = draft.Country;
        var labourLimited = world.Feeding is not null;
        var labourAvailable = state.GetAvailableLabour(country);
        var labourLeft = labourLimited ? labourAvailable : long.MaxValue;

        // (0) The Armoury, before any recipe: its bill leaves the warehouse and its
        // workers' labour the pool, as in Core's Production planning.
        for (var index = 0; index < available.Length; index++)
        {
            available[index] -= armoury.Bill[index];
            claimed[index] += armoury.Bill[index];
        }

        if (labourLimited)
        {
            labourLeft -= armoury.LabourWithheld;
        }
        var capacityLeft = world.ProductionFacilities
            .Select(facility => facility.CapacityMode == ProductionCapacityMode.Unlimited
                ? long.MaxValue
                : state.GetProductionCapacity(country, facility.Id) ?? 0)
            .ToArray();
        var capacityCommitted = new long[capacityLeft.Length];

        // (1) Recipes, in the player's order, each limited by what the earlier rows
        // left of the facility, the labour pool and the warehouse.
        var recipes = new List<RecipePreview>();
        var produced = new long[available.Length];
        foreach (var (recipeId, requested) in draft.Production)
        {
            var recipe = world.ProductionRecipes[recipeId.Value];
            var facility = world.ProductionFacilities[recipe.Facility.Value];
            var maximum = long.MaxValue;
            var limitedBy = RecipeLimit.None;
            void Limit(long value, RecipeLimit reason)
            {
                if (value < maximum)
                {
                    maximum = value;
                    limitedBy = reason;
                }
            }

            if (facility.CapacityMode == ProductionCapacityMode.Limited)
            {
                Limit(capacityLeft[facility.Id.Value] / recipe.CapacityCost, RecipeLimit.FacilityCapacity);
            }

            if (labourLimited)
            {
                Limit(Math.Max(0, labourLeft) / recipe.LabourCost, RecipeLimit.Labour);
            }

            foreach (var input in recipe.Inputs)
            {
                Limit(available[input.Commodity.Value] / input.Quantity, RecipeLimit.Warehouse);
            }

            var expected = Math.Min(requested, maximum);
            capacityCommitted[facility.Id.Value] += expected * recipe.CapacityCost;
            if (facility.CapacityMode == ProductionCapacityMode.Limited)
            {
                capacityLeft[facility.Id.Value] -= expected * recipe.CapacityCost;
            }

            if (labourLimited)
            {
                labourLeft -= expected * recipe.LabourCost;
            }

            foreach (var input in recipe.Inputs)
            {
                available[input.Commodity.Value] -= expected * input.Quantity;
                claimed[input.Commodity.Value] += expected * input.Quantity;
            }

            // Not added to `available` yet: a later recipe cannot use it this turn.
            foreach (var output in recipe.Outputs)
            {
                produced[output.Commodity.Value] += expected * output.Quantity;
            }

            recipes.Add(new RecipePreview(
                names.Key(recipe.Id),
                recipe.Name,
                names.Key(facility.Id),
                names.Amounts(recipe.Inputs),
                names.Amounts(recipe.Outputs),
                recipe.LabourCost,
                requested,
                maximum == long.MaxValue ? null : maximum,
                expected,
                expected < requested ? limitedBy : RecipeLimit.None,
                facility.CapacityMode == ProductionCapacityMode.Unlimited));
        }

        // What the recipes make is in the warehouse for everything built after them:
        // Core plans expansion, the railyard and the Capitol against production's net
        // deltas, outputs included. Never `claimed` — the market cannot sell it.
        for (var index = 0; index < available.Length; index++)
        {
            available[index] += produced[index];
        }

        // (2) Facility expansion, from what the recipes left of the warehouse, in the
        // draft's order: that is the order Core charges them, so when materials cover
        // only one building the first listed gets it, whatever its catalogue position.
        var outcomes = new Dictionary<ProductionFacilityId, ExpansionOutcome>();
        foreach (var facilityId in draft.Expansions)
        {
            var facility = world.ProductionFacilities[facilityId.Value];
            if (facility.CapacityLadder is not { } ladder || world.ExpansionCostPerCapacityPoint.Count == 0)
            {
                outcomes[facilityId] = ExpansionOutcome.CannotExpand;
                continue;
            }

            if (facility.RequiredTechnology is { } required && !state.HasTechnology(country, required))
            {
                outcomes[facilityId] = ExpansionOutcome.TechnologyNotKnown;
                continue;
            }

            var bill = ExpansionBill(world, ladder, state.GetProductionCapacity(country, facilityId) ?? 0);
            if (bill.Any(item => available[item.Commodity.Value] < item.Quantity))
            {
                outcomes[facilityId] = ExpansionOutcome.NotEnoughMaterials;
                continue;
            }

            outcomes[facilityId] = ExpansionOutcome.Expands;
            foreach (var item in bill)
            {
                available[item.Commodity.Value] -= item.Quantity;
                claimed[item.Commodity.Value] += item.Quantity;
            }
        }

        var facilities = new List<FacilityPreview>();
        foreach (var facility in world.ProductionFacilities)
        {
            var capacity = facility.CapacityMode == ProductionCapacityMode.Unlimited
                ? (long?)null
                : state.GetProductionCapacity(country, facility.Id) ?? 0;
            var gated = facility.RequiredTechnology is { } required && !state.HasTechnology(country, required);
            long? next = null;
            IReadOnlyList<CommodityAmountView> cost = [];
            var ordered = outcomes.TryGetValue(facility.Id, out var expansion);
            if (facility.CapacityLadder is { } ladder && world.ExpansionCostPerCapacityPoint.Count > 0)
            {
                var from = capacity ?? 0;
                next = ladder.NextAbove(from);
                cost = names.Amounts(ExpansionBill(world, ladder, from));
            }

            facilities.Add(new FacilityPreview(
                names.Key(facility.Id),
                facility.Name,
                capacity,
                capacityCommitted[facility.Id.Value],
                next,
                cost,
                facility.RequiredTechnology is { } technology ? names.Key(technology) : null,
                gated,
                ordered,
                expansion,
                facility.RequiredTechnology is { } namedTechnology
                    ? world.Technologies[namedTechnology.Value].Name : null));
        }

        // (3) The railyard: the warehouse (output included) after expansion, the labour
        // after recipes.
        RailyardPreview? railyard = null;
        if (world.Transport is { } transport && transport.CostPerCapacityPoint.Count > 0)
        {
            var wanted = draft.BuildTransportCapacity;
            var points = wanted;
            foreach (var item in transport.CostPerCapacityPoint)
            {
                points = Math.Min(points, available[item.Commodity.Value] / item.Quantity);
            }

            var labourPerPoint = labourLimited ? transport.LabourPerCapacityPoint : 0;
            if (labourPerPoint > 0)
            {
                points = Math.Min(points, Math.Max(0, labourLeft) / labourPerPoint);
            }

            points = Math.Max(0, points);
            foreach (var item in transport.CostPerCapacityPoint)
            {
                available[item.Commodity.Value] -= item.Quantity * points;
                claimed[item.Commodity.Value] += item.Quantity * points;
            }

            if (labourLimited)
            {
                labourLeft -= points * labourPerPoint;
            }

            railyard = new RailyardPreview(
                names.Amounts(transport.CostPerCapacityPoint), labourPerPoint, wanted, points);
        }

        // (3b) The Shipyard: after the railyard and before the Capitol, from a warehouse
        // holding this turn's output. Ships take no labour.
        shipyard = PreviewShipyard(state, draft, names, available, claimed);

        // (4) The Capitol, last, from what everything above left.
        RecruitmentPreview? recruitment = null;
        if (world.Migration is { } migration)
        {
            var owned = 0;
            for (var province = 0; province < world.Map.Provinces.Count; province++)
            {
                if (state.GetProvinceOwner(new ProvinceId(province)) == country)
                {
                    owned++;
                }
            }

            var cap = owned / migration.ProvincesPerRecruit;
            var recruited = Math.Min(draft.RecruitWorkers, cap);
            foreach (var item in migration.CostPerWorker)
            {
                recruited = Math.Min(recruited, available[item.Commodity.Value] / item.Quantity);
            }

            recruited = Math.Max(0, recruited);
            foreach (var item in migration.CostPerWorker)
            {
                available[item.Commodity.Value] -= item.Quantity * recruited;
                claimed[item.Commodity.Value] += item.Quantity * recruited;
            }

            recruitment = new RecruitmentPreview(
                cap, names.Amounts(migration.CostPerWorker), draft.RecruitWorkers, recruited);
        }

        var committedLabour = labourLimited ? labourAvailable - labourLeft : 0;
        return new IndustryPreview(
            labourLimited,
            labourAvailable,
            committedLabour,
            Array.AsReadOnly(recipes.ToArray()),
            Array.AsReadOnly(facilities.ToArray()),
            railyard,
            recruitment);
    }

    private static TransportPreview PreviewTransport(WorldState state, OrdersDraft draft, Names names)
    {
        var unlimited = state.Definition.Transport is null;
        var capacity = state.GetTransportCapacity(draft.Country);
        var remaining = unlimited ? long.MaxValue : capacity;
        var sliders = new List<TransportSliderPreview>();
        foreach (var (commodity, requested) in draft.Transport)
        {
            var committed = Math.Min(requested, remaining);
            if (!unlimited)
            {
                remaining -= committed;
            }

            sliders.Add(new TransportSliderPreview(
                names.Key(commodity),
                state.Definition.Commodities[commodity.Value].Name,
                requested,
                committed));
        }

        return new TransportPreview(
            unlimited,
            unlimited ? null : capacity,
            sliders.Sum(static slider => slider.Committed),
            Array.AsReadOnly(sliders.ToArray()));
    }

    private static TechnologyPreview PreviewTechnology(
        WorldState state, OrdersDraft draft, Names names, long claimedByOvertures, long claimedByCivilians)
    {
        var world = state.Definition;
        var country = draft.Country;
        var year = state.CurrentYear;
        var cash = state.GetCash(country);
        var offered = new List<TechnologyOfferPreview>();
        foreach (var technology in world.Technologies)
        {
            if (technology.Cost is not { } price ||
                state.HasTechnology(country, technology.Id) ||
                technology.AvailableFrom is { } arrival && year < arrival ||
                technology.Prerequisites.Any(required => !state.HasTechnology(country, required)))
            {
                continue;
            }

            offered.Add(new TechnologyOfferPreview(
                names.Key(technology.Id),
                technology.Name,
                price,
                draft.BuyTechnology.Contains(technology.Id)));
        }

        // In the draft's order, as Investment spends: an unaffordable entry is
        // skipped and a cheaper one after it may still be bought. Development pays
        // for civilians' work before Investment runs, so their drafted cost comes off
        // first, and Diplomacy pays for overtures before either.
        var left = Math.Max(0, cash - ClaimedByArmoury(state, draft) - claimedByOvertures - claimedByCivilians);
        var committed = 0L;
        var purchases = new List<TechnologyPurchasePreview>();
        foreach (var id in draft.BuyTechnology)
        {
            var offer = offered.FirstOrDefault(row => row.TechnologyKey == names.Key(id));
            var affordable = offer is not null && offer.Cost <= left;
            if (affordable)
            {
                left -= offer!.Cost;
            }

            committed += offer?.Cost ?? 0;
            purchases.Add(new TechnologyPurchasePreview(
                names.Key(id), world.Technologies[id.Value].Name, offer is not null, affordable));
        }

        return new TechnologyPreview(
            cash,
            claimedByOvertures,
            claimedByCivilians,
            committed,
            Array.AsReadOnly(offered.ToArray()),
            Array.AsReadOnly(purchases.ToArray()));
    }

    private static TradePreview PreviewTrade(
        WorldState state,
        OrdersDraft draft,
        Names names,
        long[] claimed,
        long claimedByOvertures)
    {
        var world = state.Definition;
        var country = draft.Country;
        var rows = new List<TradeRowPreview>();
        foreach (var commodity in world.Commodities
                     .Where(static commodity => commodity.TradeOrder is not null)
                     .OrderBy(static commodity => commodity.TradeOrder!.Value))
        {
            var stock = state.GetAvailableQuantity(country, commodity.Id);
            var price = state.GetWorldPrice(commodity.Id);
            var offer = draft.TradeOffers.FirstOrDefault(row => row.Commodity == commodity.Id).Quantity;
            var bid = draft.TradeBids.FirstOrDefault(row => row.Commodity == commodity.Id).Quantity;
            rows.Add(new TradeRowPreview(
                names.Key(commodity.Id),
                commodity.Name,
                price,
                stock,
                claimed[commodity.Id.Value],
                Math.Max(0, stock - claimed[commodity.Id.Value]),
                offer,
                bid,
                bid * price));
        }

        return new TradePreview(
            state.GetMerchantMarine(country),
            state.GetCash(country),
            claimedByOvertures,
            draft.TradeBids.Count(static row => row.Quantity > 0),
            Array.AsReadOnly(rows.ToArray()));
    }

    private static CommodityQuantity[] ExpansionBill(WorldDefinition world, CapacityLadder ladder, long from)
    {
        var points = ladder.NextAbove(from) - from;
        return world.ExpansionCostPerCapacityPoint
            .Select(item => new CommodityQuantity(item.Commodity, item.Quantity * points))
            .ToArray();
    }

    private sealed class Names(CompiledWorldPackage package, WorldDefinition world)
    {
        public string Key(ProductionRecipeId id) => package.Catalog.GetKey(id);

        public string Key(ProductionFacilityId id) => package.Catalog.GetKey(id);

        public string Key(CommodityId id) => package.Catalog.GetKey(id);

        public string Key(TechnologyId id) => package.Catalog.GetKey(id);

        public string Key(CountryId id) => package.Catalog.GetKey(id);

        public IReadOnlyList<CommodityAmountView> Amounts(IEnumerable<CommodityQuantity> quantities) =>
            Array.AsReadOnly(quantities
                .Select(item => new CommodityAmountView(
                    Key(item.Commodity), world.Commodities[item.Commodity.Value].Name, item.Quantity))
                .ToArray());
    }
}

/// <summary>What stopped a recipe short of what was asked, tightest first.</summary>
public enum RecipeLimit : byte
{
    None,
    FacilityCapacity,
    Labour,
    Warehouse,
}

/// <summary>What an expansion order will meet when the turn resolves.</summary>
public enum ExpansionOutcome : byte
{
    NotOrdered,
    Expands,

    /// <summary>Core refuses it, charging nothing (<c>TechnologyNotKnown</c>).</summary>
    TechnologyNotKnown,

    /// <summary>Core skips it silently; the warehouse is short after production.</summary>
    NotEnoughMaterials,

    /// <summary>No ladder or no price in this world, so Core ignores the order.</summary>
    CannotExpand,
}

public sealed record RecipePreview(
    string RecipeKey,
    string RecipeName,
    string FacilityKey,
    IReadOnlyList<CommodityAmountView> Inputs,
    IReadOnlyList<CommodityAmountView> Outputs,
    long LabourPerCycle,
    long RequestedCycles,
    long? MaximumCycles,
    long ExpectedCycles,
    RecipeLimit LimitedBy,
    bool IsNotSaved)
{
    /// <summary>A warning, never a refusal: Core will trim to <see cref="ExpectedCycles"/>.</summary>
    public bool IsOverAsked => ExpectedCycles < RequestedCycles;
}

public sealed record FacilityPreview(
    string FacilityKey,
    string FacilityName,
    long? Capacity,
    long CapacityCommitted,
    long? NextRung,
    IReadOnlyList<CommodityAmountView> ExpansionCost,
    string? RequiredTechnologyKey,
    bool IsGatedByTechnology,
    bool ExpansionOrdered,
    ExpansionOutcome Expansion,
    string? RequiredTechnologyName = null);

public sealed record RailyardPreview(
    IReadOnlyList<CommodityAmountView> CostPerPoint,
    long LabourPerPoint,
    long RequestedPoints,
    long ExpectedPoints)
{
    public bool IsShort => ExpectedPoints < RequestedPoints;
}

public sealed record RecruitmentPreview(
    long Cap,
    IReadOnlyList<CommodityAmountView> PricePerWorker,
    long Requested,
    long ExpectedRecruits)
{
    public bool IsShort => ExpectedRecruits < Requested;
}

public sealed record IndustryPreview(
    bool IsLabourLimited,
    long LabourAvailable,
    long LabourCommitted,
    IReadOnlyList<RecipePreview> Recipes,
    IReadOnlyList<FacilityPreview> Facilities,
    RailyardPreview? Railyard,
    RecruitmentPreview? Recruitment);

public sealed record TransportSliderPreview(
    string CommodityKey,
    string CommodityName,
    long Requested,
    long Committed)
{
    public bool IsShort => Committed < Requested;
}

/// <summary>
/// The shared bar. <see cref="Used"/> is an upper bound: Core trims each slider
/// again to what Extraction gathers, which nothing can know before the turn runs.
/// </summary>
public sealed record TransportPreview(
    bool IsUnlimited,
    long? Capacity,
    long Used,
    IReadOnlyList<TransportSliderPreview> Sliders)
{
    public bool IsZeroCapacity => Capacity == 0;

    public bool IsOverAsked => Sliders.Any(static slider => slider.Committed < slider.Requested);
}

public sealed record TechnologyOfferPreview(
    string TechnologyKey,
    string TechnologyName,
    long Cost,
    bool IsSelected);

/// <summary>
/// One purchase in the draft. <see cref="IsAffordable"/> is against cash as the
/// turn opens less what the drafted overtures and civilians' work claim, since
/// Diplomacy and Development pay before Investment; research is paid last, from what
/// the market leaves.
/// </summary>
public sealed record TechnologyPurchasePreview(
    string TechnologyKey,
    string TechnologyName,
    bool IsOffered,
    bool IsAffordable);

public sealed record TechnologyPreview(
    long Cash,
    long ClaimedByOvertures,
    long ClaimedByCivilians,
    long CommittedCost,
    IReadOnlyList<TechnologyOfferPreview> Offered,
    IReadOnlyList<TechnologyPurchasePreview> Purchases)
{
    /// <summary>Paid at the Armoury in <c>Production</c>, before everything else.</summary>
    public long ClaimedByArmoury { get; init; }

    public bool IsOverCash => CommittedCost > Cash - ClaimedByArmoury - ClaimedByOvertures - ClaimedByCivilians;
}

public sealed record TradeRowPreview(
    string CommodityKey,
    string CommodityName,
    long WorldPrice,
    long Stock,
    long ClaimedByIndustry,
    long Sellable,
    long Offer,
    long Bid,
    long BidCost)
{
    /// <summary>Core sells only <see cref="Sellable"/> and reports the rest unfilled.</summary>
    public bool IsOverOffered => Offer > Sellable;
}

/// <param name="ClaimedByOvertures">
/// Paid in Diplomacy before the market clears, so bids see it gone.
/// </param>
public sealed record TradePreview(
    long MerchantMarine,
    long Cash,
    long ClaimedByOvertures,
    int BidCount,
    IReadOnlyList<TradeRowPreview> Rows)
{
    public long TotalBidCost => Rows.Sum(static row => row.BidCost);

    /// <summary>The manual's four-bid limit (3107). Core does not enforce it yet.</summary>
    public bool IsOverBidLimit => BidCount > OrdersPreview.MaximumBids;

    /// <summary>Paid at the Armoury in <c>Production</c>, before the market clears.</summary>
    public long ClaimedByArmoury { get; init; }

    public bool BidsExceedCash => TotalBidCost > Cash - ClaimedByArmoury - ClaimedByOvertures;
}
