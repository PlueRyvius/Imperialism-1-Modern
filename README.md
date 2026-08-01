# Imperialism 1 Modern

A ground-up modern reimplementation of *Imperialism* (SSG/Frog City, 1997),
starting from the game's own `.map`/`.scn` scenario file formats.

## Why this is tractable

The original game's file formats have been reverse-engineered by the
community (cell-by-cell `.map` layout, tagged-record `.scn` layout). This
repo starts from a clean-room Python re-implementation of that decoding.
The parsers preserve uninterpreted bytes and are tested against a local
corpus of original and fan-created files, so the engine can load real
scenario data from day one instead of needing new assets from scratch.

The original `.map` format does not store map dimensions. Importing one
therefore requires a format profile (108 x 60 for the original game), but
dimensions belong to the in-memory map model and may be changed for new
content. The historical file format does not set the modern engine's limit.

## Legal note

This repo contains **no original game assets or data**. `.map`/`.scn`
files, game graphics, and text belong to their original copyright
holders and are never committed here — see `.gitignore`
(`fixtures/local_only/` is for local testing only). Documentation in
`docs/` is written in our own words from what we've decoded, not copied
from any third-party format write-up.

## Project phases

| Phase | Goal |
|---|---|
| 0 | `.map`/`.scn` parser library (this repo's starting point) |
| 1 | Godot map viewer: load a `.map`, render the hex grid, pan/zoom, minimap |
| 2 | Load `.scn` on top: countries, provinces, borders, army/ship placement (read-only) |
| 3 | Interactive sandbox: select provinces/units, move units, no rules yet |
| 4 | Economy simulation: production chains, transport, warehouses |
| 5 | Diplomacy + AI opponents |
| 6 | Combat resolution (land + naval) |
| 7 | Full turn loop, victory conditions, save/load |
| 8 | Polish pass |

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
