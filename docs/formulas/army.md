# The army: the Armoury, marching and moving by rail

## Summary

A country buys regiments at the Armoury and moves them about its own territory. Buying
names a **category** and a count; the Armoury builds that category's **best type the
country knows**, and each regiment costs one worker of the type's grade, armaments (some
also horses or fuel) and cash. The worker leaves industry for good the moment the order is
given. The regiment stands in the capital's province from `Delivery` and may be moved from
the next turn.

A move names regiments standing together and a destination province the country owns. An
**adjacent** destination is a march and costs nothing; anything further is **rail**, which
needs an unbroken line of the country's own rail and costs **five transport capacity per
armaments point**, from a budget equal to the country's whole network and separate from
what that network carries in commodities. Militia never leaves its province.

Content **v32** adds `armyTypes`: thirty rows in the executable's own order, which is what
a scenario `army` record's 0-based type indexes. There is **no new turn phase**: purchases
resolve in `Production` after the University, deliveries in `Delivery`, moves in
`Conflict` after the naval orders. The client smoke still counts fourteen phases.

6a **fights nothing**. Every move goes to the country's own province; entering anyone
else's is an attack, which is the land-battle slice (6b), along with health, experience,
fortifications and capture.

## Confidence

`inferred`, with every price and every category/era `verified`. The executable supplies the
Armoury's bill for all thirty types and which category and era each fills; the manual
supplies the movement rules and the technology per era; the binary's technology effect
strings corroborate the manual's eras for every regular type. Eleven rules are chosen and
labelled where they appear and in the table.

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
   province **the country owned as the turn opened** (**chosen rule 9**). Nothing changes
   ownership mid-turn yet, so this rule cannot be observed until 6b; it is a snapshot taken
   in `TurnResolver` so that capture will not need a second rule.
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
initial owner** — the owner is then stored on the regiment, so 6b can put one country's
regiments in another's province without a model change. A record on an unowned province is
not placed and is listed in `WorldState.UnplacedInitialArmies`. A world with no catalogue
(any migrated package) places none, exactly as it behaved at v31.

## What is not modelled

Land battles, health, healing, experience gain, fortifications, entrenchment and capture
(6b); militia growth and its free upgrade (rule 11); Generals; regiments aboard ships. No
soak policy orders regiments, so no soak number moved.
