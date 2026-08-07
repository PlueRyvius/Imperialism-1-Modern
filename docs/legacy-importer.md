# Phase 1 legacy importer

`Imperialism.LegacyImport` converts preserved `.map`, `.scn`, and optional
`.inf` documents into the versioned `.iworld` viewer slice. It is deliberately
conservative: only evidenced fields become modern content, while unsupported
information is counted in a stable report and remains available in the source
files. No opaque legacy record or map trailer is copied into `.iworld`.

The converter imports terrain, known resources, province and sea-zone regions,
settlement sites, ownership, local river shapes, reciprocal rails, capitals,
names, year, warehouse stock, production capacity, and the INF title. Unknown terrain codes use an explicit numeric
placeholder key because every modern cell requires terrain. Unknown resource,
town, and river codes create warnings and no inferred feature. Conflicting
owners, references that cannot form valid modern content, duplicate capitals,
conflicting years, or missing dimensions/year block output.

Every converted package defines the standard 23-commodity catalog independently
of which deposits happen to occur on that map. The 13 known legacy deposit
codes become explicit resource definitions pointing to their raw commodities;
notably forest yields timber and cattle yields livestock. Since `.iworld` v5
every imported deposit also carries a yield curve indexed by development level.
The 1997 `.map` stores which deposit sits on a cell and never how much it gives,
so the curve comes from how the original behaves rather than from the file: dug
deposits (coal, iron, oil, gems, gold) give nothing until improved and open at
two, harvested ones already give one untouched, and both double per level. No
deposit declares a technology requirement, because which technology gates which
deposit has not been measured. See `formulas/extraction.md`.

`deve` records are converted rather than deferred: the record is `[cell, level]`
and every level in the corpus is 1, 2 or 3. A cell developed more than once —
`s1` does it three times — keeps the highest level and reports a warning.

**Terrain now carries attributes**, not just a key. Each of the seventeen legacy
codes is stamped with its display name from the manual's Terrain Tiles Table and
with whether a civilian can improve it — false for ocean, towns, capitals and
the manual's three barren cases (dry plains, horse ranch, scrub forest). An
unknown code keeps its numeric placeholder key and is never improvable: nothing
is known about the ground, so letting a worker onto it would invent a rule about
a tile we cannot name. Each deposit also names the civilian that raises it, from
the Resource Development Table; fish and horses name none.

**Terrain also carries whether a Prospector may search it.** Barren hills and
mountains are open from the start; swamp, desert and tundra are gated on Oil
Drilling; the other twelve codes and every unknown one hide nothing. Coal, iron,
oil, gems and gold are marked as requiring discovery, and `civilian.prospector`
is the one type whose work is to search rather than to improve.

**The importer declares the manual's whole technology table**, twenty-eight
entries in printed order, because a `tech` record is `[country, id]` with a bare
1-based index into it and nothing naming it. That reading was falsified against
the corpus before being built on: 380 authored levels are permitted by their
owner's technologies and 4 are not, with `s3` — whose powers hold *unequal* sets
— producing no contradiction at all. `s3` also repeats six of its own grants,
which are warned about and dropped exactly as a repeated `deve` cell is.

Each deposit carries the ladder that table implies, and every power gets High
Pressure Steam Engine and Seed Drill through `startingDefaults`. The Great
Powers are identified by their `labo` records — the one record that names them
and only them, seven in every shipped scenario — rather than guessed at.

Every power also starts with a **warehouse**: the manual says a power begins with
"initial stockpiles of lumber and steel", which no skirmish's `ware` record
supplies, so `startingDefaults.inventory` does. An explicit `ware` record still
beats it, the same way `labo` beats the default workforce. The commodities are
the manual's and the quantity is a guess — and it is the guess that decides
whether a small network is survivable at all.

`tran` records become each power's starting transport capacity, one number for
the whole network. Seven scenarios carry them and four carry none; `s12` gives a
network to exactly one of its seven powers. A scenario carrying none leaves every
power on the engine's default, which is **a guess** — see
`formulas/transport.md`, and note that it decides whether an imported skirmish is
playable at all rather than merely how comfortable it is.

`cash` records become each power's starting treasury, `[country, amount]` — the
same two-field shape as `tran`. Five scenarios carry seven apiece and five carry
none; `s3` gives its own seven powers 1,500 to 15,000, which is as clear a
demonstration as the corpus offers that these are authored situations. A scenario
carrying none leaves every power on the engine's default, which is **a guess**.

Gold and gems are the only two commodities the importer prices in cash — $200
and $500 a unit, both **stated outright in the manual** — because they are the
only two that never reach the warehouse. See `formulas/money.md`.

Oil remains unreachable in imported content: no scenario grants Oil Drilling and
there is no research system, so swamp, desert and tundra can never be prospected.
That is the manual's own rule rather than a gap. See `formulas/technology.md` and
`formulas/prospecting.md`, which record the corpus counts the tests now pin.

`civi` records are converted rather than deferred. The record is `[type, cell]`
and names **no owner** — the original reads it off the province the cell sits
in, and the corpus supports that without exception: all 210 records across the
ten scenarios stand on owned land, and every owner holds a capital. Off-map,
ocean, unowned-land and unknown-type records are errors. Stacking is allowed;
`s1` gives one power two Miners. The importer declares seven civilian types: the
six the corpus uses, plus the Oil Driller, which no `civi` record is but which
the development table names as oil's improver. See `formulas/development.md`
for how the six were identified.

`labo` records become the starting workforce, and the importer emits the
standard feeding rules alongside them: the grain / fruit / grain /
livestock-or-fish cycle, canned food as the substitute, and 1/2/4 labour by
grade.

`rail` records are converted as **depots**, not as track — the map's rail byte is
where the lines come from. They are a strict subset of railed cells and no two
sit within two tiles of each other, both of which the manual predicts. A depot
on a cell with no rail is a warning; no original does it, though our own
generated `s5` does it on all 32 of its depots.

`port` records are converted too. Each names a land cell; repeats collapse with
a warning. The importer also checks that a port touches sea or a river, using
**wrapping** east-west adjacency because the 1997 grid wraps and
`Imperialism.Core`'s does not — `s3` has a port whose only water is across the
seam. A failure is a warning, not an error: it means our adjacency is wrong
before it means the map is.

The importer also emits the evidenced standard industrial catalog: seven
limited legacy facilities, unlimited food processing, and twelve recipes.
Cotton and wool are separate fabric recipes; hardware and armaments share metal
works capacity; lumber and paper share lumber-mill capacity; fish and livestock
are separate canned-food recipes. `ware` records become sparse positive initial
inventory and `capa` records become sparse capacity for their named facility.
Zero quantities are omitted. Unknown warehouse commodity or industry codes are
warnings with no inferred data; malformed records, unknown countries, and
duplicate country/item pairs block conversion.

The historical mill/factory upgrade sequences are construction rules, not a
validation rule for imported state. Tutorial scenarios contain capacities 3,
5, 6, and 7, so the modern capacity value is an unrestricted checked integer.

Keys are deterministic. Known terrain and resources use semantic keys;
countries, provinces, and sea zones retain padded numeric legacy IDs. Only
`zone` IDs referenced by ocean cells become modern sea zones. The ocean byte
uses a combined region namespace, so its scenario zone ID is decoded by
subtracting the country namespace size. Other zone
records, all unsupported scenario tags (including legacy `rail` records), INF
briefings and metadata, scenario trailing bytes, and map trailer records are
reported as deferred information.

## River codes

Legacy map byte 2 is a local path-shape code, not a reciprocal edge mask. The
following 32 mappings come from the supplied `MapDecode.rtf` table, checked
against every nonzero value in the ten-map corpus. Paths are undirected.

| Code | Endpoints | Code | Endpoints |
|---:|---|---:|---|
| 11 | NorthEast–SouthEast | 43 | NorthEast–Source |
| 12 | NorthEast–SouthWest | 44 | EastUpper–Source |
| 13 | NorthEast–WestUpper | 45 | EastLower–Source |
| 14 | NorthEast–WestLower | 46 | SouthEast–Source |
| 15 | SouthWest–EastUpper | 47 | SouthWest–Source |
| 16 | SouthWest–EastLower | 48 | WestUpper–Source |
| 17 | EastUpper–WestUpper | 49 | WestLower–Source |
| 18 | EastLower–WestUpper | 50 | NorthWest–Source |
| 19 | EastUpper–WestLower | 51 | NorthEast–Mouth |
| 20 | EastLower–WestLower | 52 | EastUpper–Mouth |
| 21 | EastUpper–NorthWest | 53 | EastLower–Mouth |
| 22 | EastLower–NorthWest | 54 | SouthEast–Mouth |
| 23 | SouthEast–WestUpper | 55 | SouthWest–Mouth |
| 24 | SouthEast–WestLower | 56 | WestUpper–Mouth |
| 25 | SouthEast–NorthWest | 57 | WestLower–Mouth |
| 26 | SouthWest–NorthWest | 58 | NorthWest–Mouth |

The source table contains sequential-label typos at 49 and 55. Their corrected
values are `WestLower–Source` and `SouthWest–Mouth`, respectively. The sequence
on either side and the complete eight-position source/mouth runs establish the
correction. Cross-cell river connectivity is intentionally deferred.

## Command line

```text
dotnet run --project tools/Imperialism.LegacyImporter -- \
  --map /path/to/s1.map --scenario /path/to/s1.scn \
  --inf /path/to/s1.inf --output /path/to/s1.iworld \
  --package-key s1 --report-json /path/to/s1-report.json
```

The CLI writes `.iworld` only when the report has no errors. Warnings do not
block output. Human-readable diagnostics go to standard output; optional JSON
contains only counts and diagnostics, never source records.
