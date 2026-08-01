"""Emit stable JSON summaries of Imperialism scenario source files."""
from __future__ import annotations

import argparse
import hashlib
import json
from collections import Counter
from pathlib import Path

from imperialism_format import MapFile, MapFormatProfile, ScenarioFile, ScenarioInfo


def map_summary(path: Path, width: int, height: int) -> dict:
    profile = MapFormatProfile(width=width, height=height)
    map_file = MapFile.load(str(path), profile=profile)
    return {
        "type": "map",
        "path": str(path),
        "sha256": hashlib.sha256(path.read_bytes()).hexdigest(),
        "width": map_file.width,
        "height": map_file.height,
        "cell_count": len(map_file.cells),
        "trailer_bytes": len(map_file.dormant_trailer),
        "terrain_counts": dict(sorted(Counter(c.terrain for c in map_file.cells).items())),
        "province_count": len({c.province for c in map_file.cells}),
    }


def scenario_summary(path: Path) -> dict:
    scenario = ScenarioFile.load(str(path))
    names = [
        {"tag": record.tag, "id": record.fields[0], "name": record.name}
        for record in scenario.records
        if record.name is not None
    ]
    return {
        "type": "scenario",
        "path": str(path),
        "sha256": hashlib.sha256(path.read_bytes()).hexdigest(),
        "record_count": len(scenario.records),
        "tag_counts": dict(sorted(Counter(r.tag for r in scenario.records).items())),
        "names": names,
        "trailing_bytes": len(scenario.trailing_bytes),
    }


def info_summary(path: Path) -> dict:
    result = ScenarioInfo.load(str(path)).to_dict()
    result.update(
        {
            "type": "scenario_info",
            "path": str(path),
            "sha256": hashlib.sha256(path.read_bytes()).hexdigest(),
        }
    )
    return result


def inspect(path: Path, width: int, height: int) -> dict:
    suffix = path.suffix.lower()
    if suffix == ".map":
        return map_summary(path, width, height)
    if suffix == ".scn":
        return scenario_summary(path)
    if suffix == ".inf":
        return info_summary(path)
    raise ValueError(f"unsupported file extension {path.suffix!r}")


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("paths", nargs="+", type=Path)
    parser.add_argument("--width", type=int, default=108)
    parser.add_argument("--height", type=int, default=60)
    args = parser.parse_args()
    results = [inspect(path, args.width, args.height) for path in args.paths]
    print(json.dumps(results, indent=2, sort_keys=True))


if __name__ == "__main__":
    main()
