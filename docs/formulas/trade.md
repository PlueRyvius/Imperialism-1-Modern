# Trade, and the ships that carry it

## Summary

**Trade is the manual's first income source and was the last one unmodelled.** Until
version 20 `TurnPhase.Trade` was an empty placeholder, `PendingDeliverySource.Trade` a
reserved enum slot with nothing behind it, and the only money in the engine came from
gold and gems — which the manual calls the smallest of three.

That absence distorted everything measured before it. Four documents carried the same
caveat in different words — *"an artefact of missing trade, not a property of the
model"* — and [technology.md](technology.md) added a fifth: a power that improves as it
earns cannot afford a $12,000 Mechanical Reaper in a century. Every one of those was
measured on an economy with no revenue.

**With a market it buys the Reaper on turn 45 and lifts its grain ceiling by turn 49.**
See [what a hundred turns looks like](#what-a-hundred-turns-looks-like).

## Confidence

`inferred`, and unusually well supported for the mechanism, badly supported for two
numbers. The split matters more than the average.

| Claim | Support |
|---|---|
| Offers meet bids at one world price nobody names | **manual** |
| Bought goods arrive next turn; sold goods leave now | **manual**, both stated |
| Industry claims its inputs before the market sees them | **manual**, stated |
| Price rises on demand, falls on supply, holds when matched | **manual**, stated — the *direction* |
| An offer passes down a ranked bidder list, part at a time | **manual**, with a worked example |
| One cargo hold per unit, usable once a turn | **manual**, stated |
| No merchant marine means no trade at all | **manual**, stated outright |
| Holds spend in a fixed commodity order, clothing first | **manual**, stated |
| Between Great Powers the buyer carries | **manual**, stated |
| **The fifteen prices** | **observed** — the original's own Bid and Offers screen |
| **Which commodities are tradable** | **observed**, and it agrees with the manual three times independently |
| **The commodity order** | **observed**, from the same screen |
| **The ship table** — order, cargo, sea zones, build bills, combat | **the executable's own naval and cost tables** |
| The opening fleet | **corpus** — all three skirmishes agree; the class is the executable's |
| A `ship` type index is 1-based | **corpus-corroborated** — see below |
| **How far a price moves** | **a guess**, quarantined behind `ITradeMarket` |
| **Which bidder gets first refusal** | **a placeholder** — the real rule needs diplomacy |
| A minor nation's holds | **inference** — the manual says they own none |

**The manual prices nothing.** Its only figures anywhere are gold $200, gems $500,
consulate $500, embassy $5,000 and grants $1,000/$10,000. Every price here comes from
the screen instead.

## The roster

Fifteen commodities, in the original's own commodity order, at its own prices.

**The order is a rule, not a listing.** It decides which deals get cargo holds:
"IMPERIALISM always uses an established order when expending the Great Powers' merchant
marine for trade… Clothing deals, for example, are always considered prior to all other
deals because clothing is the first item in commodity order. **Reserving some cargo holds
for later deals becomes an important skill.**" That last sentence is why the order is
stored explicitly rather than taken from the position of a commodity in the world's list.

| # | Commodity | Category | Price |
|---|---|---|---|
| 1 | Clothing | Goods | 900 |
| 2 | Furniture | Goods | 900 |
| 3 | Hardware | Goods | 900 |
| 4 | Arms | Goods | 900 |
| 5 | Food (canned) | Material | **100** † |
| 6 | Fabric | Material | 300 |
| 7 | Lumber | Material | 300 |
| 8 | Paper | Material | 300 |
| 9 | Steel | Material | 300 |
| 10 | Cotton | Raw | 100 |
| 11 | Wool | Raw | 100 |
| 12 | Timber | Raw | 100 |
| 13 | Coal | Raw | 100 |
| 14 | Iron | Raw | 100 |
| 15 | Horses | Raw | **300** † |

### Three tiers, and the ladder is structural

**100 raw, 300 material, 900 goods.** Every recipe consumes two input units per unit of
output, so two raw at 100 becoming one material at 300 is 2× inputs plus 50% value
added, and two materials at 300 becoming one good at 900 is the same again. That makes
this **three observed numbers plus a rule the recipe data already implies**, rather than
fifteen independent figures — the same move that settled the labour cost, arrived at from
the other direction.

### Two exceptions, one of which explains itself

- **† Food at 100, a material priced as raw.** Not a violation: canned food is made from
  grain, and **grain is untradable and so has no world price at all**, leaving the ladder
  nothing to mark up. A commodity whose input is off the market is exactly where the rule
  should stop applying.
- **† Horses at 300, a raw priced as a material.** A genuine exception. Nothing produces
  horses, so nothing derives it, and they are a military input rather than an industrial
  one. **Transcribed, not derived — do not fit a rule to it.**

### What is missing from the roster is as informative as what is on it

Eight commodities have no price, and they are exactly the ones the manual says cannot be
traded. The roster is a screenshot and the manual is prose, so **two independent sources
agree three times over**:

| Absent | The manual |
|---|---|
| grain, fruit, livestock, fish | "food resources cannot be traded on the world market" |
| gold, gems | "they never reach the industry warehouse and they cannot be traded" |
| *canned food is present* | "you may trade for canned food on the world market" |

That is much better support than tradability would otherwise have, and it is why
`IsTradable` is transcribed rather than inferred. **Absence of a price is what makes a
commodity untradable** — the same shape `TechnologyDefinition.Cost` uses for "not for
sale".

**Oil and Fuel are on neither list, and that is an open question.** The manual says each
row is one commodity and the screen shows a complete fifteen, which reads as *not traded*.
The alternative is that the screenshot predates Oil Drilling. The data is taken as it
stands; if play says otherwise it is a one-line content edit, and
[technology.md](technology.md) has just made Oil Drilling purchasable so it is now
answerable.

## Ships

**Thirteen classes: five merchants and eight warships**, and the whole table is now the
executable's. It holds fourteen 36-byte rows at `0x00698108` — an unused row 0 and the
thirteen classes — plus six 30-entry commodity arrays at `0x00695B50` for the build
bills. Cargo is still the only column this engine reads.

**The order below is the game's own**, which is what a legacy `ship` record's 1-based type
indexes into, and it was the blocking unknown on
[`../disasm/wanted-values.md`](../disasm/wanted-values.md).

| # | Class | Cargo | Sea zones | Needs | Build bill |
|---|---|---|---|---|---|
| 1 | Trader | 2 | 1 | — | 4 lumber, 2 fabric |
| 2 | Indiaman | 4 | 1 | — | 7 lumber, 3 fabric |
| 3 | Frigate | — | 3 | — | 5 lumber, 2 fabric, 2 arms |
| 4 | Ship-of-the-Line | — | 2 | — | 8 lumber, 3 fabric, 5 arms |
| 5 | Paddlewheeler | 8 | 1 | Paddlewheels | 6 lumber, 2 steel, 10 coal |
| 6 | Clipper | 4 | 1 | Streamlined Hulls | 6 lumber, 2 fabric |
| 7 | Raider | — | 5 | Paddlewheels | 6 lumber, 3 arms, 10 coal |
| 8 | Ironclad | — | 3 | — | 4 lumber, 6 arms, 4 steel, 10 coal |
| 9 | Advanced Ironclad | — | 4 | — | 8 lumber, 15 arms, 10 steel, 20 coal |
| 10 | Freighter | **16** | 1 | — | 8 steel, 20 coal |
| 11 | Armoured Cruiser | — | 6 | — | 2 lumber, 8 arms, 6 steel, 20 coal |
| 12 | Dreadnought | — | 5 | — | 24 arms, 30 steel, 20 fuel |
| 13 | Battle Cruiser | — | 6 | — | 18 arms, 22 steel, 20 fuel |

**The Freighter carries 16**, which was the last unknown cargo figure. At zero it was a
hull nobody would build; at 16 it is worth four Traders, and it is what an industrial
merchant marine is actually made of.

**Two technology entries that gate nothing else in the engine gate a hull here.**
Streamlined Hulls and Paddlewheels were dead weight in the technology table; the Clipper
and the Paddlewheeler give them work. The executable gates no other hull, which is worth
noting against the descriptions: Advanced Iron Working "permits construction of
Ironclads" and Marine Engineering "permits construction of Armoured Cruisers", so those
gates exist somewhere and are **not** in the naval table's own fields.

### The manual's "Speed" column is sea zones

This is the correction that mattered most. The naval table has **two** movement fields:
field 7, whose eight warship values are exactly the manual's printed Speed column, and
field 4, which is the battle movement the manual prints separately. So the manual's
"Speed" is a world-map allowance in sea zones per turn, and **there is no sailing speed
anywhere in the record.**

That retracts a claim this document and the code both carried: that a merchant's armour
and speed decide whether it runs a blockade. Every merchant has sea zones 1 and battle
speed 0, so neither field distinguishes them. What merchants *do* have is armour and a
hull scale — the Freighter has 25 armour, more than a Frigate — which is presumably what
blockade will read.

It also settles the oddity the owner had flagged with `(??!)`: the Clipper's speed of 0.
Not an error. Merchants have no battle movement because they never fight.

### The build bills are recovered, and the refusal to guess them paid off

This document used to say no build costs were transcribed, because the owner's cost table
had a misaligned column and every value after Hull in it was suspect — arms included, and
arms later sets "the force size that can be landed at a beachhead on hostile soil in one
turn". **The misalignment was real and the caution was right.**

The recovered bills settle the one discrepancy that was called out by name: **the Frigate
takes 2 arms**, not 3. Ship-of-the-Line's 5 was never in doubt. The merchant recipes the
old document omitted entirely are here too, and **not one of the thirteen costs cash** —
which independently confirms the owner's "at no monetary cost but with varying amounts of
resources and/or materials".

### The combat numbers

Every hull has them, merchants included; the manual's table printed warships only.

| Class | Firepower | Range | Armour | Hull scale | Battle speed | Manual's H |
|---|---|---|---|---|---|---|
| Trader | 0 | 0 | 0 | 600 | 0 | — |
| Indiaman | 0 | 0 | 5 | 1,000 | 0 | — |
| Frigate | 3 | 5 | 10 | 900 | 4 | 35 |
| Ship-of-the-Line | 6 | 6 | 20 | 1,700 | 3 | 65 |
| Paddlewheeler | 0 | 0 | 5 | 900 | 0 | — |
| Clipper | 0 | 0 | 0 | 600 | 0 | — |
| Raider | 3 | 7 | 20 | 700 | 7 | 30 |
| Ironclad | 5 | 8 | 55 | 1,200 | 5 | 50 |
| Advanced Ironclad | 10 | 10 | 60 | 1,800 | 6 | 70 |
| Freighter | 0 | 0 | 25 | 1,200 | 0 | — |
| Armoured Cruiser | 6 | 9 | 50 | 1,000 | 8 | 40 |
| Dreadnought | 20 | 13 | 70 | 2,800 | 7 | 115 |
| Battle Cruiser | 18 | 13 | 55 | 2,200 | 9 | 90 |

Two things about the storage. **Armour is stored as its complement** — the accessor
returns `100 - stored` — which is why the unused row 0 reads as 100 armour, and why a
Trader's 0 is a real zero rather than a missing value. And **hull scale is not the
manual's H.** It is the divisor the battle report normalises damage by; the ratio between
the two runs from 23.3 to 26.2 across the eight warships, so no single scale converts one
into the other. Both are kept.

Firepower, range, armour and the manual's Speed column all match the manual exactly, which
is a clean cross-check on the transcription in both directions.

## Merchant marine

**A per-turn pool, derived from the fleet rather than stored**, so it cannot drift.
"The merchant marine number represents the total cargo holds available in all the merchant
ships owned by your Great Power. Each cargo hold can carry one unit of any trading
commodity." And **each hold is usable once per turn**, which makes it the
transport-capacity pattern again: a shared pool, spent in a fixed order, refilled next
turn.

Who pays is asymmetric and stated:

| Deal | Holds spent |
|---|---|
| Great Power buys from Great Power | the **buyer's** — "the buyer always picks up the commodities" |
| Great Power buys from a minor nation | the buyer's |
| Great Power sells to a minor nation | the **seller's** |

Minor nations own none, so **a Great Power dealing with one always carries**. Two minor
nations trading spend nothing, which is not a rule the manual gives — inventing one for a
case it does not describe would be worse than letting it be free.

**A hold shortage is reported against whoever ran out of hulls**, which is not always the
bidder. Selling to a minor nation spends the seller's holds, so it is the seller who
cannot move the cargo and the seller who needs telling. Getting that wrong was a real bug
in the first draft, and the soak caught it by reporting zero hold refusals in a run where
the pool was visibly binding.

### The opening fleet is not another engine default

**All three skirmishes give every one of their seven powers three ships of type 1** —
`s10`, `s11` and `s15`, independently. That is the same agreement that settled the fair
start's mills and workforce, and it matters because
[`_index.md`](_index.md#the-seven-engine-defaults) lists seven records a skirmish omits:
`ware`, `cash`, `deve`, `tech`, `tran`, `rail`, `rela`. **`ship` is not among them.** So
the opening merchant marine is recoverable from the corpus where the transport pool beside
it is not.

**The one inference was which class, and it is no longer an inference.** The corpus says
"three of type 1"; the executable's array puts the Trader at index 1. **Three Traders —
six holds**, confirmed rather than assumed.

## Reading the `ship` records

`ship` is `[country, type, zone, count]`, already documented in
[`../scenario-semantics.md`](../scenario-semantics.md). The corpus carries **142 records
and 307 ships**, and the importer deferred all of them until the array order was
recovered.

### The type index is 1-based, and the corpus proves it

Read as 0-based it puts a Clipper — which needs Streamlined Hulls — in an 1816 skirmish
whose powers hold no technology at all, and five more in `s13` and `s14`:

| Indexing | Technology order | Records granting a hull its owner could not build |
|---|---|---|
| **1-based** | price list | **0** |
| **1-based** | manual's printed | **0** |
| 0-based | price list | 9 |
| 0-based | manual's printed | 1 |

So under 1-based, **every ship in the corpus is one its owner could have built** — 142
records, zero contradictions, under either technology ordering. Same falsification method
that validated the `tech` ids.

**Two limits on what that proves.** It pins *which offsets are gated* — types 1–4 all
appear under six or fewer technologies, so none of them can be a Clipper, Steamship or
Raider — and it says nothing about the order *within* each group. Any permutation inside
{1,2,3,4} or inside the gated hulls fits equally.

### It does not settle the technology table order

This was expected to. Clipper needs Streamlined Hulls, and Streamlined Hulls is one of the
six positions [technology.md](technology.md#the-reorder-and-why-the-corpus-cannot-decide-it)
moved — so a Clipper held by a power with 4, 5 or 6 technologies would have discriminated
the two orderings that the authored levels, the rail ends and the arrival dates all
provably could not.

**No such record exists.** `s13` and `s14` are the only six-technology scenarios and they
field types 1–4 exclusively, all ungated. That is the **third** independent corpus check to
come back silent on the ordering, and it stays on source quality.

### So `ship` records are still deferred

The importer does not convert them. With thirteen classes and an unresolved array order,
converting against a guess would hand powers the wrong hulls — and unlike a wrong price,
that is not a number to recalibrate later but a fleet that was never there.

**Trade still works on imported worlds**, because the fair-start fleet comes from
`startingDefaults` and needs no index at all. That separation is the point: content refers
to a hull by key, and only the legacy integer needs the order.

### The stats are not in the file, and that is a Ghidra job

Four pattern searches for a static ship-stat table came back empty: the executable at
1-, 2- and 4-byte widths; strided struct arrays at every stride to 64; order-independent
windows (which handles a different ship order); and clustered `mov` immediates across the
59 MB disassembly listing, where **not one window of sixty held even six of the eight hull
values**. `confenu.irg` and `tabsenu.gob` have nothing either — and the `.gob` files turn
out to be PE resource containers rather than archives, uncompressed and searchable.

So there is no array to find: the values are **assigned individually in code**, which is
how 1997 C++ builds unit-type objects and why no search clusters them. Finding the ship
constructor and reading its immediates is a decompiler task, and a far better target than
the failed labour-cost hunt — a constructor's immediates sit in the decompiled output where
a formula was spread across arithmetic.

## Design

### The phase stays second, and industry goes first

`TurnPhase.Trade` sits where it always did, before `Production`. Selling is checked against
stock minus what the turn will consume, because the screen shows the warehouse "after
deduction of the commodities you have ordered for production on the Industry screen" and
"you cannot sell items you do not own or that you have ordered industry to use this turn."

`ProductionPlanner.Create` already runs before the phase loop, so trade is planned in the
same pre-loop block against a running total and committed in its own phase — the pattern
expansion, the railyard and migration already use, and `PreflightInventoryChanges` already
covers the combination.

**The claim figure counts consumption and deliberately not production.** Every plan's delta
is `outputs − consumption` and only production has outputs, so consumption is
`outputs − delta`, commodity by commodity. Netting instead would let a country sell output
that does not reach the warehouse until `Production` commits, which is *after* Trade. Doing
it the long way is what keeps a commodity both consumed and produced honest: two lumber in
and three out nets +1, and two is still claimed.

### Buying now, using next turn, needed no new machinery

Purchases become `PendingDeliverySource.Trade` deliveries and land through `Delivery`,
which is exactly how a harvest works. So "the commodities you buy appear for your use in
the Industry screen next turn" falls out for free.

**The sharp test of "not this turn" is not where the stock sits at the end of the turn** —
`Delivery` runs long after `Trade`, so it is in the warehouse by then, same as a harvest.
It is whether anything during the turn could *use* it, and nothing can: both the market and
industry read stock before deliveries land. A country that bids for coal and offers coal in
the same turn gets the purchase and a `NothingToSell` on the offer.

### The price is world state and it remembers

One price per commodity on `WorldState`, seeded from content and carried across turns,
because the figure on the screen is "the world market prices for the commodities traded
during the previous turn". So the market has a memory and a country can wait for a better
one.

**The price answers to what was offered and bid, not to what settled.** A bid nobody could
fill is still demand, which is what makes a shortage dear; settled volume would make an
unaffordable market look balanced.

**A market nobody came to keeps its price**, which is not the same as being closely
matched. Silence carries no information and drifting on it would invent a trend.

### `ITradeMarket` exists to quarantine one number

The direction is the manual's. The **magnitude is a guess**, and the clearing price has been
the oldest unknown on [`_index.md`](_index.md) since the scoreboard was written — it is
genuinely emergent, since this turn's price shapes what countries offer, which sets this
turn's price.

`ProportionalTradeMarket` ships a deliberately plain rule: a fixed percentage step outside
a dead band, bounded as a multiple of the opening price. **Every number in it is a guess**
chosen to behave over a century, not to match the original. The floor is a *modelling*
safeguard rather than a rule about 1897: at zero a commodity would be bought and sold for
nothing and every downstream figure would divide by it.

A world may have prices and no market, and then it trades at the opening price forever.
That separation is deliberate — the prices are transcribed and the curve is not, so they
should not fall together.

### The bidder ranking is a placeholder and is labelled one

The manual ranks bidders by the seller's favoured-trading-partner list, which combines
**diplomatic relations with trade subsidies** — and this engine has neither. Bidders are
taken in country-id order: deterministic, arbitrary, and the thing in `TradePlanner` most
in need of replacing. **Read nothing into which country gets first refusal.**

## Pseudocode

```text
Trade, second in the turn, planned against what industry has claimed:

    for each commodity, in the world's commodity order:
        offered[c] <- each country's offer, trimmed to stock minus claims
        bid[c]     <- each country's bid, untrimmed

        for each seller, in order:
            for each bidder, in order, while the offer lasts:
                payer <- buyer if the buyer is a Great Power, else seller
                take  <- min(offer left, bid left, what cash buys, payer's holds)
                if take is nothing:
                    refuse: no merchant capacity if the payer has no hold
                            not enough cash otherwise
                    continue
                move the cash, take the stock, queue the delivery for next turn
            if any offer is left: refuse no buyer

        price[c] <- market.NextPrice(price[c], offered[c], bid[c])
```

## Where implemented

- `CommodityDefinition.WorldPrice` / `TradeOrder` / `IsTradable`.
- `WorldState.GetWorldPrice` / `SetWorldPrice`, and `GetMerchantMarine`, derived.
- `ShipTypeDefinition`, `ShipCombatStats`, `InitialShip`, `ShipDefault`.
- `CountryDefinition.IsGreatPower`.
- `ITradeMarket` and `ProportionalTradeMarket`.
- `TradePlanner`, on the shape of `TransportPlanner`.
- `CountryTurnOrders.TradeOffers` / `TradeBids`, and `TradeOrder`.
- `TurnPhase.Trade`'s branch in `TurnResolver`, and the `claimed` figure it builds.
- `TradeRefusal`, `CommodityTradedEvent`, `TradeUnfilledEvent`, `WorldPriceChangedEvent`.
- `.iworld` **v20**: `commodities[].worldPrice` / `tradeOrder`, `shipTypes[]`, `trade`,
  `countries[].isGreatPower`, `startingDefaults.ships`, `scenarios[].ships`, with a
  v19→v20 migration to a world that trades nothing. A hull carries `seaZones`, a
  `buildCost`, and `combat` with `hullScale`, `battleSpeed` and an optional `hull`.
- `LegacyWorldConverter.TradeRoster`, `ShipTypes`, `StartingFleet`, and Great Power status
  from `labo`. `LegacyWorldConverter.ReadShips` converts `ship` records.

## Test data

`tests/Imperialism.Core.Tests/TradeTests.cs` pins the mechanism: settling at the world
price; a purchase unusable until next turn; an untradable commodity ignored entirely;
selling more than stock; industry claiming its inputs first; this turn's output not being
sellable; an offer passing down the bidder list; an unsold offer staying in the warehouse;
a buyer taking only what it can afford; a bidder with no holds skipped and the offer
passing on; the buyer carrying between Great Powers and the Great Power carrying against a
minor; holds spent in commodity order and exhausted mid-turn; the pool refilling; the price
rising, falling and holding; an empty market not moving; the price carrying across turns; a
world with no market trading at a fixed price; a world that prices nothing trading nothing;
and the four definition invariants.

`tests/Imperialism.LegacyImport.Tests/LegacyWorldConverterTests.cs` pins the whole roster —
order, price and the eight omissions — the thirteen-class ship table, the starting fleet,
Great Power status, and that `ship` records are still deferred.

`tests/Imperialism.Content.Tests/WorldContentTests.cs` pins the v19→v20 migration, its
rejection of a contradictory v19 package, and a round trip of the roster, the hulls, the
market and the fleet.

## What a hundred turns looks like

The same fixture as [technology.md](technology.md), with a world market and something to
sell into it. The control is the ordinary-treasury greedy run from that page — same
policy, same money, differing only in whether a market exists.

```
                        grain/turn  workers  top rungs  first  bought  spent    sold   income
with a world market      21 → 63    49 → 119     21      49     14    105,000  1,534  1,199,999
no market, gold only     21 → 42    49 →  77      0    never     7     21,000      0          0
```

**Trade is worth two orders of magnitude more than the mine it sits beside.** One gold tile
a power earns about 20,000 over the century; selling the surplus earns 1.2 million. The
closed run buys the Plows on turn 30 and never affords the Reaper at all; the trading run
buys the Plows on **turn 1** and the Reaper on **turn 45, the quarter it arrives**, then
reaches the gated rung on turn 49.

**So the caveat four documents were carrying is discharged.** "A power that improves as it
earns cannot afford a $12,000 technology in a century" was true of an economy missing its
main revenue stream, and is not true of one that has it.

**The merchant marine binds, hard.** 1,534 units sold against **103,147 offered and
unsold**, and 532 rows short of a hull. Six holds a power a turn against a warehouse that
fills faster than that, so there is always something left on the quay. That is the
railyard failure mode *not* recurring: unlike transport capacity, this constraint is real
and visible, and it is the reason a Freighter's eight holds would matter.

**One thing to be careful about.** The minor nations are a fixture standing in for an
economy — they own no land, no industry and no ships, and simply hold a treasury and bid
for whatever is offered. So **the income figure is an upper bound, not a measurement.**
What is not a fixture is the constraint above, or the fact that trade clears the gap the
mine could not.

Without them a closed world of seven identical powers trades nothing worth counting: every
power holds the same surplus and wants the same things, so a sale is a swap and net cash
across the world is zero. That is a property of the fixture rather than of the model, and
it is why they exist.

**And the population outgrows its food again.** Workers reach 165 by turn 75 and fall back
to 119, with 21 sick at the end — the manual's warning about growing too fast, arrived at
rather than written in, and the same shape [development.md](development.md) reported when
the work duration was 1. Trade income buys improvement faster than the food supply follows.

## Open questions

- **The clearing price.** Still the oldest unknown here, and now load-bearing rather than
  hypothetical. `ITradeMarket` is where a recovered curve goes.
- **The bidder ranking**, which wants relations and subsidies — so it wants diplomacy.
- **Trade subsidies**, which make the price per *pair* rather than per commodity and are
  the manual's own answer to being outbid.
- **Whether oil and fuel are tradable.** The roster omits both and the manual does not say.
- ~~**The ship array order**, the Freighter's cargo, and every build cost.~~
  **All recovered**, and `ship` records convert.
- **Minor-nation behaviour.** The mechanism takes their orders and nothing generates them
  outside fixtures, which is the largest thing standing between this run and a measurement.
- **Blockade, interception and escorts**, which want conflict and give armour and sailing
  speed something to do.
- **Overseas profits**, the manual's third income, which wants colonies.
