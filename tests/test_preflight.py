"""The corpus-envelope differ.

The load-bearing assertion here is that it finds the two defects that actually
crashed the game on 2026-08-01, since that is the whole reason it exists.
"""
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "src"))
sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "tools"))

from imperialism_format import MapFile, ScenarioFile
from imperialism_format.scn_file import Record

import originals
import preflight


def corpus():
    """(pairs, envelope, counts) from the shipped scenarios, or None."""
    pairs = [p for p in originals.scenarios()]
    if len(pairs) < 8:
        return None
    envelope, counts = preflight.corpus_envelope(pairs)
    return pairs, envelope, counts


def test_the_corpus_constrains_a_works_owner_to_the_great_powers():
    """The projection that would have caught the `0051465C` crash."""
    built = corpus()
    if built is None:
        return
    _, envelope, _ = built
    for tag in ("deve", "rail", "port", "civi"):
        key = ("cell of", f"{tag} -> owner-nation")
        assert envelope[key] == set(range(7)), f"{tag}: {sorted(envelope[key])}"
        assert len(envelope[key]) <= preflight.CODE_VOCABULARY_MAX, (
            "must classify as a code, or novelty here is suppressed")


def test_the_corpus_treats_army_type_as_a_code_that_excludes_three():
    """The value sweep that would have caught the first defect."""
    built = corpus()
    if built is None:
        return
    _, envelope, _ = built
    known = envelope[("field", "army[1]")]
    assert 3 not in known, "type 3 is the value no shipped scenario carries"
    assert len(known) <= preflight.CODE_VOCABULARY_MAX


def test_cell_indices_are_classified_as_indices_not_codes():
    """Otherwise every generated world drowns in meaningless novelty."""
    built = corpus()
    if built is None:
        return
    _, envelope, _ = built
    for tag, field in (("deve", 0), ("rail", 0), ("port", 0)):
        known = envelope[("field", f"{tag}[{field}]")]
        assert len(known) > preflight.CODE_VOCABULARY_MAX, (
            f"{tag}[{field}] has {len(known)} values; would be treated as a code")


def test_a_work_moved_onto_a_minor_is_reported(tmp_path):
    """End to end: plant the real bug and confirm it surfaces, ranked first."""
    built = corpus()
    if built is None:
        return
    pairs, envelope, counts = built

    map_path, scn_path = pairs[0]
    map_file = MapFile.load(map_path)
    minor = next((i for i, c in enumerate(map_file.cells)
                  if not c.is_ocean() and c.nation_zone_a >= 7), None)
    assert minor is not None, "corpus map has no minor-owned land"

    scenario = ScenarioFile.load(scn_path)
    scenario.records.append(Record(tag="rail", fields=[minor]))
    planted_map = tmp_path / "p.map"
    planted_scn = tmp_path / "p.scn"
    map_file.save(str(planted_map))
    with open(planted_scn, "wb") as f:
        f.write(scenario.to_bytes())

    results = preflight.findings((str(planted_map), str(planted_scn)),
                                 envelope, counts)
    planted = [f for f in results if f["name"] == "rail -> owner-nation"]
    assert planted, [f["name"] for f in results]
    assert planted[0]["novel"] == [map_file.cells[minor].nation_zone_a]
    # A contradicted projection must outrank any merely-novel raw field.
    raw = [f["rank"] for f in results if f["aspect"] == "field"]
    assert planted[0]["rank"] <= min(raw, default=planted[0]["rank"])
    assert results[0]["aspect"] != "field"


def test_a_held_out_original_never_contradicts_the_great_power_rule():
    """Leave-one-out over the corpus.

    Silence is too strong a bar and asserting it would be a lie: with nine
    scenarios each is the sole source of some values, so holding `s1` out
    makes "a `deve` on nation 2" look novel. That is the tool working -- a
    small corpus really is that thin, which is why findings are leads.

    What must hold is the *rule*: no original ever puts a work on a cell owned
    by something other than a Great Power, whichever files set the bar.
    """
    pairs = [p for p in originals.scenarios()]
    if len(pairs) < 8:
        return
    for i in range(len(pairs)):
        held_out = pairs[i]
        envelope, counts = preflight.corpus_envelope(pairs[:i] + pairs[i + 1:])
        for finding in preflight.findings(held_out, envelope, counts):
            if finding["aspect"] == "cell of" and \
                    finding["name"].endswith("owner-nation"):
                assert max(finding["novel"]) < 7, (
                    f"{os.path.basename(held_out[1])}: {finding}")
