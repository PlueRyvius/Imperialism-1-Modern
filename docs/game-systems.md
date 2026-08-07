# Game systems: the specification we're building to

A condensed engineering summary of how the original game works, assembled
from its manual and shipped release notes and written in our own words for
implementation purposes. Where the two disagree the release notes win — they
correct the manual in several places, noted below.

This is the *what*. The numbers behind it are largely undocumented; those are
tracked separately in `formulas/_index.md`.

## Frame

Turn-based, one turn = 3 months, 1815–1915 (~400 turns). Seven Great Powers
plus sixteen minor nations. All powers submit orders, then **everything
resolves simultaneously**.

## Turn resolution order

Orders are freely editable until committed, then resolution runs in this
fixed order:

1. Diplomatic offers exchanged and accepted/rejected
2. Trade deals offered and accepted/rejected
3. Industrial production
4. Military conflicts
5. **Trades cancelled** where a blockade or interception invalidated them
6. Commodities transported and delivered into the warehouse **for next turn**

Two properties drive our architecture:

- **Step 5 retroactively undoes part of step 2, based on step 4.** We handle
  this by having trade emit *intents* that only step 6 commits, so
  cancellation is a filter rather than a rollback. See `architecture.md`.
- **Most outputs are deferred one turn**: traded goods, built units and
  purchased technology land the following turn, and transported goods are not
  generally available to production immediately. Two explicit same-resolution
  exceptions matter: workers eat newly transported raw food before warehouse
  food, and power is created and consumed as labour during production.

Any implementation that resolves powers sequentially rather than
simultaneously will produce visibly different games.

**Current implementation boundary.** `TurnResolver` now enforces this eleven-
phase pipeline over dense country-id-ordered submissions and emits an immutable
phase event log. Strategic time is an explicit year and quarter with no legacy
date cap. Rail-connectivity materialization, evidence-backed industrial
production, and map extraction now have system behavior; conflict, trade and
diplomacy remain deliberately unimplemented rather than filled with guessed
rules. Extraction and Feeding are phases of our own between step 5 and
step 6: the original folds gathering into transport, but separating them keeps
the harvest readable in the event log and lets it observe post-conflict
ownership. Feeding sits between the two because workers eat food transported
this turn before warehouse stock — one of the two same-resolution exceptions —
so the harvest is eaten off the cart and only the remainder is delivered.

## Economy

**Current implementation boundary.** Commodity and deposit definitions are
content-defined rather than fixed to legacy slots. Runtime stock uses checked
64-bit quantities with separate Available inventory and identifiable Pending
Delivery entries. Trade and transport entries can be cancelled independently;
the Delivery phase commits the remainder atomically and records events.
Facilities, recipes, and initial capacity are content-defined. Ordered
production requests share facility capacity, complete partially when capacity
or inputs run short, consume only Available stock, and stage outputs so they
cannot feed another recipe until the following turn. The resolver preflights
production together with pending deliveries before mutating inventory. Deposits
inside a connected catchment now pay their owner each turn and reach the
warehouse through Delivery, with unreachable output reported rather than
dropped. Yield depends on the deposit and on the cell's development level, taken from the
manual's Resource Development Table: cultivated ground runs 1/2/3/4, coal, iron
and oil 0/2/4/6, gold and gems 0/1/2/3, and fish and horses have no improvement
at all. Ports collect one fish per adjacent coast or river tile. Workers now eat: the
grain / fruit / grain / meat cycle, canned food as the substitute, sickness for
the wrong food and permanent loss for none at all, with the labour pool computed
at 1/2/4 per grade. **Production now spends that pool**, each recipe costing its
total input units, and both starvation and sickness cut it — on the turn after,
since production resolves before feeding. A fair start and capacity construction
have landed: every power can begin identical, and a facility can be built one
rung larger at one lumber and one steel per point. The Capitol recruits
untrained workers, capped at a quarter of the provinces owned and priced in
canned food, clothing and furniture. **Civilian units improve land**: a Farmer,
Rancher, Forester, Miner or Driller sent to a tile raises its development level,
which is what finally lets a food-short economy grow its harvest and pay the
Capitol's price. Improvement needs the terrain to admit a worker *and* the
deposit to be that worker's, which are two different tables in the manual.
Prices, power, sea routes, the transport capacity pool and research remain
pending — see `formulas/production.md`, `formulas/extraction.md`,
`formulas/feeding.md`, `formulas/development.md` and
`reference/manual-mechanics.md`.

**Commodity tiers.** 13 raw resources (grain, livestock, fruit, fish, cotton,
wool, horses, timber, coal, iron, oil, gold, gems) → 6 materials (canned
food, fabric, paper, lumber, steel, fuel) → 4 goods (clothing, furniture,
hardware, armaments).

**Conversions are uniformly 2:1** — 2 wool *or* cotton → 1 fabric; 1 iron +
1 coal → 1 steel; 2 steel → 1 hardware *or* 1 armaments. Food processing is
the exception (2 grain + 1 fruit + 1 fish-or-livestock → 2 canned food).

Mills step 2→4→8→16→24→+8; factories step 1→2→4→8→12→+4. Each capacity point
costs 1 lumber + 1 steel. Food processing and the railyard are uncapped.

**Labour.** Workers are untrained/trained/expert, contributing 1/2/4 labour
per turn. Experts are consumed permanently to create civilian units;
regiments consume workers permanently too, while ships consume none.

Production draws on one pool per country, shared across every facility, in the
order requests arrive — unlike capacity, which is per facility. The manual
prices one recipe (a unit of clothing costs two fabric and two labour) and the
uniform 2:1 conversion above carries that rate to the whole set; see
`formulas/production.md`.

**Food is per-worker and unforgiving.** Workers cycle through preferences in
groups of four (grain, fruit, grain, meat-or-fish → exactly 50/25/25 demand).
Each worker resolves through a cascade: preferred type transported this turn
→ preferred type from warehouse → canned food → wrong type (worker falls
sick, contributes zero) → **starves and is permanently removed**.

**Power** is not a commodity: a power plant burns fuel and adds directly to
the labour pool the same turn, is spent before human labour, and cannot be
stored or sold.

**Money.** No taxation. Income comes from trade, from gold/gems (which bypass
the warehouse entirely and convert straight to cash on transport), and from
overseas profits. Credit allows going negative up to a limit scaling with
income; past half the limit interest worsens progressively, and past the
limit the game force-sells your stock at poor prices.

## Transport and connectivity

Transport capacity is a single pool of units-moved-per-turn, allocated across
commodities by slider.

Connectivity is a graph problem, recomputed after every conquest and naval
move. A depot needs an unbroken rail path to the capital, **or** rail to a
tile holding both a port and a depot (goods then travel by sea). Ports need
coast or river. Three edge cases matter:

- A **river port** is disconnected if you lose a province *downstream*.
- A **sea port** is lost only under *undisputed* enemy naval control — a
  single friendly warship anywhere in the zone preserves it.
- A tile's output is gathered only if it is **on or within one tile** of a
  connected depot or port. Overlapping catchments waste coverage.

Towns industrialise on their own once served by a connected depot or port —
first materials, then consumer goods capped at half the material output,
gated on your matching factory reaching a capacity threshold.

**Current implementation boundary.** Phase 3 begins with country-specific
rail components: only rail edges whose two province cells are currently owned
by that country are usable. The component index is cached and rebuilt lazily
after conquest or rail changes. Extraction now consumes that graph: a deposit
pays its owner only when it sits within the catchment radius of the capital's
own rail component. River continuity, naval control, and sea-zone traversal
remain explicit later layers; the graph still does not guess those.

Depots and ports are now modelled as sites, imported from the `rail` and `port`
records. Gathering happens at **connected depots, connected ports and the
capital** — never at bare track. A depot is connected when rail carries its goods
to the capital; a port needs no rail at all, since its goods leave by water; and
the capital is always both. Replacing the old placeholder, where every railed
cell gathered, cut `s1` from 319 collection points to 134.

Still missing: the route that lets a depot reach the capital by rail to a
port-plus-depot tile and then by sea, and the two ways a port loses its
connection (losing the province downstream of a river port, and undisputed enemy
naval command). See `reference/manual-mechanics.md`.

## Trade

Trade is a **ranked auction**, and it is the heart of the game.

Each turn every nation submits sell offers and buy bids. Per commodity, a
seller's stock walks down that seller's ranked list of favoured partners —
ranking blends the subsidy offered and the diplomatic relationship. The top
bidder gets an offer sheet and may take any quantity; the remainder passes to
the next bidder, and so on.

The constraints are what make it a puzzle, and all of them must be right or
the puzzle evaporates:

- You may bid on **at most four commodities per turn**.
- **Merchant marine holds** are a shared budget: each hold is usable once per
  turn, for buying *or* selling.
- Between Great Powers **the buyer ships**, consuming the buyer's holds. Minor
  nations own no merchant marine, so trading with them always spends yours.
- Offers are presented in a **fixed commodity order**, so reserving holds for
  later-ordered goods is a real skill.
- The displayed price is **last turn's** clearing price; this turn's is
  unknowable when you commit. Sellers reacting to last turn's price produces
  genuine cobweb oscillation.
- You **cannot see a commodity's market unless you bid on it** — deliberate
  information asymmetry.

Conquering a minor nation's capital leaves it a permanent captive market that
buys only from its owner.

*Release-notes correction:* between two Great Powers a subsidy governs only
the granter's own goods — it lowers what the other power pays you, and does
not change what you pay them. Minor nations get the symmetric version.

## Diplomacy

Nine relationship levels, tracked pairwise between every pair of countries.
Relations improve through non-aggression pacts, foreign aid (many small
grants beat one large one — returns are concave), and trade volume.

Toward minor nations there's a ladder, each step gating the next: trade
consulate ($500) → embassy ($5,000, also unlocks civilian access) →
non-aggression pact (free) → join the empire (accepted only at the top
relationship level).

Declaring war shifts relations with *every* country — improving with your
target's enemies, worsening with its friends. Minor nations treat attacks on
their neighbours as threats to themselves, so geography sets the diplomatic
blast radius.

**Colony vs. conquest** are genuinely different:

- A **colony** joins whole, keeps partial independence, is a guaranteed
  market, sits permanently at max relations, and its resources go to the
  world market (with your right of first refusal) rather than your transport
  network.
- **Conquest** is province-by-province and behaves like home territory, but
  you must build all the infrastructure yourself. Taking a capital eliminates
  the country and drops its remaining provinces into anarchy — they produce
  nothing, defend but never attack, and **need no declaration of war to
  attack**, which creates an explicit vulture dynamic.

*Release-notes corrections:* you cannot ask another Great Power to join your
empire. Alliance obligations are asymmetric — refusing to help an ally who was
*attacked* breaks the alliance and carries a severe relations penalty, while
refusing an ally who was the *aggressor* breaks it with no penalty.

## Military

**Land.** 27 regiment types across 9 categories and 3 technological eras.
Once a category upgrades, the older type can no longer be built. Militia is
special: it exists everywhere from turn one, costs nothing, cannot leave its
home province, and cannot be railed.

**The tactical battle engine is not optional.** The same engine resolves every
AI-vs-AI battle in the world every turn — the "resolve strategically" option
only suppresses *rendering*. This is the single most important architectural
fact in this document: the engine must be fast and headless-capable from the
start.

Battles use per-regiment initiative rather than side-alternating turns, so
several of your units may act consecutively or the whole enemy army may move
before you. Holding fire enables opportunity fire during enemy activations.
Units track casualties and morale separately; a fully broken unit routs or
surrenders. Generals restore morale. Defenders entrench automatically (−20%)
and forts add −10% per level. Combat engineers tunnel toward walls to breach
them.

*Release-notes correction:* artillery cannot entrench, contradicting the
manual.

**Naval.** Always resolved strategically — there is no tactical naval layer.
Fleets occupy sea zones, which unlike provinces may be shared by hostile
fleets. Missions are patrol, blockade, establish landing site, or move, and
moving precludes a mission. Range is the dominant statistic. Ships are never
upgraded; whole classes go obsolete at once. Undamaged ships anchored at the
capital automatically escort merchants.

**Amphibious landings** need a fleet to spend a turn establishing a beachhead,
which persists only while you hold ships in the zone. The landing force is
capped by the total armaments cost of the ships performing it.

## Victory

One condition: **more than two-thirds of all provincial governor votes** at a
Council of Governors meeting. The council meets roughly every ten years and
first nominates the top two powers on a blend of diplomatic, industrial and
military standing.

Every province in the world carries one vote. Nominees automatically receive
the votes of provinces they own; unaligned governors mostly abstain early,
with participation rising over the game. At the final (1915) meeting all
governors must vote, guaranteeing a winner.

Losing your capital is an immediate loss.

## Known-hard areas

Ranked by risk to a faithful reimplementation:

1. **The trade auction** — central, fully undocumented numerically, and
   emergent (price depends on ranking depends on relations depends on trade).
2. **Relation deltas** — diffuse, many small triggers, easy to get 80% right
   and never notice.
3. **Council nomination and the abstention curve** — get it wrong and games
   end at turn 40 or never end.
4. **Both initiative systems** — strategic (which of two simultaneous
   invasions resolves first) and tactical (per-regiment ordering).
5. **AI**, which is load-bearing rather than optional, since it fights every
   battle in the world.
6. **Difficulty is asymmetric starting advantages**, not AI skill — resource
   density, starting stock, starting relations and army size.
