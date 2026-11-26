# The interface shell

`Imperialism.Client` is no longer a map viewer with a header bar. It is a shell:
a framed screen stack you navigate between, a status border that survives that
navigation, and a session that knows which country you are playing.

## Scope

The shell navigates, ends turns, and reports what a turn did. The player's
orders now have a home — `GameSession.Draft`, an `OrdersDraft` for the local
country, with read-only `OrdersPreview` views for Industry, Transport, Technology
and Trade (row 4a). Industry and Transport edit the draft (row 4b), as do
Bid and Offers and Investment (row 4c), and Diplomacy edits it through its own
screen (row 4e-1). An untouched draft still builds empty orders. Rivals submit nothing at all — see below, because that is the
single fact most likely to be mistaken for a bug.

The demo package ships no cash, workforce or transport, so the border reads zero
for most of its fields. That is the demo world being small, not the border being
wrong; open a converted scenario with `--world` to see it move.

## Nobody is playing the other countries

**There is no AI.** The local country submits its draft; every other country
submits empty orders, built in the same `TurnOrders` by
`OrdersDraft.BuildTurnOrders`.

The world is not frozen: Extraction, Transport, Feeding, Delivery, Migration and
Connectivity run whether or not anybody asked for anything, which is exactly why
an End Turn button is worth pressing before a single orders screen exists. But
rivals never produce, build, trade or research, and a rival that gathered its
harvest and did nothing else is the engine as it stands rather than a fault.
The report screen's notice says rivals submitted nothing, not that nobody did.

The report screen says so on its own face, so nobody has to find this document
to learn it. The report is also deliberately **not** filtered to the local
country: with no AI, a filtered report would be nearly empty and would hide the
fact that the rest of the world is inert.

## The screens

The manual: "You govern your Empire using five screens: a central Terrain Map
screen and four Orders screens accessed from the Terrain Map", plus the
Technology Investment screen the microscope reaches.

| Screen | Manual | Status and contents |
|---|---|---|
| **Terrain Map** | Terrain Map screen and its toolbar | Built. Map, camera, picking, cell inspector, debug overlays, state probe. |
| **Transport** | Transport screen | Built. Shared capacity bar, commodity sliders in draft priority order, up/down controls and shortfall warnings. `Transport`; capacity purchases live in Industry. |
| **Industry** | Industry screen | Built. Workforce down the left border, warehouse on the right; scrolling facilities, production cycles, Capitol recruitment and railyard purchases. `Production`, `Expansions`, `RecruitWorkers`, `BuildTransportCapacity`. |
| **Bid and Offers** | Bid and Offers screen | Built. Fixed commodity order, world prices, sellable stock, bounded offer input and bid input; holds, cash and all preview warnings. `TradeOffers`, `TradeBids`. |
| **Diplomacy** | Diplomacy screen | Consulates, embassies, pacts, peace, war and trade policy per country. `Overtures`, `DeclareWar`, `TradePolicies`; previewed by `OrdersPreview.Diplomacy` (row 4e-1). Built: a scrolling country list and selected-country orders, with treasury claims and per-order clearing. |
| **Investment** | Invest in Technology | Built. Purchasable technologies with prices and buy toggles, cumulative cost against cash, ordered buy list and affordability warnings. `BuyTechnology`. |

The Transport, Industry, Bid and Offers and Investment rows have their model now:
`OrdersDraft` holds each field named above, and `OrdersPreview.Create` gives the
matching `Transport`, `Industry`, `Trade` and `Technology` view. Each screen
forwards input to the draft and re-reads its preview.

**A preview warns and never refuses.** It replays the resolver's rules for these
phases in the resolver's order — recipes in priority order against facility
capacity, labour and stock; expansion and the railyard against what production
leaves; the Capitol's cap; what trade may sell after industry claims its inputs;
the transport bar as the turn opens; research against knowledge as the turn opens
— and reports what the turn is expected to do. The draft keeps every over-ask and
Core trims or refuses it with an event, as it always has. Core's planners are
internal, so the preview is a second statement of their rules;
`OrdersDraftTests` resolves each preview's fixture through `TurnResolver` and
compares events, so drift between the two fails a test. Two figures are bounds
rather than predictions: transport `Used` is trimmed again by what `Extraction`
actually gathers, and a bid's fill depends on the rest of the market.

The preview also warns where the manual sets a limit Core does not enforce: "You
may bid on only four commodities each turn" (manual 3107), as
`TradePreview.IsOverBidLimit`. The order still goes through.

### Civilian orders (row 4d-1)

The draft holds **one order per civilian** — `SetDeploy`, `SetWork` (improve or
search; the type decides which, as the original's one cursor does) or
`SetEngineer(unit, cell, structure)` — and a new click replaces the old.
`ClearUnitOrder` takes it back. `Build` maps them one to one onto `Deployments`,
`CivilianWork` and `EngineerWork`, each in unit-id order. The draft checks only that
the cell is on the map; whether the unit exists, is ours, or may go there is the
preview's warning and Core's ruling.

`OrdersPreview.Civilians` lists the drafting country's civilians — type, cell,
busy and turns left — with each drafted order's expected outcome.
`OrdersPreview.CreateUnitOrders(…, unit)` is what the map asks when a civilian is
selected: every own land cell for a deploy and for the type's work, and for an
Engineer rail to each neighbour plus depot, port and fort on its own tile. Each
target carries its **verb**, its **cash cost** (shown even when warned) and
`CivilianOrderRefusal` where Core would refuse. `Evaluate(cell, verb)` answers a
cell outside the list. A busy civilian warns `AlreadyWorking` everywhere.

**Core's planners are internal, so this is a restatement, labelled a preview.**
Public rules are read straight from `WorldState` and the map; the legality order
of `DevelopmentPlanner` and `EngineerPlanner` is repeated in
`CivilianOrderSimulation`, on an overlay that never touches the state. It follows
what `TurnResolver` does in `Development`:

1. The busy set is taken first, so a civilian finishing this turn is still
   refused as already working.
2. Work under way advances: a job finishing this turn raises its tile, records
   its search or stands its structure **before** any new order is read — so a
   Miner may be ordered onto hills a Prospector finishes searching this turn.
3. Deployments, then **every work order, then every Engineer order**, each in
   unit-id order. Cash comes off as each is accepted, and an accepted fort counts
   against its province's cap of three for the next.

**Cash follows Core's spending order.** `Trade` pays for bids before
`Development`, so `ClaimedByBids` — every bid at world price, as if it fills — comes
off first; that is the **worst case**, since an unfilled bid costs nothing and
sales are credited before Development too, so a `NotEnoughCash` warning may be one
Core does not issue. `Investment` pays for research after Development, so the
technology preview now affords purchases from cash less `ClaimedByCivilians`.
Overtures spend first of all, in `Diplomacy` (row 4e-1, below), so
`ClaimedByOvertures` comes off ahead of bids. The Armoury pays earlier still, in
`Production` (row 4e-2, below), so `ClaimedByArmoury` comes off ahead of overtures.
Training and the University also spend there but are not in the draft yet.

### Diplomacy orders (row 4e-1)

The draft holds overtures (`SetOverture(kind, target, offer)`: consulate, embassy,
pact, peace), declarations (`SetDeclareWar`) and **one trade policy per target**
(`SetTradePolicy`: none, a subsidy at one of the world's `SubsidyPercents`, or a
boycott). A new policy for the same target replaces the old one in place. A subsidy
percent the world does not offer throws, as an undefined enum does. One entry is kept
per (kind, target). `Build` maps the lists one to one, in draft order, onto
`Overtures`, `DeclareWar` and `TradePolicies`. `ClearDiplomacy` empties all three.

`OrdersPreview.Diplomacy` gives one row per other country, **labelled a preview**.
Each row shows:

- whether the country is a Great Power or eliminated;
- the relation mode, and whether a war or peace is pending;
- the posts each way, both pacts, and both standing policies;
- the drafted policy and Core's refusal of it;
- each overture with its price and refusal;
- what a declaration would cut (pacts, posts, policies, trade).

An overture that is not drafted is judged as if it were added to the end of the
list. `DiplomacySimulation` restates `DiplomacyPlanner` and the declaration rules in
`NavalPlanner`, on an overlay that never touches the state. Cash is the turn-open
cash, less what production claims, spent in list order.

The preview carries the treasury figures so the screen never subtracts:
`CashAtOpen`, `ClaimedBeforeDiplomacy` (what the Industry screen's orders claim first,
Core's `production.CashClaimed`; of training, the University and the Armoury only
the Armoury is drafted, so it equals `ClaimedByArmoury`),
`ClaimedByOvertures`, and `CashAfterOvertures`, the treasury once both are paid.
Later phases (bids, civilians, research, income) still move it after that.
`TheTreasuryAfterOverturesIsTheResolvedTreasury` resolves two overtures and checks
the figure against the treasury the next turn opens with.

**Peace offered to a Great Power always previews as `Declined`**, because Core
accepts it only if the other power offers peace back the same turn, and rivals
submit nothing. `DiplomacyOrdersTests` ends every turn through `TurnResolver` and
compares the preview against Core's overture, policy, declaration and ties-cut
events.

**One disagreement with the manual, and the preview sides with Core.** The manual
lets a player "cancel or change" a working unit's orders (1065–1069); Core refuses
any new order to a busy civilian with `AlreadyWorking`, and so does the preview.

`UnitOrdersTests` resolves every listed target of every civilian alone through
`TurnResolver` and compares the warning, the cost and the begun event.

### Terrain Map civilian controls (row 4d-2)

Click a local civilian's marker to select it. Cream circles are idle; grey
crosses are busy; a gold square means an order is drafted, and a white ring
marks the selected unit. Units sharing a hex have separate markers along its
top edge. Hover a marker to read its type, id, busy state and drafted order.

Selection highlights every target from `CreateUnitOrders` with a clickable verb
label inside its hex: deploy, improve, prospect, rail, depot, port or fort.
Multiple verbs on one hex have separate rows. Green labels are clean; amber
labels with `!` warn; the drafted target has a gold border. Hover a label to
read its verb, cash cost and Presentation's full warning text in the inspector.
Zoom in with the wheel when labels are small.

Left-click the verb to set or replace the selected civilian's draft order,
including a warned target. Right-click in the map, or press **Clear civilian
order**, to clear its draft while retaining selection. **Escape** deselects;
leaving the screen also deselects. Clearing a draft does not cancel work already
under way. Every edit re-reads both civilian previews. There are no game-rule
calculations in the overlay and no new art assets.

With no civilian selected, province picking and right-drag pan work as before.
Middle-drag, arrows, wheel zoom, Home and debug overlays remain available while
selected. Target labels take clicks only during civilian selection. Markers and
targets use one cached `Node2D` draw surface, rebuilt on preview changes, with no
per-cell nodes or per-frame rebuilding.

The Windows Godot gate is:

```bash
godot --headless --path src/Imperialism.Client -- --smoke-units --world <converted.iworld> --scenario <key> --country <key>
```

Use a scenario/country with an idle starting civilian (the tiny default demo
may have none). The flag chooses the first idle civilian and its first target
without a warning. It prints `UNITS_SMOKE_DRAFT`, `UNITS_SMOKE_PREVIEW`, the
unit's raw `UNITS_SMOKE_EVENT` and rendered `UNITS_SMOKE_RESOLVED` lines, then
`UNITS_SMOKE_OK unit=… verb=… cell=… events=…`. It fails with exit code 1 if no
idle civilian or clean target exists, the draft preview disagrees, no accepted
unit event resolves, any unit refusal occurs, or the order survives End Turn.
It exercises the screen's draft adapter and real session, not mouse routing.
Run it separately from `--smoke-turn`, whose empty-draft output is unchanged.
Godot runtime checks and screenshots remain pending the Windows PC run.

### Armoury, Shipyard and regiment moves (row 4e-2)

The draft holds **one Armoury row per category** (`SetRegimentPurchase`), **one
Shipyard row per hull class** (`SetShipBuild`) and regiment moves
(`SetRegimentMove(regiments, province)`). A new count replaces the old in place, and
a zero row is not sent. A regiment belongs to **one move at a time**: naming it in a
new move takes it out of its old one, and a move left empty is dropped.
`ClearRegimentMove`, `ClearRegimentPurchases`, `ClearShipBuilds` and
`ClearRegimentMoves` take them back. `Build` maps them one to one, in draft order,
onto `RegimentPurchases`, `ShipBuilds` and `RegimentMoves`. Naval orders (assembly,
patrol, blockade, landing, move) are **not in the draft yet**.

The previews, **labelled previews**, restate Core's planners:

- `OrdersPreview.Armoury` gives every category with the type it would build as the
  turn opens, the worker grade, the bill, cash and arms, and Core's refusal of the
  drafted row. It also lists the Garrison book: every regiment held, with its province,
  health, experience and drafted destination.
- `OrdersPreview.Shipyard` gives every hull with cargo, sea zones, bill and its gate.
  `IsWarship` is Core's own merchant test inverted (cargo and no firepower is a
  merchant).
- `OrdersPreview.ArmyMoves` gives each drafted move its verb (march, rail, attack,
  landing), the rail capacity it spends and Core's refusal, with the shared rail budget.
- `OrdersPreview.CreateArmyMoves(…, regiments)` is what the map asks when regiments
  are selected: every province with its verb, cost and refusal, judged as the newest
  move for them.

**The Armoury and the Shipyard join the warehouse and cash chain where Core runs
them.** The Armoury is planned in `Production` before any recipe, so its bill leaves
the warehouse before recipes read it. Its workers' labour leaves the pool too, and
its cash is the **first claim on the treasury**: overtures, bids, civilians and
research all see it gone. The Shipyard is paid after expansion and the railyard and
before the Capitol, against a warehouse that already holds this turn's output. It
costs no cash and no labour. Both bills count as claimed by industry, so trade's
`Sellable` falls by them.

**Moves resolve all before attacks**, each pass in list order, as in `Conflict`. A
march to an adjacent own province is free. Rail spends five capacity per armaments
point from the whole network capacity, separately from commodity transport. **An
accepted attack or landing is accepted into battle and no more**: the preview fights
no battle and does not attack a landing site, so it cannot say who wins. Later
attacks are judged against owners as the turn opens.

`MilitaryOrdersTests` ends every turn through `TurnResolver`. It compares each
purchase, hull and move warning with Core's event, and the cash and rail with what
Core charged.

### Diplomacy screen (client)

Open Diplomacy with its tab or F5. Great Powers precede minors in a scrolling
country list; eliminated countries are grey but remain selectable. Each row
shows relation and pending war/peace, posts and pacts each way, standing policies
and the drafted change. Select a country to offer consulate, embassy, pact or
peace, declare war, or choose none, a world-offered subsidy, or boycott. Prices,
refusal warnings, war consequences and all four treasury figures come directly
from `OrdersPreview.Diplomacy`. Warnings never disable an action. The right-hand
panel scrolls independently and lists every drafted diplomacy order with its
own Clear button. All changes use `session.Draft` and reread the preview.

Runtime layout, interactions and screenshots remain unverified until the Windows
Godot run. Check both scroll panels at the base size and 1920x1080, 2560x1080 and
3840x2160, keyboard navigation, country selection, warned actions, policy
replacement and each Clear button. Check that returning after End Turn shows the
new relations and cleared drafts, and that Escape/F1–F6 and modal routing still work.

## What an order remembers

`GameSession.EndTurn` calls `OrdersDraft.AdvanceTurn` once the turn resolves.
Where the manual speaks it decides; elsewhere the rule is chosen and labelled.

| Field | After End Turn | Source |
|---|---|---|
| Production at a mill or factory | **kept, as asked** — not clamped | Manual 2863–2867: orders "are saved to the next turn"; the building "remembers" the units wanted |
| Production at food processing | cleared | Manual 2651–2652: "orders to this centre are not saved". Identified as the facility with no capacity limit |
| Railyard (`BuildTransportCapacity`) | cleared | Manual 2903–2904: "these orders are not saved" |
| Technology purchases | cleared | Manual 4125–4126: a purchase cannot be cancelled once End Turn is clicked; the technology is then known |
| Transport sliders | kept, in order | **Chosen** — the manual is silent |
| Facility expansion | cleared | **Chosen** — a one-rung order, not a standing one |
| Capitol recruitment | cleared | **Chosen** — the manual is silent |
| Trade offers and bids | cleared | **Chosen** — the manual is silent |
| Civilian unit orders (deploy, work, Engineer jobs) | cleared | **Manual** 1206–1214: a unit told to do nothing "appears normally in the cycle next turn", and "next turn, if circumstances change, you can order the civilian at that time"; 1065–1069: a unit whose orders take more than one turn does not appear in the cycle while working and "returns to the cycle automatically" when done. Core keeps a job running with no new order, so nothing needs carrying |
| Overtures (consulate, embassy, pact, peace) | cleared | **Manual** 3466–3469: an overture may be cancelled "before the end of the turn", with its money returned, so it is an order for that turn; 3516–3518: the consulate "opens starting the turn after", and from then on Core holds it, not the order |
| War declarations | cleared | **Manual** 3477–3479: the other country is told "as soon as you click the End Turn button, and the war begins on the next turn". Core records the relation, so nothing needs carrying |
| Trade policy (subsidy, boycott) | cleared | **Chosen**, reading manual 3688–3690: a change "can be cancelled before the end of the turn". Core keeps the policy standing once set, so the order is a change and not the policy itself |
| Armoury and Shipyard rows | cleared | **Chosen**, reading manual 4874: they "work exactly like the University", where the order is placed and the unit appears "next turn" (4866–4872). The manual never says the order is saved, as it does for mills (2863). Carrying it would buy the same regiments again every turn |
| Regiment moves and attacks | cleared | **Manual** 1757–1758: rail capacity limits what moves "in one turn"; 1830–1831: a garrison told to do nothing does so "for this turn only" and "appears normally in the cycle next turn". Core moves the regiment, so nothing needs carrying |

**Production is kept unclamped on purpose.** A mill asked for more than it can
make keeps asking, because the manual's building keeps trying to fill the order;
the Industry preview shows the shortfall each turn instead.

Shared chrome, from the manual: a title and the hot text narrating whatever the
cursor is over along the top rail; the Left Arrow that closes a screen, bound
here to Escape; F1 through F6 for the six screens.

**Tabs run down both edges.** That is what the original's screen frame does, and
it is also forced: six 60x56 tabs stacked in one column need 418 of the 450
pixels a screen has, leaving nothing for the screen itself. This overflow passed
every gate while it was wrong — the border existed, was exactly the size it
should be, and sat below the bottom of the window — so `--smoke-screens` now
asserts the border is somewhere a person could see it.

## Architecture

```
res://scenes/Main.tscn  → GameShell (Control)
  top rail                screen title, hot text
  body                    left tabs · ScreenStack · right tabs
    ScreenStack             TerrainMapScreen + IndustryScreen + TransportScreen + TradeScreen + TechnologyScreen + DiplomacyScreen, one visible
  StatusBorder            always present, below everything
```

`GameShell` is the project's first `Control`-derived class, and that base type is
the whole reason the theme reaches anything: a plain `Node` inherits no theme.

**A node whose children are decided by data is built in code; a node whose
children are decided by layout is a scene.** So the map layers stay code-built —
a scene adds nothing to a multimesh driven by cell count — while the shell root
is a scene, which is what makes the theme visible in the editor.

### The session

`GameSession` holds the package, the scenario, the mutable `WorldState`, the
local country, and the current `WorldViewState` and `CountryStatusView`. It is a
plain class: not a Godot node, and deliberately **not an autoload**. Screens
receive it through `Enter`, so a screen can only read what the shell handed it
rather than reaching for whatever is ambient.

`Refresh()` is the one place snapshots are re-issued. It calls the two
Presentation factories and raises an event; it computes nothing.

### Where "which country am I" lives

In the client, and nowhere lower.

- **Not Core.** It is a rule about the interface, not about the world, and Core's
  public surface is held to an architecture test.
- **Not Content.** That would bake the playable power into the `.iworld`, when
  the same package must be playable as any of them.
- **Not Presentation.** Its job is issuing a detached view; choosing which view
  to issue is the caller's.

`GameSession.PlayableCountries` picks candidates most specific rule first: the
scenario's `DefaultStartCountries`, then the Great Powers, then everyone. Core
has no playable flag, so that is the closest the data comes and the order
matters. `--scenario <key>` and `--country <key>` select deterministically.

### The status border

`StatusBorder` **formats and never computes.** Every number it shows is a
property of `CountryStatusView`:

| Readout | Property |
|---|---|
| Country name, Great Power marker | `CountryName`, `IsGreatPower` |
| Date | `CurrentDate` (`TurnDate.ToString()` already yields `1815 Q1`) |
| Treasury | `Cash` |
| Labour | `AvailableLabour` |
| Workers | `TotalWorkers` |
| Transport | `TransportCapacity` |
| Holds | `MerchantMarine` |
| Untrained / Trained / Expert | `Workforce[i].Healthy`, `.Sick` |

If a readout ever needs a sum, a difference or a comparison, that arithmetic
belongs in the snapshot. `docs/architecture.md`: *"if a client script computes a
game number, that's a bug."*

**Labour is not a headcount**, and the snapshot's tests pin that: seven healthy
workers supply nothing in a world that declares no feeding rules. The border
reports what Core says.

`CountryStatusView` never touches `MapDefinition.Cells`. The border refreshes on
every navigation and every state change while `WorldViewState` already pays the
per-cell cost once; walking the map again would make opening a screen cost the
whole world. A test proves it by producing identical output on a three-cell and
a four-thousand-cell map.

## Industry and Transport orders

Both screens use the existing theme and art; fixed columns live in
`IndustryLayout.tscn` and `TransportLayout.tscn`, while preview rows are built in
code. Industry keeps the workforce on its left border and warehouse stock on the
right, with facilities and orders in the scrolling middle. Each facility shows
capacity, committed capacity, its next rung, expansion cost and technology gate.
The +1 toggle orders one expansion rung. Production rows show input/output bills,
labour per cycle, requested and expected cycles and their preview maximum, with
up/down priority buttons. Capitol and railyard controls show prices and shortfalls.

Transport keeps one capacity bar above the scrolling commodity sliders. Each row
shows the requested and reserved amounts and moves earlier or later in the draft
with its arrow buttons. The harvest remains unknown until resolution, as the
screen explains. Zero capacity disables sliders; worlds without a transport limit
are labelled unlimited. Numeric input is limited to the exact integer range of
Godot's double-valued controls, not a new economic rule.

Entering either screen inserts missing rows at zero without changing existing
quantities or priorities. Editing a control writes to `session.Draft`, then reads
`Preview()` again. Updating widget ranges suppresses input signals: a standing
order above today's maximum remains in the draft and is printed as requested,
even when the production spin box shows today's lower editable maximum. Only an
explicit edit replaces it. Shortfall comparisons live in Presentation; the client
only formats view values and warning flags.

## Investment and Bid and Offers orders

Both screens use the existing theme with fixed chrome in `TechnologyLayout.tscn`
and `TradeLayout.tscn` and scrolling rows from the preview. No new art is needed.
Investment shows the offered technologies and their prices with a buy toggle that
reads Purchasing when selected. The summary shows the cumulative buy list cost
against current cash. Below the offers, the buy list follows draft purchase order,
shows every availability and affordability warning, and lets any purchase be
cancelled. A note explains that purchases are known next turn and that cash can
change before Investment resolves. No purchasable technologies gets an explicit
empty state.

Bid and Offers keeps holds, cash, bid count and total bid cost above the scrolling
commodity rows. Each row names its commodity and shows world price, warehouse
stock, industry claims, sellable stock, offer and bid inputs, and requested amounts
and bid cost. Offer input is bounded by preview sellable stock. Like Industry,
changing a range suppresses signals: an existing offer above today's sellable stock
stays in the draft, is printed as requested, and receives the preview's warning.
Only editing it changes it. Both the four-commodity bid warning and the cash
warning appear together when applicable; neither prevents drafting an order.
An empty market gets an explicit empty state. Trade orders clear after End Turn.

## Ending a turn

The manual is specific about where the button lives and what pressing it means:
*"The End Turn button appears only on the Terrain Map screen at the bottom of the
toolbar. When you click here, you are committed. No orders can be cancelled or
changed once you end your turn."* It sits at the bottom of the Terrain Map's
right-hand column, and carries that warning as its tooltip.

`GameSession.EndTurn` calls the resolver and Presentation factories behind a guard:

1. `TurnResolver.Resolve` with `Draft.BuildTurnOrders()` — the local draft in
   its own slot and empty orders for every other country, one per
   `World.Definition.Countries` entry, the count `Resolve` validates against.
2. `TurnReportView.Create` and `NewspaperView.Create` against the post-resolve world,
   **before** the refresh, so `Refreshed` sees both snapshots. The newspaper reader
   is `LocalCountry`, the same country shown by the status border.
3. `Draft.AdvanceTurn()`, which applies "What an order remembers" above.
4. `Refresh()`, which re-issues both snapshots and moves the border's date on.

**The seed is the turn number about to resolve, and nothing consumes it.**
`TurnResolver` says so outright; it is recorded on the resolution for replay. What
matters today is only that it is deterministic and distinct per turn — a clock
would make the headless gate's output move for nothing. When a phase does start
reading it, this becomes a proper stream and the change is one line.

**The re-entrancy guard is in `GameSession`, not Core.** `Resolve` is destructive
and has no guard of its own, and a Godot button press can arrive twice. The
button disabling itself is a courtesy; the flag is the guard.

## The newspaper

**Built; untested in Godot until the PC run.** After End Turn, `NewspaperScreen`
shows the local country's `LastNewspaper` before the full turn report. Continue
or Escape dismisses the paper and opens the existing report; Close or Escape on
that report returns to the Terrain Map and enables End Turn again. F1–F6 remain
inert throughout both overlays. Neither overlay adds a navigable screen.

The masthead and date sit above a scrolling story list. Headline stories use the
existing large bold `ImperialismScreenTitle` style; their bodies and the smaller
non-headline items use the plain body font. The paper preserves Presentation's
story order and prints `and N more stories` from `StoriesOmitted`. It selects no
stories and computes no game numbers. Fonts, frame and panel art all come from
the existing theme; no art files were added.

## The turn report

**A modal, not a seventh screen.** Three reasons: both headless gates publish
`screens=6` from the navigable set and a seventh member would silently rewrite
two contracts; the original's screen frame has ten tabs and none of them is a
turn report; and a tab would let a player open a *stale* report whenever they
liked. It is the consequence of an action, not a place. `TurnReportScreen`
therefore does not implement `IShellScreen` — an `Enter` would imply otherwise.

The fourteen headings follow the pipeline, and the first six are the manual's own
list: *"Diplomatic offers are exchanged… Trade deals are offered… Industrial
production takes place. Military conflicts are resolved. Intercepted or blockaded
trades are cancelled. All commodities transported internally, or successfully
delivered by traders, are placed in the industrial warehouse."*

Every phase always appears. An empty one reads `Nothing this turn.`, except the
three whose silence a player might misread, which say what did not happen:
Diplomacy `No overtures, policies or declarations of war this turn.`, Conflict
`No fleet orders, regiment moves or battles this turn.` and TradeCancellation
`No cargo was intercepted this turn.` Those phases used to read `Not modelled yet.`
and had all gained rules by row 6e; the notes are a hand-maintained table in
`TurnReportView`, and a test now holds that none claims to be unmodelled.

`Kind` maps to a theme variation by lookup, never by reading the words:
`Outcome` takes the plain body style, and `Shortfall`, `Loss` and `Refusal` take
`ImperialismQuiet`. Giving loss and refusal their own colour is a theme change
and a follow-up.

Dismissal is Close or Escape, returning to the Terrain Map — *"each turn begins
and ends on the Terrain Map screen."* **While the report is up the function keys
are inert.** Without that, F3 navigates the stack behind the modal and the player
dismisses into a screen they never chose.

## Rendering an event

The renderer lives in `Imperialism.Presentation`, not the client, and both halves
of that are checked: `HexMapProjectionTests` forbids Godot in that assembly, and
`docs/architecture.md` forbids a client script computing a game number. Deciding
that a line is a `Shortfall` means comparing two numbers on an event — which
looks like the rule being broken until you notice which assembly it happens in.

`TurnReportRenderer` is a **separate public type on purpose.** `TurnResolution`'s
constructor is internal, so no test can fabricate a resolution carrying chosen
events; rendering one event at a time is the only seam through which a test can
prove every concrete event type produces words. Folding it back into
`TurnReportView` would take that test with it.

The verbs are not free choices. *Gathered, carried, wasted, stranded, delivered,
eaten, produced N cycles, built, recruited, improved, searched, revealed, offered
and unsold, short of a cargo hold* are what the soak harness and the formula
documents already call these facts.

**Names everywhere, keys nowhere.** Countries, commodities, recipes, facilities,
technologies, terrain, provinces and civilian types all carry authored names.
Three identifiers do not, and each has a decided answer:

- **A deposit has no name**, so it is named by the commodity it yields — *"searched
  Barren Hills in Lorraine and revealed Coal"*. The stable key would work and
  reads `resource.coal`, which is a developer string; this is the first
  player-facing prose in the project and that is the wrong precedent to set in it.
  Lossy where two deposits yield the same commodity, so a revealed list
  de-duplicates. The real fix is `ResourceDefinition.Name`, an `.iworld` bump.
- **A civilian unit id is a sparse runtime number**, and `GetCivilian` returns null
  for one that no longer exists — which is exactly the `NoSuchCivilian` refusal
  and is reachable today. It resolves to the type name when found and to
  `civilian N` when not, keeping the number so a report of something going wrong
  still identifies which one.
- **A cell** becomes `"{Terrain} in {Province}"`, and the region kind is checked
  first because **`CellRegion.Province` throws** for a sea zone or an unassigned
  cell. Any coastal tile in an event would otherwise take the whole report down.

**Enum growth has no compiler backstop.** A switch expression over an enum needs a
discard arm whatever it handles, because any integer is a possible value, so the
discard is always there and CS8509 never fires. `TurnReportTextTests` is the only
thing between a new refusal reason in Core and an exception in the middle of a
player's report.

## Theme and scale

The original interface is drawn for 640x480. The base viewport is therefore
measured in **original art pixels** — 720x450 — and `canvas_items` stretch scales
the whole canvas to the window. Art comes up at a clean 2x in the default
1440x900 window; text stays crisp because fonts rasterise at the scaled size
rather than being scaled after the fact. `stretch/aspect="expand"` hands the
extra width of a wider monitor to the layout instead of pillarboxing it, which
is what lets a 4:3 interface become a widescreen one. Sizes in the theme are in
art pixels for the same reason.

```
ui/imperialism.theme.tres
ui/styles/plate.tres          gold-framed plate, nine-patch, margins 5
ui/styles/plate_active.tres   the same plate with a green field
ui/styles/wood_panel.tres     horizontal grain, tiled
ui/styles/wood_column.tres    vertical grain, tiled
```

Type variations: `ImperialismScreenFrame`, `ImperialismSideColumn`,
`ImperialismStatusBorder`, `ImperialismScreenTitle`, `ImperialismReadout`,
`ImperialismReadoutLabel`, `ImperialismHotText`.

Three rules the theme follows, each for a reason:

**Wood is tiled, never stretched.** Stretching a grain drawn for a 640-pixel rail
across a modern window smears it into streaks; tiling reads as more of the same
plank, which is what the original looks like anyway.

**Structure is the StyleBox; decoration is a sibling node.** Anything that must
not repeat — a carved corner, a nameplate — belongs in its own `TextureRect` over
plain tiling wood, not in the nine-slice. This is what makes widescreen chrome
possible from art drawn for 640x480.

**Margins are measured, not chosen.** `plate.tres` says 5 because
`--probe 2303.BMP` says the cream field starts five pixels in on all four sides.

**The theme is applied through `project.godot`'s `gui/theme/custom` and no script
ever loads it.** That is deliberate: with the setting owning it, a machine that
has not run the extractor degrades to Godot's default theme, whereas a script
calling `GD.Load` would throw and take the headless smoke gate down with it. The
`theme=pass|fail` field reports which happened and never changes the exit code.

Texture filtering is Nearest. Bilinear turns 1997 dithered art to mud.

## Verification

Without Godot — this is what CI runs, and it compiles every screen script
because `Imperialism.Client` builds through the `Godot.NET.Sdk` NuGet package:

```bash
dotnet build Imperialism.sln -c Release && dotnet test Imperialism.sln --no-build -c Release
```

With Godot:

```bash
godot --headless --path src/Imperialism.Client -- --smoke-test
```

```bash
godot --headless --path src/Imperialism.Client -- --smoke-screens
```

```bash
godot --headless --path src/Imperialism.Client -- --smoke-turn
```

```bash
godot --headless --path src/Imperialism.Client -- --smoke-orders
```

`--smoke-orders` defaults to the separate synthetic `demo/orders.iworld`, leaving
the viewer demo and existing smoke flags unchanged. It visits Industry,
Transport, Investment and Bid and Offers, sets one production cycle, one transport
unit, one technology purchase (when offered) and one sellable commodity offer
through the draft, verifies those orders are submitted, ends the turn through the
existing newspaper and report flow, and prints rendered event lines as
`ORDERS_EVENT`, followed by `ORDERS_SMOKE_OK` (or `FAILED`). `--world` can override the fixture; a package
without recipes, commodities or sellable trade stock fails explicitly. The summary
keeps the `ORDERS_SMOKE` prefix and adds `technology` and `offers` counts. The
synthetic orders fixture includes priced timber and an affordable technology;
headless .NET tests resolve both scenarios, preserving the production and transport
checks while verifying the technology purchase and the offer event. Rival orders
remain empty, so the offer is reported unsold rather than matched. **The Godot
smoke run and screenshots remain unrun on this server; they require the Windows PC.**

```bash
godot --path src/Imperialism.Client --resolution 2560x1080 -- --screenshot /tmp/shots
```

```bash
godot --headless --path src/Imperialism.Client -- --smoke-diplomacy --world <world.iworld> --scenario <key> --country <key>
```

`--smoke-diplomacy` drafts a consulate toward the first other Great Power without
our consulate, prints `DIPLOMACY_SMOKE_PREVIEW` (including `CashAfterOvertures`),
ends the turn and prints the report's resulting events as `DIPLOMACY_SMOKE_EVENT`
lines, then `DIPLOMACY_SMOKE_OK`. A predicted refusal is valid smoke output;
use a funded scenario with diplomacy prices to observe an accepted consulate.
If no candidate exists, it prints `DIPLOMACY_SMOKE_FAIL` and exits non-zero.
This mode takes precedence over other smoke/screenshot flags. Run it separately
from `--smoke-turn`, whose empty-draft output is unchanged.

`--smoke-test` keeps the Phase 1 line's prefix and first six fields exactly, so
anything parsing it still works; new fields are appended and never reordered.
Ending a turn got its own line rather than extra fields on an existing one,
because resolving moves `statusCash` and `statusWorkers`:

```
VIEWER_SMOKE_OK map=… scenarios=… dimensions=…x… cells=… pickedCenters=… stateProbe=… country=… screens=6 statusCash=… statusWorkers=… theme=pass
SHELL_SMOKE_OK screens=6 visited=6 country=… theme=pass borderVisible=yes
TURN_SMOKE_OK turn=1 from=1815 Q1 to=1815 Q2 events=8 phases=14 lines=9 reportVisible=yes dismissed=yes country=… seed=1 paperLines=2 paperVisible=yes paperDismissed=yes
```

`paperLines` counts story text rows (headline and body separately) plus the
optional overflow row; it excludes the masthead and date and does not count
visual wrapping. The turn gate requires a nonempty paper shown first with the
report hidden, then the visible report after dismissing the paper, then dismissal
to the Terrain Map. These Godot assertions have not run on this server.

**`events=0` fails the turn gate.** A turn nobody gave an order in still gathers,
carries, feeds and delivers, so a pipeline that resolves and reports nothing is
broken rather than quiet.

`--screenshot <dir>` writes one image per screen, followed by the newspaper
(`newspaper.png`) and turn report (`turnreport.png`), then exits. The overlays
come after the six screens because resolving moves the world and the shots
would otherwise disagree with each other. No gate can tell whether wood grain
smeared or chrome landed on top of a readout, so the check for those is a person
looking at pictures and this is what produces them.

Manual checklist:

1. The border shows the chosen country's cash, labour and date.
2. Press **Probe state** on the Terrain Map; ownership shifts and the border follows.
3. All six screens by click and by F1–F6; Escape returns to the Terrain Map.
4. Map pan, zoom, Home, hover and click-to-pin still work — the map lives in a
   `SubViewport` now, which is what stops it eating clicks meant for the chrome,
   and no automated gate covers that routing.
5. Hot text appears on the top rail when the cursor is over a tab.
6. Resize to 1920x1080, 2560x1080 and 3840x2160: wood tiles without smearing and
   the border stays complete.
7. Every readout is rendered text at every size — nothing stretched.
8. **End Turn** brings up the newspaper: masthead, date, large bold headlines,
   plain smaller items, scrolling and the overflow notice when applicable.
9. Continue and Escape both open the report, listing all fourteen phases. Close
   and Escape dismiss the report to the Terrain Map; F1–F6 do nothing while
   either overlay is up. Check a second turn starts the paper scrolled to the top.
10. Pressing End Turn twice quickly resolves exactly one turn.
11. The border's date advances one quarter and the map's ownership redraws.
12. Industry: edit production, expansion, recruitment and the railyard, checking
    updated commitments, costs and warnings. Revisit without losing an order;
    after End Turn confirm mills remember requests, while food processing,
    expansions, recruitment and railyard purchases clear.
13. Transport: drag a slider, reorder priorities, then revisit and End Turn;
    requests and priority survive. Verify the shared bar and each shortfall.
14. Resize all four orders screens at the base viewport and widescreen: columns remain
    readable and the long order lists scroll inside the screen stack.
15. Investment: select and cancel purchases, verify cumulative cost and the ordered
    affordability warnings, then End Turn and confirm purchased knowledge is available.
16. Bid and Offers: adjust an offer and bid, revisit Industry to reserve stock, then
    return and check the sellable bound and any over-offer warning. Bid on five
    commodities and over current cash; both warnings must remain visible.

## Open questions

- **A country has no colour and no flag.** `CountryDefinition` carries `Id`,
  `Name` and `IsGreatPower` and nothing else, so the border names a country
  without showing it. Fixing this is a Content schema change and an `.iworld`
  version bump, and it is the single largest visible gap against the original.
- **The workforce coverall icons are unidentified**; see
  `docs/asset-pipeline.md`.
- **The lit microscope.** The manual says the Invest in Technology control
  "appears lighter than the surrounding wood when there is a new technology". The
  art for both states is extracted, but "is a new technology available" is an
  arrival-date comparison, which is a rule and therefore Core's.
- **Provinces owned** is deliberately absent from the status snapshot: it is
  O(provinces) and would break the promise that the border costs nothing.
- **The manual's real interstitials.** The newspaper at the start of every turn
  (:494-498, :557-560) is **built; untested in Godot until the PC run**; see
  `NewspaperView`, `NewspaperScreen` and `docs/formulas/newspaper.md`. Still pending:
  the Deal Book summarising trades and the deals that fell through (:3298-3306,
  :5011-5013), and the full-screen technology confirmation (:4249-4253). The Deal
  Book in particular is a re-grouping of `CommodityTradedEvent` plus
  `TradeUnfilledEvent` and needs no new data — *"potential deals that were not
  made because you rejected them or ran out of merchant marine"* is
  `TradeRefusal.NoMerchantCapacity` in prose.
- **Delivery volume.** One line per `CommodityDeliveredEvent`, unbounded. Small
  on the demo world, roughly countries × commodities per turn on a real one. The
  scroll container absorbs it and `lines=` in the turn gate is the tripwire; a
  digest would be a Presentation change.
- **`Loss` and `Refusal` share a style with `Shortfall`.** They want their own,
  which is a theme change rather than a logic one.
- **`ResourceDefinition` has no `Name`**, so the report names a deposit by the
  commodity it yields. The fix is an `.iworld` bump.
- **`CountryStatusView.TechnologyKeys` is now the only key-not-name outlier** in
  the presentation layer. It should become names, or gain a parallel, when the
  Investment screen lands and needs both — not before: it is shipped, tested, and
  no consumer benefits today.

### Windows acceptance for civilian orders

1. Open a converted scenario with local civilians. Confirm idle, busy, drafted
   and selected markers, including selecting each unit sharing a hex. Screenshot
   the idle map and selected civilian with targets visible.
2. Click a marker: every preview target appears, with separate clickable verb
   rows on shared hexes. Confirm green clean rows, amber `!` warnings and the
   drafted row's gold outline. Check deploy/improve/prospect and an Engineer's
   rail/depot/port/fort, zooming in for readability.
3. Hover each kind of target: inspector text gives the matching verb, cash cost
   and full warning sentence. Moving between verbs on the same hex updates it.
   Hover a busy marker: its remaining turns and busy state are shown.
4. Click clean and warned targets: each replaces that civilian's draft, the
   marker and target outline update, and warnings never disable ordering.
   Right-click and the Clear button both remove only the selected unit's draft.
   Busy units remain busy when their drafts are cleared.
5. Escape removes selection and targets, preserving the draft. With no unit
   selected, province pinning, debug overlays, Probe state and right-drag pan
   still work. With selection active, check middle-drag, arrows, wheel and Home.
   Leave/re-enter the screen and end/dismiss two turns: no stale targets, blocked
   camera or duplicate resolution. Check newspaper/report modality and F1–F6.
6. Run `--smoke-units` on a scenario with an idle civilian. Confirm the first idle
   unit and first clean target, the matching draft/preview, raw and rendered
   resolved events for that unit, matching target/cost, `UNITS_SMOKE_OK`, exit 0
   and cleared draft. A scenario without an idle civilian must fail clearly with
   exit 1. Run `--smoke-turn` separately with an empty draft and confirm its
   existing fields, newspaper/report visibility and dismissal remain unchanged.
7. At 1440x900, 1920x1080 and 2560x1080, confirm labels, hover text, controls and
   the status border fit without overlap. Capture the selected Engineer and a
   warned target alongside the original for Andy's visual review.
