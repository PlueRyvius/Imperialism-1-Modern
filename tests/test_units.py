import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "src"))
sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "tools", "map_editor"))

from imperialism_format import HexCell, MapFile, MapFormatProfile, ScenarioFile

import anchors
import originals
from scenario_session import ScenarioSession


def build(tmp_path, width=8, height=8):
    """A small scenario: two provinces, a town in each, a sea to the east."""
    profile = MapFormatProfile(width=width, height=height,
                               trailer_record_count=0, trailer_record_size=0)
    m = MapFile.blank(profile)
    for y in range(height):
        for x in range(width):
            if x >= 6:
                m.set(x, y, HexCell(terrain=0, province=65535, nation_zone_a=40))
            else:
                m.set(x, y, HexCell(terrain=1, terrain_underlay=0,
                                    province=1 if x < 3 else 2,
                                    nation_zone_a=0 if x < 3 else 1))
    m.get(1, 1).terrain, m.get(1, 1).town_type = 14, 34      # town of province 1
    m.get(4, 5).terrain, m.get(4, 5).town_type = 16, 35      # capital of province 2
    m.save(str(tmp_path / "t.map"))

    scn = ScenarioFile()
    scn.add("cnam", 0, name="Alba")
    scn.add("cnam", 1, name="Brenn")
    scn.add("pnam", 1, name="Northshire")
    scn.add("pnam", 2, name="Southmarch")
    scn.add("zone", 4, name="The Deep")
    scn.add("year", 5)
    scn.add("civi", 2, 2 * width + 1)      # Farmer at (1,2)
    scn.add("army", 1, 2, 4)               # 4 Regulars in Northshire
    scn.add("ship", 0, 3, 4, 2)            # 2 Frigates in The Deep
    scn.add("port", 5 * width + 5)         # (5,5)
    scn.add("deve", 3 * width + 2, 2)      # (2,3) level 2
    scn.save(str(tmp_path / "t.scn"))
    return ScenarioSession.open(str(tmp_path / "t.map"), wrap_x=False, profile=profile)


# --- anchors --------------------------------------------------------------

def test_a_province_anchors_on_its_town(tmp_path):
    s = build(tmp_path)
    m = s.map_session.map_file
    found = anchors.province_anchors(m)
    assert found[1] == 1 * m.width + 1        # the town of province 1
    assert found[2] == 5 * m.width + 4        # the capital of province 2


def test_every_anchor_is_land_of_its_own_province(tmp_path):
    s = build(tmp_path)
    m = s.map_session.map_file
    for province, index in anchors.province_anchors(m).items():
        cell = m.get(index % m.width, index // m.width)
        assert cell.province == province and cell.terrain != 0


def test_a_province_with_no_town_still_gets_an_anchor(tmp_path):
    s = build(tmp_path)
    m = s.map_session.map_file
    m.get(1, 1).town_type, m.get(1, 1).terrain = 0, 1
    index = anchors.province_anchors(m)[1]
    assert m.get(index % m.width, index // m.width).province == 1


def test_the_centre_of_a_region_crossing_the_seam_is_not_the_far_side():
    """The map wraps east-west, so a plain mean lands half a world away."""
    members = [(0, 5), (1, 5), (2, 5), (105, 5), (106, 5), (107, 5)]
    x, _ = anchors._centre_of(members, 108)
    assert x in (0, 1, 2, 105, 106, 107), x
    assert not 20 < x < 90


# --- carrying a stranded record -------------------------------------------

def test_carry_prefers_land_in_the_same_province(tmp_path):
    s = build(tmp_path)
    m = s.map_session.map_file
    m.get(1, 2).terrain = 0                    # strand the Farmer's cell
    m.get(1, 2).province = 65535
    target = anchors.carry_target(m, 1, 2, wrap_x=False)
    assert target is not None
    assert m.get(*target).province == 1


def test_carry_falls_back_to_the_same_nation_then_any_land(tmp_path):
    s = build(tmp_path)
    m = s.map_session.map_file
    for y in range(m.height):               # erase province 1 entirely
        for x in range(3):
            m.get(x, y).province = 65535
            m.get(x, y).terrain = 0
    target = anchors.carry_target(m, 1, 2, wrap_x=False)
    assert target is not None and m.get(*target).terrain != 0


def test_carry_gives_up_when_there_is_no_land(tmp_path):
    s = build(tmp_path)
    m = s.map_session.map_file
    for cell in m.cells:
        cell.terrain, cell.province = 0, 65535
    assert anchors.carry_target(m, 1, 2, wrap_x=False) is None


# --- unit summaries -------------------------------------------------------

def test_a_civilian_is_named_by_type_and_owned_by_its_ground(tmp_path):
    s = build(tmp_path)
    unit = s.units()["civilians"][0]
    assert unit["typeName"] == "Farmer"
    assert unit["ownerName"] == "Alba"       # from the cell's nation, not the record
    assert not unit["stranded"]


def test_a_civilian_on_ocean_is_reported_stranded_with_no_owner(tmp_path):
    s = build(tmp_path)
    s.apply_map([{"x": 1, "y": 2, "field": "terrain", "value": 0}])
    unit = s.units()["civilians"][0]
    assert unit["stranded"] and unit["owner"] is None


def test_an_army_resolves_to_its_province_and_town(tmp_path):
    s = build(tmp_path)
    m = s.map_session.map_file
    army = s.units()["armies"][0]
    assert army["provinceName"] == "Northshire"
    assert army["typeName"] == "Regulars" and army["count"] == 4
    assert army["cell"] == 1 * m.width + 1


def test_a_fleet_is_named_but_never_placed(tmp_path):
    """A ship's zone is a `zone` record id; the map numbers oceans differently."""
    s = build(tmp_path)
    ship = s.units()["ships"][0]
    assert ship["zoneName"] == "The Deep"
    assert ship["countryName"] == "Alba"
    assert ship["cell"] is None


def test_infrastructure_carries_its_level(tmp_path):
    s = build(tmp_path)
    kinds = {u["tag"]: u for u in s.units()["infrastructure"]}
    assert kinds["port"]["level"] is None
    assert kinds["deve"]["level"] == 2


def test_the_type_pickers_are_limited_to_the_scenario_era(tmp_path):
    """A 1820 scenario must not offer Ironclads."""
    s = build(tmp_path)
    rosters = s.units()["rosters"]
    assert "Regulars" in rosters["army"].values()
    assert "Siege Artillery" not in rosters["army"].values()
    assert "Frigate" in rosters["ship"].values()
    assert "Ironclad" not in rosters["ship"].values()


def test_at_cell_reports_what_is_placed_there(tmp_path):
    s = build(tmp_path)
    here = s.at_cell(1, 2)
    assert [r["tag"] for r in here] == ["civi"]
    assert s.at_cell(0, 0) == []


# --- record identity ------------------------------------------------------

def test_uids_are_unique_and_resolve_back(tmp_path):
    s = build(tmp_path)
    uids = [s.uid_of(r) for r in s.scenario.records]
    assert len(set(uids)) == len(uids)
    for uid, record in zip(uids, s.scenario.records):
        assert s.record_for(uid) is record


def test_an_unknown_uid_is_refused(tmp_path):
    s = build(tmp_path)
    try:
        s.record_for(9999)
        assert False, "expected ValueError"
    except ValueError:
        pass


def test_uids_survive_an_undo(tmp_path):
    s = build(tmp_path)
    before = {s.uid_of(r): (r.tag, list(r.fields)) for r in s.scenario.records}
    s.apply_scenario([{"tag": "cnam", "id": 0, "field": "name", "value": "Alba II"}])
    s.undo()
    after = {s.uid_of(r): (r.tag, list(r.fields)) for r in s.scenario.records}
    assert before == after


def test_indexing_does_not_disturb_serialisation(tmp_path):
    s = build(tmp_path)
    assert s.scenario.to_bytes() == open(s.scenario_path, "rb").read()


# --- placing records ------------------------------------------------------

def test_moving_a_record_repoints_it(tmp_path):
    s = build(tmp_path)
    m = s.map_session.map_file
    uid = s.units()["civilians"][0]["uid"]
    s.move_record(uid, 2, 4)
    assert s.record_for(uid).fields[1] == 4 * m.width + 2


def test_a_record_cannot_be_moved_onto_water(tmp_path):
    s = build(tmp_path)
    uid = s.units()["civilians"][0]["uid"]
    try:
        s.move_record(uid, 7, 3)
        assert False, "expected ValueError"
    except ValueError as exc:
        assert "province" in str(exc)


def test_a_record_cannot_be_moved_off_the_map(tmp_path):
    s = build(tmp_path)
    uid = s.units()["civilians"][0]["uid"]
    try:
        s.move_record(uid, 99, 99)
        assert False, "expected ValueError"
    except (ValueError, IndexError):
        pass


def test_an_army_cannot_be_moved_as_if_it_had_a_cell(tmp_path):
    """Its marker shows a province, so dragging it would imply false precision."""
    s = build(tmp_path)
    uid = s.units()["armies"][0]["uid"]
    try:
        s.move_record(uid, 2, 4)
        assert False, "expected ValueError"
    except ValueError as exc:
        assert "not placed on a cell" in str(exc)


def test_adding_and_deleting_leaves_the_file_as_it_was(tmp_path):
    s = build(tmp_path)
    before = s.scenario.to_bytes()
    added = s.add_record("port", 2, 4)
    assert s.scenario.to_bytes() != before
    s.delete_record(added["uid"])
    assert s.scenario.to_bytes() == before


def test_a_new_record_gets_a_usable_id(tmp_path):
    s = build(tmp_path)
    added = s.add_record("civi", 2, 4, value=3)
    assert s.record_for(added["uid"]).fields[0] == 3
    assert added["uid"] not in [s.uid_of(r) for r in s.scenario.records
                                if r is not s.record_for(added["uid"])]


def test_placing_is_refused_on_water(tmp_path):
    s = build(tmp_path)
    try:
        s.add_record("rail", 7, 3)
        assert False, "expected ValueError"
    except ValueError:
        pass


# --- stranding pre-check --------------------------------------------------

def test_an_edit_that_would_strand_a_record_is_reported_before_it_lands(tmp_path):
    s = build(tmp_path)
    before = s.map_session.map_file.to_bytes()
    warned = s.would_strand([{"x": 1, "y": 2, "field": "terrain", "value": 0}])
    assert [w["label"] for w in warned] == ["Farmer"]
    assert warned[0]["carryTo"] is not None
    # Checking must not have applied anything.
    assert s.map_session.map_file.to_bytes() == before


def test_clearing_a_province_strands_just_as_much_as_flooding(tmp_path):
    s = build(tmp_path)
    warned = s.would_strand([{"x": 1, "y": 2, "field": "province", "value": 65535}])
    assert [w["label"] for w in warned] == ["Farmer"]


def test_a_harmless_edit_warns_about_nothing(tmp_path):
    s = build(tmp_path)
    assert s.would_strand([{"x": 5, "y": 0, "field": "terrain", "value": 9}]) == []


def test_the_carry_destination_is_computed_before_the_cell_is_ruined(tmp_path):
    """After the paint the cell has no province, so its own province is
    unknowable — which is why the check happens first."""
    s = build(tmp_path)
    m = s.map_session.map_file
    warned = s.would_strand([{"x": 1, "y": 2, "field": "terrain", "value": 0}])
    target = warned[0]["carryTo"]
    assert m.get(*target).province == m.get(1, 2).province


def test_carry_then_paint_leaves_nothing_stranded(tmp_path):
    s = build(tmp_path)
    edits = [{"x": 1, "y": 2, "field": "terrain", "value": 0}]
    for record in s.would_strand(edits):
        s.move_record(record["uid"], *record["carryTo"])
    s.apply_map(edits)
    assert s.would_strand(edits) == []
    assert not any(u["stranded"] for u in s.units()["civilians"])


def test_undo_reverts_a_carry_and_its_paint_together(tmp_path):
    s = build(tmp_path)
    map_before = s.map_session.map_file.to_bytes()
    scn_before = s.scenario.to_bytes()
    edits = [{"x": 1, "y": 2, "field": "terrain", "value": 0}]
    for record in s.would_strand(edits):
        s.move_record(record["uid"], *record["carryTo"])
    s.apply_map(edits)
    while s.undo_stack:
        s.undo()
    assert s.map_session.map_file.to_bytes() == map_before
    assert s.scenario.to_bytes() == scn_before


# --- against real game data -----------------------------------------------

def test_real_scenarios_summarise_without_gaps():
    for map_path, scn_path in originals.scenarios():
        session = ScenarioSession.open(map_path)
        if session.scenario is None:
            continue
        summary = session.units()
        name = os.path.basename(map_path)
        for unit in summary["civilians"]:
            assert unit["typeName"], name
            assert not unit["stranded"], f"{name}: shipped data has no strays"
        for army in summary["armies"]:
            assert army["cell"] is not None, f"{name}: every province has a town"
        # A ship is never placeable; that is a finding, not an omission.
        assert all(s["cell"] is None for s in summary["ships"]), name


def test_loading_a_real_scenario_leaves_it_byte_exact():
    for map_path, scn_path in originals.scenarios():
        session = ScenarioSession.open(map_path)
        if session.scenario is None:
            continue
        session.units()          # exercise the uid map
        assert session.scenario.to_bytes() == open(scn_path, "rb").read(), scn_path
