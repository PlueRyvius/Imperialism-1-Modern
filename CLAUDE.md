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
| What's still unknown? | `docs/formulas/_index.md` |
| Navigating the original binary | `docs/disasm/README.md`, `docs/disasm/module-map.md` |

## Hard rules

**Never commit original game data.** `.map`, `.scn`, `.inf`, `.imp`, `.gob`,
the `.alf` disassembly, and extracted art stay in `fixtures/local_only/`
(gitignored) or outside the repo. This is repository policy regardless of a
file's legal status. CI enforces it via `tools/check_no_game_assets.py`; run it
before committing.

**Byte-exact round-trip is a hard requirement, not an aspiration.** All 20
original files (10 `.map` + 10 `.scn`) round-trip byte-for-byte today. Any
parser change that breaks this is wrong. The trick is preserving uninterpreted
bytes — map trailer records, name padding after the null terminator, bytes
past `TERM` — and re-emitting them verbatim unless the decoded value was
actually edited.

**Historical format limits stop at the importer boundary.** The 108x60 grid
constrains the 1997 files, not this engine. Import takes a `MapFormatProfile`;
the in-memory model carries its own dimensions. Never hardcode 108, 60, or
6480 outside the importer.

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

Phase 0 is in progress. The Python library (`src/imperialism_format/`) provides
byte-exact `.map`/`.scn` readers and writers, `.inf` parsing, and semantic
plaintext-scenario read/write support. The extensionless plaintext filenames
do not reliably pair with same-numbered `.scn` files; use
`tools/audit_scenario_corpus.py` rather than assuming equality. The suite has
57 tests at this point. `tools/alf/` indexes the original binary's disassembly.
The production C# formats port has not started.

Python is a **structural reference**, not an infallible oracle. A Python/C#
disagreement triggers byte-level and evidence-based triage; neither side wins
by definition. Editable `.inf` writing and the independent C# cross-check are
still required before Phase 0 is complete.

## Conventions

Python 3.12, dataclasses, type hints, `from __future__ import annotations`.
Docstrings explain *why*, not *what*. Tests that need real game files must
self-skip when absent (`os.path.exists` guard) so CI passes without them.
