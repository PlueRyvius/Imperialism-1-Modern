"""Reader/writer for Imperialism's .map binary format.

Grid is MAP_WIDTH x MAP_HEIGHT hex cells, row-major (left to right, top to
bottom), each cell a fixed 36-byte record. After the cell grid, the file
carries DORMANT_RECORD_COUNT fixed-size trailer records whose exact
purpose isn't fully understood (likely stale scenario-adjacent data); we
preserve them byte-for-byte on round-trip without interpreting them.
"""
from __future__ import annotations

from dataclasses import dataclass, field

from .constants import (
    MAP_WIDTH, MAP_HEIGHT, MAP_CELL_COUNT, MAP_CELL_SIZE,
    DORMANT_RECORD_COUNT, DORMANT_RECORD_SIZE,
)


@dataclass
class HexCell:
    terrain_underlay: int = 0
    ocean_coastline: int = 0
    river: int = 0
    nation_zone_a: int = 0
    nation_zone_b: int = 0
    unused_05: int = 255
    rail: int = 0
    national_border: int = 0
    province_border: int = 0
    land_coastline: int = 0
    like_cell_adjacency: int = 0
    hill_mountain_overlay: int = 0
    unused_12: int = 0
    unused_13: int = 0
    unused_14: int = 243
    unused_15: int = 0
    unknown_16: int = 255
    resource_a: int = 255
    resource_b: int = 255
    terrain: int = 0
    province: int = 0  # combined from two bytes (big-endian, per source docs)
    unused_22: int = 255
    unused_23: int = 0
    unused_24: int = 255
    unused_25: int = 243
    unused_26: int = 255
    unused_27: int = 255
    unused_28: int = 0
    town_type: int = 0
    unused_30: int = 243
    unused_31: int = 243
    unused_32: int = 0
    unused_33: int = 0
    unused_34: int = 0
    unused_35: int = 0

    @classmethod
    def from_bytes(cls, raw: bytes) -> "HexCell":
        if len(raw) != MAP_CELL_SIZE:
            raise ValueError(f"expected {MAP_CELL_SIZE} bytes, got {len(raw)}")
        b = raw
        return cls(
            terrain_underlay=b[0], ocean_coastline=b[1], river=b[2],
            nation_zone_a=b[3], nation_zone_b=b[4], unused_05=b[5],
            rail=b[6], national_border=b[7], province_border=b[8],
            land_coastline=b[9], like_cell_adjacency=b[10],
            hill_mountain_overlay=b[11], unused_12=b[12], unused_13=b[13],
            unused_14=b[14], unused_15=b[15], unknown_16=b[16],
            resource_a=b[17], resource_b=b[18], terrain=b[19],
            province=(b[20] << 8) | b[21],
            unused_22=b[22], unused_23=b[23], unused_24=b[24],
            unused_25=b[25], unused_26=b[26], unused_27=b[27],
            unused_28=b[28], town_type=b[29], unused_30=b[30],
            unused_31=b[31], unused_32=b[32], unused_33=b[33],
            unused_34=b[34], unused_35=b[35],
        )

    def to_bytes(self) -> bytes:
        province_hi = (self.province >> 8) & 0xFF
        province_lo = self.province & 0xFF
        return bytes([
            self.terrain_underlay, self.ocean_coastline, self.river,
            self.nation_zone_a, self.nation_zone_b, self.unused_05,
            self.rail, self.national_border, self.province_border,
            self.land_coastline, self.like_cell_adjacency,
            self.hill_mountain_overlay, self.unused_12, self.unused_13,
            self.unused_14, self.unused_15, self.unknown_16,
            self.resource_a, self.resource_b, self.terrain,
            province_hi, province_lo,
            self.unused_22, self.unused_23, self.unused_24, self.unused_25,
            self.unused_26, self.unused_27, self.unused_28, self.town_type,
            self.unused_30, self.unused_31, self.unused_32, self.unused_33,
            self.unused_34, self.unused_35,
        ])

    def is_ocean(self) -> bool:
        return self.terrain == 0


@dataclass
class MapFile:
    cells: list = field(default_factory=list)  # row-major, length MAP_CELL_COUNT
    dormant_trailer: bytes = b""

    @classmethod
    def load(cls, path: str) -> "MapFile":
        with open(path, "rb") as f:
            data = f.read()
        cells = []
        offset = 0
        for _ in range(MAP_CELL_COUNT):
            cells.append(HexCell.from_bytes(data[offset:offset + MAP_CELL_SIZE]))
            offset += MAP_CELL_SIZE
        trailer = data[offset:offset + DORMANT_RECORD_COUNT * DORMANT_RECORD_SIZE]
        return cls(cells=cells, dormant_trailer=trailer)

    @classmethod
    def blank(cls) -> "MapFile":
        cells = [HexCell(terrain=0, terrain_underlay=5, province=65535) for _ in range(MAP_CELL_COUNT)]
        trailer = bytes(DORMANT_RECORD_COUNT * DORMANT_RECORD_SIZE)
        return cls(cells=cells, dormant_trailer=trailer)

    def save(self, path: str) -> None:
        if len(self.cells) != MAP_CELL_COUNT:
            raise ValueError(f"expected {MAP_CELL_COUNT} cells, have {len(self.cells)}")
        with open(path, "wb") as f:
            for cell in self.cells:
                f.write(cell.to_bytes())
            f.write(self.dormant_trailer)

    def index(self, x: int, y: int) -> int:
        if not (0 <= x < MAP_WIDTH and 0 <= y < MAP_HEIGHT):
            raise IndexError(f"({x}, {y}) out of bounds for {MAP_WIDTH}x{MAP_HEIGHT} grid")
        return y * MAP_WIDTH + x

    def get(self, x: int, y: int) -> HexCell:
        return self.cells[self.index(x, y)]

    def set(self, x: int, y: int, cell: HexCell) -> None:
        self.cells[self.index(x, y)] = cell
