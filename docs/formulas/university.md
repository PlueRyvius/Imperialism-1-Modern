# The University

## Summary

A country buys civilians at the University and may disband them again. A purchase
names a civilian type and a count. Each civilian costs **one expert worker**, some paper
and some cash. The expert leaves the workforce for good the turn the order is given, and
the civilian appears on the capital and takes orders from the next turn. Disbanding a
civilian removes it, refunds nothing, and returns one healthy expert, who supplies labour
from the next turn.

Content **v29** adds an optional `purchase` block to each civilian type. A type without
one is not for sale. There is **no new turn phase**: both orders resolve inside
`Production`, after the Trade School and before any recipe, and the new civilians are
placed in `Delivery`. The client smoke still counts fourteen phases.

## Confidence

`inferred`. The manual states what a civilian is made of, which types are for sale and
when, when the expert leaves, when the civilian appears and what disbanding returns. **It
never prices the paper or the cash.** Seven rules below are chosen, and are labelled where
they appear and in the table.

| Claim | Support |
|---|---|
| Every civilian but the Developer costs an expert worker, paper and cash | **manual**, lines 1167–1168 and 2699 |
| The Developer cannot be built at the University | **manual**, lines 1167 and 1407–1408 |
| A civilian ordered now appears at the start of the next turn | **manual**, lines 1168–1169, 2676–2680 and 4872 |
| New units start in the province of the capital | **manual**, lines 2680–2681 |
| The expert is deducted from the workforce permanently, as soon as the order is given | **manual**, lines 4867–4870 |
| An expert supplies four labour a turn | **manual**, lines 1192–1193 and 2702–2703 |
| A shortage of experts, cash or paper is shown in red, not built short | **manual**, lines 2699–2701 |
| At the start only the Farmer, Miner, Prospector and Engineer can be built | **manual**, lines 2707–2708 |
| Feed Grasses permits the Rancher, Iron Railroad Bridges the Forester, Oil Drilling the Driller | **manual**, lines 2708–2711 |
| Disbanding returns the expert to industry | **manual**, lines 1189–1193 |
| Disbanding refunds no cash and no paper | **manual**, lines 1193–1195 — the manual's, not chosen |
| Disbanding cannot be taken back | **manual**, line 1196 — every order in a bundle is final once resolved |
| **2 paper and $1,000 per civilian** | **`guess`** (chosen rule 1) |
| Paid in `Production`, after the Trade School and before recipes and the market | **chosen rule 2** |
| All or nothing per order; a refused order does not stop later ones; a type may repeat | **chosen rule 3** |
| Only healthy experts may be sent | **chosen rule 4** |
| Delivered on the capital cell; a capital lost before delivery means no civilian and no refund | **chosen rule 5** |
| A disbanded civilian's expert works from the next turn | **chosen rule 6** |
| Technology is read as the turn opens | **chosen rule 7** |

## The price is a guess (chosen rule 1)

The importer prices every type the same: **1 expert + 2 paper + $1,000**. The expert is
the manual's. **The paper and the cash are guesses** and must not be cited.

The executable holds a candidate: a nine-record cost block at `0x00695C50`, each record
2 paper and an expert, with cash 1,500 / 500 / 1,000 / 1,000 / 2,000 / 1,000 / 1,000 /
2,000 / 5,000 (`docs/disasm/definitive-original-data.md`). Nine is the number of civilian
types, and paper plus an expert is exactly the manual's University bill. But **nobody has
traced its selector**, the same block has also been read as a depot/port candidate, and
its first figure is not ours. The project's rule is not to promote a candidate without
tracing its reader (`CLAUDE.md`), so these figures are **not** used. A row in
`docs/disasm/wanted-values.md` §5 asks for the selector to be traced. If it is the
University table, replacing the guess is an importer edit: the cash moves per type and
nothing else changes.

The importer's gates are the manual's:

| Type | Gate |
|---|---|
| Farmer, Miner, Prospector, Engineer | none |
| Rancher | Feed Grasses |
| Forester | Iron Railroad Bridges |
| Driller | Oil Drilling |

They resolve through `TechnologyKey`, like every other gate since v19. The importer emits
no Developer type, so "the Developer is not for sale" only matters for hand-authored
content, where a type with no `purchase` block is refused with `NotPurchasable`.

## The orders

`CivilianPurchaseOrder(CivilianTypeId Type, long Count)`, a list on
`CountryTurnOrders.CivilianPurchases`, and `CountryTurnOrders.CivilianDisbands`, a list of
`CivilianUnitId`. Nothing is validated when an order is built, apart from bundle shape: a
civilian named twice in the disband list, or both disbanded and given a work order in the
same bundle, is a malformed bundle and throws `ArgumentException`, like a civilian given
two work orders.

## Resolution

Both orders are planned inside `ProductionPlanner`, after the Trade School and before any
recipe, and committed in `Production`.

### Disbands first

Each disband is checked in list order:

1. **`NoSuchCivilian`**: no civilian carries that id (most likely it has died).
2. **`NotYours`**: the civilian belongs to another country.

An accepted disband removes the civilian at once and adds **one healthy expert** after
production, **whatever the type cost**. An unpurchasable type still disbands into an
expert, because the manual ties the expert to the civilian, not to the price.

### Then purchases

Each purchase is checked in this order, and the first failure refuses it with a
`CivilianPurchaseRefusedEvent` that charges nothing:

1. **`UnknownType`**: the type is not in the world's civilian list.
2. **`NonPositiveCount`**: the count is zero or less.
3. **`NotPurchasable`**: the type has no `purchase` block.
4. **`TechnologyNotKnown`**: the price names a technology the country does not know.
5. **`NoCapital`**: the country has no capital to deliver to.
6. **`NotEnoughExperts`**: too few healthy experts for the whole order.
7. **`NotEnoughCommodities`**: the warehouse cannot cover the whole bill.
8. **`NotEnoughCash`**: the treasury cannot cover the whole bill.

**Affordability is checked by division before anything is multiplied**, as the Trade
School and the Shipyard do. So a count of `long.MaxValue`, or a per-civilian bill large
enough to overflow, is refused as unaffordable and never throws.

On success the paper and cash are charged and the experts are **removed from the
workforce permanently**. They are withheld from this turn's labour pool before any recipe
runs, so the turn they are spent is the first turn they do not work. That is the manual's
"actually deducted from your work force permanently".

### All or nothing, in list order (chosen rule 3)

An order the country can only partly cover is **refused whole, not trimmed**: experts for
two of three Miners build none of them. That matches a dialog that shows a shortage in red
rather than building fewer. Orders are paid **in the order given**, and **a refused order
does not stop the ones after it**. **A type may appear more than once**, each entry
checked and paid on its own against what the entries before it left.

### Only healthy experts (chosen rule 4)

An ill expert supplies no labour this turn, so there is nothing to take. This is the
Trade School's rule, kept the same.

### When it is paid (chosen rule 2)

The University is paid after the Trade School and before any recipe. Because
`Production` runs before `Trade`, **paper and cash promised to the University cannot also
be spent at market**, as with the Trade School. The manual does not order the University
against anything else.

### Technology as the turn opens (chosen rule 7)

The gate reads knowledge as the turn opens. Research is bought in `Investment`, which runs
last, so Feed Grasses bought this turn opens the Rancher next turn. Like every other
technology gate, this is phase order, not a special case.

## Delivery (chosen rule 5)

The new civilians are created on the **capital cell** in the `Delivery` phase, idle, with
new dense `CivilianUnitId`s. The manual says "in the province of the capital". The capital
cell is in that province and is the one cell the engine can name without inventing a
choice.

**Delivery is in `Delivery`, not at the end of `Construction`, and the ordering matters.**
`Development` runs after `Construction` and reads this turn's work orders. A civilian
created in `Construction` would therefore exist when its own turn's work orders are
checked, and an order naming its predicted id would be accepted the turn it was bought.
Created in `Delivery`, after `Development`, such an order is refused `NoSuchCivilian`, and
the civilian first takes orders next turn, which is the manual's "appears at the start of
the next turn".

**A country whose capital is gone when `Delivery` runs receives nothing and is refunded
nothing.** A `CivilianDeliveryLostEvent` reports the loss. Nothing in the engine moves a
capital between `Production` and `Delivery` today, so this branch is defensive and is
not reached by any test. Capture of a capital will make it reachable.

## Disbanding (chosen rule 6)

The returned expert **supplies labour from the next turn**. It is added after production,
so it cannot be spent in this turn's recipes, and the University cannot sell it again in
the same turn: every count is read against the workforce the turn opened with. The manual
says only that disbanding "gives you four more labour per turn". Starting next turn
matches a recruit, which also works only from the turn after it arrives.

The returned expert is in the workforce when `Feeding` runs, so **it eats the turn it
returns**, as a recruit does.

## Out of scope

The Developer as a reward from foreign policy (manual lines 1406–1411), the
one-Developer cap, the Armoury, and the case of civilians killed when their province is
lost (line 1165).

## Soak

No soak policy buys or disbands civilians, so **no published soak number moved**.

## Tests

`UniversityTests` (Core), `WorldContentTests` (Content: v28→v29 migration, round-trip,
malformed blocks), `LegacyWorldConverterTests` (importer prices and gates) and
`TurnReportRendererTests` / `TurnReportTextTests` (Presentation). Each of these mutations
was run and fails a test:

| Mutation | Tests failed |
|---|---|
| Don't withhold the bought expert's labour this turn | 1 of 26 `UniversityTests` |
| Count sick experts as available | 1 of 26 |
| Drop the technology check | 1 of 26 |
| Multiply instead of dividing in `CanAfford` | 1 of 26 (the overflow case) |
| Stop at the first refused order | 2 of 26 |
| Drop the capital check | 1 of 26 |
| Don't spend the University's cash | 4 of 26 |
| Don't remove the experts | 4 of 26 |
| Let a disbanded civilian's expert work this turn | 1 of 26 |
| Return no expert on disband | 2 of 26 |
| Let a country disband another's civilian | 1 of 26 |
| Let the v28 migrator accept a `purchase` block | 1 of 124 `WorldContentTests` |
| Drop `requiredTechnology` in the compiler | 1 of 124 |
| Leave the Rancher ungated in the importer | 1 of 118 `LegacyWorldConverterTests` |
