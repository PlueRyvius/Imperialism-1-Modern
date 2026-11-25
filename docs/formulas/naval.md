# Naval orders and declared war

## Summary

A country can now **declare war** and **command its fleets** through turn orders. Until
this slice the naval state PR #35 built — relation modes, task forces, patrols, sailing
legs and the port-access predicate — could only be set up from test code. Nothing in
`TurnResolver` called it, and the `Diplomacy` and `Conflict` phases were empty.

- `CountryTurnOrders.DeclareWar` is a list of countries. Each declaration is applied in
  `Diplomacy` with `WorldState.SetRelationMode(country, target, Hostile)` and takes
  effect next turn.
- `CountryTurnOrders.Naval` is a list of `NavalOrder`: `Assemble(fleets, mission)` groups
  whole fleets into a new task force and gives it a mission in the same order;
  `Command(taskForce, mission)` gives an existing task force its mission. A mission is
  `None`, `Patrol` or `MoveTo(seaZone)`. They are applied in `Conflict`.

A hostile patrol in every sea zone next to a port, with none of the owner's own patrols
there, cuts that port's route in `Extraction` (`PortAccess`, unchanged). So a blockade is
now something a player can order.

There is **no new turn phase** (the client smoke test still counts fourteen), **no content
version bump**, and **no soak number moved**: no soak policy declares war or orders a fleet.

## Confidence

`inferred`. The manual states that a declaration cannot be refused and takes effect next
turn, and that a fleet takes a mission or a move in a turn but not both. The patrol state
and the sailing leg are PR #35's executable readings. Eleven rules below are chosen, and
are labelled where they appear and in the table.

| Claim | Support |
|---|---|
| A declaration of war takes effect next turn | **manual**, line 628 and lines 3478–3479 |
| A declaration of war cannot be refused | **manual**, line 3477 |
| War may be declared on a Great Power or a Minor Nation | **manual**, lines 3473–3475 |
| A fleet takes a mission or a move in one turn, never both | **manual**, lines 1887–1889 |
| A fleet cannot be ordered to move to the zone it is in | **manual**, lines 1915–1916 |
| A patrol is a mission in the fleet's present zone | **manual**, lines 1921–1922 |
| Only the patrol state cuts a port, and only for effectively hostile pairs | **executable**, PR #35's `PortAccess` (task-force state 3) |
| A sailing leg is no longer than the slowest hull's sea zones | **executable**, PR #35's `PlanTaskForceMove` |
| 1. A patrol persists across turns until the task force is ordered to move | **chosen rule** |
| 2. A moved task force arrives idle and must be ordered to patrol on a later turn | **chosen rule** |
| 3. Countries in id order, orders in list order, every move after every order | **chosen rule** |
| 4. Assembly is permanent: no disband or split order | **chosen rule** |
| 5. War is declared unilaterally, may target any country, and there is no peace order | **chosen rule** (partly the manual's; see below) |
| 6. An invalid order is refused with an event and never throws | **chosen rule** |
| 7. An assembly is all or nothing: a refused mission refuses the grouping too | **chosen rule** |
| 8. A mission of `None` spends nothing; re-ordering a patrol does spend the turn | **chosen rule** |
| 9. A declaration against a pair already at war is refused, not restamped | **chosen rule** |
| 10. Only whole fleet records are assembled; no ship-by-ship selection | **chosen rule** |
| 11. A destination out of range is sailed towards, not refused | **chosen rule** (inherited from PR #35's planner; the manual disagrees, see "Open") |

## Declaring war

Resolved first in the turn, in `Diplomacy`: countries in id order, each country's list in
order. A declaration calls `SetRelationMode(country, target, Hostile)`, which sets the pair
symmetrically and stamps it with this turn's relation generation, and emits
`WarDeclaredEvent`.

**It takes effect next turn, and that needed no new code.** `HasEffectiveHostility` ignores
a hostile mode stamped in the current generation, and `CompleteTurn` advances the
generation. So a war declared this turn is visible to nothing that reads hostility until
the turn has ended. The manual says the same thing twice:

> Any changes in diplomatic status take effect next turn.
> — manual line 628

> Unlike most overtures, a country cannot refuse a declaration of war. If you declare
> war on a country, it receives notification of the war as soon as you click the End
> Turn button, and the war begins on the next turn.
> — manual lines 3477–3479

A declaration is refused with a `WarDeclarationRefusedEvent`, and changes nothing, when:

| Reason | When |
|---|---|
| `NoSuchCountry` | the target id is not a country in this world |
| `Itself` | the target is the declaring country |
| `AlreadyDeclaredThisTurn` | the same target appears earlier in this country's list; the first one counts |
| `AlreadyAtWar` | the pair's mode is already `Hostile` |

### Who may declare on whom (chosen rule 5)

**War is declared unilaterally, on any country, and there is no peace order yet.** The
first two parts are the manual's: "a country cannot refuse a declaration of war" (line
3477), and "The overture of a declaration of war may be made to any Great Power or Minor
Nation" (lines 3473–3475). Core cannot tell a minor nation from a Great Power, so nothing
checks which kind the target is. What is chosen is the absence of peace: "Once you are at
war with a country, the peace overture becomes available" (line 3475), but peace needs the
other side to accept and waits for row 5. Until then a war, once declared, lasts.

The manual also says that "whenever you declare war, your relations with all countries in
the game can change significantly" (lines 3483–3484). Relation scores are not modelled, so
nothing else changes.

### Declaring twice (chosen rule 9)

**A declaration against a pair already at war is refused, not repeated.** Calling
`SetRelationMode` again would restamp the pair with the current generation, and that would
put the war's start back by a turn — a second declaration would *suspend* a war for a turn.
Refusing it keeps the first stamp.

Because a declaration sets the pair symmetrically, **a mutual declaration in one turn counts
once, for the country with the lower id**; the other is refused as `AlreadyAtWar`. Both
sides are at war from next turn either way.

## Commanding fleets

Resolved in `Conflict`. That phase runs after `Diplomacy` and before `Extraction`, so a
patrol set or a leg sailed this turn already decides this turn's port access — through
hostility that was already in effect when the turn opened.

> Mission orders are carried out in the sea zone where the fleet is already located.
> Movement sends the fleet to a new location. Fleets cannot do both a mission and a move
> in the same turn.
> — manual lines 1887–1889

### Order of resolution (chosen rule 3)

**Countries in id order; each country's orders in list order; then every sailing leg at
once.** Each order is checked, applied, and given its event. Only after every country's
orders have been applied does `ResolveTaskForceMoves` run, once, and it applies the planned
legs in task-force id order, each with a `TaskForceSailedEvent`. So no country's fleet
arrives anywhere before another country has given its orders, and task-force ids are handed
out by country id and then list order. The manual orders none of this.

### Assemble

`Assemble(fleets, mission)` groups whole fleet records into a new task force with
`WorldState.AssembleTaskForce`, emits `TaskForceAssembledEvent`, and then applies the
mission. Carrying the mission in the same order lets a player group and send a fleet in one
turn without predicting a `TaskForceId`, as the original's select-then-click does.

It is refused when:

| Reason | When |
|---|---|
| `NoFleetsSelected` | the list is empty |
| `FleetSelectedTwice` | a fleet appears twice |
| `NoSuchFleet` | no fleet carries an id |
| `FleetNotYours` | a fleet belongs to another country |
| `FleetNotPlaced` | a fleet has no sea zone — every newly built ship, which waits unplaced (`shipbuilding.md`) |
| `FleetAlreadyInTaskForce` | a fleet is already in a task force |
| `FleetsInDifferentSeaZones` | the fleets do not share one zone |

and for the mission's own reasons below. These are exactly the conditions
`AssembleTaskForce` throws on, checked first in the same order.

**An assembly is all or nothing (chosen rule 7).** If the mission would be refused — a move
to a zone that does not exist, or to the zone the fleets are already in — the fleets are not
grouped either. A refused order never leaves a half-done task force behind.

**Assembly is permanent (chosen rule 4).** There is no order to disband a task force or to
split one, so a fleet once assembled stays in its task force. **Only whole fleet records are
assembled (chosen rule 10).** The manual lets a player pick "only a portion of your ships"
with the toolbar arrows (lines 1892–1894) and single ships with the fleet book (lines
1899–1904); `AssembleTaskForce` takes fleet records, and a fleet record is some of one
country's ships of one class in one zone, so a selection is a set of records and never part
of one.

### Command

`Command(taskForce, mission)` applies a mission to a task force that already exists. It is
refused as `NoSuchTaskForce` or `TaskForceNotYours` before anything else, and then for the
mission's reasons.

### The mission

| Mission | Effect |
|---|---|
| `None` | nothing |
| `Patrol` | `PatrolTaskForce`: the task force patrols its present zone. `TaskForcePatrolOrderedEvent` |
| `MoveTo(zone)` | `PlanTaskForceMove`: a leg towards `zone`, applied after every order |

A mission is refused when:

| Reason | When |
|---|---|
| `AlreadyOrderedThisTurn` | the task force already has its patrol or move this turn |
| `NoSuchSeaZone` | the move names a zone the map does not have |
| `AlreadyInThatSeaZone` | the move names the zone the task force is in — the Wheel Cursor "does not appear in the fleet's current sea zone" (lines 1915–1916) |

**One mission or move per task force per turn** is the manual's rule (lines 1888–1889), and
the engine enforces it with a turn-local set: a patrol or a move spends the task force's
turn, and any later patrol or move for it in the same turn is refused.

**A mission of `None` spends nothing, and re-ordering a patrol does (chosen rule 8).** `None`
leaves the task force free for a real mission later in the same list, so `Assemble(…,
None)` followed by `Command(…, Patrol)` is a legal turn. A `Patrol` order to a task force
already patrolling changes nothing on the map but still counts as its order for the turn.

### Patrols persist (chosen rule 1)

**A patrol stays in place, turn after turn, until the task force is ordered to move.**
Nothing re-orders it and nothing tires it. The manual's tutorial supports a mission lasting
— "It might take more than one turn for your blockading frigate to succeed" (line 4698) —
but that sentence is about a blockade, which is not modelled, so this is a reading and not a
quotation of a rule.

### A move arrives idle (chosen rule 2)

**A task force that sails arrives idle, and must be ordered to patrol on a later turn.**
`PlanTaskForceMove` takes the task force off patrol as soon as the leg is planned (the
original's sailing planner sets task-force state 1), and nothing puts it back. Together with
one order per turn this means a fleet sent to blockade a port takes two turns: one to arrive
and one to start patrolling. The same holds for a defender: the owner's warship that sails
into a blockaded zone does not lift the blockade until it patrols there.

### Leg length (chosen rule 11)

A leg is no longer than the slowest hull's `SeaZones`, and a zone it cannot reach at all
gives a leg of length zero. That is PR #35's `PlanTaskForceMove`, unchanged. **A destination
beyond range is therefore sailed towards rather than refused**, and the event reports both
where the task force got to and where it was bound; the turn report calls a short leg a
shortfall. See "Open" for why this is labelled.

### Never throwing (chosen rule 6)

**An invalid naval order or declaration is refused with an event and never throws.**
Simultaneous turns make an impossible order ordinary — a fleet can be sunk, or a task force
can change, between the orders being written and the turn resolving — so the planner
(`NavalPlanner`) checks every condition the `WorldState` methods throw on before calling
them. Only a `null` order is malformed, and `CountryTurnOrders` rejects it on construction.

## What this does not do

- **Interception, capture and trade cancellation.** A patrol cuts ports and does nothing
  else; `TradeCancellation` stays empty. These need combat odds (row 6).
- **The port blockade mission** (the red "X" cursor, lines 1922–1923). The executable's port
  predicate counts only the patrol state, so patrol is what cuts a port.
- **Peace and treaties** (row 5), **landings**, and **placing newly built ships**: a new
  ship is unplaced, so it is refused as `FleetNotPlaced` until placement is modelled.
- **Aggressiveness** (lines 4664–4668). Not stored.

## Open: an out-of-range move

The manual says an out-of-range destination is refused at the cursor: "If the new zone is
out of range you see a red slashed circle cursor; you cannot move the f leet that far in one
turn" (lines 1916–1917). PR #35's planner instead sails the furthest leg it can towards the
destination, which it reads from the executable's planning routine, and its own comment says
the routine's "relative place in the wider simultaneous turn is still unproven". This slice
keeps the planner as it is and labels the result (chosen rule 11). If the executable's cursor
check is read and agrees with the manual, the change is one more refusal reason.

## Soak

No soak policy declares war or orders a fleet, so **no published soak number moved**.

## Tests

`NavalOrdersTests` (Core) and `TurnReportRendererTests` / `TurnReportTextTests`
(Presentation). The end-to-end test declares war on turn 1, assembles and patrols beside
the owner's port on turn 2 (port cut), holds on turn 3 (still cut), sails the owner's
warship in on turn 4 (arrives idle; still cut), patrols it on turn 5 (port open), sails it
away on turn 6 (cut) and sails the raider away on turn 7 (open).
`AWarDeclaredThisTurnCutsNothingUntilNextTurn` holds the delay.

Each of these mutations was run and fails at least one test:

| Mutation | Caught by |
|---|---|
| Remove the hostility delay (`HasEffectiveHostility` ignores the token) | `AWarDeclaredThisTurnCutsNothingUntilNextTurn` |
| Let a moved task force arrive patrolling | `AMovedTaskForceArrivesIdleAndStaysIdleUntilOrderedToPatrol`, `AMoveSpendsTheTurnSoAPatrolAfterItIsRefused`, the end-to-end test |
| Drop the one-mission-per-turn check | `ATaskForceTakesOneMissionOrMoveATurn` |
| Resolve each move at once, before later orders such as patrols | `OrdersResolveInCountryIdOrderThenListOrderAndAllMovesAfterAllOrders` |
| Resolve moves before the orders are applied | six tests, including the end-to-end test and `AMovedTaskForceArrivesIdleAndStaysIdleUntilOrderedToPatrol` |
| Throw instead of refusing: drop the unplaced-fleet check | `EveryInvalidAssemblyIsRefusedWithItsReasonAndChangesNothing` |
| Throw instead of refusing: drop the foreign-task-force check | `EveryInvalidCommandIsRefusedWithItsReasonAndChangesNothing` |
| Throw instead of refusing: drop the unknown-zone check | both `EveryInvalid…` tests above |
| Restamp a war already declared | `EveryInvalidDeclarationIsRefusedWithItsReasonAndChangesNothing`, `AMutualDeclarationInOneTurnCountsOnceForTheLowerCountryId` |
| Assemble before checking the mission (not all or nothing) | `EveryInvalidAssemblyIsRefusedWithItsReasonAndChangesNothing` |
| Allow a move to the current zone | both `EveryInvalid…` tests above |
