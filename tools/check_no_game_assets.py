"""Fail if copyrighted game data has been committed to the repository.

The project's legal position depends on never redistributing original
Imperialism content: scenario data, extracted art, the disassembly listing,
and save games all belong to their copyright holders. Conventions decay, so
this runs in CI and turns that rule into a build failure.

Checks tracked files only — anything gitignored is by definition not
committed, and `fixtures/local_only/` is expected to hold real files locally.
"""
from __future__ import annotations

import subprocess
import sys
from pathlib import Path

# Extensions that only ever belong to the original game.
FORBIDDEN_SUFFIXES = {
    ".map": "original scenario map",
    ".scn": "original scenario data",
    ".inf": "original scenario briefing",
    ".imp": "original save game",
    ".gob": "original resource archive",
    ".alf": "disassembly listing of the original executable",
    ".wpj": "original build project file",
    ".exe": "executable",
    ".dll": "library binary",
    ".irg": "original config archive",
}

# Media that would indicate extracted assets.
FORBIDDEN_MEDIA = {".avi", ".ogg", ".wav", ".bmp", ".pcx", ".ttf"}

# Any tracked file larger than this is suspicious for a source-only repo.
MAX_TRACKED_BYTES = 2 * 1024 * 1024


def tracked_files() -> list[str]:
    out = subprocess.run(
        ["git", "ls-files", "-z"],
        capture_output=True,
        check=True,
        text=True,
    ).stdout
    return [p for p in out.split("\0") if p]


def main() -> int:
    problems: list[str] = []

    for rel in tracked_files():
        path = Path(rel)
        suffix = path.suffix.lower()

        if suffix in FORBIDDEN_SUFFIXES:
            problems.append(f"{rel}: looks like {FORBIDDEN_SUFFIXES[suffix]}")
        elif suffix in FORBIDDEN_MEDIA:
            problems.append(f"{rel}: media file, likely an extracted game asset")

        # Size check catches renamed or extension-less blobs.
        try:
            size = path.stat().st_size
        except OSError:
            continue
        if size > MAX_TRACKED_BYTES:
            problems.append(f"{rel}: {size:,} bytes exceeds the {MAX_TRACKED_BYTES:,} limit")

    if problems:
        print("Copyrighted or oversized game data must never be committed:\n")
        for problem in sorted(set(problems)):
            print(f"  - {problem}")
        print(
            "\nKeep original game files in fixtures/local_only/ (gitignored) "
            "or outside the repo entirely."
        )
        return 1

    print("No committed game assets found.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
