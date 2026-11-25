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
there, cuts that port's route in `Extraction` (`PortAccess`, unchanged).

**Row 6c makes war at sea do something.** A third mission, `Blockade(country)`, sits a task
force off a capital. In `Conflict`, after every naval order and sailing leg and before any
regiment moves, a **patrol finds every hostile task force in its zone and fights it**;
`StrategicNavalBattleResolver` sinks ships, an empty fleet is removed and an empty task
force dissolved, and the loser's survivors go idle — so a beaten patrol stops cutting its
port in the same turn's `Extraction`. The empty `TradeCancellation` phase now has a body:
each deal settled in `Trade` sails capital to capital in its carrier's holds, and a hostile
patrol beside either capital (25%) or a blockade of either country (50%) may intercept it,
after first fighting the carrier's anchored escorts. An intercepted cargo is **turned back**
to the seller, **captured** by the interceptor, or **sunk** with one of the carrier's
merchant hulls. **The money already paid stands.**

**The resolver's combination, both chances and the outcome split are placeholders** until
the executable's `0x005578A0` is traced on the PC (`wanted-values.md` §7). The loop's shape
is 6b's recovered one; what is invented is labelled below as chosen rules 11–28.

**Row 6d adds the landing site**, the executable's task-force state 5. A fourth mission,
`Landing(province)`, holds a site on a province the task force's zone touches; from the next
turn regiments may cross to it from any coast with a sea path (`army.md`, rules 31–37). A
site neither cuts a port nor intercepts merchants. It is fought over in two ways the manual
names: a hostile task force **sailing into** its zone attacks it the turn it arrives, and the
first landing through it in a turn is attacked by **every** hostile task force in the zone,
whatever it is doing. Ships lost there drown troops. **The interception battle is a
placeholder** until a PC trace (`wanted-values.md` §8). Naval chosen rules 29–33 below.

There is **no new turn phase** (the client smoke test still counts fourteen), **no content
version bump**, and **no soak number moved**: no soak policy declares war or orders a fleet.

## Confidence

`inferred`. The manual states that a declaration cannot be refused and takes effect next
turn, and that a fleet takes a mission or a move in a turn but not both. The patrol state
and the sailing leg are PR #35's executable readings. Thirty-three rules below are chosen,
and are labelled where they appear and in the table. Rules 13, 22 and 25 are
**placeholders**, not readings: they stand until `0x005578A0` is traced. Rule 31 is a
placeholder until the landing path is traced (`wanted-values.md` §8).

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
| A destination beyond the slowest hull's reach, or one no sea path reaches, is refused | **manual**, lines 1916–1917 |
| A fleet may blockade; a blockade intercepts more often than a patrol and seeks no battle, but a hostile patrol finds it | **manual**, lines 1919–1933 |
| A blockade is task-force state 6, which the port predicate does not count | **executable**, `definitive-original-data.md` ("Strategic mission construction") |
| Battles and interceptions both end in sinking, capture or a cargo turned back | **manual**, lines 1929–1933 and 4696–4707 |
| Ships docked at the capital escort merchants; interception needs a war | **manual**, lines 2123–2132 and 3176–3185 |
| The loop shape: snapshot, half-health test, attacker first, 1-or-2 weight, mod 7, [70, 80, 90, 90] | **executable**, 6b's land loop (`army.md`), carried over by analogy — not traced for ships |
| Firepower, range, armour and hull scale per class | **executable**, the verified naval table |
| 11. A blockade names a country and acts on its capital | **chosen rule** |
| 12. A blockade persists like a patrol until moved or re-ordered, and does not cut ports | **chosen rule** for persistence; **executable** for the ports (state 6) |
| 13. The resolver's combination: health = hull scale, `shot = weight × multiplier × firepower × scale`, `damage = max(1, shot × (100 − armour) / 100)` | **chosen rule — a placeholder** until `0x005578A0` |
| 14. In the opening round only ships whose range matches or beats the enemy's longest fire | **chosen rule**, from "superior range is the most important statistic" (line 2113) |
| 15. Twenty rounds without a decision is a defender win | **chosen rule** |
| 16. The attacker is the patrolling side, or the lower id when both patrol | **chosen rule** |
| 17. A beaten side's surviving task forces go idle; survivors are undamaged | **chosen rule** |
| 18. A fleet in no task force is anchored and a patrol does not engage it | **chosen rule** |
| 19. Battles are pairwise in (zone, lower id, higher id) order, against live state, before land moves | **chosen rule** |
| 20. A deal sails capital to capital, exposed only in the zones touching either capital | **chosen rule** |
| 21. The carrier, not the buyer or seller as such, must be at war with the interceptor | **chosen rule** |
| 22. 25% for a patrol, 50% for a blockade | **chosen rule — a placeholder** until `0x005578A0` |
| 23. Deals in the order `Trade` settled them; interceptors in live task-force order, one try each, until one succeeds | **chosen rule** |
| 24. Escorts are warships in no task force in a zone touching the carrier's capital; one escort battle per (task force, carrier) a turn | **chosen rule** |
| 25. The three outcomes are drawn uniformly | **chosen rule — a placeholder** until `0x005578A0` |
| 26. Money settled in `Trade` is never reversed | **chosen rule** |
| 27. A sunk merchant comes from the carrier's lowest-id merchant fleet | **chosen rule** |
| 28. The generator streams and salts | **chosen rule** |
| A fleet may establish a landing site near a coast; troops may not cross the turn it is established, and may on any later turn while ships hold the zone | **manual**, lines 1935–1945 |
| A landing site is task-force state 5, which the port predicate does not count | **executable**, `definitive-original-data.md` ("Strategic mission construction") |
| A fleet moving into a zone holding a hostile landing fleet intercepts it, the one exception to a move arriving idle | **manual**, lines 1889–1891 |
| Enemy fleets "entering or present" in the zone fight the landing fleets on the turn troops cross; sunk ships drown troops | **manual**, lines 1778–1781 and 1940–1942 |
| The landing force is capped by the armaments cost of the ships performing it | `game-systems.md:272-274`, the owner's reading; **not traced** |
| 29. A site needs no war, may name an ownerless province, and is usable from the next turn; re-ordering it restamps that turn; `None` keeps it | **chosen rule** |
| 30. The arrival rule's attacker is the arriving side; if both arrived and both hold sites, the lower id | **chosen rule** |
| 31. The first landing through a site in a turn is attacked by every hostile country's task forces in the zone, any activity, one country at a time in id order, until the site is gone; once per site a turn | **chosen rule — a placeholder** until the landing path is traced |
| 32. A site persists until moved, re-ordered, stood down or dissolved, including after a landing through it; it intercepts no merchant | **chosen rule**; the ports are **executable** (state 5) |
| 33. Landing battles share the `"NAVALBAT"` salt and the turn's naval ordinal count | **chosen rule** |

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

Successful Shipyard orders are paid in `Construction` and delivered in `Delivery`, after
`Trade` and `Conflict`. This is a **chosen rule following** the manual's "at the start of
the next turn" (see `shipbuilding.md`). An existing fleet can be assembled while another
hull of its class is being built; the new hull arrives after orders resolve and can be
assembled starting next turn.

It is refused when:

| Reason | When |
|---|---|
| `NoFleetsSelected` | the list is empty |
| `FleetSelectedTwice` | a fleet appears twice |
| `NoSuchFleet` | no fleet carries an id |
| `FleetNotYours` | a fleet belongs to another country |
| `FleetNotPlaced` | a fleet has no sea zone — a merchant ship, a warship without a capital-adjacent sea zone, or a legacy record naming a zone this map does not have (`shipbuilding.md`) |
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
| `Blockade(country)` | `BlockadeTaskForce`: the task force blockades `country`'s capital from its present zone. `TaskForceBlockadeOrderedEvent`. See [The blockade](#the-blockade) |
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
but that sentence is about a blockade, so this is a reading for patrols and not a quotation
of a rule. A blockade persists the same way (chosen rule 12).

### A move arrives idle (chosen rule 2)

**A task force that sails arrives idle, and must be ordered to patrol on a later turn.**
`PlanTaskForceMove` takes the task force off patrol as soon as the leg is planned (the
original's sailing planner sets task-force state 1), and nothing puts it back. Together with
one order per turn this means a fleet sent to blockade a port takes two turns: one to arrive
and one to start patrolling. The same holds for a defender: the owner's warship that sails
into a blockaded zone does not lift the blockade until it patrols there.

### Leg length

**A move beyond the slowest selected hull's `SeaZones`, or to a zone no sea path reaches, is
refused** (`NavalOrderRefusal.OutOfRange`), charged nothing, and leaves the task force free for
another order the same turn. The manual's interface never accepts the order: "If the new zone
is out of range you see a red slashed circle cursor; you cannot move the f leet that far in one
turn" (lines 1916–1917). An unreachable zone is out of range at any distance, so it falls under
the same sentence. Orders stand for what a player can enter, so they follow the cursor.

PR #35's `PlanTaskForceMove` still trims a long request to the furthest leg it can sail, which
it reads from the executable's planning routine. That is not a contradiction: the cursor stops
the order and the planner never sees one out of range. The planner is unchanged for direct
callers, and the turn report still words a short leg as a shortfall should one ever arrive.

### Never throwing (chosen rule 6)

**An invalid naval order or declaration is refused with an event and never throws.**
Simultaneous turns make an impossible order ordinary — a fleet can be sunk, or a task force
can change, between the orders being written and the turn resolving — so the planner
(`NavalPlanner`) checks every condition the `WorldState` methods throw on before calling
them. Only a `null` order is malformed, and `CountryTurnOrders` rejects it on construction.

## The blockade

> A fleet can blockade a designated port, using a ship with a red "X" cursor, by clicking
> near the port when that cursor appears. Both of these commands are used against enemy
> forces. A blockade has greater chance of success than a mere patrol against ships that
> enter or leave the selected port. A blockade does not seek other ships in the sea zone,
> however, hostile patrolling fleets can intercept your ships on blockade.
> — manual lines 1922–1927

`NavalMission.Blockade(country)` is accepted through `Assemble` and `Command` like `Patrol`,
spends the turn like `Patrol`, and needs no war to be ordered — it acts only against a
hostile carrier.

**A blockade names a country and acts on its capital (chosen rule 11).** Merchants in this
model sail only between capitals (rule 20), so a capital is the one port a blockade can
watch; the tutorial's own example is "a blockade of Deneb's capital" (lines 4689–4690). It is refused,
with a `NavalOrderRefusedEvent` and nothing changed, when:

| Reason | When |
|---|---|
| `NoSuchCountry` | the target id is not a country in this world |
| `BlockadeOwnCountry` | the target is the ordering country |
| `TargetHasNoCapital` | the target has no capital |
| `CapitalNotOnThisSeaZone` | the target's capital cell touches no cell of the task force's present zone (`PortAccess.AdjacentSeaZones`, now shared) |

**It persists, and it never cuts a port (chosen rule 12).** A blockade lasts like a patrol
until the task force is moved or given another mission; a move clears both the activity and
`TaskForceState.BlockadeTarget`. That it does not cut a port is the executable's, not ours:
the mission builder sets task-force **state 6** for a blockade, and the port predicate
counts only states 3 and 4. `PortAccess` is unchanged. If the target later loses or moves
its capital, a blockade whose zone no longer touches the new one intercepts nothing until
re-ordered.

## Naval battles

Resolved in `Conflict`, after every naval order and every sailing leg, and **before** any
regiment moves or attacks (chosen rule 19).

> Both blockades and patrols can result in battle against hostile warships, or in
> interception of merchant ships.
> — manual lines 1929–1930

**Who fights whom.** For each sea zone holding a task force, in id order, and each pair of
countries there with lower id first: the pair fights when it is **effectively hostile** and
**at least one side patrols** there. Two blockades or two idle task forces never seek each
other (lines 1925–1927); a patrol finds a blockade and an idle task force alike. All of each
country's task forces in the zone fight together. **The attacker is the patrolling side, or
the lower id when both patrol (chosen rule 16).** A fleet in no task force is **anchored and
not engaged (chosen rule 18)** — which is what keeps escorts at home safe until they escort.
Three or more countries in one zone are resolved pairwise against live state, so a country
sunk in an earlier pair has nothing left for a later one.

**The resolver — `StrategicNavalBattleResolver`, behind `INavalBattleResolver`.** One
combatant per ship. A class with no combat stats stands aside, is not listed and cannot be
sunk; a merchant has stats and firepower 0, so it is a target that never fires. Then, as in
6b's land loop:

- health is the class's `HullScale` (600–2,800), snapshotted at the start;
- rounds go on while both sides hold a ship above half its snapshot, to at most twenty;
  **twenty rounds without a decision is a defender win (chosen rule 15)**;
- the attacker volleys before the defender each round;
- each firer draws `multiplier = 1 + rng mod 7`, has weight 2 above half health and 1 below,
  and the round scale is `[70, 80, 90, 90]` (the last repeating);
- **`shot = weight × multiplier × firepower × scale`** and the target is a uniform draw among
  the enemy's live ships; **`damage = max(1, shot × (100 − armour) / 100)`**, and a ship at 0
  is sunk.

**Every combination in that last bullet is a placeholder (chosen rule 13)** until
`0x005578A0` is traced. `ShipCombatStats` holds firepower in whole units (a Frigate is 3), so
there is no `/ 100`: a Frigate's shot at multiplier 1, full health, round 0 is
2 × 1 × 3 × 70 = 420 against a 900-point hull.

**Range opens the battle (chosen rule 14).** "Superior range is the most important statistic"
(line 2113): in round 0 a ship fires only if its range matches or beats the enemy side's
longest. From round 1 everyone fires. An outranged ship skips without drawing.

**After a battle.** Sunk ships leave their fleets (`WorldState.SinkShips`). A fleet at zero
is removed, and a task force with no fleets left is dissolved; **ids are never reused**, so
`Fleets` and `TaskForces` are live views and fleet *n* is no longer necessarily at index
*n* − 1. Use `TryGetFleet` / `TryGetTaskForce`; a command to a dissolved task force is
refused as `NoSuchTaskForce`. **The loser's surviving task forces go idle and the winner keeps
its mission (chosen rule 17)**, so a beaten patrol stops cutting its port in this turn's
`Extraction`. Survivors come out undamaged: damage is not stored. `NavalBattleFoughtEvent`
names the attacker, defender, winner, rounds and each fleet's ships before and sunk.

## Interception and escorts

Resolved in `TradeCancellation`, which runs after `Conflict` and before `Extraction` — the
manual's "Intercepted or blockaded trades are cancelled" (lines 630–641). `TurnResolver`'s
`Trade` branch keeps each deal's `DeliveryId` beside its `PlannedTrade` in a turn-local list;
nothing is added to world state.

**The route (chosen rule 20).** A deal's cargo is carried by its `HoldsPaidBy` country between
the seller's capital and the buyer's. It is exposed only in the sea zones touching either
capital; an inland capital exposes nothing at its end. There is no route geometry between
ports.

**Who may intercept.** A task force of a country effectively hostile to the **carrier (chosen
rule 21)** — not to the buyer or seller as such. Between two Great Powers the buyer carries,
so a war with the seller alone leaves the cargo alone. The force must be:

- **patrolling** a zone the deal is exposed in: **25%**; or
- **blockading** the seller or the buyer from a zone that still touches that country's
  current capital: **50%** — "a greater chance of success".

**Both chances are placeholders (chosen rule 22).** **Deals go in the order `Trade` settled
them; for each, the task forces try in live task-force order, once each, until one succeeds
(chosen rule 23).** A deal's draws come from their own stream, keyed by its index.

**Escorts (chosen rule 24).** "Any ship docked at your capital and fully repaired is used to
escort merchants during that turn" (lines 3184–3185). A carrier's escorts are its warship
fleets (firepower above 0) **in no task force** whose zone touches its capital. Damage is not
stored, so every ship is fully repaired. The first time a task force tries a deal of a
carrier that has escorts, it fights them first, as the attacker; the battle is a
`NavalBattleFoughtEvent` of kind `Escort`. A task force that loses is beaten — idle — and
intercepts nothing more this turn. One that wins goes on to its draw. **An escort battle is
fought at most once per (task force, carrier) a turn.**

**What happens to the cargo (chosen rule 25).** On success the deal's pending delivery is
cancelled and `rng mod 3` picks, uniformly — another placeholder:

| Outcome | Effect |
|---|---|
| `TurnedBack` | the goods return to the **seller**, arriving next turn ("take their cargo back to port", line 4706) |
| `Captured` | the goods go to the **interceptor**, arriving next turn |
| `Sunk` | the goods are lost, and the carrier loses one merchant hull |

**Settled money stands (chosen rule 26).** The seller may already have spent it in
`Production` or `Construction`, and no treasury may go negative, so an intercepted buyer has
paid for nothing. Whether the original refunds is a trace target.

**Which hull sinks (chosen rule 27).** The carrier's lowest-id fleet of a merchant class (cargo
above 0, firepower 0) that still has a ship. A country whose holds are counted but not held in
any fleet record loses one of those instead, lowest class first; one with neither loses
nothing, and the event says so.

**The generator (chosen rule 28).** `SplitMix64.ForBattle(seed ^ salt, ordinal)`. Battles salt
with `"NAVALBAT"` and number their ordinals from 0 in the order fought, escort battles
continuing the same count; interceptions salt with `"INTERCEP"` and use the deal index. No
naval stream shares a land battle's.

## Landing sites

Row 6d. The army side — who may cross, how much a site carries and who drowns — is in
`army.md`, rules 31–37. `game-systems.md:272-274` states the mechanic in one sentence and
keeps its wording.

**The mission.** `NavalMission.Landing(province)` is given through `Assemble` or `Command` like
a patrol or a blockade and spends the turn. The task force's activity becomes
`TaskForceActivity.Landing`, with `LandingTarget` and `LandingSince` (the turn it was
established); both clear whenever the activity changes. It is refused, with an event and no
change, as `UnknownProvince` when the id is out of range, `LandingOnOwnProvince` when the
ordering country owns the target, and `ProvinceNotOnThisSeaZone` when no cell of the target
touches the task force's present zone (`PortAccess.AdjacentSeaZones`).

**No war is needed (chosen rule 29)**, as with a patrol or blockade: war is checked when troops
land. An ownerless province may be named. "You cannot move land forces on the turn the landing
site is established" (line 1943), so a site is ready only when `LandingSince` is an earlier
turn, and **re-ordering a landing, even at the same province, restamps it**: it is a new
site. A `None` mission spends nothing and keeps the site (rule 8).

**What a site does not do (chosen rule 32).** It is state 5, so `PortAccess`, which counts only
the patrol state, never counts it: it cuts no port. It intercepts no merchant either; only a
patrol or a blockade draws for a deal. It persists until the task force moves, takes another
mission, stands down after a lost battle, or is dissolved — and a successful landing does not
end it.

**Arriving intercepts (chosen rule 30).** A moved task force arrives idle and normally seeks no
battle. The manual's one exception: "A fleet ordered to move into a sea zone containing hostile
landing fleets intercepts the landing" (lines 1889–1891). `ResolveConflict` hands
`ResolveBattles` a turn-local set of the task forces that arrived by a move; in the existing
(zone, pair) loop a pair with no patrol still fights when one side arrived and the other holds
a site there. The arriving side attacks, every task force of the landing side in the zone
defends, and the battle is a `NavalBattleFoughtEvent` of kind `Landing`. If both arrived and
both hold sites, the lower id attacks. A hostile already lying idle in the zone does not attack
the site on this step; it fights the landing itself, below.

**The interception battle (chosen rule 31 — a placeholder).** Before the first landing through a
site in a turn crosses, **every effectively hostile country with task forces in the site's zone,
whatever they are doing** — idle, patrolling, blockading, holding a site of their own — attacks
it: "Any enemy fleets entering or present in the sea zone with the landing site automatically
intercept and battle the landing fleets" (lines 1778–1781). One country at a time in id order;
the hostile side attacks and the landing country's task forces in the zone defend, through 6c's
`Fight` and resolver. The loser stands down (rule 17), so a beaten site ends at once and the
battles stop. It is fought **once per site a turn**; later landings through the site see its
result. Ships sunk here shrink what the site can carry, and the regiments that no longer fit
drown (`army.md`, rule 34).

**The stream (chosen rule 33).** Landing battles salt with `"NAVALBAT"` and continue the turn's
naval ordinal count, so they collide with neither a land battle nor a 6c battle.

**A sample landing**, from `LandingTests` (seed 1). Home holds a site on Beach from East with one
Battleship and three Lighters; a Lighter's build bill holds 3 armaments, the Battleship's
none, so the site carries **9**. Nine Regulars (1 armament each, load 9) are ordered across
from Harbour. Other has two Frigates idle in East, so they intercept: **Home wins in 2 rounds**,
both Frigates sunk and one Lighter lost. The site now carries **6**: regiments **7, 8 and 9
drown** — the end of the roster — and **1–6 land**, using 6 with 0 left. Beach is undefended
and is captured uncontested. This shows what the placeholder does. It is not evidence for it.

## What this does not do

- **Ship damage and repair**, and so the manual's "fully repaired" condition on escorts.
  **Ship experience** and **Admirals**.
- **Aggressiveness** (lines 4664–4668). Not stored.
- **Warships built for a country with no capital or capital-adjacent sea zone**: these ships
  remain unplaced and are refused as `FleetNotPlaced`, and so cannot hold a landing site.
- **Troops aboard ships** as a standing state: a landing crosses within one turn's `Conflict`.
- **Routes between ports other than capitals.**
- **Minor Nations' own merchants**: they own none (`trade.md`), so a minor carrier never
  arises.
- **AI**: nothing orders a blockade or a patrol for a country that has no player.

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

Row 6c's tests are `NavalWarTests` (Core) and
`ANavalBattleAndAnInterceptionSayWhoLostWhatAndThatTheMoneyStands` (Presentation). Core
exposes no seam for a stub generator, so a test needing a given outcome searches the turn
seeds for one, and the chance test counts successes over 200 seeds. Each of these mutations
was run, turned at least one test red, and was reverted:

| Mutation | Caught by |
|---|---|
| Drop the range rule | `InTheOpeningRoundOnlyTheLongerRangeFiresSoTheOutrangedSideTakesNothingBack` |
| Let a blockade cut ports (`PortAccess` counts `Blockading`) | `ABlockadeDoesNotCutAPortButAPatrolDoes` |
| Drop the war check on interception | `InterceptionNeedsTheCarrierAtWarWithTheInterceptor` |
| Drop the war check on battle | `ANonHostilePairDoesNotFight` |
| Swap the patrol and blockade chances | `ABlockadeInterceptsMoreOftenThanAPatrol` |
| Refund the buyer on interception | the three outcome tests, `InterceptionNeeds…`, `ABlockadeInterceptsMore…` |
| Let anchored fleets be engaged | `AnAnchoredFleetIsNotEngagedByAPatrol`, `ABeatenPatrolGoesIdle…`, the three escort tests |
| Skip the escort battle | `EscortsFightOncePerTaskForceAndCarrierEvenAcrossSeveralDeals`, `ABeatenInterceptorGoesIdleAndInterceptsNothingMore` |
| Make every outcome a capture | `ATurnedBackCargoReturnsToTheSellerAndTheMoneyStands`, `ASunkMerchantLosesTheCargoAndOneHull` |

Row 6d's tests are `LandingTests` (Core) and
`ALandingSaysWhereItCameFromWhatItCarriedAndWhoDrowned` (Presentation). Each of these
mutations was run, turned at least one test red, and was reverted (the army ones are listed in
`army.md`):

| Mutation | Caught by |
|---|---|
| Drop the arrival rule (`ResolveBattles` sees no arrivals) | `AHostileSailingIntoTheSitesZoneFightsItButOneAlreadyThereDoesNot` |
| Let only patrols intercept a landing | `AnIdleEnemyInTheSitesZoneFightsTheLanding…`, `ALostInterceptionDrownsEveryone…`, `TheInterceptionIsFoughtOncePerSitePerTurn` |
| Let a landing site cut ports (`PortAccess` counts `Landing`) | `ALandingSiteCutsNoPortWhereAPatrolDoes` |
