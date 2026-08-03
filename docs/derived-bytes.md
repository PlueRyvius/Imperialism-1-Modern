# Derived cell bytes

Several of the 36 bytes in a `.map` cell are six-bit direction masks describing
the cell's relationship to its six neighbours. They are *derived*: an editor
that repaints one cell has to recompute them for that cell and up to six
others, or the map renders with broken borders and shorelines.

This document records what we measured. `src/imperialism_format/derive.py`
implements the rules that fit; `tests/test_derive.py` holds the floors.

## Grid geometry — settled

Fitting every mask field independently produced the same neighbour table, which
is strong evidence it is right rather than a coincidence of one field:

- **Odd-r offset layout** of pointy-top hexes — odd-numbered rows sit half a
  cell to the right.
- **Bit 0 is NE, then clockwise**: NE, E, SE, SW, W, NW. Matches the order of
  `DIRECTIONS` in `constants.py`.
- **The map wraps east-west, not north-south.** This was not assumed; it fell
  out of the data. `national_border` fits 98.8% without wrap and exactly 100%
  with it, and every residual before wrapping sat on column 0 or 107.

## Rules that fit

Measured against **all ten original maps**, 64,800 cells.

| Byte | Rule | Worst map | Best |
|---|---|---|---|
| `national_border` | Off-map **or** (land neighbour with a different `nation_zone_a`). Ocean neighbours never count. | **100.000%** | 100.000% |
| `province_border` (land) | Land neighbour with a different `province`. Off-map does **not** count. | 99.985% | 100.000% |
| `province_border` (ocean) | Neighbour `d` and neighbour `d+1` clockwise are both land, in different provinces. | **100.000%** | 100.000% |
| `like_cell_adjacency` | Neighbour has the same terrain, where hill (8) and wool-hill (7) count as one. Always zero on ocean (0) and horse ranch (4). | 99.120% | 99.614% |
| `land_coastline` | Neighbour is ocean. Always zero on ocean cells. | 97.978% | **100.000%** |

`national_border` is exact on every map. `land_coastline` is exact on eight of
the ten — only `s1` (97.978%) and `s3` (99.938%) miss, which says the rule is
right and those two maps carry hand-touched shoreline art. The first version of
this document reported 95.920% for it because `s1`, the one map then available,
happens to be the worst case in the corpus.

Note the split: the two masks that carry *gameplay* meaning — where nations and
provinces end — are the two that reproduce essentially perfectly. The ones that
only select artwork are the fuzzy ones. That is the pattern you would expect if
the original tooling derived topology and let artists touch up decoration.

## Byte 8 on ocean cells: where an outline meets the coast

This byte was long assumed to be a land-only mask, and ocean cells were left
holding whatever they already had. They are not idle. Bit `d` on a water cell is
set when the neighbours in directions `d` and `d+1` (clockwise) are **both land
and in different provinces** — the point where a province outline running
inland reaches the shore and has to stop on this cell's edge.

Exact: **35,583 of 35,583** ocean cells across all fourteen maps, no exceptions.
Two consequences fall out of it and hold everywhere in the corpus — every ocean
cell carrying the byte touches at least two provinces (106/106 in `s1`, 104/104
in `s9`, 121/121 in `s11`), and a bit never points at anything but land.

Preserving instead of deriving it was a live defect, not a gap. Repaint a land
cell to sea and the old land mask survived with its bits now facing open water.
The engine builds one boundary segment per bit and resolves the province on
each side; the water side resolves to 65535, which is the sentinel
`UMapper.cpp:4751` asserts against, and which the region accessor at `00563330`
then multiplies by its 72-byte stride **without a bounds check**. Past the
assert, the virtual call at `0055F30B` dereferences whatever sits 72 bytes
before the table. That is an access violation on map load, and it reproduces by
painting an ocean brush across a province boundary.

## Bits 6 and 7 of bytes 7 and 8 are not directions

A direction mask needs six bits and these bytes have eight. The top two carry
something undecoded: 79 land and 342 ocean cells for byte 7, 1,584 land and 9
ocean cells for byte 8. Nothing yet distinguishes those cells.

They are preserved rather than recomputed, like every other undecoded byte. The
distinction is not academic — recomputing a six-bit mask over the whole byte was
zeroing them on every cell an edit touched, and it is where the entire reported
shortfall of both rules came from. Measured against the full byte, and
preserving the top two bits, `national_border` is exact on all 58,320 cells and
`province_border` misses four.

## Rules that looked right on one map and were wrong

Fitting against `s1` alone produced three validator rules that fire on shipped
data — the bar this project sets for a wrong rule. All three only surfaced once
all ten maps were available:

- **"ocean cells carry no resource"** — wrong by 3,848 cells. Fish live at sea;
  the tutorial scenarios (`s9`-`s12`, `s15`) stock their oceans with it. `s1`
  has none, so the rule looked clean.
- **"town terrain implies a town_type marker"** — wrong by 110 cells, including
  one on the otherwise-spotless `s3`. Whatever `town_type` means, it is not
  "this cell is a town". The rule was deleted rather than relaxed.
- **`town_type` 33** — a real value used by every tutorial map, 16 cells each,
  which the lookup table simply lacked.

The lesson is the cheap one: a corpus of one is not a corpus.

### The residuals

- **`province_border`** — a single cell, (104, 45), which is the whole residual
  once the top two bits are preserved rather than zeroed. It appears four times
  in the corpus only because `s1`, `s3`, `s13` and `s14` share a base map. Three
  neighbours belong to a different province but only one bit is set. No rule we
  tried explains it; treated as an anomaly in the shipped data.
- **`like_cell_adjacency`** — 55 cells, all on clear/town boundaries. Every
  other terrain pairing is 0% or 100% with no ambiguity at all.
- **`land_coastline`** — 131 cells, all in the map's eastern half (x 48-97),
  none west of x=48. The lopsidedness suggests hand-editing rather than a rule
  we have missed. There are no inland lakes on this map, so that is not the
  cause.

## Bytes we deliberately do *not* derive

- **`river` and `rail`** are authored content, not derived. The editor writes
  their direction bits directly when you draw a path.
- **`ocean_coastline` (byte 1) on *land* cells.** Nonzero for exactly two
  terrains — mountain (161 cells) and desert (101) — so it is a terrain-edge
  decoration whose meaning depends on the cell's own terrain, not a coastline.
  Still unread; preserved verbatim.

These are preserved verbatim. Painting does not touch them, so an edited map
keeps whatever the original artists put there.

## Byte 11, solved

Previously listed here as hopeless ("resisted every hypothesis; best fit ~7%").
It is in fact **exact** — 6,480 of 6,480 cells on every one of the nine
originals — once you notice it means three different things:

| cell | byte 11 |
|---|---|
| ocean | mask of adjacent **land** — the sea's half of the shoreline |
| hill or wool hill (7, 8) | mask of adjacent **mountain** (9) |
| mountain (9) | mask of adjacent **hill** (7, 8) |
| anything else | 0 |

The earlier 7% came from testing one rule against all cells at once. Three
populations with three rules average to nothing; splitting by the cell's own
terrain first is what made it fall out. Worth remembering as a method — the
same shape may be hiding in the bytes still listed above.

The practical cost of missing it: generated worlds wrote zero here, so they had
**no shoreline art on the water side at all**, which is visible the moment you
load one. `land_coastline` (byte 9) is only half the shore.

## Byte 1 on ocean cells, partly solved

Two cases, and the split is what the old 90.6% figure was blurring:

- **Open water** (no adjacent land): an island decoration — 1 sandbar,
  2 small island, 3 large island, 4 islet group, and never any other value in
  any shipped map. Authored, not derived, so `derive.py` leaves it alone.
- **Coastal**: always a **subset** of the adjacent-land mask — 2,345 of 2,345
  coastal ocean cells across `s1`, `s9` and `s11`, no exceptions — and equal to
  the whole mask in most. *Which* subset is still unknown: it tracks neither the
  terrain nor the underlay of the neighbouring land (every predicate tried
  landed between 20% and 68% and disagreed between maps), so it looks like
  shore-art variation.

`derive.py` therefore returns the full mask for coastal ocean cells: a value the
originals use, always inside the permitted subset relation, and far better than
the zero a generated world carried. It is the one deriver here that is
deliberately approximate, and it says so.

The island codes and the 1–4 vocabulary come from `MapDecode.rtf`, a
community-written format note from the old editor tools; both were then checked
against all nine originals before being believed.

## Coverage

All ten original maps. Either drop them into `fixtures/local_only/`, or set
`IMP_SCENARIO_DIR` to a game install's `Scenario` folder and the tests pick
them up without copying copyrighted data into the tree:

```sh
IMP_SCENARIO_DIR=/path/to/Imperialism/Scenario python -m pytest
```

Note that this also subjects any *edited* map sitting in that folder to the
validator, which is a useful accident: `test_validator_is_silent_on_original_maps`
will fail on a map you have broken, naming the cell.
