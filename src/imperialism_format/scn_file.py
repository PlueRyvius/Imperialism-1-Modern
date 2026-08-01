"""Reader/writer for Imperialism's .scn scenario format.

The file is a sequence of variable-length tagged records: a 4-byte ASCII
tag, followed by N big-endian 4-byte integer fields, followed (for the
three "name" tags) by a fixed 64-byte null-padded name string. The file
ends with a bare "TERM" tag (no fields).
"""
from __future__ import annotations

from dataclasses import dataclass, field

NAME_FIELD_SIZE = 64

# tag -> number of 4-byte big-endian integer fields that follow
TAG_FIELD_COUNTS = {
    "cnam": 1, "pnam": 1, "zone": 1, "tech": 2, "year": 1, "tyer": 2,
    "cash": 2, "tran": 2, "capa": 3, "army": 3, "ware": 3, "emba": 3,
    "rela": 3, "trea": 3, "port": 1, "rail": 1, "deve": 2, "civi": 2,
    "labo": 4, "tclr": 1, "tbar": 3, "ship": 4, "coun": 2, "flag": 1,
}

# Tags that carry a trailing 64-byte name string after their int fields.
NAME_TAGS = {"cnam", "pnam", "zone"}


@dataclass
class Record:
    tag: str
    fields: list  # list[int]
    name: str = None  # only set for cnam/pnam/zone

    def field_count(self) -> int:
        return TAG_FIELD_COUNTS.get(self.tag, 0)


@dataclass
class ScenarioFile:
    records: list = field(default_factory=list)  # list[Record], excludes trailing TERM

    @classmethod
    def load(cls, path: str) -> "ScenarioFile":
        with open(path, "rb") as f:
            data = f.read()
        records = []
        offset = 0
        while offset < len(data):
            tag = data[offset:offset + 4].decode("ascii")
            offset += 4
            if tag == "TERM":
                break
            count = TAG_FIELD_COUNTS.get(tag)
            if count is None:
                raise ValueError(f"unknown tag {tag!r} at offset {offset - 4}")
            fields = []
            for _ in range(count):
                fields.append(int.from_bytes(data[offset:offset + 4], "big"))
                offset += 4
            name = None
            if tag in NAME_TAGS:
                raw_name = data[offset:offset + NAME_FIELD_SIZE]
                offset += NAME_FIELD_SIZE
                name = raw_name.split(b"\x00", 1)[0].decode("ascii", errors="replace")
            records.append(Record(tag=tag, fields=fields, name=name))
        return cls(records=records)

    def save(self, path: str) -> None:
        with open(path, "wb") as f:
            for rec in self.records:
                expected = TAG_FIELD_COUNTS.get(rec.tag)
                if expected is None:
                    raise ValueError(f"unknown tag {rec.tag!r}")
                if len(rec.fields) != expected:
                    raise ValueError(
                        f"tag {rec.tag!r} expects {expected} fields, got {len(rec.fields)}"
                    )
                f.write(rec.tag.encode("ascii"))
                for value in rec.fields:
                    f.write(int(value).to_bytes(4, "big"))
                if rec.tag in NAME_TAGS:
                    name_bytes = (rec.name or "").encode("ascii", errors="replace")
                    padded = name_bytes[:NAME_FIELD_SIZE].ljust(NAME_FIELD_SIZE, b"\x00")
                    f.write(padded)
            f.write(b"TERM")

    def add(self, tag: str, *fields: int, name: str = None) -> Record:
        rec = Record(tag=tag, fields=list(fields), name=name)
        expected = TAG_FIELD_COUNTS.get(tag)
        if expected is None:
            raise ValueError(f"unknown tag {tag!r}")
        if len(rec.fields) != expected:
            raise ValueError(f"tag {tag!r} expects {expected} fields, got {len(rec.fields)}")
        if tag in NAME_TAGS and name is None:
            raise ValueError(f"tag {tag!r} requires a name")
        self.records.append(rec)
        return rec

    def find(self, tag: str) -> list:
        return [r for r in self.records if r.tag == tag]
