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

## Layout

```
src/imperialism_format/   Python library: MapFile, HexCell, ScenarioFile, Record
tests/                    pytest suite (round-trip + structural tests)
fixtures/local_only/      gitignored — drop real .map/.scn here for local testing
docs/                     our own notes on the file formats
tools/                    semantic inspection utilities
```

## Running tests

```
python -m pip install -e ".[test]"
python -m pytest
```

Inspect source files as stable JSON:

```
python tools/inspect_assets.py Scenario/s1.map Scenario/s1.scn Scenario/s1.inf
```
