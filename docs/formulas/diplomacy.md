# Diplomacy: consulates, embassies, pacts, peace, boycotts and trade subsidies

## Summary

A Great Power makes **overtures** from the Diplomacy screen. A
`DiplomaticOverture(kind, target)` sits in `CountryTurnOrders.Overtures` beside
`DeclareWar` and is one of four kinds:

| Kind | Price | Needs | Toward |
|---|---|---|---|
| Consulate | **$500** | — | any country (see chosen rule 3) |
| Embassy | **$5,000** | an open consulate | any country |
| Pact | free | an open embassy | a Minor Nation |
| Peace | free | a war in effect | any country at war with the issuer |

Every price and prerequisite in that table is the manual's. What an overture grants
**opens next turn**, and peace **ends the war next turn**, both by the relation token
that already makes a declared war begin next turn. A war breaks any pact between the
pair, and between two Great Powers it closes their consulates and embassies both ways.
Countries at war do not trade with each other.

Content **v30** adds an optional world-level `diplomacy` block,
`{consulateCost, embassyCost}`. There is **no new turn phase**: overtures resolve in
`Diplomacy`, ahead of the declarations of war that already ran there, so the client
smoke still counts fourteen. No soak policy submits an overture or a declaration, so
**no soak number moved**.

Content **v31** adds standing **trade policies** — subsidies and voluntary boycotts —
and replaces the market's id-order bidder placeholder with a ranking by subsidy. See
[Trade policy](#trade-policy-subsidies-voluntary-boycotts-and-the-bidder-ranking); its
seven chosen rules are numbered 7–13, after this section's six.

## Confidence

`inferred`. The manual prices both posts, orders the three ties and states who
accepts what. **It never says how a Great Power decides on peace**, and leaves several
orderings unstated. Six rules below are chosen and labelled.

| Claim | Support |
|---|---|
| A consulate costs $500 and opens the turn after the overture | **manual**, lines 3516–3518 |
| A consulate is required before an embassy | **manual**, lines 3527–3529 |
| An embassy costs $5,000 | **manual**, lines 3530–3531 |
| An embassy is required before a pact; a pact costs no cash | **manual**, lines 3541–3543 |
| Minor Nations always accept a consulate, an embassy and a pact | **manual**, lines 3516, 3531–3532 and 3543–3544 |
| Great Powers start with an embassy and a consulate in every other Great Power | **manual**, lines 3574–3577 |
| A war between Great Powers means re-establishing embassies after peace | **manual**, lines 3577–3578 |
| The peace overture exists only once at war | **manual**, lines 3472–3474 |
| Minor Nations never refuse peace | **manual**, lines 3496–3497 |
| Any change in diplomatic status takes effect next turn | **manual**, lines 626–629 |
| Countries at war boycott each other automatically | **manual**, lines 3714–3715 |
| Which countries are Great Powers | **corpus**: `labo` names exactly seven in every scenario (`isGreatPower`, content v20) |
| **A Great Power accepts peace only if it offers peace back the same turn** | **chosen rule 1** |
| Countries in id order, overtures in list order; a refusal charges nothing and stops nothing | **chosen rule 2** |
| A consulate may be opened in a Great Power | **chosen rule 3** |
| Overture cash is reserved before the market clears | **chosen rule 4** |
| A war breaks pacts and closes posts at the declaration | **chosen rule 5** |
| No overture but peace crosses a war, declared or in effect | **chosen rule 6** |

## Who is a Great Power

Nothing new was needed. `CountryDefinition.IsGreatPower` has existed since **v20**,
where the importer sets it for exactly the countries carrying `labo` records — seven in
every shipped scenario, ids 0–6, matching `docs/scenario-semantics.md` ("Only countries
0–6 receive `cash`, `tran`, `labo`, and `tclr` records"). The market already uses it to
decide who carries. **Only a Great Power makes overtures**; an overture from a Minor
Nation is refused as `NotAGreatPower`, because nothing plays the minors and the manual
describes overtures only from a Great Power's side.

The starting ties are seeded from the flag, not from the price block, so a world
migrated from v29 still starts every Great Power pair with both posts. A pair the
scenario authors at war starts without them.

## Next turn, by the token

`WorldState` already stamped every relation change with the current generation and
treated a hostile mode stamped *this* generation as not yet in effect. That made war
begin next turn, but a bare `SetRelationMode(Standard)` would have ended a war **at
once**: the predicate only ever delayed hostility. `HasEffectiveHostility` now reads the
mode **in effect as the generation opened** whenever the token is current. For a war
declared this turn that is the original predicate exactly, because the prior mode was
not hostile. For peace it keeps the war in effect, including its port blockade and its
boycott, until the next turn. A tie needs no token at all. Every overture is planned
against the state the turn opened with, before any is carried out, so a consulate bought
this turn is invisible to an embassy ordered beside it — refused as `NoConsulate` — and
visible to one ordered next turn. That is the same ordering argument as `Investment`
running last.

## Chosen rule 1: a Great Power accepts peace only if it offers it back

The manual: Great Powers "accept peace offers only when it is to their advantage to do
so". Nothing plays the other powers, so the only advantage the engine can observe is
the other side asking for the same thing. **Peace between two Great Powers needs both to
offer it in the same turn.** The pair's peace is made once, by the lower id, and the
higher id's offer is refused as `AlreadyMadeThisTurn`, which is how a mutual declaration
of war already counts. A one-sided offer is `Declined` and charges nothing. Replace this
when anything plays a Great Power.

## Chosen rule 2: order and refusal

Countries in id order, each country's overtures in list order, the same as every other
order list. A refusal emits `DiplomaticOvertureRefusedEvent`, charges nothing, and does
not stop the next overture. The same kind toward the same country twice in a turn is
refused the second time. Cash leaves the treasury **when the order is given**, like the
Engineer's, and is never refunded. The manual's in-turn cancellation refund (lines
3466–3468) has no counterpart, because an order bundle is final once submitted.

## Chosen rule 3: a consulate may be opened in a Great Power

The manual describes consulates only toward Minor Nations, yet it also says a war
between Great Powers makes it "necessary to re-establish your embassies when peace is
made", and an embassy needs a consulate. **The only way to rebuild one is to allow a
consulate in a Great Power**, at the same $500. Limiting consulates to minors would make
a Great Power war permanently close those posts.

## Chosen rule 4: overture cash is reserved before the market

The trade plan is computed before any phase runs, so an overture's price is planned up
front and reserved alongside what the Trade School and the University claim. **An
overture pays from what those leave**, and the market bids with what is left after all
three. Without the reservation, a country could spend the same dollar on a consulate and
a bid.

## Chosen rule 5: a war cuts ties at the declaration

When a declaration is accepted, every pact between the pair is broken in both
directions, and between two Great Powers their consulates and embassies are closed in
both directions. A war on a Minor Nation leaves the posts standing. The manual names no
other consequence. The cut happens **at the declaration** rather than when the war
takes effect. Every reader plans from turn-open state, so the closure is first observed
next turn either way. `DiplomaticTiesCutEvent` reports it and is not emitted when there
was nothing to cut.

## Chosen rule 6: only peace crosses a war

A consulate, embassy or pact toward a country this one is at war with, declared or in
effect, is refused as `AtWar`. So is one toward a country either side declares war on
**this turn**: nobody opens an embassy in a country it is going to war with.

## The boycott

"A trade boycott goes into effect automatically between countries at war." `TradePlanner`
skips a bidder whose war with the seller is in effect, so the offer passes down the
ranked list to the next bidder exactly as if that bidder had not bid. **The skip is
silent**: no new shortfall reason exists for it. An offer that no other bidder takes is
reported with the existing `NoBuyer`, which is what it is from the seller's side. A war declared this turn
does not yet boycott, and peace made this turn does not yet lift it.

## Trade policy: subsidies, voluntary boycotts and the bidder ranking

Content **v31**. A Great Power sets a standing **trade policy** toward another country
from the Diplomacy screen's third tab: nothing, a **subsidy** of some percentage, or a
**boycott**. A `TradePolicyOrder(target, policy)` sits in
`CountryTurnOrders.TradePolicies` and resolves in `Diplomacy` after every overture.
`TradePolicy.None` cancels what stands. The three are exclusive; setting one replaces
the last. There is no new turn phase and no soak number moved — no soak policy sets one.

| Claim | Support |
|---|---|
| Only Great Powers initiate subsidies and boycotts | **manual**, lines 3432–3433 |
| A policy needs a trade consulate or an embassy with the target | **manual**, lines 3663–3667 |
| A policy is a standing setting, changed by order and cancellable before End Turn | **manual**, lines 3681–3690 |
| The recipient buys from the grantor at world price less the percentage | **manual**, lines 3692–3696 |
| The recipient sells to the grantor at world price plus the percentage | **manual**, lines 3696–3699 |
| A subsidy "favours the other country on both ends of trade deals" | **manual**, lines 3018–3021 |
| A boycott means "no trade of any kind occurs between the two countries" | **manual**, lines 3436–3437 and 3714–3720 |
| An offer goes first to the seller's most favoured partner, the rest passes down its list | **manual**, lines 2976–2983 and 3393–3394 |
| Subsidies and relations together set that list | **manual**, lines 3031–3033 |
| **The percentages are 5 and 10** | **chosen rule 13**, from the tutorial (lines 5121–5126) |
| A policy takes effect next turn | **chosen rule 7** |
| Ranking by subsidy alone, ties in id order | **chosen rule 8** |
| Between two Great Powers a subsidy covers only the grantor's own sales | **rule 9**, release notes as recorded in `game-systems.md:204-206` — not chosen |
| Rounding once per deal, toward the recipient | **chosen rule 10** |
| A subsidy never moves the world price | **chosen rule 11** |
| Closing the posts clears the policy | **chosen rule 12** |

**Refusals** (`TradePolicyRefusedEvent`, each charging nothing and stopping nothing):
`NoSuchCountry`, `Itself`, `NotAGreatPower`, `NoPost` (no consulate or embassy open **as
the turn began**, so a consulate bought this turn carries a policy only from next turn),
`PercentNotOffered` (the world does not list that percentage), and
`AlreadyOrderedThisTurn` (a second policy toward a country whose first one this turn
stood; a refused first order does not count). Setting a policy is free: "the cost is in
the prices". Setting `None` where nothing stands is accepted and changes nothing.

### The market

* **Boycott.** `WorldState.IsTradeBoycotted` is the one gate, with two causes: a war in
  effect, or a voluntary boycott **set by either side**. A boycotting Great Power neither
  sells to nor buys from the target, and the target's offer passes down to the next
  bidder exactly as past a war.
* **Ranking.** Each seller ranks its bidders by **the subsidy each bidder grants it**,
  highest first. Ties — every unsubsidised bidder among them — stay in country-id order,
  so **with no subsidy anywhere the order is exactly what it was** and no published number
  moves. **Only a subsidy that moves the deal's price ranks a bidder** (rule 9): when the
  seller is a Great Power no bidder's subsidy to it counts, and its bidders stand in id
  order.
* **Price.** With world price `w`, a subsidy `p` from grantor G to recipient R:
  R buying from G pays `w·(100−p)/100` a unit. A **Minor Nation** R selling to G receives
  `w·(100+p)/100`; a **Great Power** R selling to G receives `w` (rule 9). In code the
  deal's factor is `100 − (seller's subsidy to buyer)`, plus the buyer's subsidy to the
  seller only when the seller is not a Great Power — always between 1 and 199, because
  only a Great Power grants a subsidy and so no deal ever carries two. The buyer pays and the seller receives the same
  total; `CommodityTradedEvent.Total` carries it, and `UnitPrice` stays the world price.
* **Affordability** is checked by division at the subsidised price, in 128-bit
  arithmetic, so a discount buys what it should and nothing overflows.

### Chosen rule 7: a policy takes effect next turn

The manual says "any changes in diplomatic status take effect next turn" (lines 626–629)
and lets a policy change be cancelled "before the end of the turn" (lines 3688–3690),
but never says when the new prices bite. The market is planned before any phase runs,
so a policy set in this turn's `Diplomacy` is first read by next turn's market — the
same ordering that makes a consulate open next turn.

### Chosen rule 8: ranking by subsidy alone, ties in id order

The manual combines subsidies **and relations** (lines 3031–3033) and never says how.
Relation scores are not modelled, so subsidy is the only factor and the ranking is **half
the manual's rule**. This replaces the pure id-order placeholder; when relations land,
this is the rule to revisit.

### Rule 9: between two Great Powers a subsidy covers only the grantor's own sales

**From the release notes as recorded in `docs/game-systems.md:204-206`; the release notes
themselves (README.TXT / README11.TXT in the original install) are not in the repo and are
still to be checked on the PC.** "Between two Great Powers a subsidy governs only the
granter's own goods — it lowers what the other power pays you, and does not change what you
pay them. Minor nations get the symmetric version." This project ranks the release notes
above the manual where they disagree, so the manual's two-way rule (lines 3692–3699) holds
only toward a Minor Nation.

So G granting Great Power H 10% makes H's purchases from G 10% cheaper and leaves H's sales
to G at the world price. A subsidy that moves no price ranks no bidder either: when H
sells, G's subsidy does not put G ahead of H's other bidders.

This **retires the rule that mutual subsidies net**, which v31 first shipped as chosen rule
9. A Minor Nation cannot grant a subsidy, and between Great Powers each subsidy covers only
its grantor's sales, so no deal ever carries two subsidies and there is nothing to net.

### Chosen rule 10: rounding once per deal, toward the recipient

`units · w · factor` is computed exactly and divided by 100 once for the whole deal:
the **floor** where the buyer is favoured, the **ceiling** where the seller is. Three
units at 101 under a 5% subsidy to a Minor Nation cost it 287 (287.85) when it buys and pay
it 319 (318.15) when it sells.

### Chosen rule 11: a subsidy never moves the world price

The price for next turn answers to the volumes offered and bid, through `ITradeMarket`,
whose signature takes no deal price at all. A subsidised deal moves the price exactly as
an unsubsidised one of the same volume. This is structural rather than tested by
mutation; `ASubsidyDoesNotMoveTheWorldPrice` pins the outcome.

### Chosen rule 12: closing the posts clears the policy

A Great Power war closes the consulates and embassies both ways (chosen rule 5), and a
policy needs one of them. The manual says the posts must be re-established after a war
and says nothing of the policies that stood on them. Here they are cleared, both ways,
with a `TradePolicyClearedEvent`. A war on a Minor Nation closes no post and clears
nothing; the war's own boycott already stops the trade.

### Chosen rule 13: the percentages are 5 and 10

The manual offers "a new trade subsidy of varying percentages" (line 3681) and never lists
them; its tutorial presses buttons labelled **5%** and **10%** (lines 5121 and 5126).
`docs/disasm/definitive-original-data.md` names no subsidy level at all, so a third button
is not ruled out. The importer writes `[5, 10]` into `diplomacy.subsidyPercents`; a
package migrated from v30 lists none, so it can grant no subsidy but can still boycott.

## An eliminated country (row 6e)

A country whose capital is taken is eliminated (`army.md`, rules 38–48). At that moment
every pact, consulate, embassy and trade policy involving it is cleared, in both
directions. The counts ride on `CountryEliminatedEvent`, and no `TradePolicyClearedEvent`
is raised for them (army rule 42). From then on, a war declaration, overture, peace offer,
subsidy or boycott naming it is refused with a new `CountryEliminated` reason on
`WarDeclarationRefusal`, `DiplomaticOvertureRefusal` and `TradePolicyRefusal`, and nothing
is charged. This is chosen (army rule 48): there is nobody to treat with, and its
provinces may be attacked without a war anyway. Its own orders are set aside before
`Diplomacy` runs (army rule 44). Tests: `DiplomacyNamingAnEliminatedCountryIsRefused`.

## Still open

* **Relation scores**: none of the manual's relation deltas is modelled. A consulate's
  improving relation per trade, a pact's improved relationship, and the worldwide
  penalty for breaking one all wait on _relations_ in `_index.md`.
* **Alliances** between Great Powers, and the other treaty overtures.
* **Colonies**: a Minor Nation joining an Empire peacefully needs an embassy and good
  relations.
* **Civilians entering a Minor Nation**: the manual forbids it until an embassy stands
  (lines 3535–3536). Civilians never leave their owner's land in this engine yet, so the
  gate has nothing to stop.
* **Foreign aid** (grants), which the consulate also unlocks.
* **Colony boycotts** (lines 3721–3727): a colony's own boycott list. Colonies are not
  modelled.
* **Relations in the bidder ranking** (chosen rule 8).
* **Checking rule 9 against the release notes.** README.TXT / README11.TXT from the
  original install should be read on the PC to confirm the wording `game-systems.md`
  records. If they say otherwise, the change is the Great Power guard in
  `TradePlanner.PriceFactor` and `TradePlanner.RankBidders`.

Tests: `DiplomacyTests` and `TradePolicyTests`, plus the v30 and v31 content and importer
checks in `WorldContentTests` and `LegacyWorldConverterTests`.
