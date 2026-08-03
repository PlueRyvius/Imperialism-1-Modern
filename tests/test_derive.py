import glob
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "src"))

from imperialism_format import HexCell, MapFile, MapFormatProfile
from imperialism_format import derive
from imperialism_format.derive import HexGeometry, NE, E, SE, SW, W, NW

import originals

FIXTURE_DIR = originals.FIXTURE_DIR


def real_maps():
    """Original .map files to test against.

    Set IMP_SCENARIO_DIR to a game install's Scenario folder to check all ten
    without copying copyrighted data into the repo. See tests/originals.py for
    why an edited map is replaced by its .bak.
    """
    return originals.maps()


def small_map(width=5, height=5):
    profile = MapFormatProfile(
        width=width, height=height, trailer_record_count=0, trailer_record_size=0
    )
    return MapFile.blank(profile)


# --- geometry -------------------------------------------------------------

def test_even_row_neighbours_are_odd_r_offset():
    geom = HexGeometry(width=10, height=10, wrap_x=False)
    assert geom.neighbour(5, 2, NE) == (5, 1)
    assert geom.neighbour(5, 2, E) == (6, 2)
    assert geom.neighbour(5, 2, SE) == (5, 3)
    assert geom.neighbour(5, 2, SW) == (4, 3)
    assert geom.neighbour(5, 2, W) == (4, 2)
    assert geom.neighbour(5, 2, NW) == (4, 1)


def test_odd_rows_are_shifted_right():
    geom = HexGeometry(width=10, height=10, wrap_x=False)
    assert geom.neighbour(5, 3, NE) == (6, 2)
    assert geom.neighbour(5, 3, NW) == (5, 2)
    assert geom.neighbour(5, 3, SE) == (6, 4)
    assert geom.neighbour(5, 3, SW) == (5, 4)


def test_neighbour_relation_is_symmetric():
    """If A is B's NE neighbour then B must be A's SW neighbour."""
    geom = HexGeometry(width=9, height=9, wrap_x=False)
    opposite = {NE: SW, E: W, SE: NW, SW: NE, W: E, NW: SE}
    for y in range(9):
        for x in range(9):
            for d, back in opposite.items():
                pos = geom.neighbour(x, y, d)
                if pos is not None:
                    assert geom.neighbour(*pos, back) == (x, y)


def test_x_wraps_but_y_does_not():
    geom = HexGeometry(width=8, height=8, wrap_x=True)
    assert geom.neighbour(7, 2, E) == (0, 2)
    assert geom.neighbour(0, 2, W) == (7, 2)
    assert geom.neighbour(3, 0, NE) is None
    assert geom.neighbour(3, 7, SE) is None


def test_wrap_can_be_disabled():
    geom = HexGeometry(width=8, height=8, wrap_x=False)
    assert geom.neighbour(7, 2, E) is None


def test_geometry_reads_dimensions_from_the_map():
    m = small_map(width=13, height=7)
    geom = derive.geometry_for(m)
    assert (geom.width, geom.height) == (13, 7)


# --- individual rules on synthetic data -----------------------------------

def test_land_coastline_marks_only_the_sea_facing_directions():
    m = small_map()
    for y in range(5):
        for x in range(5):
            m.set(x, y, HexCell(terrain=1))
    m.set(2, 1, HexCell(terrain=0))  # NE of (2,2) on an even row
    assert derive.land_coastline(m, 2, 2) == 1 << NE


def test_ocean_cells_carry_no_land_coastline():
    m = small_map()
    for y in range(5):
        for x in range(5):
            m.set(x, y, HexCell(terrain=1))
    m.set(2, 2, HexCell(terrain=0))
    assert derive.land_coastline(m, 2, 2) == 0


def test_national_border_treats_the_map_edge_as_a_frontier():
    m = small_map()
    for y in range(5):
        for x in range(5):
            m.set(x, y, HexCell(terrain=1, nation_zone_a=3))
    geom = HexGeometry(m.width, m.height, wrap_x=False)
    # Top-left corner on an even row: only E and SE stay on the map.
    off_map = (1 << NE) | (1 << SW) | (1 << W) | (1 << NW)
    assert derive.national_border(m, 0, 0, geom) == off_map


def test_national_border_ignores_the_sea():
    m = small_map()
    for y in range(5):
        for x in range(5):
            m.set(x, y, HexCell(terrain=1, nation_zone_a=3))
    m.set(2, 1, HexCell(terrain=0, nation_zone_a=99))
    assert derive.national_border(m, 2, 2) == 0


def test_province_border_does_not_treat_the_map_edge_as_a_boundary():
    m = small_map()
    for y in range(5):
        for x in range(5):
            m.set(x, y, HexCell(terrain=1, province=7))
    geom = HexGeometry(m.width, m.height, wrap_x=False)
    assert derive.province_border(m, 0, 0, geom) == 0


def test_province_border_marks_differing_provinces():
    m = small_map()
    for y in range(5):
        for x in range(5):
            m.set(x, y, HexCell(terrain=1, province=7))
    m.set(3, 2, HexCell(terrain=1, province=8))  # E of (2,2)
    assert derive.province_border(m, 2, 2) == 1 << E


def test_like_cell_adjacency_groups_hill_with_wool_hill():
    m = small_map()
    for y in range(5):
        for x in range(5):
            m.set(x, y, HexCell(terrain=1))
    m.set(2, 2, HexCell(terrain=8))
    m.set(3, 2, HexCell(terrain=7))
    assert derive.like_cell_adjacency(m, 2, 2) == 1 << E


def test_like_cell_adjacency_is_never_set_on_ocean():
    m = small_map()
    for y in range(5):
        for x in range(5):
            m.set(x, y, HexCell(terrain=0))
    assert derive.like_cell_adjacency(m, 2, 2) == 0


# --- batch application ----------------------------------------------------

def test_affected_by_covers_the_cell_and_its_neighbours():
    m = small_map()
    affected = derive.affected_by(m, 2, 2)
    assert (2, 2) in affected
    assert len(affected) == 7


def test_apply_edits_updates_neighbours_of_an_edited_cell():
    m = small_map()
    for y in range(5):
        for x in range(5):
            m.set(x, y, HexCell(terrain=1, province=7))
    derive.apply_edits(m, [(2, 2)])
    assert m.get(2, 2).like_cell_adjacency != 0

    # Turning one cell to sea must give its land neighbours a coastline.
    m.get(2, 1).terrain = 0
    changes = derive.apply_edits(m, [(2, 1)])
    assert m.get(2, 2).land_coastline == 1 << NE
    assert (2, 2) in changes


def test_apply_cell_reports_only_what_changed():
    m = small_map()
    for y in range(5):
        for x in range(5):
            m.set(x, y, HexCell(terrain=1))
    first = derive.apply_cell(m, 2, 2)
    assert "like_cell_adjacency" in first
    assert derive.apply_cell(m, 2, 2) == {}  # idempotent


# --- fit against real game data -------------------------------------------
#
# These reproduce the derived bytes of unmodified original maps.  Each rule has
# a measured accuracy; the thresholds below are the floors we verified, not
# aspirations.  A rule that regresses below its floor is broken.  Self-skips
# when no game data is present, since we never commit it.

FIT_FLOORS = {
    "national_border": 1.0,
    "province_border": 0.999,
    "like_cell_adjacency": 0.99,
    "land_coastline": 0.95,
    "hill_mountain_overlay": 1.0,
}


def measure(map_file, field):
    fn = derive.DERIVERS[field]
    geom = derive.geometry_for(map_file)
    matched = 0
    for y in range(map_file.height):
        for x in range(map_file.width):
            actual = getattr(map_file.get(x, y), field) & 0x3F
            if fn(map_file, x, y, geom) & 0x3F == actual:
                matched += 1
    return matched / (map_file.width * map_file.height)


def test_derivation_reproduces_real_maps():
    paths = real_maps()
    if not paths:
        return
    for path in paths:
        m = MapFile.load(path)
        for field, floor in FIT_FLOORS.items():
            fit = measure(m, field)
            assert fit >= floor, (
                f"{os.path.basename(path)}: {field} reproduced {fit:.4%} "
                f"of cells, below the {floor:.2%} floor"
            )


def test_byte_11_is_exact_on_real_maps():
    """Byte 11 is fully decoded, so nothing less than perfection will do.

    Ocean cells carry the adjacent-land mask (the sea's half of the shore),
    hills the adjacent-mountain mask, mountains the adjacent-hill mask, and
    everything else zero. Previously written off as an unknown art byte, which
    left generated worlds with no shoreline on the water side at all.
    """
    for path in real_maps():
        m = MapFile.load(path)
        assert measure(m, "hill_mountain_overlay") == 1.0, os.path.basename(path)


def test_byte_01_stays_inside_the_adjacent_land_mask():
    """Byte 01's exact subset is unknown; that it *is* a subset is not.

    True of every coastal ocean cell in every shipped map, which is what makes
    returning the full mask a safe approximation rather than a guess.
    """
    for path in real_maps():
        m = MapFile.load(path)
        geom = derive.geometry_for(m)
        for y in range(m.height):
            for x in range(m.width):
                cell = m.get(x, y)
                if cell.terrain != 0:
                    continue
                allowed = derive.ocean_coastline(m, x, y, geom)
                assert cell.ocean_coastline & ~allowed == 0, \
                    f"{os.path.basename(path)} at {x},{y}"


def test_open_sea_keeps_its_island_decoration():
    """Islands live in byte 01 where there is no adjacent land, and are
    authored rather than derived, so recomputation must not erase them."""
    for path in real_maps():
        m = MapFile.load(path)
        geom = derive.geometry_for(m)
        islands = 0
        for y in range(m.height):
            for x in range(m.width):
                cell = m.get(x, y)
                if cell.terrain != 0 or cell.ocean_coastline == 0:
                    continue
                if derive.hill_mountain_overlay(m, x, y, geom) == 0:
                    assert cell.ocean_coastline in (1, 2, 3, 4), \
                        f"{os.path.basename(path)}: {cell.ocean_coastline}"
                    assert derive.ocean_coastline(m, x, y, geom) == \
                        cell.ocean_coastline
                    islands += 1
        assert islands > 0, f"{os.path.basename(path)} has no islands at all"


def test_national_border_is_exact_on_real_maps():
    """The one rule we hold to perfection — nations are gameplay, not artwork."""
    for path in real_maps():
        m = MapFile.load(path)
        assert measure(m, "national_border") == 1.0, os.path.basename(path)


def test_province_border_on_ocean_is_exact_on_real_maps():
    """Byte 8 on water marks where a province outline reaches the coast.

    Held to perfection, like `national_border`, because the engine turns these
    bits into boundary segments and resolves the province on each side of one.
    A bit facing water has no province there to resolve.
    """
    for path in real_maps():
        m = MapFile.load(path)
        geom = derive.geometry_for(m)
        for y in range(m.height):
            for x in range(m.width):
                cell = m.get(x, y)
                if cell.terrain != 0:
                    continue
                assert derive.province_border(m, x, y, geom) == cell.province_border, \
                    f"{os.path.basename(path)} at {x},{y}"


def test_a_province_border_bit_never_faces_open_water():
    """The invariant behind the rule, stated directly and checked after editing.

    Repainting land to sea used to leave the old land mask in place, pointing
    at water that no longer belongs to any province. The engine indexes its
    region table with what it finds there, unguarded, and 65535 walks off the
    front of it — `UMapper.cpp:4751` asserts on exactly that case.
    """
    paths = real_maps()
    if not paths:
        return
    m = MapFile.load(paths[0])
    geom = derive.geometry_for(m)

    def orphaned_bits():
        found = []
        for y in range(m.height):
            for x in range(m.width):
                cell = m.get(x, y)
                if cell.terrain != 0:
                    continue
                for direction, pos in enumerate(geom.neighbours(x, y)):
                    if not (cell.province_border >> direction) & 1:
                        continue
                    if pos is None or m.get(*pos).terrain == 0:
                        found.append((x, y, direction))
        return found

    assert orphaned_bits() == []

    # Drown a stretch that crosses a province boundary, the way the editor's
    # ocean brush does, and recompute exactly what it recomputes.
    drowned = []
    for y in range(m.height):
        for x in range(m.width - 1):
            here, east = m.get(x, y), m.get(x + 1, y)
            if here.terrain and east.terrain and here.province != east.province:
                drowned = [(x, y), (x + 1, y)]
                break
        if drowned:
            break
    assert drowned, "no province boundary to paint over"

    for x, y in drowned:
        cell = m.get(x, y)
        cell.terrain = 0
        cell.terrain_underlay = 5
        cell.province = 65535
    derive.apply_edits(m, drowned, geom=geom)

    assert orphaned_bits() == []


def test_recomputing_keeps_the_undecoded_high_bits_of_a_border_byte():
    """Bits 6 and 7 of bytes 7 and 8 are not directions and are not decoded.

    They sit on 79 land and 342 ocean cells (byte 7) and 1,584 land and 9 ocean
    cells (byte 8) across the shipped corpus. Recomputing a six-bit mask over
    the whole byte silently destroyed them on every cell an edit touched.
    """
    m = small_map()
    m.get(2, 2).terrain = 1
    m.get(2, 2).province = 3
    m.get(2, 2).national_border = 0b1100_0000
    m.get(2, 2).province_border = 0b0100_0000

    derive.apply_cell(m, 2, 2)

    assert m.get(2, 2).national_border & 0b1100_0000 == 0b1100_0000
    assert m.get(2, 2).province_border & 0b1100_0000 == 0b0100_0000


def test_deriving_does_not_disturb_undecoded_bytes():
    """Recomputation must leave every non-derived byte alone."""
    paths = real_maps()
    if not paths:
        return
    m = MapFile.load(paths[0])
    before = [c.to_bytes() for c in m.cells]
    trailer = m.dormant_trailer
    derive.apply_edits(m, [(x, y) for y in range(m.height) for x in range(m.width)])
    # national/province border, land coastline, adjacency, plus the two shore
    # bytes: 11 (exact) and 1 (ocean side, approximate — see derive.py).
    derived_offsets = {1, 7, 8, 9, 10, 11}
    for i, cell in enumerate(m.cells):
        after = cell.to_bytes()
        for offset in range(36):
            if offset not in derived_offsets:
                assert after[offset] == before[i][offset], (i, offset)
    assert m.dormant_trailer == trailer
