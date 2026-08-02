"""Recomputation of the .map cell bytes that are functions of neighbouring cells.

Several of the 36 bytes in a :class:`~.map_file.HexCell` are six-bit direction
masks describing the cell's relationship to its six neighbours.  An editor that
lets you paint terrain has to keep them consistent, because changing one cell
invalidates the masks of up to six others.

Everything here was established by fitting candidate rules against real game
data and keeping only the ones that reproduce it; see ``docs/derived-bytes.md``
for the measured fit of each rule and for the bytes we deliberately do *not*
derive.

Nothing in this module may assume the legacy 108x60 grid — geometry comes from
the :class:`~.map_file.MapFile` it is handed.
"""
from __future__ import annotations

from dataclasses import dataclass

# Bit i of a direction mask corresponds to DIRECTIONS[i], starting at NE and
# proceeding clockwise.  Matches the order in constants.DIRECTIONS.
NE, E, SE, SW, W, NW = range(6)

# The grid is an odd-r offset layout of pointy-top hexes: odd-numbered rows sit
# half a cell to the right (or equivalently, even rows half a cell left).  Rows
# below use (dx, dy) per direction, indexed by row parity.
_NEIGHBOUR_OFFSETS = {
    0: [(0, -1), (1, 0), (0, 1), (-1, 1), (-1, 0), (-1, -1)],
    1: [(1, -1), (1, 0), (1, 1), (0, 1), (-1, 0), (0, -1)],
}

# Terrain types that are treated as one another for like-cell adjacency.  Hill
# and wool-hill share artwork, so the original data links them.
_ADJACENCY_GROUP = {7: 7, 8: 7}

# Terrain types whose cells never carry a like-cell adjacency mask.
_ADJACENCY_EXEMPT = frozenset({0, 4})  # ocean, horse ranch

# Byte 11's hill-to-mountain transition runs between these two groups.
_HILL_TERRAIN = frozenset({7, 8})       # wool hill, barren hill
_MOUNTAIN_TERRAIN = frozenset({9})

OCEAN_TERRAIN = 0


@dataclass(frozen=True)
class HexGeometry:
    """Neighbour lookup for an odd-r offset hex grid.

    The original maps wrap east-west — the world is a globe — but do not wrap
    north-south.  ``wrap_x`` is a property of the map being edited rather than
    of the format, so it is configurable.
    """

    width: int
    height: int
    wrap_x: bool = True

    def neighbour(self, x: int, y: int, direction: int) -> tuple[int, int] | None:
        """Return the neighbour in ``direction``, or None if it is off-map."""
        dx, dy = _NEIGHBOUR_OFFSETS[y & 1][direction]
        nx, ny = x + dx, y + dy
        if self.wrap_x:
            nx %= self.width
        if not (0 <= nx < self.width and 0 <= ny < self.height):
            return None
        return nx, ny

    def neighbours(self, x: int, y: int) -> list[tuple[int, int] | None]:
        """All six neighbours in bit order, with None for off-map."""
        return [self.neighbour(x, y, d) for d in range(6)]


def geometry_for(map_file, wrap_x: bool = True) -> HexGeometry:
    return HexGeometry(map_file.width, map_file.height, wrap_x=wrap_x)


def _mask(geom: HexGeometry, map_file, x: int, y: int, predicate) -> int:
    """Build a six-bit mask by testing ``predicate`` against each neighbour.

    ``predicate(self_cell, neighbour_cell_or_None)`` decides each bit.
    """
    cell = map_file.get(x, y)
    value = 0
    for direction, pos in enumerate(geom.neighbours(x, y)):
        neighbour = map_file.get(*pos) if pos is not None else None
        if predicate(cell, neighbour):
            value |= 1 << direction
    return value


def _is_ocean(cell) -> bool:
    return cell is not None and cell.terrain == OCEAN_TERRAIN


def national_border(map_file, x: int, y: int, geom: HexGeometry | None = None) -> int:
    """Directions in which this land cell faces a different nation.

    The map edge counts as a national border; ocean neighbours never do, so a
    coastline is not drawn as a frontier.  Ocean cells carry a nation byte too
    (a sea-zone id), but their border mask is not this function's business.
    """
    geom = geom or geometry_for(map_file)
    if _is_ocean(map_file.get(x, y)):
        return map_file.get(x, y).national_border
    return _mask(
        geom, map_file, x, y,
        lambda c, n: n is None or (not _is_ocean(n) and n.nation_zone_a != c.nation_zone_a),
    )


def province_border(map_file, x: int, y: int, geom: HexGeometry | None = None) -> int:
    """Directions in which this land cell faces a different province.

    Unlike national borders, the map edge does *not* count — provinces are
    bounded by the land they occupy, not by the edge of the world.
    """
    geom = geom or geometry_for(map_file)
    if _is_ocean(map_file.get(x, y)):
        return map_file.get(x, y).province_border
    return _mask(
        geom, map_file, x, y,
        lambda c, n: n is not None and not _is_ocean(n) and n.province != c.province,
    )


def land_coastline(map_file, x: int, y: int, geom: HexGeometry | None = None) -> int:
    """Directions in which this land cell meets the sea.

    Cosmetic: it selects shoreline artwork. Ocean cells always carry zero.
    """
    geom = geom or geometry_for(map_file)
    if _is_ocean(map_file.get(x, y)):
        return 0
    return _mask(geom, map_file, x, y, lambda c, n: _is_ocean(n))


def like_cell_adjacency(map_file, x: int, y: int, geom: HexGeometry | None = None) -> int:
    """Directions in which this cell abuts terrain of its own kind.

    Used to blend adjacent tiles of matching terrain. Ocean and horse-ranch
    cells never carry the mask, and hill/wool-hill count as the same terrain.
    """
    geom = geom or geometry_for(map_file)
    cell = map_file.get(x, y)
    if cell.terrain in _ADJACENCY_EXEMPT:
        return 0

    def group(c) -> int:
        return _ADJACENCY_GROUP.get(c.terrain, c.terrain)

    return _mask(
        geom, map_file, x, y,
        lambda c, n: n is not None and group(n) == group(c),
    )


def hill_mountain_overlay(map_file, x: int, y: int,
                          geom: HexGeometry | None = None) -> int:
    """Byte 11, which means two different things depending on the cell.

    On an **ocean** cell it is the mask of adjacent land — the sea's own half
    of the shoreline, the counterpart to `land_coastline`. On a **hill** it is
    the mask of adjacent mountains, and on a **mountain** the mask of adjacent
    hills; that is the hill-to-mountain transition the old community notes
    describe. Every other land cell carries zero.

    All three rules are exact: they reproduce byte 11 for every cell of `s1`,
    `s9` and `s11` without a single exception — 738/738, 748/748 and 859/859
    coastal ocean cells, and every hill and mountain in all three.

    A generated world that leaves this at zero has no shoreline art on the
    water side at all, which is visible immediately.
    """
    geom = geom or geometry_for(map_file)
    cell = map_file.get(x, y)
    if _is_ocean(cell):
        return _mask(geom, map_file, x, y,
                     lambda c, n: n is not None and not _is_ocean(n))
    if cell.terrain in _HILL_TERRAIN:
        return _mask(geom, map_file, x, y,
                     lambda c, n: n is not None and n.terrain in _MOUNTAIN_TERRAIN)
    if cell.terrain in _MOUNTAIN_TERRAIN:
        return _mask(geom, map_file, x, y,
                     lambda c, n: n is not None and n.terrain in _HILL_TERRAIN)
    return 0


def ocean_coastline(map_file, x: int, y: int,
                    geom: HexGeometry | None = None) -> int:
    """Byte 01 on an ocean cell: which adjacent land the shore is drawn against.

    **Approximate, unlike the others here.** Measured across `s1`, `s9` and
    `s11`, this is always a *subset* of the adjacent-land mask — 2,345 of 2,345
    coastal ocean cells, no exceptions — and equal to the whole mask in most of
    them. Which subset the original picks is not settled: it tracks neither
    terrain nor underlay of the neighbouring land (every predicate tried came
    out between 20% and 68%, and disagreed between maps), so it looks like
    shoreline art variation we cannot yet reproduce.

    So this returns the full mask: a value the originals use, always inside the
    permitted subset relation, and vastly better than the zero a generated
    world carries today. Land cells are left alone — they carry byte 01 too and
    that rule is also unknown.

    On open water the byte instead encodes an island decoration (1 sandbar,
    2 small, 3 large, 4 islet group; never anything else in any shipped map).
    Those are authored, not derived, so open sea is left untouched.
    """
    geom = geom or geometry_for(map_file)
    cell = map_file.get(x, y)
    if not _is_ocean(cell):
        return cell.ocean_coastline
    land = _mask(geom, map_file, x, y,
                 lambda c, n: n is not None and not _is_ocean(n))
    # No adjacent land: this is open sea, where the byte is an island code.
    return cell.ocean_coastline if land == 0 else land


#: Cell fields this module can recompute, mapped to the function that does it.
#: Deliberately excludes ``river`` and ``rail`` (authored, not derived) and the
#: art masks we could not pin down — see ``docs/derived-bytes.md``.
DERIVERS = {
    "national_border": national_border,
    "province_border": province_border,
    "land_coastline": land_coastline,
    "like_cell_adjacency": like_cell_adjacency,
    "hill_mountain_overlay": hill_mountain_overlay,
    "ocean_coastline": ocean_coastline,
}


def derive_cell(map_file, x: int, y: int, fields=None,
                geom: HexGeometry | None = None) -> dict[str, int]:
    """Return the recomputed value of each derivable field for one cell."""
    geom = geom or geometry_for(map_file)
    names = DERIVERS if fields is None else {f: DERIVERS[f] for f in fields}
    return {name: fn(map_file, x, y, geom) for name, fn in names.items()}


def apply_cell(map_file, x: int, y: int, fields=None,
               geom: HexGeometry | None = None) -> dict[str, tuple[int, int]]:
    """Recompute one cell's derived fields in place.

    Returns only the fields that actually changed, as ``{name: (old, new)}``,
    so callers can report a minimal diff.
    """
    cell = map_file.get(x, y)
    changed = {}
    for name, new in derive_cell(map_file, x, y, fields, geom).items():
        old = getattr(cell, name)
        if old != new:
            setattr(cell, name, new)
            changed[name] = (old, new)
    return changed


def affected_by(map_file, x: int, y: int,
                geom: HexGeometry | None = None) -> list[tuple[int, int]]:
    """The cells whose derived bytes an edit at (x, y) can invalidate.

    That is the cell itself plus its six neighbours: every rule here looks
    exactly one step out, so nothing further away can change.
    """
    geom = geom or geometry_for(map_file)
    cells = [(x, y)]
    cells.extend(p for p in geom.neighbours(x, y) if p is not None)
    return cells


def apply_edits(map_file, edited: list[tuple[int, int]], fields=None,
                geom: HexGeometry | None = None) -> dict[tuple[int, int], dict]:
    """Recompute derived bytes around a batch of edited cells.

    Returns ``{(x, y): {field: (old, new)}}`` for every cell that changed.
    """
    geom = geom or geometry_for(map_file)
    targets = {p for x, y in edited for p in affected_by(map_file, x, y, geom)}
    changes = {}
    for x, y in sorted(targets):
        delta = apply_cell(map_file, x, y, fields, geom)
        if delta:
            changes[(x, y)] = delta
    return changes
