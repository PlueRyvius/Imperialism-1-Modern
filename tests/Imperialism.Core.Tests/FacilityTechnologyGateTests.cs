using Imperialism.Core;
using Xunit;

namespace Imperialism.Core.Tests;

/// <summary>
/// The Oil Refinery and the Power Plant are "available once Oil Drilling
/// technology is purchased". The gate refuses an expansion order and nothing
/// else: authored or default capacity loads without it, and a facility a
/// country already holds runs its recipes whether or not it could have built it.
/// </summary>
public sealed class FacilityTechnologyGateTests
{
    private const int Lumber = 0;
    private const int Steel = 1;
    private const int Oil = 2;
    private const int Fuel = 3;

    private static readonly CountryId Home = new(0);
    private static readonly ProductionFacilityId MillId = new(0);
    private static readonly ProductionFacilityId RefineryId = new(1);
    private static readonly TechnologyId OilDrilling = new(0);

    [Fact]
    public void ARefineryIsNotExpandedWithoutOilDrillingAndNothingIsCharged()
    {
        var state = CreateState(knowsOilDrilling: false);

        var resolution = Resolve(state, [new ProductionExpansionOrder(RefineryId)]);

        Assert.Empty(resolution.Events.OfType<FacilityExpandedEvent>());
        var refused = Assert.Single(resolution.Events.OfType<FacilityExpansionRefusedEvent>());
        Assert.Equal(Home, refused.Country);
        Assert.Equal(RefineryId, refused.Facility);
        Assert.Equal(FacilityExpansionRefusal.TechnologyNotKnown, refused.Reason);
        Assert.Equal(TurnPhase.Construction, refused.Phase);

        Assert.Equal(1, state.GetProductionCapacity(Home, RefineryId));
        Assert.Equal(10, state.GetAvailableQuantity(Home, new CommodityId(Lumber)));
        Assert.Equal(10, state.GetAvailableQuantity(Home, new CommodityId(Steel)));
        Assert.Equal(500, state.GetCash(Home));
    }

    [Fact]
    public void ARefineryIsExpandedOnceOilDrillingIsKnown()
    {
        var state = CreateState(knowsOilDrilling: true);

        var resolution = Resolve(state, [new ProductionExpansionOrder(RefineryId)]);

        Assert.Empty(resolution.Events.OfType<FacilityExpansionRefusedEvent>());
        var expanded = Assert.Single(resolution.Events.OfType<FacilityExpandedEvent>());
        Assert.Equal(1, expanded.FromCapacity);
        Assert.Equal(2, expanded.ToCapacity);
        Assert.Equal(2, state.GetProductionCapacity(Home, RefineryId));
        Assert.Equal(9, state.GetAvailableQuantity(Home, new CommodityId(Lumber)));
        Assert.Equal(9, state.GetAvailableQuantity(Home, new CommodityId(Steel)));
    }

    [Fact]
    public void AnUngatedFacilityIsUnaffectedByTheGateBesideIt()
    {
        // Both ordered in one turn: the refusal of one must not cost the other.
        var state = CreateState(knowsOilDrilling: false);

        var resolution = Resolve(
            state,
            [new ProductionExpansionOrder(MillId), new ProductionExpansionOrder(RefineryId)]);

        var expanded = Assert.Single(resolution.Events.OfType<FacilityExpandedEvent>());
        Assert.Equal(MillId, expanded.Facility);
        Assert.Equal(4, state.GetProductionCapacity(Home, MillId));
        Assert.Equal(RefineryId, Assert.Single(
            resolution.Events.OfType<FacilityExpansionRefusedEvent>()).Facility);
        Assert.Equal(8, state.GetAvailableQuantity(Home, new CommodityId(Lumber)));
    }

    [Fact]
    public void OilDrillingBoughtThisTurnOpensTheRefineryNextTurn()
    {
        // Knowledge is read as the turn opens; Investment runs last.
        var state = CreateState(knowsOilDrilling: false);

        var first = TurnResolver.Resolve(
            state,
            new TurnOrders([new CountryTurnOrders(
                Home,
                expansions: [new ProductionExpansionOrder(RefineryId)],
                buyTechnology: [OilDrilling])]),
            0);

        Assert.Single(first.Events.OfType<TechnologyPurchasedEvent>());
        Assert.Single(first.Events.OfType<FacilityExpansionRefusedEvent>());
        Assert.Equal(1, state.GetProductionCapacity(Home, RefineryId));

        var second = Resolve(state, [new ProductionExpansionOrder(RefineryId)]);

        Assert.Empty(second.Events.OfType<FacilityExpansionRefusedEvent>());
        Assert.Single(second.Events.OfType<FacilityExpandedEvent>());
        Assert.Equal(2, state.GetProductionCapacity(Home, RefineryId));
    }

    [Fact]
    public void AuthoredCapacityOfAGatedFacilityLoadsWithoutTheTechnology()
    {
        // The gate governs building, never storing.
        var state = CreateState(knowsOilDrilling: false, refineryCapacity: 12);

        Assert.False(state.HasTechnology(Home, OilDrilling));
        Assert.Equal(12, state.GetProductionCapacity(Home, RefineryId));
    }

    [Fact]
    public void DefaultCapacityOfAGatedFacilityLoadsWithoutTheTechnology()
    {
        var state = CreateState(
            knowsOilDrilling: false,
            refineryCapacity: null,
            defaults: new StartingDefaults([new FacilityCapacityDefault(RefineryId, 2)]));

        Assert.False(state.HasTechnology(Home, OilDrilling));
        Assert.Equal(2, state.GetProductionCapacity(Home, RefineryId));
    }

    [Fact]
    public void AGatedFacilityAlreadyHeldRunsItsRecipesWithoutTheTechnology()
    {
        // Running is never gated: only expansion is.
        var state = CreateState(knowsOilDrilling: false);

        var resolution = TurnResolver.Resolve(
            state,
            new TurnOrders([new CountryTurnOrders(
                Home, [new ProductionOrder(new ProductionRecipeId(0), 1)])]),
            0);

        var produced = Assert.Single(
            resolution.Events.OfType<ProductionCompletedEvent>(),
            static entry => entry.Recipe == new ProductionRecipeId(0));
        Assert.Equal(1, produced.CompletedCycles);
        Assert.Equal(3, state.GetAvailableQuantity(Home, new CommodityId(Oil)));
        Assert.Equal(1, state.GetAvailableQuantity(Home, new CommodityId(Fuel)));
    }

    [Fact]
    public void AFacilityThatCannotBeExpandedCannotBeGated()
    {
        Assert.Throws<ArgumentException>(() => new ProductionFacilityDefinition(
            RefineryId,
            "Oil Refinery",
            ProductionCapacityMode.Limited,
            capacityLadder: null,
            requiredTechnology: OilDrilling));
    }

    [Fact]
    public void AGateNamingAMissingTechnologyIsRejected()
    {
        var exception = Assert.Throws<ArgumentException>(() => CreateState(
            knowsOilDrilling: false, gate: new TechnologyId(7)));
        Assert.Equal("productionFacilities", exception.ParamName);
    }

    private static TurnResolution Resolve(WorldState state, ProductionExpansionOrder[] expansions) =>
        TurnResolver.Resolve(
            state,
            new TurnOrders([new CountryTurnOrders(Home, expansions: expansions)]),
            0);

    private static WorldState CreateState(
        bool knowsOilDrilling,
        long? refineryCapacity = 1,
        StartingDefaults? defaults = null,
        TechnologyId? gate = null)
    {
        var map = new MapDefinition(
            new MapDimensions(1, 1),
            [new CellDefinition(
                new CellIndex(0), new HexCoord(0, 0), new TerrainId(0), CellRegion.Unassigned)]);

        var capacities = new List<InitialProductionCapacity> { new(Home, MillId, 2) };
        if (refineryCapacity is { } refinery)
        {
            capacities.Add(new InitialProductionCapacity(Home, RefineryId, refinery));
        }

        var scenario = new ScenarioDefinition(
            "Refinery",
            1815,
            [],
            initialInventory:
            [
                new InitialCommodityStock(Home, new CommodityId(Lumber), 10),
                new InitialCommodityStock(Home, new CommodityId(Steel), 10),
                new InitialCommodityStock(Home, new CommodityId(Oil), 5),
            ],
            initialProductionCapacities: capacities,
            initialCountryTechnologies: knowsOilDrilling
                ? [new InitialCountryTechnology(Home, OilDrilling)]
                : [],
            defaultStartCountries: defaults is null ? null : [Home],
            initialCash: [new InitialCash(Home, 500)]);

        return new WorldState(new WorldDefinition(
            map,
            [new CountryDefinition(Home, "Home")],
            scenario,
            [
                new CommodityDefinition(new CommodityId(Lumber), "Lumber", CommodityCategory.Material),
                new CommodityDefinition(new CommodityId(Steel), "Steel", CommodityCategory.Material),
                new CommodityDefinition(new CommodityId(Oil), "Oil", CommodityCategory.Raw),
                new CommodityDefinition(new CommodityId(Fuel), "Fuel", CommodityCategory.Material),
            ],
            [
                new ProductionFacilityDefinition(
                    MillId, "Lumber Mill", ProductionCapacityMode.Limited, new CapacityLadder([2, 4, 8, 16, 24], 8)),
                new ProductionFacilityDefinition(
                    RefineryId,
                    "Oil Refinery",
                    ProductionCapacityMode.Limited,
                    new CapacityLadder([1, 2, 4, 8, 12], 4),
                    gate ?? OilDrilling),
            ],
            [
                new ProductionRecipeDefinition(
                    new ProductionRecipeId(0), "Refine", RefineryId, 1, 2,
                    [new CommodityQuantity(new CommodityId(Oil), 2)],
                    [new CommodityQuantity(new CommodityId(Fuel), 1)]),
            ],
            null,
            [new TechnologyDefinition(OilDrilling, "Oil Drilling", cost: 100)],
            null,
            defaults,
            [
                new CommodityQuantity(new CommodityId(Lumber), 1),
                new CommodityQuantity(new CommodityId(Steel), 1),
            ]));
    }
}
