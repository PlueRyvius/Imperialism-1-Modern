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

## Confidence

`inferred`, and strongly so for the ladder; the id mapping is corroborated
against the corpus rather than assumed.

| Claim | Support |
|---|---|
| Which technology opens which rung | **manual**, stated per entry, and agreeing with the seven gates transcribed earlier in `../reference/manual-mechanics.md` |
| Every player starts with the first two | **manual**, stated outright |
| A mine opens at Level I ungated | **manual** — no technology is named for it, and the Miner is one of the four civilians buildable from the start |
| `tech` is `[country, id]`, id a 1-based index into the table | **corpus-corroborated** — see below |
| Technology is bought with cash, and cannot be kept secret | **manual**, and **not modelled** — there is no treasury |
| Prerequisites and arrival dates | **manual**, and **not modelled** — they matter only once something can be bought |

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

## Test data

`tests/Imperialism.Core.Tests/TechnologyGateTests.cs` pins Level I being a gate
like any other; each rung gated separately; a mine opening ungated and stopping
at Level II; a scenario authoring past the ladder and loading intact; the two
dead ends being reported differently; the fair start reaching only named
countries; a world with no ladders gating nothing; and a deposit refusing to gate
a level its curve never reaches.

`tests/Imperialism.LegacyImport.Tests/LegacyWorldConverterTests.cs` pins the
whole ladder per deposit, the twenty-eight-entry catalog and its order, the
starting pair, `tech` conversion including an out-of-range id, and the corpus
falsification test above.

## What a hundred turns looks like

The soak gates grain's top rung behind Mechanical Reaper — nothing else — and
runs two hundred-turn games differing only in whether it is ever handed over.
There is no research, so it is granted outright on turn 50. **That is the pattern
for exercising any gate while acquisition does not exist**, and without it a gate
is only ever tested closed.

```
                        grain/turn at  10 → 50 → 100   workers  top rungs  refusals
never granted                    42     42     42        84         0       1,960
granted on turn 50               42     42     63       105        21         889
```

First gated rung on **turn 51**, one turn after the grant, which is the work
duration showing through. The ceiling is visibly real for fifty turns and
visibly lifts.

That also moved the farming run's published numbers, and the move is the finding:
grain now stops at 42 rather than 63, and the workforce settles at 84 rather than
119, because Steel and Iron Plows and Mechanical Reaper are not free. See
[soak.md](soak.md).

## Open questions

- **Buying it.** The manual prices technology in cash on the Investment screen
  and there is no treasury. Until then, knowledge comes from a scenario, from the
  fair-start default, or from a test granting it.
- **Prerequisites and arrival dates.** Both are in the table and neither is
  modelled; they matter only once something can be bought.
- **Whether Dynamite sits at position 23.** The four `s1` exceptions would vanish
  if it were earlier. Unresolved, and cheap to revisit if a decompiler ever reads
  the technology array.
- **Civilian buildability.** Feed Grasses gates the Rancher, Iron Railroad Bridge
  the Forester, Oil Drilling the Driller. Blocked on the University and money,
  and every civilian in play still comes from a scenario.
- **Rail through terrain.** High Pressure Steam Engine, Iron Railroad Bridge,
  Compound Steam Engine and Dynamite each open ground to the Engineer. That is
  the Engineer's slice.
- **The Refinery and the Power Plant**, both behind Oil Drilling.
- **Whether technology is really world-wide.** The manual says advances "become
  available on a world-wide basis; they cannot be kept secret", which describes
  availability to buy rather than possession. Nothing here models either.
