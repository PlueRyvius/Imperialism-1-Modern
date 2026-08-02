# Imperialism 1 Modern

A ground-up reimplementation of *Imperialism* (SSG/Frog City, 1997), built on
the original's own `.map`/`.scn` file formats.

**Read `docs/architecture.md` and `docs/game-systems.md` before doing design
work.** They are the accumulated findings of this project and will save you
rediscovering them.

## Orientation

| Question | Document |
|---|---|
| How is this built? | `docs/architecture.md` |
| What are we building? (the original's rules) | `docs/game-systems.md` |
| How are files laid out on disk? | `docs/file-formats.md` |
| What do the fields *mean*? | `docs/scenario-semantics.md` |
| Which cell bytes are computed, not authored? | `docs/derived-bytes.md` |
| What's still unknown? | `docs/formulas/_index.md` |
| Where did the last session leave off? | `docs/handoff.md` |
| Navigating the original binary, and resolving a crash | `docs/disasm/README.md`, `docs/disasm/module-map.md` |

## Hard rules

**Never commit game data.** `.map`, `.scn`, `.inf`, `.imp`, `.gob`, the `.alf`
disassembly, extracted art — all copyrighted (Ubisoft). Real files live in
`fixtures/local_only/` (gitignored) or outside the repo. CI enforces this via
`tools/check_no_game_assets.py`; run it before committing.

**The `.map` trailer is a province table.** 384 slots indexed by province id,
each holding that province's town cell as a big-endian u16 at offset 4, 65535
when unused — verified on all ten maps. The other 196 bytes per record are still
unread, so the block stays preserved verbatim and `set_province_town` edits only
the field we understand. See `docs/file-formats.md`.

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

**A rule that fires on shipped data is a wrong rule, not a bad map.** This has
caught eleven so far — three map rules fitted against a single map, and four
cross-file rules that assumed a `.scn` names everything the map references. It
does not: name records are optional labels, not a registry. Hold every new
validation rule to silence across all ten scenarios before believing it.

## Current state

Python library (`src/imperialism_format/`) parses `.map`/`.scn`/`.inf` and the
plaintext scenario form, all byte-exact, 285 tests passing. `tools/alf/` indexes
the original binary's disassembly and resolves a crash to a place in it
(`python -m tools.alf.crash`). `tools/preflight.py` diffs a scenario against
the shipped corpus and reports what it holds that no shipped file does — run it
before launching a generated world. `tools/map_editor/` edits a whole scenario —
map, `.scn` identity records and `.inf` briefing — behind one undo stack, and an
edited map is confirmed to load in the real `Imperialism.exe`.
`src/imperialism_format/generate/` builds whole worlds from a keyword, modelled
on measurements of the five the game generated itself; `tools/generate_scenario.py`
writes one as a complete scenario, rivers and all. **A generated world loads in
the real game and has been played ~15 turns** — six defects found that way are
listed in `docs/handoff.md`, along with what is still known to differ. The C#
port has not started.

Point `IMP_SCENARIO_DIR` at a game install's `Scenario` folder to run the tests
against the originals without copying game data into the tree.

**`s0` is the working scenario; `s1` is the reference.** `s0` gets edited and
launched in the game to see whether it still loads, so it is never ground truth
— `tests/originals.py` excludes it, along with any scenario carrying a `.bak`.
Read `s1` when you need to know what an original looks like. Fitting a rule
against edited data is how three "never fires on shipped data" tests were
silently weakened once already.

**The map grid is odd-r offset and wraps east-west.** Bit 0 of every direction
mask is NE, proceeding clockwise. This was measured, not assumed — see
`docs/derived-bytes.md`. `src/imperialism_format/derive.py` is the one place
that encodes it; `static/render.js` mirrors it and the two must stay in step.

The Python library is retained permanently as the **reference oracle** the C#
port is tested against — it is verified, so if the two disagree, C# is at
fault.

## Conventions

Python 3.12, dataclasses, type hints, `from __future__ import annotations`.
Docstrings explain *why*, not *what*. Tests that need real game files must
self-skip when absent (`os.path.exists` guard) so CI passes without them.
