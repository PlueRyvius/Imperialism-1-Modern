import os
import sys

import pytest

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "src"))

from imperialism_format import HexCell, MapFile, MapFormatProfile
from imperialism_format.constants import MAP_CELL_COUNT

LOCAL_FIXTURE = os.path.join(os.path.dirname(__file__), "..", "fixtures", "local_only", "s1.map")


def test_blank_map_has_correct_cell_count():
    m = MapFile.blank()
    assert len(m.cells) == MAP_CELL_COUNT


def test_blank_map_round_trips(tmp_path):
    m = MapFile.blank()
    out = tmp_path / "blank.map"
    m.save(str(out))
    reloaded = MapFile.load(str(out))
    assert len(reloaded.cells) == MAP_CELL_COUNT
    assert reloaded.cells[0].to_bytes() == m.cells[0].to_bytes()


def test_custom_map_dimensions_round_trip():
    profile = MapFormatProfile(
        width=7, height=5, trailer_record_count=0, trailer_record_size=0
    )
    m = MapFile.blank(profile)
    m.set(6, 4, HexCell(terrain=8, province=9))
    reloaded = MapFile.from_bytes(m.to_bytes(), profile)
    assert (reloaded.width, reloaded.height) == (7, 5)
    assert len(reloaded.cells) == 35
    assert reloaded.get(6, 4).province == 9


def test_map_rejects_data_with_wrong_length():
    profile = MapFormatProfile(
        width=2, height=2, trailer_record_count=0, trailer_record_size=0
    )
    try:
        MapFile.from_bytes(bytes(3 * 36), profile)
        assert False, "expected ValueError"
    except ValueError as exc:
        assert "expected 144 bytes" in str(exc)


def test_set_and_get_cell_roundtrip():
    m = MapFile.blank()
    cell = HexCell(terrain=8, terrain_underlay=2, resource_a=1, province=42)
    m.set(5, 3, cell)
    fetched = m.get(5, 3)
    assert fetched.terrain == 8
    assert fetched.resource_a == 1
    assert fetched.province == 42


def test_out_of_bounds_raises():
    m = MapFile.blank()
    try:
        m.get(200, 200)
        assert False, "expected IndexError"
    except IndexError:
        pass


def test_hex_cell_byte_length_invariant():
    cell = HexCell()
    assert len(cell.to_bytes()) == 36


def test_real_game_map_loads_if_present():
    """Only runs when a real .map is dropped in fixtures/local_only (gitignored).
    We never commit actual game data, so this test self-skips otherwise.
    """
    if not os.path.exists(LOCAL_FIXTURE):
        pytest.skip("no real .map fixture in fixtures/local_only")
    m = MapFile.load(LOCAL_FIXTURE)
    assert len(m.cells) == MAP_CELL_COUNT
    assert m.to_bytes() == open(LOCAL_FIXTURE, "rb").read()


def test_blank_map_round_trip_is_byte_exact():
    m = MapFile.blank()
    data = m.to_bytes()
    reloaded = MapFile.from_bytes(data)
    assert reloaded.to_bytes() == data


def test_province_outside_uint16_raises_instead_of_wrapping():
    cell = HexCell(province=65536)
    with pytest.raises(ValueError, match="uint16"):
        cell.to_bytes()
