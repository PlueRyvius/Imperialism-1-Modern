"""The `.scn` and `.inf` that make a generated world playable.

Modelled on `s1`, a complete campaign, rather than on the shipped generated
worlds: those are tutorials and have been stripped down to one or two named
countries, so they show what the generator *placed* but not what a full
scenario *contains*.

The exception is `army`, which describes what the generator *placed* and so is
taken from `s11` and `s15` directly -- see the comment on that block.

Record shapes are the ones established in `docs/scenario-semantics.md`. Two
choices worth stating:

**No `ship` records.** A fleet names a `zone` record id, and the map numbers its
oceans in an unrelated space, so a generated fleet would be placed by guesswork.
A world with no navies beats one with fleets in the wrong sea.

**`year` sets `flag`.** The two track together across every shipped scenario —
1816 to 1826 is flag 0, 1820 flag 1, 1848 flag 2, 1882 flag 3 — so the era is
derived rather than asked for.
"""
from __future__ import annotations

import random

from .. import derive
from ..constants import ARMY_UNIT_TYPE
from ..inf_file import ScenarioInfo
from ..scn_file import ScenarioFile
from . import naming, politics

BASE_YEAR = 1815

#: Which `flag` each era uses, as observed: (minimum turn, flag).
ERA_FLAGS = [(0, 0), (5, 1), (33, 2), (67, 3)]

#: Army types available by era, from the shipped scenarios: 1820 fields
#: Minutemen through Artillery, 1882 Militia through Siege Artillery.
ERA_ARMIES = [(0, range(0, 8)), (33, range(0, 16)), (67, range(8, 16))]

#: Highest tech id a country starts with, by era. `s1` at 1882 gives its powers
#: all 21; earlier scenarios give proportionally fewer.
ERA_TECHS = [(0, 6), (33, 12), (67, 21)]

INDUSTRIES = 6          # `capa` covers six per country in every shipped file

#: How many of the top zone ids are port cities rather than open water: **one
#: per country**, powers included. Verified on `s9`, `s11` and `s15`, whose zone
#: tables each close with exactly 23 -- seven harbour names then sixteen
#: "<country> City". Reading this as 16 (one per minor) is what left the Great
#: Powers without docks.
PORT_ZONES = politics.GREAT_POWERS + politics.MINORS

#: Terrain a `port` record never sits on in any shipped scenario: wool hill,
#: hill, mountain. A dock needs a shore to stand on, not a cliff.
PORT_EXCLUDED_TERRAIN = frozenset({7, 8, 9})
GREAT_POWERS = politics.GREAT_POWERS


def _by_era(table, turns: int):
    chosen = table[0][1]
    for minimum, value in table:
        if turns >= minimum:
            chosen = value
    return chosen


def build_scenario(rng: random.Random, map_file, owner: dict, provinces: dict,
                   towns: dict, turns: int = 5, plan: list = None) -> dict:
    """Everything a generated world needs beside its map.

    Returns the `.scn`, the `.inf`, and the country names, which the caller
    needs for naming the files and reporting what it made.
    """
    plan = plan or politics.default_plan()
    scn = ScenarioFile()

    countries = naming.Pool(rng, naming.COUNTRIES, "Country")
    province_names = naming.Pool(rng, naming.PROVINCES, "Province")

    names = [countries.take() for _ in range(len(plan))]
    capital_province = _capital_provinces(owner, provinces, towns)
    country_of = _country_index(owner, provinces)

    # --- identity ---------------------------------------------------------
    for country, name in enumerate(names):
        scn.add("cnam", country, name=name)

    for province in sorted(set(provinces.values())):
        country = country_of[province]
        label = (naming.capital_name(names[country])
                 if capital_province.get(country) == province
                 else province_names.take())
        scn.add("pnam", province, name=label)

    # The zone table must span every id the map's ocean cells use. In each
    # shipped generated world it runs 0 to the highest ocean byte exactly, and
    # a lookup past the end is what a short table would hand the engine.
    #
    # The table ends with **one port city per country, all 23** -- not one per
    # minor. `s9`/`s11`/`s15` each close with seven ordinary harbour names for
    # the Great Powers followed by sixteen "<country> City" for the minors.
    # Giving only the minors a port left every Great Power, including the
    # played one, with nowhere for ships to anchor: the missing docks.
    highest = max((c.nation_zone_a for c in map_file.cells if c.terrain == 0),
                  default=len(names))
    water = highest + 1 - len(names)
    for zone, name in enumerate(naming.sea_names(rng, water)):
        scn.add("zone", zone, name=name)

    harbours = naming.Pool(rng, naming.PORT_CITIES, "Port")
    for index, country in enumerate(range(len(names))):
        label = (harbours.take() if country < GREAT_POWERS
                 else naming.capital_name(names[country]))
        scn.add("zone", water + index, name=label)

    # --- era --------------------------------------------------------------
    scn.add("year", turns)
    scn.add("flag", _by_era(ERA_FLAGS, turns))

    # --- the powers -------------------------------------------------------
    for country in range(GREAT_POWERS):
        scn.add("cash", country, rng.choice([2500, 4000, 5000, 6000, 10000]))
        scn.add("tran", country, rng.choice([80, 90, 100, 110, 120, 130]))
        scn.add("labo", country, rng.choice([5, 15, 24, 30]),
                rng.choice([5, 10, 15, 18]), rng.choice([0, 5, 24, 28]))
        scn.add("tclr", country)
        for tech in range(1, _by_era(ERA_TECHS, turns) + 1):
            scn.add("tech", country, tech)
        for industry in range(INDUSTRIES):
            scn.add("capa", country, industry, rng.choice([2, 4, 8, 12, 16]))
        for commodity in (3, 4, 7, 17):        # coal, iron, food, grain
            scn.add("ware", country, commodity, rng.choice([0, 5, 10, 20, 40]))

    # --- diplomacy --------------------------------------------------------
    # Every unordered pair gets a standing, as `s1` does (253 = 23 choose 2).
    for first in range(len(names)):
        for second in range(first + 1, len(names)):
            scn.add("rela", first, second, rng.choice([95, 100, 100, 105, 110]))
    for power in range(GREAT_POWERS):
        for minor in range(GREAT_POWERS, len(names)):
            scn.add("emba", power, minor, rng.choice([1, 2]))

    # --- forces and works -------------------------------------------------
    # `army` is not free-form. Measured on `s11` and `s15`, the two shipped
    # generated worlds whose army block is unedited, the game's own generator
    # emits exactly three records per world-shape and nothing else:
    #
    #     every province      base + 0, count 4
    #     every capital       base + 2, count 4 for a minor, 2 for a power
    #     every capital       base + 7, count 1
    #
    # 120 + 23 + 23 = 166 records, which is what both files hold. `base` is the
    # era's first unit type, and the three offsets are the same three roles in
    # either era: 1820 fields Minutemen/Regulars/Artillery, 1882 the same
    # ladder at Militia/Rifle Infantry/Siege Artillery. `s1` agrees where it is
    # not hand-edited -- one type-8 record per province, plus 10s and 15s.
    #
    # This replaces a random draw over the whole era roster. That draw put a
    # type-3 record in a generated world, a value no shipped scenario contains
    # in this field, and left every count uniform over 1-6 where the shipped
    # files use one fixed count per role.
    roster = sorted(t for t in _by_era(ERA_ARMIES, turns) if t in ARMY_UNIT_TYPE)
    base = roster[0]
    for province in sorted(set(provinces.values())):
        scn.add("army", province, base + 0, 4)
    for country, province in sorted(capital_province.items()):
        power = country < GREAT_POWERS
        scn.add("army", province, base + 2, 2 if power else 4)
        scn.add("army", province, base + 7, 1)

    _place_works(rng, scn, map_file, owner, provinces, towns)

    info = _build_info(rng, names, turns)
    return {"scenario": scn, "info": info, "names": names}


def _capital_provinces(owner: dict, provinces: dict, towns: dict) -> dict:
    out = {}
    for cell, marker in towns.items():
        if marker in (politics.CAPITAL, politics.MINOR_CAPITAL):
            out[owner[cell]] = provinces[cell]
    return out


def _country_index(owner: dict, provinces: dict) -> dict:
    """province -> country, built once rather than rescanned per province."""
    out = {}
    for cell, province in provinces.items():
        out.setdefault(province, owner[cell])
    return out


def _place_works(rng: random.Random, scn: ScenarioFile, map_file,
                 owner: dict, provinces: dict, towns: dict) -> None:
    """Ports, rail and developed land, on cells that can hold them.

    Every one of these names a cell, and the validator rejects any that points
    at something other than land with a province — which is also what strands a
    unit and crashes the game.

    **A work may only sit on a Great Power's cell.** Across all eight shipped
    scenarios, every one of the 700-odd `deve`, `rail`, `port` and `civi`
    records lands on a cell whose nation byte is 0-6; not one sits on a minor.
    The engine relies on it: it resolves a work's cell to an owner and indexes
    the 7-slot Great Power table at `006A4370` with it, unguarded, so a work on
    a minor's cell reads off the end of that table and faults at `0051465C` in
    `UMap.cpp`. That is the crash this rule was found by. Minor nations have no
    industry of their own in this game, which is why the data looks that way.
    """
    width = map_file.width
    index = lambda cell: cell[1] * width + cell[0]
    held_by_power = lambda cell: cell in owner and owner[cell] < GREAT_POWERS
    geometry = derive.geometry_for(map_file)

    # A `port` must stand on water. Every one of the 124 in the shipped
    # scenarios is on a cell that is either coastal or carries a river -- no
    # exceptions across all nine files -- because a port is where a ship ties
    # up, and it reaches open sea along the coast or down a navigable river.
    # Placing them on town cells regardless put all 21 of a generated world's
    # ports inland.
    def coastal(cell) -> bool:
        """On the sea, or on a river that reaches it — as the shipped ports are.

        `s9` puts 10 of its 13 on the coast and 3 on rivers; `s1` is 22 coastal
        against 27 inland-on-a-river. A harbour up a navigable river is an
        ordinary thing in this game.
        """
        x, y = cell
        if map_file.get(x, y).river:
            return True
        return any(map_file.get(*n).terrain == 0
                   for n in geometry.neighbours(x, y) if n is not None)

    # Neither a `port` nor a `rail` record ever names a capital in any shipped
    # scenario: their cells are town-type 0 or 34, never 33 or 35. The capital
    # already has its dock and its rail head by virtue of being the capital, so
    # a record naming it is a duplicate the originals never write.
    capitals = {cell for cell, marker in towns.items()
                if marker in (politics.CAPITAL, politics.MINOR_CAPITAL)}
    town_cells = [cell for cell in sorted(towns)
                  if held_by_power(cell) and cell not in capitals]
    for cell in town_cells:
        if rng.random() < 0.6:
            scn.add("rail", index(cell))

    # Ports are not a town thing. In the shipped generated worlds 11 of 13 sit
    # on town-type 0 -- ordinary coastal ground with no settlement on it -- and
    # they belong to Great Powers only, 0 to 3 apiece. Restricting candidates to
    # town cells produced none at all, because villages are sited inland by
    # design.
    # Nor on high ground: no shipped port stands on wool hill, hill or
    # mountain, which is the sort of rule that needs no defending once stated.
    by_power: dict = {}
    for cell in sorted(provinces):
        if (held_by_power(cell) and cell not in capitals and coastal(cell)
                and map_file.get(*cell).terrain not in PORT_EXCLUDED_TERRAIN):
            by_power.setdefault(owner[cell], []).append(cell)
    for country in range(GREAT_POWERS):
        shore = by_power.get(country)
        if not shore:
            continue
        for cell in rng.sample(shore, min(len(shore),
                                          rng.choice([0, 0, 1, 1, 2, 3]))):
            scn.add("port", index(cell))

    workable = [cell for cell in sorted(provinces)
                if held_by_power(cell)
                and map_file.get(*cell).terrain in (5, 6, 13, 3, 7, 2)]
    rng.shuffle(workable)
    for cell in workable[:len(workable) // 4]:
        scn.add("deve", index(cell), rng.randint(1, 3))

    # One civilian per great power, on ground it can actually work.
    for country in range(GREAT_POWERS):
        options = [cell for cell in sorted(provinces) if owner[cell] == country]
        if not options:
            continue
        scn.add("civi", rng.randrange(6), index(rng.choice(options)))


def _build_info(rng: random.Random, names: list, turns: int) -> ScenarioInfo:
    """The scenario select screen: title, overview, seven briefings."""
    year = BASE_YEAR + turns
    powers = ", ".join(names[:GREAT_POWERS])
    info = ScenarioInfo(
        title=f"{names[0]} and the World, {year}",
        overview=(
            # Plain ASCII only: the file is written as cp1252 with
            # errors="replace", so anything outside it lands as a question mark.
            f"A world unknown to history. Seven great powers - {powers} - "
            f"contend for trade, territory and the allegiance of sixteen "
            f"minor nations.\n^^Victory is attained by a two-thirds majority "
            f"vote of the Council of Governors, or by holding the greater part "
            f"of the world."),
        country_sections=[
            f"{name}\n^^A great power of the year {year}.\n"
            f"^^Difficulty: {rng.choice(['Introductory', 'Easy', 'Moderate', 'Hard'])}"
            for name in names[:GREAT_POWERS]
        ],
        # Seven playability codes then the default player, per file-formats.md.
        metadata=[rng.choice([1, 2, 3, 4]) for _ in range(GREAT_POWERS)] + [0],
    )
    return info
