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
- The nation byte holds a **country id on land and a sea-zone id on
  ocean** — the two namespaces share one field, disambiguated by whether the
  cell is ocean. Land cells in `s1` use exactly the 23 ids 0–22, matching
  the 23 `cnam` records (**verified**).
- Ocean cells use province id **65535** as a null marker.
- `town_type` distinguishes settlements: 34 = village, 35 = capital. `s1`
  has 190 villages and **exactly 23 capitals — one per country** (**verified**).
- Capitals are marked via `town_type`, not via the terrain code. The
  terrain value for "capital" never appears in the European maps.

## Scenario record semantics

**Cell references.** `deve[0]`, `rail[0]`, `port[0]`, and `civi[1]` are
**linear cell indices** into the row-major grid (0-based, `y * width + x`),
not coordinate pairs. **Verified** in `s1`: all 320 `deve`, 76 `rail`, 49
`port`, and 35 `civi` references are in range *and* land on non-ocean cells —
a result vanishingly unlikely by chance, and the reason this is stated as
verified rather than inferred.

**Province references.** `army[0]` is a **province id**, not a country id.
**Verified:** all 299 `army` records in `s1` reference valid `pnam` ids,
covering all 213 provinces.

**Sea zones and ports share the `zone` tag.** In `s1`, ids 0–39 are sea
zones ("North Atlantic", "English Channel") and 40–62 are **port cities**
("Chatham", "Trieste") — one per country. Consumers must not assume every
`zone` record is a body of water.

**Playable powers are the low ids.** Only countries 0–6 receive `cash`,
`tran`, `labo`, and `tclr` records — these are the seven Great Powers. Ids
7–22 are minor nations (**verified**: 7 records each of those tags).

## Corpus notes

- All ten `.map` files are exactly 309,312 bytes. **`s0.map`, `s13.map` and
  `s14.map` are byte-identical** — deduplicate before treating them as
  distinct corpora.
- Grand campaigns (`s0`, `s1`, `s3`, `s13`, `s14`) carry the full dataset:
  23 countries, 213 provinces, 63 zones, 253 relation pairs. Tutorials
  (`s9`–`s12`, `s15`) omit most names and lean on `zone`, `army`, `emba`.
- The **fish** resource appears only in tutorial maps (680–810 cells); the
  European maps contain none.
- Tutorial maps use a `town_type` value of **33**, which we have not
  identified — it is neither village nor capital. Retained, uninterpreted.
- Every one of the 24 known scenario tags appears somewhere in the corpus;
  there are no dead tags. Rarest are `coun` (5 records, only in `s0`/`s1`/`s3`)
  and `flag`/`year` (one per scenario).

## The plaintext scenario form

The tutorials ship an extensionless companion file (`Scenario/s9`, `s10`, …)
containing the same records as **CR-delimited plain text**, one per line:

```
tech 0 1
zone 83 Sindel City
army 0 0 4
labo 0 2 2 2
```

`s14` has 1,091 lines and `s14.scn` has exactly 1,091 records. These are
almost certainly the designers' editor input, left in the shipped folder.

Two consequences:

1. It is **free ground truth for the tag arity table** — the thing most
   likely to be subtly wrong. Parsing the text and diffing against the
   binary is really a test of our arity assumptions.
2. It is the natural **human-editable format for our own scenario editor**;
   we should adopt it rather than invent one.

Note the CR (`\r`) line endings — the original was a Mac and PC cross-platform
codebase (`McAppUI`, `UMacViewMgr` appear in the binary's leaked filenames).
`.inf` files use CR endings too.

## Not yet decoded

- The 384-record map trailer (198 bytes each).
- `town_type` value 33 in tutorial maps.
- Save games (`.imp`, magic `IBMA`, ~412 KB) — a serialized game state, so
  decoding it reveals what the original actually tracks.
- `.gob` resource archives — these are PE resource containers, so standard
  extractors should open them.
