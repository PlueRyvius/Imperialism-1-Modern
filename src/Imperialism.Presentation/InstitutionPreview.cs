using Imperialism.Content;
using Imperialism.Core;

namespace Imperialism.Presentation;

/// <summary>One step of the Trade School: workers of one grade raised to the next.</summary>
/// <param name="To">The grade they reach, or null when the world offers no step from <see cref="From"/>.</param>
/// <param name="CostPerWorker">Paper, or whatever the world charges, for one trainee.</param>
/// <param name="LabourPerWorker">What one trainee would have supplied this turn, and does not.</param>
/// <param name="Healthy">
/// Workers of <see cref="From"/> as the turn opens, less the sick: the only ones Core sends.
/// </param>
/// <param name="Drafted">The drafted count; zero is not sent.</param>
/// <param name="LabourWithheld">The trainees' labour, gone from this turn's pool when accepted.</param>
/// <param name="Warning">
/// Core's refusal of the drafted step, or that Core sets it aside; null when accepted or not drafted.
/// </param>
public sealed record TradeSchoolStepView(
    WorkerGrade From,
    WorkerGrade? To,
    IReadOnlyList<CommodityAmountView> CostPerWorker,
    long CashPerWorker,
    long LabourPerWorker,
    long Healthy,
    long Drafted,
    long LabourWithheld,
    OrderWarning<WorkerTrainingRefusal>? Warning);

/// <summary>
/// The Trade School dialog. Planned first in <c>Production</c>, before the University,
/// the Armoury and any recipe; each step is all or nothing, and a trainee works at the
/// new grade only from next turn.
/// </summary>
public sealed record TradeSchoolView(
    long CashClaimed,
    long LabourWithheld,
    IReadOnlyList<TradeSchoolStepView> Steps);

/// <summary>
/// The Power Plant: fuel burned in <c>Production</c> after the Armoury, its power spent by
/// recipes ahead of any human labour, and whatever they leave lost.
/// </summary>
/// <param name="IsModelled">The world has a power block; when false every other figure is empty.</param>
/// <param name="PlantCapacity">The country's Power Plant as the turn opens; zero is none.</param>
/// <param name="RequiredTechnologyKey">What the plant's expansion needs — Oil Drilling in imported worlds.</param>
/// <param name="IsGatedByTechnology">The country does not know it as the turn opens, so cannot build the plant.</param>
/// <param name="FuelHeld">Fuel in the warehouse once the Trade School, the University and the Armoury have taken theirs.</param>
/// <param name="Drafted">Fuel the draft asks to burn.</param>
/// <param name="PowerMade">Fuel burned × power per fuel, when accepted.</param>
/// <param name="PowerSpent">What the drafted recipes draw, ahead of human labour.</param>
/// <param name="Warning">
/// Core's refusal — an order over <see cref="MaxFuelPerTurn"/> is refused, not trimmed — or that
/// Core sets it aside; null when accepted or not drafted.
/// </param>
public sealed record PowerPlantView(
    bool IsModelled,
    string? FuelKey,
    string? FuelName,
    string? PlantKey,
    long PlantCapacity,
    string? RequiredTechnologyKey,
    bool IsGatedByTechnology,
    long PowerPerFuel,
    long MaxFuelPerTurn,
    long FuelHeld,
    long Drafted,
    long PowerMade,
    long PowerSpent,
    OrderWarning<PowerRefusal>? Warning)
{
    /// <summary>Power nothing drew. It "cannot be sold or stored" (manual 2656), so it is lost.</summary>
    public long PowerLost => PowerMade - PowerSpent;
}

/// <summary>One civilian type at the University.</summary>
/// <param name="IsForSale">
/// The type has a University price. The Developer has none: it "cannot be built in the
/// University" (manual 1408), and an order for it is
/// <see cref="CivilianPurchaseRefusal.NotPurchasable"/>.
/// </param>
/// <param name="ExpertsPerCivilian">Expert workers each civilian takes from the workforce for good.</param>
/// <param name="RequiredTechnologyKey">
/// What opens the type — Feed Grasses for the Rancher, Iron Railroad Bridges for the Forester,
/// Oil Drilling for the Driller in imported worlds.
/// </param>
/// <param name="IsGatedByTechnology">The country does not know it as the turn opens.</param>
/// <param name="Drafted">The drafted count; zero is not sent.</param>
/// <param name="Warning">Core's refusal of the drafted row, or that Core sets it aside; null when accepted or not drafted.</param>
public sealed record UniversityTypeView(
    CivilianTypeId Type,
    string Name,
    bool IsForSale,
    long ExpertsPerCivilian,
    IReadOnlyList<CommodityAmountView> CostPerCivilian,
    long CashPerCivilian,
    string? RequiredTechnologyKey,
    bool IsGatedByTechnology,
    long Drafted,
    OrderWarning<CivilianPurchaseRefusal>? Warning);

/// <summary>One drafted disband.</summary>
/// <param name="TypeName">The civilian's type, or null when no civilian carries the id.</param>
/// <param name="ExpertsReturned">
/// One expert when accepted, who works from next turn: Core returns him after this turn's
/// recipes, so he adds nothing to this turn's pool.
/// </param>
/// <param name="Warning">Core's refusal, or that Core sets it aside; null when accepted.</param>
public sealed record DisbandView(
    CivilianUnitId Unit,
    string? TypeName,
    long ExpertsReturned,
    OrderWarning<CivilianDisbandRefusal>? Warning)
{
    /// <summary>
    /// Cash and paper returned: none. "You do not recover the cost of cash and paper"
    /// (manual 1194–1195).
    /// </summary>
    public long Refunded => 0;
}

/// <summary>
/// The University dialog and the drafted disbands. Planned in <c>Production</c> after the
/// Trade School and before the Armoury: disbands, then purchases in the draft's order, each
/// all or nothing, against the healthy experts as the turn opens.
/// </summary>
/// <remarks>
/// A civilian bought here arrives in <c>Delivery</c>, idle on <see cref="ArrivesAt"/>, and
/// takes its first order next turn. Were the capital gone by then the civilian would be lost
/// and nothing refunded; rivals submit nothing, so nothing in this preview can take it.
/// </remarks>
/// <param name="ArrivesAt">The capital as the turn opens, or null — then every order is refused <see cref="CivilianPurchaseRefusal.NoCapital"/>.</param>
/// <param name="LabourWithheld">The bought civilians' experts' labour, gone from this turn's pool.</param>
public sealed record UniversityView(
    long CashClaimed,
    long ExpertsTaken,
    long LabourWithheld,
    CellIndex? ArrivesAt,
    IReadOnlyList<UniversityTypeView> Types,
    IReadOnlyList<DisbandView> Disbands);

/// <summary>
/// A turn-local restatement of Core's <c>Production</c> planning ahead of the recipes, for
/// one country: the Trade School, the University, the Armoury, then the Power Plant, each
/// against what the one before it left of the warehouse, the healthy workers and the
/// treasury.
/// </summary>
internal sealed class ProductionClaimsSimulation
{
    private ProductionClaimsSimulation(int commodityCount)
    {
        TradeSchoolBill = new long[commodityCount];
        UniversityBill = new long[commodityCount];
        PowerBill = new long[commodityCount];
    }

    public List<(WorkerGrade From, long Requested, WorkerTrainingRule? Rule, long LabourWithheld, OrderWarning<WorkerTrainingRefusal>? Warning)> Training
    { get; } = [];

    public List<(CivilianTypeId Type, long Count, OrderWarning<CivilianPurchaseRefusal>? Warning)> Purchases { get; } = [];

    public List<(CivilianUnitId Unit, CivilianUnit? Civilian, OrderWarning<CivilianDisbandRefusal>? Warning)> Disbands { get; } = [];

    public long[] TradeSchoolBill { get; }

    public long TradeSchoolCash { get; private set; }

    public long TradeSchoolLabour { get; private set; }

    public long[] UniversityBill { get; }

    public long UniversityCash { get; private set; }

    public long UniversityExperts { get; private set; }

    public long UniversityLabour { get; private set; }

    public ArmourySimulation Armoury { get; private set; } = null!;

    /// <summary>The fuel the plant burns, by commodity id.</summary>
    public long[] PowerBill { get; }

    public long FuelRequested { get; private set; }

    public long FuelHeld { get; private set; }

    public long FuelBurned { get; private set; }

    public long PowerMade { get; private set; }

    public OrderWarning<PowerRefusal>? PowerWarning { get; private set; }

    /// <summary>Core's <c>production.CashClaimed</c>: every cash claim before the recipes.</summary>
    public long CashClaimed => TradeSchoolCash + UniversityCash + Armoury.CashClaimed;

    public static ProductionClaimsSimulation Run(WorldState state, OrdersDraft draft)
    {
        var world = state.Definition;
        var country = draft.Country;
        var simulation = new ProductionClaimsSimulation(world.Commodities.Count);
        var available = new long[world.Commodities.Count];
        for (var index = 0; index < available.Length; index++)
        {
            available[index] = state.GetAvailableQuantity(country, new CommodityId(index));
        }

        var healthy = new long[WorkerGrades.Count];
        foreach (var grade in WorkerGrades.All)
        {
            healthy[(int)grade] = state.GetWorkers(country, grade) - state.GetSickWorkers(country, grade);
        }

        var cash = state.GetCash(country);
        var setAside = state.IsEliminated(country);

        // (a) The Trade School, grade by grade, each step against the healthy workers
        // the turn opened with.
        foreach (var grade in WorkerGrades.All)
        {
            var requested = draft.GetTraining(grade);
            if (requested == 0)
            {
                continue;
            }

            var rule = world.WorkerTraining?.GetRule(grade);
            if (setAside)
            {
                simulation.Training.Add((grade, requested, rule, 0, OrderWarning<WorkerTrainingRefusal>.SetAside));
                continue;
            }

            var refusal = CheckTraining(world, rule, requested, healthy[(int)grade], available, cash);
            if (refusal is { } reason)
            {
                simulation.Training.Add((grade, requested, rule, 0, reason));
                continue;
            }

            foreach (var item in rule!.Cost)
            {
                available[item.Commodity.Value] -= item.Quantity * requested;
                simulation.TradeSchoolBill[item.Commodity.Value] += item.Quantity * requested;
            }

            cash -= rule.CashCost * requested;
            simulation.TradeSchoolCash += rule.CashCost * requested;
            var withheld = world.Feeding is { } feeding ? requested * feeding.GetLabour(grade) : 0;
            simulation.TradeSchoolLabour += withheld;
            simulation.Training.Add((grade, requested, rule, withheld, null));
        }

        // (a2) The University: disbands, then purchases in the draft's order. A disbanded
        // civilian's expert returns after the recipes, so he is never spent here.
        foreach (var unit in draft.Disbands)
        {
            var civilian = state.GetCivilian(unit);
            OrderWarning<CivilianDisbandRefusal>? warning = setAside
                ? OrderWarning<CivilianDisbandRefusal>.SetAside
                : civilian is null
                    ? CivilianDisbandRefusal.NoSuchCivilian
                    : civilian.Country != country
                        ? CivilianDisbandRefusal.NotYours
                        : null;
            simulation.Disbands.Add((unit, civilian, warning));
        }

        // Core counts the University's experts against the turn's healthy experts, never
        // less the trainees: no step trains out of the expert grade.
        var healthyExperts = state.GetWorkers(country, WorkerGrade.Expert) -
            state.GetSickWorkers(country, WorkerGrade.Expert);
        foreach (var (type, count) in draft.CivilianPurchases)
        {
            // Build() sends only positive rows, so a zero row never reaches Core.
            if (count <= 0)
            {
                continue;
            }

            if (setAside)
            {
                simulation.Purchases.Add((type, count, OrderWarning<CivilianPurchaseRefusal>.SetAside));
                continue;
            }

            var refusal = CheckPurchase(state, country, type, count, healthyExperts, available, cash);
            simulation.Purchases.Add((type, count, refusal is { } reason ? reason : null));
            if (refusal is not null)
            {
                continue;
            }

            var price = world.CivilianTypes[type.Value].Purchase!;
            foreach (var item in price.Cost)
            {
                available[item.Commodity.Value] -= item.Quantity * count;
                simulation.UniversityBill[item.Commodity.Value] += item.Quantity * count;
            }

            cash -= price.CashCost * count;
            simulation.UniversityCash += price.CashCost * count;
            healthyExperts -= price.ExpertWorkers * count;
            simulation.UniversityExperts += price.ExpertWorkers * count;
            if (world.Feeding is { } feeding)
            {
                simulation.UniversityLabour += price.ExpertWorkers * count * feeding.GetLabour(WorkerGrade.Expert);
            }
        }

        // (a3) The Armoury, against what both left: the trainees' grade and the
        // University's experts are gone from the healthy workers.
        foreach (var (grade, requested, _, _, warning) in simulation.Training)
        {
            if (warning is null)
            {
                healthy[(int)grade] -= requested;
            }
        }

        healthy[(int)WorkerGrade.Expert] -= simulation.UniversityExperts;
        simulation.Armoury = ArmourySimulation.Run(
            state, country, draft.RegimentPurchases, available, healthy, ref cash);

        // (b) The Power Plant, against the warehouse the Armoury left.
        simulation.FuelRequested = draft.BurnFuel;
        if (world.Power is { } power)
        {
            simulation.FuelHeld = available[power.Fuel.Value];
        }

        if (draft.BurnFuel > 0)
        {
            OrderWarning<PowerRefusal>? refusal = setAside
                ? OrderWarning<PowerRefusal>.SetAside
                : CheckPower(state, country, draft.BurnFuel, available) is { } reason ? reason : null;
            simulation.PowerWarning = refusal;
            if (refusal is null)
            {
                var settings = world.Power!;
                available[settings.Fuel.Value] -= draft.BurnFuel;
                simulation.PowerBill[settings.Fuel.Value] += draft.BurnFuel;
                simulation.FuelBurned = draft.BurnFuel;
                simulation.PowerMade = draft.BurnFuel * settings.PowerPerFuel;
            }
        }

        return simulation;
    }

    /// <summary>Every commodity claimed before the recipes, by commodity id.</summary>
    public long BillOf(int commodity) =>
        TradeSchoolBill[commodity] + UniversityBill[commodity] + Armoury.Bill[commodity] + PowerBill[commodity];

    private static OrderWarning<WorkerTrainingRefusal>? CheckTraining(
        WorldDefinition world, WorkerTrainingRule? rule, long requested, long healthy, long[] available, long cash)
    {
        if (rule is null)
        {
            return WorkerTrainingRefusal.NotOffered;
        }

        if (requested > healthy)
        {
            return WorkerTrainingRefusal.NotEnoughWorkers;
        }

        if (rule.Cost.Any(item => !ArmourySimulation.CanAfford(item.Quantity, requested, available[item.Commodity.Value])))
        {
            return WorkerTrainingRefusal.NotEnoughCommodities;
        }

        return ArmourySimulation.CanAfford(rule.CashCost, requested, cash)
            ? null
            : WorkerTrainingRefusal.NotEnoughCash;
    }

    private static CivilianPurchaseRefusal? CheckPurchase(
        WorldState state,
        CountryId country,
        CivilianTypeId type,
        long count,
        long healthyExperts,
        long[] available,
        long cash)
    {
        var world = state.Definition;
        if ((uint)type.Value >= (uint)world.CivilianTypes.Count)
        {
            return CivilianPurchaseRefusal.UnknownType;
        }

        if (count <= 0)
        {
            return CivilianPurchaseRefusal.NonPositiveCount;
        }

        if (world.CivilianTypes[type.Value].Purchase is not { } price)
        {
            return CivilianPurchaseRefusal.NotPurchasable;
        }

        if (price.RequiredTechnology is { } required && !state.HasTechnology(country, required))
        {
            return CivilianPurchaseRefusal.TechnologyNotKnown;
        }

        if (state.GetCountryCapital(country) is null)
        {
            return CivilianPurchaseRefusal.NoCapital;
        }

        if (!ArmourySimulation.CanAfford(price.ExpertWorkers, count, healthyExperts))
        {
            return CivilianPurchaseRefusal.NotEnoughExperts;
        }

        if (price.Cost.Any(item => !ArmourySimulation.CanAfford(item.Quantity, count, available[item.Commodity.Value])))
        {
            return CivilianPurchaseRefusal.NotEnoughCommodities;
        }

        return ArmourySimulation.CanAfford(price.CashCost, count, cash)
            ? null
            : CivilianPurchaseRefusal.NotEnoughCash;
    }

    private static PowerRefusal? CheckPower(WorldState state, CountryId country, long fuel, long[] available)
    {
        if (state.Definition.Power is not { } settings)
        {
            return PowerRefusal.NotModelled;
        }

        if (state.GetProductionCapacity(country, settings.Plant) is not > 0)
        {
            return PowerRefusal.NoPowerPlant;
        }

        // Refused, not trimmed: Core's chosen rule.
        if (fuel > settings.MaxFuelPerTurn)
        {
            return PowerRefusal.OverTurnLimit;
        }

        return available[settings.Fuel.Value] < fuel ? PowerRefusal.NotEnoughFuel : null;
    }
}

public sealed partial class OrdersPreview
{
    /// <summary>The Trade School dialog.</summary>
    public TradeSchoolView TradeSchool { get; private init; } = null!;

    /// <summary>The Power Plant.</summary>
    public PowerPlantView PowerPlant { get; private init; } = null!;

    /// <summary>The University dialog and the drafted disbands.</summary>
    public UniversityView University { get; private init; } = null!;

    private static ProductionClaimsSimulation SimulateProductionClaims(WorldState state, OrdersDraft draft) =>
        ProductionClaimsSimulation.Run(state, draft);

    /// <summary>
    /// Cash the Industry screen's orders take in <c>Production</c> — the Trade School, the
    /// University and the Armoury — the first claim on the treasury; Core's
    /// <c>production.CashClaimed</c>.
    /// </summary>
    private static long ClaimedByProduction(WorldState state, OrdersDraft draft) =>
        SimulateProductionClaims(state, draft).CashClaimed;

    private static TradeSchoolView PreviewTradeSchool(
        WorldState state, OrdersDraft draft, Names names, ProductionClaimsSimulation claims)
    {
        var world = state.Definition;
        var country = draft.Country;
        var steps = new List<TradeSchoolStepView>();
        foreach (var grade in new[] { WorkerGrade.Untrained, WorkerGrade.Trained })
        {
            var rule = world.WorkerTraining?.GetRule(grade);
            var drafted = claims.Training.FirstOrDefault(row => row.From == grade);
            var found = claims.Training.Any(row => row.From == grade);
            steps.Add(new TradeSchoolStepView(
                grade,
                rule?.To,
                rule is null ? [] : names.Amounts(rule.Cost),
                rule?.CashCost ?? 0,
                world.Feeding?.GetLabour(grade) ?? 0,
                state.GetWorkers(country, grade) - state.GetSickWorkers(country, grade),
                draft.GetTraining(grade),
                found ? drafted.LabourWithheld : 0,
                found ? drafted.Warning : null));
        }

        return new TradeSchoolView(claims.TradeSchoolCash, claims.TradeSchoolLabour, Array.AsReadOnly(steps.ToArray()));
    }

    private static PowerPlantView PreviewPowerPlant(
        WorldState state, OrdersDraft draft, Names names, ProductionClaimsSimulation claims, long powerSpent)
    {
        var world = state.Definition;
        if (world.Power is not { } power)
        {
            return new PowerPlantView(
                false, null, null, null, 0, null, false, 0, 0, 0, draft.BurnFuel, 0, 0, claims.PowerWarning);
        }

        var plant = world.ProductionFacilities[power.Plant.Value];
        return new PowerPlantView(
            true,
            names.Key(power.Fuel),
            world.Commodities[power.Fuel.Value].Name,
            names.Key(power.Plant),
            state.GetProductionCapacity(draft.Country, power.Plant) ?? 0,
            plant.RequiredTechnology is { } required ? names.Key(required) : null,
            plant.RequiredTechnology is { } gate && !state.HasTechnology(draft.Country, gate),
            power.PowerPerFuel,
            power.MaxFuelPerTurn,
            claims.FuelHeld,
            draft.BurnFuel,
            claims.PowerMade,
            powerSpent,
            claims.PowerWarning);
    }

    private static UniversityView PreviewUniversity(
        WorldState state, OrdersDraft draft, Names names, ProductionClaimsSimulation claims)
    {
        var world = state.Definition;
        var country = draft.Country;
        var types = new List<UniversityTypeView>();
        foreach (var type in world.CivilianTypes)
        {
            var price = type.Purchase;
            var found = claims.Purchases.Any(row => row.Type == type.Id);
            var row = claims.Purchases.FirstOrDefault(row => row.Type == type.Id);
            types.Add(new UniversityTypeView(
                type.Id,
                type.Name,
                price is not null,
                price?.ExpertWorkers ?? 0,
                price is null ? [] : names.Amounts(price.Cost),
                price?.CashCost ?? 0,
                price?.RequiredTechnology is { } required ? names.Key(required) : null,
                price?.RequiredTechnology is { } gate && !state.HasTechnology(country, gate),
                draft.CivilianPurchases.FirstOrDefault(drafted => drafted.Type == type.Id).Count,
                found ? row.Warning : null));
        }

        var disbands = claims.Disbands
            .Select(row => new DisbandView(
                row.Unit,
                row.Civilian is { } civilian ? world.CivilianTypes[civilian.Type.Value].Name : null,
                row.Warning is null ? UniversityExpertsReturnedPerDisband : 0,
                row.Warning))
            .ToArray();
        return new UniversityView(
            claims.UniversityCash,
            claims.UniversityExperts,
            claims.UniversityLabour,
            state.GetCountryCapital(country),
            Array.AsReadOnly(types.ToArray()),
            Array.AsReadOnly(disbands));
    }

    /// <summary>Core's <c>UniversityPlanner.ExpertsReturnedPerDisband</c>, which is internal.</summary>
    private const long UniversityExpertsReturnedPerDisband = 1;
}
