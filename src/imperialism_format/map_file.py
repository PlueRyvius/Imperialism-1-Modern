"""Reader/writer for Imperialism's .map binary format.

Grid is MAP_WIDTH x MAP_HEIGHT hex cells, row-major (left to right, top to
bottom), each cell a fixed 36-byte record.

After the grid comes a **province table**: DORMANT_RECORD_COUNT records of
DORMANT_RECORD_SIZE bytes, indexed by province id, so 384 is the format's
province cap. Each record holds that province's **town cell index** as a
big-endian u16 at PROVINCE_TOWN_OFFSET, with NO_PROVINCE for unused slots.
Verified on all ten shipped maps: every province's town sits at its own slot,
`s1` filling 213 of the 384 and `s9` filling 120.

The rest of each record is still unread. Rebuilding a table from the town field
alone reproduces that field exactly but only about two thirds of the bytes —
offsets around 58-65, 130-135 and 158-190 carry more, and some of the tail
varies even in *unused* slots, which is the signature of uninitialised memory
written to disk.

So the block is still preserved byte-for-byte by default, exactly as name
padding and the bytes past `TERM` are elsewhere. `set_province_town` edits the
one field we understand and leaves the rest of the record alone, which is what
lets a generated map inherit a real one's table.
"""
from __future__ import annotations

from dataclasses import dataclass, field

from .constants import (
    MAP_WIDTH, MAP_HEIGHT, MAP_CELL_SIZE,
    DORMANT_RECORD_COUNT, DORMANT_RECORD_SIZE,
    NO_PROVINCE, PROVINCE_TOWN_OFFSET,
)


@dataclass(frozen=True)
class MapFormatProfile:
    """Physical layout of a legacy ``.map`` file.

    Imperialism's original files do not contain a header with their dimensions,
    so callers must supply a profile.  Keeping that fact at the import boundary
    lets the in-memory map model support maps of any width and height.
    """

    width: int
    height: int
    trailer_record_count: int = DORMANT_RECORD_COUNT
    trailer_record_size: int = DORMANT_RECORD_SIZE

    def __post_init__(self) -> None:
        if self.width <= 0 or self.height <= 0:
            raise ValueError("map width and height must be positive")
        if self.trailer_record_count < 0 or self.trailer_record_size < 0:
            raise ValueError("trailer dimensions cannot be negative")
        if self.trailer_record_count and not self.trailer_record_size:
            raise ValueError("non-empty trailer records must have a size")

    @property
    def cell_count(self) -> int:
        return self.width * self.height

    @property
    def trailer_size(self) -> int:
        return self.trailer_record_count * self.trailer_record_size

    @property
    def file_size(self) -> int:
        return self.cell_count * MAP_CELL_SIZE + self.trailer_size


LEGACY_MAP_PROFILE = MapFormatProfile(MAP_WIDTH, MAP_HEIGHT)


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
    profile: MapFormatProfile = LEGACY_MAP_PROFILE
    cells: list = field(default_factory=list)  # row-major
    dormant_trailer: bytes = b""

    @classmethod
    def load(
        cls, path: str, profile: MapFormatProfile = LEGACY_MAP_PROFILE
    ) -> "MapFile":
        with open(path, "rb") as f:
            data = f.read()
        return cls.from_bytes(data, profile=profile)

    @classmethod
    def from_bytes(
        cls, data: bytes, profile: MapFormatProfile = LEGACY_MAP_PROFILE
    ) -> "MapFile":
        if len(data) != profile.file_size:
            raise ValueError(
                f"expected {profile.file_size} bytes for a "
                f"{profile.width}x{profile.height} map, got {len(data)}"
            )
        cells = []
        offset = 0
        for _ in range(profile.cell_count):
            cells.append(HexCell.from_bytes(data[offset:offset + MAP_CELL_SIZE]))
            offset += MAP_CELL_SIZE
        trailer = data[offset:]
        return cls(profile=profile, cells=cells, dormant_trailer=trailer)

    @classmethod
    def blank(cls, profile: MapFormatProfile = LEGACY_MAP_PROFILE) -> "MapFile":
        """An all-ocean map with an empty province table.

        The table is zero-filled apart from the town field, which is set to
        NO_PROVINCE in every slot: zeroes there would claim that every province
        has its town at cell 0.
        """
        cells = [
            HexCell(terrain=0, terrain_underlay=5, province=NO_PROVINCE)
            for _ in range(profile.cell_count)
        ]
        table = bytearray(profile.trailer_size)
        for slot in range(profile.trailer_record_count):
            at = slot * profile.trailer_record_size + PROVINCE_TOWN_OFFSET
            if at + 1 < len(table):
                table[at] = NO_PROVINCE >> 8
                table[at + 1] = NO_PROVINCE & 0xFF
        return cls(profile=profile, cells=cells, dormant_trailer=bytes(table))

    @property
    def width(self) -> int:
        return self.profile.width

    @property
    def height(self) -> int:
        return self.profile.height

    def to_bytes(self) -> bytes:
        if len(self.cells) != self.profile.cell_count:
            raise ValueError(
                f"expected {self.profile.cell_count} cells, have {len(self.cells)}"
            )
        if len(self.dormant_trailer) != self.profile.trailer_size:
            raise ValueError(
                f"expected {self.profile.trailer_size} trailer bytes, "
                f"have {len(self.dormant_trailer)}"
            )
        return b"".join(cell.to_bytes() for cell in self.cells) + self.dormant_trailer

    def save(self, path: str) -> None:
        with open(path, "wb") as f:
            f.write(self.to_bytes())

    def index(self, x: int, y: int) -> int:
        if not (0 <= x < self.width and 0 <= y < self.height):
            raise IndexError(
                f"({x}, {y}) out of bounds for {self.width}x{self.height} grid"
            )
        return y * self.width + x

    def get(self, x: int, y: int) -> HexCell:
        return self.cells[self.index(x, y)]

    def set(self, x: int, y: int, cell: HexCell) -> None:
        self.cells[self.index(x, y)] = cell

    # --- the province table ------------------------------------------------

    def _province_slot(self, province: int) -> int:
        if not 0 <= province < self.profile.trailer_record_count:
            raise IndexError(
                f"province {province} is outside the table's "
                f"{self.profile.trailer_record_count} slots")
        return province * self.profile.trailer_record_size + PROVINCE_TOWN_OFFSET

    def province_town(self, province: int) -> int | None:
        """The cell index of a province's town, or None if the slot is unused."""
        at = self._province_slot(province)
        value = (self.dormant_trailer[at] << 8) | self.dormant_trailer[at + 1]
        return None if value == NO_PROVINCE else value

    def set_province_town(self, province: int, cell: int | None) -> None:
        """Point a province's slot at a town cell, or clear it.

        Writes only those two bytes. The rest of the record is undecoded, so
        editing a real map's table leaves whatever else it holds intact — and a
        generated map can inherit a table it does not fully understand.
        """
        if cell is None:
            cell = NO_PROVINCE
        elif not 0 <= cell <= 0xFFFF:
            raise ValueError(f"cell index {cell} does not fit in the field")
        at = self._province_slot(province)
        table = bytearray(self.dormant_trailer)
        table[at] = (cell >> 8) & 0xFF
        table[at + 1] = cell & 0xFF
        self.dormant_trailer = bytes(table)

    def province_towns(self) -> dict:
        """Every populated slot, as province id -> town cell index."""
        return {p: town
                for p in range(self.profile.trailer_record_count)
                if (town := self.province_town(p)) is not None}
