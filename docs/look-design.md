# The look phase: screens that look like the original

Andy's ruling (26 Nov 2026): **"Yes, match original".** The Godot client's
screens are rebuilt to look like the 1997 game's, one screen at a time. Issue
#89 tracks the phase and holds the grouped list of presentation differences.

This document is the plan. Its rules are Andy's unless a line says *chosen*.
Andy approved a target look the same day, from a Transport mockup: **"This
looks perfect, that as full screen"** (see "The canvas"). It keeps the first
ruling's whole-number scaling, adds crisp text, and withdraws the later "screens
grow to fill 1920x1080" refinement, whose extra side tabs, info cards and status
bar were, in Andy's words, "changing quite a lot more than intended".

## Goal and scope

Today each screen in `src/Imperialism.Client/screens/*.cs` is a modern panel
layout: wood columns, gold plates and lists of rows, laid out by Godot
containers on a 720x450 canvas that stretches to the window (`gui-shell.md`,
"Theme and scale"). The original draws every screen as one 640x480 picture.
Each rebuilt screen keeps the original's structure and art: Transport stays the
open ledger book with icon rows and thin sliders, Industry the drawn city with
the pop-up warehouse, and so on for the rest.
The **Transport** screen is an open ledger book: a row of commodity icons, each
with a slider and an "x / y" figure under it, demand lines beneath, and one
shared capacity bar in the lower right (manual lines 2253–2291). The
**Industry** screen is a drawn city: you click a building to open its dialog,
and the Warehouse near the top centre pops up the stockpile (manual
2515–2527).

The look phase replaces how each screen **draws**. It does not change what any
screen **shows**:

- **Engine, rules and Presentation numbers don't change.** A screen changes
  only how it shows the existing Presentation views (`OrdersPreview`,
  `TransportPreview`, `IndustryPreview`, `TurnReportView`, `NewspaperView` and
  the rest).
- A look PR has **no diff under `src/Imperialism.Core` or
  `src/Imperialism.Presentation`**. The one exception is a new **read-only view
  field** that a screen truly needs and cannot get any other way. The PR must
  flag it in its description, with a Presentation test that pins it.
- `architecture.md`'s rule still holds: a client script never computes a game
  number. If the original's screen shows a figure Presentation does not
  provide, that is the read-only-field case above, not a sum in a screen.

## The canvas

**The original screen, exactly as the original lays it out.** Frame, top rail,
desk, ledger book, icons and buttons sit where the original puts them. Nothing is
added around or between them: no side tabs, no info cards, no status bar, no
extra panels.

**A 640x480 root at a whole-number scale.** Each rebuilt screen is one 640x480
root whose children are placed at their original pixel positions. The root is
scaled by `k = floor(min(W / 640, H / 480))`, at least 1, and centred in the
window. On 1920x1080 that is **k = 2: a 1280x960 picture with 320 px either side
and 60 px above and below.** Art is drawn with nearest-neighbour filtering
(`default_texture_filter=0`, unchanged), so one-pixel lines and dithering stay
clean.

**The surround is plain near-black `#0E0905`** and holds nothing: no art, no
text, no controls. (The colour is Andy's approved mockup's.)

**Only the drawing is modernised: text is redrawn crisp.** Text is not part of
any bitmap and is not scaled up from 1x. It is rendered at the root's final
resolution, in place, at the position and size the original draws it, using the
original's own faces (`fonts/antiqua*.ttf` for body text, `fonts/display*.ttf`
for headings, both in the manifest). A label's box is the note's rectangle x k;
text does not grow beyond it. Colours follow the original's palette as sampled
with the extractor's `--probe`.

*Chosen, for how this is built in Godot:* the rebuilt screen's root is a
`Control` of 640x480 with `scale = (k, k)`, recomputed on window resize; font
oversampling renders its labels at the scaled size, which is what makes them
crisp. Whether the project's base viewport also changes (today 720x450 with
`expand`, `gui-shell.md` "Theme and scale") is the Transport PR's to settle,
provided screens not yet rebuilt still pass their smokes unchanged.

**Layout notes stay in original pixels.** A note measures the 640x480 shot
(below). The screen's code places each element at exactly the note's position
inside the root; the root's scale does the rest, so a reviewer maps any node to
its note row by name alone.

**Screens not yet rebuilt** keep their current container layout until their own
PR lands. The smokes, not the look, are their gate.

## Controls in the original's style

The stock Godot controls go: no `SpinBox`, no toggle switch (`CheckButton`), no
plain `HSlider` or `ProgressBar` on a rebuilt screen. In their place is a small
set of controls drawn from the original's bitmaps, built once under
`src/Imperialism.Client/ui/look/` and reused by every screen:

| Control | Replaces | Drawn as |
|---|---|---|
| `LedgerSlider` | `HSlider`, `SpinBox` on Transport and Industry | a thin track with the original's arrow ends and fill, value text beneath |
| `ArrowStepper` | `SpinBox` for counts (ships, regiments, recruits) | the original's up/down or left/right arrow pair beside a figure |
| `LedgerCheck` | `CheckButton`, `CheckBox` | the original's tick or pressed-button bitmap |
| `CapacityBar` | `ProgressBar` | the original's bar frame and fill |
| `PlateButton` | the theme's `Button` on rebuilt screens | the original's button bitmaps, normal, pressed and disabled |

Each is a plain `Control` that draws textures and emits the same signal the
stock control did, so a screen swaps one for the other without changing what it
calls. The Transport PR builds the ones it needs; later PRs add the rest.
They are named in the planned paths here and do not exist yet.

**`LedgerSlider`, as Andy approved it on the Transport mockup**, drawn at k from
the original's pieces:

- a dark-red rim around the track;
- the carried part filled **blue**, the spare part to the slider's maximum **tan**;
- a thin underline beneath the track, **green** when the row's demand is met and
  **red** when it is not (the demand line's own state from Presentation, never a
  sum in the screen);
- grey arrow ends, left lowering and right raising (see "Input");
- a small round handle at the carried value;
- the row's figure under the bar as **"carried / available"**, crisp text.

The slider pieces themselves (rim, fills, arrows, handle) are still to be found in
`pictuniv`; until they are, the colours above are what a reviewer checks against.

## The Transport target

Transport is the first screen and the one Andy approved. What its PR must show:

- the original's Transport screen layout: the frame and top rail, the desk, the
  open ledger (`pictuniv` **1021.BMP**), the commodity icons, and the End Turn
  button (`pictenu` **1011.BMP**); the coloured commodity icons and the slider
  pieces are still to be identified in `pictuniv`;
- each row: its icon, its good's name, its `LedgerSlider` and "carried /
  available" under the bar;
- the headings **"Transport"** and **"Ledger"** in the display face;
- the top rail's **date, treasury and hot text**, crisp, in place;
- the screen **opens pre-filled**, as the original's does (the rule itself is
  PR #88's, not this phase's).

**The references are on ETHERCAT and never in the repo:**
`C:\qf-test\evidence\mockups\transport-target-1920.png` (the approved mockup)
and `transport-mockup-compose.py` beside it, which holds the measured 1x
position of every row, bar and heading. The PC lane turns those positions into
`docs/look/transport-layout.md` (format below); server-side work builds from that
note alone.

## The art

Rule 2 of the ruling: **only the original's own bitmaps**, taken through the
existing extractor and named in the manifest. **No screenshot of the original
is ever committed**, not as art, not as a reference, not as a test fixture.

- The extractor is `tools/Imperialism.AssetExtractor`; `docs/asset-pipeline.md`
  says how to run it.
- Find a screen's pieces with `--contact-sheet` on one screen and the
  reference shot on the other. Many screens are one of the sixty-four
  full-screen 640x480 backgrounds plus small overlay bitmaps (icons, slider
  arrows, bar fills). **The full-screen backgrounds are in scope**, not only
  the 33 chrome, icon and tab pieces the manifest maps today.
- **Measure margins and offsets with `--probe`**, as `plate.tres`'s 5 was
  measured. Where a piece sits inside a background and the probe can find its
  edge, the probe's number wins over the layout note's.
- **Every new file under `src/Imperialism.Client/art/` is named in
  `assets/manifest/imperialism-art.json`**, with `evidence` and
  `confidence`. `ArtManifestTests` enforces this and the extractor's
  byte-identical rerun must leave the tree clean.
- Each screen's PR adds its entries with a `group` named after the screen
  (`transport`, `industry`, `terrain-map`, `trade`, `technology`,
  `diplomacy`, `armoury`, `fleet`, `turn-report`, `newspaper`), and its art
  under `art/<group>/`. Shared pieces stay in `chrome`.

## Open decision for Andy: where the art lives

Today the interface art is **committed** under `src/Imperialism.Client/art/`,
and the manifest test makes sure nothing arrives there unrecorded
(`asset-pipeline.md`; the "Interface art is committed" hard rule in
`CLAUDE.md`). Andy's message said the art is "extracted from the install at
build time, as now", which does not match what is done now. The two options:

1. **Keep it committed under the manifest** (the current default). A fresh
   clone runs the client with the original's look; CI and the server lanes can
   run the smokes against the real art; the extractor rerun proves the files
   are reproducible. The cost is that the original's bitmaps sit in the
   repository.
2. **Extract at build time.** Nothing from the original is committed;
   `art/` is generated from the install by the `--manifest` run before the
   client builds. The cost: every build needs the install, a fresh clone no
   longer runs the client with its art, and server-side smokes run against
   Godot's default theme unless the server holds an install.

**This document assumes option 1 until Andy rules.** Either way the manifest
is the single list of what is used, so moving from 1 to 2 later is a build
step and a `.gitignore` line, not a rework of any screen.

## The layout-note format

The reference for each screen is the matching original screenshot. Those live
**only on the test PC**, outside the repo. For each screen the PC lane writes
a plain-text layout note, committed as **`docs/look/<screen>-layout.md`**,
and that note is what server-side work builds from. It holds **positions and
sizes only, no game data**: no commodity amounts, prices, names of a
scenario's countries, or anything else read off the shot.

A note opens with the shot it was measured from (file name on the PC only,
for example `original-s1\O30.png`), the screen's background resource if
known, and then one table:

| name | x | y | w | h | art | shows |
|---|---|---|---|---|---|---|
| `slider.grain.left` | 212 | 96 | 9 | 11 | `NNNN.BMP` or `?` | `TransportSliderPreview.Requested` (decrease) |
| `capacity.bar` | 430 | 402 | 150 | 8 | `?` | `TransportPreview.Used / Capacity` |
| `hottext` | 470 | 18 | 150 | 14 | — | the hovered element's name |

- **name**: stable, dotted, unique within the screen; the screen's code uses
  the same names, so a note row and a node can be matched by search.
- **x, y, w, h**: integers in 640x480 pixels, top-left origin, measured on the
  unscaled shot.
- **art**: the resource name (`archive:NNNN.BMP`) if identified, `?` if not,
  `—` for text or a plain fill.
- **shows**: the Presentation type and field the element displays, or
  `static` for decoration. A row whose value no Presentation field provides is
  marked **`MISSING`**; that is the trigger for the read-only-field exception
  above and must be raised in the PR, not worked around.

A repeated element (one commodity row of many) may be given once with its
pitch: `row pitch dx=0 dy=24, order: as Sliders`.

## Input

- **Slider arrows.** Each end of a slider is a button: left lowers, right
  raises, by one unit per click, repeating while held (*chosen*: repeat after
  400 ms, every 60 ms). A click calls the same `OrdersDraft` setter the
  current screen calls; nothing new is computed.
- **Hot text.** Hovering an element writes its name to the screen's hot-text
  field (the upper right on Transport, manual 2263–2264). The text is a
  Presentation name (`CommodityName`, a facility's name, a demand line's
  requirement) or a fixed label for static art. It replaces Godot tooltips on
  rebuilt screens.
- **Click targets** are the note's rectangles, not the bitmap's alpha
  (*chosen*: simpler, deterministic and testable). A drawn building on
  Industry gets the rectangle the note gives it.
- **The pop-up warehouse.** Clicking the Warehouse on Industry opens a dialog
  over the city listing the stockpile as the turn opens, from
  `CountryStatusView.Warehouse`. It **stays open** while another building's
  dialog is used (manual 2517–2521). The original also shows the deductions as
  they happen. No Presentation view gives a warehouse balance after the draft's
  orders today, so that figure is **planned**: the Industry PR adds it as a
  read-only field on `IndustryPreview` under the exception above, with a
  Presentation test, rather than subtracting in the screen. Escape or a close
  button dismisses the top dialog.
- Keyboard and the shell's existing navigation keep working; the look does not
  remove a way to reach a screen.

## Screen order and per-screen acceptance

**One screen per PR**, in this order:

1. Transport
2. Industry
3. Terrain Map chrome (the frame and side panels, not the map itself)
4. Trade
5. Technology
6. Diplomacy
7. Armoury
8. Fleet / Shipyard
9. Turn report
10. Newspaper

Before a screen's PR is opened, its layout note is committed (it may ride in
the same PR). A screen's PR is accepted when all of these hold:

- **A 1920x1080 screenshot beside the original's matching shot**, taken on
  ETHERCAT from the PR's head with the client window at 1920x1080 (so k = 2,
  centred on `#0E0905`). Both stay
  on ETHERCAT, never in the repo; the PR records only their file names and the
  differences found, each marked (a) presentation or (b) misread rule, as in
  the comparison plan.
- **No stock Godot control** from the table above remains on the screen.
- **The client smokes are unchanged**: `VIEWER_SMOKE_OK`, `SHELL_SMOKE_OK`,
  `TURN_SMOKE_OK` and the orders, units, diplomacy, fleet and armoury smokes
  print OK with the same arguments as on main.
- **Presentation tests are unchanged**, and `dotnet test` passes with no
  edit under `tests/Imperialism.Presentation.Tests` beyond a test for a
  flagged new field.
- **The manifest test passes** with every new art file named.
- **A codex-3 gate**: PASS at the PR's exact head, with CI green.

## What stays out

- **The rival-power AI.** Not started, not even its blueprint, until Andy
  says so.
- **Any rule or number change.** If a side-by-side shows a number that
  differs, it is a (b) for its own issue and its own PR, never a fix folded
  into a look PR. Fault 15's transport default (#88) is the example: it is a
  rule PR, not a look one.
- New screens the original has and we do not, and sound and music; each is
  its own decision later.
