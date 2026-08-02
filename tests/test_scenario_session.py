import glob
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "src"))
sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "tools", "map_editor"))

from imperialism_format import HexCell, MapFile, MapFormatProfile, ScenarioFile
from imperialism_format.inf_file import ScenarioInfo

import validate
from scenario_session import BASE_YEAR, ScenarioSession

import originals

FIXTURE_DIR = originals.FIXTURE_DIR

INFO_TEXT = (
    "Test Scenario\r#\rAn overview.\r#\rOne\r#\rTwo\r#\rThree\r#\rFour\r"
    "#\rFive\r#\rSix\r#\rSeven\r# 1 -1 -1 -1 -1 -1 -1 0\r"
)


def build_scenario(tmp_path, width=6, height=6):
    """A minimal but complete scenario: map, .scn and .inf sharing a stem."""
    profile = MapFormatProfile(width=width, height=height,
                               trailer_record_count=0, trailer_record_size=0)
    m = MapFile.blank(profile)
    for y in range(height):
        for x in range(width):
            m.set(x, y, HexCell(terrain=1, terrain_underlay=0, province=3,
                                nation_zone_a=1))
    m.save(str(tmp_path / "s1.map"))

    scn = ScenarioFile()
    scn.add("cnam", 0, name="France")
    scn.add("cnam", 1, name="Austria")
    scn.add("pnam", 3, name="Brittany")
    scn.add("zone", 40, name="Chatham")
    scn.add("cash", 0, 5000)
    scn.add("year", 67)
    scn.add("port", 8)          # cell 8 = (2,1), land
    scn.save(str(tmp_path / "s1.scn"))

    (tmp_path / "s1.inf").write_bytes(INFO_TEXT.encode("cp1252"))
    return ScenarioSession.open(str(tmp_path / "s1.map"), wrap_x=False,
                                profile=profile)


# --- opening --------------------------------------------------------------

def test_opens_map_with_its_companions(tmp_path):
    s = build_scenario(tmp_path)
    assert s.scenario is not None and s.info is not None
    assert s.summary()["present"] and s.info_dict()["present"]


def test_a_map_with_no_companions_still_opens(tmp_path):
    profile = MapFormatProfile(width=4, height=4, trailer_record_count=0,
                               trailer_record_size=0)
    MapFile.blank(profile).save(str(tmp_path / "lonely.map"))
    s = ScenarioSession.open(str(tmp_path / "lonely.map"), profile=profile)
    assert s.scenario is None
    assert s.summary() == {"present": False}
    assert s.info_dict() == {"present": False}


def test_summary_reports_the_calendar_year(tmp_path):
    s = build_scenario(tmp_path)
    assert s.summary()["year"] == {"turns": 67, "calendar": BASE_YEAR + 67}


def test_summary_sorts_names_by_id(tmp_path):
    s = build_scenario(tmp_path)
    assert [c["id"] for c in s.summary()["countries"]] == [0, 1]


# --- scenario edits -------------------------------------------------------

def test_renames_a_country(tmp_path):
    s = build_scenario(tmp_path)
    s.apply_scenario([{"tag": "cnam", "id": 1, "field": "name", "value": "Austria-Hungary"}])
    assert s.find_record("cnam", 1).name == "Austria-Hungary"


def test_records_are_addressed_by_id_not_position(tmp_path):
    """`zone` records are not stored in id order and `pnam` ids are sparse."""
    s = build_scenario(tmp_path)
    s.apply_scenario([{"tag": "pnam", "id": 3, "field": "name", "value": "Bretagne"}])
    assert s.find_record("pnam", 3).name == "Bretagne"
    try:
        s.find_record("pnam", 4)
        assert False, "expected ValueError for an id that does not exist"
    except ValueError:
        pass


def test_edits_a_numeric_field(tmp_path):
    s = build_scenario(tmp_path)
    s.apply_scenario([{"tag": "cash", "id": 0, "field": "amount", "value": 9999}])
    assert s.find_record("cash", 0).fields[1] == 9999


def test_a_single_record_tag_needs_no_id(tmp_path):
    s = build_scenario(tmp_path)
    s.apply_scenario([{"tag": "year", "field": "turns", "value": 5}])
    assert s.summary()["year"]["calendar"] == BASE_YEAR + 5


def test_rejects_tags_we_do_not_understand(tmp_path):
    s = build_scenario(tmp_path)
    for edit in ({"tag": "flag", "field": "value", "value": 1},
                 {"tag": "cnam", "id": 0, "field": "colour", "value": 2}):
        try:
            s.apply_scenario([edit])
            assert False, f"expected {edit} to be rejected"
        except ValueError:
            pass


def test_rejects_a_name_too_long_for_the_field(tmp_path):
    """Better to refuse than silently truncate at 64 bytes on write."""
    s = build_scenario(tmp_path)
    try:
        s.apply_scenario([{"tag": "cnam", "id": 0, "field": "name", "value": "x" * 65}])
        assert False, "expected ValueError"
    except ValueError as exc:
        assert "64" in str(exc)


def test_a_no_op_scenario_edit_does_not_grow_the_undo_stack(tmp_path):
    s = build_scenario(tmp_path)
    assert s.apply_scenario([{"tag": "cnam", "id": 0, "field": "name",
                              "value": "France"}]) == []
    assert s.undo_stack == []


# --- info edits -----------------------------------------------------------

def test_edits_info_fields(tmp_path):
    s = build_scenario(tmp_path)
    s.apply_info([{"field": "title", "value": "Renamed"}])
    s.apply_info([{"field": "country", "id": 2, "value": "Third briefing"}])
    s.apply_info([{"field": "metadata", "value": [1, 2, 3, 4, 5, 6, 7, 1]}])
    info = s.info_dict()
    assert info["title"] == "Renamed"
    assert info["country_sections"][2] == "Third briefing"
    assert info["metadata"] == [1, 2, 3, 4, 5, 6, 7, 1]


def test_rejects_a_briefing_that_does_not_exist(tmp_path):
    s = build_scenario(tmp_path)
    try:
        s.apply_info([{"field": "country", "id": 99, "value": "nope"}])
        assert False, "expected ValueError"
    except ValueError:
        pass


# --- one undo stack across all three files --------------------------------

def test_undo_walks_back_across_files_in_order(tmp_path):
    s = build_scenario(tmp_path)
    s.apply_map([{"x": 2, "y": 2, "field": "terrain", "value": 9}])
    s.apply_scenario([{"tag": "cnam", "id": 0, "field": "name", "value": "Gaul"}])
    s.apply_info([{"field": "title", "value": "Retitled"}])
    assert len(s.undo_stack) == 3

    s.undo()
    assert s.info.title == "Test Scenario"
    assert s.find_record("cnam", 0).name == "Gaul"      # not yet undone

    s.undo()
    assert s.find_record("cnam", 0).name == "France"
    assert s.map_session.map_file.get(2, 2).terrain == 9

    s.undo()
    assert s.map_session.map_file.get(2, 2).terrain == 1
    assert s.undo() == {}


def test_redo_replays_across_files(tmp_path):
    s = build_scenario(tmp_path)
    s.apply_scenario([{"tag": "cnam", "id": 0, "field": "name", "value": "Gaul"}])
    s.undo()
    s.redo()
    assert s.find_record("cnam", 0).name == "Gaul"


def test_dirty_reports_each_file_separately(tmp_path):
    s = build_scenario(tmp_path)
    assert s.dirty() == {"map": 0, "scenario": False, "info": False}
    s.apply_info([{"field": "title", "value": "Changed"}])
    assert s.dirty()["info"] and not s.dirty()["scenario"]


# --- saving ---------------------------------------------------------------

def test_save_writes_only_what_changed(tmp_path):
    s = build_scenario(tmp_path)
    map_before = open(s.map_session.path, "rb").read()
    s.apply_info([{"field": "title", "value": "Only the info"}])
    written = s.save()
    assert written == [s.info_path]
    assert open(s.map_session.path, "rb").read() == map_before
    assert ScenarioInfo.load(s.info_path).title == "Only the info"


def test_save_backs_each_file_up_once(tmp_path):
    s = build_scenario(tmp_path)
    original = open(s.scenario_path, "rb").read()
    s.apply_scenario([{"tag": "cnam", "id": 0, "field": "name", "value": "Gaul"}])
    s.save()
    assert open(s.scenario_path + ".bak", "rb").read() == original
    s.apply_scenario([{"tag": "cnam", "id": 0, "field": "name", "value": "Francia"}])
    s.save()
    assert open(s.scenario_path + ".bak", "rb").read() == original


def test_save_as_writes_the_whole_scenario_under_one_stem(tmp_path):
    """Typing a name saves the scenario, not just the map."""
    s = build_scenario(tmp_path)
    s.apply_scenario([{"tag": "cnam", "id": 0, "field": "name", "value": "Gaul"}])
    written = s.save_as(str(tmp_path / "s7"))
    assert [os.path.basename(p) for p in written] == ["s7.map", "s7.scn", "s7.inf"]
    for suffix in (".map", ".scn", ".inf"):
        assert os.path.exists(tmp_path / f"s7{suffix}")


def test_save_as_writes_unchanged_files_too(tmp_path):
    """A half-cloned scenario the game cannot load would be worse than none."""
    s = build_scenario(tmp_path)
    s.apply_info([{"field": "title", "value": "Only the briefing changed"}])
    s.save_as(str(tmp_path / "s7"))
    assert os.path.exists(tmp_path / "s7.map")     # untouched, still written
    assert ScenarioFile.load(str(tmp_path / "s7.scn")).records


def test_save_as_retargets_the_whole_session(tmp_path):
    s = build_scenario(tmp_path)
    s.save_as(str(tmp_path / "s7"))
    assert s.map_session.path.endswith("s7.map")
    assert s.scenario_path.endswith("s7.scn")
    assert s.info_path.endswith("s7.inf")

    s.apply_scenario([{"tag": "cnam", "id": 0, "field": "name", "value": "Later"}])
    assert [os.path.basename(p) for p in s.save()] == ["s7.scn"]
    assert ScenarioInfo.load(str(tmp_path / "s1.inf")).title == "Test Scenario"


def test_save_as_leaves_the_scenario_it_came_from_alone(tmp_path):
    s = build_scenario(tmp_path)
    originals = {suffix: open(tmp_path / f"s1{suffix}", "rb").read()
                 for suffix in (".map", ".scn", ".inf")}
    s.apply_scenario([{"tag": "cnam", "id": 0, "field": "name", "value": "Gaul"}])
    s.save_as(str(tmp_path / "s7"))
    for suffix, content in originals.items():
        assert open(tmp_path / f"s1{suffix}", "rb").read() == content


def test_save_as_backs_up_whatever_it_overwrites(tmp_path):
    s = build_scenario(tmp_path)
    victim = tmp_path / "s7.scn"
    victim.write_bytes(b"pre-existing")
    s.save_as(str(tmp_path / "s7"))
    assert open(str(victim) + ".bak", "rb").read() == b"pre-existing"


def test_save_as_clears_the_unsaved_marker(tmp_path):
    s = build_scenario(tmp_path)
    s.apply_info([{"field": "title", "value": "Changed"}])
    s.save_as(str(tmp_path / "s7"))
    assert s.dirty() == {"map": 0, "scenario": False, "info": False}


def test_untouched_files_are_never_rewritten(tmp_path):
    s = build_scenario(tmp_path)
    assert s.save() == []


# --- cross-file validation ------------------------------------------------

def test_catches_a_scenario_record_pointing_into_the_sea(tmp_path):
    s = build_scenario(tmp_path)
    assert validate.check(s.map_session.map_file, s.scenario) == []
    s.apply_map([{"x": 2, "y": 1, "field": "terrain", "value": 0}])   # cell 8
    issues = validate.check(s.map_session.map_file, s.scenario)
    assert any("port points at an ocean cell" in i["message"] for i in issues)


def test_catches_a_cell_index_off_the_map(tmp_path):
    s = build_scenario(tmp_path)
    s.scenario.add("rail", 99999)
    issues = validate.check_cross_file(s.map_session.map_file, s.scenario)
    assert any("outside a 6x6 map" in i["message"] for i in issues)


def test_cross_file_checks_are_silent_on_real_scenarios():
    """The bar that has already caught seven wrong rules in this project.

    Name records are optional labels, not a registry: `s9` names one province
    but places armies in 120, and `s1`'s map uses sea-zone ids up to 78 while
    naming only 0-62. Nothing here may assume otherwise.
    """
    for map_path, scn_path in originals.scenarios():
        issues = validate.check_cross_file(
            MapFile.load(map_path), ScenarioFile.load(scn_path))
        assert issues == [], f"{os.path.basename(scn_path)}: {issues[:3]}"
