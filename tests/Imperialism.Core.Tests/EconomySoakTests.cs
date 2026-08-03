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
    private const int CommodityCount = 10;

    private const int TextileMill = 0;
    private const int LumberMill = 1;
    private const int SteelMill = 2;

    private const int FabricRecipe = 0;
    private const int LumberRecipe = 1;
    private const int SteelRecipe = 2;

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

    /// <summary>How much the run actually did, so a silent no-op cannot pass.</summary>
    private sealed record WorkDone(long Gathered, long Eaten, long Delivered, long Produced, long Built);

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
        var report = new StringBuilder();
        report.AppendLine($"turn  workers  fed/sick/starved  labour   stock   capacity  pending");

        var previousDate = state.CurrentDate;
        var startingWorkers = Enumerable.Range(0, Powers)
            .Select(index => state.GetTotalWorkers(new CountryId(index))).ToArray();

        for (var turn = 1; turn <= Turns; turn++)
        {
            var labourBefore = Enumerable.Range(0, Powers)
                .Select(index => state.GetAvailableLabour(new CountryId(index))).ToArray();
            var capacityBefore = CapacitySnapshot(state);

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

            AssertIntegrity(state, resolution, turn, labourBefore, capacityBefore, startingWorkers);

            gathered += resolution.Events.OfType<ResourceExtractedEvent>()
                .Sum(item => item.Collected.Sum(q => q.Quantity));
            eaten += resolution.Events.OfType<WorkersFedEvent>()
                .Sum(item => item.Eaten.Sum(q => q.Quantity));
            delivered += resolution.Events.OfType<CommodityDeliveredEvent>().Count();
            produced += resolution.Events.OfType<ProductionCompletedEvent>()
                .Sum(item => item.CompletedCycles);
            built += resolution.Events.OfType<FacilityExpandedEvent>().Count();

            if (turn is 1 or 2 or 5 or 10 or 25 or 50 or 75 or Turns)
            {
                report.AppendLine(Summarise(state, resolution, turn));
            }
        }

        work = new WorkDone(gathered, eaten, delivered, produced, built);
        report.AppendLine(
            $"gathered {gathered}, eaten {eaten}, delivered {delivered}, " +
            $"produced {produced} cycles, built {built} times");
        return report.ToString();
    }

    private static void AssertIntegrity(
        WorldState state,
        TurnResolution resolution,
        int turn,
        long[] labourBefore,
        long[] capacityBefore,
        long[] startingWorkers)
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
            Assert.True(
                workers <= startingWorkers[index],
                $"Turn {turn}: country {index} grew from {startingWorkers[index]} to {workers} workers, " +
                "and nothing in the engine recruits yet.");

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

        return $"{turn,4}  {workers,7}  {fed.Sum(f => f.WellFed),4}/{fed.Sum(f => f.Sick),4}/" +
               $"{fed.Sum(f => f.Starved),7}  {labour,6}  {stock,6}  {capacity,8}  {pending,7}";
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
        }

        var map = new MapDefinition(
            dimensions,
            cells,
            provinces,
            [],
            [
                new ResourceDefinition(new ResourceId(Grain), new CommodityId(Grain), [0, 1, 2, 3]),
                new ResourceDefinition(new ResourceId(Fruit), new CommodityId(Fruit), [0, 1, 2, 3]),
                new ResourceDefinition(new ResourceId(Livestock), new CommodityId(Livestock), [0, 1, 2, 3]),
                new ResourceDefinition(new ResourceId(Cotton), new CommodityId(Cotton), [0, 1, 2, 3]),
                new ResourceDefinition(new ResourceId(Timber), new CommodityId(Timber), [0, 1, 2, 3]),
                new ResourceDefinition(new ResourceId(Coal), new CommodityId(Coal), [0, 1, 2, 3]),
                new ResourceDefinition(new ResourceId(Iron), new CommodityId(Iron), [0, 1, 2, 3]),
            ]);

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
            Enumerable.Range(0, Powers).Select(static index => new CountryId(index)));

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
        };

        var recipes = new[]
        {
            Recipe(FabricRecipe, "Fabric", TextileMill, Cotton, Fabric),
            Recipe(LumberRecipe, "Lumber", LumberMill, Timber, Lumber),
            Recipe(SteelRecipe, "Steel", SteelMill, Coal, Steel),
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
