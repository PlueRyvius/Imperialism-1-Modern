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
  "formatVersion": 12
}
```

Version 12 is the authored version. Migration is explicit and sequential:
version 1 resource palettes become version 2 commodities/resources, version 2
packages gain empty version 3 production collections, version 3 packages gain a
per-deposit `yieldPerTurn` plus a world-level `extraction` block, and version 4
turns that flat rate into a `yieldByDevelopmentLevel` curve, and version 5 gains
ports and port fishing. This preserves everything older packages could express
without inventing factories, recipes, or capacity. Versions 4 and 5 are the
migrations that must supply values rather than empty collections, because a
deposit with no rate would silently stop producing; both use documented defaults
and say so in `formulas/extraction.md`. Every cell in a version 4 package is
undeveloped, so putting its flat rate at level zero leaves behaviour unchanged.
The version 6 step adds nothing at all: an older package has no port records,
and nothing in it says which of its commodities is fish. Version 7 adds depots
and likewise supplies none — which **changes behaviour**, since a package with
no depots now gathers only around its capital and ports rather than anywhere its
rail reaches. That is the correct model, but it is visible, so it is stated here
and in the migration's own comment. Version 8 adds the workforce and what it
eats, and likewise supplies neither: nothing in an older package says which of
its commodities are food, so its workers simply never eat. Version 9 prices each
recipe's `labourCost`, and here a value *can* be derived — the recipe's total
input units, which is the rate the manual gives for the one recipe it prices and
the same number as two per unit of output for every recipe the original ships.
That **changes behaviour** for any version 8 package that also defines feeding,
whose production is now capped by its workforce; a package without feeding is
unaffected, because labour does not bind where there is no workforce to invent.
Version 10 adds the fair start a skirmish runs on: a world-level
`startingDefaults` block, and a `defaultStartCountries` list naming the powers
that begin from it. A version 9 package gets neither, and neither can be
invented for it — the baseline is a property of the original's rules, not of an
arbitrary world — so it migrates unchanged. Version 11 lets industry grow: a per-facility `capacityLadder` and a
world-level `expansionCostPerCapacityPoint`. A version 10 package has neither,
and supplying them would be inventing a rule rather than filling in a value, so
it migrates to a world whose industry can never be built larger — which is how
it behaved before. Version 12 adds the Capitol's terms — what a recruit costs and how many
provinces buy one. A version 11 package has none, and the price of a worker is a
number nobody has measured, so it migrates to a world that cannot recruit.
Version 13 adds civilian units and the terrain attributes they need, and is the
**first bump to rename a field rather than add one**: the bare `terrainKeys`
palette becomes `terrains`, whose entries carry a display name and an
`isImprovable` flag. It also adds `civilianTypes`, `resources[].improvedBy` and
`scenarios[].civilians`. A version 12 package migrates to terrain that is named
after its key and improvable nowhere, and to a world with no civilians — which
is not a placeholder standing in for a missing value but an exact reproduction:
a version 12 world had no way to improve anything at all.
Mixed-version schemas,
unknown fields, and unsupported versions fail with a path-qualified validation
error. Generic migrated keys use the
valid `commodity/from-resource/...` form; `/` is part of the key grammar below.

## Stable keys and runtime IDs

Package references use stable keys such as `terrain.plains`, `province.berry`,
and `country.france`. Keys contain 1-128 lowercase ASCII letters, digits,
`-`, `_`, `.`, or `/`, and begin and end with a letter or digit. Display names
are unrestricted Unicode strings and are never identifiers.

At load time, the compiler validates all references and maps keys to dense,
typed integer IDs in document order. Resources and commodities have distinct
ID types: a map deposit references a resource definition, and that definition
names the commodity it yields. `WorldContentCatalog` retains both
directions of that mapping. Simulation code therefore gets compact array
lookups without making saves, mods, or editor references depend on array
positions.

## Document structure

The top-level document contains:

- ordered terrain definitions with stable key, Unicode name, and `isImprovable`;
- ordered civilian-type definitions with stable key, Unicode name, and a
  positive `workTurns`, or none at all in a world without civilians;
- ordered commodity definitions with stable key, Unicode name, and `raw`,
  `material`, or `goods` category;
- ordered resource definitions mapping each deposit key to one commodity key, a
  `yieldByDevelopmentLevel` curve, an optional `requiredTechnology`, and an
  optional `improvedBy` naming the civilian type that raises it;
- a world-level `extraction` block holding the gathering `catchmentRadius` and an
  optional `portFishing` naming one commodity and its yield per adjacent water
  tile;
- ordered technology definitions with stable key and Unicode name;
- an optional `feeding` block holding the repeating food-preference cycle, the
  labour each worker grade supplies, and the canned-food substitute;
- ordered production-facility definitions with stable key, Unicode name, and
  `limited` or `unlimited` capacity mode;
- ordered recipes naming a facility, positive capacity cost, and one or more
  positive commodity inputs and outputs;
- a keyed map with dimensions, named provinces and sea zones, row-major cells,
  and optional per-cell river paths;
- named countries;
- one or more keyed scenarios containing name/year, explicit province owners,
  rails, capitals, optional positive initial commodity quantities, sparse
  positive capacities for limited facilities, sparse starting cell development,
  which technologies each country begins knowing, the cells carrying a port or a
  rail depot, each country's starting workforce, and the civilians on the map at
  the start, each naming its owner, its type and the land cell it stands on.

Each cell references one terrain key, zero or more unique resource keys, and
at most one province or sea-zone key. Settlement sites and river paths are map
geography. A river path is an undirected pair drawn from `northEast`,
`eastUpper`, `eastLower`, `southEast`, `southWest`, `westUpper`, `westLower`,
`northWest`, `source`, and `mouth`. It records only the shape inside that cell;
the package does not infer cross-cell river connectivity.

Rails are pairs of adjacent cell indices represented internally as canonical
undirected `CellLink` values.

Within every scenario, each province has exactly one ownership entry; a null
country means unowned. Capital cells must be urban province cells initially
owned by that country. Rail links must join land cells. Several scenarios in
one package compile to `WorldDefinition` values sharing the same immutable
`MapDefinition`, so alternate starts do not duplicate map data. Width multiplied
by height uses checked arithmetic, but there is no historical map, country,
province, resource, commodity, facility, recipe, or name-size limit in the
content compiler.

Commodity definitions are package content rather than a fixed Core enum. The
original importer emits the standard 13 raw, 6 material, and 4 goods
commodities, while mods may define a different catalog. Power and money are
not commodities. Initial inventory entries are sparse authored data; runtime
inventory is a dense checked 64-bit array indexed by country and commodity.

Facility capacity is shared by every recipe that names that facility. An
unlimited facility has no stored capacity entry. Recipes are general data, not
fixed original-game slots: they may have multiple inputs and outputs, and
alternative inputs are separate recipes so allocation stays explicit. Runtime
orders refer to dense recipe IDs after compilation.

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
