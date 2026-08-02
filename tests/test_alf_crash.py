"""Parsing of Windows fault reports, without needing a Windows event log."""
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

from tools.alf import crash


def event(app="Imperialism.exe", module="Imperialism.exe", offset="0x0011465c",
          when="2026-08-01T22:40:59"):
    """One `Get-WinEvent` line in the shape `read_faults` asks PowerShell for:
    newlines already collapsed to `~`."""
    return (f"{when}|Faulting application name: {app}, version: 1.1.0.5, "
            f"time stamp: 0x3459214b~Faulting module name: {module}, "
            f"version: 1.1.0.5, time stamp: 0x3459214b~"
            f"Exception code: 0xc0000005~Fault offset: {offset}~"
            f"Faulting process id: 0x2964~")


def test_a_fault_report_yields_its_offset_and_module():
    faults = crash.parse_events(event())
    assert len(faults) == 1
    assert faults[0]["offset"] == 0x11465C
    assert faults[0]["module"] == "Imperialism.exe"
    assert faults[0]["when"] == "2026-08-01T22:40:59"


def test_the_image_base_is_added_to_reach_a_virtual_address():
    """The whole point of the tool: 0x0011465C in the log is 0x0051465C here.

    Getting this wrong lands in an unrelated function that still looks
    plausible, so it is worth a test of its own.
    """
    offset = crash.parse_events(event())[0]["offset"]
    assert offset + crash.DEFAULT_IMAGE_BASE == 0x0051465C


def test_other_applications_are_ignored():
    assert crash.parse_events(event(app="notepad.exe")) == []


def test_a_fault_in_a_dll_is_reported_but_not_resolved():
    """Its offset is relative to that DLL, so adding the exe base is nonsense."""
    fault = crash.parse_events(event(module="libvorbis-0.dll"))[0]
    assert fault["module"] == "libvorbis-0.dll"
    assert crash._module_note(fault, "Imperialism.exe") is not None
    assert crash._module_note(crash.parse_events(event())[0],
                              "Imperialism.exe") is None


def test_faults_keep_log_order_and_respect_the_limit():
    text = "\n".join([event(offset="0x1"), event(offset="0x2"),
                      event(offset="0x3")])
    assert [f["offset"] for f in crash.parse_events(text)] == [1, 2, 3]
    assert [f["offset"] for f in crash.parse_events(text, limit=2)] == [1, 2]


def test_an_unreadable_offset_is_skipped_rather_than_crashing():
    text = event(offset="not-hex") + "\n" + event(offset="0x7")
    assert [f["offset"] for f in crash.parse_events(text)] == [7]
