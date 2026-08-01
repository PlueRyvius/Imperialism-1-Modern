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

| Mechanic | Confidence | Doc | Implemented in | Tests |
|---|---|---|---|---|
| Trade clearing price | `guess` | `trade-pricing.md` | — | — |
| Favoured-partner ranking | `guess` | `trade-pricing.md` | — | — |
| Diplomatic relation deltas | `guess` | `relations.md` | — | — |
| Council nomination + abstention curve | `guess` | `council.md` | — | — |
| Tactical initiative order | `guess` | `initiative.md` | — | — |
| Strategic initiative (contested province) | `guess` | `initiative.md` | — | — |
| Town auto-industrialisation | `guess` | `town-development.md` | — | — |
| Credit limit + interest curve | `guess` | `credit.md` | — | — |

Nothing is above `guess` yet — this file exists so that stays visible rather
than being quietly forgotten.

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
