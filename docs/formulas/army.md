# The army: the Armoury, marching, moving by rail, and land battles

## Summary

A country buys regiments at the Armoury and moves them about its own territory. Buying
names a **category** and a count; the Armoury builds that category's **best type the
country knows**, and each regiment costs one worker of the type's grade, armaments (some
also horses or fuel) and cash. The worker leaves industry for good the moment the order is
given. The regiment stands in the capital's province from `Delivery` and may be moved from
the next turn.

A move names regiments standing together and a destination province the country owned as
the turn opened; any other destination is an **attack** (6b, under Battles below). An
**adjacent** destination is a march and costs nothing; anything further is **rail**, which
needs an unbroken line of the country's own rail and costs **five transport capacity per
armaments point**, from a budget equal to the country's whole network and separate from
what that network carries in commodities. Militia never leaves its province.

Content **v32** adds `armyTypes`: thirty rows in the executable's own order, which is what
a scenario `army` record's 0-based type indexes. There is **no new turn phase**: purchases
resolve in `Production` after the University, deliveries in `Delivery`, moves in
`Conflict` after the naval orders. The client smoke still counts fourteen phases.

6a fought nothing. **6b** adds attacks, a headless strategic battle, health and healing,
experience, entrenchment, the *effect* of a fort, and capture — with **no content bump and
no new phase**: attacks resolve in `Conflict` after the moves, and healing closes it.
Building a fort is still pending.

**6d** adds **landings**: an attack on a province that does not border the regiments' own,
through a **landing site** a task force of the country established on an earlier turn
(`naval.md`, rules 29–33). The regiments cross from any coast with a sea path, up to what
the site's ships can carry this turn; the hostile fleets in the zone fight the site first,
and ships lost there **drown** the regiments that no longer fit. The survivors then fight
6b's battle unchanged. **The capacity reading, drowning and the interception battle are
placeholders** until a PC trace (`wanted-values.md` §8). Rules 31–37 below; still no content
bump and no new phase.

## Confidence

`inferred`, with every price and every category/era `verified`. The executable supplies the
Armoury's bill for all thirty types and which category and era each fills; the manual
supplies the movement rules and the technology per era; the binary's technology effect
strings corroborate the manual's eras for every regular type. Eleven rules are chosen and
labelled where they appear and in the table.

**Battles (6b) are weaker.** The battle loop's shape, healing and experience are `verified`.
Casualties are a **`guess`**: a labelled placeholder until the PC trace. Rules 12–30 have
their own table under Battles.

| Claim | Support |
|---|---|
| A regiment costs a worker, armaments, cash, and for some horses or fuel | **`verified`** — the 30-record purchase table at `0x00695CD0` (`definitive-original-data.md`, "Verified army purchase costs"); **manual** 2726–2731 |
| The workers "can never return to industry" | **manual**, 2729–2731 |
| Only the best available regiment of each category can be built, and a technology replaces the old one | **manual**, 2733–2754 |
| Which category and era each of the thirty types fills | **`verified`** — the role-slot table at `0x00695528`: slots 0–7 repeat per era, the engineers are slot 8, the Generals slot 9 |
| Which technology opens each era | **manual**, the Regimental Upgrade Requirement Table, 1620–1641; **corroborated** by the `STR#ENU.GOB` technology effect strings for every regular type (below) |
| "Rifled Guns" and "Heavy Guns" are Rifled Artillery and Large Artillery | **chosen rule 10** — an inference from the manual, **now resource-backed**: the effect strings say exactly this |
| Militia is never built in the Armoury and never leaves its province | **manual**, 1645–1655 |
| Minutemen, Militia and Conscripts "cannot be deployed by rail" | **manual**, 4038–4041 |
| New units start in the province of the capital | **manual**, 2675–2682 |
| A march goes to an adjacent friendly province; the train goes further in friendly territory | **manual**, 1751–1763 |
| Five transport capacity per armaments point | **manual**, 1751–1763 and 4038–4041 |
| Total transport capacity limits regiments moved by train; moving them "does not in itself use capacity already assigned" | **manual**, 1758–1760 and 2176–2179 |
| Military conflicts are resolved after production | **manual**, 640 |
| The fighting numbers | **manual**, Regiment Abilities, 4040–4072; the executable wins on firepower (Grenadiers 12.5). **Recorded and read by nothing** |
| **A category builds its best type known as the turn opens** | **chosen rule 1** |
| **Paid in `Production`, after the University, before recipes and the market** | **chosen rule 2** |
| **All or nothing per order; later orders unaffected; a category may repeat** | **chosen rule 3** |
| **Only healthy workers are taken** | **chosen rule 4** |
| **Delivered in `Delivery` to the capital's province; no capital, nothing delivered and nothing refunded** | **chosen rule 5** |
| **Rail needs both provinces on one component of the country's own rail** | **chosen rule 6** |
| **The rail budget is total capacity, separate from commodity transport, not carried over** | **chosen rule 7** |
| **Moves are all or nothing, one per regiment per turn, countries in id order, orders in list order** | **chosen rule 8** |
| **Destination ownership is read as the turn opens** | **chosen rule 9** |
| **Militia growth as a province develops** | **chosen rule 11** — not modelled; **pending** |

## What the binary said about era gates

The task was to look for an executable era gate before transcribing the manual's, because
if one existed it would win. **There is no structured technology → army table** in the
extraction. The army records carry a category slot and a bill, and nothing names a
technology.

What the executable *does* carry is the purchase screen's description of each technology,
in `STR#ENU.GOB` (`definitive-original-data.md`, the technology table's effect column):

| Technology | Effect string |
|---|---|
| Bessemer Converter | Permits Sharpshooters and Scouts |
| Rifled Artillery | Permits Field Artillery and Siege Artillery |
| Breech-Loading Rifles | Permits Rifle Infantry, Guards, and Carbine Cavalry |
| Large Artillery | Permits Railroad Guns and Mobile Artillery |
| Machine Guns | Permits Modern Infantry, Machine Gunners, and Rangers |
| Internal Combustion | Permits Armored and Mechanized regiments |

That agrees with the manual's table for **every regular type**, and it is what turns rule 10
from a guess about two unfamiliar names into a reading two sources share: the manual's
"Rifled Guns" are the technology that permits Field and Siege Artillery, which is Rifled
Artillery, and its "Heavy Guns" are Large Artillery.

**Four gates are the manual's alone**: Combat Engineers (Bessemer Converter), Saboteurs
(Dynamite), Militia (Breech-Loading Rifles) and Conscripts (Machine Guns). No effect string
names them. The militia gates are recorded and change nothing yet, since militia is never
bought and its free upgrade is not modelled (rule 11).

**Grenadiers fire 12.5** in the executable, where the manual prints 12. The executable has
one firepower figure; an infantry row's two manual columns are equal throughout, so both
carry 12.5. That second half is a reading.

**The manual's "Horse Artillery" is the executable's Light Artillery** (id 6). The importer
uses the executable's name, since a scenario record indexes the executable's table.

## The army table

Thirty rows, in the executable's order (`LegacyWorldConverter.StandardArmyTypes`). The
engineers are three eras of one category; the three Generals are never bought, carry no
price, no ARMS and no stats.

| Ids | Era | Gate |
|---|---|---|
| 0–7 Minutemen, Skirmishers, Regulars, Grenadiers, Hussars, Cuirassiers, Light Artillery, Artillery | 1 | none |
| 8–15 Militia, Sharpshooters, Rifle Infantry, Guards, Scouts, Carbine Cavalry, Field Artillery, Siege Artillery | 2 | Breech-Loading Rifles, Bessemer, BLR, BLR, Bessemer, BLR, Rifled Artillery, Rifled Artillery |
| 16–23 Conscripts, Rangers, Modern Infantry, Machinegunners, Mechanized Infantry, Armor, Mobile Artillery, Railroad Gun | 3 | Machine Guns ×4, Internal Combustion ×2, Large Artillery ×2 |
| 24–26 Sappers, Combat Engineers, Saboteurs | 1–3 | none, Bessemer, Dynamite |
| 27–29 General | — | never bought |

`arms` is the armaments in the bill wherever both exist, and the importer test asserts it.

## Buying

1. The category resolves to the type with the highest era whose technology the country held
   **as the turn opened** (**chosen rule 1**). Buy Bessemer Converter this turn and the Light
   Cavalry category gives Scouts next turn, not this one — the same "bought this turn, known
   next turn" as every other gate, because `Investment` runs last. A category with no
   purchasable type — militia, Generals, or any category in a world without a catalogue — is
   refused `NotPurchasable`.
2. Each regiment takes **one healthy worker** of its type's grade (**chosen rule 4**), its
   bill and its cash. The worker leaves the workforce for good, and this turn's recipes
   already see the smaller pool, as with the University's expert. The pool the Armoury
   draws on is the healthy workforce the turn opened with, less this turn's Trade School
   trainees and University experts, so no worker is spent twice.
3. Paid in `Production`, after the University and before any recipe and before the market
   (**chosen rule 2**), so the armaments cannot also be sold or consumed.
4. **All or nothing** per order (**chosen rule 3**): a shortage of workers, any commodity or
   cash refuses the order and charges nothing; a later order still runs against what is left;
   a category may appear twice. Affordability is checked by division, so an overflowing count
   is refused, never thrown.
5. Delivered in `Delivery` to the province holding the capital (**chosen rule 5**). That is
   after `Conflict`, so a regiment cannot be moved the turn it is bought. A country whose
   capital has gone by then gets nothing and is refunded nothing
   (`RegimentDeliveryLostEvent`).

## Moving

1. Every regiment named must exist, belong to the ordering country, stand in one province
   with the others, and not have moved this turn. The destination must be a different
   province **the country owned as the turn opened** (**chosen rule 9**). Since 6b a
   destination outside that snapshot is not refused: it is an attack (rule 12), and the old
   `NotOwnProvince` refusal is retired, kept only so no later enum value moves.
2. Militia (`leavesProvince` false) is refused on every move, march or rail.
3. **Adjacent → march**, free. Two provinces are adjacent when a cell of one is a hex
   neighbour of a cell of the other (`ProvinceAdjacency`, derived once per map at any size).
4. **Anything else → rail.** Both provinces must hold a cell in one component of the
   country's ownership-filtered rail (**chosen rule 6**: "through friendly territory" read as
   an unbroken line of own rail). Every regiment must have `movesByRail`. The order costs
   `5 × Σ arms`.
5. The budget is the country's **whole transport capacity as the turn opened** (**chosen
   rule 7**). Commodity transport does not reduce it and it does not reduce commodity
   transport; it resets every turn and is not carried over.
6. **All or nothing** per order, one move per regiment per turn, countries in id order and
   orders in list order (**chosen rule 8**). A later order naming a regiment that has moved is
   refused whole. A refusal is an event, never an exception.

## Initial armies

A scenario's `army` records become regiments at world creation, owned by the **province's
initial owner** — the owner is then stored on the regiment, so a capture can put one
country's regiments in another's province without a model change. The record's experience
field counts **medals**; the compiler stores **hundredths**, medals × 100 clamped to 400, as
the executable does. Every imported regiment starts at health 500. A record on an unowned province is
not placed and is listed in `WorldState.UnplacedInitialArmies`. A world with no catalogue
(any migrated package) places none, exactly as it behaved at v31.

## Battles

An order whose destination the country **did not own as the turn opened** is an attack.
`Conflict` runs the naval orders, then **every move** (6a, unchanged), then **every attack**,
then **healing**. A regiment that moved cannot attack (`AlreadyMoved`), so a regiment still
acts once a turn.

**The loss formula below is a placeholder.** The recovered loop sets its shape, but it is
not the original's formula. The executable gives the loop's shape: the snapshot, the
half-health test, attacker first, the 1-or-2 weight, mod 7, the artillery half branch, the
[70, 80, 90, 90] scale and +35/+20. It does not say how those pieces combine, whether
defence enters, or how a target is chosen. Every number that depends on the combination is
chosen, and each will be replaced once the PC trace in `docs/disasm/wanted-values.md` §
"Land battles" is done. The tests are built so that no test pins a casualty figure only the
placeholder produces.

### Checking an attack

An attack is checked **when it comes to resolve**, against the live state. It needs 6a's
common checks: the regiments exist, are the country's own, stand together, are not militia,
have not moved, and the destination is a different province on the map. It is then refused
with an event, never an exception, when:

- the destination does **not border** the regiments' province: `NotAdjacent`. There is no
  attack by rail (manual 1765–1770). With a ready landing site the same order is a
  **landing** instead; see *Landings* below.
- the destination is **already the attacker's**, for instance because it was taken earlier
  this turn: `AlreadyOwn`.
- its **current** owner has **no war in effect** with the attacker as the turn opened:
  `NotAtWar`. This uses the same `HasEffectiveHostility` reading as naval patrols and
  boycotts, so a war declared this turn does not count yet. A province that changed hands
  earlier in the turn is judged by its new owner, so "the invasion does not take place"
  (manual 3798–3817).

### Fighting

**Defenders** are every regiment of the province's current owner standing in it, militia
and regiments that marched in this turn included. A type with **no combat stats** (the
Generals) is not a combatant: it travels, retreats and surrenders with its side and is never
fired on. If no defender is a combatant, the province is taken **uncontested**. If no
attacker is a combatant, the attack loses after 0 rounds.

`StrategicBattleResolver`, behind the internal `IBattleResolver` in the same way the price
rule sits behind `ITradeMarket`, works as follows:

1. Snapshot every combatant's health. *(binary)*
2. Loop while both sides hold a regiment with `health > snapshot / 2`. *(binary)* Stop after
   **20 rounds** regardless, and count that as a **defender win** (rule 17).
3. Each round the attackers fire, then the defenders. The order is the binary's; firing in
   sequence within a round is chosen. Within a side, regiments fire in id order, and every
   regiment above health 0 fires once:
   - `w` = 2 if `health > snapshot / 2`, else 1. *(binary)*
   - `m` = 1 + draw mod 7 (rule 18). For artillery, `m` = (m + 1) / 2 (rule 19).
   - `fp` = FPN × (100 + 25 × medals) / 100 (rule 20).
   - `scale` = [70, 80, 90, 90][min(round, 3)] (rule 21).
   - `shot` = ⌊w × m × fp × scale / 100⌋, in decimal.
   - The target is a uniformly drawn opposing regiment above health 0 (rule 22).
   - `loss` = max(1, shot × 5 / DEF), or 0 when shot is 0, with DEF ≤ 0 read as 1
     (rule 23). A defender's DEF is its entrenched figure unless it is artillery (rule 24).
     A defender's loss is then × (100 − 10 × fort) / 100 (rule 25).
   - Health is floored at 0. *(binary)*
4. The attacker wins if it still holds an eligible regiment and the battle was not stopped.
   The attacker is checked first. *(binary)*
5. Every survivor gains **+35 if its side won and +20 if not**, capped at 400. *(binary)*
   Regiments at health 0 are removed.

**Experience is stored in hundredths**: 100 is one medal and 400, four medals, is the cap.
"Three victories or five defeats earns one medal" (manual 1847–1857) is the binary's
+35/+20 against 100.

### Capture

When the attacker wins, or the province is undefended:

- `SetProvinceOwner` runs **at once**, so a later attack the same turn sees the new owner. If
  the province held the previous owner's capital, that capital is cleared through the same
  call (rule 27).
- The **previous owner's civilians** in the province are killed (manual 1162–1164). Other
  countries' civilians are untouched.
- The attackers move in.
- The defenders' survivors **retreat** to the lowest-id adjacent province their owner still
  holds, or **surrender** and are removed (rule 26).
- The province's fort level is **kept** (rule 28).
- `Conflict` runs before `Extraction`, so the captured ground **pays its new owner this
  turn**: "economic changes begin" (line 1570). This is a consequence of phase order, not a
  rule of its own.

When the defender wins, the attackers stay where they attacked from.

### Healing

At the end of `Conflict`, every regiment **not named in an accepted move or attack** this
turn gains **+100, capped at 500**. The amount, the cap and the "no movement order" sentinel
are the binary's; the timing is chosen (rule 29). A defender gave no order, so it heals the
same turn it fought. One `RegimentsHealedEvent` is emitted per country, and only when
something healed.

### Battle chosen rules

The numbering continues from 6a.

| # | Rule | Standing |
|---|---|---|
| 12 | The verb is decided by ownership as the turn opened: owned is a move, anything else an attack | chosen |
| 13 | Every move resolves before every attack | chosen |
| 14 | Attacks run countries in id order, orders in list order; the manual's army initiative is not modelled | chosen |
| 15 | An attack is checked live, so an earlier capture the same turn changes whom it is against | chosen, **manual**-shaped (3798–3817) |
| 16 | An ownerless province may be attacked without war: the anarchy rule (line 700) applied to Core's one ownerless state | chosen reading |
| 17 | The battle stops after 20 rounds, counted as a defender win | chosen |
| 18 | The multiplier is 1 + draw mod 7. The mod 7 is the **binary's**; the +1 is chosen | half binary |
| 19 | Artillery's half multiplier rounds up, (m + 1) / 2. The half branch is the **binary's**; the rounding is chosen | half binary |
| 20 | +25% firepower per medal, linear, to match "roughly twice as powerful" at four medals | chosen |
| 21 | The scale array is indexed by the 0-based round, clamped at 3. The array is the **binary's** | half binary |
| 22 | The target is drawn uniformly from the opposing regiments above 0 | chosen |
| 23 | loss = max(1, shot × 5 / DEF). The 5 calibrates an even fight of Regulars to break in about four rounds | **chosen placeholder** |
| 24 | Entrenchment is the entrenched DEF, so the manual's 20% is not applied twice; artillery never entrenches | chosen reading; artillery from the **release notes** |
| 25 | The fort reduction, 10% a level and at most three levels, applies to artillery too | **manual** amount (3947–3959); the scope is chosen |
| 26 | Survivors retreat to the lowest-id own neighbour, else surrender | chosen, from the manual's flee-or-surrender (3913–3915) |
| 27 | A captured capital is cleared with no elimination or anarchy | chosen gap; elimination is 6d |
| 28 | A captured province keeps its fort level | chosen |
| 29 | Healing comes after every battle | chosen timing; the amount and cap are the **binary's** |
| 30 | Each battle's generator is SplitMix64 seeded `turnSeed ^ Mix(ordinal + 1)`, with ordinal the 0-based battle index in the turn | chosen |

The same seed and the same orders give the same battles
(`TheSameSeedFightsTheSameBattleAndAnotherSeedNeedNot`).

**A sample battle.** Seed 42: two Regulars attack two Regulars in the open. The defender
holds after **8 rounds**. Attacker health goes 500 → 240 and 500 → 63, and each survivor
gains +20. Defender health goes 500 → 187 (+35) and 500 → 0, killed. This shows what the
placeholder does. It is not evidence for it.

## Landings

Row 6d. The naval side — establishing a site, the arrival rule and the interception battle — is
`naval.md`, rules 29–33. The mechanic as the owner stated it is `game-systems.md:272-274`:
"Amphibious landings need a fleet to spend a turn establishing a beachhead, which persists only
while you hold ships in the zone. The landing force is capped by the total armaments cost of the
ships performing it."

"Superficially, this seems exactly like an attack on an adjacent province. Once your fleets
establish a landing site, you select land forces and use the Crossed Swords attack cursor to
order them to move directly across the seas to the enemy province" (manual 1774–1777). So
there is no new order: a `RegimentMoveOrder` whose destination is an attack and **does not
border** the regiments' province is a landing when the country has a **ready site** for it.

**A ready site (chosen rule 31)** is a task force of the country holding a landing site on the
destination, established on an **earlier** turn, whose present zone still touches the
destination, checked live. If several are ready, the lowest task-force id carries.

**The checks (chosen rule 35)**, after 6a's common checks (so militia is still
`CannotLeaveProvince`), in this order:

1. An **adjacent** destination is 6b's attack, unchanged. A site never changes it.
2. No ready site: `NotAdjacent`, as before.
3. No cell of the regiments' province touches a sea zone: `NotCoastal`.
4. No sea path joins a zone touching the regiments' province and the site's zone: `NoSeaPath`.
5. 6b's `AlreadyOwn`, then `NotAtWar` (an ownerless province needs no war), against live
   ownership.
6. The load does not fit what the site has left: `NotEnoughLandingCapacity`.

**Coast and sea path (chosen rule 32).** Troops embark from any coast of their own province, not
only the site's own zone, and **the path has no length limit**: they "move directly across the
seas", and the fleet already sailed there. Whether the original makes them embark on the site's
coast is a trace target.

**Capacity (chosen rule 33 — a placeholder).** A site carries **Σ over its live fleets of count ×
the armaments in the hull's build bill**; a merchant's bill holds none. A landing's **load** is Σ
of its regiments' `arms`, a type with none (a General) counting 0. Capacity is **per site per
turn**, spent **cumulatively** by the landings through it in resolution order, and a landing that
does not fit what is left is **refused whole** — all or nothing, like the rail budget. It is a
separate budget from rail and from commodity transport, and nothing carries over. Core has no
key lookup for commodities, so the armaments commodity is **the one every purchasable armed
army type's bill holds in exactly its `arms` quantity**; if that does not single one out, no
site carries anything (chosen rule 37).

**Drowning (chosen rule 34 — a placeholder).** "If any of your ships are sunk, some of your land
forces may be drowned" (manual 1780–1781). After the site's interception battle (naval rule 31),
the capacity is recomputed from the surviving ships. If the load no longer fits, regiments drown
from the **end of the order's roster** — the highest ids, since attackers are sorted — until the
rest fit; a drowned regiment is removed. If the site lost or was sunk, **every regiment drowns**
and the landing does not happen: once embarked, troops whose transports are gone do not come
home. Later landings through the site are judged against what it has left, or against "no ready
site" if it is gone. A refused landing never embarks, so it never drowns.

**The battle (chosen rule 36).** The survivors attack through 6b's resolver and `Capture`
unchanged — same ordinal stream, same retreat and surrender, same experience, and no terrain or
sea penalty invented for landing. A **beaten** landing force stays in its source province, as a
beaten 6b attacker does. Landed regiments have acted, so they do not heal this turn. The site
**persists** after the landing (naval rule 32).

`RegimentsLandedEvent` names the task force, source, destination, arms used and room left, and
comes before the battle; `RegimentsDrownedEvent` names who drowned. A sample landing, with its
interception battle, is in `naval.md` under *Landing sites*.

### Landing chosen rules

| # | Rule | Standing |
|---|---|---|
| 31 | A non-adjacent attack with a ready site is a landing; a ready site is the lowest-id own task force holding a site on the destination, established on an earlier turn, its zone still touching the destination | chosen; the earlier turn is the **manual's** (1943) |
| 32 | Troops embark from any coast of their province, by any sea path of any length | chosen |
| 33 | Capacity = Σ count × armaments in each hull's build bill, per site per turn, spent cumulatively; a landing that does not fit is refused whole | **chosen placeholder**; the cap by armaments is `game-systems.md:272-274` |
| 34 | Regiments drown from the end of the roster until the rest fit; all of them if the site was beaten or sunk | **chosen placeholder** |
| 35 | The refusal order: adjacent → 6b, then `NotAdjacent`, `NotCoastal`, `NoSeaPath`, `AlreadyOwn`, `NotAtWar`, `NotEnoughLandingCapacity` | chosen |
| 36 | The survivors fight 6b's battle unchanged; a beaten landing stays home; landed regiments do not heal | chosen |
| 37 | The armaments commodity is the one every armed type's bill holds in exactly its `arms` quantity | chosen, for want of a key lookup in Core |

Row 6d's mutations, each run, red, and reverted (the naval ones are in `naval.md`):

| Mutation | Caught by |
|---|---|
| A site is ready the turn it is established | `TroopsCannotCrossTheTurnTheSiteIsEstablishedButMayTheNext` |
| Capacity per order instead of cumulative | `TwoLandingsThroughOneSiteSpendItsCapacityCumulatively` and three more |
| Drown from the front of the roster | `AnIdleEnemyInTheSitesZoneFightsTheLanding…DrownFromTheEnd` and three more |

## What is not modelled

Building forts (an Engineer order with no known price or gate); the tactical battlefield and
initiative; Generals in battle and morale; Combat Engineers against forts; elimination and
anarchy (6e); alliances joining a war; militia growth and its free upgrade (rule 11);
regiments aboard ships as a standing state, and ship damage. No soak policy orders
regiments, so no soak number moved.
