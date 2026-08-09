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

The 26 non-starting technologies receive an inclusive pseudo-random availability offset from the 26 two-word windows at `0x0066ABA4`, initialized by `0x005AF330`; the two starting technologies have no generated window. These are executable turn-offset ranges, not a claim that the earliest boundary is the only arrival date. The effect summaries and structured `effects` arrays are direct normalizations of the corresponding description strings, not guesses about numeric data. The same archive provides `Purchased in [1:year]`, one- and two-prerequisite templates, and the purchase-screen labels for cost, benefits, details, and purchase/cancel actions. The separate executable prerequisite graph and the final conversion from these offsets to the displayed calendar year remain pending.

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
