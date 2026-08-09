# Transport capacity

## Summary

Everything this project had built between the land and the warehouse was free.
`Extraction` gathered and `Delivery` committed the lot; the soak gathered 16,240
units over a hundred turns and every one arrived. The original does not work that
way, and this is the first constraint in the middle of the chain.

The manual is explicit: *"transport capacity is the total number of commodities
that your network can move each turn"*, raised at the railyard, and allocated by
the player on the Transport screen — a slider per commodity against one shared
bar, with demand lines warning when what you carry falls short of what your
workers and mills need.

## Confidence

`inferred`, with **one guess** clearly separated and **one chosen rule**.

| Claim | Support |
|---|---|
| Capacity is one shared number, and one point moves one unit | **manual**, stated outright |
| The player allocates it per commodity | **manual** — a slider each, one bar |
| It is raised at the railyard, for lumber and steel | **manual** |
| The railyard has no ceiling | **manual** — "you can build as much as you want" |
| The railyard costs labour, unlike expanding a mill | **manual** — "provided you have steel, lumber, and available labour" |
| Capacity built now works next turn | **manual**, by "as with other industrial expansion" |
| `tran` is `[country, capacity]` | **corpus-verified** across seven scenarios |
| A power starts with stockpiles of lumber and steel | **manual** — "you must construct a lumber and steel mill with your *initial stockpiles of lumber and steel*" |
| **What the network cannot carry is lost** | **a chosen rule** — see below |
| **What a skirmish's network starts at, and how big the stockpile is** | **guesses.** Nothing anywhere |

## Design

### Extraction fills a pool; Transport empties it

A new phase between `Extraction` and `Feeding`. It has to be after Extraction
because you can only carry what you gathered, and before Feeding because workers
eat transported food ahead of warehouse stock — which is what the manual's grain
demand line is for.

The pool is turn-local and lives nowhere: `ExtractionPlanner` already returns what
each country gathered, and `TurnResolver` hands it straight to `TransportPlanner`.
Nothing is stored on `WorldState`, because nothing survives the turn.

### Allocation is an order, trimmed twice

A `TransportAllocationOrder` is a ceiling, not a demand — orders are written
before the turn resolves and nobody can know exactly what the land will yield.
The planner trims each to what was gathered, then to the capacity left, walking
the country's sliders in the order given. That is the rule production and
facility capacity already use for contention, so there is no new tie-break.

**A commodity left off the orders moves nothing.** Capacity does not allocate
itself; every slider at zero is a network that carries nothing, which the soak
demonstrates at scale.

### What is left behind is lost

Un-carried output does not accumulate. The pool refills from the tiles next turn
and yesterday's unmoved grain is gone.

**This is a chosen rule, not a finding** — the same standing as "which grade
takes the damage" in [feeding.md](feeding.md). The manual's phrasing supports it
and never states it, and a stockpile-at-the-depot reading is not absurd. Losing
it is the reading that makes capacity matter at all. It is reported in
`CommoditiesTransportedEvent.Wasted` rather than dropped silently, which is also
what the original's Minister warning about "wasting transport capacity" implies
exists.

**Wasted is not stranded.** A cell can now fail to reach the warehouse two ways —
no route at all, or no room on the network — and they want different fixes: a
depot, or a railyard. `ResourceExtractedEvent.Stranded` keeps the first;
`CommoditiesTransportedEvent.Wasted` is the second.

### The railyard is an order, not a facility

Capacity is a country pool rather than a facility's capacity, so it needs no
`ProductionFacilityDefinition` and no ladder. `BuildTransportCapacity` names a
number of points, priced per point and preflighted in the same chain as
production, expansion and migration, so a turn cannot spend the same steel twice.

Capacity bought this turn carries **next** turn: `Transport` reads the figure the
turn opened with, snapshotted before any phase runs.

### The 2:1 prediction was wrong, and is retracted

`production.md` and `CLAUDE.md` both predicted the railyard would be the first
recipe to break 2:1 and separate the three readings of the manual's labour
sentence. **It does not.** A point of capacity costs one lumber and one steel —
two input units for one point — so "one labour per input unit" and "two labour
per unit of output" still give the same answer, exactly as for every shipped
recipe.

Worse for the prediction: the manual never prices the railyard's labour at all.
It names labour as a requirement and gives no quantity, so the number here comes
from the same total-input-units rule rather than from evidence — which means the
railyard could not have tested that rule even if its ratio had differed. The
first real test is still ahead, and nothing currently in view supplies it.

What the railyard *does* establish is a genuine difference from facility
expansion, which the manual prices at one lumber and one steel and for which it
names no labour at all.

## The one guess: what a network starts with

A skirmish carries no `tran` record, so the corpus attests only that the engine
supplies a value. The seven scenarios that do carry one are authored situations,
and this project has a standing rule against mining `capa`, `labo` or `tran` for
constants.

Their spread makes the point better than the rule does: `s1` gives its powers
80–170, `s3` gives 22–60, `s13` and `s14` give 10–25, and **`s12` gives a network
to exactly one of its seven powers**. There is no constant in there to find.

Zero was the alternative and it is worse — it makes every imported skirmish
unplayable, because nothing can leave the land. So a number is invented, lives in
content as `startingDefaults.transportCapacity` where changing it is an edit, and
is labelled here. **Do not cite it as evidence for anything.**

### The stockpile is a second guess, and the one that rescued the first

A power's opening warehouse is `startingDefaults.inventory`. **That there is one
at all is the manual's, and so are the two commodities** — "you must construct a
lumber and steel mill with your *initial stockpiles of lumber and steel*, or you
may be forced to beg for lumber and steel from other Great Powers", which a power
starting empty could not do. **How much is a guess.**

It is the more important of the two. An empty warehouse plus a small network is a
trap with no exit, because escaping needs a railyard, a railyard needs materials,
and carrying materials means not carrying food. The soak below found that trap,
concluded it was a property of the model, and was wrong: it is a property of
starting with nothing, which the original does not do.

**That is also why `ware` deserves promoting** in
[the seven engine defaults](_index.md#the-seven-engine-defaults). It used to be a
number nothing read. It now decides whether a start is viable.

### Allocation is re-chosen every turn

Worth stating because it is the whole point of a slider rather than a rule: a
country may put its entire network on coal one turn and entirely on iron the
next, or split it evenly both turns. Orders are per-turn submissions and nothing
carries over.

The soak's policies each hold one fixed ordering for a hundred turns, which is a
fixture simplification and not a property of the model — a real player would vary
it as demand moved, and the demand lines exist to tell them when.

## Where implemented

- `TransportSettings` (`ExtractionDefinitions.cs`) and
  `StartingDefaults.TransportCapacity`.
- `WorldState.GetTransportCapacity` / `SetTransportCapacity`, and
  `InitialTransportCapacity`.
- `TransportPlanner.Create` for allocation and `CreateRailyard` for the build.
- `TurnPhase.Transport`, `CommoditiesTransportedEvent`,
  `TransportCapacityBuiltEvent`.
- `TransportAllocationOrder` and `CountryTurnOrders.BuildTransportCapacity`.
- `.iworld` **v16**: `transport`, `startingDefaults.transportCapacity`,
  `scenarios[].transportCapacity`, with a v15→v16 migration to no limit at all.
- `LegacyWorldConverter.ReadTransportCapacity`; `tran` is no longer deferred.

## Test data

`tests/Imperialism.Core.Tests/TransportTests.cs` pins an allocation trimmed to
what the land yielded and trimmed again to capacity; sliders honoured in the
order given; ordering nothing moving nothing; what is left behind not keeping;
what is carried reaching the warehouse next turn; capacity bought this turn
carrying next turn; a partial railyard build; and a world with no settings
carrying everything.

`tests/Imperialism.LegacyImport.Tests/LegacyWorldConverterTests.cs` pins `tran`
conversion, the railyard's price, the default for a scenario carrying none, and
the per-scenario record counts across the corpus.

## What a hundred turns looks like

Runs on the farming world at ten points a power, against fourteen units gathered
and seven eaten. **With an empty warehouse**, the slider order decides everything:

```
                        workers  sick  carried/gathered  produced  capacity built
unlimited (before)           77     0     16,240/16,240     3,038              —
food first                   49     7      7,000/16,240         7               0
materials first              42     0     15,673/16,240     1,386             672
```

Food first fills the network with grain, fruit and livestock; coal is at the back
of the queue and almost never arrives, so the steel mill has nothing, so no
railyard is ever affordable, so the network never grows. A century of comfortable
stagnation. Materials first starves seven workers immediately and runs sick for
twenty turns, then the mills run and the railyard grows the network.

**Food first used to produce exactly nothing and now produces seven cycles**, an
artefact of the work duration moving from 1 to 3: slower improvement means less
grain per turn, which leaves a sliver of the network free for coal. Seven against
1,386 is the same conclusion arrived at less tidily. **The zero that matters is
still zero** — food first never affords a railyard, so its network never grows.

### And then the stockpile changed the answer

**Both of those readings are artefacts of an empty warehouse**, which is not how
a game starts. The manual says a power begins with stockpiles of lumber and steel.
Give the same world twenty of each:

```
                        workers  sick  carried/gathered  produced  capacity built
food first, stocked          49     0     16,212/16,240     1,372             805
materials first, stocked     42     0     16,212/16,240     1,386             812
```

They converge. With something to build from, either ordering buys an adequate
network within a few turns, after which nothing is scarce and the choice stops
mattering — and materials first is now strictly *worse*, because it still costs
seven workers on turn one and buys nothing the other ordering does not reach
anyway.

**The slider order is worth exactly as much as capacity is scarce**, and a
stockpile makes it scarce only briefly. That is a much smaller claim than this
document made before the stockpile existed, and it is the true one.

### What survived the correction, and what did not

**Did not survive:** *"a network below subsistence never recovers."* At four
points a power with an empty warehouse the country is stuck for the century — 28
workers, 14 permanently ill, nothing ever produced. With the stockpile it buys its
way out on turn one and ends carrying 16,142 of 16,240 with nobody ill and 798
points built. **The trap was the empty warehouse, not the small network**, and the
claim is retracted rather than quietly softened.

**Did survive:** a network under what its workforce eats **costs that workforce on
the first turn regardless**. Capacity bought on turn one does not carry until turn
two and the workers eat on turn one, so the opening headcount is set by the
network a scenario hands you and nothing can be done about it that turn. That is
why the guessed starting capacity is still worth getting right.

**Also worth naming:** 805 points of capacity over a century is absurd, and it is
the same runaway [soak.md](soak.md) already reports for mills. It is a property
of the fixture rather than of the model, and the distinction is worth drawing
carefully.

**That absurdity has since cost a prediction.** This document expected the
Engineer to oppose capacity — extend the network, gather more without carrying
more, and watch the waste figure rise until a railyard caught up. It does not:
half again as much harvest and the waste figure does not move at all, because an
unopposed railyard outruns anything an Engineer can reach. The tension is real in
the original, where ships and trade and hardware want the same two commodities;
it is not real here. Retracted rather than softened, in
[engineer.md](engineer.md).

The competing claims on lumber and steel:

- **Already in the model, and simply not ordered by these policies:** furniture
  from lumber, which the Capitol needs to recruit anybody; hardware and armaments
  from steel.
- **Genuinely absent:** ships, which the manual has consuming lumber early and
  steel later; trade, which turns hardware into money; and the upgrades the
  original charges against every producing building. The Engineer is **not** on
  this list: it spends cash rather than materials, which is exactly why it fails
  to oppose the railyard.

So the railyard is not cheap — it is unopposed. Do not price it against these
numbers, and re-read them once anything above competes for the same two
commodities.

## Open questions

- **What a network really starts with.** The guess above. The likeliest source is
  the binary, since the corpus cannot answer and the manual does not.
- **Whether unmoved output really evaporates.** The chosen rule.
- **Moving regiments**, which the manual prices at five capacity per armaments
  point and which needs military units to exist.
- ~~**Merchant marine capacity**, which is trade's separate pool.~~ **Built**, and it is
  genuinely separate: derived from the cargo of a country's ships, spent in the world's
  commodity order, and refilled each turn. **Unlike the pool on this page it binds** — the
  soak leaves 103,147 units offered and unsold — so the two are worth comparing rather than
  conflating. See [trade.md](trade.md).
- **Demand lines** — the red and green marks under each slider. Presentation over
  numbers this phase now exposes, and a good early test of the event log as a
  presentation contract.
- **Whether the original's pool is per commodity or one shared total.** The
  manual's single bar says shared, and that is what is built, but a per-commodity
  cap would look identical in every screenshot.
- **Town production**, which the manual says enters the transport network rather
  than the warehouse directly, and so interacts with capacity in a way nothing
  here models.
