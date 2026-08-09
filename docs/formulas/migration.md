# Migration

## Summary

The Capitol draws rural workers into industry. It is the only thing in the
engine that makes a workforce grow, and the demand half of the answer to the
soak's standing food deficit — the supply half is improving farms, which needs
civilian units that do not exist yet.

## Confidence

`inferred`, and **the one `guess` it used to rest on is recovered.**

| Claim | Support |
|---|---|
| Recruits arrive **untrained** | **manual** — "new workers migrate to industry, their untrained efforts supply only one unit of labour" |
| The price is **canned food, clothing and furniture** | **manual** — "the comforts of a developing economy" |
| The cap is **floor(provinces owned / 4)** | **manual**, stated outright |
| It may be done every turn the commodities are there | **manual** |
| **How much of each commodity per worker** | **resource-backed** — one of each, and the guess was right |
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
of any commodity a worker costs**. One of each shipped as a placeholder chosen
because nothing better was available, under a warning label saying nothing
downstream should cite it.

**The original's own help text states it, and the placeholder was right:** one food
plus one furniture plus one clothing makes one untrained worker. See
[`../disasm/definitive-original-data.md`](../disasm/definitive-original-data.md).

Worth recording precisely *because* it came out right. A guess that survives
verification is not evidence that guessing works — the same block prices two things
this project had no number for at all, and there was no way to know in advance which
of the three the manual's silence was hiding. The warning label was correct while it
stood, and removing it now is the point of having had one.

**The same block prices worker training, and nothing models it.** An untrained worker
plus one paper and $100 becomes trained; a trained worker plus two paper and $1,000
becomes expert. The Capitol recruits untrained workers and nothing promotes them, so
these two numbers are recorded and unused — waiting on the Trade School and the
University.

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

## What unblocked it

Civilian units did, and the same soak now shows both sides. The Farmers each
power starts with are left idle in the run above and put to work in
`FarmersImprovingGrainCloseTheDeficitAndUnblockMigration`, and nothing else
differs:

| | idle Farmers | Farmers working |
|---|---|---|
| Grain a turn | 21 throughout | 21 → 63 |
| Sick workers | 7 throughout | 0 by turn 2 |
| First recruit | never | **turn 4** |
| Workforce after 100 turns | 49 | 119 |
| Requests answered | 0 of 700 | 119 of 700 |

The four-step chain above runs in reverse once a Farmer improves a tile: spare
grain appears, canned food gets made, the Capitol's price can be paid, and
people arrive.

It then finds its own ceiling. Every tile a Farmer can work is at the top of its
curve by turn 10, grain stops at 63, and the population keeps growing until it
outruns the harvest — sickness returns on turn 14 and the economy settles at 119
workers with 21 permanently ill. That is the manual's warning about growing
faster than you can feed, and it is reported rather than tuned away. See
`development.md`.

## Open questions

- ~~What a worker actually costs.~~ **Recovered**, and the placeholder was right.
- **Worker training**, priced above and unmodelled: the Trade School and the
  University are what would spend it.
- The Capitol upgrade to one-third, and what triggers it.
- Whether recruits should eat on their arrival turn. The phase order forces it
  and the manual's warning agrees, but nothing observed confirms it.
