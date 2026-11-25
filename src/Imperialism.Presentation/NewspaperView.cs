using Imperialism.Content;
using Imperialism.Core;

namespace Imperialism.Presentation;

/// <summary>What a newspaper story is about, so the client can style or illustrate it without reading it.</summary>
/// <remarks>Declared in headline priority order, then the items. See <c>docs/formulas/newspaper.md</c>.</remarks>
public enum NewspaperStoryKind : byte
{
    // ---- Headlines (chosen rule N2) ----
    NewInvention,
    CountryEliminated,
    WarDeclared,
    PeaceMade,
    ProvinceCaptured,
    NavalBattle,
    Landing,
    LandBattle,
    MerchantIntercepted,
    Hunger,
    CiviliansLost,
    RegimentsLost,
    ShipsLost,
    TiesCut,

    // ---- Items: "the newsworthy doings of the little people" ----
    MarketReport,
    WorkersRecruited,
    ShipsLaunched,
    CiviliansArrived,
    RegimentsMustered,
    Investment,
    ConstructionCompleted,

    /// <summary>The fixed item a paper prints when nothing else qualified.</summary>
    QuietQuarter,
}

/// <summary>One finished newspaper story. Every string is complete; the client concatenates nothing.</summary>
public sealed record NewspaperStory(
    NewspaperStoryKind Kind, bool IsHeadline, string Headline, string Body);

/// <summary>
/// The newspaper one country reads at the start of a turn: the key events of the
/// quarter just resolved, chosen and ordered for that reader, detached from the world.
/// </summary>
/// <remarks>
/// "Each new turn begins with a newspaper summarising the key events of the past
/// three months" (manual, lines 557–558), and a story "that offers information,
/// provides a warning, or affects the play of the game appears under a bold headline"
/// (lines 494–498). Nothing of the original's selection has been recovered, so
/// <b>every rule here is chosen</b>, and each carries its label from
/// <c>docs/formulas/newspaper.md</c>:
/// <list type="bullet">
/// <item><b>N2</b>, which events make stories and at what priority;</item>
/// <item><b>N3</b>, which of them this reader sees;</item>
/// <item><b>N4</b>, the order and the cap of <see cref="MaximumStories"/>.</item>
/// </list>
/// The paper is not the record. The turn report still carries every event, refusals
/// and bookkeeping included; a paper that omits something has lost nothing.
///
/// Detached exactly as <see cref="TurnReportView"/> is: strings, a date and value
/// types, and no <c>WorldState</c>, <c>WorldDefinition</c> or package kept.
/// </remarks>
public sealed class NewspaperView
{
    /// <summary>Chosen rule N4. The rest are counted in <see cref="StoriesOmitted"/>.</summary>
    public const int MaximumStories = 12;

    private readonly IReadOnlyList<NewspaperStory> _stories;

    private NewspaperView(TurnDate date, string masthead, NewspaperStory[] stories, int omitted)
    {
        Date = date;
        Masthead = masthead;
        _stories = Array.AsReadOnly(stories);
        StoriesOmitted = omitted;
    }

    /// <summary>The date the resolved turn ended on: the paper is dated the turn it opens.</summary>
    public TurnDate Date { get; }

    /// <summary>"The &lt;reader&gt; Gazette". A placeholder; the original's masthead is not recovered.</summary>
    public string Masthead { get; }

    /// <summary>Headlines first, then items. Never empty.</summary>
    public IReadOnlyList<NewspaperStory> Stories => _stories;

    /// <summary>Qualifying stories the cap left out: "and N more stories".</summary>
    public int StoriesOmitted { get; }

    public static NewspaperView Create(
        CompiledWorldPackage package,
        string scenarioKey,
        WorldState state,
        TurnResolution resolution,
        CountryId reader)
    {
        ArgumentNullException.ThrowIfNull(resolution);
        ArgumentNullException.ThrowIfNull(state);

        // As for the report: a state that has run on since would name owners and
        // borders that were not there when the events happened.
        if (resolution.TurnNumber != state.CompletedTurnCount)
        {
            throw new ArgumentException(
                $"Turn {resolution.TurnNumber} cannot be reported against a world that has " +
                $"completed {state.CompletedTurnCount} turns.",
                nameof(resolution));
        }

        return Compose(package, scenarioKey, state, resolution.EndedAt, resolution.Events, reader);
    }

    /// <summary>
    /// Composes a paper from an explicit event list.
    /// </summary>
    /// <remarks>
    /// The seam <see cref="TurnReportRenderer"/> exists for, applied here:
    /// <c>TurnResolution</c>'s constructor is internal, so a test can only choose
    /// which events a paper sees through this method. <see cref="Create"/> is the
    /// one a client calls.
    /// </remarks>
    public static NewspaperView Compose(
        CompiledWorldPackage package,
        string scenarioKey,
        WorldState state,
        TurnDate date,
        IEnumerable<TurnEvent> events,
        CountryId reader)
    {
        ArgumentNullException.ThrowIfNull(events);
        var renderer = TurnReportRenderer.Create(package, scenarioKey, state);
        var world = state.Definition;
        if ((uint)reader.Value >= (uint)world.Countries.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(reader));
        }

        var editor = new Editor(world, state, renderer, reader);
        var candidates = new List<(int Priority, int Order, NewspaperStory Story)>();
        var order = 0;
        foreach (var turnEvent in events)
        {
            ArgumentNullException.ThrowIfNull(turnEvent);
            if (editor.Select(turnEvent) is { } chosen)
            {
                candidates.Add((chosen.Priority, order, chosen.Story));
            }

            order++;
        }

        // N4: every headline before any item, so the cap only ever cuts items
        // while there are items to cut. Headlines by priority, then by the order
        // the log gave them; items in log order.
        var headlines = candidates
            .Where(static entry => entry.Story.IsHeadline)
            .OrderBy(static entry => entry.Priority)
            .ThenBy(static entry => entry.Order);
        var items = candidates
            .Where(static entry => !entry.Story.IsHeadline)
            .OrderBy(static entry => entry.Order);
        var kept = headlines
            .Concat(items)
            .Select(static entry => entry.Story)
            .Take(MaximumStories)
            .ToArray();
        var omitted = candidates.Count - kept.Length;

        if (kept.Length == 0)
        {
            kept =
            [
                new NewspaperStory(
                    NewspaperStoryKind.QuietQuarter,
                    false,
                    NewspaperText.QuietQuarterHeadline,
                    NewspaperText.QuietQuarterBody),
            ];
        }

        return new NewspaperView(
            date, NewspaperText.Masthead(renderer.Country(reader)), kept, omitted);
    }

    /// <summary>Decides, for one reader, whether an event is a story and what it says.</summary>
    private sealed class Editor(
        WorldDefinition world, WorldState state, TurnReportRenderer renderer, CountryId reader)
    {
        public (int Priority, NewspaperStory Story)? Select(TurnEvent turnEvent) => turnEvent switch
        {
            // ---- 1. New Invention: world-wide, the manual's own headline ----
            TechnologyAvailableEvent available => Headline(
                1,
                NewspaperStoryKind.NewInvention,
                NewspaperText.NewInvention,
                NewspaperText.InventionBody(renderer.Technology(available.Technology))),

            // ---- 2. The fall of a country ----
            CountryEliminatedEvent eliminated => Headline(
                2,
                NewspaperStoryKind.CountryEliminated,
                NewspaperText.Eliminated(renderer.Country(eliminated.Country)),
                Body(eliminated)),

            // ---- 3. War and peace ----
            WarDeclaredEvent declared => Headline(
                3,
                NewspaperStoryKind.WarDeclared,
                NewspaperText.WarDeclared(renderer.Country(declared.Country), renderer.Country(declared.Target)),
                Body(declared)),
            DiplomaticOvertureAcceptedEvent { Kind: DiplomaticOvertureKind.Peace } peace => Headline(
                3,
                NewspaperStoryKind.PeaceMade,
                NewspaperText.PeaceMade(renderer.Country(peace.Country), renderer.Country(peace.Target)),
                Body(peace)),

            // ---- 4. A province changes hands ----
            ProvinceCapturedEvent captured => Headline(
                4,
                NewspaperStoryKind.ProvinceCaptured,
                NewspaperText.ProvinceCaptured(
                    renderer.Province(captured.Province), renderer.Country(captured.NewOwner)),
                Body(captured)),

            // ---- 5. Battles: only those N3 says this reader hears of ----
            NavalBattleFoughtEvent battle when IsParty(battle.Attacker, battle.Defender) ||
                IsGreatPower(battle.Attacker) || IsGreatPower(battle.Defender) => Headline(
                5,
                NewspaperStoryKind.NavalBattle,
                NewspaperText.NavalBattle(renderer.SeaZone(battle.SeaZone)),
                Body(battle)),
            RegimentsLandedEvent landed when IsParty(landed.Country) ||
                IsGreatPower(landed.Country) || IsNearReader(landed.Destination) => Headline(
                5,
                NewspaperStoryKind.Landing,
                NewspaperText.Landing(renderer.Country(landed.Country), renderer.Province(landed.Destination)),
                Body(landed)),
            BattleFoughtEvent battle when IsParty(battle.Attacker, battle.Defender) ||
                IsGreatPower(battle.Attacker) || IsGreatPower(battle.Defender) ||
                IsNearReader(battle.Province) => Headline(
                5,
                NewspaperStoryKind.LandBattle,
                NewspaperText.LandBattle(renderer.Province(battle.Province)),
                Body(battle)),

            // ---- 6. A merchant the reader had a stake in ----
            MerchantInterceptedEvent intercepted
                when IsParty(intercepted.Carrier, intercepted.Seller, intercepted.Buyer) => Headline(
                6,
                NewspaperStoryKind.MerchantIntercepted,
                NewspaperText.MerchantIntercepted(renderer.Country(intercepted.Carrier)),
                Body(intercepted)),

            // ---- 7. Warnings to the reader ----
            WorkersFedEvent fed when IsParty(fed.Country) && (fed.Sick > 0 || fed.Starved > 0) => Headline(
                7,
                NewspaperStoryKind.Hunger,
                NewspaperText.Hunger(renderer.Country(fed.Country)),
                Body(fed, TurnReportKind.Loss)),
            CivilianDeliveryLostEvent lost when IsParty(lost.Country) => Headline(
                7, NewspaperStoryKind.CiviliansLost, NewspaperText.CiviliansLost(), Body(lost)),
            RegimentDeliveryLostEvent lost when IsParty(lost.Country) => Headline(
                7, NewspaperStoryKind.RegimentsLost, NewspaperText.RegimentsLost(), Body(lost)),
            ShipDeliveryLostEvent lost when IsParty(lost.Country) => Headline(
                7, NewspaperStoryKind.ShipsLost, NewspaperText.ShipsLost(), Body(lost)),
            DiplomaticTiesCutEvent cut when IsParty(cut.Country, cut.Target) => Headline(
                7,
                NewspaperStoryKind.TiesCut,
                NewspaperText.TiesCut(renderer.Country(cut.Country), renderer.Country(cut.Target)),
                Body(cut)),

            // ---- Items ----
            // A world price belongs to no country, so every reader's market page carries it.
            WorldPriceChangedEvent price => Item(
                NewspaperStoryKind.MarketReport,
                NewspaperText.MarketReport(renderer.Commodity(price.Commodity)),
                Body(price)),
            WorkersRecruitedEvent recruited when IsParty(recruited.Country) && recruited.Recruited > 0 => Item(
                NewspaperStoryKind.WorkersRecruited, NewspaperText.WorkersRecruited(), Body(recruited)),
            ShipsBuiltEvent built when IsParty(built.Country) => Item(
                NewspaperStoryKind.ShipsLaunched,
                NewspaperText.ShipsLaunched(renderer.Ship(built.Type)),
                Body(built)),
            CiviliansDeliveredEvent delivered when IsParty(delivered.Country) => Item(
                NewspaperStoryKind.CiviliansArrived, NewspaperText.CiviliansArrived(), Body(delivered)),
            RegimentsDeliveredEvent delivered when IsParty(delivered.Country) => Item(
                NewspaperStoryKind.RegimentsMustered, NewspaperText.RegimentsMustered(), Body(delivered)),
            TechnologyPurchasedEvent bought when IsGreatPower(bought.Country) => Item(
                NewspaperStoryKind.Investment,
                NewspaperText.Investment(renderer.Country(bought.Country), renderer.Technology(bought.Technology)),
                Body(bought)),
            ConstructionCompletedEvent done when IsParty(done.Country) => Item(
                NewspaperStoryKind.ConstructionCompleted, NewspaperText.ConstructionCompleted(), Body(done)),

            // Refusals, phase markers, deliveries, the harvest and the network are
            // the turn report's business and never the paper's.
            _ => null,
        };

        private static (int, NewspaperStory) Headline(
            int priority, NewspaperStoryKind kind, string headline, string body) =>
            (priority, new NewspaperStory(kind, true, headline, body));

        private static (int, NewspaperStory) Item(NewspaperStoryKind kind, string headline, string body) =>
            (0, new NewspaperStory(kind, false, headline, body));

        /// <summary>The turn report's own sentence, or the lines of one kind where an event says several things.</summary>
        private string Body(TurnEvent turnEvent, TurnReportKind? only = null) => string.Join(
            " ",
            renderer.Render(turnEvent)
                .Where(line => only is null || line.Kind == only)
                .Select(static line => line.Text));

        private bool IsParty(params CountryId[] countries) => countries.Contains(reader);

        private bool IsGreatPower(CountryId country) =>
            (uint)country.Value < (uint)world.Countries.Count && world.Countries[country.Value].IsGreatPower;

        /// <summary>
        /// Whether the reader owns this province or one next to it, as the turn ended.
        /// </summary>
        private bool IsNearReader(ProvinceId province)
        {
            if ((uint)province.Value >= (uint)world.Map.Provinces.Count)
            {
                return false;
            }

            return state.GetProvinceOwner(province) == reader ||
                world.ProvinceAdjacency.GetNeighbours(province)
                    .Any(neighbour => state.GetProvinceOwner(neighbour) == reader);
        }
    }
}
