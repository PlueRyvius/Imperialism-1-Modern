"""Compare a scenario against the shipped corpus and report where it leaves it.

A lead generator for "the game crashes on my generated world", run before
launching rather than after. It answers one question — *what does this file
contain that no file the game shipped contains?* — and both crashes diagnosed
on 2026-08-01 were sitting in its output.

    python tools/preflight.py E:/Imperialism/Scenario/s5
    python tools/preflight.py s5 --corpus E:/Imperialism/Scenario --verbose

**This reports, it does not judge.** Leaving the envelope is not proof of a
bug: nine scenarios are a small corpus and a generated world is entitled to be
new. Findings are leads, ordered by how much the corpus constrains the thing
they contradict. The validator in `tools/map_editor/validate.py` is the place
for rules certain enough to fail on.

Two ideas do the work.

**Codes versus indices.** A field whose shipped values form a small closed set
is a *code*, and a value outside that set is worth looking at. A field holding
hundreds of scattered values is an *index* into the map, and novelty there is
meaningless — of course a new world puts a railway on a cell no shipped world
used. Sweeping raw values without this distinction buries the one real signal
under five false ones, which is exactly what happened: `army` field 1 carrying
a type-3 record mattered; four "novel" cell indices did not.

**Projections.** The bug that actually crashed the game was invisible to any
value sweep, because every value involved was individually ordinary. What was
new was what those values *resolved to*: works sitting on cells owned by minor
nations, which no shipped scenario does, because the engine indexes a 7-slot
Great Power table with that owner. So each reference is followed into the map
and the properties it lands on are swept too. When a value diff comes up thin,
diff what the values mean.
"""
from __future__ import annotations

import argparse
import collections
import glob
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "src"))

from imperialism_format.map_file import MapFile
from imperialism_format.scn_file import ScenarioFile

#: Above this many distinct values across the corpus, a field is an index into
#: the map rather than a code, and novel values in it say nothing. The gap is
#: wide in practice -- codes top out around 16 values, cell indices run to
#: several hundred -- so the exact threshold is not delicate.
CODE_VOCABULARY_MAX = 40

#: Scenario fields holding a linear cell index, and which field it is.
CELL_FIELDS = {"deve": 0, "rail": 0, "port": 0, "civi": 1}

#: Scenario fields holding a province id.
PROVINCE_FIELDS = {"army": 0, "pnam": 0}

#: Scenarios that are this project's own output, not the game's. Kept in step
#: with `tests/originals.py`; admitting one would set the bar using the very
#: file under test.
NOT_ORIGINALS = {"s0", "s5"}


def _cell_properties(map_file, cell_index: int) -> dict:
    """What a cell reference lands on. The projections that matter."""
    if not 0 <= cell_index < len(map_file.cells):
        return {"cell-in-range": 0}
    cell = map_file.cells[cell_index]
    return {
        "owner-nation": cell.nation_zone_a,
        "terrain": cell.terrain,
        "town-type": cell.town_type,
        "is-ocean": int(cell.is_ocean()),
    }


def _province_owners(map_file) -> dict:
    """province id -> the nation byte its cells carry, by majority."""
    tally = collections.defaultdict(collections.Counter)
    for cell in map_file.cells:
        if not cell.is_ocean():
            tally[cell.province][cell.nation_zone_a] += 1
    return {p: c.most_common(1)[0][0] for p, c in tally.items()}


def observations(map_file, scenario) -> dict:
    """Every (aspect, key) -> set of values this scenario exhibits."""
    seen = collections.defaultdict(set)
    owners = _province_owners(map_file)
    towns = map_file.province_towns()

    for record in scenario.records:
        for i, value in enumerate(record.fields):
            seen[("field", f"{record.tag}[{i}]")].add(value)

        field = CELL_FIELDS.get(record.tag)
        if field is not None:
            for name, value in _cell_properties(
                    map_file, record.fields[field]).items():
                seen[("cell of", f"{record.tag} -> {name}")].add(value)

        field = PROVINCE_FIELDS.get(record.tag)
        if field is not None:
            province = record.fields[field]
            seen[("province of", f"{record.tag} -> exists")].add(
                int(province in owners))
            if province in owners:
                seen[("province of", f"{record.tag} -> owner-nation")].add(
                    owners[province])
                town = towns.get(province)
                if town is not None and 0 <= town < len(map_file.cells):
                    seen[("province of", f"{record.tag} -> town-type")].add(
                        map_file.cells[town].town_type)

    for cell in map_file.cells:
        for name, value in vars(cell).items():
            seen[("map byte", name)].add(value)

    return seen


def _tags(scenario) -> collections.Counter:
    return collections.Counter(r.tag for r in scenario.records)


def corpus_envelope(pairs: list) -> tuple[dict, dict]:
    """Union of everything the corpus exhibits, and its per-tag record counts."""
    envelope = collections.defaultdict(set)
    counts = collections.defaultdict(list)
    for map_path, scn_path in pairs:
        map_file, scenario = MapFile.load(map_path), ScenarioFile.load(scn_path)
        for key, values in observations(map_file, scenario).items():
            envelope[key] |= values
        for tag, n in _tags(scenario).items():
            counts[tag].append(n)
    return envelope, counts


def findings(candidate: tuple, envelope: dict, counts: dict,
             verbose: bool = False) -> list[dict]:
    """Where the candidate leaves the envelope, most constrained first."""
    map_file = MapFile.load(candidate[0])
    scenario = ScenarioFile.load(candidate[1])
    out = []

    for key, values in sorted(observations(map_file, scenario).items()):
        aspect, name = key
        known = envelope.get(key)
        if known is None:
            out.append({"aspect": aspect, "name": name, "novel": None,
                        "known": None, "rank": 1,
                        "note": "no shipped scenario has this at all"})
            continue
        novel = values - known
        if not novel:
            continue
        is_code = len(known) <= CODE_VOCABULARY_MAX
        if not is_code and not verbose:
            continue            # an index: novelty here means nothing
        out.append({
            "aspect": aspect, "name": name,
            "novel": sorted(novel), "known": sorted(known),
            # A projection contradicted is worth more than a raw field, and a
            # tight vocabulary is worth more than a loose one.
            "rank": (0 if aspect != "field" else 2) + (0 if is_code else 5),
            "note": "" if is_code else "wide-ranging field; novelty is weak",
        })

    for tag, n in sorted(_tags(scenario).items()):
        if tag not in counts:
            out.append({"aspect": "record count", "name": tag, "novel": [n],
                        "known": [], "rank": 1,
                        "note": "tag appears in no shipped scenario"})

    out.sort(key=lambda f: (f["rank"], f["aspect"], f["name"]))
    return out


def _pairs(directory: str, exclude: set) -> list:
    found = []
    for scn in sorted(glob.glob(os.path.join(directory, "*.scn"))):
        stem = os.path.splitext(scn)[0]
        if os.path.basename(stem).lower() in exclude:
            continue
        if os.path.exists(stem + ".map"):
            found.append((stem + ".map", scn))
    return found


def _short(values, limit=12) -> str:
    head = ", ".join(str(v) for v in values[:limit])
    return head + (f", ... ({len(values)} total)" if len(values) > limit else "")


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(
        description=__doc__.split("\n\n")[0],
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="Findings are leads, not verdicts. Exit status is 0 either way.")
    ap.add_argument("scenario", help="scenario to check, with or without .scn")
    ap.add_argument("--corpus", default=os.environ.get("IMP_SCENARIO_DIR"),
                    help="folder of shipped scenarios (default $IMP_SCENARIO_DIR)")
    ap.add_argument("--verbose", action="store_true",
                    help="also report novelty in index-like fields")
    args = ap.parse_args(argv)

    if not args.corpus:
        print("error: no corpus. Pass --corpus or set IMP_SCENARIO_DIR.",
              file=sys.stderr)
        return 2

    stem = os.path.splitext(args.scenario)[0]
    if not os.path.dirname(stem):
        stem = os.path.join(args.corpus, stem)
    candidate = (stem + ".map", stem + ".scn")
    for path in candidate:
        if not os.path.exists(path):
            print(f"error: {path} not found", file=sys.stderr)
            return 2

    name = os.path.basename(stem).lower()
    pairs = _pairs(args.corpus, NOT_ORIGINALS | {name})
    if not pairs:
        print(f"error: no shipped scenarios in {args.corpus}", file=sys.stderr)
        return 2

    envelope, counts = corpus_envelope(pairs)
    print(f"{os.path.basename(stem)} against {len(pairs)} shipped scenarios: "
          f"{', '.join(os.path.basename(s)[:-4] for _, s in pairs)}\n")

    results = findings(candidate, envelope, counts, verbose=args.verbose)
    if not results:
        print("inside the envelope on every aspect checked.")
        return 0

    current = None
    for f in results:
        if f["aspect"] != current:
            current = f["aspect"]
            print(f"-- {current} " + "-" * (60 - len(current)))
        print(f"  {f['name']}")
        if f["novel"] is not None:
            print(f"      has:    {_short(f['novel'])}")
        if f["known"]:
            print(f"      corpus: {_short(f['known'])}")
        if f["note"]:
            print(f"      ({f['note']})")
    # Plain ASCII: this prints to a cp1252 console, where a dash lands as "?".
    print(f"\n{len(results)} finding(s). Leads, not verdicts - a generated "
          f"world is entitled to be new.")
    if not args.verbose:
        print("Re-run with --verbose to include index-like fields.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
