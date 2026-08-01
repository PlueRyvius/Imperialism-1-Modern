# Roadmap 0: source-of-truth foundation

Roadmap 0 turns the available game files and research material into a
tested, inspectable specification for the modern engine.

## Design rules

1. Historical format restrictions stop at the importer boundary.
2. Parsed objects carry explicit dimensions and identifiers.
3. Unknown bytes are retained until their meaning is demonstrated.
4. Every interpretation is backed by a corpus test or documented evidence.
5. Semantic summaries are stable and reviewable as JSON.

## Current import profiles

The original `.map` files do not contain dimensions. The legacy profile is
108 x 60 cells, 36 bytes per cell, followed by 384 records of 198 bytes.
Alternative profiles may specify another width and height. The eventual
engine content package will include its dimensions in its own header.

## Verification corpus

Real source files remain outside version control. Put local test pairs in
`fixtures/local_only/`; CI exercises generated fixtures, malformed-input
checks, arbitrary map dimensions, and lossless name-field handling.

Local corpus verification should additionally cover:

- byte-identical load/save for every `.map`;
- byte-identical load/save for every unchanged `.scn`;
- semantic snapshots for `.map`, `.scn`, and `.inf` triplets;
- cross-file validation of province, country, sea-zone, and location IDs.

## Next decoding targets

- Meaning of the 384 map trailer records
- Complete field semantics for each scenario tag
- Scenario-selection metadata in `.inf`
- Save-game (`.imp`) structure
- Resource archive (`.gob`) indexes and payload formats
