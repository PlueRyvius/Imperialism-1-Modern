# Transport capacity

## Summary

Everything this project had built between the land and the warehouse was free.
`Extraction` gathered and `Delivery` committed the lot; the soak gathered 16,548
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
| **What the network cannot carry is lost** | **a chosen rule** — see below |
| **What a skirmish starts with** | **a guess.** Nothing anywhere |

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

The soak below shows why this one matters more than most guesses: it is not a
balance knob but a viability threshold.

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

Three runs on the farming world, all at ten points a power against fourteen units
gathered and seven eaten.

```
                        workers  sick  carried/gathered  produced  capacity built
unlimited (before)           84     7     16,548/16,548     3,101              —
food first                   49     7      7,000/16,548         0               0
materials first              42     0     15,673/16,548     1,386             672
```

**Food first feeds everyone and never makes a thing.** Grain, fruit and livestock
fill the network; coal is at the back of the queue and never arrives, so the steel
mill has nothing, so no railyard is ever affordable, so the network never grows.
A hundred turns of comfortable stagnation.

**Materials first starves seven workers in the first turn** and spends the next
twenty-odd sick — then the mills run, the railyard grows the network, and by turn
25 it is carrying almost everything it gathers with nobody ill at all. It ends
with 672 points of capacity built and a workforce that is smaller but fed.

That trade-off is the phase working. Neither ordering is asserted to be correct;
what is asserted is that they differ, which is what proves the slider order is
load-bearing rather than decorative.

### A network below subsistence never recovers

At four points a power — under the seven a workforce eats — the country falls to
the headcount its network can feed and stays there for the century, even carrying
food first. Escaping needs a railyard, which needs lumber and steel, which need
timber and coal carried, and every unit carried is one not carrying food.

**That is a property of the model, not of this fixture**, and it is why the
guessed starting capacity is a viability threshold rather than a balance knob.
Anyone tuning it should know that below a certain point there is no game.

## Open questions

- **What a network really starts with.** The guess above. The likeliest source is
  the binary, since the corpus cannot answer and the manual does not.
- **Whether unmoved output really evaporates.** The chosen rule.
- **Moving regiments**, which the manual prices at five capacity per armaments
  point and which needs military units to exist.
- **Merchant marine capacity**, which is trade's separate pool.
- **Demand lines** — the red and green marks under each slider. Presentation over
  numbers this phase now exposes, and a good early test of the event log as a
  presentation contract.
- **Whether the original's pool is per commodity or one shared total.** The
  manual's single bar says shared, and that is what is built, but a per-commodity
  cap would look identical in every screenshot.
- **Town production**, which the manual says enters the transport network rather
  than the warehouse directly, and so interacts with capacity in a way nothing
  here models.
