import glob
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "src"))

from imperialism_format import ScenarioInfo

import originals

FIXTURE_DIR = originals.FIXTURE_DIR

SAMPLE = (
    "A New World\r#\rDescription line\r#\rCountry one\r^^Difficulty: Easy\r"
    "#\rCountry two\r# 2 -1 4\r"
)


def real_infs():
    return originals.infs()


# --- parsing --------------------------------------------------------------

def test_parses_scenario_info_sections_and_metadata():
    info = ScenarioInfo.parse(SAMPLE)
    assert info.title == "A New World"
    assert info.overview == "Description line"
    assert info.country_sections == [
        "Country one\n^^Difficulty: Easy",
        "Country two",
    ]
    assert info.metadata == [2, -1, 4]


def test_rejects_empty_info_file():
    try:
        ScenarioInfo.parse("\r\n")
        assert False, "expected ValueError"
    except ValueError as exc:
        assert "no text" in str(exc)


def test_section_text_is_normalised_to_newlines():
    """The file uses CR; an editor should not have to care."""
    info = ScenarioInfo.parse(SAMPLE)
    assert "\r" not in info.country_sections[0]
    assert "\n" in info.country_sections[0]


# --- writing --------------------------------------------------------------

def test_unedited_text_round_trips_exactly():
    assert ScenarioInfo.parse(SAMPLE).to_text() == SAMPLE


def test_editing_the_title_leaves_every_other_byte_alone():
    info = ScenarioInfo.parse(SAMPLE)
    info.title = "Another World"
    assert info.to_text() == SAMPLE.replace("A New World", "Another World", 1)


def test_editing_a_country_section_does_not_touch_its_neighbours():
    info = ScenarioInfo.parse(SAMPLE)
    info.country_sections[1] = "Rewritten"
    out = info.to_text()
    assert "Country one\r^^Difficulty: Easy" in out   # untouched, CR intact
    assert "Rewritten" in out
    assert "Country two" not in out


def test_editing_metadata_rewrites_only_the_numbers():
    info = ScenarioInfo.parse(SAMPLE)
    info.metadata = [1, 2, 3]
    out = info.to_text()
    assert out.endswith("# 1 2 3\r")
    assert out.startswith("A New World\r#\r")


def test_edits_keep_the_files_own_line_endings():
    info = ScenarioInfo.parse(SAMPLE)
    info.overview = "Line one\nLine two"
    out = info.to_text()
    assert "\n" not in out
    assert "Line one\rLine two\r" in out


def test_an_edited_file_reparses_to_what_was_written():
    info = ScenarioInfo.parse(SAMPLE)
    info.title = "Retitled"
    info.country_sections[0] = "New briefing"
    info.metadata = [9, 8, 7]
    again = ScenarioInfo.parse(info.to_text())
    assert again.title == "Retitled"
    assert again.country_sections[0] == "New briefing"
    assert again.metadata == [9, 8, 7]


def test_an_instance_built_by_hand_still_renders():
    """No spans to splice into, so it has to lay the whole file out."""
    info = ScenarioInfo(
        title="Handmade", overview="Overview",
        country_sections=["One", "Two"], metadata=[1, -1, 0])
    again = ScenarioInfo.parse(info.to_text())
    assert again.title == "Handmade"
    assert again.overview == "Overview"
    assert again.country_sections == ["One", "Two"]
    assert again.metadata == [1, -1, 0]


def test_save_writes_bytes_that_reload_identically(tmp_path):
    info = ScenarioInfo.parse(SAMPLE)
    out = tmp_path / "s.inf"
    info.save(str(out))
    assert open(out, "rb").read() == SAMPLE.encode("cp1252")
    assert ScenarioInfo.load(str(out)).title == "A New World"


# --- against real game data -----------------------------------------------

def test_real_inf_files_round_trip_byte_for_byte():
    """The hard rule the rest of this project already meets."""
    for path in real_infs():
        raw = open(path, "rb").read()
        rendered = ScenarioInfo.load(path).to_text().encode("cp1252", errors="replace")
        assert rendered == raw, os.path.basename(path)


def test_real_inf_files_all_have_the_documented_shape():
    paths = real_infs()
    if not paths:
        return
    for path in paths:
        info = ScenarioInfo.load(path)
        name = os.path.basename(path)
        assert info.title, name
        assert len(info.country_sections) == 7, f"{name}: {len(info.country_sections)}"
        assert len(info.metadata) == 8, f"{name}: {info.metadata}"
        # Seven playability codes then the default player index.
        assert all(-1 <= value <= 6 for value in info.metadata), f"{name}: {info.metadata}"
        assert 0 <= info.metadata[7] <= 6, name


def test_editing_a_real_file_changes_nothing_else():
    paths = real_infs()
    if not paths:
        return
    path = paths[0]
    raw = open(path, "rb").read()
    info = ScenarioInfo.load(path)
    original_title = info.title
    info.title = "Edited Title"
    out = info.to_text().encode("cp1252", errors="replace")
    assert out == raw.replace(original_title.encode("cp1252"), b"Edited Title", 1)
