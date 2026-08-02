"""Countries, provinces and towns.

The shipped generated worlds are strikingly regular, and all five agree
exactly: **nations 0-6 are great powers with 8 provinces each, nations 7-22 are
minors with 4**, 120 provinces in all, every one between 10 and 20 cells. Every
province is a single connected blob, and so is every country.

Every province holds exactly one town. A great power's capital carries
`town_type` 35, a minor's carries 33, and everything else is a village at 34.
The terrain code for "capital" is never used by any shipped map.

Countries cannot straddle the sea, so the work is done per landmass: countries
are packed into masses by size, grown from spread seeds inside each, and then
each country is cut into provinces the same way.
"""
from __future__ import annotations

import random
from collections import deque

from ..constants import GREAT_POWERS  # noqa: F401  -- re-exported for callers
from ..derive import HexGeometry

OCEAN = 0
NO_PROVINCE = 65535
TOWN_TERRAIN = 14
VILLAGE, MINOR_CAPITAL, CAPITAL = 34, 33, 35

MINORS = 16
GP_PROVINCES = 8
MINOR_PROVINCES = 4

#: Hard ceiling on a province, in cells. The shipped generated worlds never
#: exceed 20 and province size drives the economy, so this is enforced rather
#: than aimed at: a province of 30 is a materially different game.
#:
#: It constrains a country too. A nation cannot hold more land than its
#: provinces can legally contain, so `plan[country] * MAX_PROVINCE_CELLS` is
#: also its cap, and land beyond that is returned to the sea.
MAX_PROVINCE_CELLS = 20


def default_plan() -> list:
    """Provinces per country, in country-id order, as the game generates them."""
    return ([GP_PROVINCES] * GREAT_POWERS) + ([MINOR_PROVINCES] * MINORS)


def landmasses(geom: HexGeometry, land: set) -> list:
    """Connected landmasses, largest first."""
    seen, masses = set(), []
    for start in sorted(land):
        if start in seen:
            continue
        mass, stack = {start}, [start]
        seen.add(start)
        while stack:
            current = stack.pop()
            for neighbour in geom.neighbours(*current):
                if neighbour in land and neighbour not in seen:
                    seen.add(neighbour)
                    mass.add(neighbour)
                    stack.append(neighbour)
        masses.append(mass)
    return sorted(masses, key=len, reverse=True)


def _pack(masses: list, plan: list) -> tuple:
    """Which countries go on which landmass, measured in **cells**.

    A country cannot span the sea, so this has to happen before any growing.
    The unit matters: packing by province *count* against a mass's *cell* count
    makes every island look big enough for everyone, and eighteen nations end
    up sharing one sandbar.

    Returns (assignment, unhostable) — masses too small for even the smallest
    country come back so the caller can sink them. Land nobody owns would be a
    province-less cell, which the game reads as null.
    """
    total_cells = sum(len(mass) for mass in masses)
    per_province = total_cells / max(1, sum(plan))
    want = [plan[c] * per_province for c in range(len(plan))]

    room = [float(len(mass)) for mass in masses]
    assigned: dict = {index: [] for index in range(len(masses))}

    for country in sorted(range(len(plan)), key=lambda c: -want[c]):
        # Most room left, so the great powers land on the continents.
        chosen = max(range(len(masses)), key=lambda i: room[i])
        assigned[chosen].append(country)
        room[chosen] -= want[country]

    # Every mass wants at least one country; otherwise it is uninhabited land.
    smallest = min(range(len(plan)), key=lambda c: want[c])
    for index in range(len(masses)):
        if assigned[index] or len(masses[index]) < want[smallest] * 0.5:
            continue
        donor = max(assigned, key=lambda i: len(assigned[i]))
        if len(assigned[donor]) > 1:
            assigned[index].append(assigned[donor].pop())

    unhostable = {index for index, countries in assigned.items() if not countries}
    return {i: c for i, c in assigned.items() if c}, unhostable


def _grow_regions(rng: random.Random, geom: HexGeometry, area: set,
                  targets: dict, locked: dict = None,
                  ceiling: int = None) -> tuple:
    """Split `area` into connected regions of the requested sizes.

    Multi-source growth: every region takes a turn claiming one cell adjacent
    to what it already holds. Growing them together rather than one after
    another is what keeps them interlocking instead of leaving the last region
    with whatever scraps the others refused.
    """
    owner: dict = dict(locked or {})
    frontier: dict = {region: deque() for region in targets}
    held = {region: 0 for region in targets}

    for cell, region in owner.items():
        if region in held:
            held[region] += 1
            frontier[region].append(cell)

    unseeded = [region for region in targets if not frontier[region]]
    for seed, region in zip(_spread_seeds(rng, geom, area, owner, len(unseeded)),
                            unseeded):
        owner[seed] = region
        held[region] += 1
        frontier[region].append(seed)

    while True:
        # Let the most starved region move first. Round-robin lets a region
        # that seeded in a corner stay boxed in while its neighbours take the
        # ground it needed, and it ends up a fraction of its size.
        limit = {r: min(targets[r], ceiling) if ceiling else targets[r]
                 for r in targets}
        hungry = sorted((r for r in targets if held[r] < limit[r]),
                        key=lambda r: held[r] / max(1, limit[r]))
        if not hungry:
            break
        progressed = False
        for region in hungry:
            if _claim_one(rng, geom, area, owner, frontier[region], region):
                held[region] += 1
                progressed = True
        if not progressed:
            break

    stranded = _absorb_leftovers(geom, area, owner, ceiling)
    return owner, stranded


def _spread_seeds(rng: random.Random, geom: HexGeometry, area: set,
                  owner: dict, count: int) -> list:
    """Farthest-point sampling: each seed as far as possible from the others.

    Seeding at random puts two capitals in the same valley and leaves the far
    end of the continent for whoever grows there last.
    """
    free = sorted(cell for cell in area if cell not in owner)
    if not free or count <= 0:
        return []

    seeds = [rng.choice(free)]
    distance = _distances(geom, area, {seeds[0]} | set(owner))
    while len(seeds) < count:
        candidate = max(free, key=lambda c: (distance.get(c, -1), c))
        if candidate in seeds:
            break
        seeds.append(candidate)
        nearer = _distances(geom, area, {candidate})
        for cell, value in nearer.items():
            if value < distance.get(cell, 1 << 30):
                distance[cell] = value
    return seeds


def _distances(geom: HexGeometry, area: set, sources: set) -> dict:
    """Breadth-first distance from a set of cells, within the area."""
    distance = {cell: 0 for cell in sources if cell in area}
    queue = deque(distance)
    while queue:
        current = queue.popleft()
        for neighbour in geom.neighbours(*current):
            if neighbour in area and neighbour not in distance:
                distance[neighbour] = distance[current] + 1
                queue.append(neighbour)
    return distance


def _claim_one(rng: random.Random, geom: HexGeometry, area: set, owner: dict,
               frontier: deque, region) -> bool:
    while frontier:
        cell = frontier[0]
        options = [n for n in geom.neighbours(*cell)
                   if n is not None and n in area and n not in owner]
        if not options:
            frontier.popleft()
            continue
        chosen = rng.choice(options)
        owner[chosen] = region
        frontier.append(chosen)
        return True
    return False


def _absorb_leftovers(geom: HexGeometry, area: set, owner: dict,
                      ceiling: int = None) -> set:
    """Give stranded cells to a neighbour that has room, and report the rest.

    Growth can encircle a pocket no region can reach. Absorbing it used to be
    unconditional, which is exactly how provinces grew past their size: every
    orphan landed on whichever region happened to surround it. With a ceiling
    the smallest neighbour under it takes the cell, and anything with no
    willing neighbour comes back for the caller to sink — land with no province
    is a null the game cannot read.
    """
    held: dict = {}
    for region in owner.values():
        held[region] = held.get(region, 0) + 1

    remaining = {cell for cell in area if cell not in owner}
    while remaining:
        progressed = False
        for cell in sorted(remaining):
            options = [owner[n] for n in geom.neighbours(*cell)
                       if n is not None and n in owner
                       and (ceiling is None or held.get(owner[n], 0) < ceiling)]
            if not options:
                continue
            # Smallest neighbour first, so absorption evens regions out rather
            # than piling onto whichever one happens to be adjacent.
            chosen = min(set(options), key=lambda r: (held.get(r, 0), r))
            owner[cell] = chosen
            held[chosen] = held.get(chosen, 0) + 1
            remaining.discard(cell)
            progressed = True
        if not progressed:
            break
    return remaining


def assign_countries(rng: random.Random, geom: HexGeometry, land: set,
                     plan: list = None, locked: dict = None) -> tuple:
    """Give every land cell an owner, keeping each country in one piece.

    Returns (owner, sunk). `sunk` is land on islands too small to host any
    country; the caller turns those cells back into sea, because land with no
    owner would have no province either.
    """
    plan = plan or default_plan()
    locked = dict(locked or {})

    masses = landmasses(geom, land)
    per_mass, unhostable = _pack(masses, plan)

    # A locked country keeps whatever it already holds. Put its mass beyond
    # reach of the packer so a regeneration cannot hand its ground away.
    for cell, country in locked.items():
        for index, mass in enumerate(masses):
            if cell in mass and country not in per_mass.get(index, []):
                per_mass.setdefault(index, []).append(country)
                unhostable.discard(index)

    owner: dict = {}
    overflow: set = set()
    for mass_index, countries in per_mass.items():
        area = masses[mass_index]
        share = sum(plan[c] for c in countries) or 1
        # A country can hold no more than its provinces legally can. Without
        # this the province cap is unsatisfiable further down and the surplus
        # has nowhere to go.
        targets = {c: min(max(1, round(len(area) * plan[c] / share)),
                          plan[c] * MAX_PROVINCE_CELLS)
                   for c in countries}
        mass_locked = {cell: c for cell, c in locked.items()
                       if cell in area and c in targets}
        grown, stranded = _grow_regions(rng, geom, area, targets, mass_locked,
                                        ceiling=None)
        owner.update(grown)
        overflow |= stranded

    owner.update(locked)
    sunk = {cell for index in unhostable for cell in masses[index]} | overflow
    return {cell: c for cell, c in owner.items() if cell not in sunk}, sunk


def assign_provinces(rng: random.Random, geom: HexGeometry, owner: dict,
                     plan: list = None, locked: dict = None) -> tuple:
    """Cut every country into provinces, none larger than the cap.

    Returns (provinces, stranded). A cell only strands when every province
    around it is already full, which the caller sinks rather than letting one
    province run over — province size drives the economy, so the ceiling is
    the point.
    """
    plan = plan or default_plan()
    provinces: dict = {}
    next_id = 0
    by_country: dict = {}
    for cell, country in owner.items():
        by_country.setdefault(country, set()).add(cell)

    stranded: set = set()
    for country in sorted(by_country):
        area = by_country[country]
        wanted = plan[country] if country < len(plan) else 1
        ids = list(range(next_id, next_id + wanted))
        next_id += wanted
        # Ceil rather than round: rounding down leaves a remainder that has to
        # land somewhere, and every somewhere is already at its size.
        share = -(-len(area) // max(1, wanted))
        targets = {pid: min(max(1, share), MAX_PROVINCE_CELLS) for pid in ids}
        held = {cell: pid for cell, pid in (locked or {}).items() if cell in area}
        grown, left = _grow_regions(rng, geom, area, targets, held,
                                    ceiling=MAX_PROVINCE_CELLS)
        provinces.update(grown)
        stranded |= left
    return provinces, stranded


def restore_plan(rng: random.Random, geom: HexGeometry, owner: dict,
                 provinces: dict, plan: list = None) -> dict:
    """Give every country back the province count its rank calls for.

    Sinking surplus land can take a whole province with it, leaving a great
    power with seven. The count is the most visible thing about the political
    layer, so it is repaired here by splitting the country's largest province
    until the tally is right — always possible while the country has at least
    as many cells as provinces.
    """
    plan = plan or default_plan()
    by_country: dict = {}
    for cell, country in owner.items():
        by_country.setdefault(country, {}).setdefault(provinces[cell], set()).add(cell)

    next_id = max(provinces.values(), default=-1) + 1
    for country, held in sorted(by_country.items()):
        wanted = plan[country] if country < len(plan) else 1
        while len(held) < wanted:
            biggest = max(held, key=lambda p: len(held[p]))
            if len(held[biggest]) < 2:
                break                       # nothing left large enough to cut
            halves = _split(rng, geom, held[biggest], next_id)
            if halves is None:
                break
            keep, moved = halves
            held[biggest] = keep
            held[next_id] = moved
            for cell in moved:
                provinces[cell] = next_id
            next_id += 1
    return _renumber(provinces)


def _split(rng: random.Random, geom: HexGeometry, cells: set, new_id: int):
    """Cut one province into two connected halves."""
    seeds = _spread_seeds(rng, geom, cells, {}, 2)
    if len(seeds) < 2:
        return None
    targets = {0: len(cells) // 2, 1: len(cells) - len(cells) // 2}
    owner, stranded = _grow_regions(rng, geom, cells,
                                    targets, {seeds[0]: 0, seeds[1]: 1})
    keep = {c for c, side in owner.items() if side == 0} | stranded
    moved = {c for c, side in owner.items() if side == 1}
    if not keep or not moved:
        return None
    return keep, moved


def _renumber(provinces: dict) -> dict:
    """Compact province ids to 0..n-1, which the trailer table indexes by."""
    order = {old: new for new, old in enumerate(sorted(set(provinces.values())))}
    return {cell: order[old] for cell, old in provinces.items()}


def place_towns(rng: random.Random, geom: HexGeometry, owner: dict,
                provinces: dict, plan: list = None, water: set = None) -> dict:
    """One town per province, marking each country's capital.

    Villages go as far from the province's edge as possible, so one does not
    end up on a one-cell spit of coast. **Capitals are the exception: they must
    reach the sea.**

    Every capital in every shipped scenario -- 184 of them, Great Power and
    minor alike, across all eight files -- sits either on the coast or on a
    river. Not one is landlocked. That is what gives a nation its port: the
    dock grows from the capital and follows the water out to open sea, so a
    capital with no water access has nowhere for ships to anchor and the
    country is cut out of naval trade entirely.

    Placing every town by `_most_interior` guaranteed the opposite, and a real
    launch showed it: all 23 capitals landlocked, no docks anywhere.

    So a capital takes the largest province *that touches the sea*, and sits on
    a coastal cell within it -- preferring one with land around it, which is
    what the original spit-of-coast concern was actually about. A country with
    no coastline at all falls back to the old behaviour; see the note on rivers
    in `docs/handoff.md`.
    """
    plan = plan or default_plan()
    by_province: dict = {}
    country_of: dict = {}
    for cell, province in provinces.items():
        by_province.setdefault(province, set()).add(cell)
        country_of[province] = owner[cell]

    land = set(owner)
    water = water or set()

    def coastal(cell) -> bool:
        """Water access: the open sea next door, or a river running through.

        A river counts because the shipped worlds use it that way — nine or ten
        of every generated world's capitals are coastal and the rest sit on a
        river, which is how a country with no coast of its own still gets a
        port.
        """
        if cell in water:
            return True
        return any(n is not None and n not in land
                   for n in geom.neighbours(*cell))

    # The capital goes in each country's largest province, so a great power's
    # seat of government is not a four-cell corner of its own territory --
    # but a coastal province beats a bigger inland one, because a landlocked
    # capital costs the country its port.
    capital_province: dict = {}
    for province, cells in sorted(by_province.items()):
        country = country_of[province]
        best = capital_province.get(country)
        if best is None:
            capital_province[country] = province
            continue
        rank = (any(coastal(c) for c in cells), len(cells))
        best_rank = (any(coastal(c) for c in by_province[best]),
                     len(by_province[best]))
        if rank > best_rank:
            capital_province[country] = province

    towns = {}
    for province, cells in sorted(by_province.items()):
        country = country_of[province]
        if province != capital_province[country]:
            towns[_most_interior(geom, cells)] = VILLAGE
            continue
        marker = CAPITAL if country < GREAT_POWERS else MINOR_CAPITAL
        towns[_capital_seat(geom, cells, coastal)] = marker
    return towns


def _capital_seat(geom: HexGeometry, cells: set, coastal):
    """A coastal cell in this province, as sheltered as one can be.

    Among the province's coastal cells, take the one with the most land
    neighbours: a harbour set into the coast rather than the tip of a spit.
    Falls back to the interior rule when the province has no coast at all.
    """
    shore = [c for c in sorted(cells) if coastal(c)]
    if not shore:
        return _most_interior(geom, cells)
    return max(shore, key=lambda c: (
        sum(1 for n in geom.neighbours(*c) if n in cells), -c[0], -c[1]))


def _most_interior(geom: HexGeometry, cells: set):
    """The cell furthest from the region's edge, by breadth-first distance."""
    edge = [c for c in cells
            if any(n not in cells for n in geom.neighbours(*c))]
    if not edge:
        return min(cells)
    depth = {cell: 0 for cell in edge}
    queue = deque(edge)
    furthest = edge[0]
    while queue:
        current = queue.popleft()
        for neighbour in geom.neighbours(*current):
            if neighbour in cells and neighbour not in depth:
                depth[neighbour] = depth[current] + 1
                if depth[neighbour] > depth[furthest]:
                    furthest = neighbour
                queue.append(neighbour)
    return furthest
