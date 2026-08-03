"""Locating unmodified game files for the tests that must stay silent on them.

Several rules in this project are held to "never fires on shipped data". That
bar only means something if the files really are shipped data, and a game
install you have been editing is not: `IMP_SCENARIO_DIR` points at a live
`Scenario` folder where maps get overwritten.

A `.bak` beside a file is not a safe substitute. It is written before the
*first* save, which makes it the original only if there has been exactly one
editing session — after a Save As onto the same stem, or a backup taken from an
already-edited file, it is just an older edit. Relying on it put edited data
into the corpus and quietly weakened three "never fires on shipped data" tests.

So the rule is stricter and does not require judging content: **a `.bak`
anywhere beside a scenario means that scenario has been edited, so the whole
scenario is excluded.** Nine untouched scenarios are a better corpus than ten
with one lie in it.

`s0` is excluded outright as well. It is the working scenario — edited, launched
in the game to see whether it still loads, reverted when it does not — so it is
never reference data, `.bak` or no `.bak`. **`s1` is the pristine reference**,
and is the copy kept in `fixtures/local_only`.

`s5` is excluded for a different reason: it is where generated worlds are
written for in-game testing, so it is this project's own output wearing an
original's filename. See `WORKING_SCENARIOS`.
"""
from __future__ import annotations

import glob
import os

import pytest

FIXTURE_DIR = os.path.join(os.path.dirname(__file__), "..", "fixtures", "local_only")

#: How many scenarios a full install yields once the exclusions are applied:
#: ten shipped, minus `s0` and `s5`, plus `s1` from the fixture directory when
#: it has not itself been edited.
CORPUS_SIZE = 9


def roots() -> list[str]:
    dirs = [FIXTURE_DIR]
    external = os.environ.get("IMP_SCENARIO_DIR")
    if external:
        dirs.append(external)
    return [d for d in dirs if os.path.isdir(d)]


#: Scenarios that are never reference data whatever their files look like.
#:
#: `s0` is the working scenario. `s5` is the slot Imperialism-1-Forge's
#: `generate_scenario.py` writes a generated world into for in-game testing —
#: that is *our* output, so
#: admitting it here would mean holding generated data to a bar set partly by
#: generated data. It was in the corpus for a while and did exactly that: the
#: "generated values stay inside what the originals use" check could not see a
#: novel value in `s5`, because `s5` was one of the originals it compared
#: against. Anything else this project writes into a live `Scenario` folder
#: belongs on this list the moment it is written.
WORKING_SCENARIOS = {"s0", "s5"}


def _edited(path: str) -> bool:
    """Whether this file, or any companion sharing its stem, has been edited.

    Checked across the whole scenario: editing the `.scn` says nothing about
    the `.map`, but it does mean the folder is a working area rather than a
    pristine install, and the map beside it is no more trustworthy.
    """
    stem = os.path.splitext(path)[0]
    name = os.path.basename(stem).lower()
    if name in WORKING_SCENARIOS:
        return True
    if os.path.basename(path).lower().startswith("old "):
        return True                       # a hand-made copy, not ours to judge
    return any(os.path.exists(stem + suffix + ".bak")
               for suffix in (".map", ".scn", ".inf", ""))


def maps() -> list[str]:
    """Original `.map` files, excluding any the editor has touched."""
    return sorted({p for root in roots()
                   for p in glob.glob(os.path.join(root, "*.map"))
                   if not _edited(p)})


def scenarios() -> list[tuple[str, str]]:
    """(map, scn) pairs where both exist and neither has been edited."""
    pairs = []
    for root in roots():
        for scn in glob.glob(os.path.join(root, "*.scn")):
            map_path = scn[:-4] + ".map"
            if os.path.exists(map_path) and not _edited(scn):
                pairs.append((map_path, scn))
    return sorted(set(pairs))


def infs() -> list[str]:
    return sorted({p for root in roots()
                   for p in glob.glob(os.path.join(root, "*.inf"))
                   if not _edited(p)})


def _require(found: list, what: str) -> list:
    """The corpus, or a visible skip — never an empty list.

    `for path in maps():` over an empty list passes green having checked
    nothing, and that is the state CI actually runs in: `IMP_SCENARIO_DIR` is
    unset there, and the one file in `fixtures/local_only` excludes itself the
    moment a `.bak` appears beside it. Eleven tests holding rules to
    "exact on every shipped map" were reporting success without opening a map.

    A skip says so out loud. Use `require_*` for anything whose whole claim is
    about real data; plain `maps()` is still right where an empty corpus is a
    legitimate answer.
    """
    # Setting the variable is a declaration that the full corpus is there, so a
    # short one is a broken setup rather than a reason to test less — checked
    # before the skip, so pointing it at the wrong folder fails instead of
    # quietly testing nothing. The fixture directory alone is not held to that:
    # it is documented as holding `s1` and nothing else.
    if os.environ.get("IMP_SCENARIO_DIR") and len(found) < CORPUS_SIZE:
        raise AssertionError(
            f"IMP_SCENARIO_DIR is set but only {len(found)} unedited {what} "
            f"were found, expected {CORPUS_SIZE}. A .bak beside a scenario "
            f"excludes the whole scenario. See this module's docstring."
        )

    if not found:
        pytest.skip(
            f"no unedited {what} available. Set IMP_SCENARIO_DIR to a game "
            f"install's Scenario folder, or put one in fixtures/local_only"
        )

    return found


def require_maps() -> list[str]:
    return _require(maps(), "*.map files")


def require_scenarios() -> list[tuple[str, str]]:
    return _require(scenarios(), "(map, scn) pairs")


def require_infs() -> list[str]:
    return _require(infs(), "*.inf files")
