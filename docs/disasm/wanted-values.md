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
| `rela` | diplomatic relations | raw records recovered; gameplay meaning remains deferred |

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
country is present **and the port owner's bit is absent**. It calls the country
manager at `0x006A43D0` through vtable slot `+0x48` for each present foreign
country and the port owner; a nonzero answer blocks the port. The raw scenario
`rela` loader (`0x004EFCB0`) stores its 23 by 23 symmetric, signed-16-bit table
at country manager `+0x79C`; values above 255 are clamped and values below 50
take an additional eligibility path. The port virtual call resolves exactly to
`0x004EF590`; it first invokes another pair predicate at `+0x44`, then compares
a **separate** 23 by 23
signed-16-bit effective-token table at `+0xFE0` with the value from global
object vtable slot `+0x3C` (`0x0057D8B0` returns its `+0x2C` field).

The prerequisite pair predicate is now recovered: vtable `+0x44` resolves to
`0x004EF540`, which requires both countries to be active and their relation-mode
matrix entry at `+0xBBE` to equal `6`. The mode matrix is initialized to `4` and
the effective-token matrix to `-1`. Its symmetric setter, `0x004F1B70`, writes
the chosen mode to both directions and stamps both effective tokens with the
current global sequence. Its case-`6` branch applies the original hostile
300-point effects. Consequently the sea-port rule is exact: block only when a
foreign qualifying state-3/4 fleet is present, no qualifying fleet of the port
owner is present, the pair is in mode `6`, and that pair's effective token is
different from the current sequence. A newly set mode-`6` relationship is
therefore not effective until the sequence subsequently advances.

Shipped `rela` records are the 253 unordered pairs among 23 countries and carry
raw values such as 80 through 150 and 255. A controlled live run of the
recovered original executable (MD5 `7dc20de2f44b9425be072f360e614157`) observed
the global comparison field at zero before scenario load, then 269 on reaching
the map. It advanced independently through 270 to 277 as map actions and
blockade-resolution turns occurred, while the raw `+0x79C` entries changed on
their own timeline (for example, 100 to 49 and 129 to 134). That rules out
treating the global field as a `rela` threshold. The same live session found
`+0xFE0` samples of 271 and -1 while the comparison field was 281, confirming
that it is a separate, initialized table; its non-constructor writer remains
to be recovered. This is the executable form of
undisputed enemy control: it is not a simple hostile-ship-presence rule. More
precisely, the routine scans
the global `TShip` list at `0x006A3EDC`; a ship qualifies only when its `UOcean`
at `+0x08` is the target, its `TTaskForce*` at `+0x0C` is non-null and active
(`TTaskForce+0x26 == 0`), and that task force's state at `+0x08` is 3 or 4. It
then sets the bit for `TShip+0x14` (owner), so a qualifying friendly force
prevents a hostile force from blocking the port.

Modern world-content version 23 represents this active state separately from
the imported raw `rela` list: an optional `relationSequence` plus ordered
`relationStates` entries preserve the signed 16-bit mode and token values.
Older packages migrate to sequence 0 with no entries, which reconstructs the
EXE defaults (mode 4, token -1) without assigning a meaning to historical raw
relation scores.

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
`records[r - 0x17]`. A scenario `zone`/`ship` id is **not** `r - 0x17`, though:
it is the dense id the load-time scan at `0x00529F60` gives each ocean-region
value in order of first appearance by cell index (see
`definitive-original-data.md`), then merged and compacted by `0x0052D750`
(`docs/legacy-importer.md`). Still wanted: where `[0x006A43D4]+20`, the wrap flag
the merge's neighbour test reads, is set (assumed on).

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
at `0x00557560`, but it is not a player-facing mission. The refresh first
rebuilds a per-country task force and prunes unsuitable members, then evaluates
the target's virtual `+0x38` predicate. The base `UOcean` implementation at
`0x0055E840` returns false, so an ordinary sea-zone target becomes state 4;
the `TPortZone` override at `0x00561680` returns true, so a port-zone target
becomes state 7. State 4 is therefore an automatic target-type resolution
outcome, not a recovered player command, and must not become an invented Modern
command.

Actions 14 and 16 are now separated more sharply than their UI names alone
suggest. Action 14 resolves the selected target through `0x005633B0`, which
returns either a `TPortZone` for eligible port geometry or an ocean node for a
sea-region cell, then assigns state 6. Action 16 instead resolves the selected
land target through `0x00563360` and assigns state 5. During navy resolution,
`0x005578A0` sends every state-5 task force through `0x00556100`, which marks
one owner bit at the selected target's `+0xA1`, and then immediately invokes
`0x00558960` twice. That is a separate, large land-conflict resolver; no direct
ownership write occurs in the naval state-5 handler. State 6 is likewise not a
simple port switch: `0x005578A0` feeds blockaders into the randomized naval
interaction helpers `0x00555720`, `0x00555920`, and `0x00555D10`. Therefore
landing outcomes and blockade effects remain deferred with naval combat, while
the state assignments themselves are recovered.

The same phase routine at `0x0057F280` performs its per-power updates, then calls
the `TNavyMgr` refresh at `0x005577B0` (which calls `0x00557560`), followed by
`0x005578A0`. The latter resolves interactions among task forces, includes a
randomized check, and then clears/rebuilds manager state. This proves a naval
resolution pass and its local order; it does not yet place a particular
production/extraction call relative to that pass or a modern equivalence for
original naval combat.

The global phase-name table at `0x005421E0` independently identifies the
original high-level turn phases: `kPhEverybodyDoDiplomacy`,
`kPhEverybodyDoTrade`, `kPhEverybodyDoCity`, `kPhEverybodyDoCivilians`,
`kPhEverybodyDoMilitary`, `kPhMoneyLenders`, `kPhDealBook`, and
`kPhStratBatReport`, followed by start-turn/technology/map states. Its enum
ordering confirms that city, civilian, and military work are separate named
phases; `kOptPhTransport` is instead an optional UI phase. The table is a
name formatter, however, not the state-transition loop, so it does not prove
the interleaving of the individual city collection operation with naval
resolution. The modern pipeline therefore remains unchanged.

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

The traversal order is now also pinned: `0x0055E360` maps neighbour indices
`0..5` to north-east, east, south-east, south-west, west, north-west. The
builder inserts a reciprocal edge the first time that its ascending cell scan
encounters it; it does not sort node ids. This encounter order is material to
shortest-path ties and is preserved by the modern imported topology. The
original's 108 by 60 limits are confined to this legacy helper, not imported
modern map dimensions.

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
relations plus subsidies; since v31 we rank by subsidy alone, ties in id order, and label it a
chosen rule pending relations; a Great Power seller ranks by nothing, per the release-notes
rule (`game-systems.md:204-206`), which README.TXT / README11.TXT should confirm. Also wanted: **the subsidy buttons' percentages** — we ship the
tutorial's 5 and 10, and nothing in the extraction names a level or a third button.

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
| Civilian price at the University | 1 expert + 2 paper + $1,000, every type | **expert is the manual's; paper and cash are guesses** (`docs/formulas/university.md`). **Wanted: trace the selector of the `0x00695C50` block below** — if it is the University's table, its per-type cash replaces the flat $1,000 |
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

## 6. Land battles: how the strategic loop deals damage

Row 6b ships a strategic auto-resolver that follows the recovered loop. The snapshot, the
half-health test, attacker first, the 1-or-2 weight, mod 7, the artillery half branch, the
[70, 80, 90, 90] scale, +35/+20, and +100 healing to 500 are all the binary's. **How those
pieces combine into a loss is a placeholder**: `loss = max(1, shot × 5 / DEF)`, chosen rules
17–25 in `docs/formulas/army.md`. Every target below needs the disassembly on the PC.

| Target | Question | What it replaces |
|---|---|---|
| `0x004A8040` | How the mod-7 multiplier, the firepower entry, the 1-or-2 weight, the artillery branch and the round scale combine into damage, and **whether defence enters at all** | rules 18–21 and 23: the +1, the artillery rounding, 25% a medal, the round indexing, and the constant 5 |
| The target selection inside that loop | How a firing regiment picks its target: uniformly, the weakest, roster order? | rule 22 |
| The `+0x3a` bit 1 on a regiment | What the flag means. It sits beside the fields the loop reads | — |
| The writer of the `+0x0c` movement sentinel read by `0x004A1F80` | When "no movement order" is set and cleared, which decides who heals and when | rule 29, healing after the battles |
| The capture path after `0x004A5CA0` | After the winner is written to the control field: what happens to the owner byte, civilians in the province, the capital, and anarchy | rules 26–28; the civilians rule is the manual's |

**Wanted once traced:** replace `StrategicBattleResolver`'s body behind `IBattleResolver`.
`LandBattleTests` pins no casualty figure that only the placeholder produces, so it should
survive the replacement unchanged.

## 7. Naval war: what `0x005578A0` decides

Row 6c ships naval battles, the blockade mission, interception and escorts. The naval table
(firepower, range, armour, hull scale), the patrol-only port predicate and blockade as
task-force state 6 are the binary's. **Everything `0x005578A0` decides is a placeholder**:
chosen rules 13, 22 and 25 in `docs/formulas/naval.md`, and the structural rules 14–27 that
stand in for its control flow. `definitive-original-data.md` records only that it "resolves
task-force interactions with randomized checks".

| Target | Question | What it replaces |
|---|---|---|
| `0x005578A0`, the interaction checks | Which task-force pairs it tests, in what order, and whether a blockade (state 6) is ever the one to start a battle | rules 16, 18, 19 |
| Interception odds inside it | The patrol and blockade chances, and whether they depend on ship count, speed or the merchant's class | rule 22: 25% and 50% |
| The outcome split | How turned back, captured and sunk are chosen, and in what proportion | rule 25: uniform `rng mod 3` |
| What an interception does to the money | **Does an intercepted deal refund the buyer?** And does a captured cargo become the captor's warehouse stock or something else | rule 26: money stands |
| The naval loss combination | How firepower, range, armour and hull scale combine into a sunk ship; whether the 6b loop shape (half-health test, 1-or-2 weight, mod 7, [70, 80, 90, 90]) is shared with ships at all; whether armour reduces damage and how | rules 13–15 |
| Escort selection | What "docked at your capital" means in the data: a fleet's zone, a port flag, an unassigned state; and whether escorts fight once a turn or once per interception | rule 24 |
| State 6 duration | Whether a blockade lapses after some turns, or persists like a patrol | rule 12's persistence (not its port effect, which is verified) |
| Which merchant is lost | When a merchant is sunk, which hull goes | rule 27 |

**Wanted once traced:** replace `StrategicNavalBattleResolver` behind `INavalBattleResolver`
and the constants and draws in `NavalWarPlanner`. `NavalWarTests` drives outcomes by
searching turn seeds and asserts directions (a blockade beats a patrol) rather than exact
rates, so most of it should survive a change of numbers; the range test pins the opening
volley and will need the traced rule.

## 8. Landings: what the landing path decides

Row 6d ships the landing site (task-force state 5, which the port predicate does not count —
that much is the binary's), the arrival rule and the landing attack. **Three parts are
placeholders**: the capacity reading (army rule 33), drowning (army rule 34) and the
interception battle (naval rule 31). Army rule 32, embarking from any coast, waits on the
same trace.

| Target | Question | What it replaces |
|---|---|---|
| Action 16 / `0x00563360` | What the land target is: a province, a cell, the site's own province? And does it check the site's age, so troops cannot cross the turn it is established? | army rules 31 and 35; naval rule 29's restamp |
| `0x00556100`, the `+0xA1` owner bit | What the bit marks — the site's owner, a "landing ready" flag, or a per-turn used flag — and when it is set and cleared | army rule 31's readiness; naval rule 32's persistence |
| `0x00558960`, called twice | What the two calls are: the interception battle and the landing, two interceptions, or a check then an action | naval rule 31: one battle per site a turn, every hostile in the zone |
| Where the landing capacity is computed | Is it the sum of the armaments in the ships' build bills, the naval table's own armaments field, or something else? Per site, per task force or per country? Spent cumulatively across landings in a turn? | army rule 33 |
| Drowning | Which regiments drown when ships are lost — from the end, at random, by type — and whether a beaten site drowns everyone or sends them home | army rule 34 |
| Embarkation | Must troops embark on a coast touching the site's zone, or any coast with a sea path? Is there a distance limit? | army rule 32 |

**Wanted once traced:** replace `LandingPlanner`'s `Capacity`, `Land` and `Intercept`.
`LandingTests` finds its battle outcomes by searching seeds and asserts the rules on what it
finds (drowned from the end, capacity after the battle), so the capacity and drowning tests
will need the traced rules while the refusal and readiness tests should survive.

---

## 9. Forts and elimination: what row 6e guessed

Row 6e builds forts and eliminates countries. **Three parts are guesses or chosen rules**
waiting on the binary.

| Target | Question | What it replaces |
|---|---|---|
| (a) The Engineer construction dialog's price lookup | What a fort level costs, and whether the price rises per level. The nine-record block at `0x00695C50` is a **candidate only**; its selector is untraced | `fortCashCost` = $2,000, engineer rule F5 — **a guess with no evidence at all**, the port's figure reused |
| (b) The elimination path after the capture writer (`0x004A5CA0`, already in §6) | What happens to an eliminated power's fleets, treaties, warehouse, treasury and units. Do its ships sink, pass to the conqueror, or stay in limbo? | army rules 40–42 and 46 |
| (c) The healing pass | Whether regiments in an anarchic province heal | army rule 47's healing |

**Wanted once traced:** (a) is a content edit in `LegacyWorldConverter.CreateStandardConstruction`;
(b) replaces `EliminationPlanner.Eliminate`; (c) is one guard in `LandBattles` healing.

## 10. The newspaper: what row 7b chose

Row 7b composes a paper from each turn's events. **All four selection rules are chosen**
(`docs/formulas/newspaper.md` N1–N4); nothing of the original's is recovered beyond the
manual's prose. Done on the PC.

| Target | Question | What it replaces |
|---|---|---|
| (a) `UNewspaper.cpp`, `0x0055BA30`–`0x0055BB8F` | What selects the stories, in what order, and is there a cap on how many a paper prints? | N2's priorities, N3's filter, N4's cap of 12 |
| (b) The newspaper story strings in `STR#ENU.GOB` | How many story templates there are, by kind — **counts only**, no text copied | `NewspaperText`'s story kinds, which are ours |
| (c) The same path | Does the original report a rival's technology purchase? | The `TechnologyPurchasedEvent` item, chosen in N2 |

**Wanted once traced:** (a) replaces `NewspaperView`'s `Editor` and the cap; (b) tells us
which kinds of story the original has and we lack; (c) is one arm of the `Editor` switch.

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
