import os
import sys

import pytest

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "src"))

from imperialism_format import ScenarioFile
from imperialism_format.scn_file import NAME_FIELD_SIZE, NAME_TAGS, TAG_FIELD_COUNTS

LOCAL_FIXTURE = os.path.join(os.path.dirname(__file__), "..", "fixtures", "local_only", "s1.scn")


def make_sample():
    scn = ScenarioFile()
    scn.add("cnam", 0, name="Testland")
    scn.add("cash", 0, 2500)
    scn.add("year", 1815)
    scn.add("rela", 0, 1, 100)
    return scn


def test_round_trip(tmp_path):
    scn = make_sample()
    out = tmp_path / "sample.scn"
    scn.save(str(out))
    reloaded = ScenarioFile.load(str(out))
    tags = [r.tag for r in reloaded.records]
    assert tags == ["cnam", "cash", "year", "rela"]
    cnam = reloaded.find("cnam")[0]
    assert cnam.fields == [0]
    assert cnam.name == "Testland"


def test_rejects_wrong_field_count():
    scn = ScenarioFile()
    try:
        scn.add("cash", 0)  # cash expects 2 fields
        assert False, "expected ValueError"
    except ValueError:
        pass


def test_terminates_with_term(tmp_path):
    scn = make_sample()
    out = tmp_path / "sample.scn"
    scn.save(str(out))
    raw = out.read_bytes()
    assert raw.endswith(b"TERM")


def test_preserves_original_name_padding_byte_for_byte():
    name_field = b"Testland\x00" + bytes(range(1, 56))
    raw = b"cnam" + (0).to_bytes(4, "big") + name_field + b"TERM"
    scn = ScenarioFile.from_bytes(raw)
    assert scn.records[0].name == "Testland"
    assert scn.to_bytes() == raw


def test_reencodes_name_after_edit():
    name_field = b"Old\x00" + b"junk".ljust(60, b"!")
    scn = ScenarioFile.from_bytes(
        b"cnam" + (0).to_bytes(4, "big") + name_field + b"TERM"
    )
    scn.records[0].name = "New"
    encoded = scn.to_bytes()
    assert encoded[8:72] == b"New".ljust(64, b"\x00")


def test_rejects_missing_term():
    try:
        ScenarioFile.from_bytes(b"year" + (5).to_bytes(4, "big"))
        assert False, "expected ValueError"
    except ValueError as exc:
        assert "TERM" in str(exc)


def test_real_game_scn_loads_if_present():
    if not os.path.exists(LOCAL_FIXTURE):
        pytest.skip("no real .scn fixture in fixtures/local_only")
    scn = ScenarioFile.load(LOCAL_FIXTURE)
    assert len(scn.records) > 0
    assert scn.to_bytes() == open(LOCAL_FIXTURE, "rb").read()


@pytest.mark.parametrize("newline", ["\r", "\n", "\r\n"])
def test_text_parser_accepts_original_and_modern_line_endings(newline):
    text = newline.join(("year 5", "zone 40 Port Said", "cash 0 2500")) + newline
    scn = ScenarioFile.from_text(text)

    assert [record.tag for record in scn.records] == ["year", "zone", "cash"]
    assert scn.records[1].fields == [40]
    assert scn.records[1].name == "Port Said"


def test_text_parser_handles_all_known_tags():
    lines = []
    for tag, field_count in TAG_FIELD_COUNTS.items():
        fields = " ".join(str(i) for i in range(field_count))
        suffix = " Example Name" if tag in NAME_TAGS else ""
        lines.append(f"{tag} {fields}{suffix}".strip())

    scn = ScenarioFile.from_text("\r".join(lines))

    assert [record.tag for record in scn.records] == list(TAG_FIELD_COUNTS)


def test_text_parser_normalizes_spacing_and_trailing_whitespace():
    scn = ScenarioFile.from_text("  zone\t40\tPort Said   \r\nyear   5  \r\n")

    assert scn.to_text() == "zone 40 Port Said\ryear 5\r"


def test_text_round_trip_is_semantic_not_whitespace_exact():
    original = ScenarioFile.from_text("cnam 0 Test Republic\nrela 0 1 100\n")
    reparsed = ScenarioFile.from_text(original.to_text())

    assert [
        (record.tag, record.fields, record.name) for record in reparsed.records
    ] == [
        (record.tag, record.fields, record.name) for record in original.records
    ]


def test_text_load_and_save_use_ascii_and_canonical_cr(tmp_path):
    source = tmp_path / "scenario.txt"
    source.write_bytes(b"year 5\nzone 40 Port Said\n")

    scn = ScenarioFile.load_text(str(source))
    output = tmp_path / "canonical.txt"
    scn.save_text(str(output))

    assert output.read_bytes() == b"year 5\rzone 40 Port Said\r"


@pytest.mark.parametrize(
    ("text", "message"),
    [
        ("year 5\nnope 1\n", "line 2: unknown tag"),
        ("year 5\ncash 0\n", "line 2: tag 'cash' expects 2 integer fields"),
        ("year 5\nzone 40\n", "line 2: tag 'zone' expects 1 integer fields followed by a name"),
        ("year 5\ncash zero 1\n", "line 2: field 1 for tag 'cash' is not a decimal integer"),
        ("year 5\ncash -1 1\n", "line 2: field 1 for tag 'cash' is outside uint32 range"),
        ("year 5\ncash 0 4294967296\n", "line 2: field 2 for tag 'cash' is outside uint32 range"),
    ],
)
def test_text_parser_reports_line_numbered_errors(text, message):
    with pytest.raises(ValueError) as exc_info:
        ScenarioFile.from_text(text)
    assert message in str(exc_info.value)


def test_edited_non_ascii_name_raises_instead_of_silent_replacement():
    scn = ScenarioFile()
    scn.add("cnam", 0, name="France")
    scn.records[0].name = "Café"
    with pytest.raises(ValueError, match="ASCII"):
        scn.to_bytes()


def test_blank_name_field_round_trips_via_raw_bytes():
    raw = b"cnam" + (0).to_bytes(4, "big") + bytes(NAME_FIELD_SIZE) + b"TERM"
    scn = ScenarioFile.from_bytes(raw)
    assert scn.to_bytes() == raw
