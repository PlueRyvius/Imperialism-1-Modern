# Definitive original-game information

Publication label: definitive_original_information

Status: This is the repository's authoritative record of observations directly recovered from the original Imperialism.exe and Data/STR#ENU.GOB. “Definitive” applies to values and resource text directly observed in those original files. Entries marked candidate or described as unresolved labels remain hypotheses, not confirmed semantics.

Source identity: Imperialism.exe SHA-256 6afab8495db715fd9e719cffa74abe5ede4dd763428ff65d73be4edf16c9e691; Data/STR#ENU.GOB SHA-256 d754e503d144086051b70be53a085ae428e151908992530b8463c2e040c2d97f.

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

## Resource-backed technology catalog

The executable's `STR#ENU.GOB` resources contain all 28 technology names in progression order. This identifies the catalog and ordering; the numeric research costs, years, prerequisites, and benefits are deliberately left pending until their data readers are isolated.

| ID | Technology | Name resource |
|---:|---|---|
| 1 | High Pressure Steam Engine | `#1073[10]` |
| 2 | Seed Drill | `#1073[11]` |
| 3 | Cotton Gin | `#1073[12]` |
| 4 | Streamlined Hulls | `#1073[13]` |
| 5 | Square-Set Timbering | `#1073[14]` |
| 6 | Iron Railroad Bridge | `#1073[15]` |
| 7 | Feed Grasses | `#1074[0]` |
| 8 | Spinning Jenny | `#1074[1]` |
| 9 | Paddlewheels | `#1074[2]` |
| 10 | Steel Plows | `#1074[3]` |
| 11 | Bessemer Converter | `#1074[4]` |
| 12 | Compound Steam Engine | `#1074[5]` |
| 13 | Rifled Artillery | `#1074[6]` |
| 14 | Breech-Loading Rifles | `#1074[7]` |
| 15 | Advanced Iron Working | `#1074[8]` |
| 16 | Power Loom | `#1074[9]` |
| 17 | Mechanical Reaper | `#1074[10]` |
| 18 | Commercial Fertilizer | `#1074[11]` |
| 19 | Oil Drilling | `#1074[12]` |
| 20 | Barbed Wire | `#1074[13]` |
| 21 | Steel Armor Plate | `#1074[14]` |
| 22 | Large Artillery | `#1074[15]` |
| 23 | Dynamite | `#1075[0]` |
| 24 | Marine Engineering | `#1075[1]` |
| 25 | Machine Guns | `#1075[2]` |
| 26 | Chemistry | `#1075[3]` |
| 27 | Improved Range-Finding | `#1075[4]` |
| 28 | Internal Combustion | `#1075[5]` |

The same archive provides `Purchased in [1:year]`, one- and two-prerequisite templates, and the purchase-screen labels for cost, benefits, details, and purchase/cancel actions. The corresponding description blocks are `#144`-`#146`; their full text is preserved per technology in the JSON, including explicit unit unlocks, upgrades, terrain access, and production effects.

## Additional cost-table leads

The `UCity.cpp` constructor creates these records as generic cost objects. Methods at `0x004B7080`, `0x004B7210`, and `0x004B7320` calculate availability, deduct commodities/cash, and produce order costs. Their owning action/list labels remain unresolved:
- `0x00695C50`: 9 records; each is 2 paper + expert worker, with cash values $1,500, $500, $1,000, $1,000, $2,000, $1,000, $1,000, $2,000, and $5,000.
- `0x00695CDE`: 7 records with stored IDs 1-7; all fields duplicate authoritative army records 1-7.

## Still candidate

The cash-table leads at `0x0065046A`, `0x00650650`, and `0x00650660` are preserved in JSON, but their exact city-action labels remain unresolved. The verified unit purchase tables are now separate from these general cash-action leads.

## Source

- Executable: `Imperialism.exe`
- SHA-256: `6afab8495db715fd9e719cffa74abe5ede4dd763428ff65d73be4edf16c9e691`
- W32Dasm audit: `local disassembly audit database`
