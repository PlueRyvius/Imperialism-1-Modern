# Scenario data semantics

What the `.map` and `.scn` *fields mean*, as distinct from how they're laid
out on disk (that's `file-formats.md`). Everything here was established
empirically against the ten original scenarios and is reproducible with
`tools/inspect_assets.py`.

Claims are marked **verified** (checked against real data, stated with the
check) or **inferred** (consistent with the data but not proven).

## Province ownership is scenario state, not arithmetic

**This is the most important correction in this document, and getting it
wrong would be a silent, catastrophic bug in the world model.**

It is tempting to conclude that a province's owner can be derived from its
id — `province_id >> 4` yields a plausible country index, and a naive check
"is `province_id >> 4` a valid country id?" passes for all 213 provinces.

**That check is near-vacuous** and the rule is false. Province ids run 0–348
across 23 countries, so `>>4` always lands in 0–21, which is always a valid
country id. Tested against the actual owner recorded on each map cell, the
rule holds for only **20 of 213 provinces (9.4%)**.

Two independent reasons it fails:

1. **Province ids are allocated in contiguous, variable-length runs per
   country.** France holds 22 provinces (ids 0–21), so its run spills past
   the 16-boundary and its later provinces (Orleanais, Berry, Burgundy,
   Limousin, Auvergne, Corsica) compute to "block 1". No fixed shift can
   work when the runs aren't a fixed width.
2. **Ownership differs from the id block by design.** In the 1882 scenario,
   Alsace (id 14) and Lorraine (id 13) sit inside France's id run but are
   owned by Germany — historically exact, since Germany annexed them in
   1871. The id block reflects a province's *historical/default* grouping;
   who holds it is per-scenario state.

**The rule:** read province ownership from the map — every land cell carries
its owning country in its nation byte. **Verified:** all 213 provinces in
`s1` have a single, internally consistent owner across all their cells (zero
provinces show a conflicting nation byte).

The id block is still useful as a *hint* about a province's default
grouping. It must never be used as ownership.

## Map cell fields

- **Nation byte is duplicated.** Bytes 3 and 4 are byte-identical in all
  6,480 cells of `s1` (**verified**). One is presumably a stale mirror; we
  preserve both and read from the first.
- The nation byte holds a **country id on land and a combined region id on
  ocean**. Ocean values follow the country namespace, so the scenario `zone`
  id is `raw_region - country_namespace_size`. All ten maps use 23 country
  slots: their ocean values decode this way to named `zone` records with no
  misses (**verified**). Land cells in `s1` use exactly ids 0–22, matching
  the 23 `cnam` records.
- Ocean cells use province id **65535** as a null marker.
- `town_type` distinguishes settlements: 34 = village, 35 = capital,
  **33 = a minor nation's capital**. `s1` has 190 villages and **exactly 23
  capitals — one per country** (**verified**).
- **Every province holds exactly one town cell** — 213/213 in `s1`, 120/120 in
  each generated world (**verified**).
- Capitals are marked via `town_type`, not via the terrain code. The
  terrain value for "capital" never appears in the European maps.

## Scenario record semantics

**Cell references.** `deve[0]`, `rail[0]`, `port[0]`, and `civi[1]` are
**linear cell indices** into the row-major grid (0-based, `y * width + x`),
not coordinate pairs. **Verified** in `s1`: all 320 `deve`, 76 `rail`, 49
`port`, and 35 `civi` references are in range *and* land on non-ocean cells —
a result vanishingly unlikely by chance, and the reason this is stated as
verified rather than inferred.

**Every capital reaches the sea.** All 184 capitals across the eight shipped
scenarios — Great Power (`town_type` 35) and minor (33) alike — sit either on
the coast or on a river. **Not one is landlocked**, and the rule holds on the
historical maps and the generated ones equally.

This is what gives a nation its port: the dock grows from the capital and
follows the water out to open sea, along the coastline or down a navigable
river. A landlocked capital has nowhere for ships to tie up and the country is
cut out of naval trade. A generated world that sited its towns for shelter put
all 23 capitals inland and had no docks at all.

**A `port` record is not the capital's dock.** It is an additional harbour, and
the shipped data constrains it three ways, each silent across all nine files:

- **On water** — coastal or on a river; all 124 shipped ports, no exceptions.
- **Not on high ground** — never wool hill, hill or mountain.
- **Not on a capital** — their cells are `town_type` 0 or 34, never 33 or 35,
  and in the generated worlds 11 of 13 are on 0, ordinary coastal ground with
  no settlement. They belong to Great Powers only, 0–3 apiece.

**A work only ever sits on a Great Power's cell.** Every `deve`, `rail`,
`port` and `civi` record in all nine originals — over 700 of them — names a
cell whose nation byte is 0–6. Not one sits on a minor nation's cell.
**Verified** silent across the whole corpus.

This is not a stylistic habit, it is load-bearing. The engine resolves a
work's cell to an owning country and uses that to index the **7-slot** Great
Power table at `006A4370`, with no range check; a minor's id (7–22) reads past
the end and the engine calls a method on the garbage it gets back. That is the
fault at `0051465C` in `UMap.cpp`, and it is what a generated world crashed on.
Minor nations have no industry of their own in this game, which is why the
shipped data looks the way it does. See `docs/disasm/README.md` for the table
and its 24-slot neighbour.

**Province references.** `army[0]` is a **province id**, not a country id.
**Verified:** all 299 `army` records in `s1` reference valid `pnam` ids,
covering all 213 provinces.

**The generator's `army` block is a fixed three-role pattern.** `s11` and
`s15` — the shipped generated worlds whose army block looks unedited — carry
exactly 166 `army` records each, and the same ones:

| records | type | count | placed on |
|---|---|---|---|
| 120 | `base + 0` | 4 | every province |
| 23 | `base + 2` | 2 power / 4 minor | every capital province |
| 23 | `base + 7` | 1 | every capital province |

`base` is the era's first unit type, and the three offsets are the same three
roles either side of the era boundary: 1820 fields Minutemen / Regulars /
Artillery, 1882 the same ladder at Militia / Rifle Infantry / Siege Artillery.
`s9` and `s12` add a few type-34-province records on top; `s1`, hand-authored,
agrees on the backbone (one type-8 record per province, plus 10s and 15s).

Capital provinces are identified by their town cell's `town_type`: 35 for a
Great Power, 33 for a minor. **A power's capital garrison is the *smaller*
one** (2 against a minor's 4).

This is worth holding to: field 1 is a small closed vocabulary, not a free
draw over the era roster. A generated world that drew randomly produced a
type-3 (`Grenadiers`) record — a value that appears in **no** shipped
scenario, in a field whose observed range is otherwise 0–15.

**Sea zones and ports share the `zone` tag.** In `s1`, ids 0–39 are sea
zones ("North Atlantic", "English Channel") and 40–62 are **port cities**
("Chatham", "Trieste") — one per country. Consumers must not assume every
`zone` record is a body of water.

**Playable powers are the low ids.** Only countries 0–6 receive `cash`,
`tran`, `labo`, and `tclr` records — these are the seven Great Powers. Ids
7–22 are minor nations (**verified**: 7 records each of those tags).

**Industrial setup is a dense power-by-industry table.** Every binary scenario
contains exactly 42 `capa` records: seven Great Powers times six industries,
with no duplicate pair or invalid country/industry reference. The corpus has no
industry-6 oil-refinery capacity record. Historical campaigns use documented
upgrade steps, but tutorial scenarios also use 3, 5, 6, and 7; therefore
capacity values are state, not an enum of legal upgrade levels. `ware` records
use commodity codes 0–20 and sparse nonnegative quantities; the audited corpus
contains no duplicate country/commodity pair or invalid reference.

## Corpus notes

- All ten `.map` files are exactly 309,312 bytes. **`s0.map`, `s13.map` and
  `s14.map` are byte-identical** — deduplicate before treating them as
  distinct corpora.
- Grand campaigns (`s0`, `s1`, `s3`, `s13`, `s14`) carry the full dataset:
  23 countries, 213 provinces, 63 zones, 253 relation pairs. Tutorials
  (`s9`–`s12`, `s15`) omit most names and lean on `zone`, `army`, `emba`.
- The **fish** resource appears only in tutorial maps (680–810 cells); the
  European maps contain none.
- `town_type` **33 marks a minor nation's capital** (**verified**). Each of the
  five generated worlds has exactly 16 of them, one per minor nation (ids 7-22),
  alongside exactly 7 type-35 capitals, one per great power. The historical maps
  use none: there, all 23 countries get a type-35 capital. 97 villages + 7
  capitals + 16 minor capitals = 120 = one town per province.
- Every one of the 24 known scenario tags appears somewhere in the corpus;
  there are no dead tags. `coun` is rarest: one record in `s0` and four in
  `s1`. Every scenario has one `flag`; most have one `year`, while `s10` and
  `s15` each contain two identical `year` records.

## A ship's sea zone is not the map's ocean zone byte

**Verified, and it corrects the claim above that the ocean nation byte is "a
sea-zone id".** It is *a* sea-region id, but not the one `zone` records and
`ship` records use. The two numberings are unrelated.

Identifying a map region by the land nations its cells border, in `s1`:

| sea | `zone` record | map ocean byte |
|---|---|---|
| English Channel | 14 | 48 |
| Black Sea | 15 | 23 |
| North Sea | 10 | 63 |

No constant offset fits, and testing one produces nonsense — it puts the Black
Sea in western Europe and the Dardanelles west of Portugal. The map carries
ocean bytes up to 78 while `zone` records stop at 62, and the 23 zones numbered
0-22 appear on no ocean cell at all.

Nothing in the four files maps one space onto the other. The consequence for
tooling is concrete: **a fleet can be named but not located.** The editor lists
ships by zone name and does not draw them.

Recovering the correspondence is open work. The most promising route is the
`port` records, since `zone` ids 40-62 are port cities and `port` records point
at coastal cells, which would at least tie the port zones down.

## Unit records

**Verified.** `civi` is `[type, cell]`, not `[owner, cell]`: field 0 matches the
cell's owning nation in only 4 of 35 records in `s1` — chance — while each
country receives one of each type, and every Forester stands on forest, every
Rancher on wool hill or cattle ranch. **A civilian's owner is whoever owns the
cell it stands on**, which means moving one across a border changes its side.

`army` is `[province, type, count]` and `ship` is `[country, type, zone, count]`.
The type tiers track the scenario's year, which is a useful cross-check on both:
1820 fields Minutemen, Regulars, Hussars and Ship-of-the-Line; 1882 fields
Militia through Siege Artillery, Paddlewheelers and Ironclads; 1848 spans the
two. A type picker that ignores the year will offer units the scenario has never
heard of.

`deve` is `[cell, level 1-3]`. `ware` is `[country, commodity, amount]` with
field 1 matching `COMMODITY` exactly. `capa` is `[country, 0-5, capacity]` —
probably industry, but `INDUSTRY_TYPE` has seven entries and only six values
appear, so treat it as **inferred**.

## The plaintext scenario form

Seven scenarios ship an extensionless file (`Scenario/s9` through `s15`)
containing **CR-delimited plain text**, one record per line:

```
tech 0 1
zone 83 Sindel City
army 0 0 4
labo 0 2 2 2
```

These appear to be editor-source artifacts, but their numbers are **not
reliable pairings** with the binary files. An all-pairs semantic audit gives:

| text source | best binary match | relationship |
|---|---|---|
| `s9` | `s9.scn` | ordered subset, 512 of 522 records |
| `s10` | `s12.scn` | near match, 506 shared records |
| `s11` | `s11.scn` | ordered subset, 383 of 392 records |
| `s12` | `s10.scn` | ordered subset, 381 of 391 records |
| `s13` | `s15.scn` | ordered subset, 406 of 416 records |
| `s14` | `s13.scn` | exact, all 1,091 records |
| `s15` | `s14.scn` | exact, all 1,091 records |

Notably, `s14` and `s14.scn` have the same record count but differ in one
semantic record after whitespace normalisation. Same-name text/binary equality
must not be used as a format invariant.

Three consequences:

1. Successfully parsing every line is useful evidence for the tag arity table,
   but binary equality is not: the source/binary pairing is uncertain.
2. It is the natural **human-editable format for our own scenario editor**;
   we should adopt it rather than invent one.
3. Run `tools/audit_scenario_corpus.py` after parser changes to compare every
   source with every binary without committing or printing source records.

### It is a genuine arity oracle, with three gaps

All 4,373 lines across the seven text files parse cleanly under
`TAG_FIELD_COUNTS`. That is a real check rather than a tautology: the parser
rejects a line with tokens left over after its declared field count, so a tag
whose arity we had wrong would fail on any line that used it.

Coverage is 21 of 24 tags. **`coun`, `tbar` and `tclr` appear in no plaintext
file**, so their field counts rest on the binary alone and remain unconfirmed.

The two exact reproductions in the table are the stronger result: they validate
field *order* and name handling as well as arity, across 2,182 records.

### The text form is lossy against the binary

It carries no `TERM` terminator and no trailing bytes, so round-tripping a
`.scn` through text is **not** byte-exact. Go binary-to-binary and treat text as
import/export only.

Note the CR (`\r`) line endings — the original was a Mac and PC cross-platform
codebase (`McAppUI`, `UMacViewMgr` appear in the binary's leaked filenames).
`.inf` files use CR endings too.

## Not yet decoded

- The other 196 bytes of each province-table record. Its town-cell field is
  decoded — see `file-formats.md`.
- How a `ship` record's `zone` id relates to the map's ocean zone byte.
- Save games (`.imp`, magic `IBMA`, ~412 KB) — a serialized game state, so
  decoding it reveals what the original actually tracks.
- `.gob` resource archives — these are PE resource containers, so standard
  extractors should open them.
