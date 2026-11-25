using Imperialism.Core;
using Xunit;

namespace Imperialism.Core.Tests;

/// <summary>
/// Row 6b: an order into foreign ground is an attack, fought headlessly in
/// <c>Conflict</c> by the strategic resolver, and a win takes the province at once.
/// </summary>
/// <remarks>
/// The loss rule is a labelled placeholder until the executable's own is traced, so
/// nothing here pins a casualty figure that only the placeholder produces. Each test
/// is built so its outcome follows from the rules whatever the draws: a side that
/// fires nothing (engineers), a defence so deep no draw can break it, or a defence so
/// thin every draw breaks it. Chosen rules 12 onwards are in <c>docs/formulas/army.md</c>.
/// </remarks>
public sealed class LandBattleTests
{
    private const int Armaments = 0;
    private const int Grain = 1;
    private const int Fish = 2;

    private const int Minutemen = 0;
    private const int Regulars = 1;
    private const int Sappers = 2;
    private const int Bulwark = 3;
    private const int OpenBulwark = 4;
    private const int Gunners = 5;
    private const int General = 6;

    private const int Width = 8;

    /// <summary>The blueprint's stop: a battle still undecided after twenty rounds is the defender's.</summary>
    private const int MaxRounds = 20;

    private static readonly CountryId Home = new(0);
    private static readonly CountryId Other = new(1);
    private static readonly CountryId Third = new(2);

    // ---- Undefended capture ----------------------------------------------

    [Fact]
    public void AnUndefendedProvinceIsTakenAtOnceAndOnlyItsOwnersCiviliansDie()
    {
        var state = AtWar(CreateState());
        var regiment = state.CreateRegiment(Home, Province(2), Type(Regulars));
        var theirs = state.CreateCivilian(Other, Farmer, new CellIndex(3));
        var visitor = state.CreateCivilian(Third, Farmer, new CellIndex(3));
        var elsewhere = state.CreateCivilian(Other, Farmer, new CellIndex(4));

        var events = Resolve(state, home: Orders(Home, move: [new([regiment], Province(3))]));

        var captured = Assert.Single(events.OfType<ProvinceCapturedEvent>());
        Assert.Equal(TurnPhase.Conflict, captured.Phase);
        Assert.True(captured.Uncontested);
        Assert.Equal(Other, captured.PreviousOwner);
        Assert.Equal(Home, captured.NewOwner);
        Assert.Equal([regiment], captured.Regiments);
        Assert.Equal([theirs], captured.CiviliansKilled);
        Assert.False(captured.CapitalLost);
        Assert.Empty(events.OfType<BattleFoughtEvent>());
        Assert.Empty(events.OfType<RegimentsMovedEvent>());

        Assert.Equal(Home, state.GetProvinceOwner(Province(3)));
        Assert.Equal(Province(3), state.GetRegiment(regiment)!.Province);
        Assert.Null(state.GetCivilian(theirs));
        Assert.NotNull(state.GetCivilian(visitor));
        Assert.NotNull(state.GetCivilian(elsewhere));
    }

    [Fact]
    public void AProvinceWithNoOwnerMayBeTakenWithoutAWar()
    {
        var state = CreateState();
        var regiment = state.CreateRegiment(Third, Province(6), Type(Regulars));

        var events = Resolve(state, third: Orders(Third, move: [new([regiment], Province(7))]));

        var captured = Assert.Single(events.OfType<ProvinceCapturedEvent>());
        Assert.Null(captured.PreviousOwner);
        Assert.Equal(Third, state.GetProvinceOwner(Province(7)));
    }

    [Fact]
    public void ACaptureFeedsExtractionTheSameTurn()
    {
        // Grain on cells 1–5 and a catchment wider than the map, so the capital
        // gathers every grain tile its country owns when Extraction runs.
        var attacking = AtWar(CreateState());
        var control = AtWar(CreateState());
        var regiment = attacking.CreateRegiment(Home, Province(2), Type(Regulars));
        _ = control.CreateRegiment(Home, Province(2), Type(Regulars));

        var attacked = Resolve(attacking, home: Orders(Home, move: [new([regiment], Province(3))]));
        var idle = Resolve(control);

        Assert.Equal(Collected(idle, Home) + 1, Collected(attacked, Home));
        Assert.Equal(Collected(idle, Other) - 1, Collected(attacked, Other));
    }

    // ---- Refusals ---------------------------------------------------------

    [Fact]
    public void AnAttackWithoutAWarInEffectIsRefusedEvenIfDeclaredThisTurn()
    {
        var state = CreateState();
        var regiment = state.CreateRegiment(Home, Province(2), Type(Regulars));
        CountryTurnOrders Attack(bool declare) => new(
            Home,
            declareWar: declare ? [Other] : null,
            regimentMoves: [new([regiment], Province(3))]);

        var declared = Resolve(state, home: Attack(declare: true));
        Assert.Single(declared.OfType<WarDeclaredEvent>());
        AssertRefused(declared, RegimentMoveRefusal.NotAtWar);
        Assert.Equal(Other, state.GetProvinceOwner(Province(3)));
        Assert.Equal(Province(2), state.GetRegiment(regiment)!.Province);

        // Next turn the war is in effect and the same order goes through.
        var next = Resolve(state, home: Attack(declare: false));
        Assert.Single(next.OfType<ProvinceCapturedEvent>());
        Assert.Equal(Home, state.GetProvinceOwner(Province(3)));
    }

    [Fact]
    public void AnAttackMustComeFromANeighbouringProvince()
    {
        var state = AtWar(CreateState());
        var regiment = state.CreateRegiment(Home, Province(0), Type(Regulars));

        var events = Resolve(state, home: Orders(Home, move: [new([regiment], Province(3))]));

        AssertRefused(events, RegimentMoveRefusal.NotAdjacent);
        Assert.Equal(Province(0), state.GetRegiment(regiment)!.Province);
    }

    [Fact]
    public void ARegimentThatMovedCannotAttackTheSameTurn()
    {
        var state = AtWar(CreateState());
        var regiment = state.CreateRegiment(Home, Province(1), Type(Regulars));

        var events = Resolve(state, home: Orders(Home, move:
        [
            // Listed after the move on purpose: every move resolves before any attack.
            new([regiment], Province(3)),
            new([regiment], Province(2)),
        ]));

        Assert.Single(events.OfType<RegimentsMovedEvent>());
        AssertRefused(events, RegimentMoveRefusal.AlreadyMoved);
        Assert.Equal(Other, state.GetProvinceOwner(Province(3)));
    }

    [Fact]
    public void TheSecondAttackOnAProvinceTakenThisTurnIsRefused()
    {
        var state = AtWar(CreateState());
        var first = state.CreateRegiment(Home, Province(2), Type(Regulars));
        var second = state.CreateRegiment(Home, Province(2), Type(Regulars));

        var events = Resolve(state, home: Orders(Home, move:
        [
            new([first], Province(3)),
            new([second], Province(3)),
        ]));

        Assert.Single(events.OfType<ProvinceCapturedEvent>());
        AssertRefused(events, RegimentMoveRefusal.AlreadyOwn);
        Assert.Equal(Province(3), state.GetRegiment(first)!.Province);
        Assert.Equal(Province(2), state.GetRegiment(second)!.Province);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ASecondInvaderIsJudgedByTheProvincesNewOwner(bool thirdAtWarWithHome)
    {
        // Manual 3798–3817: Home takes Other's province 3 first (lower id). Third, at war
        // with Other, then attacks the same province. Not at war with Home, "the invasion
        // does not take place"; at war with Home, it fights Home's regiment there. Third's
        // regiment stands in Other's province 4 only because the strip offers no province
        // two countries both border.
        var state = CreateState();
        state.SetRelationMode(Home, Other, CountryRelationMode.Hostile);
        state.SetRelationMode(Third, Other, CountryRelationMode.Hostile);
        if (thirdAtWarWithHome)
        {
            state.SetRelationMode(Third, Home, CountryRelationMode.Hostile);
        }

        _ = TurnResolver.Resolve(state, TurnOrders.Empty(3), 0);
        var sapper = state.CreateRegiment(Home, Province(2), Type(Sappers));
        var invader = state.CreateRegiment(Third, Province(4), Type(Regulars));

        var events = Resolve(
            state,
            home: Orders(Home, move: [new([sapper], Province(3))]),
            third: Orders(Third, move: [new([invader], Province(3))]));

        var captures = events.OfType<ProvinceCapturedEvent>().ToArray();
        Assert.Equal((Province(3), (CountryId?)Other, Home, true), (captures[0].Province, captures[0].PreviousOwner, captures[0].NewOwner, captures[0].Uncontested));
        if (!thirdAtWarWithHome)
        {
            Assert.Single(captures);
            AssertRefused(events, RegimentMoveRefusal.NotAtWar);
            Assert.Empty(events.OfType<BattleFoughtEvent>());
            Assert.Equal(Home, state.GetProvinceOwner(Province(3)));
            Assert.Equal(Province(4), state.GetRegiment(invader)!.Province);
            return;
        }

        // The sappers fire nothing, so the invader must break them.
        var battle = Assert.Single(events.OfType<BattleFoughtEvent>());
        Assert.Equal((Third, Home, Third), (battle.Attacker, battle.Defender, battle.Winner));
        Assert.Equal([sapper], battle.Participants.Where(static item => item.Side == BattleSide.Defender).Select(static item => item.Regiment));
        Assert.Equal(2, captures.Length);
        Assert.Equal((Province(3), (CountryId?)Home, Third), (captures[1].Province, captures[1].PreviousOwner, captures[1].NewOwner));
        Assert.Equal(Third, state.GetProvinceOwner(Province(3)));
        Assert.Equal(Province(2), state.GetRegiment(sapper)!.Province);
    }

    // ---- Battles ----------------------------------------------------------

    [Fact]
    public void ADefendedProvinceIsFoughtForAndExperienceIsAwardedToTheCeiling()
    {
        // Sappers fire nothing, so the Regulars cannot lose health and must break them
        // within twenty rounds whatever the draws (rule 18's 1 + draw mod 7 is at least 1).
        var state = AtWar(CreateState());
        var veteran = state.CreateRegiment(Home, Province(2), Type(Regulars), experience: 390);
        var recruit = state.CreateRegiment(Home, Province(2), Type(Regulars));
        var sapper = state.CreateRegiment(Other, Province(3), Type(Sappers), experience: 100);

        var events = Resolve(state, home: Orders(Home, move: [new([veteran, recruit], Province(3))]));

        var battle = Assert.Single(events.OfType<BattleFoughtEvent>());
        Assert.Equal(Home, battle.Winner);
        Assert.Equal(Province(3), battle.Province);
        Assert.InRange(battle.Rounds, 1, MaxRounds);
        Assert.Equal(
            [(veteran, BattleSide.Attacker), (recruit, BattleSide.Attacker), (sapper, BattleSide.Defender)],
            battle.Participants.Select(static item => (item.Regiment, item.Side)));

        Assert.Equal(400, state.GetRegiment(veteran)!.Experience);
        Assert.Equal(35, state.GetRegiment(recruit)!.Experience);
        Assert.Equal([10L, 35L, 20L], battle.Participants.Select(static item => item.ExperienceGained));
        Assert.All(
            battle.Participants.Where(static item => item.Side == BattleSide.Attacker),
            item => Assert.Equal(Regiment.MaxHealth, item.HealthAfter));

        var defender = battle.Participants.Single(static item => item.Side == BattleSide.Defender);
        Assert.InRange(defender.HealthAfter, 1, Regiment.MaxHealth / 2);
        Assert.Equal(120, state.GetRegiment(sapper)!.Experience);

        // Won, so the attackers moved in and the sappers fell back to Other's next province.
        Assert.Equal(Home, state.GetProvinceOwner(Province(3)));
        Assert.Equal(Province(3), state.GetRegiment(veteran)!.Province);
        var retreat = Assert.Single(events.OfType<RegimentsRetreatedEvent>());
        Assert.Equal((Other, Province(3), Province(4)), (retreat.Country, retreat.From, retreat.To));
        Assert.Equal(Province(4), state.GetRegiment(sapper)!.Province);
        Assert.False(Assert.Single(events.OfType<ProvinceCapturedEvent>()).Uncontested);
    }

    [Fact]
    public void TheSameSeedFightsTheSameBattleAndAnotherSeedNeedNot()
    {
        static BattleFoughtEvent Fight(ulong seed)
        {
            var state = AtWar(CreateState());
            var attackers = new[]
            {
                state.CreateRegiment(Home, Province(2), Type(Regulars)),
                state.CreateRegiment(Home, Province(2), Type(Regulars)),
            };
            _ = state.CreateRegiment(Other, Province(3), Type(Regulars));
            _ = state.CreateRegiment(Other, Province(3), Type(Regulars));
            return Assert.Single(Resolve(state, seed, home: Orders(Home, move: [new(attackers, Province(3))]))
                .OfType<BattleFoughtEvent>());
        }

        static string Signature(BattleFoughtEvent battle) =>
            $"{battle.Winner}/{battle.Rounds}/" +
            string.Join(",", battle.Participants.Select(static item => item.HealthAfter));

        Assert.Equal(Signature(Fight(42)), Signature(Fight(42)));
        Assert.True(
            Enumerable.Range(0, 8).Select(seed => Signature(Fight((ulong)seed))).Distinct().Count() > 1,
            "Eight seeds fought eight identical battles: the seed is not reaching the resolver.");
    }

    [Theory]
    [InlineData(0UL)]
    [InlineData(42UL)]
    [InlineData(ulong.MaxValue)]
    public void TheAttackerFiresFirst(ulong seed)
    {
        // Both sides at health 1: the smallest possible hit (2 × 1 × 10 × 70 / 100 = 14,
        // over a defence of at most 6) takes at least 11, so whoever fires first wins in
        // one round whatever the draws — and that must be the attacker.
        var state = AtWar(CreateState());
        var ours = state.CreateRegiment(Home, Province(2), Type(Regulars), health: 1);
        var theirs = state.CreateRegiment(Other, Province(3), Type(Regulars), health: 1);

        var battle = Assert.Single(Resolve(state, seed, home: Orders(Home, move: [new([ours], Province(3))]))
            .OfType<BattleFoughtEvent>());

        Assert.Equal((Home, 1), (battle.Winner, battle.Rounds));
        Assert.Equal([theirs], battle.Killed);
        Assert.Equal(1, battle.Participants.Single(item => item.Regiment == ours).HealthAfter);
    }

    [Fact]
    public void AnEntrenchedDefenderTakesItsEntrenchedDefence()
    {
        // Defence 1 and entrenched 100, firing nothing. Entrenched, no hit can take more
        // than 126 × 5 / 100 = 6, so twenty rounds cannot halve it and the defence holds.
        // Without the trench every hit takes at least 70 and it breaks.
        var trenched = Defend(Bulwark);
        var open = Defend(OpenBulwark);

        Assert.Equal(Other, trenched.Winner);
        Assert.Equal(MaxRounds, trenched.Rounds);
        Assert.Equal(Home, open.Winner);
    }

    [Fact]
    public void ArtilleryDoesNotEntrench()
    {
        // Gunners carry the Bulwark's numbers but are artillery, so the 100 is ignored.
        var gunners = Defend(Gunners);

        Assert.Equal(Home, gunners.Winner);
        Assert.True(gunners.Rounds < MaxRounds);
    }

    [Fact]
    public void AFortTakesTenPercentALevelOffEveryHit()
    {
        // The Bulwark holds for twenty rounds either way and only one target is drawn
        // from, so both battles see the same draws and only the fort differs.
        var open = Defend(Bulwark, fort: 0);
        var fortified = Defend(Bulwark, fort: 3);

        var openLoss = Regiment.MaxHealth - DefenderHealth(open);
        var fortLoss = Regiment.MaxHealth - DefenderHealth(fortified);
        Assert.Equal(MaxRounds * 2, open.Rounds + fortified.Rounds);
        Assert.True(openLoss > 0);
        Assert.InRange(fortLoss, (openLoss * 7 / 10) - MaxRounds, openLoss * 7 / 10);
    }

    [Fact]
    public void EngineersAgainstEngineersStopAtTwentyRoundsAndTheDefenderHolds()
    {
        var state = AtWar(CreateState());
        var sapper = state.CreateRegiment(Home, Province(2), Type(Sappers));
        var theirs = state.CreateRegiment(Other, Province(3), Type(Sappers));

        var events = Resolve(state, home: Orders(Home, move: [new([sapper], Province(3))]));

        var battle = Assert.Single(events.OfType<BattleFoughtEvent>());
        Assert.Equal(MaxRounds, battle.Rounds);
        Assert.Equal(Other, battle.Winner);
        Assert.Empty(events.OfType<ProvinceCapturedEvent>());
        Assert.Equal(Other, state.GetProvinceOwner(Province(3)));
        Assert.Equal(Province(2), state.GetRegiment(sapper)!.Province);
        Assert.Equal(20, state.GetRegiment(sapper)!.Experience);
        Assert.Equal(35, state.GetRegiment(theirs)!.Experience);
    }

    [Fact]
    public void MilitiaDefendsItsProvinceButCannotAttack()
    {
        var state = AtWar(CreateState());
        var regulars = state.CreateRegiment(Home, Province(2), Type(Regulars));
        var minutemen = state.CreateRegiment(Other, Province(3), Type(Minutemen));
        var ours = state.CreateRegiment(Home, Province(2), Type(Minutemen));

        var events = Resolve(state, home: Orders(Home, move:
        [
            new([ours], Province(3)),
            new([regulars], Province(3)),
        ]));

        AssertRefused(events, RegimentMoveRefusal.CannotLeaveProvince);
        var battle = Assert.Single(events.OfType<BattleFoughtEvent>());
        Assert.Contains(battle.Participants, item => item.Regiment == minutemen && item.Side == BattleSide.Defender);
        Assert.Equal(Province(2), state.GetRegiment(ours)!.Province);
    }

    [Fact]
    public void DefendersWithNowhereToFallBackSurrender()
    {
        // Province 3's neighbours are 2 and 4, and both are Home's.
        var state = AtWar(CreateState());
        state.SetProvinceOwner(Province(4), Home);
        var regiment = state.CreateRegiment(Home, Province(2), Type(Regulars));
        var sapper = state.CreateRegiment(Other, Province(3), Type(Sappers));

        var events = Resolve(state, home: Orders(Home, move: [new([regiment], Province(3))]));

        Assert.Equal(Home, Assert.Single(events.OfType<BattleFoughtEvent>()).Winner);
        var surrendered = Assert.Single(events.OfType<RegimentsSurrenderedEvent>());
        Assert.Equal((Other, Province(3)), (surrendered.Country, surrendered.Province));
        Assert.Equal([sapper], surrendered.Regiments);
        Assert.Null(state.GetRegiment(sapper));
        Assert.Empty(events.OfType<RegimentsRetreatedEvent>());
    }

    [Fact]
    public void AGeneralStandsAsideAndMovesWithItsSide()
    {
        // No combat numbers, so a General alone does not defend: the province falls
        // uncontested and the General falls back with nobody to fight for it.
        var state = AtWar(CreateState());
        var regiment = state.CreateRegiment(Home, Province(2), Type(Regulars));
        var general = state.CreateRegiment(Home, Province(2), Type(General));
        var theirs = state.CreateRegiment(Other, Province(3), Type(General));

        var events = Resolve(state, home: Orders(Home, move: [new([regiment, general], Province(3))]));

        Assert.Empty(events.OfType<BattleFoughtEvent>());
        Assert.True(Assert.Single(events.OfType<ProvinceCapturedEvent>()).Uncontested);
        Assert.Equal(Province(3), state.GetRegiment(general)!.Province);
        Assert.Equal(Province(4), state.GetRegiment(theirs)!.Province);
    }

    // ---- Capital ----------------------------------------------------------

    [Fact]
    public void ACapturedCapitalIsLostAndTheArmouryHasNowhereToMuster()
    {
        var state = AtWar(CreateState());
        state.SetProvinceOwner(Province(4), Home);
        var regiment = state.CreateRegiment(Home, Province(4), Type(Regulars));

        var events = Resolve(state, home: Orders(Home, move: [new([regiment], Province(5))]));

        var captured = Assert.Single(events.OfType<ProvinceCapturedEvent>());
        Assert.True(captured.CapitalLost);
        Assert.Null(state.GetCountryCapital(Other));

        var later = Resolve(state, other: new CountryTurnOrders(
            Other, regimentPurchases: [new(ArmyCategory.RegularInfantry, 1)]));
        Assert.Equal(
            RegimentPurchaseRefusal.NoCapital,
            Assert.Single(later.OfType<RegimentPurchaseRefusedEvent>()).Reason);
    }

    // ---- Healing ----------------------------------------------------------

    [Fact]
    public void ARegimentThatStaysHealsAHundredToTheCeilingAndOneThatMovesDoesNot()
    {
        var state = CreateState();
        var resting = state.CreateRegiment(Home, Province(0), Type(Regulars), health: 300);
        var nearlyWell = state.CreateRegiment(Home, Province(0), Type(Regulars), health: 450);
        var marching = state.CreateRegiment(Home, Province(0), Type(Regulars), health: 300);
        var well = state.CreateRegiment(Home, Province(0), Type(Regulars));

        var events = Resolve(state, home: Orders(Home, move: [new([marching], Province(1))]));

        Assert.Equal(400, state.GetRegiment(resting)!.Health);
        Assert.Equal(Regiment.MaxHealth, state.GetRegiment(nearlyWell)!.Health);
        Assert.Equal(300, state.GetRegiment(marching)!.Health);
        var healed = Assert.Single(events.OfType<RegimentsHealedEvent>());
        Assert.Equal(TurnPhase.Conflict, healed.Phase);
        Assert.Equal(Home, healed.Country);
        Assert.Equal([resting, nearlyWell], healed.Regiments);
        Assert.Equal(150, healed.HealthRestored);
        Assert.DoesNotContain(well, healed.Regiments);
    }

    [Fact]
    public void NothingHealedReportsNothing()
    {
        var state = CreateState();
        _ = state.CreateRegiment(Home, Province(0), Type(Regulars));

        Assert.Empty(Resolve(state).OfType<RegimentsHealedEvent>());
    }

    [Fact]
    public void AnAttackerDoesNotHealTheTurnItAttacks()
    {
        var state = AtWar(CreateState());
        var regiment = state.CreateRegiment(Home, Province(2), Type(Regulars), health: 300);

        var events = Resolve(state, home: Orders(Home, move: [new([regiment], Province(3))]));

        Assert.Single(events.OfType<ProvinceCapturedEvent>());
        Assert.Equal(300, state.GetRegiment(regiment)!.Health);
        Assert.Empty(events.OfType<RegimentsHealedEvent>());
    }

    // ---- Helpers ----------------------------------------------------------

    private static readonly CivilianTypeId Farmer = new(0);

    private static BattleFoughtEvent Defend(int type, int fort = 0)
    {
        var state = AtWar(CreateState());
        state.SetFortLevel(Province(3), fort);
        var regiment = state.CreateRegiment(Home, Province(2), Type(Regulars));
        _ = state.CreateRegiment(Other, Province(3), Type(type));
        return Assert.Single(Resolve(state, 7, home: Orders(Home, move: [new([regiment], Province(3))]))
            .OfType<BattleFoughtEvent>());
    }

    private static long DefenderHealth(BattleFoughtEvent battle) =>
        battle.Participants.Single(static item => item.Side == BattleSide.Defender).HealthAfter;

    private static int Collected(IReadOnlyList<TurnEvent> events, CountryId country) =>
        events.OfType<ResourceExtractedEvent>().Single(item => item.Country == country).CollectedCellCount;

    private static void AssertRefused(IReadOnlyList<TurnEvent> events, RegimentMoveRefusal reason)
    {
        var refused = Assert.Single(events.OfType<RegimentMoveRefusedEvent>());
        Assert.Equal(reason, refused.Reason);
        Assert.Equal(TurnPhase.Conflict, refused.Phase);
    }

    private static ArmyTypeId Type(int value) => new(value);

    private static ProvinceId Province(int value) => new(value);

    private static CountryTurnOrders Orders(CountryId country, IEnumerable<RegimentMoveOrder>? move = null) =>
        new(country, regimentMoves: move);

    private static IReadOnlyList<TurnEvent> Resolve(
        WorldState state,
        ulong seed = 0,
        CountryTurnOrders? home = null,
        CountryTurnOrders? other = null,
        CountryTurnOrders? third = null) =>
        TurnResolver.Resolve(
            state,
            new TurnOrders(
            [
                home ?? new CountryTurnOrders(Home),
                other ?? new CountryTurnOrders(Other),
                third ?? new CountryTurnOrders(Third),
            ]),
            seed).Events;

    /// <summary>Home declares on Other, and an empty turn puts the war in effect.</summary>
    private static WorldState AtWar(WorldState state)
    {
        state.SetRelationMode(Home, Other, CountryRelationMode.Hostile);
        _ = TurnResolver.Resolve(state, TurnOrders.Empty(3), 0);
        Assert.True(state.HasEffectiveHostility(Home, Other));
        return state;
    }

    /// <summary>
    /// Eight farm tiles in a row, one province each. Home holds 0–2 with its capital on
    /// 0; Other holds 3–5 with its capital on 5; Third holds 6 with its capital there;
    /// 7 has no owner. Grain on 1–5.
    /// </summary>
    private static WorldState CreateState()
    {
        var dimensions = new MapDimensions(Width, 1);
        var cells = Enumerable.Range(0, Width)
            .Select(index => new CellDefinition(
                new CellIndex(index),
                dimensions.GetCoordinate(new CellIndex(index)),
                new TerrainId(0),
                CellRegion.ForProvince(new ProvinceId(index)),
                index is >= 1 and <= 5 ? [new ResourceId(0)] : null,
                index is 0 or 5 or 6 ? SettlementSiteKind.Urban : SettlementSiteKind.None))
            .ToArray();

        var map = new MapDefinition(
            dimensions,
            cells,
            Enumerable.Range(0, Width)
                .Select(static index => new ProvinceDefinition(new ProvinceId(index), $"Province {index}")),
            [],
            [new ResourceDefinition(new ResourceId(0), new CommodityId(Grain), [1])],
            [new TerrainDefinition(new TerrainId(0), "Farm")]);

        CountryId? Owner(int index) => index switch
        {
            <= 2 => Home,
            <= 5 => Other,
            6 => Third,
            _ => null,
        };

        CountryId[] countries = [Home, Other, Third];
        var scenario = new ScenarioDefinition(
            "Battles",
            1816,
            Enumerable.Range(0, Width).Select(Owner),
            initialCountryCapitals:
            [
                new CountryCapital(Home, new CellIndex(0)),
                new CountryCapital(Other, new CellIndex(5)),
                new CountryCapital(Third, new CellIndex(6)),
            ],
            initialInventory: countries
                .Select(static country => new InitialCommodityStock(country, new CommodityId(Fish), 1000))
                .Append(new InitialCommodityStock(Other, new CommodityId(Armaments), 10)),
            initialWorkforce: [new InitialWorkforce(Other, 3, 0, 0)],
            initialCash: [new InitialCash(Other, 5000)]);

        static ArmyCombatStats Stats(decimal firepower, long defence, long? entrenched) =>
            new(firepower, firepower, 5, null, defence, entrenched, 4);

        ArmyTypeDefinition[] catalogue =
        [
            // The executable's Minutemen, Regulars and Sappers; the rest are shaped for tests.
            new(Type(Minutemen), "Minutemen", ArmyCategory.Militia, 1,
                movesByRail: false, leavesProvince: false, stats: Stats(5, 4, 5)),
            new(Type(Regulars), "Regulars", ArmyCategory.RegularInfantry, 1,
                purchase: new(WorkerGrade.Untrained, [new CommodityQuantity(new CommodityId(Armaments), 1)], 500),
                arms: 1, stats: Stats(10, 5, 6)),
            new(Type(Sappers), "Sappers", ArmyCategory.CombatEngineers, 1, stats: Stats(0, 3, 4)),
            new(Type(Bulwark), "Bulwark", ArmyCategory.HeavyInfantry, 1, stats: Stats(0, 1, 100)),
            new(Type(OpenBulwark), "Open Bulwark", ArmyCategory.HeavyInfantry, 1, stats: Stats(0, 1, null)),
            new(Type(Gunners), "Gunners", ArmyCategory.HeavyArtillery, 1, stats: Stats(0, 1, 100)),
            new(Type(General), "General", ArmyCategory.General, 1),
        ];

        var definition = new WorldDefinition(
            map,
            [
                new CountryDefinition(Home, "Home"),
                new CountryDefinition(Other, "Other"),
                new CountryDefinition(Third, "Third"),
            ],
            scenario,
            [
                new CommodityDefinition(new CommodityId(Armaments), "Armaments", CommodityCategory.Goods),
                new CommodityDefinition(new CommodityId(Grain), "Grain", CommodityCategory.Raw),
                new CommodityDefinition(new CommodityId(Fish), "Fish", CommodityCategory.Raw),
            ],
            [],
            [],
            extraction: new ExtractionSettings(Width),
            feeding: new FeedingSettings([new FoodPreference([new CommodityId(Fish)])], [1, 2, 4]),
            civilianTypes: [new CivilianTypeDefinition(Farmer, "Farmer", 3)],
            armyTypes: catalogue);
        return new WorldState(definition);
    }
}
