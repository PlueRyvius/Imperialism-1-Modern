import glob
import os
import re
import sys

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "src"))

from imperialism_format import scn_text
from imperialism_format.scn_file import ScenarioFile, TAG_FIELD_COUNTS

FIXTURE_DIR = os.path.join(os.path.dirname(__file__), "..", "fixtures", "local_only")


def scenario_dirs():
    dirs = [FIXTURE_DIR]
    external = os.environ.get("IMP_SCENARIO_DIR")
    if external:
        dirs.append(external)
    return [d for d in dirs if os.path.isdir(d)]


def text_scenarios():
    """The extensionless plaintext scenarios, e.g. `s14`."""
    found = []
    for d in scenario_dirs():
        for path in glob.glob(os.path.join(d, "s*")):
            if os.path.isfile(path) and re.fullmatch(r"s\d+", os.path.basename(path)):
                found.append(path)
    return sorted(found)


# --- parsing --------------------------------------------------------------

def test_parses_fields_and_names():
    scenario = scn_text.parse("tech 0 1\r cnam 1 Austrian Empire\r labo 0 2 2 2")
    assert [r.tag for r in scenario.records] == ["tech", "cnam", "labo"]
    assert scenario.records[0].fields == [0, 1]
    assert scenario.records[1].fields == [1]
    assert scenario.records[1].name == "Austrian Empire"
    assert scenario.records[2].fields == [0, 2, 2, 2]


def test_blank_lines_are_skipped():
    assert len(scn_text.parse("tech 0 1\r\r\r year 5\r").records) == 2


def test_a_name_keeps_its_internal_spacing():
    scenario = scn_text.parse("zone 13 Atlantic  Ocean I")
    assert scenario.records[0].name == "Atlantic  Ocean I"


def test_rejects_an_unknown_tag():
    try:
        scn_text.parse("nope 1 2")
        assert False, "expected ScenarioTextError"
    except scn_text.ScenarioTextError as exc:
        assert "nope" in str(exc)


def test_rejects_too_few_fields():
    try:
        scn_text.parse("labo 0 2")
        assert False, "expected ScenarioTextError"
    except scn_text.ScenarioTextError as exc:
        assert "4" in str(exc)


def test_rejects_trailing_text_on_a_tag_that_takes_no_name():
    """This is the check that makes the corpus an arity oracle: a tag whose
    field count we had wrong would leave unexplained tokens on the line."""
    try:
        scn_text.parse("tech 0 1 2")
        assert False, "expected ScenarioTextError"
    except scn_text.ScenarioTextError as exc:
        assert "trailing" in str(exc)


def test_rejects_a_name_tag_with_no_name():
    try:
        scn_text.parse("cnam 4")
        assert False, "expected ScenarioTextError"
    except scn_text.ScenarioTextError:
        pass


def test_errors_name_the_line():
    try:
        scn_text.parse("tech 0 1\r nope 1")
        assert False, "expected ScenarioTextError"
    except scn_text.ScenarioTextError as exc:
        assert "line 2" in str(exc)


def test_round_trips_through_text():
    original = "tech 0 1\rcnam 1 Austrian Empire\ryear 5"
    assert scn_text.to_text(scn_text.parse(original)) == original


# --- against real game data -----------------------------------------------
#
# The plaintext scenarios state the field count of every record independently of
# the binary, so they check TAG_FIELD_COUNTS against something other than our
# own assumptions. Self-skip when absent; point IMP_SCENARIO_DIR at an install.

def test_every_plaintext_line_conforms_to_the_arity_table():
    paths = text_scenarios()
    if not paths:
        return
    for path in paths:
        scenario = scn_text.load(path)          # raises if any line disagrees
        with open(path, encoding="cp1252", errors="replace", newline=None) as f:
            lines = [ln for ln in f.read().splitlines() if ln.strip()]
        assert len(scenario.records) == len(lines), os.path.basename(path)


def test_the_plaintext_corpus_covers_most_tags():
    """Guards the claim in docs/scenario-semantics.md about what is verified."""
    paths = text_scenarios()
    if not paths:
        return
    seen = {r.tag for p in paths for r in scn_text.load(p).records}
    unverified = set(TAG_FIELD_COUNTS) - seen
    # coun, tbar and tclr appear in no plaintext file, so their field counts
    # rest on the binary alone. If that set shrinks, update the docs.
    assert unverified <= {"coun", "tbar", "tclr"}, unverified


def test_two_plaintext_files_reproduce_a_binary_exactly():
    """The strongest form of the check: same tags, same values, same names.

    The plaintext files are named one ahead of the binary they correspond to —
    `s14` is the source of `s13.scn`, `s15` of `s14.scn`. The other five match
    no shipped binary and are stale drafts.
    """
    for d in scenario_dirs():
        for text_name, binary_name in (("s14", "s13.scn"), ("s15", "s14.scn")):
            text_path = os.path.join(d, text_name)
            binary_path = os.path.join(d, binary_name)
            if not (os.path.exists(text_path) and os.path.exists(binary_path)):
                continue
            problems = scn_text.differences(
                scn_text.load(text_path), ScenarioFile.load(binary_path))
            assert problems == [], f"{text_name} vs {binary_name}: {problems[:5]}"
