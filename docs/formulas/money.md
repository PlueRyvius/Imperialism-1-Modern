# Money

## Summary

Everything this project had built was barter. A country held commodities and
labour and nothing else, so the manual's Investment screen, its University, its
civilian recruitment and everything an Engineer builds were all unreachable for
the same reason: **there was no treasury.**

This adds one, and the income the manual pairs with it. Gold and gems never
reach the industry warehouse — "all gems and gold transported convert
immediately into cash" — so the network that carries them is also what pays for
extending it. That makes a loop rather than a one-way drain: mines pay for the
network, the network reaches more mines.

## Confidence

`inferred`, with **the two conversion rates transcribed outright** and **one
guess** clearly separated.

| Claim | Support |
|---|---|
| A Great Power has a treasury and starts with something in it | **manual** — "each Great Power begins the game with a limited amount of cash which is totally inadequate to meet its needs" |
| Gold and gems never reach the warehouse and cannot be traded | **manual**, stated outright |
| They convert **as they are transported**, not as they are mined | **manual** — "all gems and gold *transported* convert immediately into cash" |
| **Gold is $200 a unit** | **manual**, stated outright |
| **Gems are $500 a unit** | **manual**, stated outright |
| `cash` is `[country, amount]` | **corpus-measured** across five scenarios |
| Gold and gems still cost transport capacity | **inference** — see below |
| **What a skirmish's treasury starts at** | **a guess.** Nothing anywhere |

The two rates are worth dwelling on. Almost every number in this scoreboard is
either a guess or a shape read off the manual's prose; these are printed figures
in the manual's own Other Resources section, in the same register as the Resource
Development Table. They are the strongest gameplay numbers recovered since that
table.

## Design

### A treasury is a `long` per country

`WorldState.GetCash` / `SetCash` / `AddCash` / `TrySpendCash`, alongside the
transport-capacity pool it most resembles: a country-wide number rather than
anything a cell or a facility owns.

`TrySpendCash` is **all or nothing**, the same shape `TryConsumeAvailable`
already uses for the warehouse. A structure half paid for is not a structure.

### Conversion happens where the goods are carried

The manual attaches the payment to the transporting, so the rate lives on
`CommodityDefinition.CashPerUnit` and the conversion happens in
`TransportPlanner`. Null is the ordinary case — the commodity reaches the
warehouse — and the whole rule is two entries in imported content.

**Gold and gems still spend capacity.** The manual never says so, and it is the
reading that makes them a decision: a point of network spent on gold is a point
not spent on grain. The alternative — free carriage for the two commodities that
pay — would make a gold mine strictly better than any other tile and delete the
choice. Recorded as an inference rather than a finding.

They are reported separately from what reached the warehouse:
`CommoditiesTransportedEvent.Converted` and `.CashEarned`, disjoint from
`.Moved` and counted in the same `CapacityUsed`.

**Unmoved gold pays nothing**, which follows from the chosen rule in
[transport.md](transport.md) rather than being a new one: what the network
leaves behind does not keep, and gold left on the ground is gold that was never
transported.

## The one guess: what a treasury starts with

A skirmish carries no `cash` record, so the corpus attests only that the engine
supplies a value — exactly the position `tran` and `ware` are in.

The five scenarios that do carry one show why it cannot be mined:

| | Authored treasuries |
|---|---|
| `s1` | 5000, 5000, 2500, 2500, 6000, 2500, 10000 |
| `s3` | 10000, 6000, 1500, 2000, 4000, 3000, 15000 |
| `s5` (generated) | 5000, 6000, 6000, 6000, 5000, 6000, 5000 |
| `s13`, `s14` | 5000, 5000, 1500, 1500, 4000, 1500, 10000 |
| `s9`, `s10`, `s11`, `s12`, `s15` | none |

`s3` spans a factor of ten across its own seven powers. That is a designer
setting up a mission, and this project has a standing rule against reading it as
a rule.

So a number is invented — **5,000**, in
`startingDefaults.cash` where changing it is an edit — chosen to sit inside that
spread rather than derived from it, and sized so that a power can afford a couple
of structures and not a network. **Do not cite it as evidence for anything.**

**It is now load-bearing twice over.** It was already what decides whether an
Engineer can build a first depot; with improvement priced it also decides how far
a fresh start can develop before it has to earn something. 5,000 buys exactly
five Level II improvements, which the soak spends inside thirty-five turns. A
number labelled "do not cite" when nothing read it now sets the pace of the whole
early game.

Zero was the alternative and is worse in the familiar way: with no treasury, an
Engineer can never build the first depot, so no new ground is ever reached, and
the slice that motivated the treasury does nothing.

**`cash` is promoted in [the seven engine defaults](_index.md#the-seven-engine-defaults)**
the way `ware` was, and for the same reason: it used to be a number nothing read,
and it now decides whether an imported skirmish can build anything at all.

## What competes for the treasury, and in what order

Cash moves in and out at four points now, and they are settled by **where they sit in the
turn** rather than by any pooling or preflight:

| Charged in | What | Notes |
|---|---|---|
| **`Trade`** | **selling pays, buying costs** | **second in the turn, so the money is there to spend** |
| `Development` | an improvement, per rung | 100 / 1,000 / 3,000 |
| `Development` | an Engineer's rail, depot or port | rail per terrain |
| `Migration` | a recruit at the Capitol | commodities, not cash |
| **`Investment`** | **technology** | **last, so it takes the remainder** |

**Trade sits first because it is income**, and being before `Development` is what lets a
sale pay for an improvement in the same turn. That is not a chosen ordering — the phase was
already second in the manual's fixed pipeline.

**Research running last is a chosen rule**, not a finding. Construction and
improvement are charged during `Development` and get first call; technology spends
what survives. There is no preflight and no reservation: within a phase, orders are
read in sequence and the first one the treasury cannot cover is refused outright
rather than part-funded.

**This is not a formality, and the soak measures it.** A power that improves
whenever it can never accumulates the twelve thousand a Mechanical Reaper costs, so
its ceiling never lifts at all — it ends the century a thousand short. A power that
stops its Farmers to bank for the purchase gets it the quarter it becomes
available. The rule forces a real trade: improve now, or improve higher later. See
[technology.md](technology.md#what-a-hundred-turns-looks-like).

**Trade changes the size of that problem rather than its shape.** With a market the same
greedy power buys the Plows on turn one and the Reaper the quarter it arrives, because
income has stopped being the binding constraint. The rule still decides who gets paid
first; it stops mattering when there is enough. **Keep the rule and stop citing the knife
edge** — that was measured on an economy with no revenue.

**It also demotes the guessed starting treasury, which is the first time a guess here has
become *less* load-bearing.** While gold was the only income that figure decided three
things — the first depot, five Level II improvements, and whether a late technology was
reachable inside a century at all. A century of selling earns fifty times it, so what a
power *starts* with now decides only its opening few turns.

## What the manual says that is *not* modelled

Recorded so the next slice does not have to rediscover it.

- ~~**Trade is the real income.** "Every time you sell commodities to other
  countries you receive a cash payment for the sale", and it is the first entry
  on the manual's own list of three. Nothing here models it, so the only income
  in this engine is the smallest of the three.~~ **Implemented, and the "real"
  turned out to be an understatement.** In the soak, selling the surplus earns
  **1.2 million** over a century against the gold mine's **20,000** — two orders of
  magnitude, and it discharges the caveat this document and three others were
  carrying. See [trade.md](trade.md).
- **Overseas profits** are the third, and need embassies and Minor Nations.
- ~~**Technology is bought with cash** on the Investment screen. A treasury makes
  that newly possible and it still wants the prerequisite graph and arrival
  dates.~~ **Implemented.** Prices, prerequisites and arrival dates all come from
  the price list, and `TurnPhase.Investment` spends the treasury on them. See
  [technology.md](technology.md), and the section below on what that does to
  contention.
- ~~**Civilian units cost cash, and so does their work** — recorded and
  deliberately not implemented.~~ **The work half is implemented now.** The
  owner supplies the prices — 100, 1,000 and 3,000 for the three rungs — and
  every civilian's improvement is charged for while prospecting stays free. See
  [development.md](development.md). **Building the unit itself is still not
  modelled**: that wants the University, and experts and paper as well as cash.
- **The University and recruitment** price experts and civilians in cash and
  paper.

## Where implemented

- `CommodityDefinition.CashPerUnit`.
- `WorldState.GetCash` / `SetCash` / `AddCash` / `TrySpendCash`, and
  `InitialCash`.
- `StartingDefaults.Cash`, applied to `DefaultStartCountries` alongside the
  workforce, capacity, technology and stockpile defaults.
- `TransportPlanner.Create`, which credits rather than queues.
- `CommoditiesTransportedEvent.Converted` / `.CashEarned`.
- `.iworld` **v17**: `commodities[].cashPerUnit`, `startingDefaults.cash`,
  `scenarios[].cash`, with a v16→v17 migration to a world with no money at all.
- `LegacyWorldConverter.ReadCountryCash`, `CashPerUnit` and
  `DefaultStartingCash`. `cash` is no longer deferred.

## Test data

`tests/Imperialism.Core.Tests/TreasuryTests.cs` pins the authored treasury, the
fair-start default and an explicit record beating it, a country outside the fair
start getting nothing, gold and gems paying cash rather than filling the
warehouse, each paying its own rate, conversion still spending the network, gold
left on the ground paying nothing, cash accumulating across turns, and spending
being all or nothing.

`tests/Imperialism.Content.Tests/WorldContentTests.cs` pins the v16→v17
migration, its rejection of a contradictory version 16 package, a round trip, and
a commodity priced at zero being rejected.

`tests/Imperialism.LegacyImport.Tests/LegacyWorldConverterTests.cs` pins `cash`
conversion, the default for a scenario carrying none, gold and gems being the
only two priced, and the per-scenario record counts across the corpus.

## Open questions

- **What a treasury really starts with.** The guess above. The likeliest source
  is the binary, since the corpus cannot answer and the manual does not.
- **Whether gold and gems really cost capacity.** The inference above.
- **Whether conversion is affected by anything.** The manual gives two flat
  rates and no modifier — no market, no diplomacy, no era. Trade, when it lands,
  is where a clearing price would live; these two are explicitly *not* traded.
- **Overseas profits**, which the manual says use gold and gems in an
  unconquered Minor Nation *without* the transport network.
- **Credit and interest**, which are a country's other relationship with money
  and have their own scoreboard row.
