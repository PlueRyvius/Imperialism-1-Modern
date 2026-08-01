# File format notes

Our own working notes on the two binary formats this project reads and
writes, based on what round-trip testing against real game files
confirms. Not copied from any third-party document — this is what we
verified ourselves in `tests/`.

## `.map`

- Grid: 108 x 60 hex cells, stored row-major (left-to-right, top-to-bottom).
- Each cell is a fixed 36-byte record — see `HexCell` in
  `src/imperialism_format/map_file.py` for the full field layout
  (terrain type, resources, province/country ownership, rail, rivers,
  border/coastline overlays, town/capital markers).
- After all 6,480 cell records, the file has a trailer of 384 records of
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
- Round-trip note: the original files have leftover garbage bytes after
  each name's null terminator (unused padding the original program
  never zeroed). Our writer zero-pads instead — semantically identical
  since the game only reads up to the null terminator, but not a
  byte-for-byte match on those specific padding bytes. Every other byte
  matches exactly.
