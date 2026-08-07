# Formula recovery scoreboard

The original game's manual documents *what* its systems do but almost none of
the numbers behind them. Those undocumented formulas are the main risk to a
faithful reimplementation: get the trade clearing price wrong and the entire
pacing of the game changes.

This index tracks what we know, how confident we are, and — critically —
whether a test pins it down. Each mechanic gets its own document following the
template below.

## Confidence levels

| Level | Meaning |
|---|---|
| `guess` | Plausible invention. Behaves sanely, matches no evidence. |
| `inferred` | Derived from manual text, observed play, or partial disassembly. Shape right, numbers uncertain. |
| `verified` | Confirmed against the original's behaviour and pinned by a test with real input/output pairs. |

## What counts as evidence *for gameplay*

Not the same thing as evidence for the file format, and the two get mixed up
here more than anything else. For the format the corpus is the authority. For
gameplay it is weak, because **the ten scenarios are authored missions**, not a
picture of how a game plays.

| Source | Weight for a gameplay number |
|---|---|
| The engine binary | **decisive** — it is the behaviour |
| The manual and quick reference | **strong** for anything they state outright |
| Release notes | **strong**, and they beat the manual where they disagree |
| A *skirmish-shaped* scenario (`s10`, `s11`, `s15`) | **good** — all seven powers identical, so it shows the intended fair start |
| A mission scenario (`s1`, `s3`, `s9`, `s12`–`s14`) | **weak** — it shows what one designer authored for one mission |
| Observed play | **good** for shape, poor for exact numbers |

The trap in practice: `capa`, `labo` and `tran` all look like gameplay
constants, and in the missions they are nothing of the kind. `s1` gives one
power `[60, 5, 0]` workers and another `[5, 15, 40]`; that is a scenario design,
not a rule. The skirmish baseline is `[4, 2, 1]` for everybody.

Corollary worth stating, because it has cost time: **a symmetric default is a
design decision, not an invented number.** A fair start *is* the design. What
deserves a warning label is a number picked with no reasoning at all — not one
picked because every power must begin equal.

## The seven engine defaults

A skirmish scenario carries **none** of these records, so a fair start runs on
the engine's own value for each:

| Record | What it defaults | Status |
|---|---|---|
| `ware` | starting warehouse stock | unrecovered |
| `cash` | starting treasury | unrecovered |
| `deve` | cell development levels | none developed — but see below |
| `tech` | starting technologies | none known |
| `tran` | transport capacity pool | unrecovered |
| `rail` | depots | none built |
| `rela` | diplomatic relations | unrecovered |

Verified by record-type comparison: `s10` has no `ware`, `cash`, `deve`, `tech`,
`tran`, `rail` or `rela` at all, while `s1` carries all seven.

These are **not recoverable from the corpus** — its whole evidence is that they
are absent. They are constants in the binary, which makes them one target class
rather than seven scattered guesses, and the strongest argument for decompiling
the engine rather than reading its disassembly.

`deve` gained a concrete instance of this while civilian units were being built.
The manual says farms and orchards adjacent to the capital begin at Level I, and
the corpus does **not** author that: of 350 capital-adjacent farm and orchard
tiles across the ten scenarios only 27 carry a `deve` record, and `s3` carries 59
records with not one of them there. So the rule is the engine's, exactly as this
table predicts, and it is still unimplemented. See
[development.md](development.md).

## Status

Doc filenames below are the intended names; none are written yet. Create one
the first time a mechanic is actually investigated, using the template at the
bottom of this file.

| Mechanic | Confidence | Doc | Implemented in | Tests |
|---|---|---|---|---|
| Industrial recipes and capacity | `inferred` | [production](production.md) | Core, Content, LegacyImport | generated + local corpus |
| Labour cost per production cycle | `inferred` | [production](production.md) | Core, Content, LegacyImport | generated + local corpus |
| Resource extraction and catchment | `inferred` | [extraction](extraction.md) | Core, Content, LegacyImport | generated + local corpus |
| Worker feeding and labour supply | `inferred` | [feeding](feeding.md) | Core, Content, LegacyImport | generated + local corpus |
| Which grade starves or falls ill | `guess` | [feeding](feeding.md) | Core | generated |
| Migration cap and price | `inferred`, on one `guess` | [migration](migration.md) | Core, Content, LegacyImport | generated |
| Which terrain a civilian may improve | `inferred` | [development](development.md) | Core, Content, LegacyImport | generated + local corpus |
| Which civilian improves which deposit | `inferred` | [development](development.md) | Core, Content, LegacyImport | generated + local corpus |
| How many turns a civilian's work takes | `guess` | [development](development.md) | Content | generated |
| Which deposits must be found before use | `inferred` | [prospecting](prospecting.md) | Core, Content, LegacyImport | generated + local corpus |
| Which ground a Prospector may search | `inferred` | [prospecting](prospecting.md) | Core, Content, LegacyImport | generated + local corpus |
| What a captured mine's new owner knows | `guess` | [prospecting](prospecting.md) | Core | generated |
| Whether the whole economy holds up over time | — | [soak](soak.md) | — | 100-turn soak, 7 powers |
| Trade clearing price | `guess` | _trade-pricing_ | — | — |
| Favoured-partner ranking | `guess` | _trade-pricing_ | — | — |
| Diplomatic relation deltas | `guess` | _relations_ | — | — |
| Council nomination + abstention curve | `guess` | _council_ | — | — |
| Tactical initiative order | `guess` | _initiative_ | — | — |
| Strategic initiative (contested province) | `guess` | _initiative_ | — | — |
| Town auto-industrialisation | `guess` | _town-development_ | — | — |
| Credit limit + interest curve | `guess` | _credit_ | — | — |

Industrial production is the first evidence-backed entry, but remains
`inferred` until controlled original-behaviour traces verify the resolver's
shortage and persistence semantics.

Labour per production cycle was the blocking unknown and is now priced from the
manual's tutorial, which costs one unit of clothing at two fabric and two
labour. It is worth being precise about how far that goes: it prices *one*
recipe, and it settles the rest only because every recipe the original ships
consumes two input units per unit of output, which makes the competing readings
of that sentence numerically identical. A recipe that broke 2:1 would separate
them, and none exists yet — the railyard, when it lands, is the first candidate.
`production.md` records the disassembly search that failed to find the rate, so
the next attempt does not repeat it.

Prospecting is the closest this table comes to a mechanic recovered without any
invention. The manual states the hidden five, the searchable terrain, the one
technology gate, and that knowledge is per Great Power and permanent; the corpus
then agrees with the terrain rule from the other direction, counting **4,449**
searchable tiles across the ten scenarios, which is the same number
`development.md` reached by counting barren hills and mountains for a different
purpose. It carries **no new guess** — a search reuses the work duration already
flagged there. What it does carry is a live consequence: **oil is unreachable in
imported content** until research exists, because the gate is real and nothing
can pass it. See [prospecting.md](prospecting.md).

Extraction carries its evidence at three different strengths at once, and its
document tabulates them rather than averaging them: the development levels are
corpus-verified (`deve` records are 1–3 everywhere), the base rates come from
observed play, and the doubling progression between levels is a deliberate
design choice nobody has measured. It sits at `inferred` because that is the
weakest thing load-bearing in it — a mechanic is only as trustworthy as the
number you would notice being wrong.

## Where to dig

The disassembly indexer (`tools/alf/`) attributes address ranges to original
source files via `assert()` anchors. Starting points for the modules that
hold the formulas above:

| Module | Role | Notable span | Anchors |
|---|---|---|---|
| `UDefenseMinister.cpp` | tactical AI | `004EC160`–`004ED4CF` (~5 KB, high) | 7 |
| `UCity.cpp` | **not the economy** — see `../disasm/ghidra.md` | `004B3080`–`004B427F` | 3 |
| `UCountry.cpp` | country state | `004DAF30`–`004DBB7F` (~3.1 KB, high) | 1 |
| `UCountryAuto.cpp` | strategic AI | `0053C2B0`–`0053E67F` (~9 KB, low) | 1 |
| `UAmbit.cpp` | diplomacy | `0049E6A0`–`0049E9CF`, `0049EB00`–`0049EE8F` | 3 |
| `UTacPlayer.cpp` | tactical player | — | 1 |

Query them with `python -m tools.alf.query func --name UCity`.

**One documented dead end.** The labour rate was hunted here first and not
found; `production.md` lists exactly what was searched, including why the
obvious pattern for `untrained*1 + trained*2 + expert*4` finds nothing in this
build. Read it before repeating that search.

**Prefer the decompiler for a formula**, but do not expect the module map to
lead you to one. `tools/alf/` answers "where" and "who calls this"; Ghidra
answers "what does this compute", which is what every entry above actually asks.
The catch is measured in `../disasm/ghidra.md`: the map names half the binary,
and it is the **UI half**, because every filename comes from an `assert()` and
asserts live in view code. A calibration run went looking for the labour cost —
a number already known from the manual — in the two best-named candidates and
found neither. `UCity.cpp` turned out not to be the economy at all.

**Temper expectations.** Assert density correlates with UI code, not gameplay
math — `UCityViews` has 73 anchors across 150 KB while `UCountryAuto` has
**one** across 13 KB. The aggregate "55.7% attributed" figure is carried by
view code; coverage is weakest exactly where we need it most. Two consequences:

1. Expect formula recovery to be slower than the headline suggests.
2. This is *why* the architecture insists every unknown gets a placeholder
   behind an interface. Archaeology must never sit on the critical path.

Two techniques that make the listing usable at all, both already handled by
the indexer and both easy to rediscover the hard way:

- Nearly every `call` targets a 5-byte incremental-link **thunk**, not the
  real function. Unresolved, the call graph reads as empty.
- The disassembler never covers `.data`, so C++ **vtables are invisible** in
  the listing. Reading them from the PE yields ~10,400 function starts versus
  ~5,300 from direct calls — in a binary this virtual, that's the dominant
  signal for finding functions.

## Priority

Ordered by impact x uncertainty, not by ease:

0. **The seven engine defaults** — every one of them is needed before a fair
   start can exist at all, they cannot come from the corpus, and they come as a
   set from one place. Cheapest thing on this list per number recovered, once
   there is a decompiler to read them with.
1. **Trade clearing price / favoured-partner ranking** — the central game loop,
   fully undocumented, and emergent (price depends on last turn's clearing,
   which depends on ranking, which depends on relations). Mitigated by an
   `ITradeMarket` interface so a fixed-price implementation ships first and
   never blocks the playable milestone.
2. **Relation deltas** — diffuse, many small triggers, easy to get 80% right
   and never notice. Model as an enumerable event->delta table so the trigger
   set is auditable.
3. **Combat/initiative constants** — structure is knowable, numbers are not.
   Keep every constant in data so recalibration is an edit, not a refactor.
4. **Town development, credit** — self-contained, moderate blast radius.
5. **Council curve** — needed latest, lowest blast radius. Deferring is fine.

## Document template

```markdown
# <Mechanic>

## Summary
One paragraph: what this controls and why it matters.

## Confidence
guess | inferred | verified — and what would raise it.

## Evidence
Addresses, quoted disassembly, manual page references, observed play.
Separate what was *observed* from what was *concluded*.

## Pseudocode
Our current best understanding, in plain terms.

## Where implemented
Type/method, plus the data keys holding its constants.

## Test data
Link to the input/output table pinning this down. If empty, say so.

## Open questions
What we still don't know.
```

## Ground rules

- **Findings flow one way**: disassembly -> a doc here -> code or data, plus a
  test. The game never reads the original binary at runtime.
- **A doc without a test is a hypothesis**, not a finding. The link from
  document to concrete test case is what stops this becoming a graveyard of
  notes.
- **Placeholders are fine and expected.** Every unknown gets an interface and a
  plausible default so implementation is never blocked on archaeology.
- **Record what was tried and failed.** A documented dead end saves the next
  attempt from repeating it.
