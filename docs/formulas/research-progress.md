# Research progress

Does the original game have a research *progress* mechanic: points that
accumulate, a bar that fills, a price paid in instalments, a delay longer than one
turn? The phase 3 plan listed "research progress" as pending, and
[technology.md](technology.md) ends with "The manual has purchase as instant and
this models it that way; nothing accumulates points." This document checks that
sentence against every source we hold, before anyone builds against it.

Line numbers refer to the files as of `origin/main` at `ab6b013`. Manual quotes
come from `docs/reference/imperialism-manual.txt`, which is ISO-8859-1. Search it
with `grep -a`, or the tool will treat it as binary.

## Verdict

**There is no research progress mechanic in the original. Nothing needs to be
built.**

- A technology is **bought, not researched**. Its whole price leaves the treasury
  the moment the player clicks it, and the player gets the money back if they
  cancel before ending the turn.
- It **takes effect the next turn**. Nothing in the manual or the extracted
  binary describes a delay longer than that.
- The only part of technology that unfolds over time is **when each technology
  becomes available**. That is a date, the same for every power, drawn once from a
  window in the executable. It is not progress that a player builds up.
- **Neither the University nor the newspaper accumulates anything toward a
  technology.** The University builds civilians. The newspaper announces new
  inventions.
- So "research progress" is **already covered** by arrival dates plus instant
  purchase, both modelled today. The checklist item should be closed, not
  built. There are two smaller gaps nearby (below). Neither is progress.

## Evidence

### 1. The executable (`docs/disasm/definitive-original-data.md`)

The extraction found **one price per technology, deducted by the purchase
path**:

> 162: The technology-store code also reads a zero-based 28-entry cash-cost table
> at `0x0066AAE8` through the purchase paths at `0x005B0B30` and `0x005B0BB0`; the
> purchase record's `+0x64` field supplies that internal selector. [...] The first
> two entries are starting technologies and have zero purchase cost.

The JSON record names the field
(`docs/disasm/definitive-original-data.json`, `technology.purchase_cost_table`):

> "field": "cash deducted for technology purchase"

The only per-technology timing data is the **availability window**:

> 195: The 26 non-starting technologies receive an inclusive pseudo-random
> availability offset from the 26 two-word windows at `0x0066ABA4`, initialized by
> `0x005AF330`; the two starting technologies have no generated window. These are
> executable turn-offset ranges, not a claim that the earliest boundary is the only
> arrival date.

The purchase screen's own strings, from the same line, record a **single moment**
of purchase:

> 195: The same archive provides `Purchased in [1:year]`, one- and two-prerequisite
> templates, and the purchase-screen labels for cost, benefits, details, and
> purchase/cancel actions.

Those labels are listed in full in the JSON (`technology.purchase_ui_templates`):
`Purchasing`, `Technologies`, `Cost`, `Benefits`, `View Technology Details`,
`Purchase Technology`, `Cancel Purchase`. None of them says progress, points,
percent, turns remaining or instalment.

**What this does and does not prove.** The extraction holds one cost table, two
paths that read it, one window table and its initialiser. It holds no
per-technology points table, no per-turn rate, and no string that a progress
screen would need. That is evidence of absence from an extraction that went
looking at exactly these routines. It is **not** a full trace of
`0x005B0B30`/`0x005B0BB0`. If someone wants certainty, the check is cheap: read
those two functions in Ghidra and confirm that each writes the treasury once and
sets one "owned" flag. See [Left to build](#what-is-left-to-build).

### 2. The manual

**Buying: the whole price at once, refundable until the turn ends.**

> 4120: To invest, click on a button listing the price of the new technology. The button
> 4121: changes to the word Purchasing, and your cash is reduced by the amount shown on
> 4122: the button. If you change your mind about this investment, click on the button
> 4123: again. The cash is restored to your reserves.
>
> 4125: You cannot cancel a purchase of technology once you click the End Turn button on
> 4126: the Terrain Map screen.

**Taking effect: the next turn, and no later.**

> 4248: Confirmation of an Investment.
>
> 4250: When you click the End Turn button, any investment made in technology that turn
> 4251: are final. Before the next turn begins you receive a full screen picture of the new
> 4252: technology on your desk confirming that your Great Power now has access to the
> 4253: new technology. You may take advantage of the new discovery or invention on
> 4254: your new turn.

This passage is the decisive one. It names the turn the technology arrives, and
that turn is the next one. The manual says the same about militia, whose upgrade is
triggered by the purchase itself:

> 1654: automatically, at no cost to you, when the appropriate new technology is purchased.

**Availability: by date, world-wide, and permanent.**

> 4078: As the Industrial Revolution progresses, new discoveries and inventions multiply
> 4079: rapidly. These advancements become available on a world-wide basis; they cannot
> 4080: be kept secret. However, there are economic costs for using a new technology; for
> 4081: example, the cost of equipping your expert farmers with the new mechanical
> 4082: reaper or the cost of re-training your military to use rifled artillery.

The price pays for *adopting* a discovery. Nobody pays to *make* the discovery,
and nothing is left for a progress mechanic to accumulate.

> 4084: Usually, it is impossible to invest in every new technology as it becomes available
> 4085: for the first time. Not only must you pick and choose among various
> 4086: advancements, but you must also decide how much of your Great Power's cash
> 4087: can go to technology in the first place.
>
> 4090: Often you can afford to wait. Technology, once available, does not vanish. If you
> 4091: cannot afford the cotton gin in 1818, invest in 1830.

What a player spreads across turns is **cash**, saved until they can afford a
purchase. Saving is not progress: nothing is set aside toward a particular
technology.

> 4144: Technology.            Benefits of Technology.                         Approximate    Prerequisites.
> 4145:                                                                        Arrival Date.  None

The table's date column says *Approximate Arrival Date*. That fits the
executable's random window: the date is drawn, not fixed.

**Announcing: the newspaper and the Ministers tell you. They accumulate nothing.**

> 4096: When a new technology first becomes available, you see a story with a large bold
> 4097: headline in the newspaper titled "New Invention." The text of the article gives
> 4098: you an idea of what this invention does so you can decide whether to invest in it.
>
> 4100: Your Ministers might provide additional reminders on a technology if one of them
> 4101: feels a particular technology is important.
>
> 1014:           The Invest in Technology button, which appears as a microscope, takes you to
> 1015:           the technology investment screen. This button appears lighter than the
> 1016:           surrounding wood when there is a new technology to invest in.
>
> 363: as wasting transport capacity or failing to purchase technology. For more

**The University builds civilians, not knowledge.**

> 2675: Each of the three unit construction centres; the University, the Armoury, and the
> 2676: Shipyard, builds a different sort of unit: civilians, land regiments, and ships
>
> 2699: All civilian units require expert workers, cash, and paper.
>
> 2708: Farmer, the Miner, the Prospector, and the Engineer. Soon after the game begins,
> 2709: you can invest in Feed Grasses technology and Iron Railroad Bridges technology.
> 2710: These developments permit the construction of the Rancher and the Forester

Technology gates what the University can build. The University does not produce
technology. The manual says nothing anywhere about research points, a research
budget, scientists, or a progress bar. Searching
`technolog|invest|research|universit|newspaper|progress` turns up only the passages
quoted here. The few uses of "progress" are about the game or the Industrial
Revolution moving on (744, 4078, 4113). None is about research.

**Where game time does gate technology, it is through the arrival date.**

> 1611:                   technologies, but there is a grouping of the available military
> 1612:                   technology in the 1840s and in the 1870s. By the end of each of
> 1613:                   these decades, all nine categories have normally received their new
> 1614:                   type of unit. Of course, you must invest in the technology to make
> 1615:                   the new regiment types available for construction.

### 3. This repository's own documents

`docs/reference/manual-mechanics.md` already reads it this way:

> 97: Technology is **bought with cash** on the Investment screen; an investment can
> 98: be cancelled until the turn ends and not after. Advances "become available on a
> 99: world-wide basis; they cannot be kept secret", and cost money to adopt rather
> 100: than to discover.

(Line 100's "None of the purchasing is modelled" is stale since v19.)

`docs/game-systems.md`:

> 34: - **Most outputs are deferred one turn**: traded goods, built units and
> 35:   purchased technology land the following turn

`docs/formulas/technology.md` gives the same answer and lists the simplification:

> 62: | **One fixed year per technology** | **a simplification** of the executable's pseudo-random window |
>
> 420: **Being last is the whole mechanism for "bought this turn, known next turn."**
>
> 646: - **Research progress.** The manual has purchase as instant and this models it that
> 647:   way; nothing accumulates points.

`docs/disasm/wanted-values.md` has no entry for research progress. Its technology
section (113–146) asks only for the order, the prices, the arrival years and the
prerequisite graph. The first three are recovered and the graph is still open.

## What is modelled today

| Original behaviour | Source | Engine today | Status |
|---|---|---|---|
| Full price, paid once | manual 4120–4123, exe `0x0066AAE8` | `TechnologyPlanner` takes `Cost` in one charge | matches |
| Effect on the next turn | manual 4250–4254 | `TurnPhase.Investment` runs last in the turn, so the technology is known from the next one | matches |
| Can only be cancelled before End Turn | manual 4122–4126 | Orders are submitted when the turn ends, so withdrawing an order before then is cancelling it | matches (the UI is still to come) |
| Available world-wide from a date | manual 4078–4091, exe `0x0066ABA4` | `CurrentYear >= AvailableFrom`, the same for every country | matches |
| Arrival date **drawn** from a window | exe 195, manual 4145 "Approximate" | One fixed year, the window's minimum | **simplification**, listed in technology.md:62 |
| A chain of prerequisites takes a turn per link | owner's reading (technology.md:427–433) | `known` snapshotted at the start of the phase | matches |
| The cash comes out **when the player clicks** | manual 4121 | Charged in `Investment` after `Development` has paid for building | **diverges**. See below |
| "New Invention" newspaper story, Minister reminder, lit toolbar button | manual 1014–1016, 4096–4102 | The events exist and nothing shows them | presentation, pending (technology.md:648) |

## What is left to build

**No research progress mechanic. Nothing here needs a points model, a progress
state or a per-turn instalment.** The phase 3 item "research progress" should be
closed by pointing at this document, and technology.md:646–647 should point here
as well.

Three nearby items came up while checking. None of them is progress. They are
listed so this document does not become the place they get lost.

### A. The arrival date is drawn, not fixed (optional)

The executable draws one offset per technology from its window at game start
(exe 195). Two games of the original see Oil Drilling arrive in different years.
We use the minimum every time, which is a labelled simplification.

If this is ever built:

- One draw per world from `[min, max]`, stored in `WorldState` and the same for
  every country, because availability is world-wide (manual 4079). **Chosen rule:**
  draw once at world creation from the scenario's seed, so a replay is
  deterministic.
- `.iworld` would gain `availableUntil` (the top of the window) next to
  `availableFrom`. A world that leaves it out keeps today's fixed year, so the
  migration preserves behaviour.
- **Unknown: how a window offset becomes a year.** The extraction calls the windows
  "turn-offset ranges" and says that conversion "remain[s] pending" (exe 195).
  technology.md reads them as years after 1815 because 25 of 26 observed years fall
  inside. That reading should be confirmed from `0x005AF330` before anyone draws
  from it.
- **Unknown: whether a scenario start fixes the draw.** Corpus `tech` grants already
  run ahead of the minimum in places (technology.md:272), so a mission evidently
  does not respect the windows. That is authoring, and authoring is not design.

### B. When the cash leaves the treasury (a divergence to record, not to fix here)

In the original, "your cash is reduced by the amount shown on the button" (4121),
at the moment of clicking. Civilian work and the Engineer are charged the same way
(development.md:221, engineer.md:130). So when cash is short, whatever the player
clicks first gets it. The engine instead charges research **after** construction
and improvement, which [technology.md](technology.md#research-takes-what-construction-and-improvement-leave)
already labels a chosen rule. That rule is still sound for a headless turn with no
click order. Any order screen, though, should **show** money spent on a technology
as committed when it is clicked, or the screen will promise cash that the next
click then takes. Recorded here because it is the one way the purchase timing
differs from the original.

### C. Confirm the purchase path in the binary (cheap, optional)

To turn the evidence of absence in §1 into a measurement, read `0x005B0B30` and
`0x005B0BB0` and confirm:

1. The treasury is debited by `table[+0x64]` once per purchase, with no per-turn
   repeat.
2. Cancelling credits back the same amount.
3. The flag that sets ownership is written at the end-of-turn step. That would
   match manual 4250–4254.

If any of the three fails, this verdict is wrong and should be **retracted, not
softened**. If they hold, add a line to `docs/disasm/definitive-original-data.md`
and nothing else changes.
