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
| Which cell bytes are computed, not authored? | `docs/derived-bytes.md` |
| How does legacy content become `.iworld`? | `docs/legacy-importer.md` |
| How does the Godot map viewer work? | `docs/map-viewer.md` |
| What fills the warehouse from the map? | `docs/formulas/extraction.md` |
| Who eats it, and what labour they supply | `docs/formulas/feeding.md` |
| What does the manual actually specify? | `docs/reference/manual-mechanics.md` |
| What's still unknown? | `docs/formulas/_index.md` |
| Navigating the original binary, and resolving a crash | `docs/disasm/README.md`, `docs/disasm/module-map.md` |

## Hard rules

**Reference material lives in `docs/reference/`.** The game manual's text is
committed there so its mechanics can be searched and cited rather than
rediscovered. It settled the Resource Development Table, the technology gates on
improvement levels and the fishing rule, all of which had been guesses.

Binary game data — `.map`, `.scn`, `.inf`, `.gob`, the `.alf` disassembly,
extracted art — still has no reason to be in the tree: the tests read it from
`IMP_SCENARIO_DIR` / `IMPERIALISM_SCENARIO_DIR`, and `fixtures/local_only/` is
gitignored for local copies. There is no longer an automated guard, so this is a
convention rather than a check.

**The `.map` trailer is a province table.** 384 slots indexed by province id,
each holding that province's town cell as a big-endian u16 at offset 4, 65535
when unused — verified on all ten maps. The other 196 bytes per record are still
unread, so the block stays preserved verbatim and `set_province_town` edits only
the field we understand. See `docs/file-formats.md`.

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

**A rule that fires on shipped data is a wrong rule, not a bad map.** This has
caught eleven so far — three map rules fitted against a single map, and four
cross-file rules that assumed a `.scn` names everything the map references. It
does not: name records are optional labels, not a registry. Hold every new
validation rule to silence across all ten scenarios before believing it.

**That rule is about the format, not about the game.** Read the next section
before applying it to a gameplay decision; carrying it across the line is the
one mistake this project keeps making.

## Two domains, two authorities

Confusing them is the main source of self-inflicted friction here.

| | Authority | The rule |
|---|---|---|
| **Format / importer** | the corpus | Never fires on shipped data. Byte-exact round-trip. |
| **Gameplay** | the manual, then the engine | Scenario data is *authoring*, not design. |

**The ten scenarios are authored missions, not a picture of how the game
plays.** Most games are skirmishes: every power starts identical, so the start
is fair. Three of the nine untouched scenarios are shaped that way and agree
exactly with each other:

| | |
|---|---|
| Textile / steel / lumber mill | **2** each |
| Clothing factory / metal works / furniture factory | **1** each |
| Refinery | **absent** — gated behind Oil Drilling |
| Workforce | **[4 untrained, 2 trained, 1 expert]** |

`s10`, `s11` and `s15`, independently. That is exactly the manual's construction
floor — a mill is always built at 2, a factory begins at 1 — so **the fair start
and the bottom rung of the build ladder are the same thing.** Nothing there
needs inventing.

The other six missions author whatever they like, including 53 `capa` records
that sit off the build ladder entirely. Both facts live together without
contradiction once the domains are separate: **the ladder governs what a player
may build; a scenario may author anything; the importer must accept both.**

So: a mission's numbers constrain the importer and never the rules. Do not mine
`capa`, `labo` or `tran` for gameplay constants — that is reading six authored
special cases as if they were the design.

**A symmetric default is a design decision, not an invented number**, and does
not need apologising for in a formula document. What does need flagging is a
number chosen with no reasoning behind it at all.

**Seven values a skirmish leaves entirely to the engine.** `s10` carries none of
`ware`, `cash`, `deve`, `tech`, `tran`, `rail` or `rela`, so a fair start runs on
built-in defaults for all seven. They are constants in the binary and are not
recoverable from the corpus at all. See `docs/formulas/_index.md`.

## Current state

Phases 0 and 1 are complete. `src/Imperialism.Formats/` is the production .NET 8
formats library for `.map`, binary/plaintext scenarios, and editable `.inf`
files. The Python library (`src/imperialism_format/`) remains an independent
structural reference. The extensionless plaintext filenames
do not reliably pair with same-numbered `.scn` files; use
`tools/audit_scenario_corpus.py` rather than assuming equality. The Python and
C# suites cover generated fixtures and optional local corpus
gates. `tools/alf/` indexes the original binary's disassembly and resolves a
fault address to a place in it (`python -m tools.alf.crash`).
`tools/compare_format_oracles.py` compares per-field,
per-record, per-section, and preserved-byte hashes across both implementations.

Point `IMP_SCENARIO_DIR` at a game install's `Scenario` folder to run the Python
tests against the originals without copying game data into the tree.

**`s0` is the working scenario; `s1` is the reference.** `s0` gets edited and
launched in the game to see whether it still loads, so it is never ground truth
— `tests/originals.py` excludes it, along with any scenario carrying a `.bak`.
Read `s1` when you need to know what an original looks like. Fitting a rule
against edited data is how three "never fires on shipped data" tests were
silently weakened once already.

**The legacy map grid is odd-r offset and wraps east-west.** Bit 0 of every
direction mask is NE, proceeding clockwise. This was measured, not assumed — see
`docs/derived-bytes.md`. `src/imperialism_format/derive.py` is the one place
that encodes it. Note this describes the 1997 files: `Imperialism.Core`'s own
hex grid does not wrap.

**Scenario authoring lives in a separate project.** The world generator, the web
map editor and `preflight.py` are in
[Imperialism-1-Forge](https://github.com/PlueRyvius/Imperialism-1-Forge), which
consumes this repository's `imperialism_format` package. They exist to author
content for the real 1997 executable, not for this engine, and keeping them out
stops that goal drifting into the port's scope.

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
inert dense order bundle, unrestricted quarterly `TurnDate`, fixed nine-phase
`TurnResolver`, and immutable event log. `.iworld` defines stable
commodities, facilities, recipes, and sparse scenario capacity, with explicit
v1→v2→v3 migration. Core stores checked dense Available inventory and
identifiable pending extraction, transport or trade deliveries. Ordered production
requests share facility capacity, stage outputs until the next turn, and commit
atomically with delivery preflight. `.iworld` v7 adds a per-deposit yield curve indexed by
development level, an optional technology requirement, a technology catalog,
sparse starting development, ports, port fishing, and rail depots. Gathering
happens at **connected depots, connected ports and the capital** — never at bare
track. Deposits within the catchment of one of those pay their owner each turn
through the `Extraction` phase, scaled by the cell's development level; ports
and the capital fish their adjacent water; unreachable output is reported as
stranded rather than dropped. `.iworld` v8 adds the workforce and feeding: every
worker eats one unit a turn on the grain / fruit / grain / meat cycle, canned
food substitutes without illness, the wrong food means sick and no labour, and
nothing at all means permanent loss. `.iworld` v9 prices each recipe's
`labourCost`, deriving it for older packages as the recipe's input total.
Transient power, capacity construction, research, conflict, trade markets,
diplomacy, sea routes between ports, blockade, and the transport capacity pool
remain explicitly pending.

**Production spends labour.** Each recipe costs its total input units, from one
pool per country shared across every facility. The manual prices exactly one
recipe — a unit of clothing costs two fabric and two labour — and settles the
rest only because every shipped recipe consumes two input units per unit of
output, which collapses the competing readings of that sentence into the same
number. A recipe that broke 2:1 would separate them; the railyard will be the
first. Read `docs/formulas/production.md` before changing the rate.

**Starving and sickening both cost labour on the *next* turn**, because
`Production` runs before `Feeding`. A workforce works the turn it dies or falls
ill; the turn after is the first whose orders could have been given knowing.
Which grade takes the damage — cheapest first, starvation before illness — is
**the one chosen rule in the model**, not a finding. It is in
`docs/formulas/feeding.md` under that heading; don't cite it as evidence.

**The game manual is in `docs/reference/` and it is authoritative for numbers.**
It carries a Resource Development Table giving every deposit's yield at each of
the four levels, names the technologies gating each improvement, and states the
fishing rule. It has already corrected shipped code once: the yield curve used
to double and is in fact linear with a slope that differs per deposit. Read
`docs/reference/manual-mechanics.md` before inventing an extraction number.

**`deve` records can repeat a cell.** `s1` does it three times. The importer
keeps the highest level and warns. Erroring on it was the first implementation
and the corpus rejected it on the first run.

**`rail` scenario records are depots, not track.** The map's rail byte carries
the lines; the records name the depots built on them. They are a strict subset
of railed cells (76 of 310 in `s1`) and no two sit within two tiles, exactly as
the manual advises. Bare track gathers nothing — assuming otherwise cost `s1`
185 phantom collection points.

**The legacy grid wraps east-west; `Imperialism.Core`'s does not.** Any rule
about a cell's neighbours must say which grid it means. `s3` has a port whose
only water is across the seam, so "a port touches water" is true of the 1997 map
and false of ours — the check lives in the importer, with wrapping adjacency, and
Core checks only that a port is on land. That is the second rule the corpus has
overturned; convert the whole corpus before believing a new one
(`TheWholeShippedCorpusConvertsWhenItIsConfigured`).

## Conventions

Python 3.12, dataclasses, type hints, `from __future__ import annotations`.
Docstrings explain *why*, not *what*. Tests that need real game files must
self-skip when absent so CI passes without them — but **skip visibly, never
iterate an empty corpus**. `for path in originals.maps():` over an empty list
reports success having opened nothing, and that is the state CI runs in. Eleven
tests holding rules to "exact on every shipped map" were green that way. Use
`originals.require_maps()` / `require_scenarios()` / `require_infs()`, which
skip out loud when there is no corpus and fail when `IMP_SCENARIO_DIR` is set
but does not hold one. The C# gates take the same shape: return when the
variable is unset, assert the count when it is not.
