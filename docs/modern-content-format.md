# Modern world content

`.iworld` is the engine's authored world-content format. It is canonical,
versioned UTF-8 JSON and is intentionally independent of the original
`.map`, `.scn`, and `.inf` layouts.

The legacy formats remain lossless import/export and research boundaries.
Imported content is converted into a `WorldContentDocument`, validated, and
saved as `.iworld`; the simulation never reads legacy records directly.

## Why JSON first

World content is loaded once, while maps and scenarios are edited, reviewed,
diffed, and migrated many times. Canonical JSON makes those workflows simple
and keeps the format open to external editors. The writer emits readable
Unicode, LF line endings, a trailing newline, and no byte-order mark.

If profiling later shows startup cost matters, a compiled binary cache may be
stored beside the authored file. It is disposable acceleration, not the source
of truth, so changing cache layout cannot strand scenarios or mods.

`.iworld` is also not a saved running game. Saves will serialize mutable
`WorldState` using their own explicit version and state hash.

## Envelope and versions

Every document starts with:

```json
{
  "format": "imperialism-world",
  "formatVersion": 1
}
```

Version 1 is the only accepted version today. Unknown fields and unsupported
versions fail with a path-qualified validation error; they are never silently
ignored. When version 2 is introduced, its loader must either migrate older
documents explicitly or retain a version-specific reader.

## Stable keys and runtime IDs

Package references use stable keys such as `terrain.plains`, `province.berry`,
and `country.france`. Keys contain 1-128 lowercase ASCII letters, digits,
`-`, `_`, `.`, or `/`, and begin and end with a letter or digit. Display names
are unrestricted Unicode strings and are never identifiers.

At load time, the compiler validates all references and maps keys to dense,
typed integer IDs in document order. `WorldContentCatalog` retains both
directions of that mapping. Simulation code therefore gets compact array
lookups without making saves, mods, or editor references depend on array
positions.

## Document structure

The top-level document contains:

- ordered terrain and resource key palettes;
- a keyed map with dimensions, named provinces and sea zones, row-major cells,
  and rivers;
- named countries;
- one or more keyed scenarios containing name/year, explicit province owners,
  rails, and capitals.

Each cell references one terrain key, zero or more unique resource keys, and
at most one province or sea-zone key. Settlement sites are map geography.
Rivers and rails are pairs of adjacent cell indices represented internally as
canonical undirected `CellLink` values.

Within every scenario, each province has exactly one ownership entry; a null
country means unowned. Capital cells must be urban province cells initially
owned by that country. Rail links must join land cells. Several scenarios in
one package compile to `WorldDefinition` values sharing the same immutable
`MapDefinition`, so alternate starts do not duplicate map data. Width multiplied
by height uses checked arithmetic, but there is no historical map, country,
province, resource, or name-size limit in the content compiler.

## APIs

- `WorldContentCodec.Decode/Encode/Load/Save` reads and writes validated
  `WorldContentDocument` values.
- `WorldContentCompiler.CompilePackage` compiles every keyed scenario and
  shares one map and catalog between them.
- `WorldContentCompiler.Compile(document, scenarioKey)` selects one scenario as
  `CompiledWorldContent`; the keyless overload is convenient for one-scenario
  packages and rejects ambiguous multi-scenario input.
- `WorldContentCodec.DecodeAndCompile` and `DecodeAndCompilePackage` are the
  direct runtime loading paths.

Documents are editable data objects. Compilation defensively copies values,
so later editor mutations cannot alter an already loaded world.
