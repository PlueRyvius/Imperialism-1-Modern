import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "src"))
sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "tools", "map_editor"))

from imperialism_format import MapFile, ScenarioFile, ScenarioInfo
from imperialism_format.generate import build, politics, scenario as scenario_mod

import originals
import validate

KEYWORDS = ("Pippin", "Zimm", "Kathay")


def template():
    found = [p for p in originals.maps() if os.path.basename(p) == "s1.map"]
    return MapFile.load(found[0]) if found else None


def world(keyword="Pippin", **kwargs):
    tpl = template()
    return None if tpl is None else build.generate_world(
        keyword, template=tpl, **kwargs)


def tags(scn):
    counts = {}
    for record in scn.records:
        counts[record.tag] = counts.get(record.tag, 0) + 1
    return counts


# --- the record set -------------------------------------------------------

def test_every_country_and_province_is_named():
    result = world()
    if result is None:
        return
    counts = tags(result["scenario"])
    assert counts["cnam"] == 23
    assert counts["pnam"] == 120


def test_each_country_has_a_capital_named_after_it():
    """A random map names capitals "(country) City" - README.TXT XI-B."""
    result = world()
    if result is None:
        return
    scn = result["scenario"]
    names = {r.fields[0]: r.name for r in scn.records if r.tag == "cnam"}
    capitals = {r.name for r in scn.records
                if r.tag == "pnam" and r.name.endswith(" City")}
    for country in range(politics.GREAT_POWERS):
        assert f"{names[country]} City" in capitals


def test_no_ship_records_are_written():
    """A fleet's zone id has no known relation to the map's ocean numbering."""
    result = world()
    if result is None:
        return
    assert "ship" not in tags(result["scenario"])


def test_the_powers_get_an_economy_and_the_minors_do_not():
    result = world()
    if result is None:
        return
    scn = result["scenario"]
    for tag in ("cash", "tran", "labo", "tclr"):
        holders = {r.fields[0] for r in scn.records if r.tag == tag}
        assert holders == set(range(politics.GREAT_POWERS)), tag
    assert tags(scn)["capa"] == politics.GREAT_POWERS * scenario_mod.INDUSTRIES


def test_every_country_pair_has_a_standing():
    result = world()
    if result is None:
        return
    counts = tags(result["scenario"])
    assert counts["rela"] == 23 * 22 // 2
    assert counts["emba"] == politics.GREAT_POWERS * 16


def test_every_province_has_a_garrison():
    result = world()
    if result is None:
        return
    scn = result["scenario"]
    garrisoned = {r.fields[0] for r in scn.records if r.tag == "army"}
    assert garrisoned == set(range(120))


def test_the_era_decides_the_flag_and_the_unit_roster():
    for turns, flag, expected in ((5, 1, "Minutemen"), (67, 3, "Militia")):
        result = world("Pippin", turns=turns)
        if result is None:
            return
        scn = result["scenario"]
        assert next(r.fields[0] for r in scn.records if r.tag == "flag") == flag
        assert next(r.fields[0] for r in scn.records if r.tag == "year") == turns
        types = {r.fields[1] for r in scn.records if r.tag == "army"}
        from imperialism_format.constants import ARMY_UNIT_TYPE
        assert expected in {ARMY_UNIT_TYPE[t] for t in types}


CELL_FIELD = {"deve": 0, "rail": 0, "port": 0, "civi": 1}


def _works_on_minor_cells(map_file, scn):
    """Work records sitting on a cell owned by a minor nation (7-22)."""
    stray = []
    for record in scn.records:
        field = CELL_FIELD.get(record.tag)
        if field is None:
            continue
        cell = map_file.cells[record.fields[field]]
        if cell.nation_zone_a >= politics.GREAT_POWERS:
            stray.append((record.tag, record.fields[field], cell.nation_zone_a))
    return stray


def _water_access(map_file, geom, x, y):
    """Coastal, on a river, or landlocked — how a cell reaches the sea."""
    cell = map_file.get(x, y)
    if any(map_file.get(*p).terrain == 0 for p in geom.neighbours(x, y) if p):
        return "coastal"
    return "river" if cell.river else "landlocked"


def test_no_shipped_scenario_has_a_landlocked_capital():
    """The corpus rule, checked before it is enforced on generated worlds.

    184 capitals across eight scenarios, Great Power and minor alike, every one
    coastal or on a river. A capital is where a nation's port grows from, so a
    landlocked one has no dock and no access to naval trade.
    """
    from imperialism_format.derive import geometry_for
    checked = 0
    for map_path, _ in originals.scenarios():
        m = MapFile.load(map_path)
        geom = geometry_for(m)
        for y in range(m.height):
            for x in range(m.width):
                if m.get(x, y).town_type not in (33, 35):
                    continue
                assert _water_access(m, geom, x, y) != "landlocked", \
                    f"{os.path.basename(map_path)}: capital at {x},{y}"
                checked += 1
    assert checked == 0 or checked > 150


def test_every_great_power_capital_reaches_the_sea():
    """What a real launch found missing: all 23 capitals inland, no docks.

    Enforced for the seven Great Powers. A minor can still come out landlocked
    when its territory touches no coast at all — the shipped worlds avoid that
    with rivers, which we do not generate yet (see docs/handoff.md).
    """
    from imperialism_format.derive import geometry_for
    for keyword in KEYWORDS + ("Ryvius",):
        result = world(keyword)
        if result is None:
            return
        m = result["map"]
        geom = geometry_for(m)
        seats = [(x, y) for y in range(m.height) for x in range(m.width)
                 if m.get(x, y).town_type == 35]
        assert len(seats) == politics.GREAT_POWERS, f"{keyword}: {len(seats)}"
        for x, y in seats:
            assert _water_access(m, geom, x, y) != "landlocked", \
                f"{keyword}: landlocked capital at {x},{y}"


def test_no_port_record_stands_inland():
    """Every one of the 124 shipped ports is coastal or on a river."""
    from imperialism_format.derive import geometry_for
    for map_path, scn_path in originals.scenarios():
        m = MapFile.load(map_path)
        geom = geometry_for(m)
        for record in ScenarioFile.load(scn_path).records:
            if record.tag != "port":
                continue
            index = record.fields[0]
            x, y = index % m.width, index // m.width
            assert _water_access(m, geom, x, y) != "landlocked", \
                f"{os.path.basename(scn_path)}: port at {x},{y}"

    for keyword in KEYWORDS:
        result = world(keyword)
        if result is None:
            return
        m = result["map"]
        geom = geometry_for(m)
        for record in result["scenario"].records:
            if record.tag != "port":
                continue
            index = record.fields[0]
            x, y = index % m.width, index // m.width
            assert _water_access(m, geom, x, y) != "landlocked", \
                f"{keyword}: port at {x},{y}"


def test_no_port_stands_on_high_ground():
    """No shipped port sits on wool hill, hill or mountain."""
    for map_path, scn_path in originals.scenarios():
        m = MapFile.load(map_path)
        for record in ScenarioFile.load(scn_path).records:
            if record.tag != "port":
                continue
            terrain = m.cells[record.fields[0]].terrain
            assert terrain not in scenario_mod.PORT_EXCLUDED_TERRAIN, \
                f"{os.path.basename(scn_path)}: port on terrain {terrain}"

    for keyword in KEYWORDS:
        result = world(keyword)
        if result is None:
            return
        m = result["map"]
        for record in result["scenario"].records:
            if record.tag != "port":
                continue
            terrain = m.cells[record.fields[0]].terrain
            assert terrain not in scenario_mod.PORT_EXCLUDED_TERRAIN, \
                f"{keyword}: port on terrain {terrain}"


def test_ports_belong_to_great_powers_and_are_few():
    """0-3 per Great Power, none for a minor, as the shipped worlds place them."""
    for keyword in KEYWORDS:
        result = world(keyword)
        if result is None:
            return
        m = result["map"]
        counts = {}
        for record in result["scenario"].records:
            if record.tag != "port":
                continue
            nation = m.cells[record.fields[0]].nation_zone_a
            assert nation < politics.GREAT_POWERS, f"{keyword}: minor {nation}"
            counts[nation] = counts.get(nation, 0) + 1
        assert all(n <= 3 for n in counts.values()), f"{keyword}: {counts}"


def test_every_finished_country_is_a_single_blob():
    """No nation ships in disconnected pieces.

    Asserted on the finished world, not on the politics layer: the stages that
    sink unusable land can bisect a country, and `build._split_fragments` is
    what puts it right. A detached enclave cannot be walked to, which is a
    complaint a real launch produced.
    """
    from imperialism_format.derive import HexGeometry
    for keyword in KEYWORDS + ("Ryvius",):
        result = world(keyword)
        if result is None:
            return
        owner = result["owner"]
        geom = HexGeometry(result["map"].width, result["map"].height)
        by_country = {}
        for cell, country in owner.items():
            by_country.setdefault(country, set()).add(cell)
        for country, cells in by_country.items():
            seen, blobs = set(), 0
            for start in cells:
                if start in seen:
                    continue
                blobs += 1
                stack = [start]
                seen.add(start)
                while stack:
                    current = stack.pop()
                    for neighbour in geom.neighbours(*current):
                        if neighbour in cells and neighbour not in seen:
                            seen.add(neighbour)
                            stack.append(neighbour)
            assert blobs == 1, f"{keyword}: country {country} in {blobs} pieces"


def test_no_shipped_scenario_puts_a_work_on_a_minor_nations_cell():
    """The rule the generator is held to, checked against shipped data first.

    A rule that fires on an original is a wrong rule. This one is silent on
    all of them, which is what makes it safe to enforce below.
    """
    checked = 0
    for map_path, scn_path in originals.scenarios():
        stray = _works_on_minor_cells(MapFile.load(map_path),
                                      ScenarioFile.load(scn_path))
        assert not stray, f"{os.path.basename(scn_path)} has {stray[:5]}"
        checked += 1
    assert checked == 0 or checked >= 8


def test_a_generated_world_puts_no_work_on_a_minor_nations_cell():
    """Why: the engine indexes a 7-slot Great Power table with the owner of a
    work's cell, unguarded, and faults at `0051465C` when it is a minor."""
    for keyword in KEYWORDS:
        result = world(keyword)
        if result is None:
            return
        stray = _works_on_minor_cells(result["map"], result["scenario"])
        assert not stray, f"{keyword}: {stray[:5]}"


def test_the_army_block_has_the_shape_the_games_own_generator_writes():
    """`army` is a fixed three-role pattern, not a draw over the roster.

    Measured on `s11` and `s15`, the shipped generated worlds whose army block
    is unedited. A random draw over the era roster once put a type-3 record in
    a generated world -- a value no shipped scenario carries in this field.
    """
    result = world()
    if result is None:
        return
    army = [r.fields for r in result["scenario"].records if r.tag == "army"]
    by_type = {}
    for province, unit, count in army:
        by_type.setdefault(unit, []).append((province, count))

    assert sorted(by_type) == [0, 2, 7], "1820 fields exactly three roles"
    # One garrison per province, at a fixed strength.
    assert sorted(p for p, _ in by_type[0]) == list(range(120))
    assert {c for _, c in by_type[0]} == {4}
    # One of each capital role per capital, and the same capitals for both.
    assert {p for p, _ in by_type[2]} == {p for p, _ in by_type[7]}
    assert len(by_type[2]) == politics.GREAT_POWERS + 16
    assert {c for _, c in by_type[7]} == {1}
    # Powers keep a smaller capital garrison than minors do.
    strengths = sorted(c for _, c in by_type[2])
    assert strengths.count(2) == politics.GREAT_POWERS
    assert strengths.count(4) == 16
    assert len(army) == 120 + 2 * (politics.GREAT_POWERS + 16)


def test_the_generated_army_block_matches_the_shipped_generated_worlds():
    """The same shape, read straight off `s11`/`s15`. Skips if absent."""
    shipped = [path for name, path in originals.scenarios()
               if os.path.basename(path) in ("s11.scn", "s15.scn")]
    if not shipped:
        return

    def shape(records):
        out = {}
        for province, unit, count in records:
            out.setdefault(unit, []).append(count)
        return {u: sorted(cs) for u, cs in out.items()}

    result = world()
    if result is None:
        return
    ours = shape([r.fields for r in result["scenario"].records if r.tag == "army"])
    for path in shipped:
        theirs = shape([r.fields for r in ScenarioFile.load(path).records
                        if r.tag == "army"])
        assert ours == theirs, f"army block differs from {os.path.basename(path)}"


def test_an_1882_world_fields_no_napoleonic_infantry():
    result = world("Pippin", turns=67)
    if result is None:
        return
    types = {r.fields[1] for r in result["scenario"].records if r.tag == "army"}
    assert min(types) >= 8, "1882 should not field Minutemen"


# --- consistency with the map --------------------------------------------

def test_the_generated_scenario_agrees_with_its_map():
    """The cross-file checks, which are what a stranded unit trips."""
    for keyword in KEYWORDS:
        result = world(keyword)
        if result is None:
            return
        assert validate.check(result["map"], result["scenario"]) == [], keyword


def test_nothing_is_placed_on_water():
    for keyword in KEYWORDS:
        result = world(keyword)
        if result is None:
            return
        m = result["map"]
        for record in result["scenario"].records:
            field = {"deve": 0, "rail": 0, "port": 0, "civi": 1}.get(record.tag)
            if field is None:
                continue
            index = record.fields[field]
            x, y = index % m.width, index // m.width
            assert m.get(x, y).terrain != 0, (keyword, record.tag)


# --- the briefing ---------------------------------------------------------

def test_the_briefing_has_the_shape_the_game_expects():
    result = world()
    if result is None:
        return
    info = result["info"]
    assert info.title
    assert len(info.country_sections) == 7
    assert len(info.metadata) == 8
    assert all(-1 <= v <= 6 for v in info.metadata)


def test_the_briefing_survives_being_written_as_cp1252():
    """The file is cp1252; a stray dash would land in it as a question mark."""
    for keyword in KEYWORDS:
        result = world(keyword)
        if result is None:
            return
        text = result["info"].to_text()
        assert text.encode("cp1252", errors="replace").decode("cp1252") == text


# --- writing --------------------------------------------------------------

def test_saving_writes_all_three_files(tmp_path):
    result = world()
    if result is None:
        return
    written = build.save_world(result, str(tmp_path / "s5"))
    assert [os.path.basename(p) for p in written] == ["s5.map", "s5.scn", "s5.inf"]

    reloaded = MapFile.load(written[0])
    scn = ScenarioFile.load(written[1])
    info = ScenarioInfo.load(written[2])
    assert len(reloaded.to_bytes()) == 309312
    assert scn.to_bytes() == open(written[1], "rb").read()
    assert info.to_text().encode("cp1252") == open(written[2], "rb").read()
    assert validate.check(reloaded, scn) == []


def test_the_same_keyword_writes_the_same_scenario():
    first, second = world("Pippin"), world("Pippin")
    if first is None:
        return
    assert first["scenario"].to_bytes() == second["scenario"].to_bytes()
    assert first["info"].to_text() == second["info"].to_text()
    assert world("Otto")["scenario"].to_bytes() != first["scenario"].to_bytes()


def test_names_are_never_reused_within_a_world():
    result = world()
    if result is None:
        return
    scn = result["scenario"]
    for tag in ("cnam", "pnam"):
        names = [r.name for r in scn.records if r.tag == tag]
        assert len(names) == len(set(names)), tag


# --- the two ways a generated world has crashed the game ------------------

def test_the_ocean_is_partitioned_into_many_zones():
    """One zone for the whole sea crashed the engine in UOcean.cpp.

    Numbering by connected sea body gives exactly that, because the ocean is a
    single body in every shipped map. The real ones carve it into 42-61
    regions regardless of connectivity.
    """
    for keyword in KEYWORDS:
        result = world(keyword)
        if result is None:
            return
        m = result["map"]
        ids = {c.nation_zone_a for c in m.cells if c.terrain == 0}
        assert 30 <= len(ids) <= 61, f"{keyword}: {len(ids)} sea zones"
        sizes = {}
        for cell in m.cells:
            if cell.terrain == 0:
                sizes[cell.nation_zone_a] = sizes.get(cell.nation_zone_a, 0) + 1
        assert max(sizes.values()) <= 200, f"{keyword}: {max(sizes.values())}"


def test_every_country_gets_a_port_city_not_just_the_minors():
    """Ships anchor at a port city named in the zone table.

    `s9`/`s11`/`s15` each close their zone table with one per country, all 23:
    seven ordinary harbour names for the Great Powers then sixteen
    "<country> City" for the minors. Naming only the minors left every Great
    Power -- including the played one -- with no dock, which is what a real
    launch showed.
    """
    for keyword in KEYWORDS:
        result = world(keyword)
        if result is None:
            return
        names = result["names"]
        zones = sorted((r.fields[0], r.name)
                       for r in result["scenario"].records if r.tag == "zone")
        ports = zones[-len(names):]

        minor_ports = [n for _, n in ports if n.endswith(" City")]
        assert len(ports) == len(names), f"{keyword}: {len(ports)} port cities"
        assert len(minor_ports) == len(names) - politics.GREAT_POWERS
        # Every minor is represented, and no Great Power is named this way.
        for country in names[politics.GREAT_POWERS:]:
            assert f"{country} City" in minor_ports, f"{keyword}: {country}"
        for country in names[:politics.GREAT_POWERS]:
            assert f"{country} City" not in minor_ports
        assert len({n for _, n in ports}) == len(ports), "a repeated port name"


def test_the_port_block_matches_the_shipped_generated_worlds():
    """Same tail shape as `s9`/`s11`/`s15`: 7 plain names then 16 "X City"."""
    shipped = [p for _, p in originals.scenarios()
               if os.path.basename(p) in ("s9.scn", "s11.scn", "s15.scn")]
    if not shipped:
        return

    def tail_shape(records):
        # Case-insensitively: `s11` ships "Issa city", lowercase. Matching on
        # " City" exactly made this fire on shipped data, which means the rule
        # was wrong, not the file.
        zones = sorted((r.fields[0], r.name) for r in records if r.tag == "zone")
        tail = zones[-23:]
        return [n.lower().endswith(" city") for _, n in tail]

    expected = [False] * 7 + [True] * 16
    for path in shipped:
        assert tail_shape(ScenarioFile.load(path).records) == expected, \
            f"{os.path.basename(path)} does not have the assumed tail"

    result = world()
    if result is None:
        return
    assert tail_shape(result["scenario"].records) == expected


def test_every_ocean_id_has_a_zone_record():
    """A short zone table is a lookup off the end of it.

    Each shipped generated world runs its zone records from 0 to the highest
    ocean byte exactly, so any id the map uses lands inside the table.
    """
    for keyword in KEYWORDS:
        result = world(keyword)
        if result is None:
            return
        m, scn = result["map"], result["scenario"]
        used = {c.nation_zone_a for c in m.cells if c.terrain == 0}
        declared = {r.fields[0] for r in scn.records if r.tag == "zone"}
        assert used <= declared, f"{keyword}: {sorted(used - declared)}"
        assert declared == set(range(max(declared) + 1)), f"{keyword}: gaps"


def test_generated_cell_values_stay_inside_what_the_originals_use():
    """A value no shipped map contains is a value the engine may not handle."""
    tpl = template()
    if tpl is None:
        return
    from imperialism_format.map_file import HexCell
    seen = {f: set() for f in HexCell.__dataclass_fields__}
    for path in originals.maps():
        for cell in MapFile.load(path).cells:
            for field in seen:
                seen[field].add(getattr(cell, field))

    # Derived direction masks are exempt: `like_cell_adjacency` uses all 64
    # values in the shipped maps, so the engine handles arbitrary masks, and
    # our coastlines legitimately produce arrangements theirs do not.
    derived = {"land_coastline", "province_border", "national_border",
               "like_cell_adjacency", "ocean_coastline", "hill_mountain_overlay"}
    result = build.generate_world("Pippin", template=tpl)["map"]
    for cell in result.cells:
        for field, allowed in seen.items():
            if field in derived:
                continue
            assert getattr(cell, field) in allowed, (field, getattr(cell, field))
