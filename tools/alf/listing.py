"""Streaming parser for W32Dasm ``.alf`` disassembly listings.

Why a hand-rolled parser: W32Dasm's output is a fixed-column report, not a
machine format.  Its only structure is that *annotation blocks* (lines starting
with ``*``, continued by lines starting with ``|``) describe the **next**
disassembled line.  Everything downstream -- cross references, string
references, the assert-filename module map -- is recovered by pairing each
annotation block with the instruction that follows it, so that pairing lives in
one place here and is unit-testable against tiny inline fixtures.

The parser is a generator over lines and never materialises the file, which
matters: the real listing is 59 MB / 1.39 M lines.

Format notes (reverse-engineered; see ``docs/disasm/README.md``):

* A disassembled line is ``:AAAAAAAA <hex bytes><pad to col 34><text>``.  The
  raw-byte field is padded to 24 characters, but long instructions overflow it,
  so we split on "two or more spaces" instead of a hard column.
* Padding and alignment gaps appear as ``BYTE  4 DUP(0)`` in the same shape as
  an instruction; they are kept but flagged ``is_data``.
* Annotation source lists wrap across several ``|`` lines and are terminated by
  a bare ``|``.  Jump sources carry a ``(C)``/``(U)`` suffix; call sources do
  not.
* Mnemonics are lower-case except ``Call``/``BYTE``/``DWORD``-style pseudo-ops,
  and raw bytes are always upper-case hex, which is what lets the split regex
  stay unambiguous.
"""
from __future__ import annotations

import re
from dataclasses import dataclass, field
from typing import Iterable, Iterator

# ``:00401000 E93B881300              jmp 00539840``
#
# The two-or-more-space separator is deliberate: it survives instructions whose
# encoding is longer than the 24-column raw-byte field.
_LINE_RE = re.compile(r"^:([0-9A-Fa-f]{8}) ([0-9A-Fa-f]*)\s{2,}(\S.*?)\s*$")

_XREF_JUMP_RE = re.compile(
    r"^\* Referenced by an? \(U\)nconditional or \(C\)onditional Jump at Address(?:es)?:"
)
_XREF_CALL_RE = re.compile(r"^\* Referenced by a CALL at Address(?:es)?:")
_STRING_RE = re.compile(
    r'^\* Possible (Indirect )?StringData Ref from (Data|Code) Obj ->"(.*)"\s*$'
)
_IMPORT_RE = re.compile(
    r"^\* Reference To: ([^.,]+)\.([^,]+?)(?:, Ord:([0-9A-Fa-f]+)h)?\s*$"
)
_RESOURCE_RE = re.compile(r"^\* Possible (?:Reference|Ref) to (Dialog|Menu|String Resource[^:]*):")
_SOURCE_ADDR_RE = re.compile(r":([0-9A-Fa-f]{8})(?:\(([CU])\))?")

#: Marks the start of the disassembly proper.  Everything before it is the
#: menu/dialog/import report, which uses a different (and unrelated) layout.
CODE_LISTING_MARKER = "ASSEMBLY CODE LISTING"

#: W32Dasm's sentinel final line.
END_OF_LISTING = ":FFFFFFFF"


@dataclass(frozen=True)
class Xref:
    """A caller/jumper recorded in a ``Referenced by ...`` annotation block."""

    from_address: int
    kind: str  # "call", "jump_cond", "jump_uncond", "jump"


@dataclass(frozen=True)
class StringRef:
    """A ``Possible StringData Ref`` annotation.

    ``origin`` is ``data`` or ``code``; ``indirect`` records whether W32Dasm
    thought the reference went through a pointer, which matters because indirect
    refs are markedly less reliable.
    """

    value: str
    origin: str
    indirect: bool = False


@dataclass(frozen=True)
class ImportRef:
    """A ``Reference To: KERNEL32.GetVersion, Ord:014Ch`` annotation."""

    module: str
    symbol: str
    ordinal: int | None = None


@dataclass
class ListingLine:
    """One disassembled address plus every annotation attached to it."""

    address: int
    line_no: int
    raw_bytes: str
    mnemonic: str
    operands: str
    is_data: bool = False
    xrefs: list[Xref] = field(default_factory=list)
    strings: list[StringRef] = field(default_factory=list)
    imports: list[ImportRef] = field(default_factory=list)
    resources: list[str] = field(default_factory=list)

    @property
    def text(self) -> str:
        return f"{self.mnemonic} {self.operands}".strip()


def _split_text(text: str) -> tuple[str, str, bool]:
    """Split the disassembled text into (mnemonic, operands, is_data)."""
    head, _, tail = text.partition(" ")
    is_data = head.isupper() and head.isalpha()
    return head, tail.strip(), is_data


def _parse_operand_addresses(operands: str) -> list[int]:
    """Every 8-hex-digit token in an operand string, as integers.

    Used for call/jump targets and, crucially, for spotting pushes of the
    assert-filename string addresses even where W32Dasm declined to annotate
    them.
    """
    return [int(m, 16) for m in re.findall(r"\b([0-9A-F]{8})\b", operands)]


def call_target(line: ListingLine) -> int | None:
    """Direct (non-indirect) call target of ``line``, if it has one."""
    if line.mnemonic.lower() != "call":
        return None
    operands = line.operands.strip()
    if not re.fullmatch(r"[0-9A-F]{8}", operands):
        return None  # register or memory-indirect call
    return int(operands, 16)


class _PendingAnnotations:
    """Annotation block accumulator, drained onto the next disassembled line."""

    def __init__(self) -> None:
        self.xrefs: list[Xref] = []
        self.strings: list[StringRef] = []
        self.imports: list[ImportRef] = []
        self.resources: list[str] = []
        self.pending_kind: str | None = None

    def __bool__(self) -> bool:
        return bool(self.xrefs or self.strings or self.imports or self.resources)

    def clear(self) -> None:
        self.xrefs = []
        self.strings = []
        self.imports = []
        self.resources = []
        self.pending_kind = None

    def start_header(self, raw: str) -> bool:
        """Consume an annotation header line.  Returns True if recognised."""
        if _XREF_JUMP_RE.match(raw):
            self.pending_kind = "jump"
            return True
        if _XREF_CALL_RE.match(raw):
            self.pending_kind = "call"
            return True
        m = _STRING_RE.match(raw)
        if m:
            self.pending_kind = None
            self.strings.append(
                StringRef(value=m.group(3), origin=m.group(2).lower(), indirect=bool(m.group(1)))
            )
            return True
        m = _IMPORT_RE.match(raw)
        if m:
            self.pending_kind = None
            ordinal = int(m.group(3), 16) if m.group(3) else None
            self.imports.append(ImportRef(m.group(1).strip(), m.group(2).strip(), ordinal))
            return True
        m = _RESOURCE_RE.match(raw)
        if m:
            self.pending_kind = None
            self.resources.append(raw.lstrip("* ").rstrip())
            return True
        # Unrecognised ``*`` line: remember nothing but do not treat it as data.
        self.pending_kind = None
        return True

    def continuation(self, raw: str) -> None:
        """Consume a ``|`` continuation line of the current block."""
        if self.pending_kind is None:
            return
        for addr, flag in _SOURCE_ADDR_RE.findall(raw):
            if self.pending_kind == "call":
                kind = "call"
            elif flag == "C":
                kind = "jump_cond"
            elif flag == "U":
                kind = "jump_uncond"
            else:
                kind = "jump"
            self.xrefs.append(Xref(int(addr, 16), kind))


def parse_listing(
    lines: Iterable[str],
    *,
    start_line: int = 1,
    require_marker: bool = False,
) -> Iterator[ListingLine]:
    """Yield one :class:`ListingLine` per disassembled address.

    ``start_line`` is the 1-based line number of the first element of ``lines``;
    it lets the indexer resume mid-file and still report true line numbers.
    ``require_marker`` skips everything before the ``ASSEMBLY CODE LISTING``
    banner -- off by default so that tests can feed bare snippets.
    """
    pending = _PendingAnnotations()
    in_code = not require_marker
    line_no = start_line - 1

    for raw in lines:
        line_no += 1
        raw = raw.rstrip("\n").rstrip("\r")
        if not in_code:
            if CODE_LISTING_MARKER in raw:
                in_code = True
            continue
        if not raw.strip():
            continue
        if raw.startswith("*"):
            pending.start_header(raw)
            continue
        if raw.startswith("|"):
            pending.continuation(raw)
            continue
        if raw.startswith(END_OF_LISTING):
            break
        m = _LINE_RE.match(raw)
        if not m:
            continue
        mnemonic, operands, is_data = _split_text(m.group(3))
        yield ListingLine(
            address=int(m.group(1), 16),
            line_no=line_no,
            raw_bytes=m.group(2).upper(),
            mnemonic=mnemonic,
            operands=operands,
            is_data=is_data,
            xrefs=pending.xrefs,
            strings=pending.strings,
            imports=pending.imports,
            resources=pending.resources,
        )
        pending.clear()
