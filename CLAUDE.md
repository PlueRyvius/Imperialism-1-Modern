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
| Which cell bytes are computed, not authored? | Forge's `docs/derived-bytes.md` |
| How does legacy content become `.iworld`? | `docs/legacy-importer.md` |
| How does the Godot map viewer work? | `docs/map-viewer.md` |
| How does the interface shell work? | `docs/gui-shell.md` |
| Where does the original art come from? | `docs/asset-pipeline.md` |
| What fills the warehouse from the map? | `docs/formulas/extraction.md` |
| Who eats it, and what labour they supply | `docs/formulas/feeding.md` |
| How a workforce grows | `docs/formulas/migration.md` |
| How civilians improve land | `docs/formulas/development.md` |
| What a Prospector finds, and what stays hidden | `docs/formulas/prospecting.md` |
| Which technology opens which improvement level | `docs/formulas/technology.md` |
| How much the network can carry, and what that costs | `docs/formulas/transport.md` |
| Where a country's money comes from and goes | `docs/formulas/money.md` |
| How the world market clears, and what carries the cargo | `docs/formulas/trade.md` |
| How a player changes what the network reaches | `docs/formulas/engineer.md` |
| What does the manual actually specify? | `docs/reference/manual-mechanics.md` |
| What's still unknown? | `docs/formulas/_index.md` |
| Does the economy hold up over 100 turns? | `docs/formulas/soak.md` |
| Navigating the original binary, and resolving a crash | `docs/disasm/README.md`, `docs/disasm/module-map.md` |
| **What the binary actually said** | `docs/disasm/definitive-original-data.md` |
| **What we want out of the binary, and how to check it** | `docs/disasm/wanted-values.md` |
| Reading the binary's *behaviour* rather than its addresses | `docs/disasm/ghidra.md` |

## Hard rules

**Reference material lives in `docs/reference/`.** The game manual's text is
committed there so its mechanics can be searched and cited rather than
rediscovered. It settled the Resource Development Table, the technology gates on
improvement levels and the fishing rule, all of which had been guesses.

Binary game data — `.map`, `.scn`, `.inf`, `.gob`, the `.alf` disassembly —
still has no reason to be in the tree: the tests read it from
`IMP_SCENARIO_DIR` / `IMPERIALISM_SCENARIO_DIR`, and `fixtures/local_only/` is
gitignored for local copies. There is no longer an automated guard, so this is a
convention rather than a check.

**Interface art is committed**, under `src/Imperialism.Client/art/`, so the shell
runs from a clone. The constraint that remains is bookkeeping, not policy: every
file there must be named by `assets/manifest/imperialism-art.json`, and a test
enforces it, so nothing arrives without a recorded source and a re-run of the
extractor reproduces it byte for byte. See `docs/asset-pipeline.md`.

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
files, and it is now the only parser here. The extensionless plaintext filenames
do not reliably pair with same-numbered `.scn` files; do not assume equality.

**The Python in this repository is `tools/alf/` and nothing else.** It indexes
the original binary's disassembly and resolves a fault address to a place in it
(`python -m tools.alf.crash`). This project reads the original *executable*;
reading its *data files* in Python is Forge's job now.

**The map and scenario parsers moved to Forge.** `imperialism_format`, its
tests, the corpus audit tools and `docs/derived-bytes.md` live in
[Imperialism-1-Forge](https://github.com/PlueRyvius/Imperialism-1-Forge). It had
been kept here on the grounds that it doubled as the C# port's reference oracle,
and the price was that map-editor work landed in this repository. `s0`/`s1`
conventions, `IMP_SCENARIO_DIR` and the `.bak` exclusion rule all moved with it.

That cost something worth naming: `tools/compare_format_oracles.py` compared
per-field, per-record, per-section and preserved-byte hashes across the two
implementations, and it cannot live in either repository alone. What still holds
the C# parser to account is byte-exact round-trip on all thirty originals.

**The legacy map grid is odd-r offset and wraps east-west.** Bit 0 of every
direction mask is NE, proceeding clockwise. This was measured, not assumed —
Forge's `docs/derived-bytes.md` has the evidence and its `derive.py` is the one
place that encodes it. Note this describes the 1997 files: `Imperialism.Core`'s
own hex grid does not wrap.

**Scenario authoring lives in that separate project too.** The world generator,
the web map editor and `preflight.py` exist to author content for the real 1997
executable, not for this engine, and keeping them out stops that goal drifting
into the port's scope.

Godot and the versioned modern large-map package were delivered
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
without Godot. It also renders a resolved turn's events into the sentences a
player reads, because `HexMapProjectionTests` forbids Godot there and
`architecture.md` forbids a client script computing a game number.
`src/Imperialism.Client/` is the single Godot 4.7.1 project. It reads only
`.iworld`, uses batched cell rendering, updates ownership and dynamic features
without rebuilding terrain, and offers normal and debug-overlay modes; see
`docs/map-viewer.md`.

**The client ends turns now, and every country submits empty orders.** No orders
screen exists and nothing plays the other powers, so a rival that gathered its
harvest and did nothing else is the engine as it stands rather than a bug. The
turn report says so on its own face. See `docs/gui-shell.md`.

Phase 3 is in progress. Core has packed, ownership-filtered rail connectivity
with lazy invalidation and generated coverage at 64,800 cells. It also has an
inert dense order bundle, unrestricted quarterly `TurnDate`, fixed fourteen-phase
`TurnResolver` with only `Diplomacy`, `Conflict` and `TradeCancellation` still empty,
and immutable event log. `.iworld` defines stable
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
`.iworld` v10 adds the fair start: a world-level `startingDefaults` block and a
scenario-level `defaultStartCountries` list. **Defaults apply to named countries
only** — the original equips its Great Powers and not its minor nations, Core
cannot tell them apart, and applying them everywhere would arm every statelet on
the map. An explicit record still beats the default, which is what lets a
mission and a skirmish share one mechanism.

`.iworld` v11 lets industry grow: a per-facility `capacityLadder` and a
world-level cost per point. **The ladder validates building, never storing** —
53 shipped `capa` records sit off it and the importer must keep taking them. A
new `Construction` phase runs after `Production`, which is the whole of how
"completes next turn" is modelled.

`.iworld` v12 adds the Capitol: a country recruits untrained workers at
floor(provinces owned / 4) a turn, priced in canned food, clothing and
furniture. **The price per worker is a guess** — the manual names the
commodities and the cap and never the quantities. A recruit eats the turn it
arrives and supplies labour only from the next.

`.iworld` v13 puts civilians on the map. Terrain gains attributes — a display
name and `isImprovable` — replacing the bare `terrainKeys` palette, and this is
the first version bump to rename a field rather than add one. A world declares
`civilianTypes`, each deposit names the civilian that `improvedBy` raises it,
and a scenario lists its `civilians`. A new `Development` phase sits beside
`Construction` and is the only thing in the engine that creates development.

**Improvement needs terrain and deposit to agree, and they come from two
different tables.** The Terrain Tiles Table says which ground admits a worker;
the Resource Development Table says which worker raises which deposit. Neither
subsumes the other — grain names a Farmer wherever it sits, and dry plains admit
no civilian however good the grain. The corpus corroborates: 481 `deve` records
and not one on dry plains, horse ranch or scrub forest.

**Migration was inert on a food-deficit economy, and civilian units unblocked
it.** The soak's Farmers close the grain deficit by turn 4 and the Capitol
recruits for the first time on turn 6, and the workforce settles at 77. See
`docs/formulas/development.md` and `docs/formulas/migration.md`.

**That run used to overshoot and no longer does**, which is the measured work
duration showing through: at one turn the farms improved fast enough for the
population to reach 84 and outrun its own food, so a deficit reopened on turn 14
— reported as the manual's warning about growing too fast, arrived at rather than
written in. At three turns it settles at 77 and nobody is ever ill again. Both
end on 42 grain and half the workforce wants grain, so 84 needs exactly 42 and 77
needs 39: **a knife edge a measured number happened to fall the other side of.**

**How long a civilian's work takes used to be the one guess in that phase and is
not any more.** It is **3 turns**, from observed play — an iron mine takes three
turns to open and three more for each later rung. It stays in content as a
per-type `workTurns` so changing it is an edit, and applying it to the Prospector
and the Engineer is extrapolation rather than something watched.

**Moving it from 1 to 3 moved every published soak table**, and the moves are
recorded in place rather than swapped in silently: the workforce fell from 84 to
77, the first improvement slid from turn 2 to turn 4, the first gated rung from
turn 51 to turn 53, and food-first transport went from producing exactly nothing
to producing seven cycles in a century. **The grain columns did not move**, which
separates two recoveries cleanly — *where* a ceiling sits is technology, *how
fast* a country reaches it is duration.

`.iworld` v14 hides five deposits. Coal, iron, gold, gems and oil are on the map
and invisible until a Prospector of that Great Power has searched the tile —
knowledge stored as one bit per (country, cell) — and a Miner or Driller sent to
unsearched ground is refused. Terrain declares whether it may be searched and at
what price in knowledge; the deposit declares whether it hides. What a civilian's
work does is a property of its **type**, not of the order.

**Discovery gates the work order and never `Extraction`.** The manual's yield
curve already gives all five nothing at level 0 — "until a mine is built the tile
does not produce minerals" — so an undiscovered deposit pays nothing whether or
not anything checks. One gate, one place.

**Most searches find nothing, and that is the mechanic.** 449 of 2,860 barren
hills and 346 of 1,589 mountains carry a marker, so a fruitless search is a
first-class outcome that still marks the tile. Counting the corpus's searchable
ground gives 4,449 tiles — the same number `development.md` reached counting
hills and mountains for something else entirely.

**Oil is unreachable in imported content, on purpose.** The importer emits one
technology, `technology.oil-drilling`, and gives it to nobody: `tech` records are
not converted and there is no research. So no imported world may prospect swamp,
desert or tundra. That is the manual's rule applied honestly rather than a gap —
the fair start has no refinery for the same reason — and leaving the ground open
because our research is missing would invent permission the original never gave.
Re-read `docs/formulas/prospecting.md` when research lands; the gate is in
content and should need no code change.

**A built mine is visible without a survey**, so `CanSeeDeposits` is *searched or
developed* and nothing is seeded at world creation. Conquest then needs no rule
of its own: take a working mine and you may deepen it, take bare ground and you
must still look.

`.iworld` v15 gates improvement behind the manual's **Benefits of Technology
Table** — twenty-eight entries with names, benefits, prerequisites and arrival
dates, the densest recovered rules in the project. **Every improvement level is
gated except a mine opening at Level I.** Seed Drill for grain and orchards to
Level I, Steel and Iron Plows for Level II, Mechanical Reaper for Level III, and
so on per deposit. `ResourceDefinition.TechnologyByDevelopmentLevel` runs
parallel to the yield curve.

**It is a gate, not a wall.** Every power starts holding High Pressure Steam
Engine and Seed Drill — the manual says so outright — so an 1815 start still
farms and mines on turn one. That is also **the first of the seven engine
defaults to be recovered, and it came from the manual, not a decompiler.** Look
there before reaching for Ghidra for `ware` and `cash`.

**The gate governs building and never storing**, exactly as the capacity ladder
does. `s1` authors four timber tiles at Level III for a power without Dynamite
and the importer must take them.

**A `tech` record is `[country, id]`, the id a 1-based index into that table.**
Nothing names it, so the reading was falsified against the corpus before anything
was built on it: 380 authored levels permitted, 4 not. **`s3` is the decisive
case** — its powers hold unequal sets of 9, 13 and 14, and a shifted table would
fire at once on the one holding nine. It fires not at all.
`EveryAuthoredLevelInTheCorpusIsOneItsOwnerCouldHaveBuilt` keeps that in the
suite; if the count moves, the transcription moved.

**Where the soak's grain stops is now a technology question.** The farming run
reported 63 grain and 119 workers before this and reports 42 and 84 after,
because Level II and III are no longer free. The move is the finding.

**A gate with no research behind it is only ever tested closed**, so the soak
grants a technology on turn 50 and watches the ceiling lift on turn 51. That was
the pattern until v19; the soak no longer needs it and the granting run is now a
control. See below.

`.iworld` v16 gives the network a size. A new **`Transport` phase** sits between
`Extraction` and `Feeding`: extraction no longer queues what it gathers, it fills
a turn-local pool, and transport carries as much of that as capacity allows.
Sliders are per commodity against one shared bar, trimmed to what was gathered
and then to what is left, in the order the player set them.

**What the network cannot carry is lost, and that is a chosen rule** — the same
standing as which grade takes the damage in `feeding.md`. Reported in
`CommoditiesTransportedEvent.Wasted`, and **distinct from stranded**: no route at
all wants a depot, no room wants a railyard.

**The railyard is an order, not a facility**, because capacity is a country pool
and the manual gives it no ceiling. It is also the one build that costs labour;
expanding a mill does not.

**The railyard was predicted to break 2:1 and it does not.** `production.md`
carried that expectation twice and it is now retracted there rather than quietly
dropped: a capacity point costs one lumber and one steel, two inputs for one
point, so every reading of the labour sentence still agrees — and the manual never
prices the railyard's labour at all. The executable's own recipe for transport
capacity is 2 labour, 1 steel, 1 lumber, so what shipped here was right for the
wrong reason. **Canned food was the recipe that broke it**, and it was on the list
the whole time; see the binary-recovery section below.

**A power starts with a warehouse, and forgetting that produced a wrong
conclusion.** The manual says so outright — "you must construct a lumber and steel
mill with your *initial stockpiles of lumber and steel*" — so
`startingDefaults.inventory` carries lumber and steel. The **existence** is the
manual's and the **quantity is a guess**; `ware` in the seven engine defaults is
promoted accordingly.

Why it matters: the soak first concluded that a network below subsistence can
never recover, because escaping needs a railyard that needs materials that need
carrying. **That was an artefact of an empty warehouse and is retracted.** With a
stockpile the same starved country buys its way out on turn one. Likewise
"which slider comes first decides what a country becomes" holds only while the
warehouse is bare; stocked, food-first and materials-first converge and
materials-first is simply worse. **The slider order is worth as much as capacity
is scarce, and no more.**

What survives: a network under what its workforce eats costs that workforce on
the **first** turn regardless, because capacity bought on turn one does not carry
until turn two. So `tran`'s default still sets a country's opening headcount. It
is a guess in `startingDefaults.transportCapacity`; do not cite it, and do not
read a constant out of the corpus's `tran` records — `s1` gives 80–170, `s13`
gives 10–25, and `s12` gives a network to exactly one of its seven powers.

**Allocation is re-chosen every turn.** A country may put its whole network on
coal one turn and on iron the next, or split it evenly. The soak's policies hold
one fixed ordering for a century, which is a fixture simplification and not a
property of the model.

`.iworld` v18 charges a civilian for its work. **Every improvement costs cash** —
100 to reach Level I, 1,000 for Level II, 3,000 for Level III, from observed play
— charged per rung, so a worker opens a mine for 100 and comes back to pay ten
times that when the technology for the next rung arrives. **Prospecting is free.**
The manual implies the cost without printing a figure: a player might pass a turn
"when you lack the cash to pay for the civilian's improvements". **The price is
per cell and not per deposit** — a hex carrying two resources costs the same as
one, which is already how a cell's development level works. Cash leaves the
treasury when the order is given, like the Engineer's, and is not refunded.

**That makes the guessed starting treasury load-bearing twice over**: 5,000 buys
five Level II improvements, which the soak spends inside thirty-five turns. With
no income a country then stands still for the rest of the century — **an artefact
of missing trade, not a property of the model**, and the same trap the empty
warehouse set. One gold tile a power closes the loop. See
`docs/formulas/development.md`.

**A depot has two ways to be connected and only one was implemented.** The manual
gives rail to the capital *and* rail "to a tile with a port that also contains a
depot", from which goods "travel to the capital by water". The port is the sea
end and the co-located depot is the rail end; a port without one is connected for
itself and **a dead end for every depot behind it**, which the manual spells out.
This was a live bug rather than a missing subsystem — blockade is not modelled
and "in general, a port is always connected" was already the simplification in
place. Six of the ten shipped scenarios author such a hex, and `s9` and `s12`
each gained a fifth more collecting ground when it landed. `engineer.md` claimed
this needed the sea-route rules; that claim is retracted.

`.iworld` v17 gives a country **money**, and an **Engineer** to spend it. A
per-country treasury sits beside the transport pool; the one income modelled is
the one the manual pairs with it, because gold and gems "never reach the industry
warehouse" and all of both "transported convert immediately into cash". **The
manual prices both outright — $200 a unit of gold, $500 of gems** — which makes
them the strongest gameplay numbers recovered since the Resource Development
Table. Conversion happens where the goods are *carried*, so it lives in
`TransportPlanner`; they still cost capacity, which is what keeps carrying gold a
choice against carrying grain. See `docs/formulas/money.md`.

**The Engineer breaks the rule that a civilian's type decides its work**, and the
manual says it should: it is "the only civilian with multiple functions". A
`CivilianWorkKind.Construct` civilian takes `EngineerOrder` instead of
`CivilianWorkOrder`, and **which tile the order names decides the verb** — an
adjacent tile lays rail, its own tile builds a depot or a port, exactly as the
original's two cursors do. The type still decides which *family* of work is
possible; only inside construction does the order have anything left to say.
Unlike every other work order it does **not** move the civilian, because rail is
built from where the Engineer stands.

**The rail terrain gates are the best-corroborated reading in this project.**
The Benefits of Technology Table gives four — High Pressure Steam Engine for
farms, plains, deserts, forests and tundra; Iron Railroad Bridge for swamp;
Compound Steam Engine for hills; Dynamite for mountains — and reading them
against the corpus gives **1,140 rail ends permitted and none not**. It is not a
vacuous check: `s9` and `s12`, whose powers lack Compound Steam Engine, author
137 rail links with not one hill among them while `s1`, whose powers hold it,
rails forty-two. And **no shipped power holds Dynamite while no shipped scenario
rails a single mountain.** As always, the gate governs building and never
authoring. Depots reuse the rail gate on the manual's own pairing, which is an
inference and flagged as one, as are fertile hills taking the hills gate and
towns taking the plains one.

**Everything an Engineer builds costs cash, and those three prices are the
weakest numbers here** — a depot and a port are the owner's recollection from
play, and rail's is invention. The manual prices none of them and says only that
a port costs more than a depot.

**The Engineer was predicted to oppose the railyard and it does not.** The
expectation from the transport slice was that extending reach without extending
capacity would push the waste figure up; the soak gathers half again as much and
wastes exactly the same 35. The railyard is *unopposed* — nothing yet competes
for lumber and steel — so it outruns anything an Engineer can reach. Retracted in
`docs/formulas/engineer.md` rather than softened, and worth re-reading once ships
or trade land.

`.iworld` **v19** lets a country **buy technology**, and that ends the longest
running dead end in this project. Until now a `tech` record was the only source of
knowledge in the engine, so **three separate slices of gate machinery could only
ever be tested shut** — the improvement ladder, the Engineer's four rail terrains,
and oil prospecting, which `prospecting.md` recorded as making imported oil
permanently unreachable. **None of the three needed a code change.** Every gate was
already expressed in content; they only ever needed something able to pass them.

**A new `Investment` phase sits after `Delivery`, and being last is the whole
mechanism.** Everything that reads knowledge during a turn has already run, so
"bought this turn, known next turn" falls out of the ordering exactly as "completes
next turn" falls out of `Construction` following `Production`. **A chain of
prerequisites therefore takes a turn per link** — the owner's reading of the
original, where buying spends the money and the research "finishes after the turn
ends before the next starts". `TechnologyPlanner` snapshots knowledge before
spending any, so the phase never reads its own output.

**Research is charged last and takes what building leaves. A chosen rule**, and not
a formality: the soak's greedy power never accumulates the 12,000 a Mechanical
Reaper costs and its ceiling never lifts, while a patient one buys it the quarter it
arrives. Note the honest caveat — the greedy run misses by a *thousand*, so that is
a knife edge and the robust claim is only the direction.

**The prices, prerequisites and arrival dates came from a price list the owner
supplied**, transcribed from a fan wiki of the original's Investment screen. The
host 402s, so `docs/formulas/technology.md` **is** the record of it. It is
data-derived rather than remembered, which earned it a new row on the evidence scale
in `_index.md`, and it is second-hand and unverifiable, which kept it below the
manual. **Only its arrival dates survived the binary** — see the recovery section
below. The prerequisites are still its and are now the weakest column in the table.

**It reordered the table, the corpus provably could not say whether that was
right, and the binary says it was not.** A `tech` id is a bare index, so the six
moved positions change what every shipped power holds. All three corpus checks were
run under both orderings and **none discriminated** — not because the orderings agree
but because the one case where they genuinely disagree, a power holding exactly
*five* technologies, does not occur in the corpus (its counts are 0, 6, 9, 13, 14,
21). 380/4 and 1,140/0 are identical either way, and stayed identical through the
reversion. The order shipped on source quality, labelled the weakest link in the
chain, which is exactly what made replacing it cost one assertion. **Prefix closure
is a vacuous control between them and is labelled as one.**

**While reordering, the gates were name-keyed.** `ResourceTechnologyLadders`, the
four rail constants and Oil Drilling were table *positions*, so a reorder silently
rewired every gate while looking like a rename. They resolve through `TechnologyKey`
now.

**A scenario's `year` field is an offset from 1815, and the importer read it as an
absolute year.** The corpus's fields are 1, 5, 10, 11, 33, 67. `s1.inf` is titled
"Naval Competition 1882" against a field of 67 and `s3.inf` names 1848 against 33 —
both exactly `1815 + field`. **This is the fourth instance of the same pattern as
`tran` and `cash`**: nothing read the value, so nothing caught it, until arrival
dates made it load-bearing. **An unread field is unverified, not correct.** The
price list's arrival years then corroborated the epoch from a direction nobody was
looking in: `s1`, `s3` and `s9` grant nothing that has not yet arrived, and `s9`
sits exactly on a boundary year holding 9 of exactly 9 available. A skirmish starts
in **1816**, which is what the data says rather than what looks tidy.

**Rail is priced per terrain now, and that is a guess becoming an observation.**
The flat $500 this repository called "a guess. Nothing supports it at all" is gone;
the list charges 100 for plains, farm and desert, 150 for tundra and either forest,
200 for hills, 300 for swamp. The price moved to `RailRule.CashCost` beside the
gate, because a terrain that cannot carry rail needs no price. **A link pays for its
dearer end — a chosen rule**, since the list prices a ground and a link has two;
summing would double every attested figure and charging the target end would reward
building from the cheap side. **Mountains are the one ground the list skips** and
take swamp's figure rather than a fifth invented number. The depot and the port are
now the two weakest numbers in the importer.

**The v18→v19 migration deliberately does not preserve behaviour, and it is the
first here that does not.** The flat rail price is dropped rather than carried, so a
migrated package builds free track. The number is *retracted*, not superseded, and
keeping it would give an invention a longer life than it earned.

`.iworld` **v20** opens a **world market**, and that discharges a caveat four documents
were carrying. Trade is the manual's first income source and was the last one unmodelled;
`soak.md`, `money.md`, `development.md` and `technology.md` all said some version of
*"an artefact of missing trade, not a property of the model"*. **It earns 1.2 million a
century against the gold mine's 20,000** — two orders of magnitude — so the power that
could not afford a $12,000 Mechanical Reaper buys it the quarter it arrives.

**Most of the mechanism is documented, and only two numbers are not.** The manual states
offers and bids at one price nobody names, an offer passing down a ranked bidder list a part
at a time, goods sold leaving now and goods bought arriving next turn, industry claiming its
inputs first, one cargo hold per unit usable once a turn, holds spent in a fixed commodity
order, and the buyer carrying between Great Powers. What it never states is **how far a
price moves** — behind `ITradeMarket` — and **which bidder gets first refusal**, which is a
labelled placeholder because the real rule wants relations and subsidies. `_index.md`'s
priority-1 entry turned out smaller than it looked.

**The prices are observed, not guessed.** Fifteen commodities from the original's own Bid
and Offers screen, in three tiers — 100 raw, 300 material, 900 goods — where the 3× step is
structural: two inputs plus 50% value added. Two entries break the tiers and are transcribed
rather than fitted: canned food at 100 (its input is grain, which has no market price to
mark up) and horses at 300.

**What is absent from that roster corroborates the manual three times independently.** The
eight unpriced commodities are exactly raw food, gold and gems — all three stated in prose —
while canned food is present, which the manual also says. A screenshot agreeing with prose
from a different source is why `IsTradable` is transcribed rather than inferred. **Absence
of a price is what makes a commodity untradable**, the shape `TechnologyDefinition.Cost`
already uses.

**Merchant marine binds where the railyard did not.** Derived from the cargo of the ships a
country owns rather than stored, spent in the fixed commodity order, refilled each turn. The
soak sells 1,534 units and leaves **103,147 offered and unsold** — the constraint is real
and visible from turn one. Who pays is asymmetric: the buyer carries between Great Powers,
and a Great Power dealing with a minor nation always carries, because minor nations own
none. **A hold shortage is reported against whoever ran out of hulls**, which is not always
the bidder; getting that wrong was a live bug the soak caught by reporting zero refusals in
a run where the pool was visibly binding.

**The opening fleet is not an eighth engine default.** All three skirmishes give every power
three ships of type 1, independently — and `ship` is *not* one of the seven records a
skirmish omits. So six cargo holds is recoverable from the corpus where the transport pool
beside it is not. The inference was only which class, and it is no longer an inference: the
executable's array puts the Trader at index 1.

**A `ship` type index is 1-based, and the corpus proves it.** Read as 0-based it puts a
Clipper — which needs Streamlined Hulls — in an 1816 skirmish whose powers hold nothing, and
five more in `s13`/`s14`. Under 1-based, **142 records and 307 ships produce zero
contradictions** under either technology ordering. It pins which offsets are gated and says
nothing about the order within each group.

**It did not settle the technology table order, and that was the hope.** A Clipper held by a
power with 4–6 technologies would have discriminated the two orderings; no such record
exists, because the only six-technology scenarios field ungated hulls exclusively. **Third
independent corpus check to come back silent** — and then the binary answered it anyway.

## The binary answered, and four shipped tables moved

`docs/disasm/definitive-original-data.md` is an extraction from `Imperialism.exe` and
`Data/STR#ENU.GOB`. It resolved the two blocking entries on `docs/disasm/wanted-values.md`
and corrected a rule nobody had listed as open. Where it disagrees with anything, **it wins
and the other source is retracted, not balanced against.**

**The technology table's order is the manual's printed order**, so the fan-wiki reordering
shipped at v19 is retracted. One assertion changed in the whole suite:
`TechRecordsBecomeStartingKnowledge`, which pins id 5 precisely because it was one of the
six disputed positions.

**The two falsification counts are unverified against this change, and that matters.** The
analysis says 380/4 and 1,140/0 cannot move under a permutation within a prefix, and the
tests holding them return silently unless `IMPERIALISM_SCENARIO_DIR` names the full
ten-scenario corpus. Anyone with that corpus should run them; until then the counts are a
prediction. **This is the "skip visibly, never iterate an empty corpus" trap from the
Conventions section, in its C# form** — the gate returns rather than skipping, so a run
with no corpus is indistinguishable from a run that checked nothing.

**Twelve of the twenty-six technology prices were wrong, and all in the same way.** From
Streamlined Hulls onwards, every price the wiki carried is the price of the *next*
technology in the recovered order — a column read one row down. Oil Drilling is 12,000, not
25,000. The arrival years are now `1815 + the executable's turn-offset window minimum`; 25
of the wiki's 26 observed years fall inside their window, which is what pins the reading and
is also the one column of that source that survived.

**The ship array order is recovered, so nothing about ships is a guess any more.** Thirteen
classes in the executable's own order — which is what a `ship` record's 1-based type indexes
into — with cargo, sea zones, build bills and combat stats. **The Freighter carries 16**, the
last unknown cargo figure. **The Frigate takes 2 arms, not 3**, settling the discrepancy that
mattered because arms later set beachhead force size. And **the manual's "Speed" column is
sea zones**, not a sailing rate: the naval table has a separate battle-movement field, and no
sailing speed at all. The claim that armour and speed decide whether a merchant runs a
blockade was inferred from that label and is retracted.

**Labour is two per production cycle, flat.** `production.md` implemented "the recipe's total
input units" and said no shipped recipe distinguished the readings. One did, and it was on
the list the whole time: food processing takes four input units and makes two units of canned
food, and the original charges two labour for it like everything else. Canned food's labour
cost halves. **No published soak table moves** — the soak's fixture recipes were all already
costing two.

**Two searches failed for the same reason, and it is worth internalising.** The ship stats
were called "not in any file" after four pattern searches; the table was at `0x00698108` all
along, and every field the fingerprint clustered on is *encoded* — firepower ×100, armour as
its complement against 100, hull as an internal 600–2,800 scale. The technology fingerprint
was built from the wiki's price column and would have confirmed a table shifted by one.
**A fingerprint carries its source's errors and then confirms them; find the reader
function instead.** And the labour rate hunted through 59 MB of listing was printed in
English in the resource archive — **read `STR#ENU.GOB` before disassembling anything.**

**`ship` records convert.** They were deferred for want of the array order and all 142 now
import as fleets: `[country, type, zone, count]`, the zone carried and never interpreted, a
repeated class summed rather than warned about, and an unnameable type dropped rather than
clamped. A country the scenario equips takes its authored fleet instead of the fair-start
three Traders, which is Core's existing rule and needed no change.
**`EveryShipInTheCorpusIsAHullItsOwnerCouldHaveBuilt` is the falsification check** —
`s1`'s 29 records / 59 hulls are measured, the 142/307 totals are transcribed from
`trade.md` and want a full corpus to confirm.

**What is recorded and deliberately unused.** The army purchase and stat tables, the map
geometry, the town-development rules, the power conversion (1 fuel → 6 power, 100 fuel a
turn) and worker training (1 paper + $100 to train, 2 paper + $1,000 to promote) are all in
the extraction and modelled nowhere. So is every entry marked `candidate` — including a
nine-record cost block that *looks* like the civilian purchase list and whose first figure
happens to match our remembered depot cost. **Do not promote a candidate without tracing its
selector.**

Transient power, research *progress*, conflict, diplomacy, trade subsidies, sea routes
between ports, blockade, moving regiments by rail, building ships, the University, the
newspaper and fortifications remain explicitly pending. So does
**whether a civilian's work costs cash generally** — the manual implies it does, and
`docs/formulas/money.md` records the finding without acting on it, because pricing
every civilian's work would move every number in the soak.

**Production spends labour: two per cycle, flat**, from one pool per country shared
across every facility. The manual prices exactly one recipe — a unit of clothing
costs two fabric and two labour — and the original's own help resources charge the
same two for all nine of theirs, food processing's four-input cycle included. The
"total input units" rule this repository shipped is retracted; so is the standing
claim that no recipe distinguished the readings. Read
`docs/formulas/production.md` before changing the rate.

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

**`civi` records name no owner.** The record is `[type, cell]`; the owner is the
owner of the province the cell sits in. Verified across the whole corpus — all
210 stand on owned land and every owner holds a capital.

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
