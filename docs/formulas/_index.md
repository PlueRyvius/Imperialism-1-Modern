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

## Status

Doc filenames below are the intended names; none are written yet. Create one
the first time a mechanic is actually investigated, using the template at the
bottom of this file.

| Mechanic | Confidence | Doc | Implemented in | Tests |
|---|---|---|---|---|
| Industrial recipes and capacity | `inferred` | [production](production.md) | Core, Content, LegacyImport | generated + local corpus |
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

## Where to dig

The disassembly indexer (`tools/alf/`) attributes address ranges to original
source files via `assert()` anchors. Starting points for the modules that
hold the formulas above:

| Module | Role | Notable span | Anchors |
|---|---|---|---|
| `UDefenseMinister.cpp` | tactical AI | `004EC160`–`004ED4CF` (~5 KB, high) | 7 |
| `UCity.cpp` | economy | `004B3080`–`004B427F` (~4.6 KB, high) | 3 |
| `UCountry.cpp` | country state | `004DAF30`–`004DBB7F` (~3.1 KB, high) | 1 |
| `UCountryAuto.cpp` | strategic AI | `0053C2B0`–`0053E67F` (~9 KB, low) | 1 |
| `UAmbit.cpp` | diplomacy | `0049E6A0`–`0049E9CF`, `0049EB00`–`0049EE8F` | 3 |
| `UTacPlayer.cpp` | tactical player | — | 1 |

Query them with `python -m tools.alf.query func --name UCity`.

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
