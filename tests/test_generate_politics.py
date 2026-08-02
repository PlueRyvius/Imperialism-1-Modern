import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "src"))
sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "tools", "map_editor"))

from imperialism_format import MapFile
from imperialism_format.derive import HexGeometry
from imperialism_format.generate import build, politics, rng, world

import anchors
import originals
import validate

WIDTH, HEIGHT = 108, 60
KEYWORDS = ("Pippin", "Otto", "Ryvius", "Imperium")


def layers(keyword="Pippin"):
    generator = rng.generator(keyword)
    geom = HexGeometry(WIDTH, HEIGHT)
    cells = world.generate_geography(generator, WIDTH, HEIGHT)
    land = {c for c, plan in cells.items() if plan["terrain"] != world.OCEAN}
    owner, sunk = politics.assign_countries(generator, geom, land)
    provinces, stranded = politics.assign_provinces(generator, geom, owner)
    for cell in stranded:
        owner.pop(cell, None)
    towns = politics.place_towns(generator, geom, owner, provinces)
    return geom, owner, provinces, towns, sunk | stranded


def pieces(geom, groups):
    """How many connected components each group has."""
    out = {}
    for key, cells in groups.items():
        seen, count = set(), 0
        for start in cells:
            if start in seen:
                continue
            count += 1
            stack = [start]
            seen.add(start)
            while stack:
                current = stack.pop()
                for neighbour in geom.neighbours(*current):
                    if neighbour in cells and neighbour not in seen:
                        seen.add(neighbour)
                        stack.append(neighbour)
        out[key] = count
    return out


def group(mapping):
    out = {}
    for cell, key in mapping.items():
        out.setdefault(key, set()).add(cell)
    return out


# --- structure ------------------------------------------------------------

def test_the_plan_matches_the_shipped_generated_worlds():
    plan = politics.default_plan()
    assert len(plan) == 23
    assert plan[:7] == [8] * 7
    assert plan[7:] == [4] * 16
    assert sum(plan) == 120


def test_every_country_gets_the_provinces_its_rank_calls_for():
    for keyword in KEYWORDS:
        _, owner, provinces, _, _ = layers(keyword)
        per_country = {}
        for cell, province in provinces.items():
            per_country.setdefault(owner[cell], set()).add(province)
        counts = [len(per_country[c]) for c in sorted(per_country)]
        assert counts == politics.default_plan(), f"{keyword}: {counts}"


def test_there_are_a_hundred_and_twenty_provinces():
    for keyword in KEYWORDS:
        _, _, provinces, _, _ = layers(keyword)
        assert len(set(provinces.values())) == 120, keyword


def test_every_province_is_a_single_blob():
    for keyword in KEYWORDS:
        geom, _, provinces, _, _ = layers(keyword)
        broken = {k: n for k, n in pieces(geom, group(provinces)).items() if n != 1}
        assert not broken, f"{keyword}: {broken}"


def test_country_packing_puts_each_country_on_one_landmass():
    """A country cannot span the sea, so packing happens before growing.

    This is what `assign_countries` alone guarantees, and it is weaker than
    "one connected blob". The later stages sink land that could not be given a
    province, and when the sunk cells were the isthmus between two lobes of a
    mass they cut the country that spanned it. Repairing that is
    `build._split_fragments`' job, so the blob invariant is asserted against the
    finished world in `test_every_finished_country_is_a_single_blob` rather than
    here — asserting it at this stage passed only by luck of the geography, and
    stopped holding the moment the edge margin changed the coastlines.
    """
    for keyword in KEYWORDS:
        geom, owner, _, _, _ = layers(keyword)
        land = set(owner)
        masses = {}
        seen = set()
        for start in land:
            if start in seen:
                continue
            stack, mass = [start], set()
            seen.add(start)
            while stack:
                current = stack.pop()
                mass.add(current)
                for neighbour in geom.neighbours(*current):
                    if neighbour in land and neighbour not in seen:
                        seen.add(neighbour)
                        stack.append(neighbour)
            for cell in mass:
                masses[cell] = id(mass)
        for country, cells in group(owner).items():
            assert len({masses[c] for c in cells}) == 1, \
                f"{keyword}: country {country} spans separate landmasses"


def test_all_land_is_owned_and_provinced():
    for keyword in KEYWORDS:
        geom, owner, provinces, _, sunk = layers(keyword)
        assert set(owner) == set(provinces), keyword
        assert not (set(owner) & sunk), "sunk islands must not keep an owner"


def test_no_province_exceeds_the_hard_cap():
    """Province size drives the economy, so the ceiling is enforced, not aimed at."""
    for keyword in KEYWORDS:
        _, _, provinces, _, _ = layers(keyword)
        sizes = sorted(len(cells) for cells in group(provinces).values())
        assert sizes[-1] <= politics.MAX_PROVINCE_CELLS, f"{keyword}: {sizes[-1]}"
        median = sizes[len(sizes) // 2]
        assert 12 <= median <= 20, f"{keyword}: median {median}"


def test_a_country_can_hold_no_more_than_its_provinces_legally_can():
    for keyword in KEYWORDS:
        _, owner, _, _, _ = layers(keyword)
        plan = politics.default_plan()
        for country, cells in group(owner).items():
            assert len(cells) <= plan[country] * politics.MAX_PROVINCE_CELLS


# --- towns ----------------------------------------------------------------

def test_one_town_per_province():
    for keyword in KEYWORDS:
        _, _, provinces, towns, _ = layers(keyword)
        assert len(towns) == len(set(provinces.values())), keyword


def test_capitals_follow_the_shipped_convention():
    """7 great-power capitals, 16 minor capitals, the rest villages."""
    for keyword in KEYWORDS:
        _, owner, _, towns, _ = layers(keyword)
        by_marker = {}
        for cell, marker in towns.items():
            by_marker.setdefault(marker, set()).add(owner[cell])
        assert by_marker[politics.CAPITAL] == set(range(7)), keyword
        assert by_marker[politics.MINOR_CAPITAL] == set(range(7, 23)), keyword
        assert sum(1 for m in towns.values() if m == politics.VILLAGE) == 97


# --- locking --------------------------------------------------------------

def test_a_locked_country_keeps_every_cell_when_the_world_is_rebuilt():
    """Refining a home country then regenerating must not move its ground."""
    geom, owner, _, _, _ = layers("Pippin")
    home = {cell: c for cell, c in owner.items() if c == 0}
    generator = rng.generator("Otto")
    cells = world.generate_geography(rng.generator("Pippin"), WIDTH, HEIGHT)
    land = {c for c, plan in cells.items() if plan["terrain"] != world.OCEAN}
    again, _ = politics.assign_countries(generator, geom, land, locked=home)
    for cell in home:
        assert again[cell] == 0, cell


# --- the assembled map ----------------------------------------------------

def template():
    maps = [p for p in originals.maps() if os.path.basename(p) == "s1.map"]
    return MapFile.load(maps[0]) if maps else None


def test_a_generated_map_is_the_right_size_and_validates():
    tpl = template()
    if tpl is None:
        return
    result = build.generate_world("Pippin", template=tpl)["map"]
    assert len(result.to_bytes()) == len(tpl.to_bytes())
    assert validate.check(result) == []


def test_the_province_table_points_at_the_towns():
    tpl = template()
    if tpl is None:
        return
    result = build.generate_world("Pippin", template=tpl)["map"]
    assert result.province_towns() == anchors.province_anchors(result)
    assert len(result.province_towns()) == 120


def test_the_inherited_table_is_changed_only_where_we_understand_it():
    tpl = template()
    if tpl is None:
        return
    result = build.generate_world("Pippin", template=tpl)["map"]
    differing = [i for i, (a, b) in
                 enumerate(zip(tpl.dormant_trailer, result.dormant_trailer))
                 if a != b]
    assert differing, "the table should have been rewritten at all"
    assert all(i % tpl.profile.trailer_record_size in (4, 5) for i in differing)


def test_the_same_keyword_builds_the_same_map():
    tpl = template()
    if tpl is None:
        return
    first = build.generate_world("Pippin", template=tpl)["map"].to_bytes()
    assert build.generate_world("Pippin", template=tpl)["map"].to_bytes() == first
    assert build.generate_world("Otto", template=tpl)["map"].to_bytes() != first


def test_a_generated_map_needs_no_derivation_fixing():
    """Derived bytes are computed by `derive`, so recomputing changes nothing."""
    from imperialism_format import derive
    tpl = template()
    if tpl is None:
        return
    result = build.generate_world("Pippin", template=tpl)["map"]
    before = result.to_bytes()
    derive.apply_edits(result, [(x, y) for y in range(result.height)
                                for x in range(result.width)])
    assert result.to_bytes() == before


def test_the_finished_map_keeps_every_invariant():
    """The properties that matter, checked on the assembled map across seeds."""
    tpl = template()
    if tpl is None:
        return
    from imperialism_format.derive import HexGeometry
    for keyword in ("Pippin", "Zimm", "Kathay"):
        result = build.generate_world(keyword, template=tpl)["map"]
        geom = HexGeometry(result.width, result.height)
        by_province, by_country, per_country = {}, {}, {}
        for y in range(result.height):
            for x in range(result.width):
                cell = result.get(x, y)
                if cell.terrain == world.OCEAN:
                    continue
                by_province.setdefault(cell.province, set()).add((x, y))
                by_country.setdefault(cell.nation_zone_a, set()).add((x, y))
                per_country.setdefault(cell.nation_zone_a, set()).add(cell.province)

        assert sorted(by_province) == list(range(len(by_province))), keyword
        assert len(by_province) == 120, keyword
        assert sorted((len(v) for v in per_country.values()), reverse=True) ==             politics.default_plan(), keyword
        assert max(len(v) for v in by_province.values()) <= 20, keyword
        assert not [k for k, n in pieces(geom, by_province).items() if n != 1]
        assert not [k for k, n in pieces(geom, by_country).items() if n != 1]
        assert sum(1 for c in result.cells if c.town_type) == 120, keyword
        assert validate.check(result) == [], keyword


def test_ocean_carries_no_province_and_land_always_does():
    tpl = template()
    if tpl is None:
        return
    result = build.generate_world("Pippin", template=tpl)["map"]
    for cell in result.cells:
        if cell.terrain == world.OCEAN:
            assert cell.province == politics.NO_PROVINCE
        else:
            assert cell.province != politics.NO_PROVINCE
