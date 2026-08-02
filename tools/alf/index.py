"""Build the SQLite index from a W32Dasm ``.alf`` listing.

Usage::

    python tools/alf/index.py --alf "C:/.../Imperialism.alf" \
        [--exe "C:/.../Imperialism.exe"] [--db path.sqlite] [--rebuild]

Design notes
------------
*Streaming*: the listing is 59 MB / ~1.39 M lines, so it is read line by line
and flushed to SQLite in batches.

*Resumable / idempotent*: after every flush we record the last fully-processed
line number in ``meta``.  A flush is only taken at a point where no annotation
block is half-consumed, so resuming from that line can never split a block.
Re-running over an already-complete index is a no-op beyond re-deriving
functions and modules, and every insert is an upsert keyed on address.

Pass 2 (functions/modules) is cheap relative to the scan and always re-runs, so
that improvements to the heuristics take effect without a full re-index.
"""
from __future__ import annotations

import argparse
import os
import re
import sys
import time
from pathlib import Path
from typing import Iterator

if __package__ in (None, ""):  # allow `python tools/alf/index.py`
    sys.path.insert(0, str(Path(__file__).resolve().parents[2]))

from tools.alf import db as dbmod
from tools.alf.listing import ListingLine, call_target, parse_listing
from tools.alf.modules import (
    derive_functions,
    derive_modules,
    scan_pe_code_pointers,
    scan_pe_source_filenames,
)

BATCH_LINES = 100_000


def _iter_source(path: Path, start_line: int) -> Iterator[str]:
    """Yield lines of ``path`` from ``start_line`` onward, cheaply skipping ahead."""
    with path.open("r", encoding="latin-1", errors="replace", newline="") as fh:
        for n, raw in enumerate(fh, start=1):
            if n < start_line:
                continue
            yield raw


def _flush(conn, instructions, xrefs, strings, imports) -> None:
    conn.executemany(
        "INSERT INTO instructions(address, line_no, raw_bytes, mnemonic, operands, is_data) "
        "VALUES(?,?,?,?,?,?) ON CONFLICT(address) DO UPDATE SET "
        "line_no=excluded.line_no, raw_bytes=excluded.raw_bytes, "
        "mnemonic=excluded.mnemonic, operands=excluded.operands, is_data=excluded.is_data",
        instructions,
    )
    conn.executemany(
        "INSERT OR IGNORE INTO xrefs(from_address, to_address, kind) VALUES(?,?,?)", xrefs
    )
    conn.executemany(
        "INSERT OR IGNORE INTO string_refs(address, string_value, origin, indirect) "
        "VALUES(?,?,?,?)",
        strings,
    )
    conn.executemany(
        "INSERT OR IGNORE INTO import_refs(address, module, symbol, ordinal) VALUES(?,?,?,?)",
        imports,
    )


def _collect(line: ListingLine, instructions, xrefs, strings, imports) -> None:
    instructions.append(
        (
            line.address,
            line.line_no,
            line.raw_bytes,
            line.mnemonic,
            line.operands,
            int(line.is_data),
        )
    )
    for x in line.xrefs:
        xrefs.append((x.from_address, line.address, x.kind))
    # A direct `call 004057A4` is itself an xref; W32Dasm only records the
    # reverse direction (and only when it resolved the label), so we add the
    # forward edge ourselves to make the call graph complete.
    target = call_target(line)
    if target is not None:
        xrefs.append((line.address, target, "call_direct"))
    for s in line.strings:
        strings.append((line.address, s.value, s.origin, int(s.indirect)))
    for i in line.imports:
        imports.append((line.address, i.module, i.symbol, i.ordinal))


def scan(alf_path: Path, conn, *, quiet: bool = False) -> int:
    """Pass 1: stream the listing into ``instructions``/``xrefs``/``string_refs``."""
    total_lines = int(dbmod.get_meta(conn, "source_lines") or 0)
    resume_from = int(dbmod.get_meta(conn, "scan_last_line") or 0) + 1
    if resume_from > 1 and not quiet:
        print(f"resuming scan at line {resume_from:,}")

    instructions: list[tuple] = []
    xrefs: list[tuple] = []
    strings: list[tuple] = []
    imports: list[tuple] = []

    started = time.time()
    count = 0
    last_line = resume_from - 1
    next_flush = resume_from + BATCH_LINES

    for line in parse_listing(
        _iter_source(alf_path, resume_from), start_line=resume_from, require_marker=False
    ):
        _collect(line, instructions, xrefs, strings, imports)
        count += 1
        last_line = line.line_no
        # Flushing on an instruction boundary is safe: at this point the
        # annotation accumulator is empty by construction.
        if line.line_no >= next_flush:
            _flush(conn, instructions, xrefs, strings, imports)
            dbmod.set_meta(conn, "scan_last_line", str(last_line))
            conn.commit()
            instructions.clear(); xrefs.clear(); strings.clear(); imports.clear()
            next_flush = line.line_no + BATCH_LINES
            if not quiet:
                elapsed = time.time() - started
                pct = f" ({100.0 * last_line / total_lines:5.1f}%)" if total_lines else ""
                print(
                    f"  line {last_line:>10,}{pct}  {count:>9,} rows  {elapsed:6.1f}s",
                    flush=True,
                )

    _flush(conn, instructions, xrefs, strings, imports)
    dbmod.set_meta(conn, "scan_last_line", str(last_line))
    dbmod.set_meta(conn, "scan_complete", "1")
    conn.commit()
    if not quiet:
        print(f"scan finished: {count:,} new rows in {time.time() - started:.1f}s")
    return count


def record_module_strings(conn, exe_path: Path | None) -> None:
    """Populate ``module_strings`` with the assert-filename literals.

    Two independent sources, because neither alone is complete:

    1. W32Dasm's ``Possible StringData Ref`` annotations give us
       (code address -> filename) pairs, from which the *data* address is the
       push operand.  Covers only filenames W32Dasm chose to annotate.
    2. Scanning the PE's ``.data`` section directly finds every literal,
       including the ones with no annotated reference at all.
    """
    rows = conn.execute(
        r"SELECT sr.address, sr.string_value, i.operands FROM string_refs sr "
        r"JOIN instructions i ON i.address = sr.address "
        r"WHERE sr.string_value LIKE 'D:\Ambit\%' OR sr.string_value LIKE 'd:\Ambit\%'"
    ).fetchall()
    seen: dict[int, tuple[str, str]] = {}
    for row in rows:
        m = re.search(r"\b([0-9A-F]{8})\b", row["operands"] or "")
        if m:
            seen[int(m.group(1), 16)] = (row["string_value"], "listing")

    if exe_path and exe_path.exists():
        for addr, name in scan_pe_source_filenames(exe_path).items():
            seen.setdefault(addr, (name, "pe"))
        conn.execute("DELETE FROM code_pointers")
        conn.executemany(
            "INSERT OR REPLACE INTO code_pointers(data_address, code_address) VALUES(?,?)",
            scan_pe_code_pointers(exe_path).items(),
        )

    conn.executemany(
        "INSERT INTO module_strings(data_address, filename, source) VALUES(?,?,?) "
        "ON CONFLICT(data_address) DO UPDATE SET filename=excluded.filename",
        [(a, n, s) for a, (n, s) in seen.items()],
    )
    conn.commit()


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--alf", required=True, help="path to Imperialism.alf (read only)")
    ap.add_argument("--exe", help="path to Imperialism.exe; improves module coverage")
    ap.add_argument("--db", default=str(dbmod.default_db_path()))
    ap.add_argument("--rebuild", action="store_true", help="discard any existing index first")
    ap.add_argument("--modules-only", action="store_true", help="skip pass 1, re-derive only")
    ap.add_argument("--quiet", action="store_true")
    args = ap.parse_args(argv)

    alf_path = Path(args.alf)
    if not alf_path.exists():
        print(f"error: {alf_path} not found", file=sys.stderr)
        return 2
    exe_path = Path(args.exe) if args.exe else None

    db_path = Path(args.db)
    if args.rebuild and db_path.exists():
        db_path.unlink()
        for suffix in ("-wal", "-shm"):
            side = Path(str(db_path) + suffix)
            if side.exists():
                side.unlink()

    conn = dbmod.connect(db_path)
    dbmod.set_meta(conn, "source_path", str(alf_path))
    dbmod.set_meta(conn, "source_size", str(alf_path.stat().st_size))

    if not args.modules_only:
        if not dbmod.get_meta(conn, "source_lines"):
            if not args.quiet:
                print("counting lines (one-off, for progress reporting)...", flush=True)
            with alf_path.open("rb") as fh:
                total = 0
                last_byte = b""
                for chunk in iter(lambda: fh.read(1 << 22), b""):
                    total += chunk.count(b"\n")
                    last_byte = chunk[-1:]
            if last_byte and last_byte != b"\n":
                total += 1  # final line has no trailing newline
            dbmod.set_meta(conn, "source_lines", str(total))
            conn.commit()
        scan(alf_path, conn, quiet=args.quiet)

    if not args.quiet:
        print("locating assert() source filenames and vtable pointers...", flush=True)
    record_module_strings(conn, exe_path)
    if not args.quiet:
        print("deriving function boundaries...", flush=True)
    derive_functions(conn)
    if not args.quiet:
        print("attributing modules...", flush=True)
    derive_modules(conn)
    conn.commit()

    if not args.quiet:
        for table in ("instructions", "xrefs", "string_refs", "import_refs",
                      "functions", "thunks", "code_pointers",
                      "module_strings", "module_refs"):
            n = conn.execute(f"SELECT COUNT(*) FROM {table}").fetchone()[0]
            print(f"  {table:<16} {n:>10,}")
        print(f"index written to {db_path}")
    conn.close()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
