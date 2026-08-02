# Resource extraction

## Summary

Every turn, cells carrying a deposit hand their output to the country owning
their province, provided a connected route reaches them. This is the only rule
that puts anything into the warehouse from the map, so it sets the ceiling on
everything industry can do. Output does not arrive immediately: it is queued
during the Extraction phase and committed during Delivery, so it is available
to the *following* turn's production.

## Confidence

`guess`, and the label is doing real work — the shape and the numbers are not
equally well supported:

- **The gathering rule is `inferred`.** That a tile pays only when it is on or
  within one tile of a connected collection point, that overlapping catchments
  waste coverage, that the route must reach the capital, and that output lands
  in the warehouse for next turn all come from the manual and release-note
  summary in `game-systems.md`.
- **The rate is a plain `guess`.** Nothing in the corpus or the documentation
  read so far states how much a deposit produces. Every deposit currently
  yields 1 per turn because a number was required, not because 1 was measured.

Raising it needs two different things: controlled original-game traces to pin
the rate, and modelling depots, ports and development levels to make the
gathering rule itself faithful rather than merely conservative.

## Evidence

From `docs/game-systems.md`, itself condensed from the manual and shipped
release notes:

- "A tile's output is gathered only if it is **on or within one tile** of a
  connected depot or port. Overlapping catchments waste coverage."
- "A depot needs an unbroken rail path to the capital, **or** rail to a tile
  holding both a port and a depot (goods then travel by sea)."
- Turn step 6: "Commodities transported and delivered into the warehouse **for
  next turn**."
- "Transport capacity is a single pool of units-moved-per-turn, allocated
  across commodities by slider."

From the file formats: the 1997 `.map` records *which* deposit sits on a cell
and nothing about its output, so no rate can be recovered from the corpus. This
is why `LegacyWorldConverter` now stamps every imported deposit with the same
placeholder rather than inventing a table per resource.

Separate what is observed from what is concluded: the four bullets above are
observed. The conclusion drawn from them here is only that *connectivity to the
capital gates gathering*, which is the part implemented.

## Pseudocode

```text
for each country by dense id:
    collection points = the capital, plus every cell sharing the capital's
                        rail component
    catchment = collection points, widened by CatchmentRadius hex steps
    for each cell holding a deposit whose province this country owns:
        if the cell is in the catchment:
            collected[deposit.commodity] += deposit.yieldPerTurn
        else:
            stranded[deposit.commodity] += deposit.yieldPerTurn
    queue collected as pending deliveries; report stranded

Delivery commits the queue, so the goods are in Available stock when the next
turn's Production phase reads it.
```

A cell is stamped once per country, so a cell inside two collection points'
catchments pays once — the manual's "overlapping catchments waste coverage".

**Where this is deliberately conservative.** Depots and ports are not modelled,
so every cell of the capital's rail component stands in for a depot. Against
the original this can only *under*-collect where a depot would extend reach
that rail alone does not, and it over-collects only if the original required an
explicitly built depot on a railed tile. The substitution is recorded here
rather than hidden because it will change when depots arrive.

## Where implemented

- `ExtractionSettings` and `ResourceDefinition.YieldPerTurn` in `Imperialism.Core`.
- `ExtractionPlanner` and the `TurnPhase.Extraction` branch of `TurnResolver`.
- `ResourceExtractedEvent` carries both collected and stranded totals.
- `.iworld` v4 `resources[].yieldPerTurn` and `extraction.catchmentRadius`, with
  a v3 to v4 migration in `WorldContentMigrator`.
- Defaults shared by the migration and the legacy importer live on
  `WorldContentCodec.DefaultResourceYieldPerTurn` / `DefaultCatchmentRadius`.

## Test data

`tests/Imperialism.Core.Tests/ExtractionTests.cs` pins the catchment radius
(including radius 0), connectivity to the capital, a rail component that no
longer reaches the capital, a country with no capital, province ownership,
single-payment on overlapping catchments, multiple deposits on one cell, and
that this turn's harvest only reaches next turn's production.
`tests/Imperialism.Content.Tests/WorldContentTests.cs` pins the v3 to v4
migration and the content validation.

There is **no test pinning the rate against original behaviour**, because there
is no observed input/output pair to pin it to. That is the gap between `guess`
and `verified` here.

## Open questions

- The actual per-deposit rates, and whether they differ by resource.
- Development levels: the original raises a cell's output as engineers improve
  it, which is why a flat rate is a placeholder rather than a simplification.
- Depots and ports as buildable, losable objects, including the river-port and
  sea-port edge cases in `game-systems.md`.
- The transport capacity pool. Until it exists, a connected country moves
  everything it gathers, which is the most optimistic possible reading.
- Whether gold and gems bypass the warehouse and convert straight to cash on
  transport, as `game-systems.md` states for money.
