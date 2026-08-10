# The interface shell

`Imperialism.Client` is no longer a map viewer with a header bar. It is a shell:
a framed screen stack you navigate between, a status border that survives that
navigation, and a session that knows which country you are playing.

## Scope

This slice builds the skeleton and nothing else. It **does not** end a turn,
take an order, or call `TurnResolver.Resolve`. Four of the six screens are stubs
that name the manual section specifying them and the `CountryTurnOrders` fields
they will fill. The point is to establish the pattern, the asset pipeline and the
presentation boundary before any one screen's detail argues about them.

The demo package ships no cash, workforce or transport, so the border reads zero
for most of its fields. That is the demo world being small, not the border being
wrong; open a converted scenario with `--world` to see it move.

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
godot --path src/Imperialism.Client --resolution 2560x1080 -- --screenshot /tmp/shots
```

`--smoke-test` keeps the Phase 1 line's prefix and first six fields exactly, so
anything parsing it still works; new fields are appended and never reordered:

```
VIEWER_SMOKE_OK map=… scenarios=… dimensions=…x… cells=… pickedCenters=… stateProbe=… country=… screens=6 statusCash=… statusWorkers=… theme=pass
SHELL_SMOKE_OK screens=6 visited=6 country=… theme=pass borderVisible=yes
```

`--screenshot <dir>` writes one image per screen and exits. No gate can tell
whether wood grain smeared or chrome landed on top of a readout, so the check for
those is a person looking at pictures and this is what produces them.

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
- **Ending a turn.** `TurnResolver.Resolve` is still never called from the
  client. When it is, the border should update from `TurnResolution.Events`
  rather than by diffing state — `docs/architecture.md` calls the event log the
  presentation contract.
