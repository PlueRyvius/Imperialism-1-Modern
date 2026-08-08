using Imperialism.Core;
using Xunit;

namespace Imperialism.Core.Tests;

/// <summary>
/// The Engineer: the one civilian that changes what the network *reaches*
/// rather than what a tile yields.
/// </summary>
/// <remarks>
/// The fixture is a 6x3 map. Row 0 (cells 0–5) is inland land, row 1 (cells
/// 6–11) is coastal land touching the ocean of row 2, and country 0 owns every
/// land cell. The capital is cell 0, rail is authored from it as far as cell 2,
/// and a grain tile sits at cell 3 just outside the capital's catchment — which
/// is what makes it the thing a depot is *for*.
/// <para>
/// Cell 4 is hills and cell 5 mountains, so the manual's rail gates have
/// somewhere to bite: Compound Steam Engine stands in as technology 0 and
/// Dynamite as technology 1.
/// </para>
/// </remarks>
public sealed class EngineerTests
{
    private const int Plains = 0;
    private const int Hills = 1;
    private const int Mountains = 2;
    private const int Ocean = 3;

    private const int Capital = 0;
    private const int RailEnd = 2;
    private const int GrainCell = 3;
    private const int HillCell = 4;
    private const int MountainCell = 5;
    private const int CoastCell = 6;

    // Rail is priced by the ground it crosses, so the fixture's three terrains
    // carry three prices and the depot and the port carry the world's two.
    private const long PlainsRail = 100;
    private const long HillRail = 200;
    private const long MountainRail = 300;
    private const long DepotCost = 1500;
    private const long PortCost = 2000;

    private static readonly CountryId Country = new(0);
    private static readonly CommodityId GrainId = new(0);
    private static readonly TechnologyId HillTechnology = new(0);
    private static readonly TechnologyId MountainTechnology = new(1);

    /// <summary>
    /// <b>The payoff.</b> A tile the network could not reach is stranded; a depot
    /// at the end of the line brings it into the catchment, and nothing in
    /// <see cref="ExtractionPlanner"/> had to be told that a new depot exists.
    /// </summary>
    [Fact]
    public void ADepotLightsUpACatchmentThatWasStranded()
    {
        var state = CreateState(engineerAt: RailEnd);

        // Turn one: the depot is ordered and the grain is still out of reach.
        var ordered = Resolve(state, Build(RailEnd, EngineerConstruction.Depot));
        var before = Assert.Single(ordered.Events.OfType<ResourceExtractedEvent>());
        Assert.Equal([new CommodityQuantity(GrainId, 1)], before.Stranded);
        Assert.Empty(before.Collected);

        // Turn two: the work finishes in Development, which runs before
        // Extraction, so the same turn gathers it.
        var finished = Resolve(state, TurnOrders.Empty(1));
        var built = Assert.Single(finished.Events.OfType<ConstructionCompletedEvent>());
        Assert.Equal(EngineerConstruction.Depot, built.Structure);
        Assert.True(state.HasDepot(new CellIndex(RailEnd)));

        var after = Assert.Single(finished.Events.OfType<ResourceExtractedEvent>());
        Assert.Equal([new CommodityQuantity(GrainId, 1)], after.Collected);
        Assert.Empty(after.Stranded);
    }

    /// <summary>
    /// A depot off the capital's rail component gathers nothing, so laying the
    /// line is what makes the structure worth building — the two halves of the
    /// Engineer's job needing each other.
    /// </summary>
    [Fact]
    public void RailLaidToADepotIsWhatConnectsIt()
    {
        // The depot sits one tile past the end of the line, so it is built and
        // useless until an Engineer reaches it.
        var state = CreateState(engineerAt: RailEnd, depots: [GrainCell]);

        var stranded = Assert.Single(
            Resolve(state, TurnOrders.Empty(1)).Events.OfType<ResourceExtractedEvent>());
        Assert.Equal([new CommodityQuantity(GrainId, 1)], stranded.Stranded);

        _ = Resolve(state, Build(GrainCell, EngineerConstruction.Rail));
        var connected = Assert.Single(
            Resolve(state, TurnOrders.Empty(1)).Events.OfType<ResourceExtractedEvent>());
        Assert.Equal([new CommodityQuantity(GrainId, 1)], connected.Collected);
    }

    /// <summary>
    /// "You do not always have the technology necessary to build rail into
    /// certain terrain." Compound Steam Engine for hills, in the manual's table.
    /// </summary>
    [Fact]
    public void RailIsRefusedIntoTerrainTheCountryCannotYetCross()
    {
        var state = CreateState(engineerAt: GrainCell);

        var refusal = Assert.Single(
            Resolve(state, Build(HillCell, EngineerConstruction.Rail))
                .Events.OfType<CivilianOrderRefusedEvent>());

        Assert.Equal(CivilianOrderRefusal.ConstructionTechnologyNotKnown, refusal.Reason);
        Assert.False(state.HasRail(new CellLink(new CellIndex(GrainCell), new CellIndex(HillCell))));

        // And nothing was charged for the order that was refused.
        Assert.Equal(10_000, state.GetCash(Country));
    }

    [Fact]
    public void RailIsAcceptedOnceTheCountryHasInvested()
    {
        var state = CreateState(engineerAt: GrainCell);
        state.GrantTechnology(Country, HillTechnology);

        _ = Resolve(state, Build(HillCell, EngineerConstruction.Rail));
        _ = Resolve(state, TurnOrders.Empty(1));

        Assert.True(state.HasRail(new CellLink(new CellIndex(GrainCell), new CellIndex(HillCell))));
    }

    /// <summary>
    /// Each gate is its own. Holding what it takes to cross hills says nothing
    /// about mountains, which want Dynamite.
    /// </summary>
    [Fact]
    public void EachTerrainWantsItsOwnTechnology()
    {
        var state = CreateState(engineerAt: HillCell);
        state.GrantTechnology(Country, HillTechnology);

        var refusal = Assert.Single(
            Resolve(state, Build(MountainCell, EngineerConstruction.Rail))
                .Events.OfType<CivilianOrderRefusedEvent>());
        Assert.Equal(CivilianOrderRefusal.ConstructionTechnologyNotKnown, refusal.Reason);

        state.GrantTechnology(Country, MountainTechnology);
        _ = Resolve(state, Build(MountainCell, EngineerConstruction.Rail));
        _ = Resolve(state, TurnOrders.Empty(1));
        Assert.True(state.HasRail(
            new CellLink(new CellIndex(HillCell), new CellIndex(MountainCell))));
    }

    /// <summary>
    /// The original shows the track cursor only "over tiles adjacent to the
    /// Engineer's current location", so a distant tile is not an order it could
    /// have produced.
    /// </summary>
    [Fact]
    public void RailIsRefusedToATileThatIsNotAdjacent()
    {
        var state = CreateState(engineerAt: Capital);

        var refusal = Assert.Single(
            Resolve(state, Build(GrainCell, EngineerConstruction.Rail))
                .Events.OfType<CivilianOrderRefusedEvent>());

        Assert.Equal(CivilianOrderRefusal.RailNeedsAnAdjacentTile, refusal.Reason);
    }

    /// <summary>
    /// Rail is the only construction that names a tile the Engineer is not
    /// standing on; the dialog opens "when you click on the tile where the
    /// Engineer is located".
    /// </summary>
    [Fact]
    public void ADepotIsRefusedAnywhereButTheEngineersOwnTile()
    {
        var state = CreateState(engineerAt: RailEnd);

        var refusal = Assert.Single(
            Resolve(state, Build(GrainCell, EngineerConstruction.Depot))
                .Events.OfType<CivilianOrderRefusedEvent>());

        Assert.Equal(CivilianOrderRefusal.StructureNeedsTheEngineersOwnTile, refusal.Reason);
    }

    /// <summary>
    /// "Ports always require access to water", and may be built "only on coasts
    /// and tiles containing a river."
    /// </summary>
    [Fact]
    public void APortIsRefusedInlandAndAcceptedOnTheCoast()
    {
        var inland = CreateState(engineerAt: Capital);
        var refusal = Assert.Single(
            Resolve(inland, Build(Capital, EngineerConstruction.Port))
                .Events.OfType<CivilianOrderRefusedEvent>());
        Assert.Equal(CivilianOrderRefusal.PortNeedsWater, refusal.Reason);

        var coastal = CreateState(engineerAt: CoastCell);
        _ = Resolve(coastal, Build(CoastCell, EngineerConstruction.Port));
        _ = Resolve(coastal, TurnOrders.Empty(1));
        Assert.True(coastal.HasPort(new CellIndex(CoastCell)));
    }

    /// <summary>
    /// A river tile counts as water even with no sea in sight — the manual says
    /// so outright, and 45 of the corpus's 124 ports have no adjacent sea at all.
    /// </summary>
    [Fact]
    public void APortIsAcceptedOnARiverWithNoSeaNearby()
    {
        var state = CreateState(engineerAt: Capital, riverAt: Capital);

        _ = Resolve(state, Build(Capital, EngineerConstruction.Port));
        _ = Resolve(state, TurnOrders.Empty(1));

        Assert.True(state.HasPort(new CellIndex(Capital)));
    }

    /// <summary>
    /// Depots reuse the rail gate. <b>An inference</b> — "more advanced
    /// construction technology increases the number of types terrain where rails
    /// may be laid and depots may be built", with no separate table given.
    /// </summary>
    [Fact]
    public void ADepotObeysTheSameTerrainGateAsRail()
    {
        var state = CreateState(engineerAt: HillCell);

        var refusal = Assert.Single(
            Resolve(state, Build(HillCell, EngineerConstruction.Depot))
                .Events.OfType<CivilianOrderRefusedEvent>());
        Assert.Equal(CivilianOrderRefusal.ConstructionTechnologyNotKnown, refusal.Reason);

        state.GrantTechnology(Country, HillTechnology);
        _ = Resolve(state, Build(HillCell, EngineerConstruction.Depot));
        _ = Resolve(state, TurnOrders.Empty(1));
        Assert.True(state.HasDepot(new CellIndex(HillCell)));
    }

    /// <summary>
    /// A port needs water and not a railhead, so it is the one construction the
    /// terrain gate does not touch.
    /// </summary>
    [Fact]
    public void APortIgnoresTheRailTerrainGate()
    {
        // Cell 11 is coastal and hilly, so rail would be refused there.
        var state = CreateState(engineerAt: 11);

        _ = Resolve(state, Build(11, EngineerConstruction.Port));
        _ = Resolve(state, TurnOrders.Empty(1));

        Assert.True(state.HasPort(new CellIndex(11)));
    }

    [Fact]
    public void ConstructionIsRefusedForWantOfCash()
    {
        var state = CreateState(engineerAt: RailEnd, cash: DepotCost - 1);

        var refusal = Assert.Single(
            Resolve(state, Build(RailEnd, EngineerConstruction.Depot))
                .Events.OfType<CivilianOrderRefusedEvent>());

        Assert.Equal(CivilianOrderRefusal.NotEnoughCash, refusal.Reason);
        Assert.Equal(DepotCost - 1, state.GetCash(Country));
        Assert.False(state.HasDepot(new CellIndex(RailEnd)));
    }

    /// <summary>
    /// Two Engineers and one treasury: the first order takes what it needs and
    /// the second is refused outright rather than half built.
    /// </summary>
    [Fact]
    public void ATreasuryThatCoversOneOrderDoesNotHalfBuildTheSecond()
    {
        var state = CreateState(engineerAt: RailEnd, secondEngineerAt: CoastCell, cash: DepotCost);

        var result = Resolve(state, new TurnOrders(
        [
            new CountryTurnOrders(
                Country,
                engineerWork:
                [
                    new EngineerOrder(new CivilianUnitId(1), new CellIndex(RailEnd),
                        EngineerConstruction.Depot),
                    new EngineerOrder(new CivilianUnitId(2), new CellIndex(CoastCell),
                        EngineerConstruction.Port),
                ]),
        ]));

        var begun = Assert.Single(result.Events.OfType<ConstructionBegunEvent>());
        Assert.Equal((EngineerConstruction.Depot, DepotCost), (begun.Structure, begun.Paid));

        var refusal = Assert.Single(result.Events.OfType<CivilianOrderRefusedEvent>());
        Assert.Equal(CivilianOrderRefusal.NotEnoughCash, refusal.Reason);
        Assert.Equal(0, state.GetCash(Country));
    }

    /// <summary>
    /// The cash leaves the treasury when the order is given, which is what makes
    /// a refusal something the player learns on the turn they ordered it.
    /// </summary>
    [Fact]
    public void CashIsSpentWhenTheOrderIsGivenRatherThanWhenItFinishes()
    {
        var state = CreateState(engineerAt: RailEnd);

        _ = Resolve(state, Build(RailEnd, EngineerConstruction.Depot));
        Assert.Equal(10_000 - DepotCost, state.GetCash(Country));

        _ = Resolve(state, TurnOrders.Empty(1));
        Assert.Equal(10_000 - DepotCost, state.GetCash(Country));
    }

    [Fact]
    public void EachStructureIsPricedSeparately()
    {
        var rail = CreateState(engineerAt: RailEnd);
        _ = Resolve(rail, Build(GrainCell, EngineerConstruction.Rail));
        Assert.Equal(10_000 - PlainsRail, rail.GetCash(Country));

        var port = CreateState(engineerAt: CoastCell);
        _ = Resolve(port, Build(CoastCell, EngineerConstruction.Port));
        Assert.Equal(10_000 - PortCost, port.GetCash(Country));
    }

    /// <summary>
    /// Rail is priced by the ground, and a link crossing two grounds pays for
    /// **the dearer of them** — which is a chosen rule, not a finding. The price
    /// list gives one figure per ground and a link has two ends.
    /// </summary>
    /// <remarks>
    /// The two rejected alternatives are what this pins down. Summing would charge
    /// 300 for the plains-to-hill link below and 200 for a plains-to-plains one,
    /// doubling every attested figure. Charging the *target* end would make the
    /// same link cost 200 built uphill and 100 built downhill, so a player would
    /// simply always build from the cheaper side.
    /// </remarks>
    [Fact]
    public void ALinkPaysForItsDearerEnd()
    {
        var uphill = CreateState(engineerAt: GrainCell);
        uphill.GrantTechnology(Country, HillTechnology);
        _ = Resolve(uphill, Build(HillCell, EngineerConstruction.Rail));
        Assert.Equal(10_000 - HillRail, uphill.GetCash(Country));

        // The same link from the other end, for the same price.
        var downhill = CreateState(engineerAt: HillCell);
        downhill.GrantTechnology(Country, HillTechnology);
        _ = Resolve(downhill, Build(GrainCell, EngineerConstruction.Rail));
        Assert.Equal(10_000 - HillRail, downhill.GetCash(Country));
    }

    /// <summary>
    /// A terrain that names no price builds free, the way a world with no
    /// <c>improvement</c> block improves free. Zero is free and not forbidden.
    /// </summary>
    [Fact]
    public void RailAcrossUnpricedGroundIsFree()
    {
        var state = CreateState(engineerAt: RailEnd, unpricedRail: true);

        // Ordered on the first turn and finished on the second, like any other
        // construction: free is not the same as instant.
        _ = Resolve(state, Build(GrainCell, EngineerConstruction.Rail));
        _ = Resolve(state, TurnOrders.Empty(1));

        Assert.Equal(10_000, state.GetCash(Country));
        Assert.True(state.HasRail(new CellLink(new CellIndex(RailEnd), new CellIndex(GrainCell))));
    }

    /// <summary>
    /// Building what is already there would spend the treasury and change
    /// nothing, so it is refused rather than charged for.
    /// </summary>
    [Fact]
    public void BuildingWhatIsAlreadyThereIsRefused()
    {
        var state = CreateState(engineerAt: RailEnd, depots: [RailEnd]);

        var depot = Assert.Single(
            Resolve(state, Build(RailEnd, EngineerConstruction.Depot))
                .Events.OfType<CivilianOrderRefusedEvent>());
        Assert.Equal(CivilianOrderRefusal.DepotAlreadyBuilt, depot.Reason);

        // Cell 1 to cell 2 is authored track.
        var moved = CreateState(engineerAt: 1);
        var rail = Assert.Single(
            Resolve(moved, Build(RailEnd, EngineerConstruction.Rail))
                .Events.OfType<CivilianOrderRefusedEvent>());
        Assert.Equal(CivilianOrderRefusal.RailAlreadyBuilt, rail.Reason);
    }

    /// <summary>
    /// What a civilian can be <em>asked</em> to do is still a property of its
    /// type. Only the choice within construction belongs to the order.
    /// </summary>
    [Fact]
    public void OnlyAnEngineerBuildsAndAnEngineerOnlyBuilds()
    {
        var state = CreateState(engineerAt: GrainCell, farmerAt: GrainCell);

        var farmer = Assert.Single(
            Resolve(state, new TurnOrders(
            [
                new CountryTurnOrders(
                    Country,
                    engineerWork:
                    [
                        new EngineerOrder(new CivilianUnitId(2), new CellIndex(HillCell),
                            EngineerConstruction.Rail),
                    ]),
            ])).Events.OfType<CivilianOrderRefusedEvent>());
        Assert.Equal(CivilianOrderRefusal.NotAnEngineer, farmer.Reason);

        var engineer = Assert.Single(
            Resolve(state, new TurnOrders(
            [
                new CountryTurnOrders(
                    Country,
                    civilianWork: [new CivilianWorkOrder(new CivilianUnitId(1), new CellIndex(GrainCell))]),
            ])).Events.OfType<CivilianOrderRefusedEvent>());
        Assert.Equal(CivilianOrderRefusal.NotAnEngineer, engineer.Reason);
    }

    /// <summary>
    /// Ocean carries no line, and neither does ground a world says nothing
    /// about. Distinct from a gate, because no investment ever opens it.
    /// </summary>
    [Fact]
    public void RailIsRefusedOntoWater()
    {
        var state = CreateState(engineerAt: CoastCell);

        var refusal = Assert.Single(
            Resolve(state, Build(12, EngineerConstruction.Rail))
                .Events.OfType<CivilianOrderRefusedEvent>());

        // Ocean is not this country's territory before it is unrailable, and the
        // entry rule is checked first because it is the more basic refusal.
        Assert.Equal(CivilianOrderRefusal.TargetNotLand, refusal.Reason);
    }

    /// <summary>
    /// A world that prices no construction has none, which is how every world
    /// behaved before Engineers could build and what a package older than
    /// version 17 still means.
    /// </summary>
    [Fact]
    public void AWorldWithNoConstructionSettingsBuildsNothing()
    {
        var state = CreateState(engineerAt: RailEnd, withConstruction: false);

        var refusal = Assert.Single(
            Resolve(state, Build(RailEnd, EngineerConstruction.Depot))
                .Events.OfType<CivilianOrderRefusedEvent>());

        Assert.Equal(CivilianOrderRefusal.NothingCanBeBuiltInThisWorld, refusal.Reason);
    }

    /// <summary>
    /// The Engineer's two cursors are alternatives to each other, exactly as
    /// deploying and working already are.
    /// </summary>
    [Fact]
    public void AnEngineerTakesOneOrderATurn() =>
        Assert.Throws<ArgumentException>(() => new CountryTurnOrders(
            Country,
            deployments: [new CivilianDeployOrder(new CivilianUnitId(1), new CellIndex(1))],
            engineerWork:
            [
                new EngineerOrder(new CivilianUnitId(1), new CellIndex(1), EngineerConstruction.Depot),
            ]));

    private static TurnOrders Build(int cell, EngineerConstruction structure) => new(
    [
        new CountryTurnOrders(
            Country,
            engineerWork:
            [
                new EngineerOrder(new CivilianUnitId(1), new CellIndex(cell), structure),
            ]),
    ]);

    private static TurnResolution Resolve(WorldState state, TurnOrders orders) =>
        TurnResolver.Resolve(state, orders, 0);

    private static WorldState CreateState(
        int engineerAt,
        long cash = 10_000,
        int? secondEngineerAt = null,
        int? farmerAt = null,
        int? riverAt = null,
        IEnumerable<int>? depots = null,
        bool withConstruction = true,
        bool unpricedRail = false)
    {
        const int width = 6;
        const int height = 3;
        var dimensions = new MapDimensions(width, height);

        // Row 2 is ocean; cell 5 is mountains, cell 4 and cell 11 hills, and
        // everything else plains.
        var cells = new CellDefinition[width * height];
        for (var index = 0; index < cells.Length; index++)
        {
            var row = index / width;
            var terrain = row == 2
                ? Ocean
                : index switch
                {
                    MountainCell => Mountains,
                    HillCell or 11 => Hills,
                    _ => Plains,
                };

            cells[index] = new CellDefinition(
                new CellIndex(index),
                dimensions.GetCoordinate(new CellIndex(index)),
                new TerrainId(terrain),
                row == 2
                    ? CellRegion.ForSeaZone(new SeaZoneId(0))
                    : CellRegion.ForProvince(new ProvinceId(index)),
                index == GrainCell ? [new ResourceId(0)] : null,
                index == Capital ? SettlementSiteKind.Urban : SettlementSiteKind.None,
                index == riverAt ? new RiverPath(RiverEndpoint.NorthEast, RiverEndpoint.SouthEast) : null);
        }

        var map = new MapDefinition(
            dimensions,
            cells,
            Enumerable.Range(0, width * 2)
                .Select(static index => new ProvinceDefinition(new ProvinceId(index), $"P{index}")),
            [new SeaZoneDefinition(new SeaZoneId(0), "Sea")],
            [new ResourceDefinition(new ResourceId(0), GrainId, [1])],
            [
                new TerrainDefinition(new TerrainId(Plains), "Plains", true,
                    rail: unpricedRail ? RailRule.Unrestricted : new RailRule(cashCost: PlainsRail)),
                new TerrainDefinition(new TerrainId(Hills), "Hills", true,
                    rail: new RailRule(HillTechnology, unpricedRail ? 0 : HillRail)),
                new TerrainDefinition(new TerrainId(Mountains), "Mountains", true,
                    rail: new RailRule(MountainTechnology, unpricedRail ? 0 : MountainRail)),
                new TerrainDefinition(new TerrainId(Ocean), "Ocean"),
            ]);

        var civilians = new List<InitialCivilian>
        {
            new(Country, new CivilianTypeId(0), new CellIndex(engineerAt)),
        };
        if (secondEngineerAt is { } second)
        {
            civilians.Add(new InitialCivilian(Country, new CivilianTypeId(0), new CellIndex(second)));
        }

        if (farmerAt is { } farmer)
        {
            civilians.Add(new InitialCivilian(Country, new CivilianTypeId(1), new CellIndex(farmer)));
        }

        var scenario = new ScenarioDefinition(
            "Engineer",
            1815,
            Enumerable.Repeat<CountryId?>(Country, width * 2),
            initialRailLinks:
            [
                new CellLink(new CellIndex(0), new CellIndex(1)),
                new CellLink(new CellIndex(1), new CellIndex(RailEnd)),
            ],
            initialCountryCapitals: [new CountryCapital(Country, new CellIndex(Capital))],
            initialDepots: depots?.Select(static cell => new CellIndex(cell)),
            initialCivilians: civilians,
            initialTransportCapacity: [new InitialTransportCapacity(Country, 100)],
            initialCash: [new InitialCash(Country, cash)]);

        return new WorldState(new WorldDefinition(
            map,
            [new CountryDefinition(Country, "Country 0")],
            scenario,
            [new CommodityDefinition(GrainId, "Grain", CommodityCategory.Raw)],
            technologies:
            [
                new TechnologyDefinition(HillTechnology, "Compound Steam Engine"),
                new TechnologyDefinition(MountainTechnology, "Dynamite"),
            ],
            civilianTypes:
            [
                new CivilianTypeDefinition(
                    new CivilianTypeId(0), "Engineer", 1, CivilianWorkKind.Construct),
                new CivilianTypeDefinition(new CivilianTypeId(1), "Farmer", 1),
            ],
            construction: withConstruction
                ? new ConstructionSettings(DepotCost, PortCost)
                : null));
    }
}
