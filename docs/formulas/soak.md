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

Those figures are from the no-orders run, which never improves anything and is
therefore untouched by everything below.

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
                          5       49    49/  0          35          119
                         10       70    70/  0          42          140
                         25       77    77/  0          42          168
                        100       77    77/  0          42          168
```

The chain the owner described runs end to end: farms improved on turn 4,
sickness gone the same turn, **first recruit on turn 6** — the first time
migration has done anything since it was built — and the workforce growing by
more than half.

**The deficit used to reopen and no longer does.** At a one-turn work duration
the farms improved fast enough for the population to reach 84 and outrun its own
food, so sickness returned on turn 14 — reported here as the manual's own
warning about growing faster than you can feed. At the measured three turns the
population settles at 77 and nobody is ever ill again. Both runs end on the same
42 grain a turn and half the workforce wants grain, so 84 needs exactly 42 and
77 needs 39: **a knife edge that a measured number happened to fall the other
side of.** Reported rather than engineered back in.

**Grain stops where it does because of technology.** These numbers were 63 and
119 before the Benefits of Technology Table was transcribed, when a Farmer could
walk a tile to Level 3 for nothing. Steel and Iron Plows and Mechanical Reaper
are not free, so the ceiling arrives sooner and lower. The move is the finding
rather than a regression; see the run below and
[technology.md](technology.md).

One caveat before reading anything into the turn numbers: this fixture's yield
curve starts at 0 rather than the manual's 1, so the ceiling is a property of
the fixture. The *speed* no longer is — the work duration was a guess when this
was written and has since been measured at three turns
(`development.md`).

### A gate opening, halfway through

Grain's top rung is gated behind Mechanical Reaper. There is no research to earn
it, so a third pair of runs grants it outright on turn 50 — **the pattern for
exercising any gate while acquisition does not exist**, and without which a gate
is only ever tested closed.

```
                    grain/turn at  10 → 50 → 100   workers  top rungs  refusals
never granted                42     42     42         77         0       1,820
granted on turn 50           42     42     63        105        21         749
```

First gated rung on turn 53, three turns after the grant, which is the work
duration showing through. Twenty-eight extra workers by turn 100 is what one
technology is worth to this fixture.

**The grain columns held when the duration was measured and the others did
not** — workers fell from 84 to 77, refusals from 1,960 and 889 to 1,820 and
749, and the first gated rung slid from turn 51 to 53. *Where* the ceiling sits
is technology; *how fast* a country reaches it is duration.

### And then the grant stopped being necessary

**The soak no longer has to cheat to open a gate.** With the Investment screen
built, a fourth group of runs has powers pay for the technology out of a gold mine,
and the granting run above becomes the *control* rather than the only way in.

The fixture now starts in **1840** rather than 1815, and only for this: a turn is a
quarter, so a hundred turns from 1815 stop in 1839 and Mechanical Reaper's real 1851
arrival would be permanently out of reach. Nothing else in the fixture reads the
year, so moving it moved **no** published number in this document. The chain and the
prices are the real ones: Steel and Iron Plows at 3,000 arrived in 1831 and are
buyable on turn one, Mechanical Reaper at 12,000 wants the Plows and arrives on turn
45.

```
                              grain/turn  workers  top rungs  first  bought  spent    treasuries
funded and patient             21 → 63    49 → 105     21       59    14     105,000    21,000
ordinary treasury, greedy      21 → 42    49 →  77      0     never    7      21,000    77,000
granted free on turn 50        21 → 63    49 → 105     21       53     0           0    77,000
```

These three are a richer configuration than the pair above — improvement priced, a
gold mine attached — so they are not a restatement of it.

**Two walls, and the run separates them.** The funded run buys the Reaper on turn
45, *the quarter it arrives*, having had the money ready for dozens of turns: the
calendar is a wall no amount of cash moves, and the 308 refusals are it being hit
over and over. The greedy run — ordinary treasury, improving whenever it can — buys
the Plows and never the Reaper, because improvement is charged first and research
takes the remainder.

**The second finding is a knife edge and should not be leaned on hard.** The greedy
run ends holding 11,000 a power against a 12,000 price: it misses by a *thousand*,
so a slightly richer mine flips it. Same shape as the grain knife edge above. What
is robust is the direction — twelve thousand is most of a century of one gold
mine's income, and a power that spends as it earns does not get there.

The first gated rung lands on turn 59, fourteen turns after the purchase rather
than the bare three: the Farmer has to finish what it was already doing and walk.
That gap is the difference between "the ceiling lifted" and "a tile reached it".

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
Prospectors idle           0      0      0    16,240            168
Prospectors working       28     14     14    17,486            182
```

First search on turn 4, first mine on turn 8. **Workers, grain and sickness are
identical in both** — 49 rising to 77, and nobody ill after turn 4 — so the whole
observable effect of discovery is 14 mines and 1,246 coal. That separation is
worth having: it means the food chain above can be read without wondering what
the minerals were doing.

The idle run's other number is the one to remember: **700 refusals**, one per
power per turn, each a Miner turned back from hills its country cannot see.
See [prospecting.md](prospecting.md).

### The network is the constraint everything else was hiding behind

Every run above carries everything it gathers, because until transport capacity
existed nothing limited the middle of the chain. Three more runs give the same
world ten points of capacity a power, against fourteen units gathered and seven
eaten.

```
                        workers  sick  carried/gathered  produced  capacity built
unlimited                    77     0     16,240/16,240     3,038              —
food first                   49     7      7,000/16,240         7               0
materials first              42     0     15,673/16,240     1,386             672
```

**On an empty warehouse, which slider comes first decides what the country
becomes.** Food first keeps everyone fed and never produces a single cycle,
because coal sits at the back of the queue and the steel mill never sees any — so
no railyard is affordable and the network never grows. Materials first starves
seven workers immediately, runs sick for twenty turns, and comes out carrying
almost everything.

**But the empty warehouse was doing the work, and that is a correction.** The
manual says a power starts with stockpiles of lumber and steel. Give the same
world twenty of each and the two orderings converge — both reach 16,212 carried
and roughly 800 points of capacity built, and materials-first is simply worse,
since it still costs seven workers on turn one for nothing.

```
                        workers  sick  carried/gathered  produced  capacity built
food first, stocked          49     0     16,212/16,240     1,372             805
materials first, stocked     42     0     16,212/16,240     1,386             812
```

So the slider order is worth as much as capacity is scarce, and a stockpile makes
it scarce only briefly.

**This file previously concluded that a network below subsistence never
recovers. That was wrong** — it was true of an empty warehouse, not of a small
network. At four points a power with a stockpile the country buys its way out on
turn one. What survives is narrower and still worth knowing: a network under what
its workforce eats costs that workforce on the **first** turn whatever is in the
warehouse, because capacity bought on turn one does not carry until turn two. See
[transport.md](transport.md).

### Paying for development, and the loop that funds it

Three runs on the farming world, differing only in whether improvement is charged
for and whether anything earns.

```
                              tiles improved  gathered  treasury at 100
free                                      70    16,240                0
priced, no income                         35    13,139                0
priced, with a gold mine                  63    16,135           98,000
```

Priced with nothing coming in, 5,000 a power buys exactly five rungs at 1,000
apiece and the last sixty-five turns are spent standing still. **That is an
artefact of missing trade rather than a property of the model** — the same trap
this file fell into over the empty warehouse — and the middle row reads
*development is now something a country has to afford*, never *a country cannot
afford development*.

With one gold tile a power at the manual's $200 a unit the loop closes: 20,000
over the century buys back nearly all the development the free run got for
nothing, with change to spare. **This is the first run in this file where
anything has ever needed an income.**

The extra development does not land on grain — its top rung is gated behind
Mechanical Reaper and none of these runs grants it — so cash and technology show
up here as separate ceilings, and only one of them is lifted. See
[development.md](development.md).

**Those two ceilings are now connected**, which is what the Investment screen did to
this run: the gold that buys development is also the gold that buys the technology
raising the development's ceiling. The investing runs above are this run with both
lifted, and they show the two competing for the same treasury.

### The Engineer reaches, and nothing pushes back

Two runs of the same world, the last six columns of each row being grain on
railed ground with no depot near it — stranded until an Engineer builds one. The
only difference is whether the Engineer is given orders; each treasury covers
exactly two depots.

```
                     gathered  carried  wasted  grain/turn at 100  structures  treasuries
Engineer idle          15,512   15,484      28                 42           0      42,000
Engineer building      23,037   23,009      28                126          14      21,000
```

**The reach is large** — half again as much harvest over the century, and grain a
turn triples. Nothing else in the engine can do that; every other civilian raises
what a tile yields, and this raises how much of the map is a tile.

**And the waste figure does not move.** This run was written to confirm the
opposite: that gathering more without carrying more would push waste up until a
railyard caught up. It is 28 either way. The reason is the runaway already
reported above — **the railyard is unopposed**, so capacity outruns anything an
Engineer can reach. That expectation is retracted in
[engineer.md](engineer.md) and in [transport.md](transport.md) rather than
softened, and this table is the one to re-read once anything else competes for
lumber and steel.

## Open

- Soak an imported `s1` rather than a synthetic fixture. That needs a headless
  runner, since the simulation cannot currently be driven outside xUnit.
- Re-read the sickness result once trade exists.
- A longer run than 100 turns; the original goes to roughly 400.
