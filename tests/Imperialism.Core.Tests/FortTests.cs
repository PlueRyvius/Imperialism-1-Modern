using Imperialism.Core;
using Xunit;

namespace Imperialism.Core.Tests;

/// <summary>
/// Row 6e: an Engineer builds a fort on its own tile, and the fort stands "throughout
/// the province" — one level a job, three at most, counting jobs already under way.
/// </summary>
/// <remarks>
/// The fixture is eight plains tiles in a row, two to a province. Home holds provinces
/// 0 and 1 (cells 0–3) with its capital on 0; Other holds 2 and 3 (cells 4–7) with its
/// capital on 7. An Engineer works <see cref="WorkTurns"/> turns, and a fort costs
/// <see cref="FortCost"/>, which is this fixture's and not the importer's guess.
/// </remarks>
public sealed class FortTests
{
    private const int WorkTurns = 2;
    private const long FortCost = 2000;
    private const long StartingCash = 10_000;

    private const int Regulars = 0;
    private const int Bulwark = 1;

    private static readonly CountryId Home = new(0);
    private static readonly CountryId Other = new(1);
    private static readonly CivilianTypeId Engineer = new(0);

    [Fact]
    public void AnEngineerBuildsAFortThroughoutItsProvinceAfterItsWork()
    {
        var state = CreateState();
        var engineer = state.CreateCivilian(Home, Engineer, new CellIndex(2));

        var ordered = Resolve(state, home: Fort(engineer, 2));
        var begun = Assert.Single(ordered.OfType<ConstructionBegunEvent>());
        Assert.Equal(EngineerConstruction.Fort, begun.Structure);
        Assert.Equal(StartingCash - FortCost, state.GetCash(Home));
        Assert.Equal(0, state.GetFortLevel(Province(1)));

        // One turn of work left after the turn it was ordered.
        for (var turn = 1; turn < WorkTurns; turn++)
        {
            Assert.Empty(Resolve(state).OfType<ConstructionCompletedEvent>());
            Assert.Equal(0, state.GetFortLevel(Province(1)));
        }

        var done = Assert.Single(Resolve(state).OfType<ConstructionCompletedEvent>());
        Assert.Equal(EngineerConstruction.Fort, done.Structure);
        Assert.Equal(1, state.GetFortLevel(Province(1)));
        Assert.Equal((Province(1), 1), (done.FortProvince, done.FortLevel));

        // Province-wide: the fort is the province's, read the same from either tile,
        // and no other province is touched. Paid once, at the order.
        Assert.Contains(new CellIndex(3), state.Definition.Map.GetCells(Province(1)));
        Assert.Equal(0, state.GetFortLevel(Province(0)));
        Assert.Equal(StartingCash - FortCost, state.GetCash(Home));
    }

    [Fact]
    public void AJobUnderWayCountsAgainstTheCeiling()
    {
        var state = CreateState();
        state.SetFortLevel(Province(1), 2);
        var first = state.CreateCivilian(Home, Engineer, new CellIndex(2));
        var second = state.CreateCivilian(Home, Engineer, new CellIndex(3));

        var events = Resolve(state, home: new CountryTurnOrders(
            Home,
            engineerWork:
            [
                new EngineerOrder(first, new CellIndex(2), EngineerConstruction.Fort),
                new EngineerOrder(second, new CellIndex(3), EngineerConstruction.Fort),
            ]));

        var begun = Assert.Single(events.OfType<ConstructionBegunEvent>());
        Assert.Equal(first, begun.Unit);
        var refused = Assert.Single(events.OfType<CivilianOrderRefusedEvent>());
        Assert.Equal(second, refused.Unit);
        Assert.Equal(CivilianOrderRefusal.FortAtMaximum, refused.Reason);
        Assert.Equal(StartingCash - FortCost, state.GetCash(Home));

        // And a later turn's order still sees the job under way.
        var later = Resolve(state, home: Fort(second, 3));
        Assert.Equal(
            CivilianOrderRefusal.FortAtMaximum,
            Assert.Single(later.OfType<CivilianOrderRefusedEvent>()).Reason);
    }

    [Fact]
    public void AFortAtItsHighestLevelOffTheOwnTileOrOnForeignGroundIsRefused()
    {
        var full = CreateState();
        full.SetFortLevel(Province(1), WorldState.MaxFortLevel);
        var atFull = full.CreateCivilian(Home, Engineer, new CellIndex(2));
        Assert.Equal(CivilianOrderRefusal.FortAtMaximum, Refusal(Resolve(full, home: Fort(atFull, 2))));
        Assert.Equal(StartingCash, full.GetCash(Home));

        var adjacent = CreateState();
        var beside = adjacent.CreateCivilian(Home, Engineer, new CellIndex(2));
        Assert.Equal(
            CivilianOrderRefusal.StructureNeedsTheEngineersOwnTile,
            Refusal(Resolve(adjacent, home: Fort(beside, 3))));

        var foreign = CreateState();
        var abroad = foreign.CreateCivilian(Home, Engineer, new CellIndex(4));
        Assert.Equal(
            CivilianOrderRefusal.TargetNotYourTerritory,
            Refusal(Resolve(foreign, home: Fort(abroad, 4))));
        Assert.Equal(StartingCash, foreign.GetCash(Home));
    }

    /// <summary>
    /// A province that changes hands while the fort is under way refuses the job when it
    /// finishes, and the cash already paid stays spent. A capture by battle kills the
    /// old owner's civilians there, so the job cannot finish at all that way; the
    /// transfer here is the state's own, standing in for anything that changes an owner
    /// without killing the Engineer.
    /// </summary>
    [Fact]
    public void AProvinceLostWhileTheFortIsUnderWayRefusesItOnFinishing()
    {
        var state = CreateState();
        var engineer = state.CreateCivilian(Home, Engineer, new CellIndex(2));
        _ = Resolve(state, home: Fort(engineer, 2));
        state.SetProvinceOwner(Province(1), Other);

        TurnEvent[] events = [];
        for (var turn = 1; turn <= WorkTurns; turn++)
        {
            events = [.. events, .. Resolve(state)];
        }

        Assert.Empty(events.OfType<ConstructionCompletedEvent>());
        var refused = Assert.Single(events.OfType<CivilianOrderRefusedEvent>());
        Assert.Equal(CivilianOrderRefusal.TargetNotYourTerritory, refused.Reason);
        Assert.Equal(0, state.GetFortLevel(Province(1)));
        Assert.Equal(StartingCash - FortCost, state.GetCash(Home));
    }

    [Fact]
    public void AWorldThatPricesNoFortRefusesEveryFortOrder()
    {
        var state = CreateState(fortCost: null);
        var engineer = state.CreateCivilian(Home, Engineer, new CellIndex(2));

        Assert.Equal(
            CivilianOrderRefusal.FortsNotPricedInThisWorld,
            Refusal(Resolve(state, home: Fort(engineer, 2))));
        Assert.Equal(StartingCash, state.GetCash(Home));
        Assert.Throws<InvalidOperationException>(
            () => state.Definition.Construction!.GetCashCost(EngineerConstruction.Fort));
    }

    [Fact]
    public void AFortTheEngineerBuiltCutsTheDefendersLossInTheNextBattle()
    {
        static long Loss(bool build)
        {
            var state = CreateState();
            var engineer = state.CreateCivilian(Other, Engineer, new CellIndex(4));
            var attacker = state.CreateRegiment(Home, Province(1), new ArmyTypeId(Regulars));
            _ = state.CreateRegiment(Other, Province(2), new ArmyTypeId(Bulwark));
            state.SetRelationMode(Home, Other, CountryRelationMode.Hostile);

            // Both states run the same turns, so the battle's seed and ordinal match.
            _ = Resolve(state, other: build ? Fort(engineer, 4) : null);
            for (var turn = 1; turn <= WorkTurns; turn++)
            {
                _ = Resolve(state);
            }

            Assert.Equal(build ? 1 : 0, state.GetFortLevel(Province(2)));
            var battle = Assert.Single(
                Resolve(state, home: new CountryTurnOrders(
                    Home, regimentMoves: [new RegimentMoveOrder([attacker], Province(2))]))
                .OfType<BattleFoughtEvent>());
            return Regiment.MaxHealth -
                battle.Participants.Single(static item => item.Side == BattleSide.Defender).HealthAfter;
        }

        var open = Loss(build: false);
        var fortified = Loss(build: true);
        Assert.True(open > 0);
        Assert.True(fortified < open, $"fortified {fortified} should be below open {open}");
    }

    // ---- Helpers ----------------------------------------------------------

    private static CivilianOrderRefusal Refusal(IReadOnlyList<TurnEvent> events) =>
        Assert.Single(events.OfType<CivilianOrderRefusedEvent>()).Reason;

    /// <summary>A fort order; <see cref="Resolve"/> files it under whichever side it is passed as.</summary>
    private static CountryTurnOrders Fort(CivilianUnitId engineer, int cell) => new(
        Home,
        engineerWork: [new EngineerOrder(engineer, new CellIndex(cell), EngineerConstruction.Fort)]);

    private static ProvinceId Province(int value) => new(value);

    private static IReadOnlyList<TurnEvent> Resolve(
        WorldState state, CountryTurnOrders? home = null, CountryTurnOrders? other = null) =>
        TurnResolver.Resolve(
            state,
            new TurnOrders(
            [
                Rebind(home, Home),
                Rebind(other, Other),
            ]),
            7).Events;

    private static CountryTurnOrders Rebind(CountryTurnOrders? orders, CountryId country) =>
        orders is null
            ? new CountryTurnOrders(country)
            : new CountryTurnOrders(country, engineerWork: orders.EngineerWork, regimentMoves: orders.RegimentMoves);

    private static WorldState CreateState(long? fortCost = FortCost)
    {
        const int width = 8;
        var dimensions = new MapDimensions(width, 1);
        var cells = Enumerable.Range(0, width)
            .Select(index => new CellDefinition(
                new CellIndex(index),
                dimensions.GetCoordinate(new CellIndex(index)),
                new TerrainId(0),
                CellRegion.ForProvince(new ProvinceId(index / 2)),
                null,
                index is 0 or 7 ? SettlementSiteKind.Urban : SettlementSiteKind.None))
            .ToArray();

        var map = new MapDefinition(
            dimensions,
            cells,
            Enumerable.Range(0, width / 2)
                .Select(static index => new ProvinceDefinition(new ProvinceId(index), $"Province {index}")),
            [],
            [],
            [new TerrainDefinition(new TerrainId(0), "Plains", true, rail: RailRule.Unrestricted)]);

        var scenario = new ScenarioDefinition(
            "Forts",
            1816,
            Enumerable.Range(0, width / 2).Select(index => (CountryId?)(index < 2 ? Home : Other)),
            initialCountryCapitals:
            [
                new CountryCapital(Home, new CellIndex(0)),
                new CountryCapital(Other, new CellIndex(7)),
            ],
            initialCash: [new InitialCash(Home, StartingCash), new InitialCash(Other, StartingCash)]);

        static ArmyCombatStats Stats(decimal firepower, long defence, long? entrenched) =>
            new(firepower, firepower, 5, null, defence, entrenched, 4);

        return new WorldState(new WorldDefinition(
            map,
            [new CountryDefinition(Home, "Home"), new CountryDefinition(Other, "Other")],
            scenario,
            [new CommodityDefinition(new CommodityId(0), "Grain", CommodityCategory.Raw)],
            civilianTypes:
            [
                new CivilianTypeDefinition(Engineer, "Engineer", WorkTurns, CivilianWorkKind.Construct),
            ],
            construction: new ConstructionSettings(1500, 2000, fortCost),
            armyTypes:
            [
                new ArmyTypeDefinition(new ArmyTypeId(Regulars), "Regulars", ArmyCategory.RegularInfantry, 1,
                    stats: Stats(10, 5, 6)),
                new ArmyTypeDefinition(new ArmyTypeId(Bulwark), "Bulwark", ArmyCategory.HeavyInfantry, 1,
                    stats: Stats(0, 1, 100)),
            ]));
    }
}
