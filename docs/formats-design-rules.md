# Formats layer: design rules

Rules governing how this project reads and writes the original game's file
formats. They apply to the Python library here and carry over unchanged to
the C# `Imperialism.Formats` port.

For the project roadmap, see the phase table in the README — this document
covers the formats layer only.

## Design rules

1. Historical format restrictions stop at the importer boundary.
2. Parsed objects carry explicit dimensions and identifiers.
3. Unknown bytes are retained until their meaning is demonstrated.
4. Every interpretation is backed by a corpus test or documented evidence.
5. Semantic summaries are stable and reviewable as JSON.

Rule 1 is the load-bearing one. The 108x60 grid is a property of the 1997
files, not of the engine; keeping that fact at the importer boundary is what
makes maps of any size possible later without reworking the model.

Rule 3 is what makes byte-exact round-trip achievable: preserve the original
bytes of anything not yet understood (map trailer records, name-field padding
after the null terminator, bytes past `TERM`) and re-emit them verbatim unless
the decoded value was actually edited.

## Current import profiles

The original `.map` files do not contain dimensions. The legacy profile is
108 x 60 cells, 36 bytes per cell, followed by 384 records of 198 bytes.
Alternative profiles may specify another width and height. The eventual
engine content package will include its dimensions in its own header.

## Verification corpus

Real source files remain outside version control. Put local test pairs in
`fixtures/local_only/`; CI exercises generated fixtures, malformed-input
checks, arbitrary map dimensions, and lossless name-field handling.

**Status: byte-exact round-trip is verified** against all 20 original files
(10 `.map` + 10 `.scn`) from the shipped scenario set. This is a hard
requirement, not an aspiration — the C# port must match it, and any parser
that cannot is wrong.

Local corpus verification should additionally cover:

- byte-identical load/save for every `.map`;
- byte-identical load/save for every unchanged `.scn`;
- semantic snapshots for `.map`, `.scn`, and `.inf` triplets;
- cross-file validation of province, country, sea-zone, and location IDs;
- equivalence between the binary `.scn` and the CR-delimited plaintext form
  shipped with the tutorial scenarios — effectively a test of the tag arity
  table, which is the easiest thing to get subtly wrong.

## Next decoding targets

- The other 196 bytes of each province-table record (the town-cell
  field at offset 4 is decoded; see `file-formats.md`)
- Complete field semantics for each scenario tag
- Scenario-selection metadata in `.inf`
- Save-game (`.imp`) structure — magic `IBMA`; a serialized game state, so
  decoding it reveals what the original actually tracks
- Resource archive (`.gob`) indexes and payload formats — these are PE
  resource containers, so standard extractors should open them
