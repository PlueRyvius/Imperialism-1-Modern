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
| **A community transcription of an in-game screen** | **good for exact numbers, and unverifiable.** See below |
| A mission scenario (`s1`, `s3`, `s9`, `s12`–`s14`) | **weak** — it shows what one designer authored for one mission |
| Observed play | **good** for shape, poor for exact numbers |

**The first row stopped being hypothetical.** Values recovered directly from
`Imperialism.exe` and `Data/STR#ENU.GOB` are recorded in
[`../disasm/definitive-original-data.md`](../disasm/definitive-original-data.md), and
they are now the source for the technology table, the thirteen ship classes, the
production recipes and the raw-resource yield curves. Where the binary and any other
source disagree, the binary wins and the other is **retracted rather than balanced
against it**.

The fifth row needs re-reading in that light. The technology price list is a fan-wiki
transcription of the original's own Investment screen: data-derived rather than
remembered, which put it above observed play, and second-hand and unreachable from
here, which put it below the manual. **Its price column turned out to be off by one
for twenty-four consecutive rows**, and its ordering turned out to be wrong. Its
arrival years, the one column with independent corpus support at the time, are the
one column that survived — 25 of 26 land inside the executable's own availability
windows.

The lesson is not that the row's weight was set too high. It is that **the
corroborated column was the good one and the uncorroborated ones were not**, which is
exactly what the scale was for. A transcription is worth what its cross-checks are
worth, column by column, and not as a single verdict on the source.

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
| `ware` | starting warehouse stock | **exists, per the manual** — quantity unrecovered |
| `cash` | starting treasury | **exists, per the manual** — quantity unrecovered, **and now load-bearing three times over** |
| `deve` | cell development levels | none developed — but see below |
| `tech` | starting technologies | **recovered — from the manual** |
| `tran` | transport capacity pool | unrecovered — **and now load-bearing** |
| `rail` | depots | none built |
| `rela` | diplomatic relations | unrecovered |

Verified by record-type comparison: `s10` has no `ware`, `cash`, `deve`, `tech`,
`tran`, `rail` or `rela` at all, while `s1` carries all seven.

These are **not recoverable from the corpus** — its whole evidence is that they
are absent. They are constants in the binary, which makes them one target class
rather than seven scattered guesses, and the strongest argument for decompiling
the engine rather than reading its disassembly.

**Three of the six that remain are now load-bearing**, and the manual moved two
of them further than expected. `ware` is no longer merely unrecovered: the manual
says outright that a power begins with "initial stockpiles of lumber and steel",
so its *existence* is attested and only the quantity is missing. That mattered
immediately — [transport.md](transport.md) concluded that a small network was an
inescapable trap, and it was wrong: an *empty warehouse* is the trap, and a power
that starts with something to build from buys its way out on the first turn.

`cash` is the same story a slice later. "Each Great Power begins the game with a
limited amount of cash which is totally inadequate to meet its needs" attests the
treasury and never its size, and with construction priced in money the size now
decides whether an imported skirmish can build its first depot. **Three slices on it
decides three things**: that first depot, how many Level II improvements a power can
afford, and — since technology is bought with cash and charged last — whether a late
technology is reachable inside a century at all. See [money.md](money.md).

`tran` is the third. It used to be a number nothing read; it now sets a country's
opening headcount, because capacity bought on turn one does not carry until turn
two while the workers eat on turn one regardless.

All three are guesses in content today, and all three are better arguments for
reading the binary than anything else on this list.

**One of them has fallen, and not to a decompiler.** The manual states the
`tech` default outright: "every player always starts with the first two
technologies listed below: High Pressure Steam Engine and Seed Drill." That is
why the row above reads recovered while the other six do not, and it is worth
drawing the lesson — the corpus could never have supplied it, and neither had
anyone read the manual for it. Two of the six remaining (`ware`, `cash`) are
plain numbers that the same source might yet state somewhere; it has now been
searched for both and states neither, though it does price what fills them. Look
there before reaching for Ghidra. See [technology.md](technology.md).

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
| Labour cost per production cycle | **`verified`** — the original's own recipe help strings | [production](production.md) | Core, Content, LegacyImport | generated + local corpus |
| Resource extraction and catchment | `inferred` | [extraction](extraction.md) | Core, Content, LegacyImport | generated + local corpus |
| When a depot counts as connected | `inferred` | [extraction](extraction.md) | Core | generated + local corpus |
| Worker feeding and labour supply | `inferred` | [feeding](feeding.md) | Core, Content, LegacyImport | generated + local corpus |
| Which grade starves or falls ill | `guess` | [feeding](feeding.md) | Core | generated |
| Migration cap and price | `inferred`, on one `guess` | [migration](migration.md) | Core, Content, LegacyImport | generated |
| Which terrain a civilian may improve | `inferred` | [development](development.md) | Core, Content, LegacyImport | generated + local corpus |
| Which civilian improves which deposit | `inferred` | [development](development.md) | Core, Content, LegacyImport | generated + local corpus |
| How many turns a civilian's work takes | `inferred`, from observed play | [development](development.md) | Content | generated |
| What an improvement costs in cash | `inferred` — Levels I and II **corroborated by a second source**, III not | [development](development.md) | Core, Content, LegacyImport | generated |
| Which deposits must be found before use | `inferred` | [prospecting](prospecting.md) | Core, Content, LegacyImport | generated + local corpus |
| Which ground a Prospector may search | `inferred` | [prospecting](prospecting.md) | Core, Content, LegacyImport | generated + local corpus |
| Which technology opens which improvement level | `inferred` | [technology](technology.md) | Core, Content, LegacyImport | generated + local corpus |
| What a `tech` id names | `inferred` | [technology](technology.md) | LegacyImport | local corpus |
| The technologies every power starts with | `inferred` | [technology](technology.md) | Core, Content, LegacyImport | generated |
| What technology costs | **`verified`** — the executable's cash table. **Twelve of 26 moved** | [technology](technology.md) | Core, Content, LegacyImport | generated |
| A technology's prerequisites | `inferred` — from the price list, and now its weakest column | [technology](technology.md) | Core, Content, LegacyImport | generated |
| When a technology becomes available | `inferred` — derived from the executable's windows, **corroborated 25 of 26** | [technology](technology.md) | Core, Content, LegacyImport | generated + local corpus |
| **The order of the technology table** | **`verified`** — the executable's, and it is the manual's printed order | [technology](technology.md) | LegacyImport | local corpus |
| What a scenario's `year` field means | `inferred` — **the briefings state two of them outright** | [technology](technology.md) | LegacyImport | local corpus |
| When research is charged against the treasury | **a chosen rule** — last, after building | [technology](technology.md) | Core | generated |
| How much a network can carry, and what raises it | `inferred` | [transport](transport.md) | Core, Content, LegacyImport | generated + local corpus |
| Whether un-carried output keeps | `guess` | [transport](transport.md) | Core | generated |
| What a network starts with | `guess` | [transport](transport.md) | Content, LegacyImport | generated |
| What a warehouse starts with | `inferred` existence, `guess` quantity | [transport](transport.md) | Core, Content, LegacyImport | generated |
| What gold and gems are worth in cash | `inferred` — **both rates stated outright** | [money](money.md) | Core, Content, LegacyImport | generated + local corpus |
| Whether carrying them costs capacity | `inferred` | [money](money.md) | Core | generated |
| What a treasury starts with | `inferred` existence, `guess` quantity — **load-bearing three times over now** | [money](money.md) | Core, Content, LegacyImport | generated + local corpus |
| Which terrain admits rail, and which depots | `inferred` | [engineer](engineer.md) | Core, Content, LegacyImport | generated + local corpus |
| Where an Engineer may build each structure | `inferred` | [engineer](engineer.md) | Core, Content, LegacyImport | generated |
| What a depot and a port cost | `inferred`, from observed play | [engineer](engineer.md) | Content, LegacyImport | generated |
| What rail costs, per terrain | `inferred` — from the price list. **Was a `guess`** | [engineer](engineer.md) | Core, Content, LegacyImport | generated |
| What mountains' rail costs | `guess` — the one ground the list skips | [engineer](engineer.md) | LegacyImport | generated |
| What a link crossing two grounds costs | **a chosen rule** — the dearer end | [engineer](engineer.md) | Core | generated |
| Whether the whole economy holds up over time | — | [soak](soak.md) | — | 100-turn soak, 7 powers |
| How trade clears: offers, bids, delivery, who pays | `inferred` — **the manual states nearly all of it** | [trade](trade.md) | Core, Content, LegacyImport | generated |
| What the fifteen traded commodities are worth | `inferred` — **observed** from the game's own screen | [trade](trade.md) | Core, Content, LegacyImport | generated |
| Which commodities are tradable at all | `inferred` — **observed, and it agrees with the manual three times** | [trade](trade.md) | Core, Content, LegacyImport | generated |
| The commodity order holds are spent in | `inferred` — observed | [trade](trade.md) | Core, Content, LegacyImport | generated |
| **Trade clearing price** — how far a price moves | `guess`, quarantined behind `ITradeMarket` | [trade](trade.md) | Core, Content | generated |
| **Favoured-partner ranking** | **a placeholder**, not a rule — wants diplomacy | [trade](trade.md) | Core | generated |
| Merchant marine, and what limits it | `inferred` — the manual states the rules, the executable the cargo | [trade](trade.md) | Core, Content, LegacyImport | generated |
| The fleet a power opens with | `inferred` — **all three skirmishes agree**, and the class is the executable's | [trade](trade.md) | Core, Content, LegacyImport | generated + local corpus |
| What a `ship` type index means | `inferred` — **corpus-corroborated**, 1-based | [trade](trade.md) | — | local corpus |
| **The ship array's order, cargo, sea zones, build costs and combat stats** | **`verified`** — the executable's naval and cost tables | [trade](trade.md) | Core, Content, LegacyImport | generated |
| The raw-resource yield curves | **`verified`** — the executable's table agrees with the manual row for row | [extraction](extraction.md) | Core, Content, LegacyImport | generated + local corpus |
| What the Capitol charges per recruit | **`verified`** — the worker-conversion help string | [migration](migration.md) | Core, Content, LegacyImport | generated |
| Diplomatic relation deltas | `guess` | _relations_ | — | — |
| Council nomination + abstention curve | `guess` | _council_ | — | — |
| Tactical initiative order | `guess` | _initiative_ | — | — |
| Strategic initiative (contested province) | `guess` | _initiative_ | — | — |
| Town auto-industrialisation | `guess` | _town-development_ | — | — |
| Credit limit + interest curve | `guess` | _credit_ | — | — |

Industrial production is the first evidence-backed entry, but remains
`inferred` until controlled original-behaviour traces verify the resolver's
shortage and persistence semantics.

Labour per production cycle was the blocking unknown and is **resource-backed
now**: the original's own help strings price all nine of its recipes at two
labour. That closes an argument this page carried for several slices. The manual
prices exactly one recipe — two fabric and two labour for a unit of clothing —
which admits two labour per cycle, one per input unit, or two per unit of output,
and this page said no shipped recipe separated them.

**One did, and it was on the list.** Food processing takes four input units and
makes two units of canned food, so the flat reading prices it at two and the other
two at four. It was miscounted as agreeing because the recipe is 2:1 as a *ratio*.
The flat rate is the original's; the input-total rule this engine shipped is
retracted, and canned food's labour cost halves. **The prediction that the railyard
would be the first test was retracted separately, and correctly** — its recovered
recipe is 2 labour, 1 steel, 1 lumber, exactly what `transport.md` shipped.

The lesson is not about labour. It is that **"no case distinguishes them" is a claim
to re-derive, not to inherit** — this one was written once, restated three times,
and false the whole way.

Technology is the largest single recovery here and the one that most changed
what was already built: the manual's Benefits of Technology Table gates **every**
improvement level bar a mine opening at Level I, where the engine had let any
Farmer walk a tile to the top of its curve for free. The `tech` record's ids
resolve against a printed order, which was falsified against the corpus
before anything was built on it — 380 authored levels permitted, 4 not, and the
decisive case is `s3`, whose powers hold *unequal* sets and produce no
contradiction at all. See [technology.md](technology.md).

**Buying it landed next, and it turned three gates from dead code into rules.**
Until then a `tech` record was the only source of knowledge in the engine, so the
improvement ladder, the Engineer's rail terrains and oil prospecting could only ever
be tested shut — the soak had to call `GrantTechnology` outright to see one open.
The prices, prerequisites and arrival dates come from the price list.

Three things about that slice belong on this page rather than only in its own
document, because each is a lesson about evidence:

- **A negative result is a result.** The price list reorders six entries, and
  reordering them changes which technologies every shipped power holds. All three
  available corpus checks were run under both orderings and **none discriminates** —
  not because the orderings agree, but because the one case where they genuinely
  disagree (a power holding exactly five technologies) does not occur in the corpus.
  The order therefore shipped on source quality, which is a weaker footing than
  anything else in the chain, and it said so.

  **The executable has since settled it, and the reorder was wrong.** The recovered
  order is the manual's printed one. Worth keeping the whole bullet rather than
  rewriting it, because the negative result was *correct* and did its job: it said the
  order rested on nothing measurable, which is why the reversion cost exactly one
  assertion. **Labelling a weak link is what makes it cheap to replace.**

  **A second lesson landed on top of it, less flattering.** The two falsification counts
  have *not* been re-measured against the reversion: their tests read
  `IMPERIALISM_SCENARIO_DIR`, return silently when it is unset, and the machine this was
  done on holds one scenario of ten. A green suite proved nothing about them. That is the
  `CLAUDE.md` convention — "skip visibly, never iterate an empty corpus" — failing in its
  C# form, where the gate is a bare `return`. **A corpus check that can pass without a
  corpus is a check you have to remember to run**, and remembering is exactly what the
  convention exists to avoid.
- **A number nothing reads is a bug waiting to be load-bearing.** A scenario's
  `year` field is an offset from 1815 and the importer read it as an absolute year.
  Nothing noticed for four slices because nothing read the year. This is now the
  fourth instance of the pattern `tran` and `cash` established, and the lesson is
  worth generalising: **an unread field is unverified, not correct.**
- **Corroboration can arrive from an unexpected column.** The price list's arrival
  years were transcribed for the Investment screen and turned out to confirm the
  year epoch: three of the four dated missions grant nothing that has not yet
  arrived, and `s9` sits exactly on a boundary year. Neither fact was sought.

  **That column is the only one of the three that survived contact with the binary**,
  and its corroboration is why. The executable stores availability as a pseudo-random
  turn-offset window rather than a year, and 25 of those 26 transcribed years fall
  inside their window. The uncorroborated columns beside it — order and price — were
  both wrong. Corroboration is not decoration on a source; **it is the part of the
  source you are allowed to keep.**

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

The Engineer's terrain gates are the **best-corroborated reading in the project**
and it is worth saying why, because the technology ladder set the pattern and
this beats it. Reading the Benefits of Technology Table's rail column against the
corpus gives **1,140 rail ends permitted and none not**, and the check is not
vacuous: `s9` and `s12`, whose powers lack Compound Steam Engine, author 137 rail
links with not one hill among them, while `s1`, whose powers hold it, rails
forty-two. And no shipped power holds Dynamite while no shipped scenario rails a
single mountain. See [engineer](engineer.md).

Against that, **the construction prices used to be the weakest numbers here and
one of them has been fixed.** Rail was pure invention — a flat 500, reasoned from
nothing — and the price list charges by the ground crossed, 100 to 300 across five
terrains. That is a guess becoming an observation, and the flat figure was not
merely superseded but *wrong*: no single number can be right when the real one
varies threefold. The depot and the port remain the owner's recollection, and the
price list does not price either, so they stand unchallenged. Mountains are the one
ground it skips and the one guess left in that table.

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

**One documented dead end, and it had an open door beside it.** The labour rate was
hunted here first and not found; `production.md` lists exactly what was searched.
It was in `Data/STR#ENU.GOB` the whole time, written in English in the help text for
every recipe. **Read the resource archive before disassembling anything** — it also
states the power conversion and the worker training costs, none of which anyone had
tried to find in code.

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

   **The mitigation worked and the plan held.** Trade shipped with the interface in
   place, and the mechanism turned out to be the *documented* part — the manual states
   offers, bids, delivery timing, hold accounting, who carries and the price
   *direction*. What was left undocumented is exactly two things: how far a price
   moves, which is behind `ITradeMarket`, and which bidder gets first refusal, which
   is a labelled placeholder waiting on diplomacy. The prices themselves came from the
   game's own screen and are not guesses at all. **This entry is smaller than it
   looked**, and what remains of it now sits behind item 0's decompiler rather than
   ahead of it. See [trade.md](trade.md).
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
