"""Lookup tables for the Imperialism .map / .scn binary formats.

Derived from community reverse-engineering (byte offsets and value meanings),
re-expressed here in our own structures rather than copied from any single
source document.
"""

MAP_WIDTH = 108
MAP_HEIGHT = 60
MAP_CELL_COUNT = MAP_WIDTH * MAP_HEIGHT
MAP_CELL_SIZE = 36
# The province table after the cell grid: one record per province id, so the
# count is the format's province cap. Only the town-cell field is decoded.
DORMANT_RECORD_COUNT = 384
DORMANT_RECORD_SIZE = 198
PROVINCE_TOWN_OFFSET = 4       # big-endian u16 within the record
NO_PROVINCE = 65535            # empty slot, and an ocean cell's province

TERRAIN_UNDERLAY = {
    0: "level_grass",
    1: "forest",
    2: "hill",
    3: "mountain",
    4: "swamp",
    5: "ocean",
    6: "wasteland",
    7: "farmland",
}

TERRAIN_TYPE = {
    0: "ocean",
    1: "clear",
    2: "cotton",
    3: "cattle_ranch",
    4: "horse_ranch",
    5: "grain_farm",
    6: "orchard",
    7: "wool_hill",
    8: "hill",
    9: "mountain",
    10: "swamp",
    11: "desert",
    12: "tundra",
    13: "forest",
    14: "town",
    15: "scrub_forest",
    16: "capital",
}

RESOURCE = {
    0: "cotton",
    1: "wool",
    2: "forest",
    3: "coal",
    4: "iron",
    5: "horses",
    6: "oil",
    17: "grain",
    18: "fruit",
    19: "fish",
    20: "cattle",
    21: "gems",
    22: "gold",
    255: "none",
}

# Terrain types that represent *developed* land — a resource that has been
# improved into a farm, ranch, orchard or managed forest. Each carries exactly
# the resource it exploits; across all 1,245 such cells in the shipped maps the
# pairing never breaks, so the terrain and the resource cannot be set
# independently.
#
# The implication runs one way only. A resource on undeveloped land is a normal
# state — the fruit on clear ground at (63, 15) of s1 is waiting for a Farmer —
# so a resource does *not* imply a developed terrain.
DEVELOPED_TERRAIN_RESOURCE = {
    2: 0,    # cotton       -> cotton
    3: 20,   # cattle ranch -> cattle
    4: 5,    # horse ranch  -> horses
    5: 17,   # grain farm   -> grain
    6: 18,   # orchard      -> fruit
    7: 1,    # wool hill    -> wool
    13: 2,   # forest       -> forest
}

# Every province holds exactly one town cell (120/120 and 213/213 in the
# shipped maps). What distinguishes them is this marker, not the terrain —
# terrain 16 ("capital") is never used by any shipped map.
TOWN_TYPE = {
    0: "none",
    # A minor nation's capital. Exactly one per minor (nations 7-22) in every
    # generated world and absent from the historical maps, which give all 23
    # countries a type-35 capital instead.
    33: "minor_capital",
    34: "village",
    35: "capital",
}

# Resources that may sit on an ocean cell. Fish is the only one the shipped
# maps use there, on 3,848 cells across the tutorial scenarios.
OCEAN_RESOURCES = frozenset({19, 255})  # fish, none

# Six-bit directional overlay used for rail/borders/coastline/adjacency bytes.
DIRECTIONS = ["NE", "E", "SE", "SW", "W", "NW"]

#: Country ids below this are the playable Great Powers; 7-22 are minors.
#:
#: A format-level fact, not a design choice: the engine keeps its powers in a
#: **7-slot** table at `006A4370` and indexes it with a country id in several
#: places without a range check, so a minor's id reads past the end. Only ids
#: 0-6 receive `cash`/`tran`/`labo`/`tclr` records in any shipped scenario.
GREAT_POWERS = 7

COUNTRIES_1882 = {
    0: "France", 1: "Austria", 2: "Ottoman", 3: "Russia", 4: "Germany",
    5: "Italy", 6: "Britain", 7: "Portugal", 8: "Spain", 9: "Catalonia",
    10: "Morocco", 11: "North Africa", 12: "Bulgaria", 13: "Romania",
    14: "Egypt", 15: "Albania", 16: "Switzerland", 17: "Netherlands",
    18: "Libya", 19: "Denmark", 20: "Sweden", 21: "Serbia", 22: "Greece",
}

ARMY_UNIT_TYPE = {
    0: "Minutemen", 1: "Skirmishers", 2: "Regulars", 3: "Grenadiers",
    4: "Hussars", 5: "Cuirassers", 6: "Light Artillery", 7: "Artillery",
    8: "Militia", 9: "Sharpshooters", 10: "Rifle Infantry", 11: "Guards",
    12: "Scouts", 13: "Carbine Cavalry", 14: "Field Artillery",
    15: "Siege Artillery", 16: "Conscripts", 17: "Rangers",
    18: "Modern Infantry", 19: "Machinegunners", 20: "Mechanized Infantry",
    21: "Armor", 22: "Mobile Artillery", 23: "Railroad Gun", 24: "Sapper",
    25: "Combat Engineer", 26: "Saboteurs", 27: "General", 28: "General",
    29: "General",
}

CIVILIAN_UNIT_TYPE = {
    0: "Miner", 1: "Prospector", 2: "Farmer", 3: "Forester",
    4: "Engineer", 5: "Rancher", 6: "Fisherman", 7: "Developer",
    8: "Oil Driller",
}

SHIP_TYPE = {
    1: "Trader", 2: "Indiaman", 3: "Frigate", 4: "Ship-of-the-Line",
    5: "Paddlewheeler", 6: "Clipper", 7: "Raider", 8: "Ironclad",
    9: "Advanced Ironclad", 10: "Freighter", 11: "Armored Cruiser",
    12: "Dreadnaught", 13: "Battlecruiser",
}

COMMODITY = {
    0: "cotton", 1: "wool", 2: "timber", 3: "coal", 4: "iron",
    5: "horses", 6: "oil", 7: "food", 8: "fabric", 9: "lumber",
    10: "paper", 11: "steel", 12: "fuel", 13: "clothing",
    14: "furniture", 15: "hardware", 16: "armaments", 17: "grain",
    18: "fruit", 19: "fish", 20: "meat",
}

INDUSTRY_TYPE = {
    0: "Textile Mill", 1: "Clothing Factory", 2: "Steel Mill",
    3: "Metal Works", 4: "Lumber Mill", 5: "Furniture Factory",
    6: "Oil Refinery",
}
