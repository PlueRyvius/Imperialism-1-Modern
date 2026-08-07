using System.Text;
using Imperialism.Core;
using Xunit;
using Xunit.Abstractions;

namespace Imperialism.Core.Tests;

/// <summary>
/// Runs the whole economy forward for a hundred turns and looks at what happens.
/// </summary>
/// <remarks>
/// <para>
/// Every other test in this suite resolves one turn, or two. Extraction,
/// development, connectivity, production, labour, feeding and capacity
/// construction have each been pinned in isolation and never once been watched
/// interacting over time — so an economy that starves itself by turn six, or
/// piles up stock without bound, would pass every one of them.
/// </para>
/// <para>
/// **These tests assert integrity, not outcomes.** Whether a power starves is a
/// question about game balance that nobody has the evidence to settle yet, and
/// pinning it here would freeze in a guess. What is asserted is only what can
/// never be true of a correct run: nothing negative, nothing impossible, nothing
/// that grows for ever. What actually happens is *reported*, so it can be looked
/// at and decided on deliberately.
/// </para>
/// </remarks>
public sealed class EconomySoakTests(ITestOutputHelper output)
{
    private const int Turns = 100;
    private const int Powers = 7;

    // Commodities. Raw first, then what the mills make of them.
    private const int Grain = 0;
    private const int Fruit = 1;
    private const int Livestock = 2;
    private const int Cotton = 3;
    private const int Timber = 4;
    private const int Coal = 5;
    private const int Iron = 6;
    private const int Fabric = 7;
    private const int Lumber = 8;
    private const int Steel = 9;

    // The comforts a recruit costs. One uncapped workshop makes all three,
    // which is a fixture convenience rather than the original's building list.
    private const int CannedFood = 10;
    private const int Clothing = 11;
    private const int Furniture = 12;
    private const int CommodityCount = 13;

    private const int TextileMill = 0;
    private const int LumberMill = 1;
    private const int SteelMill = 2;
    private const int Workshop = 3;

    private const int FabricRecipe = 0;
    private const int LumberRecipe = 1;
    private const int SteelRecipe = 2;
    private const int CannedFoodRecipe = 3;
    private const int ClothingRecipe = 4;
    private const int FurnitureRecipe = 5;

    private const int Farmer = 0;
    private const int Rancher = 1;
    private const int Forester = 2;
    private const int Miner = 3;

    /// <summary>Farmers seeded per power. The original builds them; this fixture cannot.</summary>
    private const int FarmersPerPower = 3;

    /// <summary>The guessed work duration, spelled out so the run's shape is traceable to it.</summary>
    private const int CivilianWorkTurns = 1;

    /// <summary>
    /// A deposit and the civilian that improves it, on this fixture's own yield
    /// curve. The curve is not the manual's — nothing here starts at 1 — and is
    /// deliberately harsher, so the economy has to work for its food.
    /// </summary>
    private static ResourceDefinition Deposit(int resource, int improver) =>
        new(new ResourceId(resource),
            new CommodityId(resource),
            [0, 1, 2, 3],
            null,
            new CivilianTypeId(improver));

    [Fact]
    public void AHundredTurnsWithNoOrdersKeepsTheWorldIntact()
    {
        // Nothing invented at all: no production, no expansion. This exercises
        // Extraction, Feeding and Delivery and nothing else, so anything it
        // finds is in the loop that fills a warehouse and empties it again.
        var state = CreateWorld();
        var log = Run(state, orderPolicy: null, out var work);

        output.WriteLine(log);

        // A hundred turns that gathered nothing and ate nothing would pass every
        // assertion above while testing none of them. #24 is the standing lesson
        // here: a green run is not the same as a run that did anything.
        Assert.True(work.Gathered > 0, "Nothing was ever extracted.");
        Assert.True(work.Eaten > 0, "Nobody ever ate.");
        Assert.True(work.Delivered > 0, "Nothing was ever delivered.");
        Assert.Equal(0, work.Produced);
        Assert.Equal(0, work.Built);
    }

    [Fact]
    public void AHundredTurnsOfProductionAndBuildingKeepsTheWorldIntact()
    {
        // Adds the stated policy below. Same assertions; more of the engine.
        var state = CreateWorld();
        var log = Run(state, orderPolicy: FoodFirstPolicy, out var work);

        output.WriteLine(log);

        Assert.True(work.Gathered > 0, "Nothing was ever extracted.");
        Assert.True(work.Eaten > 0, "Nobody ever ate.");
        Assert.True(work.Produced > 0, "No production cycle ever completed.");
        Assert.True(work.Built > 0, "No facility was ever built larger.");
    }

    [Fact]
    public void AHundredTurnsOfRecruitingShowsWhatGrowthCosts()
    {
        // The owner's read of the standing sickness result: in a real game you
        // grow farms and population as you go. Population is now possible;
        // farms are not, because improving a tile needs civilian units and Core
        // has no unit model. So this run grows demand against a fixed supply,
        // which should make the food balance worse rather than better. That is
        // the correct answer, and worth seeing rather than asserting away.
        var state = CreateWorld();
        var log = Run(state, orderPolicy: GrowthPolicy, out var work);

        output.WriteLine(log);

        Assert.True(work.RecruitAttempts > 0, "The Capitol was never asked for anybody.");
        Assert.True(work.Produced > 0, "No production cycle ever completed.");
    }

    /// <summary>
    /// The point of the civilian-units phase, end to end: farms improved, food
    /// deficit closed, canned food finally makeable, and migration — inert since
    /// it was built — recruiting at last.
    /// </summary>
    /// <remarks>
    /// The same world as <see cref="AHundredTurnsOfRecruitingShowsWhatGrowthCosts"/>,
    /// with one difference: the Farmers standing idle in that run are put to
    /// work. Every power holds three grain tiles against four workers who want
    /// grain, which is the permanent one-worker deficit the soak has reported
    /// since it was written, and improvement is the only thing that can close
    /// it.
    /// <para>
    /// What is asserted is the chain itself, in order, and not any number in it.
    /// The turn each stage lands on depends on the guessed work duration and on
    /// this fixture's arbitrary yield curve, so those are reported.
    /// </para>
    /// <para>
    /// <b>The run does not end with the deficit closed, and that is the finding
    /// rather than a failure.</b> Sickness clears on turn 2 and growth begins on
    /// turn 4; the population then outgrows the improved farms and settles with
    /// a fresh deficit at more than twice the headcount it started with. That is
    /// the manual's own warning about growing faster than you can feed, arrived
    /// at rather than written in, so it is reported and not asserted away.
    /// </para>
    /// </remarks>
    [Fact]
    public void FarmersImprovingGrainCloseTheDeficitAndUnblockMigration()
    {
        var state = CreateWorld();
        var log = Run(state, orderPolicy: FarmingGrowthPolicy, out var work);

        output.WriteLine(log);

        Assert.True(work.Developed > 0, "No tile was ever improved.");
        Assert.True(work.Recruited > 0, "Migration is still inert.");

        // The chain the owner described: farms, then food, then population.
        Assert.NotNull(work.FirstDevelopedTurn);
        Assert.NotNull(work.FirstSicknessFreeTurn);
        Assert.NotNull(work.FirstRecruitedTurn);
        Assert.True(
            work.FirstDevelopedTurn <= work.FirstSicknessFreeTurn,
            $"Sickness cleared on turn {work.FirstSicknessFreeTurn}, before any tile was improved " +
            $"on turn {work.FirstDevelopedTurn}.");
        Assert.True(
            work.FirstSicknessFreeTurn <= work.FirstRecruitedTurn,
            $"Somebody was recruited on turn {work.FirstRecruitedTurn}, while every worker was still " +
            $"ill through turn {work.FirstSicknessFreeTurn}.");

        // Improved farms really do gather more, and the workforce really does
        // grow on the strength of it.
        Assert.True(
            work.LastTurnGrain > work.FirstTurnGrain,
            $"Grain went from {work.FirstTurnGrain} to {work.LastTurnGrain} a turn.");
        Assert.True(
            work.LastTurnWorkers > work.FirstTurnWorkers,
            $"The workforce went from {work.FirstTurnWorkers} to {work.LastTurnWorkers}.");
    }

    /// <summary>
    /// The control. Identical world, identical orders, except that no Farmer is
    /// ever told to work — so the deficit stands, the harvest never moves, and
    /// migration stays as inert as it has been since it was built. Without this
    /// the run above would show a chain without showing what caused it.
    /// </summary>
    [Fact]
    public void LeavingTheFarmersIdleLeavesTheDeficitExactlyWhereItWas()
    {
        var state = CreateWorld();
        var log = Run(state, orderPolicy: GrowthPolicy, out var work);

        output.WriteLine(log);

        Assert.Equal(0, work.Developed);
        Assert.Equal(0, work.Recruited);
        Assert.True(work.RecruitAttempts > 0, "The Capitol was never asked for anybody.");
        Assert.Equal(work.FirstTurnGrain, work.LastTurnGrain);
        Assert.Equal(work.FirstTurnWorkers, work.LastTurnWorkers);
        Assert.True(work.LastTurnSick > 0, "The standing deficit closed with nobody working a field.");
    }

    /// <summary>How much the run actually did, so a silent no-op cannot pass.</summary>
    private sealed record WorkDone(
        long Gathered, long Eaten, long Delivered, long Produced, long Built,
        long Recruited, long RecruitAttempts, long Developed,
        int? FirstDevelopedTurn, int? FirstSicknessFreeTurn, int? FirstRecruitedTurn,
        int? SicknessReturnedTurn,
        long FirstTurnGrain, long LastTurnGrain, long LastTurnSick,
        long FirstTurnWorkers, long LastTurnWorkers);

    /// <summary>
    /// Makes the comforts a recruit costs and then recruits. A fixture, not an
    /// AI and not a finding — same warning as <see cref="FoodFirstPolicy"/>.
    /// </summary>
    private static CountryTurnOrders GrowthPolicy(WorldState state, CountryId country)
    {
        long Held(int commodity) => state.GetAvailableQuantity(country, new CommodityId(commodity));

        var production = new List<ProductionOrder>();
        foreach (var (recipe, input) in new[]
                 {
                     (FabricRecipe, Cotton), (LumberRecipe, Timber),
                     (CannedFoodRecipe, Grain), (ClothingRecipe, Fabric),
                     (FurnitureRecipe, Lumber),
                 })
        {
            var affordable = Held(input) / 2;
            if (affordable > 0)
            {
                production.Add(new ProductionOrder(new ProductionRecipeId(recipe), affordable));
            }
        }

        // Ask for far more than the country can possibly take, the way a player
        // drags the slider to its stop. The planner trims to what the country's
        // size allows and then to what it can pay, so an order that brings
        // nobody still reports why.
        return new CountryTurnOrders(country, production, null, recruitWorkers: 99);
    }

    /// <summary>
    /// <see cref="GrowthPolicy"/> plus the one thing this phase added: send every
    /// idle Farmer to the least-improved grain tile still worth working. Same
    /// warning — a fixture, not an AI, and nothing downstream should cite it.
    /// </summary>
    private static CountryTurnOrders FarmingGrowthPolicy(WorldState state, CountryId country)
    {
        var grown = GrowthPolicy(state, country);
        var map = state.Definition.Map;
        var taken = new HashSet<CellIndex>();
        var work = new List<CivilianWorkOrder>();

        foreach (var farmer in state.GetCivilians(country).Where(static item => !item.IsBusy))
        {
            var target = map.Cells
                .Where(cell => cell.Region.Kind == CellRegionKind.Province &&
                    state.GetProvinceOwner(cell.Region.Province) == country &&
                    map.GetTerrain(cell.Terrain) is { IsImprovable: true } &&
                    !taken.Contains(cell.Index) &&
                    cell.Resources.Any(resource =>
                        map.Resources[resource.Value].ImprovedBy == farmer.Type &&
                        state.GetCellDevelopment(cell.Index) <
                            map.Resources[resource.Value].MaxDevelopmentLevel))
                .OrderBy(cell => state.GetCellDevelopment(cell.Index))
                .ThenBy(static cell => cell.Index.Value)
                .FirstOrDefault();
            if (target is null)
            {
                continue;
            }

            taken.Add(target.Index);
            work.Add(new CivilianWorkOrder(farmer.Id, target.Index));
        }

        return new CountryTurnOrders(
            country,
            grown.Production,
            grown.Expansions,
            grown.RecruitWorkers,
            civilianWork: work);
    }

    /// <summary>
    /// A stand-in for a player, and **not an AI and not a finding**. It exists
    /// so the soak exercises production and construction at all; every choice
    /// in it is arbitrary and nothing downstream should cite it.
    /// </summary>
    private static CountryTurnOrders FoodFirstPolicy(WorldState state, CountryId country)
    {
        var production = new List<ProductionOrder>();
        foreach (var (recipe, input) in new[]
                 {
                     (FabricRecipe, Cotton), (LumberRecipe, Timber), (SteelRecipe, Coal),
                 })
        {
            var affordable = state.GetAvailableQuantity(country, new CommodityId(input)) / 2;
            if (affordable > 0)
            {
                production.Add(new ProductionOrder(new ProductionRecipeId(recipe), affordable));
            }
        }

        // Build whenever both materials are lying around, which is the crudest
        // possible rule and deliberately so.
        var expansions = new List<ProductionExpansionOrder>();
        if (state.GetAvailableQuantity(country, new CommodityId(Lumber)) >= 8 &&
            state.GetAvailableQuantity(country, new CommodityId(Steel)) >= 8)
        {
            expansions.Add(new ProductionExpansionOrder(new ProductionFacilityId(TextileMill)));
        }

        return new CountryTurnOrders(country, production, expansions);
    }

    private string Run(
        WorldState state,
        Func<WorldState, CountryId, CountryTurnOrders>? orderPolicy,
        out WorkDone work)
    {
        long gathered = 0, eaten = 0, delivered = 0, produced = 0, built = 0;
        long recruited = 0, recruitAttempts = 0, developed = 0;
        int? firstDeveloped = null, firstSicknessFree = null, firstRecruited = null;
        int? sicknessReturned = null;
        long firstTurnGrain = 0, lastTurnGrain = 0, lastTurnSick = 0;
        long firstTurnWorkers = 0, lastTurnWorkers = 0;
        var report = new StringBuilder();
        report.AppendLine(
            "turn  workers  fed/sick/starved  labour   stock   capacity  pending  grain  levels");

        var previousDate = state.CurrentDate;

        for (var turn = 1; turn <= Turns; turn++)
        {
            var labourBefore = Enumerable.Range(0, Powers)
                .Select(index => state.GetAvailableLabour(new CountryId(index))).ToArray();
            var capacityBefore = CapacitySnapshot(state);
            var workersBefore = Enumerable.Range(0, Powers)
                .Select(index => state.GetTotalWorkers(new CountryId(index))).ToArray();

            var orders = new TurnOrders(Enumerable.Range(0, Powers)
                .Select(index =>
                {
                    var country = new CountryId(index);
                    return orderPolicy is null
                        ? new CountryTurnOrders(country)
                        : orderPolicy(state, country);
                })
                .ToArray());

            var resolution = TurnResolver.Resolve(state, orders, (ulong)turn);

            Assert.Equal(turn, resolution.TurnNumber);
            Assert.Equal(previousDate, resolution.StartedAt);
            Assert.Equal(previousDate.Next(), resolution.EndedAt);
            previousDate = resolution.EndedAt;

            AssertIntegrity(state, resolution, turn, labourBefore, capacityBefore, workersBefore);

            gathered += resolution.Events.OfType<ResourceExtractedEvent>()
                .Sum(item => item.Collected.Sum(q => q.Quantity));
            eaten += resolution.Events.OfType<WorkersFedEvent>()
                .Sum(item => item.Eaten.Sum(q => q.Quantity));
            delivered += resolution.Events.OfType<CommodityDeliveredEvent>().Count();
            produced += resolution.Events.OfType<ProductionCompletedEvent>()
                .Sum(item => item.CompletedCycles);
            built += resolution.Events.OfType<FacilityExpandedEvent>().Count();
            var recruitedThisTurn = resolution.Events.OfType<WorkersRecruitedEvent>()
                .Sum(item => item.Recruited);
            recruited += recruitedThisTurn;
            recruitAttempts += resolution.Events.OfType<WorkersRecruitedEvent>().Count();

            var developedThisTurn = resolution.Events.OfType<CellDevelopedEvent>().Count();
            developed += developedThisTurn;
            if (developedThisTurn > 0)
            {
                firstDeveloped ??= turn;
            }

            if (recruitedThisTurn > 0)
            {
                firstRecruited ??= turn;
            }

            var sick = resolution.Events.OfType<WorkersFedEvent>().Sum(item => item.Sick);
            if (sick == 0)
            {
                firstSicknessFree ??= turn;
            }
            else if (firstSicknessFree is not null)
            {
                // A deficit that closes and then reopens is the interesting
                // case, so it is recorded rather than erasing the first date.
                sicknessReturned ??= turn;
            }

            var grain = GrainGathered(resolution);
            var headcount = Enumerable.Range(0, Powers)
                .Sum(index => state.GetTotalWorkers(new CountryId(index)));
            lastTurnGrain = grain;
            lastTurnSick = sick;
            lastTurnWorkers = headcount;
            if (turn == 1)
            {
                firstTurnGrain = grain;
                firstTurnWorkers = headcount;
            }

            if (turn is 1 or 2 or 5 or 10 or 25 or 50 or 75 or Turns)
            {
                report.AppendLine(Summarise(state, resolution, turn));
            }
        }

        work = new WorkDone(
            gathered, eaten, delivered, produced, built, recruited, recruitAttempts, developed,
            firstDeveloped, firstSicknessFree, firstRecruited, sicknessReturned,
            firstTurnGrain, lastTurnGrain, lastTurnSick, firstTurnWorkers, lastTurnWorkers);
        report.AppendLine(
            $"gathered {gathered}, eaten {eaten}, delivered {delivered}, " +
            $"produced {produced} cycles, built {built} times, " +
            $"recruited {recruited} from {recruitAttempts} requests, " +
            $"{developed} tiles improved");
        report.AppendLine(
            $"first improvement turn {Or(firstDeveloped)}, " +
            $"sickness first cleared turn {Or(firstSicknessFree)}, " +
            $"first recruit turn {Or(firstRecruited)}, " +
            $"sickness returned turn {Or(sicknessReturned)}");
        report.AppendLine(
            $"grain a turn {firstTurnGrain} -> {lastTurnGrain}; " +
            $"workers {firstTurnWorkers} -> {lastTurnWorkers}; " +
            $"sick at the end {lastTurnSick}");
        return report.ToString();

        static string Or(int? turn) => turn?.ToString() ?? "never";
    }

    private static void AssertIntegrity(
        WorldState state,
        TurnResolution resolution,
        int turn,
        long[] labourBefore,
        long[] capacityBefore,
        long[] workersBefore)
    {
        for (var index = 0; index < Powers; index++)
        {
            var country = new CountryId(index);

            for (var commodity = 0; commodity < CommodityCount; commodity++)
            {
                var held = state.GetAvailableQuantity(country, new CommodityId(commodity));
                Assert.True(held >= 0, $"Turn {turn}: country {index} holds {held} of commodity {commodity}.");
            }

            var workers = state.GetTotalWorkers(country);
            Assert.True(workers >= 0, $"Turn {turn}: country {index} has {workers} workers.");

            // Growth used to be forbidden outright, on the grounds that nothing
            // recruited. Migration changed that, so the rule is now the stronger
            // one: every worker that appears must be accounted for by a
            // recruitment event. An unexplained worker is still a failure.
            var recruited = resolution.Events.OfType<WorkersRecruitedEvent>()
                .Where(item => item.Country == country)
                .Sum(item => item.Recruited);
            Assert.True(
                workers <= workersBefore[index] + recruited,
                $"Turn {turn}: country {index} went from {workersBefore[index]} to {workers} " +
                $"workers with only {recruited} recruited.");

            foreach (var grade in WorkerGrades.All)
            {
                Assert.True(
                    state.GetSickWorkers(country, grade) <= state.GetWorkers(country, grade),
                    $"Turn {turn}: country {index} has more sick {grade} workers than {grade} workers.");
            }

            var spent = resolution.Events.OfType<ProductionCompletedEvent>()
                .Where(item => item.Country == country)
                .Sum(item => item.LabourUsed);
            Assert.True(
                spent <= labourBefore[index],
                $"Turn {turn}: country {index} spent {spent} labour from a pool of {labourBefore[index]}.");
        }

        // Capacity may only move during Construction, only upward, and only to
        // a rung the ladder actually offers.
        var capacityAfter = CapacitySnapshot(state);
        var built = resolution.Events.OfType<FacilityExpandedEvent>().ToArray();
        for (var slot = 0; slot < capacityAfter.Length; slot++)
        {
            if (capacityAfter[slot] == capacityBefore[slot])
            {
                continue;
            }

            Assert.True(
                capacityAfter[slot] > capacityBefore[slot],
                $"Turn {turn}: capacity slot {slot} shrank from {capacityBefore[slot]} to {capacityAfter[slot]}.");
            Assert.Contains(built, item =>
                item.FromCapacity == capacityBefore[slot] && item.ToCapacity == capacityAfter[slot]);
        }

        Assert.All(built, item => Assert.Equal(TurnPhase.Construction, item.Phase));
    }

    private static long[] CapacitySnapshot(WorldState state) =>
        Enumerable.Range(0, Powers)
            .SelectMany(country => new[] { TextileMill, LumberMill, SteelMill }
                .Select(facility => state.GetProductionCapacity(
                    new CountryId(country), new ProductionFacilityId(facility)) ?? 0))
            .ToArray();

    private static long GrainGathered(TurnResolution resolution) => resolution.Events
        .OfType<ResourceExtractedEvent>()
        .SelectMany(static item => item.Collected)
        .Where(static item => item.Commodity == new CommodityId(Grain))
        .Sum(static item => item.Quantity);

    private static string Summarise(WorldState state, TurnResolution resolution, int turn)
    {
        var fed = resolution.Events.OfType<WorkersFedEvent>().ToArray();
        var workers = Enumerable.Range(0, Powers).Sum(i => state.GetTotalWorkers(new CountryId(i)));
        var labour = Enumerable.Range(0, Powers).Sum(i => state.GetAvailableLabour(new CountryId(i)));
        var stock = Enumerable.Range(0, Powers)
            .Sum(i => Enumerable.Range(0, CommodityCount)
                .Sum(c => state.GetAvailableQuantity(new CountryId(i), new CommodityId(c))));
        var capacity = CapacitySnapshot(state).Sum();
        var pending = state.GetPendingDeliveries().Count;

        var levels = Enumerable.Range(0, state.Definition.Map.Dimensions.CellCount)
            .Sum(index => state.GetCellDevelopment(new CellIndex(index)));

        return $"{turn,4}  {workers,7}  {fed.Sum(f => f.WellFed),4}/{fed.Sum(f => f.Sick),4}/" +
               $"{fed.Sum(f => f.Starved),7}  {labour,6}  {stock,6}  {capacity,8}  {pending,7}  " +
               $"{GrainGathered(resolution),5}  {levels,6}";
    }

    /// <summary>
    /// Seven powers, each with a capital, a connected depot and a thin deposit
    /// base — two or three of each resource type, which is what a normal start
    /// actually looks like. A resource-rich fixture would come back healthy and
    /// prove nothing.
    /// </summary>
    private static WorldState CreateWorld()
    {
        // Each power gets a row of 22 cells: a capital at column 0, then a
        // repeating deposit / depot / deposit run. A depot reaches one step, so
        // every deposit sits beside one and nothing is stranded — this fixture
        // is about the economy, not about connectivity, which has its own tests.
        const int width = 22;
        var dimensions = new MapDimensions(width, Powers);

        // Two or three of each resource, which is what a normal start looks
        // like. Grain gets three because every worker eats and half of them
        // want grain specifically.
        int[] depositCycle =
        [
            Grain, Fruit, Grain, Livestock, Cotton, Timber,
            Grain, Fruit, Coal, Livestock, Cotton, Timber,
            Coal, Iron,
        ];

        var cells = new List<CellDefinition>();
        var provinces = new List<ProvinceDefinition>();
        var rails = new List<CellLink>();
        var capitals = new List<CountryCapital>();
        var depots = new List<CellIndex>();
        var development = new List<InitialCellDevelopment>();
        var owners = new List<CountryId?>();
        var civilians = new List<InitialCivilian>();

        for (var power = 0; power < Powers; power++)
        {
            var deposited = 0;
            for (var column = 0; column < width; column++)
            {
                var index = (power * width) + column;
                var cell = new CellIndex(index);
                provinces.Add(new ProvinceDefinition(new ProvinceId(index), $"P{power}-{column}"));
                owners.Add(new CountryId(power));

                var isCapital = column == 0;
                var isDepot = !isCapital && column % 3 == 2;

                ResourceId[] deposits = [];
                if (!isCapital && !isDepot)
                {
                    deposits = [new ResourceId(depositCycle[deposited % depositCycle.Length])];
                    deposited++;

                    // Level 1: developed enough to yield, nowhere near improved.
                    development.Add(new InitialCellDevelopment(cell, 1));
                }

                cells.Add(new CellDefinition(
                    cell,
                    dimensions.GetCoordinate(cell),
                    new TerrainId(0),
                    CellRegion.ForProvince(new ProvinceId(index)),
                    deposits,
                    isCapital ? SettlementSiteKind.Urban : SettlementSiteKind.None));

                if (column > 0)
                {
                    rails.Add(new CellLink(new CellIndex(index - 1), cell));
                }

                if (isDepot)
                {
                    depots.Add(cell);
                }
            }

            capitals.Add(new CountryCapital(new CountryId(power), new CellIndex(power * width)));

            // Standing in the capital with nothing to do, which is where the
            // original's University would have put them. Whether they are ever
            // told to work is the only difference between the two runs below.
            for (var farmer = 0; farmer < FarmersPerPower; farmer++)
            {
                civilians.Add(new InitialCivilian(
                    new CountryId(power), new CivilianTypeId(Farmer), new CellIndex(power * width)));
            }
        }

        var map = new MapDefinition(
            dimensions,
            cells,
            provinces,
            [],
            [
                Deposit(Grain, Farmer),
                Deposit(Fruit, Farmer),
                Deposit(Livestock, Rancher),
                Deposit(Cotton, Farmer),
                Deposit(Timber, Forester),
                Deposit(Coal, Miner),
                Deposit(Iron, Miner),
            ],

            // One terrain, and a civilian may work it. The three the manual
            // calls unimprovable have their own test; this fixture is about
            // whether improving anything at all changes the economy.
            [new TerrainDefinition(new TerrainId(0), "Farmland", isImprovable: true)]);

        var scenario = new ScenarioDefinition(
            "Soak",
            1815,
            owners,
            rails,
            capitals,
            null,
            null,
            development,
            null,
            null,
            depots,
            null,
            Enumerable.Range(0, Powers).Select(static index => new CountryId(index)),
            civilians);

        var facilities = new[]
        {
            new ProductionFacilityDefinition(
                new ProductionFacilityId(TextileMill), "Textile Mill",
                ProductionCapacityMode.Limited, new CapacityLadder([2, 4, 8, 16, 24], 8)),
            new ProductionFacilityDefinition(
                new ProductionFacilityId(LumberMill), "Lumber Mill",
                ProductionCapacityMode.Limited, new CapacityLadder([2, 4, 8, 16, 24], 8)),
            new ProductionFacilityDefinition(
                new ProductionFacilityId(SteelMill), "Steel Mill",
                ProductionCapacityMode.Limited, new CapacityLadder([2, 4, 8, 16, 24], 8)),
            new ProductionFacilityDefinition(
                new ProductionFacilityId(Workshop), "Workshop",
                ProductionCapacityMode.Unlimited),
        };

        var recipes = new[]
        {
            Recipe(FabricRecipe, "Fabric", TextileMill, Cotton, Fabric),
            Recipe(LumberRecipe, "Lumber", LumberMill, Timber, Lumber),
            Recipe(SteelRecipe, "Steel", SteelMill, Coal, Steel),
            Recipe(CannedFoodRecipe, "Canned Food", Workshop, Grain, CannedFood),
            Recipe(ClothingRecipe, "Clothing", Workshop, Fabric, Clothing),
            Recipe(FurnitureRecipe, "Furniture", Workshop, Lumber, Furniture),
        };

        return new WorldState(new WorldDefinition(
            map,
            Enumerable.Range(0, Powers)
                .Select(static index => new CountryDefinition(new CountryId(index), $"Power {index}")),
            scenario,
            [
                new CommodityDefinition(new CommodityId(Grain), "Grain", CommodityCategory.Raw),
                new CommodityDefinition(new CommodityId(Fruit), "Fruit", CommodityCategory.Raw),
                new CommodityDefinition(new CommodityId(Livestock), "Livestock", CommodityCategory.Raw),
                new CommodityDefinition(new CommodityId(Cotton), "Cotton", CommodityCategory.Raw),
                new CommodityDefinition(new CommodityId(Timber), "Timber", CommodityCategory.Raw),
                new CommodityDefinition(new CommodityId(Coal), "Coal", CommodityCategory.Raw),
                new CommodityDefinition(new CommodityId(Iron), "Iron", CommodityCategory.Raw),
                new CommodityDefinition(new CommodityId(Fabric), "Fabric", CommodityCategory.Material),
                new CommodityDefinition(new CommodityId(Lumber), "Lumber", CommodityCategory.Material),
                new CommodityDefinition(new CommodityId(Steel), "Steel", CommodityCategory.Material),
                new CommodityDefinition(new CommodityId(CannedFood), "Canned Food", CommodityCategory.Material),
                new CommodityDefinition(new CommodityId(Clothing), "Clothing", CommodityCategory.Goods),
                new CommodityDefinition(new CommodityId(Furniture), "Furniture", CommodityCategory.Goods),
            ],
            facilities,
            recipes,
            new ExtractionSettings(catchmentRadius: 1),
            null,
            // The original's cycle: grain, fruit, grain, meat.
            new FeedingSettings(
                [
                    new FoodPreference([new CommodityId(Grain)]),
                    new FoodPreference([new CommodityId(Fruit)]),
                    new FoodPreference([new CommodityId(Grain)]),
                    new FoodPreference([new CommodityId(Livestock)]),
                ],
                [1, 2, 4]),
            new StartingDefaults(
                [
                    new FacilityCapacityDefault(new ProductionFacilityId(TextileMill), 2),
                    new FacilityCapacityDefault(new ProductionFacilityId(LumberMill), 2),
                    new FacilityCapacityDefault(new ProductionFacilityId(SteelMill), 2),
                ],
                new WorkforceDefault(untrained: 4, trained: 2, expert: 1)),
            [
                new CommodityQuantity(new CommodityId(Lumber), 1),
                new CommodityQuantity(new CommodityId(Steel), 1),
            ],
            new MigrationSettings(
                [
                    new CommodityQuantity(new CommodityId(CannedFood), 1),
                    new CommodityQuantity(new CommodityId(Clothing), 1),
                    new CommodityQuantity(new CommodityId(Furniture), 1),
                ],
                provincesPerRecruit: 4),
            [
                new CivilianTypeDefinition(new CivilianTypeId(Farmer), "Farmer", CivilianWorkTurns),
                new CivilianTypeDefinition(new CivilianTypeId(Rancher), "Rancher", CivilianWorkTurns),
                new CivilianTypeDefinition(new CivilianTypeId(Forester), "Forester", CivilianWorkTurns),
                new CivilianTypeDefinition(new CivilianTypeId(Miner), "Miner", CivilianWorkTurns),
            ]));
    }

    private static ProductionRecipeDefinition Recipe(
        int id, string name, int facility, int input, int output) =>
        new(new ProductionRecipeId(id),
            name,
            new ProductionFacilityId(facility),
            1,
            2,
            [new CommodityQuantity(new CommodityId(input), 2)],
            [new CommodityQuantity(new CommodityId(output), 1)]);
}
