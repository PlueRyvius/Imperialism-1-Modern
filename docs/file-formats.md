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
  `src/imperialism_format/map_file.py` for the full field layout
  (terrain type, resources, province/country ownership, rail, rivers,
  border/coastline overlays, town/capital markers).
- In the original profile, after all 6,480 cell records, the file has a
  trailer of 384 records of
  198 bytes each (`DORMANT_RECORD_COUNT` / `DORMANT_RECORD_SIZE`). We
  preserve this trailer byte-for-byte on load/save without interpreting
  it — round-trip tests confirm this reproduces the original file
  exactly.

## `.scn`

- A flat sequence of tagged variable-length records: a 4-byte ASCII tag,
  then a fixed number of big-endian 4-byte integer fields (the count
  depends on the tag — see `TAG_FIELD_COUNTS` in
  `src/imperialism_format/scn_file.py`), then for three tags
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
  **CR (`\r`) line endings** — read with universal-newline translation.
- Section 1 is the title, section 2 the overview/victory text, then **seven
  per-country briefings** in country-id order (unused slots contain literal
  placeholder text), then a final section of **eight integers**.
- Of those eight: the first seven appear to be per-country difficulty or
  availability codes (`-1` marks a country as unplayable, and tutorials have
  exactly one playable entry), and the eighth is the default player country
  index. **Inferred**, not proven — consistent across all ten scenarios.
- `^^` denotes a paragraph break within a section.

## Semantics

This document covers on-disk layout only. For what the fields *mean* —
province ownership, cell references, sea zones vs. ports, and the corpus-wide
findings that back them — see `scenario-semantics.md`.
