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

The manual names the technologies precisely:

| Gate | Technology |
|---|---|
| Mine level II | Square Set Timbering |
| Mine level III | Dynamite |
| Prospecting for oil at all | Oil Drilling |
| Derrick level II | Chemistry |
| Derrick level III | Internal Combustion |
| Rancher unit buildable | Feed Grasses |
| Forester unit buildable | Iron Railroad Bridges |

Note the shape: technology gates the *improvement*, and in oil's case the
*discovery*. It does not gate extraction from a deposit that is already open.
That is why our importer declares no technology requirement on any deposit.

## Prospecting

Coal, iron, gold, gems and oil must be found by a Prospector before any other
civilian can work them, and the four minerals occur only in barren hill and
mountain tiles. Everything else is visible from the terrain type — a cotton
plantation obviously has cotton.

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
