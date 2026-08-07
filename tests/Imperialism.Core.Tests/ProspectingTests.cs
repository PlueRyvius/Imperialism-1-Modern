using Imperialism.Core;
using Xunit;

namespace Imperialism.Core.Tests;

/// <summary>
/// Prospector discovery: the five deposits that must be found before anyone can
/// work them, and the ground worth searching for them.
/// </summary>
/// <remarks>
/// The fixture is a 6x1 strip. Cell 0 is the capital on a farm; cells 1 and 2
/// are barren hills, the first carrying coal and the second carrying nothing at
/// all; cell 3 is swamp carrying oil, which cannot be searched without Oil
/// Drilling; cell 4 is barren hills carrying coal in country 1's hands; cell 5
/// is sea.
/// <para>
/// The empty barren hill is the important one. Most of the ground a Prospector
/// is sent to holds nothing — 449 of the corpus's 2,860 barren hills carry a
/// marker — so a fruitless search is the ordinary case, not an error case.
/// </para>
/// </remarks>
public sealed class ProspectingTests
{
    private const int Farm = 0;
    private const int BarrenHills = 1;
    private const int Swamp = 2;

    private const int Grain = 0;
    private const int Coal = 1;
    private const int Oil = 2;

    private const int Farmer = 0;
    private const int Prospector = 1;
    private const int Miner = 2;

    private const int OilDrilling = 0;

    private static readonly CellIndex CoalHill = new(1);
    private static readonly CellIndex EmptyHill = new(2);
    private static readonly CellIndex OilSwamp = new(3);

    [Fact]
    public void AProspectorRevealsTheDepositUnderneathAndTheTileIsSearchedFromThenOn()
    {
        var state = CreateState();
        var prospector = Unit(state, Prospector);

        var first = Resolve(state, Work(prospector, CoalHill));
        Assert.Equal(CoalHill, Assert.Single(first.Events.OfType<CivilianWorkBegunEvent>()).Cell);
        Assert.False(state.HasProspected(new CountryId(0), CoalHill));

        var second = Resolve(state, TurnOrders.Empty(2));
        var found = Assert.Single(second.Events.OfType<CellProspectedEvent>());
        Assert.Equal(CoalHill, found.Cell);
        Assert.Equal(prospector, found.Unit);
        Assert.Equal([new ResourceId(Coal)], found.Revealed);
        Assert.True(state.HasProspected(new CountryId(0), CoalHill));
        Assert.False(state.GetCivilian(prospector)!.IsBusy);

        // Searching is knowledge, not development: the tile is no more improved
        // than it was, and nothing has been gathered off it.
        Assert.Equal(0, state.GetCellDevelopment(CoalHill));
        Assert.Equal(0, Collected(second, Coal));
    }

    /// <summary>
    /// The common case. Empty ground is legal to search, completes normally, and
    /// reveals nothing — and the tile still counts as searched afterwards, which
    /// is what the original's toolbar counter is counting down.
    /// </summary>
    [Fact]
    public void AFruitlessSearchStillMarksTheTile()
    {
        var state = CreateState();
        var prospector = Unit(state, Prospector);

        _ = Resolve(state, Work(prospector, EmptyHill));
        var second = Resolve(state, TurnOrders.Empty(2));

        var found = Assert.Single(second.Events.OfType<CellProspectedEvent>());
        Assert.Empty(found.Revealed);
        Assert.True(state.HasProspected(new CountryId(0), EmptyHill));
        Assert.Empty(second.Events.OfType<CivilianOrderRefusedEvent>());
    }

    /// <summary>
    /// The point of the whole slice. "Miners cannot be used until a Prospector
    /// locates some gold, gems, coal, or iron to mine."
    /// </summary>
    [Fact]
    public void AMinerIsRefusedBeforeTheSearchAndAcceptedAfterIt()
    {
        var state = CreateState();
        var prospector = Unit(state, Prospector);
        var miner = Unit(state, Miner);

        var refused = Resolve(state, Work(miner, CoalHill));
        Assert.Equal(
            CivilianOrderRefusal.DepositNotYetDiscovered,
            Assert.Single(refused.Events.OfType<CivilianOrderRefusedEvent>()).Reason);

        _ = Resolve(state, Work(prospector, CoalHill));
        _ = Resolve(state, TurnOrders.Empty(2));

        _ = Resolve(state, Work(miner, CoalHill));
        var opened = Resolve(state, TurnOrders.Empty(2));

        var mine = Assert.Single(opened.Events.OfType<CellDevelopedEvent>());
        Assert.Equal((CoalHill, 0, 1), (mine.Cell, mine.FromLevel, mine.ToLevel));

        // The manual's curve: nothing at level 0, two per level after. Extraction
        // runs later in the same turn, so the new mine already pays.
        Assert.Equal(2, Collected(opened, Coal));
    }

    /// <summary>
    /// Knowledge is permanent, and a second visit is refused rather than wasted.
    /// The original marks a searched tile with a pickaxe and a red X.
    /// </summary>
    [Fact]
    public void ASecondSearchOfTheSameTileIsRefused()
    {
        var state = CreateState();
        var prospector = Unit(state, Prospector);

        _ = Resolve(state, Work(prospector, EmptyHill));
        _ = Resolve(state, TurnOrders.Empty(2));

        var again = Resolve(state, Work(prospector, EmptyHill));

        Assert.Equal(
            CivilianOrderRefusal.AlreadyProspected,
            Assert.Single(again.Events.OfType<CivilianOrderRefusedEvent>()).Reason);
    }

    /// <summary>
    /// "A Prospector cannot look for oil until you invest in Oil Drilling
    /// technology." Nothing else in the manual gates a search.
    /// </summary>
    [Fact]
    public void OilGroundCannotBeSearchedWithoutOilDrilling()
    {
        var state = CreateState();
        var prospector = Unit(state, Prospector);

        var refused = Resolve(state, Work(prospector, OilSwamp));
        Assert.Equal(
            CivilianOrderRefusal.ProspectingTechnologyNotKnown,
            Assert.Single(refused.Events.OfType<CivilianOrderRefusedEvent>()).Reason);

        state.GrantTechnology(new CountryId(0), new TechnologyId(OilDrilling));

        _ = Resolve(state, Work(prospector, OilSwamp));
        var found = Assert.Single(
            Resolve(state, TurnOrders.Empty(2)).Events.OfType<CellProspectedEvent>());
        Assert.Equal([new ResourceId(Oil)], found.Revealed);
    }

    /// <summary>
    /// A scenario can grant the technology outright, which is the only way an
    /// imported world reaches its oil at all: the converter emits Oil Drilling
    /// and gives it to nobody, and there is no research system to earn it with.
    /// </summary>
    [Fact]
    public void AScenarioMayStartACountryKnowingOilDrilling()
    {
        var state = CreateState(knowsOilDrilling: true);
        var prospector = Unit(state, Prospector);

        _ = Resolve(state, Work(prospector, OilSwamp));
        var found = Assert.Single(
            Resolve(state, TurnOrders.Empty(2)).Events.OfType<CellProspectedEvent>());

        Assert.Equal([new ResourceId(Oil)], found.Revealed);
    }

    /// <summary>
    /// "Most resources on the Terrain Map are automatically revealed to you just
    /// by looking at the type of terrain tile." A farm hides nothing, so there is
    /// nothing there for a Prospector to do.
    /// </summary>
    [Fact]
    public void GroundThatAnnouncesItselfCannotBeSearched()
    {
        var state = CreateState();
        var prospector = Unit(state, Prospector);

        var refused = Resolve(state, Work(prospector, new CellIndex(0)));

        Assert.Equal(
            CivilianOrderRefusal.TerrainCannotBeProspected,
            Assert.Single(refused.Events.OfType<CivilianOrderRefusedEvent>()).Reason);
    }

    /// <summary>
    /// A world whose terrain declares no prospecting terms hides nothing and
    /// searches nothing — which is exactly how every world behaved before this
    /// existed, and what a migrated version 13 package still means.
    /// </summary>
    [Fact]
    public void AWorldThatDeclaresNoProspectableGroundSearchesNothing()
    {
        var state = CreateState(withProspecting: false);
        var prospector = Unit(state, Prospector);

        var refused = Resolve(state, Work(prospector, CoalHill));

        Assert.Equal(
            CivilianOrderRefusal.TerrainCannotBeProspected,
            Assert.Single(refused.Events.OfType<CivilianOrderRefusedEvent>()).Reason);
    }

    /// <summary>
    /// The work a civilian does is a property of its type. A Farmer sent to
    /// barren hills is trying to improve them, not search them, so it is refused
    /// for what it cannot grow rather than for what it cannot find.
    /// </summary>
    [Fact]
    public void OnlyAProspectorSearches()
    {
        var state = CreateState();
        var farmer = Unit(state, Farmer);

        var refused = Resolve(state, Work(farmer, EmptyHill));

        Assert.Equal(
            CivilianOrderRefusal.NoDepositThisCivilianWorks,
            Assert.Single(refused.Events.OfType<CivilianOrderRefusedEvent>()).Reason);
    }

    /// <summary>
    /// Knowledge is per Great Power. One country searching a tile tells the
    /// others nothing, even about ground they can see.
    /// </summary>
    [Fact]
    public void OneCountrysSurveyIsNotAnothers()
    {
        var state = CreateState();
        var prospector = Unit(state, Prospector);

        _ = Resolve(state, Work(prospector, CoalHill));
        _ = Resolve(state, TurnOrders.Empty(2));

        Assert.True(state.HasProspected(new CountryId(0), CoalHill));
        Assert.False(state.HasProspected(new CountryId(1), CoalHill));
    }

    /// <summary>
    /// A scenario that authored a development level has, by saying so, put a
    /// visible mine on the tile. Nothing is seeded for this — the level itself
    /// is the signal, which is why the 1997 format needing no record for it
    /// costs us nothing.
    /// </summary>
    [Fact]
    public void AnAuthoredMineIsWorkableWithoutASearch()
    {
        var state = CreateState(initialDevelopment: [(1, 1)]);
        var miner = Unit(state, Miner);

        Assert.False(state.HasProspected(new CountryId(0), CoalHill));
        Assert.True(state.CanSeeDeposits(new CountryId(0), CoalHill));

        _ = Resolve(state, Work(miner, CoalHill));
        var deepened = Resolve(state, TurnOrders.Empty(2));

        var mine = Assert.Single(deepened.Events.OfType<CellDevelopedEvent>());
        Assert.Equal((1, 2), (mine.FromLevel, mine.ToLevel));
    }

    /// <summary>
    /// A mine that has been dug is a structure standing on the ground, so its
    /// new owner can see it without sending anybody to look. Capturing a working
    /// mine hands over a working mine.
    /// </summary>
    /// <remarks>
    /// Nobody has surveyed the tile — <see cref="WorldState.HasProspected"/> is
    /// still false for the new owner, and would stay false if the mine were ever
    /// abandoned back to level 0. What changes is only what they may act on.
    /// </remarks>
    [Fact]
    public void AConqueredMineIsVisibleBecauseItIsBuilt()
    {
        // Country 1's cell 4 starts as a level-1 coal mine.
        var state = CreateState(initialDevelopment: [(4, 1)]);
        var conquered = new CellIndex(4);
        Assert.False(state.HasProspected(new CountryId(0), conquered));
        Assert.True(state.CanSeeDeposits(new CountryId(0), conquered));

        state.SetProvinceOwner(new ProvinceId(4), new CountryId(0));
        var miner = Unit(state, Miner);

        _ = Resolve(state, Work(miner, conquered));
        var deepened = Resolve(state, TurnOrders.Empty(2));

        var mine = Assert.Single(deepened.Events.OfType<CellDevelopedEvent>());
        Assert.Equal((1, 2), (mine.FromLevel, mine.ToLevel));
    }

    /// <summary>
    /// Bare ground still has to be surveyed, however it was acquired. Conquest
    /// hands over what is visible on the surface and nothing that is not.
    /// </summary>
    [Fact]
    public void ConqueredGroundWithNoMineOnItStillNeedsASurvey()
    {
        var state = CreateState();
        var conquered = new CellIndex(4);

        state.SetProvinceOwner(new ProvinceId(4), new CountryId(0));
        var miner = Unit(state, Miner);

        var refused = Resolve(state, Work(miner, conquered));

        Assert.Equal(
            CivilianOrderRefusal.DepositNotYetDiscovered,
            Assert.Single(refused.Events.OfType<CivilianOrderRefusedEvent>()).Reason);
    }

    /// <summary>
    /// Territory is checked before anything else, so a Prospector is turned back
    /// at the border rather than told about the ground beyond it.
    /// </summary>
    [Fact]
    public void AProspectorMayNotSearchAnotherCountrysGround()
    {
        var state = CreateState();
        var prospector = Unit(state, Prospector);

        var refused = Resolve(state, Work(prospector, new CellIndex(4)));

        Assert.Equal(
            CivilianOrderRefusal.TargetNotYourTerritory,
            Assert.Single(refused.Events.OfType<CivilianOrderRefusedEvent>()).Reason);
        Assert.False(state.HasProspected(new CountryId(0), new CellIndex(4)));
    }

    /// <summary>
    /// Extraction is deliberately not gated on discovery, because the manual's
    /// yield curve already is: a mineral pays nothing until a mine is built.
    /// This pins that the undiscovered coal really does sit there paying nobody.
    /// </summary>
    [Fact]
    public void AnUndiscoveredDepositYieldsNothingBecauseItsCurveStartsAtZero()
    {
        var state = CreateState();

        var result = Resolve(state, TurnOrders.Empty(2));

        Assert.Equal(0, Collected(result, Coal));
        Assert.Equal(1, Collected(result, Grain));
    }

    private static CivilianUnitId Unit(WorldState state, int type) => state
        .GetCivilians(new CountryId(0))
        .Single(item => item.Type == new CivilianTypeId(type))
        .Id;

    private static TurnOrders Work(CivilianUnitId unit, CellIndex cell) => new(
    [
        new CountryTurnOrders(new CountryId(0), civilianWork: [new CivilianWorkOrder(unit, cell)]),
        new CountryTurnOrders(new CountryId(1)),
    ]);

    private static TurnResolution Resolve(WorldState state, TurnOrders orders) =>
        TurnResolver.Resolve(state, orders, 0);

    private static long Collected(TurnResolution resolution, int commodity) => resolution.Events
        .OfType<ResourceExtractedEvent>()
        .Where(static item => item.Country == new CountryId(0))
        .SelectMany(static item => item.Collected)
        .Where(item => item.Commodity == new CommodityId(commodity))
        .Sum(static item => item.Quantity);

    private static WorldState CreateState(
        bool withProspecting = true,
        bool knowsOilDrilling = false,
        (int Cell, int Level)[]? initialDevelopment = null)
    {
        const int width = 6;
        var dimensions = new MapDimensions(width, 1);
        var terrains = new[] { Farm, BarrenHills, BarrenHills, Swamp, BarrenHills, Farm };
        var deposits = new int?[] { Grain, Coal, null, Oil, Coal, null };
        var cells = new CellDefinition[width];
        for (var index = 0; index < width; index++)
        {
            cells[index] = new CellDefinition(
                new CellIndex(index),
                dimensions.GetCoordinate(new CellIndex(index)),
                new TerrainId(terrains[index]),
                index == 5
                    ? CellRegion.ForSeaZone(new SeaZoneId(0))
                    : CellRegion.ForProvince(new ProvinceId(index)),
                deposits[index] is { } deposit ? [new ResourceId(deposit)] : null,
                index == 0 ? SettlementSiteKind.Urban : SettlementSiteKind.None);
        }

        var map = new MapDefinition(
            dimensions,
            cells,
            Enumerable.Range(0, 5)
                .Select(static index => new ProvinceDefinition(new ProvinceId(index), $"Province {index}")),
            [new SeaZoneDefinition(new SeaZoneId(0), "Open Sea")],
            [
                new ResourceDefinition(
                    new ResourceId(Grain),
                    new CommodityId(Grain),
                    [1, 2, 3, 4],
                    improvedBy: new CivilianTypeId(Farmer)),

                // The manual's heavy-mineral curve: nothing until a mine is
                // built, then two a level. That zero is what makes gating
                // extraction on discovery unnecessary.
                new ResourceDefinition(
                    new ResourceId(Coal),
                    new CommodityId(Coal),
                    [0, 2, 4, 6],
                    improvedBy: new CivilianTypeId(Miner),
                    requiresDiscovery: true),
                new ResourceDefinition(
                    new ResourceId(Oil),
                    new CommodityId(Oil),
                    [0, 2, 4, 6],
                    requiresDiscovery: true),
            ],
            [
                new TerrainDefinition(new TerrainId(Farm), "Farm", isImprovable: true),
                new TerrainDefinition(
                    new TerrainId(BarrenHills),
                    "Barren Hills",
                    isImprovable: true,
                    prospecting: withProspecting ? ProspectingRule.Unrestricted : null),
                new TerrainDefinition(
                    new TerrainId(Swamp),
                    "Swamp",
                    isImprovable: true,
                    prospecting: withProspecting
                        ? new ProspectingRule(new TechnologyId(OilDrilling))
                        : null),
            ]);

        var scenario = new ScenarioDefinition(
            "Prospecting",
            1815,
            [new CountryId(0), new CountryId(0), new CountryId(0), new CountryId(0), new CountryId(1)],
            initialCountryCapitals: [new CountryCapital(new CountryId(0), new CellIndex(0))],
            initialCellDevelopment: initialDevelopment
                ?.Select(static item => new InitialCellDevelopment(new CellIndex(item.Cell), item.Level)),
            initialCountryTechnologies: knowsOilDrilling
                ? [new InitialCountryTechnology(new CountryId(0), new TechnologyId(OilDrilling))]
                : null,
            initialCivilians:
            [
                new InitialCivilian(new CountryId(0), new CivilianTypeId(Farmer), new CellIndex(0)),
                new InitialCivilian(new CountryId(0), new CivilianTypeId(Prospector), new CellIndex(0)),
                new InitialCivilian(new CountryId(0), new CivilianTypeId(Miner), new CellIndex(0)),
            ]);

        return new WorldState(new WorldDefinition(
            map,
            [
                new CountryDefinition(new CountryId(0), "Country 0"),
                new CountryDefinition(new CountryId(1), "Country 1"),
            ],
            scenario,
            [
                new CommodityDefinition(new CommodityId(Grain), "Grain", CommodityCategory.Raw),
                new CommodityDefinition(new CommodityId(Coal), "Coal", CommodityCategory.Raw),
                new CommodityDefinition(new CommodityId(Oil), "Oil", CommodityCategory.Raw),
            ],
            extraction: new ExtractionSettings(width),
            technologies: [new TechnologyDefinition(new TechnologyId(OilDrilling), "Oil Drilling")],
            civilianTypes:
            [
                new CivilianTypeDefinition(new CivilianTypeId(Farmer), "Farmer", 1),
                new CivilianTypeDefinition(
                    new CivilianTypeId(Prospector), "Prospector", 1, CivilianWorkKind.Prospect),
                new CivilianTypeDefinition(new CivilianTypeId(Miner), "Miner", 1),
            ]));
    }
}
