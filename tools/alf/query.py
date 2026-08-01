"""Query CLI over the disassembly index.

::

    python -m tools.alf.query addr 0x00525300 --context 40
    python -m tools.alf.query xrefs 0x00525300
    python -m tools.alf.query func --name UCity
    python -m tools.alf.query strings --grep vote
    python -m tools.alf.query calls-into 0x004057A4
    python -m tools.alf.query imports --grep Registry
    python -m tools.alf.query modules
    python -m tools.alf.query stats

Every subcommand prints plain text to stdout so results compose with ordinary
shell tooling; nothing here writes to the index.
"""
from __future__ import annotations

import argparse
import sqlite3
import sys
from pathlib import Path

if __package__ in (None, ""):  # allow `python tools/alf/query.py`
    sys.path.insert(0, str(Path(__file__).resolve().parents[2]))

from tools.alf import db as dbmod
from tools.alf.modules import module_ranges


def parse_address(text: str) -> int:
    """Accept ``0x004057A4``, ``004057A4`` or ``4218276``."""
    text = text.strip().rstrip(":").lstrip(":")
    if text.lower().startswith("0x"):
        return int(text, 16)
    try:
        return int(text, 16)
    except ValueError:
        return int(text, 10)


def _function_at(conn: sqlite3.Connection, address: int) -> sqlite3.Row | None:
    return conn.execute(
        "SELECT * FROM functions WHERE start_address <= ? AND end_address >= ? "
        "ORDER BY start_address DESC LIMIT 1",
        (address, address),
    ).fetchone()


def _format_instruction(conn: sqlite3.Connection, row: sqlite3.Row, marker: str = " ") -> str:
    notes = []
    for s in conn.execute(
        "SELECT string_value FROM string_refs WHERE address=?", (row["address"],)
    ):
        notes.append(f'str="{s[0]}"')
    for s in conn.execute(
        "SELECT module, symbol FROM import_refs WHERE address=?", (row["address"],)
    ):
        notes.append(f"import={s[0]}.{s[1]}")
    incoming = conn.execute(
        "SELECT COUNT(*) FROM xrefs WHERE to_address=?", (row["address"],)
    ).fetchone()[0]
    if incoming:
        notes.append(f"xrefs={incoming}")
    suffix = ("   ; " + "  ".join(notes)) if notes else ""
    return (
        f"{marker}:{row['address']:08X} {row['raw_bytes']:<20} "
        f"{row['mnemonic']} {row['operands']}".rstrip() + suffix
    )


def cmd_addr(conn: sqlite3.Connection, args) -> int:
    address = parse_address(args.address)
    before = conn.execute(
        "SELECT * FROM instructions WHERE address <= ? ORDER BY address DESC LIMIT ?",
        (address, args.context + 1),
    ).fetchall()
    after = conn.execute(
        "SELECT * FROM instructions WHERE address > ? ORDER BY address LIMIT ?",
        (address, args.context),
    ).fetchall()
    if not before:
        print(f"no instruction at or before {address:08X}", file=sys.stderr)
        return 1
    fn = _function_at(conn, address)
    if fn:
        print(
            f"; function {fn['start_address']:08X}-{fn['end_address']:08X}"
            f"  module={fn['inferred_module'] or '?'}"
            f"  confidence={fn['confidence']}"
            + (f"  ({fn['evidence']})" if fn["evidence"] else "")
        )
    for row in reversed(before):
        print(_format_instruction(conn, row, ">" if row["address"] == address else " "))
    for row in after:
        print(_format_instruction(conn, row))
    return 0


def cmd_xrefs(conn: sqlite3.Connection, args) -> int:
    address = parse_address(args.address)
    incoming = conn.execute(
        "SELECT from_address, kind FROM xrefs WHERE to_address=? ORDER BY from_address",
        (address,),
    ).fetchall()
    outgoing = conn.execute(
        "SELECT to_address, kind FROM xrefs WHERE from_address=? ORDER BY to_address",
        (address,),
    ).fetchall()
    print(f"incoming to {address:08X}: {len(incoming)}")
    for row in incoming:
        fn = _function_at(conn, row["from_address"])
        where = f"  [{fn['inferred_module']}]" if fn and fn["inferred_module"] else ""
        print(f"  {row['from_address']:08X}  {row['kind']}{where}")
    print(f"outgoing from {address:08X}: {len(outgoing)}")
    for row in outgoing:
        print(f"  {row['to_address']:08X}  {row['kind']}")
    return 0


def cmd_calls_into(conn: sqlite3.Connection, args) -> int:
    """Who calls this -- resolved to functions rather than raw addresses."""
    address = parse_address(args.address)
    fn = _function_at(conn, address)
    target = fn["start_address"] if fn and not args.exact else address
    rows = conn.execute(
        "SELECT DISTINCT from_address FROM xrefs "
        "WHERE to_address=? AND kind IN ('call','call_direct','call_thunk') "
        "ORDER BY from_address",
        (target,),
    ).fetchall()
    print(f"{len(rows)} call site(s) into {target:08X}")
    for row in rows:
        caller = _function_at(conn, row["from_address"])
        if caller:
            print(
                f"  {row['from_address']:08X}  in {caller['start_address']:08X} "
                f"[{caller['inferred_module'] or '?'} / {caller['confidence']}]"
            )
        else:
            print(f"  {row['from_address']:08X}  (outside any detected function)")
    return 0


def cmd_func(conn: sqlite3.Connection, args) -> int:
    sql = "SELECT * FROM functions"
    params: list = []
    clauses = []
    if args.name:
        clauses.append("(inferred_module LIKE ? OR inferred_name LIKE ?)")
        params += [f"%{args.name}%", f"%{args.name}%"]
    if args.confidence:
        clauses.append("confidence = ?")
        params.append(args.confidence)
    if args.at:
        address = parse_address(args.at)
        clauses.append("start_address <= ? AND end_address >= ?")
        params += [address, address]
    if clauses:
        sql += " WHERE " + " AND ".join(clauses)
    sql += " ORDER BY start_address LIMIT ?"
    params.append(args.limit)
    rows = conn.execute(sql, params).fetchall()
    print(f"{len(rows)} function(s)")
    for row in rows:
        size = row["end_address"] - row["start_address"]
        print(
            f"  {row['start_address']:08X}-{row['end_address']:08X} "
            f"({size:>6} bytes)  {row['inferred_module'] or '-':<34} "
            f"{row['confidence']}"
        )
    return 0


def cmd_strings(conn: sqlite3.Connection, args) -> int:
    sql = "SELECT address, string_value, origin FROM string_refs"
    params: list = []
    if args.grep:
        sql += " WHERE string_value LIKE ?"
        params.append(f"%{args.grep}%")
    sql += " ORDER BY address LIMIT ?"
    params.append(args.limit)
    rows = conn.execute(sql, params).fetchall()
    print(f"{len(rows)} string reference(s)")
    for row in rows:
        fn = _function_at(conn, row["address"])
        where = f"  [{fn['inferred_module']}]" if fn and fn["inferred_module"] else ""
        print(f'  {row["address"]:08X}  ({row["origin"]}) "{row["string_value"]}"{where}')
    return 0


def cmd_imports(conn: sqlite3.Connection, args) -> int:
    sql = "SELECT address, module, symbol FROM import_refs"
    params: list = []
    if args.grep:
        sql += " WHERE symbol LIKE ? OR module LIKE ?"
        params += [f"%{args.grep}%", f"%{args.grep}%"]
    sql += " ORDER BY address LIMIT ?"
    params.append(args.limit)
    rows = conn.execute(sql, params).fetchall()
    print(f"{len(rows)} import reference(s)")
    for row in rows:
        print(f"  {row['address']:08X}  {row['module']}.{row['symbol']}")
    return 0


def cmd_modules(conn: sqlite3.Connection, args) -> int:
    rows = conn.execute(
        "SELECT inferred_module AS m, confidence AS c, COUNT(*) AS n, "
        "SUM(end_address - start_address) AS bytes FROM functions "
        "WHERE inferred_module IS NOT NULL GROUP BY m, c ORDER BY m, c"
    ).fetchall()
    print(f"{'module':<38} {'conf':<8} {'funcs':>6} {'bytes':>9}")
    for row in rows:
        print(f"  {row['m']:<36} {row['c']:<8} {row['n']:>6} {row['bytes']:>9}")
    if args.ranges:
        print()
        for r in module_ranges(conn):
            print(
                f"  {r.start:08X}-{r.end:08X}  {r.filename:<36} "
                f"{r.confidence:<8} {r.function_count:>5} funcs  {r.anchor_count} anchors"
            )
    return 0


def cmd_stats(conn: sqlite3.Connection, args) -> int:
    for table in (
        "instructions", "xrefs", "string_refs", "import_refs",
        "functions", "thunks", "code_pointers", "module_strings", "module_refs",
    ):
        n = conn.execute(f"SELECT COUNT(*) FROM {table}").fetchone()[0]
        print(f"  {table:<16} {n:>10,}")
    for key in ("source_path", "source_lines", "scan_complete"):
        print(f"  {key:<16} {dbmod.get_meta(conn, key)}")
    return 0


def build_parser() -> argparse.ArgumentParser:
    ap = argparse.ArgumentParser(
        prog="tools.alf.query",
        description=__doc__,
        formatter_class=argparse.RawDescriptionHelpFormatter,
    )
    ap.add_argument("--db", default=str(dbmod.default_db_path()))
    sub = ap.add_subparsers(dest="command", required=True)

    p = sub.add_parser("addr", help="disassembly around an address")
    p.add_argument("address")
    p.add_argument("--context", type=int, default=20)
    p.set_defaults(func=cmd_addr)

    p = sub.add_parser("xrefs", help="incoming and outgoing references")
    p.add_argument("address")
    p.set_defaults(func=cmd_xrefs)

    p = sub.add_parser("calls-into", help="who calls the function containing an address")
    p.add_argument("address")
    p.add_argument("--exact", action="store_true", help="do not widen to the function start")
    p.set_defaults(func=cmd_calls_into)

    p = sub.add_parser("func", help="list detected functions")
    p.add_argument("--name", help="substring of the inferred module or name")
    p.add_argument("--at", help="function containing this address")
    p.add_argument("--confidence", choices=["high", "medium", "low", "none"])
    p.add_argument("--limit", type=int, default=200)
    p.set_defaults(func=cmd_func)

    p = sub.add_parser("strings", help="string references")
    p.add_argument("--grep")
    p.add_argument("--limit", type=int, default=200)
    p.set_defaults(func=cmd_strings)

    p = sub.add_parser("imports", help="Win32 import references")
    p.add_argument("--grep")
    p.add_argument("--limit", type=int, default=200)
    p.set_defaults(func=cmd_imports)

    p = sub.add_parser("modules", help="module attribution summary")
    p.add_argument("--ranges", action="store_true")
    p.set_defaults(func=cmd_modules)

    p = sub.add_parser("stats", help="index row counts")
    p.set_defaults(func=cmd_stats)
    return ap


def main(argv: list[str] | None = None) -> int:
    args = build_parser().parse_args(argv)
    try:
        conn = dbmod.connect(args.db, create=False)
    except FileNotFoundError as exc:
        print(f"error: {exc}", file=sys.stderr)
        return 2
    try:
        return args.func(conn, args)
    finally:
        conn.close()


if __name__ == "__main__":
    raise SystemExit(main())
