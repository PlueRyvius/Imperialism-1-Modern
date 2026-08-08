# What the manual actually specifies

The game manual's text is in `imperialism-manual.txt` beside this file. It is
searchable, and it turns out to state several numbers this project had been
guessing at. This document records what it says, in our own words, so findings
can be cited without re-reading it every time.

**Cite this file, not the raw text.** Where the manual and the shipped release
notes disagree, `game-systems.md` says the release notes win — that rule still
holds, and nothing here has been checked against them.

## Resource Development Table

Every tile has a development level from 0 to 3. Level 0 is what a tile produces
before any civilian has worked it. The manual gives the output per turn for a
single tile at each level, and it is **linear, with a slope that differs by
deposit**:

| Resource | Improved by | L0 | L1 | L2 | L3 |
|---|---|---|---|---|---|
| Grain | Farmer | 1 | 2 | 3 | 4 |
| Fruit | Farmer | 1 | 2 | 3 | 4 |
| Cotton | Farmer | 1 | 2 | 3 | 4 |
| Livestock | Rancher | 1 | 2 | 3 | 4 |
| Wool | Rancher | 1 | 2 | 3 | 4 |
| Timber | Forester | 1 | 2 | 3 | 4 |
| Coal | Miner | 0 | 2 | 4 | 6 |
| Iron | Miner | 0 | 2 | 4 | 6 |
| Oil | Driller | 0 | 2 | 4 | 6 |
| Gold | Miner | 0 | 1 | 2 | 3 |
| Gems | Miner | 0 | 1 | 2 | 3 |
| Fish | none | 1 | — | — | — |

The surrounding text puts it three ways that agree with the table: gold and gems
give one unit per level of the mine; coal and iron give double that, so a level
III mine gives six; oil is calculated at double the level of the derrick, again
six at level III.

**Horses are missing from the table.** The Terrain Tiles table lists Horse Ranch
with a civilian worker of "None", so horses behave like fish: level 0 only.

**Nothing is produced by an unopened mine or derrick.** The manual is explicit
that until a mine is built the tile produces no minerals, and until a derrick is
built no oil is produced. That is what the 0 at level 0 means.

## Improvability is a property of terrain, not of the resource

Three terrain types yield but can never be improved: **dry plains** (grain),
**horse ranch** (horses) and **scrub forest** (timber).

This matters because grain and timber *are* improvable elsewhere — on farms and
in hardwood forest. So improvability cannot be selected by resource alone.
`TerrainDefinition.IsImprovable` now carries it, and the *curve* stays keyed off
the resource, which is correct: the two tables answer two different questions.

**The corpus corroborates this without exception.** 481 `deve` records across
five scenarios, and not one on any of the three. Four dry-plains cells carry
fruit and are the only cells in the corpus where a terrain-keyed and a
resource-keyed rule would disagree; no `deve` record touches them. See
`../formulas/development.md`.

## Technology gates improvement levels

The manual carries a full **Benefits of Technology Table** — twenty-eight
entries with names, benefits, prerequisites and approximate arrival dates. Read
for improvement, it gates **every level bar one**:

| Deposit | Level I | Level II | Level III |
|---|---|---|---|
| Grain | Seed Drill | Steel and Iron Plows | Mechanical Reaper |
| Fruit (orchards) | Seed Drill | Steel and Iron Plows | Commercial Fertiliser |
| Cotton | Cotton Gin | Spinning Jenny | Power Loom |
| Wool | Feed Grasses | Spinning Jenny | Power Loom |
| Livestock | Feed Grasses | Barbed Wire | Chemistry |
| Timber | Iron Railroad Bridge | Compound Steam Engine | Dynamite |
| Coal, iron, gold, gems | **none** | Square-Set Timbering | Dynamite |
| Oil | Oil Drilling | Chemistry | Internal Combustion |

**A mine opening at Level I is the exception** — no technology is named for it,
which fits the Miner being one of the four civilians buildable from the start.

The table also gates things this project does not model: rail through particular
terrain (High Pressure Steam Engine, Iron Railroad Bridge, Compound Steam Engine,
Dynamite), the Rancher (Feed Grasses), the Forester (Iron Railroad Bridge), the
Driller and the Refinery and Power Plant (Oil Drilling), and every regiment and
ship type.

Note the shape: technology gates the *improvement*, and in oil's case the
*discovery*. It does not gate extraction from a deposit that is already open.
That is why our importer declares no `requiredTechnology` on any deposit.

**Every player always starts with the first two**, High Pressure Steam Engine
and Seed Drill. That is stated outright and is one of the seven engine defaults
in `../formulas/_index.md` — the only one recovered so far, and it came from
here rather than from a decompiler.

Technology is **bought with cash** on the Investment screen; an investment can
be cancelled until the turn ends and not after. Advances "become available on a
world-wide basis; they cannot be kept secret", and cost money to adopt rather
than to discover. None of the purchasing is modelled — see
`../formulas/technology.md`.

## Prospecting

Coal, iron, gold, gems and oil must be found by a Prospector before any other
civilian can work them, and the four minerals occur only in barren hill and
mountain tiles. Everything else is visible from the terrain type — a cotton
plantation obviously has cotton.

Three further things the manual states outright, all of them load-bearing:

- **Searched-ness is per Great Power and permanent.** "If a Prospector of your
  Great Power has already searched a tile, you see a small pickaxe and a red X."
- **The searchable set grows with technology.** The eye cursor appears over
  barren hills and mountains from the start, and "when your country invests in
  Oil Drilling technology, the eye cursor appears over unprospected swamps,
  deserts, and tundra as well". The Terrain Tiles Table agrees, pairing
  "Driller, Prospector" with exactly those three.
- **An empty search still counts.** The toolbar shows "how many terrain tiles are
  left to search in the country the Prospector is in", a number that can only
  fall if ground found empty stops being worth revisiting.

A new mine or derrick opens at **Level I**, not at the top of its curve.

This is implemented; see `../formulas/prospecting.md`, including the one
consequence — imported worlds can never reach their oil, because nothing
converts a `tech` record and there is no research.

## Collection: depots, ports and the catchment

The same sentence appears for the Miner, Farmer, Rancher, Forester and Driller:
a worked tile must be **on or within one tile of a connected port or rail
depot**, or its output does not reach the transport network. Tiles next to the
capital are exempt.

So the collection points are **depots, ports and the capital** — not raw rail.
The transport-network section spells the rest out:

- Depots and ports, once connected, gather everything in their own tile and in
  adjacent tiles **within your country**.
- **Unconnected depots and ports gather nothing.** A depot's signal post shows
  red when it is not connected.
- **The capital city is always both a connected depot and a connected port.**
- A depot is connected when it has a rail line to the capital — or rail to a
  tile holding both a port *and* a depot, from which goods travel by water.
- **Ports need no railroad.** "In general, a port is always connected." They may
  be built only on coasts and river tiles, and they cost more than depots.
- Build depots and ports **at least two tiles apart**, so each tile is gathered
  by only one structure and coverage is not wasted.

Connections are lost when a province along the line to the capital is taken;
when the province downstream of a river port is lost; or when an enemy fleet
holds **undisputed** command of a sea zone — undisputed meaning you have no
warship of your own present.

Conquered territory works the same way as home territory: resources gathered at
ports and depots there enter your network.

## Fishing

**Rivers, like coasts, produce one unit of fish per turn for adjacent ports.**
Any tile with a river can produce fish, in addition to whatever else it holds.

Consequences worth stating:

- A river port fishes exactly like a sea port. 45 of the corpus's 124 ports have
  no adjacent sea at all.
- The rate is **1 per adjacent water tile**, matching the table's Fish row.
- No civilian improves fishing, which is why fish has no level above 0.

**Open question:** the generated maps mark 15–19% of ocean cells with a fish
deposit, and the historical maps mark none. Under this rule that marker does not
drive fishing at all, so what it means is unknown.

## Starting state

Farms and orchards adjacent to the capital begin at Level I automatically,
before any Farmer exists.

**The shipped scenarios do not author this**, which is evidence for it being an
engine rule rather than against the rule itself: of 350 capital-adjacent farm
and orchard tiles across the ten scenarios only 27 carry a `deve` record, and
`s3` ships 59 records with none of them there. It joins the seven engine
defaults in `../formulas/_index.md`, and it is **not implemented** — see
`../formulas/development.md`.

## Gold and gems bypass the warehouse

They never reach the industry warehouse and cannot be traded. Everything
transported converts immediately into cash. Our commodity model does not yet
distinguish them.

## Workers, labour and food

**Labour per worker per turn:** untrained 1, trained 2, expert 4. Power is not a
commodity and needs no labour to create; on the turn it is generated it adds
directly to the labour pool and is spent before any human labour.

Labour is required to produce — "without some labour you cannot produce fabric".
The manual never states a general rate, but the tutorial walk-through prices one
recipe outright: ordering a unit of clothing "you expend two units of fabric and
use (for this turn) two units of labour". Clothing is 2 fabric → 1 clothing, so
that sentence reads equally as two labour per cycle, one per input unit, or two
per unit of output — and since every recipe in the game is 2:1 by units, the
three give identical answers everywhere they can be checked. See
`../formulas/production.md` for what that does and does not establish.

The labour total is one number on the screen border, drawn down by every
production dialog, against per-facility capacity bars that do not interact:
labour is pooled per country, capacity is not.

**Food.** Every worker eats one unit per turn, and each enjoys only one type:

- **Half** want grain, **a quarter** fruit, and the remaining quarter livestock
  **or** fish, either being acceptable. The manual's own advice for satisfying
  that last group is to build ports and transport fish.
- A worker that cannot get its type eats **canned food** instead and works
  normally — "workers do not get sick when they eat canned food".
- A worker forced onto an undesired type **reports sick and stays home**,
  performing no labour that turn.
- Workers eat any food rather than starve.

Canned food may also be traded, and is used to recruit more workers.

## Trade

The manual specifies the world market far more tightly than it specifies anything
about prices, which it never gives at all. Quoted here because
`../formulas/trade.md` builds on all of it.

**The shape of a turn's trading.** Every Great Power and Minor Nation submits
offers to sell and bids to buy on the Bid and Offers screen, naming quantities and
never a price: "it is impossible to predict the final price for this turn, because
the buy bids and sell offers which determine the price come from all the countries
in the game, not just from your own Great Power."

**Timing.** Goods bought "appear for your use in the Industry screen next turn";
goods sold "are deducted from the warehouse of your Industry screen". The screen
shows stock "after deduction of the commodities you have ordered for production on
the Industry screen", because "you cannot sell items you do not own or that you
have ordered industry to use this turn" — **industry gets first claim**.

**Offers pass down a ranked list, a part at a time.** All of a seller's offer "first
appears as an Offer Sheet to Great Britain, the most favoured trading partner of
Belgium, which bid to buy coal this turn. If the ruler of Britain decides to buy
only some (or none) of the offered coal, then the coal remaining… passes to the next
coal-bidding country on the list of Belgium's favourite trading partners. This
process continues until the bidders purchase all the offered coal or until there are
no more coal bidders." A buyer "can accept any number up to the amount offered".

**The ranking is relations and subsidies.** "Subsidised prices and improved
diplomatic relations both affect the order in which countries receive offers to
buy." A trade subsidy changes the price "by the percentage amount of the subsidy",
favouring the other country at both ends.

**Price direction, and no magnitude anywhere.** The figure shown is "the world
market prices for the commodities traded during the previous turn. This price is a
starting point for this turn's price, which may go higher or lower depending on
supply and demand. If, during this turn, demand for a commodity is stronger than the
supply, the price rises. If the reverse is true, the price falls. If supply and
demand are closely matched, the price this turn remains much the same as last turn's
price."

**What cannot be traded.** "Even though food resources cannot be traded on the world
market, you should consider transporting extra food whenever you can." Gold and gems
"never reach the industry warehouse and they cannot be traded". Canned food can be:
"of course, you may trade for canned food on the world market."

**Merchant marine.** "The merchant marine number represents the total cargo holds
available in all the merchant ships owned by your Great Power. Each cargo hold can
carry one unit of any trading commodity." The binding rule is that "each cargo hold
can be used only once per turn", and it limits buying as much as selling: "if your
merchant marine number is four and you sell four units of clothing to a Minor
Nation, none of the bids you entered this turn can be filled. **You can buy nothing
if you have no merchant marine to move the cargo.**"

**Who carries.** "No Minor Nation owns merchant marine. When you trade with a Minor
Nation, as either buyer or seller, you can be sure that your merchant marine is
required." Between Great Powers, "the buyer always picks up the commodities. If a
bidder has no remaining cargo holds available, the bidder is not permitted to accept
the deal, and the items are offered to the next bidder on the list."

**Holds spend in a fixed commodity order.** "IMPERIALISM always uses an established
order when expending the Great Powers' merchant marine for trade. This commodity
order is shown on the Bid and Offers screen from top to bottom. Clothing deals, for
example, are always considered prior to all other deals because clothing is the
first item in commodity order. **Reserving some cargo holds for later deals becomes
an important skill.**"

**Ships.** Built at the shipyard for materials and never for cash, usable the turn
after they are ordered, with no upkeep — unlike army units. Five merchant classes
eventually become available (Trader, Indiaman, Steamship, Clipper, Freighter) and
eight warships, four fast and four battleships. Merchant ships "do not appear on the
terrain map"; each "adds its cargo capacity… to the total you have available each
turn for trade", and "if you build faster ships, the average sailing speed of your
merchant marine increases, making blockade and interception much more difficult for
hostile navies."

**The manual prices nothing in trade.** Its only cash figures anywhere are gold at
$200 a unit, gems at $500, a consulate at $500, an embassy at $5,000, and foreign-aid
grants at $1,000 and $10,000. Every commodity price in this project comes from a
transcription of the game's own screen instead.
