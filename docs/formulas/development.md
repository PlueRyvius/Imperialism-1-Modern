# Civilian units and land improvement

## Summary

Civilians are how a country makes its land worth more. A Farmer sent to a farm
tile raises that tile's development level, and `Extraction` reads that level
through `WorldState.GetCellDevelopment`, so the same tile pays more every turn
afterwards. Before this existed, a cell's level was whatever the scenario
authored and nothing in the engine could change it.

That matters out of proportion to its size, because it is the first link in a
chain that was jammed. The 100-turn soak found every power in a permanent grain
deficit with one worker sick for ever. Migration was then built and turned out
completely inert — 700 requests over 100 turns, nobody recruited — because
canned food needs grain and hungry workers eat every grain there is. The
sequence is **farms → food → population**, and everything downstream was blocked
on the first link.

## Confidence

`inferred`, with **one `guess`** clearly separated below.

| Claim | Support |
|---|---|
| `civi` is `[type, cell]` | **corpus-verified** — 210 records across ten scenarios, every one two fields, every cell in range and on land |
| Types 0–5 are Miner, Prospector, Farmer, Forester, Engineer, Rancher | **corpus-verified** — see the identification below |
| A civilian's owner is the owner of the province it stands in | **corpus-verified** — 210 of 210 stand on owned land, and every owner holds a capital |
| Civilians move any distance each turn | **manual**, stated outright — there is no movement-point model to build |
| They cannot enter another Great Power's land, nor a Minor Nation's without an embassy | **manual** |
| A civilian in a province that is lost is killed | **manual** |
| Which civilian improves which resource | **manual** — the Resource Development Table |
| Dry plains, horse ranch and scrub forest cannot be improved | **manual**, stated outright, and **corroborated by the corpus** — see below |
| Moving and working are alternatives, not a sequence | **manual** — the cursor table gives "deploy to tile, no work this turn" its own cursor |
| **How many turns a civilian's work takes** | **nothing, anywhere. The one guess.** |

## The corpus check that came before the code

The manual says improvability is a property of terrain rather than of the
deposit, and that farms and orchards next to the capital start at Level I. Both
were checked against all ten shipped scenarios before anything was built on
them, because this project has twice been caught building on a rule the corpus
then overturned.

**Terrain-based improvability is corroborated, without exception.** 481 `deve`
records land across five scenarios, and **not one** falls on dry plains, horse
ranch or scrub forest:

| Terrain | `deve` records |
|---|---|
| Farm | 159 |
| Hardwood forest | 107 |
| Orchard | 77 |
| Barren hills | 52 |
| Fertile hills | 44 |
| Open range | 33 |
| Mountains | 6 |
| Swamp | 3 |
| **Dry plains, horse ranch, scrub forest** | **0** |

Every terrain in that list is one the manual's Terrain Tiles Table gives a
civilian worker, and every terrain it gives "None" is absent. Scrub forest is a
weaker case than the other two: **the code never appears in any shipped map at
all**, so its unimprovability rests on the manual alone.

**The discriminating cells exist, and they agree.** Deposits otherwise sit only
on their matching terrain — all 3,920 grain markers on farms, all 2,442 timber
markers in hardwood forest, and so on — which makes terrain-keyed and
resource-keyed improvability agree nearly everywhere. The exception is **four
dry-plains cells carrying fruit**, which a resource-keyed rule would let a
Farmer improve and a terrain-keyed rule would not. No `deve` record touches any
of the four.

**Capital-adjacent farms are *not* developed in the shipped data.** Across the
ten scenarios there are 350 farm or orchard tiles adjacent to a capital, and
only 27 of them carry a `deve` record — all in `s1` and `s5`, which develop
several hundred cells apiece anyway. `s3` ships 59 `deve` records and not one of
them is a capital-adjacent farm; `s9`–`s12` and `s15` ship none at all.

That does not contradict the manual. It places the rule where the manual puts
it — "**at the beginning of the game**, the farms and orchards adjacent to your
capital city are automatically improved to Level I" — which is the engine acting
on a scenario, not a scenario authoring itself. It joins the seven values in
`_index.md` that a skirmish leaves entirely to the engine. **It is not
implemented here**; see Open questions.

## Identifying the six civilian types

`civi[0]` is a small integer and nothing names it. It is settled by where the
units stand, across all ten scenarios:

| Code | Where its 210 instances stand | Reading |
|---|---|---|
| 4 | towns 16, and everywhere else | **Engineer** — the manual says only the Engineer may work in a town |
| 5 | fertile hills 6, open range 3 | **Rancher** — exactly the Rancher's two terrains |
| 2 | farms 14, plantations 5, orchards 5 | **Farmer** — exactly the Farmer's three |
| 3 | hardwood forest 11 | **Forester** |
| 0 | barren hills 11, mountains 6 | **Miner** |
| 1 | spread over everything, 62 of them | **Prospector** |

The last pair is the one that could have been swapped, and the skirmishes settle
it: `s11` and `s15` give **each of the seven powers exactly one type 1 and one
type 4** — a Prospector and an Engineer, which is the fair start. A count
confirms the ordering independently: excluding the generated `s5`, the corpus
ships 62 of type 1 and 30 of type 0, matching the known 62 Prospectors and 30
Miners.

Fisherman, Developer and Oil Driller never appear.

## Design

**Improvement is legal only when all four hold.** They come from different
places and none subsumes another:

1. the cell's **terrain** is improvable — the manual's Terrain Tiles Table;
2. the cell carries a **deposit this civilian's type improves** — the Resource
   Development Table;
3. the cell is in a province the civilian's **country owns**;
4. the level is **below the top of that deposit's curve**.

A cell with two deposits has one level, so one deposit still short of the top of
its curve is reason enough to keep working.

**One order per civilian per turn**, and `CountryTurnOrders` rejects a second.
Deploying moves without working; a work order moves *and* works, because the
original's hammer cursor does both in one click.

**Where a civilian may go is narrowed to its own territory.** The manual bars
another Great Power's land outright and a Minor Nation's without an embassy.
Nothing here models diplomacy or can tell a minor nation from a great one, so
the rule is narrowed to what is unambiguously allowed. It can only refuse more
than the original did, never less.

**A `Development` phase**, immediately after `Construction`, which it resembles:
both take an order now and pay it off later. It runs **before `Extraction`**, so
a tile finished this turn is gathered at its new rate this turn — and that
harvest reaches the warehouse through `Delivery` for next turn's production,
like every other harvest.

**Refusals are reported, not thrown.** A civilian can die and a tile can change
hands between orders being written and the turn resolving, so an impossible
order is an ordinary outcome of simultaneous turns. `CivilianOrderRefusedEvent`
names the reason.

**Work already under way advances before new orders are read**, and the set of
busy civilians is taken before *that* — so a civilian whose job finishes during
this phase still cannot accept an order written while it was busy.

**A job whose tile is no longer legal finishes without raising anything.** If
the province was lost mid-job, the worker is freed and the level is untouched.

## The one guess: work takes one turn

Nothing in the manual, the corpus or the binary says how long a civilian's work
takes. One turn is what is shipped, and it lives in content as a per-civilian-
type `workTurns` so changing it is an edit rather than a code change.

Two things argue it may be longer. The manual speaks of "when a Miner finishes
opening a new mine", which reads like an event worth waiting for. And with
unlimited movement, a one-turn rule makes a handful of Farmers very fast — the
soak below improves every farm tile a power owns inside ten turns.

Ordering it takes effect the following turn: a job ordered on turn *N* completes
during turn *N+workTurns*'s Development phase. That is what makes the duration
mean something; at zero a civilian would improve a tile every turn for free, and
`CivilianTypeDefinition` refuses it.

Record this as a `guess`, not a finding. It is the only number here without
evidence behind it.

## Pseudocode

```text
Development phase, after Construction and before Extraction:

    busyAtStart = every civilian with a job

    for each civilian with a job, oldest first:
        if turns remaining > 1: decrement and continue
        clear the job
        if the work is no longer legal: report the refusal and continue
        raise the cell's development level by one; report it

    for each country by dense id:
        for each deploy order:
            refuse if: no such civilian, not yours, busy at start,
                       target off map, not land, not your territory
            otherwise move the civilian
        for each work order:
            refuse if: any of the above, or the terrain cannot be improved,
                       or no deposit there is this civilian's work,
                       or the tile is already at the top of its curve
            otherwise move the civilian and start a job of workTurns turns
```

Improvement costs nothing. The civilian was paid for when it was built, and the
manual prices no materials for the work itself — so unlike `Construction` there
is no shared pool to book against the other phases first.

## What the soak shows

`EconomySoakTests` runs a hundred turns over seven identical powers. Each holds
three grain tiles against four workers who want grain, which is the standing
one-worker deficit the soak has reported since it was written. Two runs differ
only in whether the three Farmers each power starts with are ever told to work.

**Idle Farmers — the control.** Grain stays at 21 a turn, seven workers stay
sick, the workforce never moves off 49, and the Capitol turns down all 700
recruitment requests. Exactly as before this phase existed.

**Farmers working:**

| turn | workers | fed / sick | grain a turn | total levels |
|---|---|---|---|---|
| 1 | 49 | 42 / 7 | 21 | 98 |
| 2 | 49 | 49 / 0 | 35 | 119 |
| 5 | 56 | 56 / 0 | 42 | 140 |
| 10 | 77 | 77 / 0 | 63 | 196 |
| 25 | 126 | 105 / 21 | 63 | 196 |
| 100 | 119 | 98 / 21 | 63 | 196 |

The chain completes, in order: first improvement on turn 2, sickness gone the
same turn, **first recruit on turn 4** — migration doing something for the first
time since it was built — and the workforce more than doubling.

**Then it reopens, and that is the finding rather than a failure.** Every tile a
Farmer can work reaches the top of its curve by turn 10, grain stops at 63, and
the population keeps growing until it outruns the harvest. Sickness returns on
turn 14 and the economy settles at 119 workers with 21 permanently ill. That is
the manual's own warning about growing faster than you can feed, arrived at
rather than written in.

Both runs are reported rather than asserted into a target, which is the standing
split in `soak.md`: the soak asserts integrity and *reports* behaviour.

## Where implemented

- `TerrainDefinition` (`Definitions.cs`) and `MapDefinition.Terrains` /
  `GetTerrain`. An empty terrain table is legal and means nothing is improvable —
  "unknown" and "unimprovable" reach the same answer deliberately, because a
  silent default of improvable would invent permission out of an omission.
- `CivilianUnits.cs`: `CivilianTypeDefinition`, `CivilianUnit`,
  `CivilianWorkInProgress`, `InitialCivilian`.
- `ResourceDefinition.ImprovedBy`.
- `WorldState.CreateCivilian` / `GetCivilian` / `GetCivilians` /
  `RemoveCivilian` / `MoveCivilian`, and `SetCivilianWork`.
- `DevelopmentPlanner` and the `TurnPhase.Development` branch of `TurnResolver`.
- `CivilianDeployOrder`, `CivilianWorkOrder` on `CountryTurnOrders`.
- `CellDevelopedEvent`, `CivilianWorkBegunEvent`, `CivilianDeployedEvent`,
  `CivilianOrderRefusedEvent` with `CivilianOrderRefusal`.
- `.iworld` **v13**: `terrains` replacing `terrainKeys`, `civilianTypes`,
  `resources[].improvedBy`, `scenarios[].civilians`, with a v12→v13 migration.
- `LegacyWorldConverter` converts `civi`, stamps each terrain code with the
  manual's improvability, and names each deposit's improver.

## Test data

`tests/Imperialism.Core.Tests/DevelopmentTests.cs` pins a Farmer raising a grain
tile and that turn's harvest reaping the new rate; dry plains carrying grain
refusing a Farmer; a world with no terrain table improving nothing; the wrong
civilian type; both halves of the foreign-territory rule; a tile at the top of
its curve; a busy civilian refusing a new order; the declared work duration;
deploying without working; one order per civilian per turn; orders for a missing
or foreign civilian; a tile lost mid-job; and scenario civilians taking ids in
order.

`tests/Imperialism.LegacyImport.Tests/LegacyWorldConverterTests.cs` pins all
seventeen terrain codes against the manual's improvability one by one, an
unknown code never being improvable, every deposit's improver, `civi` conversion
with the owner derived from the province, and the four ways a `civi` record is
rejected. The whole-corpus test asserts **210 civilians across ten scenarios**
with no diagnostics, and that `civi` is no longer reported as deferred.

`tests/Imperialism.Core.Tests/EconomySoakTests.cs` carries the payoff run and
its control.

## Open questions

- **How long the work takes.** The only guess here.
- **Building civilians in the University.** Costs an expert worker, paper *and
  cash*, and Core has no money model at all. Blocked on a separate system, which
  is why every civilian in play comes from a scenario.
- ~~**Prospector discovery.**~~ Built. Coal, iron, gold, gems and oil are hidden
  until a Prospector of that Great Power searches the tile, and a Miner sent to
  unsearched ground is refused. The 449 of 2,860 barren hills and 346 of 1,589
  mountains that carry a marker are now the odds a search faces rather than a
  curiosity. See [prospecting.md](prospecting.md).
- **Engineer construction** — rail, ports, depots, forts. Its own slice.
- **Developer and buying land** — needs money and diplomacy.
- **Technology gates on levels II and III.** Square Set Timbering, Dynamite,
  Feed Grasses, Iron Railroad Bridges, Chemistry, Internal Combustion.
  `ResourceDefinition.RequiredTechnology` gates *extraction*; the manual gates
  the *improvement level*, which is a different hook and does not exist yet.
- **Capital-adjacent farms and orchards starting at Level I.** Stated by the
  manual, absent from the shipped `deve` records, and not implemented. It is an
  engine start rule, and adding it would move the imported corpus's economy.
- **Dry plains and scrub forest produce and are not marked.** The manual says
  dry plains yield grain and scrub forest "a minimal amount of timber", yet
  4,080 of the corpus's 4,084 dry-plains cells carry no deposit marker at all.
  Under our model they yield nothing. Whether the original derives their output
  from terrain is unresolved, and 4,084 free grain tiles is far too large a
  change to make on an inference.
- **A civilian in a lost province is killed.** `RemoveCivilian` exists and
  nothing calls it, because `Conflict` is not modelled.
