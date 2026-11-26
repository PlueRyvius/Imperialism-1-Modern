using Imperialism.Content;
using Imperialism.Core;
using Xunit;

namespace Imperialism.Presentation.Tests;

/// <summary>
/// Row 4e-1: overtures, declarations of war and trade policy in the draft, and the
/// preview that restates Core's Diplomacy phase. Every preview warning is checked
/// against the event Core actually emits, and every effect against the state the next
/// turn opens with.
/// </summary>
public sealed class DiplomacyOrdersTests
{
    private const string Scenario = "scenario.demo";
    private static readonly CountryId Blue = new(0);
    private static readonly CountryId Red = new(1);
    private static readonly CountryId Green = new(2);

    /// <summary>
    /// The turn-report world with posts priced and a Minor Nation, Green, on cell 3.
    /// Blue and Red are Great Powers, so they open holding a consulate and an embassy
    /// in each other; nobody holds anything in Green.
    /// </summary>
    private static CompiledWorldPackage Compile(long blueCash = 5000)
    {
        var document = TurnReportFixture.CreateDocument();
        AddDiplomacy(document);
        foreach (var scenario in document.Scenarios)
        {
            scenario.Cash =
            [
                new CountryCashContent { Country = "country.blue", Amount = blueCash },
                new CountryCashContent { Country = "country.red", Amount = 5000 },
            ];
        }

        return WorldContentCompiler.CompilePackage(document);
    }

    private static void AddDiplomacy(WorldContentDocument document)
    {
        document.Diplomacy = new DiplomacyContentSettings
        {
            ConsulateCost = 500, EmbassyCost = 5000, SubsidyPercents = [5, 10],
        };
        document.Countries =
        [
            .. document.Countries,
            new CountryContentDefinition { Key = "country.green", Name = "Green Duchy", IsGreatPower = false },
        ];
        if (document.Map.Cells.Length == 4)
        {
            document.Map.Provinces =
            [
                .. document.Map.Provinces,
                new NamedContentDefinition { Key = "province.green", Name = "Green Province" },
            ];
            document.Map.Cells[3].Region = new CellRegionContent { Province = "province.green" };
            document.Map.Cells[3].HasSettlementSite = true;
            foreach (var scenario in document.Scenarios)
            {
                scenario.ProvinceOwners =
                [
                    .. scenario.ProvinceOwners,
                    new ProvinceOwnerContent { Province = "province.green", Country = "country.green" },
                ];
                scenario.Capitals =
                [
                    .. scenario.Capitals,
                    new CountryCapitalContent { Country = "country.green", Cell = 3 },
                ];
            }
        }
    }

    private sealed class Game(CompiledWorldPackage package)
    {
        public CompiledWorldPackage Package { get; } = package;

        public WorldState State { get; } = new(package.GetWorld(Scenario));

        public OrdersDraft Draft { get; } = new(package.GetWorld(Scenario), Blue);

        public int Turn { get; private set; } = 1;

        public OrdersPreview Preview() => OrdersPreview.Create(Package, Scenario, State, Draft);

        /// <summary>Resolves the draft, checks the preview against Core, and advances.</summary>
        public IReadOnlyList<TurnEvent> EndTurn()
        {
            var preview = Preview().Diplomacy;
            var events = TurnResolver.Resolve(State, Draft.BuildTurnOrders(), (ulong)Turn).Events;
            AssertPreviewMatches(Package, preview, events);
            Draft.AdvanceTurn();
            Turn++;
            return events;
        }
    }

    /// <summary>
    /// The preview's warning for every drafted overture, declaration and policy is the
    /// refusal Core gives it, in the same order, and its cash is what Core charged.
    /// </summary>
    private static void AssertPreviewMatches(
        CompiledWorldPackage package, DiplomacyPreview preview, IReadOnlyList<TurnEvent> events)
    {
        string Key(CountryId id) => package.Catalog.GetKey(id);

        var overtures = events
            .Where(item => item is DiplomaticOvertureAcceptedEvent { Country.Value: 0 } or
                DiplomaticOvertureRefusedEvent { Country.Value: 0 })
            .Select(item => item switch
            {
                DiplomaticOvertureAcceptedEvent accepted =>
                    new DraftedOvertureView(Key(accepted.Target), accepted.Kind, accepted.CashCost, null),
                DiplomaticOvertureRefusedEvent refused =>
                    new DraftedOvertureView(Key(refused.Target), refused.Kind, 0, refused.Reason),
                _ => throw new InvalidOperationException(),
            });
        Assert.Equal(overtures, preview.Overtures);

        var policies = events
            .Where(item => item is TradePolicySetEvent { Country.Value: 0 } or TradePolicyRefusedEvent { Country.Value: 0 })
            .Select(item => item switch
            {
                TradePolicySetEvent set => new DraftedPolicyView(Key(set.Target), set.Policy, null),
                TradePolicyRefusedEvent refused => new DraftedPolicyView(Key(refused.Target), refused.Policy, refused.Reason),
                _ => throw new InvalidOperationException(),
            });
        Assert.Equal(policies, preview.Policies);

        var declarations = events
            .Where(item => item is WarDeclaredEvent { Country.Value: 0 } or WarDeclarationRefusedEvent { Country.Value: 0 })
            .Select(item => item switch
            {
                WarDeclaredEvent declared => (Key(declared.Target), (WarDeclarationRefusal?)null),
                WarDeclarationRefusedEvent refused => (Key(refused.Target), refused.Reason),
                _ => throw new InvalidOperationException(),
            });
        Assert.Equal(declarations, preview.Declarations.Select(row => (row.TargetKey, row.Warning)));

        // What each accepted declaration cut, against Core's own report of it.
        foreach (var war in preview.Declarations.Where(row => row.Warning is null))
        {
            var cut = events.OfType<DiplomaticTiesCutEvent>()
                .SingleOrDefault(item => item.Country == Blue && Key(item.Target) == war.TargetKey);
            Assert.Equal(cut?.PactBroken ?? false, war.Effects!.BreaksPact);
            Assert.Equal(cut?.PostsClosed ?? false, war.Effects.ClosesPosts);
            var cleared = events.OfType<TradePolicyClearedEvent>().ToArray();
            Assert.Equal(
                cleared.Any(item => item.Country == Blue && Key(item.Target) == war.TargetKey),
                war.Effects.ClearsOurPolicy);
            Assert.Equal(
                cleared.Any(item => item.Target == Blue && Key(item.Country) == war.TargetKey),
                war.Effects.ClearsTheirPolicy);
        }

        Assert.Equal(
            events.OfType<DiplomaticOvertureAcceptedEvent>().Where(item => item.Country == Blue).Sum(item => item.CashCost),
            preview.ClaimedByOvertures);
    }

    private static DiplomaticCountryView View(OrdersPreview preview, string key) =>
        preview.Diplomacy.Countries.Single(row => row.CountryKey == key);

    private static OvertureOptionView Option(OrdersPreview preview, string key, DiplomaticOvertureKind kind) =>
        View(preview, key).Overtures.Single(row => row.Kind == kind);

    [Fact]
    public void BuildMapsEachListOneToOneInDraftOrder()
    {
        var package = Compile();
        var draft = new OrdersDraft(package.GetWorld(Scenario), Blue);
        Assert.True(draft.IsEmpty);

        draft.SetOverture(DiplomaticOvertureKind.Embassy, Green, true);
        draft.SetOverture(DiplomaticOvertureKind.Consulate, Green, true);
        draft.SetOverture(DiplomaticOvertureKind.Consulate, Green, true);
        draft.SetDeclareWar(Red, true);
        draft.SetDeclareWar(Red, true);
        draft.SetTradePolicy(Red, TradePolicy.Subsidy(5));
        draft.SetTradePolicy(Green, TradePolicy.Boycott);
        Assert.False(draft.IsEmpty);

        var orders = draft.Build();
        Assert.Equal(
            [
                new DiplomaticOverture(DiplomaticOvertureKind.Embassy, Green),
                new DiplomaticOverture(DiplomaticOvertureKind.Consulate, Green),
            ],
            orders.Overtures);
        Assert.Equal([Red], orders.DeclareWar);
        Assert.Equal(
            [new TradePolicyOrder(Red, TradePolicy.Subsidy(5)), new TradePolicyOrder(Green, TradePolicy.Boycott)],
            orders.TradePolicies);

        // A new policy for a target replaces the old one, in its place.
        draft.SetTradePolicy(Red, TradePolicy.Boycott);
        Assert.Equal(
            [new TradePolicyOrder(Red, TradePolicy.Boycott), new TradePolicyOrder(Green, TradePolicy.Boycott)],
            draft.Build().TradePolicies);
        Assert.Equal(TradePolicy.Boycott, draft.GetTradePolicy(Red));

        // Taking each order back.
        draft.SetOverture(DiplomaticOvertureKind.Embassy, Green, false);
        draft.SetDeclareWar(Red, false);
        Assert.True(draft.ClearTradePolicy(Red));
        Assert.False(draft.ClearTradePolicy(Red));
        Assert.Null(draft.GetTradePolicy(Red));
        Assert.Equal([new DiplomaticOverture(DiplomaticOvertureKind.Consulate, Green)], draft.Build().Overtures);
        Assert.Empty(draft.Build().DeclareWar);
        Assert.Equal([new TradePolicyOrder(Green, TradePolicy.Boycott)], draft.Build().TradePolicies);

        draft.ClearDiplomacy();
        Assert.True(draft.IsEmpty);

        // Screen bugs throw; Core's refusals are left to Core.
        Assert.Throws<ArgumentException>(() => draft.SetTradePolicy(Red, TradePolicy.Subsidy(7)));
        Assert.Throws<ArgumentOutOfRangeException>(() => draft.SetDeclareWar(new CountryId(3), true));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => draft.SetOverture((DiplomaticOvertureKind)9, Red, true));
        draft.SetDeclareWar(Blue, true);
        Assert.Equal([Blue], draft.Build().DeclareWar);
    }

    [Fact]
    public void EndTurnClearsEveryDiplomaticOrder()
    {
        // Overtures and declarations are made at End Turn (manual 3466-3469, 3478-3479);
        // a policy change stands in Core once made, so nothing is carried.
        var game = new Game(Compile());
        game.Draft.SetOverture(DiplomaticOvertureKind.Consulate, Green, true);
        game.Draft.SetDeclareWar(Red, true);
        game.Draft.SetTradePolicy(Red, TradePolicy.Subsidy(10));
        _ = game.EndTurn();

        Assert.True(game.Draft.IsEmpty);
        Assert.Empty(game.Draft.Overtures);
        Assert.Empty(game.Draft.DeclareWar);
        Assert.Empty(game.Draft.TradePolicies);
    }

    [Fact]
    public void AnUntouchedDraftStillResolvesLikeEmptyOrders()
    {
        var package = Compile();
        var world = package.GetWorld(Scenario);
        var draft = new OrdersDraft(world, Blue);
        var before = new WorldState(world);
        var after = new WorldState(world);
        var expected = TurnReportView.Create(
            package, Scenario, before, TurnResolver.Resolve(before, TurnOrders.Empty(world.Countries.Count), 1));
        var actual = TurnReportView.Create(
            package, Scenario, after, TurnResolver.Resolve(after, draft.BuildTurnOrders(), 1));
        Assert.Equal(expected.EventCount, actual.EventCount);
        Assert.Equal(Lines(expected), Lines(actual));
        Assert.Equal(before.GetCash(Blue), after.GetCash(Blue));
        Assert.Equal(before.RelationSequence, after.RelationSequence);
    }

    [Fact]
    public void ConsulateEmbassyAndPactAreBoughtInTurnAndOpenNextTurn()
    {
        var game = new Game(Compile(blueCash: 6000));

        // Turn 1: an embassy before any consulate is refused; the consulate is bought.
        var preview = game.Preview();
        Assert.Equal(DiplomaticOvertureRefusal.NoConsulate, Option(preview, "country.green", DiplomaticOvertureKind.Embassy).Warning);
        Assert.Equal(500, Option(preview, "country.green", DiplomaticOvertureKind.Consulate).Price);
        Assert.True(Option(preview, "country.green", DiplomaticOvertureKind.Consulate).IsAccepted);
        game.Draft.SetOverture(DiplomaticOvertureKind.Embassy, Green, true);
        game.Draft.SetOverture(DiplomaticOvertureKind.Consulate, Green, true);
        Assert.Equal(DiplomaticOvertureRefusal.NoConsulate, game.Preview().Diplomacy.Overtures[0].Warning);
        Assert.Equal(500, game.Preview().Diplomacy.ClaimedByOvertures);
        var events = game.EndTurn();
        Assert.Contains(events, item => item is DiplomaticOvertureAcceptedEvent { Kind: DiplomaticOvertureKind.Consulate, CashCost: 500 });
        Assert.True(game.State.HoldsDiplomaticTie(Blue, Green, DiplomaticTie.Consulate));
        Assert.False(game.State.HoldsDiplomaticTie(Blue, Green, DiplomaticTie.Embassy));

        // Turn 2: a pact before any embassy is refused; the embassy is bought.
        preview = game.Preview();
        Assert.True(View(preview, "country.green").OurConsulate);
        Assert.Equal(DiplomaticOvertureRefusal.AlreadyEstablished, Option(preview, "country.green", DiplomaticOvertureKind.Consulate).Warning);
        Assert.Equal(DiplomaticOvertureRefusal.NoEmbassy, Option(preview, "country.green", DiplomaticOvertureKind.Pact).Warning);
        var cash = game.State.GetCash(Blue);
        game.Draft.SetOverture(DiplomaticOvertureKind.Pact, Green, true);
        game.Draft.SetOverture(DiplomaticOvertureKind.Embassy, Green, true);
        _ = game.EndTurn();
        Assert.Equal(cash - 5000, game.State.GetCash(Blue));
        Assert.True(game.State.HoldsDiplomaticTie(Blue, Green, DiplomaticTie.Embassy));
        Assert.False(game.State.HoldsDiplomaticTie(Blue, Green, DiplomaticTie.Pact));

        // Turn 3: the pact is free; one to a Great Power is refused.
        preview = game.Preview();
        Assert.Equal(DiplomaticOvertureRefusal.TargetIsAGreatPower, Option(preview, "country.red", DiplomaticOvertureKind.Pact).Warning);
        Assert.Equal(0, Option(preview, "country.green", DiplomaticOvertureKind.Pact).Price);
        game.Draft.SetOverture(DiplomaticOvertureKind.Pact, Green, true);
        game.Draft.SetOverture(DiplomaticOvertureKind.Pact, Red, true);
        cash = game.State.GetCash(Blue);
        _ = game.EndTurn();
        Assert.Equal(cash, game.State.GetCash(Blue));
        Assert.True(game.State.HoldsDiplomaticTie(Blue, Green, DiplomaticTie.Pact));
        Assert.True(View(game.Preview(), "country.green").OurPact);
    }

    [Fact]
    public void TheTreasuryAfterOverturesIsTheResolvedTreasury()
    {
        // Two overtures: a consulate Core accepts and an embassy it refuses for want of
        // a consulate. Nothing else in this world moves Blue's cash, so the screen's
        // figure must be the treasury the next turn opens with.
        var game = new Game(Compile(blueCash: 5000));
        game.Draft.SetOverture(DiplomaticOvertureKind.Consulate, Green, true);
        game.Draft.SetOverture(DiplomaticOvertureKind.Embassy, Green, true);
        var preview = game.Preview().Diplomacy;
        Assert.Equal(5000, preview.CashAtOpen);
        Assert.Equal(0, preview.ClaimedBeforeDiplomacy);
        Assert.Equal(500, preview.ClaimedByOvertures);
        Assert.Equal(4500, preview.CashAfterOvertures);

        var events = game.EndTurn();
        Assert.Equal(2, events.Count(item => item is DiplomaticOvertureAcceptedEvent or DiplomaticOvertureRefusedEvent));
        Assert.Equal(preview.CashAfterOvertures, game.State.GetCash(Blue));
    }

    [Fact]
    public void AnOvertureTheTreasuryCannotPayIsRefusedAndCostsNothing()
    {
        var game = new Game(Compile(blueCash: 400));
        game.Draft.SetOverture(DiplomaticOvertureKind.Consulate, Green, true);
        Assert.Equal(DiplomaticOvertureRefusal.NotEnoughCash, game.Preview().Diplomacy.Overtures.Single().Warning);
        var before = game.State.GetCash(Blue);
        var events = game.EndTurn();
        Assert.Contains(events, item => item is DiplomaticOvertureRefusedEvent { Reason: DiplomaticOvertureRefusal.NotEnoughCash });
        Assert.False(game.State.HoldsDiplomaticTie(Blue, Green, DiplomaticTie.Consulate));
        Assert.Equal(before, game.State.GetCash(Blue));
    }

    [Fact]
    public void WarBetweenGreatPowersClosesThePostsClearsPoliciesAndBoycottsTrade()
    {
        var game = new Game(Compile());

        // Turn 1: a standing subsidy toward Red, so the war has a policy to clear.
        game.Draft.SetTradePolicy(Red, TradePolicy.Subsidy(5));
        _ = game.EndTurn();
        Assert.Equal(TradePolicy.Subsidy(5), game.State.GetTradePolicy(Blue, Red));

        // Turn 2: declare. The preview says what the war cuts before it is drafted.
        var red = View(game.Preview(), "country.red");
        Assert.True(red.IsGreatPower);
        Assert.False(red.IsAtWar);
        Assert.True(red.OurConsulate && red.OurEmbassy && red.TheirConsulate && red.TheirEmbassy);
        Assert.False(red.War.IsDrafted);
        Assert.Equal(new WarEffectsView(false, true, true, false, true), red.War.Effects);

        game.Draft.SetDeclareWar(Red, true);
        game.Draft.SetOverture(DiplomaticOvertureKind.Peace, Red, true);
        var drafted = View(game.Preview(), "country.red");
        Assert.True(drafted.War.IsDrafted);
        Assert.Equal(DiplomaticOvertureRefusal.NotAtWar, drafted.Overtures.Single(row => row.Kind == DiplomaticOvertureKind.Peace).Warning);
        var cash = game.State.GetCash(Blue);
        var events = game.EndTurn();
        Assert.Equal(cash, game.State.GetCash(Blue));
        Assert.Contains(events, item => item is WarDeclaredEvent);
        Assert.Contains(events, item => item is DiplomaticTiesCutEvent { PostsClosed: true });
        Assert.Contains(events, item => item is TradePolicyClearedEvent);

        // Turn 3 opens at war: no posts either way, no policy, trade boycotted.
        Assert.True(game.State.HasEffectiveHostility(Blue, Red));
        Assert.False(game.State.HoldsDiplomaticTie(Blue, Red, DiplomaticTie.Consulate));
        Assert.False(game.State.HoldsDiplomaticTie(Red, Blue, DiplomaticTie.Embassy));
        Assert.Equal(TradePolicy.None, game.State.GetTradePolicy(Blue, Red));
        Assert.True(game.State.IsTradeBoycotted(Blue, Red));
        red = View(game.Preview(), "country.red");
        Assert.True(red.IsAtWar);
        Assert.True(red.IsTradeBoycotted);
        Assert.Equal(TradePolicyRefusal.NoPost, red.PolicyUnavailable);

        // Declaring again is refused; peace to a Great Power that offers none back is
        // declined, and an embassy across a war is refused.
        Assert.Equal(WarDeclarationRefusal.AlreadyAtWar, red.War.Warning);
        Assert.Equal(DiplomaticOvertureRefusal.Declined, red.Overtures.Single(row => row.Kind == DiplomaticOvertureKind.Peace).Warning);
        Assert.Equal(DiplomaticOvertureRefusal.AtWar, red.Overtures.Single(row => row.Kind == DiplomaticOvertureKind.Consulate).Warning);
        game.Draft.SetDeclareWar(Red, true);
        game.Draft.SetOverture(DiplomaticOvertureKind.Peace, Red, true);
        game.Draft.SetOverture(DiplomaticOvertureKind.Consulate, Red, true);
        game.Draft.SetTradePolicy(Red, TradePolicy.Boycott);
        events = game.EndTurn();
        Assert.Contains(events, item => item is WarDeclarationRefusedEvent { Reason: WarDeclarationRefusal.AlreadyAtWar });
        Assert.Contains(events, item => item is TradePolicyRefusedEvent { Reason: TradePolicyRefusal.NoPost });
        Assert.True(game.State.HasEffectiveHostility(Blue, Red));
        Assert.False(game.State.HoldsDiplomaticTie(Blue, Red, DiplomaticTie.Consulate));
    }

    [Fact]
    public void PeaceWithAMinorNationIsAcceptedAndEndsTheWarNextTurn()
    {
        var game = new Game(Compile());

        // Turn 1: war on Green, a minor: no posts to close, no pact to break.
        var green = View(game.Preview(), "country.green");
        Assert.False(green.IsGreatPower);
        Assert.Equal(new WarEffectsView(false, false, false, false, true), green.War.Effects);
        game.Draft.SetDeclareWar(Green, true);
        _ = game.EndTurn();

        // Turn 2: the war is in effect; peace offered to a minor is accepted.
        green = View(game.Preview(), "country.green");
        Assert.True(green.IsAtWar);
        Assert.False(green.IsPeacePending);
        Assert.True(green.Overtures.Single(row => row.Kind == DiplomaticOvertureKind.Peace).IsAccepted);
        Assert.True(green.PeaceLiftsBoycott);
        game.Draft.SetOverture(DiplomaticOvertureKind.Peace, Green, true);
        var events = game.EndTurn();
        Assert.Contains(events, item => item is DiplomaticOvertureAcceptedEvent { Kind: DiplomaticOvertureKind.Peace, CashCost: 0 });

        // Turn 3 opens at peace.
        Assert.False(game.State.HasEffectiveHostility(Blue, Green));
        Assert.False(game.State.IsTradeBoycotted(Blue, Green));
        green = View(game.Preview(), "country.green");
        Assert.False(green.IsAtWar);
        Assert.Equal(
            DiplomaticOvertureRefusal.NotAtWar,
            green.Overtures.Single(row => row.Kind == DiplomaticOvertureKind.Peace).Warning);
    }

    [Fact]
    public void ADeclaredWarIsPendingUntilNextTurnAndBlocksOvertures()
    {
        var game = new Game(Compile());
        game.Draft.SetDeclareWar(Green, true);
        game.Draft.SetOverture(DiplomaticOvertureKind.Consulate, Green, true);
        Assert.Equal(DiplomaticOvertureRefusal.AtWar, game.Preview().Diplomacy.Overtures.Single().Warning);
        _ = game.EndTurn();
        Assert.False(game.State.HoldsDiplomaticTie(Blue, Green, DiplomaticTie.Consulate));
    }

    [Fact]
    public void EachPolicyKindIsSetOrRefusedAsCoreDoes()
    {
        var game = new Game(Compile());

        // Turn 1: subsidy to Red (posts open), boycott to Green (none): set and refused.
        var green = View(game.Preview(), "country.green");
        Assert.Equal(TradePolicyRefusal.NoPost, green.PolicyUnavailable);
        Assert.Null(View(game.Preview(), "country.red").PolicyUnavailable);
        game.Draft.SetTradePolicy(Red, TradePolicy.Subsidy(10));
        game.Draft.SetTradePolicy(Green, TradePolicy.Boycott);
        var red = View(game.Preview(), "country.red");
        Assert.Equal(TradePolicy.Subsidy(10), red.DraftedPolicy);
        Assert.Null(red.DraftedPolicyWarning);
        Assert.Equal(TradePolicyRefusal.NoPost, View(game.Preview(), "country.green").DraftedPolicyWarning);
        _ = game.EndTurn();
        Assert.Equal(TradePolicy.Subsidy(10), game.State.GetTradePolicy(Blue, Red));
        Assert.Equal(TradePolicy.None, game.State.GetTradePolicy(Blue, Green));
        Assert.Equal(TradePolicy.Subsidy(10), View(game.Preview(), "country.red").OurPolicy);

        // Turn 2: a boycott replaces it.
        game.Draft.SetTradePolicy(Red, TradePolicy.Boycott);
        _ = game.EndTurn();
        Assert.Equal(TradePolicy.Boycott, game.State.GetTradePolicy(Blue, Red));
        Assert.True(game.State.IsTradeBoycotted(Blue, Red));
        Assert.True(View(game.Preview(), "country.red").IsTradeBoycotted);

        // Turn 3: None cancels what stands.
        game.Draft.SetTradePolicy(Red, TradePolicy.None);
        _ = game.EndTurn();
        Assert.Equal(TradePolicy.None, game.State.GetTradePolicy(Blue, Red));
        Assert.False(game.State.IsTradeBoycotted(Blue, Red));
    }

    [Fact]
    public void AMinorNationsDraftIsRefusedForEveryOrderButWar()
    {
        var package = Compile();
        var state = new WorldState(package.GetWorld(Scenario));
        var draft = new OrdersDraft(state.Definition, Green);
        draft.SetOverture(DiplomaticOvertureKind.Consulate, Blue, true);
        draft.SetTradePolicy(Blue, TradePolicy.Boycott);
        draft.SetDeclareWar(Blue, true);
        var preview = OrdersPreview.Create(package, Scenario, state, draft).Diplomacy;
        Assert.False(preview.IsGreatPower);
        Assert.Equal(DiplomaticOvertureRefusal.NotAGreatPower, preview.Overtures.Single().Warning);
        Assert.Equal(TradePolicyRefusal.NotAGreatPower, preview.Policies.Single().Warning);
        Assert.Null(preview.Declarations.Single().Warning);
        var events = TurnResolver.Resolve(state, draft.BuildTurnOrders(), 1).Events;
        Assert.Contains(events, item => item is DiplomaticOvertureRefusedEvent { Reason: DiplomaticOvertureRefusal.NotAGreatPower });
        Assert.Contains(events, item => item is TradePolicyRefusedEvent { Reason: TradePolicyRefusal.NotAGreatPower });
        Assert.Contains(events, item => item is WarDeclaredEvent { Country.Value: 2 });
    }

    [Fact]
    public void ADraftedEmbassyComesOffTheCashCiviliansAndResearchSee()
    {
        // Overtures are paid in Diplomacy, the first phase: ahead of bids, civilians'
        // work and research. Blue opens turn 2 with 5,050 after a $500 consulate; the
        // embassy leaves 50, which pays for neither a Level I farm nor Plows.
        var package = UnitOrdersFixture.Compile(
            blueCash: 5550,
            customise: document =>
            {
                AddDiplomacy(document);
                document.Technologies = document.Technologies
                    .Select(item => item.Key == "technology.plows"
                        ? new TechnologyContentDefinition { Key = item.Key, Name = item.Name, Cost = 1000 }
                        : item)
                    .ToArray();
            });
        var state = UnitOrdersFixture.NewState(package);
        var draft = UnitOrdersFixture.NewDraft(state);
        var plows = UnitOrdersFixture.Technology(package, "technology.plows");
        draft.SetOverture(DiplomaticOvertureKind.Consulate, Green, true);
        _ = TurnResolver.Resolve(state, draft.BuildTurnOrders(), 1);
        draft.AdvanceTurn();
        Assert.Equal(5050, state.GetCash(Blue));

        draft.SetWork(UnitOrdersFixture.Farmer, UnitOrdersFixture.Cell(1));
        draft.SetTechnologyPurchase(plows, true);
        var without = OrdersPreview.Create(package, UnitOrdersFixture.Scenario, state, draft);
        Assert.Null(without.Civilians.Civilians.Single(row => row.Unit == UnitOrdersFixture.Farmer).Order!.Warning);
        Assert.True(without.Technology.Purchases.Single().IsAffordable);
        Assert.False(without.Technology.IsOverCash);

        draft.SetOverture(DiplomaticOvertureKind.Embassy, Green, true);
        var with = OrdersPreview.Create(package, UnitOrdersFixture.Scenario, state, draft);
        Assert.Equal(5000, with.Diplomacy.ClaimedByOvertures);
        Assert.Equal(5000, with.Civilians.ClaimedByOvertures);
        Assert.Equal(50, with.Civilians.CashBeforeCivilians);
        Assert.Equal(
            CivilianOrderRefusal.NotEnoughCash,
            with.Civilians.Civilians.Single(row => row.Unit == UnitOrdersFixture.Farmer).Order!.Warning);
        Assert.Equal(5000, with.Technology.ClaimedByOvertures);
        Assert.False(with.Technology.Purchases.Single().IsAffordable);
        Assert.True(with.Technology.IsOverCash);
        Assert.Equal(5000, with.Trade.ClaimedByOvertures);
        Assert.Equal(
            50,
            OrdersPreview.CreateUnitOrders(package, UnitOrdersFixture.Scenario, state, draft, UnitOrdersFixture.Farmer)
                .CashBeforeOrder);

        // Core agrees: the embassy is bought, the farm and the research are not.
        var events = TurnResolver.Resolve(state, draft.BuildTurnOrders(), 2).Events;
        Assert.Contains(events, item => item is DiplomaticOvertureAcceptedEvent { Kind: DiplomaticOvertureKind.Embassy });
        Assert.Contains(events, item => item is CivilianOrderRefusedEvent { Reason: CivilianOrderRefusal.NotEnoughCash });
        Assert.False(state.HasTechnology(Blue, plows));
        Assert.True(state.HoldsDiplomaticTie(Blue, Green, DiplomaticTie.Embassy));
    }

    private static string[] Lines(TurnReportView report) =>
        report.Phases.SelectMany(static phase => phase.Lines.Select(line => $"{phase.Phase}:{line}")).ToArray();
}
