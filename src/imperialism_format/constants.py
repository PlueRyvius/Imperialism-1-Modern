"""Lookup tables for the Imperialism .map / .scn binary formats.

Derived from community reverse-engineering (byte offsets and value meanings),
re-expressed here in our own structures rather than copied from any single
source document.
"""

MAP_WIDTH = 108
MAP_HEIGHT = 60
MAP_CELL_COUNT = MAP_WIDTH * MAP_HEIGHT
MAP_CELL_SIZE = 36
DORMANT_RECORD_COUNT = 384
DORMANT_RECORD_SIZE = 198

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

TOWN_TYPE = {
    0: "none",
    34: "village",
    35: "capital",
}

# Six-bit directional overlay used for rail/borders/coastline/adjacency bytes.
DIRECTIONS = ["NE", "E", "SE", "SW", "W", "NW"]

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
