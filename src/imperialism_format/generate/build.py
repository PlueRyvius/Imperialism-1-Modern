"""Assembling a generated world into a `.map`.

Two things here are worth knowing.

**Derived bytes are not generated.** Coastlines, borders and adjacency are
recomputed by `derive`, which is already verified against all ten shipped maps.
Writing them here would be a second implementation of rules that were expensive
to establish.

**The province table is inherited, not fabricated.** Its town-cell field is
decoded and gets rewritten, but the other 196 bytes of each record are not, and
a table full of zeroes is nothing any shipped map resembles. Passing a real map
as `template` copies its table and rewrites only the field we understand —
which is how the rest of this project treats bytes it cannot read.
"""
from __future__ import annotations

import random

from .. import derive
from ..map_file import LEGACY_MAP_PROFILE, HexCell, MapFile, MapFormatProfile
from . import politics, rivers, scenario as scenario_mod, world


def build_map(rng, cells: dict, owner: dict, provinces: dict, towns: dict,
              profile: MapFormatProfile = LEGACY_MAP_PROFILE,
              template: MapFile = None, wrap_x: bool = True,
              river: dict = None) -> MapFile:
    """Turn a generated world into a map, derived bytes and all."""
    result = MapFile.blank(profile)
    if template is not None:
        _inherit_table(result, template)

    for (x, y), plan in cells.items():
        cell = result.get(x, y)
        cell.terrain = plan["terrain"]
        cell.terrain_underlay = plan["underlay"]
        cell.resource_a = plan["resource"]
        cell.resource_b = 255

        if cell.terrain == world.OCEAN:
            cell.province = politics.NO_PROVINCE
            cell.nation_zone_a = cell.nation_zone_b = 0
            continue

        cell.province = provinces[(x, y)]
        country = owner[(x, y)]
        cell.nation_zone_a = cell.nation_zone_b = country
        marker = towns.get((x, y))
        if marker:
            # A town sits on town terrain; the "capital" terrain code is never
            # used by any shipped map, the marker carries that meaning.
            cell.terrain = politics.TOWN_TERRAIN
            cell.terrain_underlay = world.UNDERLAY_FOR[politics.TOWN_TERRAIN]
            cell.resource_a = 255
            cell.town_type = marker

    # Byte 2, written straight from the carved course. A mouth lands on an
    # ocean cell, so this runs over every cell rather than only the land.
    for cell, value in (river or {}).items():
        result.get(*cell).river = value

    _number_sea_zones(rng, result, wrap_x=wrap_x)
    _write_province_table(result, provinces, towns)

    geom = derive.HexGeometry(result.width, result.height, wrap_x=wrap_x)
    derive.apply_edits(result,
                       [(x, y) for y in range(result.height)
                        for x in range(result.width)],
                       geom=geom)
    return result


def _inherit_table(result: MapFile, template: MapFile) -> None:
    if len(template.dormant_trailer) != len(result.dormant_trailer):
        raise ValueError("template map has a different province table size")
    result.dormant_trailer = template.dormant_trailer


def _write_province_table(result: MapFile, provinces: dict, towns: dict) -> None:
    """Point each province's slot at its town, and empty the unused slots."""
    town_of = {}
    for (x, y) in towns:
        town_of[provinces[(x, y)]] = y * result.width + x
    for slot in range(result.profile.trailer_record_count):
        result.set_province_town(slot, town_of.get(slot))


#: The ocean is carved into regions of roughly this many cells. Measured:
#: `s1` averages 78 across 42 zones, `s9` 74 across 61.
SEA_ZONE_CELLS = 76

#: Ocean ids observed in the shipped maps: 23 up to 83.
FIRST_SEA_ZONE, LAST_SEA_ZONE = 23, 83


def _number_sea_zones(rng, result: MapFile, wrap_x: bool = True) -> None:
    """Carve the ocean into zones the way the shipped maps do.

    **Not one id per connected sea.** The ocean is a single connected body in
    every shipped map, so that gives one zone covering everything — which is
    what crashed the game in `UOcean.cpp`, handed a single region of 4,565
    cells where it expects dozens. The real maps partition the water into
    42-61 regions of 17-48 cells regardless of connectivity, so this grows
    them the same way provinces are grown.
    """
    geom = derive.HexGeometry(result.width, result.height, wrap_x=wrap_x)
    sea = {(x, y) for y in range(result.height) for x in range(result.width)
           if result.get(x, y).terrain == world.OCEAN}
    if not sea:
        return

    count = max(1, min(round(len(sea) / SEA_ZONE_CELLS),
                       LAST_SEA_ZONE - FIRST_SEA_ZONE + 1))
    targets = {index: len(sea) // count for index in range(count)}
    zones, stranded = politics._grow_regions(rng, geom, sea, targets)
    for cell in stranded:
        zones.setdefault(cell, 0)

    for (x, y), index in zones.items():
        cell = result.get(x, y)
        cell.nation_zone_a = cell.nation_zone_b = FIRST_SEA_ZONE + index


def generate_world(keyword: str, profile: MapFormatProfile = LEGACY_MAP_PROFILE,
                   land_share: float = 0.305, template: MapFile = None,
                   plan: list = None, locked: dict = None,
                   wrap_x: bool = True, turns: int = 5) -> dict:
    """Generate a whole world from a keyword.

    Returns the map together with the political layers, which the scenario pass
    needs in order to name things.
    """
    rng_ = random.Random(_seed(keyword))
    geom = derive.HexGeometry(profile.width, profile.height, wrap_x=wrap_x)

    cells = world.generate_geography(rng_, profile.width, profile.height,
                                     land_share=land_share, wrap_x=wrap_x)
    land = {cell for cell, plan_ in cells.items()
            if plan_["terrain"] != world.OCEAN}

    owner, sunk = politics.assign_countries(rng_, geom, land, plan, locked)
    provinces, stranded = politics.assign_provinces(rng_, geom, owner, plan)

    # Land with no owner or no province is a null the game cannot read, so
    # anything that could not be placed within the province cap goes back to
    # sea. Sinking happens from the coast inward so the result is a smaller
    # continent rather than one with lakes punched through it.
    _sink(geom, _sink_order(geom, sunk | stranded, owner), cells, owner, provinces)

    # Removing coastal cells can cut a country in two — the invariant every
    # shipped world holds. Sink the smaller half rather than ship a nation in
    # pieces.
    _sink(geom, _split_fragments(geom, owner), cells, owner, provinces)
    _drain_lakes(geom, cells, owner, provinces)
    provinces = politics.restore_plan(rng_, geom, owner, provinces, plan)

    # Rivers are cut before the towns are sited, because a river is water
    # access: every capital in every shipped scenario reaches the sea, and for
    # a country with no coast of its own a river is the only way it can.
    terrain = {cell: plan_["terrain"] for cell, plan_ in cells.items()}
    river = rivers.carve(rng_, geom, terrain)

    towns = politics.place_towns(rng_, geom, owner, provinces, plan,
                                 water=set(river))

    map_file = build_map(rng_, cells, owner, provinces, towns,
                         profile, template, wrap_x, river=river)
    parts = scenario_mod.build_scenario(rng_, map_file, owner, provinces, towns,
                                        turns=turns, plan=plan)
    return {
        "map": map_file,
        "scenario": parts["scenario"],
        "info": parts["info"],
        "names": parts["names"],
        "owner": owner,
        "provinces": provinces,
        "towns": towns,
        "keyword": keyword,
    }


def _sink_order(geom, cells: set, owner: dict) -> list:
    """Cells to return to the sea, outermost first.

    Sinking an interior cell would leave a lake, and the shipped maps have
    none — every ocean cell reaches the open sea. Taking the cell with the
    fewest land neighbours each time eats inward from the coast instead.
    """
    remaining = set(cells)
    land = set(owner) | remaining
    order = []
    while remaining:
        chosen = min(remaining, key=lambda c: (
            sum(1 for n in geom.neighbours(*c) if n in land), c))
        order.append(chosen)
        remaining.discard(chosen)
        land.discard(chosen)
    return order


def _sink(geom, order, cells: dict, owner: dict, provinces: dict) -> None:
    for cell in order:
        cells[cell] = {"terrain": world.OCEAN,
                       "underlay": world.OCEAN_UNDERLAY,
                       "resource": world.NO_RESOURCE}
        owner.pop(cell, None)
        provinces.pop(cell, None)


def _components(geom, cells: set) -> list:
    seen, out = set(), []
    for start in sorted(cells):
        if start in seen:
            continue
        piece, stack = {start}, [start]
        seen.add(start)
        while stack:
            current = stack.pop()
            for neighbour in geom.neighbours(*current):
                if neighbour in cells and neighbour not in seen:
                    seen.add(neighbour)
                    piece.add(neighbour)
                    stack.append(neighbour)
        out.append(piece)
    return out


def _split_fragments(geom, owner: dict) -> list:
    """Every piece of a broken country except its largest."""
    by_country: dict = {}
    for cell, country in owner.items():
        by_country.setdefault(country, set()).add(cell)

    doomed = []
    for cells in by_country.values():
        pieces = _components(geom, cells)
        if len(pieces) > 1:
            pieces.sort(key=len, reverse=True)
            for fragment in pieces[1:]:
                doomed.extend(sorted(fragment))
    return doomed


def _drain_lakes(geom, cells: dict, owner: dict, provinces: dict) -> None:
    """Turn enclosed water back into land, joining a province with room.

    Every ocean cell in the shipped maps reaches the open sea. Sinking surplus
    land can seal off a pocket; giving it back to a neighbouring province is
    closer to the originals than leaving an inland sea nothing can sail to.
    """
    height = geom.height
    ocean = {cell for cell, plan in cells.items() if plan["terrain"] == world.OCEAN}
    open_sea = set()
    stack = [cell for cell in ocean if cell[1] in (0, height - 1)]
    open_sea.update(stack)
    while stack:
        current = stack.pop()
        for neighbour in geom.neighbours(*current):
            if neighbour in ocean and neighbour not in open_sea:
                open_sea.add(neighbour)
                stack.append(neighbour)

    held: dict = {}
    for province in provinces.values():
        held[province] = held.get(province, 0) + 1

    for cell in sorted(ocean - open_sea):
        options = [provinces[n] for n in geom.neighbours(*cell)
                   if n in provinces
                   and held.get(provinces[n], 0) < politics.MAX_PROVINCE_CELLS]
        if not options:
            continue                       # nowhere with room; leave the lake
        province = min(set(options), key=lambda p: (held.get(p, 0), p))
        country = next(owner[n] for n in geom.neighbours(*cell)
                       if n in provinces and provinces[n] == province)
        neighbour_plan = next(cells[n] for n in geom.neighbours(*cell)
                              if n in provinces and provinces[n] == province)
        cells[cell] = dict(neighbour_plan)
        owner[cell] = country
        provinces[cell] = province
        held[province] = held.get(province, 0) + 1


def _seed(keyword: str) -> int:
    from .rng import seed_from
    return seed_from(keyword)


def save_world(result: dict, stem: str) -> list:
    """Write a generated world as a complete scenario: `.map`, `.scn`, `.inf`.

    All three, always. A scenario missing one of its files is not something the
    game can open, so a partial write would be worse than none.
    """
    import os

    stem = os.path.splitext(stem)[0]
    result["map"].save(stem + ".map")
    result["scenario"].save(stem + ".scn")
    result["info"].save(stem + ".inf")
    return [stem + ".map", stem + ".scn", stem + ".inf"]
