# The interface shell

`Imperialism.Client` is no longer a map viewer with a header bar. It is a shell:
a framed screen stack you navigate between, a status border that survives that
navigation, and a session that knows which country you are playing.

## Scope

The shell navigates, ends turns, and reports what a turn did. It **does not**
take an order: four of the six screens are stubs that name the manual section
specifying them and the `CountryTurnOrders` fields they will fill. So every turn
resolves on empty orders — see below, because that is the single fact most likely
to be mistaken for a bug.

The demo package ships no cash, workforce or transport, so the border reads zero
for most of its fields. That is the demo world being small, not the border being
wrong; open a converted scenario with `--world` to see it move.

## Nobody is playing the other countries

**There is no AI.** Every country, the player's included, submits
`TurnOrders.Empty`. No orders screen exists, so there is nothing to submit.

The world is not frozen: Extraction, Transport, Feeding, Delivery, Migration and
Connectivity run whether or not anybody asked for anything, which is exactly why
an End Turn button is worth pressing before a single orders screen exists. But
nobody produces, builds, trades or researches, and a rival that gathered its
harvest and did nothing else is the engine as it stands rather than a fault.

The report screen says so on its own face, so nobody has to find this document
to learn it. The report is also deliberately **not** filtered to the local
country: with no AI, a filtered report would be nearly empty and would hide the
fact that the rest of the world is inert.

## The screens

The manual: "You govern your Empire using five screens: a central Terrain Map
screen and four Orders screens accessed from the Terrain Map", plus the
Technology Investment screen the microscope reaches.

| Screen | Manual | What it will do |
|---|---|---|
| **Terrain Map** | Terrain Map screen and its toolbar | Built. Map, camera, picking, cell inspector, debug overlays, state probe. |
| Transport | Transport screen | Commodity sliders against one shared capacity bar, in the order the player sets them. `Transport`, `BuildTransportCapacity`. |
| Industry | Industry screen | Warehouse, buildings, production orders, the workforce down the left border. `Production`, `Expansions`, `RecruitWorkers`. |
| Bid and Offers | Bid and Offers screen | Offers and bids per commodity in a fixed order. `TradeOffers`, `TradeBids`. |
| Diplomacy | Diplomacy screen | Waiting on rules, not on interface: Core's `Diplomacy` phase is still empty. |
| Investment | Invest in Technology | The Benefits of Technology Table, its prices and arrival dates. `BuyTechnology`. |

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
    ScreenStack             TerrainMapScreen + five StubScreens, one visible
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

## Ending a turn

The manual is specific about where the button lives and what pressing it means:
*"The End Turn button appears only on the Terrain Map screen at the bottom of the
toolbar. When you click here, you are committed. No orders can be cancelled or
changed once you end your turn."* It sits at the bottom of the Terrain Map's
right-hand column, and carries that warning as its tooltip.

`GameSession.EndTurn` is three statements and a guard:

1. `TurnResolver.Resolve` with `TurnOrders.Empty(World.Definition.Countries.Count)`
   — the same count `Resolve` validates against, read from the same place.
2. `TurnReportView.Create` against the post-resolve world, **before** the refresh,
   so nothing handling `Refreshed` can observe a session whose report is not made.
3. `Refresh()`, which re-issues both snapshots and moves the border's date on.

**The seed is the turn number about to resolve, and nothing consumes it.**
`TurnResolver` says so outright; it is recorded on the resolution for replay. What
matters today is only that it is deterministic and distinct per turn — a clock
would make the headless gate's output move for nothing. When a phase does start
reading it, this becomes a proper stream and the change is one line.

**The re-entrancy guard is in `GameSession`, not Core.** `Resolve` is destructive
and has no guard of its own, and a Godot button press can arrive twice. The
button disabling itself is a courtesy; the flag is the guard.

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

Every phase always appears. An empty one reads `Nothing this turn.`; the three
Core leaves unimplemented — Diplomacy, Conflict, TradeCancellation — read
`Not modelled yet.` instead, so a silence by design is distinguishable from a
silence by circumstance. **That distinction is a hand-maintained table in
`TurnReportView` and nothing will fail when it goes stale**; when those phases
gain rules, the table has to be edited by hand.

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
godot --path src/Imperialism.Client --resolution 2560x1080 -- --screenshot /tmp/shots
```

`--smoke-test` keeps the Phase 1 line's prefix and first six fields exactly, so
anything parsing it still works; new fields are appended and never reordered.
Ending a turn got its own line rather than extra fields on an existing one,
because resolving moves `statusCash` and `statusWorkers`:

```
VIEWER_SMOKE_OK map=… scenarios=… dimensions=…x… cells=… pickedCenters=… stateProbe=… country=… screens=6 statusCash=… statusWorkers=… theme=pass
SHELL_SMOKE_OK screens=6 visited=6 country=… theme=pass borderVisible=yes
TURN_SMOKE_OK turn=1 from=1815 Q1 to=1815 Q2 events=8 phases=14 lines=9 reportVisible=yes dismissed=yes country=… seed=1
```

**`events=0` fails the turn gate.** A turn nobody gave an order in still gathers,
carries, feeds and delivers, so a pipeline that resolves and reports nothing is
broken rather than quiet.

`--screenshot <dir>` writes one image per screen and exits, and then one of the
turn report — after the six, because resolving moves the world and the shots
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
8. **End Turn** brings up the report, listing all fourteen phases.
9. Close and Escape both dismiss it to the Terrain Map; F1–F6 do nothing while
   it is up.
10. Pressing End Turn twice quickly resolves exactly one turn.
11. The border's date advances one quarter and the map's ownership redraws.

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
- **The manual's real interstitials.** It describes three, and this slice builds
  none of them: the newspaper at the start of every turn (:494-498, :557-560),
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
