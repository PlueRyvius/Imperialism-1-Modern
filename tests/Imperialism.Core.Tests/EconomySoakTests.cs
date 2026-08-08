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

    /// <summary>
    /// The one commodity here that never reaches the warehouse. Only the gold
    /// variant puts a deposit of it on the map, so every other run gathers none
    /// and is untouched by its existence.
    /// </summary>
    private const int Gold = 13;
    private const int CommodityCount = 14;

    /// <summary>
    /// Gold's deposit id. Every other deposit here reuses its commodity's id;
    /// gold cannot, because resource ids must be dense and the commodities run
    /// further than the deposits do.
    /// </summary>
    private const int GoldDeposit = 7;

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

    private const int Prospector = 4;
    private const int Engineer = 5;

    /// <summary>Farmers seeded per power. The original builds them; this fixture cannot.</summary>
    private const int FarmersPerPower = 3;

    /// <summary>
    /// Terrain ids. The default fixture uses only <see cref="Farmland"/>; the
    /// hidden-minerals variant adds hills, which are the only ground here a
    /// Prospector may search.
    /// </summary>
    private const int Farmland = 0;
    private const int BarrenHills = 1;

    /// <summary>
    /// Columns appended to each power's row when the hidden-minerals variant is
    /// asked for: a coal hill, a depot to lift it, a second coal hill, and one
    /// bare hill that hides nothing. The bare one matters — most searches find
    /// nothing, and a run in which every search succeeds would not be the game.
    /// </summary>
    private const int HiddenMineralColumns = 4;

    /// <summary>
    /// Columns appended when the Engineer variant is asked for: six grain tiles
    /// on railed ground with <b>no depot anywhere near them</b>, so every one is
    /// stranded until an Engineer builds one. They sit past the last base depot's
    /// reach on purpose — the point of the run is what happens when the network
    /// starts reaching further.
    /// </summary>
    private const int EngineerColumns = 6;

    /// <summary>Where the Engineer builds, in order. Each depot reaches one step either side.</summary>
    private static readonly int[] DepotSites = [23, 26];

    /// <summary>
    /// Columns appended when the gold variant is asked for: a depot and a gold
    /// tile beside it, already open at Level I so no Prospector is needed. One
    /// unit a turn a power, which the manual prices at $200.
    /// </summary>
    private const int GoldColumns = 2;

    /// <summary>What the manual pays for a unit of gold, stated outright.</summary>
    private const long GoldCashPerUnit = 200;

    /// <summary>
    /// What raising a tile costs, indexed by the level reached. The owner's
    /// recollection from play; see <c>docs/formulas/development.md</c>.
    /// </summary>
    private static readonly long[] ImprovementLadder = [0, 100, 1000, 3000];

    /// <summary>
    /// The work duration, spelled out so the run's shape is traceable to it.
    /// **Three, from observed play** — it used to be 1 and a flagged guess, and
    /// moving it moved every table this file publishes.
    /// </summary>
    private const int CivilianWorkTurns = 3;

    /// <summary>
    /// The three technologies this fixture models, in the order the real table
    /// puts them: Seed Drill, then Steel and Iron Plows, then Mechanical Reaper.
    /// </summary>
    /// <remarks>
    /// Only the Reaper gates anything here — grain at Level III — and the other two
    /// exist so the **prerequisite chain is the real one** rather than a convenient
    /// single purchase. Seed Drill is the one every power starts holding and is not
    /// for sale; the Plows are its dependent and the Reaper's prerequisite.
    /// </remarks>
    private const int SeedDrill = 0;
    private const int SteelAndIronPlows = 1;
    private const int MechanicalReaper = 2;

    /// <summary>
    /// The one class of ship this fixture models. Cargo 2, and three a power, which is what
    /// all three shipped skirmishes give — six holds each.
    /// </summary>
    private const int Trader = 0;

    /// <summary>
    /// Minor nations added when a run trades, so the market has a counterparty.
    /// </summary>
    /// <remarks>
    /// <b>They are a fixture standing in for an economy, and the income figures they
    /// produce are an upper bound rather than a measurement.</b> They own no land, no
    /// industry and no ships; they simply hold a treasury and bid for whatever is offered,
    /// which is the manual's role for them — "most goods go to the Minor Nations, not your
    /// competition" — with none of the behaviour that would decide *how much* they want.
    /// <para>
    /// Without them a closed world of seven identical powers trades nothing worth counting:
    /// every power holds the same surplus and wants the same things, so a sale is a swap and
    /// the net cash across the world is zero. That is a property of the fixture rather than
    /// of the model, and it is why they exist.
    /// </para>
    /// </remarks>
    private const int MinorNations = 3;

    /// <summary>What each minor nation has to spend over the century. A fixture number.</summary>
    private const long MinorNationTreasury = 400_000;

    /// <summary>
    /// The price list's own terms for the two that are for sale. **Not fixture
    /// numbers** — that is the point of the investing run: what a century buys has
    /// to be measured against real prices or it measures nothing.
    /// </summary>
    private const long PlowsCost = 3_000;
    private const long PlowsYear = 1831;
    private const long ReaperCost = 12_000;
    private const int ReaperYear = 1851;

    /// <summary>
    /// The year this fixture starts. **1840 rather than 1815, and only so the real
    /// arrival dates fall inside a hundred-quarter run.**
    /// </summary>
    /// <remarks>
    /// A turn is a quarter, so a hundred turns from 1815 stop in 1839 and the
    /// Mechanical Reaper's 1851 would be permanently out of reach — the gate would
    /// go back to being tested only shut, which is the whole thing this slice
    /// exists to end. From 1840 the run reaches 1864: the Plows have already
    /// arrived and are buyable on turn one, and the Reaper arrives on
    /// <see cref="ReaperArrivalTurn"/>, a little before halfway.
    /// <para>
    /// Nothing else in the fixture reads the year, so moving it moved no published
    /// number.
    /// </para>
    /// </remarks>
    private const int StartYear = 1840;

    /// <summary>
    /// The turn the Reaper becomes buyable: quarter one of 1851, counting from
    /// <see cref="StartYear"/>.
    /// </summary>
    private const int ReaperArrivalTurn = ((ReaperYear - StartYear) * 4) + 1;

    /// <summary>The turn the Reaper is handed over in the granting run.</summary>
    private const int UnlockTurn = 50;

    /// <summary>
    /// A deposit and the civilian that improves it, on this fixture's own yield
    /// curve. The curve is not the manual's — nothing here starts at 1 — and is
    /// deliberately harsher, so the economy has to work for its food.
    /// </summary>
    /// <remarks>
    /// Grain's top rung is gated, and nothing else is. That is enough to show a
    /// ceiling lifting mid-run without turning this fixture into a second
    /// technology test; <see cref="TechnologyGateTests"/> covers the rule
    /// itself.
    /// </remarks>
    private static ResourceDefinition Deposit(int resource, int improver) =>
        new(new ResourceId(resource),
            new CommodityId(resource),
            [0, 1, 2, 3],
            null,
            new CivilianTypeId(improver),

            // The manual's five: a Miner's deposits have to be found first. The
            // existing rows are all authored at level 1, so a mine already
            // stands on them and no survey is needed — this changes nothing for
            // the runs above.
            requiresDiscovery: improver == Miner,
            technologyByDevelopmentLevel: resource == Grain
                ? [null, null, null, new TechnologyId(MechanicalReaper)]
                : null);

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
    /// The turn each stage lands on depends on the work duration and on this
    /// fixture's arbitrary yield curve, so those are reported.
    /// </para>
    /// <para>
    /// <b>The overshoot this run used to report is gone, and the reason is worth
    /// naming.</b> At a one-turn work duration the farms improved fast enough
    /// (105 tiles) for the population to reach 84 and then outrun its own food,
    /// so a fresh deficit opened on turn 14 — which was reported here as the
    /// manual's warning about growing too fast, arrived at rather than written
    /// in. At three turns the farms improve more slowly (70 tiles), the
    /// population settles at 77, and sickness never returns.
    /// </para>
    /// <para>
    /// <b>It is a knife edge rather than a reversal.</b> Both runs end on the
    /// same 42 grain a turn, and half the workforce wants grain specifically, so
    /// 84 workers need exactly 42 and 77 need 39. The overshoot was real and it
    /// depended on a number that has since been measured. Reported rather than
    /// engineered back in.
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
    /// A hundred turns on a network too small for the land it serves.
    /// </summary>
    /// <remarks>
    /// The first constraint between what the land yields and what industry gets.
    /// Every other run in this file carries everything it gathers; this one
    /// starts with capacity for a fraction of it and has to build its way out,
    /// spending the same lumber and steel its mills want.
    /// <para>
    /// <b>Reported, not asserted.</b> Whether a country claws its way to a
    /// working network is a balance question nobody has the evidence to settle,
    /// and the starting capacity is a guess. What is asserted is only that the
    /// constraint bites at all and that nothing carried exceeds what was
    /// gathered.
    /// </para>
    /// </remarks>
    [Fact]
    public void AHundredTurnsOnANetworkTooSmallForItsLand()
    {
        var state = CreateWorld(withTransportLimit: true, startingTransportCapacity: 10);
        var log = Run(state, orderPolicy: FoodFirstTransportPolicy, out var work);

        output.WriteLine(log);

        Assert.True(work.Wasted > 0, "Nothing was ever left on the ground, so nothing was scarce.");
        Assert.True(work.Carried > 0, "The network never carried anything at all.");
        Assert.True(
            work.Carried <= work.Gathered,
            $"Carried {work.Carried} of {work.Gathered} gathered.");
    }

    /// <summary>
    /// The choice a small network forces **on a country with nothing in the
    /// warehouse**: the same world, the same capacity, the sliders in the other
    /// order.
    /// </summary>
    /// <remarks>
    /// Food first keeps everybody fed and barely makes a thing, because the coal
    /// the steel mill wants is at the back of the queue and hardly ever arrives.
    /// Materials first feeds the mills and lets the railyard grow the network —
    /// and the workers pay for it in the meantime.
    /// <para>
    /// <b>The empty warehouse is doing a lot of the work here.</b> Give the same
    /// country the manual's opening stockpile and both orderings buy an adequate
    /// network within a few turns, after which nothing is scarce and the choice
    /// stops mattering — see
    /// <see cref="AStockpileMakesTheSliderOrderStopMattering"/>. So this is what
    /// the slider order is worth while capacity is genuinely tight, not a
    /// standing property of the game.
    /// </para>
    /// <para>
    /// <b>Reported, not asserted into a target.</b> Which is the better opening
    /// is a balance question nobody has the evidence to settle. What is asserted
    /// is only that the two differ, which is what proves the allocation order is
    /// load-bearing rather than decorative.
    /// </para>
    /// </remarks>
    [Fact]
    public void WhichSliderComesFirstDecidesWhatAnUnstockedCountryBecomes()
    {
        var foodFirst = CreateWorld(withTransportLimit: true, startingTransportCapacity: 10);
        var foodLog = Run(foodFirst, orderPolicy: FoodFirstTransportPolicy, out var food);

        var materialsFirst = CreateWorld(withTransportLimit: true, startingTransportCapacity: 10);
        var materialsLog = Run(materialsFirst, orderPolicy: MaterialsFirstTransportPolicy, out var materials);

        output.WriteLine("=== food first ===");
        output.WriteLine(foodLog);
        output.WriteLine("=== materials first ===");
        output.WriteLine(materialsLog);

        // Food first barely runs a mill; materials first runs them properly.
        //
        // **This used to be exactly zero and is now seven**, because three-turn
        // work means less grain per turn, which leaves a sliver of the network
        // free for coal. Seven cycles in a century against 1,386 is the same
        // conclusion arrived at less tidily, so the assertion states the ratio
        // rather than being weakened to "greater than".
        Assert.True(
            food.Produced * 100 < materials.Produced,
            $"Food-first produced {food.Produced} against materials-first {materials.Produced}.");
        Assert.True(materials.Produced > 0, "Materials-first never produced anything either.");

        // And only the one that fed its mills could ever build a railyard. This
        // one is still exactly zero, and it is the load-bearing half: a country
        // that never affords a railyard never grows its network.
        Assert.Equal(0, food.CapacityBuilt);
        Assert.True(
            materials.CapacityBuilt > 0,
            "The railyard was never built even with materials at the front of the queue.");
        Assert.True(materials.FinalCapacity > food.FinalCapacity);
    }

    /// <summary>
    /// **The stockpile is what makes the slider order stop mattering.** The same
    /// tight network as
    /// <see cref="WhichSliderComesFirstDecidesWhatAnUnstockedCountryBecomes"/>,
    /// with the opening lumber and steel the manual says a power begins with.
    /// </summary>
    /// <remarks>
    /// Both orderings converge: each buys an adequate network within a few turns,
    /// after which nothing is scarce and the choice stops paying. Materials first
    /// is then simply *worse*, because it still costs workers on turn one and
    /// reaches nowhere the other ordering does not.
    /// <para>
    /// This run exists because <c>docs/formulas/transport.md</c> published its
    /// numbers and nothing in the suite produced them — a table no test can
    /// reproduce is a hypothesis, by this project's own rule. It is asserted
    /// comparatively; the figures are reported.
    /// </para>
    /// </remarks>
    [Fact]
    public void AStockpileMakesTheSliderOrderStopMattering()
    {
        var foodFirst = CreateWorld(
            withTransportLimit: true, startingTransportCapacity: 10, startingStock: 20);
        var foodLog = Run(foodFirst, orderPolicy: FoodFirstTransportPolicy, out var food);

        var materialsFirst = CreateWorld(
            withTransportLimit: true, startingTransportCapacity: 10, startingStock: 20);
        var materialsLog = Run(
            materialsFirst, orderPolicy: MaterialsFirstTransportPolicy, out var materials);

        output.WriteLine("=== food first, stocked ===");
        output.WriteLine(foodLog);
        output.WriteLine("=== materials first, stocked ===");
        output.WriteLine(materialsLog);

        // Stocked, food first can now do everything materials first can — which
        // is the whole point, and the opposite of the unstocked pair above.
        Assert.True(food.Produced > 0, "Food-first still produced nothing even with a stockpile.");
        Assert.True(food.CapacityBuilt > 0, "Food-first still never built a railyard.");

        // And it costs no workers doing it, where materials first still does.
        Assert.True(
            food.LastTurnWorkers >= materials.LastTurnWorkers,
            $"Food-first ended with {food.LastTurnWorkers} workers against " +
            $"materials-first {materials.LastTurnWorkers}.");

        // Converged: both carry nearly everything, so the ordering stops paying.
        Assert.True(
            food.Carried * 10 > materials.Carried * 9,
            $"Carried {food.Carried} food-first against {materials.Carried} materials-first.");
    }

    /// <summary>
    /// A network below subsistence cannot dig itself out, and that is a property
    /// of the model rather than of this fixture.
    /// </summary>
    /// <remarks>
    /// Escaping needs a railyard; a railyard needs lumber and steel; those need
    /// timber and coal carried; and every unit carried is one not carrying food.
    /// Set capacity under what the workforce eats and the country falls to the
    /// headcount its network can feed and stays there, with no route back however
    /// long it runs — this is a hundred turns of it. Even carrying food first,
    /// which is the most forgiving order there is.
    /// <para>
    /// Reported rather than asserted as desirable, but worth knowing before
    /// anyone picks a starting capacity for real content: it means the guessed
    /// default is not a balance knob but a viability threshold.
    /// </para>
    /// </remarks>
    /// <summary>
    /// **The starting stockpile is what makes a small network survivable**, and
    /// finding that out corrected a claim this file used to make.
    /// </summary>
    /// <remarks>
    /// Both runs are the same starved network — four points a power, under the
    /// seven its workforce eats. The only difference is whether the warehouse
    /// begins with the lumber and steel the manual says a power starts with:
    /// "you must construct a lumber and steel mill with your initial stockpiles
    /// of lumber and steel".
    /// <para>
    /// With an empty warehouse the country is trapped — escaping needs a
    /// railyard, a railyard needs materials, materials need carrying, and every
    /// unit carried is one not carrying food. With a stockpile it buys its way
    /// out on the first turn and ends carrying almost everything it gathers.
    /// </para>
    /// <para>
    /// So the trap is a property of an empty warehouse rather than of a small
    /// network, which is the opposite of what this file concluded before the
    /// stockpile existed. The turn-one starvation survives either way: capacity
    /// bought on turn one does not carry until turn two, and the workforce eats
    /// on turn one regardless.
    /// </para>
    /// </remarks>
    [Fact]
    public void AStartingStockpileIsWhatMakesASmallNetworkSurvivable()
    {
        var bare = CreateWorld(withTransportLimit: true, startingTransportCapacity: 4);
        var bareLog = Run(bare, orderPolicy: FoodFirstTransportPolicy, out var without);

        var stocked = CreateWorld(
            withTransportLimit: true, startingTransportCapacity: 4, startingStock: 20);
        var stockedLog = Run(stocked, orderPolicy: FoodFirstTransportPolicy, out var with);

        output.WriteLine("=== empty warehouse ===");
        output.WriteLine(bareLog);
        output.WriteLine("=== stocked warehouse ===");
        output.WriteLine(stockedLog);

        // The empty warehouse cannot buy anything, ever.
        Assert.Equal(0, without.CapacityBuilt);
        Assert.Equal(0, without.Produced);
        Assert.True(without.LastTurnSick > 0, "The bare run was expected to stay permanently ill.");

        // The stocked one buys its way out and stops being ill at all.
        Assert.True(with.CapacityBuilt > 0, "Even with a stockpile the railyard was never built.");
        Assert.True(with.Produced > 0);
        Assert.Equal(0, with.LastTurnSick);
        Assert.True(
            with.Carried > without.Carried * 2,
            $"Carried {with.Carried} stocked against {without.Carried} bare.");
    }

    /// <summary>
    /// **What it costs to develop land, and what happens when nobody can pay.**
    /// The same farming world, priced, against the free control.
    /// </summary>
    /// <remarks>
    /// The treasury is fixed and there is no income at all, so improvement runs
    /// until the money is gone and then stops for the rest of the century.
    /// <para>
    /// <b>That is an artefact of missing trade, not a property of the model</b>,
    /// and the distinction matters because this fixture has set exactly this
    /// trap before: <c>transport.md</c> concluded that a small network could
    /// never recover, which was true only of a warehouse that started empty. The
    /// original's main income is selling commodities, and nothing here models
    /// it. Read this run as "development is now something a country has to
    /// afford", not as "a country cannot afford development".
    /// </para>
    /// </remarks>
    [Fact]
    public void PricingImprovementMakesDevelopmentSomethingACountryHasToAfford()
    {
        var free = CreateWorld();
        var freeLog = Run(free, orderPolicy: FarmingGrowthPolicy, out var unpriced);

        var priced = CreateWorld(withImprovementCost: true, startingCash: 5000);
        var pricedLog = Run(priced, orderPolicy: FarmingGrowthPolicy, out var charged);

        output.WriteLine("=== improvement free ===");
        output.WriteLine(freeLog);
        output.WriteLine("=== improvement priced, no income ===");
        output.WriteLine(pricedLog);

        // The control has to be a control: free improvement costs nothing.
        Assert.Equal(0, unpriced.FinalCash);

        // Priced, the treasury does the limiting rather than the Farmers.
        Assert.True(
            charged.Developed < unpriced.Developed,
            $"Pricing improvement changed nothing: {charged.Developed} tiles against " +
            $"{unpriced.Developed}.");
        Assert.True(charged.Developed > 0, "Nothing was improved at all, so nothing was afforded.");
        Assert.True(
            charged.FinalCash < Powers * 5000,
            "The treasury was never drawn on.");
    }

    /// <summary>
    /// **The closed loop.** A gold mine is the only income this engine has, and
    /// this is the first run in which anything needs one: the mine pays for the
    /// Farmers, and the Farmers feed the workers.
    /// </summary>
    /// <remarks>
    /// Both runs price improvement and start with the same treasury. The only
    /// difference is two extra columns per power — a depot and a gold tile
    /// already open at Level I — so one country has an income and the other does
    /// not.
    /// <para>
    /// <b>The extra development does not land on grain</b>, which is worth
    /// knowing before reading the numbers: grain's top rung is gated behind
    /// Mechanical Reaper and this run never grants it, so grain sits at 42 in
    /// both. The money buys fruit, cotton, timber and livestock instead. Cash
    /// and technology are separate ceilings and this run only lifts one.
    /// </para>
    /// <para>
    /// <b>What this run does not exercise is the carrying trade-off.</b> The
    /// fixture's network is unlimited here, so gold costs nothing to move and
    /// never competes with grain for the bar. In a capacity-limited world it
    /// would, and that is the more interesting version of this run; it wants a
    /// policy that allocates against a scarce network, which
    /// <see cref="Transporting"/> has and this one deliberately does not.
    /// </para>
    /// </remarks>
    [Fact]
    public void AGoldMinePaysForTheImprovementsThatReachIt()
    {
        var broke = CreateWorld(withImprovementCost: true, startingCash: 5000);
        var brokeLog = Run(broke, orderPolicy: FarmingGrowthPolicy, out var without);

        var earning = CreateWorld(
            withImprovementCost: true, startingCash: 5000, withGoldMine: true);
        var earningLog = Run(earning, orderPolicy: FarmingGrowthPolicy, out var with);

        output.WriteLine("=== priced, no income ===");
        output.WriteLine(brokeLog);
        output.WriteLine("=== priced, with a gold mine ===");
        output.WriteLine(earningLog);

        // The control has to be a control: no mine, no income, so the treasury
        // only ever falls.
        Assert.True(
            without.FinalCash < Powers * 5000,
            "The unmined control somehow ended richer than it started.");

        // The mine pays, and the money buys development the other run could not.
        Assert.True(
            with.Developed > without.Developed,
            $"The gold bought no development: {with.Developed} tiles against {without.Developed}.");
        Assert.True(
            with.Gathered > without.Gathered,
            $"The extra development gathered nothing: {with.Gathered} against {without.Gathered}.");

        // And the mine outearns the spend: the treasury ends far above where it
        // started, where the control's is empty.
        Assert.Equal(0, without.FinalCash);
        Assert.True(
            with.FinalCash > Powers * 5000,
            $"The mine ended with {with.FinalCash} against a {Powers * 5000} start.");
    }

    /// <summary>
    /// **What the Engineer costs the network.** Two runs of the same world, one
    /// where the Engineer stands still and one where it walks out past the last
    /// depot and builds two more.
    /// </summary>
    /// <remarks>
    /// The fixture's last six columns are grain on railed ground with no depot
    /// near them, so they are stranded until an Engineer builds one. Each depot
    /// reaches one step either side, and the treasury covers exactly two of them.
    /// <para>
    /// <b>The reach is real and large.</b> Two depots a power take the harvest
    /// from 15,841 to 23,814 over the century, and grain from 42 a turn to 126.
    /// Nothing but an Engineer can do that: every other civilian raises what a
    /// tile yields, and this raises how much of the map is a tile at all.
    /// </para>
    /// <para>
    /// <b>The prediction this run was written to confirm is wrong, and is
    /// retracted rather than softened.</b> The expectation — carried into this
    /// slice from <c>docs/formulas/transport.md</c> — was that gathering more
    /// without carrying more would push the waste figure up until a railyard
    /// caught up. It does not move at all: 35 either way. The railyard outruns
    /// the Engineer easily, which is the same "805 points of capacity is absurd"
    /// that <c>transport.md</c> already reports, seen from the other side. <b>The
    /// two halves of the Engineer's job do not in fact pull against each other
    /// here</b>, and they will not until something else competes for lumber and
    /// steel.
    /// </para>
    /// <para>
    /// <b>Reported, not asserted into a target.</b> What is asserted is that the
    /// two runs genuinely differ, that the new depots lit up ground that was
    /// stranded, that reaching further cost the treasury, and that the waste
    /// figure did <em>not</em> rise. See <c>docs/formulas/engineer.md</c>.
    /// </para>
    /// </remarks>
    [Fact]
    public void AnEngineerReachesFurtherThanTheNetworkCanCarry()
    {
        var idle = CreateWorld(
            withTransportLimit: true, startingTransportCapacity: 10, startingStock: 20,
            withEngineer: true);
        var idleLog = Run(idle, orderPolicy: FoodFirstTransportPolicy, out var standing);

        var extending = CreateWorld(
            withTransportLimit: true, startingTransportCapacity: 10, startingStock: 20,
            withEngineer: true);
        var extendingLog = Run(extending, orderPolicy: EngineeringTransportPolicy, out var building);

        output.WriteLine("=== Engineer idle ===");
        output.WriteLine(idleLog);
        output.WriteLine("=== Engineer building ===");
        output.WriteLine(extendingLog);

        // The control has to be a control. An Engineer given no orders builds
        // nothing and spends nothing.
        Assert.Equal(0, standing.Constructed);
        Assert.Equal(Powers * 6000, standing.FinalCash);

        // Two depots a power, and the treasury paid for them.
        Assert.Equal(Powers * 2, building.Constructed);
        Assert.True(
            building.FinalCash < standing.FinalCash,
            $"Reaching further cost nothing: {building.FinalCash} against {standing.FinalCash}.");

        // And the reach is real: ground nothing gathered is gathered now.
        Assert.True(
            building.Gathered > standing.Gathered,
            $"Extending the network gathered no more: {building.Gathered} against {standing.Gathered}.");

        // The retracted prediction. Half again as much harvest and not one more
        // unit left on the ground, because the railyard is unopposed in this
        // fixture and grows faster than the Engineer reaches. If something ever
        // competes for lumber and steel, this is the assertion that will move.
        Assert.True(
            building.Wasted <= standing.Wasted,
            $"Waste rose after all: {building.Wasted} against {standing.Wasted}.");
    }

    /// <summary>
    /// A network under what its workforce eats costs that workforce on the first
    /// turn, whatever is in the warehouse.
    /// </summary>
    /// <remarks>
    /// Capacity bought on turn one does not carry until turn two, and the
    /// workers eat on turn one regardless — so the opening headcount is set by
    /// the network a scenario hands you and nothing can be done about it that
    /// turn. That is what makes the guessed starting capacity worth getting
    /// right rather than merely plausible, even now a stockpile makes the rest
    /// of the century survivable.
    /// </remarks>
    [Fact]
    public void ANetworkUnderSubsistenceCostsWorkersOnTheFirstTurnRegardless()
    {
        var state = CreateWorld(
            withTransportLimit: true, startingTransportCapacity: 4, startingStock: 20);
        var log = Run(state, orderPolicy: FoodFirstTransportPolicy, out var work);

        output.WriteLine(log);

        // The fair start is [4, 2, 1] a power, so seven powers begin with 49.
        Assert.True(
            work.LastTurnWorkers is > 0 and < 49,
            $"The workforce ended at {work.LastTurnWorkers}, having started at 49.");
    }

    /// <summary>
    /// Prospecting end to end over a hundred turns: ground searched, a deposit
    /// found, a Miner sent to open it, and coal from a mine that did not exist
    /// at the start reaching the warehouse.
    /// </summary>
    /// <remarks>
    /// The world is the farming one plus four columns of barren hills per power
    /// — two carrying coal, one a depot to lift it, one bare. The bare hill is
    /// the honest part: in the shipped corpus only 449 of 2,860 barren hills
    /// hold anything, so a Prospector that always succeeds would be testing a
    /// game nobody plays.
    /// <para>
    /// <b>The order is asserted; the turn numbers are reported.</b> A tile
    /// cannot be mined before it has been searched, and coal from the new hills
    /// cannot arrive before both. When each lands depends on the guessed work
    /// duration, so it is printed rather than pinned.
    /// </para>
    /// </remarks>
    [Fact]
    public void AProspectorFindsCoalAndAMinerTurnsItIntoAWarehouse()
    {
        var state = CreateWorld(withHiddenMinerals: true);
        var log = Run(state, orderPolicy: ProspectingGrowthPolicy, out var work);

        output.WriteLine(log);

        Assert.True(work.Prospected > 0, "No ground was ever searched.");
        Assert.True(work.Revealed > 0, "No search ever found anything.");
        Assert.True(work.MinesOpened > 0, "Nothing found was ever mined.");

        // The chain, in order. Anything else means a Miner reached ground its
        // country could not see.
        Assert.NotNull(work.FirstProspected);
        Assert.NotNull(work.FirstMineOpened);
        Assert.True(
            work.FirstProspected < work.FirstMineOpened,
            $"A mine opened on turn {work.FirstMineOpened} on ground first searched " +
            $"on turn {work.FirstProspected}.");
    }

    /// <summary>
    /// A ceiling lifting mid-run. Grain's top rung is gated behind Mechanical
    /// Reaper, which nobody starts with and no research can earn, so it is
    /// handed to every power on turn 50.
    /// </summary>
    /// <remarks>
    /// This is the pattern for exercising any gate while acquisition does not
    /// exist, and the reason it is worth having: without it a gate is only ever
    /// tested closed. Oil is the obvious next use — no imported world can reach
    /// it at all today.
    /// <para>
    /// <b>The order is asserted; the turns are reported.</b> No tile may reach
    /// the top rung before the grant, and Farmers must be refused for want of
    /// knowledge before it. When the first one lands afterwards depends on the
    /// guessed work duration.
    /// </para>
    /// </remarks>
    [Fact]
    public void AGatedRungOpensWhenTheTechnologyArrives()
    {
        var state = CreateWorld();
        var log = Run(state, orderPolicy: FarmingGrowthPolicy, out var work, grantOnTurn: UnlockTurn);

        output.WriteLine(log);

        Assert.True(
            work.KnowledgeRefusals > 0,
            "No Farmer was ever turned back for want of Mechanical Reaper.");
        Assert.True(work.TopRungs > 0, "The gated rung was never reached even after the grant.");
        Assert.NotNull(work.FirstTopRung);
        Assert.True(
            work.FirstTopRung >= UnlockTurn,
            $"A tile reached the gated rung on turn {work.FirstTopRung}, before the grant on {UnlockTurn}.");
    }

    /// <summary>
    /// **Trade pays for the technology, and this is the run four documents were waiting
    /// for.** The same fixture, with a world market and something to sell into it.
    /// </summary>
    /// <remarks>
    /// <c>soak.md</c>, <c>money.md</c>, <c>development.md</c> and <c>technology.md</c> all
    /// carry the same caveat in different words — *"an artefact of missing trade, not a
    /// property of the model"* — and the technology slice added a fifth: a power that
    /// improves as it earns cannot afford a $12,000 Mechanical Reaper in a century. Every
    /// one of those was measured on an economy with no revenue but a gold mine.
    /// <para>
    /// The comparison is against <see cref="PowersThatInvestLiftTheCeilingThemselves"/>'s
    /// greedy run: the same ordinary treasury, the same improve-whenever-you-can policy,
    /// differing only in whether there is a market. If trade is worth anything, the run
    /// with one buys what the run without could not.
    /// </para>
    /// <para>
    /// <b>The minor nations are a fixture standing in for an economy</b>, so the income
    /// figure is an upper bound rather than a measurement. What is not a fixture is the
    /// constraint: six cargo holds a power a turn, and every sale to a minor nation spends
    /// the seller's own.
    /// </para>
    /// </remarks>
    [Fact]
    public void TradePaysForWhatAGoldMineCouldNot()
    {
        var trading = CreateWorld(
            withImprovementCost: true, startingCash: 5000, withGoldMine: true, withTrade: true);
        var tradingLog = Run(trading, orderPolicy: TradingGrowthPolicy, out var traded);

        // The control: the same ordinary treasury and the same greed, with no market.
        var closed = CreateWorld(
            withImprovementCost: true, startingCash: 5000, withGoldMine: true);
        var closedLog = Run(closed, orderPolicy: SpendingGrowthPolicy, out var isolated);

        output.WriteLine("=== with a world market ===");
        output.WriteLine(tradingLog);
        output.WriteLine("=== no market, gold mine only ===");
        output.WriteLine(closedLog);

        // The market did something, and the powers were on the selling side of it.
        Assert.True(traded.Sold > 0, "Nothing was ever sold.");
        Assert.True(
            traded.TradeIncome > 0,
            $"Selling {traded.Sold} units earned nothing.");

        // **The control could not afford the Reaper and the trading run can.** That is the
        // whole point: the closed economy's ceiling never lifts.
        Assert.Null(isolated.FirstReaperBought);
        Assert.Equal(0, isolated.TopRungs);
        Assert.NotNull(traded.FirstReaperBought);
        Assert.True(
            traded.TopRungs > 0,
            "The ceiling never lifted even with trade income behind it.");

        // Trade dwarfs the mine: a century of one gold tile is about 20,000 a power.
        Assert.True(
            traded.TradeIncome > Powers * 20_000,
            $"Trade earned {traded.TradeIncome}, no better than the mine it sits beside.");

        // **The merchant marine binds, which is what stops this being the railyard again.**
        // Six holds a power a turn against a warehouse that fills faster than that, so
        // there is always something left on the quay.
        Assert.True(
            traded.Unsold > 0,
            "Everything offered sold, so capacity never bound — check the fixture.");
        Assert.True(
            traded.HoldRefusals > 0,
            "No row was ever short of a hull, so merchant marine is unopposed.");
    }

    /// <summary>
    /// The control for the run above: the same world with the technology never
    /// granted. The ceiling holds for the whole hundred turns.
    /// </summary>
    [Fact]
    public void AGatedRungNeverOpensWithoutTheTechnology()
    {
        var state = CreateWorld();
        var log = Run(state, orderPolicy: FarmingGrowthPolicy, out var work);

        output.WriteLine(log);

        Assert.Equal(0, work.TopRungs);
        Assert.True(work.KnowledgeRefusals > 0, "Nothing was ever refused for want of knowledge.");
    }

    /// <summary>
    /// **The soak stops cheating.** The same ceiling, lifted by powers that pay for
    /// the technology out of a gold mine instead of being handed it on turn 50.
    /// </summary>
    /// <remarks>
    /// This is what the slice was for. Every gate in this project — three
    /// improvement rungs per deposit, four rail terrains, oil prospecting — could
    /// previously only be tested shut, because knowledge came from a `tech` record
    /// and nothing else; <see cref="AGatedRungOpensWhenTheTechnologyArrives"/> had
    /// to call <c>GrantTechnology</c> outright to see a gate open at all.
    /// <para>
    /// The chain is the real one at the real prices. Steel and Iron Plows cost
    /// 3,000 and arrived in 1831, so they are buyable on turn one from a 1840 start;
    /// Mechanical Reaper costs 12,000, wants the Plows, and does not arrive until
    /// 1851 — turn <see cref="ReaperArrivalTurn"/>. So <b>two different walls stand
    /// in the way and the run has to clear both</b>: the money, from a mine, and the
    /// calendar, which no amount of money moves.
    /// </para>
    /// <para>
    /// <b>The granting run is the control and the two must differ.</b> If a bought
    /// ceiling and a gifted one produced the same log, this run would be measuring
    /// nothing.
    /// </para>
    /// </remarks>
    [Fact]
    public void PowersThatInvestLiftTheCeilingThemselves()
    {
        // A treasury big enough that the calendar is the only thing left in the
        // way. **The figure is a dial, not evidence** — the starting treasury is
        // one of the seven engine defaults and a guess — and it is set here so the
        // run separates the two walls instead of blurring them together.
        var investing = CreateWorld(
            withImprovementCost: true, startingCash: 20_000, withGoldMine: true);
        var investingLog = Run(investing, orderPolicy: InvestingGrowthPolicy, out var invested);

        // The same world at the ordinary treasury, improving whenever it can and
        // buying out of the remainder.
        var spending = CreateWorld(
            withImprovementCost: true, startingCash: 5000, withGoldMine: true);
        var spendingLog = Run(spending, orderPolicy: SpendingGrowthPolicy, out var spent);

        // The control: the same world and the technology handed over free on 50.
        var granted = CreateWorld(
            withImprovementCost: true, startingCash: 20_000, withGoldMine: true);
        var grantedLog = Run(
            granted, orderPolicy: FarmingGrowthPolicy, out var gifted, grantOnTurn: UnlockTurn);

        output.WriteLine("=== funded and patient ===");
        output.WriteLine(investingLog);
        output.WriteLine("=== ordinary treasury, improves first ===");
        output.WriteLine(spendingLog);
        output.WriteLine("=== granted free on turn 50 ===");
        output.WriteLine(grantedLog);

        // Both technologies bought by every power, and paid for at list price.
        Assert.Equal(Powers * 2, invested.Bought);
        Assert.Equal(Powers * (PlowsCost + ReaperCost), invested.SpentOnResearch);

        // **The calendar is the only wall left.** The Plows are affordable at once
        // and the Reaper is bought the very quarter it arrives, not a turn earlier.
        Assert.Equal(1, invested.FirstPlowsBought);
        Assert.Equal(ReaperArrivalTurn, invested.FirstReaperBought);

        // The policy walks into the closed gate every turn until it opens, so the
        // refusals are the date being real rather than noise.
        Assert.True(
            invested.PurchaseRefusals > 0,
            "Nothing was ever refused, so no wall was ever hit.");

        // And the ceiling lifted, after the purchase rather than before it.
        Assert.True(invested.TopRungs > 0, "The gated rung was never reached despite buying it.");
        Assert.True(
            invested.FirstTopRung > invested.FirstReaperBought,
            $"A tile reached the gated rung on turn {invested.FirstTopRung}, before the Reaper " +
            $"was bought on {invested.FirstReaperBought}.");

        // **The money is a wall too, and this is where it shows.** A power on the
        // ordinary treasury that improves whenever it can buys the Plows and never
        // the Reaper: twelve thousand is more than a century of one gold mine has
        // left over once its Farmers have been paid.
        Assert.Equal(Powers, spent.Bought);
        Assert.NotNull(spent.FirstPlowsBought);
        Assert.Null(spent.FirstReaperBought);
        Assert.Equal(0, spent.TopRungs);

        // **The three runs differ**, so none is measuring another. The gifted run
        // pays nothing, buys nothing, and reaches its ceiling on its own turn.
        Assert.Equal(0, gifted.Bought);
        Assert.Equal(0, gifted.SpentOnResearch);
        Assert.NotEqual(gifted.FirstTopRung, invested.FirstTopRung);
        Assert.True(
            invested.FinalCash < gifted.FinalCash,
            $"Paying {invested.SpentOnResearch} for the ceiling left {invested.FinalCash} against " +
            $"the gifted run's {gifted.FinalCash}.");
    }

    /// <summary>
    /// The control for the run above. Identical world, and the Prospectors are
    /// never told to look — so the hills stay unsearched, no mine is ever
    /// opened, and the four extra columns pay nothing for a hundred turns.
    /// </summary>
    [Fact]
    public void HillsNobodySearchesStayWorthNothing()
    {
        var state = CreateWorld(withHiddenMinerals: true);
        var log = Run(state, orderPolicy: FarmingGrowthPolicy, out var work);

        output.WriteLine(log);

        Assert.Equal(0, work.Prospected);
        Assert.Equal(0, work.MinesOpened);

        // The Miner is idle for a different reason than the Prospector: the
        // farming policy does send it somewhere, and every hill it could open
        // is invisible, so it is refused.
        Assert.True(
            work.DiscoveryRefusals > 0,
            "No Miner was ever turned back for want of a survey.");
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
        long FirstTurnWorkers, long LastTurnWorkers,
        long Prospected, long Revealed, long MinesOpened, long DiscoveryRefusals,
        int? FirstProspected, int? FirstMineOpened,
        long KnowledgeRefusals, long TopRungs, int? FirstTopRung,
        long Carried, long Wasted, long CapacityBuilt, long FinalCapacity,
        long Constructed, long FinalCash,
        long Bought, long SpentOnResearch, long PurchaseRefusals,
        int? FirstPlowsBought, int? FirstReaperBought,
        long Sold, long TradeIncome, long Unsold, long HoldRefusals);

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
    /// <see cref="FarmingGrowthPolicy"/> plus prospecting: every idle Prospector
    /// searches the nearest unsearched hill, and every idle Miner opens the
    /// nearest coal somebody has already found. Same warning as the others — a
    /// fixture, not an AI.
    /// </summary>
    private static CountryTurnOrders ProspectingGrowthPolicy(WorldState state, CountryId country)
    {
        var farming = FarmingGrowthPolicy(state, country);
        var map = state.Definition.Map;

        // The farming policy sends every idle civilian to something its type
        // improves, and a Miner qualifies for the coal already on the rows. The
        // discovery chain wins the argument: its orders are dropped and rewritten
        // below, because one civilian may take only one order a turn.
        var retasked = state.GetCivilians(country)
            .Where(item => !item.IsBusy && Searches(state, item.Type) || item.Type == new CivilianTypeId(Miner))
            .Select(static item => item.Id)
            .ToHashSet();
        var kept = farming.CivilianWork.Where(item => !retasked.Contains(item.Unit)).ToArray();
        var taken = kept.Select(static item => item.Cell).ToHashSet();
        var work = kept.ToList();

        foreach (var civilian in state.GetCivilians(country).Where(static item => !item.IsBusy))
        {
            var kind = state.Definition.CivilianTypes[civilian.Type.Value].Work;
            if (kind != CivilianWorkKind.Prospect && civilian.Type != new CivilianTypeId(Miner))
            {
                continue;
            }

            var target = map.Cells.FirstOrDefault(cell =>
                cell.Region.Kind == CellRegionKind.Province &&
                state.GetProvinceOwner(cell.Region.Province) == country &&
                !taken.Contains(cell.Index) &&
                (kind == CivilianWorkKind.Prospect
                    ? map.GetTerrain(cell.Terrain)?.Prospecting is not null &&
                        !state.HasProspected(country, cell.Index)
                    : state.HasProspected(country, cell.Index) &&
                        state.GetCellDevelopment(cell.Index) == 0 &&
                        cell.Resources.Any(resource =>
                            map.Resources[resource.Value].ImprovedBy == civilian.Type)));
            if (target is null)
            {
                continue;
            }

            taken.Add(target.Index);
            work.Add(new CivilianWorkOrder(civilian.Id, target.Index));
        }

        return new CountryTurnOrders(
            country,
            farming.Production,
            farming.Expansions,
            farming.RecruitWorkers,
            civilianWork: work);
    }

    /// <summary>
    /// <see cref="FarmingGrowthPolicy"/> plus a Transport screen: move food
    /// first, then the industrial inputs, and spend anything spare on the
    /// railyard. A fixture, not an AI — but the ordering is the one choice here
    /// that matters, because a network too small to carry the harvest starves
    /// its workers before it stalls its mills.
    /// </summary>
    /// <summary>Food before materials, which is the cautious reading of the demand lines.</summary>
    private static CountryTurnOrders FoodFirstTransportPolicy(WorldState state, CountryId country) =>
        Transporting(state, country, materialsFirst: false);

    /// <summary>
    /// Materials before food: starve the workers a little to feed the railyard,
    /// and see whether the network can grow its way out.
    /// </summary>
    private static CountryTurnOrders MaterialsFirstTransportPolicy(WorldState state, CountryId country) =>
        Transporting(state, country, materialsFirst: true);

    private static CountryTurnOrders Transporting(
        WorldState state,
        CountryId country,
        bool materialsFirst)
    {
        var farming = FarmingGrowthPolicy(state, country);

        // The choice the network forces. Whatever is at the front of this list
        // gets carried; whatever is at the back is left on the ground.
        int[] priority = materialsFirst
            ? [Timber, Coal, Grain, Fruit, Livestock, Cotton, Iron]
            : [Grain, Fruit, Livestock, Cotton, Timber, Coal, Iron];
        var allocations = priority
            .Select(static commodity => new TransportAllocationOrder(
                new CommodityId(commodity), long.MaxValue / 4))
            .ToArray();

        // **This policy spends on the network rather than on goods.** It makes
        // only lumber and steel, which is what a railyard eats — ordering
        // furniture as well starves the yard, and a power on a small network
        // that keeps furnishing its houses never builds its way out. That is a
        // fixture choice, not a finding, but it is the one that makes the
        // railyard visible at soak scale.
        var production = new List<ProductionOrder>();
        foreach (var (recipe, input) in new[] { (LumberRecipe, Timber), (SteelRecipe, Coal) })
        {
            var affordable = state.GetAvailableQuantity(country, new CommodityId(input)) / 2;
            if (affordable > 0)
            {
                production.Add(new ProductionOrder(new ProductionRecipeId(recipe), affordable));
            }
        }

        // Keep a small reserve so the mills are not stripped bare. A starved
        // network accumulates materials slowly, so the reserve is low on purpose.
        var spare = Math.Min(
            state.GetAvailableQuantity(country, new CommodityId(Lumber)),
            state.GetAvailableQuantity(country, new CommodityId(Steel))) - 2;

        return new CountryTurnOrders(
            country,
            production,
            recruitWorkers: farming.RecruitWorkers,
            civilianWork: farming.CivilianWork,
            transport: allocations,
            buildTransportCapacity: Math.Max(0, spare));
    }

    private static bool Searches(WorldState state, CivilianTypeId type) =>
        state.Definition.CivilianTypes[type.Value].Work == CivilianWorkKind.Prospect;

    /// <summary>
    /// <see cref="FarmingGrowthPolicy"/> plus a power that actually invests, so the
    /// grain ceiling lifts because somebody paid for it rather than because a test
    /// handed it over. **Saves for the next technology instead of improving.**
    /// </summary>
    /// <remarks>
    /// It orders every unowned purchasable technology every turn and lets the engine
    /// refuse what is not yet buyable, which is what makes the refusal count worth
    /// reading: the policy walks into the closed gate every turn and the log shows
    /// the date being real.
    /// <para>
    /// The saving is the interesting half. <b>Improvement is charged during
    /// Development and research takes what is left</b> — the chosen contention rule
    /// — so a power that improves whenever it can never accumulates the twelve
    /// thousand a Mechanical Reaper wants.
    /// <see cref="SpendingGrowthPolicy"/> is exactly that power, and it never buys
    /// the Reaper at all. This one stops its Farmers while the money piles up, which
    /// is the trade the rule forces on a player: improve now, or improve higher
    /// later.
    /// </para>
    /// </remarks>
    private static CountryTurnOrders InvestingGrowthPolicy(WorldState state, CountryId country) =>
        Investing(state, country, saveForResearch: true);

    /// <summary>
    /// The same investor with no patience: it improves whenever it can and buys
    /// only from what is left over. **It never affords the Reaper**, which is the
    /// measured consequence of research being charged last.
    /// </summary>
    private static CountryTurnOrders SpendingGrowthPolicy(WorldState state, CountryId country) =>
        Investing(state, country, saveForResearch: false);

    /// <summary>
    /// <see cref="InvestingGrowthPolicy"/> plus selling the surplus. A fixture, not an AI:
    /// it offers everything the warehouse holds above what its workers will eat, and bids
    /// for nothing.
    /// </summary>
    /// <remarks>
    /// <b>Selling only is deliberate, and it is the honest half of the mechanism.</b> This
    /// fixture's seven powers are identical, so they hold identical surpluses and want
    /// identical things; a policy that also bid would have every power bidding for what
    /// every power was selling, and the run would measure the tie-break rather than the
    /// trade. What it does measure is real: whether a country with goods can turn them into
    /// the cash that buys technology.
    /// <para>
    /// It leaves the buyers to be the minor nations, which own no merchant marine — so
    /// every deal spends the <em>seller's</em> holds, and six holds a turn is the ceiling
    /// the whole run pushes against.
    /// </para>
    /// </remarks>
    private static CountryTurnOrders TradingGrowthPolicy(WorldState state, CountryId country)
    {
        var investing = Investing(state, country, saveForResearch: true);
        var offers = new List<TradeOrder>();
        foreach (var commodity in state.Definition.Commodities)
        {
            if (!commodity.IsTradable)
            {
                continue;
            }

            var held = state.GetAvailableQuantity(country, commodity.Id);
            if (held > 0)
            {
                offers.Add(new TradeOrder(commodity.Id, held));
            }
        }

        return new CountryTurnOrders(
            country,
            investing.Production,
            recruitWorkers: investing.RecruitWorkers,
            civilianWork: investing.CivilianWork,
            buyTechnology: investing.BuyTechnology,
            tradeOffers: offers);
    }

    /// <summary>
    /// A minor nation that buys whatever is going. It exists so the market has a
    /// counterparty at all, and it is a fixture rather than an AI.
    /// </summary>
    private static CountryTurnOrders BuyingPolicy(WorldState state, CountryId country)
    {
        var bids = state.Definition.Commodities
            .Where(static commodity => commodity.IsTradable)
            .Select(static commodity => new TradeOrder(commodity.Id, 99))
            .ToArray();
        return new CountryTurnOrders(country, tradeBids: bids);
    }

    private static CountryTurnOrders Investing(
        WorldState state,
        CountryId country,
        bool saveForResearch)
    {
        var farming = FarmingGrowthPolicy(state, country);
        var wanted = new List<TechnologyId>();
        long? saveFor = null;
        for (var index = 0; index < state.Definition.Technologies.Count; index++)
        {
            var definition = state.Definition.Technologies[index];
            var technology = new TechnologyId(index);
            if (definition.Cost is not { } cost || state.HasTechnology(country, technology))
            {
                continue;
            }

            wanted.Add(technology);

            // The cheapest thing still out of reach is what there is any point
            // banking for, whether or not it has arrived yet — a player who can
            // read the Investment screen knows what is coming and saves for it.
            if (cost > state.GetCash(country) && (saveFor is null || cost < saveFor))
            {
                saveFor = cost;
            }
        }

        return new CountryTurnOrders(
            country,
            farming.Production,
            recruitWorkers: farming.RecruitWorkers,
            civilianWork: saveForResearch && saveFor is not null ? null : farming.CivilianWork,
            buyTechnology: wanted);
    }

    /// <summary>
    /// <see cref="FoodFirstTransportPolicy"/> plus an Engineer walking out past
    /// the end of the depots and building more. A fixture, not an AI.
    /// </summary>
    /// <remarks>
    /// It works one site at a time and in a fixed order — deploy, then build,
    /// then move on — which is the simplest thing that extends a network. The
    /// treasury runs out after the second depot, which is the point: the
    /// Engineer stops and the run shows what the reach it bought is worth.
    /// </remarks>
    private static CountryTurnOrders EngineeringTransportPolicy(WorldState state, CountryId country)
    {
        var transporting = Transporting(state, country, materialsFirst: false);
        var width = state.Definition.Map.Dimensions.Width;
        var engineer = state.GetCivilians(country)
            .FirstOrDefault(item =>
                !item.IsBusy &&
                state.Definition.CivilianTypes[item.Type.Value].Work == CivilianWorkKind.Construct);
        var site = DepotSites
            .Select(column => new CellIndex((country.Value * width) + column))
            .Where(cell => !state.HasDepot(cell))
            .Select(static cell => (CellIndex?)cell)
            .FirstOrDefault();
        if (engineer is null || site is not { } target)
        {
            return transporting;
        }

        return new CountryTurnOrders(
            country,
            transporting.Production,
            recruitWorkers: transporting.RecruitWorkers,

            // The Engineer builds from where it stands, so reaching the site and
            // building on it are two turns rather than one.
            deployments: engineer.Cell == target
                ? null
                : [new CivilianDeployOrder(engineer.Id, target)],
            civilianWork: transporting.CivilianWork,
            transport: transporting.Transport,
            buildTransportCapacity: transporting.BuildTransportCapacity,
            engineerWork: engineer.Cell == target
                ? [new EngineerOrder(engineer.Id, target, EngineerConstruction.Depot)]
                : null);
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

    /// <summary>
    /// Runs the fixture forward, optionally handing every power a technology
    /// part way through.
    /// </summary>
    /// <remarks>
    /// <paramref name="grantOnTurn"/> is how a gate is exercised over time
    /// without a research system to open it: the ceiling is real for the first
    /// half of the run and lifts in the second. It is the pattern to reuse for
    /// every future gate — oil, which no imported world can otherwise reach, is
    /// the obvious next one.
    /// </remarks>
    private string Run(
        WorldState state,
        Func<WorldState, CountryId, CountryTurnOrders>? orderPolicy,
        out WorkDone work,
        int? grantOnTurn = null,
        int grantTechnology = MechanicalReaper)
    {
        long gathered = 0, eaten = 0, delivered = 0, produced = 0, built = 0;
        long recruited = 0, recruitAttempts = 0, developed = 0;
        int? firstDeveloped = null, firstSicknessFree = null, firstRecruited = null;
        int? sicknessReturned = null;
        long firstTurnGrain = 0, lastTurnGrain = 0, lastTurnSick = 0;
        long firstTurnWorkers = 0, lastTurnWorkers = 0;
        long prospected = 0, revealed = 0, minesOpened = 0, discoveryRefusals = 0;
        int? firstProspected = null, firstMineOpened = null;
        long knowledgeRefusals = 0, topRungs = 0;
        int? firstTopRung = null;
        long carried = 0, wasted = 0, capacityBuilt = 0, constructed = 0;
        long bought = 0, spentOnResearch = 0, purchaseRefusals = 0;
        int? firstPlowsBought = null, firstReaperBought = null;
        long sold = 0, tradeIncome = 0, unsold = 0, holdRefusals = 0;
        var hills = Enumerable.Range(0, state.Definition.Map.Dimensions.CellCount)
            .Select(static index => new CellIndex(index))
            .Where(cell => state.Definition.Map
                .GetTerrain(state.Definition.Map[cell].Terrain)?.Prospecting is not null)
            .ToHashSet();

        // Grain is the only deposit with a gated rung, so it is the only one
        // whose top level says anything about technology. Every other deposit
        // walks to 3 unhindered and would drown the signal.
        var grainCells = Enumerable.Range(0, state.Definition.Map.Dimensions.CellCount)
            .Select(static index => new CellIndex(index))
            .Where(cell => state.Definition.Map[cell].Resources.Contains(new ResourceId(Grain)))
            .ToHashSet();
        var report = new StringBuilder();
        report.AppendLine(
            "turn  workers  fed/sick/starved  labour   stock   capacity  pending  grain  levels");

        var previousDate = state.CurrentDate;

        for (var turn = 1; turn <= Turns; turn++)
        {
            // Before orders are written, so the turn it lands on is the first
            // whose orders could have been given knowing it.
            if (turn == grantOnTurn)
            {
                for (var index = 0; index < Powers; index++)
                {
                    state.GrantTechnology(new CountryId(index), new TechnologyId(grantTechnology));
                }
            }

            var labourBefore = Enumerable.Range(0, Powers)
                .Select(index => state.GetAvailableLabour(new CountryId(index))).ToArray();
            var capacityBefore = CapacitySnapshot(state);
            var workersBefore = Enumerable.Range(0, Powers)
                .Select(index => state.GetTotalWorkers(new CountryId(index))).ToArray();

            // Every country, not just the powers: a trading world carries minor nations
            // beyond index Powers, and they are the ones who bid.
            var orders = new TurnOrders(Enumerable.Range(0, state.Definition.Countries.Count)
                .Select(index =>
                {
                    var country = new CountryId(index);
                    if (index >= Powers)
                    {
                        return BuyingPolicy(state, country);
                    }

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
            carried += resolution.Events.OfType<CommoditiesTransportedEvent>()
                .Sum(item => item.Moved.Sum(q => q.Quantity));
            wasted += resolution.Events.OfType<CommoditiesTransportedEvent>()
                .Sum(item => item.Wasted.Sum(q => q.Quantity));
            capacityBuilt += resolution.Events.OfType<TransportCapacityBuiltEvent>()
                .Sum(item => item.ToCapacity - item.FromCapacity);
            constructed += resolution.Events.OfType<ConstructionCompletedEvent>().Count();
            foreach (var purchase in resolution.Events.OfType<TechnologyPurchasedEvent>())
            {
                bought++;
                spentOnResearch += purchase.Paid;
                if (purchase.Technology.Value == SteelAndIronPlows)
                {
                    firstPlowsBought ??= turn;
                }
                else if (purchase.Technology.Value == MechanicalReaper)
                {
                    firstReaperBought ??= turn;
                }
            }

            purchaseRefusals += resolution.Events.OfType<TechnologyPurchaseRefusedEvent>().Count();

            // Only the powers' side of the market is counted: a minor nation's spending is
            // fixture money, not a measurement.
            foreach (var deal in resolution.Events.OfType<CommodityTradedEvent>())
            {
                if (deal.Seller.Value < Powers)
                {
                    sold += deal.Quantity;
                    tradeIncome += deal.Total;
                }
            }

            foreach (var item in resolution.Events.OfType<TradeUnfilledEvent>())
            {
                if (item.Country.Value >= Powers)
                {
                    continue;
                }

                unsold += item.Requested - item.Settled;
                if (item.Reason == TradeRefusal.NoMerchantCapacity)
                {
                    holdRefusals++;
                }
            }
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

            var searches = resolution.Events.OfType<CellProspectedEvent>().ToArray();
            prospected += searches.Length;
            revealed += searches.Sum(static item => item.Revealed.Count);
            if (searches.Length > 0)
            {
                firstProspected ??= turn;
            }

            // Only the hills count as mines opened. The rows' own coal was
            // authored at level 1 and improving it is ordinary development.
            var opened = resolution.Events.OfType<CellDevelopedEvent>()
                .Count(item => item.FromLevel == 0 && hills.Contains(item.Cell));
            minesOpened += opened;
            if (opened > 0)
            {
                firstMineOpened ??= turn;
            }

            discoveryRefusals += resolution.Events.OfType<CivilianOrderRefusedEvent>()
                .Count(static item => item.Reason == CivilianOrderRefusal.DepositNotYetDiscovered);
            knowledgeRefusals += resolution.Events.OfType<CivilianOrderRefusedEvent>()
                .Count(static item => item.Reason == CivilianOrderRefusal.ImprovementTechnologyNotKnown);

            // The gated rung: grain at level 3, which needs the Reaper.
            var reachedTop = resolution.Events.OfType<CellDevelopedEvent>()
                .Count(item => item.ToLevel == 3 && grainCells.Contains(item.Cell));
            topRungs += reachedTop;
            if (reachedTop > 0)
            {
                firstTopRung ??= turn;
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
            firstTurnGrain, lastTurnGrain, lastTurnSick, firstTurnWorkers, lastTurnWorkers,
            prospected, revealed, minesOpened, discoveryRefusals,
            firstProspected, firstMineOpened,
            knowledgeRefusals, topRungs, firstTopRung,
            carried, wasted, capacityBuilt,
            Enumerable.Range(0, Powers).Sum(index => state.GetTransportCapacity(new CountryId(index))),
            constructed,
            Enumerable.Range(0, Powers).Sum(index => state.GetCash(new CountryId(index))),
            bought,
            spentOnResearch,
            purchaseRefusals,
            firstPlowsBought,
            firstReaperBought,
            sold,
            tradeIncome,
            unsold,
            holdRefusals);
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
        report.AppendLine(
            $"{prospected} tiles searched revealing {revealed} deposits, " +
            $"{minesOpened} mines opened, {discoveryRefusals} refused for want of a survey; " +
            $"first search turn {Or(firstProspected)}, first mine turn {Or(firstMineOpened)}");
        report.AppendLine(
            $"{topRungs} tiles reached the gated top rung, " +
            $"{knowledgeRefusals} refused for want of knowledge; " +
            $"first top rung turn {Or(firstTopRung)}");
        report.AppendLine(
            $"carried {carried} of {gathered} gathered, wasted {wasted}; " +
            $"built {capacityBuilt} points of capacity");
        report.AppendLine(
            $"{constructed} structures built by Engineers; " +
            $"treasuries hold {Enumerable.Range(0, Powers).Sum(index => state.GetCash(new CountryId(index)))}");
        report.AppendLine(
            $"{bought} technologies bought for {spentOnResearch}, " +
            $"{purchaseRefusals} purchases refused; " +
            $"first Plows turn {Or(firstPlowsBought)}, first Reaper turn {Or(firstReaperBought)}");
        report.AppendLine(
            $"sold {sold} units for {tradeIncome}, {unsold} offered and unsold, " +
            $"{holdRefusals} rows short of a cargo hold");
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
    /// <summary>
    /// A commodity the world market sees, at the price list's own figure and place in the
    /// commodity order — or an untradable one when the run does not want a market.
    /// </summary>
    /// <remarks>
    /// Gated so that every run published before trade existed keeps its numbers: a world
    /// with no prices trades nothing, which is exactly how those runs behaved.
    /// </remarks>
    private static CommodityDefinition Tradable(
        int id,
        string name,
        CommodityCategory category,
        long price,
        int order,
        bool withTrade) =>
        new(new CommodityId(id),
            name,
            category,
            worldPrice: withTrade ? price : null,
            tradeOrder: withTrade ? order : null);

    private static WorldState CreateWorld(
        bool withHiddenMinerals = false,
        bool withTransportLimit = false,
        long startingTransportCapacity = 0,
        long startingStock = 0,
        bool withEngineer = false,
        bool withImprovementCost = false,
        long startingCash = 0,
        bool withGoldMine = false,
        bool withTrade = false)
    {
        // Each power gets a row of 22 cells: a capital at column 0, then a
        // repeating deposit / depot / deposit run. A depot reaches one step, so
        // every deposit sits beside one and nothing is stranded — this fixture
        // is about the economy, not about connectivity, which has its own tests.
        //
        // The hidden-minerals variant appends four more columns rather than
        // editing these, so the two published runs above are byte for byte the
        // world they were reported against.
        const int baseWidth = 22;
        var engineerStart = baseWidth + (withHiddenMinerals ? HiddenMineralColumns : 0);
        var goldStart = engineerStart + (withEngineer ? EngineerColumns : 0);
        var width = goldStart + (withGoldMine ? GoldColumns : 0);
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

                var hidden = withHiddenMinerals && column >= baseWidth && column < engineerStart;
                var beyondTheDepots = withEngineer && column >= engineerStart && column < goldStart;
                var goldColumn = withGoldMine && column >= goldStart;
                var isCapital = column == 0;
                var isDepot = hidden
                    ? column == baseWidth + 1
                    : goldColumn
                        ? column == goldStart
                        : !isCapital && !beyondTheDepots && column % 3 == 2;

                ResourceId[] deposits = [];
                if (goldColumn && !isDepot)
                {
                    // Open at Level I, so it pays from turn one and needs no
                    // Prospector. The point of this run is the money, not the
                    // discovery chain.
                    deposits = [new ResourceId(GoldDeposit)];
                    development.Add(new InitialCellDevelopment(cell, 1));
                }
                else if (beyondTheDepots)
                {
                    // Grain on railed ground that nothing gathers. An Engineer
                    // is the only thing in the engine that can change that.
                    deposits = [new ResourceId(Grain)];
                    development.Add(new InitialCellDevelopment(cell, 1));
                }
                else if (hidden)
                {
                    // Undeveloped, so it yields nothing until a Miner opens it —
                    // and unsearched, so no Miner may go near it. The last
                    // column is left bare on purpose.
                    if (!isDepot && column != width - 1)
                    {
                        deposits = [new ResourceId(Coal)];
                    }
                }
                else if (!isCapital && !isDepot)
                {
                    deposits = [new ResourceId(depositCycle[deposited % depositCycle.Length])];
                    deposited++;

                    // Level 1: developed enough to yield, nowhere near improved.
                    development.Add(new InitialCellDevelopment(cell, 1));
                }

                cells.Add(new CellDefinition(
                    cell,
                    dimensions.GetCoordinate(cell),
                    new TerrainId(hidden || goldColumn ? BarrenHills : Farmland),
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

            if (withHiddenMinerals)
            {
                civilians.Add(new InitialCivilian(
                    new CountryId(power), new CivilianTypeId(Prospector), new CellIndex(power * width)));
                civilians.Add(new InitialCivilian(
                    new CountryId(power), new CivilianTypeId(Miner), new CellIndex(power * width)));
            }

            if (withEngineer)
            {
                civilians.Add(new InitialCivilian(
                    new CountryId(power), new CivilianTypeId(Engineer), new CellIndex(power * width)));
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

                // The one deposit whose commodity id is not its own, and the
                // only one that pays cash instead of filling a warehouse.
                new ResourceDefinition(
                    new ResourceId(GoldDeposit),
                    new CommodityId(Gold),
                    [0, 1, 2, 3],
                    null,
                    new CivilianTypeId(Miner),
                    requiresDiscovery: true),
            ],

            // Farmland is worked but never searched — it announces its crops by
            // being farmland. Hills are the only ground here that hides
            // anything, and they need no technology, matching the manual's
            // barren hills and mountains.
            //
            // Both carry rail with no technology behind it. The terrain gates
            // are a rule of their own and EngineerTests covers them; making this
            // fixture fight them too would only obscure what it is for. They do
            // carry the price list's real per-terrain prices, so a run that ever
            // starts laying track is charged for it — no run does today, and the
            // Engineer run's treasury column is depots and nothing else.
            [
                new TerrainDefinition(
                    new TerrainId(Farmland),
                    "Farmland",
                    isImprovable: true,
                    rail: new RailRule(cashCost: 100)),
                new TerrainDefinition(
                    new TerrainId(BarrenHills),
                    "Barren Hills",
                    isImprovable: true,
                    prospecting: ProspectingRule.Unrestricted,
                    rail: new RailRule(cashCost: 200)),
            ]);

        var scenario = new ScenarioDefinition(
            "Soak",
            StartYear,
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

            // Only the powers get the fair-start defaults. Minor nations own no land and
            // no industry — the same reason the original equips its Great Powers and not
            // its statelets.
            Enumerable.Range(0, Powers).Select(static index => new CountryId(index)),
            civilians,
            initialCash: Enumerable.Range(0, withTrade ? MinorNations : 0)
                .Select(static index => new InitialCash(
                    new CountryId(Powers + index), MinorNationTreasury)));

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
            // The seven powers, and — where a run trades — minor nations to trade with.
            // The manual's economy runs on them: "most goods go to the Minor Nations, not
            // your competition", and they own no merchant marine, so a Great Power selling
            // to one carries the cargo itself.
            Enumerable.Range(0, Powers)
                .Select(static index => new CountryDefinition(
                    new CountryId(index), $"Power {index}", isGreatPower: true))
                .Concat(Enumerable.Range(0, withTrade ? MinorNations : 0)
                    .Select(static index => new CountryDefinition(
                        new CountryId(Powers + index), $"Minor {index}"))),
            scenario,
            [
                // Raw food is untradable — "food resources cannot be traded on the world
                // market" — so grain, fruit and livestock carry no price whatever the
                // run asks for.
                new CommodityDefinition(new CommodityId(Grain), "Grain", CommodityCategory.Raw),
                new CommodityDefinition(new CommodityId(Fruit), "Fruit", CommodityCategory.Raw),
                new CommodityDefinition(new CommodityId(Livestock), "Livestock", CommodityCategory.Raw),
                Tradable(Cotton, "Cotton", CommodityCategory.Raw, 100, 9, withTrade),
                Tradable(Timber, "Timber", CommodityCategory.Raw, 100, 11, withTrade),
                Tradable(Coal, "Coal", CommodityCategory.Raw, 100, 12, withTrade),
                Tradable(Iron, "Iron", CommodityCategory.Raw, 100, 13, withTrade),
                Tradable(Fabric, "Fabric", CommodityCategory.Material, 300, 5, withTrade),
                Tradable(Lumber, "Lumber", CommodityCategory.Material, 300, 6, withTrade),
                Tradable(Steel, "Steel", CommodityCategory.Material, 300, 8, withTrade),
                Tradable(CannedFood, "Canned Food", CommodityCategory.Material, 100, 4, withTrade),
                Tradable(Clothing, "Clothing", CommodityCategory.Goods, 900, 0, withTrade),
                Tradable(Furniture, "Furniture", CommodityCategory.Goods, 900, 1, withTrade),

                // The manual's own rate. Gold never reaches the warehouse, so
                // every unit the network carries is cash instead — and it "cannot be
                // traded", so it is never on the roster.
                new CommodityDefinition(
                    new CommodityId(Gold), "Gold", CommodityCategory.Raw, GoldCashPerUnit),
            ],
            facilities,
            recipes,
            new ExtractionSettings(catchmentRadius: 1),
            // The price list's own costs, years and prerequisite chain. Seed Drill
            // has no price because every power already holds it, which is what
            // makes it a prerequisite and never a purchase.
            [
                new TechnologyDefinition(new TechnologyId(SeedDrill), "Seed Drill", null, 1815),
                new TechnologyDefinition(
                    new TechnologyId(SteelAndIronPlows),
                    "Steel and Iron Plows",
                    [new TechnologyId(SeedDrill)],
                    (int)PlowsYear,
                    PlowsCost),
                new TechnologyDefinition(
                    new TechnologyId(MechanicalReaper),
                    "Mechanical Reaper",
                    [new TechnologyId(SteelAndIronPlows)],
                    ReaperYear,
                    ReaperCost),
            ],
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
                new WorkforceDefault(untrained: 4, trained: 2, expert: 1),

                // "Every player always starts with the first two technologies."
                // Only Seed Drill of the two is in this catalog, and it is the
                // Plows' prerequisite — so the investing run's chain starts from
                // where a real power starts rather than from nothing.
                technologies: [new TechnologyId(SeedDrill)],

                // Three Traders a power, six cargo holds, which all three shipped
                // skirmishes agree on. Only given where a run trades, so nothing else
                // acquires a fleet it never uses.
                ships: withTrade ? [new ShipDefault(new ShipTypeId(Trader), 3)] : null,
                transportCapacity: withTransportLimit ? startingTransportCapacity : null,
                cash: withEngineer ? 6000 : startingCash == 0 ? null : startingCash,

                // The manual's initial stockpile of lumber and steel. Without
                // one a starved network cannot buy the railyard that would
                // unstarve it, which is a trap the original plainly does not
                // intend — see docs/formulas/transport.md.
                inventory: startingStock == 0
                    ? null
                    :
                    [
                        new CommodityQuantity(new CommodityId(Lumber), startingStock),
                        new CommodityQuantity(new CommodityId(Steel), startingStock),
                    ]),
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
                new CivilianTypeDefinition(
                    new CivilianTypeId(Prospector),
                    "Prospector",
                    CivilianWorkTurns,
                    CivilianWorkKind.Prospect),
                new CivilianTypeDefinition(
                    new CivilianTypeId(Engineer),
                    "Engineer",
                    CivilianWorkTurns,
                    CivilianWorkKind.Construct),
            ],
            transport: withTransportLimit
                ? new TransportSettings(
                    [
                        new CommodityQuantity(new CommodityId(Lumber), 1),
                        new CommodityQuantity(new CommodityId(Steel), 1),
                    ],
                    labourPerCapacityPoint: 2)
                : null,

            // Priced so that a treasury of 6,000 buys the two depots this run
            // needs and no more. Neither number is evidence; see
            // docs/formulas/engineer.md. Rail is priced on the terrain instead.
            construction: withEngineer ? new ConstructionSettings(1500, 2000) : null,
            improvement: withImprovementCost ? new ImprovementSettings(ImprovementLadder) : null,

            // One merchant class, because merchant marine is the only ship number trade
            // reads and the warships would contribute nothing but noise.
            shipTypes: withTrade
                ? [new ShipTypeDefinition(new ShipTypeId(Trader), "Trader", cargo: 2)]
                : null,
            trade: withTrade ? new ProportionalTradeMarket() : null));
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
