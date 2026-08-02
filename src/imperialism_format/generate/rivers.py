"""Rivers, carved from a mountain down to the sea.

The five worlds the game generated itself each carry **exactly ten rivers** —
ten riverheads, ten river mouths, 90 to 95 river cells, about 5% of the land —
and nine or ten of the ten heads sit on a mountain. `s1`, hand-authored, has 23
and runs to 11%. These follow the generated shape.

## The format

Byte 2 holds one value describing how the river crosses the cell, and the
values split three ways, verified against all nine originals:

- **11-26** a through-flow, on land only
- **43-50** a riverhead — the spring, on land only
- **51-58** a river mouth, **on the ocean cell** the river empties into, never
  on land

So a river's last *land* cell is an ordinary through-flow, and the sea cell
beyond it carries the mouth pointing back at it.

Every value names the one or two hex directions the water crosses, and the pairs
were measured rather than read: for each value, which neighbours also carry a
river. The result is unambiguous — every through-flow value showed exactly two
directions at 100% across six maps.

## The 60-degree rule

The sixteen through-flow values cover only **nine** of the fifteen possible
direction pairs. The six they omit are exactly the six 60-degree turns, and
exactly the "invalid combinations" the old community notes list. So:

**A river runs straight or turns 120 degrees. It can never turn 60 degrees.**

That is the one hard constraint on carving a course, and it is why the walk
below tracks the direction it arrived from.

## High and low

East and west crossings come in two heights, and the pairs `(13,14)`,
`(17,18,19,20)`, `(21,22)`, `(23,24)` are the same topology drawn at different
heights. A height is a property of the *edge*: where a river leaves east at the
high crossing, the cell to its east must take it in at the high crossing too.
Measured as two clean classes, and the values cover all four east-west
combinations, so any pair of heights can be represented.

Two errors in the community notes were caught this way and are corrected here:
49 is a **west** low head, not east, and 55 is a **south-west** mouth, not
south-east.
"""
from __future__ import annotations

import random
from collections import deque

#: Direction indices, matching `derive._NEIGHBOUR_OFFSETS`: NE E SE SW W NW.
NE, E, SE, SW, W, NW = range(6)

HIGH, LOW = "high", "low"

#: Which directions a through-flow value connects, and at what height. Measured:
#: for each value, the neighbours that also carry a river, across six maps.
VALUE_PORTS = {
    11: {(NE, None), (SE, None)},
    12: {(NE, None), (SW, None)},
    13: {(NE, None), (W, HIGH)},
    14: {(NE, None), (W, LOW)},
    15: {(E, HIGH), (SW, None)},
    16: {(E, LOW), (SW, None)},
    17: {(E, HIGH), (W, HIGH)},
    18: {(E, LOW), (W, HIGH)},
    19: {(E, HIGH), (W, LOW)},
    20: {(E, LOW), (W, LOW)},
    21: {(E, HIGH), (NW, None)},
    22: {(E, LOW), (NW, None)},
    23: {(SE, None), (W, HIGH)},
    24: {(SE, None), (W, LOW)},
    25: {(SE, None), (NW, None)},
    26: {(SW, None), (NW, None)},
}

#: Riverhead (spring) by the single direction the water leaves in.
HEAD_VALUE = {
    (NE, None): 43, (E, HIGH): 44, (E, LOW): 45, (SE, None): 46,
    (SW, None): 47, (W, HIGH): 48, (W, LOW): 49, (NW, None): 50,
}

#: River mouth, carried by the **ocean** cell, by the direction of the land
#: cell it takes the water from.
MOUTH_VALUE = {
    (NE, None): 51, (E, HIGH): 52, (E, LOW): 53, (SE, None): 54,
    (SW, None): 55, (W, HIGH): 56, (W, LOW): 57, (NW, None): 58,
}

_THROUGH_VALUE = {frozenset(ports): value for value, ports in VALUE_PORTS.items()}

#: The nine direction pairs a river may cross a cell by. Everything else is a
#: 60-degree turn, which the format cannot express.
LEGAL_TURNS = {frozenset(d for d, _ in ports) for ports in VALUE_PORTS.values()}

#: Directions whose crossing has a high and a low variant.
HEIGHTED = {E, W}

#: Rivers per generated world, and how long a course may run before it is
#: abandoned. Both from the shipped generated worlds: ten rivers, 90-95 cells.
RIVER_COUNT = 10
MAX_COURSE = 40

#: How far a course may stray from the straight run downhill. Rivers in the
#: shipped generated worlds average about nine cells; a strictly downhill walk
#: produces four or five, because it dashes for the nearest coast.
WANDER = 2.4

MOUNTAIN = 9
OCEAN = 0


def opposite(direction: int) -> int:
    return (direction + 3) % 6


def _height(direction: int, rng: random.Random):
    """Pick a crossing height, for the directions that have one."""
    return rng.choice((HIGH, LOW)) if direction in HEIGHTED else None


def through_value(port_a, port_b):
    """The value crossing these two ports, or None if the format cannot."""
    return _THROUGH_VALUE.get(frozenset((port_a, port_b)))


def _distance_to_sea(geom, is_ocean) -> dict:
    """Steps from each land cell to the nearest sea, by breadth-first search.

    Carving downhill needs a gradient, and the shipped maps do not record one.
    Distance to the coast is the honest stand-in: it falls monotonically toward
    the sea, which is the only property the course actually needs.
    """
    distance: dict = {}
    queue = deque()
    for y in range(geom.height):
        for x in range(geom.width):
            if is_ocean((x, y)):
                distance[(x, y)] = 0
                queue.append((x, y))
    while queue:
        cell = queue.popleft()
        for neighbour in geom.neighbours(*cell):
            if neighbour is not None and neighbour not in distance:
                distance[neighbour] = distance[cell] + 1
                queue.append(neighbour)
    return distance


def _course(rng, geom, source, arrived_from, is_ocean, taken, distance):
    """Walk one river from `source` to the sea, or return None.

    `arrived_from` is the direction back toward the previous cell — the port the
    water came in by — and the next step must pair with it legally. Preference
    goes to whichever legal step gets closest to the sea, with ties broken at
    random so courses do not all run the same way.
    """
    course = []                      # [(cell, out_direction)]
    cell, back = source, arrived_from
    for _ in range(MAX_COURSE):
        options = []
        for direction in range(6):
            if back is not None and frozenset((back, direction)) not in LEGAL_TURNS:
                continue
            if back is None and direction == back:
                continue
            neighbour = geom.neighbour(*cell, direction)
            if neighbour is None or neighbour in taken:
                continue
            if any(c == neighbour for c, _ in course):
                continue
            # Strictly downhill gives a straight dash for the coast about half
            # the length of a real one. A little slack lets the course wander
            # sideways without ever climbing away from the sea for long.
            score = distance.get(neighbour, 1 << 20) + rng.random() * WANDER
            options.append((score, direction, neighbour))
        if not options:
            return None
        options.sort()
        _, direction, neighbour = options[0]
        course.append((cell, direction))
        if is_ocean(neighbour):
            return course, neighbour, direction
        cell, back = neighbour, opposite(direction)
    return None


def carve(rng: random.Random, geom, terrain: dict, count: int = RIVER_COUNT) -> dict:
    """Cut `count` rivers into the map, returning cell -> byte 2 value.

    The returned dict covers land cells (through-flows and heads) and the ocean
    cells that receive a mouth. Cells absent from it carry no river.

    Sources are mountains, as in every shipped generated world, and a source
    already beside the sea is skipped — a one-cell river is not worth drawing.
    """
    def is_ocean(cell) -> bool:
        return terrain.get(cell, OCEAN) == OCEAN

    distance = _distance_to_sea(geom, is_ocean)
    sources = [cell for cell, kind in sorted(terrain.items())
               if kind == MOUNTAIN and distance.get(cell, 0) > 1]
    rng.shuffle(sources)
    # Inland mountains first: a spring two cells from the coast can only ever
    # make a stub, and the shipped worlds' rivers cross real distance.
    sources.sort(key=lambda cell: -distance.get(cell, 0))

    river: dict = {}
    taken: set = set()
    for source in sources:
        if len(taken) and sum(1 for v in river.values() if 43 <= v <= 50) >= count:
            break
        if source in taken:
            continue
        walked = _course(rng, geom, source, None, is_ocean, taken, distance)
        if walked is None:
            continue
        course, mouth_cell, mouth_direction = walked
        if len(course) < 2:
            continue                 # too short to read as a river
        _write(rng, river, taken, course, mouth_cell, mouth_direction)
    return river


def _write(rng, river: dict, taken: set, course, mouth_cell, mouth_direction):
    """Turn a walked course into byte-2 values, sharing a height per edge."""
    # One height per crossing, shared by the two cells that meet on it.
    heights = [_height(direction, rng) for _, direction in course]

    ports: dict = {}
    for index, (cell, direction) in enumerate(course):
        height = heights[index]
        ports.setdefault(cell, []).append((direction, height))
        following = (course[index + 1][0] if index + 1 < len(course)
                     else mouth_cell)
        ports.setdefault(following, []).append((opposite(direction), height))

    values = {}
    for cell, cell_ports in ports.items():
        if len(cell_ports) == 1:
            table = MOUTH_VALUE if cell == mouth_cell else HEAD_VALUE
            value = table.get(cell_ports[0])
        else:
            value = through_value(*cell_ports[:2])
        if value is None:
            return               # unrepresentable; abandon rather than corrupt
        values[cell] = value

    river.update(values)
    taken.update(values)
