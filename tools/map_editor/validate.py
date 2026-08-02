"""Consistency checks for an edited map, and the repairs that follow from them.

Every rule here is silent on the unmodified original maps.  That is the bar: a
rule that fires on data the 1997 game shipped is a wrong rule, not a bad map,
and `tests/test_map_editor.py` enforces it.  When a check has to be relaxed to
stay quiet on real data, relax it — do not add an exception list.

An issue may carry a ``fix``: the cell edits that repair it.  A fix is only
attached where the format leaves exactly **one** correct answer — an ocean cell's
province is 65535 and nothing else, a grain farm carries grain and nothing else.
Where a repair would need a judgement call, there is no fix and the issue says
why.  "Land with no province" has 213 equally valid answers, and a civilian unit
stranded in the sea can be rescued by restoring the land or by moving the unit,
which are different maps.  Guessing there would quietly destroy intent, which is
worse than the error it replaced.
"""
from __future__ import annotations

from imperialism_format.constants import (
    DEVELOPED_TERRAIN_RESOURCE, GREAT_POWERS, OCEAN_RESOURCES, RESOURCE,
    TERRAIN_TYPE, TERRAIN_UNDERLAY, TOWN_TYPE, COUNTRIES_1882,
)

OCEAN_TERRAIN = 0
NO_PROVINCE = 65535

#: Tags whose fields hold a linear cell index (`y * width + x`), and which one.
CELL_INDEX_FIELDS = {"deve": 0, "rail": 0, "port": 0, "civi": 1}


def _issue(x, y, rule, message, severity="error", fix=None, why=None):
    """One finding.

    ``fix`` is the list of cell edits that repair it, or None when the format
    leaves more than one defensible answer. ``why`` explains that absence, so
    the UI can say what it needs from you instead of just refusing.
    """
    return {
        "x": x, "y": y, "rule": rule, "message": message, "severity": severity,
        "fix": fix, "why": why,
    }


def _set(x, y, **fields):
    """Cell edits in the shape `/api/edit` already accepts."""
    return [{"x": x, "y": y, "field": name, "value": value}
            for name, value in fields.items()]


def check_cross_file(map_file, scenario) -> list[dict]:
    """Checks that need the map and the `.scn` together.

    These are the reason a scenario is edited as one document. The `.scn` holds
    raw cell indices and province ids with nothing to keep them honest, so
    repainting the map can leave a port in open water or an army in a province
    that no longer exists.
    """
    issues = []
    width, height = map_file.width, map_file.height
    cell_count = width * height

    for record in scenario.records:
        index_field = CELL_INDEX_FIELDS.get(record.tag)
        if index_field is None:
            continue
        cell_index = record.fields[index_field]
        if not 0 <= cell_index < cell_count:
            issues.append(_issue(
                -1, -1, "scenario",
                f"{record.tag} points at cell {cell_index}, "
                f"outside a {width}x{height} map",
                why="Only you know where this belongs on the map."))
            continue
        x, y = cell_index % width, cell_index // width
        cell = map_file.get(x, y)
        if cell.terrain == OCEAN_TERRAIN:
            issues.append(_issue(
                x, y, "scenario",
                f"{record.tag} points at an ocean cell",
                why="Restoring the land and moving the record are different "
                    "maps, so this one is yours to decide."))
            continue
        if cell.nation_zone_a >= GREAT_POWERS:
            issues.append(_issue(
                x, y, "scenario",
                f"{record.tag} sits on a cell owned by minor nation "
                f"{cell.nation_zone_a}",
                why="Give the cell to a Great Power or move the record — "
                    "those are different maps, so the choice is yours."))
    return issues

# Deliberately absent: any rule requiring a `cnam`/`pnam`/`zone` record to exist
# for an id the map uses. Name records are optional labels, not a registry, and
# every such rule fires on shipped data:
#
#   army in an unnamed province     825 firings  (s9 names 1 province, arms 120)
#   land in an unnamed province     593
#   land owned by an unnamed country 103
#   sea in an unnamed zone           50          (s1 map uses ids to 78, names to 62)
#
# The cell-index checks above fire zero times across all ten scenarios, which is
# why they are the ones kept.
#
# The minor-owned-cell check is the third of them and the one with teeth: every
# `deve`/`rail`/`port`/`civi` record in all nine originals -- 700-odd -- sits on
# a Great Power's cell, never a minor's. The engine depends on it, resolving a
# work's cell to an owner and indexing its 7-slot power table with the result
# unguarded, so a minor's id walks off the end. A generated world that broke
# this crashed the real game at `0051465C` in `UMap.cpp`.


def check(map_file, scenario=None) -> list[dict]:
    """Return every rule violation found, in reading order.

    Pass ``scenario`` to add the checks that span the map and the `.scn`.
    """
    issues = []
    for y in range(map_file.height):
        for x in range(map_file.width):
            c = map_file.get(x, y)
            ocean = c.terrain == OCEAN_TERRAIN

            unknown = "Repaint the cell: we cannot guess what was intended."
            if c.terrain not in TERRAIN_TYPE:
                issues.append(_issue(x, y, "terrain", f"unknown terrain {c.terrain}",
                                     why=unknown))
            if c.terrain_underlay not in TERRAIN_UNDERLAY:
                issues.append(_issue(x, y, "underlay",
                                     f"unknown underlay {c.terrain_underlay}",
                                     why=unknown))
            for name in ("resource_a", "resource_b"):
                v = getattr(c, name)
                if v not in RESOURCE:
                    issues.append(_issue(x, y, "resource", f"unknown {name} {v}",
                                         fix=_set(x, y, **{name: 255}),
                                         why=None))
            if c.town_type not in TOWN_TYPE:
                issues.append(_issue(x, y, "town", f"unknown town type {c.town_type}",
                                     why=unknown))

            if ocean and c.province != NO_PROVINCE:
                issues.append(_issue(
                    x, y, "province", "ocean cell must use province 65535",
                    fix=_set(x, y, province=NO_PROVINCE)))
            if not ocean and c.province == NO_PROVINCE:
                issues.append(_issue(
                    x, y, "province", "land cell has no province",
                    why="There are 213 provinces and no way to tell which this "
                        "is. Use the Province tool."))
            if not ocean and c.nation_zone_a not in COUNTRIES_1882:
                issues.append(_issue(
                    x, y, "nation",
                    f"land cell owned by unknown country {c.nation_zone_a}",
                    severity="warning",
                    why="Ownership is yours to set. Use the Nation tool."))

            # Fish live at sea, so ocean cells legitimately carry a resource.
            # Only a *land* resource out there is wrong.
            if ocean and c.resource_a not in OCEAN_RESOURCES:
                issues.append(_issue(
                    x, y, "resource",
                    f"ocean cell carries {RESOURCE.get(c.resource_a, c.resource_a)}",
                    fix=_set(x, y, resource_a=255)))
            if ocean and c.resource_b not in OCEAN_RESOURCES:
                issues.append(_issue(
                    x, y, "resource",
                    f"ocean cell carries a second resource, "
                    f"{RESOURCE.get(c.resource_b, c.resource_b)}",
                    fix=_set(x, y, resource_b=255)))

            # The second slot stacks onto the first, so it cannot stand alone.
            # Nothing else about stacking is constrained: the shipped maps use
            # it on just two cells, which is far too little to draw rules from.
            if c.resource_b != 255 and c.resource_a == 255:
                # Clearing the orphan rather than promoting it to the primary
                # slot: a stacked deposit implies a base deposit was intended,
                # and inventing that base is a bigger claim than dropping it.
                issues.append(_issue(
                    x, y, "resource", "second resource set with no primary resource",
                    fix=_set(x, y, resource_b=255)))
            if ocean and c.rail:
                issues.append(_issue(x, y, "rail", "rail on an ocean cell",
                                     fix=_set(x, y, rail=0)))
            # No rule tying town terrain to the town_type marker: the shipped
            # maps have 110 town cells with no marker, including one on the
            # otherwise-clean s3. Whatever town_type means, it is not "this
            # cell is a town".

            # Developed land and the resource it exploits are one thing, not
            # two. Only this direction is checked: a resource sitting on
            # undeveloped ground is perfectly normal.
            expected = DEVELOPED_TERRAIN_RESOURCE.get(c.terrain)
            if expected is not None and c.resource_a != expected:
                # Setting the resource rather than clearing the terrain: the
                # pairing holds on 10,524 of 10,525 developed cells in the
                # shipped maps, so the terrain is the statement of intent.
                issues.append(_issue(
                    x, y, "resource",
                    f"{TERRAIN_TYPE[c.terrain]} must carry "
                    f"{RESOURCE[expected]}, found {RESOURCE.get(c.resource_a, c.resource_a)}",
                    fix=_set(x, y, resource_a=expected)))

    if scenario is not None:
        issues.extend(check_cross_file(map_file, scenario))
    return issues


def fixable(issues) -> list[dict]:
    """The subset that carries a repair."""
    return [i for i in issues if i.get("fix")]


def fix_edits(issues) -> list[dict]:
    """Flatten selected issues into one batch of cell edits.

    Later edits win where two issues touch the same field of the same cell,
    which is what you want: the batch is applied as a single step and then
    re-validated, so anything the merge got wrong simply reappears.
    """
    merged = {}
    for issue in issues:
        for edit in issue.get("fix") or ():
            merged[(edit["x"], edit["y"], edit["field"])] = edit
    return list(merged.values())
