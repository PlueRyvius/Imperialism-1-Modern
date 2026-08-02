"""Resolve a game crash to a place in the disassembly, in one command.

Every crash diagnosed in this project has followed the same four steps: read
the Windows Application log for the faulting offset, add the image base to get
a virtual address, resolve that address against the disassembly index, and read
the module name off the enclosing function to learn which subsystem died. This
runs all four.

    python -m tools.alf.crash                  # the newest fault
    python -m tools.alf.crash --list 10        # what has crashed recently
    python -m tools.alf.crash --offset 0x11465c  # resolve one by hand
    python -m tools.alf.crash --nth 1          # the one before the newest

`--offset` needs no event log, so a fault reported by someone else -- or on
another machine -- still resolves.

**The offset in the log is not an address.** It is relative to the module base;
`Fault offset: 0x0011465C` in a module loaded at the usual 0x400000 means
`0x0051465C`. Getting this wrong lands you in an unrelated function that looks
plausible, which is worse than getting nothing, so the arithmetic is printed.

What the module name buys you: it says which subsystem to distrust before you
have read a single instruction. `UMap.cpp` and `UMapper.cpp` are map data,
`UOcean.cpp` sea zones, `UArmyMgr.cpp` units. From there the productive move
has consistently been to diff generated output against the shipped files for
something they never contain. Imperialism-1-Forge's `preflight.py` automates
that diff for a generated scenario.
"""
from __future__ import annotations

import argparse
import re
import subprocess
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[2]))

from tools.alf import db as dbmod
from tools.alf import query

#: Where `Imperialism.exe` is linked to load. Every fault seen so far has been
#: in the exe itself, not a DLL, so this is the default addend.
DEFAULT_IMAGE_BASE = 0x400000

DEFAULT_APP = "Imperialism.exe"

#: The fields worth lifting out of the log entry's message body.
_FIELDS = (
    "Faulting application name", "Faulting module name", "Exception code",
    "Fault offset",
)


class EventLogUnavailable(RuntimeError):
    pass


def read_faults(app: str = DEFAULT_APP, limit: int = 20) -> list[dict]:
    """Recent `Application Error` entries for `app`, newest first.

    Windows-only, and deliberately tolerant: the log is a convenience here, and
    `--offset` covers every case where it cannot be read.
    """
    script = (
        "Get-WinEvent -FilterHashtable @{LogName='Application';"
        "ProviderName='Application Error'} -MaxEvents 200 -ErrorAction Stop |"
        " ForEach-Object { $_.TimeCreated.ToString('s') + '|' +"
        " ($_.Message -replace \"`r`n\", '~') }"
    )
    try:
        out = subprocess.run(
            ["powershell", "-NoProfile", "-NonInteractive", "-Command", script],
            capture_output=True, text=True, timeout=60,
        )
    except (OSError, subprocess.TimeoutExpired) as exc:
        raise EventLogUnavailable(str(exc)) from exc
    if out.returncode != 0:
        raise EventLogUnavailable(out.stderr.strip() or "Get-WinEvent failed")
    return parse_events(out.stdout, app, limit)


def parse_events(text: str, app: str = DEFAULT_APP, limit: int = 20) -> list[dict]:
    """`when|message` lines -> fault records, newest first.

    Split out from the PowerShell call so the parsing is testable without a
    Windows event log, which is most of what can go wrong here.
    """
    faults = []
    for line in text.splitlines():
        when, _, message = line.partition("|")
        if app.lower() not in message.lower():
            continue
        record = {"when": when}
        for field in _FIELDS:
            match = re.search(rf"{re.escape(field)}:\s*([^~]+)", message)
            record[field] = match.group(1).strip() if match else ""
        if record["Faulting application name"].split(",")[0].strip().lower() \
                != app.lower():
            continue
        try:
            record["offset"] = int(record["Fault offset"], 16)
        except ValueError:
            continue
        record["module"] = record["Faulting module name"].split(",")[0].strip()
        faults.append(record)
        if len(faults) >= limit:
            break
    return faults


def describe(fault: dict) -> str:
    return (f"{fault['when']}  offset {fault['Fault offset']}"
            f"  in {fault['module']}"
            f"  ({fault['Exception code'].split(',')[0].strip()})")


def resolve(conn, address: int, context: int) -> int:
    """Print the disassembly around `address`, reusing `query`'s formatting."""
    args = argparse.Namespace(address=hex(address), context=context)
    return query.cmd_addr(conn, args)


def _module_note(fault: dict, app: str) -> str | None:
    """Why an offset may not be resolvable against this index."""
    if fault["module"].lower() != app.lower():
        return (f"the fault is in {fault['module']}, not {app}, so its offset "
                f"is relative to that module. Nothing to resolve here.")
    return None


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(
        description=__doc__.split("\n\n")[0],
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="Offsets come from the Windows Application log; --offset skips it.",
    )
    ap.add_argument("--offset", help="fault offset to resolve, e.g. 0x11465c")
    ap.add_argument("--base", default=hex(DEFAULT_IMAGE_BASE),
                    help="image base to add (default 0x400000)")
    ap.add_argument("--nth", type=int, default=0,
                    help="which recent fault, 0 = newest (default 0)")
    ap.add_argument("--list", type=int, metavar="N", nargs="?", const=10,
                    help="list the N most recent faults and stop")
    ap.add_argument("--app", default=DEFAULT_APP, help="executable name to match")
    ap.add_argument("--context", type=int, default=20,
                    help="instructions either side (default 20)")
    ap.add_argument("--db", default=None)
    args = ap.parse_args(argv)

    base = query.parse_address(args.base)

    fault = None
    if args.offset is None or args.list is not None:
        try:
            faults = read_faults(args.app, limit=max(args.list or 0, args.nth + 1))
        except EventLogUnavailable as exc:
            print(f"error: could not read the Application log ({exc}).\n"
                  f"       Pass --offset 0x... to resolve one by hand.",
                  file=sys.stderr)
            return 2
        if not faults:
            print(f"no {args.app} faults in the Application log.", file=sys.stderr)
            return 1
        if args.list is not None:
            for i, f in enumerate(faults[:args.list]):
                print(f"[{i}] {describe(f)}")
            return 0
        if args.nth >= len(faults):
            print(f"only {len(faults)} fault(s) found.", file=sys.stderr)
            return 1
        fault = faults[args.nth]

    if fault is not None:
        offset = fault["offset"]
        print(f"; {describe(fault)}")
        note = _module_note(fault, args.app)
        if note:
            print(f"; {note}", file=sys.stderr)
            return 1
    else:
        offset = query.parse_address(args.offset)

    address = offset + base
    print(f"; fault offset {offset:#08x} + image base {base:#x} "
          f"= {address:08X}\n")

    try:
        conn = dbmod.connect(args.db or dbmod.default_db_path(), create=False)
    except FileNotFoundError as exc:
        print(f"error: {exc}", file=sys.stderr)
        return 2
    try:
        return resolve(conn, address, args.context)
    finally:
        conn.close()


if __name__ == "__main__":
    raise SystemExit(main())
