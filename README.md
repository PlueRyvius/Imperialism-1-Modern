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

*Imperialism* and everything shipped with it belong to their original copyright
holders. This is a non-commercial reimplementation project.

`docs/reference/` holds the text of the game manual, kept so its documented
mechanics can be searched and cited. Everything else in `docs/` is written in
our own words from what we have decoded, not copied from any third-party format
write-up.

No binary game data is committed: `.map`/`.scn`/`.inf` files, the disassembly
listing and game graphics are read from a local install at test time, and
`fixtures/local_only/` is gitignored for local copies.

Playing requires your own copy of the original game: assets are extracted
from your installation at import time and cached locally. We ship tools,
never content.

## Target stack

Godot 4 + C#. The simulation lives in a plain, headless, unit-testable C#
library with no Godot dependency; Godot is only the presentation layer.
This Python library is retained as research tooling and as an independent
**structural reference** for the C# parsers. A disagreement identifies a bug
to investigate; it does not make either implementation correct by definition.

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

**Phase 0 status: complete.**

- [x] Python research readers/writers for `.map`, `.scn`, editable `.inf`, and plaintext scenarios
- [x] Python plaintext-scenario reader/writer and corpus relationship audit
- [x] Production .NET 8 `Imperialism.Formats` codecs with arbitrary map dimensions
- [x] Byte-exact local verification of all 10 maps, 10 binary scenarios, and 10 INF files
- [x] Semantic local verification of all seven extensionless scenario sources
- [x] Independent C#/Python structural-hash comparison over generated and original corpora

**Phase 1 status: complete.**

- [x] Typed identifiers and dimension-independent pointy-top odd-row geometry
- [x] Immutable map/scenario definitions and mutable `WorldState`
- [x] Versioned `.iworld` packages without legacy entity or map-size limits
- [x] Conservative legacy conversion with diagnostics for deferred information
- [x] Godot 4.7.1 viewer with batched rendering, pan/zoom, picking, and debug mode
- [x] Independent static-map and mutable-state presentation layers
- [x] Local viewer verification across all ten original scenario triples

The viewer updates current ownership, rails, capitals, and quarterly date without
rebuilding immutable terrain or resetting camera and selection. Original art is
an optional local presentation source; the procedural fallback keeps assets
from blocking simulation work. The next shortest-path milestone is the Phase 3
transport graph and deterministic turn skeleton.

**Phase 3 status: in progress.**

- [x] Packed, deterministic rail-connectivity snapshots filtered by current ownership
- [x] Lazy connectivity rebuild after rail construction, removal, or conquest
- [x] Generated 64,800-cell scale regression (ten times the original map area)
- [x] Capital/depot collection and static port/depot sea gateways
- [x] Deterministic quarterly turn pipeline and immutable event log
- [x] Content-defined commodity catalog, checked inventory, and deferred-delivery intents
- [x] Content-defined facilities/recipes, shared capacity, staged output, and legacy economy import
- [x] Worker feeding, and labour priced per recipe and spent by production
- [x] Starvation and sickness cutting the labour pool from the following turn
- [x] Transport capacity construction and per-turn allocation
- [x] River downstream connectivity
- [ ] Naval control of sea ports and transient power

The tactical battle engine is deliberately early: the original runs it for
every AI-vs-AI battle in the world every turn, just unrendered, so it's
load-bearing infrastructure rather than a late feature.

### On map size

The 108x60 grid is a limit of the 1997 file format and binary, not of this
engine. Import takes a format profile; the in-memory model carries its own
dimensions. Its base sea-zone movement graph is derived from those dimensions,
not from a legacy cell count; a map opts into an east-west seam explicitly.
Keeping that boundary clean is what makes larger maps cheap —
the remaining cost is authoring effort, not engine work.

The generated Core regression uses 360x180 (64,800 cells), exactly ten times
the original cell count. Increasing both dimensions tenfold would instead be
1080x600 (648,000 cells, one hundred times the area). Core data structures
remain dimension-independent at that size, but the viewer will need chunked
rendering and packed presentation state before 648,000 cells becomes a smooth
interactive target.

### On legacy files

The original formats are import and research inputs, not the engine's native
data model. `Imperialism.Formats` preserves them faithfully at the boundary;
Core receives validated modern definitions and has no dependency on legacy
records, byte layouts, filename pairings, or historical entity limits. New and
imported content will be saved in an explicit, versioned modern package so it
can support larger maps, richer metadata, Unicode names, and future migrations.

## Documentation

| Document | Contents |
|---|---|
| `docs/architecture.md` | Target C# architecture, turn resolution, phases, verification strategy |
| `docs/game-systems.md` | How the original's systems work — the spec we're building to |
| `docs/file-formats.md` | On-disk layout of `.map`, `.scn`, `.inf` |
| `docs/scenario-semantics.md` | What the fields *mean*, verified against real data |
| `docs/formats-design-rules.md` | Rules governing the formats layer |
| `docs/modern-content-format.md` | Versioned `.iworld` content and stable-key compilation |
| `docs/legacy-importer.md` | Conservative `.map`/`.scn`/`.inf` to `.iworld` conversion and river codes |
| `docs/map-viewer.md` | Godot viewer architecture, controls, and smoke-test commands |
| `docs/formulas/_index.md` | Scoreboard for the undocumented formulas, and where to dig |
| `docs/disasm/` | Disassembly listing format and the module map |

Start with `architecture.md` for how it's built and `game-systems.md` for
what's being built.

## Layout

```
src/Imperialism.Formats/  Production .NET 8 format library
src/Imperialism.Core/     Headless modern world and simulation domain
src/Imperialism.Content/  Versioned modern world documents and compiler
src/Imperialism.LegacyImport/ Conservative Phase 1 legacy converter
src/Imperialism.Presentation/ Testable projection, picking, and viewer snapshots
src/Imperialism.Client/   Godot 4.7.1 viewer (the solution's only Godot project)
tests/                    xUnit and pytest round-trip/structural suites
fixtures/local_only/      gitignored — drop real .map/.scn here for local testing
docs/                     format notes, systems spec, architecture, research
tools/                    C# inspectors and the disassembly indexer (tools/alf)
```

## Authoring content for the original game

The world generator and the browser-based map editor live in a separate
project, **[Imperialism-1-Forge](https://github.com/PlueRyvius/Imperialism-1-Forge)**.
It generates a complete scenario from a keyword, edits one in a localhost web
app, and checks it against the shipped corpus before you launch it — all
targeting the real 1997 executable rather than this engine.

Forge owns the Python parser (`imperialism_format`) as of this repository
shedding it. It had been kept here on the grounds that it doubled as the C#
port's reference oracle, and the price was that map-editor work landed in the
port's repository, CI and review surface. This project reads the original
*executable*; reading its *data files* in Python is Forge's job.

## Running tests

```
python -m pip install -e ".[test]"
python -m pytest
dotnet test Imperialism.sln --configuration Release
```

The Python suite here is `tools/alf/` only. Asset inspection, corpus audits
and the C#-versus-Python oracle comparison moved to Forge with the parser; the
oracle comparison did not survive the move, and byte-exact round-trip on all
thirty originals is what holds the C# parser to account now.

Convert one legacy scenario triple into viewer-ready modern content:

```
dotnet run --project tools/Imperialism.LegacyImporter -- \
  --map /path/to/s1.map --scenario /path/to/s1.scn --inf /path/to/s1.inf \
  --output /path/to/s1.iworld --package-key s1 --report-json /path/to/report.json
```

Run the synthetic viewer demo, or open an imported package:

```
godot --path src/Imperialism.Client
godot --path src/Imperialism.Client -- --world /path/to/s1.iworld
godot --headless --path src/Imperialism.Client -- --smoke-test
```

Build and query the disassembly index (requires your own copy of the game;
the index is written outside the repo and is gitignored regardless):

```
python -m tools.alf.index --alf /path/to/Imperialism.alf --exe /path/to/Imperialism.exe
python -m tools.alf.query func --name UCity
```
