# The Engineer, and what a network reaches

## Summary

Every other civilian raises what a tile *yields*. The Engineer changes how much
of the map is a tile at all: rail, depots and ports existed only where a scenario
authored them, so a player could improve their land indefinitely and never
extend past the lines the 1997 designer drew.

The manual calls it "the only civilian with multiple functions", whose "most
important duty is the construction of a transport network". It is also the
natural counterpart to [transport.md](transport.md): capacity limits what you can
*carry*, and the Engineer changes what you can *reach*.

It needed [money.md](money.md) first, because everything it builds costs cash.

## Confidence

`inferred`. **The terrain gates are transcribed and corpus-corroborated; the
prices are the weakest numbers in the project.**

| Claim | Support |
|---|---|
| Rail is built between the Engineer's tile and an **adjacent** one | **manual** — the track cursor appears "over tiles adjacent to the Engineer's current location" |
| It takes the turn | **manual** — "spend the turn building a railroad line" |
| Depots, ports and forts are built on the Engineer's **own** tile | **manual** — the dialog opens "when you click on the tile where the Engineer is located" |
| Which terrain admits rail, per technology | **manual** — the Benefits of Technology Table, four entries |
| That reading of the table | **corpus-corroborated** — 1,140 rail ends, no contradiction. See below |
| Ports need water, and only coasts or river tiles | **manual**, stated outright |
| Ports cost more than depots | **manual**, and it is the *only* thing it says about any price |
| **Depots reuse the rail terrain gate** | **inference** — "rails may be laid **and** depots may be built", with no separate table |
| **Fertile hills take the hills gate** | **inference** — the manual says "hills" unqualified |
| **Towns and capitals are railable** | **inference** — a capital could not be a hub otherwise |
| What a depot and a port cost | **observed play**, and tentative |
| **What rail costs** | **a guess.** Nothing anywhere |

## The terrain gates, and the corpus check

From the Benefits of Technology Table, read for its rail column:

| Terrain | Technology | Table position |
|---|---|---|
| farms, plains, deserts, forests, tundra | High Pressure Steam Engine | 1 |
| swamp | Iron Railroad Bridge | 6 |
| hills | Compound Steam Engine | 12 |
| mountains | Dynamite | 23 |

**Every power starts holding position 1**, so an 1815 start lays track across
most of its land on turn one. Ocean never admits a line.

### 1,140 rail ends, and not one contradiction

The reading was falsified against the corpus before anything was built on it, the
same way the improvement ladder was: every end of every railed link a scenario
authors, against what its owner's technologies would have permitted.

**1,140 permitted, 0 not.** And the check is not vacuous — the gated ground is
genuinely built on, and the pattern across scenarios is exactly what the gates
predict:

| Scenario | Technologies held | Rails swamp | Rails hills | Rails mountains |
|---|---|---|---|---|
| `s1` | 1–21 (has IRB **and** CSE) | 3 ends | 42 ends | **none** |
| `s3` | 9, 13, 14 (unequal) | none | 2 ends, both under powers holding ≥13 | **none** |
| `s9`, `s12` | 1–9 (has IRB, **not** CSE) | 1 end | **none in 137 links** | **none** |
| `s13`, `s14` | 1–6 | none | none | **none** |

The `s9`/`s12` row is the strong one: two scenarios whose powers cannot cross
hills author 137 rail links between them and not one hill among them, while `s1`,
whose powers can, has forty-two.

**And the striking pair: no shipped power holds Dynamite** — the corpus tops out
at 21 and Dynamite is position 23 — **and no shipped scenario rails a single
mountain.** The one terrain nobody could build on is the one terrain nobody
built on.

`EveryRailedCellInTheCorpusIsOneItsOwnerCouldHaveBuilt` keeps that in the suite
with the counts pinned. As everywhere else here, **the gate governs building and
never authoring**: a scenario may lay track wherever it likes and the importer
must take it. Nothing validates against this; the count is measured so a mistaken
transcription would announce itself.

### The two inferences the corpus cannot settle

- **Fertile hills.** They take the hills gate, because the manual writes "hills"
  without qualification and this project does not invent permission. The corpus
  is *consistent* with it — the only scenario that rails fertile hills is `s1`,
  which holds Compound Steam Engine anyway — and therefore cannot separate it
  from "fertile hills are ungated".
- **Towns and capitals.** The table lists neither. They take the plains gate,
  which every power holds from turn one, so the choice is invisible in play; the
  alternative of "never railable" is plainly wrong, because a capital is the hub
  every depot connects to. 154 town rail ends across the corpus, all permitted.

## Design

### The order carries the verb, and this is the exception it looks like

`CivilianWorkKind` is keyed on the civilian *type* because that is where the
original puts it: the cursor follows the unit you selected. **The Engineer is the
exception the manual names outright**, so `CivilianWorkKind.Construct` selects a
civilian that takes `EngineerOrder` instead of `CivilianWorkOrder`, and *that*
order carries which of the two cursors was used.

The type still decides which *family* of work is possible — a Farmer given an
`EngineerOrder` is refused `NotAnEngineer`, and so is an Engineer given a
`CivilianWorkOrder`. Only inside construction does the order have anything left
to say.

Which construction, in turn, is decided by **which tile was clicked**, exactly as
the original decides it:

- an **adjacent** cell → lay rail between here and there
- the Engineer's **own** cell → the dialog, and the order names depot or port

An order whose structure disagrees with its target is refused rather than
guessed at: `RailNeedsAnAdjacentTile` or `StructureNeedsTheEngineersOwnTile`.

### It does not move the Engineer

`CivilianWorkOrder` moves the civilian and sets it to work in one command,
because the original's hammer cursor does. **An `EngineerOrder` does not**, and
that is a real difference rather than an oversight: rail is built *from* where
the Engineer stands, so moving it first would silently change which tile the line
starts at. Deploy, then order the work — which is what the manual's own tutorial
walks a player through.

### Cash is spent when the order is given

Not on completion. The manual frames it that way — a player might tell a civilian
to do nothing "when you lack the cash to pay for the civilian's improvements" —
and it is the only ordering in which a refusal is useful, since being told on the
turn you ordered it beats discovering it a turn later.

Nothing is refunded if the ground changes hands before the work finishes, which
is the same bargain the engine already makes with a civilian's *time*.

**A partial spend is refused rather than half-built.** `TrySpendCash` is
all-or-nothing, like `TryConsumeAvailable`. Two Engineers of one country drawing
on one treasury are resolved by reading the orders in turn: the first takes what
it needs and the second is refused `NotEnoughCash`.

### It reuses what was already there

- `WorldState.BuildDepot` / `BuildPort` already validated their sites.
- `BuildRail` already invalidates the cached `RailConnectivityIndex`, so a depot
  at the end of a new line lights up with nothing having to be told.
- `ExtractionPlanner.SeedCollectionPoints` already decides which depots and ports
  are connected. **Nothing in it changed**, and the payoff test asserts a
  previously-stranded deposit being gathered rather than asserting the planner
  was called.

## What it costs

Time — the Engineer's turn, through the same per-type `workTurns` every other
civilian uses, because the manual's sentence is the same one — **and cash**.

| | Amount | Standing |
|---|---|---|
| Depot | 1,500 | **the owner's recollection from play.** "Good for shape, poor for exact numbers" |
| Port | 2,000 | the same, and it satisfies the manual's one constraint |
| Rail | 500 | **a guess. Nothing supports it at all** |

The manual prices none of the three. It says only that ports "cost more than
depots"; these satisfy that and nothing else. Rail's number is set below the
depot's on the reasoning that a single tile of track plainly buys less than the
structure that gathers a whole catchment, which is an argument about plausibility
rather than evidence. **Do not cite any of the three.** All live in content, so
changing them is an edit.

## What a hundred turns looks like

The soak's last six columns are grain on railed ground with no depot near them —
stranded until an Engineer builds one. Two runs of the same world, differing only
in whether the Engineer is given orders. Each power's treasury covers exactly two
depots.

```
                     gathered  carried  wasted  grain/turn at 100  structures  treasuries
Engineer idle          15,512   15,484      28                 42           0      42,000
Engineer building      23,037   23,009      28                126          14      21,000
```

**The reach is large.** Half again as much harvest over the century, and grain a
turn triples. Nothing else in the engine can do that: every other civilian raises
what a tile yields, and this raises how much of the map is a tile.

### The prediction was wrong, and is retracted

This run was written to confirm an expectation carried over from
[transport.md](transport.md): that gathering more without carrying more would
push the waste figure up until a railyard caught up, and that reach and capacity
would visibly oppose each other.

**They do not. Waste is 28 either way — it does not move at all.**

The reason is one `transport.md` already reports from the other side: 805 points
of capacity over a century is absurd, and it is absurd because **the railyard is
unopposed**. Nothing in the fixture competes for lumber and steel, so capacity
outruns anything the Engineer can reach and the harvest never troubles the bar.
The tension is real in the original, where ships, trade goods, hardware and
building upgrades all want the same two commodities; it is not real here, and
saying otherwise would be citing a fixture's shape as a property of the model.

That is the honest reading and it is recorded rather than tuned away. Re-read
this table once anything else competes for lumber and steel — that is when the
opposition should appear, and if it does not, something is wrong.

## Where implemented

- `CivilianWorkKind.Construct`, `EngineerConstruction`, `EngineerJob`, and
  `CivilianWorkInProgress.Construction`.
- `EngineerOrder` and `CountryTurnOrders.EngineerWork`, folded into the existing
  one-order-per-civilian check.
- `RailRule` on `TerrainDefinition`.
- `ConstructionSettings` on `WorldDefinition`.
- `EngineerPlanner`, called from `DevelopmentPlanner` — the Engineer works in the
  existing **`Development` phase**, because it is a civilian taking a turn to do
  a job, which is what that phase is.
- `ConstructionBegunEvent`, `ConstructionCompletedEvent`, and eleven new
  `CivilianOrderRefusal` values.
- `.iworld` **v17**: `terrains[].rail`, a world-level `construction` block, and
  `civilianTypes[].work` accepting `construct`. A v16 package migrates to a world
  where nothing can be built.
- `LegacyWorldConverter.Terrains` (the `Rail` column), `CreateStandardConstruction`,
  and the Engineer's work kind.

## Test data

`tests/Imperialism.Core.Tests/EngineerTests.cs` pins the payoff — a depot
lighting up a catchment that was stranded — plus rail connecting a depot that was
built and useless; rail refused into terrain the country cannot cross and
accepted once it has invested; each terrain wanting its own technology; rail
refused to a non-adjacent tile and a depot refused anywhere but the Engineer's
own; a port refused inland, accepted on a coast, accepted on a river with no sea
near, and ignoring the rail gate; a depot obeying it; construction refused for
want of cash; two Engineers and one treasury; cash spent at order time; each
structure priced separately; building what is already there; only an Engineer
building and an Engineer only building; rail refused onto water; a world with no
construction settings building nothing; and one order a turn.

`tests/Imperialism.Core.Tests/EconomySoakTests.cs` carries the hundred-turn run
above, with a control that is verified to differ.

`tests/Imperialism.LegacyImport.Tests/LegacyWorldConverterTests.cs` pins the rail
gate per terrain, the three prices, the Engineer's work kind, and the corpus
falsification check above.

## Open questions

- **What any of it costs.** Two recollections and one invention. The binary is
  the only plausible source; the manual has been searched and prices nothing.
- **Fortifications**, the dialog's third choice — military, and built
  "throughout the province, not just the current tile", which is a different
  shape entirely.
- **Whether a depot really follows the rail gate**, and whether fertile hills and
  towns really take the gates assigned here. All three are inferences the corpus
  cannot separate from their alternatives.
- **The port-needs-a-depot case.** The manual is explicit that a port far from
  the capital needs a depot in the same tile or the depots along the new line
  have no route. `ExtractionPlanner` already models a port as always connected,
  so nothing here can express it; it wants the sea-route rules.
- **Losing a connection** — a province falling along the line, the province
  downstream of a river port, an undisputed enemy fleet. All want conflict.
- **Whether a civilian's work costs cash generally.** The manual implies it does;
  see [money.md](money.md), where the finding is recorded and deliberately not
  implemented.
- **Building the Engineer at all.** Every civilian in play still comes from a
  scenario. The University wants money *and* experts *and* paper.
