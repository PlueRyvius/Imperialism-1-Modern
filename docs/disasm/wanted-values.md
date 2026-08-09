# Values wanted from the binary

A shopping list for anyone decompiling `Imperialism.exe`, in priority order. Each entry
says **what is wanted**, **how to recognise it**, and **how we will check the answer** —
the last matters because most of these already have a test that will fire if the value is
wrong.

Read [`ghidra.md`](ghidra.md) first for setup. Nothing here needs the game running.

**Two of the five entries below are struck through**, answered by the extraction recorded
in [`definitive-original-data.md`](definitive-original-data.md). They are kept rather than
deleted, because each was wrong about something instructive:

- **Entry 1 said there was no static ship table.** There is. Every search missed it because
  the fields are encoded, and the fingerprint was built from decoded values.
- **Entry 3 offered a cost fingerprint for the technology table.** Half of it was wrong,
  from a source with a column read one row down. Matching against it would have located a
  table shifted by one and confirmed a mistake.

Both failures have the same shape: **a fingerprint carries the errors of whatever source
it came from, and confirms them.** Where a reader function can be found instead, find it.
That is how every field in the naval table was labelled.

**Read the resource archive before disassembling anything.** `STR#ENU.GOB`'s help strings
state the production recipes, the power conversion and the worker conversion costs
outright. The labour rate this project hunted through 59 MB of listing for — and gave up
on — is printed there in English, and it corrected a shipped rule when it was read.

## How to send results back

Plain text or a table is fine. For anything that is an *array*, **the order is usually the
valuable part** — more so than the values, in two cases below — so please give entries in
the order they appear in memory, and say whether the array looked 0-based or 1-based if
that is visible from the indexing code.

If a value is stored scaled (×10, fixed-point, percent) please say so rather than
normalising, because we cannot tell afterwards.

---

## 1. ~~The ship type array~~ — RECOVERED

**Answered in full**, by
[`definitive-original-data.md`](definitive-original-data.md): the naval table at
`0x00698108`, fourteen 36-byte rows, plus six 30-entry commodity arrays at `0x00695B50`
for the build bills. Order, cargo, sea zones, combat stats and every build cost are in
the importer now.

Kept here for what the failure taught, because the same trap is waiting in the next hunt.

**The searches failed because they were looking for decoded values.** This entry said
"there is no static table — four pattern searches failed" and concluded the values were
assigned individually in a constructor. There *was* a static table, sitting in 36-byte
rows of nine dword slots, and almost every field it holds is encoded:

| What the manual prints | What the table stores |
|---|---|
| Firepower 20 | `2000` — stored ×100 |
| Armour 70 | `30` — stored as `100 - armour` |
| Hull 115 | `2800` — an internal damage-normalisation scale, not the printed number |
| Speed 5 | `5` in field **7**, which is a sea-zone allowance |
| Battle movement 7 | `7` in field **4** |

Of the eight numbers the fingerprint clustered on, only range and sea zones are stored as
printed. **A window of sixty could not have held six of them because six of them are not
in the file.** The lesson: search for what a table would *store*, and when the encoding is
unknown, find the reader rather than the data. Every field above was identified from its
accessor.

**Two specifics it settled.** The Frigate takes **2 arms**, not 3 — the discrepancy this
entry called out because arms later set the force landable at a beachhead. And the
**Freighter carries 16**, which was the last unknown cargo figure.

**The falsification test agreed without being fitted.** The corpus check had already
pinned that types 1–4 must be ungated, because 1816 skirmish powers with no technology
hold them. The recovered array's first four are Trader, Indiaman, Frigate and
Ship-of-the-Line. Ungated, exactly as required.

`ship` records convert now — 142 of them, against `EveryShipInTheCorpusIsAHullItsOwnerCouldHaveBuilt`.

---

## 2. The seven engine defaults

**The highest-value set on this list**, and the reason a decompiler was wanted at all. These
are what a *skirmish* starts a power with. They are constants in the binary and **cannot be
recovered from the corpus**, because the whole evidence is that the records are absent —
`s10` carries none of them and every power still starts equipped.

| Record | What it defaults | Current state |
|---|---|---|
| `ware` | starting warehouse stock | existence attested by the manual, **quantity is a guess** |
| `cash` | starting treasury | same — **quantity is a guess** |
| `tran` | transport capacity pool | **pure guess, and load-bearing**: at 0 an imported skirmish is unplayable |
| `deve` | starting cell development levels | assumed none |
| `rail` | depots built at the start | assumed none |
| `rela` | diplomatic relations | unrecovered, and nothing reads it yet |

`tech` — the seventh — is already recovered, from the manual: every power starts with High
Pressure Steam Engine and Seed Drill.

**How to recognise it.** Look for the code path that runs when a scenario has *no* record of
a given type. A skirmish load is the case to trace.

**How we will check it.** `tran` must be positive or nothing can leave the land. `ware` must
include lumber and steel, because the manual says a power builds its first mills from
"initial stockpiles of lumber and steel". Beyond that these are unfalsifiable from data we
hold — which is exactly why they are wanted.

---

## 3. ~~The technology table order~~ — RECOVERED, and the prices with it

**The order is the manual's printed order.** `STR#ENU.GOB` blocks #1073–#1075 carry all 28
names in progression order; the fan-wiki reordering we shipped at v19 is retracted.

**The two pinned counts now run only with a visible corpus gate.**
`EveryAuthoredLevelInTheCorpusIsOneItsOwnerCouldHaveBuilt` and
`EveryRailedCellInTheCorpusIsOneItsOwnerCouldHaveBuilt` are discovery-time
`CorpusFact`s: set `IMPERIALISM_SCENARIO_DIR` to a complete legal local corpus or
see an explicit skip. The full local corpus pass confirmed the expected 380/4 and
1,140/0 counts.

**The costs came with it**, from a 28-entry cash table at `0x0066AAE8`, and **twelve of the
twenty-six were wrong.** The fingerprint above was built from the fan-wiki list, and that
list's price column is off by one from Streamlined Hulls onwards: every entry carried the
*next* one's price. Had the search fingerprinted on it, it would have matched a table
shifted one row and been believed.

**A fingerprint is only as good as the source it comes from.** The costs were the one
column here described as something "we have plausible values for already". They were the
column that was wrong.

**Arrival years are derived, not stored.** The executable generates each non-starting
technology an inclusive pseudo-random turn-offset window (26 two-word entries at
`0x0066ABA4`). Reading offset *n* as year `1815 + n` puts 25 of the wiki's 26 observed
years inside their window and 19 exactly on the minimum, which is what pins the reading.

### ~~The prerequisite graph~~ — RECOVERED

The technology-store reader at `0x005B0A90` reads two raw little-endian signed
16-bit IDs per 1-based technology row from the 29-row table at `0x0066AC10`; row
0 is the all-zero sentinel, and a zero field means no prerequisite. This is direct
executable data and control flow, not a fingerprint. The full table and the material
corrections to the former fan-wiki graph are in `definitive-original-data.md`.

### Port connectivity: recovered predicate and task-force state

The executable registers distinct `TControlSeaZoneMission` and
`TBlockadePortMission` classes. The control mission's vtable begins at
`0x0065A740`; its unique paths include `0x005387F0` and `0x00538FE0`, which
initialise and compare a computed sea-zone strength/result. The blockade mission's
vtable begins at `0x0065AC60`; its target-validation path at `0x0053ADF0` indexes
the country table, so its name alone does not prove that it disconnects every port
belonging to that country.

The decisive consumer is `UMap`'s `0x00513CA0`, called by the town/connection
path at `0x005B7830`. It checks each of the six neighbouring cells. For adjacent
ocean it calls `0x00561510`, which builds a country bit mask from qualifying
task-force ships at that target and returns blocked only when an opposing eligible
country is present **and the port owner's bit is absent** (with the diplomacy test
at the same call site). This is the executable form of undisputed enemy control:
it is not a simple hostile-ship-presence rule. More precisely, the routine scans
the global `TShip` list at `0x006A3EDC`; a ship qualifies only when its `UOcean`
at `+0x08` is the target, its `TTaskForce*` at `+0x0C` is non-null and active
(`TTaskForce+0x26 == 0`), and that task force's state at `+0x08` is 3 or 4. It
then sets the bit for `TShip+0x14` (owner), so a qualifying friendly force
prevents a hostile force from blocking the port.

For river access, the same predicate calls `0x00563B70`. That routine follows the
original river-code transition tables for up to 100 cells and flags a change in
the raw land `NationZoneA` value before it reaches ocean. The caller accepts the
river continuation only when that trace reports no change. This proves that the
connection test is downstream/path sensitive. The modern core now materialises
that source-to-mouth geometry, including the original horizontal map seam and
100-cell guard.

The scenario fleet record and its map bridge are now recovered. The `.scn`
loader at `0x00581E60` dispatches big-endian four-character tags through the
table at `0x00698B50`: `ship` reaches `0x00582720`, while `zone` reaches
`0x00582FA0`. The four-field `ship` reader consumes `(country, type, zone,
count)`: it uses field 0 to update the seven-power country table, resolves
field 2 through `0x0055F100`, and creates one `TShip` per field 3 through
`0x0054F8E0`. The `zone` reader consumes its zone id and resolves that same id
through `0x0055F100`. `TShip+0x08` is the resulting `UOcean` record and
`TShip+0x14` is the owning country id. `UOcean`'s map resolver at `0x00563300`
selects the 0x48-byte ocean record for raw map region `r` as
`records[r - 0x17]`. Thus a scenario `zone`/`ship` id `z` denotes raw map-ocean
region `z + 0x17`.

The loader is called from setup state 2 at `0x0057DA70`, after the pre-load
map/zone preparation in state 3 (`0x0057CAD0`), and before the per-power
post-load virtual calls at `+0x168` and `+0x184`. This clears the placement and
setup-order gates, but not the command, range, or strategic-resolution rules.

The first command-side trace distinguishes queued mission objects from the
fleet state used by the predicate. `UCountryAuto` at `0x004E8540` creates an object through the mission
factory at `0x005350D0` and appends it through a virtual `+0x30` call on the
country's queued-mission collection. Factory case 4 builds the object whose
vtable is `TBlockadePortMission` (`0x0065AC60`); the control-sea-zone vtable is
installed by cases 0 and 2. Its handler at `0x005387F0` reads a target at
object offset `+0x14` and iterates `TPortZone` entries through
`0x00561C80`/`0x00561D40`; those iterators are not the active fleet records.

`TTaskForce` has vtable `0x0065C468` and owns a list of ships. Attaching a ship
at `0x00553BC0` writes the task-force pointer to `TShip+0x0C`; detachment clears
that field. The command dispatcher at `0x0055A160` sets state 3 for action 12,
state 6 for action 14, and state 5 for action 16. The status renderer identifies
state 3 as `patrolling` and state 6 as `blockading`; notably, the port predicate
accepts states 3 and 4, not state 6. State 4 is produced by the manager refresh
at `0x00557560`, but its player-facing semantic remains unproven.

The same phase routine at `0x0057F280` performs its per-power updates, then calls
the `TNavyMgr` refresh at `0x005577B0` (which calls `0x00557560`), followed by
`0x005578A0`. The latter resolves interactions among task forces, includes a
randomized check, and then clears/rebuilds manager state. This proves a naval
resolution pass and its local order; it does not yet place a particular
production/extraction call relative to that pass or a modern equivalence for
original naval combat.

### Fleet movement: shortest path, slowest hull, one resolved leg

Strategic movement is now recovered. A task force stores its current zone at
`+0x18` when constructed and receives a requested destination in `+0x0C`. The
planner at `0x005533F0` runs the `TPortZone`/ocean graph search at `0x00560F80`,
roots it at the requested destination, resets `+0x0C` to the current zone, and
walks adjacent nodes with decreasing shortest-path distance. It performs at most
the minimum `sea zones` value across the selected ships (`0x00554A80`, reading
`0x00698124 + 0x24 * shipType`), then leaves the reachable leg in `+0x0C` and
sets state 1.

The state-1 resolver at `0x00556100` writes that leg to every member's
`TShip+0x08` and marks the task force complete. Therefore fleets move by a
shortest-path leg of up to the slowest hull's allowance per resolution, rather
than teleporting to a distant selected zone. `0x005610B0` memoizes the graph
distance matrix used by command validation and path planning. The movement
adjacency list is runtime data at node `+0x28`/`+0x30`, but its builder is now
recovered: UMapper allocates one base `UOcean` for each raw sea region beginning
at map region `0x17`, and UOcean setup (`0x00562340`) finishes with
`0x00563F50`. That pass scans all `108 × 60` map cells, resolves each to its
base-ocean or `TPortZone` node, examines all six neighbours through
`0x0055E360`, rejects duplicate/self links, and inserts reciprocal movement
edges. The neighbour helper uses row-dependent hex geometry; north/south never
wrap, while east/west wrapping depends on the map seam flag at map `+0x20`.
`0x005635E0` supplies `TPortZone` nodes from the original's eligible port
geometry and attaches them to the adjacent ocean graph. The exact lifecycle for
ports created or removed after setup remains a separate trace, but static sea
topology is map-derived rather than scenario-serialized.

The direct task-force path overwrites its requested destination with the resolved
leg. A queued AI mission may reissue a later order, but the present trace does not
prove player-facing automatic continuation, so the modern core must not invent it.
Landing (state 5), blockade (state 6), and combat remain separate command paths.

---

## 4. The trade clearing price

The oldest unknown in the project. We have the **direction** from the manual (demand up,
supply down, matched flat) and no magnitude at all.

**Wanted:** how a commodity's world price moves between turns. Specifically whether it is a
percentage step, a proportional function of the supply/demand ratio, or a table; whether
there is a dead band; and whether prices are bounded.

**How we will check it.** It slots straight into `ITradeMarket`, which exists to be replaced
— no other code changes. Sanity: the soak currently sells 1,534 units for about 1.2 million
over a century, so a curve that changes that by orders of magnitude is worth a second look.

Also wanted, same area: **whether the bidder ranking is what we think.** The manual says
relations plus subsidies; we currently use a placeholder (country id order) and have labelled
it as such.

---

## 5. Prices we hold on weak evidence

None of these blocks anything. All live in content, so correcting one is an edit.

| Value | Currently | Standing |
|---|---|---|
| Depot cash cost | 1,500 | owner's recollection |
| Port cash cost | 2,000 | owner's recollection; manual only says port > depot |
| Improvement, Level III | 3,000 | owner's recollection. **Levels I and II are corroborated at 100 and 1,000; III is not** |
| Rail across mountains | 300 | **the one ground the price list does not price**; we reuse swamp's figure |
| Civilian work turns | 3 | observed play, applied to every civilian type |
| ~~Migration cost per worker~~ | 1 each of canned food, clothing, furniture | **RECOVERED** — the help resources state the conversion outright, and the guess was right |

**A candidate for the depot and the port.** The `0x00695C50` block holds nine cost records,
each 2 paper plus an expert worker, with cash values 1,500 / 500 / 1,000 / 1,000 / 2,000 /
1,000 / 1,000 / 2,000 / 5,000. Nine is also the number of civilian types, and 2 paper plus
an expert worker is exactly the University's price for one. **This is a candidate, not an
answer** — the owning action label is unresolved, and reading it as the civilian purchase
list would explain the first figure matching our remembered depot cost only by coincidence.
Worth tracing the selector before anything is moved.

Also useful if it falls out: **whether oil and fuel are tradable.** They appear on neither
the trade roster nor the manual's untradable list, and we currently treat both as untradable.

---

## What is *not* wanted

Please do not spend time on these — they are settled well enough that a binary value would
not change anything:

- **The Resource Development Table** (yield per deposit per level). Transcribed from the
  manual and independently corroborated.
- **The technology gates on improvement levels**, and the four rail terrain gates. The rail
  gates in particular are the best-corroborated reading in the project: 1,140 corpus rail
  ends permitted, none not.
- **Gold at $200 and gems at $500 a unit.** The manual states both outright.
- **Commodity trade prices.** Transcribed from the game's own Bid and Offers screen, in three
  tiers, and the tiers agree with the recipe structure.
- **The scenario `year` epoch.** Settled: a `year` field is an offset from 1815, confirmed
  against two scenarios' own briefing text.
- **The production recipes and the labour rate.** Stated outright in `STR#ENU.GOB` and
  transcribed: nine recipes, all costing two labour a cycle.
- **The raw-resource yield curves.** The executable's table at `0x00696D98` agrees with the
  manual's Resource Development Table row for row. Its one apparent exception, fish at
  1/2/3/4 where the manual says fish is not improvable, is a source quantity rather than a
  development level — see `definitive-original-data.md`.
- **The legacy grid's six neighbour offsets.** `0x00696E70` gives NE, E, SE, SW, W, NW in
  that order, which is exactly the clockwise-from-NE reading Forge measured from the maps.
