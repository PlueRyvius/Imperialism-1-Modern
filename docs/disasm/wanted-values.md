# Values wanted from the binary

A shopping list for anyone decompiling `Imperialism.exe`, in priority order. Each entry
says **what is wanted**, **how to recognise it**, and **how we will check the answer** —
the last matters because most of these already have a test that will fire if the value is
wrong.

Read [`ghidra.md`](ghidra.md) first for setup. Nothing here needs the game running.

## How to send results back

Plain text or a table is fine. For anything that is an *array*, **the order is usually the
valuable part** — more so than the values, in two cases below — so please give entries in
the order they appear in memory, and say whether the array looked 0-based or 1-based if
that is visible from the indexing code.

If a value is stored scaled (×10, fixed-point, percent) please say so rather than
normalising, because we cannot tell afterwards.

---

## 1. The ship type array

**Blocking work right now.** The importer reads the corpus's 142 `ship` records but cannot
convert them, because a record's `type` is a bare index into this array and we do not know
the order.

**Wanted, in array order:**

| Field | Notes |
|---|---|
| **Order and names** | 13 classes expected: 5 merchant, 8 warship |
| **Cargo holds** | We have Trader 2, Indiaman 4, Steamship 8, Clipper 4. **Freighter unknown.** Warships should be 0 |
| **Build cost** | Fabric, lumber, arms, steel, coal per class. **We have none we trust** — see below |
| **Technology prerequisite** | Expect Streamlined Hulls → Clipper, Paddlewheels → Steamship and Raider |

**How to recognise it.** There is no static table — four pattern searches failed (executable
at 1/2/4-byte widths, strided struct arrays to stride 64, order-independent windows, and
clustered `mov` immediates across the 59 MB listing). So the values are almost certainly
**assigned individually in a constructor or initialiser**. Find that function and read its
immediates in order.

**Use the combat stats as a fingerprint.** These are known from the manual, so a function
storing them *is* the ship initialiser:

| Ship | FRP | RNG | ARM | HULL | BATTMV | Speed |
|---|---|---|---|---|---|---|
| Frigate | 3 | 5 | 10 | 35 | 4 | 3 |
| Ship-of-the-Line | 6 | 6 | 20 | 65 | 3 | 2 |
| Raider | 3 | 7 | 20 | 30 | 7 | 5 |
| Ironclad | 5 | 8 | 55 | 50 | 5 | 3 |
| Armoured Cruiser | 6 | 9 | 50 | 40 | 8 | 6 |
| Advanced Ironclad | 10 | 10 | 60 | 70 | 6 | 4 |
| Battle Cruiser | 18 | 13 | 55 | 90 | 9 | 6 |
| Dreadnought | 20 | 13 | 70 | 115 | 7 | 5 |

Cargo and build costs should be adjacent fields on the same records.

**One specific discrepancy to settle:** **Frigate arms is 2 in one source and 3 in
another.** Ship-of-the-Line's 5 agrees in both. This matters beyond the build bill — the
arms that went into a warship later set the force size landable at a beachhead in one turn.

**How we will check it.** `LegacyWorldConverterTests` has a falsification test ready: under
the correct order, **all 142 records / 307 ships must be hulls their owner could have
built** — nobody granted a Clipper without Streamlined Hulls, etc. We already know the index
is **1-based**, because 0-based produces 9 contradictions (a Clipper in an 1816 skirmish
whose powers hold no technology, plus five in `s13`/`s14`) where 1-based produces zero. So a
proposed order that makes any record illegal is wrong.

Corpus usage, if it helps identify classes: types **1–9** appear. Types 1, 3, 4 are held by
1816 skirmish powers with **no technology at all**, so those three must be ungated. Types 5,
7, 8, 9 appear only in `s1` (1882).

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

## 3. The technology table order

**Three independent corpus checks have failed to settle this**, so the binary is the only
remaining source. The two candidate orderings differ at six positions:

| Position | Manual's printed order | Fan-wiki order (what we ship) |
|---|---|---|
| 4 | Streamlined Hulls | Iron Railroad Bridge |
| 5 | Square-Set Timbering | Feed Grasses |
| 6 | Iron Railroad Bridge | Square-Set Timbering |
| 7 | Feed Grasses | Streamlined Hulls |
| 13 | Rifled Artillery | Breech-Loading Rifles |
| 14 | Breech-Loading Rifles | Rifled Artillery |

**Wanted:** the 28 entries in array order. Costs, arrival years and prerequisites too if
they are visible, though we have plausible values for all three already.

**How to recognise it.** Same shape as the ships — likely an initialiser. Fingerprint by
cost, which we believe runs 1,000 / 1,500 / 3,000 / 6,000 / 7,000 / 10,000 / 12,000 /
20,000 / 25,000 / 40,000 / 100,000 / 120,000 / 150,000. The first two entries should have
**no cost at all** (every power starts with them).

**How we will check it.** Two pinned falsification counts must not move: **380 authored
development levels permitted and 4 not**, and **1,140 rail ends permitted and 0 not**. Both
already come out identical under either ordering, so they cannot *confirm* an order — but
they will reject a third one.

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
| Migration cost per worker | 1 each of canned food, clothing, furniture | guess; the manual names the commodities and never the quantities |

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
