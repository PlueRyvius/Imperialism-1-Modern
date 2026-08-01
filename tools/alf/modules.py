"""Function-boundary detection and source-module attribution.

The premise of this whole tool: Imperialism.exe was built with ``assert()``
left in, and MSVC's assert macro emits ``push <line>; push <"file.cpp">; call
<assert handler>`` at every call site.  Those 55 string literals are the only
symbolic information the stripped binary leaks, and because the linker lays out
each translation unit's code contiguously, an assert site is a reliable
*anchor* saying "this address belongs to UCity.cpp".

Attribution therefore proceeds anchor-first and then interpolates, and every
row records how it was decided so that consumers can distinguish fact from
guess:

``high``    the function itself contains an assert naming that file.
``medium``  the function sits between two ``high`` anchors naming the same file
            (linker locality; strong but not proven).
``low``     no anchor, but every direct caller we know of resolves to one
            module.  Treat as a hint only.
``none``    unattributed.
"""
from __future__ import annotations

import re
import sqlite3
import struct
from dataclasses import dataclass
from pathlib import Path

#: ``.text`` of Imperialism.exe (imagebase 0x400000, RVA 0x1000, size 0x23D000).
#: Discovered from the listing's own object table; used only to keep function
#: detection from wandering into the ``.patch`` section at 0x707000.
TEXT_START = 0x00401000
TEXT_END = 0x0063E000

_HEX8 = re.compile(r"\b([0-9A-F]{8})\b")
_IMM = re.compile(r"^([0-9A-F]{8})$")

CONFIDENCE_ORDER = {"high": 3, "medium": 2, "low": 1, "none": 0}


@dataclass(frozen=True)
class ModuleRange:
    """A contiguous run of same-module functions, for the module map document."""

    filename: str
    start: int
    end: int
    confidence: str
    function_count: int
    anchor_count: int


# --------------------------------------------------------------------------
# PE scanning
# --------------------------------------------------------------------------

@dataclass(frozen=True)
class PeImage:
    """Just enough of a PE to map file offsets to virtual addresses."""

    data: bytes
    image_base: int
    sections: tuple[tuple[str, int, int, int], ...]  # (name, rawptr, rawsize, vaddr)

    @classmethod
    def load(cls, exe_path: Path) -> "PeImage":
        data = exe_path.read_bytes()
        if data[:2] != b"MZ":
            raise ValueError(f"{exe_path} is not a PE image")
        pe_off = struct.unpack_from("<I", data, 0x3C)[0]
        if data[pe_off:pe_off + 4] != b"PE\0\0":
            raise ValueError(f"{exe_path} has no PE signature")
        n_sections = struct.unpack_from("<H", data, pe_off + 6)[0]
        opt_size = struct.unpack_from("<H", data, pe_off + 20)[0]
        image_base = struct.unpack_from("<I", data, pe_off + 24 + 28)[0]
        sec_off = pe_off + 24 + opt_size
        sections = []
        for i in range(n_sections):
            base = sec_off + i * 40
            name = data[base:base + 8].rstrip(b"\0").decode("latin-1")
            _vsize, vaddr, rawsize, rawptr = struct.unpack_from("<IIII", data, base + 8)
            sections.append((name, rawptr, rawsize, vaddr))
        return cls(data, image_base, tuple(sections))

    def to_va(self, offset: int) -> int | None:
        for _name, rawptr, rawsize, vaddr in self.sections:
            if rawsize and rawptr <= offset < rawptr + rawsize:
                return self.image_base + vaddr + (offset - rawptr)
        return None


def scan_pe_source_filenames(exe_path: Path) -> dict[int, str]:
    """Map virtual address -> source filename for every ``D:\\Ambit\\...`` literal.

    Read straight from the executable rather than the listing because W32Dasm
    never disassembles ``.data``: the literals exist in the listing only as
    incidental annotation text, and thirteen of them are annotated nowhere at
    all.
    """
    pe = PeImage.load(exe_path)
    found: dict[int, str] = {}
    for m in re.finditer(rb"[Dd]:\\Ambit\\[ -~]{3,60}?\.(?:cpp|h)\x00", pe.data):
        va = pe.to_va(m.start())
        if va is not None:
            found[va] = m.group()[:-1].decode("latin-1")
    return found


def scan_pe_code_pointers(exe_path: Path) -> dict[int, int]:
    """Map data address -> code address for every ``.data``/``.rdata`` dword
    that points into ``.text``.

    These are overwhelmingly C++ vtable slots, and they are the single most
    valuable function-start signal in this binary: Imperialism is deeply
    virtual, so thousands of methods are never the target of a direct ``call``
    and would otherwise be invisible.  Empirically ~99.6% of the hits land
    exactly on an instruction boundary, which is what rules out coincidence.
    """
    pe = PeImage.load(exe_path)
    pointers: dict[int, int] = {}
    for name, rawptr, rawsize, vaddr in pe.sections:
        if name not in (".rdata", ".data"):
            continue
        blob = pe.data[rawptr:rawptr + rawsize]
        for off in range(0, len(blob) - 3, 4):
            value = struct.unpack_from("<I", blob, off)[0]
            if TEXT_START <= value < TEXT_END:
                pointers[pe.image_base + vaddr + off] = value
    return pointers


# --------------------------------------------------------------------------
# Function boundaries
# --------------------------------------------------------------------------

def resolve_thunks(conn: sqlite3.Connection) -> int:
    """Record incremental-link thunks and re-point calls through them.

    This binary was linked with MSVC incremental linking, so almost every
    ``call`` goes to a five-byte ``jmp <real function>`` stub in a huge thunk
    table at the bottom of ``.text``.  Left alone that is fatal to analysis:
    "who calls UCity::DoTurn" returns nothing, because everyone calls its thunk.

    We therefore add a synthetic ``call_thunk`` edge from each original call
    site directly to the thunk's destination, and keep the thunk mapping so the
    query layer can explain the indirection.
    """
    conn.execute(
        "CREATE TABLE IF NOT EXISTS thunks ("
        " thunk_address INTEGER PRIMARY KEY, target_address INTEGER NOT NULL)"
    )
    conn.execute("DELETE FROM thunks")
    called = {
        row[0]
        for row in conn.execute(
            "SELECT DISTINCT to_address FROM xrefs WHERE kind IN ('call','call_direct')"
        )
    }
    thunks: dict[int, int] = {}
    for address, operands in conn.execute(
        "SELECT address, operands FROM instructions "
        "WHERE mnemonic='jmp' AND LENGTH(raw_bytes)=10 AND raw_bytes LIKE 'E9%'"
    ):
        if address not in called:
            continue
        ops = operands.strip()
        if _IMM.match(ops):
            target = int(ops, 16)
            if TEXT_START <= target < TEXT_END and target != address:
                thunks[address] = target
    conn.executemany(
        "INSERT OR REPLACE INTO thunks(thunk_address, target_address) VALUES(?,?)",
        thunks.items(),
    )
    edges = [
        (row[0], thunks[row[1]], "call_thunk")
        for row in conn.execute(
            "SELECT from_address, to_address FROM xrefs WHERE kind IN ('call','call_direct')"
        )
        if row[1] in thunks
    ]
    conn.executemany("INSERT OR IGNORE INTO xrefs(from_address,to_address,kind) VALUES(?,?,?)", edges)
    conn.commit()
    return len(thunks)


def derive_functions(conn: sqlite3.Connection) -> int:
    """Recompute the ``functions`` table from instructions and xrefs.

    Four independent signals for a function start, unioned:

    * being the target of a ``call`` -- near-certain;
    * being a thunk's destination (see :func:`resolve_thunks`);
    * being pointed at by a ``.data``/``.rdata`` dword, i.e. a vtable slot --
      this finds the thousands of virtual methods that are never called
      directly, and needs the ``.exe`` (see :func:`scan_pe_code_pointers`);
    * an MSVC ``push ebp`` / ``mov ebp, esp`` prologue, plus the first real
      instruction after a run of ``int 3`` / ``BYTE n DUP(0)`` inter-function
      padding, provided nothing jumps to it.

    Deliberately *not* used: "anything after a ``ret``".  This build omits
    frame pointers and pads only sporadically, so that rule roughly doubles the
    function count by splitting at every internal early-return.

    Ends are "up to the next start", trimmed of trailing padding.  A function
    reached only through a jump table and with no other evidence will still be
    absorbed into its predecessor.
    """
    resolve_thunks(conn)

    starts: set[int] = {
        row[0]
        for row in conn.execute(
            "SELECT DISTINCT to_address FROM xrefs "
            "WHERE kind IN ('call','call_direct','call_thunk')"
        )
        if TEXT_START <= row[0] < TEXT_END
    }
    starts |= {row[0] for row in conn.execute("SELECT target_address FROM thunks")}
    starts |= {row[0] for row in conn.execute("SELECT code_address FROM code_pointers")}

    jump_targets = {
        row[0]
        for row in conn.execute(
            "SELECT DISTINCT to_address FROM xrefs WHERE kind LIKE 'jump%'"
        )
    }

    rows = list(
        conn.execute(
            "SELECT address, mnemonic, operands, is_data, LENGTH(raw_bytes)/2 "
            "FROM instructions WHERE address >= ? AND address < ? ORDER BY address",
            (TEXT_START, TEXT_END),
        )
    )
    padded = False
    for i, (a, mn, ops, is_data, _size) in enumerate(rows):
        if mn == "push" and ops == "ebp" and i + 1 < len(rows):
            b_mn, b_ops = rows[i + 1][1], rows[i + 1][2]
            if b_mn == "mov" and b_ops.replace(" ", "") == "ebp,esp":
                starts.add(a)
        if is_data or mn == "int":
            padded = True
            continue
        if padded:
            if a not in jump_targets:
                starts.add(a)
            padded = False

    ordered = sorted(starts)
    if not ordered:
        return 0

    # Index instruction positions once so end-trimming is a slice, not a query.
    addr_at = [r[0] for r in rows]
    pos = {a: i for i, a in enumerate(addr_at)}

    records = []
    for idx, start in enumerate(ordered):
        limit = ordered[idx + 1] if idx + 1 < len(ordered) else TEXT_END
        j = pos.get(start)
        if j is None:
            continue
        k = j
        last_real = j
        while k < len(rows) and addr_at[k] < limit:
            mn, is_data = rows[k][1], rows[k][3]
            if not (is_data or mn == "int"):
                last_real = k
            k += 1
        # end_address is the last *byte* of the last real instruction, so that
        # a one-instruction thunk reports its true five-byte size.
        records.append((start, addr_at[last_real] + max(rows[last_real][4], 1) - 1))

    thunks = {row[0]: row[1] for row in conn.execute("SELECT thunk_address, target_address FROM thunks")}
    conn.execute("DELETE FROM functions")
    conn.executemany(
        "INSERT INTO functions(start_address, end_address, inferred_name, confidence) "
        "VALUES(?,?,?, 'none')",
        [(s, e, f"thunk_to_{thunks[s]:08X}" if s in thunks else None) for s, e in records],
    )
    conn.commit()
    return len(records)


# --------------------------------------------------------------------------
# Module attribution
# --------------------------------------------------------------------------

def find_module_refs(conn: sqlite3.Connection) -> int:
    """Populate ``module_refs``: code addresses that reference a filename literal.

    Operand matching, not annotation matching.  W32Dasm annotates only a subset
    of the pushes (42 of the 55 files get at least one annotation, and even
    annotated files have unannotated sites), whereas the operand *is* the
    literal's address and is always there.
    """
    literals = {
        row["data_address"]: row["filename"]
        for row in conn.execute("SELECT data_address, filename FROM module_strings")
    }
    if not literals:
        return 0
    annotated = {
        row[0]
        for row in conn.execute(
            r"SELECT address FROM string_refs WHERE string_value LIKE '%:\Ambit\%'"
        )
    }

    found: list[tuple] = []
    prev_imm: int | None = None
    for address, mnemonic, operands in conn.execute(
        "SELECT address, mnemonic, operands FROM instructions "
        "WHERE address >= ? AND address < ? AND is_data = 0 ORDER BY address",
        (TEXT_START, TEXT_END),
    ):
        hit = None
        for tok in _HEX8.findall(operands):
            value = int(tok, 16)
            if value in literals:
                hit = value
                break
        if hit is not None:
            # MSVC's assert emits `push <line>` immediately before `push <file>`,
            # so the previous immediate is the source line number.
            found.append((address, hit, literals[hit], prev_imm, int(address in annotated)))
        if mnemonic == "push" and _IMM.match(operands.strip()):
            prev_imm = int(operands.strip(), 16)
        else:
            prev_imm = None

    conn.execute("DELETE FROM module_refs")
    conn.executemany(
        "INSERT OR REPLACE INTO module_refs"
        "(address, data_address, filename, assert_line, annotated) VALUES(?,?,?,?,?)",
        found,
    )
    conn.commit()
    return len(found)


def derive_modules(conn: sqlite3.Connection) -> dict[str, int]:
    """Attribute every detected function to a source file where possible."""
    find_module_refs(conn)

    funcs = list(
        conn.execute(
            "SELECT start_address, end_address FROM functions ORDER BY start_address"
        )
    )
    if not funcs:
        return {}
    starts = [f[0] for f in funcs]
    module: list[str | None] = [None] * len(funcs)
    confidence: list[str] = ["none"] * len(funcs)
    evidence: list[str | None] = [None] * len(funcs)
    anchors: list[int] = [0] * len(funcs)

    import bisect

    def index_of(address: int) -> int | None:
        i = bisect.bisect_right(starts, address) - 1
        if i < 0:
            return None
        return i if address <= funcs[i][1] else None

    # -- pass A: hard anchors ------------------------------------------------
    votes: dict[int, dict[str, int]] = {}
    for address, filename in conn.execute("SELECT address, filename FROM module_refs"):
        i = index_of(address)
        if i is None:
            continue
        votes.setdefault(i, {}).setdefault(filename, 0)
        votes[i][filename] += 1
    for i, tally in votes.items():
        best = max(tally.items(), key=lambda kv: kv[1])
        module[i] = best[0]
        confidence[i] = "high"
        anchors[i] = sum(tally.values())
        evidence[i] = f"{sum(tally.values())} assert site(s)"

    # -- pass B: interpolate between like anchors ---------------------------
    # The linker emits each .obj's code contiguously, so a gap bracketed by two
    # anchors naming the same file is almost certainly that file too.
    anchor_idx = [i for i in range(len(funcs)) if confidence[i] == "high"]
    for a, b in zip(anchor_idx, anchor_idx[1:]):
        if module[a] != module[b] or b - a < 2:
            continue
        for i in range(a + 1, b):
            module[i] = module[a]
            confidence[i] = "medium"
            evidence[i] = f"between anchors {funcs[a][0]:08X} and {funcs[b][0]:08X}"

    # -- pass C: vtable cohorts ---------------------------------------------
    # A run of consecutive dwords in .data pointing into .text is one class's
    # vtable.  All its slots are methods of that class, hence of one source
    # file, so a single anchored slot colours the whole table.
    slots = list(
        conn.execute("SELECT data_address, code_address FROM code_pointers ORDER BY data_address")
    )
    tables: list[list[int]] = []
    run: list[int] = []
    prev_addr: int | None = None
    for data_address, code_address in slots:
        if prev_addr is not None and data_address != prev_addr + 4:
            if len(run) > 1:
                tables.append(run)
            run = []
        run.append(code_address)
        prev_addr = data_address
    if len(run) > 1:
        tables.append(run)

    for table in tables:
        indices = {index_of(a) for a in table}
        indices.discard(None)
        named = {module[i] for i in indices if module[i] is not None}
        if len(named) != 1:
            continue  # empty or ambiguous -- refuse to guess
        name = next(iter(named))
        for i in indices:
            if module[i] is None:
                module[i] = name
                confidence[i] = "medium"
                evidence[i] = f"vtable cohort ({len(table)} slots)"

    # -- pass D: unanimous-caller vote --------------------------------------
    thunk_starts = {
        index_of(row[0]) for row in conn.execute("SELECT thunk_address FROM thunks")
    }
    callers: dict[int, set[str]] = {}
    for to_addr, from_addr in conn.execute(
        "SELECT to_address, from_address FROM xrefs "
        "WHERE kind IN ('call','call_direct','call_thunk')"
    ):
        ti = index_of(to_addr)
        fi = index_of(from_addr)
        if ti is None or fi is None or fi in thunk_starts or ti in thunk_starts:
            continue
        callers.setdefault(ti, set()).add(fi)
    # Two rounds: a function coloured in round one can colour its own callees.
    for _ in range(2):
        for i in range(len(funcs)):
            if module[i] is not None:
                continue
            names = {module[fi] for fi in callers.get(i, ()) if module[fi] is not None}
            if len(names) == 1:
                module[i] = next(iter(names))
                confidence[i] = "low"
                evidence[i] = "all known callers in this module"

    # Thunks are not code of their own: mirror whatever their target resolved
    # to, and never let them vote (they would smear one module over the whole
    # thunk table via the caller heuristic).
    thunk_map = {
        row[0]: row[1] for row in conn.execute("SELECT thunk_address, target_address FROM thunks")
    }
    for thunk_address, target in thunk_map.items():
        ti = index_of(thunk_address)
        gi = index_of(target)
        if ti is None:
            continue
        if gi is not None and module[gi] is not None:
            module[ti], confidence[ti] = module[gi], confidence[gi]
            evidence[ti] = f"incremental-link thunk to {target:08X}"
        else:
            module[ti], confidence[ti], evidence[ti] = None, "none", None

    conn.executemany(
        "UPDATE functions SET inferred_module=?, confidence=?, evidence=? WHERE start_address=?",
        [
            (module[i], confidence[i], evidence[i], funcs[i][0])
            for i in range(len(funcs))
        ],
    )
    conn.commit()

    summary: dict[str, int] = {}
    for c in confidence:
        summary[c] = summary.get(c, 0) + 1
    return summary


def module_ranges(conn: sqlite3.Connection) -> list[ModuleRange]:
    """Collapse per-function attribution into contiguous address ranges."""
    rows = list(
        conn.execute(
            "SELECT f.start_address, f.end_address, f.inferred_module, f.confidence, "
            "(SELECT COUNT(*) FROM module_refs m "
            " WHERE m.address BETWEEN f.start_address AND f.end_address) AS anchors "
            "FROM functions f WHERE f.inferred_module IS NOT NULL "
            "ORDER BY f.start_address"
        )
    )
    ranges: list[ModuleRange] = []
    cur: list = []
    for row in rows:
        if cur and row["inferred_module"] == cur[0] and row["confidence"] == cur[3]:
            cur[2] = row["end_address"]
            cur[4] += 1
            cur[5] += row["anchors"]
            continue
        if cur:
            ranges.append(ModuleRange(cur[0], cur[1], cur[2], cur[3], cur[4], cur[5]))
        cur = [
            row["inferred_module"], row["start_address"], row["end_address"],
            row["confidence"], 1, row["anchors"],
        ]
    if cur:
        ranges.append(ModuleRange(cur[0], cur[1], cur[2], cur[3], cur[4], cur[5]))
    return ranges
