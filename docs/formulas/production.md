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

## Pseudocode

```text
for each country by dense id:
    remaining capacity = that country's limited facility capacities
    starting inputs = a copy of Available inventory
    for each requested recipe in submitted priority order:
        cycles = minimum(requested, available inputs, shared facility capacity)
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

- `ProductionFacilityDefinition`, `ProductionRecipeDefinition`, and
  `InitialProductionCapacity` in `Imperialism.Core`.
- `ProductionPlanner` and `TurnResolver` in `Imperialism.Core`.
- `.iworld` v3 production definitions and capacities in `Imperialism.Content`.
- Standard catalog plus `capa`/`ware` conversion in
  `Imperialism.LegacyImport.LegacyWorldConverter`.

## Test data

Generated tests pin shared capacity, priority order, partial completion,
same-turn output isolation, unlimited facilities, overflow atomicity, strict
content validation, v2→v3 migration, all standard legacy recipes, and arbitrary
tutorial-style capacities. Original-corpus conversion is a local gate because
source files are not tracked.

## Open questions

- Exact interaction between persisted factory orders and player edits after a
  shortage. Modern turn orders are currently explicit per-turn submissions.
- Labour and transient power allocation order within a facility.
- Whether the UI reserves inputs exactly when an order is entered in every edge
  case, or only presents that behavior while the turn resolver recomputes it.
- Capacity construction timing and costs in conjunction with the railyard.
