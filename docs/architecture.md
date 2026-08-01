# Architecture

Target stack is **Godot 4 + C#**. The simulation is a plain, headless,
unit-testable C# library with no Godot dependency; Godot is only the
presentation layer.

## Solution layout

One solution. Godot owns exactly one csproj; everything else is plain
`net8.0` classlib.

```
src/
  Imperialism.Formats/   binary + text IO, zero game rules
  Imperialism.Core/      the simulation — no Godot, no IO, no float
  Imperialism.Content/   static rules data (JSON) + immutable RuleSet
  Imperialism.AI/        strategic, tactical and advisor AI
  Imperialism.Assets/    .gob extraction, cache, upscale orchestration
  Imperialism.Headless/  exe: batch sims, replay, oracle diffing
  Imperialism.Client/    Godot csproj — presentation only
tests/   Formats.Tests, Core.Tests, AI.Tests, Golden.Tests
tools/   Python: format oracle, .gob unpack, upscale, .alf indexing
docs/
```

`Core` sub-namespaces deliberately mirror the original's module names, which
the binary leaks via assert paths (see `disasm/module-map.md`) — `Core.Economy`
↔ `UCity`, `Core.Diplomacy` ↔ `UAmbit`, `Core.Military` ↔ `UArmyMgr`/`UNavy`.
This keeps reverse-engineering notes mapping 1:1 onto our code.

### Enforced boundaries

An architecture test asserts the compiled `Core` assembly references none of
the following. A convention that isn't compiled is a convention that decays.

- **No `Godot.*`** — define our own `HexCoord`/`CellIndex`. `Vector2I` is
  right there and it is poison.
- **No `System.IO`** — content is parsed by the host and handed in already
  parsed.
- **No `System.Random`** — its algorithm is not contractually stable across
  runtimes, which would break replay.
- **No `float`/`double`.** Use `int` and a fixed-point type. The original was
  integer-heavy 1997 C++, so this both matches it more closely and gives
  bit-identical cross-platform replays.

## World data boundaries

The legacy codecs are adapters, not the domain model. They decode original
files into preserved format documents; a converter will then validate and map
those documents into Core definitions. Core does not know about 36-byte cells,
tagged scenario records, CP1252, raw padding, or original filename conventions.

Modern content uses three layers:

1. `MapDefinition` is immutable geography: dimensions, cells, terrain,
   provinces, sea zones, resources, and settlements.
2. `ScenarioDefinition` is an immutable starting setup over a map, initially
   including year and province ownership.
3. `WorldState` is the mutable state of one running game. It copies scenario
   values on creation, so loading or simulating a game cannot mutate reusable
   content definitions.

Runtime identifiers are compact typed integers and definitions are dense by
identifier for predictable lookup and cache behavior. The future modern
package may use stable textual keys; its loader is responsible for validating
and remapping those keys to dense runtime identifiers. This keeps save and
authoring stability separate from simulation storage.

Cells may contain more than the legacy format's resource count. Names are
Unicode strings and no original country/province ceiling is part of Core.
These freedoms are tested so an importer detail cannot silently become an
engine constraint.

## Hex coordinates

The original map evidence identifies a pointy-top, odd-row offset grid
(`odd-r`): odd-numbered rows are shifted right. Storage is row-major and
`index = row * width + column`. Direction values retain the original six-bit
ordering because it is useful at the import boundary:

| Direction | Bit | Even row delta | Odd row delta |
|---|---:|---:|---:|
| NE | 1 | `(0,-1)` | `(+1,-1)` |
| E  | 2 | `(+1,0)` | `(+1,0)` |
| SE | 4 | `(0,+1)` | `(+1,+1)` |
| SW | 8 | `(-1,+1)` | `(0,+1)` |
| W  | 16 | `(-1,0)` | `(-1,0)` |
| NW | 32 | `(-1,-1)` | `(0,-1)` |

This interpretation was checked against original rail reciprocity and named
city indices. Core owns the coordinate and adjacency math, clips neighbors at
explicit map dimensions, and converts through axial coordinates for distance.
No client pixel coordinate enters the simulation API.

## Simultaneous turn resolution

Mutable `GameState` POCO, **inert** order objects (data only — no `Execute()`
method), and a fixed phase pipeline emitting an event log. Not event
sourcing, not snapshot-and-diff.

```
TurnResolver.Resolve(GameState, TurnOrders[7], seed)
```

Phases run in the original's fixed order: Diplomacy → Trade → Production →
Conflict → TradeCancellation → Delivery → Connectivity.

**The central trick.** The original's step 5 retroactively cancels trades that
step 4's blockades invalidated. That's only a hard rollback if trade committed
something. So `TradePhase` writes *pending shipment intents* and commits
nothing physical; `TradeCancellationPhase` becomes a filter over a list rather
than an undo. This is also the faithful reading: most physical output from a
turn is a queue of deferred effects.

Money is the only thing that genuinely reverses. Model it as a ledger where
cancellation appends a compensating entry, never by mutating history.

**Structural guarantees against order-dependence.** Powers live in a
fixed-length array indexed by country id, so unordered iteration is
impossible. Any `foreach` over a dictionary inside a phase is a latent bug.
Genuine contention (two powers invading one province) resolves by an explicit
seeded tiebreak, never by iteration order.

**Deferred delivery is modelled once, with explicit exceptions.** Warehouse
stock is `Available`; transport and trade create `PendingDelivery` entries.
Production cannot generally use pending goods, but worker feeding consumes
transported raw food from `PendingDelivery` before warehouse food, matching
the original's documented priority. Power is separate transient labour: it is
created and consumed during the same production phase and never enters either
inventory. After blockade cancellation and food consumption, `Delivery`
commits the remaining pending goods for use on the following turn.

**The event log is the presentation contract.** The client animates the log
and never diffs state. This gives newspaper and battle-report views nearly
free, and makes headless mode provably identical to rendered mode.

## Tactical engine

A pure function: setup + RNG + optional observer → result. The observer is
null for the dozens of unrendered AI battles per turn and non-null (recording
a frame stream) for the player's, so a watched battle is a replay of a
recorded stream and both paths are provably the same battle.

Struct-of-arrays, no inner-loop allocation, target under 1 ms per battle with
a benchmark test enforcing it. Not async — for human input, use a controller
interface plus an explicit step the client pumps.

## Determinism

Implement the RNG ourselves (xoshiro/PCG-class). No global instance; derive
per-context streams from `(seed, turn, phase, entity)` so that fixing a combat
bug doesn't reshuffle diplomacy, and any single battle can be unit-tested with
its exact stream.

Saves use an explicit versioned reader/writer from v1 — never reflection-based
serialization, which couples every refactor to save compatibility. Embed a
state hash so a load-hash mismatch is an instant alarm.

## Phases

Shortest path to playable is 0 → 1 → 3 → 4 → 5; phase 2 slots in whenever
wanted.

| # | Deliverable |
|---|---|
| 0 | Format library: `.map`/`.scn`/`.inf` + text scenarios, byte-exact round-trip |
| 1 | World model + map viewer: hex grid, provinces, ownership, pan/zoom |
| 2 | Asset pipeline: `.gob` extraction, palettes, cache, optional upscale |
| 3 | Turn skeleton + transport connectivity + economy |
| 4 | Tactical battle engine, built and tested standalone |
| 5 | **First playable**: 2-power game, movement, capture, save/load |
| 6 | Trade auction + diplomacy + blockades |
| 7 | Minor nations, Council of Governors, victory |
| 8 | AI depth, tuned via headless tournaments |
| 9 | Polish, scenario editor, house rules |

Tactical combat is deliberately early because the original runs its battle
engine for every AI-vs-AI fight every turn — it is load-bearing infrastructure,
not a late feature.

## Risk sequencing

Every undocumented formula gets an **interface plus a plausible placeholder**,
so reverse-engineering runs as a parallel workstream and **no phase ever
blocks on a disassembly finding**. Concretely, trade ships as a fixed-price
implementation behind an interface long before the real auction exists.

Ordered by impact × uncertainty: trade pricing → transport connectivity →
combat constants → relation deltas → council curve → AI quality. Connectivity
is second not because it's uncertain but because its blast radius is enormous
and silent — discovering it's wrong late invalidates every golden trace.

## Map size

The 108×60 grid constrains the 1997 file format and binary, not this engine.
Import takes a format profile; the in-memory model carries its own dimensions.
Keeping that boundary clean is what makes larger maps cheap.

Downstream requirements: hex adjacency must be dimension-agnostic, the
connectivity graph must not assume a fixed cell count, and new sizes need a
content format of our own with dimensions in its header. The remaining cost is
authoring effort, not engine work.

## Traps to avoid

- Logic in the client. Views are id-keyed and rule-free; if a client script
  computes a game number, that's a bug.
- Naive connectivity recompute. Design the API as invalidate-plus-lazy-recompute
  from the start, even if v1's body is a full traversal.
- Premature ECS. Entity counts are in the hundreds and the logic is relational
  and rule-heavy; ECS would obscure the phase pipeline. Plain arrays and ids.
  Struct-of-arrays belongs inside the tactical engine only.
- Over-abstracting for future map sizes. Don't build a generic map engine —
  just never hardcode dimensions outside the importer.
- Letting the tactical engine grow an implicit dependency on strategic state.
  Everything goes through the battle setup, or battles stop being testable in
  isolation.

## Verification

- Byte-exact round-trip on every original file is a hard requirement at the
  legacy boundary and is independently demonstrated in Python and C#.
- Core geometry is tested with exhaustive coordinate round-trips,
  neighbor/opposite properties, arbitrary dimensions, and boundaries that
  prove edges never wrap into another row.
- Cross-check: C# and the Python structural reference must agree on every
  corpus file. A disagreement triggers byte-level and evidence-based triage;
  neither implementation wins by definition.
- Arbitrary-dimension tests, to stop 108×60 creeping back in as an assumption.
- Plaintext corpus audit: every source must parse, and aggregate comparisons
  track exact, ordered-subset, near, and unrelated binary relationships. The
  shipped filenames are not assumed to identify matching pairs.
- Golden replay from day one: record `(seed, scenario, orders)`, assert replay
  reproduces the per-turn state-hash sequence. An all-AI long-run trace is the
  canary for every refactor.
- Property tests on the two silent-corruption risks: connectivity (adding rail
  never disconnects; losing a province never increases connectivity) and the
  auction (goods conserved, no overselling, no hold used twice).
