"""Editing state for one open document: mutation, undo/redo, and diffing.

Deliberately not map-specific in shape.  A `.scn` editor will want the same
"apply a batch, remember how to reverse it" machinery, so the undo stack stores
opaque before/after snapshots rather than anything that understands cells.
"""
from __future__ import annotations

import os
import shutil
from dataclasses import dataclass, field

from imperialism_format import derive
from imperialism_format.map_file import LEGACY_MAP_PROFILE, MapFile, MapFormatProfile

#: Cell fields the client is allowed to set directly.  Everything else is
#: either derived (see `derive.DERIVERS`) or undecoded and left alone.
EDITABLE_FIELDS = frozenset({
    "terrain", "terrain_underlay", "resource_a", "resource_b",
    "province", "nation_zone_a", "nation_zone_b", "town_type",
    "river", "rail", "hill_mountain_overlay", "ocean_coastline",
})

#: Fields sent to the client for rendering.  Kept explicit so adding a byte to
#: the wire format is a deliberate act.
WIRE_FIELDS = (
    "terrain", "terrain_underlay", "resource_a", "resource_b",
    "province", "nation_zone_a", "town_type", "river", "rail",
    "national_border", "province_border", "land_coastline",
    "like_cell_adjacency", "hill_mountain_overlay",
)


@dataclass
class Batch:
    """One undoable step: the prior and resulting bytes of every cell touched."""

    before: dict
    after: dict
    label: str = ""


@dataclass
class MapSession:
    map_file: MapFile
    path: str
    baseline: list = field(default_factory=list)  # cell bytes as loaded
    undo_stack: list = field(default_factory=list)
    redo_stack: list = field(default_factory=list)
    wrap_x: bool = True

    @classmethod
    def open(cls, path: str, wrap_x: bool = True,
             profile: MapFormatProfile = LEGACY_MAP_PROFILE) -> "MapSession":
        """Open a map for editing.

        ``profile`` supplies the dimensions the file itself does not carry.
        It defaults to the 1997 layout because that is what you edit, but the
        session never assumes it.
        """
        m = MapFile.load(path, profile)
        return cls(
            map_file=m,
            path=path,
            baseline=[c.to_bytes() for c in m.cells],
            wrap_x=wrap_x,
        )

    @property
    def geometry(self):
        return derive.geometry_for(self.map_file, wrap_x=self.wrap_x)

    # --- mutation ---------------------------------------------------------

    def apply(self, edits: list[dict], label: str = "") -> list[tuple[int, int]]:
        """Apply direct edits, then recompute derived bytes around them.

        Returns every cell whose bytes ended up different, so the client can
        repaint exactly those — including neighbours it never touched.
        """
        for e in edits:
            if e["field"] not in EDITABLE_FIELDS:
                raise ValueError(f"field {e['field']!r} is not editable")

        touched = {(e["x"], e["y"]) for e in edits}
        watched = {p for x, y in touched
                   for p in derive.affected_by(self.map_file, x, y, self.geometry)}
        before = {p: self.map_file.get(*p).to_bytes() for p in watched}

        for e in edits:
            setattr(self.map_file.get(e["x"], e["y"]), e["field"], int(e["value"]))
        derive.apply_edits(self.map_file, sorted(touched), geom=self.geometry)

        changed = [p for p in sorted(watched)
                   if self.map_file.get(*p).to_bytes() != before[p]]
        if not changed:
            return []

        self.undo_stack.append(Batch(
            before={p: before[p] for p in changed},
            after={p: self.map_file.get(*p).to_bytes() for p in changed},
            label=label,
        ))
        self.redo_stack.clear()
        return changed

    def _restore(self, snapshot: dict) -> list[tuple[int, int]]:
        from imperialism_format.map_file import HexCell
        for (x, y), raw in snapshot.items():
            self.map_file.set(x, y, HexCell.from_bytes(raw))
        return sorted(snapshot)

    def undo(self) -> list[tuple[int, int]]:
        if not self.undo_stack:
            return []
        batch = self.undo_stack.pop()
        self.redo_stack.append(batch)
        return self._restore(batch.before)

    def redo(self) -> list[tuple[int, int]]:
        if not self.redo_stack:
            return []
        batch = self.redo_stack.pop()
        self.undo_stack.append(batch)
        return self._restore(batch.after)

    # --- inspection -------------------------------------------------------

    def dirty_cells(self) -> list[tuple[int, int]]:
        """Cells differing from the file as it was loaded."""
        out = []
        for i, cell in enumerate(self.map_file.cells):
            if cell.to_bytes() != self.baseline[i]:
                out.append((i % self.map_file.width, i // self.map_file.width))
        return out

    def cell_dict(self, x: int, y: int) -> dict:
        cell = self.map_file.get(x, y)
        raw = cell.to_bytes()
        return {
            "x": x, "y": y,
            "bytes": list(raw),
            "dirty": raw != self.baseline[self.map_file.index(x, y)],
            **{f: getattr(cell, f) for f in WIRE_FIELDS},
        }

    # --- persistence ------------------------------------------------------

    def save(self, path: str | None = None, backup: bool = True) -> str:
        """Write the map, keeping a one-shot backup of what we overwrote.

        The backup is written only when it does not already exist, so repeated
        saves during a session cannot erase the original you started from.

        Saving to a new path retargets the session at it, as an editor's "save
        as" normally does — subsequent saves go to the new file, leaving the
        one you opened untouched from that point on.
        """
        target = path or self.path
        if backup and os.path.exists(target):
            bak = target + ".bak"
            if not os.path.exists(bak):
                shutil.copy2(target, bak)
        self.map_file.save(target)
        self.baseline = [c.to_bytes() for c in self.map_file.cells]
        self.path = target
        return target
