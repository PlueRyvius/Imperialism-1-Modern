import glob
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "src"))
sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "tools", "map_editor"))

from imperialism_format import HexCell, MapFile, MapFormatProfile

import dialogs
import validate
from session import MapSession

import originals

FIXTURE_DIR = originals.FIXTURE_DIR


def real_maps():
    """Original .map files to test against.

    Set IMP_SCENARIO_DIR to a game install's Scenario folder to check all ten
    without copying copyrighted data into the repo. See tests/originals.py for
    why an edited map is replaced by its .bak.
    """
    return originals.maps()


def small_session(tmp_path, width=6, height=6):
    profile = MapFormatProfile(
        width=width, height=height, trailer_record_count=2, trailer_record_size=8
    )
    m = MapFile.blank(profile)
    for y in range(height):
        for x in range(width):
            m.set(x, y, HexCell(terrain=1, terrain_underlay=0, province=3,
                                nation_zone_a=1))
    m.dormant_trailer = bytes(range(16))
    path = tmp_path / "test.map"
    m.save(str(path))
    return MapSession.open(str(path), wrap_x=False, profile=profile)


# --- editing --------------------------------------------------------------

def test_edit_updates_the_cell_and_reports_it(tmp_path):
    s = small_session(tmp_path)
    changed = s.apply([{"x": 2, "y": 2, "field": "terrain", "value": 9}])
    assert (2, 2) in changed
    assert s.map_file.get(2, 2).terrain == 9


def test_edit_also_reports_neighbours_whose_derived_bytes_moved(tmp_path):
    s = small_session(tmp_path)
    s.apply([{"x": 2, "y": 2, "field": "province", "value": 4}])
    changed = set(s.apply([{"x": 2, "y": 2, "field": "province", "value": 5}]))
    # Neighbours' province_border does not change (still "different"), but the
    # edited cell must be reported.
    assert (2, 2) in changed
    # A terrain change to ocean must give neighbours a coastline.
    changed = set(s.apply([{"x": 2, "y": 2, "field": "terrain", "value": 0}]))
    assert len(changed) > 1
    assert s.map_file.get(2, 1).land_coastline != 0


def test_rejects_edits_to_derived_and_unknown_fields(tmp_path):
    s = small_session(tmp_path)
    for field in ("national_border", "unused_14", "nonsense"):
        try:
            s.apply([{"x": 1, "y": 1, "field": field, "value": 1}])
            assert False, f"expected {field} to be rejected"
        except ValueError:
            pass


def test_no_op_edit_does_not_grow_the_undo_stack(tmp_path):
    s = small_session(tmp_path)
    s.apply([{"x": 2, "y": 2, "field": "terrain", "value": 9}])
    depth = len(s.undo_stack)
    assert s.apply([{"x": 2, "y": 2, "field": "terrain", "value": 9}]) == []
    assert len(s.undo_stack) == depth


# --- undo / redo ----------------------------------------------------------

def test_undo_restores_every_cell_the_edit_touched(tmp_path):
    s = small_session(tmp_path)
    before = s.map_file.to_bytes()
    s.apply([{"x": 3, "y": 3, "field": "terrain", "value": 0}])
    assert s.map_file.to_bytes() != before
    s.undo()
    assert s.map_file.to_bytes() == before


def test_redo_reapplies(tmp_path):
    s = small_session(tmp_path)
    s.apply([{"x": 3, "y": 3, "field": "terrain", "value": 0}])
    after = s.map_file.to_bytes()
    s.undo()
    s.redo()
    assert s.map_file.to_bytes() == after


def test_new_edit_clears_the_redo_stack(tmp_path):
    s = small_session(tmp_path)
    s.apply([{"x": 3, "y": 3, "field": "terrain", "value": 0}])
    s.undo()
    s.apply([{"x": 1, "y": 1, "field": "terrain", "value": 9}])
    assert s.redo_stack == []


def test_undo_on_empty_stack_is_harmless(tmp_path):
    s = small_session(tmp_path)
    assert s.undo() == []
    assert s.redo() == []


# --- persistence ----------------------------------------------------------

def test_save_preserves_the_undecoded_trailer(tmp_path):
    s = small_session(tmp_path)
    trailer = s.map_file.dormant_trailer
    s.apply([{"x": 2, "y": 2, "field": "terrain", "value": 9}])
    s.save()
    reloaded = MapFile.load(s.path, s.map_file.profile)
    assert reloaded.dormant_trailer == trailer
    assert reloaded.get(2, 2).terrain == 9


def test_save_backs_up_the_original_only_once(tmp_path):
    s = small_session(tmp_path)
    original = open(s.path, "rb").read()
    s.apply([{"x": 2, "y": 2, "field": "terrain", "value": 9}])
    s.save()
    assert open(s.path + ".bak", "rb").read() == original
    s.apply([{"x": 3, "y": 3, "field": "terrain", "value": 9}])
    s.save()
    assert open(s.path + ".bak", "rb").read() == original


def test_save_as_retargets_the_session_and_spares_the_original(tmp_path):
    s = small_session(tmp_path)
    original = open(s.path, "rb").read()
    first = s.path
    s.apply([{"x": 2, "y": 2, "field": "terrain", "value": 9}])
    s.save(str(tmp_path / "copy.map"))
    assert s.path == str(tmp_path / "copy.map")
    assert open(first, "rb").read() == original

    # A later plain save must land on the copy, not back on the original.
    s.apply([{"x": 3, "y": 3, "field": "terrain", "value": 9}])
    s.save()
    assert open(first, "rb").read() == original
    assert MapFile.load(s.path, s.map_file.profile).get(3, 3).terrain == 9


def test_dirty_tracking_resets_after_save(tmp_path):
    s = small_session(tmp_path)
    s.apply([{"x": 2, "y": 2, "field": "terrain", "value": 9}])
    assert s.dirty_cells()
    s.save()
    assert s.dirty_cells() == []


# --- native file dialogs --------------------------------------------------
#
# The dialog itself needs a person in front of it, so these exercise the
# subprocess contract around it: a chosen path arrives on stdout, a cancel
# arrives as silence, and a machine that cannot show a dialog says so rather
# than looking like a cancel.

def test_a_chosen_path_comes_back_normalised():
    chosen = dialogs._run("import sys; sys.stdout.write(r'C:\\\\maps\\\\.\\\\a.map')")
    assert chosen == os.path.normpath(r"C:\maps\a.map")


def test_cancelling_yields_none():
    assert dialogs._run("pass") is None


def test_whitespace_only_output_is_treated_as_a_cancel():
    assert dialogs._run("import sys; sys.stdout.write('   \\n')") is None


def test_a_broken_dialog_raises_rather_than_looking_like_a_cancel():
    """A missing tkinter must be reported, not silently swallowed."""
    try:
        dialogs._run("import sys; sys.stderr.write('No module named tkinter\\n');"
                     " sys.exit(1)")
        assert False, "expected DialogUnavailable"
    except dialogs.DialogUnavailable as exc:
        assert "tkinter" in str(exc)


def test_a_hung_dialog_times_out_instead_of_wedging_the_editor(monkeypatch):
    monkeypatch.setattr(dialogs, "TIMEOUT_SECONDS", 0.4)
    assert dialogs._run("import time; time.sleep(30)") is None


def test_the_generated_child_script_is_valid_python():
    """The script is built by string formatting, so it can break silently."""
    for script in (
        dialogs._CHILD.format(title="Open map", filetypes=dialogs.FILETYPES,
                              initialdir=r"C:\maps", initialfile="", save=False),
        dialogs._CHILD.format(title="Save map as", filetypes=dialogs.FILETYPES,
                              initialdir="", initialfile="s1.map", save=True),
    ):
        compile(script, "<dialog>", "exec")


def test_paths_with_spaces_and_non_ascii_survive():
    weird = r"C:\Users\Ryvius\Mes cartes\s1 – copie.map"
    assert dialogs._run(
        f"import sys; sys.stdout.buffer.write({weird!r}.encode('utf-8'))"
    ) == os.path.normpath(weird)


# --- real data ------------------------------------------------------------

def test_opening_and_saving_an_untouched_map_is_byte_exact(tmp_path):
    """The whole design rests on this: the editor must be a no-op until you edit."""
    paths = real_maps()
    if not paths:
        return
    original = open(paths[0], "rb").read()
    s = MapSession.open(paths[0])
    out = tmp_path / "out.map"
    s.save(str(out), backup=False)
    assert open(out, "rb").read() == original


def test_validator_is_silent_on_original_maps():
    """A rule that fires on shipped data is a wrong rule, not a bad map."""
    for path in real_maps():
        issues = validate.check(MapFile.load(path))
        assert issues == [], f"{os.path.basename(path)}: {issues[:3]}"


def test_validator_is_silent_on_original_scenarios_cross_file():
    """The cross-file rules are held to the same bar as the map rules."""
    from imperialism_format import ScenarioFile
    for map_path, scn_path in originals.scenarios():
        issues = validate.check_cross_file(MapFile.load(map_path),
                                           ScenarioFile.load(scn_path))
        assert issues == [], f"{os.path.basename(scn_path)}: {issues[:3]}"


def test_validator_rejects_a_work_on_a_minor_nations_cell(tmp_path):
    """The rule that would have caught the `0051465C` crash before launch.

    The engine resolves a work's cell to an owner and indexes a 7-slot Great
    Power table with it, unguarded, so a minor's id (7-22) reads off the end.
    """
    from imperialism_format import ScenarioFile
    from imperialism_format.scn_file import Record
    s = small_session(tmp_path)
    width = s.map_file.width

    s.map_file.get(2, 2).nation_zone_a = 14        # a minor nation
    scn = ScenarioFile(records=[Record(tag="rail", fields=[2 * width + 2])])
    issues = validate.check_cross_file(s.map_file, scn)
    assert any(i["rule"] == "scenario" and (i["x"], i["y"]) == (2, 2)
               for i in issues), issues

    s.map_file.get(2, 2).nation_zone_a = 6         # the last Great Power
    assert validate.check_cross_file(s.map_file, scn) == []


def test_validator_requires_developed_terrain_to_carry_its_resource(tmp_path):
    s = small_session(tmp_path)
    cell = s.map_file.get(2, 2)
    cell.terrain = 5          # grain farm, with no resource set
    issues = validate.check(s.map_file)
    assert any(i["rule"] == "resource" and (i["x"], i["y"]) == (2, 2) for i in issues)

    cell.resource_a = 17      # grain
    assert not any(i["rule"] == "resource" and (i["x"], i["y"]) == (2, 2)
                   for i in validate.check(s.map_file))

    cell.resource_a = 3       # coal on a grain farm: wrong resource, not just absent
    assert any(i["rule"] == "resource" and (i["x"], i["y"]) == (2, 2)
               for i in validate.check(s.map_file))


def test_validator_allows_a_resource_on_undeveloped_land(tmp_path):
    """An unworked deposit is a real state - s1 has fruit on clear ground."""
    s = small_session(tmp_path)
    cell = s.map_file.get(2, 2)
    cell.terrain = 1          # clear
    cell.resource_a = 18      # fruit, not yet an orchard
    assert not any(i["rule"] == "resource" for i in validate.check(s.map_file))


def test_validator_requires_a_stacked_resource_to_have_a_base(tmp_path):
    s = small_session(tmp_path)
    cell = s.map_file.get(2, 2)
    cell.terrain = 9              # mountain
    cell.resource_a = 255
    cell.resource_b = 22          # gold stacked on nothing
    assert any(i["rule"] == "resource" and (i["x"], i["y"]) == (2, 2)
               for i in validate.check(s.map_file))

    cell.resource_a = 3           # coal + gold, as shipped on s1's mountains
    assert not any(i["rule"] == "resource" for i in validate.check(s.map_file))


def test_validator_does_not_restrict_which_resources_stack(tmp_path):
    """Two cells of evidence cannot justify constraining the combinations."""
    s = small_session(tmp_path)
    cell = s.map_file.get(2, 2)
    cell.terrain = 8              # a hill, not a mountain
    cell.resource_a = 4           # iron
    cell.resource_b = 21          # gems - a pairing the originals never ship
    assert not any(i["rule"] == "resource" for i in validate.check(s.map_file))


def test_validator_catches_an_inconsistent_edit(tmp_path):
    s = small_session(tmp_path)
    # Ocean cells must not keep a land province; painting via the tool clears
    # it, but a raw edit that skips that step should be caught.
    s.map_file.get(2, 2).terrain = 0
    issues = validate.check(s.map_file)
    assert any(i["rule"] == "province" and i["x"] == 2 for i in issues)
