# Imperialism 1 Modern

A ground-up modern reimplementation of *Imperialism* (SSG/Frog City, 1997),
starting from the game's own `.map`/`.scn` scenario file formats.

## Why this is tractable

The original game's file formats have been reverse-engineered by the
community (cell-by-cell `.map` layout, tagged-record `.scn` layout). This
repo starts from a Python re-implementation of that decoding. The parsers
preserve uninterpreted bytes and round-trip every original file
byte-for-byte, so the engine can load real scenario data from day one
instead of needing new assets from scratch.

The project is **disassembly-informed**: where the original's rules are
undocumented (trade pricing, diplomatic relations, council votes), we
consult a disassembly of the original executable and reimplement the
behaviour in our own code. Findings flow one way — disassembly →
`docs/formulas/` → our code plus a test. The game itself never reads any
original binary.

The original `.map` format does not store map dimensions. Importing one
therefore requires a format profile (108 x 60 for the original game), but
dimensions belong to the in-memory map model and may be changed for new
content. The historical file format does not set the modern engine's limit.

## Legal note

This repo contains **no original game assets or data**. `.map`/`.scn`
files, the disassembly listing, game graphics, and text belong to their
original copyright holders and are never committed here — see
`.gitignore` (`fixtures/local_only/` is for local testing only).
Documentation in `docs/` is written in our own words from what we've
decoded, not copied from any third-party format write-up.

Playing requires your own copy of the original game: assets are extracted
from your installation at import time and cached locally. We ship tools,
never content.

## Target stack

Godot 4 + C#. The simulation lives in a plain, headless, unit-testable C#
library with no Godot dependency; Godot is only the presentation layer.
This Python library is retained as research tooling and as the **reference
oracle** the C# parsers are tested against.

## Project phases

Shortest path to something playable is 0 → 1 → 3 → 4 → 5; phase 2 slots in
whenever it's wanted.

| Phase | Goal |
|---|---|
| 0 | Format library: `.map`/`.scn`/`.inf` + text scenarios, byte-exact round-trip |
| 1 | World model + map viewer: hex grid, provinces, ownership, pan/zoom |
| 2 | Asset pipeline: `.gob` extraction, palettes, cache, optional upscale |
| 3 | Turn skeleton + transport connectivity graph + economy |
| 4 | Tactical battle engine (headless-capable, built and tested standalone) |
| 5 | **First playable**: 2-power game, movement, capture, save/load |
| 6 | Trade auction + diplomacy + blockades |
| 7 | Minor nations, Council of Governors, victory |
| 8 | AI depth, tuned via headless tournaments |
| 9 | Polish, scenario editor, house rules |

The tactical battle engine is deliberately early: the original runs it for
every AI-vs-AI battle in the world every turn, just unrendered, so it's
load-bearing infrastructure rather than a late feature.

### On map size

The 108x60 grid is a limit of the 1997 file format and binary, not of this
engine. Import takes a format profile; the in-memory model carries its own
dimensions. Keeping that boundary clean is what makes larger maps cheap —
the remaining cost is authoring effort, not engine work.

## Documentation

| Document | Contents |
|---|---|
| `docs/architecture.md` | Target C# architecture, turn resolution, phases, verification strategy |
| `docs/game-systems.md` | How the original's systems work — the spec we're building to |
| `docs/file-formats.md` | On-disk layout of `.map`, `.scn`, `.inf` |
| `docs/scenario-semantics.md` | What the fields *mean*, verified against real data |
| `docs/derived-bytes.md` | Which cell bytes are computed from neighbours, and how well each rule fits |
| `docs/handoff.md` | Where the last session left off, and what is still open |
| `docs/formats-design-rules.md` | Rules governing the formats layer |
| `docs/formulas/_index.md` | Scoreboard for the undocumented formulas, and where to dig |
| `docs/disasm/` | Disassembly listing format and the module map |

Start with `architecture.md` for how it's built and `game-systems.md` for
what's being built.

## Layout

```
src/imperialism_format/   Python library: MapFile, HexCell, ScenarioFile, Record
tests/                    pytest suite (round-trip + structural tests)
fixtures/local_only/      gitignored — drop real .map/.scn here for local testing
docs/                     format notes, systems spec, architecture, research
tools/                    inspection utilities and the disassembly indexer
tools/map_editor/         browser-based .map editor for the original game
```

## World generation

```
python tools/generate_scenario.py --seed Pippin --template /path/to/Scenario/s1.map --out /path/to/Scenario/s5
```

Builds a complete scenario — `.map`, `.scn`, `.inf` — from a keyword, the way
the original does ("Imperialism generates random worlds based on a key word").
The model is measured from the five worlds the game's own generator produced,
which ship as the tutorial scenarios. A template `.map` is required: the
province table at the end of the format is only partly decoded, so a generated
map inherits a real one's and rewrites just the field we understand.

## Map editor

Double-click `tools/map_editor/Map Editor.bat`, or from a terminal:

```
python tools/map_editor/server.py fixtures/local_only/s1.map
```

A localhost web app for painting terrain, resources, provinces, nations, towns,
rivers and rail, with borders and shorelines recomputed as you draw. The server
owns the file and does all the parsing, so the browser never handles map bytes
and undecoded parts of the format survive editing untouched. See
`tools/map_editor/README.md`.

## Running tests

```
python -m pip install -e ".[test]"
python -m pytest
```

Inspect source files as stable JSON:

```
python tools/inspect_assets.py Scenario/s1.map Scenario/s1.scn Scenario/s1.inf
```

Build and query the disassembly index (requires your own copy of the game;
the index is written outside the repo and is gitignored regardless):

```
python -m tools.alf.index --alf /path/to/Imperialism.alf --exe /path/to/Imperialism.exe
python -m tools.alf.query func --name UCity
```
