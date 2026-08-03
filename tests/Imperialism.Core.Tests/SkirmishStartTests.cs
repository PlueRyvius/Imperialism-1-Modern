using Imperialism.Core;
using Xunit;

namespace Imperialism.Core.Tests;

/// <summary>
/// The fair start: every power begins identical, which is how most games are
/// played. The ten shipped scenarios are authored missions; `s10`, `s11` and
/// `s15` are the skirmish-shaped ones and agree exactly — mills at 2, factories
/// at 1, no refinery, and four untrained, two trained and one expert.
/// </summary>
/// <remarks>
/// That is the manual's construction floor, so the fair start and the bottom
/// rung of the build ladder are the same thing. See
/// <c>docs/formulas/production.md</c> and the domain split in <c>CLAUDE.md</c>.
/// </remarks>
public sealed class SkirmishStartTests
{
    private const int Mill = 0;
    private const int Factory = 1;

    private static readonly CountryId First = new(0);
    private static readonly CountryId Second = new(1);
    private static readonly CountryId Bystander = new(2);

    [Fact]
    public void EveryListedPowerStartsIdentical()
    {
        var state = CreateState(defaultStart: [First, Second]);

        foreach (var power in new[] { First, Second })
        {
            Assert.Equal(2, state.GetProductionCapacity(power, new ProductionFacilityId(Mill)));
            Assert.Equal(1, state.GetProductionCapacity(power, new ProductionFacilityId(Factory)));
            Assert.Equal(7, state.GetTotalWorkers(power));
            Assert.Equal(4, state.GetWorkers(power, WorkerGrade.Untrained));
            Assert.Equal(2, state.GetWorkers(power, WorkerGrade.Trained));
            Assert.Equal(1, state.GetWorkers(power, WorkerGrade.Expert));

            // 4x1 + 2x2 + 1x4 = 12 labour apiece.
            Assert.Equal(12, state.GetAvailableLabour(power));
        }
    }

    [Fact]
    public void ACountryThatWasNotListedGetsNothing()
    {
        // The original equips its seven Great Powers and leaves the minor
        // nations without an industry screen at all. Core cannot tell them
        // apart, so the scenario names who starts equipped rather than the
        // engine guessing and handing a workforce to every statelet.
        var state = CreateState(defaultStart: [First, Second]);

        Assert.Equal(0, state.GetProductionCapacity(Bystander, new ProductionFacilityId(Mill)));
        Assert.Equal(0, state.GetTotalWorkers(Bystander));
        Assert.Equal(0, state.GetAvailableLabour(Bystander));
    }

    [Fact]
    public void AnExplicitEntryStillBeatsTheDefault()
    {
        // A mission can start from the fair values and then differ where it
        // means to, which is the only way both kinds of scenario share a
        // mechanism.
        var state = CreateState(
            defaultStart: [First, Second],
            capacityOverrides: [new InitialProductionCapacity(Second, new ProductionFacilityId(Mill), 16)],
            workforceOverrides: [new InitialWorkforce(Second, 60, 5, 0)]);

        Assert.Equal(2, state.GetProductionCapacity(First, new ProductionFacilityId(Mill)));
        Assert.Equal(16, state.GetProductionCapacity(Second, new ProductionFacilityId(Mill)));

        // The override replaces the whole workforce, not just the grades it
        // mentions: s1 country 2 is [60, 5, 0] and has no experts at all.
        Assert.Equal(65, state.GetTotalWorkers(Second));
        Assert.Equal(0, state.GetWorkers(Second, WorkerGrade.Expert));

        // A facility the override is silent about keeps the default.
        Assert.Equal(1, state.GetProductionCapacity(Second, new ProductionFacilityId(Factory)));
    }

    [Fact]
    public void AFairStartResolvesATurnWithNoScenarioAuthoring()
    {
        // The point of the whole mechanism: a playable turn without importing
        // a mission. Both powers order two mill cycles; both can afford them.
        var state = CreateState(defaultStart: [First, Second], stock: 20);

        var resolution = TurnResolver.Resolve(
            state,
            new TurnOrders(
            [
                new CountryTurnOrders(First, [new ProductionOrder(new ProductionRecipeId(0), 2)]),
                new CountryTurnOrders(Second, [new ProductionOrder(new ProductionRecipeId(0), 2)]),
                new CountryTurnOrders(Bystander, []),
            ]),
            0);

        var produced = resolution.Events.OfType<ProductionCompletedEvent>().ToArray();
        Assert.Equal(2, produced.Length);
        Assert.All(produced, entry => Assert.Equal(2, entry.CompletedCycles));

        // Identical starts, identical orders, identical outcomes. If this ever
        // splits, something is reading a country's id where it should not.
        Assert.Equal(produced[0].LabourUsed, produced[1].LabourUsed);
        Assert.Equal(
            state.GetAvailableQuantity(First, new CommodityId(1)),
            state.GetAvailableQuantity(Second, new CommodityId(1)));
    }

    [Fact]
    public void AWorldWithNoDefaultsIsUnchanged()
    {
        var state = CreateState(defaultStart: [], withDefaults: false);

        Assert.Equal(0, state.GetProductionCapacity(First, new ProductionFacilityId(Mill)));
        Assert.Equal(0, state.GetTotalWorkers(First));
    }

    private static WorldState CreateState(
        IEnumerable<CountryId> defaultStart,
        IEnumerable<InitialProductionCapacity>? capacityOverrides = null,
        IEnumerable<InitialWorkforce>? workforceOverrides = null,
        long stock = 0,
        bool withDefaults = true)
    {
        var map = new MapDefinition(
            new MapDimensions(1, 1),
            [new CellDefinition(
                new CellIndex(0), new HexCoord(0, 0), new TerrainId(0), CellRegion.Unassigned)]);

        var inventory = new List<InitialCommodityStock>();
        if (stock > 0)
        {
            foreach (var country in new[] { First, Second })
            {
                inventory.Add(new InitialCommodityStock(country, new CommodityId(0), stock));
                inventory.Add(new InitialCommodityStock(country, new CommodityId(2), stock));
            }
        }

        var scenario = new ScenarioDefinition(
            "Skirmish",
            1815,
            [],
            initialInventory: inventory,
            initialProductionCapacities: capacityOverrides,
            initialWorkforce: workforceOverrides,
            defaultStartCountries: defaultStart);

        var facilities = new[]
        {
            new ProductionFacilityDefinition(
                new ProductionFacilityId(Mill), "Textile Mill", ProductionCapacityMode.Limited),
            new ProductionFacilityDefinition(
                new ProductionFacilityId(Factory), "Clothing Factory", ProductionCapacityMode.Limited),
        };

        var recipes = new[]
        {
            new ProductionRecipeDefinition(
                new ProductionRecipeId(0), "Fabric", new ProductionFacilityId(Mill), 1, 2,
                [new CommodityQuantity(new CommodityId(0), 2)],
                [new CommodityQuantity(new CommodityId(1), 1)]),
        };

        // Mills at 2 and factories at 1: the manual's construction floor, and
        // what all three skirmish scenarios give every power.
        var defaults = withDefaults
            ? new StartingDefaults(
                [
                    new FacilityCapacityDefault(new ProductionFacilityId(Mill), 2),
                    new FacilityCapacityDefault(new ProductionFacilityId(Factory), 1),
                ],
                new WorkforceDefault(untrained: 4, trained: 2, expert: 1))
            : null;

        return new WorldState(new WorldDefinition(
            map,
            [
                new CountryDefinition(First, "First"),
                new CountryDefinition(Second, "Second"),
                new CountryDefinition(Bystander, "Bystander"),
            ],
            scenario,
            [
                new CommodityDefinition(new CommodityId(0), "Cotton", CommodityCategory.Raw),
                new CommodityDefinition(new CommodityId(1), "Fabric", CommodityCategory.Material),
                new CommodityDefinition(new CommodityId(2), "Fish", CommodityCategory.Raw),
            ],
            facilities,
            recipes,
            null,
            null,
            new FeedingSettings([new FoodPreference([new CommodityId(2)])], [1, 2, 4]),
            defaults));
    }
}
