# File format notes

Our own working notes on the two binary formats this project reads and
writes, based on what round-trip testing against real game files
confirms. Not copied from any third-party document — this is what we
verified ourselves in `tests/`.

## `.map`

- Original-game profile: 108 x 60 hex cells, stored row-major
  (left-to-right, top-to-bottom).
- The binary format has no dimension header. `MapFormatProfile` supplies
  dimensions at import time; `MapFile` carries those dimensions thereafter,
  so new content is not restricted to 108 x 60.
- Each cell is a fixed 36-byte record — see `HexCell` in
  Forge's `src/imperialism_format/map_file.py` for the full field layout
  (terrain type, resources, province/country ownership, rail, rivers,
  border/coastline overlays, town/capital markers).
- After the cell grid comes a **province table**: 384 records of 198 bytes
  (`DORMANT_RECORD_COUNT` / `DORMANT_RECORD_SIZE`), **indexed by province id**,
  so 384 is the format's province cap. Each record holds that province's
  **town cell index** as a big-endian u16 at offset 4, with 65535 for unused
  slots.

  **Verified on all ten maps**: every province's town sits at its own slot —
  `s1` fills 213 of the 384, `s9` fills 120 — and no populated slot disagrees
  with the town found on the map. This explains why province ids are sparse
  (0-348 for 213 provinces) yet bounded.

  The rest of each record is **not decoded**. Rebuilding a table from the town
  field alone reproduces that field exactly but only about two thirds of the
  bytes; offsets around 58-65, 130-135 and 158-190 carry more. Part of the tail
  varies even in *unused* slots, which is the signature of uninitialised memory
  written to disk rather than of meaningful data.

  So the block is still preserved byte-for-byte by default.
  `MapFile.set_province_town` writes only those two bytes, which is what lets a
  generated map inherit a real map's table instead of fabricating one.

### Byte 2, the river

One value describes how the water crosses the cell. The three ranges are
absolute, and hold across all nine originals:

| range | meaning | lives on |
|---|---|---|
| 11–26 | a through-flow | land |
| 43–50 | a riverhead (the spring) | land |
| 51–58 | a river **mouth** | the **ocean** cell, never land |

So a river's last land cell is an ordinary through-flow, and the sea cell it
empties into carries the mouth, pointing back at it.

Every value names the one or two hex directions the water crosses. Those were
**measured** — for each value, which neighbours also carry a river — and came
out unambiguous: every through-flow value showed exactly two directions at 100%
across six maps. The table is `generate/rivers.py`.

**A river runs straight or turns 120°; it can never turn 60°.** The sixteen
through-flow values cover only nine of the fifteen possible direction pairs, and
the six they omit are exactly the six 60° turns. That is the one hard constraint
on carving a course.

**East and west crossings have a high and a low variant**, and the height
belongs to the *edge*: a river leaving east high must be taken in west high by
the cell to its east. `(13,14)`, `(17,18,19,20)`, `(21,22)` and `(23,24)` are
the same topology at different heights, and all four east–west combinations
exist, so no course is ever blocked for want of a value.

Two errors in `MapDecode.rtf` were caught here and are corrected in the table:
**49 is a west low head, not east**, and **55 is a south-west mouth, not
south-east**. Generating from the notes as written draws both in the wrong
place.

## `.scn`

- A flat sequence of tagged variable-length records: a 4-byte ASCII tag,
  then a fixed number of big-endian 4-byte integer fields (the count
  depends on the tag — see `TAG_FIELD_COUNTS` in
  Forge's `src/imperialism_format/scn_file.py`), then for three tags
  (`cnam`, `pnam`, `zone`) a trailing 64-byte null-padded name string.
- The file ends with a bare `TERM` tag and has no length prefix or
  checksum.
- Original files may contain leftover bytes after each name's null
  terminator. The parser retains the full 64-byte field and reuses it if
  the name is unchanged. Editing a name intentionally replaces the field
  with a null-padded representation.

### Plaintext scenario form

- The extensionless editor sources use the same tags and field arities, one
  whitespace-delimited record per line, with the remainder of a name record's
  line holding its name. They have no `TERM` sentinel.
- Input accepts CR, LF, or CRLF. Canonical output is ASCII with single spaces
  between fields and CR line endings; whitespace-exact preservation is not a
  goal.
- Source numbers do not reliably pair with same-numbered `.scn` files. Use the
  all-pairs corpus audit described in `scenario-semantics.md`.

## `.inf`

- Plain-text scenario description split into `#`-delimited sections, using
  **CR (`\r`) line endings** and cp1252 text. Do **not** read with
  universal-newline translation: it destroys the endings and makes a byte-exact
  round trip impossible.
- The layout is identical in all ten scenarios (**verified**): nine `#` lines,
  giving a title block of exactly one line, an overview block, **seven
  per-country briefings** in country-id order (unused slots contain literal
  `Fake Text` placeholders), and a final `# ` line of **eight integers**.
- Of those eight: the first seven are per-country difficulty or availability
  codes (`-1` marks a country unplayable; tutorials have exactly one playable
  entry) and the eighth is the default player country index. Every value across
  all ten files falls in `-1..6` and the eighth is always a valid country index
  — consistent with the reading, though the difficulty scale itself is still
  **inferred**.
- `^^` denotes a paragraph break within a section.
- Unchanged documents retain their original bytes, including newline style and
  final-newline choice, so all ten round-trip exactly
  (`tests/test_inf_file.py`). Once edited, writers emit canonical CP1252 with CR
  line endings and enforce exactly seven country sections and eight integers.
  The C# `ScenarioInfoCodec` follows the same unchanged-or-canonical rule, which
  is what lets the two implementations be compared on edited files at all.

## Semantics

This document covers on-disk layout only. For what the fields *mean* —
province ownership, cell references, sea zones vs. ports, and the corpus-wide
findings that back them — see `scenario-semantics.md`.
