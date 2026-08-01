"""Tooling for indexing the W32Dasm disassembly listing of Imperialism.exe.

The listing itself (``Imperialism.alf``) and the executable it came from are
copyrighted and live outside this repository.  Everything here reads them from
an absolute path supplied at runtime and writes a derived SQLite index that is
likewise kept out of version control.
"""
from __future__ import annotations

__all__ = ["listing", "db", "index", "modules", "query"]
