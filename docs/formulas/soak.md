# What a hundred turns actually looks like

## Summary

Every other test resolves one turn, or two. Extraction, development,
connectivity, production, labour, feeding and capacity construction had each
been pinned in isolation and never watched interacting over time, so an economy
that starved itself by turn six would have passed the entire suite.

`tests/Imperialism.Core.Tests/EconomySoakTests.cs` runs seven powers forward a
hundred turns and reports what happens.

**It found no bugs.** Both runs are stable, every integrity invariant holds, and
the totals reconcile against the fixture by hand. What it did surface is two
behaviours that are consequences of systems not built yet, recorded here so they
are not mistaken for defects later.

## The fixture

A resource-rich map would come back healthy and prove nothing, so the fixture is
deliberately thin — roughly **two or three deposits of each resource type** per
power, which is what a normal start looks like.

Each power gets a row of 22 cells: a capital, then a repeating
deposit / depot / deposit run so every deposit sits beside a connected depot.
Fourteen deposits each, all at development level 1, so every one yields 1 a turn.

Fair start throughout: mills at 2, factories at 1, workforce `[4, 2, 1]`.

| | per power, per turn |
|---|---|
| Grain gathered | **3** |
| Grain wanted | **4** — seven workers on the grain/fruit/grain/meat cycle |
| Fruit, livestock | 2 each, against 2 and 1 wanted |

## What happens

```
turn  workers  fed/sick/starved  labour   stock   capacity
   1       49    42/   7/      0      77      49        42
  10       49    42/   7/      0      77     273        56
  50       49    42/   7/      0      77     777       364
 100       49    42/   7/      0      77    1505       700
```

Totals over the production run: gathered 9,800, eaten 4,900, delivered 2,800,
produced 2,079 cycles, built 91 times. Those reconcile — 98 gathered a turn is
14 deposits times seven powers, 49 eaten a turn is one per worker.

**Nobody starves and nothing collapses.** The workforce holds at 49 for the
whole hundred turns.

## Two things worth knowing

### Chronic sickness has no way out

Exactly **one worker per power is sick every turn, for a hundred turns**, and
never recovers. That is the model working, not failing: grain supply is 3
against a demand of 4, so precisely one worker eats the wrong thing every turn.
Labour sits at 77 against a healthy 84 — a permanent 8% tax with no route back.

In the original a player fixes this by **buying grain**. We have no trade, so
the shortage is unfixable by construction. This is a missing system showing
through rather than a balance problem, and it is worth re-reading once
`ITradeMarket` is real: a chronic deficit should be solvable then, and if it
still is not, that *is* a defect.

### Building is the only sink, so capacity runs away

The stated policy expands whenever eight lumber and eight steel are lying about,
and over a hundred turns that takes a textile mill from 2 to **96** — while its
cotton input is 2 a turn, enough for a single cycle. Ninety-four points of
capacity bought for ninety-four lumber and ninety-four steel, which is very
nearly a power's entire output of both.

Faithful to the manual, which puts no cap on building and no penalty on unused
capacity. But it is only sane-looking because **nothing else consumes lumber and
steel yet** — no units, no railyard, no transport capacity, no trade. Expect this
picture to change completely once there is competition for those two commodities,
and do not tune anything against it in the meantime.

## What is asserted, and what deliberately is not

**Asserted** — only what can never be true of a correct run: no negative stock,
sick never exceeding workers per grade, the workforce never growing (nothing
recruits yet), capacity moving only during `Construction` and only upward to a
rung the ladder offers, labour spent never exceeding the pool at turn start, and
the date advancing exactly one quarter.

**Not asserted** — that nobody starves, how much stock accumulates, how large
capacity grows. Those are balance questions nobody has the evidence to settle,
and pinning them now would freeze a guess into a test.

**Also asserted: that the run did something.** A hundred turns which gathered
nothing and ate nothing would satisfy every invariant above while testing none
of them, which is exactly how eleven Python tests stayed green in #24. The soak
counts what it gathered, ate, delivered, produced and built, and fails if the
answer is nothing.

## Open

- Soak an imported `s1` rather than a synthetic fixture. That needs a headless
  runner, since the simulation cannot currently be driven outside xUnit.
- Re-read the sickness result once trade exists.
- A longer run than 100 turns; the original goes to roughly 400.
