# Phase 1 legacy importer

`Imperialism.LegacyImport` converts preserved `.map`, `.scn`, and optional
`.inf` documents into the versioned `.iworld` viewer slice. It is deliberately
conservative: only evidenced fields become modern content, while unsupported
information is counted in a stable report and remains available in the source
files. No opaque legacy record or map trailer is copied into `.iworld`.

The converter imports terrain, known resources, province and sea-zone regions,
settlement sites, ownership, local river shapes, reciprocal rails, capitals,
names, year, and the INF title. Unknown terrain codes use an explicit numeric
placeholder key because every modern cell requires terrain. Unknown resource,
town, and river codes create warnings and no inferred feature. Conflicting
owners, references that cannot form valid modern content, duplicate capitals,
conflicting years, or missing dimensions/year block output.

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
