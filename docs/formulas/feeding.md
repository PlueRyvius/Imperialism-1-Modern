# Worker feeding and labour

## Summary

Every worker eats one unit of food per turn, and what it eats decides whether it
works. This is the demand side of everything extraction produces, and the only
mechanic that can permanently destroy a country's capacity to do anything.

## Confidence

`inferred`. The rules come from the manual and the workforce numbers from the
corpus, so almost nothing here is invented — but the manual is documentation
rather than observed behaviour, and where it and the release notes disagree
`game-systems.md` says the release notes win.

| Claim | Support |
|---|---|
| Half want grain, a quarter fruit, the rest livestock **or** fish | **manual** |
| One unit per worker per turn | **manual** — "each individual worker enjoys only one type of food" |
| Canned food substitutes without illness | **manual** |
| The wrong food means sick, and no labour that turn | **manual** |
| Nothing at all means starvation and permanent removal | **manual** |
| Labour 1 / 2 / 4 by grade | **manual** |
| Starting workforces | **corpus-verified** — `labo`, 7 records in all ten scenarios |
| Grade order untrained, trained, expert | **corpus-verified** — see below |
| Preference runs as a repeating cycle of four | `game-systems.md`, from the release notes |
| **Which grade starves first** | **a choice, not a finding** |

## Evidence

### The grade order is settled by the data

`labo` is `[country, untrained, trained, expert]`. `s1` gives country 2
`[60, 5, 0]` and country 3 `[120, 20, 0]` — backward powers with a mass of
unskilled labour and no experts at all. Read the other way round they would be
powers with 60 and 120 *experts* and nobody in training, which no designer would
author. Country 6 is `[5, 15, 40]`, the opposite profile, and country 0 is
`[15, 15, 30]`.

Every one of the ten scenarios carries all seven records.

### The cycle

The manual gives proportions — half, a quarter, the rest —
and `game-systems.md` records from the release notes that this runs as a
repeating cycle of four: **grain, fruit, grain, meat-or-fish**. Walking the cycle
one worker at a time gives those proportions exactly for any headcount, with no
rounding rule to invent. Only the remainder distinguishes it from computing
`n/2`, `n/4`, `n/4` directly, and only when the headcount is not a multiple of
four.

## Pseudocode

```text
for each country:
    for worker i in 0 .. headcount-1:
        preference = cycle[i mod cycle.length]
        eat one of:
            any commodity the preference accepts   -> well fed
            canned food                            -> well fed
            any other food at all                  -> sick, no labour this turn
            nothing                                -> starved, removed for good

food is taken from this turn's pending deliveries before warehouse stock
labour = sum over grades of (workers * labourPerGrade)
```

**Food arriving this turn is eaten first.** This is one of the two documented
same-resolution exceptions to everything else being deferred a turn, and it is
why `Feeding` sits between `Extraction` and `Delivery`: the harvest is eaten off
the back of the cart, and only the remainder reaches the warehouse.

## The one invented rule

**Which grade starves first: untrained, then trained, then expert.** The manual
says a starving worker is permanently removed but never which one. This ordering
mirrors the way the pool grows — new arrivals are untrained — and costs the
player least.

The alternatives are proportional loss, or losing whichever grade the hungry
worker happened to be, which would need workers to be individually ordered by
grade as well as by preference. Nothing in the corpus distinguishes them,
because no shipped scenario starves on the turn it begins.

## Where implemented

- `WorkerGrade`, `FeedingSettings`, `FoodPreference` and `InitialWorkforce` in
  `Imperialism.Core/FeedingDefinitions.cs`.
- `FeedingPlanner`, and the `TurnPhase.Feeding` branch of `TurnResolver`.
- `WorldState.GetWorkers` / `SetWorkers` / `GetTotalWorkers` /
  `GetAvailableLabour`, and `ConsumePending`, which lets feeding take from a
  delivery before it lands.
- `WorkersFedEvent` reports well fed, sick, starved and what was eaten.
- `.iworld` v8 `feeding` and `scenarios[].workers`, with a v7 to v8 migration
  that adds neither.

## Labour is spent, but sickness still is not

**Production spends the pool.** Each recipe costs its total input units in
labour, which the manual's tutorial prices outright for clothing and which every
shipped recipe agrees with; see `production.md` for the evidence and the
readings it leaves open. `GetAvailableLabour` is what `ProductionPlanner` draws
against.

**Starvation therefore has teeth.** Workers who find nothing are removed, and
the smaller workforce supplies less labour when the next turn's orders resolve.
`Production` sits ahead of `Feeding` in the pipeline, so a workforce that
starves still works the turn it dies — which matches the original's ordering,
where the labour you allocate is the labour you were shown before ending the
turn.

**Sickness still does not reduce the pool**, and closing that gap needs a rule
nobody has evidence for. Feeding walks workers by position in the preference
cycle and counts how many ate badly; it never learns *which grade* they were.
Docking the pool would mean deciding whether a sick worker is untrained, trained
or expert — the same invention this document already declined for starvation,
except that starvation at least has a cheapest-first convention to fall back on,
and sickness would need workers ordered by grade as well as by preference.

So a sick worker is reported and costs nothing. That is a known understatement
of the penalty, recorded here rather than papered over.

## Test data

`tests/Imperialism.Core.Tests/FeedingTests.cs` pins the cycle for headcounts
that do and do not divide by four, either meat satisfying the fourth preference,
canned food substituting without illness, the wrong food causing sickness,
starvation removing the untrained first, pending deliveries being eaten before
warehouse stock, a partly eaten delivery keeping its remainder, and the labour
sum.

`LegacyWorldConverterTests` converts the whole corpus and asserts all seven
workforces per scenario and `s1`'s spread specifically. It also **resolves a real
turn on imported `s1`**: 60 workers, 165 labour, and all 60 well fed with none
sick and none starved. A shipped scenario feeding its workforce properly on turn
one is a good signal that the feeding rules, the extraction model and the
scenario's own starting stock agree.

## Open questions

- **Which grade falls sick**, which is what keeps sickness free. See above.
- Power plants, which the manual says add directly to the labour pool and are
  spent before human labour.
- The Trade School: how workers are promoted between grades.
- Recruiting new workers, which the manual says costs canned food.
- Whether a sick worker recovers automatically the next turn. Nothing here makes
  sickness persist, which is the simplest reading of "no labour **that turn**".
