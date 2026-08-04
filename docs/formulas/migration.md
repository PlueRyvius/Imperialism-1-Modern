# Migration

## Summary

The Capitol draws rural workers into industry. It is the only thing in the
engine that makes a workforce grow, and the demand half of the answer to the
soak's standing food deficit — the supply half is improving farms, which needs
civilian units that do not exist yet.

## Confidence

`inferred`, and it rests on one `guess`.

| Claim | Support |
|---|---|
| Recruits arrive **untrained** | **manual** — "new workers migrate to industry, their untrained efforts supply only one unit of labour" |
| The price is **canned food, clothing and furniture** | **manual** — "the comforts of a developing economy" |
| The cap is **floor(provinces owned / 4)** | **manual**, stated outright |
| It may be done every turn the commodities are there | **manual** |
| **How much of each commodity per worker** | **a guess** — one of each |
| Recruits eat on the turn they arrive | follows from the phase order; see below |

## Evidence

> To recruit rural workers for industry you need to supply them with the
> comforts of a developing economy: canned foods, clothing, and furniture.
>
> You may recruit untrained workers using this dialog during every turn, when
> you have the necessary commodities. However, the size of your country limits
> the number of workers that migrate during one turn to one-fourth of the number
> of provinces you own, rounded down. Later in the game, your Capitol building
> may upgrade in response to successful foreign policies. Then the limit becomes
> one-third of the number of provinces owned.
>
> Be careful not to increase your population too fast. In addition to the cost
> of recruiting the new worker, you have to supply the workers with food each
> turn.

The manual names the three commodities and the divisor and **never says how much
of any commodity a worker costs**. One of each is a placeholder chosen because
nothing better is available. It is a real economic constant nobody has measured
— not a symmetric default in the `CLAUDE.md` sense — so it carries a warning
label and nothing downstream should cite it.

**The Capitol upgrade to one-third is not implemented.** There is no upgrade
mechanic and the manual gives no trigger beyond "successful foreign policies",
so inventing one would be inventing a rule rather than filling in a value.

## Pseudocode

```text
for each country by dense id:
    if it asked for nobody, skip
    owned  = provinces whose owner is this country
    limit  = owned / provincesPerRecruit          # integer division, rounds down
    coming = min(requested, limit)
    for each commodity in the cost:
        coming = min(coming, available / cost per worker)
    charge coming * cost, add coming untrained workers
    emit an event even when coming is zero
```

**A migration order is not all-or-nothing**, unlike an expansion. The manual
describes a slider dragged until something runs out, so asking for more than the
country can afford brings as many as it can rather than none.

**Zero is still reported.** "Your country is too small for any" and "you cannot
afford it" are both things a player needs to see; a silence would leave them
dragging a slider that does nothing.

## Where it sits in the turn

`Migration` runs **after `Construction` and before `Feeding`**, and that decides
two behaviours:

- **A recruit eats on the turn it arrives.** The manual's warning about growing
  too fast only has teeth if it does.
- **A recruit supplies no labour until the next turn**, because `Production` has
  already run. Same shape as capacity construction, and no deferral machinery
  needed.

Migration is priced last, after production and building have committed their
spending, so one turn cannot spend the same clothing twice.

## Where implemented

- `MigrationSettings`, `MigrationPlanner`, and the `TurnPhase.Migration` branch
  of `TurnResolver` in `Imperialism.Core`.
- `CountryTurnOrders.RecruitWorkers`; `WorkersRecruitedEvent` reports requested,
  recruited, the size limit and what was paid.
- `.iworld` v12 `migration`, with a v11 to v12 migration that adds none.
- The standard catalog in `Imperialism.LegacyImport.LegacyWorldConverter`.

## Test data

`tests/Imperialism.Core.Tests/MigrationTests.cs` pins recruits arriving
untrained, the size cap at four provinces per recruit including the country of
three that can recruit nobody, the scarcest comfort deciding how many come, a
request beyond the warehouse bringing as many as it can afford, zero being
reported rather than swallowed, a recruit eating on arrival and working only
from the next turn, and a world with no Capitol terms being unable to recruit.

## What the soak showed: nobody came

`EconomySoakTests.AHundredTurnsOfRecruitingShowsWhatGrowthCosts` asks the
Capitol for the maximum every turn for a hundred turns. Across seven powers that
is **700 requests and zero arrivals.**

The chain is worth following, because it is the whole shape of the problem:

1. Canned food is made from grain.
2. Grain supply is 3 a turn against a demand of 4, so every unit is eaten.
3. There is never a spare grain unit, so canned food is never made.
4. The Capitol's price can never be paid, so the workforce never grows.

**Migration is implemented and correct and completely inert**, because
population growth is gated behind solving the food deficit — and the way to
solve that is improving farms, which needs civilian units. The mechanism is
proved by the unit tests; the soak proves what it is waiting for.

## Open questions

- What a worker actually costs. The one thing here that is guessed.
- The Capitol upgrade to one-third, and what triggers it.
- Whether recruits should eat on their arrival turn. The phase order forces it
  and the manual's warning agrees, but nothing observed confirms it.
