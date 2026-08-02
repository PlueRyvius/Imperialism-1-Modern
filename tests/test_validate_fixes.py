import glob
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "src"))
sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "tools", "map_editor"))

from imperialism_format import HexCell, MapFile, MapFormatProfile, ScenarioFile

import validate
from session import EDITABLE_FIELDS

import originals

FIXTURE_DIR = originals.FIXTURE_DIR


def small_map(width=6, height=6):
    profile = MapFormatProfile(width=width, height=height,
                               trailer_record_count=0, trailer_record_size=0)
    m = MapFile.blank(profile)
    for y in range(height):
        for x in range(width):
            m.set(x, y, HexCell(terrain=1, terrain_underlay=0, province=3,
                                nation_zone_a=1))
    return m


def apply(map_file, issues):
    """Stand in for the client: fixes are ordinary cell edits."""
    for edit in validate.fix_edits(issues):
        setattr(map_file.get(edit["x"], edit["y"]), edit["field"], edit["value"])


def real_scenarios():
    return originals.scenarios()


# --- what carries a repair ------------------------------------------------

def test_ocean_keeping_a_province_is_fixable():
    m = small_map()
    m.get(2, 2).terrain = 0          # painted to sea, province left behind
    issues = [i for i in validate.check(m) if i["rule"] == "province"]
    assert issues and all(i["fix"] for i in issues)
    apply(m, issues)
    assert m.get(2, 2).province == validate.NO_PROVINCE
    assert not [i for i in validate.check(m) if i["rule"] == "province"]


def test_developed_terrain_is_repaired_by_setting_its_resource():
    """The terrain is the statement of intent, so the resource moves to match."""
    m = small_map()
    m.get(1, 1).terrain = 5          # grain farm with no grain
    issues = [i for i in validate.check(m) if i["rule"] == "resource"]
    apply(m, issues)
    assert m.get(1, 1).resource_a == 17
    assert m.get(1, 1).terrain == 5


def test_ocean_carrying_land_attributes_is_cleared():
    m = small_map()
    cell = m.get(3, 3)
    cell.terrain, cell.province = 0, validate.NO_PROVINCE
    cell.resource_a, cell.rail = 3, 12      # coal and a railway at sea
    apply(m, validate.check(m))
    assert cell.resource_a == 255 and cell.rail == 0


def test_an_orphaned_second_resource_is_cleared_not_promoted():
    m = small_map()
    cell = m.get(2, 3)
    cell.terrain, cell.resource_a, cell.resource_b = 9, 255, 22
    apply(m, validate.check(m))
    assert cell.resource_b == 255
    assert cell.resource_a == 255, "must not invent a base deposit"


# --- what deliberately carries none ---------------------------------------

def test_land_with_no_province_is_not_guessed_at():
    m = small_map()
    m.get(2, 2).province = validate.NO_PROVINCE
    issue = next(i for i in validate.check(m) if i["message"] == "land cell has no province")
    assert not issue["fix"]
    assert "213" in issue["why"]


def test_unknown_terrain_is_not_guessed_at():
    m = small_map()
    m.get(2, 2).terrain = 99
    issue = next(i for i in validate.check(m) if i["rule"] == "terrain")
    assert not issue["fix"] and issue["why"]


def test_a_record_stranded_at_sea_is_not_guessed_at():
    """Restoring the land and moving the record are different maps."""
    m = small_map()
    scenario = ScenarioFile()
    scenario.add("port", 8)
    m.get(2, 1).terrain = 0                    # cell 8
    issue = next(i for i in validate.check(m, scenario) if i["rule"] == "scenario")
    assert not issue["fix"] and issue["why"]


def test_every_issue_either_fixes_or_explains():
    """No dead end: an issue with no repair must say what it needs from you."""
    m = small_map()
    m.get(0, 0).terrain = 99
    m.get(1, 0).province = validate.NO_PROVINCE
    m.get(2, 0).terrain = 0
    m.get(3, 0).nation_zone_a = 200
    scenario = ScenarioFile()
    scenario.add("civi", 2, 12)
    for issue in validate.check(m, scenario):
        assert issue["fix"] or issue["why"], issue


# --- the repairs have to be applicable ------------------------------------

def test_fixes_only_touch_fields_the_editor_accepts():
    """Repairs travel the ordinary edit path, which rejects derived fields."""
    m = small_map()
    m.get(2, 2).terrain = 0
    m.get(1, 1).terrain = 5
    m.get(3, 3).resource_b = 22
    for edit in validate.fix_edits(validate.check(m)):
        assert edit["field"] in EDITABLE_FIELDS, edit
        assert {"x", "y", "field", "value"} == set(edit)


def test_merging_keeps_one_edit_per_cell_and_field():
    issues = [
        {"fix": [{"x": 1, "y": 1, "field": "resource_a", "value": 1}]},
        {"fix": [{"x": 1, "y": 1, "field": "resource_a", "value": 2}]},
        {"fix": [{"x": 1, "y": 1, "field": "province", "value": 3}]},
    ]
    edits = validate.fix_edits(issues)
    assert len(edits) == 2
    assert {"x": 1, "y": 1, "field": "resource_a", "value": 2} in edits


def test_fixing_converges_on_a_clean_map():
    """Applying every repair must not leave, or create, a fixable issue."""
    m = small_map()
    m.get(2, 2).terrain = 0                      # sea keeping its province
    m.get(1, 1).terrain = 5                      # farm with no grain
    m.get(4, 4).terrain, m.get(4, 4).rail = 0, 9
    m.get(4, 4).province = validate.NO_PROVINCE
    apply(m, validate.check(m))
    assert not validate.fixable(validate.check(m))


# --- and must never fire on shipped data ----------------------------------

def test_no_repairs_are_offered_on_unmodified_scenarios():
    for map_path, scn_path in real_scenarios():
        issues = validate.check(MapFile.load(map_path), ScenarioFile.load(scn_path))
        assert validate.fixable(issues) == [], os.path.basename(map_path)
