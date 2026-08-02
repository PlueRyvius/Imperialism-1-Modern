"""Rivers: the format model, and the courses carved with it.

The model was measured, not read — the community notes it started from carry at
least two errors — so the first tests here check it back against the originals.
"""
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "src"))

from imperialism_format import MapFile, derive
from imperialism_format.generate import build, rivers

import originals

KEYWORDS = ("Pippin", "Zimm", "Kathay", "Ryvius")

THROUGH = range(11, 27)
HEADS = range(43, 51)
MOUTHS = range(51, 59)


def _ports(value):
    """The direction/height ports a byte-2 value connects."""
    if value in rivers.VALUE_PORTS:
        return rivers.VALUE_PORTS[value]
    for table in (rivers.HEAD_VALUE, rivers.MOUTH_VALUE):
        for port, coded in table.items():
            if coded == value:
                return {port}
    return None


def _template():
    found = [p for p in originals.maps() if os.path.basename(p) == "s1.map"]
    return MapFile.load(found[0]) if found else None


def world(keyword):
    template = _template()
    return None if template is None else build.generate_world(
        keyword, template=template)


# --- the model, against shipped data --------------------------------------

def test_river_values_split_by_land_and_sea_as_measured():
    """11-26 and 43-50 on land, 51-58 only ever on an ocean cell."""
    for path in originals.maps():
        m = MapFile.load(path)
        for cell in m.cells:
            if not cell.river:
                continue
            assert _ports(cell.river) is not None, \
                f"{os.path.basename(path)}: unknown river value {cell.river}"
            if cell.river in MOUTHS:
                assert cell.terrain == 0, "a mouth belongs on the sea"
            else:
                assert cell.terrain != 0, "a through-flow belongs on land"


def test_every_river_port_meets_a_river_in_the_generated_originals():
    """The model's direction table, checked against the game's own worlds.

    Exact on `s9`, `s11`, `s12` and `s15`. `s1` and `s13` are hand-authored and
    each carry two or three mouths drawn with no river on the land beside them,
    so they are excluded rather than allowed for.
    """
    checked = 0
    for path in originals.maps():
        if os.path.basename(path) not in ("s9.map", "s11.map", "s12.map",
                                          "s15.map"):
            continue
        m = MapFile.load(path)
        geom = derive.geometry_for(m)
        for y in range(m.height):
            for x in range(m.width):
                value = m.get(x, y).river
                if not value:
                    continue
                for direction, _ in _ports(value):
                    neighbour = geom.neighbour(x, y, direction)
                    assert neighbour is not None and m.get(*neighbour).river, \
                        f"{os.path.basename(path)}: {value} at {x},{y} dangles"
                    checked += 1
    assert checked == 0 or checked > 300


def test_the_format_cannot_turn_sixty_degrees():
    """Nine of the fifteen direction pairs exist; the six missing are the
    60-degree turns. This is the one hard constraint on carving a course."""
    assert len(rivers.LEGAL_TURNS) == 9
    for direction in range(6):
        adjacent = frozenset((direction, (direction + 1) % 6))
        assert adjacent not in rivers.LEGAL_TURNS, adjacent
        straight = frozenset((direction, rivers.opposite(direction)))
        assert straight in rivers.LEGAL_TURNS, straight


def test_east_and_west_crossings_pair_by_height():
    """A river leaving east high must be taken in west high by the cell east
    of it. All four combinations are representable, so no course is blocked."""
    for east in (rivers.HIGH, rivers.LOW):
        for west in (rivers.HIGH, rivers.LOW):
            value = rivers.through_value((rivers.E, east), (rivers.W, west))
            assert value is not None, (east, west)
    assert rivers.through_value((rivers.E, rivers.HIGH),
                                (rivers.W, rivers.HIGH)) == 17
    assert rivers.through_value((rivers.E, rivers.LOW),
                                (rivers.W, rivers.LOW)) == 20


def test_the_two_errors_in_the_community_notes_stay_corrected():
    """`MapDecode.rtf` labels 49 an east head and 55 a south-east mouth. The
    shipped data says west and south-west, and generating from the notes as
    written would draw both in the wrong place."""
    assert rivers.HEAD_VALUE[(rivers.W, rivers.LOW)] == 49
    assert rivers.MOUTH_VALUE[(rivers.SW, None)] == 55


# --- the courses we carve -------------------------------------------------

def test_a_generated_world_carries_ten_rivers_that_reach_the_sea():
    for keyword in KEYWORDS:
        result = world(keyword)
        if result is None:
            return
        m = result["map"]
        heads = [c for c in m.cells if c.river in HEADS]
        mouths = [c for c in m.cells if c.river in MOUTHS]
        assert len(heads) == rivers.RIVER_COUNT, f"{keyword}: {len(heads)}"
        assert len(mouths) == rivers.RIVER_COUNT, f"{keyword}: {len(mouths)}"
        for cell in mouths:
            assert cell.terrain == 0


def test_no_generated_river_port_dangles():
    """Every port meets a river on the other side — the courses are connected."""
    for keyword in KEYWORDS:
        result = world(keyword)
        if result is None:
            return
        m = result["map"]
        geom = derive.geometry_for(m)
        for y in range(m.height):
            for x in range(m.width):
                value = m.get(x, y).river
                if not value:
                    continue
                for direction, _ in _ports(value):
                    neighbour = geom.neighbour(x, y, direction)
                    assert neighbour is not None and m.get(*neighbour).river, \
                        f"{keyword}: {value} at {x},{y} dangles {direction}"


def test_rivers_cover_about_as_much_land_as_the_shipped_worlds():
    """90-95 cells on 5% of the land, in every world the game generated."""
    for keyword in KEYWORDS:
        result = world(keyword)
        if result is None:
            return
        m = result["map"]
        river = sum(1 for c in m.cells if c.river)
        land = sum(1 for c in m.cells if c.terrain != 0)
        assert 0.02 <= river / land <= 0.09, f"{keyword}: {river / land:.3f}"


def test_no_capital_is_landlocked_now_that_rivers_exist():
    """The gap rivers were built to close: a country with no coast of its own
    can still seat its capital on a river and get a port."""
    for keyword in KEYWORDS + ("Alpha", "Beta"):
        result = world(keyword)
        if result is None:
            return
        m = result["map"]
        geom = derive.geometry_for(m)
        for y in range(m.height):
            for x in range(m.width):
                cell = m.get(x, y)
                if cell.town_type not in (33, 35):
                    continue
                coastal = any(m.get(*p).terrain == 0
                              for p in geom.neighbours(x, y) if p)
                assert coastal or cell.river, \
                    f"{keyword}: landlocked capital at {x},{y}"
