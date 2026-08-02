"""Generate a complete scenario from a keyword.

    python tools/generate_scenario.py --template E:/Imperialism/Scenario/s1.map \
                                      --out E:/Imperialism/Scenario/s5 --seed Pippin

Writes `<out>.map`, `<out>.scn` and `<out>.inf`. All three, always: a scenario
missing one of its files is not something the game can open.

`--template` is required and must be a real `.map`. The province table at the
end of the format is only partly decoded, so a generated map inherits a real
one's and rewrites just the field we understand — see `docs/file-formats.md`.
"""
from __future__ import annotations

import argparse
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "src"))
sys.path.insert(0, os.path.join(os.path.dirname(__file__), "map_editor"))

from imperialism_format import MapFile  # noqa: E402
from imperialism_format.generate import build  # noqa: E402

import validate  # noqa: E402


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--seed", required=True,
                    help="keyword the world is generated from; the same "
                         "keyword always gives the same world")
    ap.add_argument("--out", required=True, help="output stem, e.g. .../s5")
    ap.add_argument("--template", required=True,
                    help="a real .map to inherit the province table from")
    ap.add_argument("--year", type=int, default=1820,
                    help="scenario year; sets the era and unit rosters")
    ap.add_argument("--land", type=float, default=0.305,
                    help="share of the map that is land (default 0.305)")
    args = ap.parse_args(argv)

    template = MapFile.load(args.template)
    result = build.generate_world(args.seed, template=template,
                                  land_share=args.land,
                                  turns=max(0, args.year - 1815))

    issues = validate.check(result["map"], result["scenario"])
    if issues:
        print(f"refusing to write: {len(issues)} validation issues")
        for issue in issues[:5]:
            print(f"  ({issue['x']}, {issue['y']}) {issue['message']}")
        return 1

    written = build.save_world(result, args.out)
    print(f"{result['info'].title}")
    print(f"  powers: {', '.join(result['names'][:7])}")
    for path in written:
        print(f"  wrote {path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
