# Industrial production

## Summary

Industrial recipes convert warehouse commodities at the end of a turn. Mills
and factories have shared output capacity; food processing is uncapped. This
document separates the well-documented recipe ratios from rules that still need
original-behaviour verification, especially labour and remembered orders.

## Confidence

`inferred`. The manual and quick-reference card independently establish the
recipe graph, ratios, facility sharing, capacity progressions, and next-turn
availability. The original scenario corpus confirms facility IDs and stored
capacity values. Confidence remains below `verified` until controlled original
game input/output traces pin down shortage and order-persistence behaviour.

Labour carries its own evidence, tabulated rather than averaged:

| Claim | Support |
|---|---|
| Production consumes labour at all | **manual** — "without some labour you cannot produce fabric" |
| One unit of clothing costs two fabric **and two labour** | **manual**, quoted below — the only recipe it prices |
| Every shipped recipe spends two input units per unit of output | **corpus-verified** against the recipe table |
| Therefore labour per cycle = total input units | follows from the two above, for the shipped set only |
| Labour is one pool per country, not per facility | **manual** — one arm icon on the border, drawn down by every dialog |
| Power is spent before human labour | **manual** — not modelled yet |

## Evidence

- Manual PDF pages 54–64 (printed industry pages 50–60): production runs at End
  Turn; output is available the following turn; entered orders reserve stock;
  mills/factories have finite shared output capacity; food processing is
  uncapped; the documented recipe ratios are listed below.
- Quick reference card page 4: independently shows the commodity dependency
  graph and separates power from stored commodities.
- `ImpEdit v2/ImpScen.doc`: `capa country industry capacity` maps IDs 0–6 to
  textile, clothing, steel, metal works, lumber, furniture, and oil facilities.
- All ten binary scenarios contain 42 `capa` records: seven powers times six
  industries. Tutorials contain capacities 3, 5, 6, and 7, proving that imported
  capacity cannot be restricted to the historical upgrade ladder.

The original recipes represented by the legacy importer are:

- 2 cotton or 2 wool → 1 fabric;
- 2 fabric → 1 clothing;
- 1 coal + 1 iron → 1 steel;
- 2 steel → 1 hardware or 1 armaments;
- 2 timber → 1 lumber or 1 paper;
- 2 lumber → 1 furniture;
- 2 oil → 1 fuel;
- 2 grain + 1 fruit + 1 fish or livestock → 2 canned food.

### Labour per cycle

The manual documents that production needs labour and then never prices it —
except once, in the tutorial walk-through, where it prices it exactly:

> Every time you order a new unit of clothing you expend two units of fabric
> and use (for this turn) two units of labour.

Clothing is `2 fabric → 1 clothing`, so that single sentence admits three
readings: two labour per cycle, one per input unit, or two per unit of output.
**They are the same number for every recipe the original ships.** Check the list
above: each one consumes exactly two input units for each unit it produces —
including canned food, whose cycle takes four inputs and makes two. There is no
shipped recipe on which the readings disagree, so the rate is determined for all
of them without choosing between the readings.

We implement it as **the recipe's total input units**, carried as an explicit
`labourCost` per recipe rather than derived at runtime, so a recipe that is not
2:1 — a modded one, or a future railyard — must state its own price instead of
silently inheriting a rule that was only ever verified on 2:1 recipes.

The pool is **per country and shared across every facility**: the manual shows a
single arm icon on the screen border that every production dialog draws down,
against per-facility capacity bars that do not interact. Requests are served in
submitted priority order, the same rule capacity already used.

It counts only workers who are well. Illness is decided in `Feeding`, which runs
after `Production`, so a bad harvest shows up in the pool one turn later — see
`feeding.md`, which also records the one chosen rule in the model: which grade
takes the damage.

Three things the manual states that are **not modelled yet**: power adds to the
labour pool on the turn it is generated and is spent before any human labour;
the Trade School takes a worker out of the pool for the turn it trains; and
building a civilian in the University permanently removes an expert worker.

### What the disassembly could not settle

The labour rate was looked for in the original binary first and not found.
Recorded so the next attempt starts further along:

- `UCity.cpp` (`004B3080`–`004B427F`) is the economy module by assert anchor, but
  reading it for a specific constant is a project, not a lookup.
- Searching for the labour *total* — `untrained*1 + trained*2 + expert*4` — as a
  shift-and-add pattern found nothing. `shl reg, 01` does not occur in this
  build at all (the compiler emits `add reg, reg`, only 28 sites), and the 991
  `4*` / 838 `2*` `lea` forms are overwhelmingly array indexing. The one
  promising hit inside a `UCountry.cpp` span, `004DBB02`, turned out to be a
  six-direction map walk over the 36-byte cell record.
- The quick reference card holds the industrial dependency graph but is a
  scan with no text layer and no numbers on it; its production page shows the
  commodity arrows only.
- `README.TXT` / `README11.TXT` mention labour once, about power, with no rate.

Whether the original stores a per-recipe labour cost in a table or derives it is
still unknown. Finding that table would confirm or refute the input-total
reading on recipes we have no other evidence for.

## Pseudocode

```text
for each country by dense id:
    remaining capacity = that country's limited facility capacities
    remaining labour = the healthy workforce's labour, or unbounded with no feeding
    starting inputs = a copy of Available inventory
    for each requested recipe in submitted priority order:
        cycles = minimum(requested, shared facility capacity,
                         remaining labour / recipe labour cost, available inputs)
        subtract cycles * labour cost from remaining labour
        subtract scaled inputs from starting inputs and final inventory delta
        add scaled outputs only to final inventory delta
        emit one result event, including zero or partial completion

preflight final inventory after production and all pending deliveries
commit production during Production; commit deliveries during Delivery
```

Produced goods are deliberately excluded from `starting inputs`, so they cannot
feed another recipe in the same resolution. Alternative ingredients are
separate recipes rather than a special `or` expression, making allocation order
explicit and keeping the content model general.

## Where implemented

- `ProductionFacilityDefinition`, `ProductionRecipeDefinition` (including
  `LabourCost`), and `InitialProductionCapacity` in `Imperialism.Core`.
- `ProductionPlanner` and `TurnResolver` in `Imperialism.Core`, with the pool
  read from `WorldState.GetAvailableLabour`, which excludes the sick.
- `ProductionCompletedEvent.LabourUsed` reports what each request actually spent.
- `.iworld` v3 production definitions and capacities in `Imperialism.Content`;
  v9 adds the per-recipe `labourCost`.
- Standard catalog plus `capa`/`ware` conversion in
  `Imperialism.LegacyImport.LegacyWorldConverter`.

## Test data

Generated tests pin shared capacity, priority order, partial completion,
same-turn output isolation, unlimited facilities, overflow atomicity, strict
content validation, v2→v3 migration, all standard legacy recipes, and arbitrary
tutorial-style capacities. Original-corpus conversion is a local gate because
source files are not tracked.

`tests/Imperialism.Core.Tests/LabourTests.cs` pins the manual's clothing example
outright, labour capping cycles the warehouse could otherwise afford, the 1/2/4
grade multipliers, one pool shared across facilities in submitted order (and the
reverse order giving the opposite answer), labour billed only for cycles that
ran, a workforce of none producing nothing, a world with no feeding ignoring
labour entirely, and starvation costing labour on the following turn.

`LegacyWorldConverterTests` asserts that every standard recipe's labour equals
both its input total and twice its output total — the two readings the manual
leaves open, which coincide throughout. `AnImportedScenarioResolvesATurn`
orders Britain's clothing factory flat out on imported `s1`: it completes two
cycles for four labour out of a pool of 165, so the factory's capacity binds and
the workforce does not, which is how a starting position should read.

## Open questions

- Exact interaction between persisted factory orders and player edits after a
  shortage. Modern turn orders are currently explicit per-turn submissions.
- Whether the original stores a per-recipe labour cost or derives one. Every
  shipped recipe is 2:1, so no evidence we have distinguishes them; a recipe
  that is not 2:1 would, and none exists yet.
- Transient power, which the manual says joins the pool on the turn it is
  generated and is spent before any human labour.
- The Trade School and the University, which take labour and workers out of the
  pool respectively.
- Whether the UI reserves inputs exactly when an order is entered in every edge
  case, or only presents that behavior while the turn resolver recomputes it.
- Capacity construction timing and costs in conjunction with the railyard. The
  railyard is the first facility we expect *not* to be 2:1, so it is where the
  input-total reading gets its first real test.
