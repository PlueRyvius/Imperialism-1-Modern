# Ship building

## Summary

A country orders hulls at the Shipyard. Each order names a class and a count; the
Shipyard charges that class's bill once per hull from the Available warehouse, and the
ships join the country's fleets at the end of `Construction`. A merchant's holds
therefore first count in **next** turn's trade. Ships cost no cash and no labour.

There is no new turn phase and no content version bump. The thirteen hulls, their bills
and their technology requirements were already in content (`trade.md`); this slice is
the first code to read `BuildCost` and `RequiredTechnology`.

## Confidence

`inferred`. The manual states what a ship consumes and what it does not, and when new
units appear. The bills are the executable's own (`verified`, see `trade.md`). Chosen
rules are labelled where they appear and in the table.

| Claim | Support |
|---|---|
| A hull costs commodities: lumber and/or steel, fabric, coal or fuel, and arms for a warship | **manual**, lines 2763–2764 |
| Building a ship spends no labour | **manual**, line 2766 — "Ships do not require workers" |
| Building a ship spends no cash | **executable** — none of the thirteen bills carries cash; the owner's account agrees (`Ships.cs`) |
| A shortage is shown on the Shipyard dialog | **manual**, lines 2764–2765 — "the shortage appears in red text" |
| A merchant adds its holds to the trade total | **manual**, lines 2772–2774 |
| New units appear at the start of the next turn | **manual**, lines 2677–2680 |
| Merchant ships are the exception to new units appearing on the map; other new units start in the capital's province or port | **manual**, lines 2676–2682 |
| Merchant classes are those with absent or zero firepower in imported combat data: Trader, Indiaman, Paddlewheeler, Clipper and Freighter | **chosen rule**, based on the 13 imported `ShipTypeDefinition.Combat` values |
| Each order is all or nothing | **chosen rule** |
| A refused order does not stop the orders after it | **chosen rule** |
| A class may appear more than once in one turn's orders, each entry standing alone | **chosen rule** |
| The Shipyard is paid after production, facility expansion and the railyard, and before Capitol recruitment and the market | **chosen rule** |
| Merchant ships remain unplaced; warships are delivered to a water cell adjacent to the capital | **chosen rule**, implementing the manual's capital-port location |
| When multiple sea zones touch the capital, the lowest `SeaZoneId` wins | **chosen rule** |
| With no capital or no adjacent sea zone, a warship stays unplaced and starts a new fleet | **chosen rule** |
| A fleet that receives ships in Construction cannot be assembled until the next turn | **chosen rule**, because Conflict resolves later in the same turn |
| Merchants merge into the **first** unplaced, unassembled fleet of their class; warships merge into the **first** unassembled fleet of their class in the capital's sea zone | **chosen rule** |
| No cap on how many hulls a turn, other than materials | **chosen rule** |
| A hull with an empty bill builds for nothing | **chosen rule** (no imported hull has one) |

## The order

`ShipBuildOrder(ShipTypeId Type, long Count)`, a list on `CountryTurnOrders.ShipBuilds`.
Nothing is validated when the order is built. A count of zero or less, or a class the
world does not define, is refused with an event when the turn resolves, the way an order
naming a dead civilian is. **A class may appear more than once in the list, and each entry
stands alone (a chosen rule)**: each is checked and paid on its own, in list order, against
what the entries before it left.

## Resolution

`ShipyardPlanner` runs with the other builds before the pipeline starts, and its plan is
committed in `Construction`. Each order is checked in this order, and the first failure
refuses it with a `ShipBuildRefusedEvent` that charges nothing:

1. **`UnknownType`**: the class is not in the world's ship table.
2. **`NonPositiveCount`**: the count is zero or less.
3. **`TechnologyNotKnown`**: the hull names a technology the country does not know.
   Knowledge is read as the turn opens, from the same state the facility gate reads, and
   research is bought in `Investment`, which runs last. So a technology bought this turn
   opens the hull next turn.
4. **`NotEnoughCommodities`**: the warehouse cannot pay the whole bill for every hull in
   the order.

**Affordability is checked by division before anything is multiplied**, the same way as
the Trade School's bill (`CanAfford`). So a count of `long.MaxValue` is refused as
unaffordable and never overflows.

### All or nothing (a chosen rule)

An order the warehouse can only partly cover is **refused whole, not trimmed**: materials
for two of three Traders build none of them. The manual describes a dialog that shows a
shortage in red rather than one that builds fewer, so this is the reading that matches
the screen. It is still a choice, the same one the Power Plant and the Trade School make.

Orders are paid **in the order given**, and **a refused order does not stop the ones after
it (a chosen rule)**: three Traders refused and then one Trader accepted is a legal turn.

**A hull with an empty bill builds for nothing (a chosen rule).** `ShipTypeDefinition`
allows an empty `BuildCost`, and the planner charges exactly the bill, so such a hull is
limited by nothing. No imported hull has an empty bill (all thirteen carry the
executable's), so this only matters for hand-authored content.

### Who is paid first (a chosen rule)

The Shipyard is planned after production, facility expansion and the railyard, and
before Capitol recruitment, so it takes what the mills and the railyard leave. The manual
orders none of these against each other. Because the Shipyard is planned before the
market clears, **materials promised to a hull cannot also be sold**. That follows the
manual's "you cannot sell items … that you have ordered industry to use this turn".

## Delivery

At the end of `Construction` the hulls are added to the country's ship count, which is
what `GetMerchantMarine` sums. `Trade` runs earlier in the same turn and was planned
before the pipeline started, so **the market that clears this turn never sees them**.
Their holds first count next turn. That is the manual's "appear … at the start of the
next turn", and it falls out of phase order, as "completes next turn" does for facility
expansion.

### Where the ships go (chosen rules)

The manual says that all new units except merchant ships appear on the Terrain Map at the
start of the next turn, in the capital's province or port (lines 2676–2682). A hull is a
merchant ship when its imported `Combat` data is absent or has zero firepower. In the
13 imported classes that makes **Trader, Indiaman, Paddlewheeler, Clipper and Freighter**
merchants; **Frigate, Ship-of-the-Line, Raider, Ironclad, Advanced Ironclad, Armoured
Cruiser, Dreadnought and Battle Cruiser** are warships.

Merchant hulls remain unplaced (`SeaZone` null) and in no task force. Their holds still
count in the merchant marine as soon as Construction ends, and they **join the first
fleet of the same country and class that is unplaced and in no task force**, or start an
unplaced fleet with the next dense `FleetId`.

A warship is placed in the sea zone of a water cell adjacent to the capital, using Core's
bounded, non-wrapping neighbor lookup. If multiple sea zones touch the capital, the
**lowest `SeaZoneId` wins**. If the country has no capital or no sea zone touches it, the
warship remains unplaced and starts its own unplaced fleet; these are chosen rules because
the manual does not specify those cases. A placed warship **joins the first fleet of the same country and class in that
sea zone and in no task force**, or starts a new fleet there. A fleet in a task force is
never merged into, because doing so would move the new ships with it. Fleet ids stay
dense and are never reused. Fleet `n` is always at index `n - 1` of `WorldState.Fleets`,
and that list is appendable but read-only to callers. Since `Conflict` runs after
`Construction`, a fleet that receives ships during Construction is not assembleable until
the next turn. `ShipsBuiltEvent.SeaZone` records the delivery zone, or null when the fleet
remains unplaced.

The fair start's three Traders have never been fleets. They are ship counts without a
`FleetState` (see `FleetStateTests.DefaultMerchantShipsDoNotInventAPositionedFleet`). So
the first Trader a skirmish power builds starts a new fleet holding only the new ship,
while its ship count, and so its holds, includes the defaults.

## Out of scope

Obsolescence and scrapping (manual line 2785: "An obsolete ship is broken up"), flagships,
repair, blockade and interception, and any shipyard cap. On the cap, the chosen rule is
**no limit but materials**. The manual's warning at lines 2768–2769 that "over-construction can
slow your early development" is about materials, not a separate cap.

## Open: most later hulls are not gated in imported content

**This is a gap in content, not in the planner.** The executable's naval table gates only
three hulls: Paddlewheels for the Paddlewheeler and the Raider, and Streamlined Hulls for
the Clipper. Those are the only `RequiredTechnology` values the importer emits. The
manual's Benefits of Technology Table gates more:

- Advanced Iron Working, "allows construction of Ironclads" (manual line 4194);
- Advanced Ironclads (line 4217);
- "a fast, powerful armoured cruiser and an enormous, all steel freighter" (lines
  4228–4229);
- "Dreadnoughts and Battle Cruisers" (lines 4240–4241).

`trade.md` already notes that these gates "exist somewhere and are **not** in the naval
table's own fields". Until the importer emits them, **an imported power can build an
Ironclad, a Freighter or a Dreadnought from 1816**, provided it has the steel, arms and
coal. The Dreadnought and Battle Cruiser still need fuel, so they are held back
indirectly by the refinery's Oil Drilling gate.

Fixing this is an importer change with no format change, since `requiredTechnology`
already exists. It wants two things first: the extracted table read row by row, because
the layout around lines 4217–4241 is ambiguous about which row each sentence belongs to;
and the corpus ship check (`EveryShipInTheCorpusIsAHullItsOwnerCouldHaveBuilt`) re-run
against the full corpus, because new gates could make an authored fleet one its owner
could not have built.

## Soak

No soak policy orders ships, so **no published soak number moved**. No Trader control
run was added.

## Tests

`ShipbuildingTests` (Core) and `TurnReportRendererTests` / `TurnReportTextTests`
(Presentation). Each of these mutations was run and fails a test:

| Mutation | Caught by |
|---|---|
| Drop the technology check | `AHullWhoseTechnologyIsNotKnownIsRefusedAndChargesNothing`, `ATechnologyBoughtThisTurnOpensTheHullNextTurn` |
| Trim an order to what the warehouse covers, instead of refusing it | `AnOrderTheWarehouseCannotCoverIsRefusedWholeNotTrimmed`, `AnOrderShortOfOneCommodityIsRefusedAndChargesNothing`, `AnOverflowingCountIsRefusedNotThrown` |
| Count the ships before the market is planned | `NewHoldsCountInNextTurnsTradeButNotThisTurns` |
