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
