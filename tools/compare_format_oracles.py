"""Compare independent Python and C# structural summaries for format files."""
from __future__ import annotations

import argparse
import hashlib
import json
import struct
import subprocess
import sys
import tempfile
from collections import Counter
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "src"))

from imperialism_format import HexCell, MapFile, MapFormatProfile, ScenarioFile, ScenarioInfo


INSPECTOR = ROOT / "tools" / "Imperialism.FormatInspector" / "Imperialism.FormatInspector.csproj"


def digest(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def record_digest(record) -> str:
    name = b"" if record.name is None else record.name.encode("utf-8")
    payload = record.tag.encode("ascii")
    payload += b"".join(struct.pack(">I", value) for value in record.fields)
    payload += struct.pack(">I", len(name)) + name
    return digest(payload)


def map_summary(path: Path, profile: MapFormatProfile) -> dict:
    document = MapFile.load(str(path), profile=profile)
    encoded_cells = [cell.to_bytes() for cell in document.cells]
    return {
        "type": "map",
        "path": str(path.resolve()),
        "width": document.width,
        "height": document.height,
        "cell_count": len(document.cells),
        "field_hashes": {
            f"byte_{offset:02d}": digest(bytes(cell[offset] for cell in encoded_cells))
            for offset in range(36)
        },
        "preserved_hashes": {
            "encoded_bytes": digest(document.to_bytes()),
            "source_bytes": digest(path.read_bytes()),
            "trailer_bytes": digest(document.dormant_trailer),
        },
    }


def scenario_summary(path: Path, plaintext: bool) -> dict:
    document = (
        ScenarioFile.load_text(str(path)) if plaintext else ScenarioFile.load(str(path))
    )
    preserved = {
        "trailing_bytes": digest(document.trailing_bytes),
        "raw_name_fields": [
            digest(record.raw_name_field)
            for record in document.records
            if record.raw_name_field is not None
        ],
    }
    if not plaintext:
        preserved["encoded_bytes"] = digest(document.to_bytes())
        preserved["source_bytes"] = digest(path.read_bytes())
    return {
        "type": "scenario_text" if plaintext else "scenario",
        "path": str(path.resolve()),
        "record_count": len(document.records),
        "tag_counts": dict(sorted(Counter(record.tag for record in document.records).items())),
        "record_hashes": [record_digest(record) for record in document.records],
        "preserved_hashes": preserved,
    }


def info_summary(path: Path) -> dict:
    raw = path.read_bytes()
    document = ScenarioInfo.from_bytes(raw)
    sections = {"title": digest(document.title.encode("utf-8"))}
    sections["overview"] = digest(document.overview.encode("utf-8"))
    sections.update(
        {
            f"country_{index}": digest(section.encode("utf-8"))
            for index, section in enumerate(document.country_sections)
        }
    )
    return {
        "type": "scenario_info",
        "path": str(path.resolve()),
        "section_hashes": dict(sorted(sections.items())),
        "metadata": document.metadata,
        "preserved_hashes": {
            "encoded_bytes": digest(document.to_bytes()),
            "raw_bytes": digest(raw),
        },
    }


def summarize(path: Path, profile: MapFormatProfile) -> dict:
    suffix = path.suffix.lower()
    if suffix == ".map":
        return map_summary(path, profile)
    if suffix == ".scn":
        return scenario_summary(path, False)
    if suffix == ".inf":
        return info_summary(path)
    if not suffix:
        return scenario_summary(path, True)
    raise ValueError(f"unsupported file extension {suffix!r}")


def create_generated_corpus(directory: Path, profile: MapFormatProfile) -> list[Path]:
    cells = [
        HexCell.from_bytes(bytes((cell_index + offset) % 256 for offset in range(36)))
        for cell_index in range(profile.cell_count)
    ]
    map_path = directory / "generated.map"
    map_path.write_bytes(
        MapFile(
            profile=profile,
            cells=cells,
            dormant_trailer=bytes(range(profile.trailer_size)),
        ).to_bytes()
    )

    raw_name = b"Generated Republic\0" + b"padding evidence"
    raw_name = raw_name.ljust(64, b"!")
    scenario_path = directory / "generated.scn"
    scenario_path.write_bytes(
        b"cnam" + struct.pack(">I", 0) + raw_name
        + b"year" + struct.pack(">I", 5)
        + b"cash" + struct.pack(">II", 0, 2500)
        + b"TERM\x09\x08\x07"
    )

    info_path = directory / "generated.inf"
    info_path.write_bytes(
        "L'été nouveau\n#\nOverview\n"
        "#\nCountry 0\n#\nCountry 1\n#\nCountry 2\n#\nCountry 3\n"
        "#\nCountry 4\n#\nCountry 5\n#\nCountry 6\n# 2 -1 4 3 2 4 1 0"
        .encode("cp1252")
    )

    text_path = directory / "generated"
    text_path.write_bytes(b"year 5\r\nzone 40 Port Said\r\ncash 0 2500\r\n")
    return [map_path, scenario_path, info_path, text_path]


def corpus_paths(directory: Path) -> list[Path]:
    return sorted(
        (
            path
            for path in directory.iterdir()
            if path.suffix.lower() in {".map", ".scn", ".inf"} or not path.suffix
        ),
        key=lambda path: path.name,
    )


def csharp_summaries(
    paths: list[Path], profile: MapFormatProfile, no_build: bool
) -> list[dict]:
    if not no_build:
        subprocess.run(
            ["dotnet", "build", str(ROOT / "Imperialism.sln"), "--configuration", "Release"],
            cwd=ROOT,
            check=True,
        )
    command = [
        "dotnet", "run", "--project", str(INSPECTOR), "--configuration", "Release",
        "--no-build", "--",
        "--width", str(profile.width),
        "--height", str(profile.height),
        "--trailer-count", str(profile.trailer_record_count),
        "--trailer-size", str(profile.trailer_record_size),
        *(str(path.resolve()) for path in paths),
    ]
    result = subprocess.run(command, cwd=ROOT, check=True, capture_output=True, text=True)
    return json.loads(result.stdout)


def compare(paths: list[Path], profile: MapFormatProfile, no_build: bool) -> list[dict]:
    python_results = [summarize(path, profile) for path in paths]
    csharp_results = csharp_summaries(paths, profile, no_build)
    if len(python_results) != len(csharp_results):
        raise RuntimeError(
            f"oracle result count differs: Python={len(python_results)}, C#={len(csharp_results)}"
        )
    for path, python_result, csharp_result in zip(paths, python_results, csharp_results):
        if python_result != csharp_result:
            print(f"Oracle disagreement for {path}:")
            print(json.dumps({"python": python_result, "csharp": csharp_result}, indent=2))
            raise SystemExit(1)
        preserved = python_result["preserved_hashes"]
        source_key = "raw_bytes" if python_result["type"] == "scenario_info" else "source_bytes"
        if source_key in preserved and preserved.get("encoded_bytes") != preserved[source_key]:
            raise SystemExit(f"Round-trip bytes differ for {path}")
    return python_results


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("paths", nargs="*", type=Path)
    parser.add_argument("--corpus", type=Path)
    parser.add_argument("--generated", action="store_true")
    parser.add_argument("--no-build", action="store_true")
    parser.add_argument("--json", action="store_true")
    parser.add_argument("--width", type=int, default=108)
    parser.add_argument("--height", type=int, default=60)
    parser.add_argument("--trailer-count", type=int, default=384)
    parser.add_argument("--trailer-size", type=int, default=198)
    args = parser.parse_args()

    if sum((bool(args.paths), args.corpus is not None, args.generated)) != 1:
        parser.error("choose exactly one of paths, --corpus, or --generated")

    if args.generated:
        profile = MapFormatProfile(7, 5, 2, 3)
        with tempfile.TemporaryDirectory(prefix="imperialism-oracle-") as temp:
            paths = create_generated_corpus(Path(temp), profile)
            results = compare(paths, profile, args.no_build)
    else:
        profile = MapFormatProfile(
            args.width, args.height, args.trailer_count, args.trailer_size
        )
        paths = corpus_paths(args.corpus) if args.corpus else args.paths
        results = compare(paths, profile, args.no_build)

    if args.json:
        print(json.dumps(results, indent=2, sort_keys=True))
    else:
        print(f"Cross-oracle agreement: {len(results)} files.")


if __name__ == "__main__":
    main()
