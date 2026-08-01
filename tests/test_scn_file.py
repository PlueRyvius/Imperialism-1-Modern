import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "src"))

from imperialism_format import ScenarioFile

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
        return
    scn = ScenarioFile.load(LOCAL_FIXTURE)
    assert len(scn.records) > 0
    assert scn.to_bytes() == open(LOCAL_FIXTURE, "rb").read()
