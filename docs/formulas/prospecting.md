# Prospector discovery

## Summary

Five deposits are on the map and invisible to the country that owns the ground:
coal, iron, gold, gems and oil. A Prospector has to search the tile before any
other civilian may work it. Before this existed, every marker was visible from
the moment the map loaded, a Miner could open any mine on turn one, and the most
numerous civilian in the shipped corpus — 62 Prospectors against 30 Miners — had
nothing whatever to do.

This is the least invented thing built here so far. The manual states almost all
of it outright, including the one technology gate and the fact that a search
marks a tile whether or not it finds anything.

## Confidence

`inferred`, and unusually close to `verified` for a mechanic with no
disassembly behind it. **No new guess.** The work duration is the same
per-type `workTurns` [development.md](development.md) already flags; a search
reuses it rather than adding a second unmeasured number.

| Claim | Support |
|---|---|
| Coal, iron, gold, gems and oil must be found first | **manual**, stated outright |
| Everything else is read off the terrain | **manual** — "you know that cotton is present at every cotton plantation" |
| Barren hills and mountains are searchable from the start | **manual** — "the eye appears only over those tiles at the beginning of the game" |
| Swamp, desert and tundra become searchable with Oil Drilling | **manual**, stated outright |
| Searched-ness is per Great Power and permanent | **manual** — "if a Prospector of *your Great Power* has already searched a tile, you see a small pickaxe and a red X" |
| A fruitless search still marks the tile | **manual**, by implication — the toolbar counts "how many terrain tiles are left to search", which only decreases if empty ground counts |
| A new mine opens at Level I | **manual** — "when a Miner finishes opening a new mine it produces at Level I" |
| The four minerals occur only in barren hills and mountains | **manual**, and **corpus-corroborated** |
| A built mine is visible without a survey | **inference**, and the one this document leans on — see Conquest |

## The corpus agrees, from the other direction

The importer marks five terrain codes as searchable. Counting the cells that
carry them across the ten shipped scenarios gives **4,449** tiles a Prospector
may search without any technology — which is exactly the 2,860 barren hills plus
1,589 mountains that [development.md](development.md) counted while establishing
something else entirely. Two independent walks over the corpus, same number.

The four historical maps agree with each other exactly, as they should:

| | searchable now | behind Oil Drilling |
|---|---|---|
| `s1`, `s3`, `s13`, `s14` | 598 | 414 |
| `s9`, `s12` | 371 | 428 |
| `s10` | 382 | 425 |
| `s11` | 364 | 448 |
| `s15` | 368 | 452 |
| `s5` (generated) | 201 | 517 |

`TheWholeShippedCorpusConvertsWhenItIsConfigured` pins every row.

**Most of that ground holds nothing.** 449 of 2,860 barren hills and 346 of
1,589 mountains carry a marker at all, so roughly six searches in seven come back
empty. That is the mechanic rather than a defect in it, and the reason a
fruitless search has to be a first-class outcome instead of an error.

## Design

### Discovery gates improvement, and deliberately not extraction

A Miner's or Driller's work order onto unsearched ground is refused.
`ExtractionPlanner` is **untouched**, because the manual's Resource Development
Table already does the job: coal, iron and oil yield **0** at level 0, gold and
gems the same — "until a mine is built the tile does not produce minerals". An
undiscovered deposit pays nothing whether or not anything checks.

One gate, in one place. The catch is worth naming rather than leaving to be
discovered: a content package that gave one of the five a non-zero level-0 yield
would collect it without any search. Nothing shipped does, and teaching
extraction about discovery as well buys nothing for the content that exists.

### Two attributes, from two tables, neither subsuming the other

The same split [development.md](development.md) established for improvement:

- **`ResourceDefinition.RequiresDiscovery`** — which deposits hide. From the
  Prospector paragraph.
- **`TerrainDefinition.Prospecting`** — which ground is worth searching, and what
  it costs in knowledge. From the Terrain Tiles Table.

The terrain side is a **nullable rule object, not a bool beside a nullable
technology**. "Cannot be searched" and "searchable, requiring nothing" are
different answers and a bool pair spells them the same way — barren hills need no
technology and a farm needs nothing because nobody may look at all.

### The work kind lives on the civilian type

`CivilianTypeDefinition.Work` is `Improve` or `Prospect`. The order stays a bare
(unit, cell) pair, because that is where the original puts the distinction: the
cursor a player sees is decided by the unit they have selected. A Prospector
never improves and no other civilian ever searches.

It also keeps the string "Prospector" out of Core, the way `PortFishing` keeps
"fish" out.

### Storage is a packed per-country bitset

Searched-ness is one bit per (country, cell). At the 64,800-cell scale
regression across 23 countries that is ~187 KB as a `ulong[]` against 1.5 MB as
`bool[]`, and packed is what `RailConnectivityIndex` already does.

**Nothing is seeded.** A cell that has been developed already has workings on it,
and a mine is a structure you can see from outside. So what a country may act on
is `CanSeeDeposits` — *searched, or built on* — and the development level does the
second half of that job by itself:

```
visible = HasProspected(country, cell) || GetCellDevelopment(cell) > 0
```

That covers `s1`'s 52 barren-hill and 6 mountain `deve` records without a
seeding pass, and it is why the 1997 format having no record for who-searched-what
costs nothing. `HasProspected` stays the honest record of who actually *looked*.

## Oil is gated, and that makes it unreachable

The importer now emits **one** technology, `technology.oil-drilling`, and gives
it to nobody. A 1997 `tech` record is not converted, and there is no research
system. So **no imported world can ever prospect swamp, desert or tundra**, and
its oil is unreachable.

That is the manual applied honestly rather than a gap. It says a Prospector
cannot look for oil until the country invests; nothing here can invest; so
nothing here may look. The fair start already has no refinery for exactly this
reason. The alternative — leaving the oil ground open because our research is
missing — would invent permission the original never granted, which is the one
thing this project has repeatedly agreed not to do.

A scenario can still grant it outright through `InitialCountryTechnologies`,
which is what makes the gate testable and what a hand-authored world would use.
**Re-read this section when research lands**; the gate is already in the content
and should need no code change.

## Conquest

Capturing a working mine hands over a working mine: the level is above zero, so
the new owner can see it and may deepen it. Capturing bare ground with unfound
coal under it hands over bare ground, and they must still send a Prospector.

Both fall out of the one rule above rather than needing a rule of their own, which
is the argument for reading visibility off the development level instead of
seeding a bit. Nobody has surveyed the captured tile — `HasProspected` stays false
for the new owner — and it would matter again if the mine were ever abandoned back
to level 0.

Note the refusal is `DepositNotYetDiscovered` and not `AlreadyFullyDeveloped`. A
hidden deposit is not merely unworkable to its owner — as far as they are
concerned it is not there — so it cannot make a tile look finished either.

## Pseudocode

```text
Development phase, when a job finishes:

    if the civilian's type prospects:
        mark (country, cell) searched
        report every deposit on the cell that requires discovery
                — possibly none, which is the usual answer
    else:
        raise the cell's development level by one

Legality of a work order, after the shared entry rules
(exists, yours, idle, on the map, on land, your territory):

    prospecting:
        refuse if the terrain declares no prospecting rule
        refuse if that rule names a technology the country lacks
        refuse if this country has already searched the tile
        — and never refuse for the tile being empty

    improving:
        refuse if the terrain is not improvable
        for each deposit this civilian's type works:
            if it requires discovery and the tile is unsearched: remember and skip
            if the level is below the top of its curve: allow
        refuse: not yet discovered, if any were skipped
                already fully developed, if any were workable
                nothing here this civilian works, otherwise
```

Searching costs nothing, like improving: the civilian was paid for when it was
built and the manual prices no materials for the work.

## Where implemented

- `CivilianWorkKind` and `CivilianTypeDefinition.Work` (`CivilianUnits.cs`).
- `ProspectingRule` and `TerrainDefinition.Prospecting` (`Definitions.cs`).
- `ResourceDefinition.RequiresDiscovery` (`EconomyDefinitions.cs`).
- `WorldState.HasProspected` / `SetProspected`, the bitset, and the seeding loop
  beside `_cellDevelopment` in the constructor (`WorldDefinition.cs`).
- `DevelopmentPlanner.LegalityOfProspecting`, the fork in `LegalityOfWork`, and
  the discovery check inside `LegalityOfImprovement`.
- `CellProspectedEvent`, and four `CivilianOrderRefusal` members:
  `TerrainCannotBeProspected`, `ProspectingTechnologyNotKnown`,
  `AlreadyProspected`, `DepositNotYetDiscovered`.
- `.iworld` **v14**: `terrains[].prospecting`, `resources[].requiresDiscovery`,
  `civilianTypes[].work`, with a v13→v14 migration to a world where nothing
  hides and nothing searches — which is exactly how v13 behaved.
- `LegacyWorldConverter` stamps the five terrain codes, marks codes 3, 4, 6, 21
  and 22 as hidden, gives `civilian.prospector` the `Prospect` kind, and emits
  Oil Drilling.

## Test data

`tests/Imperialism.Core.Tests/ProspectingTests.cs` pins a search revealing its
deposit; a fruitless search still marking the tile; a Miner refused before and
accepted after, with the new mine paying 2 in the same turn; a second search
refused; oil ground refused without Oil Drilling and accepted with it; a scenario
granting it; a farm refusing a Prospector; a world declaring no prospectable
ground; only a Prospector searching; one country's survey not being another's; an
authored mine workable without a search; the conquest case; and the undiscovered
deposit yielding nothing because its curve starts at zero.

`tests/Imperialism.LegacyImport.Tests/LegacyWorldConverterTests.cs` pins all
seventeen terrain codes against the manual's prospectability one by one,
including which three are gated; the five hidden deposits against the other
eight; and the work kind per civilian. The whole-corpus test pins the searchable
counts in the table above and their 4,449 total.

## What a hundred turns looks like

`EconomySoakTests` gains a third world: the farming one plus four columns of
barren hills per power — two carrying coal, one a depot to lift it, and **one
bare**, because a run where every search succeeds is not the game. Two runs
differ only in whether the Prospectors are ever told to look.

```
                    searched  found  mines  gathered  levels at 100
Prospectors idle           0      0      0    16,240            168
Prospectors working       28     14     14    17,486            182
```

First search on turn 4, first mine on turn 8. The workforce, the grain and the
sickness are **identical** in both runs — 49 workers rising to 77, and nobody
ill after turn 4 — which is the point: the discovery chain is cleanly separable
from the food chain, and the whole of its effect is 14 mines and 1,246 coal that
would otherwise have sat in the ground for a century.

**These figures were stale and are now re-run.** They were published against a
119-worker baseline that the technology gates had already invalidated, and the
work duration has since moved from 1 to 3 as well. Neither recovery touches
anything this document argues: the searched, found and mines columns are
unchanged at 28, 14 and 14, because how long a search takes and what it costs to
mine afterwards do not change what is buried.

The control's other number is the useful one: **700 refusals**, one per power per
turn, every one a Miner turned back from hills its country could not see.

## Open questions

- **Research**, without which the oil gate can never open in imported content.
- **The Driller has no ground to work.** It is declared, it improves oil, and no
  world it can reach will ever reveal any. It becomes real with research.
- **Prospecting abroad.** The manual's Checklist for Working Abroad sends a
  Prospector into a Minor Nation once an embassy exists. Civilians are narrowed
  to their own territory until diplomacy is modelled, so this is refused.
- **Gold and gems bypass the warehouse** and convert straight to cash. Our
  commodity model does not distinguish them, so a discovered gold mine currently
  fills a warehouse with gold. Blocked on there being money at all.
- **Whether the original re-rolls anything.** We treat a search as revealing what
  the map already holds, deterministically. If the original decides the contents
  at search time rather than at map generation, the shipped markers would be its
  answer anyway — but this has not been checked in the binary.
