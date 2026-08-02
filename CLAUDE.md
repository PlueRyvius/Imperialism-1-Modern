# Imperialism 1 Modern

A ground-up reimplementation of *Imperialism* (SSG/Frog City, 1997), built on
the original's own `.map`/`.scn` file formats.

**Read `README.md`, `docs/architecture.md`, and `docs/game-systems.md` before
doing design work.** This file is only a cold-session index; the repository
documentation and tests are authoritative when a summary here becomes stale.

## Orientation

| Question | Document |
|---|---|
| How is this built? | `docs/architecture.md` |
| What are we building? (the original's rules) | `docs/game-systems.md` |
| How are files laid out on disk? | `docs/file-formats.md` |
| What do the fields *mean*? | `docs/scenario-semantics.md` |
| How does legacy content become `.iworld`? | `docs/legacy-importer.md` |
| How does the Godot map viewer work? | `docs/map-viewer.md` |
| What's still unknown? | `docs/formulas/_index.md` |
| Navigating the original binary | `docs/disasm/README.md`, `docs/disasm/module-map.md` |

## Hard rules

**Never commit original game data.** `.map`, `.scn`, `.inf`, `.imp`, `.gob`,
the `.alf` disassembly, and extracted art stay in `fixtures/local_only/`
(gitignored) or outside the repo. This is repository policy regardless of a
file's legal status. CI enforces it via `tools/check_no_game_assets.py`; run it
before committing.

**Byte-exact round-trip is a hard requirement, not an aspiration.** All 30
original files (10 `.map` + 10 `.scn` + 10 `.inf`) round-trip byte-for-byte
today. Any parser change that breaks this is wrong. The trick is preserving uninterpreted
bytes — map trailer records, name padding after the null terminator, bytes
past `TERM` — and re-emitting them verbatim unless the decoded value was
actually edited.

**Historical format limits stop at the importer boundary.** The 108x60 grid
constrains the 1997 files, not this engine. Import takes a `MapFormatProfile`;
the in-memory model carries its own dimensions. Never make runtime code assume
108, 60, or 6480 outside the legacy profile/importer. Documentation and tests
may name those values only to identify the original corpus or prove a larger
regression baseline.

## Traps that have already bitten

**Province ownership is NOT derivable from the province id.** The plausible
`province_id >> 4 == country` rule holds for only 20 of 213 provinces. The
obvious check confirming it is near-vacuous — ids run 0-348 across 23
countries, so the shifted value is always a valid country id regardless. Read
ownership from the map's nation byte. Details and reasoning in
`docs/scenario-semantics.md`.

**The tactical battle engine is not optional and not a late feature.** The
original runs it for every AI-vs-AI battle in the world every turn, just
unrendered. It must be fast and headless-capable from the start.

**Verify agent and doc claims against real data before building on them.**
Two significant errors in this project's history were caught this way, both
reported confidently. Cheap to check, expensive to inherit.

## Current state

Phases 0 and 1 are complete. `src/Imperialism.Formats/` is the production .NET 8
formats library for `.map`, binary/plaintext scenarios, and editable `.inf`
files. The Python library (`src/imperialism_format/`) remains an independent
structural reference. The extensionless plaintext filenames
do not reliably pair with same-numbered `.scn` files; use
`tools/audit_scenario_corpus.py` rather than assuming equality. The Python and
C# suites cover generated fixtures and optional local corpus
gates. `tools/alf/` indexes the original binary's disassembly.
`tools/compare_format_oracles.py` compares per-field,
per-record, per-section, and preserved-byte hashes across both implementations.

Python is a **structural reference**, not an infallible oracle. A Python/C#
disagreement triggers byte-level and evidence-based triage; neither side wins
by definition. Godot and the versioned modern large-map package were delivered
in Phase 1.

`src/Imperialism.Core/` is the headless modern domain. It owns typed IDs,
arbitrary map dimensions, verified odd-row hex geometry, immutable map and
scenario definitions, and mutable world state. It must remain independent of
Godot, filesystem IO, and legacy format structures. Original files are import
inputs. `src/Imperialism.Content/` reads and compiles versioned `.iworld`
UTF-8 JSON using stable external keys and dense runtime IDs; see
`docs/modern-content-format.md`. New and imported content uses this modern
package rather than extending the 1997 layouts.

`src/Imperialism.LegacyImport/` conservatively converts viewer-ready legacy
geography and scenario setup into `.iworld`. Its report counts unsupported
gameplay and briefing data instead of copying opaque records. River byte 2 is
a per-cell path-shape code, not an edge mask; see `docs/legacy-importer.md`.

`src/Imperialism.Presentation/` keeps map projection, deterministic picking,
immutable map presentation, and detached mutable-state snapshots testable
without Godot. `src/Imperialism.Client/` is the single Godot 4.7.1 project. It
reads only `.iworld`, uses batched cell rendering, updates ownership and dynamic
features without rebuilding terrain, and offers normal and debug-overlay modes;
see `docs/map-viewer.md`.

Phase 3 is in progress. Core has packed, ownership-filtered rail connectivity
with lazy invalidation and generated coverage at 64,800 cells. It also has an
inert dense order bundle, unrestricted quarterly `TurnDate`, fixed seven-phase
`TurnResolver`, and immutable event log. `.iworld` v3 defines stable
commodities, facilities, recipes, and sparse scenario capacity, with explicit
v1→v2→v3 migration. Core stores checked dense Available inventory and
identifiable pending transport or trade deliveries. Ordered production requests
share facility capacity, stage outputs until the next turn, and commit atomically
with delivery preflight. Labour, feeding, transient power, capacity construction,
conflict, trade markets, diplomacy, ports, and river traversal remain explicitly
pending.

## Conventions

Python 3.12, dataclasses, type hints, `from __future__ import annotations`.
Docstrings explain *why*, not *what*. Tests that need real game files must
self-skip when absent (`os.path.exists` guard) so CI passes without them.
