"""Geography: where the land is, and what grows on it.

Every number here was measured from the five generated worlds the game itself
shipped — the tutorial scenarios `s9`-`s12` and `s15` — rather than invented.
Pooling all five gives a steadier picture than any single map.

The climate is latitude-banded and roughly Earth-like: tundra at both poles,
temperate forest and farmland in the mid latitudes, **two desert belts** either
side of an equatorial forest band. Land is scarce at the poles (1-2%) and peaks
around 43% in the northern temperate belt.
"""
from __future__ import annotations

import random
from collections import deque

from ..constants import DEVELOPED_TERRAIN_RESOURCE
from ..derive import HexGeometry
from .rng import weighted_choice

OCEAN = 0
OCEAN_UNDERLAY = 5
FISH = 19
NO_RESOURCE = 255

#: Fraction of each latitude band that is land, measured from the shipped
#: generated worlds. Keyed by the band's start as a fraction of map height so
#: the model does not assume a 60-row map.
LAND_BY_BAND = [
    (0.00, 0.02), (0.07, 0.26), (0.13, 0.43), (0.20, 0.43), (0.27, 0.34),
    (0.33, 0.30), (0.40, 0.36), (0.47, 0.37), (0.53, 0.33), (0.60, 0.30),
    (0.67, 0.37), (0.73, 0.39), (0.80, 0.35), (0.87, 0.25), (0.93, 0.01),
]

#: Terrain mix per band, as measured. Terrain ids are from `TERRAIN_TYPE`.
#: Towns are absent: they are placed later, with the provinces they belong to.
_POLAR = {12: 88, 8: 7, 1: 5}
_SUBPOLAR = {12: 40, 5: 12, 8: 8, 6: 7, 13: 10, 1: 8, 9: 8, 10: 7}
_TEMPERATE = {13: 20, 5: 16, 10: 12, 8: 12, 1: 10, 6: 8, 9: 8, 3: 8, 7: 3, 2: 3}
_WARM = {5: 17, 1: 12, 6: 12, 13: 11, 8: 10, 3: 10, 9: 8, 11: 10, 2: 6, 4: 4}
_DESERT = {11: 40, 5: 12, 8: 11, 1: 6, 9: 8, 3: 7, 13: 8, 6: 5, 10: 3}
_EQUATOR = {13: 19, 5: 18, 8: 12, 1: 11, 10: 10, 6: 9, 9: 9, 3: 7, 2: 5}

TERRAIN_BY_BAND = [
    (0.00, _POLAR), (0.07, _SUBPOLAR), (0.13, _TEMPERATE), (0.20, _TEMPERATE),
    (0.27, _WARM), (0.33, _DESERT), (0.40, _DESERT), (0.47, _EQUATOR),
    (0.53, _WARM), (0.60, _DESERT), (0.67, _TEMPERATE), (0.73, _TEMPERATE),
    (0.80, _TEMPERATE), (0.87, _SUBPOLAR), (0.93, _POLAR),
]

#: Chance a cell of each terrain carries a mineral, measured from the same
#: worlds. Developed terrains are absent — they always carry their own
#: resource, per `DEVELOPED_TERRAIN_RESOURCE`.
MINERALS = {
    8: {3: 0.11, 4: 0.10},                    # hill: coal, iron
    9: {4: 0.12, 22: 0.10, 3: 0.10, 21: 0.02},  # mountain: iron, gold, coal, gems
    11: {6: 0.14},                            # desert: oil
    10: {6: 0.17},                            # swamp: oil
    12: {6: 0.15},                            # tundra: oil
}

#: Landmass sizes as a share of all land, from `s9`: two large continents, two
#: medium, five small. Generating to a shape rather than scattering blobs is
#: what stops the map becoming an archipelago.
LANDMASS_SHARES = [0.265, 0.262, 0.138, 0.128, 0.069, 0.037, 0.034, 0.034, 0.033]

#: Share of ocean cells carrying fish. The historical maps have none; every
#: generated one does.
FISH_SHARE = 0.166


def _band(table, y: int, height: int):
    """The band covering row `y`, by fraction of map height."""
    position = y / max(1, height - 1)
    chosen = table[0][1]
    for start, value in table:
        if position >= start:
            chosen = value
        else:
            break
    return chosen


def land_fraction(y: int, height: int) -> float:
    return _band(LAND_BY_BAND, y, height)


def terrain_weights(y: int, height: int) -> dict:
    return _band(TERRAIN_BY_BAND, y, height)


#: Columns of open sea held at the map's east and west edges.
#:
#: The grid wraps east-west, but the game's own worlds do not use the wrap for
#: land: `s9`-`s12` and `s15` have **no land at all** in either edge column, and
#: the historical maps have none in the west one. Growing a continent across
#: the seam splits a country between the two sides of the screen with no way to
#: walk between them, which is what a real launch showed.
EDGE_SEA_MARGIN = 1


def in_bounds(geom: HexGeometry, cell) -> bool:
    """Whether land may occupy this cell, given the edge margin."""
    return EDGE_SEA_MARGIN <= cell[0] < geom.width - EDGE_SEA_MARGIN


def row_targets(geom: HexGeometry, land_target: int) -> list:
    """How many land cells each row should end up with.

    Latitude has to be a **budget**, not a per-cell coin flip. Growing a
    continent by accepting cells with the probability that their row is land
    lets whichever rows the nuclei happened to land in fill up solid, and the
    finished map bears no resemblance to the measured bands. Deciding the
    per-row counts up front and spending them is what makes the histogram come
    out right.
    """
    raw = [land_fraction(y, geom.height) * geom.width for y in range(geom.height)]
    scale = land_target / max(1e-9, sum(raw))
    return [int(round(value * scale)) for value in raw]


def grow_landmasses(rng: random.Random, geom: HexGeometry,
                    land_target: int) -> set:
    """Grow separate continents, spending each row's land budget.

    Masses are kept apart by refusing any cell that touches a *different* mass,
    so they stay the nine distinct landmasses the shipped worlds have instead
    of merging into one supercontinent.
    """
    budget = row_targets(geom, land_target)
    owner: dict = {}

    def free(cell, mass_id):
        if cell in owner or budget[cell[1]] <= 0:
            return False
        if not in_bounds(geom, cell):
            return False
        return all(owner.get(n, mass_id) == mass_id
                   for n in geom.neighbours(*cell) if n is not None)

    for mass_id, share in enumerate(LANDMASS_SHARES):
        target = max(1, round(land_target * share))
        seed = _pick_nucleus(rng, geom, owner, budget)
        if seed is None:
            continue
        owner[seed] = mass_id
        budget[seed[1]] -= 1
        frontier = [seed]
        grown = 1

        while frontier and grown < target:
            # Take from a random point in the frontier rather than the end:
            # depth-first growth produces tendrils, breadth-first produces
            # discs, and this lands between the two.
            index = rng.randrange(len(frontier))
            frontier[index], frontier[-1] = frontier[-1], frontier[index]
            x, y = frontier.pop()

            neighbours = [n for n in geom.neighbours(x, y) if n is not None]
            rng.shuffle(neighbours)
            spread = False
            for cell in neighbours:
                if grown >= target or not free(cell, mass_id):
                    continue
                owner[cell] = mass_id
                budget[cell[1]] -= 1
                frontier.append(cell)
                grown += 1
                spread = True
            if spread:
                frontier.append((x, y))     # still has room to grow later

    _spend_remaining(rng, geom, owner, budget, free)
    return set(owner)


def _spend_remaining(rng: random.Random, geom: HexGeometry, owner: dict,
                     budget: list, free) -> None:
    """Grow the existing masses into whatever budget is left over.

    The large continents are placed first and spend the rows they land in, so
    without this the far side of the map keeps an unspendable budget and comes
    out visibly emptier than the shipped worlds. Growing what is already there,
    rather than adding masses, keeps the landmass count as measured.
    """
    for _ in range(64):
        if not any(budget):
            return
        edge = [cell for cell in owner
                if any(n is not None and n not in owner and budget[n[1]] > 0
                       for n in geom.neighbours(*cell))]
        if not edge:
            return
        rng.shuffle(edge)
        progressed = False
        for cell in edge:
            mass_id = owner[cell]
            for neighbour in geom.neighbours(*cell):
                if neighbour is None or not free(neighbour, mass_id):
                    continue
                owner[neighbour] = mass_id
                budget[neighbour[1]] -= 1
                progressed = True
        if not progressed:
            return


def _pick_nucleus(rng: random.Random, geom: HexGeometry, owner: dict,
                  budget: list):
    """A starting cell in a row with budget left, clear of existing masses."""
    rows = [y for y in range(geom.height) if budget[y] > 0]
    if not rows:
        return None
    weights = {y: budget[y] for y in rows}
    for _ in range(600):
        y = weighted_choice(rng, weights)
        x = rng.randrange(EDGE_SEA_MARGIN, geom.width - EDGE_SEA_MARGIN)
        cell = (x, y)
        if cell in owner:
            continue
        # Keep nuclei apart, or two masses start adjacent and one is stillborn.
        if any(n in owner for n in geom.neighbours(x, y) if n is not None):
            continue
        return cell
    return None


def smooth(rng: random.Random, geom: HexGeometry, terrain: dict,
           passes: int = 2, strength: float = 0.55) -> None:
    """Pull each cell toward its neighbours' majority terrain.

    Drawing every cell independently from its band gives confetti. A couple of
    majority passes turn that into forests and deserts you could point at,
    without moving any cell across a band boundary.
    """
    for _ in range(passes):
        for (x, y) in sorted(terrain):
            if rng.random() > strength:
                continue
            counts: dict = {}
            for neighbour in geom.neighbours(x, y):
                if neighbour in terrain:
                    counts[terrain[neighbour]] = counts.get(terrain[neighbour], 0) + 1
            if counts:
                terrain[(x, y)] = max(counts, key=counts.get)


def generate_geography(rng: random.Random, width: int, height: int,
                       land_share: float = 0.305, wrap_x: bool = True) -> dict:
    """Land, terrain and resources for a whole map.

    Returns ``{(x, y): {"terrain", "underlay", "resource"}}`` for every cell,
    leaving provinces, ownership and towns to the political pass.
    """
    geom = HexGeometry(width, height, wrap_x=wrap_x)
    land = grow_landmasses(rng, geom, round(width * height * land_share))

    terrain = {cell: weighted_choice(rng, terrain_weights(cell[1], height))
               for cell in land}
    smooth(rng, geom, terrain)

    cells = {}
    for y in range(height):
        for x in range(width):
            if (x, y) in terrain:
                kind = terrain[(x, y)]
                cells[(x, y)] = {
                    "terrain": kind,
                    "underlay": UNDERLAY_FOR.get(kind, 0),
                    "resource": _resource_for(rng, kind),
                }
            else:
                cells[(x, y)] = {
                    "terrain": OCEAN,
                    "underlay": OCEAN_UNDERLAY,
                    "resource": FISH if rng.random() < FISH_SHARE else NO_RESOURCE,
                }
    return cells


def _resource_for(rng: random.Random, kind: int) -> int:
    """Developed land always carries its own resource; wild land may hide one."""
    if kind in DEVELOPED_TERRAIN_RESOURCE:
        return DEVELOPED_TERRAIN_RESOURCE[kind]
    roll = rng.random()
    for resource, chance in MINERALS.get(kind, {}).items():
        if roll < chance:
            return resource
        roll -= chance
    return NO_RESOURCE


#: The base each terrain sits on. Mirrors the table the editor's paint tools
#: use; wrong here and the original renders the cell oddly.
UNDERLAY_FOR = {
    0: 5, 1: 0, 2: 7, 3: 0, 4: 0, 5: 7, 6: 7, 7: 2, 8: 2,
    9: 3, 10: 4, 11: 6, 12: 6, 13: 1, 14: 0, 15: 1, 16: 0,
}
