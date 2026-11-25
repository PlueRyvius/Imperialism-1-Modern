# The newspaper

Phase 3 row 7b. **No content bump and no new turn phase.** One Core event,
`TechnologyAvailableEvent`, and one detached Presentation view, `NewspaperView`.
The Godot screen that shows the paper at the start of each turn is a later task.

## What the manual says

- 494–498: "The newspaper appears at the start of every turn … a story that offers
  information, provides a warning, or affects the play of the game appears under a
  bold headline", and it carries the "newsworthy doings of the little people".
- 557–558: "Each new turn begins with a newspaper summarising the key events of the
  past three months."
- 4096–4099: "When a new technology first becomes available, you see a story with a
  large bold headline in the newspaper titled 'New Invention.'"

That is everything. The binary's newspaper module, `UNewspaper.cpp`
(`0x0055BA30`–`0x0055BB8F`, `docs/disasm/module-map.md`), is untraced, and its
story strings in `STR#ENU.GOB` are unread. **Every selection rule below is chosen.**
The questions that would replace them are `docs/disasm/wanted-values.md` §10.

## The event

`TechnologyAvailableEvent(turnNumber, technology)`, phase `Investment`, with no
country, because an arrival is world-wide. `TurnResolver` emits it at the end of the
Investment block, after the purchases and before the phase marker. The turn report
says "*X* is now available to every power, from next turn."

## The chosen rules

**N1 — an invention is a crossing, not a level.** A technology is announced on the
turn whose date advance crosses into its arrival year: `yearNow < AvailableFrom <=
yearAfter`, where `yearNow` is the year the turn started in and `yearAfter` is the
year of `TurnDate.Next()`. So the turn that ends on 1830 Q1 announces every 1830
invention, and the next turn's orders can buy them. Same-year arrivals are announced
in id order. A technology already available as the game opens is never announced,
and neither is an **unpriced** one (`Cost` null): it never appears on the Investment
screen, so there is nothing to invest in. Tests: `TechnologyAvailableEventTests`.

**N2 — what is a story.** The manual's three-way test applied to our events.
Headlines, in priority order:

1. `TechnologyAvailableEvent`: "New Invention", the manual's own title. The body is
   the technology's name, because content carries no description of what an
   invention does.
2. `CountryEliminatedEvent`.
3. `WarDeclaredEvent`, and peace accepted (`DiplomaticOvertureAcceptedEvent` of kind
   `Peace`).
4. `ProvinceCapturedEvent`.
5. `NavalBattleFoughtEvent`, `RegimentsLandedEvent`, `BattleFoughtEvent`.
6. `MerchantInterceptedEvent`.
7. Warnings to the reader: a `WorkersFedEvent` with workers sick or starved,
   `CivilianDeliveryLostEvent`, `RegimentDeliveryLostEvent`,
   `DiplomaticTiesCutEvent`.

Items, the "doings of the little people": `WorldPriceChangedEvent`, a
`WorkersRecruitedEvent` that recruited someone, `ShipsBuiltEvent`,
`CiviliansDeliveredEvent`, `RegimentsDeliveredEvent`, a `TechnologyPurchasedEvent`
by any Great Power, and `ConstructionCompletedEvent`.

A story's body is the turn report's own sentence for the same event, so names and
numbers are formatted in one place. A hunger story carries only the report's loss
line. The masthead, "The *reader* Gazette", is a placeholder.

**N3 — who reads what.** Inventions, eliminations, war, peace and captures go to
every reader. A naval battle appears if a Great Power fought it or the reader did.
A land battle or a landing appears if a Great Power fought it, the reader did, or the
province is the reader's or borders it, judged by ownership as the turn ended. An
interception appears to its carrier, seller and buyer, not its interceptor. A world
price belongs to no country, so every reader's market page carries it. A rival's
technology purchase appears if the buyer is a Great Power. Everything else appears
only to the country it names. **Refusals, phase markers, deliveries, extraction and
transport never appear**: the turn report stays the complete record.

**N4 — order and cap.** Every headline comes before any item. Headlines go by
priority, then by their order in the log; items go in log order. A paper holds at
most **12 stories** and counts the rest in `StoriesOmitted`, so the cap only ever
drops items while there are items to drop. A quarter with nothing to report prints
one fixed item, "A Quiet Quarter", and never an empty paper, because the original
shows the paper every turn.

Mutation-checked: announcing an invention already arrived, swapping priorities 2
and 4, cutting in log order instead of headlines first, and printing a refusal each
turn a test red (`TechnologyAvailableEventTests`, `NewspaperViewTests`). No soak
number moved.
