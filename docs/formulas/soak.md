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

### Chronic sickness had no way out — and now has one

Exactly **one worker per power is sick every turn, for a hundred turns**, and
never recovers. That is the model working, not failing: grain supply is 3
against a demand of 4, so precisely one worker eats the wrong thing every turn.
Labour sits at 77 against a healthy 84 — a permanent 8% tax with no route back.

That was written when nothing in the engine could improve a tile. Civilian units
changed it, and the fixture now seeds three Farmers per power so both answers can
be seen side by side. The paragraph above still describes every run that leaves
them idle.

In the original a player also fixes this by **buying grain**. We have no trade,
so that route remains closed, and it is still worth re-reading this once
`ITradeMarket` is real.

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

### Improving farms unblocks everything, and then hits its own ceiling

The fixture seeds three Farmers per power. Two runs differ only in whether they
are ever told to work.

```
                       turn  workers  fed/sick  grain/turn  total levels
idle Farmers            100       49    42/  7          21           98
Farmers working           1       49    42/  7          21           98
                          2       49    49/  0          35          119
                         10       77    77/  0          63          196
                         25      126   105/ 21          63          196
                        100      119    98/ 21          63          196
```

The chain the owner described runs end to end: farms improved on turn 2,
sickness gone the same turn, **first recruit on turn 4** — the first time
migration has done anything since it was built — and the workforce more than
doubling.

Then the deficit reopens. Every tile a Farmer can work reaches the top of its
curve by turn 10, grain stops at 63, and the population keeps growing until it
outruns the harvest; sickness returns on turn 14 and the economy settles at 119
workers with 21 permanently ill. **That is the manual's own warning about
growing faster than you can feed**, arrived at rather than written in, and it is
reported rather than tuned away.

Two caveats before reading anything into the turn numbers. The work duration is
a guess (`development.md`), and this fixture's yield curve starts at 0 rather
than the manual's 1, so both the speed and the ceiling are properties of the
fixture.

## What is asserted, and what deliberately is not

**Asserted** — only what can never be true of a correct run: no negative stock,
sick never exceeding workers per grade, every worker who appears accounted for
by a recruitment event, capacity moving only during `Construction` and only
upward to a rung the ladder offers, labour spent never exceeding the pool at
turn start, and the date advancing exactly one quarter.

The farming run adds one more of the same kind: the chain must happen **in
order** — a tile improved before sickness clears, sickness cleared before anyone
is recruited. Which turn each lands on is reported, not asserted.

**Not asserted** — that nobody starves, how much stock accumulates, how large
capacity grows. Those are balance questions nobody has the evidence to settle,
and pinning them now would freeze a guess into a test.

**Also asserted: that the run did something.** A hundred turns which gathered
nothing and ate nothing would satisfy every invariant above while testing none
of them, which is exactly how eleven Python tests stayed green in #24. The soak
counts what it gathered, ate, delivered, produced and built, and fails if the
answer is nothing.

### A Prospector finding coal is separable from all of it

A third world adds four columns of barren hills to each power's row — two
carrying coal, one a depot, one bare — and two runs differ only in whether the
Prospectors look.

```
                    searched  found  mines  gathered  levels at 100
Prospectors idle           0      0      0    19,138            196
Prospectors working       28     14     14    20,468            210
```

First search on turn 2, first mine on turn 4. **Workers, grain and sickness are
identical in both** — 49 rising to 119, sickness back on turn 14 — so the whole
observable effect of discovery is 14 mines and 1,330 coal. That separation is
worth having: it means the food chain above can be read without wondering what
the minerals were doing.

The idle run's other number is the one to remember: **700 refusals**, one per
power per turn, each a Miner turned back from hills its country cannot see.
See [prospecting.md](prospecting.md).

## Open

- Soak an imported `s1` rather than a synthetic fixture. That needs a headless
  runner, since the simulation cannot currently be driven outside xUnit.
- Re-read the sickness result once trade exists.
- A longer run than 100 turns; the original goes to roughly 400.
