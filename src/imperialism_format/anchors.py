"""Where to draw things the scenario places somewhere other than a cell.

`civi`, `deve`, `port` and `rail` name a cell outright. `army` names a
**province** and `ship` names a **sea zone**, neither of which is a position —
so to show them on a map we have to choose one.

An army is drawn on its province's town, which is a real place rather than an
arbitrary middle: all 213 provinces in the shipped maps contain a town marker,
and province ids are shared between the map and the `pnam` records.

A fleet **cannot** be drawn. A `ship` names a `zone` record id, and the map's
ocean cells carry a different numbering entirely: the English Channel is `zone`
record 14 but ocean byte 48, the Black Sea record 15 but byte 23, the North Sea
record 10 but byte 63. No offset relates them and nothing in the files maps one
to the other, so fleets are listed rather than placed.

Also home to the search used when carrying a stranded record back to usable
ground, which is the same neighbour walk the derived bytes use.
"""
from __future__ import annotations

from collections import deque

from . import derive

OCEAN_TERRAIN = 0
NO_PROVINCE = 65535
TOWN_TERRAINS = (14, 16)


def _index(map_file, x: int, y: int) -> int:
    return y * map_file.width + x


def province_anchors(map_file) -> dict:
    """province id -> cell index to draw its garrison on.

    Prefers the province's town, then its capital-ish highest town_type, then
    falls back to the cell nearest the province's centre of mass. The fallback
    matters for edited maps, where a province may have lost its town.
    """
    cells: dict[int, list] = {}
    towns: dict[int, tuple] = {}
    for y in range(map_file.height):
        for x in range(map_file.width):
            cell = map_file.get(x, y)
            if cell.terrain == OCEAN_TERRAIN or cell.province == NO_PROVINCE:
                continue
            cells.setdefault(cell.province, []).append((x, y))
            if cell.terrain in TOWN_TERRAINS or cell.town_type:
                # A capital outranks a village when a province has both.
                rank = (cell.town_type, cell.terrain)
                if cell.province not in towns or rank > towns[cell.province][0]:
                    towns[cell.province] = (rank, (x, y))

    anchors = {}
    for province, members in cells.items():
        if province in towns:
            x, y = towns[province][1]
        else:
            x, y = _centre_of(members, map_file.width)
        anchors[province] = _index(map_file, x, y)
    return anchors


def sea_regions(map_file) -> dict:
    """The map's own ocean-zone byte -> a representative cell for each region.

    **This is not the same numbering as a `zone` record id**, so it cannot be
    used to place a fleet — see `ship_zones_are_not_map_zones` in
    `docs/scenario-semantics.md`. It is here for showing the sea regions
    themselves, which is a map property rather than a scenario one.
    """
    cells: dict[int, list] = {}
    for y in range(map_file.height):
        for x in range(map_file.width):
            cell = map_file.get(x, y)
            if cell.terrain == OCEAN_TERRAIN:
                cells.setdefault(cell.nation_zone_a, []).append((x, y))
    return {zone: _index(map_file, *_centre_of(members, map_file.width))
            for zone, members in cells.items()}


def _centre_of(members: list, width: int) -> tuple:
    """The member cell closest to the group's centre of mass.

    Snapped to a real member, because a region can be concave — the arithmetic
    mean of a horseshoe-shaped sea lies on land.

    The x mean is circular. The map wraps east-west, so a region straddling the
    seam has cells at both x=0 and x=107, and a plain average would drop its
    marker in the middle of the map — the opposite side of the world.
    """
    import math

    angles = [2 * math.pi * x / width for x, _ in members]
    mean_angle = math.atan2(
        sum(math.sin(a) for a in angles) / len(angles),
        sum(math.cos(a) for a in angles) / len(angles),
    )
    mean_x = (mean_angle * width / (2 * math.pi)) % width
    mean_y = sum(y for _, y in members) / len(members)

    def distance(point):
        dx = abs(point[0] - mean_x)
        dx = min(dx, width - dx)          # measure the short way round
        return dx * dx + (point[1] - mean_y) ** 2

    return min(members, key=distance)


def nearest_cell(map_file, x: int, y: int, accepts, *, wrap_x: bool = True,
                 limit: int = 4000):
    """Breadth-first search outward from (x, y) for a cell ``accepts`` allows.

    Uses the same neighbour walk as the derived bytes, so "nearest" means
    nearest across the hex grid — including the east-west wrap — rather than in
    screen space. Returns (x, y) or None.
    """
    geom = derive.HexGeometry(map_file.width, map_file.height, wrap_x=wrap_x)
    start = (x, y)
    seen = {start}
    queue = deque([start])
    while queue and len(seen) <= limit:
        current = queue.popleft()
        if current != start and accepts(map_file.get(*current), current):
            return current
        for neighbour in geom.neighbours(*current):
            if neighbour is not None and neighbour not in seen:
                seen.add(neighbour)
                queue.append(neighbour)
    return None


def carry_target(map_file, x: int, y: int, *, wrap_x: bool = True):
    """Where a record stranded at (x, y) should go.

    Prefers land in the province the cell used to belong to, then land owned by
    the same nation, then any land at all. Each pass is a full search, so a
    unit is never dropped just over a border when its own province was a few
    cells further on.
    """
    was = map_file.get(x, y)

    def land(cell):
        return cell.terrain != OCEAN_TERRAIN and cell.province != NO_PROVINCE

    for accepts in (
        lambda cell, _: land(cell) and cell.province == was.province,
        lambda cell, _: land(cell) and cell.nation_zone_a == was.nation_zone_a,
        lambda cell, _: land(cell),
    ):
        found = nearest_cell(map_file, x, y, accepts, wrap_x=wrap_x)
        if found is not None:
            return found
    return None
