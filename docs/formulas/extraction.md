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
| Gathered only within one tile of a connected collection point | manual, via `game-systems.md` |
| Route must reach the capital; overlapping catchments waste coverage | manual |
| Output lands in the warehouse for next turn | manual |
| Development levels run 1–3 | **corpus-verified** — `deve` records across all shipped scenarios |
| Surface deposits yield 1 undeveloped | observed play |
| Subsurface deposits yield nothing undeveloped, 2 once dug | observed play |
| Higher levels and some deposits are gated behind technology | observed play |
| **Yield doubles at each level** | **a deliberate design choice, not a measurement** |

The doubling curve is the one number nobody has checked. It was chosen because a
progression was needed and doubling is simple and predictable; the original's
actual curve may well be gentler. It is content data precisely so that
recalibrating it is an edit rather than a refactor.

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
  output, so no rate can be recovered from the files. The rates here come from
  play, not from the corpus.

Separate what is observed from what is concluded: the bullets above are
observed. The split of the thirteen deposits into surface and subsurface is
concluded from them — coal, iron, oil, gems and gold are dug; cotton, wool,
timber, horses, grain, fruit, fish and livestock are harvested.

## Pseudocode

```text
for each country by dense id:
    collection points = the capital, plus every cell sharing the capital's
                        rail component
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

The curves currently shipped:

| | level 0 | 1 | 2 | 3 |
|---|---|---|---|---|
| Surface | 1 | 2 | 4 | 8 |
| Subsurface | 0 | 2 | 4 | 8 |

Zero at level 0 is meaningful rather than a missing value: it is exactly what
makes a mine worthless until a worker has dug it. A curve that is zero at
*every* level is rejected, since nothing would ever come of it. Above the top of
the curve the yield holds rather than throwing, so a scenario carrying a level
the deposit has no entry for still behaves sensibly.

A cell is stamped once per country, so a cell inside two collection points'
catchments pays once — the manual's "overlapping catchments waste coverage".

**Where this is deliberately conservative.** Depots and ports are not modelled,
so every cell of the capital's rail component stands in for a depot. Against the
original this can only *under*-collect where a depot would extend reach that
rail alone does not.

## Technology

The gate is real and enforced: a deposit naming a technology yields nothing
until the owning country knows it. What is *not* here is any way to learn one —
no research, no cost, no prerequisites. A scenario states what each country
begins knowing, and `WorldState.GrantTechnology` is the only other way in.

**No imported deposit declares a requirement.** Which technologies gate which
deposits has not been measured, and guessing it would quietly make part of every
converted map worthless. The mechanism is exercised by synthetic content in the
tests instead.

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
- `ResourceExtractedEvent` carries collected and stranded totals plus cell counts.
- `.iworld` v5 `resources[].yieldByDevelopmentLevel`,
  `resources[].requiredTechnology`, `technologies`,
  `scenarios[].cellDevelopment` and `scenarios[].countryTechnologies`, with a v4
  to v5 migration.
- `LegacyWorldConverter` converts `deve` records and assigns curves from
  `WorldContentCodec.SurfaceYieldByDevelopmentLevel` /
  `SubsurfaceYieldByDevelopmentLevel`.

## Test data

`tests/Imperialism.Core.Tests/ExtractionTests.cs` pins the catchment radius
(including 0), connectivity to the capital, an orphaned rail component, a
country with no capital, province ownership, single payment on overlapping
catchments, several deposits on one cell, the doubling curve, a mine yielding
nothing undeveloped, the technology gate, scenario-seeded development and
technology, and that this turn's harvest only reaches next turn's production.

`tests/Imperialism.LegacyImport.Tests/LegacyWorldConverterTests.cs` pins `deve`
conversion, the repeated-cell rule, rejection of off-map, ocean and
out-of-range levels, and the surface/subsurface curve split.

All ten shipped scenarios import with **zero errors**: 317 developed cells in
`s1`, 59 in `s3`, 4 each in `s13` and `s14`.

There is still **no test pinning a rate against original behaviour**, because
there is no observed input/output pair to pin it to. That is the gap between
`inferred` and `verified`.

## Open questions

- The real progression per level. Doubling is a placeholder.
- Whether base rates differ within the surface group, or within the subsurface
  group. They are uniform here.
- Which technologies gate which deposits, and which gate *levels* rather than
  initial extraction.
- Whether fish behaves like the other surface deposits. It is grouped with them
  on the assumption that it yields untouched, which is the least certain of the
  thirteen.
- How a worker actually builds a level, and what it costs. Nothing here creates
  development; only scenarios and direct calls set it.
- Depots and ports as buildable, losable objects, including the river-port and
  sea-port edge cases in `game-systems.md`.
- The transport capacity pool. Until it exists, a connected country moves
  everything it gathers.
- Whether gold and gems bypass the warehouse and convert straight to cash.
