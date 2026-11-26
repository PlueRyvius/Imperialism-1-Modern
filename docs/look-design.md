# The look phase: screens that look like the original

Andy's ruling (26 Nov 2026): **"Yes, match original".** The Godot client's
screens are rebuilt to look like the 1997 game's, one screen at a time. Issue
#89 tracks the phase and holds the grouped list of presentation differences.

This document is the plan. Its rules are Andy's unless a line says *chosen*.

## Goal and scope

Today each screen in `src/Imperialism.Client/screens/*.cs` is a modern panel
layout: wood columns, gold plates and lists of rows, laid out by Godot
containers on a 720x450 canvas that stretches to the window (`gui-shell.md`,
"Theme and scale"). The original draws every screen as one 640x480 picture.
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

**One logical root of 640x480.** Each rebuilt screen is laid out in a
`Control` exactly 640x480 in size, with every child placed in absolute pixels
from its layout note (below). No containers decide positions on a look screen.
A position is copied from the note, never tuned by eye.

**Whole-number scaling.** The root is scaled by an integer factor and centred
in the window:

```
k = max(1, floor(min(W / 640, H / 480)))
```

`W` and `H` are the window's size in physical pixels. A 1920x1080 window gives
`min(3.0, 2.25)`, so `k = 2`: a 1280x960 picture with a 320-pixel border left
and right and a 60-pixel border top and bottom. A 1280x960 window gives 2; a
2560x1440 window gives 3; anything smaller than 1280x960 gives 1. `k` is
recomputed when the window is resized. It never takes a fractional value,
because a non-integer scale smears one-pixel lines and dithering in
palette art.

**Implementation (chosen).** `project.godot`'s base viewport becomes 640x480.
The stretch mode stays `canvas_items` (not `viewport`, which would scale text
as a bitmap), `window/stretch/aspect` becomes `keep`, and
`window/stretch/scale_mode` becomes `integer` (Godot 4.3+), which floors the
scale factor with a minimum of 1 — the formula above. If that setting behaves
differently on the Windows check, the fallback is a script on the shell root
that sets the root's scale to `k` and its position to the centred offset.
Either way the formula is the one above, and the Transport PR states which
way it was done.

**The border.** What lies outside the 640x480 picture is filled with **flat
black**, the colour the original showed around its 640x480 mode on a larger
monitor. It carries no art and takes no input. (*Chosen*: a tiled wood border
was considered and dropped, because it competes with the screen's own frame.)

**Fonts stay crisp.** The fonts are the original's own TrueType faces
(`fonts/antiqua*.ttf`, `fonts/display*.ttf` in the manifest). With
`canvas_items` stretch, Godot rasterises glyphs at the scaled size, so an 11-px
label is drawn from the outline at 22 px when `k = 2`, not doubled from an
11-px bitmap. Bitmaps are already drawn with nearest-neighbour filtering
(`project.godot`'s `default_texture_filter=0`, unchanged), so art
doubles cleanly while text is still sharp. Font sizes in a layout note are
given in 640x480 pixels, like everything else.

**Screens not yet rebuilt.** Until a screen's own PR lands, it keeps its
current layout, drawn inside the same 640x480 root. Some of today's screens
were built for 720x450 and may crowd at 640; that is accepted for the
duration, and the smokes, not the look, are the gate for an unrebuilt screen.
The canvas change itself lands with the Transport PR.

## The art

Rule 2 of the ruling: **only the original's own bitmaps**, taken through the
existing extractor and named in the manifest. **No screenshot of the original
is ever committed**, not as art, not as a reference, not as a test fixture.

- The extractor is `tools/Imperialism.AssetExtractor`; `docs/asset-pipeline.md`
  says how to run it.
- Find a screen's pieces with `--contact-sheet` on one screen and the
  reference shot on the other. Many screens are one of the sixty-four
  full-screen 640x480 backgrounds plus small overlay bitmaps (icons, slider
  arrows, bar fills).
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
  over the city listing `IndustryPreview`'s stockpile. It **stays open** while
  another building's dialog is used, so the deductions show as they happen
  (manual 2517–2521): both dialogs read the same draft preview, so it is a
  refresh, not a sum. Escape or a close button dismisses the top dialog.
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
9. Turn report and newspaper

Before a screen's PR is opened, its layout note is committed (it may ride in
the same PR). A screen's PR is accepted when all of these hold:

- **A side-by-side on the test PC at 2x** against the original shot, taken
  from the PR's head with the client at 1280x960 or larger. The side-by-side
  stays on the PC; the PR records only its file names and the differences
  found, each marked (a) presentation or (b) misread rule, as in the
  comparison plan.
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
