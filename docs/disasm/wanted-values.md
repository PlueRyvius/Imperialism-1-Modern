# Values wanted from the binary

A shopping list for anyone decompiling `Imperialism.exe`, in priority order. Each entry
says **what is wanted**, **how to recognise it**, and **how we will check the answer** —
the last matters because most of these already have a test that will fire if the value is
wrong.

Read [`ghidra.md`](ghidra.md) first for setup. Nothing here needs the game running.

**Two of the five entries below are struck through**, answered by the extraction recorded
in [`definitive-original-data.md`](definitive-original-data.md). They are kept rather than
deleted, because each was wrong about something instructive:

- **Entry 1 said there was no static ship table.** There is. Every search missed it because
  the fields are encoded, and the fingerprint was built from decoded values.
- **Entry 3 offered a cost fingerprint for the technology table.** Half of it was wrong,
  from a source with a column read one row down. Matching against it would have located a
  table shifted by one and confirmed a mistake.

Both failures have the same shape: **a fingerprint carries the errors of whatever source
it came from, and confirms them.** Where a reader function can be found instead, find it.
That is how every field in the naval table was labelled.

**Read the resource archive before disassembling anything.** `STR#ENU.GOB`'s help strings
state the production recipes, the power conversion and the worker conversion costs
outright. The labour rate this project hunted through 59 MB of listing for — and gave up
on — is printed there in English, and it corrected a shipped rule when it was read.

## How to send results back

Plain text or a table is fine. For anything that is an *array*, **the order is usually the
valuable part** — more so than the values, in two cases below — so please give entries in
the order they appear in memory, and say whether the array looked 0-based or 1-based if
that is visible from the indexing code.

If a value is stored scaled (×10, fixed-point, percent) please say so rather than
normalising, because we cannot tell afterwards.

---

## 1. ~~The ship type array~~ — RECOVERED

**Answered in full**, by
[`definitive-original-data.md`](definitive-original-data.md): the naval table at
`0x00698108`, fourteen 36-byte rows, plus six 30-entry commodity arrays at `0x00695B50`
for the build bills. Order, cargo, sea zones, combat stats and every build cost are in
the importer now.

Kept here for what the failure taught, because the same trap is waiting in the next hunt.

**The searches failed because they were looking for decoded values.** This entry said
"there is no static table — four pattern searches failed" and concluded the values were
assigned individually in a constructor. There *was* a static table, sitting in 36-byte
rows of nine dword slots, and almost every field it holds is encoded:

| What the manual prints | What the table stores |
|---|---|
| Firepower 20 | `2000` — stored ×100 |
| Armour 70 | `30` — stored as `100 - armour` |
| Hull 115 | `2800` — an internal damage-normalisation scale, not the printed number |
| Speed 5 | `5` in field **7**, which is a sea-zone allowance |
| Battle movement 7 | `7` in field **4** |

Of the eight numbers the fingerprint clustered on, only range and sea zones are stored as
printed. **A window of sixty could not have held six of them because six of them are not
in the file.** The lesson: search for what a table would *store*, and when the encoding is
unknown, find the reader rather than the data. Every field above was identified from its
accessor.

**Two specifics it settled.** The Frigate takes **2 arms**, not 3 — the discrepancy this
entry called out because arms later set the force landable at a beachhead. And the
**Freighter carries 16**, which was the last unknown cargo figure.

**The falsification test agreed without being fitted.** The corpus check had already
pinned that types 1–4 must be ungated, because 1816 skirmish powers with no technology
hold them. The recovered array's first four are Trader, Indiaman, Frigate and
Ship-of-the-Line. Ungated, exactly as required.

`ship` records convert now — 142 of them, against `EveryShipInTheCorpusIsAHullItsOwnerCouldHaveBuilt`.

---

## 2. The seven engine defaults

**The highest-value set on this list**, and the reason a decompiler was wanted at all. These
are what a *skirmish* starts a power with. They are constants in the binary and **cannot be
recovered from the corpus**, because the whole evidence is that the records are absent —
`s10` carries none of them and every power still starts equipped.

| Record | What it defaults | Current state |
|---|---|---|
| `ware` | starting warehouse stock | existence attested by the manual, **quantity is a guess** |
| `cash` | starting treasury | same — **quantity is a guess** |
| `tran` | transport capacity pool | **pure guess, and load-bearing**: at 0 an imported skirmish is unplayable |
| `deve` | starting cell development levels | assumed none |
| `rail` | depots built at the start | assumed none |
| `rela` | diplomatic relations | unrecovered, and nothing reads it yet |

`tech` — the seventh — is already recovered, from the manual: every power starts with High
Pressure Steam Engine and Seed Drill.

**How to recognise it.** Look for the code path that runs when a scenario has *no* record of
a given type. A skirmish load is the case to trace.

**How we will check it.** `tran` must be positive or nothing can leave the land. `ware` must
include lumber and steel, because the manual says a power builds its first mills from
"initial stockpiles of lumber and steel". Beyond that these are unfalsifiable from data we
hold — which is exactly why they are wanted.

---

## 3. ~~The technology table order~~ — RECOVERED, and the prices with it

**The order is the manual's printed order.** `STR#ENU.GOB` blocks #1073–#1075 carry all 28
names in progression order; the fan-wiki reordering we shipped at v19 is retracted.

**The two pinned counts this entry named as the check are UNVERIFIED against the
reversion.** `EveryAuthoredLevelInTheCorpusIsOneItsOwnerCouldHaveBuilt` and
`EveryRailedCellInTheCorpusIsOneItsOwnerCouldHaveBuilt` both return silently unless
`IMPERIALISM_SCENARIO_DIR` points at the full ten-scenario corpus, and the machine this
was done on holds only `s1`. The analysis says 380/4 and 1,140/0 cannot move — the two
orderings are a permutation within a prefix — but **that is a prediction, not a
measurement. Run both with a full corpus before treating them as confirmed.**

**The costs came with it**, from a 28-entry cash table at `0x0066AAE8`, and **twelve of the
twenty-six were wrong.** The fingerprint above was built from the fan-wiki list, and that
list's price column is off by one from Streamlined Hulls onwards: every entry carried the
*next* one's price. Had the search fingerprinted on it, it would have matched a table
shifted one row and been believed.

**A fingerprint is only as good as the source it comes from.** The costs were the one
column here described as something "we have plausible values for already". They were the
column that was wrong.

**Arrival years are derived, not stored.** The executable generates each non-starting
technology an inclusive pseudo-random turn-offset window (26 two-word entries at
`0x0066ABA4`). Reading offset *n* as year `1815 + n` puts 25 of the wiki's 26 observed
years inside their window and 19 exactly on the minimum, which is what pins the reading.

### Still wanted here: the prerequisite graph

The executable has one and it has not been read. Ours is the fan-wiki's, and after the
price slip it is the weakest column in the table — 16 entries naming 19 edges, all of
which happen to point backwards. **Recognise it** by that shape: a small per-technology
list of earlier indices. **Check it** against `EveryPrerequisiteSitsEarlierInTheTable`,
which the engine also enforces in `TechnologyDefinition`.

---

## 4. The trade clearing price

The oldest unknown in the project. We have the **direction** from the manual (demand up,
supply down, matched flat) and no magnitude at all.

**Wanted:** how a commodity's world price moves between turns. Specifically whether it is a
percentage step, a proportional function of the supply/demand ratio, or a table; whether
there is a dead band; and whether prices are bounded.

**How we will check it.** It slots straight into `ITradeMarket`, which exists to be replaced
— no other code changes. Sanity: the soak currently sells 1,534 units for about 1.2 million
over a century, so a curve that changes that by orders of magnitude is worth a second look.

Also wanted, same area: **whether the bidder ranking is what we think.** The manual says
relations plus subsidies; we currently use a placeholder (country id order) and have labelled
it as such.

---

## 5. Prices we hold on weak evidence

None of these blocks anything. All live in content, so correcting one is an edit.

| Value | Currently | Standing |
|---|---|---|
| Depot cash cost | 1,500 | owner's recollection |
| Port cash cost | 2,000 | owner's recollection; manual only says port > depot |
| Improvement, Level III | 3,000 | owner's recollection. **Levels I and II are corroborated at 100 and 1,000; III is not** |
| Rail across mountains | 300 | **the one ground the price list does not price**; we reuse swamp's figure |
| Civilian work turns | 3 | observed play, applied to every civilian type |
| ~~Migration cost per worker~~ | 1 each of canned food, clothing, furniture | **RECOVERED** — the help resources state the conversion outright, and the guess was right |

**A candidate for the depot and the port.** The `0x00695C50` block holds nine cost records,
each 2 paper plus an expert worker, with cash values 1,500 / 500 / 1,000 / 1,000 / 2,000 /
1,000 / 1,000 / 2,000 / 5,000. Nine is also the number of civilian types, and 2 paper plus
an expert worker is exactly the University's price for one. **This is a candidate, not an
answer** — the owning action label is unresolved, and reading it as the civilian purchase
list would explain the first figure matching our remembered depot cost only by coincidence.
Worth tracing the selector before anything is moved.

Also useful if it falls out: **whether oil and fuel are tradable.** They appear on neither
the trade roster nor the manual's untradable list, and we currently treat both as untradable.

---

## What is *not* wanted

Please do not spend time on these — they are settled well enough that a binary value would
not change anything:

- **The Resource Development Table** (yield per deposit per level). Transcribed from the
  manual and independently corroborated.
- **The technology gates on improvement levels**, and the four rail terrain gates. The rail
  gates in particular are the best-corroborated reading in the project: 1,140 corpus rail
  ends permitted, none not.
- **Gold at $200 and gems at $500 a unit.** The manual states both outright.
- **Commodity trade prices.** Transcribed from the game's own Bid and Offers screen, in three
  tiers, and the tiers agree with the recipe structure.
- **The scenario `year` epoch.** Settled: a `year` field is an offset from 1815, confirmed
  against two scenarios' own briefing text.
- **The production recipes and the labour rate.** Stated outright in `STR#ENU.GOB` and
  transcribed: nine recipes, all costing two labour a cycle.
- **The raw-resource yield curves.** The executable's table at `0x00696D98` agrees with the
  manual's Resource Development Table row for row. Its one apparent exception, fish at
  1/2/3/4 where the manual says fish is not improvable, is a source quantity rather than a
  development level — see `definitive-original-data.md`.
- **The legacy grid's six neighbour offsets.** `0x00696E70` gives NE, E, SE, SW, W, NW in
  that order, which is exactly the clockwise-from-NE reading Forge measured from the maps.
