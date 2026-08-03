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
| **Which grade starves, and which falls ill** | **a choice, not a finding** — cheapest first |
| **That both penalties land the turn after** | follows from the phase order, and from food being eaten as a turn ends |

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

## The invented rule, and its two applications

**The cheapest grades take the damage: untrained, then trained, then expert.**
Applied to starvation first, then to illness among whoever survived, so no
worker is counted twice.

The manual says a starving worker is permanently removed and that a badly fed
one does no labour, but never which worker either is. It cannot: the workforce
is a headcount per grade, and the feeding cycle orders workers by *preference*,
not by training. Something has to decide, and taking the cheapest mirrors the
way the pool grows — new arrivals are untrained — and costs the player least.

The alternatives are proportional loss, or losing whichever grade the hungry
worker happened to be, which would need workers individually ordered by grade as
well as by preference. Nothing in the corpus distinguishes them, because no
shipped scenario starves or sickens on the turn it begins.

**This is the one place the model is chosen rather than found.** It is also the
only place where getting it wrong is quietly expensive: an expert is worth four
untrained, so an ordering that sacrificed experts first would cost four times as
much labour for the same shortage.

## Where implemented

- `WorkerGrade`, `FeedingSettings`, `FoodPreference` and `InitialWorkforce` in
  `Imperialism.Core/FeedingDefinitions.cs`.
- `FeedingPlanner`, and the `TurnPhase.Feeding` branch of `TurnResolver`.
- `WorldState.GetWorkers` / `SetWorkers` / `GetTotalWorkers` /
  `GetAvailableLabour`, and `ConsumePending`, which lets feeding take from a
  delivery before it lands.
- `WorkersFedEvent` reports well fed, sick, starved and what was eaten.
- `WorldState.SetSickWorkers` / `GetSickWorkers`, which `GetAvailableLabour`
  nets out of the pool. Illness is **runtime state, not content**: a scenario
  cannot author it because nothing could be read from and nothing sensible
  invented, so every world starts well.
- `.iworld` v8 `feeding` and `scenarios[].workers`, with a v7 to v8 migration
  that adds neither.

## What eating badly costs

**Production spends the pool.** Each recipe costs its total input units in
labour, which the manual's tutorial prices outright for clothing and which every
shipped recipe agrees with; see `production.md` for the evidence and the
readings it leaves open. `GetAvailableLabour` is what `ProductionPlanner` draws
against, and it excludes anyone currently ill.

**Both penalties land on the following turn**, because `Production` sits ahead
of `Feeding` in the pipeline. A workforce that starves still works the turn it
dies; a worker who ate the wrong thing still works the turn it was served. That
is the faithful ordering rather than a concession to the phase list: food is
eaten as the turn ends, and the arm icon the player allocates against has to
know already who is unwell. The turn after is the first whose orders could have
been given in light of it.

**Illness is rewritten every turn, not accumulated.** That is what makes
recovery need no rule of its own — one good meal and the pool is whole again,
which is the simplest reading of "no labour *that turn*". Nothing carries an
illness forward on its own.

## Test data

`tests/Imperialism.Core.Tests/FeedingTests.cs` pins the cycle for headcounts
that do and do not divide by four, either meat satisfying the fourth preference,
canned food substituting without illness, the wrong food causing sickness,
starvation removing the untrained first, pending deliveries being eaten before
warehouse stock, a partly eaten delivery keeping its remainder, and the labour
sum.

`tests/Imperialism.Core.Tests/LabourTests.cs` pins what illness costs: falling
ill leaves this turn's production untouched and cuts the next turn's, the
cheapest grade falls ill first, eating properly again restores the whole pool,
starvation and illness together take the cheapest workers in that order without
double-counting anyone, and a grade can never hold more sick workers than
workers.

`LegacyWorldConverterTests` converts the whole corpus and asserts all seven
workforces per scenario and `s1`'s spread specifically. It also **resolves a real
turn on imported `s1`**: 60 workers, 165 labour, all 60 well fed with none sick
and none starved, and so 165 labour still standing for turn two. A shipped scenario feeding its workforce properly on turn
one is a good signal that the feeding rules, the extraction model and the
scenario's own starting stock agree.

## Open questions

- Power plants, which the manual says add directly to the labour pool and are
  spent before human labour.
- The Trade School: how workers are promoted between grades.
- Recruiting new workers, which the manual says costs canned food.
- Whether the original penalises illness on the same turn it is diagnosed rather
  than the next. Our phase order forces the next, and the player-facing reading
  agrees, but nothing observed confirms it.
