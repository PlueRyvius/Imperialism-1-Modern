"""Compare plaintext scenario sources with shipped binary scenarios.

The extensionless files are not reliably paired with the same-numbered
``.scn`` files.  This tool therefore compares every text file with every
binary and reports aggregate relationships without printing source records.

Usage::

    python tools/audit_scenario_corpus.py /path/to/Scenario
    python tools/audit_scenario_corpus.py /path/to/Scenario --json
"""
from __future__ import annotations

import argparse
import json
import re
import sys
from collections import Counter
from dataclasses import asdict, dataclass
from pathlib import Path

if __package__ in (None, ""):  # allow `python tools/audit_scenario_corpus.py`
    sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "src"))

from imperialism_format import ScenarioFile

NEAR_MATCH_THRESHOLD = 0.5
_SCENARIO_NAME_RE = re.compile(r"s(\d+)", re.IGNORECASE)


@dataclass(frozen=True)
class PairComparison:
    binary: str
    relationship: str
    text_records: int
    binary_records: int
    shared_records: int
    similarity: float


def _record_keys(scenario: ScenarioFile) -> list[tuple[str, tuple[int, ...], str | None]]:
    return [
        (record.tag, tuple(record.fields), record.name)
        for record in scenario.records
    ]


def _is_ordered_subset(source: list[tuple], target: list[tuple]) -> bool:
    if not source or len(source) >= len(target):
        return False
    target_iter = iter(target)
    return all(any(candidate == item for candidate in target_iter) for item in source)


def compare_records(
    text_scenario: ScenarioFile,
    binary_scenario: ScenarioFile,
    *,
    binary_name: str,
) -> PairComparison:
    """Classify a text/binary pair using semantic records, not formatting."""
    text_records = _record_keys(text_scenario)
    binary_records = _record_keys(binary_scenario)
    shared = sum((Counter(text_records) & Counter(binary_records)).values())
    denominator = len(text_records) + len(binary_records)
    similarity = (2.0 * shared / denominator) if denominator else 1.0

    if text_records == binary_records:
        relationship = "exact"
    elif _is_ordered_subset(text_records, binary_records):
        relationship = "ordered subset"
    elif similarity >= NEAR_MATCH_THRESHOLD:
        relationship = "near match"
    else:
        relationship = "unrelated"

    return PairComparison(
        binary=binary_name,
        relationship=relationship,
        text_records=len(text_records),
        binary_records=len(binary_records),
        shared_records=shared,
        similarity=round(similarity, 6),
    )


def _sort_key(path: Path) -> tuple[int, str]:
    match = _SCENARIO_NAME_RE.fullmatch(path.stem if path.suffix else path.name)
    return (int(match.group(1)), path.name.lower()) if match else (sys.maxsize, path.name.lower())


def audit_corpus(directory: Path) -> dict:
    """Return a stable, JSON-serialisable audit of a scenario directory."""
    if not directory.is_dir():
        raise ValueError(f"scenario directory not found: {directory}")

    paths = list(directory.iterdir())
    text_paths = sorted(
        (path for path in paths if path.is_file() and not path.suffix and _SCENARIO_NAME_RE.fullmatch(path.name)),
        key=_sort_key,
    )
    binary_paths = sorted(
        (path for path in paths if path.is_file() and path.suffix.lower() == ".scn" and _SCENARIO_NAME_RE.fullmatch(path.stem)),
        key=_sort_key,
    )
    if not text_paths:
        raise ValueError(f"no extensionless scenario files found in {directory}")
    if not binary_paths:
        raise ValueError(f"no .scn files found in {directory}")

    text_scenarios = {path.name: ScenarioFile.load_text(str(path)) for path in text_paths}
    binary_scenarios = {path.stem: ScenarioFile.load(str(path)) for path in binary_paths}

    scenarios = []
    for text_name, text_scenario in text_scenarios.items():
        comparisons = [
            compare_records(text_scenario, binary, binary_name=binary_name)
            for binary_name, binary in binary_scenarios.items()
        ]
        comparisons.sort(key=lambda item: (-item.similarity, -item.shared_records, item.binary))
        same_name = next((item for item in comparisons if item.binary == text_name), None)
        scenarios.append(
            {
                "text": text_name,
                "text_records": len(text_scenario.records),
                "same_name": asdict(same_name) if same_name else None,
                "best_match": asdict(comparisons[0]),
            }
        )

    return {
        "text_files": len(text_paths),
        "binary_files": len(binary_paths),
        "scenarios": scenarios,
    }


def format_report(audit: dict) -> str:
    lines = [
        f"Scenario corpus: {audit['text_files']} text files, {audit['binary_files']} binary files",
        "",
        "text  records  same-name relationship  best binary  best relationship  shared  similarity",
        "----  -------  ----------------------  -----------  -----------------  ------  ----------",
    ]
    for scenario in audit["scenarios"]:
        same = scenario["same_name"]
        best = scenario["best_match"]
        same_relationship = same["relationship"] if same else "missing"
        lines.append(
            f"{scenario['text']:<4}  {scenario['text_records']:>7}  "
            f"{same_relationship:<22}  {best['binary']:<11}  "
            f"{best['relationship']:<17}  {best['shared_records']:>6}  "
            f"{best['similarity']:.6f}"
        )
    return "\n".join(lines)


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("directory", type=Path, help="path to the game's Scenario directory")
    parser.add_argument("--json", action="store_true", help="emit stable JSON instead of a table")
    args = parser.parse_args(argv)

    try:
        audit = audit_corpus(args.directory)
    except (OSError, UnicodeError, ValueError) as exc:
        print(f"error: {exc}", file=sys.stderr)
        return 2

    if args.json:
        print(json.dumps(audit, indent=2, sort_keys=True))
    else:
        print(format_report(audit))
    return 0


if __name__ == "__main__":
    sys.exit(main())
