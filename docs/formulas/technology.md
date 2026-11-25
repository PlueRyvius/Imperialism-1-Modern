# Technology, and the levels it gates

## Summary

The manual carries a **Benefits of Technology Table** — twenty-eight entries with
names, what each unlocks, prerequisites and approximate arrival dates. It is the
single densest piece of recovered rules in this project, and it says something the
engine had wrong: **every improvement level is gated by a technology, with one
exception**. Before this, any Farmer walked any tile to the top of its curve for
free.

It also settles one of the [seven engine defaults](_index.md#the-seven-engine-defaults)
that this index called unrecoverable: "every player always starts with the first
two technologies listed below: High Pressure Steam Engine and Seed Drill." That
is `tech` recovered from the manual rather than from a decompiler, and it is the
first of the seven to fall.

**This is a gate, not a wall.** Because a game starts holding those two, a fresh
1815 start can improve grain and orchards to Level I and open mines at Level I on
its first turn. What stops is the free walk to Level 3.

## Every one of those gates used to be permanently shut

Until version 19 a `tech` record was the only source of knowledge in the engine.
There was no research, so **three separate slices of gate machinery were dead code
in a real game**: the improvement ladder above, the Engineer's four rail terrains,
and oil prospecting — which `prospecting.md` recorded plainly as "oil is
unreachable in imported content". The soak could only see a gate open by calling
`GrantTechnology` outright on turn 50.

Version 19 builds the Investment screen: **prerequisites, arrival dates, and buying
with cash.** It closes the loop the treasury opened — gold and gems pay for
technology, technology opens better improvement, better improvement reaches more
gold. It needed [money.md](money.md) first, for the same reason the Engineer did.

Three things came out of it beyond the buying itself, and each is recorded in its
own section below: a **price list** the owner supplied, a **reordering** of the
table that the corpus turned out to be unable to judge, and a **live bug** — a
scenario's `year` field is an offset from 1815 and this importer read it as a year.

**All three have since been overturned by the executable itself.** The
reordering is retracted — the recovered order is the manual's printed one — and
twelve of the price list's twenty-six prices were wrong, all in the same direction
and for the same reason, and the prerequisite graph is now read directly. The sections below record what happened rather than
what was believed; nothing here is quietly swapped.

## Confidence

`inferred`, and strongly so for the ladder; the id mapping is corroborated
against the corpus rather than assumed.

| Claim | Support |
|---|---|
| Which technology opens which rung | **manual**, stated per entry, and agreeing with the seven gates transcribed earlier in `../reference/manual-mechanics.md` |
| Every player starts with the first two | **manual**, stated outright |
| A mine opens at Level I ungated | **manual** — no technology is named for it, and the Miner is one of the four civilians buildable from the start |
| `tech` is `[country, id]`, id a 1-based index into the table | **corpus-corroborated** — see below |
| Technology is bought with cash, and cannot be kept secret | **manual**, and now modelled |
| What each one costs | **the executable** — a 28-entry cash table the technology store reads |
| **The table's order** | **the executable**, and it is the manual's printed order |
| Arrival dates | **derived** from the executable's turn-offset windows; the price list corroborates 25 of 26 |
| **One fixed year per technology** | **a simplification** of the executable's pseudo-random window |
| Prerequisites | **the executable** — a 29-row table of two raw 16-bit IDs read by the technology store |
| **A scenario `year` field is an offset from 1815** | **the scenarios' own briefing text**, and corpus-corroborated |
| Research runs last, after construction and improvement | **a chosen rule** |
| A prerequisite chain cannot be bought in one turn | **the original's behaviour**, from the owner |

## The table

**Order and cost are the executable's.** `STR#ENU.GOB` blocks #1073–#1075 hold all
twenty-eight names in progression order, and the technology store reads a 28-entry
cash table at `0x0066AAE8` indexed by that same position. Both are recorded in
[`../disasm/definitive-original-data.md`](../disasm/definitive-original-data.md).
The technology-store reader at `0x005B0A90` reads a 29-row table at `0x0066AC10`.
Rows are the 1-based technology IDs, row 0 is the zero sentinel, and each row holds
up to two raw 16-bit prerequisite IDs. See
[`../disasm/definitive-original-data.md`](../disasm/definitive-original-data.md).

Cost in cash; a dash is no prerequisite. Years are derived; see below.

| # | Technology | Cost | Available | Prerequisites |
|---|---|---|---|---|
| 1 | High Pressure Steam Engine | — | 1815 | — |
| 2 | Seed Drill | — | 1815 | — |
| 3 | Cotton Gin | 1,000 | 1816 | — |
| 4 | Streamlined Hulls | 1,000 | 1821 | — |
| 5 | Square-Set Timbering | 1,500 | 1821 | High Pressure Steam Engine |
| 6 | Iron Railroad Bridge | 1,500 | 1821 | High Pressure Steam Engine |
| 7 | Feed Grasses | 1,500 | 1821 | — |
| 8 | Spinning Jenny | 1,500 | 1826 | Feed Grasses, Cotton Gin |
| 9 | Paddlewheels | 3,000 | 1826 | — |
| 10 | Steel and Iron Plows | 3,000 | 1831 | Seed Drill |
| 11 | Bessemer Converter | 3,000 | 1836 | — |
| 12 | Compound Steam Engine | 6,000 | 1836 | Iron Railroad Bridge |
| 13 | Rifled Artillery | 7,000 | 1841 | — |
| 14 | Breech-Loading Rifles | 10,000 | 1841 | Bessemer Converter |
| 15 | Advanced Iron Working | 12,000 | 1846 | — |
| 16 | Power Loom | 12,000 | 1846 | Spinning Jenny |
| 17 | Mechanical Reaper | 12,000 | 1851 | Steel and Iron Plows |
| 18 | Commercial Fertiliser | 12,000 | 1856 | Steel and Iron Plows |
| 19 | Oil Drilling | 12,000 | 1856 | — |
| 20 | Barbed Wire | 25,000 | 1861 | Feed Grasses |
| 21 | Steel Armour Plate | 20,000 | 1866 | Advanced Iron Working |
| 22 | Large Artillery | 40,000 | 1871 | Rifled Artillery |
| 23 | Dynamite | 40,000 | 1871 | Square-Set Timbering, Compound Steam Engine |
| 24 | Marine Engineering | 40,000 | 1871 | Paddlewheels, Steel and Iron Plows |
| 25 | Machine Guns | 40,000 | 1876 | Breech-Loading Rifles |
| 26 | Chemistry | 100,000 | 1876 | Oil Drilling |
| 27 | Improved Range-Finding | 120,000 | 1881 | Marine Engineering |
| 28 | Internal Combustion | 150,000 | 1881 | Chemistry |

**Names stay the manual's** where the sources disagree: "Steel and Iron Plows" over
"Steel Plows", "Fertiliser" over "Fertilizer", "Armour" over "Armor". The keys are
name-derived and already shipped, so renaming would break content for nothing.

**The first two are not purchasable rather than free.** The cash table gives both a
zero and every power starts holding them. `Cost == null` says "never on the screen",
which is a different fact from a price of zero, and it is also what makes a package
older than v19 behave exactly as it did.

### The price list was off by one, and the binary shows exactly where

The owner supplied a transcription of
`imperialism.fandom.com/wiki/Technology_(Imp1)` — a community transcription of the
original's own Investment screen — and v19 shipped its order, prices and dates. The
host 402s from this environment, so this document was the record of it. **Twelve of
the twenty-six prices have moved.**

They did not move at random. From Streamlined Hulls onwards, **each price the list
carried is the price of the next technology in the recovered order**:

| Technology | Price list | Executable | Where the list's number belongs |
|---|---|---|---|
| Streamlined Hulls | 1,500 | **1,000** | Square-Set Timbering's |
| Spinning Jenny | 3,000 | **1,500** | Paddlewheels' |
| Bessemer Converter | 6,000 | **3,000** | Compound Steam Engine's |
| Compound Steam Engine | 7,000 | **6,000** | Rifled Artillery's |
| Rifled Artillery | 10,000 | **7,000** | Breech-Loading Rifles' |
| Breech-Loading Rifles | 12,000 | **10,000** | Advanced Iron Working's |
| Oil Drilling | 25,000 | **12,000** | Barbed Wire's |
| Barbed Wire | 20,000 | **25,000** | Steel Armour Plate's |
| Steel Armour Plate | 40,000 | **20,000** | Large Artillery's |
| Machine Guns | 100,000 | **40,000** | Chemistry's |
| Chemistry | 120,000 | **100,000** | Improved Range-Finding's |
| Improved Range-Finding | 150,000 | **120,000** | Internal Combustion's |

Internal Combustion, the last entry, has nothing after it to borrow from and is the
one late price that was already right. A slip that reproduces itself for
twenty-four consecutive rows is not two sources disagreeing about a game; **it is
one source wrong in a legible way** — a column read one row down, most likely
against the header.

That was a reason to trust the list's remaining column less rather than more. The
executable has now settled it too: its reader and raw table recover the prerequisite
graph exactly, including the rows where the list was wrong.

### The years are derived, and 25 of 26 corroborate the derivation

**The executable stores no year.** It generates each of the twenty-six non-starting
technologies an inclusive pseudo-random *turn-offset* window, from 26 two-word
entries at `0x0066ABA4`. Cotton Gin's is 1–5, Internal Combustion's is 66–70.

Read as one offset per year from the 1815 epoch the corpus already established, the
price list's observed years line up with those windows almost exactly:

- **19 of 26 sit on the window's minimum**, to the year.
- **25 of 26 fall inside their window.** Chemistry is the single miss, one year
  early — and it is also one of the two rows the list itself had out of order.

That is two independent sources agreeing on a mapping neither states. It also
explains the list's scattered later dates, which this document previously recorded
as ranges the manual called "approximate": 1871, 1873 and 1874 for three
technologies sharing the window 1871–1875 are **three draws from one range**, not
three dates.

So the year above is `1815 + window minimum` — the earliest anybody may buy, which
is what `AvailableFrom` means. Seven of the twenty-six moved, all later than 1856
and all by one to three years.

### Two things it settles for free

**Dynamite really does sit at position 23.** `technology.md` asked whether it
might sit earlier, which would have made the four `s1` timber exceptions vanish.
The list agrees it is 23, so **the four are authoring liberty and not a
transcription error** — closing an open question without any new work.

**A mine reaches Level II at 4 units.** The list's "Miner upgrades mines to level
2 (4 units)" independently corroborates the manual's Resource Development Table,
which `extraction.md` transcribes.

## The gate table

| Deposit | Level I | Level II | Level III |
|---|---|---|---|
| Grain | Seed Drill | Steel and Iron Plows | Mechanical Reaper |
| Fruit (orchards) | Seed Drill | Steel and Iron Plows | Commercial Fertiliser |
| Cotton | Cotton Gin | Spinning Jenny | Power Loom |
| Wool | Feed Grasses | Spinning Jenny | Power Loom |
| Livestock | Feed Grasses | Barbed Wire | Chemistry |
| Timber | Iron Railroad Bridge | Compound Steam Engine | Dynamite |
| Coal, iron, gold, gems | **none** | Square-Set Timbering | Dynamite |
| Oil | Oil Drilling | Chemistry | Internal Combustion |

Fish and horses are absent because no civilian improves either.

The table's other columns are for systems that do not exist here — regiments,
ships, the Refinery, rail through particular terrain. All twenty-eight names are
transcribed anyway, in printed order, because **the order is what a `tech` record
is indexed against**.

## Reading the `tech` ids

`tech` is `[country, id]` with a small 1-based id and nothing naming it. What the
binary corpus shows:

| | `tech` records | ids | `deve` |
|---|---|---|---|
| `s1` | 147 | 1–21, all seven powers alike | 320 |
| `s3` | 98 | 1–14, **unequal**: 9, 13 and 14 per power | 59 |
| `s5` (generated) | 42 | 1–6 | 94 |
| `s9`, `s12` | 63 | 1–9 | 0 |
| `s13`, `s14` | 42 | 1–6 | 4 |
| `s10`, `s11`, `s15` | 0 | — | 0 |

Ids are contiguous from 1, and the count grows with the scenario's year. A
skirmish grants none at all and its powers still farm, which is the two engine
defaults showing through.

### The falsification test

Reading id N as the Nth row of the manual's table is an inference, so it was
tested before anything was built on it: **every level a scenario authors,
compared against what its owner's technologies would let a civilian build.**

Across the four originals carrying both records: **380 permitted, 4 not.** The
four are all one thing — timber at Level III, in one country of `s1`, needing
Dynamite.

**`s3` is the decisive case.** Its powers hold *unequal* sets — one has 9
technologies, another 13, the rest 14 — and it produces **no** contradiction at
all. A shifted table would fire at once on the power holding only nine. That is
much stronger evidence than the uniform scenarios could ever give, and it is why
`s3` earns its own sentence here.

`EveryAuthoredLevelInTheCorpusIsOneItsOwnerCouldHaveBuilt` keeps the check in the
suite with both numbers pinned. If the transcription is ever wrong, that count
moves.

## The order is settled, and it is the manual's

For v19 and v20 this section argued that the corpus could not choose between the
manual's printed order and the price list's, and that the list shipped "on source
quality and not on evidence". **The executable's name blocks settle it: the order
is the manual's printed order.** The reorder is retracted, not superseded, and the
six positions that moved are back where the manual printed them:

| Position | Executable (= manual) | Price list, retracted |
|---|---|---|
| 4 | **Streamlined Hulls** | Iron Railroad Bridge |
| 5 | **Square-Set Timbering** | Feed Grasses |
| 6 | **Iron Railroad Bridge** | Square-Set Timbering |
| 7 | **Feed Grasses** | Streamlined Hulls |
| 13 | **Rifled Artillery** | Breech-Loading Rifles |
| 14 | **Breech-Loading Rifles** | Rifled Artillery |

**Nothing measured should move with it — and that is a prediction, not yet a
measurement.** All three corpus checks were run under both orderings when the question was
open and none discriminated, so the reversion ought to leave every count where it was:

| Check | Under either ordering | Re-measured after the reversion? |
|---|---|---|
| Authored levels permitted / not | **380 / 4** | **no** |
| Rail ends permitted / not | **1,140 / 0** | **no** |
| Grants ahead of their arrival year | 56 of 491 | **no** |

**The corpus tests now skip visibly when the directory is unset.** Each is a
discovery-time `CorpusFact`, so a passing suite can no longer quietly mean that it
iterated no scenarios. Set `IMPERIALISM_SCENARIO_DIR` to the complete legal local
corpus to run these measurements; the tests require at least the ten original files.

The reason the corpus was silent is worth keeping, because it is the shape of a
question the corpus will be silent about again:

- **A power holding five technologies holds different gates under each ordering.**
  Under the executable's it holds Square-Set Timbering, which opens mines to Level
  II. Under the price list's it held Iron Railroad Bridge and Feed Grasses instead
  — timber, wool and livestock to Level I — and *not* Square-Set Timbering. Neither
  set contains the other.
- **The corpus does not contain such a power.** Its counts are 0, 6, 9, 13, 14 and
  21. Never 5.
- From **six** upwards the price list's prefix was a superset of the manual's, and
  from **seven** upwards the two prefixes were the same *set*. Positions 13 and 14
  gate nothing this engine models. Positions 4–7 all arrive in 1821, so the dates
  could not separate them either.

**One assertion did move, and it is the one written to catch this.**
`TechRecordsBecomeStartingKnowledge` pins id 5, chosen because it is one of the six
disputed positions: it resolved to Feed Grasses and resolves to Square-Set
Timbering now. Everything else in the suite was blind to the change, which is the
same fact the table above states.

### Prefix closure is a vacuous control, and is kept anyway

Every prerequisite in the table points strictly earlier — 18 of the 28 entries
name one, 21 edges in all. So any contiguous prefix `1..N` is prerequisite-closed,
which is exactly the shape a `tech` record has.

**This proves nothing about the ordering**, because it holds under the manual's
printed order too. It is kept as `EveryPrerequisiteSitsEarlierInTheTable` because
it catches a *future* edit that moves an entry above something it depends on, and
the engine enforces the same rule in `TechnologyDefinition`.

## A scenario `year` is an offset from 1815

**Found by this slice, and it was a live bug.** The importer passed the `year`
field through as an absolute year. The corpus's fields are 1, 5, 10, 11, 33 and
67, so every imported scenario claimed to start in year 1 to 67 — inert until
technology gained an arrival date, at which point nothing would ever have been
buyable. The same story as `tran` and `cash`: a value nothing read, and then
suddenly load-bearing.

The epoch comes from the scenarios' **own briefing text**:

- `s1.inf` is titled "Naval Competition 1882" and says "the year 1882 finds
  Germany with industrial and educational superiority". Its field is **67**.
- `s3.inf` is "Unification Movements 1848-1890" and says "in 1848 France is still
  the leading power on the continent". Its field is **33**.

Both are `1815 + field` exactly, which is also the manual's own campaign start.
The three skirmishes carry field 1, so **a skirmish starts in 1816** — what the
data says, not rounded to 1815 to look tidier.

### The arrival dates corroborate it, and were not used to derive it

Reading each scenario's grants against the arrival years. **These are the counts from
before the reorder and the seven years that moved with it**, and they have not been
re-measured since. `HowMuchTheCorpusGrantsAheadOfItsArrivalDates` now skips visibly
without the full corpus, so rerun it with `IMPERIALISM_SCENARIO_DIR` set before treating
the table below as confirmed.

| Scenario | Year | Grants | Available then | Ahead of their date |
|---|---|---|---|---|
| `s1` | 1882 | 21 a power | 27 | **0** |
| `s3` | 1848 | 9, 13, 14 | 16 | **0** |
| `s9` | 1826 | 9 a power | **exactly 9** | **0** |
| `s12` | 1825 | 9 a power | 7 | 2, both one year early |
| `s13`, `s14`, `s5` | 1820 | 6 a power | 3 | 3, all one year early |
| `s10`, `s11`, `s15` | 1816 | none | 3 | — |

**`s9` is the striking case.** In 1826 it holds nine technologies and exactly nine
are available; Spinning Jenny and Paddlewheels both arrive in 1826 and it holds
both and nothing later. An epoch off by a few years breaks that.

Three of the four dated missions grant nothing early at all, and the two that do
are each **one year** short. For dates the manual calls "approximate", against an
epoch derived from two briefing paragraphs, that is far tighter than a designer's
authoring liberty would predict — so it corroborates the years and the epoch
together.

**It is measured and not enforced.** The gate governs buying and never authoring:
a scenario may grant whatever it likes, and
`HowMuchTheCorpusGrantsAheadOfItsArrivalDates` records the count so that a change
announces itself.

### The four exceptions are not failures

**The gate governs a civilian raising a level and never a scenario authoring
one**, exactly as the capacity ladder governs building and not storing. Authoring
past the ladder is legal input and the importer must take it. The four are
counted rather than tolerated silently, because a moving count is how a mistaken
transcription would announce itself.

Whether they are authoring liberty or a sign that Dynamite sits slightly earlier
than position 23 is unresolved. The page they come from is where the manual's
column layout is worst, so the second is possible. Four records out of 384 does
not distinguish them.

**`s5` is excluded from the check.** It is a generated world holding six
technologies with Level III tiles scattered across it, and it authors 74 levels
no power in it could have built. That is a demonstration of the rule rather than
a breach of it, and averaging it in would drown the signal.

## Design

### The gate is per level, on the deposit

`ResourceDefinition.TechnologyByDevelopmentLevel` runs parallel to
`YieldByDevelopmentLevel`: entry *n* is what it takes to reach level *n*. Two
parallel arrays keep the two answers the manual gives per rung in the same shape.
Null is an ungated rung, and a short or absent list leaves everything above it
ungated — which is what makes a pre-v15 package behave exactly as it did.

`RequiredTechnology`, which gates *extraction* from an already-open deposit, is
untouched and still unused. The manual never does that.

### Refusals distinguish the two dead ends

`ImprovementTechnologyNotKnown` is separate from `AlreadyFullyDeveloped` because
a player can act on one and not the other: invest, or find another tile.

### The fair start carries the knowledge

`StartingDefaults.Technologies`, applied to the countries a scenario names in
`defaultStartCountries` — the same rule the workforce and capacity defaults
follow, and for the same reason: the original equips its Great Powers and not its
minor nations.

The importer identifies them by their `labo` records. That is not a guess: `labo`
is the one record naming the Great Powers and only them, seven in every shipped
scenario.

### Buying it: a new `Investment` phase, last

`CountryTurnOrders.BuyTechnology` is a list, because the manual lets a player
invest in several before ending the turn. `TurnPhase.Investment` sits **after
`Delivery`**, second from last.

**Being last is the whole mechanism for "bought this turn, known next turn."**
Everything that reads knowledge during a turn — `Development`'s three gates,
`Extraction`'s unused `RequiredTechnology` — has already run against what the turn
opened with. That is the same trick `Construction` uses to complete next turn, and
it means the rule needs no code of its own: order a Farmer onto a gated rung on
the turn you buy its gate and it is refused, and it succeeds the turn after.

**A chain of prerequisites cannot be bought in one turn.** This is the owner's
reading of the original: buying does not research, it "spends the money and the
research finishes after the turn ends before the next starts", and the dependent
entry cannot even be clicked. `TechnologyPlanner` snapshots the country's
knowledge before spending any of it, so `AlreadyKnown` and `PrerequisiteNotKnown`
are answered against the turn's opening state — which stops `Investment` being the
one phase in the pipeline that reads its own output.

**Availability is world-wide and by date.** The test is
`state.CurrentYear >= AvailableFrom` and there is nothing per country: advances
"become available on a world-wide basis; they cannot be kept secret", and
"technology, once available, does not vanish. If you cannot afford the cotton gin
in 1818, invest in 1830."

### Research takes what construction and improvement leave

**A chosen rule.** Running last also settles cash contention without a preflight:
construction and improvement are charged during `Development` and get first call
on the treasury; research spends the remainder. Two purchases by one country are
read in order and the second is refused `NotEnoughCash` outright rather than
part-funded — the same bargain two Engineers of one country already make.

The soak shows this is not a formality: a power that improves whenever it can
never accumulates the twelve thousand a Mechanical Reaper wants. See
[below](#what-a-hundred-turns-looks-like).

### Refusals get their own enum

`TechnologyPurchaseRefusal` rather than folding into `CivilianOrderRefusal`, which
is about civilians: nothing on the Investment screen has a unit, a tile or a
terrain.

| Refusal | Meaning |
|---|---|
| `NoSuchTechnology` | no such id in this world |
| `AlreadyKnown` | held already, so the original never offers it |
| `NotYetAvailable` | the year has not come |
| `PrerequisiteNotKnown` | something it builds on is not known **yet** |
| `NotEnoughCash` | the treasury will not cover it |
| `NotForSale` | it has no price — the first two, and everything pre-v19 |

### A prerequisite must point earlier in the catalog

**A chosen constraint, not a finding.** `TechnologyDefinition` refuses a
prerequisite at or past its own id. That forbids cycles without a graph walk, and
it is exactly what makes any contiguous prefix of the catalog
prerequisite-closed — which is the shape a `tech` record needs, being a bare index
into it. The 1997 table satisfies it throughout.

## Pseudocode

```text
Improving, after the shared entry and terrain rules:

    for each deposit on the cell this civilian's type works:
        if it requires discovery and the country cannot see the cell: skip
        if the level is at the top of its curve: skip
        if reaching level+1 needs knowledge the country lacks: remember and skip
        allow

    refuse: not yet discovered   if any were hidden
            technology not known if any wanted knowledge
            already developed    if any were simply finished
            not this civilian's work otherwise
```

```text
Investment, last in the turn, per country:

    known <- snapshot of what this country knows now
    for each technology ordered, in the order given:
        if no such id:                     refuse no such technology
        if known[it]:                      refuse already known
        if it has no cost:                 refuse not for sale
        if this year < its arrival year:   refuse not yet available
        if any prerequisite not in known:  refuse prerequisite not known
        if the treasury cannot pay:        refuse not enough cash
        take the cash, grant the knowledge

    `known` is never updated, so a chain takes a turn per link.
```

## Where implemented

- `ResourceDefinition.TechnologyByDevelopmentLevel` / `GetRequiredTechnology`.
- `StartingDefaults.Technologies`, applied in the `WorldState` constructor
  alongside the workforce and capacity defaults.
- `DevelopmentPlanner.LegalityOfImprovement`.
- `CivilianOrderRefusal.ImprovementTechnologyNotKnown`.
- `.iworld` **v15**: `resources[].technologyByDevelopmentLevel`,
  `startingDefaults.technologies`, with a v14→v15 migration to an ungated world.
- `LegacyWorldConverter.TechnologyTable`, `ResourceTechnologyLadders`,
  `ReadCountryTechnologies`, and the starting-defaults block. `tech` is no longer
  deferred.

Buying it, from v19:

- `TechnologyDefinition.Prerequisites` / `AvailableFrom` / `Cost`. **No cost means
  not for sale**, the same shape `RailRule?` and `ImprovementSettings` use.
- `TechnologyPlanner`, on the shape of `EngineerPlanner`.
- `TurnPhase.Investment` and its branch in `TurnResolver`, after `Delivery`.
- `CountryTurnOrders.BuyTechnology`.
- `TechnologyPurchaseRefusal`, `TechnologyPurchasedEvent`,
  `TechnologyPurchaseRefusedEvent`.
- `.iworld` **v19**: `technologies[]` becomes `TechnologyContentDefinition` with
  `cost`, `availableFrom` and `prerequisites`, with a v18→v19 migration to a world
  where nothing is for sale.
- `LegacyWorldConverter.TechnologyTable` now carries all three columns per entry,
  and `LegacyWorldConverter.ScenarioYearEpoch` corrects the `year` field.
- **`ResourceTechnologyLadders` and the rail gates are name-keyed now**, resolved
  through `TechnologyKey`, so the next reorder cannot silently rewire a gate. They
  used to be table positions, which made the order load-bearing twice over.

## Test data

`tests/Imperialism.Core.Tests/TechnologyGateTests.cs` pins Level I being a gate
like any other; each rung gated separately; a mine opening ungated and stopping
at Level II; a scenario authoring past the ladder and loading intact; the two
dead ends being reported differently; the fair start reaching only named
countries; a world with no ladders gating nothing; and a deposit refusing to gate
a level its curve never reaches.

`tests/Imperialism.Core.Tests/TechnologyInvestmentTests.cs` pins the buying: a
purchase refused before its year and accepted after; an arrived technology staying
on the screen decades later; refused for a missing prerequisite and accepted once
held; **a prerequisite and its dependent ordered in the same turn, the dependent
refused**; refused for want of cash, with nothing taken; one treasury covering the
first purchase and refusing the second; several independent purchases in one turn;
refused when already known; a priceless technology never for sale; a whole world
that prices nothing selling nothing; availability being world-wide; and **the
payoff — a Farmer refused on the turn its gate was bought and succeeding the turn
after.**

`tests/Imperialism.LegacyImport.Tests/LegacyWorldConverterTests.cs` pins the
whole ladder per deposit, **the entire twenty-eight-entry table — order, costs,
years and prerequisites**, prerequisite closure (flagged as vacuous between the
orderings), the starting pair, `tech` conversion including an out-of-range id,
the 1815 epoch against the corpus, the arrival-date measurement, and the corpus
falsification test above.

`tests/Imperialism.Content.Tests/WorldContentTests.cs` pins the v18→v19 migration
to unpurchasable technology, its rejection of a contradictory v18 package, the
dropping of the flat rail price, and a round trip of all three new columns.

## What a hundred turns looks like

The soak gates grain's top rung behind Mechanical Reaper and nothing else. **It no
longer has to cheat to open it.**

The fixture now starts in **1840** rather than 1815, purely so the real arrival
dates fall inside a hundred-quarter run: a turn is a quarter, so a century from
1815 stops in 1839 and the Reaper's 1851 would be permanently out of reach — the
gate would go back to being tested only shut. Nothing else reads the year, so
moving it moved no published number. Steel and Iron Plows cost 3,000 and arrived
in 1831, so they are buyable on turn one; Mechanical Reaper costs 12,000, wants the
Plows, and arrives on **turn 45**.

```
                              grain/turn  workers  top rungs  first  bought  spent    treasuries
funded and patient             21 → 63    49 → 105     21       59    14     105,000    21,000
ordinary treasury, greedy      21 → 42    49 →  77      0     never    7      21,000    77,000
granted free on turn 50        21 → 63    49 → 105     21       53     0           0    77,000
```

**Two different walls, and the run separates them.**

*The calendar is one, and no money moves it.* The funded run buys the Reaper on
**turn 45, the very quarter it arrives** — not a turn earlier, having had the cash
ready for dozens of turns. Its 308 refusals are that wall being hit over and over.
The first gated rung follows on turn 59, fourteen turns later: the work duration
plus a Farmer having to finish what it was already doing and walk.

*The money is the other.* The greedy run — ordinary treasury, improving whenever it
can — buys the Plows and **never the Reaper**, so its ceiling never lifts at all.
That is the chosen contention rule biting: improvement is charged during
`Development` and research takes the remainder.

**Be careful how hard that second finding is leaned on.** The greedy run ends
holding 11,000 a power against a 12,000 price — **it misses by a thousand**, which
is a knife edge and not a comfortable margin, the same shape as the grain
knife edge in [development.md](development.md). A slightly richer mine flips it.
What the run does establish robustly is the *direction*: twelve thousand is most of
a century of one gold mine's income, and a power that spends as it earns does not
get there.

**The granting run is kept as the control and the three genuinely differ** — in
what they buy, what they spend, and which turn their ceiling lifts. A bought
ceiling and a gifted one are not the same run.

The older two-run table published here — 42/42/42 against 42/42/63, first rung on
53 — was measured on the plain fixture with no improvement cost and no gold mine,
and `AGatedRungOpensWhenTheTechnologyArrives` still pins it. The three rows above
are a richer configuration and not a restatement of it.

## Open questions

Four of these closed with v19 and are recorded above rather than deleted:
**buying it**, **prerequisites**, **arrival dates**, and **whether Dynamite sits at
position 23** (it does — both the price list and the executable put it there, so
the four `s1` timber exceptions are authoring liberty).

**Two more closed with the executable.** The table's order was the single weakest
link in this chain and is now the strongest: recovered outright, and it is the
manual's. The prices are recovered too, and twelve of them were wrong. Both are
recorded above.

- **The prerequisite graph** is closed: the executable's 29-row table is imported
  directly, including the six corrections to the old price-list transcription.
- **Mountains' rail price**, the one ground the price list does not price. See
  [engineer.md](engineer.md).
- **Civilian buildability.** Feed Grasses gates the Rancher, Iron Railroad Bridge
  the Forester, Oil Drilling the Driller. Blocked on the University, not on money
  any more — and every civilian in play still comes from a scenario.
- **Rail through terrain** is done, and **oil is now reachable**: an imported world
  can buy Oil Drilling for 12,000 from 1856, so the prospecting gate
  `prospecting.md` called permanently shut is open. Nothing has run a soak on it.
- **The Refinery and the Power Plant**, both behind Oil Drilling.
- **Research progress.** The manual has purchase as instant and this models it that
  way; nothing accumulates points.
- **The newspaper**, which is how the original tells you a technology has arrived.
  The events exist and nothing presents them.
