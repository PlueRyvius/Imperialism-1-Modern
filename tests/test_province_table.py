import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "src"))

from imperialism_format import MapFile, MapFormatProfile, anchors
from imperialism_format.constants import (
    DORMANT_RECORD_COUNT, NO_PROVINCE, PROVINCE_TOWN_OFFSET,
)

import originals


def small(width=6, height=6, slots=8, size=12):
    profile = MapFormatProfile(width=width, height=height,
                               trailer_record_count=slots, trailer_record_size=size)
    return MapFile.blank(profile)


# --- the field ------------------------------------------------------------

def test_a_blank_table_claims_no_towns():
    """Zeroes would mean every province has its town at cell 0."""
    m = small()
    assert m.province_towns() == {}
    assert all(m.province_town(p) is None for p in range(8))


def test_setting_and_reading_a_town():
    m = small()
    m.set_province_town(3, 27)
    assert m.province_town(3) == 27
    assert m.province_towns() == {3: 27}


def test_clearing_a_town():
    m = small()
    m.set_province_town(3, 27)
    m.set_province_town(3, None)
    assert m.province_town(3) is None


def test_an_edit_touches_only_its_own_two_bytes():
    """The rest of the record is undecoded, so it must survive untouched."""
    m = small()
    m.dormant_trailer = bytes(range(1, 97))          # recognisable filler
    before = m.dormant_trailer
    m.set_province_town(2, 0x1234)
    after = m.dormant_trailer
    differing = [i for i, (a, b) in enumerate(zip(before, after)) if a != b]
    assert differing == [2 * 12 + PROVINCE_TOWN_OFFSET,
                         2 * 12 + PROVINCE_TOWN_OFFSET + 1]


def test_the_field_is_big_endian():
    m = small()
    m.set_province_town(0, 0x1234)
    at = PROVINCE_TOWN_OFFSET
    assert m.dormant_trailer[at] == 0x12 and m.dormant_trailer[at + 1] == 0x34


def test_a_slot_outside_the_table_is_refused():
    m = small(slots=8)
    for bad in (-1, 8, 400):
        try:
            m.province_town(bad)
            assert False, f"expected IndexError for slot {bad}"
        except IndexError:
            pass


def test_a_cell_index_too_large_for_the_field_is_refused():
    m = small()
    try:
        m.set_province_town(0, 70000)
        assert False, "expected ValueError"
    except ValueError:
        pass


def test_the_table_survives_a_round_trip(tmp_path):
    m = small()
    m.set_province_town(1, 5)
    m.set_province_town(6, 31)
    path = tmp_path / "t.map"
    m.save(str(path))
    again = MapFile.load(str(path), m.profile)
    assert again.province_towns() == {1: 5, 6: 31}


# --- against real game data -----------------------------------------------

def test_the_table_holds_every_provinces_town_on_real_maps():
    """The decode: slot i is province i's town cell, 65535 when unused."""
    for path in originals.require_maps():
        m = MapFile.load(path)
        expected = {p: c for p, c in anchors.province_anchors(m).items()
                    if p < DORMANT_RECORD_COUNT}
        assert m.province_towns() == expected, os.path.basename(path)


def test_unused_slots_are_the_null_sentinel():
    for path in originals.require_maps():
        m = MapFile.load(path)
        used = set(m.province_towns())
        empty = [p for p in range(DORMANT_RECORD_COUNT) if p not in used]
        assert empty, os.path.basename(path)
        assert all(m.province_town(p) is None for p in empty)


def test_rewriting_the_towns_leaves_a_real_map_byte_identical():
    """Writing back what is already there must be a no-op, which is what lets
    a generated map inherit a table it does not fully understand."""
    for path in originals.require_maps():
        m = MapFile.load(path)
        before = m.dormant_trailer
        for province, cell in m.province_towns().items():
            m.set_province_town(province, cell)
        assert m.dormant_trailer == before, os.path.basename(path)
