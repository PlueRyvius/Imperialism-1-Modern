using Imperialism.Core;
using Xunit;

namespace Imperialism.Core.Tests;

/// <summary>
/// The Benefits of Technology Table as a gate on improvement: what a country has
/// to know before a civilian may climb the next rung.
/// </summary>
/// <remarks>
/// The fixture is a 4x1 strip owned by country 0 except cell 3, which is sea.
/// Cell 1 is a farm carrying grain, whose ladder runs Seed Drill, Steel and Iron
/// Plows, Mechanical Reaper. Cell 2 is barren hills carrying coal, whose Level I
/// is <em>ungated</em> — the manual's one exception, and the reason a Miner is
/// one of the four civilians buildable from the start.
/// <para>
/// Discovery is deliberately switched off here. Prospecting has its own file;
/// this one is about knowledge and nothing else.
/// </para>
/// </remarks>
public sealed class TechnologyGateTests
{
    private const int Farm = 0;
    private const int Hills = 1;

    private const int Grain = 0;
    private const int Coal = 1;

    private const int Farmer = 0;
    private const int Miner = 1;

    private const int SeedDrill = 0;
    private const int Plows = 1;
    private const int Reaper = 2;
    private const int Timbering = 3;

    private static readonly CellIndex GrainFarm = new(1);
    private static readonly CellIndex CoalHill = new(2);

    /// <summary>
    /// Level I is a gate like any other. "Seed Drill: allows Farmers to improve
    /// Grain farms and Orchards to Level I."
    /// </summary>
    [Fact]
    public void AFarmerCannotReachLevelOneWithoutSeedDrill()
    {
        var state = CreateState();
        var farmer = Unit(state, Farmer);

        var refused = Resolve(state, Work(farmer, GrainFarm));
        Assert.Equal(
            CivilianOrderRefusal.ImprovementTechnologyNotKnown,
            Assert.Single(refused.Events.OfType<CivilianOrderRefusedEvent>()).Reason);

        state.GrantTechnology(new CountryId(0), new TechnologyId(SeedDrill));

        _ = Resolve(state, Work(farmer, GrainFarm));
        var raised = Assert.Single(
            Resolve(state, TurnOrders.Empty(2)).Events.OfType<CellDevelopedEvent>());
        Assert.Equal((0, 1), (raised.FromLevel, raised.ToLevel));
    }

    /// <summary>
    /// Each rung is its own gate, so knowing how to start a farm says nothing
    /// about knowing how to finish one.
    /// </summary>
    [Fact]
    public void EachRungIsGatedSeparately()
    {
        var state = CreateState(known: [SeedDrill]);
        var farmer = Unit(state, Farmer);

        _ = Resolve(state, Work(farmer, GrainFarm));
        _ = Resolve(state, TurnOrders.Empty(2));
        Assert.Equal(1, state.GetCellDevelopment(GrainFarm));

        // Level II wants Steel and Iron Plows, which this country has not bought.
        var refused = Resolve(state, Work(farmer, GrainFarm));
        Assert.Equal(
            CivilianOrderRefusal.ImprovementTechnologyNotKnown,
            Assert.Single(refused.Events.OfType<CivilianOrderRefusedEvent>()).Reason);

        state.GrantTechnology(new CountryId(0), new TechnologyId(Plows));
        _ = Resolve(state, Work(farmer, GrainFarm));
        _ = Resolve(state, TurnOrders.Empty(2));
        Assert.Equal(2, state.GetCellDevelopment(GrainFarm));

        // And Level III wants Mechanical Reaper, which it still has not.
        var stopped = Resolve(state, Work(farmer, GrainFarm));
        Assert.Equal(
            CivilianOrderRefusal.ImprovementTechnologyNotKnown,
            Assert.Single(stopped.Events.OfType<CivilianOrderRefusedEvent>()).Reason);
    }

    /// <summary>
    /// The manual's one ungated rung: "when a Miner finishes opening a new mine
    /// it produces at Level I", with no technology named anywhere. Level II is
    /// Square-Set Timbering.
    /// </summary>
    [Fact]
    public void AMineOpensAtLevelOneUngatedAndThenStops()
    {
        var state = CreateState();
        var miner = Unit(state, Miner);

        _ = Resolve(state, Work(miner, CoalHill));
        var opened = Assert.Single(
            Resolve(state, TurnOrders.Empty(2)).Events.OfType<CellDevelopedEvent>());
        Assert.Equal((0, 1), (opened.FromLevel, opened.ToLevel));

        var refused = Resolve(state, Work(miner, CoalHill));
        Assert.Equal(
            CivilianOrderRefusal.ImprovementTechnologyNotKnown,
            Assert.Single(refused.Events.OfType<CivilianOrderRefusedEvent>()).Reason);

        state.GrantTechnology(new CountryId(0), new TechnologyId(Timbering));
        _ = Resolve(state, Work(miner, CoalHill));
        _ = Resolve(state, TurnOrders.Empty(2));
        Assert.Equal(2, state.GetCellDevelopment(CoalHill));
    }

    /// <summary>
    /// <b>The gate governs building, never storing</b> — the same line the
    /// capacity ladder draws for <c>capa</c>. `s1` authors four timber tiles at
    /// Level III for a power that does not hold Dynamite, and the importer must
    /// take them.
    /// </summary>
    [Fact]
    public void AScenarioMayAuthorALevelItsOwnerCouldNotHaveBuilt()
    {
        var state = CreateState(initialDevelopment: [(1, 3)]);

        // Loaded intact, with no technology at all behind it.
        Assert.Equal(3, state.GetCellDevelopment(GrainFarm));
        Assert.False(state.HasTechnology(new CountryId(0), new TechnologyId(SeedDrill)));

        // And still refused any further work, because that is the part the gate
        // is actually about. Here the curve has run out first.
        var farmer = Unit(state, Farmer);
        var refused = Resolve(state, Work(farmer, GrainFarm));
        Assert.Equal(
            CivilianOrderRefusal.AlreadyFullyDeveloped,
            Assert.Single(refused.Events.OfType<CivilianOrderRefusedEvent>()).Reason);
    }

    /// <summary>
    /// "Already at the top of its curve" and "you do not know how" are different
    /// answers, and a player can act on only one of them.
    /// </summary>
    [Fact]
    public void RunningOutOfCurveIsReportedDifferentlyFromRunningOutOfKnowledge()
    {
        var finished = CreateState(known: [SeedDrill, Plows, Reaper], initialDevelopment: [(1, 3)]);
        Assert.Equal(
            CivilianOrderRefusal.AlreadyFullyDeveloped,
            Assert.Single(Resolve(finished, Work(Unit(finished, Farmer), GrainFarm))
                .Events.OfType<CivilianOrderRefusedEvent>()).Reason);

        var ignorant = CreateState(initialDevelopment: [(1, 2)]);
        Assert.Equal(
            CivilianOrderRefusal.ImprovementTechnologyNotKnown,
            Assert.Single(Resolve(ignorant, Work(Unit(ignorant, Farmer), GrainFarm))
                .Events.OfType<CivilianOrderRefusedEvent>()).Reason);
    }

    /// <summary>
    /// The fair start's knowledge reaches the countries a scenario names and no
    /// others — the same rule the workforce and capacity defaults follow, and
    /// for the same reason: the original equips its Great Powers and not its
    /// minor nations.
    /// </summary>
    [Fact]
    public void StartingTechnologyReachesOnlyTheNamedCountries()
    {
        var state = CreateState(defaultTechnologies: [SeedDrill], defaultStart: [0]);

        Assert.True(state.HasTechnology(new CountryId(0), new TechnologyId(SeedDrill)));
        Assert.False(state.HasTechnology(new CountryId(1), new TechnologyId(SeedDrill)));

        // And it works: a Farmer can reach Level I on turn one without anything
        // being granted at runtime.
        var farmer = Unit(state, Farmer);
        _ = Resolve(state, Work(farmer, GrainFarm));
        Assert.Single(Resolve(state, TurnOrders.Empty(2)).Events.OfType<CellDevelopedEvent>());
    }

    /// <summary>
    /// A world that declares no ladders gates nothing, which is what a package
    /// older than version 15 still means.
    /// </summary>
    [Fact]
    public void AWorldWithNoLaddersGatesNothing()
    {
        var state = CreateState(withLadders: false);
        var farmer = Unit(state, Farmer);

        _ = Resolve(state, Work(farmer, GrainFarm));

        Assert.Single(Resolve(state, TurnOrders.Empty(2)).Events.OfType<CellDevelopedEvent>());
    }

    [Fact]
    public void ADepositCannotGateALevelItsCurveNeverReaches()
    {
        Assert.Throws<ArgumentException>(() => new ResourceDefinition(
            new ResourceId(0),
            new CommodityId(0),
            [1, 2],
            technologyByDevelopmentLevel: [null, null, new TechnologyId(0)]));
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

    private static WorldState CreateState(
        int[]? known = null,
        int[]? defaultTechnologies = null,
        int[]? defaultStart = null,
        (int Cell, int Level)[]? initialDevelopment = null,
        bool withLadders = true)
    {
        const int width = 4;
        var dimensions = new MapDimensions(width, 1);
        var terrains = new[] { Farm, Farm, Hills, Farm };
        var deposits = new int?[] { Grain, Grain, Coal, null };
        var cells = new CellDefinition[width];
        for (var index = 0; index < width; index++)
        {
            cells[index] = new CellDefinition(
                new CellIndex(index),
                dimensions.GetCoordinate(new CellIndex(index)),
                new TerrainId(terrains[index]),
                index == 3
                    ? CellRegion.ForSeaZone(new SeaZoneId(0))
                    : CellRegion.ForProvince(new ProvinceId(index)),
                deposits[index] is { } deposit ? [new ResourceId(deposit)] : null,
                index == 0 ? SettlementSiteKind.Urban : SettlementSiteKind.None);
        }

        TechnologyId? Gate(int technology) => new TechnologyId(technology);

        var map = new MapDefinition(
            dimensions,
            cells,
            Enumerable.Range(0, 3)
                .Select(static index => new ProvinceDefinition(new ProvinceId(index), $"Province {index}")),
            [new SeaZoneDefinition(new SeaZoneId(0), "Open Sea")],
            [
                new ResourceDefinition(
                    new ResourceId(Grain),
                    new CommodityId(Grain),
                    [1, 2, 3, 4],
                    improvedBy: new CivilianTypeId(Farmer),
                    technologyByDevelopmentLevel: withLadders
                        ? [null, Gate(SeedDrill), Gate(Plows), Gate(Reaper)]
                        : null),

                // Level I ungated, which is the manual's one exception.
                new ResourceDefinition(
                    new ResourceId(Coal),
                    new CommodityId(Coal),
                    [0, 2, 4, 6],
                    improvedBy: new CivilianTypeId(Miner),
                    technologyByDevelopmentLevel: withLadders
                        ? [null, null, Gate(Timbering), null]
                        : null),
            ],
            [
                new TerrainDefinition(new TerrainId(Farm), "Farm", isImprovable: true),
                new TerrainDefinition(new TerrainId(Hills), "Barren Hills", isImprovable: true),
            ]);

        var scenario = new ScenarioDefinition(
            "Technology",
            1815,
            [new CountryId(0), new CountryId(0), new CountryId(0)],
            initialCountryCapitals: [new CountryCapital(new CountryId(0), new CellIndex(0))],
            initialCellDevelopment: initialDevelopment
                ?.Select(static item => new InitialCellDevelopment(new CellIndex(item.Cell), item.Level)),
            initialCountryTechnologies: known
                ?.Select(static item => new InitialCountryTechnology(
                    new CountryId(0), new TechnologyId(item))),
            defaultStartCountries: defaultStart?.Select(static item => new CountryId(item)),
            initialCivilians:
            [
                new InitialCivilian(new CountryId(0), new CivilianTypeId(Farmer), new CellIndex(0)),
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
            ],
            extraction: new ExtractionSettings(width),
            technologies:
            [
                new TechnologyDefinition(new TechnologyId(SeedDrill), "Seed Drill"),
                new TechnologyDefinition(new TechnologyId(Plows), "Steel and Iron Plows"),
                new TechnologyDefinition(new TechnologyId(Reaper), "Mechanical Reaper"),
                new TechnologyDefinition(new TechnologyId(Timbering), "Square-Set Timbering"),
            ],
            startingDefaults: defaultTechnologies is null
                ? null
                : new StartingDefaults(
                    [],
                    technologies: defaultTechnologies.Select(static item => new TechnologyId(item))),
            civilianTypes:
            [
                new CivilianTypeDefinition(new CivilianTypeId(Farmer), "Farmer", 1),
                new CivilianTypeDefinition(new CivilianTypeId(Miner), "Miner", 1),
            ]));
    }
}
