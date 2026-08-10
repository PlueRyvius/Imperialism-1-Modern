# Definitive original-game information

Publication label: `definitive_original_information`

Status: This is the repository's authoritative record of observations directly recovered from the original Imperialism.exe and Data/STR#ENU.GOB. “Definitive” applies to values and resource text directly observed in those original files. Entries marked candidate or described as unresolved labels remain hypotheses, not confirmed semantics.

Source identity: Imperialism.exe SHA-256 `6afab8495db715fd9e719cffa74abe5ede4dd763428ff65d73be4edf16c9e691`; Data/STR#ENU.GOB SHA-256 `d754e503d144086051b70be53a085ae428e151908992530b8463c2e040c2d97f`.

The original binary, resource archive, disassembly listing, and audit database are not committed. The logical source names and hashes above identify the local source used for this extraction.

See [definitive-original-data.json](definitive-original-data.json) for the machine-readable record and full technology descriptions.

## Extracted tables and formulas

## Verified naval table

The executable contains 14 rows × 9 fields at `0x00698108`, with 36-byte rows. Accessors read the low signed 16 bits from each dword slot.

| ID | Ship | Firepower | Range | Armor | Hull scale | Speed | Cargo | Sea zones |
|---:|---|---:|---:|---:|---:|---:|---:|---:|
| 0 | unused/placeholder | 0.0 | 0 | 100 | 0 | 0 | 0 | 0 |
| 1 | Trader | 0.0 | 0 | 0 | 600 | 0 | 2 | 1 |
| 2 | Indiaman | 0.0 | 0 | 5 | 1000 | 0 | 4 | 1 |
| 3 | Frigate | 3.0 | 5 | 10 | 900 | 4 | 0 | 3 |
| 4 | Ship-of-the-Line | 6.0 | 6 | 20 | 1700 | 3 | 0 | 2 |
| 5 | Paddlewheeler | 0.0 | 0 | 5 | 900 | 0 | 8 | 1 |
| 6 | Clipper | 0.0 | 0 | 0 | 600 | 0 | 4 | 1 |
| 7 | Raider | 3.0 | 7 | 20 | 700 | 7 | 0 | 5 |
| 8 | Ironclad | 5.0 | 8 | 55 | 1200 | 5 | 0 | 3 |
| 9 | Advanced Ironclad | 10.0 | 10 | 60 | 1800 | 6 | 0 | 4 |
| 10 | Freighter | 0.0 | 0 | 25 | 1200 | 0 | 16 | 1 |
| 11 | Armored Cruiser | 6.0 | 9 | 50 | 1000 | 8 | 0 | 6 |
| 12 | Dreadnought | 20.0 | 13 | 70 | 2800 | 7 | 0 | 5 |
| 13 | Battlecruiser | 18.0 | 13 | 55 | 2200 | 9 | 0 | 6 |

Field 2 is stored as an armor complement: `armor = 100 - stored_value`. Field 3 is an internal hull scale used by the battle-report damage normalization path, so it should not be replaced with the manual's H number without preserving that conversion. Field 6 is the internal sorted combat-group key; field 8 is an internal combat-tier threshold used by fleet-strength aggregation, but its user-facing label remains a candidate.

## Verified naval material costs

Six 30-entry uint16 arrays at `0x00695B50`, `0x00695B70`, `0x00695B90`, `0x00695BB0`, `0x00695BD0`, and `0x00695BF0` are consumed by the ship availability, maximum-quantity, deduction, order-cost, and UI cost-list paths. The UI reader maps them to the following commodities:

| Ship | Fabric/Textiles | Lumber | Armaments | Steel | Coal | Fuel |
|---|---:|---:|---:|---:|---:|---:|
| Trader | 2 | 4 | 0 | 0 | 0 | 0 |
| Indiaman | 3 | 7 | 0 | 0 | 0 | 0 |
| Frigate | 2 | 5 | 2 | 0 | 0 | 0 |
| Ship-of-the-Line | 3 | 8 | 5 | 0 | 0 | 0 |
| Paddlewheeler | 0 | 6 | 0 | 2 | 10 | 0 |
| Clipper | 2 | 6 | 0 | 0 | 0 | 0 |
| Raider | 0 | 6 | 3 | 0 | 10 | 0 |
| Ironclad | 0 | 4 | 6 | 4 | 10 | 0 |
| Advanced Ironclad | 0 | 8 | 15 | 10 | 20 | 0 |
| Freighter | 0 | 0 | 0 | 8 | 20 | 0 |
| Armored Cruiser | 0 | 2 | 8 | 6 | 20 | 0 |
| Dreadnought | 0 | 0 | 24 | 30 | 0 | 20 |
| Battlecruiser | 0 | 0 | 18 | 22 | 0 | 20 |

The Frigate row is 2 fabric/textiles + 5 lumber + 2 armaments; the Dreadnought row is 24 armaments + 30 steel + 20 fuel. These match the legacy unit document for the documented warships. Merchant-ship rows are included from the executable even where the old document omits their recipes.

## Verified army purchase costs

The executable contains 30 seven-word records at `0x00695CD0`, with 14-byte strides. The record contains the unit ID, two optional commodity requirements, cash, and worker tier. Commodity IDs are 16 armaments, 5 horses, and 12 fuel; 65535 means no commodity.

| ID | Unit | Worker | Materials | Cash |
|---:|---|---|---|---:|
| 0 | Minutemen | untrained | - | $0 |
| 1 | Skirmishers | untrained | 1 armaments | $200 |
| 2 | Regulars | untrained | 1 armaments | $500 |
| 3 | Grenadiers | trained | 1 armaments | $1,000 |
| 4 | Hussars | untrained | 1 armaments, 1 horses | $100 |
| 5 | Cuirassers | trained | 1 armaments, 1 horses | $500 |
| 6 | Light Artillery | trained | 2 armaments, 1 horses | $1,000 |
| 7 | Artillery | trained | 2 armaments | $1,000 |
| 8 | Militia | untrained | - | $0 |
| 9 | Sharpshooters | untrained | 2 armaments | $3,000 |
| 10 | Rifle Infantry | untrained | 2 armaments | $3,000 |
| 11 | Guards | trained | 2 armaments | $4,000 |
| 12 | Scouts | untrained | 2 armaments, 1 horses | $2,000 |
| 13 | Carbine Cavalry | trained | 2 armaments, 1 horses | $3,500 |
| 14 | Field Artillery | trained | 4 armaments, 1 horses | $5,000 |
| 15 | Siege Artillery | trained | 4 armaments | $5,000 |
| 16 | Conscripts | untrained | - | $0 |
| 17 | Rangers | trained | 4 armaments | $5,000 |
| 18 | Modern Infantry | trained | 4 armaments | $5,000 |
| 19 | Machinegunners | trained | 4 armaments | $7,000 |
| 20 | Mechanized Infantry | trained | 4 armaments, 4 fuel | $5,000 |
| 21 | Armor | trained | 10 armaments, 4 fuel | $9,000 |
| 22 | Mobile Artillery | trained | 6 armaments, 4 fuel | $5,000 |
| 23 | Railroad Gun | trained | 8 armaments | $9,000 |
| 24 | Sappers | expert | 2 armaments | $5,000 |
| 25 | Combat Engineers | expert | 2 armaments | $7,000 |
| 26 | Saboteurs | expert | 3 armaments | $9,000 |
| 27 | General | expert | - | $0 |
| 28 | General | expert | - | $0 |
| 29 | General | expert | - | $0 |

The table matches the documented army costs, including horse requirements for cavalry/artillery, fuel for mechanized units, and expert-worker requirements for Sappers, Combat Engineers, and Saboteurs. The purchase-cost reader at `0x004EA700` combines these requirements with live commodity and worker prices.

## Verified army fields

The army manager has contiguous 30-entry tables indexed by army type:

| Field | Address | Storage/decode | Reader | Status |
|---|---|---|---|---|
| movement | `0x0064C660` | uint16 / `stored_value / 10` | `0x004A7F4E` | verified |
| firepower | `0x0064C6A0` | float32 / `stored_value / 10` | `0x004A7F28` | verified |
| range | `0x0064C790` | uint32 / `stored_value` | `0x004A7F33` | verified |
| unknown_combat_factor | `0x0064C718` | float32 / `stored_value` | `0x004A818A` | candidate |
| behavior_flags | `0x0064C808` | uint8 / `stored_value != 0` | `0x004A38F6` | candidate |
| score_additive | `0x00695578` | uint32 / `stored_value` | `0x004A7F99` | candidate |
| unit_value_candidate | `0x006955F0` | uint32[30] / `stored_value` | `0x004A5AA0 / 0x004E3111` | candidate |

Movement, firepower, and range match the unit reference table for the 30 army IDs. The executable stores movement in tenths and firepower as a float scale divided by 10; it preserves a 12.5 Grenadier value where the old editor note rounds it to 12. The candidate `0x006955F0` unit-value table is preserved in JSON because army-manager routines sum and condition-weight it for internal scoring.

## Verified internal army/tactical tables

These compact tables are read by the army manager, defense minister, tactical damage, and order-list paths:

| Field | Address | Storage/decode | Reader | Status |
|---|---|---|---|---|
| composition_class | `0x00695380` | uint16[30] / `stored class value (1-3)` | `0x004A2214 / 0x004A7C85 / 0x00515156` | verified_internal_role |
| composition_lookup | `0x006953C0` | int8[4][4] / `lookup[min_class + 4 * max_class]` | `0x004A224B / 0x004A7CBC` | verified_internal_role |
| defense_power_x10 | `0x006953E8` | uint16[30] / `stored_value / 10` | `0x004ED1DF` | verified_internal_role |
| artillery_flag | `0x00695428` | uint8[30] / `stored_value != 0` | `0x004A8173` | verified_internal_role |
| unit_role_slot | `0x00695528` | uint16[30] / `stored role slot (0-9)` | `0x005C3494 / 0x005B0355` | verified_internal_role |

The artillery flag is set exactly for the six artillery records. The role-slot table repeats slots 0-7 for each technology era, then reserves slot 8 for the three engineering units and slot 9 for General records. The defense-minister power table is a related internal combat contribution: most ordinary units mirror the main firepower scale, Armor uses its alternate 60 value, and artillery/special records are zero. The composition matrix is an internal mixed-stack code; its final UI label remains open.

## Verified tactical roster and AI boundary

The original scenario `army` record is `[province, type, count]`, with `type`
zero-based into the 30-row table above. It supplies a province stack; it does
not carry a duplicate owner. The original battle setup at `0x005A4790` creates
two side controllers in fixed input order: its first side is marked `1` and
its second side `0`. For each side it invokes `0x0059B1B0`, which iterates the
source army list and allocates one 0x58-byte tactical unit object per source
entry through `0x005A5F20`. The surrounding army-manager entry at `0x004A5B10`
creates the parent battle object, calls that setup, then hands control to the
parent's virtual lifecycle entry at `0x0059FC20`.

`UTacPlayer`'s decision callback at `0x0059C440` consumes those side rosters,
gathers side statistics through `0x0059B5B0`, and sets per-unit orders through
its state-specific helpers. This proves a headless tactical-AI/roster boundary
and attacker-first setup order, but not deployment, legal movement, damage,
victory, or province-capture rules. Those remain deferred.

The unattributed function at `0x005C4C60` is not the tactical resolver: its
strings and caller form randomized battle-report prose. Do not use that range
as evidence for tactical mechanics.

## Verified original map geometry

The `UMap.cpp` coordinate routines establish a 108 Ã— 60 legacy cell grid. Internally, horizontal positions use a doubled coordinate, so the row-major cell index is `floor(horizontal_subcoordinate / 2) + 108 * y`; the vertical coordinate is bounded to 0 through 59. The six signed neighbor offsets are read from `0x00696E70` and `0x00696E80`.

| Direction | Horizontal subcoordinate delta | Vertical delta |
|---:|---:|---:|
| 0 | 1 | -1 |
| 1 | 2 | 0 |
| 2 | 1 | 1 |
| 3 | -1 | 1 |
| 4 | -2 | 0 |
| 5 | -1 | -1 |

The seam-normalization routine at `0x00512D13` applies the original horizontal wrap behavior, while `0x00512850` performs the row-major index calculation. This is geometry evidence only; it does not imply that the Modern engine must retain the original fixed dimensions.

## Resource-backed production formulas

The original `STR#ENU.GOB` help resources directly state the following:

- Textile Mill: 2 labor, 2 cotton_or_wool → 1 fabric
- Clothing Factory: 2 labor, 2 fabric → 1 clothing
- Steel Mill: 2 labor, 1 coal, 1 iron → 1 steel
- Metalworks: 2 labor, 2 steel → 1 armaments_or_hardware
- Lumber Mill: 2 labor, 2 timber → 1 lumber_or_paper
- Furniture Factory: 2 labor, 2 lumber → 1 furniture
- Oil Refinery: 2 labor, 2 oil → 1 fuel
- Food processing: 2 labor, 2 grain, 1 produce, 1 meat → 2 canned_food
- Transport capacity: 2 labor, 1 steel, 1 lumber → 1 transport_capacity
- Power: 1 fuel → 6 power, with up to 100 fuel per turn.
- Worker conversion: 1 food + 1 furniture + 1 clothing → 1 untrained worker; untrained worker + 1 paper + $100 → trained worker; trained worker + 2 paper + $1,000 → expert worker.

## Verified technology purchase metadata and resource-backed catalog

The executable's `STR#ENU.GOB` resources contain all 28 technology names in progression order. The technology-store code also reads a zero-based 28-entry cash-cost table at `0x0066AAE8` through the purchase paths at `0x005B0B30` and `0x005B0BB0`; the purchase record's `+0x64` field supplies that internal selector. The report displays those entries as the resource/scenario progression IDs (1-based), so the displayed ID is internal index + 1. The first two entries are starting technologies and have zero purchase cost.

| ID | Technology | Cash cost | Availability offset window | Name resource | Resource-backed effect summary |
|---:|---|---:|---|---|---|
| 1 | High Pressure Steam Engine | $0 | starting technology | `#1073[10]` | Engineers may build railroads through farms, plains, deserts, and forests. |
| 2 | Seed Drill | $0 | starting technology | `#1073[11]` | Farmers improve Grain and Produce to Level I, producing 2 per turn. |
| 3 | Cotton Gin | $1,000 | 1–5 (inclusive) | `#1073[12]` | Cotton plantations improve to Level I, producing 2 Cotton per turn. |
| 4 | Streamlined Hulls | $1,000 | 6–10 (inclusive) | `#1073[13]` | Permits construction of Clippers. |
| 5 | Square-Set Timbering | $1,500 | 6–10 (inclusive) | `#1073[14]` | All mines improve to Level II; Coal/Iron produce 4 and Gold/Gems produce 2 per turn. |
| 6 | Iron Railroad Bridge | $1,500 | 6–10 (inclusive) | `#1073[15]` | Engineers may rail swamps; Foresters improve Timber to Level I, producing 2 per turn. |
| 7 | Feed Grasses | $1,500 | 6–10 (inclusive) | `#1074[0]` | Ranchers improve Wool and Livestock to Level I, producing 2 per turn. |
| 8 | Spinning Jenny | $1,500 | 11–15 (inclusive) | `#1074[1]` | Cotton and Wool improve to Level II, producing 3 per turn. |
| 9 | Paddlewheels | $3,000 | 11–15 (inclusive) | `#1074[2]` | Permits construction of Large Merchant Ships and Raiders. |
| 10 | Steel Plows | $3,000 | 16–20 (inclusive) | `#1074[3]` | Grain and Produce improve to Level II, producing 3 per turn. |
| 11 | Bessemer Converter | $3,000 | 21–25 (inclusive) | `#1074[4]` | Permits Sharpshooters and Scouts, and upgrades Light Infantry and Hussars. |
| 12 | Compound Steam Engine | $6,000 | 21–25 (inclusive) | `#1074[5]` | Engineers may rail hills; Foresters improve Timber to Level II, producing 3 per turn. |
| 13 | Rifled Artillery | $7,000 | 26–30 (inclusive) | `#1074[6]` | Permits Field Artillery and Siege Artillery, and upgrades older artillery. |
| 14 | Breech-Loading Rifles | $10,000 | 26–30 (inclusive) | `#1074[7]` | Permits Rifle Infantry, Guards, and Carbine Cavalry, and upgrades older regiments. |
| 15 | Advanced Iron Working | $12,000 | 31–35 (inclusive) | `#1074[8]` | Permits construction of Ironclads. |
| 16 | Power Loom | $12,000 | 31–35 (inclusive) | `#1074[9]` | Cotton and Wool improve to Level III, producing 4 per turn. |
| 17 | Mechanical Reaper | $12,000 | 36–40 (inclusive) | `#1074[10]` | Grain improves to Level III, producing 4 per turn. |
| 18 | Commercial Fertilizer | $12,000 | 41–45 (inclusive) | `#1074[11]` | Produce improves to Level III, producing 4 per turn. |
| 19 | Oil Drilling | $12,000 | 41–45 (inclusive) | `#1074[12]` | Permits Drillers, Oil Level I, oil prospecting in Desert/Tundra/Swamp, Refineries, and Power Plants. |
| 20 | Barbed Wire | $25,000 | 46–50 (inclusive) | `#1074[13]` | Livestock improves to Level II, producing 3 per turn. |
| 21 | Steel Armor Plate | $20,000 | 51–55 (inclusive) | `#1074[14]` | Permits warships with larger guns and heavier armor. |
| 22 | Large Artillery | $40,000 | 56–60 (inclusive) | `#1074[15]` | Permits Railroad Guns and Mobile Artillery, and upgrades older artillery. |
| 23 | Dynamite | $40,000 | 56–60 (inclusive) | `#1075[0]` | Engineers may rail mountains; Foresters improve Timber to Level III (4); all mines reach Level III (Coal/Iron 6, Gold/Gems 3). |
| 24 | Marine Engineering | $40,000 | 56–60 (inclusive) | `#1075[1]` | Permits construction of Armored Cruisers. |
| 25 | Machine Guns | $40,000 | 61–65 (inclusive) | `#1075[2]` | Permits Modern Infantry, Machine Gunners, and Rangers, and upgrades older regiments. |
| 26 | Chemistry | $100,000 | 61–65 (inclusive) | `#1075[3]` | Drillers improve Oil to Level II (4); Ranchers improve Livestock to Level III (4). |
| 27 | Improved Range-Finding | $120,000 | 66–70 (inclusive) | `#1075[4]` | Dreadnoughts and Battle Cruisers may use Oil rather than Coal. |
| 28 | Internal Combustion | $150,000 | 66–70 (inclusive) | `#1075[5]` | Permits Armored and Mechanized regiments and upgrades older units; Drillers improve Oil to Level III, producing 6 per turn. |

The 26 non-starting technologies receive an inclusive pseudo-random availability offset from the 26 two-word windows at `0x0066ABA4`, initialized by `0x005AF330`; the two starting technologies have no generated window. These are executable turn-offset ranges, not a claim that the earliest boundary is the only arrival date. The effect summaries and structured `effects` arrays are direct normalizations of the corresponding description strings, not guesses about numeric data. The same archive provides `Purchased in [1:year]`, one- and two-prerequisite templates, and the purchase-screen labels for cost, benefits, details, and purchase/cancel actions. The final conversion from these offsets to the displayed calendar year remains a model simplification.

### Verified technology prerequisite graph

The technology-store reader at `0x005B0A90` reads the prerequisite pair for the
requested 1-based technology ID from `0x0066AC10 + 4 * id`; its two field reads
are at `0x005B0AA1` and `0x005B0AB9`/`0x005B0AE0`. The table is 29 rows of two
little-endian signed 16-bit IDs: row 0 is the all-zero sentinel, rows 1–28 use the
same one-based namespace as scenario `tech` records, and zero in either field means
no prerequisite. The store UI calls this reader at `0x005B1768`.

| ID | Prerequisite IDs, in stored order |
|---:|---|
| 1–4 | — |
| 5 | 1 High Pressure Steam Engine |
| 6 | 1 High Pressure Steam Engine |
| 7 | — |
| 8 | 7 Feed Grasses; 3 Cotton Gin |
| 9 | — |
| 10 | 2 Seed Drill |
| 11 | — |
| 12 | 6 Iron Railroad Bridge |
| 13 | — |
| 14 | 11 Bessemer Converter |
| 15 | — |
| 16 | 8 Spinning Jenny |
| 17 | 10 Steel and Iron Plows |
| 18 | 10 Steel and Iron Plows |
| 19 | — |
| 20 | 7 Feed Grasses |
| 21 | 15 Advanced Iron Working |
| 22 | 13 Rifled Artillery |
| 23 | 5 Square-Set Timbering; 12 Compound Steam Engine |
| 24 | 9 Paddlewheels; 10 Steel and Iron Plows |
| 25 | 14 Breech-Loading Rifles |
| 26 | 19 Oil Drilling |
| 27 | 24 Marine Engineering |
| 28 | 26 Chemistry |

## Verified executable raw-resource output curves

The executable contains a 23-row × 4-byte raw-resource production table at `0x00696D98`. It uses the same slot namespace as town/warehouse output, but only the map-deposit rows are nonzero:

| Resource ID | Resource | Level 0 | Level 1 | Level 2 | Level 3 |
|---:|---|---:|---:|---:|---:|
| 0 | cotton | 1 | 2 | 3 | 4 |
| 1 | wool | 1 | 2 | 3 | 4 |
| 2 | timber | 1 | 2 | 3 | 4 |
| 3 | coal | 0 | 2 | 4 | 6 |
| 4 | iron | 0 | 2 | 4 | 6 |
| 5 | horses | 1 | 1 | 1 | 1 |
| 6 | oil | 0 | 2 | 4 | 6 |
| 17 | grain | 1 | 2 | 3 | 4 |
| 18 | fruit | 1 | 2 | 3 | 4 |
| 19 | fish | 1 | 2 | 3 | 4 |
| 20 | livestock | 1 | 2 | 3 | 4 |
| 21 | gems | 0 | 1 | 2 | 3 |
| 22 | gold | 0 | 1 | 2 | 3 |

The companion selector at `0x00696DF8` tells the reader at `0x00513610` whether to use the low or high nibble of `resource_record+0x0C`; high-nibble resources are coal, iron, oil, gems, and gold. Fish uses the low nibble, so its 1/2/3/4 row is a stored source quantity rather than a confirmed civilian development level; the exact adjacency calculation remains unresolved.

Most rows directly confirm the manual's Resource Development Table: cultivated resources use 1/2/3/4, coal/iron/oil use 0/2/4/6, horses use flat 1, and gold/gems use 0/1/2/3. Fish is an important exception: the executable row is 1/2/3/4, while the manual describes fish as one per adjacent water source and not civilian-improvable. That tells us this table's fish input is a production-count/adjacency quantity, not a confirmed fish development level. The zero rows 7–16 are not missing data; they are processed-commodity slots that cannot be map deposits. The town output reader at `0x005B73E0` consumes this table. The adjacent class-gate bytes at `0x0066D770` are also verified as a production filter, but their remaining internal labels are still open.

## Town industrial development: executable rules

The original executable contains a `TTown` record (`0x0066D780`, 0x50 bytes) and a per-update method at `0x005B7570`. A town must first be eligible (`+0x4D`) and pass the six-neighbor map/transport query at `0x00513CA0`, which is the executable form of the manual's connected depot/port requirement.

The update method uses `age = current_turn - creation_turn`:
- Material progress is considered only when `age > 4` and age is even. Each eligible material channel rises by exactly 1, capped by both half of its town raw-output bucket and one quarter of the matching capital-city capacity (rounded up).
- The first three material channels use internal capital-capacity IDs 1, 5, and 3. The fourth channel uses a province-record condition and has no direct capital-capacity lookup in this method. The factory-name mapping for those numeric IDs remains deliberately open.
- Consumer-good progress is considered only when `age > 9` and age is odd. It is capped at half of the corresponding material progress and by the central consumer-capacity sum. This is the exact executable form of the manual's “materials first” and “goods capped at half material output” description.

The traced update changes production counters, not a direct hamlet/village/town map-sprite field. The two growth messages are present in `STR#ENU.GOB` block #99 (strings 12 and 13), but the separate visual/message threshold consumer remains pending; no fixed threshold is being invented here.

### Port connection: executable predicate

`0x005B7830` calls the `UMap` predicate at `0x00513CA0` before treating an
eligible town/port as connected. The predicate inspects the six original hex
neighbours. Its ocean branch calls `0x00561510`, which builds a bit mask of
eligible task forces at the target. It scans the global `TShip` list at
`0x006A3EDC`, requiring the target `UOcean` at `TShip+0x08`, an active
`TTaskForce*` at `+0x0C`, and task-force state 3 or 4 at `TTaskForce+0x08`; the
country bit comes from `TShip+0x14`. It returns blocked only when a hostile
eligible country's bit is present and the port owner's bit is absent; this is
executable evidence for the manual's *undisputed* enemy-control rule, rather than
a presence-only fleet rule.

The river branch calls `0x00563B70`, which follows the original river-code
transition tables at `0x0065C632`/`0x0065C668` (with a 100-cell safety bound).
It reports a different raw land `NationZoneA` before its route reaches ocean, and
`0x00513CA0` accepts the route only when that report is clear. Thus a river port's
connection is path-sensitive and ownership-sensitive in the original. The modern
core now reifies the map transition geometry and its 100-cell guard.

The scenario fleet representation and map bridge are recovered. The `.scn`
loader at `0x00581E60` dispatches `ship` records to the four-field big-endian
reader at `0x00582720` and `zone` records to `0x00582FA0`; both handlers resolve
their zone id through `0x0055F100`. The `ship` fields are `(country, type,
zone, count)` and create one `TShip` at `0x0054F8E0` per `count`. The created
record has the resolved `UOcean` object at `+0x08` and its owner country id at
`+0x14`. `UOcean` maps raw ocean region `r` to a 0x48-byte runtime entry at
`r - 0x17` through `0x00563300`; therefore a scenario zone id `z` maps to raw
ocean region `z + 0x17`. The loader runs in setup state 2 at `0x0057DA70`,
after state 3's map/zone preparation and before the per-power post-load calls.
Fleet placement is now executable-backed, while mission eligibility, range,
and movement remain open.

### Strategic mission construction: partial behavioral trace

The original AI submits strategic work through `UCountryAuto` at `0x004E8540`.
It calls the mission factory at `0x005350D0`, appends the resulting object to a
queued-mission collection by virtual method `+0x30`, and marks the relevant
country-side work flags pending. Factory case 4 instantiates the
`TBlockadePortMission` vtable at `0x0065AC60`; cases 0 and 2 install the
`TControlSeaZoneMission` vtable at `0x0065A740`. The control mission handler
at `0x005387F0` reads its target from object `+0x14`, enumerates `TPortZone`
entries through `0x00561C80`/`0x00561D40`, and adjusts a zone-local result when
an entry matches that target. Those iterators are not active fleet records. The
fleet record is `TTaskForce` (vtable `0x0065C468`):
`0x00553BC0` attaches its ships by writing `TShip+0x0C`, and the port predicate
uses task-force state 3 or 4. The command dispatcher `0x0055A160` maps action 12
to state 3 (`patrolling` in the status renderer), action 14 to state 6
(`blockading`), and action 16 to state 5 (invasion). State 6 does not qualify for
the port predicate. State 4 is not a player-facing mission: the navy refresh at
`0x00557560` rebuilds a per-country task force, prunes members, and invokes the
target's virtual `+0x38` predicate. The base `UOcean` implementation at
`0x0055E840` returns false and selects state 4 for ordinary sea zones, whereas
the `TPortZone` override at `0x00561680` returns true and selects state 7.
Thus state 4 is an automatic target-type resolution outcome, not a recovered
Modern command.

The phase routine at `0x0057F280` performs per-power updates, calls the navy
manager refresh `0x005577B0`/`0x00557560`, then calls `0x005578A0`. That follow-on
resolves task-force interactions with randomized checks and clears/rebuilds
manager state. This establishes the local naval-resolution order, but not the
relative order of a specific production/extraction query or a modern equivalence
for original naval combat.

The global phase-name table at `0x005421E0` names the surrounding high-level
turn phases as diplomacy, trade, city, civilians, military, money lenders,
deal book, and strategic battle report. It establishes that city/civilian and
military work are distinct original phases, while `kOptPhTransport` is an
optional UI phase. Since the table formats enum values rather than advancing
the phase state, it does not establish the exact extraction-versus-navy
interleaving; that ordering remains deferred.

### Fleet movement and range

The strategic movement record is `TTaskForce`: construction records the current
ocean/port-zone object at `+0x18`, while a requested destination is staged at
`+0x0C`. `0x005533F0` runs the graph search at `0x00560F80` from that requested
destination, begins from the current object, and repeatedly selects an adjacent
node with lower shortest-path distance. The number of selections is capped by
`0x00554A80`'s minimum `sea zones` value across the selected ship types. It then
writes the resulting reachable leg back to `+0x0C` and selects task-force state
1. `0x00556100` resolves state 1 by assigning that object to every member's
`TShip+0x08` and marking the task force complete.

`0x005610B0` caches shortest-path distances between the runtime ocean/port-zone
objects. Its pathfinder walks each node's runtime movement adjacency list
(`+0x28` with count at `+0x30`). The graph is generated from the map rather than
serialized by a scenario: UMapper allocates a base `UOcean` for each raw sea
region beginning at `0x17`, then UOcean setup (`0x00562340`) invokes
`0x00563F50`. That builder scans every `108 × 60` map cell, resolves its
base-ocean or eligible-port `TPortZone` node, and checks its six
`0x0055E360` hex neighbours. Distinct valid nodes are duplicate-checked and
linked reciprocally. The helper has row-dependent hex geometry, does not wrap
north/south, and wraps east/west only when the map seam flag at `+0x20` permits
it. `0x005635E0` creates the port-zone nodes from original eligible port
geometry and attaches them to this graph. Thus the original moves a fleet up to
its slowest member's strategic allowance along a shortest path in one
resolution; it does not move a fleet directly to a remote selected destination.
Whether a queued player order automatically reissues after a partial leg, and
the lifecycle for ports created or removed after setup, remain open. State 5
landing and state 6 blockade are not being conflated with this state-1 sailing
rule.

## Additional cost-table leads

The `UCity.cpp` constructor creates these records as generic cost objects. Methods at `0x004B7080`, `0x004B7210`, and `0x004B7320` calculate availability, deduct commodities/cash, and produce order costs. Their owning action/list labels remain unresolved:
- `0x00695C50`: 9 records; each is 2 paper + expert worker, with cash values $1,500, $500, $1,000, $1,000, $2,000, $1,000, $1,000, $2,000, and $5,000.
- `0x00695CDE`: 7 records with stored IDs 1-7; all fields duplicate authoritative army records 1-7.

## Still candidate

The cash-table leads at `0x0065046A`, `0x00650650`, and `0x00650660` are preserved in JSON, but their exact city-action labels remain unresolved. The verified unit purchase tables are now separate from these general cash-action leads.

## Source

- Executable: `Imperialism.exe`
- SHA-256: `6afab8495db715fd9e719cffa74abe5ede4dd763428ff65d73be4edf16c9e691`
- W32Dasm audit: `local disassembly audit database (not committed)`
