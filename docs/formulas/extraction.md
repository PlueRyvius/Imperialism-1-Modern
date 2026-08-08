# Resource extraction

## Summary

Every turn, cells carrying a deposit hand their output to the country owning
their province, provided a connected route reaches them. How much they hand over
depends on the deposit and on how far the cell has been improved. This is the
only rule that puts anything into the warehouse from the map, so it sets the
ceiling on everything industry can do.

Output does not arrive immediately: it is queued during the Extraction phase and
committed during Delivery, so it is available to the *following* turn's
production.

## Confidence

`inferred`, and the parts are not equally supported. Read this table before
trusting any number below.

| Claim | Support |
|---|---|
| Gathered only within one tile of a connected collection point | **manual**, Terrain Map section |
| Route must reach the capital; overlapping catchments waste coverage | **manual** |
| Output lands in the warehouse for next turn | **manual** |
| Every per-level yield in the table below | **manual**, Resource Development Table |
| Ports fish 1 per adjacent coast or river tile | **manual** |
| Development levels run 1–3 | **corpus-verified** — `deve` records across all shipped scenarios |
| Ports stand on land touching water | **corpus-verified** — 124 of 124 records |
| Gathering happens at connected depots, ports and the capital | **manual**, transport-network section |
| Ports need no railroad; the capital is always both | **manual** |
| `rail` records are depots | **corpus-verified** — a strict subset of railed cells, and none within two tiles of another |

Everything load-bearing here is now transcribed from the manual rather than
guessed. An earlier version of this file shipped a doubling curve described as
"a deliberate design choice, not a measurement"; the manual says the progression
is linear, and the code was corrected. See `docs/reference/manual-mechanics.md`.

Two things keep this below `verified`. The manual is documentation, not observed
behaviour — the confidence ladder puts manual-derived rules at `inferred` — and
where the manual and the shipped release notes disagree, `game-systems.md` says
the release notes win. Nothing here has been checked against them.

What would raise this to `verified`: controlled traces of the original showing a
known cell at a known level producing a known amount, for one surface and one
subsurface deposit.

## Evidence

From `docs/game-systems.md`, itself condensed from the manual and release notes:

- "A tile's output is gathered only if it is **on or within one tile** of a
  connected depot or port. Overlapping catchments waste coverage."
- "A depot needs an unbroken rail path to the capital, **or** rail to a tile
  holding both a port and a depot (goods then travel by sea)."
- Turn step 6: "Commodities transported and delivered into the warehouse **for
  next turn**."

From the corpus, measured directly:

- `deve` is `[cell, level]` and **every level in every shipped scenario is 1, 2
  or 3**, which is what fixes the length of the yield curve. Counts: `s1` 320
  records over 317 cells, `s3` 59, `s13` and `s14` 4 each, and none at all in
  `s9`–`s12` or `s15`.
- **A cell can be developed more than once.** `s1` does it three times — levels
  `[2,1]`, `[1,1]` and `[2,1]`. This is shipped data, so it is legal by
  definition. See the repeated-`deve` section below.
- The 1997 `.map` records *which* deposit sits on a cell and nothing about its
  output, so no rate can be recovered from the files. The rates come from the
  manual.
- **Ports always name a land cell** — 124 of 124, none on ocean, none off-map.
- **Every port touches water**, but only once adjacency wraps east-west the way
  the 1997 grid does. `s3` puts a port on the last column of row 0 whose only
  sea lies across the seam. `Imperialism.Core`'s grid does not wrap, so that rule
  lives in the importer and Core checks only that a port is on land.
- **Fish markers sit on ocean cells only** — none on land in any map — and only
  in the generated worlds, at 15–19% of ocean. The historical maps carry none.
  Under the manual's rule those markers do not drive fishing, so what they are
  for is an open question.

Separate what is observed from what is concluded: the bullets above are
observed. Assigning horses the fish treatment is concluded rather than stated —
the development table omits horses, but the Terrain Tiles table gives Horse
Ranch no civilian worker.

## Pseudocode

```text
for each country by dense id:
    if the country has no capital: it gathers nothing
    gateways = the capital's rail component
             + the rail component of every owned tile holding a port AND a depot
    collection points = the capital (always a connected depot and port)
                      + every owned port      (needs no rail; goods go by water)
                      + every owned depot whose rail component is a gateway
    catchment = collection points, widened by CatchmentRadius hex steps
    for each cell holding a deposit whose province this country owns:
        for each deposit on the cell:
            if deposit is gated and the country lacks the technology: skip
            amount = deposit.yieldByDevelopmentLevel[cell development level]
            if the cell is in the catchment: collected += amount
            else:                            stranded  += amount
    queue collected as pending deliveries; report stranded

Delivery commits the queue, so the goods are in Available stock when the next
turn's Production phase reads it.
```

The curves currently shipped, straight from the manual's table:

| | level 0 | 1 | 2 | 3 |
|---|---|---|---|---|
| Grain, fruit, cotton, livestock, wool, timber | 1 | 2 | 3 | 4 |
| Coal, iron, oil | 0 | 2 | 4 | 6 |
| Gold, gems | 0 | 1 | 2 | 3 |
| Fish, horses | 1 | — | — | — |

Zero at level 0 is meaningful rather than a missing value: it is exactly what
makes a mine worthless until a worker has dug it. A curve that is zero at
*every* level is rejected, since nothing would ever come of it. Above the top of
the curve the yield holds rather than throwing, which is what lets fish and
horses have a single-entry curve.

**Development runs earlier in the same turn.** A tile a civilian finished this
turn is gathered here at its new rate, and reaches the warehouse through
`Delivery` for next turn's production, like every other harvest. See
`development.md`.

Fishing runs alongside this, not through it. A port collects
`yieldPerAdjacentWaterTile` for each neighbouring tile that is open sea **or**
carries a river. The **capital fishes too**, being a connected port by
definition, whether or not a `port` record names it. No port in the corpus
depends on the river running through its own cell: all 124 have at least one
neighbouring water tile.

A port needs no rail, so the only thing that strands a catch is having no
capital for it to reach.

A cell is stamped once per country, so a cell inside two collection points'
catchments pays once — the manual's "overlapping catchments waste coverage".

**Track alone gathers nothing.** Rail moves goods past a tile; a depot is what
lifts them off it. Treating every railed cell as a collection point was a
placeholder, and a costly one: `s1` has 310 railed cells and 76 depots, and
replacing the placeholder with the real model cut that scenario from 319
collection points to 134.

**A depot has two ways to be connected and the manual gives both.** The obvious
one is rail to the capital. The other is rail "to a tile with a port that also
contains a depot", from which "the commodities must pass through the second depot
to reach the port and then travel to the capital by water" — so any rail
component holding a port-and-depot hex is a gateway, whether or not that
component also reaches the capital.

Both structures are needed at the gateway and they do different jobs: **the port
is the sea end and the depot is the rail end**, the thing that can accept goods
arriving down a line. A port without one is connected for itself and a dead end
for everything behind it, which is the trap the manual spells out — "the port
itself is connected, but the future depots constructed along your new railroad
have no way to move their commodities to the port."

**The second route was missing and its absence was expensive.** Six of the ten
shipped scenarios author at least one port-and-depot hex, and implementing the
rule reconnected real ground in every one of them:

| | port+depot hexes | collecting cells before | after |
|---|---|---|---|
| `s1` | 12 | 463 | 471 |
| `s3` | 3 | 235 | 239 |
| `s9` | 4 | 126 | **156** |
| `s12` | 4 | 124 | **154** |
| `s13`, `s14` | 1 | 105 | 109 |

`s5`, `s10`, `s11` and `s15` author none and are unchanged, which is the control
the measurement needs. `s9` and `s12` gained a fifth more collecting ground.

**What is still missing** are the ways a connection is *lost*: a province taken
along the line, the province downstream of a river port falling, and an
undisputed enemy fleet. All three want conflict, so every owned port is connected
here.

**Where the missing east-west wrap costs output.** `Imperialism.Core`'s grid does
not wrap; the 1997 one does. A port or deposit on the first or last column has
fewer neighbours here than it did in the original, so an edge port catches less
fish. It affects a handful of cells and is a known consequence of the modern
grid rather than a bug in this rule.

## Technology

The gate is real and enforced: a deposit naming a technology yields nothing
until the owning country knows it. What is *not* here is any way to learn one —
no research, no cost, no prerequisites. A scenario states what each country
begins knowing, and `WorldState.GrantTechnology` is the only other way in.

**No imported deposit declares a requirement, and that is now a positive
finding rather than a gap.** The manual gates the *improvement levels* behind
technology — mine level II needs Square Set Timbering, level III needs Dynamite,
a derrick needs Chemistry then Internal Combustion — and gates *prospecting* for
oil behind Oil Drilling. It does not gate extraction from a deposit that is
already open. Since nothing here builds a level yet, there is nothing for those
gates to bite on. The full table is in
`docs/reference/manual-mechanics.md`.

## The repeated `deve` rule

`s1` develops three cells twice. The importer keeps the **highest** level and
emits a warning naming the cell and what it kept.

The reasoning: development is a level a cell *has*, not a stack of separate
works, so the largest record is the only one consistent with all of them.
Last-record-wins is the alternative reading, and exactly two cells in one file
tell the two apart — `[1,1]` gives 1 either way. This is recorded as a choice
rather than a finding.

Erroring on the duplicate was the first implementation, and the corpus rejected
it within one run. A rule that fires on shipped data is a wrong rule.

## Where implemented

- `ResourceDefinition.YieldByDevelopmentLevel` / `GetYield`, `RequiredTechnology`,
  and `ExtractionSettings` in `Imperialism.Core`.
- `WorldState.GetCellDevelopment` / `SetCellDevelopment`, `HasTechnology` /
  `GrantTechnology`.
- `ExtractionPlanner` and the `TurnPhase.Extraction` branch of `TurnResolver`.
- `WorldState.HasPort` / `BuildPort` / `RemovePort` / `GetPorts`, and
  `ExtractionSettings.PortFishing`.
- `ResourceExtractedEvent` carries collected and stranded totals, deposit cell
  counts, and fishing/stranded port counts.
- `.iworld` v6 `resources[].yieldByDevelopmentLevel`,
  `resources[].requiredTechnology`, `technologies`,
  `extraction.portFishing`, `scenarios[].cellDevelopment`,
  `scenarios[].countryTechnologies` and `scenarios[].ports`, with v4→v5→v6
  migrations.
- `LegacyWorldConverter` converts `deve` and `port` records and assigns each
  deposit its curve from the manual's table via `ResourceYieldCurves`.

## Test data

`tests/Imperialism.Core.Tests/ExtractionTests.cs` pins the catchment radius
(including 0), connectivity to the capital, an orphaned rail component, a
country with no capital, province ownership, single payment on overlapping
catchments, several deposits on one cell, the yield curve, a mine yielding
nothing undeveloped, the technology gate, scenario-seeded development and
technology, a coastal port fishing, a river port fishing, a port off the network
stranding its catch, port placement, and that this turn's harvest only reaches
next turn's production.

`tests/Imperialism.LegacyImport.Tests/LegacyWorldConverterTests.cs` pins `deve`
and `port` conversion, the repeated-cell rules, rejection of off-map, ocean and
out-of-range levels, and each deposit's curve.

It also converts **the whole shipped corpus** when `IMPERIALISM_SCENARIO_DIR` is
set, asserting zero errors, no landlocked port, and the port counts per
scenario. That test exists because the corpus has now caught two wrong rules
that synthetic fixtures agreed with: the repeated `deve` cell, and the
seam-crossing port in `s3`. Run it before believing any new invariant.

All ten shipped scenarios import with **zero errors**: 49 ports and 317
developed cells in `s1`, 21 ports and 59 developed in `s3`, 10 ports each in
`s13` and `s14`.

There is still **no test pinning a rate against observed original behaviour**,
only against the manual. That is the gap between `inferred` and `verified`.

## Open questions

- Whether the release notes correct any of the manual's numbers.
- What the ocean fish marker is for. The generated maps put it on 15–19% of
  ocean and the historical maps use none, yet fishing does not read it.
- Whether a port on a river tile fishes its own river. No corpus port needs it,
  so only neighbours are counted.
- ~~**Improvability is terrain-dependent, not resource-dependent.**~~ **Settled.**
  Terrain now carries an `IsImprovable` attribute and the deposit names the
  civilian that works it; both must agree. The curve is still keyed off the
  resource, which is correct — the manual's two tables answer two different
  questions. See `development.md`, which also records the corpus check: 481
  `deve` records and not one on dry plains, horse ranch or scrub forest.
- ~~How a worker actually builds a level~~ — the `Development` phase does, and
  it costs nothing but the civilian's time. What is still missing is where the
  civilian comes from: building one in the University needs a money model.
- Depots as distinct from rail cells, and ports as buildable, losable objects —
  including the river-port and sea-port edge cases in `game-systems.md`.
- The transport capacity pool. Until it exists, a connected country moves
  everything it gathers.
- Gold and gems bypass the warehouse and convert straight to cash. The manual
  confirms it; the commodity model does not distinguish them yet.
