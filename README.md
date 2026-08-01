# Imperialism 1 Modern

A ground-up modern reimplementation of *Imperialism* (SSG/Frog City, 1997),
starting from the game's own `.map`/`.scn` scenario file formats.

## Why this is tractable

The original game's file formats have been reverse-engineered by the
community (cell-by-cell `.map` layout, tagged-record `.scn` layout). This
repo starts from a clean-room Python re-implementation of that decoding,
verified to **round-trip real game files byte-for-byte** (see
`tests/`), so the engine we build on top can load actual scenario data
from day one instead of needing new assets from scratch.

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
```

## Running tests

```
pip install pytest
python -m pytest
```
