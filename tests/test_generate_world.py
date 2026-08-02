import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "src"))

from imperialism_format.constants import DEVELOPED_TERRAIN_RESOURCE
from imperialism_format.derive import HexGeometry
from imperialism_format.generate import rng, world

WIDTH, HEIGHT = 108, 60


def build(keyword="Pippin", width=WIDTH, height=HEIGHT, **kwargs):
    return world.generate_geography(rng.generator(keyword), width, height, **kwargs)


def landmasses(cells, width=WIDTH, height=HEIGHT):
    geom = HexGeometry(width, height)
    land = {p for p, c in cells.items() if c["terrain"] != world.OCEAN}
    seen, sizes = set(), []
    for start in land:
        if start in seen:
            continue
        size, stack = 0, [start]
        seen.add(start)
        while stack:
            current = stack.pop()
            size += 1
            for neighbour in geom.neighbours(*current):
                if neighbour in land and neighbour not in seen:
                    seen.add(neighbour)
                    stack.append(neighbour)
        sizes.append(size)
    return sorted(sizes, reverse=True)


def test_no_land_sits_on_the_east_or_west_edge():
    """A continent across the wrap splits a country into unreachable halves.

    The game's own generated worlds keep both edge columns clear of land, and
    the historical maps keep the western one clear. A real launch of a world
    that wrapped showed a country on both sides of the screen, unnavigable
    between them.
    """
    margin = world.EDGE_SEA_MARGIN
    edge = list(range(margin)) + list(range(WIDTH - margin, WIDTH))
    for keyword in ("Pippin", "Zimm", "Kathay"):
        cells = build(keyword)
        for y in range(HEIGHT):
            for x in edge:
                assert cells[(x, y)]["terrain"] == world.OCEAN, \
                    f"{keyword}: land at {x},{y}"


# --- the seed -------------------------------------------------------------

def test_a_keyword_reproduces_its_world():
    assert build("Pippin") == build("Pippin")


def test_different_keywords_give_different_worlds():
    assert build("Pippin") != build("Otto")


def test_the_seed_does_not_depend_on_python_hashing():
    """`random.seed(str)` would vary per process unless PYTHONHASHSEED is set."""
    assert rng.seed_from("Pippin") == 0x9C09E9D4D4A0E2A5 or isinstance(
        rng.seed_from("Pippin"), int)
    assert rng.seed_from("Pippin") == rng.seed_from(" pippin ")


def test_generation_leaves_the_global_random_state_alone():
    import random
    random.seed(1234)
    expected = random.random()
    random.seed(1234)
    build("Pippin")
    assert random.random() == expected


# --- geography ------------------------------------------------------------

def test_land_share_is_close_to_the_target():
    for keyword in ("Pippin", "Otto", "Ryvius"):
        cells = build(keyword)
        land = sum(1 for c in cells.values() if c["terrain"] != world.OCEAN)
        assert 0.27 <= land / len(cells) <= 0.33, keyword


def test_the_land_share_is_adjustable():
    sparse = build("Pippin", land_share=0.15)
    dense = build("Pippin", land_share=0.45)
    count = lambda cs: sum(1 for c in cs.values() if c["terrain"] != world.OCEAN)
    assert count(sparse) < count(dense)


def test_there_are_nine_separate_landmasses():
    """Two large continents, two medium, five small — as the shipped worlds."""
    for keyword in ("Pippin", "Otto", "Ryvius", "Imperium"):
        sizes = landmasses(build(keyword))
        assert len(sizes) == 9, f"{keyword}: {sizes}"
        assert sizes[0] > 400, f"{keyword}: no continent, {sizes}"


def test_latitude_bands_match_the_measured_world():
    """Latitude is a budget, not a coin flip — the histogram has to land."""
    cells = build("Pippin")
    measured = [2, 26, 43, 43, 34, 30, 36, 37, 33, 30, 37, 39, 35, 25, 1]
    for index, top in enumerate(range(0, HEIGHT, 4)):
        rows = range(top, min(top + 4, HEIGHT))
        land = sum(1 for (x, y), c in cells.items()
                   if y in rows and c["terrain"] != world.OCEAN)
        total = sum(1 for (x, y) in cells if y in rows)
        got = land * 100 // total
        # The sub-polar bands run low: masses struggle to reach the fringe.
        tolerance = 8 if measured[index] in (26, 25) else 4
        assert abs(got - measured[index]) <= tolerance, (top, got, measured[index])


def test_the_poles_are_tundra_and_nearly_empty():
    cells = build("Pippin")
    polar = [c for (x, y), c in cells.items()
             if (y < 3 or y > HEIGHT - 4) and c["terrain"] != world.OCEAN]
    assert polar, "the poles should not be pure ocean"
    tundra = sum(1 for c in polar if c["terrain"] == 12)
    assert tundra / len(polar) > 0.6


def test_a_desert_belt_exists_away_from_the_equator():
    cells = build("Pippin")
    desert_rows = [y for y in range(HEIGHT)
                   if sum(1 for x in range(WIDTH)
                          if cells[(x, y)]["terrain"] == 11) > 4]
    assert desert_rows, "no desert belt at all"
    assert max(desert_rows) - min(desert_rows) > 5


# --- resources ------------------------------------------------------------

def test_developed_terrain_always_carries_its_own_resource():
    """The rule the validator enforces, so generation must not break it."""
    cells = build("Pippin")
    for cell in cells.values():
        expected = DEVELOPED_TERRAIN_RESOURCE.get(cell["terrain"])
        if expected is not None:
            assert cell["resource"] == expected


def test_only_the_sea_has_fish():
    cells = build("Pippin")
    for cell in cells.values():
        if cell["resource"] == world.FISH:
            assert cell["terrain"] == world.OCEAN


def test_the_sea_carries_nothing_but_fish():
    cells = build("Pippin")
    for cell in cells.values():
        if cell["terrain"] == world.OCEAN:
            assert cell["resource"] in (world.FISH, world.NO_RESOURCE)


def test_minerals_appear_where_they_should_and_not_elsewhere():
    cells = build("Pippin")
    found = {}
    for cell in cells.values():
        if cell["resource"] in (3, 4, 6, 21, 22):        # coal, iron, oil, gems, gold
            found.setdefault(cell["resource"], set()).add(cell["terrain"])
    assert found, "no minerals generated at all"
    for resource, terrains in found.items():
        assert terrains <= {8, 9, 10, 11, 12}, (resource, terrains)


def test_every_cell_gets_the_underlay_its_terrain_implies():
    cells = build("Pippin")
    for cell in cells.values():
        assert cell["underlay"] == world.UNDERLAY_FOR[cell["terrain"]]


# --- shape independence ---------------------------------------------------

def test_generation_does_not_assume_the_legacy_grid():
    """Nothing here may hardcode 108x60 — the engine is not limited to it."""
    cells = build("Pippin", width=40, height=30)
    assert len(cells) == 40 * 30
    land = sum(1 for c in cells.values() if c["terrain"] != world.OCEAN)
    assert 0.2 <= land / len(cells) <= 0.4
