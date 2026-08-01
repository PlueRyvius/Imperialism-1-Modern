"""SQLite schema and helpers for the disassembly index.

The database is *derived from copyrighted material* (it contains the original
instruction stream verbatim), so it must never live inside the repository.
:func:`default_db_path` therefore points outside the tree by default, and the
repo's ``.gitignore`` additionally covers ``*.alfdb`` as a belt-and-braces
measure for anyone who overrides it.
"""
from __future__ import annotations

import os
import sqlite3
from pathlib import Path

SCHEMA_VERSION = 1

SCHEMA = """
CREATE TABLE IF NOT EXISTS meta (
    key   TEXT PRIMARY KEY,
    value TEXT NOT NULL
);

-- One row per disassembled address.  ``is_data`` marks W32Dasm's
-- ``BYTE n DUP(0)`` alignment filler, which shares the instruction layout.
CREATE TABLE IF NOT EXISTS instructions (
    address   INTEGER PRIMARY KEY,
    line_no   INTEGER NOT NULL,
    raw_bytes TEXT NOT NULL,
    mnemonic  TEXT NOT NULL,
    operands  TEXT NOT NULL,
    is_data   INTEGER NOT NULL DEFAULT 0
);
CREATE INDEX IF NOT EXISTS idx_instructions_line ON instructions(line_no);
CREATE INDEX IF NOT EXISTS idx_instructions_mnemonic ON instructions(mnemonic);

-- kind: call | jump_cond | jump_uncond | jump | call_direct
CREATE TABLE IF NOT EXISTS xrefs (
    from_address INTEGER NOT NULL,
    to_address   INTEGER NOT NULL,
    kind         TEXT NOT NULL,
    PRIMARY KEY (from_address, to_address, kind)
) WITHOUT ROWID;
CREATE INDEX IF NOT EXISTS idx_xrefs_to ON xrefs(to_address);

CREATE TABLE IF NOT EXISTS string_refs (
    address      INTEGER NOT NULL,
    string_value TEXT NOT NULL,
    origin       TEXT NOT NULL,
    indirect     INTEGER NOT NULL DEFAULT 0,
    PRIMARY KEY (address, string_value, origin)
) WITHOUT ROWID;
CREATE INDEX IF NOT EXISTS idx_string_refs_value ON string_refs(string_value);

CREATE TABLE IF NOT EXISTS import_refs (
    address INTEGER NOT NULL,
    module  TEXT NOT NULL,
    symbol  TEXT NOT NULL,
    ordinal INTEGER,
    PRIMARY KEY (address, module, symbol)
) WITHOUT ROWID;
CREATE INDEX IF NOT EXISTS idx_import_refs_symbol ON import_refs(symbol);

CREATE TABLE IF NOT EXISTS functions (
    start_address   INTEGER PRIMARY KEY,
    end_address     INTEGER NOT NULL,
    inferred_name   TEXT,
    inferred_module TEXT,
    confidence      TEXT NOT NULL DEFAULT 'none',
    evidence        TEXT
);
CREATE INDEX IF NOT EXISTS idx_functions_module ON functions(inferred_module);

-- Virtual addresses of the assert() source-filename literals in .data.
CREATE TABLE IF NOT EXISTS module_strings (
    data_address INTEGER PRIMARY KEY,
    filename     TEXT NOT NULL,
    source       TEXT NOT NULL   -- 'listing' (W32Dasm annotation) or 'pe' (exe scan)
);

-- Code addresses that push one of those literals: the assert call sites.
CREATE TABLE IF NOT EXISTS module_refs (
    address      INTEGER PRIMARY KEY,
    data_address INTEGER NOT NULL,
    filename     TEXT NOT NULL,
    assert_line  INTEGER,
    annotated    INTEGER NOT NULL DEFAULT 0
);
CREATE INDEX IF NOT EXISTS idx_module_refs_file ON module_refs(filename);

-- Incremental-link `jmp` stubs: thunk_address -> real function.
CREATE TABLE IF NOT EXISTS thunks (
    thunk_address  INTEGER PRIMARY KEY,
    target_address INTEGER NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_thunks_target ON thunks(target_address);

-- .data/.rdata dwords that point into .text: overwhelmingly C++ vtable slots.
CREATE TABLE IF NOT EXISTS code_pointers (
    data_address INTEGER PRIMARY KEY,
    code_address INTEGER NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_code_pointers_code ON code_pointers(code_address);
"""


def default_db_path() -> Path:
    """Where the index lives unless the caller says otherwise.

    Deliberately outside the repository: the index embeds the disassembly.
    """
    env = os.environ.get("IMP_ALF_DB")
    if env:
        return Path(env)
    return Path.home() / ".cache" / "imperialism" / "imperialism-alf.sqlite"


def connect(path: str | os.PathLike[str], *, create: bool = True) -> sqlite3.Connection:
    p = Path(path)
    if create:
        p.parent.mkdir(parents=True, exist_ok=True)
    elif not p.exists():
        raise FileNotFoundError(
            f"index not found at {p} -- build it first with "
            f"`python tools/alf/index.py --alf <path to Imperialism.alf>`"
        )
    conn = sqlite3.connect(str(p))
    conn.row_factory = sqlite3.Row
    # Bulk-load settings: the index is disposable and fully rebuildable, so
    # durability guarantees are not worth the ~10x slowdown.
    conn.execute("PRAGMA journal_mode=WAL")
    conn.execute("PRAGMA synchronous=OFF")
    if create:
        conn.executescript(SCHEMA)
        set_meta(conn, "schema_version", str(SCHEMA_VERSION))
    return conn


def set_meta(conn: sqlite3.Connection, key: str, value: str) -> None:
    conn.execute(
        "INSERT INTO meta(key, value) VALUES(?, ?) "
        "ON CONFLICT(key) DO UPDATE SET value=excluded.value",
        (key, value),
    )


def get_meta(conn: sqlite3.Connection, key: str, default: str | None = None) -> str | None:
    row = conn.execute("SELECT value FROM meta WHERE key=?", (key,)).fetchone()
    return row["value"] if row else default
