"""A scenario as one editable document: the map plus its `.scn` and `.inf`.

The four files that make a scenario share a stem and depend on each other —
`deve`, `rail`, `port` and `civi` hold linear cell indices, `army` holds a
province id, and the map's nation byte has to match a `cnam` record. Editing
them as separate documents means those references can rot unnoticed, so this
holds them together behind **one undo stack**: Ctrl+Z walks back through your
edits in the order you made them, whichever file each one landed in.

`MapSession` is reused unchanged rather than absorbed. Its per-cell undo batches
are moved onto the shared stack as they are created, so cell edits keep their
cheap, precise representation while `.scn` and `.inf` steps — small files, rare
edits — snapshot whole.
"""
from __future__ import annotations

import os
from dataclasses import dataclass, field

from imperialism_format import ScenarioFile, ScenarioInfo
from imperialism_format.constants import (
    ARMY_UNIT_TYPE, CIVILIAN_UNIT_TYPE, COUNTRIES_1882, SHIP_TYPE,
)
from imperialism_format.scn_file import NAME_FIELD_SIZE, NAME_TAGS

import anchors
from session import MapSession

#: What may be edited in a `.scn`, keyed by tag then by the name the client
#: uses. The value says where it lives: the record's name, or a field index.
#: Only tags whose meaning is established appear here — see
#: `docs/scenario-semantics.md` for why `flag`, `tclr` and `coun` do not.
EDITABLE_RECORDS = {
    "cnam": {"name": ("name", None)},
    "pnam": {"name": ("name", None)},
    "zone": {"name": ("name", None)},
    "cash": {"amount": ("field", 1)},
    "year": {"turns": ("field", 0)},
}

#: `.inf` values the client may set.
EDITABLE_INFO = {"title", "overview", "country", "metadata"}

#: The scenario's calendar epoch. `year` counts turns from here: s0=5 -> 1820,
#: s3=33 -> 1848, s1=67 -> 1882, each matching the title in its own `.inf`.
BASE_YEAR = 1815

#: Tags whose first field is a cell index, and `civi` whose second is.
CELL_INDEX_FIELDS = {"deve": 0, "rail": 0, "port": 0, "civi": 1}

#: Army and ship rosters change with the era: 1820 fields Minutemen and
#: Ship-of-the-Line, 1882 fields Militia and Ironclads. Offering the whole
#: table would let you place units the scenario's own year has never heard of.
#: Bands are the type ids observed in the shipped scenarios of each era.
ARMY_ERAS = ((0, 7), (8, 15), (16, 26))
SHIP_ERAS = ((1, 4), (5, 9), (10, 13))


@dataclass
class Step:
    """One undoable action, tagged with which document it touched."""

    kind: str  # "map" | "scn" | "inf"
    before: object
    after: object
    label: str = ""


def companion(path: str, suffix: str) -> str | None:
    """`.../s1.map` -> `.../s1.scn` when it exists."""
    stem = os.path.splitext(path)[0]
    candidate = stem + suffix
    return candidate if os.path.exists(candidate) else None


@dataclass
class ScenarioSession:
    map_session: MapSession
    scenario: ScenarioFile = None
    scenario_path: str = None
    info: ScenarioInfo = None
    info_path: str = None

    scenario_baseline: bytes = None
    info_baseline: str = None

    undo_stack: list = field(default_factory=list)
    redo_stack: list = field(default_factory=list)

    #: Stable record ids, rebuilt whenever the record list is replaced.
    _uids: dict = field(default_factory=dict, repr=False)
    _next_uid: int = field(default=0, repr=False)

    def __post_init__(self):
        self._index_records()

    @classmethod
    def open(cls, map_path: str, wrap_x: bool = True, **kwargs) -> "ScenarioSession":
        """Open a map together with whichever companions are beside it.

        A map with no `.scn`/`.inf` still opens; the scenario side is simply
        absent, which the UI reflects rather than erroring on.
        """
        map_session = MapSession.open(map_path, wrap_x=wrap_x, **kwargs)
        scenario_path = companion(map_path, ".scn")
        info_path = companion(map_path, ".inf")
        scenario = ScenarioFile.load(scenario_path) if scenario_path else None
        info = ScenarioInfo.load(info_path) if info_path else None
        return cls(
            map_session=map_session,
            scenario=scenario, scenario_path=scenario_path,
            info=info, info_path=info_path,
            scenario_baseline=scenario.to_bytes() if scenario else None,
            info_baseline=info.to_text() if info else None,
        )

    # --- map edits --------------------------------------------------------

    def apply_map(self, edits: list[dict], label: str = "") -> list:
        """Apply cell edits, moving the resulting batch onto the shared stack."""
        changed = self.map_session.apply(edits, label)
        if self.map_session.undo_stack:
            batch = self.map_session.undo_stack.pop()
            self.undo_stack.append(Step("map", batch.before, batch.after, label))
            self.redo_stack.clear()
        return changed

    # --- scenario edits ---------------------------------------------------

    def find_record(self, tag: str, record_id: int = None):
        """Locate a record by tag and id.

        Addressed by id rather than position on purpose: `zone` records are not
        stored in id order and `pnam` ids are sparse (0-348 for 213 provinces),
        so an index would be a trap.
        """
        matches = [r for r in self.scenario.records if r.tag == tag]
        if not matches:
            raise ValueError(f"scenario has no {tag} records")
        if record_id is None:
            if len(matches) > 1:
                raise ValueError(f"{tag} needs an id: {len(matches)} records")
            return matches[0]
        for record in matches:
            if record.fields[0] == record_id:
                return record
        raise ValueError(f"no {tag} record with id {record_id}")

    def apply_scenario(self, edits: list[dict], label: str = "") -> list[dict]:
        """Set fields on `.scn` records. Returns the records that changed."""
        if self.scenario is None:
            raise ValueError("this scenario has no .scn file")

        before = self.scenario.to_bytes()
        touched = []
        for edit in edits:
            tag = edit["tag"]
            if tag not in EDITABLE_RECORDS:
                raise ValueError(f"{tag} records are not editable")
            key = edit["field"]
            if key not in EDITABLE_RECORDS[tag]:
                raise ValueError(f"{tag} has no editable field {key!r}")

            record = self.find_record(tag, edit.get("id"))
            kind, index = EDITABLE_RECORDS[tag][key]
            if kind == "name":
                record.name = _checked_name(edit["value"])
            else:
                record.fields[index] = _checked_field(edit["value"])
            touched.append(record)

        after = self.scenario.to_bytes()
        if after == before:
            return []
        self.undo_stack.append(Step("scn", before, after, label))
        self.redo_stack.clear()
        return [self.record_dict(r) for r in touched]

    # --- info edits -------------------------------------------------------

    def apply_info(self, edits: list[dict], label: str = "") -> bool:
        """Set `.inf` values. Returns whether anything actually changed."""
        if self.info is None:
            raise ValueError("this scenario has no .inf file")

        before = self.info.to_text()
        for edit in edits:
            field_name = edit["field"]
            if field_name not in EDITABLE_INFO:
                raise ValueError(f"{field_name!r} is not an editable .inf field")
            if field_name == "title":
                self.info.title = str(edit["value"]).strip()
            elif field_name == "overview":
                self.info.overview = str(edit["value"])
            elif field_name == "country":
                position = int(edit["id"])
                if not 0 <= position < len(self.info.country_sections):
                    raise ValueError(f"no country briefing {position}")
                self.info.country_sections[position] = str(edit["value"])
            else:
                self.info.metadata = [int(v) for v in edit["value"]]

        after = self.info.to_text()
        if after == before:
            return False
        self.undo_stack.append(Step("inf", before, after, label))
        self.redo_stack.clear()
        return True

    # --- placing records --------------------------------------------------

    def _usable(self, cell) -> bool:
        """Whether a cell can hold a placed record.

        Land with a province, and nothing narrower. It is tempting to add that
        a port must be coastal or a development must sit on workable terrain,
        but the shipped data says otherwise — 46 of 134 ports touch no ocean at
        all. This is the only condition all 10 scenarios actually meet.
        """
        return cell.terrain != 0 and cell.province != 65535

    def _cell_index(self, x: int, y: int) -> int:
        m = self.map_session.map_file
        if not (0 <= x < m.width and 0 <= y < m.height):
            raise ValueError(f"({x}, {y}) is off the map")
        return y * m.width + x

    def move_record(self, uid: int, x: int, y: int, label: str = "move") -> dict:
        """Point a cell-anchored record at another cell."""
        record = self.record_for(uid)
        field = CELL_INDEX_FIELDS.get(record.tag)
        if field is None:
            raise ValueError(f"a {record.tag} record is not placed on a cell")
        m = self.map_session.map_file
        if not self._usable(m.get(x, y)):
            raise ValueError(
                f"({x}, {y}) is not land with a province, so a "
                f"{record.tag} record cannot sit there")

        before = self.scenario.to_bytes()
        record.fields[field] = self._cell_index(x, y)
        self._commit_scenario(before, label)
        return {"uid": uid, **self.record_dict(record)}

    def delete_record(self, uid: int, label: str = "delete") -> None:
        record = self.record_for(uid)
        before = self.scenario.to_bytes()
        self.scenario.records.remove(record)
        self._commit_scenario(before, label)
        self._index_records()

    def add_record(self, tag: str, x: int, y: int, *, value: int = 0,
                   label: str = "add") -> dict:
        """Place a new cell-anchored record."""
        field = CELL_INDEX_FIELDS.get(tag)
        if field is None:
            raise ValueError(f"{tag} records are not placed on a cell")
        m = self.map_session.map_file
        if not self._usable(m.get(x, y)):
            raise ValueError(f"({x}, {y}) is not land with a province")

        index = self._cell_index(x, y)
        fields = {"civi": [value, index], "deve": [index, value or 1],
                  "port": [index], "rail": [index]}[tag]
        before = self.scenario.to_bytes()
        record = self.scenario.add(tag, *fields)
        self._commit_scenario(before, label)
        self._uids[self._next_uid] = record
        self._by_identity[id(record)] = self._next_uid
        self._next_uid += 1
        return {"uid": self._by_identity[id(record)], **self.record_dict(record)}

    def _commit_scenario(self, before: bytes, label: str) -> None:
        after = self.scenario.to_bytes()
        if after != before:
            self.undo_stack.append(Step("scn", before, after, label))
            self.redo_stack.clear()

    def would_strand(self, edits: list) -> list:
        """Records that the proposed cell edits would leave unusable.

        Checked *before* applying, so the map and the `.scn` are never
        simultaneously wrong — and so the carry destination is computed while
        the cell still knows which province it belonged to.
        """
        if self.scenario is None or not edits:
            return []
        m = self.map_session.map_file

        pending = {}
        for edit in edits:
            pending.setdefault((edit["x"], edit["y"]), {})[edit["field"]] = edit["value"]

        doomed = {}
        for (x, y), changes in pending.items():
            cell = m.get(x, y)
            terrain = changes.get("terrain", cell.terrain)
            province = changes.get("province", cell.province)
            if terrain == 0 or province == 65535:
                doomed[y * m.width + x] = (x, y)
        if not doomed:
            return []

        stranded = []
        for record in self.scenario.records:
            field = CELL_INDEX_FIELDS.get(record.tag)
            if field is None or record.fields[field] not in doomed:
                continue
            x, y = doomed[record.fields[field]]
            target = anchors.carry_target(m, x, y, wrap_x=self.map_session.wrap_x)
            stranded.append({
                "uid": self.uid_of(record),
                "tag": record.tag,
                "label": self._describe(record),
                "at": [x, y],
                "carryTo": list(target) if target else None,
            })
        return stranded

    def _describe(self, record) -> str:
        if record.tag == "civi":
            return CIVILIAN_UNIT_TYPE.get(record.fields[0], f"civilian {record.fields[0]}")
        return {"port": "Port", "rail": "Railway",
                "deve": "Development"}.get(record.tag, record.tag)

    # --- undo / redo ------------------------------------------------------

    def _restore(self, step: Step, snapshot) -> dict:
        if step.kind == "map":
            return {"cells": self.map_session._restore(snapshot)}
        if step.kind == "scn":
            # A restore replaces every Record object, so the ids have to be
            # reissued. They are assigned in file order, so a record that has
            # not moved keeps the id the client is already holding.
            self.scenario = ScenarioFile.from_bytes(snapshot)
            self._index_records()
            return {"scenario": True}
        self.info = ScenarioInfo.parse(snapshot)
        return {"info": True}

    def undo(self) -> dict:
        if not self.undo_stack:
            return {}
        step = self.undo_stack.pop()
        self.redo_stack.append(step)
        return self._restore(step, step.before)

    def redo(self) -> dict:
        if not self.redo_stack:
            return {}
        step = self.redo_stack.pop()
        self.undo_stack.append(step)
        return self._restore(step, step.after)

    # --- record identity --------------------------------------------------

    def _index_records(self) -> None:
        """Give every record a stable id.

        Not list position: deleting shifts everything after it, and two
        civilians of the same type are otherwise indistinguishable. The id
        lives here rather than on `Record` so the format library — the oracle —
        stays a plain description of the file.
        """
        self._uids = {}
        self._by_identity = {}
        self._next_uid = 0
        if self.scenario is None:
            return
        for record in self.scenario.records:
            self._uids[self._next_uid] = record
            self._by_identity[id(record)] = self._next_uid
            self._next_uid += 1

    def uid_of(self, record) -> int | None:
        # Keyed on object identity: a linear scan per record would make
        # listing 1,881 records quadratic.
        return self._by_identity.get(id(record))

    def record_for(self, uid: int):
        record = self._uids.get(int(uid))
        if record is None or record not in self.scenario.records:
            raise ValueError(f"no scenario record with id {uid}")
        return record

    # --- units ------------------------------------------------------------

    def _names(self, tag) -> dict:
        return {r.fields[0]: r.name
                for r in self.scenario.records if r.tag == tag}

    def _era_roster(self, table, bands) -> dict:
        """The slice of a unit table this scenario's year actually fields."""
        used = set()
        for record in self.scenario.records:
            if record.tag == "army" and table is ARMY_UNIT_TYPE:
                used.add(record.fields[1])
            elif record.tag == "ship" and table is SHIP_TYPE:
                used.add(record.fields[1])
        bands_used = [b for b in bands if any(b[0] <= t <= b[1] for t in used)]
        if not bands_used:
            bands_used = list(bands)
        return {t: name for t, name in table.items()
                if any(low <= t <= high for low, high in bands_used)}

    def units(self) -> dict:
        """Everything the scenario places, named and located where possible."""
        if self.scenario is None:
            return {"present": False}

        m = self.map_session.map_file
        width = m.width
        provinces = self._names("pnam")
        zones = self._names("zone")
        countries = self._names("cnam")
        province_at = anchors.province_anchors(m)

        def owner_of(cell_index):
            """A civilian belongs to whoever owns the ground it stands on."""
            x, y = cell_index % width, cell_index // width
            if not 0 <= cell_index < width * m.height:
                return None
            cell = m.get(x, y)
            if cell.terrain == 0:
                return None
            return cell.nation_zone_a

        out = {"present": True, "civilians": [], "armies": [], "ships": [],
               "infrastructure": []}

        for record in self.scenario.records:
            uid = self.uid_of(record)
            if record.tag == "civi":
                kind, cell_index = record.fields
                owner = owner_of(cell_index)
                out["civilians"].append({
                    "uid": uid, "tag": "civi", "cell": cell_index,
                    "type": kind, "typeName": CIVILIAN_UNIT_TYPE.get(kind, f"type {kind}"),
                    "owner": owner,
                    "ownerName": countries.get(owner) or COUNTRIES_1882.get(owner),
                    "stranded": owner is None,
                })
            elif record.tag == "army":
                province, kind, count = record.fields
                out["armies"].append({
                    "uid": uid, "tag": "army", "province": province,
                    "provinceName": provinces.get(province),
                    "type": kind, "typeName": ARMY_UNIT_TYPE.get(kind, f"type {kind}"),
                    "count": count, "cell": province_at.get(province),
                })
            elif record.tag == "ship":
                country, kind, zone, count = record.fields
                out["ships"].append({
                    "uid": uid, "tag": "ship", "country": country,
                    "countryName": countries.get(country) or COUNTRIES_1882.get(country),
                    "type": kind, "typeName": SHIP_TYPE.get(kind, f"type {kind}"),
                    "zone": zone, "zoneName": zones.get(zone),
                    # No cell: a ship names a `zone` record, and the map's
                    # ocean cells use an unrelated numbering. See anchors.py.
                    "count": count, "cell": None,
                })
            elif record.tag in ("port", "rail", "deve"):
                cell_index = record.fields[0]
                out["infrastructure"].append({
                    "uid": uid, "tag": record.tag, "cell": cell_index,
                    "level": record.fields[1] if record.tag == "deve" else None,
                    "owner": owner_of(cell_index),
                    "stranded": owner_of(cell_index) is None,
                })

        out["rosters"] = {
            "army": self._era_roster(ARMY_UNIT_TYPE, ARMY_ERAS),
            "ship": self._era_roster(SHIP_TYPE, SHIP_ERAS),
            "civilian": CIVILIAN_UNIT_TYPE,
        }
        return out

    def at_cell(self, x: int, y: int) -> list:
        """Every record anchored to this cell — what the inspector shows."""
        if self.scenario is None:
            return []
        index = y * self.map_session.map_file.width + x
        found = []
        for record in self.scenario.records:
            field = CELL_INDEX_FIELDS.get(record.tag)
            if field is not None and record.fields[field] == index:
                found.append({"uid": self.uid_of(record), **self.record_dict(record)})
        return found

    # --- inspection -------------------------------------------------------

    def record_dict(self, record) -> dict:
        out = {"tag": record.tag, "fields": list(record.fields)}
        if record.tag in NAME_TAGS:
            out["name"] = record.name
        return out

    def dirty(self) -> dict:
        return {
            "map": len(self.map_session.dirty_cells()),
            "scenario": bool(self.scenario is not None
                             and self.scenario.to_bytes() != self.scenario_baseline),
            "info": bool(self.info is not None
                         and self.info.to_text() != self.info_baseline),
        }

    def summary(self) -> dict:
        """The scenario side, shaped for the UI."""
        if self.scenario is None:
            return {"present": False}

        def named(tag):
            return sorted(
                ({"id": r.fields[0], "name": r.name}
                 for r in self.scenario.records if r.tag == tag),
                key=lambda e: e["id"])

        counts = {}
        for record in self.scenario.records:
            counts[record.tag] = counts.get(record.tag, 0) + 1

        year = next((r.fields[0] for r in self.scenario.records if r.tag == "year"), None)
        return {
            "present": True,
            "path": self.scenario_path,
            "countries": named("cnam"),
            "provinces": named("pnam"),
            "zones": named("zone"),
            "cash": [{"id": r.fields[0], "amount": r.fields[1]}
                     for r in self.scenario.records if r.tag == "cash"],
            "year": {"turns": year, "calendar": None if year is None else BASE_YEAR + year},
            "counts": counts,
        }

    def info_dict(self) -> dict:
        if self.info is None:
            return {"present": False}
        return {"present": True, "path": self.info_path, **self.info.to_dict()}

    def spatial_overlays(self) -> dict:
        """Cell indices the scenario points at, for drawing on the map."""
        if self.scenario is None:
            return {}
        out = {tag: [] for tag in CELL_INDEX_FIELDS}
        for record in self.scenario.records:
            index = CELL_INDEX_FIELDS.get(record.tag)
            if index is not None:
                out[record.tag].append(record.fields[index])
        return out

    # --- persistence ------------------------------------------------------

    def save_as(self, target: str) -> list[str]:
        """Write the whole scenario under a new stem and continue there.

        Unlike `save`, this writes **every** file the scenario has, changed or
        not: the point is to end up with a complete `<stem>.map/.scn/.inf` set
        that the game can load, and a half-cloned scenario would be worse than
        none. The session retargets, so later saves follow the new name.

        Whatever it overwrites is backed up once per file first, as elsewhere.
        """
        stem = os.path.splitext(target)[0]
        written = []

        for path, write in (
            (stem + ".map", lambda p: self.map_session.map_file.save(p)),
            (stem + ".scn", (lambda p: self.scenario.save(p)) if self.scenario else None),
            (stem + ".inf", (lambda p: self.info.save(p)) if self.info else None),
        ):
            if write is None:
                continue
            _backup(path)
            write(path)
            written.append(path)

        self.map_session.path = stem + ".map"
        self.map_session.baseline = [c.to_bytes() for c in self.map_session.map_file.cells]
        if self.scenario:
            self.scenario_path = stem + ".scn"
            self.scenario_baseline = self.scenario.to_bytes()
        if self.info:
            self.info_path = stem + ".inf"
            self.info_baseline = self.info.to_text()
        return written

    def save(self) -> list[str]:
        """Write only the files that changed. Returns what was written."""
        written = []
        state = self.dirty()
        if state["map"]:
            written.append(self.map_session.save())
        if state["scenario"]:
            _backup(self.scenario_path)
            self.scenario.save(self.scenario_path)
            self.scenario_baseline = self.scenario.to_bytes()
            written.append(self.scenario_path)
        if state["info"]:
            _backup(self.info_path)
            self.info.save(self.info_path)
            self.info_baseline = self.info.to_text()
            written.append(self.info_path)
        return written


def _backup(path: str) -> None:
    """One-shot backup, matching `MapSession.save`: never overwrite an existing
    `.bak`, so repeated saves cannot erase what you started from."""
    import shutil
    if path and os.path.exists(path) and not os.path.exists(path + ".bak"):
        shutil.copy2(path, path + ".bak")


def _checked_name(value) -> str:
    """Names live in a fixed 64-byte ASCII field; refuse rather than truncate."""
    name = str(value)
    encoded = name.encode("ascii", errors="replace")
    if len(encoded) > NAME_FIELD_SIZE:
        raise ValueError(
            f"name is {len(encoded)} bytes; the format allows {NAME_FIELD_SIZE}")
    return name


def _checked_field(value) -> int:
    number = int(value)
    if not 0 <= number <= 0xFFFFFFFF:
        raise ValueError(f"{number} does not fit in a 32-bit field")
    return number
