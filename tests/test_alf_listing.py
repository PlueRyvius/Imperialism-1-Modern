"""Unit tests for the W32Dasm listing parser.

Everything here runs against inline fixture strings that reproduce the shapes
observed in the real listing, so CI passes on a machine with no game files.
The two tests that touch the real ``.alf``/``.exe`` self-skip when absent,
following the pattern in ``tests/test_map_file.py``.
"""
import os
import sqlite3
import sys

import pytest

sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

from tools.alf import db as dbmod
from tools.alf.listing import call_target, parse_listing
from tools.alf.modules import derive_functions, derive_modules, module_ranges
from tools.alf.query import parse_address

# A real assert site from UCity.cpp, verbatim in shape (see docs/disasm/README.md).
ASSERT_SNIPPET = """\
:004B45AD 85C0                    test eax, eax
:004B45AF 7522                    jne 004B45D3
:004B45B1 6A30                    push 00000030

* Possible StringData Ref from Data Obj ->"Nil Pointer"
                                  |
:004B45B8 68C84F6900              push 00694FC8
:004B45BD 6A00                    push 00000000
:004B45BF FFD3                    call ebx
:004B45C1 683A050000              push 0000053A

* Possible StringData Ref from Data Obj ->"D:\\Ambit\\Cross\\UCity.cpp"
                                  |
:004B45C6 68185F6900              push 00695F18
:004B45CB E8D411F5FF              call 004057A4
:004B45D0 83C408                  add esp, 00000008

* Referenced by a (U)nconditional or (C)onditional Jump at Address:
|:004B45AF(C)
|
:004B45D3 6A50                    push 00000050
"""


def parse(text):
    return list(parse_listing(text.splitlines()))


def test_parses_plain_instruction():
    (line,) = parse(":00401000 E93B881300              jmp 00539840\n")
    assert line.address == 0x00401000
    assert line.raw_bytes == "E93B881300"
    assert line.mnemonic == "jmp"
    assert line.operands == "00539840"
    assert line.is_data is False
    assert line.line_no == 1


def test_line_numbers_track_the_source_file():
    lines = parse(ASSERT_SNIPPET)
    assert lines[0].line_no == 1
    # The last instruction sits on line 21 of the snippet.
    assert lines[-1].address == 0x004B45D3
    assert lines[-1].line_no == 21


def test_start_line_offsets_line_numbers_for_resume():
    (line,) = list(
        parse_listing([":00401000 90                      nop"], start_line=500_001)
    )
    assert line.line_no == 500_001


def test_long_encoding_overflowing_the_byte_column_still_splits():
    (line,) = parse(":004B45FC C7442418FFFFFFFF        mov [esp+18], FFFFFFFF\n")
    assert line.raw_bytes == "C7442418FFFFFFFF"
    assert line.mnemonic == "mov"
    assert line.operands == "[esp+18], FFFFFFFF"


def test_alignment_padding_is_flagged_as_data():
    (line,) = parse(":005190E4 00000000                BYTE  4 DUP(0)\n")
    assert line.is_data is True
    assert line.mnemonic == "BYTE"


def test_string_ref_attaches_to_the_following_instruction():
    lines = parse(ASSERT_SNIPPET)
    by_address = {line.address: line for line in lines}
    assert by_address[0x004B45C6].strings[0].value == r"D:\Ambit\Cross\UCity.cpp"
    assert by_address[0x004B45C6].strings[0].origin == "data"
    # ...and not to the instruction before it.
    assert by_address[0x004B45C1].strings == []


def test_assert_line_number_precedes_the_filename_push():
    by_address = {line.address: line for line in parse(ASSERT_SNIPPET)}
    assert by_address[0x004B45C1].operands == "0000053A"
    assert int(by_address[0x004B45C1].operands, 16) == 1338


def test_conditional_jump_xref_block():
    by_address = {line.address: line for line in parse(ASSERT_SNIPPET)}
    (xref,) = by_address[0x004B45D3].xrefs
    assert xref.from_address == 0x004B45AF
    assert xref.kind == "jump_cond"


def test_unconditional_jump_xref_block():
    lines = parse(
        "* Referenced by a (U)nconditional or (C)onditional Jump at Address:\n"
        "|:004B45F6(U)\n"
        "|\n"
        ":004B45FA 85FF                    test edi, edi\n"
    )
    assert lines[0].xrefs[0].kind == "jump_uncond"


def test_multi_line_call_xref_block():
    lines = parse(
        "* Referenced by a CALL at Addresses:\n"
        "|:004E739E   , :004E8424   , :004E84B6   , :004E84D6   , :004E84E5   \n"
        "|:004E9CF5   , :004E9D14   \n"
        "|\n"
        ":004014A6 E995700E00              jmp 004E8540\n"
    )
    (line,) = lines
    assert len(line.xrefs) == 7
    assert all(x.kind == "call" for x in line.xrefs)
    assert line.xrefs[0].from_address == 0x004E739E
    assert line.xrefs[-1].from_address == 0x004E9D14


def test_import_reference_block():
    lines = parse(
        "* Reference To: ADVAPI32.RegOpenKeyExA, Ord:012Eh\n"
        "                                  |\n"
        ":00412672 FF15E4AB6A00            Call dword ptr [006AABE4]\n"
    )
    (imp,) = lines[0].imports
    assert (imp.module, imp.symbol, imp.ordinal) == ("ADVAPI32", "RegOpenKeyExA", 0x012E)


def test_indirect_string_ref_is_flagged():
    lines = parse(
        '* Possible Indirect StringData Ref from Data Obj ->"Out of Memory!!!"\n'
        "                                  |\n"
        ":00401000 90                      nop\n"
    )
    assert lines[0].strings[0].indirect is True


def test_resource_annotation_does_not_leak_into_the_next_line():
    lines = parse(
        "* Possible Reference to Dialog:  \n"
        "                                  |\n"
        ":004B4608 68D84F6900              push 00694FD8\n"
        ":004B460D 68C84F6900              push 00694FC8\n"
    )
    assert lines[0].resources and not lines[1].resources


def test_header_sections_are_skipped_when_marker_is_required():
    text = (
        "Disassembly of File: X:\\game\\Imperialism.exe\n"
        " Addr:002AB770 hint(0014) Name: auxGetNumDevs\n"
        "+++++++++++++++++++ ASSEMBLY CODE LISTING ++++++++++++++++++\n"
        ":00401000 90                      nop\n"
    )
    lines = list(parse_listing(text.splitlines(), require_marker=True))
    assert [line.address for line in lines] == [0x00401000]


def test_end_of_listing_sentinel_stops_parsing():
    lines = parse(
        ":00401000 90                      nop\n"
        ":FFFFFFFF    End Of Listing\n"
        ":00401001 90                      nop\n"
    )
    assert [line.address for line in lines] == [0x00401000]


def test_call_target_resolves_only_direct_calls():
    (direct,) = parse(":004B45CB E8D411F5FF              call 004057A4\n")
    (indirect,) = parse(":00412672 FF15E4AB6A00            Call dword ptr [006AABE4]\n")
    (register,) = parse(":004B45BF FFD3                    call ebx\n")
    assert call_target(direct) == 0x004057A4
    assert call_target(indirect) is None
    assert call_target(register) is None


def test_parse_address_accepts_the_forms_the_listing_uses():
    assert parse_address("0x00525300") == 0x00525300
    assert parse_address(":00525300") == 0x00525300
    assert parse_address("00525300") == 0x00525300


# --------------------------------------------------------------------------
# End-to-end against a synthetic index
# --------------------------------------------------------------------------

FAKE_LISTING = """\
* Referenced by a CALL at Address:
|:00500010
|
:00401000 E9FB0F1000              jmp 00501000

* Possible StringData Ref from Data Obj ->"D:\\Ambit\\Cross\\UCity.cpp"
                                  |
:00501000 68185F6900              push 00695F18
:00501005 E8F6F7EEFF              call 003F0800
:0050100A C3                      ret
:0050100B CC                      int 03
:0050100C 90                      nop
:0050100D C3                      ret
:00501010 E8EBFFFFFF              call 00501000
:00501015 C3                      ret
"""


def build_index(tmp_path):
    from tools.alf.index import _collect, _flush

    conn = dbmod.connect(tmp_path / "test.sqlite")
    buckets = ([], [], [], [])
    for line in parse_listing(FAKE_LISTING.splitlines()):
        _collect(line, *buckets)
    _flush(conn, *buckets)
    conn.execute(
        "INSERT INTO module_strings(data_address, filename, source) VALUES(?,?,?)",
        (0x00695F18, r"D:\Ambit\Cross\UCity.cpp", "test"),
    )
    conn.commit()
    derive_functions(conn)
    derive_modules(conn)
    return conn


def test_index_records_instructions_and_xrefs(tmp_path):
    conn = build_index(tmp_path)
    assert conn.execute("SELECT COUNT(*) FROM instructions").fetchone()[0] == 9
    # The annotated CALL edge plus the two direct calls we synthesise.
    kinds = dict(conn.execute("SELECT kind, COUNT(*) FROM xrefs GROUP BY kind"))
    assert kinds["call"] == 1
    assert kinds["call_direct"] == 2
    conn.close()


def test_index_detects_the_thunk_and_rewrites_calls_through_it(tmp_path):
    conn = build_index(tmp_path)
    assert [
        row["target_address"] for row in conn.execute("SELECT target_address FROM thunks")
    ] == [0x00501000]
    # 00500010 called the thunk, so it must now show as calling the real target.
    assert conn.execute(
        "SELECT COUNT(*) FROM xrefs WHERE kind='call_thunk' AND from_address=? AND to_address=?",
        (0x00500010, 0x00501000),
    ).fetchone()[0] == 1
    conn.close()


def test_assert_site_anchors_its_function_to_a_module(tmp_path):
    conn = build_index(tmp_path)
    row = conn.execute(
        "SELECT filename, address FROM module_refs"
    ).fetchone()
    assert row["filename"] == r"D:\Ambit\Cross\UCity.cpp"
    assert row["address"] == 0x00501000
    fn = conn.execute(
        "SELECT * FROM functions WHERE start_address <= ? AND end_address >= ?",
        (0x00501000, 0x00501000),
    ).fetchone()
    assert fn["inferred_module"] == r"D:\Ambit\Cross\UCity.cpp"
    assert fn["confidence"] == "high"
    conn.close()


def test_module_ranges_are_produced(tmp_path):
    conn = build_index(tmp_path)
    ranges = module_ranges(conn)
    assert ranges
    assert all(r.start <= r.end for r in ranges)
    assert any(r.filename.endswith("UCity.cpp") for r in ranges)
    conn.close()


def test_indexing_is_idempotent(tmp_path):
    from tools.alf.index import _collect, _flush

    conn = build_index(tmp_path)
    before = conn.execute("SELECT COUNT(*) FROM instructions").fetchone()[0]
    buckets = ([], [], [], [])
    for line in parse_listing(FAKE_LISTING.splitlines()):
        _collect(line, *buckets)
    _flush(conn, *buckets)
    conn.commit()
    assert conn.execute("SELECT COUNT(*) FROM instructions").fetchone()[0] == before
    conn.close()


# --------------------------------------------------------------------------
# Optional: only run where the real (copyrighted, uncommitted) files exist
# --------------------------------------------------------------------------

# Optional: only run where the real (copyrighted, uncommitted) files exist.
# Point these environment variables at a local game install to enable them.
REAL_ALF = os.environ.get("IMPERIALISM_ALF", "")
REAL_EXE = os.environ.get("IMPERIALISM_EXE", "")


def test_real_listing_header_is_the_shape_we_expect():
    if not REAL_ALF or not os.path.exists(REAL_ALF):
        pytest.skip("IMPERIALISM_ALF is not set to a real .alf listing")
    with open(REAL_ALF, "r", encoding="latin-1") as fh:
        head = [next(fh) for _ in range(3)]
    assert head[0].startswith("Disassembly of File:")
    assert "Code Offset" in head[1]


def test_real_executable_leaks_source_filenames():
    if not REAL_EXE or not os.path.exists(REAL_EXE):
        pytest.skip("IMPERIALISM_EXE is not set to a real Imperialism.exe")
    from pathlib import Path

    from tools.alf.modules import scan_pe_source_filenames

    names = scan_pe_source_filenames(Path(REAL_EXE))
    assert len(names) >= 50
    assert any(n.endswith("UCity.cpp") for n in names.values())
